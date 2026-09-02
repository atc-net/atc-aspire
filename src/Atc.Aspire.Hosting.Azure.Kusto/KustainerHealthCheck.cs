namespace Atc.Aspire.Hosting.Azure.Kusto;

/// <summary>
/// A health check to validate that the Kustainer (Kusto emulator) service is available and responsive.
/// </summary>
internal sealed class KustainerHealthCheck : IHealthCheck
{
    private readonly KustoConnectionStringBuilder kcsb;
    private readonly bool isClusterCheck;

    private static readonly ClientRequestProperties DefaultClientRequestProperties = GetClientRequestProperties();

    public KustainerHealthCheck(
        KustoConnectionStringBuilder connectionStringBuilder,
        bool isClusterCheck)
    {
        ArgumentNullException.ThrowIfNull(connectionStringBuilder);

        this.kcsb = connectionStringBuilder;
        this.isClusterCheck = isClusterCheck;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return isClusterCheck
                ? await CheckClusterHealthAsync().ConfigureAwait(false)
                : await CheckDatabaseHealthAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }

    private static ClientRequestProperties GetClientRequestProperties()
    {
        var clientRequestProps = new ClientRequestProperties();
        clientRequestProps.SetOption("client_timeout", TimeSpan.FromSeconds(30));
        return clientRequestProps;
    }

    private async Task<HealthCheckResult> CheckClusterHealthAsync()
    {
        using var adminProvider = KustoClientFactory.CreateCslAdminProvider(kcsb);

        var results = await adminProvider.ExecuteControlCommandAsync<string>(".show version", DefaultClientRequestProperties).ConfigureAwait(false);
        return results.Any()
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }

    private async Task<HealthCheckResult> CheckDatabaseHealthAsync(
        CancellationToken cancellationToken)
    {
        const string query = "print message = \"Hello, World!\"";

        using var client = KustoClientFactory.CreateCslQueryProvider(kcsb);
        using var reader = await client.ExecuteQueryAsync(client.DefaultDatabaseName, query, DefaultClientRequestProperties, cancellationToken).ConfigureAwait(false);
        return reader.Read()
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}