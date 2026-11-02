// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

public static partial class IoTEdgeDeviceBuilderExtensions
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Loaded deployment manifest '{Path}' with {RouteCount} routes and desired properties for {ModuleCount} modules.")]
    private static partial void LogDeploymentManifestLoaded(
        this ILogger logger,
        string path,
        int routeCount,
        int moduleCount);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Information,
        Message = "Provisioning IoT Edge device '{DeviceId}' in IoT Hub '{IotHubHostName}'.")]
    private static partial void LogProvisioningDevice(
        this ILogger logger,
        string deviceId,
        string iotHubHostName);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Information,
        Message = "IoT Edge device '{DeviceId}' is ready with modules {Modules}. Modules connect: {Mode}.")]
    private static partial void LogDeviceReady(
        this ILogger logger,
        string deviceId,
        string modules,
        string mode);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Error,
        Message = "Provisioning IoT Edge device '{DeviceId}' failed: {ErrorMessage}")]
    private static partial void LogProvisioningFailed(
        this ILogger logger,
        Exception exception,
        string deviceId,
        string errorMessage);

    [LoggerMessage(
        EventId = 204,
        Level = LogLevel.Warning,
        Message = "Module '{ModuleId}' runs image '{ContainerImage}', but the deployment manifest declares '{ManifestImage}' for it. Check that the right container is attached to the module.")]
    private static partial void LogModuleImageDiffersFromManifest(
        this ILogger logger,
        string moduleId,
        string containerImage,
        string manifestImage);

    [LoggerMessage(
        EventId = 205,
        Level = LogLevel.Warning,
        Message = "Module '{ModuleId}' ({ProjectPath}) doesn't reference the IoT device SDK (Microsoft.Azure.Devices.Client), so it can't connect as an IoT Edge module.")]
    private static partial void LogModuleWithoutDeviceSdk(
        this ILogger logger,
        string moduleId,
        string projectPath);

    [LoggerMessage(
        EventId = 206,
        Level = LogLevel.Warning,
        Message = "Deleted IoT Edge device '{DeviceId}' and its module identities from IoT Hub. {StoppedResources} Restart the AppHost to provision it again.")]
    private static partial void LogDeviceDeleted(
        this ILogger logger,
        string deviceId,
        string stoppedResources);
}