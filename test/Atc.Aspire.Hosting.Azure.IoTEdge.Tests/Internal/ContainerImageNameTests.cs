namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class ContainerImageNameTests
{
    [Theory]
    [InlineData("mcr.microsoft.com/iotedge/opc-publisher:2.9.18", "iotedge/opc-publisher")]
    [InlineData("mcr.microsoft.com/iotedge/opc-publisher", "iotedge/opc-publisher")]
    [InlineData("iotedge/opc-publisher:2.9.18", "iotedge/opc-publisher")]
    [InlineData("MCR.Microsoft.com/IoTEdge/OPC-Publisher:2.9.18", "iotedge/opc-publisher")]
    [InlineData("localhost:5000/team/module:1.0", "team/module")]
    [InlineData("localhost/team/module", "team/module")]
    [InlineData("myregistry.azurecr.io/module@sha256:abc123", "module")]
    [InlineData("nginx:latest", "nginx")]
    [InlineData("library/nginx", "nginx")]
    public void GetRepository_ImageReference_ReturnsRepositoryWithoutRegistryAndTag(
        string imageReference,
        string expected)
        => ContainerImageName.GetRepository(imageReference).Should().Be(expected);

    [Theory]
    [InlineData("${MODULES.simulationmodule}", true)]
    [InlineData("${MODULES.opcpublishernodemanagermodule.debug}", true)]
    [InlineData("mcr.microsoft.com/iotedge/opc-publisher:2.9.18", false)]
    public void IsPlaceholder_DeploymentTemplateImage_ReturnsTrueOnlyForPlaceholders(
        string image,
        bool expected)
        => ContainerImageName.IsPlaceholder(image).Should().Be(expected);

    [Fact]
    public void GetReference_SampleOpcPublisherContainer_IncludesRegistryAndTag()
    {
        // Arrange
        var image = new ContainerImageAnnotation
        {
            Registry = "mcr.microsoft.com",
            Image = "iotedge/opc-publisher",
            Tag = "2.9.18",
        };

        // Act
        var reference = ContainerImageName.GetReference(image);

        // Assert
        reference.Should().Be("mcr.microsoft.com/iotedge/opc-publisher:2.9.18");
    }
}