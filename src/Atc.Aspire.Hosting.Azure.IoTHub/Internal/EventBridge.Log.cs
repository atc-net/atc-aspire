namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

internal sealed partial class EventBridge
{
    [LoggerMessage(
        EventId = LoggingEventIdConstants.EventForwarded,
        Level = LogLevel.Information,
        Message = "Forwarded {EventType} ({Source}) of '{Origin}' to Event Hub '{EventHubName}'.")]
    private partial void LogForwarded(
        string eventType,
        IoTHubEventSource source,
        string origin,
        string eventHubName);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.EventForwardFailed,
        Level = LogLevel.Warning,
        Message = "Could not forward {EventType} ({Source}) of '{Origin}' to Event Hub '{EventHubName}': {Error}")]
    private partial void LogForwardFailed(
        Exception exception,
        string eventType,
        IoTHubEventSource source,
        string origin,
        string eventHubName,
        string error);
}