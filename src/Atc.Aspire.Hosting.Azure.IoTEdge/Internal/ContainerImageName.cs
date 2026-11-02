namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Compares container image references by repository, ignoring registry, tag and digest.
/// </summary>
internal static class ContainerImageName
{
    private const string DockerHubLibraryPrefix = "library/";

    /// <summary>
    /// Gets the repository of an image reference in lower case,
    /// e.g. <c>iotedge/opc-publisher</c> for <c>mcr.microsoft.com/iotedge/opc-publisher:2.9.18</c>.
    /// </summary>
    /// <param name="imageReference">The image reference.</param>
    /// <returns>The repository.</returns>
    public static string GetRepository(string imageReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageReference);

        var reference = imageReference.Trim();

        var digestSeparator = reference.IndexOf('@', StringComparison.Ordinal);
        if (digestSeparator >= 0)
        {
            reference = reference[..digestSeparator];
        }

        var tagSeparator = reference.LastIndexOf(':');
        if (tagSeparator > reference.LastIndexOf('/'))
        {
            reference = reference[..tagSeparator];
        }

        // The first path segment is a registry when it looks like a host name, e.g. mcr.microsoft.com or localhost:5000.
        var firstSlash = reference.IndexOf('/', StringComparison.Ordinal);
        if (firstSlash > 0)
        {
            var firstSegment = reference[..firstSlash];
            if (firstSegment.Contains('.', StringComparison.Ordinal) ||
                firstSegment.Contains(':', StringComparison.Ordinal) ||
                firstSegment.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                reference = reference[(firstSlash + 1)..];
            }
        }

        if (reference.StartsWith(DockerHubLibraryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            reference = reference[DockerHubLibraryPrefix.Length..];
        }

        return reference.ToLowerInvariant();
    }

    /// <summary>
    /// Gets whether an image in a deployment template is a placeholder that the build tooling fills in,
    /// e.g. <c>${MODULES.simulationmodule}</c>.
    /// </summary>
    /// <param name="image">The image in the deployment template.</param>
    /// <returns><see langword="true"/> for a placeholder.</returns>
    public static bool IsPlaceholder(string image)
        => image.Contains("${", StringComparison.Ordinal);

    /// <summary>
    /// Gets the full image reference of a container resource.
    /// </summary>
    /// <param name="image">The container image annotation.</param>
    /// <returns>The image reference, e.g. <c>mcr.microsoft.com/iotedge/opc-publisher:2.9.18</c>.</returns>
    public static string GetReference(ContainerImageAnnotation image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var reference = string.IsNullOrEmpty(image.Registry)
            ? image.Image
            : $"{image.Registry}/{image.Image}";

        return string.IsNullOrEmpty(image.Tag)
            ? reference
            : $"{reference}:{image.Tag}";
    }
}