namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// The certificate files needed to run edgeHub outside the IoT Edge runtime.
/// </summary>
/// <param name="DirectoryPath">The directory holding the files.</param>
/// <param name="DeviceCaCertificateFile">The root CA (PEM) that modules trust.</param>
/// <param name="ChainCertificateFile">The CA chain (PEM) edgeHub presents together with its server certificate.</param>
/// <param name="ServerCertificateFile">The edgeHub server certificate including its private key (PFX without password).</param>
public sealed record EdgeHubCertificateFiles(
    string DirectoryPath,
    string DeviceCaCertificateFile,
    string ChainCertificateFile,
    string ServerCertificateFile)
{
    /// <summary>
    /// The file name of the root CA (PEM) that modules trust.
    /// </summary>
    public const string DeviceCaCertificateFileName = "edge-device-ca.cert.pem";

    /// <summary>
    /// The file name of the CA chain (PEM) edgeHub presents together with its server certificate.
    /// </summary>
    public const string ChainCertificateFileName = "edge-chain-ca.cert.pem";

    /// <summary>
    /// The file name of the edgeHub server certificate including its private key (PFX without password).
    /// </summary>
    public const string ServerCertificateFileName = "edge-hub-server.cert.pfx";

    /// <summary>
    /// Gets the certificate file paths in a directory. The files are not created or checked.
    /// </summary>
    /// <param name="directoryPath">The directory holding the files.</param>
    /// <returns>The certificate file paths.</returns>
    public static EdgeHubCertificateFiles ForDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);

        return new EdgeHubCertificateFiles(
            directoryPath,
            Path.Combine(directoryPath, DeviceCaCertificateFileName),
            Path.Combine(directoryPath, ChainCertificateFileName),
            Path.Combine(directoryPath, ServerCertificateFileName));
    }
}