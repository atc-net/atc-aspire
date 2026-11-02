namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class EdgeHubHealthCheckTests : IDisposable
{
    private readonly DirectoryInfo certificateDirectory = Directory.CreateTempSubdirectory("atc-iotedge-healthcheck-tests-");
    private readonly EdgeHubCertificateFiles certificates;

    public EdgeHubHealthCheckTests()
        => certificates = EdgeHubCertificateGenerator.GenerateCertificates(
            Path.Combine(certificateDirectory.FullName, "device"),
            ["localhost"]);

    public void Dispose()
        => certificateDirectory.Delete(recursive: true);

    [Fact]
    public async Task CheckHealthAsync_NothingListening_ReturnsUnhealthy()
    {
        // Arrange
        var healthCheck = new EdgeHubHealthCheck(GetFreePort(), certificates);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ServerAcceptsTcpWithoutTls_ReturnsUnhealthy()
    {
        // Arrange
        // Like a published container port before edgeHub listens: the connection is accepted, then closed.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var server = AcceptAndCloseAsync(listener, TestContext.Current.CancellationToken);
        var healthCheck = new EdgeHubHealthCheck(((IPEndPoint)listener.LocalEndpoint).Port, certificates);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        await server;

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ServerCompletesTlsHandshake_ReturnsHealthy()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var server = AcceptTlsAsync(listener, certificates, TestContext.Current.CancellationToken);
        var healthCheck = new EdgeHubHealthCheck(((IPEndPoint)listener.LocalEndpoint).Port, certificates);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        await server;

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ServerCertificateFromOtherDeviceCa_ReturnsUnhealthy()
    {
        // Arrange
        var otherCertificates = EdgeHubCertificateGenerator.GenerateCertificates(
            Path.Combine(certificateDirectory.FullName, "other-device"),
            ["localhost"]);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var server = AcceptTlsAsync(listener, otherCertificates, TestContext.Current.CancellationToken);
        var healthCheck = new EdgeHubHealthCheck(((IPEndPoint)listener.LocalEndpoint).Port, certificates);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // The server's side may or may not fail, depending on when the client rejects the certificate.
        await Task.WhenAny(server);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    private static async Task AcceptAndCloseAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
    }

    // Like edgeHub: presents the server certificate together with the intermediate CA from the chain file.
    private static async Task AcceptTlsAsync(
        TcpListener listener,
        EdgeHubCertificateFiles serverCertificates,
        CancellationToken cancellationToken)
    {
        using var serverCertificate = X509CertificateLoader.LoadPkcs12FromFile(serverCertificates.ServerCertificateFile, password: null);
        var chain = new X509Certificate2Collection();
        chain.ImportFromPemFile(serverCertificates.ChainCertificateFile);

        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var sslStream = new SslStream(client.GetStream());
        await sslStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions
            {
                ServerCertificateContext = SslStreamCertificateContext.Create(serverCertificate, chain, offline: true),
            },
            cancellationToken);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}