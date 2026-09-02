namespace Atc.Aspire.Hosting.Azure.Kusto;

/// <summary>
/// Default values for the Kustainer (Kusto emulator) container.
/// </summary>
internal static class KustoEmulatorContainerDefaults
{
    /// <summary>
    /// The default (emulator local) path used for persisting Kusto databases. This path
    /// can be mounted as a volume to persist database data across container restarts.
    /// </summary>
    /// <remarks>/kustodata/dbs/</remarks>
    public const string DefaultPersistencePath = "/kustodata/dbs/";

    /// <summary>
    /// Builds the default database creation command, persisting metadata and data under
    /// <paramref name="persistencePathRoot"/>.
    /// </summary>
    /// <param name="dbName">The name of the database to create.</param>
    /// <param name="persistencePathRoot">The root path used to persist the database.</param>
    /// <returns>A KQL command that creates the database if it does not already exist.</returns>
    public static string DefaultCreateDatabaseCommand(
        string dbName,
        string persistencePathRoot = DefaultPersistencePath)
    {
        var root = persistencePathRoot.AsSpan().TrimEnd('/');

        return CslCommandGenerator.GenerateDatabaseCreateCommand(
            dbName,
            metadataPersistentPath: $"{root}/{dbName}/md",
            dataPersistentPath: $"{root}/{dbName}/data",
            ifNotExists: true);
    }
}