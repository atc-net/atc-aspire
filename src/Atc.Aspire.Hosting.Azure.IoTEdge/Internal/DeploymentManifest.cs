namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// The parts of an IoT Edge deployment manifest (or deployment template) that matter when emulating a device locally:
/// the edgeHub configuration and its routes, the custom modules declared for edgeAgent, and the desired properties for each module twin.
/// </summary>
/// <param name="Routes">The edgeHub routes keyed by route name.</param>
/// <param name="ModuleIds">The custom module ids declared under <c>$edgeAgent.properties.desired.modules</c>.</param>
/// <param name="ModuleImages">The image of each custom module (its <c>settings.image</c>), keyed by module id. Template placeholders are kept as is.</param>
/// <param name="ModuleDesiredProperties">The desired properties (as JSON) for each custom module twin, keyed by module id.</param>
/// <param name="EdgeHubDesiredProperties">
/// The desired properties (as JSON) of the <c>$edgeHub</c> twin, i.e. the edgeHub configuration with the schema version, routes
/// and store and forward settings, if the manifest has them.
/// </param>
internal sealed record DeploymentManifest(
    IReadOnlyDictionary<string, string> Routes,
    IReadOnlyList<string> ModuleIds,
    IReadOnlyDictionary<string, string> ModuleImages,
    IReadOnlyDictionary<string, string> ModuleDesiredProperties,
    string? EdgeHubDesiredProperties)
{
    /// <summary>
    /// Loads and parses a deployment manifest or deployment template file.
    /// </summary>
    /// <param name="filePath">The path to the manifest.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed manifest.</returns>
    public static async Task<DeploymentManifest> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Parse(json);
    }

    /// <summary>
    /// Parses a deployment manifest or deployment template.
    /// </summary>
    /// <param name="json">The manifest JSON.</param>
    /// <returns>The parsed manifest.</returns>
    /// <exception cref="FormatException">The JSON is not a deployment manifest.</exception>
    public static DeploymentManifest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new FormatException("The deployment manifest is not valid JSON.", ex);
        }

        if (root?[PropertyNames.Manifest.ModulesContent] is not JsonObject modulesContent)
        {
            throw new FormatException($"The deployment manifest has no '{PropertyNames.Manifest.ModulesContent}' section.");
        }

        return new DeploymentManifest(
            GetRoutes(modulesContent),
            GetModuleIds(modulesContent),
            GetModuleImages(modulesContent),
            GetModuleDesiredProperties(modulesContent),
            GetEdgeHubDesiredProperties(modulesContent));
    }

    private static string? GetEdgeHubDesiredProperties(
        JsonObject modulesContent)
        => modulesContent[EdgeHubConstants.ModuleId]?[PropertyNames.Manifest.PropertiesDesired] is JsonObject desired
            ? desired.ToJsonString()
            : null;

    private static Dictionary<string, string> GetRoutes(
        JsonObject modulesContent)
    {
        var routes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (modulesContent[EdgeHubConstants.ModuleId]?[PropertyNames.Manifest.PropertiesDesired]?[PropertyNames.Manifest.Routes] is not JsonObject routesNode)
        {
            return routes;
        }

        foreach (var (name, value) in routesNode)
        {
            // A route is either a plain string or an object (schema 1.1+) with "route", "priority" and "timeToLiveSecs".
            var route = value switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
                JsonObject jsonObject => jsonObject[PropertyNames.Manifest.Route]?.GetValue<string>(),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(route))
            {
                routes[name] = route;
            }
        }

        return routes;
    }

    private static List<string> GetModuleIds(JsonObject modulesContent)
    {
        if (modulesContent[EdgeAgentConstants.ModuleId]?[PropertyNames.Manifest.PropertiesDesired]?[PropertyNames.Modules] is not JsonObject modulesNode)
        {
            return [];
        }

        return modulesNode
            .Select(x => x.Key)
            .ToList();
    }

    private static Dictionary<string, string> GetModuleImages(
        JsonObject modulesContent)
    {
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        if (modulesContent[EdgeAgentConstants.ModuleId]?[PropertyNames.Manifest.PropertiesDesired]?[PropertyNames.Modules] is not JsonObject modulesNode)
        {
            return images;
        }

        foreach (var (moduleId, value) in modulesNode)
        {
            if (value?[PropertyNames.ModuleSettings]?[PropertyNames.Manifest.Image] is JsonValue imageValue &&
                imageValue.TryGetValue<string>(out var image) &&
                !string.IsNullOrWhiteSpace(image))
            {
                images[moduleId] = image;
            }
        }

        return images;
    }

    private static Dictionary<string, string> GetModuleDesiredProperties(
        JsonObject modulesContent)
    {
        var desiredProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (moduleId, value) in modulesContent)
        {
            if (moduleId.StartsWith('$') ||
                value?[PropertyNames.Manifest.PropertiesDesired] is not JsonObject desired)
            {
                continue;
            }

            desiredProperties[moduleId] = desired.ToJsonString();
        }

        return desiredProperties;
    }
}