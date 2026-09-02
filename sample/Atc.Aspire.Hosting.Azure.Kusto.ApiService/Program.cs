var builder = WebApplication.CreateBuilder(args);

builder.ConfigureAzureDataExplorer("kusto");

builder.AddServiceDefaults();

var apiVersioningBuilder = builder.Services.ConfigureApiVersioning();

builder.Services.ConfigureScalar(apiVersioningBuilder);

var app = builder.Build();

app.UseStaticFiles();

app.ConfigureScalar();

app.MapDefaultEndpoints();

app.MapGet(
        "/todo",
        (IKustoProcessor processor, CancellationToken cancellationToken)
        => processor.ExecuteQuery(
            new TodoQuery(),
            cancellationToken))
    .WithName("GetTodos");

await app.RunAsync();