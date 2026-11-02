namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// How the bridge reads IoT Hub's built-in endpoint.
/// </summary>
/// <param name="ConsumerGroup">The consumer group of the built-in endpoint to read with.</param>
/// <param name="UseWebSockets">Whether to connect over AMQP over WebSockets (port 443) instead of AMQP (port 5671).</param>
internal sealed record IoTHubReaderOptions(
    string ConsumerGroup,
    bool UseWebSockets)
{
    /// <summary>
    /// Gets how the bridge connects to IoT Hub, as shown on the dashboard and in error messages.
    /// </summary>
    public string TransportDescription
        => UseWebSockets
            ? "AMQP over WebSockets (port 443)"
            : "AMQP (port 5671)";
}