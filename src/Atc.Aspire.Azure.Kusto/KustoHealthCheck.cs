namespace Atc.Aspire.Azure.Kusto;

internal sealed class KustoHealthCheck : IHealthCheck
{
    private readonly ICslQueryProvider queryProvider;
    private readonly string? dataBaseName;

    public KustoHealthCheck(
        ICslQueryProvider queryProvider,
        string? dataBaseName)
    {
        ArgumentNullException.ThrowIfNull(queryProvider);
        this.queryProvider = queryProvider;
        this.dataBaseName = dataBaseName;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientRequestProperties = new ClientRequestProperties { ClientRequestId = Guid.NewGuid().ToString() };

            using (var reader = await queryProvider
                       .ExecuteQueryAsync(
                           databaseName: null,
                           query: ".show databases;",
                           clientRequestProperties,
                           cancellationToken)
                       .ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    var databaseName = reader.GetString(0);
                    if (databaseName.Equals(dataBaseName, StringComparison.OrdinalIgnoreCase))
                    {
                        return HealthCheckResult.Healthy();
                    }
                }
            }

            return new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}