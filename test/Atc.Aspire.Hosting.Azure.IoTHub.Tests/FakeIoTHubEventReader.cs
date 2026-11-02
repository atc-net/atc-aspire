namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

/// <summary>
/// An IoT Hub event reader whose partitions deliver the events and errors a test writes, in order,
/// and that records where each read started.
/// </summary>
internal sealed class FakeIoTHubEventReader : IIoTHubEventReader
{
    private readonly Dictionary<string, Channel<(EventData? EventData, Exception? Error)>> partitions;
    private readonly Lock positionsLock = new();
    private readonly List<(string PartitionId, EventPosition Position)> positions = [];

    public FakeIoTHubEventReader(params string[] partitionIds)
        => partitions = partitionIds.ToDictionary(x => x, _ => Channel.CreateUnbounded<(EventData?, Exception?)>(), StringComparer.Ordinal);

    /// <summary>
    /// Gets the partition and position of each read, in order.
    /// </summary>
    public IReadOnlyList<(string PartitionId, EventPosition Position)> Positions
    {
        get
        {
            lock (positionsLock)
            {
                return [.. positions];
            }
        }
    }

    /// <summary>
    /// Gets whether the reader was disposed, i.e. its connection closed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    public void Write(
        string partitionId,
        EventData eventData)
        => partitions[partitionId].Writer.TryWrite((eventData, null)).Should().BeTrue();

    /// <summary>
    /// Makes the read of a partition fail, after the events written so far.
    /// </summary>
    /// <param name="partitionId">The partition.</param>
    /// <param name="error">The error the read fails with.</param>
    public void Fail(
        string partitionId,
        Exception error)
        => partitions[partitionId].Writer.TryWrite((null, error)).Should().BeTrue();

    public Task<string[]> GetPartitionIdsAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(partitions.Keys.ToArray());

    public async IAsyncEnumerable<EventData> ReadAsync(
        string partitionId,
        EventPosition position,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        lock (positionsLock)
        {
            positions.Add((partitionId, position));
        }

        await foreach (var (eventData, error) in partitions[partitionId].Reader.ReadAllAsync(cancellationToken))
        {
            if (error is not null)
            {
                throw error;
            }

            yield return eventData!;
        }
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}