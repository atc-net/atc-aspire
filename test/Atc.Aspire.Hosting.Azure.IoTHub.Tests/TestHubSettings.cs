namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

/// <summary>
/// IoT Hub settings for tests, as Azure Resource Manager reports them.
/// </summary>
internal static class TestHubSettings
{
    /// <summary>
    /// A hub set up for the bridge: lifecycle and twin change routes to the built-in endpoint, the fallback route on,
    /// and the bridge's consumer group.
    /// </summary>
    /// <param name="consumerGroup">The bridge's consumer group.</param>
    /// <returns>The settings.</returns>
    public static IoTHubSettings SetUp(
        string consumerGroup = IoTHubEventBridgeResource.DefaultConsumerGroup)
        => new(
            [
                Route("lifecycle-to-events", "DeviceLifecycleEvents"),
                Route("twinchanges-to-events", "TwinChangeEvents"),
            ],
            Route("$fallback", "DeviceMessages"),
            CustomEndpointNames: [],
            ConsumerGroups: ["$Default", consumerGroup]);

    public static IoTHubRoute Route(
        string name,
        string source,
        string endpointName = "events",
        bool isEnabled = true)
        => new(name, source, [endpointName], isEnabled);
}