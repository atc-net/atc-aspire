namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class ArmIoTHubSettingsReaderTests
{
    [Fact]
    public void FromData_HubFromAzureResourceManager_ReadsRoutesFallbackEndpointsAndConsumerGroups()
    {
        // Arrange
        var routing = new IotHubRoutingProperties
        {
            Endpoints = new RoutingEndpoints(),
            FallbackRoute = new IotHubFallbackRouteProperties(IotHubRoutingSource.DeviceMessages, ["events"], isEnabled: true) { Name = "$fallback" },
        };

        routing.Routes.Add(new RoutingRuleProperties("lifecycle-to-events", IotHubRoutingSource.DeviceLifecycleEvents, ["events"], isEnabled: true));
        routing.Endpoints.EventHubs.Add(new RoutingEventHubProperties("archive"));
        routing.Endpoints.StorageContainers.Add(new RoutingStorageContainerProperties("cold", "container"));

        var data = new IotHubDescriptionData(AzureLocation.WestEurope, new IotHubSkuInfo(IotHubSku.S1))
        {
            Properties = new IotHubProperties { Routing = routing },
        };

        // Act
        var result = ArmIoTHubSettingsReader.FromData(data, ["$Default", "aspire-bridge"]);

        // Assert
        result.Routes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new IoTHubRoute("lifecycle-to-events", "DeviceLifecycleEvents", ["events"], IsEnabled: true));
        result.FallbackRoute.Should().BeEquivalentTo(new IoTHubRoute("$fallback", "DeviceMessages", ["events"], IsEnabled: true));
        result.CustomEndpointNames.Should().Equal("archive", "cold");
        result.ConsumerGroups.Should().Equal("$Default", "aspire-bridge");
    }

    [Fact]
    public void FromData_HubWithoutRouting_HasNoRoutesOrEndpoints()
    {
        // Arrange
        var data = new IotHubDescriptionData(AzureLocation.WestEurope, new IotHubSkuInfo(IotHubSku.F1));

        // Act
        var result = ArmIoTHubSettingsReader.FromData(data, ["$Default"]);

        // Assert
        result.Routes.Should().BeEmpty();
        result.FallbackRoute.Should().BeNull();
        result.CustomEndpointNames.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("my hub")]
    [InlineData("hub' or name != '")]
    public async Task ReadAsync_NotAnIoTHubName_IsNotVerifiedWithoutAskingAzure(
        string hubName)
    {
        // Arrange
        var reader = new ArmIoTHubSettingsReader(new UnusableCredential());

        // Act
        var result = await reader.ReadAsync(hubName, TestContext.Current.CancellationToken);

        // Assert
        result.Settings.Should().BeNull();
        result.NotVerifiedReason.Should().Be($"'{hubName}' isn't an IoT Hub name.");
    }

    // Fails the test if anything tries to sign in.
    private sealed class UnusableCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The reader asked Azure.");

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The reader asked Azure.");
    }
}