namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Kustainer (Kusto emulator) container.
/// </summary>
/// <param name="name">The name of the resource</param>
public sealed class KustoContainerResource([ResourceName] string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const ushort DefaultHttpPort = 8080;

    public const string DefaultDbName = "NetDefaultDB";

    private EndpointReference? primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Kusto emulator.
    /// </summary>
    public EndpointReference PrimaryEndpoint
        => primaryEndpoint ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Gets the connection URI expression for the Kusto emulator.
    /// </summary>
    public ReferenceExpression UriExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the connection string for the Kusto emulator.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    /// <summary>
    /// The databases for this Kusto emulator.
    /// </summary>
    internal List<KustoDatabaseResource> Databases { get; } = [];

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Uri", UriExpression);
    }
}