var builder = DistributedApplication.CreateBuilder(args);

var kusto = builder
    .AddKustainer()
    .WithDataVolume()
    .WithBindMount("./kusto", target: "/kustodata")
    .WithEntrypoint("/kustodata/entrypoint.sh");

builder.AddProject<Atc_Aspire_Hosting_Azure_Kusto_ApiService>("apiservice")
    .WithReference(kusto)
    .WaitFor(kusto)
    .WithEnvironment("KustoDatabaseName", KustoContainerResource.DefaultDbName)
    .WithHttpHealthCheck("/health");

await builder.Build().RunAsync();