namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class DesiredPropertiesPatchTests
{
    [Fact]
    public void Create_TwinPropertyMissingFromManifest_SetsItToNull()
    {
        // Act
        var patch = DesiredPropertiesPatch.Create(
            """{ "interval": 1, "old": true }""",
            """{ "interval": 5 }""");

        // Assert
        patch.Should().Be("""{"interval":5,"old":null}""");
    }

    [Fact]
    public void Create_NestedTwinPropertyMissingFromManifest_SetsItToNull()
    {
        // Act
        var patch = DesiredPropertiesPatch.Create(
            """{ "settings": { "interval": 1, "old": true } }""",
            """{ "settings": { "interval": 5 } }""");

        // Assert
        patch.Should().Be("""{"settings":{"interval":5,"old":null}}""");
    }

    [Fact]
    public void Create_ManifestValueInsteadOfTwinObject_UsesTheManifestValue()
    {
        // Act
        var patch = DesiredPropertiesPatch.Create(
            """{ "settings": { "interval": 1 } }""",
            """{ "settings": "default" }""");

        // Assert
        patch.Should().Be("""{"settings":"default"}""");
    }

    [Fact]
    public void Create_TwinMetadataAndVersion_AreLeftOutOfThePatch()
    {
        // Act
        var patch = DesiredPropertiesPatch.Create(
            """{ "interval": 1, "$metadata": { "$lastUpdated": "2026-10-28T08:00:00Z" }, "$version": 3 }""",
            """{ "interval": 5 }""");

        // Assert
        patch.Should().Be("""{"interval":5}""");
    }
}