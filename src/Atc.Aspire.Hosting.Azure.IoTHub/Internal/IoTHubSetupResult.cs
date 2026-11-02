namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The outcome of checking an IoT Hub's setup for the bridge.
/// </summary>
/// <param name="Missing">What the bridge needs and the hub doesn't have.</param>
/// <param name="BlockedReason">
/// Why setup must not change the hub, e.g. because others read its built-in endpoint, or <see langword="null"/>.
/// </param>
internal sealed record IoTHubSetupResult(
    IReadOnlyList<IoTHubSetupItem> Missing,
    string? BlockedReason)
{
    /// <summary>
    /// Gets whether the hub has everything the bridge needs.
    /// </summary>
    public bool IsComplete
        => Missing.Count == 0;
}