namespace Atc.Aspire.Hosting.Azure.Kusto.ApiService.Extensions;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder ConfigureScalar(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference("/scalar", options =>
        {
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        return app;
    }
}