namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

public sealed class IoTHubEventBridgeStartTests : IAsyncDisposable
{
    // A well-formed connection string; the bridge reads through the fake reader, not from IoT Hub.
    // The key is built at runtime, so the source holds nothing that secret scanners take for a real connection string.
    private static readonly string ConnectionString =
        $"HostName=myhub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey={Convert.ToBase64String(new byte[32])}";

    private readonly FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeIoTHubEventReader reader = new("0");
    private readonly FakeEventHubSender lifecycle = new("lifecycle");
    private readonly List<string> senderEventHubs = [];
    private string? failingEventHub;
    private DistributedApplication? app;
    private IoTHubConnectionInfo? readerIotHub;
    private IoTHubReaderOptions? readerOptions;

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            if (Bridge(app).Controller is { } controller)
            {
                await controller.DisposeAsync();
            }

            await app.DisposeAsync();
        }

        await reader.DisposeAsync();
        await lifecycle.DisposeAsync();
    }

    [Fact]
    public async Task Start_WithoutExplicitStart_RunsAndForwardsFromShortlyBeforeTheAppHostStarted()
    {
        // Arrange
        var bridge = CreateBridge().WithConsumerGroup("my-group");

        // Act
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));

        // Assert
        var (_, partitionKey) = await lifecycle.ReceiveAsync();

        partitionKey.Should().Be("device1");
        readerIotHub!.HostName.Should().Be("myhub.azure-devices.net");
        readerOptions.Should().Be(new IoTHubReaderOptions("my-group", UseWebSockets: false));
        reader.Positions.Should().Equal(("0", EventPosition.FromEnqueuedTime(timeProvider.GetUtcNow().AddSeconds(-30))));

        GetProperties().Should().Contain(
            new KeyValuePair<string, string?>("iothub.hostName", "myhub.azure-devices.net"),
            new KeyValuePair<string, string?>("iothub.transport", "AMQP (port 5671)"),
            new KeyValuePair<string, string?>("iothub.consumerGroup", "my-group"),
            new KeyValuePair<string, string?>("bridge.startTime", "2026-10-31 11:59:30Z"),
            new KeyValuePair<string, string?>("bridge.devices", "All devices"));
    }

    [Fact]
    public async Task Start_WithWebSockets_ReadsOverWebSocketsAndShowsIt()
    {
        // Arrange
        var bridge = CreateBridge().WithWebSockets();

        // Act
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);

        // Assert
        readerOptions!.UseWebSockets.Should().BeTrue();
        GetProperties()["iothub.transport"].Should().Be("AMQP over WebSockets (port 443)");
    }

    [Fact]
    public async Task Start_WithExplicitStart_StaysNotStartedUntilTheStartCommand()
    {
        // Arrange
        var bridge = CreateBridge().WithExplicitStart();
        await InitializeAsync(bridge);

        // Act
        var stateBefore = GetState();
        var result = await ExecuteCommandAsync("iothub-bridge-start");

        // Assert
        stateBefore.Should().Be(KnownResourceStates.NotStarted);
        result.Success.Should().BeTrue();
        await WaitForStateAsync(KnownResourceStates.Running);
    }

    [Fact]
    public async Task Start_NothingToForward_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var bridge = builder.AddIoTHubEventBridge("iothub-events", builder.AddParameter("iothub-connection-string", ConnectionString, secret: true));

        // Act
        await InitializeAsync(bridge);

        // Assert
        await WaitForStateAsync(KnownResourceStates.FailedToStart);
        readerIotHub.Should().BeNull();
    }

    [Fact]
    public async Task Start_TwoKindsToOneEventHub_SendsBothThroughOneSender()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var events = builder.AddAzureEventHubs("eventhubs").RunAsEmulator().AddHub("events");
        var bridge = builder
            .AddIoTHubEventBridge("iothub-events", builder.AddParameter("iothub-connection-string", ConnectionString, secret: true))
            .ForwardDeviceLifecycleEvents(events)
            .ForwardTwinChangeEvents(events);

        // Act
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        reader.Write("0", TestEvents.TwinChange("device2", "module1", sequenceNumber: 2));

        // Assert
        (await lifecycle.ReceiveAsync()).PartitionKey.Should().Be("device1");
        (await lifecycle.ReceiveAsync()).PartitionKey.Should().Be("device2");
        senderEventHubs.Should().Equal("events");
    }

    [Fact]
    public async Task Start_SecondSenderFails_ClosesTheReaderAndTheFirstSenderAndFailsToStart()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();
        var bridge = builder
            .AddIoTHubEventBridge("iothub-events", builder.AddParameter("iothub-connection-string", ConnectionString, secret: true))
            .ForwardDeviceLifecycleEvents(eventHubs.AddHub("lifecycle"))
            .ForwardTwinChangeEvents(eventHubs.AddHub("twin-changes"));

        failingEventHub = "twin-changes";

        // Act
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.FailedToStart);

        // Assert
        senderEventHubs.Should().Equal("lifecycle", "twin-changes");
        reader.IsDisposed.Should().BeTrue();
        lifecycle.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Start_DeviceIdFromAParameter_ForwardsOnlyThatDevice()
    {
        // Arrange
        var bridge = CreateBridge();
        bridge.ForDevices(bridge.ApplicationBuilder.AddParameter("edge-device-id", "edge1-mypc"));

        // Act
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        reader.Write("0", TestEvents.Lifecycle("edge1-mypc", sequenceNumber: 2));

        // Assert
        (await lifecycle.ReceiveAsync()).PartitionKey.Should().Be("edge1-mypc");
        GetProperties().Should().Contain(new KeyValuePair<string, string?>("bridge.devices", "edge1-mypc"));
    }

    [Fact]
    public async Task Start_EmptyDeviceIdParameter_FailsToStartWithoutConnecting()
    {
        // Arrange
        var bridge = CreateBridge();
        bridge.ForDevices(bridge.ApplicationBuilder.AddParameter("edge-device-id", string.Empty));

        // Act
        await InitializeAsync(bridge);

        // Assert
        await WaitForStateAsync(KnownResourceStates.FailedToStart);
        readerIotHub.Should().BeNull();
    }

    [Fact]
    public async Task Start_EmptyConnectionString_FailsToStartWithoutConnecting()
    {
        // Arrange
        var bridge = CreateBridge(connectionString: string.Empty);

        // Act
        await InitializeAsync(bridge);

        // Assert
        await WaitForStateAsync(KnownResourceStates.FailedToStart);
        readerIotHub.Should().BeNull();
    }

    [Fact]
    public async Task PauseAndResume_Running_ShowsPausedAndThenReadsAfterTheLastEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 5));
        await lifecycle.ReceiveAsync();

        // Act
        var pause = await ExecuteCommandAsync("iothub-bridge-pause");
        var stateWhilePaused = GetState();
        var resume = await ExecuteCommandAsync("iothub-bridge-resume");
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 6));
        await lifecycle.ReceiveAsync();

        // Assert
        pause.Success.Should().BeTrue();
        resume.Success.Should().BeTrue();
        stateWhilePaused.Should().Be("Paused");
        GetState().Should().Be(KnownResourceStates.Running);
        reader.Positions.Select(x => x.Position).Should().Equal(
            EventPosition.FromEnqueuedTime(timeProvider.GetUtcNow().AddSeconds(-30)),
            EventPosition.FromSequenceNumber(5, isInclusive: false));
    }

    [Fact]
    public async Task Pause_NotRunning_FailsWithTheReason()
    {
        // Arrange
        var bridge = CreateBridge().WithExplicitStart();
        await InitializeAsync(bridge);

        // Act
        var result = await ExecuteCommandAsync("iothub-bridge-pause");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("The bridge isn't running.");
    }

    [Fact]
    public async Task Resume_Running_FailsWithTheReason()
    {
        // Arrange
        var bridge = CreateBridge();
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);

        // Act
        var result = await ExecuteCommandAsync("iothub-bridge-resume");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("The bridge isn't paused or failed.");
    }

    [Fact]
    public async Task Run_ReadingFails_ShowsFailedAndResumeContinues()
    {
        // Arrange
        var bridge = CreateBridge();
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);

        // Act
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        reader.Fail("0", new EventHubsException(isTransient: false, "myhub", "The consumer group doesn't exist.", EventHubsException.FailureReason.ResourceNotFound));
        await WaitForStateAsync("Failed");

        var resume = await ExecuteCommandAsync("iothub-bridge-resume");
        reader.Write("0", TestEvents.Lifecycle("device2", sequenceNumber: 2));

        // Assert
        resume.Success.Should().BeTrue();
        GetState().Should().Be(KnownResourceStates.Running);

        (await lifecycle.ReceiveAsync()).PartitionKey.Should().Be("device1");
        (await lifecycle.ReceiveAsync()).PartitionKey.Should().Be("device2");
        reader.Positions.Select(x => x.Position).Last().Should().Be(EventPosition.FromSequenceNumber(1, isInclusive: false));
    }

    [Fact]
    public async Task Counts_EventsForwarded_ShowsThemOnTheDashboardAfterASecond()
    {
        // Arrange
        var bridge = CreateBridge();
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);

        // Act
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        reader.Write("0", TestEvents.Lifecycle("device2", sequenceNumber: 2));
        await lifecycle.ReceiveAsync();
        await lifecycle.ReceiveAsync();

        var countsBefore = GetProperties()["bridge.deviceLifecycleEvents"];
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Assert
        countsBefore.Should().Be("lifecycle: 0 forwarded, 0 skipped, 0 failed");
        await app!.ResourceNotifications.WaitForResourceAsync(
            "iothub-events",
            x => x.Snapshot.Properties.Any(p => p.Name == "bridge.deviceLifecycleEvents" &&
                                                 Equals(p.Value, "lifecycle: 2 forwarded, 0 skipped, 0 failed")),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispose_RunningTwice_ClosesTheReaderAndTheSender()
    {
        // Arrange
        var bridge = CreateBridge();
        await InitializeAsync(bridge);
        await WaitForStateAsync(KnownResourceStates.Running);
        var controller = bridge.Resource.Controller!;

        // Act
        await controller.DisposeAsync();
        await controller.DisposeAsync();

        // Assert
        reader.IsDisposed.Should().BeTrue();
        lifecycle.IsDisposed.Should().BeTrue();
    }

    private static IoTHubEventBridgeResource Bridge(
        DistributedApplication application)
        => application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<IoTHubEventBridgeResource>().Single();

    private static IResourceBuilder<IoTHubEventBridgeResource> CreateBridge(
        string? connectionString = null)
    {
        var builder = DistributedApplication.CreateBuilder();
        var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();

        return builder
            .AddIoTHubEventBridge("iothub-events", builder.AddParameter("iothub-connection-string", connectionString ?? ConnectionString, secret: true))
            .ForwardDeviceLifecycleEvents(eventHubs.AddHub("lifecycle"));
    }

    private static void RemoveWaits(IoTHubEventBridgeResource resource)
    {
        foreach (var wait in resource.Annotations.OfType<WaitAnnotation>().ToList())
        {
            resource.Annotations.Remove(wait);
        }
    }

    private Task InitializeAsync(
        IResourceBuilder<IoTHubEventBridgeResource> bridge)
    {
        var resource = bridge.Resource;
        resource.TimeProvider = timeProvider;
        resource.CreateReader = (iotHub, options, _) =>
        {
            readerIotHub = iotHub;
            readerOptions = options;
            return Task.FromResult<IIoTHubEventReader>(reader);
        };

        resource.CreateSender = (eventHub, _) =>
        {
            senderEventHubs.Add(eventHub.Name);

            return eventHub.Name == failingEventHub
                ? Task.FromException<IEventHubSender>(new InvalidOperationException($"Event Hub '{eventHub.Name}' is unreachable."))
                : Task.FromResult<IEventHubSender>(lifecycle);
        };

        // The Event Hubs emulator doesn't run in these tests, so the bridge doesn't wait for it.
        RemoveWaits(resource);

        // Raises the event Aspire raises when the AppHost starts, without starting anything else.
        app = bridge.ApplicationBuilder.Build();

        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();

        return eventing.PublishAsync(
            new InitializeResourceEvent(
                resource,
                eventing,
                app.Services.GetRequiredService<ResourceLoggerService>(),
                app.ResourceNotifications,
                app.Services),
            TestContext.Current.CancellationToken);
    }

    private async Task WaitForStateAsync(string state)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        await app!.ResourceNotifications.WaitForResourceAsync("iothub-events", state, timeout.Token);
    }

    private string? GetState()
        => app!.ResourceNotifications.TryGetCurrentState("iothub-events", out var resourceEvent)
            ? resourceEvent.Snapshot.State?.Text
            : null;

    private Dictionary<string, string?> GetProperties()
        => app!.ResourceNotifications.TryGetCurrentState("iothub-events", out var resourceEvent)
            ? resourceEvent.Snapshot.Properties.ToDictionary(x => x.Name, x => x.Value?.ToString(), StringComparer.Ordinal)
            : [];

    private Task<ExecuteCommandResult> ExecuteCommandAsync(string commandName)
        => Bridge(app!).Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(x => x.Name == commandName)
            .ExecuteCommand(new ExecuteCommandContext
            {
                Services = app!.Services,
                Logger = NullLogger.Instance,
                ResourceName = "iothub-events",
                CancellationToken = TestContext.Current.CancellationToken,
                Arguments = new InteractionInputCollection([]),
            });
}