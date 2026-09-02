namespace Aspire.Hosting.Utils;

/// <summary>
/// DistributedApplication.CreateBuilder() creates a builder that includes configuration to read from appsettings.json.
/// The builder has a FileSystemWatcher, which can't be cleaned up unless a DistributedApplication is built and disposed.
/// This class wraps the builder and provides a way to automatically dispose it to prevent test failures from excessive
/// FileSystemWatcher instances from many tests.
/// </summary>
public static class TestDistributedApplicationBuilder
{
    public static IDistributedApplicationTestingBuilder Create(
        DistributedApplicationOperation operation)
    {
        var args = operation switch
        {
            DistributedApplicationOperation.Run => (string[])[],
            DistributedApplicationOperation.Publish => ["Publishing:Publisher=manifest"],
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        return Create(args);
    }

    public static IDistributedApplicationTestingBuilder Create(
        params string[] args)
        => CreateCore(args, configureOptions: null);

    public static IDistributedApplicationTestingBuilder Create(
        ITestOutputHelper testOutputHelper,
        params string[] args)
        => CreateCore(args, configureOptions: null, testOutputHelper);

    public static IDistributedApplicationTestingBuilder Create(
        Action<DistributedApplicationOptions>? configureOptions,
        ITestOutputHelper? testOutputHelper = null)
        => CreateCore([], configureOptions, testOutputHelper);

    private static IDistributedApplicationTestingBuilder CreateCore(
        string[] args,
        Action<DistributedApplicationOptions>? configureOptions,
        ITestOutputHelper? testOutputHelper = null)
    {
        var appAssembly = typeof(TestDistributedApplicationBuilder).Assembly;
        var assemblyName = appAssembly.FullName;

        var builder = DistributedApplicationTestingBuilder.Create(args, Configure);

        builder.Services.AddHttpClient();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        if (testOutputHelper is not null)
        {
            WithTestAndResourceLogging(builder, testOutputHelper);
        }

        return builder;

        void Configure(
            DistributedApplicationOptions applicationOptions,
            HostApplicationBuilderSettings hostBuilderOptions)
        {
            hostBuilderOptions.EnvironmentName = Environments.Development;
            hostBuilderOptions.ApplicationName = appAssembly.GetName().Name;
            applicationOptions.AssemblyName = assemblyName;
            applicationOptions.DisableDashboard = true;
            var cfg = hostBuilderOptions.Configuration ??= new();
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DcpPublisher:RandomizePorts"] = "true",
                ["DcpPublisher:DeleteResourcesOnShutdown"] = "true",
                ["DcpPublisher:ResourceNameSuffix"] = $"{Random.Shared.Next():x}",
            });

            configureOptions?.Invoke(applicationOptions);
        }
    }

    private static void WithTestAndResourceLogging(
        IDistributedApplicationTestingBuilder builder,
        ITestOutputHelper testOutputHelper)
    {
        builder.Services.AddHostedService<ResourceLoggerForwarderService>();
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddXUnit(testOutputHelper);
            loggingBuilder.AddFilter("Aspire.Hosting", LogLevel.Trace);
            loggingBuilder.AddFilter("Aspire.CommunityToolkit.Hosting", LogLevel.Trace);
        });
    }
}