namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Checks JSON entered as a command argument in the dashboard, the CLI or an MCP client.
/// </summary>
internal static class JsonArgument
{
    /// <summary>
    /// Gets what is wrong with the JSON, phrased for display next to the input.
    /// </summary>
    /// <param name="json">The JSON entered.</param>
    /// <param name="requireObject">Whether the JSON must be an object.</param>
    /// <returns>The error, or <see langword="null"/> when the JSON is valid.</returns>
    public static string? GetError(
        string json,
        bool requireObject)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            return requireObject && document.RootElement.ValueKind != JsonValueKind.Object
                ? "Enter a JSON object, e.g. { \"setting\": \"value\" }."
                : null;
        }
        catch (JsonException ex)
        {
            return $"Enter valid JSON. {ex.Message}";
        }
    }
}