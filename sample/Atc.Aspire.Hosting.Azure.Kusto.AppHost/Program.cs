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
    .WithHttpHealthCheck("/health")
    .WithUrls(context =>
    {
        foreach (var tuple in context.Urls
                     .Select(url => (Url: url, Uri: new Uri(url.Url)))
                     .OrderByDescending(_ => _.Uri.Scheme is "https")
                     .Select((pair, index) => (pair.Url, pair.Uri.Scheme, Index: index)))
        {
            var (url, scheme, index) = tuple;

            // Order HTTPS first.
            var order = context.Urls.Count - 1 - index;

            url.DisplayText = $"Swagger ({scheme.ToUpper(CultureInfo.InvariantCulture)})";
            url.DisplayOrder = order;
        }
    });

await builder.Build().RunAsync();