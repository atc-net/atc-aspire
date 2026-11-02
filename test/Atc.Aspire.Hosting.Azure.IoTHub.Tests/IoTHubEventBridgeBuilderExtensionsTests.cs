namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

public sealed class IoTHubEventBridgeBuilderExtensionsTests
{
    [Fact]
    public void AddIoTHubEventBridge_Defaults_ReadsWithTheBridgeConsumerGroupForAllDevices()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var connectionString = builder.AddParameter("iothub-connection-string", secret: true);

        // Act
        var bridge = builder.AddIoTHubEventBridge("iothub-events", connectionString);

        // Assert
        bridge.Resource.IotHubConnectionString.Should().BeSameAs(connectionString.Resource);
        bridge.Resource.ConsumerGroup.Should().Be("aspire-bridge");
        bridge.Resource.DeviceIds.Should().BeNull();
        bridge.Resource.Targets.Should().BeEmpty();
    }

    [Fact]
    public void ForwardEachKind_EventHubs_SetsTheTargetsAndWaitsForTheEventHubsAndTheirNamespace()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();
        var lifecycle = eventHubs.AddHub("lifecycle");
        var twinChanges = eventHubs.AddHub("twin-changes");
        var telemetry = eventHubs.AddHub("telemetry");

        // Act
        var bridge = AddBridge(builder)
            .ForwardDeviceLifecycleEvents(lifecycle)
            .ForwardTwinChangeEvents(twinChanges)
            .ForwardTelemetry(telemetry);

        // Assert
        bridge.Resource.Targets.Should().HaveCount(3);
        bridge.Resource.Targets[IoTHubEventSource.DeviceLifecycleEvents].Should().BeSameAs(lifecycle.Resource);
        bridge.Resource.Targets[IoTHubEventSource.TwinChangeEvents].Should().BeSameAs(twinChanges.Resource);
        bridge.Resource.Targets[IoTHubEventSource.Telemetry].Should().BeSameAs(telemetry.Resource);

        // Aspire also waits for the namespace of an Event Hub, e.g. the emulator.
        bridge.Resource.Annotations.OfType<WaitAnnotation>()
            .Select(x => x.Resource)
            .Distinct()
            .Should().Equal(eventHubs.Resource, lifecycle.Resource, twinChanges.Resource, telemetry.Resource);
    }

    [Fact]
    public void ForwardTwinChangeEvents_SecondEventHub_ThrowsNamingTheFirst()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();
        var bridge = AddBridge(builder).ForwardTwinChangeEvents(eventHubs.AddHub("twin-changes"));

        // Act
        var act = () => bridge.ForwardTwinChangeEvents(eventHubs.AddHub("other"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("IoT Hub event bridge 'iothub-events' already forwards TwinChangeEvents to Event Hub 'twin-changes'.");
    }

    [Fact]
    public void WithConsumerGroup_Name_SetsIt()
        => AddBridge(DistributedApplication.CreateBuilder())
            .WithConsumerGroup("my-group")
            .Resource.ConsumerGroup.Should().Be("my-group");

    [Theory]
    [InlineData("iothub-bridge-start", "NotStarted", true)]
    [InlineData("iothub-bridge-start", "FailedToStart", true)]
    [InlineData("iothub-bridge-start", "Starting", false)]
    [InlineData("iothub-bridge-start", "Running", false)]
    [InlineData("iothub-bridge-start", "Failed", false)]
    [InlineData("iothub-bridge-pause", "Running", true)]
    [InlineData("iothub-bridge-pause", "Starting", false)]
    [InlineData("iothub-bridge-pause", "Paused", false)]
    [InlineData("iothub-bridge-pause", "Failed", false)]
    [InlineData("iothub-bridge-resume", "Paused", true)]
    [InlineData("iothub-bridge-resume", "Failed", true)]
    [InlineData("iothub-bridge-resume", "Running", false)]
    [InlineData("iothub-bridge-resume", "FailedToStart", false)]
    public void Command_State_IsShownOnlyWhenItApplies(
        string commandName,
        string state,
        bool shown)
    {
        // Arrange
        var command = AddBridge(DistributedApplication.CreateBuilder())
            .Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Single(x => x.Name == commandName);

        using var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var result = command.UpdateState(new UpdateCommandStateContext
        {
            ResourceSnapshot = new CustomResourceSnapshot { ResourceType = "IoTHubEventBridge", Properties = [], State = state },
            Services = services,
        });

        // Assert
        result.Should().Be(shown ? ResourceCommandState.Enabled : ResourceCommandState.Hidden);
    }

    [Fact]
    public void WithWebSockets_SetsIt()
        => AddBridge(DistributedApplication.CreateBuilder())
            .WithWebSockets()
            .Resource.UseWebSockets.Should().BeTrue();

    [Fact]
    public void ForDevices_DeviceIds_SetsThem()
        => AddBridge(DistributedApplication.CreateBuilder())
            .ForDevices("device1", "device2")
            .Resource.DeviceIds.Should().BeEquivalentTo("device1", "device2");

    [Fact]
    public void ForAllDevices_AfterForDevices_ClearsTheDevices()
        => AddBridge(DistributedApplication.CreateBuilder())
            .ForDevices("device1")
            .ForAllDevices()
            .Resource.DeviceIds.Should().BeNull();

    [Fact]
    public void ForDevices_Parameter_KeepsItForTheStartAndReferencesIt()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var deviceId = builder.AddParameter("edge-device-id");

        // Act
        var bridge = AddBridge(builder).ForDevices("device1").ForDevices(deviceId).Resource;

        // Assert
        bridge.DeviceIds.Should().BeNull();
        bridge.DeviceIdParameters.Should().Equal(deviceId.Resource);
        bridge.Annotations.OfType<ResourceRelationshipAnnotation>().Should().Contain(x => x.Resource == deviceId.Resource);
    }

    [Fact]
    public void ForDevices_DeviceIdsAfterAParameter_DropsTheParameterAndItsReference()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var deviceId = builder.AddParameter("edge-device-id");

        // Act
        var bridge = AddBridge(builder).ForDevices(deviceId).ForDevices("device1").Resource;

        // Assert
        bridge.DeviceIds.Should().BeEquivalentTo("device1");
        bridge.DeviceIdParameters.Should().BeEmpty();
        bridge.Annotations.OfType<ResourceRelationshipAnnotation>().Should().NotContain(x => x.Resource == deviceId.Resource);
    }

    [Fact]
    public void ForDevices_NoParameter_Throws()
        => FluentActions.Invoking(() => AddBridge(DistributedApplication.CreateBuilder()).ForDevices(Array.Empty<IResourceBuilder<ParameterResource>>()))
            .Should().Throw<ArgumentException>()
            .WithParameterName("deviceIds");

    [Theory]
    [InlineData]
    [InlineData("device1", "")]
    public void ForDevices_NoOrEmptyDeviceId_Throws(params string[] deviceIds)
        => FluentActions.Invoking(() => AddBridge(DistributedApplication.CreateBuilder()).ForDevices(deviceIds))
            .Should().Throw<ArgumentException>()
            .WithParameterName(nameof(deviceIds));

    private static IResourceBuilder<IoTHubEventBridgeResource> AddBridge(
        IDistributedApplicationBuilder builder)
        => builder.AddIoTHubEventBridge("iothub-events", builder.AddParameter("iothub-connection-string", secret: true));
}