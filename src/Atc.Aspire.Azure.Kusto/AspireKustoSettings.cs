namespace Atc.Aspire.Azure.Kusto;

public sealed class AspireKustoSettings
{
    /// <summary>
    /// The endpoint URI string of the Kusto server to connect to.
    /// </summary>
    public Uri? HostAddress { get; set; }

    public string? DatabaseName { get; set; }

    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the kustainer health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets an integer value that indicates the kustainer health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }
}