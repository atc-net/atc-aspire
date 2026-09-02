namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kustainer (Kusto emulator) health checks.
/// </summary>
internal static class KustainerHealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a Kustainer health check to the <see cref="IHealthChecksBuilder"/>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The name of the health check.</param>
    /// <param name="isCluster">Whether the health check targets the cluster (<see langword="true"/>) or a database (<see langword="false"/>).</param>
    /// <param name="kcsbFactory">A factory that provides the connection string builder used by the health check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddKustoHealthCheck(
        this IHealthChecksBuilder builder,
        string name,
        bool isCluster,
        Func<IServiceProvider, KustoConnectionStringBuilder> kcsbFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(kcsbFactory);

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new KustainerHealthCheck(kcsbFactory(sp), isCluster),
            failureStatus: null,
            tags: null,
            timeout: null));
    }
}