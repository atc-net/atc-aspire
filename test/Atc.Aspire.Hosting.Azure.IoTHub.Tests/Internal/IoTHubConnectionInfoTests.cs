namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class IoTHubConnectionInfoTests
{
    // The key is built at runtime, so the source holds nothing that secret scanners take for a real connection string.
    private static readonly string Key = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void Parse_ServiceConnectionString_ReadsHostPolicyAndKey()
    {
        // Act
        var result = IoTHubConnectionInfo.Parse($"HostName=myhub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey={Key}");

        // Assert
        result.Should().Be(new IoTHubConnectionInfo("myhub.azure-devices.net", "service", Key));
        result.HubName.Should().Be("myhub");
    }

    [Fact]
    public void Parse_PartsInAnyOrderWithSpaces_ReadsThem()
    {
        // Act
        var result = IoTHubConnectionInfo.Parse($" SharedAccessKey={Key} ; sharedaccesskeyname=service ; HostName=myhub.azure-devices.net ;");

        // Assert
        result.Should().Be(new IoTHubConnectionInfo("myhub.azure-devices.net", "service", Key));
    }

    [Theory]
    [InlineData("SharedAccessKeyName=service;SharedAccessKey={0}", "HostName")]
    [InlineData("HostName=myhub.azure-devices.net;SharedAccessKey={0}", "SharedAccessKeyName")]
    [InlineData("HostName=myhub.azure-devices.net;SharedAccessKeyName=service", "SharedAccessKey")]
    [InlineData("HostName=myhub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=", "SharedAccessKey")]
    public void Parse_MissingPart_ThrowsNamingThePart(
        string connectionStringFormat,
        string part)
        => FluentActions.Invoking(() => IoTHubConnectionInfo.Parse(string.Format(CultureInfo.InvariantCulture, connectionStringFormat, Key)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"The IoT Hub connection string has no {part}.");

    [Fact]
    public void Parse_KeyIsNotBase64_ThrowsWithoutTheKey()
    {
        // Arrange
        var damagedKey = Key[..^2];

        // Act
        var act = () => IoTHubConnectionInfo.Parse($"HostName=myhub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey={damagedKey}");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The IoT Hub connection string's SharedAccessKey isn't valid base64.*")
            .Which.Message.Should().NotContain(damagedKey);
    }

    [Fact]
    public void Parse_DeviceConnectionString_ThrowsAskingForAHubPolicy()
        => FluentActions.Invoking(() => IoTHubConnectionInfo.Parse($"HostName=myhub.azure-devices.net;DeviceId=device1;SharedAccessKey={Key}"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*device connection string*shared access policy of the hub*")
            .Which.Message.Should().NotContain(Key);

    [Fact]
    public void ToString_LeavesOutTheKey()
        => new IoTHubConnectionInfo("myhub.azure-devices.net", "service", Key).ToString()
            .Should().Be("HostName=myhub.azure-devices.net;SharedAccessKeyName=service");
}