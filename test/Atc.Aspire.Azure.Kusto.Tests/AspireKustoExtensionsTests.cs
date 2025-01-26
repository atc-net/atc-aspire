// ReSharper disable ExpressionIsAlwaysNull
namespace Atc.Aspire.Azure.Kusto.Tests;

public sealed class AspireKustoExtensionsTests
{
    private const string ConfigSectionName = "Aspire:Kusto:Client";
    private const string ConnectionName = "mykusto";

    [Fact]
    public async Task ConfigureAzureDataExplorer_WithValidConfiguration_RegistersHealthCheck()
    {
        // Arrange
        const string connectionString = "http://localhost:8080";
        const string databaseName = "TestDatabase";

        var inMemorySettings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { $"ConnectionStrings:{ConnectionName}", connectionString },
            { $"{ConfigSectionName}:DatabaseName", databaseName },
        };

        var builder = CreateBuilder(inMemorySettings);

        builder.Services.AddHealthChecks();

        builder.ConfigureAzureDataExplorer(ConnectionName);

        // Act
        using var host = builder.Build();
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        // Assert
        const string expectedName = $"Kusto_{ConnectionName}";
        Assert.Contains(report.Entries, entry => entry.Key == expectedName);
    }

    [Fact]
    public void ConfigureAzureDataExplorer_WithHealthChecksDisabled_DoesNotRegisterHealthCheck()
    {
        // Arrange
        const string connectionString = "http://localhost:8080";
        const string databaseName = "TestDatabase";

        var inMemorySettings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { $"ConnectionStrings:{ConnectionName}", connectionString },
                { $"{ConfigSectionName}:DatabaseName", databaseName },
            };

        var builder = CreateBuilder(inMemorySettings);

        builder.ConfigureAzureDataExplorer(ConnectionName, options =>
        {
            options.DisableHealthChecks = true;
        });

        // Act
        using var host = builder.Build();
        var healthCheckService = host.Services.GetService<HealthCheckService>();

        // Assert
        Assert.Null(healthCheckService);
    }

    [Fact]
    public void ConfigureAzureDataExplorer_WithInvalidConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        const string connectionString = "invalid";
        const string databaseName = "TestDatabase";

        var inMemorySettings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { $"ConnectionStrings:{ConnectionName}", connectionString },
                { $"{ConfigSectionName}:DatabaseName", databaseName },
            };

        var builder = CreateBuilder(inMemorySettings);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureAzureDataExplorer(ConnectionName));

        // Assert
        Assert.Contains($"Invalid connection string for Kusto: {connectionString}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureAzureDataExplorer_MissingHostAddress_ThrowsInvalidOperationException()
    {
        // Arrange
        const string databaseName = "TestDatabase";

        var inMemorySettings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
                { $"{ConfigSectionName}:DatabaseName", databaseName },
        };

        var builder = CreateBuilder(inMemorySettings);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureAzureDataExplorer(ConnectionName));

        Assert.Contains("Ensure a valid host address is provided", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureAzureDataExplorer_MissingDatabaseName_ThrowsInvalidOperationException()
    {
        // Arrange
        const string connectionString = "http://localhost:8080";

        var inMemorySettings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
                { $"ConnectionStrings:{ConnectionName}", connectionString },
        };

        var builder = CreateBuilder(inMemorySettings);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureAzureDataExplorer(ConnectionName));

        Assert.Contains("Ensure a valid database name is provided", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureAzureDataExplorer_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IHostApplicationBuilder? builder = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder!.ConfigureAzureDataExplorer(ConnectionName));
    }

    [Fact]
    public void ConfigureAzureDataExplorer_EmptyConnectionName_ThrowsArgumentException()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        Assert.Throws<ArgumentException>(() => builder.ConfigureAzureDataExplorer(string.Empty));
    }

    private static HostApplicationBuilder CreateBuilder(IDictionary<string, string?> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(configuration);

        return builder;
    }
}