namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

/// <summary>
/// Builds events shaped like the ones IoT Hub delivers on its built-in endpoint, as captured from a real hub.
/// </summary>
internal static class TestEvents
{
    public static EventData Lifecycle(
        string deviceId,
        long sequenceNumber,
        string operationType = "createDeviceIdentity")
        => Create("deviceLifecycleEvents", deviceId, moduleId: null, operationType, sequenceNumber);

    public static EventData TwinChange(
        string deviceId,
        string moduleId,
        long sequenceNumber)
        => Create("twinChangeEvents", deviceId, moduleId, "updateTwin", sequenceNumber);

    public static EventData Telemetry(
        string deviceId,
        string moduleId,
        long sequenceNumber)
        => Create("Telemetry", deviceId, moduleId, operationType: null, sequenceNumber, """{"temperature":21.5}""");

    public static EventData Create(
        string? messageSource,
        string? deviceId,
        string? moduleId,
        string? operationType,
        long sequenceNumber,
        string body = """{"version":2}""")
    {
        var eventData = new EventData(Encoding.UTF8.GetBytes(body))
        {
            ContentType = "application/json",
        };

        var message = eventData.GetRawAmqpMessage();
        message.Properties.ContentEncoding = "utf-8";
        message.MessageAnnotations["x-opt-sequence-number"] = sequenceNumber;
        message.MessageAnnotations["x-opt-offset"] = (sequenceNumber * 100).ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (messageSource is not null)
        {
            message.MessageAnnotations["iothub-message-source"] = messageSource;
        }

        if (deviceId is not null)
        {
            message.MessageAnnotations["iothub-connection-device-id"] = deviceId;
        }

        if (moduleId is not null)
        {
            message.MessageAnnotations["iothub-connection-module-id"] = moduleId;
        }

        // Lifecycle and twin change events also carry their device, module and operation as application properties.
        if (operationType is not null)
        {
            eventData.Properties["hubName"] = "myhub";
            eventData.Properties["deviceId"] = deviceId;
            eventData.Properties["opType"] = operationType;

            if (moduleId is not null)
            {
                eventData.Properties["moduleId"] = moduleId;
            }
        }

        return eventData;
    }
}