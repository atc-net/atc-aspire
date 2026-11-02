namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

internal static class LoggingEventIdConstants
{
    public const int EventForwarded = 300;
    public const int EventForwardFailed = 301;
    public const int BridgeStarting = 302;
    public const int BridgeRunning = 303;
    public const int BridgeStartFailed = 304;
    public const int BridgeFailed = 305;
    public const int BridgePaused = 306;
    public const int BridgeResumed = 307;
    public const int CountsNotPublished = 308;
    public const int SetupVerified = 309;
    public const int SetupNotVerified = 310;
    public const int SetupMissing = 311;
    public const int SetupApplied = 312;
    public const int SetupFailed = 313;
}