namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class DeploymentManifestTests
{
    [Fact]
    public async Task LoadAsync_SampleDeploymentTemplate_ReadsRoutesAndModules()
    {
        // Act
        var manifest = await DeploymentManifest.LoadAsync(Path.Combine("TestData", "deployment.template.json"), TestContext.Current.CancellationToken);

        // Assert
        manifest.Routes.Should().HaveCount(3);
        manifest.Routes["opcpublisherToIoTHub"].Should().Be("FROM /messages/modules/opcpublisher/* INTO $upstream");
        manifest.Routes["simulationmoduleToIoTHub"].Should().Be("FROM /messages/modules/simulationmodule/outputs/* INTO $upstream");
        manifest.ModuleIds.Should().BeEquivalentTo("opcpublisher", "simulationmodule", "opcpublishernodemanagermodule");
        manifest.ModuleImages["opcpublisher"].Should().Be("mcr.microsoft.com/iotedge/opc-publisher:2.9.18");
        manifest.ModuleImages["simulationmodule"].Should().Be("${MODULES.simulationmodule}");
        manifest.ModuleDesiredProperties.Should().BeEmpty();
    }

    [Fact]
    public void Parse_StringRoutesAndModuleTwins_ReadsRoutesAndDesiredProperties()
    {
        // Arrange
        const string json = """
            {
              "modulesContent": {
                "$edgeAgent": {
                  "properties.desired": {
                    "modules": {
                      "mymodule": { "type": "docker" }
                    }
                  }
                },
                "$edgeHub": {
                  "properties.desired": {
                    "routes": {
                      "upstream": "FROM /messages/* INTO $upstream"
                    }
                  }
                },
                "mymodule": {
                  "properties.desired": {
                    "interval": 5
                  }
                }
              }
            }
            """;

        // Act
        var manifest = DeploymentManifest.Parse(json);

        // Assert
        manifest.Routes.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("upstream", "FROM /messages/* INTO $upstream"));
        manifest.ModuleIds.Should().Equal("mymodule");
        manifest.ModuleDesiredProperties.Should().ContainKey("mymodule");
        manifest.ModuleDesiredProperties["mymodule"].Should().Contain("\"interval\"");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"something\": {} }")]
    public void Parse_NotADeploymentManifest_ThrowsFormatException(string json)
    {
        // Act
        var act = () => DeploymentManifest.Parse(json);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_EdgeHubDesiredProperties_ReadsTheEdgeHubConfiguration()
    {
        // Arrange
        const string json = """
            {
              "modulesContent": {
                "$edgeHub": {
                  "properties.desired": {
                    "schemaVersion": "1.1",
                    "routes": { "toCloud": { "route": "FROM /messages/* INTO $upstream", "priority": 1 } },
                    "storeAndForwardConfiguration": { "timeToLiveSecs": 7200 }
                  }
                }
              }
            }
            """;

        // Act
        var manifest = DeploymentManifest.Parse(json);

        // Assert
        manifest.EdgeHubDesiredProperties.Should().Be(
            """{"schemaVersion":"1.1","routes":{"toCloud":{"route":"FROM /messages/* INTO $upstream","priority":1}},"storeAndForwardConfiguration":{"timeToLiveSecs":7200}}""");
    }

    [Fact]
    public void Parse_NoEdgeHubSection_HasNoEdgeHubConfiguration()
    {
        // Act
        var manifest = DeploymentManifest.Parse("""{ "modulesContent": {} }""");

        // Assert
        manifest.EdgeHubDesiredProperties.Should().BeNull();
    }
}