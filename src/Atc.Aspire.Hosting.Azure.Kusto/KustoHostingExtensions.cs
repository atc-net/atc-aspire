namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Kusto to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class KustoHostingExtensions
{
    /// <summary>
    /// Adds a Kusto emulator resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the emulator resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTP port number for the Kusto emulator container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<KustoContainerResource> AddKustainer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name = "kusto-emulator",
        int? httpPort = null)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));

        return builder.AddKustainer(name, KustoContainerImageType.MarinerLinux, httpPort);
    }

    /// <summary>
    /// Adds a Kusto emulator resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the emulator resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="imageType">The <see cref="KustoContainerImageType"/> to specify the container image.</param>
    /// <param name="httpPort">The HTTP port number for the Kusto emulator container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<KustoContainerResource> AddKustainer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        KustoContainerImageType imageType,
        int? httpPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));

        var resource = new KustoContainerResource(name);

        var (image, tag) = GetImageAndTag(imageType);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(image)
            .WithImageTag(tag)
            .WithImageRegistry(KustoContainerImageTags.Registry)
            .WithHttpEndpoint(
                port: httpPort,
                targetPort: KustoContainerResource.DefaultHttpPort,
                name: KustoContainerResource.HttpEndpointName)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("-m", "4G")
            .ExcludeFromManifest()
            .WithKustoDefaults();

        ICslQueryProvider? queryProvider = null;

        resourceBuilder.ApplicationBuilder.Eventing.Subscribe<ConnectionStringAvailableEvent>(
            resource,
            async (_, ct) =>
            {
                var connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct) ??
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");

                queryProvider = CreateCslQueryProvider(connectionString);
            });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
         .Add(new HealthCheckRegistration(
             healthCheckKey,
             _ => new KustoHealthCheck(queryProvider!, KustoContainerResource.DefaultDbName),
             failureStatus: default,
             tags: default,
             timeout: default));

        return resourceBuilder.WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Kusto container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<KustoContainerResource> WithDataVolume(
        this IResourceBuilder<KustoContainerResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"), "/kusto/tmp/Kusto.Personal");
    }

    /// <summary>
    /// Determines the image and tag based on the provided <see cref="KustoContainerImageType"/> type.
    /// </summary>
    /// <param name="imageType">The container image type.</param>
    /// <returns>Tuple containing the image and tag.</returns>
    private static (string Image, string Tag) GetImageAndTag(KustoContainerImageType imageType)
        => imageType switch
        {
            KustoContainerImageType.MarinerLinux => (KustoContainerImageTags.MarinerLinuxImage, KustoContainerImageTags.DefaultTag),
            _ => throw new ArgumentOutOfRangeException(nameof(imageType), imageType, "Unknown Kusto container image type."),
        };

    private static IResourceBuilder<KustoContainerResource> WithKustoDefaults(
        this IResourceBuilder<KustoContainerResource> builder) =>
        builder.WithOtlpExporter();

    internal static ICslQueryProvider CreateCslQueryProvider(string? connectionString)
    {
        if (connectionString is null)
        {
            throw new InvalidOperationException("Connection string is unavailable");
        }

        return KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(connectionString));
    }
}