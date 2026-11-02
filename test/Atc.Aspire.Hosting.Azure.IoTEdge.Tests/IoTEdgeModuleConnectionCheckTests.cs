namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeModuleConnectionCheckTests : IAsyncDisposable
{
    private const string ConnectionPropertyName = "iotedge.connection";
    private const int MaxTicks = 50;

    private readonly IIoTHubService iotHubService = Substitute.For<IIoTHubService>();
    private readonly FakeTimeProvider timeProvider = new();
    private readonly IResourceBuilder<IoTEdgeDeviceResource> device;
    private readonly IResourceBuilder<ContainerResource> module;
    private readonly DistributedApplication app;

    public IoTEdgeModuleConnectionCheckTests()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Without edgeHub, the check reads the connection state from the registry.
        device = IoTEdgeTestHelpers.AddDevice(builder).WithDeviceId("edge-device");
        module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        device.Resource.IoTHubService = iotHubService;
        device.Resource.TimeProvider = timeProvider;
        device.Resource.SetProvisioned(IoTEdgeTestHelpers.CreateIdentity());

        app = builder.Build();
    }

    public ValueTask DisposeAsync()
        => app.DisposeAsync();

    [Fact]
    public async Task ConnectionCheck_ModuleConnectsInTheSecondRound_ShowsConnectedAfterTwoReads()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(
                ModulesWithState(DeviceConnectionState.Disconnected),
                ModulesWithState(DeviceConnectionState.Connected));

        // Act
        await RaiseReadyAsync();
        var connection = await AdvanceUntilConnectionIsAsync("Connected");

        // Assert
        connection.Should().Be("Connected");

        await iotHubService
            .Received(2)
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectionCheck_ModuleNeverConnects_ShowsNotConnectedAfterSixReads()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(ModulesWithState(DeviceConnectionState.Disconnected));

        // Act
        await RaiseReadyAsync();
        var connection = await AdvanceUntilConnectionIsAsync("Not connected");

        // Assert
        connection.Should().Be("Not connected");

        await iotHubService
            .Received(6)
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectionCheck_IoTHubFailsOnce_KeepsCheckingAndShowsConnected()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new ThrottlingException("Too many requests"),
                _ => Task.FromResult(ModulesWithState(DeviceConnectionState.Connected)));

        // Act
        await RaiseReadyAsync();
        var connection = await AdvanceUntilConnectionIsAsync("Connected");

        // Assert
        connection.Should().Be("Connected");

        await iotHubService
            .Received(2)
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectionCheck_IoTHubFailsEveryTime_ShowsNotConnectedAfterSixReads()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        await RaiseReadyAsync();
        var connection = await AdvanceUntilConnectionIsAsync("Not connected");

        // Assert
        connection.Should().Be("Not connected");

        await iotHubService
            .Received(6)
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectionCheck_DeviceDeleted_StopsWithoutReading()
    {
        // Arrange
        device.Resource.SetDeleted();

        // Act
        await RaiseReadyAsync();
        var connection = await AdvanceUntilConnectionIsAsync("Connected");

        // Assert
        connection.Should().Be("Checking");

        await iotHubService
            .DidNotReceive()
            .GetModulesOnIotEdgeDevice(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IReadOnlyCollection<Module> ModulesWithState(
        DeviceConnectionState connectionState)
        => [new Module("edge-device", "module") { ConnectionState = connectionState }];

    private Task RaiseReadyAsync()
        => app.Services
            .GetRequiredService<IDistributedApplicationEventing>()
            .PublishAsync(new ResourceReadyEvent(module.Resource, app.Services), TestContext.Current.CancellationToken);

    private async Task<string?> AdvanceUntilConnectionIsAsync(string expected)
    {
        // The check runs in the background: move its clock one round at a time, and let it run between rounds.
        for (var tick = 0; tick < MaxTicks && GetConnection() != expected; tick++)
        {
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }

        return GetConnection();
    }

    private string? GetConnection()
        => app.Services.GetRequiredService<ResourceNotificationService>().TryGetCurrentState(module.Resource.Name, out var resourceEvent)
            ? resourceEvent.Snapshot.Properties.FirstOrDefault(x => x.Name == ConnectionPropertyName)?.Value as string
            : null;
}