namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeDeviceCommandsTests : IAsyncDisposable
{
    private const string DeleteDeviceCommand = "iotedge-delete-device";

    private readonly IIoTHubService iotHubService = Substitute.For<IIoTHubService>();
    private readonly IResourceBuilder<IoTEdgeDeviceResource> device;
    private readonly IResourceBuilder<ContainerResource> module;
    private readonly DistributedApplication app;

    public IoTEdgeDeviceCommandsTests()
    {
        var builder = DistributedApplication.CreateBuilder();

        device = IoTEdgeTestHelpers.AddDevice(builder).WithDeviceId("edge-device");
        device.Resource.IoTHubService = iotHubService;
        device.Resource.Provisioner = new IoTEdgeDeviceProvisioner(iotHubService, "myhub.azure-devices.net");
        module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        app = builder.Build();
    }

    public ValueTask DisposeAsync()
        => app.DisposeAsync();

    [Fact]
    public async Task DeleteDevice_ProvisionedDevice_DeletesItAndShowsDeleted()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await ExecuteDeleteAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Deleted device 'edge-device'. Nothing was running. Restart the AppHost to provision it again.");
        device.Resource.IsProvisioned.Should().BeFalse();
        GetState().Should().Be("Deleted");
    }

    [Fact]
    public async Task DeleteDevice_DeviceIsAlreadyGone_ShowsDeleted()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await ExecuteDeleteAsync();

        // Assert
        result.Success.Should().BeTrue();
        device.Resource.IsProvisioned.Should().BeFalse();
        GetState().Should().Be("Deleted");
    }

    [Fact]
    public async Task DeleteDevice_IoTHubRefuses_FailsWithTheReasonAndKeepsTheDevice()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException("Unauthorized"));

        // Act
        var result = await ExecuteDeleteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Could not delete device 'edge-device': Unauthorized");
        device.Resource.IsProvisioned.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDevice_DeviceNotProvisioned_FailsWithoutCallingIoTHub()
    {
        // Act
        var result = await ExecuteDeleteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("The device has not been provisioned.");

        await iotHubService
            .DidNotReceive()
            .DeleteDevice(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteDevice_ModuleWaiting_FinishesWithoutStoppingIt()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(true);

        await GetNotifications().PublishUpdateAsync(module.Resource, snapshot => snapshot with { State = KnownResourceStates.Waiting });

        // Act
        var result = await ExecuteDeleteAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Nothing was running.");
        GetModuleState().Should().Be(KnownResourceStates.Waiting);
    }

    [Fact]
    public async Task DeleteDevice_ProvisionedDevice_DisablesTheModuleCommands()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await ExecuteDeleteAsync();

        // Assert
        GetNotifications().TryGetCurrentState(module.Resource.Name, out var resourceEvent).Should().BeTrue();
        resourceEvent!.Snapshot.Commands
            .Where(x => x.Name.StartsWith("iotedge-", StringComparison.Ordinal))
            .Should().NotBeEmpty()
            .And.OnlyContain(x => x.State == ResourceCommandState.Disabled);
    }

    [Fact]
    public async Task UpdateState_DeviceDeleted_ReturnsDisabled()
    {
        // Arrange
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        iotHubService
            .DeleteDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(true);

        await ExecuteDeleteAsync();

        // Act
        var state = GetDeleteCommand().UpdateState(new UpdateCommandStateContext
        {
            ResourceSnapshot = new CustomResourceSnapshot { ResourceType = "IoTEdgeDevice", Properties = [] },
            Services = app.Services,
        });

        // Assert
        state.Should().Be(ResourceCommandState.Disabled);
    }

    private ResourceCommandAnnotation GetDeleteCommand()
        => device.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(x => x.Name == DeleteDeviceCommand);

    private Task<ExecuteCommandResult> ExecuteDeleteAsync()
        => GetDeleteCommand().ExecuteCommand(new ExecuteCommandContext
        {
            Services = app.Services,
            Logger = NullLogger.Instance,
            ResourceName = device.Resource.Name,
            CancellationToken = TestContext.Current.CancellationToken,
            Arguments = new InteractionInputCollection([]),
        });

    private ResourceNotificationService GetNotifications()
        => app.Services.GetRequiredService<ResourceNotificationService>();

    private string? GetModuleState()
        => GetNotifications().TryGetCurrentState(module.Resource.Name, out var resourceEvent)
            ? resourceEvent.Snapshot.State?.Text
            : null;

    private string? GetState()
        => app.Services.GetRequiredService<ResourceNotificationService>().TryGetCurrentState(device.Resource.Name, out var resourceEvent)
            ? resourceEvent.Snapshot.State?.Text
            : null;
}