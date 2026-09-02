namespace Atc.Aspire.Hosting.Azure.Kusto.Tests;

public sealed class AddDatabaseTests
{
    [Fact]
    public async Task AddDatabaseAddsChildResource()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddKustainer().AddDatabase("mydb");

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var database = Assert.Single(appModel.Resources.OfType<KustoDatabaseResource>());
        Assert.Equal("mydb", database.Name);
        Assert.Equal("mydb", database.DatabaseName);

        var container = Assert.Single(appModel.Resources.OfType<KustoContainerResource>());
        Assert.Equal("kusto-emulator", database.Parent.Name);
        Assert.Same(container, database.Parent);
        Assert.Single(container.Databases);

        Assert.Single(database.Annotations.OfType<HealthCheckAnnotation>(), x => x.Key == "mydb_check");
    }

    [Fact]
    public async Task AddDatabaseUsesExplicitDatabaseName()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddKustainer().AddDatabase("mydb", "PhysicalDb");

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var database = Assert.Single(appModel.Resources.OfType<KustoDatabaseResource>());
        Assert.Equal("mydb", database.Name);
        Assert.Equal("PhysicalDb", database.DatabaseName);
    }

    [Fact]
    public async Task DatabaseCreatesConnectionStringWithInitialCatalog()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        var kusto = appBuilder
            .AddKustainer()
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8080));

        kusto.AddDatabase("mydb", "PhysicalDb");

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        IResourceWithConnectionString database = Assert.Single(appModel.Resources.OfType<KustoDatabaseResource>());
        var connectionString = await database.GetConnectionStringAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("http://localhost:8080;", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PhysicalDb", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DatabaseExposesConnectionProperties()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        var kusto = appBuilder.AddKustainer();
        kusto.AddDatabase("mydb", "PhysicalDb");

        // Act
        await using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        IResourceWithConnectionString database = Assert.Single(appModel.Resources.OfType<KustoDatabaseResource>());
        var properties = await database.GetConnectionProperties().ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(properties, p => p.Key == "Uri");
        Assert.Contains(properties, p => p.Key == "DatabaseName");
    }

    [Fact]
    public void ContainerExposesUriConnectionProperty()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var kusto = appBuilder.AddKustainer();

        // Act
        var properties = ((IResourceWithConnectionString)kusto.Resource).GetConnectionProperties().ToList();

        // Assert
        var property = Assert.Single(properties);
        Assert.Equal("Uri", property.Key);
    }

    [Fact]
    public void WithCreationScriptAddsAnnotation()
    {
        // Arrange
        const string script = ".create table Foo (Bar: string)";
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var database = appBuilder.AddKustainer().AddDatabase("mydb").WithCreationScript(script);

        // Assert
        Assert.Equal(script, database.Resource.GetCreationScript());
    }

    [Fact]
    public void GetCreationScriptReturnsNullWhenNoScriptProvided()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var database = appBuilder.AddKustainer().AddDatabase("mydb");

        // Assert
        Assert.Null(database.Resource.GetCreationScript());
    }
}