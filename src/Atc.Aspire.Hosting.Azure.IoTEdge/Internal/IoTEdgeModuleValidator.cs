namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Checks that the resources attached as IoT Edge modules match the deployment manifest and can act as IoT Edge modules.
/// </summary>
internal static class IoTEdgeModuleValidator
{
    // Library keys in project.assets.json are "<name>/<version>". The slash matches "Microsoft.Azure.Devices.Client/1.42.3"
    // but not another package whose name starts the same, e.g. "Microsoft.Azure.Devices.Client.Extensions/1.0.0".
    private const string DeviceSdkLibraryPrefix = "Microsoft.Azure.Devices.Client/";

    /// <summary>
    /// Gets the module ids that the deployment manifest doesn't declare. Modules in the manifest without a resource are fine.
    /// </summary>
    /// <param name="deploymentManifest">The deployment manifest.</param>
    /// <param name="moduleIds">The module ids attached to the device.</param>
    /// <returns>The missing module ids, or an empty list when the manifest declares no modules.</returns>
    public static IReadOnlyList<string> GetModulesMissingFromManifest(
        DeploymentManifest deploymentManifest,
        IEnumerable<string> moduleIds)
    {
        ArgumentNullException.ThrowIfNull(deploymentManifest);
        ArgumentNullException.ThrowIfNull(moduleIds);

        if (deploymentManifest.ModuleIds.Count == 0)
        {
            return [];
        }

        var declared = deploymentManifest.ModuleIds.ToHashSet(StringComparer.Ordinal);
        return moduleIds
            .Where(x => !declared.Contains(x))
            .ToList();
    }

    /// <summary>
    /// Gets the image the deployment manifest declares for a module, when its repository differs from the container's image.
    /// </summary>
    /// <param name="deploymentManifest">The deployment manifest.</param>
    /// <param name="moduleId">The module id.</param>
    /// <param name="containerImageReference">The image the container resource runs.</param>
    /// <returns>
    /// The manifest image when it differs; otherwise <see langword="null"/>, also when the manifest has no image for the module
    /// or only a template placeholder.
    /// </returns>
    public static string? GetDifferingManifestImage(
        DeploymentManifest deploymentManifest,
        string moduleId,
        string containerImageReference)
    {
        ArgumentNullException.ThrowIfNull(deploymentManifest);
        ArgumentException.ThrowIfNullOrEmpty(moduleId);
        ArgumentException.ThrowIfNullOrEmpty(containerImageReference);

        if (!deploymentManifest.ModuleImages.TryGetValue(moduleId, out var manifestImage) ||
            string.IsNullOrWhiteSpace(manifestImage) ||
            ContainerImageName.IsPlaceholder(manifestImage))
        {
            return null;
        }

        return string.Equals(
            ContainerImageName.GetRepository(manifestImage),
            ContainerImageName.GetRepository(containerImageReference),
            StringComparison.Ordinal)
            ? null
            : manifestImage;
    }

    /// <summary>
    /// Gets whether a project references the IoT device SDK (Microsoft.Azure.Devices.Client), directly or transitively,
    /// by reading the restore output in <c>obj/project.assets.json</c>.
    /// </summary>
    /// <param name="projectPath">The path of the project file.</param>
    /// <returns>
    /// Whether the project references the SDK, or <see langword="null"/> when it can't be determined,
    /// e.g. when the project isn't restored or uses another intermediate output path.
    /// </returns>
    public static bool? ReferencesDeviceSdk(string projectPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (projectDirectory is null)
        {
            return null;
        }

        var assetsFile = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(assetsFile);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("libraries", out var libraries) ||
                libraries.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return libraries
                .EnumerateObject()
                .Any(x => x.Name.StartsWith(DeviceSdkLibraryPrefix, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}