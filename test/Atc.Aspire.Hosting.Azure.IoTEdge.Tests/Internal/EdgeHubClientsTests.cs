namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class EdgeHubClientsTests
{
    [Fact]
    public void GetConnectionStates_ReportedClients_ReturnsOnlyTheModulesOfTheDevice()
    {
        // Arrange
        const string reported = """
            {
              "clients": {
                "edge-device/opcpublisher": { "status": "Connected", "lastConnectedTimeUtc": "2026-10-25T10:00:00Z" },
                "edge-device/simulationmodule": { "status": "Disconnected" },
                "other-device/opcpublisher": { "status": "Connected" }
              },
              "$version": 7
            }
            """;

        // Act
        var states = EdgeHubClients.GetConnectionStates(reported, "edge-device");

        // Assert
        states.Should().HaveCount(2);
        states["opcpublisher"].Should().Be(EdgeHubClients.ConnectedStatus);
        states["simulationmodule"].Should().Be("Disconnected");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "$version": 1 }""")]
    [InlineData("""{ "clients": "unexpected" }""")]
    [InlineData("""{ "clients": { "edge-device/opcpublisher": { "status": 1 } } }""")]
    [InlineData("""[ 1, 2 ]""")]
    public void GetConnectionStates_UnexpectedFormat_ReturnsEmpty(
        string reported)
        => EdgeHubClients.GetConnectionStates(reported, "edge-device").Should().BeEmpty();
}