namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

internal sealed partial class EventBridgeController
{
    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgeStarting,
        Level = LogLevel.Information,
        Message = "Connecting to IoT Hub '{IotHubHostName}' over {Transport} with consumer group '{ConsumerGroup}'.")]
    private partial void LogStarting(
        string iotHubHostName,
        string transport,
        string consumerGroup);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgeRunning,
        Level = LogLevel.Information,
        Message = "Forwarding the events of IoT Hub '{IotHubHostName}' since {StartTime} for {Devices}.")]
    private partial void LogRunning(
        string iotHubHostName,
        string startTime,
        string devices);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgeStartFailed,
        Level = LogLevel.Error,
        Message = "The IoT Hub event bridge could not start: {Error}")]
    private partial void LogStartFailed(
        Exception exception,
        string error);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgeFailed,
        Level = LogLevel.Error,
        Message = "The IoT Hub event bridge stopped reading: {Error} Resume it from the dashboard to continue after the last event read.")]
    private partial void LogFailed(
        Exception exception,
        string error);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgePaused,
        Level = LogLevel.Information,
        Message = "Paused. Resume continues after the last event read.")]
    private partial void LogPaused();

    [LoggerMessage(
        EventId = LoggingEventIdConstants.BridgeResumed,
        Level = LogLevel.Information,
        Message = "Resumed after the last event read.")]
    private partial void LogResumed();

    [LoggerMessage(
        EventId = LoggingEventIdConstants.CountsNotPublished,
        Level = LogLevel.Debug,
        Message = "Could not show the event counts on the dashboard: {Error}")]
    private partial void LogCountsNotPublished(
        Exception exception,
        string error);
}