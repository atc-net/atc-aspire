namespace Atc.Aspire.Hosting.Azure.Kusto;

internal static class KustoDatabaseResourceExtensions
{
    /// <summary>
    /// Gets the custom creation script from the resource annotation, if one was provided via
    /// <c>WithCreationScript</c>. Returns <see langword="null"/> when no custom script is set.
    /// </summary>
    /// <param name="databaseResource">
    /// The <see cref="KustoDatabaseResource"/> resource to inspect for annotations.
    /// </param>
    /// <remarks>
    /// The custom script is executed against the database <em>after</em> it has been created, so it is
    /// intended for creating tables, functions, or seeding data.
    /// </remarks>
    /// <returns>The custom KQL script, or <see langword="null"/> if none was provided.</returns>
    public static string? GetCreationScript(
        this KustoDatabaseResource databaseResource)
    {
        ArgumentNullException.ThrowIfNull(databaseResource);

        return databaseResource.Annotations.OfType<KustoCreateDatabaseScriptAnnotation>().LastOrDefault()?.Script;
    }
}