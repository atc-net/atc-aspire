namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Counts the events the bridge forwarded, skipped and failed to forward, per kind of event.
/// </summary>
internal sealed class EventBridgeCounters
{
    private readonly long[] forwarded = new long[Enum.GetValues<IoTHubEventSource>().Length];
    private readonly long[] skipped = new long[Enum.GetValues<IoTHubEventSource>().Length];
    private readonly long[] failed = new long[Enum.GetValues<IoTHubEventSource>().Length];
    private int changed;

    public void AddForwarded(IoTHubEventSource source)
        => Add(forwarded, source);

    public void AddSkipped(IoTHubEventSource source)
        => Add(skipped, source);

    public void AddFailed(IoTHubEventSource source)
        => Add(failed, source);

    public EventCounts Get(IoTHubEventSource source)
        => new(
            Interlocked.Read(ref forwarded[(int)source]),
            Interlocked.Read(ref skipped[(int)source]),
            Interlocked.Read(ref failed[(int)source]));

    /// <summary>
    /// Gets whether a count changed since the last call.
    /// </summary>
    /// <returns><see langword="true"/> when a count changed.</returns>
    public bool TakeChanged()
        => Interlocked.Exchange(ref changed, 0) == 1;

    private void Add(
        long[] counts,
        IoTHubEventSource source)
    {
        // The count goes up before the flag, so a timer that sees the flag always reads the new count.
        Interlocked.Increment(ref counts[(int)source]);
        Interlocked.Exchange(ref changed, 1);
    }
}