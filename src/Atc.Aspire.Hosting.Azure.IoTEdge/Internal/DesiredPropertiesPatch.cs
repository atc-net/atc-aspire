namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Builds the twin patch that makes the desired properties of a module twin equal to those in a deployment manifest.
/// A deployment overwrites the desired properties of a module twin, but a twin update merges them,
/// so the patch sets each property the manifest doesn't have to <see langword="null"/>, which removes it from the twin.
/// </summary>
internal static class DesiredPropertiesPatch
{
    /// <summary>
    /// Creates the patch.
    /// </summary>
    /// <param name="currentDesiredProperties">The desired properties in the module twin now, as JSON.</param>
    /// <param name="desiredProperties">The desired properties from the deployment manifest, as JSON.</param>
    /// <returns>The patch, as JSON.</returns>
    public static string Create(
        string currentDesiredProperties,
        string desiredProperties)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentDesiredProperties);
        ArgumentException.ThrowIfNullOrEmpty(desiredProperties);

        var patch = JsonNode.Parse(desiredProperties)!.AsObject();
        if (JsonNode.Parse(currentDesiredProperties) is JsonObject current)
        {
            RemoveMissingProperties(current, patch);
        }

        return patch.ToJsonString();
    }

    private static void RemoveMissingProperties(
        JsonObject current,
        JsonObject patch)
    {
        foreach (var (name, currentValue) in current)
        {
            // $metadata and $version are kept by IoT Hub, not set by the manifest.
            if (name.StartsWith('$'))
            {
                continue;
            }

            if (!patch.TryGetPropertyValue(name, out var value))
            {
                patch[name] = null;
            }
            else if (currentValue is JsonObject currentObject &&
                     value is JsonObject patchObject)
            {
                RemoveMissingProperties(currentObject, patchObject);
            }
        }
    }
}