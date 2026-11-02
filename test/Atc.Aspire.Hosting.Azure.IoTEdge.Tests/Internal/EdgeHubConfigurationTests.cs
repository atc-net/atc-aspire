namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class EdgeHubConfigurationTests
{
    [Theory]
    [InlineData("""{ "schemaVersion": "1.1", "routes": {} }""", true)]
    [InlineData("""{ "routes": {} }""", false)]
    [InlineData("""{ "schemaVersion": "" }""", false)]
    [InlineData("""{ "schemaVersion": 1.1 }""", false)]
    public void HasSchemaVersion_DesiredProperties_ReturnsTrueOnlyForAVersionString(
        string desiredProperties,
        bool expected)
        => EdgeHubConfiguration.HasSchemaVersion(desiredProperties).Should().Be(expected);

    [Fact]
    public void CreateDeploymentContent_EdgeHubConfiguration_HasBothReservedModules()
    {
        // Act
        var content = EdgeHubConfiguration.CreateDeploymentContent(EdgeHubConfiguration.Default);

        // Assert
        content.ModulesContent.Keys.Should().BeEquivalentTo("$edgeAgent", "$edgeHub");
        content.ModulesContent["$edgeHub"]["properties.desired"].ToString().Should()
            .Be(Newtonsoft.Json.Linq.JObject.Parse(EdgeHubConfiguration.Default).ToString());
    }

    [Fact]
    public void Default_RoutesEverythingUpstreamWithASchemaVersion()
    {
        // Act
        var configuration = JsonNode.Parse(EdgeHubConfiguration.Default)!;

        // Assert
        EdgeHubConfiguration.HasSchemaVersion(EdgeHubConfiguration.Default).Should().BeTrue();
        configuration["routes"]!["upstream"]!.GetValue<string>().Should().Be("FROM /messages/* INTO $upstream");
        configuration["storeAndForwardConfiguration"]!["timeToLiveSecs"]!.GetValue<int>().Should().Be(7200);
    }
}