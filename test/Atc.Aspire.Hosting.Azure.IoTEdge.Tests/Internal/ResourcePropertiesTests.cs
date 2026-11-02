namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class ResourcePropertiesTests
{
    [Fact]
    public void Set_ExistingAndNewProperties_ReplacesByNameAndKeepsTheRest()
    {
        // Arrange
        ImmutableArray<ResourcePropertySnapshot> properties =
        [
            new("container.image", "iotedge/opc-publisher:2.9.18"),
            new("iotedge.connection", "Checking"),
        ];

        // Act
        var result = ResourceProperties.Set(
            properties,
            new ResourcePropertySnapshot("iotedge.connection", "Connected"),
            new ResourcePropertySnapshot("iotedge.mode", "Through edgeHub"));

        // Assert
        result.Select(x => $"{x.Name}={x.Value}").Should().Equal(
            "container.image=iotedge/opc-publisher:2.9.18",
            "iotedge.connection=Connected",
            "iotedge.mode=Through edgeHub");
    }
}