var builder = WebApplication.CreateBuilder(args);

builder.ConfigureAzureDataExplorer("kusto-emulator", o =>
{
    o.DatabaseName = builder.Configuration["KustoDatabaseName"] ?? "NetDefaultDB";
});

builder.AddServiceDefaults();

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseStaticFiles();

app.ConfigureSwagger();

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.MapGet(
        "/todo",
        (IKustoProcessor processor, CancellationToken cancellationToken)
        => processor.ExecuteQuery(
            new TodoQuery(),
            cancellationToken))
    .WithName("GetTodos")
    .WithOpenApi();

await app.RunAsync();