namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// A message route of an IoT Hub, or its fallback route.
/// </summary>
/// <param name="Name">The route name.</param>
/// <param name="Source">The routing source, e.g. <c>DeviceLifecycleEvents</c>, <c>TwinChangeEvents</c> or <c>DeviceMessages</c>.</param>
/// <param name="EndpointNames">The endpoints the route sends to; the built-in endpoint is <c>events</c>.</param>
/// <param name="IsEnabled">Whether the route is enabled.</param>
internal sealed record IoTHubRoute(
    string Name,
    string Source,
    IReadOnlyList<string> EndpointNames,
    bool IsEnabled);