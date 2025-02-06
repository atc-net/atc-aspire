namespace Atc.Aspire.Hosting.Azure.Kusto.Tests;

public sealed class AddKustainerTests
{
    [Fact]
    public async Task AddKustainerWithDefaultsAddsAnnotationMetadata()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddKustainer();

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var resource = Assert.Single(appModel.Resources.OfType<KustoContainerResource>());
        Assert.Equal("kusto-emulator", resource.Name);

        // Assuming the resource stores endpoint and image annotations.
        var endpointAnnotation = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == KustoContainerResource.HttpEndpointName);
        Assert.Equal(KustoContainerResource.DefaultHttpPort, endpointAnnotation.TargetPort);

        var containerAnnotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(KustoContainerImageTags.MarinerLinuxImage, containerAnnotation.Image);
        Assert.Equal(KustoContainerImageTags.DefaultTag, containerAnnotation.Tag);
        Assert.Equal(KustoContainerImageTags.Registry, containerAnnotation.Registry);

        Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>(), x => x.Key == "kusto-emulator_check");

        var environmentAnnotations = await resource.Annotations
            .Where(a => a.GetType().Name == "EnvironmentAnnotation")
            .ToListAsync();

        Assert.Equal(2, environmentAnnotations.Count);
    }

    [Fact]
    public async Task KustainerCreatesConnectionString()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder
            .AddKustainer()
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8080));

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        IResourceWithConnectionString resource = Assert.Single(appModel.Resources.OfType<KustoContainerResource>());
        var connectionString = await resource.GetConnectionStringAsync();

        Assert.Equal("http://localhost:8080", connectionString);
    }
}