namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Generates the test-only certificate chain edgeHub needs when it runs as a plain container:
/// a device root CA, an intermediate CA and an edgeHub server certificate for the given host names.
/// </summary>
/// <remarks>
/// The layout mirrors what Azure/iotedgehubdev generates, so the files can be passed to edgeHub through
/// <see cref="EdgeHubEnvironmentVariableNames.EdgeModuleHubServerCertificateFile"/> and
/// <see cref="EdgeHubEnvironmentVariableNames.EdgeModuleHubServerCaChainCertificateFile"/>,
/// and to modules through <see cref="EdgeHubEnvironmentVariableNames.EdgeModuleCaCertificateFile"/>.
/// The certificates are for local development only.
/// </remarks>
internal static class EdgeHubCertificateGenerator
{
    private const int KeySize = 2048;
    private const string DeviceCaSubject = "CN=Atc IoT Edge Device CA (test only)";
    private const string IntermediateCaSubject = "CN=Atc IoT Edge Hub CA (test only)";

    private static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(365);
    private static readonly TimeSpan RenewalThreshold = TimeSpan.FromDays(7);

    /// <summary>
    /// Ensures a valid certificate set exists in <paramref name="directoryPath"/> for the given host names.
    /// Existing certificates are reused while they are valid and cover all host names; otherwise a new set is generated.
    /// </summary>
    /// <param name="directoryPath">The directory to write the certificates to.</param>
    /// <param name="hostNames">The DNS names modules use to reach edgeHub, e.g. localhost and the container name.</param>
    /// <param name="validity">How long new certificates are valid. Defaults to 365 days.</param>
    /// <returns>The certificate file paths.</returns>
    public static EdgeHubCertificateFiles EnsureCertificates(
        string directoryPath,
        IReadOnlyCollection<string> hostNames,
        TimeSpan? validity = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);
        ArgumentNullException.ThrowIfNull(hostNames);

        if (hostNames.Count == 0)
        {
            throw new ArgumentException("At least one host name is required.", nameof(hostNames));
        }

        var files = EdgeHubCertificateFiles.ForDirectory(directoryPath);
        if (AreCertificatesUsable(files, hostNames))
        {
            return files;
        }

        Generate(files, hostNames, validity ?? DefaultValidity);
        return files;
    }

    /// <summary>
    /// Generates a new certificate set in <paramref name="directoryPath"/>, overwriting existing files.
    /// </summary>
    /// <param name="directoryPath">The directory to write the certificates to.</param>
    /// <param name="hostNames">The DNS names modules use to reach edgeHub.</param>
    /// <param name="validity">How long the certificates are valid. Defaults to 365 days.</param>
    /// <returns>The certificate file paths.</returns>
    public static EdgeHubCertificateFiles GenerateCertificates(
        string directoryPath,
        IReadOnlyCollection<string> hostNames,
        TimeSpan? validity = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);
        ArgumentNullException.ThrowIfNull(hostNames);

        var files = EdgeHubCertificateFiles.ForDirectory(directoryPath);
        Generate(files, hostNames, validity ?? DefaultValidity);
        return files;
    }

    private static bool AreCertificatesUsable(
        EdgeHubCertificateFiles files,
        IReadOnlyCollection<string> hostNames)
    {
        if (!File.Exists(files.DeviceCaCertificateFile) ||
            !File.Exists(files.ChainCertificateFile) ||
            !File.Exists(files.ServerCertificateFile))
        {
            return false;
        }

        try
        {
            using var serverCertificate = X509CertificateLoader.LoadPkcs12FromFile(files.ServerCertificateFile, password: null);
            if (serverCertificate.NotAfter.ToUniversalTime() - DateTime.UtcNow < RenewalThreshold)
            {
                return false;
            }

            var dnsNames = serverCertificate.Extensions
                .OfType<X509SubjectAlternativeNameExtension>()
                .SelectMany(x => x.EnumerateDnsNames())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return hostNames.All(dnsNames.Contains);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static void Generate(
        EdgeHubCertificateFiles files,
        IReadOnlyCollection<string> hostNames,
        TimeSpan validity)
    {
        Directory.CreateDirectory(files.DirectoryPath);

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = notBefore.Add(validity);

        using var deviceCaKey = RSA.Create(KeySize);
        using var deviceCa = CreateDeviceCa(deviceCaKey, notBefore, notAfter);

        using var intermediateCaKey = RSA.Create(KeySize);
        using var intermediateCaPublic = CreateIntermediateCa(intermediateCaKey, deviceCa, notBefore, notAfter);
        using var intermediateCa = intermediateCaPublic.CopyWithPrivateKey(intermediateCaKey);

        using var serverKey = RSA.Create(KeySize);
        using var serverPublic = CreateServerCertificate(serverKey, intermediateCa, hostNames, notBefore, notAfter);
        using var server = serverPublic.CopyWithPrivateKey(serverKey);

        WriteFile(files.DeviceCaCertificateFile, deviceCa.ExportCertificatePem() + "\n");
        WriteFile(files.ChainCertificateFile, intermediateCa.ExportCertificatePem() + "\n" + deviceCa.ExportCertificatePem() + "\n");
        WriteFile(files.ServerCertificateFile, server.Export(X509ContentType.Pkcs12));
    }

    private static X509Certificate2 CreateDeviceCa(
        RSA key,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(DeviceCaSubject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateIntermediateCa(
        RSA key,
        X509Certificate2 issuer,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(IntermediateCaSubject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        return request.Create(issuer, notBefore, notAfter, CreateSerialNumber());
    }

    private static X509Certificate2 CreateServerCertificate(
        RSA key,
        X509Certificate2 issuer,
        IReadOnlyCollection<string> hostNames,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        var request = new CertificateRequest($"CN={hostNames.First()}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication")], critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        foreach (var hostName in hostNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            subjectAlternativeNames.AddDnsName(hostName);
        }

        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        return request.Create(issuer, notBefore, notAfter, CreateSerialNumber());
    }

    private static byte[] CreateSerialNumber()
    {
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7F;
        return serialNumber;
    }

    private static void WriteFile(
        string path,
        string content)
        => WriteFile(path, System.Text.Encoding.ASCII.GetBytes(content));

    private static void WriteFile(
        string path,
        byte[] content)
    {
        File.WriteAllBytes(path, content);

        // The edgeHub container runs as a non-root user, so the files must be readable for everyone.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
    }
}