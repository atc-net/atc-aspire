namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeDeviceResourceTests
{
    private const string EdgeHubReportedProperties = """
        {
          "clients": {
            "edge-device/module": { "status": "Connected" }
          }
        }
        """;

    private readonly IIoTHubService iotHubService = Substitute.For<IIoTHubService>();
    private readonly FakeTimeProvider timeProvider = new();
    private readonly IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    private readonly IResourceBuilder<IoTEdgeDeviceResource> device;

    public IoTEdgeDeviceResourceTests()
    {
        device = builder
            .AddIoTEdgeDevice("edge-device", builder.AddParameter("iothub-connection-string", secret: true))
            .WithDeviceId("edge-device");

        device.Resource.IoTHubService = iotHubService;
        device.Resource.TimeProvider = timeProvider;
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_WithEdgeHub_ReadsTheClientsEdgeHubReports()
    {
        // Arrange
        device.WithEdgeHub();
        SetUpEdgeHubTwin();

        // Act
        var states = await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Assert
        states.Should().Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["module"] = "Connected" });
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_WithoutEdgeHub_ReadsTheRegistry()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .Returns([new Module("edge-device", "module") { ConnectionState = DeviceConnectionState.Connected }]);

        // Act
        var states = await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Assert
        states.Should().Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["module"] = "Connected" });
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_CalledTwiceWithinFiveSeconds_ReadsOnce()
    {
        // Arrange
        device.WithEdgeHub();
        SetUpEdgeHubTwin();

        await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(4));

        // Act
        await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Assert
        await iotHubService
            .Received(1)
            .GetModuleTwin("edge-device", "$edgeHub", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_CalledAgainAfterFiveSeconds_ReadsAgain()
    {
        // Arrange
        device.WithEdgeHub();
        SetUpEdgeHubTwin();

        await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(6));

        // Act
        await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Assert
        await iotHubService
            .Received(2)
            .GetModuleTwin("edge-device", "$edgeHub", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_PreviousReadFailed_ReadsAgain()
    {
        // Arrange
        device.WithEdgeHub();

        iotHubService
            .GetModuleTwin("edge-device", "$edgeHub", Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<Twin?>(new IotHubException("Unauthorized")),
                _ => Task.FromResult<Twin?>(CreateEdgeHubTwin()));

        var failedRead = () => device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);
        await failedRead.Should().ThrowAsync<IotHubException>();

        // Act
        var states = await device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Assert
        states.Should().ContainKey("module");
    }

    [Fact]
    public async Task GetModuleConnectionStatesAsync_FirstCallerCancels_OtherCallersStillGetTheStates()
    {
        // Arrange
        device.WithEdgeHub();

        var twin = new TaskCompletionSource<Twin?>(TaskCreationOptions.RunContinuationsAsynchronously);
        iotHubService
            .GetModuleTwin("edge-device", "$edgeHub", Arg.Any<CancellationToken>())
            .Returns(twin.Task);

        using var firstCaller = new CancellationTokenSource();
        var firstRead = device.Resource.GetModuleConnectionStatesAsync(firstCaller.Token);
        var secondRead = device.Resource.GetModuleConnectionStatesAsync(TestContext.Current.CancellationToken);

        // Act
        await firstCaller.CancelAsync();
        twin.SetResult(CreateEdgeHubTwin());

        // Assert
        await FluentActions.Awaiting(() => firstRead).Should().ThrowAsync<OperationCanceledException>();

        var states = await secondRead;
        states.Should().ContainKey("module");
    }

    private void SetUpEdgeHubTwin()
        => iotHubService
            .GetModuleTwin("edge-device", "$edgeHub", Arg.Any<CancellationToken>())
            .Returns(CreateEdgeHubTwin());

    private static Twin CreateEdgeHubTwin()
        => new() { Properties = new TwinProperties { Reported = new TwinCollection(EdgeHubReportedProperties) } };
}