namespace Atc.Aspire.Hosting.Azure.Kusto.ApiService.Extensions;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder ConfigureSwagger(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ////if (!app.Environment.IsDevelopment())
        ////{
        ////    return app;
        ////}

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.EnableTryItOutByDefault();
            options.InjectStylesheet("/swagger-ui/SwaggerDark.css");
            options.InjectJavascript("/swagger-ui/main.js");
        });

        return app;
    }
}