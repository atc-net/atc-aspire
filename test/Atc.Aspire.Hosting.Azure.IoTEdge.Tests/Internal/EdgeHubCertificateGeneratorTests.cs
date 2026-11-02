namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class EdgeHubCertificateGeneratorTests : IDisposable
{
    private readonly string directoryPath = Path.Combine(Path.GetTempPath(), $"atc-iotedge-certs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void GenerateCertificates_ServerCertificate_ChainsToDeviceCa()
    {
        // Act
        var files = EdgeHubCertificateGenerator.GenerateCertificates(directoryPath, ["edgehub", "localhost"]);

        // Assert
        using var server = X509CertificateLoader.LoadPkcs12FromFile(files.ServerCertificateFile, password: null);
        using var deviceCa = X509CertificateLoader.LoadCertificateFromFile(files.DeviceCaCertificateFile);
        var chainCertificates = new X509Certificate2Collection();
        chainCertificates.ImportFromPemFile(files.ChainCertificateFile);

        server.HasPrivateKey.Should().BeTrue();
        server.MatchesHostname("edgehub").Should().BeTrue();
        server.MatchesHostname("localhost").Should().BeTrue();
        server.MatchesHostname("other").Should().BeFalse();

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(deviceCa);
        chain.ChainPolicy.ExtraStore.AddRange(chainCertificates);

        chain.Build(server).Should().BeTrue(string.Join(", ", chain.ChainStatus.Select(x => x.StatusInformation)));
        chain.ChainElements.Should().HaveCount(3);

        foreach (var certificate in chainCertificates)
        {
            certificate.Dispose();
        }
    }

    [Fact]
    public void EnsureCertificates_SameHostNames_ReusesCertificates()
    {
        // Arrange
        var files = EdgeHubCertificateGenerator.EnsureCertificates(directoryPath, ["edgehub", "localhost"]);
        var thumbprint = GetServerThumbprint(files);

        // Act
        files = EdgeHubCertificateGenerator.EnsureCertificates(directoryPath, ["localhost"]);

        // Assert
        GetServerThumbprint(files).Should().Be(thumbprint);
    }

    [Fact]
    public void EnsureCertificates_NewHostName_RegeneratesCertificates()
    {
        // Arrange
        var files = EdgeHubCertificateGenerator.EnsureCertificates(directoryPath, ["edgehub"]);
        var thumbprint = GetServerThumbprint(files);

        // Act
        files = EdgeHubCertificateGenerator.EnsureCertificates(directoryPath, ["edgehub", "localhost"]);

        // Assert
        GetServerThumbprint(files).Should().NotBe(thumbprint);
    }

    private static string GetServerThumbprint(EdgeHubCertificateFiles files)
    {
        using var server = X509CertificateLoader.LoadPkcs12FromFile(files.ServerCertificateFile, password: null);
        return server.Thumbprint;
    }
}