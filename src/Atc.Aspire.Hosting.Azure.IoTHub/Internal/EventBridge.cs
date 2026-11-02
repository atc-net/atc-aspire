namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Reads the events of an IoT Hub's built-in endpoint and sends a copy of each to the Event Hub for its kind.
/// It remembers the last event read from each partition, so a run after a pause continues where the last one stopped.
/// </summary>
internal sealed partial class EventBridge
{
    private readonly IIoTHubEventReader reader;
    private readonly IReadOnlyDictionary<IoTHubEventSource, IEventHubSender> senders;
    private readonly IReadOnlySet<string>? deviceIds;
    private readonly DateTimeOffset startTime;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, long> lastSequenceNumbers = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="EventBridge"/> class.
    /// </summary>
    /// <param name="reader">The reader for the IoT Hub's built-in endpoint.</param>
    /// <param name="senders">The sender for each kind of event to forward.</param>
    /// <param name="deviceIds">The devices to forward events of, or <see langword="null"/> for all devices.</param>
    /// <param name="startTime">The time of the first events to read.</param>
    /// <param name="logger">The logger.</param>
    public EventBridge(
        IIoTHubEventReader reader,
        IReadOnlyDictionary<IoTHubEventSource, IEventHubSender> senders,
        IReadOnlySet<string>? deviceIds,
        DateTimeOffset startTime,
        ILogger logger)
    {
        this.reader = reader;
        this.senders = senders;
        this.deviceIds = deviceIds;
        this.startTime = startTime;
        this.logger = logger;
    }

    public EventBridgeCounters Counters { get; } = new();

    /// <summary>
    /// Reads and forwards events from all partitions until cancelled, or until reading a partition fails.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when cancelled, or fails with the error of a partition.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var partitionIds = await reader.GetPartitionIdsAsync(cancellationToken);

        // When one partition fails, the others stop too, so the bridge fails as a whole.
        using var partitions = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Task.WhenAll is awaited here, so every read has ended before partitions is disposed.
        // ReSharper disable once AccessToDisposedClosure
        await Task.WhenAll(partitionIds.Select(partitionId => ReadPartitionAsync(partitionId, partitions)));
    }

    private async Task ReadPartitionAsync(
        string partitionId,
        CancellationTokenSource partitions)
    {
        var cancellationToken = partitions.Token;

        try
        {
            await foreach (var eventData in reader.ReadAsync(partitionId, GetPosition(partitionId), cancellationToken))
            {
                await ForwardAsync(eventData, cancellationToken);
                lastSequenceNumbers[partitionId] = eventData.SequenceNumber;
            }
        }

        // A real error, or a cancellation nobody asked for, e.g. a timeout: stop the other partitions and fail.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await partitions.CancelAsync();

            // Rethrown, a cancellation would only say "A task was canceled." when the bridge fails.
            if (ex is OperationCanceledException)
            {
                throw new InvalidOperationException($"Reading partition '{partitionId}' stopped unexpectedly: {ex.Message}", ex);
            }

            throw;
        }
    }

    private EventPosition GetPosition(string partitionId)
        => lastSequenceNumbers.TryGetValue(partitionId, out var sequenceNumber)
            ? EventPosition.FromSequenceNumber(sequenceNumber, isInclusive: false)
            : EventPosition.FromEnqueuedTime(startTime);

    private async Task ForwardAsync(
        EventData eventData,
        CancellationToken cancellationToken)
    {
        // Kinds the bridge doesn't know, e.g. connection state events, and kinds with no Event Hub aren't meant for it,
        // so they aren't counted: skipped only counts the events ForDevices filters out.
        if (IoTHubEvent.GetSource(eventData) is not { } source ||
            !senders.TryGetValue(source, out var sender))
        {
            return;
        }

        var deviceId = IoTHubEvent.GetDeviceId(eventData);
        if (deviceIds is not null &&
            (deviceId is null || !deviceIds.Contains(deviceId)))
        {
            Counters.AddSkipped(source);
            return;
        }

        var moduleId = IoTHubEvent.GetModuleId(eventData);
        var origin = moduleId is null
            ? deviceId ?? "unknown device"
            : $"{deviceId}/{moduleId}";

        var eventType = IoTHubEvent.GetOperationType(eventData) ?? "message";

        try
        {
            // The device id as partition key keeps the events of a device in order, as IoT Hub does.
            await sender.SendAsync(IoTHubEvent.Copy(eventData), deviceId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Counters.AddFailed(source);
            LogForwardFailed(ex, eventType, source, origin, sender.EventHubName, ex.Message);
            return;
        }

        Counters.AddForwarded(source);
        LogForwarded(eventType, source, origin, sender.EventHubName);
    }
}