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
        EventId = LoggingEventIdConstants.SetupVerified,
        Level = LogLevel.Information,
        Message = "IoT Hub '{IotHubHostName}' is set up for the bridge.")]
    private partial void LogSetupVerified(string iotHubHostName);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.SetupNotVerified,
        Level = LogLevel.Information,
        Message = "Could not check the setup of IoT Hub '{IotHubHostName}': {Reason} Forwarding anyway; if events are missing, check the hub's routes and consumer group.")]
    private partial void LogSetupNotVerified(
        string iotHubHostName,
        string reason);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.SetupMissing,
        Level = LogLevel.Warning,
        Message = "IoT Hub '{IotHubHostName}' isn't set up for the bridge. Missing: {Missing}. {NextStep}")]
    private partial void LogSetupMissing(
        string iotHubHostName,
        string missing,
        string nextStep);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.SetupApplied,
        Level = LogLevel.Information,
        Message = "Set up IoT Hub '{IotHubHostName}': added {Added}.")]
    private partial void LogSetupApplied(
        string iotHubHostName,
        string added);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.SetupFailed,
        Level = LogLevel.Warning,
        Message = "IoT Hub setup didn't complete: {Error}")]
    private partial void LogSetupFailed(string error);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.CountsNotPublished,
        Level = LogLevel.Debug,
        Message = "Could not show the event counts on the dashboard: {Error}")]
    private partial void LogCountsNotPublished(
        Exception exception,
        string error);
}