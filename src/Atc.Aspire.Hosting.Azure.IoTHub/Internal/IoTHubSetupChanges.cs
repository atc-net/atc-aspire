namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Adds the routes the bridge needs to an IoT Hub as Azure Resource Manager reports it, before it is written back.
/// </summary>
internal static class IoTHubSetupChanges
{
    public const string DeviceLifecycleEventsRouteName = "aspire-bridge-device-lifecycle-events";
    public const string TwinChangeEventsRouteName = "aspire-bridge-twin-change-events";

    private const string FallbackRouteName = "$fallback";

    /// <summary>
    /// Adds the missing routes, and switches the fallback route on for telemetry. The consumer group isn't part of the hub's settings.
    /// </summary>
    /// <param name="hub">The hub as read from Azure Resource Manager; changed in place.</param>
    /// <param name="missing">What the hub misses.</param>
    /// <returns><see langword="true"/> when the hub's settings changed and must be written back.</returns>
    public static bool AddRoutes(
        IotHubDescriptionData hub,
        IEnumerable<IoTHubSetupItem> missing)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(missing);

        var routing = (hub.Properties ??= new IotHubProperties()).Routing ??= new IotHubRoutingProperties();
        var changed = false;

        foreach (var item in missing.Distinct())
        {
            switch (item)
            {
                case IoTHubSetupItem.DeviceLifecycleEventsRoute:
                    SetRoute(routing, DeviceLifecycleEventsRouteName, IotHubRoutingSource.DeviceLifecycleEvents);
                    changed = true;
                    break;
                case IoTHubSetupItem.TwinChangeEventsRoute:
                    SetRoute(routing, TwinChangeEventsRouteName, IotHubRoutingSource.TwinChangeEvents);
                    changed = true;
                    break;
                case IoTHubSetupItem.TelemetryRoute:
                    SetFallbackRoute(routing);
                    changed = true;
                    break;
            }
        }

        return changed;
    }

    // A route of the bridge's name left from an earlier setup, e.g. disabled since, is set again instead of added twice.
    private static void SetRoute(
        IotHubRoutingProperties routing,
        string name,
        IotHubRoutingSource source)
    {
        var route = routing.Routes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            routing.Routes.Add(new RoutingRuleProperties(name, source, [IoTHubSetupCheck.BuiltInEndpointName], isEnabled: true)
            {
                Condition = "true",
            });

            return;
        }

        route.Source = source;
        route.Condition = "true";
        route.IsEnabled = true;
        route.EndpointNames.Clear();
        route.EndpointNames.Add(IoTHubSetupCheck.BuiltInEndpointName);
    }

    // The fallback route can only send to the built-in endpoint.
    private static void SetFallbackRoute(IotHubRoutingProperties routing)
    {
        var fallback = routing.FallbackRoute ??= new IotHubFallbackRouteProperties(
            IotHubRoutingSource.DeviceMessages,
            [IoTHubSetupCheck.BuiltInEndpointName],
            isEnabled: true)
        {
            Name = FallbackRouteName,
            Condition = "true",
        };

        fallback.IsEnabled = true;

        if (!fallback.EndpointNames.Contains(IoTHubSetupCheck.BuiltInEndpointName, StringComparer.OrdinalIgnoreCase))
        {
            fallback.EndpointNames.Clear();
            fallback.EndpointNames.Add(IoTHubSetupCheck.BuiltInEndpointName);
        }
    }
}