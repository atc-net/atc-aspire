namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// The configuration edgeHub reads from the desired properties of its <c>$edgeHub</c> twin: the schema version,
/// the routes and the store and forward settings. IoT Hub only lets a deployment write the twins of the reserved
/// <c>$edgeAgent</c> and <c>$edgeHub</c> modules, so it is applied as a single-device deployment, like
/// <c>az iot edge set-modules</c>.
/// </summary>
internal static class EdgeHubConfiguration
{
    /// <summary>
    /// The configuration without a deployment manifest: every message goes upstream, and messages that can't be delivered
    /// are kept for two hours, the IoT Edge default.
    /// </summary>
    public const string Default =
        """
        {
          "schemaVersion": "1.1",
          "routes": {
            "upstream": "FROM /messages/* INTO $upstream"
          },
          "storeAndForwardConfiguration": {
            "timeToLiveSecs": 7200
          }
        }
        """;

    /// <summary>
    /// The <c>$edgeAgent</c> part of the deployment. A deployment needs it, but edgeAgent doesn't run here: Aspire decides
    /// which modules run, so only the system modules are listed.
    /// </summary>
    public const string EdgeAgent =
        """
        {
          "schemaVersion": "1.1",
          "runtime": {
            "type": "docker",
            "settings": {}
          },
          "systemModules": {
            "edgeAgent": {
              "type": "docker",
              "settings": {
                "image": "mcr.microsoft.com/azureiotedge-agent:1.6"
              }
            },
            "edgeHub": {
              "type": "docker",
              "status": "running",
              "restartPolicy": "always",
              "settings": {
                "image": "mcr.microsoft.com/azureiotedge-hub:1.6"
              }
            }
          },
          "modules": {}
        }
        """;

    private const string SchemaVersionPropertyName = "schemaVersion";
    private const string PropertiesDesired = "properties.desired";

    /// <summary>
    /// Gets whether the configuration has a schema version. edgeHub rejects a configuration without one and runs without routes.
    /// </summary>
    /// <param name="desiredProperties">The desired properties of the <c>$edgeHub</c> twin, as JSON.</param>
    /// <returns><see langword="true"/> when the configuration has a schema version; otherwise <see langword="false"/>.</returns>
    public static bool HasSchemaVersion(string desiredProperties)
    {
        ArgumentException.ThrowIfNullOrEmpty(desiredProperties);

        return JsonNode.Parse(desiredProperties) is JsonObject configuration &&
               configuration[SchemaVersionPropertyName] is JsonValue schemaVersion &&
               schemaVersion.TryGetValue<string>(out var version) &&
               !string.IsNullOrWhiteSpace(version);
    }

    /// <summary>
    /// Creates the single-device deployment that sets the edgeHub configuration.
    /// </summary>
    /// <param name="desiredProperties">The desired properties of the <c>$edgeHub</c> twin, as JSON.</param>
    /// <returns>The deployment content with the <c>$edgeAgent</c> and <c>$edgeHub</c> desired properties.</returns>
    public static ConfigurationContent CreateDeploymentContent(
        string desiredProperties)
    {
        ArgumentException.ThrowIfNullOrEmpty(desiredProperties);

        // The IoT Hub SDK serializes the content with Newtonsoft.Json, so the desired properties are JObjects.
        return new ConfigurationContent
        {
            ModulesContent = new Dictionary<string, IDictionary<string, object>>(StringComparer.Ordinal)
            {
                [EdgeAgentConstants.ModuleId] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [PropertiesDesired] = Newtonsoft.Json.Linq.JObject.Parse(EdgeAgent),
                },
                [EdgeHubConstants.ModuleId] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [PropertiesDesired] = Newtonsoft.Json.Linq.JObject.Parse(desiredProperties),
                },
            },
        };
    }
}