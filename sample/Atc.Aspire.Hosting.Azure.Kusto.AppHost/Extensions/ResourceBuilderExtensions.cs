namespace Atc.Aspire.Hosting.Azure.Kusto.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> WithApiExplorerUrl(
        this IResourceBuilder<ProjectResource> project)
        => project.WithUrls(context =>
        {
            foreach (var url in context.Urls)
            {
                url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
            }

            var endpoint = context.GetEndpoint("https");

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = $"{endpoint!.Url}/scalar/v1",
                DisplayText = "Scalar",
                Endpoint = endpoint,
            });
        });
}