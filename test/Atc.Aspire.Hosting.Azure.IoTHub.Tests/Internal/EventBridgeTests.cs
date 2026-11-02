namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class EventBridgeTests : IAsyncDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeIoTHubEventReader reader = new("0", "1");
    private readonly FakeEventHubSender lifecycle = new("lifecycle");
    private readonly FakeEventHubSender twinChanges = new("twin-changes");
    private readonly FakeEventHubSender telemetry = new("telemetry");
    private readonly CancellationTokenSource running = new();
    private Task? run;

    public async ValueTask DisposeAsync()
    {
        await running.CancelAsync();

        if (run is not null)
        {
            await FluentActions.Awaiting(() => run).Should().ThrowAsync<OperationCanceledException>();
        }

        running.Dispose();

        await reader.DisposeAsync();
        await lifecycle.DisposeAsync();
        await twinChanges.DisposeAsync();
        await telemetry.DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_EventOfEachKind_SendsACopyToItsEventHubWithTheDeviceIdAsPartitionKey()
    {
        // Arrange
        var bridge = CreateBridge();
        var lifecycleEvent = TestEvents.Lifecycle("device1", sequenceNumber: 1);
        var twinChangeEvent = TestEvents.TwinChange("device1", "module1", sequenceNumber: 2);
        var telemetryEvent = TestEvents.Telemetry("device2", "module1", sequenceNumber: 1);

        // Act
        Start(bridge);
        reader.Write("0", lifecycleEvent);
        reader.Write("0", twinChangeEvent);
        reader.Write("1", telemetryEvent);

        // Assert
        var (sentLifecycle, lifecycleKey) = await lifecycle.ReceiveAsync();
        var (sentTwinChange, twinChangeKey) = await twinChanges.ReceiveAsync();
        var (sentTelemetry, telemetryKey) = await telemetry.ReceiveAsync();

        sentLifecycle.Should().NotBeSameAs(lifecycleEvent);
        sentLifecycle.Properties["opType"].Should().Be("createDeviceIdentity");
        sentTwinChange.Properties["opType"].Should().Be("updateTwin");
        sentTelemetry.EventBody.ToString().Should().Be("""{"temperature":21.5}""");
        (lifecycleKey, twinChangeKey, telemetryKey).Should().Be(("device1", "device1", "device2"));

        bridge.Counters.Get(IoTHubEventSource.DeviceLifecycleEvents).Should().Be(new EventCounts(Forwarded: 1, Skipped: 0, Failed: 0));
        bridge.Counters.Get(IoTHubEventSource.TwinChangeEvents).Should().Be(new EventCounts(Forwarded: 1, Skipped: 0, Failed: 0));
        bridge.Counters.Get(IoTHubEventSource.Telemetry).Should().Be(new EventCounts(Forwarded: 1, Skipped: 0, Failed: 0));
    }

    [Fact]
    public async Task RunAsync_EventOfAnotherDevice_SkipsAndCountsIt()
    {
        // Arrange
        var bridge = CreateBridge(deviceIds: new HashSet<string>(StringComparer.Ordinal) { "device1" });

        // Act
        Start(bridge);
        reader.Write("0", TestEvents.Lifecycle("other-device", sequenceNumber: 1));
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 2));

        // Assert
        var (sent, _) = await lifecycle.ReceiveAsync();

        IoTHubEvent.GetDeviceId(sent).Should().Be("device1");
        lifecycle.HasPending().Should().BeFalse();
        bridge.Counters.Get(IoTHubEventSource.DeviceLifecycleEvents).Should().Be(new EventCounts(Forwarded: 1, Skipped: 1, Failed: 0));
    }

    [Fact]
    public async Task RunAsync_KindWithoutEventHub_IgnoresItWithoutCounting()
    {
        // Arrange
        var bridge = CreateBridge(forwardTelemetry: false);

        // Act
        Start(bridge);
        reader.Write("0", TestEvents.Telemetry("device1", "module1", sequenceNumber: 1));
        reader.Write("0", TestEvents.Create("deviceConnectionStateEvents", "device1", moduleId: null, operationType: "deviceConnected", sequenceNumber: 2));
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 3));

        // Assert
        await lifecycle.ReceiveAsync();

        telemetry.HasPending().Should().BeFalse();
        bridge.Counters.Get(IoTHubEventSource.Telemetry).Should().Be(default(EventCounts));
    }

    [Fact]
    public async Task RunAsync_SendFails_CountsTheFailureAndForwardsTheNextEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        twinChanges.SendError = new EventHubsException(isTransient: false, "twin-changes", "The Event Hub is gone.");

        // Act
        Start(bridge);
        reader.Write("0", TestEvents.TwinChange("device1", "module1", sequenceNumber: 1));
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 2));

        // Assert
        await lifecycle.ReceiveAsync();

        bridge.Counters.Get(IoTHubEventSource.TwinChangeEvents).Should().Be(new EventCounts(Forwarded: 0, Skipped: 0, Failed: 1));
        bridge.Counters.Get(IoTHubEventSource.DeviceLifecycleEvents).Should().Be(new EventCounts(Forwarded: 1, Skipped: 0, Failed: 0));
    }

    [Fact]
    public async Task RunAsync_FirstRun_ReadsEachPartitionFromTheStartTime()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        Start(bridge);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        reader.Write("1", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        await lifecycle.ReceiveAsync();
        await lifecycle.ReceiveAsync();

        // Assert
        reader.Positions.Should().BeEquivalentTo(
        [
            ("0", EventPosition.FromEnqueuedTime(StartTime)),
            ("1", EventPosition.FromEnqueuedTime(StartTime)),
        ]);
    }

    [Fact]
    public async Task RunAsync_RunAfterACancelledRun_ReadsAfterTheLastEventOfEachPartition()
    {
        // Arrange
        var bridge = CreateBridge();
        using var firstRun = new CancellationTokenSource();
        var first = bridge.RunAsync(firstRun.Token);

        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 7));
        await lifecycle.ReceiveAsync();
        await firstRun.CancelAsync();
        await FluentActions.Awaiting(() => first).Should().ThrowAsync<OperationCanceledException>();

        // Act
        Start(bridge);
        reader.Write("0", TestEvents.Lifecycle("device1", sequenceNumber: 8));
        reader.Write("1", TestEvents.Lifecycle("device1", sequenceNumber: 1));
        await lifecycle.ReceiveAsync();
        await lifecycle.ReceiveAsync();

        // Assert
        reader.Positions.Skip(2).Should().BeEquivalentTo(
        [
            ("0", EventPosition.FromSequenceNumber(7, isInclusive: false)),
            ("1", EventPosition.FromEnqueuedTime(StartTime)),
        ]);
    }

    [Fact]
    public Task RunAsync_ReadingAPartitionFails_StopsAndThrowsTheError()
    {
        // Arrange
        var bridge = CreateBridge();
        var error = new EventHubsException(isTransient: false, "myhub", "The consumer group doesn't exist.", EventHubsException.FailureReason.ResourceNotFound);

        // Act
        var runTask = bridge.RunAsync(TestContext.Current.CancellationToken);
        reader.Fail("0", error);

        // Assert
        return FluentActions.Awaiting(() => runTask)
            .Should().ThrowAsync<EventHubsException>()
            .WithMessage("The consumer group doesn't exist.*");
    }

    [Fact]
    public Task RunAsync_ReadingAPartitionIsCancelledUnexpectedly_ThrowsAnErrorNamingThePartition()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        var runTask = bridge.RunAsync(TestContext.Current.CancellationToken);
        reader.Fail("1", new OperationCanceledException("The operation timed out."));

        // Assert
        return FluentActions.Awaiting(() => runTask)
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Reading partition '1' stopped unexpectedly: The operation timed out.");
    }

    private EventBridge CreateBridge(
        IReadOnlySet<string>? deviceIds = null,
        bool forwardTelemetry = true)
    {
        var senders = new Dictionary<IoTHubEventSource, IEventHubSender>
        {
            [IoTHubEventSource.DeviceLifecycleEvents] = lifecycle,
            [IoTHubEventSource.TwinChangeEvents] = twinChanges,
        };

        if (forwardTelemetry)
        {
            senders[IoTHubEventSource.Telemetry] = telemetry;
        }

        return new EventBridge(reader, senders, deviceIds, StartTime, NullLogger.Instance);
    }

    private void Start(EventBridge bridge)
        => run = bridge.RunAsync(running.Token);
}