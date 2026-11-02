namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class IoTEdgeDeviceProvisionerTests
{
    private const string IotHubHostName = "myhub.azure-devices.net";
    private const string DeviceId = "mydevice";
    private const string ModuleId = "mymodule";
    private const string EdgeHubModuleId = "$edgeHub";

    private readonly IIoTHubService iotHubService = Substitute.For<IIoTHubService>();
    private readonly FakeLogger logger = new();
    private readonly IoTEdgeDeviceProvisioner provisioner;

    public IoTEdgeDeviceProvisionerTests()
    {
        provisioner = new IoTEdgeDeviceProvisioner(iotHubService, IotHubHostName, logger);

        iotHubService
            .GetDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns(new Device(DeviceId) { Capabilities = new DeviceCapabilities { IotEdge = true } });

        iotHubService
            .UpdateModule(Arg.Any<Module>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<Module>());
    }

    [Fact]
    public async Task Provision_EdgeHubWithoutCredentials_AssignsSymmetricKeys()
    {
        // Arrange
        // IoT Hub creates $edgeHub with authentication type 'none'.
        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), new Module(DeviceId, EdgeHubModuleId)]);

        // Act
        await provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        await iotHubService
            .Received(1)
            .UpdateModule(
                Arg.Is<Module>(x =>
                    x.Id == EdgeHubModuleId &&
                    x.Authentication.Type == AuthenticationType.Sas &&
                    !string.IsNullOrEmpty(x.Authentication.SymmetricKey.PrimaryKey) &&
                    !string.IsNullOrEmpty(x.Authentication.SymmetricKey.SecondaryKey)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_ModulesWithKeys_DoesNotUpdateModules()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        // Act
        var identity = await provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        identity.Should().Be(new IoTEdgeDeviceIdentity(IotHubHostName, DeviceId));

        await iotHubService
            .DidNotReceive()
            .UpdateModule(Arg.Any<Module>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public Task Provision_AssigningKeysRefusedForAWrongKey_ThrowsWithTheIoTHubErrorAndAHint()
    {
        // Arrange
        // As the IoT Hub SDK words a wrong key: IoT Hub's JSON inside the SDK's JSON.
        const string sdkMessage =
            """
            {"Message":"{\"errorCode\":401002,\"message\":\"The specified SAS token has an invalid signature.\"}","ExceptionMessage":""}
            """;

        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), new Module(DeviceId, EdgeHubModuleId)]);

        iotHubService
            .UpdateModule(Arg.Any<Module>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException(sdkMessage));

        // Act
        var act = () => provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not assign symmetric keys to module identity '$edgeHub' on IoT Edge device 'mydevice': The specified SAS token has an invalid signature. (401002) Check the connection string's key and its registry write permissions.");
    }

    [Fact]
    public async Task Provision_DeviceMissing_CreatesIoTEdgeDevice()
    {
        // Arrange
        iotHubService
            .GetDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        iotHubService
            .CreateDevice(DeviceId, edgeEnabled: true, Arg.Any<CancellationToken>())
            .Returns(new Device(DeviceId));

        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        // Act
        await provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        await iotHubService
            .Received(1)
            .CreateDevice(DeviceId, edgeEnabled: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public Task Provision_IoTHubRefusesTheDeviceRead_ThrowsWithTheIoTHubError()
    {
        // Arrange
        iotHubService
            .GetDevice(DeviceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        var act = () => provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not create IoT Edge device 'mydevice' in IoT Hub 'myhub.azure-devices.net': Too many requests");
    }

    [Fact]
    public async Task Provision_ExistingDeviceIsNotIoTEdge_ThrowsWithoutCreatingADevice()
    {
        // Arrange
        iotHubService
            .GetDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns(new Device(DeviceId));

        // Act
        var act = () => provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not an IoT Edge device*");

        await iotHubService
            .DidNotReceive()
            .CreateDevice(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_DeviceUsedByIoTEdgeRuntime_ThrowsWithoutChangingIt()
    {
        // Arrange
        // A real runtime reports its status in the $edgeAgent twin.
        iotHubService
            .GetModuleTwin(DeviceId, "$edgeAgent", Arg.Any<CancellationToken>())
            .Returns(new Twin { Properties = new TwinProperties { Reported = new TwinCollection("""{ "lastDesiredStatus": { "code": 200 } }""") } });

        // Act
        var act = () => provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, EdgeHubConfiguration.Default, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*is used by a real IoT Edge runtime*nothing was changed*");

        await iotHubService
            .DidNotReceive()
            .AddModuleToDevice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await iotHubService
            .DidNotReceive()
            .ApplyConfigurationContentOnDevice(Arg.Any<string>(), Arg.Any<ConfigurationContent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_ModuleMissing_CreatesOnlyTheMissingModule()
    {
        // Arrange
        const string newModuleId = "newmodule";

        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        iotHubService
            .AddModuleToDevice(DeviceId, newModuleId, Arg.Any<CancellationToken>())
            .Returns(CreateModule(newModuleId, "newmodulekey"));

        // Act
        await provisioner.Provision(DeviceId, [ModuleId, newModuleId], deploymentManifest: null, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        await iotHubService
            .Received(1)
            .AddModuleToDevice(DeviceId, newModuleId, Arg.Any<CancellationToken>());

        await iotHubService
            .DidNotReceive()
            .UpdateModule(Arg.Any<Module>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_DeploymentManifest_AppliesDesiredPropertiesOnlyToProvisionedModules()
    {
        // Arrange
        var manifest = DeploymentManifest.Parse(
            """
            {
              "modulesContent": {
                "mymodule": { "properties.desired": { "interval": 5 } },
                "othermodule": { "properties.desired": { "interval": 10 } }
              }
            }
            """);

        var patches = SetUpDesiredPropertiesUpdate(currentDesiredProperties: "{}");

        // Act
        await provisioner.Provision(DeviceId, [ModuleId], manifest, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        patches.Should().Equal("""{"interval":5}""");

        await iotHubService
            .DidNotReceive()
            .UpdateDesiredProperties(DeviceId, "othermodule", Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_TwinPropertyMissingFromManifest_PatchesItToNull()
    {
        // Arrange
        var manifest = DeploymentManifest.Parse(
            """{ "modulesContent": { "mymodule": { "properties.desired": { "interval": 5 } } } }""");

        var patches = SetUpDesiredPropertiesUpdate(currentDesiredProperties: """{ "interval": 1, "old": true }""");

        // Act
        await provisioner.Provision(DeviceId, [ModuleId], manifest, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        patches.Should().Equal("""{"interval":5,"old":null}""");
    }

    [Fact]
    public async Task Provision_IoTHubRefusesTheDesiredProperties_LogsAWarningAndStillProvisionsTheDevice()
    {
        // Arrange
        var manifest = DeploymentManifest.Parse(
            """{ "modulesContent": { "mymodule": { "properties.desired": { "interval": 5 } } } }""");

        SetUpDesiredPropertiesUpdate(currentDesiredProperties: "{}");

        iotHubService
            .UpdateDesiredProperties(DeviceId, ModuleId, Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        var identity = await provisioner.Provision(DeviceId, [ModuleId], manifest, edgeHubConfiguration: null, CancellationToken.None);

        // Assert
        identity.DeviceId.Should().Be(DeviceId);

        logger.Collector.GetSnapshot().Should().Contain(x =>
            x.Level == LogLevel.Warning &&
            x.Message.Contains("Could not apply desired properties", StringComparison.Ordinal) &&
            x.Message.EndsWith("Too many requests", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Provision_EdgeHubConfiguration_AppliesItAsASingleDeviceDeployment()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        ConfigurationContent? content = null;
        iotHubService
            .ApplyConfigurationContentOnDevice(DeviceId, Arg.Do<ConfigurationContent>(x => content = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, EdgeHubConfiguration.Default, CancellationToken.None);

        // Assert
        content!.ModulesContent[EdgeHubModuleId]["properties.desired"].ToString().Should()
            .Be(Newtonsoft.Json.Linq.JObject.Parse(EdgeHubConfiguration.Default).ToString());

        await iotHubService
            .DidNotReceive()
            .UpdateDesiredProperties(DeviceId, EdgeHubModuleId, Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public Task Provision_EdgeHubConfigurationRefusedForAWrongKey_ThrowsWithTheIoTHubErrorAndAHint()
    {
        // Arrange
        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        iotHubService
            .ApplyConfigurationContentOnDevice(DeviceId, Arg.Any<ConfigurationContent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException("Unauthorized"));

        // Act
        var act = () => provisioner.Provision(DeviceId, [ModuleId], deploymentManifest: null, EdgeHubConfiguration.Default, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not apply the edgeHub configuration to IoT Edge device 'mydevice': Unauthorized Check the connection string's key and its registry write permissions.");
    }

    [Fact]
    public async Task Deprovision_DeviceExists_DeletesIt()
    {
        // Arrange
        iotHubService
            .DeleteDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await provisioner.Deprovision(DeviceId, CancellationToken.None);

        // Assert
        logger.Collector.LatestRecord.Message.Should().Contain($"Deleted IoT Edge device '{DeviceId}'");
    }

    [Fact]
    public async Task Deprovision_DeviceIsAlreadyGone_CompletesAndSaysSo()
    {
        // Arrange
        iotHubService
            .DeleteDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await provisioner.Deprovision(DeviceId, CancellationToken.None);

        // Assert
        logger.Collector.LatestRecord.Message.Should().Contain($"IoT Edge device '{DeviceId}' was already deleted");
    }

    private List<string> SetUpDesiredPropertiesUpdate(
        string currentDesiredProperties)
    {
        iotHubService
            .GetModulesOnIotEdgeDevice(DeviceId, Arg.Any<CancellationToken>())
            .Returns([CreateModule(ModuleId, "modulekey"), CreateModule(EdgeHubModuleId, "edgehubkey")]);

        iotHubService
            .GetModuleTwin(DeviceId, ModuleId, Arg.Any<CancellationToken>())
            .Returns(new Twin { Properties = new TwinProperties { Desired = new TwinCollection(currentDesiredProperties) } });

        var patches = new List<string>();
        iotHubService
            .UpdateDesiredProperties(DeviceId, ModuleId, Arg.Do<TwinCollection>(x => patches.Add(x.ToJson())), Arg.Any<CancellationToken>())
            .Returns(new Twin());

        return patches;
    }

    private static Module CreateModule(
        string moduleId,
        string primaryKey)
        => new(DeviceId, moduleId)
        {
            Authentication = new AuthenticationMechanism
            {
                Type = AuthenticationType.Sas,
                SymmetricKey = new SymmetricKey
                {
                    PrimaryKey = primaryKey,
                    SecondaryKey = primaryKey,
                },
            },
        };
}