namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Reads the events of an IoT Hub's built-in endpoint, partition by partition.
/// </summary>
internal interface IIoTHubEventReader : IAsyncDisposable
{
    Task<string[]> GetPartitionIdsAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<EventData> ReadAsync(
        string partitionId,
        EventPosition position,
        CancellationToken cancellationToken);
}