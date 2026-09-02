var builder = DistributedApplication.CreateBuilder(args);

var kusto = builder
    .AddKustainer()
    .WithDataVolume();

// The database is created automatically when the emulator is ready.
// WithCreationScript runs a KQL script against the database afterwards - here it
// creates the 'Todo' table (schema inferred from the datatable) and seeds it with rows.
var database = kusto
    .AddDatabase("kusto", KustoContainerResource.DefaultDbName)
    .WithCreationScript(
        """
        .set-or-replace Todo <|
            datatable(Id: int, Title: string, Description: string, Status: string, Created: datetime, Priority: string, Closed: datetime)
            [
                1, "Watch Netflix", "Watch the new show", "Pending", datetime(2025-01-28T10:00:00Z), "Low", datetime(null),
                2, "Make food", "Try out the new dish from the Netflix show", "Pending", datetime(2025-01-27T15:30:00Z), "Medium", datetime(null),
                3, "Coding", "Code up the new feature in atc-aspire kusto package", "Ended", datetime(2025-01-26T09:15:00Z), "High", datetime(2025-01-27T12:00:00Z)
            ]
        """);

builder.AddProject<Api>("apiservice")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health")
    .WithApiExplorerUrl();

await builder.Build().RunAsync();