namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Kusto read-write database resource, which is a child resource of a <see cref="KustoContainerResource"/>.
/// </summary>
public sealed class KustoDatabaseResource : Resource, IResourceWithParent<KustoContainerResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KustoDatabaseResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="parent">The Kusto container (emulator) resource associated with this database.</param>
    public KustoDatabaseResource(
        string name,
        string databaseName,
        KustoContainerResource parent)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentNullException.ThrowIfNull(parent);

        DatabaseName = databaseName;
        Parent = parent;
    }

    /// <summary>
    /// Gets the parent Kusto container resource.
    /// </summary>
    public KustoContainerResource Parent { get; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string expression for the Kusto database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var connectionStringBuilder = new KustoConnectionStringBuilder
            {
                InitialCatalog = DatabaseName,
            };

            return ReferenceExpression.Create($"{Parent.ConnectionStringExpression};{connectionStringBuilder.ToString()}");
        }
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        foreach (var property in ((IResourceWithConnectionString)Parent).GetConnectionProperties())
        {
            yield return property;
        }

        yield return new("DatabaseName", ReferenceExpression.Create($"{DatabaseName}"));
    }
}