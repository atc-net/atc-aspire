namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Kusto to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static partial class KustoHostingExtensions
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating database '{DatabaseName}'")]
    private static partial void LogCreatingDatabase(ILogger logger, string databaseName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database '{DatabaseName}' created successfully")]
    private static partial void LogDatabaseCreated(ILogger logger, string databaseName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Executing custom creation script for database '{DatabaseName}'")]
    private static partial void LogExecutingCreationScript(ILogger logger, string databaseName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed custom creation script for database '{DatabaseName}'")]
    private static partial void LogCompletedCreationScript(ILogger logger, string databaseName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create database '{DatabaseName}'")]
    private static partial void LogFailedToCreateDatabase(ILogger logger, Exception exception, string databaseName);

    /// <summary>
    /// Adds a Kustainer (Kusto emulator) resource to the application model.
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
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.AddKustainer(name, KustoContainerImageType.MarinerLinux, httpPort);
    }

    /// <summary>
    /// Adds a Kustainer (Kusto emulator) resource to the application model.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

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
            .WithIconName("DatabaseMultiple")
            .ExcludeFromManifest()
            .WithKustoDefaults();

        AddKustoHealthChecksAndDatabaseManagement(resourceBuilder);
        AddKustoCustomCommands(resourceBuilder);

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a Kusto read-write database to the application model.
    /// </summary>
    /// <param name="builder">The Kusto emulator resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="databaseName">The name of the database. If not provided, this defaults to the same value as <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<KustoDatabaseResource> AddDatabase(
        this IResourceBuilder<KustoContainerResource> builder,
        [ResourceName] string name,
        string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Use the resource name as the database name if it's not provided.
        databaseName ??= name;

        var kustoDatabase = new KustoDatabaseResource(name, databaseName, builder.Resource);
        builder.Resource.Databases.Add(kustoDatabase);

        var resourceBuilder = builder.ApplicationBuilder
            .AddResource(kustoDatabase)
            .WithIconName("Database");

        // Register a health check that will be used to verify the database is available.
        KustoConnectionStringBuilder? kcsb = null;
        resourceBuilder.OnConnectionStringAvailable(async (db, _, ct) =>
        {
            var connectionString = await db.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false) ??
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent published for resource '{db.Name}', but the connection string was null.");

            kcsb = new KustoConnectionStringBuilder(connectionString);
        });

        var healthCheckKey = $"{kustoDatabase.Name}_check";
        resourceBuilder.ApplicationBuilder
            .Services
            .AddHealthChecks()
            .AddKustoHealthCheck(healthCheckKey, isCluster: false, _ => kcsb!);

        return resourceBuilder.WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Defines the script used to create the database.
    /// </summary>
    /// <remarks>
    /// This script is executed against the Kustainer emulator when the database becomes available.
    /// </remarks>
    /// <param name="builder">The resource builder to configure.</param>
    /// <param name="script">KQL script to create databases, tables, or data.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<KustoDatabaseResource> WithCreationScript(
        this IResourceBuilder<KustoDatabaseResource> builder,
        string script)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(script);

        builder.WithAnnotation(new KustoCreateDatabaseScriptAnnotation(script));

        return builder;
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

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/kusto/tmp/Kusto.Personal");
    }

    /// <summary>
    /// Determines the image and tag based on the provided <see cref="KustoContainerImageType"/> type.
    /// </summary>
    /// <param name="imageType">The container image type.</param>
    /// <returns>Tuple containing the image and tag.</returns>
    private static (string Image, string Tag) GetImageAndTag(
        KustoContainerImageType imageType)
        => imageType switch
        {
            KustoContainerImageType.MarinerLinux => (KustoContainerImageTags.MarinerLinuxImage, KustoContainerImageTags.DefaultTag),
            _ => throw new ArgumentOutOfRangeException(nameof(imageType), imageType, "Unknown Kusto container image type."),
        };

    private static IResourceBuilder<KustoContainerResource> WithKustoDefaults(
        this IResourceBuilder<KustoContainerResource> builder) =>
        builder.WithOtlpExporter();

    /// <summary>
    /// Registers the cluster health check and drives automatic database creation once the emulator is ready.
    /// </summary>
    private static void AddKustoHealthChecksAndDatabaseManagement(
        IResourceBuilder<KustoContainerResource> resourceBuilder)
    {
        var resource = resourceBuilder.Resource;

        KustoConnectionStringBuilder? kcsb = null;
        resourceBuilder.OnConnectionStringAvailable(async (res, _, ct) =>
        {
            var connectionString = await res.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false) ??
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent published for resource '{res.Name}', but the connection string was null.");

            kcsb = new KustoConnectionStringBuilder(connectionString);
        });

        var healthCheckKey = $"{resource.Name}_check";
        resourceBuilder.ApplicationBuilder
            .Services
            .AddHealthChecks()
            .AddKustoHealthCheck(healthCheckKey, isCluster: true, _ => kcsb!);

        // Create any databases now that the emulator is ready.
        resourceBuilder.OnResourceReady(async (res, evt, ct) =>
        {
            if (kcsb is null)
            {
                throw new DistributedApplicationException($"Connection string for Kusto resource '{res.Name}' is not set.");
            }

            using var adminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);
            foreach (var kustoDatabase in res.Databases)
            {
                await CreateDatabaseAsync(adminProvider, kustoDatabase, evt.Services, ct).ConfigureAwait(false);
            }
        });

        resourceBuilder.WithHealthCheck(healthCheckKey);
    }

    private static async Task CreateDatabaseAsync(
        ICslAdminProvider adminProvider,
        KustoDatabaseResource databaseResource,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var crp = new ClientRequestProperties
        {
            ClientRequestId = Guid.NewGuid().ToString(),
        };
        crp.SetParameter(ClientRequestProperties.OptionQueryConsistency, ClientRequestProperties.OptionQueryConsistency_Strong);

        var logger = serviceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(databaseResource);
        var rns = serviceProvider.GetRequiredService<ResourceNotificationService>();

        var databaseName = databaseResource.DatabaseName;
        var customScript = databaseResource.GetCreationScript();

        LogCreatingDatabase(logger, databaseName);

        try
        {
            // 1. Always ensure the database exists.
            var createDatabaseCommand = KustoEmulatorContainerDefaults.DefaultCreateDatabaseCommand(databaseName);
            await KustoEmulatorResiliencePipelines.Default.ExecuteAsync(
                async _ => await adminProvider.ExecuteControlCommandAsync(databaseName, createDatabaseCommand, crp).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            LogDatabaseCreated(logger, databaseName);

            // 2. Run the custom creation script (tables, functions, seed data) against the database, if provided.
            if (customScript is not null)
            {
                LogExecutingCreationScript(logger, databaseName);

                await KustoEmulatorResiliencePipelines.Default.ExecuteAsync(
                    async _ => await adminProvider.ExecuteControlCommandAsync(databaseName, customScript, crp).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);

                LogCompletedCreationScript(logger, databaseName);
            }
        }
        catch (Exception e)
        {
            LogFailedToCreateDatabase(logger, e, databaseName);
            await rns.PublishUpdateAsync(databaseResource, state => state with
            {
                State = KnownResourceStates.FailedToStart,
            }).ConfigureAwait(false);
        }
    }

    private static void AddKustoCustomCommands(
        IResourceBuilder<KustoContainerResource> resourceBuilder)
    {
        resourceBuilder.WithCommand(
            name: "open-kusto-explorer-desktop",
            displayName: "Open in Kusto Explorer (Desktop)",
            executeCommand: context => OnOpenInKustoExplorerDesktop(resourceBuilder, context),
            commandOptions: new CommandOptions
            {
                UpdateState = UpdateStateDesktop,
                IconName = "DatabaseSearch",
            });

        resourceBuilder.WithCommand(
            name: "open-kusto-explorer-web",
            displayName: "Open in Kusto Explorer (Web)",
            executeCommand: context => OnOpenInKustoExplorerWeb(resourceBuilder, context),
            commandOptions: new CommandOptions
            {
                UpdateState = UpdateStateWeb,
                IconName = "DatabaseSearch",
            });

        static ResourceCommandState UpdateStateDesktop(
            UpdateCommandStateContext context)
        {
            // The Desktop Kusto.Explorer is only available on Windows, so don't show the command on other platforms.
            if (!OperatingSystem.IsWindows())
            {
                return ResourceCommandState.Hidden;
            }

            return context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                ? ResourceCommandState.Enabled
                : ResourceCommandState.Disabled;
        }

        static ResourceCommandState UpdateStateWeb(
            UpdateCommandStateContext context)
            => context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                ? ResourceCommandState.Enabled
                : ResourceCommandState.Disabled;

        static async Task<ExecuteCommandResult> OnOpenInKustoExplorerDesktop(
            IResourceBuilder<KustoContainerResource> resourceBuilder,
            ExecuteCommandContext context)
        {
            var connectionString = await resourceBuilder
                .Resource
                .ConnectionStringExpression
                .GetValueAsync(context.CancellationToken)
                .ConfigureAwait(false) ??
                throw new DistributedApplicationException($"Connection string for Kusto resource '{resourceBuilder.Resource.Name}' is not set.");

            var launcher = new KustoClientToolLauncher();
            var result = launcher.TryLaunchKustoExplorer(title: string.Empty, resourceBuilder.Resource.Name, connectionString, requestText: string.Empty);

            return result ? CommandResults.Success() : CommandResults.Failure("Failed to launch Kusto Explorer");
        }

        static async Task<ExecuteCommandResult> OnOpenInKustoExplorerWeb(
            IResourceBuilder<KustoContainerResource> resourceBuilder,
            ExecuteCommandContext context)
        {
            var connectionString = await resourceBuilder
                .Resource
                .ConnectionStringExpression
                .GetValueAsync(context.CancellationToken)
                .ConfigureAwait(false) ??
                throw new DistributedApplicationException($"Connection string for Kusto resource '{resourceBuilder.Resource.Name}' is not set.");

            var launcher = new KustoClientToolLauncher();
            launcher.TryLaunchKustoWebExplorer(title: string.Empty, resourceBuilder.Resource.Name, connectionString, requestText: string.Empty);

            return CommandResults.Success();
        }
    }
}