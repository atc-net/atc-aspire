namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class IoTHubSetupCheckTests
{
    private static readonly IoTHubEventSource[] AllSources =
    [
        IoTHubEventSource.DeviceLifecycleEvents,
        IoTHubEventSource.TwinChangeEvents,
        IoTHubEventSource.Telemetry,
    ];

    [Fact]
    public void Check_HubSetUp_IsCompleteAndNotBlocked()
    {
        // Act
        var result = IoTHubSetupCheck.Check(TestHubSettings.SetUp(), "aspire-bridge", AllSources);

        // Assert
        result.IsComplete.Should().BeTrue();
        result.BlockedReason.Should().BeNull();
    }

    [Fact]
    public void Check_NoConsumerGroupAndNoRoutes_MissesEverythingInOrder()
    {
        // Arrange
        var settings = new IoTHubSettings([], FallbackRoute: null, [], ["$Default"]);

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", AllSources.Reverse());

        // Assert
        result.Missing.Should().Equal(
            IoTHubSetupItem.ConsumerGroup,
            IoTHubSetupItem.DeviceLifecycleEventsRoute,
            IoTHubSetupItem.TwinChangeEventsRoute,
            IoTHubSetupItem.TelemetryRoute);
    }

    [Fact]
    public void Check_OnlyTwinChangesForwarded_NeedsNoLifecycleOrTelemetryRoute()
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with { Routes = [TestHubSettings.Route("twins", "TwinChangeEvents")], FallbackRoute = null };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", [IoTHubEventSource.TwinChangeEvents]);

        // Assert
        result.IsComplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, "events")]
    [InlineData(true, "my-storage")]
    public void Check_LifecycleRouteDisabledOrToAnotherEndpoint_MissesIt(
        bool isEnabled,
        string endpointName)
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with
        {
            Routes = [TestHubSettings.Route("lifecycle", "DeviceLifecycleEvents", endpointName, isEnabled)],
        };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", [IoTHubEventSource.DeviceLifecycleEvents]);

        // Assert
        result.Missing.Should().Equal(IoTHubSetupItem.DeviceLifecycleEventsRoute);
    }

    [Fact]
    public void Check_TelemetryWithoutFallbackButWithADeviceMessagesRoute_IsComplete()
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with
        {
            Routes = [TestHubSettings.Route("telemetry", "DeviceMessages")],
            FallbackRoute = TestHubSettings.Route("$fallback", "DeviceMessages", isEnabled: false),
        };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", [IoTHubEventSource.Telemetry]);

        // Assert
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Check_TelemetryWithTheFallbackRouteOff_MissesTheTelemetryRoute()
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with { FallbackRoute = TestHubSettings.Route("$fallback", "DeviceMessages", isEnabled: false) };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", [IoTHubEventSource.Telemetry]);

        // Assert
        result.Missing.Should().Equal(IoTHubSetupItem.TelemetryRoute);
    }

    [Fact]
    public void Check_CustomEndpoints_IsBlockedNamingThem()
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with { CustomEndpointNames = ["archive", "alerts"] };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", AllSources);

        // Assert
        result.BlockedReason.Should().Be("it has custom routing endpoints (archive, alerts)");
    }

    [Fact]
    public void Check_OtherConsumerGroups_IsBlockedNamingThemButNotDefaultOrOwn()
    {
        // Arrange
        var settings = TestHubSettings.SetUp() with { ConsumerGroups = ["$Default", "aspire-bridge", "functionApp"] };

        // Act
        var result = IoTHubSetupCheck.Check(settings, "aspire-bridge", AllSources);

        // Assert
        result.IsComplete.Should().BeTrue();
        result.BlockedReason.Should().Be("other consumer groups read its built-in endpoint (functionApp)");
    }

    [Fact]
    public void Describe_ConsumerGroup_NamesIt()
        => IoTHubSetupCheck.Describe(IoTHubSetupItem.ConsumerGroup, "my-group")
            .Should().Be("the consumer group 'my-group' on the built-in endpoint");
}