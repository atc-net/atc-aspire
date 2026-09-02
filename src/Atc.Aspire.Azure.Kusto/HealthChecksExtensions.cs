namespace Aspire;

/// <summary>
/// Provides extension methods for adding health checks to an <see cref="IHostApplicationBuilder"/>.
/// </summary>
internal static class HealthChecksExtensions
{
    /// <summary>
    /// Adds the specified <see cref="HealthCheckRegistration"/> to the host application builder if a health check with the same name has not already been added.
    /// </summary>
    /// <param name="builder">The host application builder to which the health check is added.</param>
    /// <param name="healthCheckRegistration">The registration details for the health check.</param>
    public static void TryAddHealthCheck(
        this IHostApplicationBuilder builder,
        HealthCheckRegistration healthCheckRegistration)
    {
        builder.TryAddHealthCheck(healthCheckRegistration.Name, hcBuilder => hcBuilder.Add(healthCheckRegistration));
    }

    /// <summary>
    /// Adds a health check to the host application builder by invoking the specified <paramref name="addHealthCheck"/> action,
    /// but only if a health check with the given <paramref name="name"/> has not already been added.
    /// </summary>
    /// <param name="builder">The host application builder to which the health check is added.</param>
    /// <param name="name">The unique name of the health check to add.</param>
    /// <param name="addHealthCheck">
    /// An action that configures the health check on an <see cref="IHealthChecksBuilder"/>.
    /// This action is invoked if a health check with the specified name has not already been added.
    /// </param>
    public static void TryAddHealthCheck(
        this IHostApplicationBuilder builder,
        string name,
        Action<IHealthChecksBuilder> addHealthCheck)
    {
        var healthCheckKey = $"Aspire.HealthChecks.{name}";

        if (builder.Properties.ContainsKey(healthCheckKey))
        {
            return;
        }

        builder.Properties[healthCheckKey] = true;
        addHealthCheck(builder.Services.AddHealthChecks());
    }
}