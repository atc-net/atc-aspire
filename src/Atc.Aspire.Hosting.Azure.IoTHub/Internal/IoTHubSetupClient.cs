namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// What the bridge needs to check and set up an IoT Hub; Azure Resource Manager and the dashboard by default, fakes in tests.
/// </summary>
/// <param name="ReadSettingsQuietly">
/// Reads a hub's settings by name with the existing Azure sign-in only, never a browser, for the check at start, which nobody asked for.
/// </param>
/// <param name="ReadSettingsSigningIn">
/// Reads a hub's settings by name, signing in with the browser if there's no existing sign-in, for the setup, which the developer started.
/// </param>
/// <param name="Apply">Adds the missing parts to a hub by name, for the bridge's consumer group.</param>
/// <param name="Confirm">
/// Asks the developer to confirm, with a title and a Markdown message;
/// throws <see cref="InvalidOperationException"/> when nobody can be asked.
/// </param>
internal sealed record IoTHubSetupClient(
    Func<string, CancellationToken, Task<IoTHubSettingsReadResult>> ReadSettingsQuietly,
    Func<string, CancellationToken, Task<IoTHubSettingsReadResult>> ReadSettingsSigningIn,
    Func<string, IReadOnlyList<IoTHubSetupItem>, string, CancellationToken, Task> Apply,
    Func<string, string, CancellationToken, Task<bool>> Confirm);