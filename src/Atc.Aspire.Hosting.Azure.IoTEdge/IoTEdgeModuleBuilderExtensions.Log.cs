// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

public static partial class IoTEdgeModuleBuilderExtensions
{
    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Information,
        Message = "Module '{ModuleId}' is connected to {Target}.")]
    private static partial void LogModuleConnected(
        this ILogger logger,
        string moduleId,
        string target);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Warning,
        Message = "Module '{ModuleId}' has not connected to edgeHub within {Seconds} seconds. A module only connects when it uses the IoT device SDK (ModuleClient.CreateFromEnvironmentAsync) and doesn't run in Standalone mode. Use the 'Check connection' command to check again.")]
    private static partial void LogModuleNotConnectedToEdgeHub(
        this ILogger logger,
        string moduleId,
        int seconds);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Information,
        Message = "IoT Hub hasn't reported module '{ModuleId}' as connected within {Seconds} seconds. IoT Hub can take a few minutes to update it; use the 'Check connection' command to check again.")]
    private static partial void LogModuleConnectionNotReportedYet(
        this ILogger logger,
        string moduleId,
        int seconds);

    [LoggerMessage(
        EventId = 303,
        Level = LogLevel.Warning,
        Message = "Could not check whether module '{ModuleId}' is connected: {Error}")]
    private static partial void LogConnectionCheckFailed(
        this ILogger logger,
        Exception exception,
        string moduleId,
        string error);
}