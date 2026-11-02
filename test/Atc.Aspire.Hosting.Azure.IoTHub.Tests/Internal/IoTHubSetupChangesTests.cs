namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class IoTHubSetupChangesTests
{
    [Fact]
    public void AddRoutes_LifecycleAndTwinChangeRoutesMissing_AddsEnabledRoutesToTheBuiltInEndpointAndKeepsTheOthers()
    {
        // Arrange
        var hub = CreateHub();

        hub.Properties.Routing.Routes.Add(new RoutingRuleProperties("alerts", IotHubRoutingSource.DeviceMessages, ["events"], isEnabled: true));

        // Act
        var changed = IoTHubSetupChanges.AddRoutes(
            hub,
            [IoTHubSetupItem.ConsumerGroup, IoTHubSetupItem.DeviceLifecycleEventsRoute, IoTHubSetupItem.TwinChangeEventsRoute]);

        // Assert
        changed.Should().BeTrue();

        hub.Properties.Routing.Routes.Select(x => (x.Name, x.Source.ToString(), x.Condition, string.Join(",", x.EndpointNames), x.IsEnabled))
            .Should().Equal(
                ("alerts", "DeviceMessages", null, "events", true),
                ("aspire-bridge-device-lifecycle-events", "DeviceLifecycleEvents", "true", "events", true),
                ("aspire-bridge-twin-change-events", "TwinChangeEvents", "true", "events", true));
    }

    [Fact]
    public void AddRoutes_DisabledRouteOfTheBridge_EnablesItInsteadOfAddingAnother()
    {
        // Arrange
        var hub = CreateHub();

        hub.Properties.Routing.Routes.Add(
            new RoutingRuleProperties("aspire-bridge-twin-change-events", IotHubRoutingSource.TwinChangeEvents, ["archive"], isEnabled: false));

        // Act
        IoTHubSetupChanges.AddRoutes(hub, [IoTHubSetupItem.TwinChangeEventsRoute]);

        // Assert
        var route = hub.Properties.Routing.Routes.Should().ContainSingle().Subject;
        route.IsEnabled.Should().BeTrue();
        route.EndpointNames.Should().Equal("events");
    }

    [Fact]
    public void AddRoutes_TelemetryWithTheFallbackRouteOff_SwitchesItOn()
    {
        // Arrange
        var hub = CreateHub();

        hub.Properties.Routing.FallbackRoute = new IotHubFallbackRouteProperties(IotHubRoutingSource.DeviceMessages, ["events"], isEnabled: false)
        {
            Name = "$fallback",
        };

        // Act
        var changed = IoTHubSetupChanges.AddRoutes(hub, [IoTHubSetupItem.TelemetryRoute]);

        // Assert
        changed.Should().BeTrue();
        hub.Properties.Routing.FallbackRoute.IsEnabled.Should().BeTrue();
        hub.Properties.Routing.Routes.Should().BeEmpty();
    }

    [Fact]
    public void AddRoutes_TelemetryWithoutAFallbackRoute_AddsOneToTheBuiltInEndpoint()
    {
        // Arrange
        var hub = CreateHub();

        // Act
        IoTHubSetupChanges.AddRoutes(hub, [IoTHubSetupItem.TelemetryRoute]);

        // Assert
        var fallback = hub.Properties.Routing.FallbackRoute;
        fallback.Should().NotBeNull();
        fallback.Name.Should().Be("$fallback");
        fallback.Source.Should().Be(IotHubRoutingSource.DeviceMessages);
        fallback.EndpointNames.Should().Equal("events");
        fallback.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddRoutes_OnlyTheConsumerGroupMissing_LeavesTheHubAlone()
        => IoTHubSetupChanges.AddRoutes(CreateHub(), [IoTHubSetupItem.ConsumerGroup])
            .Should().BeFalse();

    private static IotHubDescriptionData CreateHub()
        => new(AzureLocation.WestEurope, new IotHubSkuInfo(IotHubSku.S1))
        {
            Properties = new IotHubProperties
            {
                Routing = new IotHubRoutingProperties(),
            },
        };
}