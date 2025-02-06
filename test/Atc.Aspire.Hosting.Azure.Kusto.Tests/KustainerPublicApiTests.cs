// ReSharper disable AssignNullToNotNullAttribute
namespace Atc.Aspire.Hosting.Azure.Kusto.Tests;

public sealed class KustainerPublicApiTests
{
    [Fact]
    public void AddKustainerShouldThrowWhenBuilderIsNull()
    {
        // Arrange
        IDistributedApplicationBuilder builder = null!;
        const string name = "Kusto";

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddKustainer(name));
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKustainerShouldThrowWhenNameIsNull()
    {
        // Arrange
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddKustainer(name));
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldThrowWhenBuilderIsNull()
    {
        // Arrange
        IResourceBuilder<KustoContainerResource> builder = null!;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => builder.WithDataVolume());
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void CtorKustoContainerResourceShouldThrowWhenNameIsNull()
    {
        // Arrange
        const string name = null!;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new KustoContainerResource(name!));
        Assert.Equal(nameof(name), exception.ParamName);
    }
}