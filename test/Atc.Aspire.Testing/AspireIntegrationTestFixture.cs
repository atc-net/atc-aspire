namespace Atc.Aspire.Testing;

public sealed class AspireIntegrationTestFixture<TEntryPoint>() : DistributedApplicationFactory(typeof(TEntryPoint), []), IAsyncLifetime
    where TEntryPoint : class
{
    public ResourceNotificationService ResourceNotificationService => App.Services.GetRequiredService<ResourceNotificationService>();

    public DistributedApplication App { get; private set; } = null!;

    protected override void OnBuilt(DistributedApplication application)
    {
        App = application;
        base.OnBuilt(application);
    }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.AddLogging(builder =>
            {
                builder.AddXUnit();

                builder.SetMinimumLevel(Environment.GetEnvironmentVariable("RUNNER_DEBUG") is (not null) or "1"
                    ? LogLevel.Trace
                    : LogLevel.Information);
            })
            .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        base.OnBuilderCreated(applicationBuilder);
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (Exception)
        {
            // Ignore exceptions during disposal
        }
    }

    public Task InitializeAsync()
        => StartAsync().WaitAsync(TimeSpan.FromMinutes(10));

    async Task IAsyncLifetime.DisposeAsync() =>
        await DisposeAsync();
}