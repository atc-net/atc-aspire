namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class JsonArgumentTests
{
    [Theory]
    [InlineData("{}", false)]
    [InlineData("""{ "pageIndex": 0 }""", false)]
    [InlineData("[1, 2]", false)]
    [InlineData("""{ "interval": 5 }""", true)]
    public void GetError_ValidJson_ReturnsNull(
        string json,
        bool requireObject)
        => JsonArgument.GetError(json, requireObject).Should().BeNull();

    [Fact]
    public void GetError_InvalidJson_AsksForValidJson()
    {
        // Act
        var error = JsonArgument.GetError("{ not json", requireObject: false);

        // Assert
        error.Should().StartWith("Enter valid JSON.");
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("5")]
    [InlineData("\"text\"")]
    public void GetError_NotAnObjectWhenRequired_AsksForAnObject(string json)
    {
        // Act
        var error = JsonArgument.GetError(json, requireObject: true);

        // Assert
        error.Should().StartWith("Enter a JSON object");
    }
}