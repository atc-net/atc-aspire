namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// IoTEdgeDeviceProvisioner LoggerMessages.
/// </summary>
internal sealed partial class IoTEdgeDeviceProvisioner
{
    private readonly ILogger logger;

    [LoggerMessage(
        EventId = LoggingEventIdConstants.DeviceCreated,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Created IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}'.")]
    private partial void LogDeviceCreated(
        string iotHubHostName,
        string deviceId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ModuleCreated,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Created module identity '{ModuleId}' on IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}'.")]
    private partial void LogModuleCreated(
        string iotHubHostName,
        string deviceId,
        string moduleId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ModuleKeysAssigned,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Assigned symmetric keys to module identity '{ModuleId}' on IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}'.")]
    private partial void LogModuleKeysAssigned(
        string iotHubHostName,
        string deviceId,
        string moduleId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ModuleDesiredPropertiesApplied,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Applied desired properties from the deployment manifest to module '{ModuleId}' on IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}'.")]
    private partial void LogModuleDesiredPropertiesApplied(
        string iotHubHostName,
        string deviceId,
        string moduleId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.ModuleDesiredPropertiesNotApplied,
        Level = LogLevel.Warning,
        Message = "{CallerMethodName}({CallerLineNumber}) - Could not apply desired properties from the deployment manifest to module '{ModuleId}' on IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}': {Error}")]
    private partial void LogModuleDesiredPropertiesNotApplied(
        Exception exception,
        string iotHubHostName,
        string deviceId,
        string moduleId,
        string error,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.EdgeHubConfigurationApplied,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Applied the edgeHub configuration (routes, store and forward) to IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}' as a single-device deployment.")]
    private partial void LogEdgeHubConfigurationApplied(
        string iotHubHostName,
        string deviceId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.DeviceProvisioned,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Provisioned IoT Edge device '{DeviceId}' with {ModuleCount} module identities in IoT Hub '{IotHubHostName}'.")]
    private partial void LogDeviceProvisioned(
        string iotHubHostName,
        string deviceId,
        int moduleCount,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.DeviceDeprovisioned,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - Deleted IoT Edge device '{DeviceId}' from IoT Hub '{IotHubHostName}'.")]
    private partial void LogDeviceDeprovisioned(
        string iotHubHostName,
        string deviceId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    [LoggerMessage(
        EventId = LoggingEventIdConstants.DeviceAlreadyGone,
        Level = LogLevel.Information,
        Message = "{CallerMethodName}({CallerLineNumber}) - IoT Edge device '{DeviceId}' was already deleted from IoT Hub '{IotHubHostName}'.")]
    private partial void LogDeviceAlreadyGone(
        string iotHubHostName,
        string deviceId,
        [CallerMemberName] string callerMethodName = "",
        [CallerLineNumber] int callerLineNumber = 0);
}