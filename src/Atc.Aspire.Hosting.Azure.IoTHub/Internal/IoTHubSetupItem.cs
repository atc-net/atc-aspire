namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// A part of an IoT Hub's setup that the bridge needs.
/// </summary>
internal enum IoTHubSetupItem
{
    /// <summary>
    /// The bridge's consumer group on the built-in endpoint.
    /// </summary>
    ConsumerGroup,

    /// <summary>
    /// An enabled route that sends device lifecycle events to the built-in endpoint.
    /// </summary>
    DeviceLifecycleEventsRoute,

    /// <summary>
    /// An enabled route that sends twin change events to the built-in endpoint.
    /// </summary>
    TwinChangeEventsRoute,

    /// <summary>
    /// The enabled fallback route, or an enabled route that sends device messages to the built-in endpoint.
    /// </summary>
    TelemetryRoute,
}