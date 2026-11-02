namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// The protocol edgeHub uses to connect to IoT Hub.
/// </summary>
public enum EdgeHubUpstreamProtocol
{
    /// <summary>
    /// AMQP over TCP port 5671 (the edgeHub default).
    /// </summary>
    Amqp,

    /// <summary>
    /// AMQP over WebSockets on port 443. Use this behind firewalls that only allow HTTPS.
    /// </summary>
    AmqpWs,

    /// <summary>
    /// MQTT over TCP port 8883.
    /// </summary>
    Mqtt,

    /// <summary>
    /// MQTT over WebSockets on port 443.
    /// </summary>
    MqttWs,
}