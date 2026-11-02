namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class IoTHubIdentifierTests
{
    [Theory]
    [InlineData("opcpublisher")]
    [InlineData("edge-device-my-machine")]
    [InlineData("SN-12345")]
    [InlineData("a.b_c%d*e?f!g(h)i,j:k=l@m$n'o")]
    public void IsValid_AllowedCharacters_ReturnsTrue(string id)
        => IoTHubIdentifier.IsValid(id).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("plus+sign")]
    [InlineData("hash#sign")]
    [InlineData("slash/char")]
    [InlineData("æøå")]
    public void IsValid_NotAllowedCharacters_ReturnsFalse(string id)
        => IoTHubIdentifier.IsValid(id).Should().BeFalse();

    [Fact]
    public void IsValid_LongerThan128Characters_ReturnsFalse()
    {
        // Arrange
        var maxLength = new string('a', 128);
        var tooLong = new string('a', 129);

        // Act
        var maxLengthIsValid = IoTHubIdentifier.IsValid(maxLength);
        var tooLongIsValid = IoTHubIdentifier.IsValid(tooLong);

        // Assert
        maxLengthIsValid.Should().BeTrue();
        tooLongIsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("$edgeHub")]
    [InlineData("$edgeAgent")]
    [InlineData("$custom")]
    public void EnsureValidModuleId_ReservedForTheRuntime_ThrowsArgumentException(
        string moduleId)
    {
        // Act
        var act = () => IoTHubIdentifier.EnsureValidModuleId(moduleId, nameof(moduleId));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*reserved*")
            .Which.ParamName.Should().Be("moduleId");
    }

    [Fact]
    public void EnsureValidModuleId_InvalidCharacters_ThrowsArgumentException()
    {
        // Act
        var act = () => IoTHubIdentifier.EnsureValidModuleId("my module", "moduleId");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*not a valid IoT Hub module id*");
    }

    [Fact]
    public void EnsureValidDeviceId_DollarSign_DoesNotThrow()
    {
        // Act
        var act = () => IoTHubIdentifier.EnsureValidDeviceId("$device", "deviceId");

        // Assert
        act.Should().NotThrow();
    }
}