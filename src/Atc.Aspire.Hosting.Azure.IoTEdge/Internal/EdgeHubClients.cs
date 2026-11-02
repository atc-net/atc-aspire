namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Reads the client connection states edgeHub reports in the <c>clients</c> section of its twin's reported properties,
/// keyed by <c>&lt;deviceId&gt;/&lt;moduleId&gt;</c> with a <c>status</c> such as <c>Connected</c>.
/// The format isn't a documented contract, so anything unexpected is ignored.
/// </summary>
internal static class EdgeHubClients
{
    public const string ConnectedStatus = "Connected";

    private const string ClientsPropertyName = "clients";
    private const string StatusPropertyName = "status";

    /// <summary>
    /// Gets the connection status of each module of a device.
    /// </summary>
    /// <param name="reportedPropertiesJson">The reported properties of the <c>$edgeHub</c> module twin, as JSON.</param>
    /// <param name="deviceId">The device id.</param>
    /// <returns>The status keyed by module id; empty when edgeHub reports no clients.</returns>
    public static IReadOnlyDictionary<string, string> GetConnectionStates(
        string reportedPropertiesJson,
        string deviceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(reportedPropertiesJson))
        {
            return states;
        }

        try
        {
            using var document = JsonDocument.Parse(reportedPropertiesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(ClientsPropertyName, out var clients) ||
                clients.ValueKind != JsonValueKind.Object)
            {
                return states;
            }

            var prefix = deviceId + "/";
            foreach (var client in clients.EnumerateObject())
            {
                if (!client.Name.StartsWith(prefix, StringComparison.Ordinal) ||
                    client.Value.ValueKind != JsonValueKind.Object ||
                    !client.Value.TryGetProperty(StatusPropertyName, out var status) ||
                    status.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                states[client.Name[prefix.Length..]] = status.GetString()!;
            }
        }
        catch (JsonException)
        {
            // An unexpected format means unknown, not an error.
        }

        return states;
    }
}