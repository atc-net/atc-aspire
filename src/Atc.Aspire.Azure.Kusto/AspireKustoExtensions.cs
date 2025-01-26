namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering kusto-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireKustoExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Kusto:Client";

    /// <summary>
    /// Registers <see cref="IKustoProcessor"/> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read configuration from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureOptions">
    /// An optional method that can be used for customizing the <see cref="AspireKustoSettings"/>. It is invoked after the settings are bound.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a valid host address or database name cannot be found in the configuration.
    /// </exception>
    public static void ConfigureAzureDataExplorer(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<AspireKustoSettings>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        ConfigureAzureDataExplorer(builder, DefaultConfigSectionName, configureOptions, connectionName);
    }

    private static void ConfigureAzureDataExplorer(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<AspireKustoSettings>? configureOptions,
        string connectionName)
    {
        var options = new AspireKustoSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(options);

        if (builder.Configuration.GetConnectionString(connectionName) is { } connectionString)
        {
            if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            {
                options.HostAddress = uri;
            }
            else
            {
                throw new InvalidOperationException($"Invalid connection string for Kusto: {connectionString}");
            }
        }

        configureOptions?.Invoke(options);

        if (options.HostAddress is null)
        {
            throw new InvalidOperationException(
                $"KustoProcessor could not be configured. Ensure a valid host address is provided in 'ConnectionStrings:{connectionName}' " +
                $"or in the '{configurationSectionName}' configuration section.");
        }

        if (string.IsNullOrEmpty(options.DatabaseName))
        {
            throw new InvalidOperationException(
                $"KustoProcessor could not be configured. Ensure a valid database name is provided in 'ConnectionStrings:{connectionName}' " +
                $"or in the '{configurationSectionName}' configuration section.");
        }

        builder.Services.ConfigureAzureDataExplorer(o =>
        {
            o.HostAddress = options.HostAddress.AbsoluteUri;
            o.DatabaseName = options.DatabaseName;
            o.Credential = options.Credential;
        });

        if (!options.DisableHealthChecks)
        {
            var healthCheckName = $"Kusto_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new KustoHealthCheck(sp.GetRequiredService<ICslQueryProvider>(), options.DatabaseName),
                failureStatus: null,
                tags: null,
                timeout: options.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(options.HealthCheckTimeout.Value) : null));
        }
    }
}