namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class IoTHubEventHubEndpointTests
{
    [Fact]
    public void FromRedirect_HostAndAddressFromIoTHub_ReadsNamespaceAndEventHubName()
    {
        // Act
        var result = IoTHubEventHubEndpoint.FromRedirect(
            "iothub-ns-myhub-1234567-0123456789.servicebus.windows.net",
            "amqps://iothub-ns-myhub-1234567-0123456789.servicebus.windows.net:5671/myhub/$management");

        // Assert
        result.Should().Be(new IoTHubEventHubEndpoint("iothub-ns-myhub-1234567-0123456789.servicebus.windows.net", "myhub"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromRedirect_NoHostName_Throws(string? hostName)
        => FluentActions.Invoking(() => IoTHubEventHubEndpoint.FromRedirect(hostName, "amqps://ns.servicebus.windows.net:5671/myhub/$management"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*without a host name*");

    [Theory]
    [InlineData(null)]
    [InlineData("amqps://ns.servicebus.windows.net/myhub/$management")]
    [InlineData("amqps://ns.servicebus.windows.net:5671/$management")]
    [InlineData("amqps://ns.servicebus.windows.net:5671/myhub")]
    public void FromRedirect_AddressWithoutEventHubName_ThrowsWithTheAddress(
        string? address)
        => FluentActions.Invoking(() => IoTHubEventHubEndpoint.FromRedirect("ns.servicebus.windows.net", address))
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"IoT Hub redirected to '{address}', which has no Event Hub name.");

    [Fact]
    public void CreateTransportSettings_Amqp_ConnectsWithTlsOnPort5671()
    {
        // Act
        var result = IoTHubEventHubEndpoint.CreateTransportSettings("myhub.azure-devices.net", useWebSockets: false);

        // Assert
        var tls = result.Should().BeOfType<TlsTransportSettings>().Subject;
        tls.TargetHost.Should().Be("myhub.azure-devices.net");
        tls.InnerTransportSettings.Should().BeOfType<TcpTransportSettings>()
            .Which.Should().BeEquivalentTo(new { Host = "myhub.azure-devices.net", Port = 5671 });
    }

    [Fact]
    public void CreateTransportSettings_WebSockets_ConnectsToIoTHubsWebSocketPathOnPort443()
    {
        // Act
        var result = IoTHubEventHubEndpoint.CreateTransportSettings("myhub.azure-devices.net", useWebSockets: true);

        // Assert
        var webSocket = result.Should().BeOfType<WebSocketTransportSettings>().Subject;
        webSocket.Uri.Should().Be(new Uri("wss://myhub.azure-devices.net/$iothub/websocket"));
        webSocket.SubProtocol.Should().Be("AMQPWSB10");
        webSocket.Proxy.Should().BeNull();
    }

    [Fact]
    public void GetConnectionString_IoTHubPolicy_BuildsAnEventHubsConnectionStringWithThePolicy()
    {
        // Arrange
        var key = Convert.ToBase64String(new byte[32]);
        var endpoint = new IoTHubEventHubEndpoint("ns.servicebus.windows.net", "myhub");
        var iotHub = new IoTHubConnectionInfo("myhub.azure-devices.net", "service", key);

        // Act
        var result = endpoint.GetConnectionString(iotHub);

        // Assert
        result.Should().Be($"Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=service;SharedAccessKey={key};EntityPath=myhub");
    }
}