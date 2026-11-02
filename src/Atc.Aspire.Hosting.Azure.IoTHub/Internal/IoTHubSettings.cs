namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The settings of an IoT Hub that decide whether the bridge gets its events, as Azure Resource Manager reports them.
/// </summary>
/// <param name="Routes">The message routes.</param>
/// <param name="FallbackRoute">The fallback route, or <see langword="null"/> when the hub has none.</param>
/// <param name="CustomEndpointNames">The names of the custom routing endpoints (Event Hubs, Service Bus, storage, Cosmos DB).</param>
/// <param name="ConsumerGroups">The consumer groups of the built-in endpoint.</param>
internal sealed record IoTHubSettings(
    IReadOnlyList<IoTHubRoute> Routes,
    IoTHubRoute? FallbackRoute,
    IReadOnlyList<string> CustomEndpointNames,
    IReadOnlyList<string> ConsumerGroups);