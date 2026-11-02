namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Reports edgeHub as healthy once its MQTT head completes a TLS handshake with a certificate from the device CA.
/// A plain TCP connect is not enough, because the container runtime accepts connections on published ports
/// before edgeHub itself listens.
/// </summary>
/// <param name="mqttPort">The host port edgeHub's MQTT head is published on.</param>
/// <param name="certificates">The certificates generated for edgeHub, whose device CA issued its server certificate.</param>
internal sealed class EdgeHubHealthCheck(
    int mqttPort,
    EdgeHubCertificateFiles certificates) : IHealthCheck
{
    /// <summary>
    /// Connects to the edgeHub MQTT port on the host and completes a TLS handshake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain TCP connect would report edgeHub as healthy too early: the container runtime accepts connections on a
    /// published port as soon as the container starts, before edgeHub listens inside it. Modules started at that moment
    /// would fail their first connection.
    /// </para>
    /// <para>
    /// A TLS handshake only completes when edgeHub itself answers. The server certificate is validated as the modules
    /// validate it: it must chain to the generated device CA and be issued for <c>localhost</c>.
    /// </para>
    /// <para>
    /// The check runs in the AppHost process, so it connects to <c>localhost</c> on the port published on the host.
    /// </para>
    /// </remarks>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Healthy when the TLS handshake completes; otherwise unhealthy.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var deviceCa = X509Certificate2.CreateFromPem(
                await File.ReadAllTextAsync(certificates.DeviceCaCertificateFile, cancellationToken));
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("localhost", mqttPort, cancellationToken);

            // edgeHub sends the intermediate CA from its chain file, so trusting the device CA is enough.
            var chainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
            };

            chainPolicy.CustomTrustStore.Add(deviceCa);

            await using var sslStream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    CertificateChainPolicy = chainPolicy,
                },
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is SocketException or IOException or System.Security.Authentication.AuthenticationException)
        {
            return HealthCheckResult.Unhealthy($"edgeHub has not completed a trusted TLS handshake on MQTT port {mqttPort} yet.", ex);
        }
    }
}