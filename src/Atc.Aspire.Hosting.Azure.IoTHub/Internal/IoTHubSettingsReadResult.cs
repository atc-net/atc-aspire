namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The settings of an IoT Hub, or why they couldn't be read, e.g. no Azure sign-in.
/// </summary>
/// <param name="Settings">The settings, or <see langword="null"/> when they couldn't be read.</param>
/// <param name="NotVerifiedReason">Why the settings couldn't be read, or <see langword="null"/>.</param>
internal sealed record IoTHubSettingsReadResult(
    IoTHubSettings? Settings,
    string? NotVerifiedReason)
{
    public static IoTHubSettingsReadResult Read(IoTHubSettings settings)
        => new(settings, NotVerifiedReason: null);

    public static IoTHubSettingsReadResult NotVerified(string reason)
        => new(Settings: null, reason);
}