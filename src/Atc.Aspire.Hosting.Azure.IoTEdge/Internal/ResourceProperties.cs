namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Sets the properties the dashboard shows for a resource.
/// </summary>
internal static class ResourceProperties
{
    /// <summary>
    /// Sets properties on a snapshot, replacing any that already have the same name, so each property is shown once.
    /// </summary>
    /// <param name="properties">The properties of the current snapshot.</param>
    /// <param name="values">The properties to set.</param>
    /// <returns>The properties for the new snapshot.</returns>
    public static ImmutableArray<ResourcePropertySnapshot> Set(
        ImmutableArray<ResourcePropertySnapshot> properties,
        params ResourcePropertySnapshot[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var names = values
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        return [.. properties.Where(x => !names.Contains(x.Name)), .. values];
    }
}