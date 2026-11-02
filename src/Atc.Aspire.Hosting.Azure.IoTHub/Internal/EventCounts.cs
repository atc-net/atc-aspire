namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The counts of one kind of event.
/// </summary>
/// <param name="Forwarded">The events sent to the Event Hub.</param>
/// <param name="Skipped">The events of devices the bridge doesn't forward for.</param>
/// <param name="Failed">The events the Event Hub didn't accept.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EventCounts(
    long Forwarded,
    long Skipped,
    long Failed)
{
    public override string ToString()
        => $"{Forwarded} forwarded, {Skipped} skipped, {Failed} failed";
}