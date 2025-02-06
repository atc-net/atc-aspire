namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents Kusto.
/// </summary>
/// <param name="name">The name of the resource</param>
public class KustoContainerResource([ResourceName] string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const ushort DefaultHttpPort = 8080;

    public const string DefaultDbName = "NetDefaultDB";

    private EndpointReference? primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the EventStore server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => primaryEndpoint ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Gets the connection string for the EventStore server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
}