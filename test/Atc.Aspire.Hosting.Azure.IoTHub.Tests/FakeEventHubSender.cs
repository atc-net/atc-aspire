namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests;

/// <summary>
/// An Event Hub sender that keeps what it is sent, so a test can wait for the events it expects.
/// </summary>
internal sealed class FakeEventHubSender(string eventHubName) : IEventHubSender
{
    private readonly Channel<(EventData EventData, string? PartitionKey)> sent = Channel.CreateUnbounded<(EventData, string?)>();

    public string EventHubName { get; } = eventHubName;

    /// <summary>
    /// Gets or sets an error that sends fail with.
    /// </summary>
    public Exception? SendError { get; set; }

    /// <summary>
    /// Gets whether the sender was disposed, i.e. its connection closed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    public Task SendAsync(
        EventData eventData,
        string? partitionKey,
        CancellationToken cancellationToken)
    {
        if (SendError is { } error)
        {
            return Task.FromException(error);
        }

        sent.Writer.TryWrite((eventData, partitionKey));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the next event sent.
    /// </summary>
    /// <returns>The event and its partition key.</returns>
    public async Task<(EventData EventData, string? PartitionKey)> ReceiveAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        return await sent.Reader.ReadAsync(timeout.Token);
    }

    /// <summary>
    /// Gets whether an event was sent that no test received yet.
    /// </summary>
    /// <returns><see langword="true"/> when an event is waiting.</returns>
    public bool HasPending()
        => sent.Reader.TryPeek(out _);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}