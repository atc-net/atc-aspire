namespace Atc.Aspire.Hosting.Azure.IoTHub;

/// <summary>
/// The kinds of IoT Hub events the bridge forwards: the <c>iothub-message-source</c> of an event on the built-in endpoint.
/// </summary>
public enum IoTHubEventSource
{
    /// <summary>
    /// A device or module identity was created or deleted (<c>deviceLifecycleEvents</c>).
    /// </summary>
    DeviceLifecycleEvents,

    /// <summary>
    /// The tags or desired or reported properties of a device or module twin changed (<c>twinChangeEvents</c>).
    /// </summary>
    TwinChangeEvents,

    /// <summary>
    /// A device-to-cloud message from a device or module (<c>Telemetry</c>).
    /// </summary>
    Telemetry,
}