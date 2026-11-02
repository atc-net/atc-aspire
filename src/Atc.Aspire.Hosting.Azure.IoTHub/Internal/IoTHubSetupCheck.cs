namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Decides whether an IoT Hub is set up for the bridge, and whether setup may change it.
/// </summary>
internal static class IoTHubSetupCheck
{
    /// <summary>
    /// The name routes use for the built-in endpoint.
    /// </summary>
    public const string BuiltInEndpointName = "events";

    private const string DefaultConsumerGroup = "$Default";
    private const string DeviceMessagesSource = "DeviceMessages";

    /// <summary>
    /// Checks the settings of a hub against what the bridge forwards.
    /// </summary>
    /// <param name="settings">The hub's settings.</param>
    /// <param name="consumerGroup">The bridge's consumer group.</param>
    /// <param name="sources">The kinds of events the bridge forwards.</param>
    /// <returns>What is missing, and why setup must not change the hub, if it must not.</returns>
    public static IoTHubSetupResult Check(
        IoTHubSettings settings,
        string consumerGroup,
        IEnumerable<IoTHubEventSource> sources)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sources);

        var missing = new List<IoTHubSetupItem>();

        if (!settings.ConsumerGroups.Contains(consumerGroup, StringComparer.OrdinalIgnoreCase))
        {
            missing.Add(IoTHubSetupItem.ConsumerGroup);
        }

        // Only what the bridge forwards is needed, e.g. no lifecycle route when it only forwards twin changes.
        missing.AddRange(sources
            .Distinct()
            .Order()
            .Where(source => !HasRoute(settings, source))
            .Select(GetRouteItem));

        return new IoTHubSetupResult(missing, GetBlockedReason(settings, consumerGroup));
    }

    /// <summary>
    /// Describes a missing part for the dashboard and the log.
    /// </summary>
    /// <param name="item">The missing part.</param>
    /// <param name="consumerGroup">The bridge's consumer group.</param>
    /// <returns>The description.</returns>
    public static string Describe(
        IoTHubSetupItem item,
        string consumerGroup)
        => item switch
        {
            IoTHubSetupItem.ConsumerGroup => $"the consumer group '{consumerGroup}' on the built-in endpoint",
            IoTHubSetupItem.DeviceLifecycleEventsRoute => "a route for device lifecycle events to the built-in endpoint",
            IoTHubSetupItem.TwinChangeEventsRoute => "a route for twin change events to the built-in endpoint",
            IoTHubSetupItem.TelemetryRoute => "the fallback route, or a route for device messages, to the built-in endpoint",
            _ => throw new ArgumentOutOfRangeException(nameof(item), item, message: null),
        };

    private static bool HasRoute(
        IoTHubSettings settings,
        IoTHubEventSource source)
        => source switch
        {
            IoTHubEventSource.DeviceLifecycleEvents => settings.Routes.Any(x => SendsToBuiltInEndpoint(x, "DeviceLifecycleEvents")),
            IoTHubEventSource.TwinChangeEvents => settings.Routes.Any(x => SendsToBuiltInEndpoint(x, "TwinChangeEvents")),

            // Device messages that match no route take the fallback route.
            IoTHubEventSource.Telemetry => (settings.FallbackRoute is { } fallback && SendsToBuiltInEndpoint(fallback, DeviceMessagesSource)) ||
                                           settings.Routes.Any(x => SendsToBuiltInEndpoint(x, DeviceMessagesSource)),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, message: null),
        };

    private static bool SendsToBuiltInEndpoint(
        IoTHubRoute route,
        string source)
        => route.IsEnabled &&
           string.Equals(route.Source, source, StringComparison.OrdinalIgnoreCase) &&
           route.EndpointNames.Contains(BuiltInEndpointName, StringComparer.OrdinalIgnoreCase);

    private static IoTHubSetupItem GetRouteItem(IoTHubEventSource source)
        => source switch
        {
            IoTHubEventSource.DeviceLifecycleEvents => IoTHubSetupItem.DeviceLifecycleEventsRoute,
            IoTHubEventSource.TwinChangeEvents => IoTHubSetupItem.TwinChangeEventsRoute,
            IoTHubEventSource.Telemetry => IoTHubSetupItem.TelemetryRoute,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, message: null),
        };

    // A hub in use, e.g. a shared DEV hub, must not be changed: its routes may be managed elsewhere, and others read it.
    private static string? GetBlockedReason(
        IoTHubSettings settings,
        string consumerGroup)
    {
        if (settings.CustomEndpointNames.Count > 0)
        {
            return $"it has custom routing endpoints ({string.Join(", ", settings.CustomEndpointNames)})";
        }

        var otherConsumerGroups = settings.ConsumerGroups
            .Where(x => !string.Equals(x, DefaultConsumerGroup, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(x, consumerGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return otherConsumerGroups.Count > 0
            ? $"other consumer groups read its built-in endpoint ({string.Join(", ", otherConsumerGroups)})"
            : null;
    }
}