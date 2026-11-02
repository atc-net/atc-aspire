namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// The parts of an IoT Hub service connection string that the bridge needs: a shared access policy of the hub,
/// not a device or module key.
/// </summary>
/// <param name="HostName">The IoT Hub host name, e.g. <c>myhub.azure-devices.net</c>.</param>
/// <param name="SharedAccessKeyName">The name of the shared access policy, e.g. <c>iothubowner</c>.</param>
/// <param name="SharedAccessKey">The key of the shared access policy.</param>
internal sealed record IoTHubConnectionInfo(
    string HostName,
    string SharedAccessKeyName,
    string SharedAccessKey)
{
    /// <summary>
    /// Gets the IoT Hub name: the first label of the host name.
    /// </summary>
    public string HubName => HostName.Split('.')[0];

    /// <summary>
    /// Reads an IoT Hub service connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection info.</returns>
    /// <exception cref="InvalidOperationException">A part is missing, or the key isn't base64; the message never contains the key.</exception>
    public static IoTHubConnectionInfo Parse(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .GroupBy(x => x[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (parts.ContainsKey("DeviceId"))
        {
            throw new InvalidOperationException(
                "The IoT Hub connection string is a device connection string. The bridge needs a shared access policy of the hub with service connect permission, e.g. iothubowner or service.");
        }

        var key = GetRequired(parts, "SharedAccessKey");

        // Checked here, so a key damaged by copying fails with this message instead of .NET's base64 error when signing.
        if (!Convert.TryFromBase64String(key, new byte[key.Length], out _))
        {
            throw new InvalidOperationException(
                "The IoT Hub connection string's SharedAccessKey isn't valid base64. Copy the connection string again from the IoT Hub's shared access policies.");
        }

        return new IoTHubConnectionInfo(
            GetRequired(parts, "HostName"),
            GetRequired(parts, "SharedAccessKeyName"),
            key);
    }

    // The key is left out, so the connection string can be logged or shown without it.
    public override string ToString()
        => $"HostName={HostName};SharedAccessKeyName={SharedAccessKeyName}";

    private static string GetRequired(
        Dictionary<string, string> parts,
        string name)
        => parts.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"The IoT Hub connection string has no {name}.");
}