namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeDeviceBuilderExtensionsTests
{
    [Fact]
    public void AddIoTEdgeDevice_InvalidDeviceId_ThrowsArgumentException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var connectionString = builder.AddParameter("iothub-connection-string", secret: true);

        // Act
        var act = () => builder.AddIoTEdgeDevice("edge-device", connectionString, deviceId: "my device");

        // Assert
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("deviceId");
    }

    [Fact]
    public void AddIoTEdgeDevice_NoDeviceId_UsesTheNameAndMachineNameInLowercase()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();

        // Act
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Assert
        device.Resource.DeviceId.Should().Be($"edge-device-{Environment.MachineName}".ToLowerInvariant());
        IoTHubIdentifier.IsValid(device.Resource.DeviceId).Should().BeTrue();
    }

    [Fact]
    public void WithDeploymentManifest_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        var act = () => device.WithDeploymentManifest("does-not-exist/deployment.json");

        // Assert
        act.Should().Throw<FileNotFoundException>().WithMessage("*deployment.json' does not exist.");
    }

    [Fact]
    public void WithDeploymentManifest_RelativePath_ResolvesAgainstTheAppHostDirectory()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        var directoryName = $"atc-iotedge-tests-{Guid.NewGuid():N}";
        var directory = Directory.CreateDirectory(Path.Combine(builder.AppHostDirectory, directoryName));

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "deployment.json"), "{}");

            // Act
            device.WithDeploymentManifest($"{directoryName}/deployment.json");

            // Assert
            device.Resource.DeploymentManifestPath.Should().Be(Path.Combine(directory.FullName, "deployment.json"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WithUpstreamProtocol_Mqtt_PassesTheProtocolToEdgeHub()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        device.WithEdgeHub(edgeHub => edgeHub.WithUpstreamProtocol(EdgeHubUpstreamProtocol.Mqtt));

        // Assert
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(device.Resource.EdgeHub!);
        environment.Should().Contain("UpstreamProtocol", "Mqtt");
    }

    [Fact]
    public void WithDeviceId_Parameter_ReplacesTheFixedDeviceId()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithDeviceId("fixed-id");
        var deviceId = builder.AddParameter("edge-device-id");

        // Act
        device.WithDeviceId(deviceId);

        // Assert
        device.Resource.DeviceIdParameter.Should().BeSameAs(deviceId.Resource);
    }

    [Fact]
    public void WithDeviceId_FixedIdAfterParameter_ClearsTheParameter()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithDeviceId(builder.AddParameter("edge-device-id"));

        // Act
        device.WithDeviceId("fixed-id");

        // Assert
        device.Resource.DeviceIdParameter.Should().BeNull();
        device.Resource.DeviceId.Should().Be("fixed-id");
        GetReferencedResourceNames(device).Should().NotContain("edge-device-id");
    }

    [Fact]
    public void WithDeviceId_OtherParameter_ReferencesOnlyTheNewParameter()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithDeviceId(builder.AddParameter("first-device-id"));
        var secondDeviceId = builder.AddParameter("second-device-id");

        // Act
        device.WithDeviceId(secondDeviceId);

        // Assert
        device.Resource.DeviceIdParameter.Should().BeSameAs(secondDeviceId.Resource);
        GetReferencedResourceNames(device).Should().Contain("second-device-id").And.NotContain("first-device-id");
    }

    [Fact]
    public void WithDeviceId_InvalidFixedId_ThrowsArgumentException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        var act = () => device.WithDeviceId("hash#id");

        // Assert
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("deviceId");
    }

    [Fact]
    public void WithEdgeHub_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();

        // Act
        var act = () => device.WithEdgeHub();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'edge-device' already has an edgeHub*");
    }

    [Fact]
    public void WithEdgeHub_SecondDeviceOnTheStandardPorts_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        var secondDevice = AddSecondDevice(builder);

        // Act
        var act = () => secondDevice.WithEdgeHub();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'second-device'*'edge-device-edgehub'*");
        secondDevice.Resource.EdgeHub.Should().BeNull();
    }

    [Fact]
    public void WithEdgeHub_SecondDeviceOnOtherPorts_AddsItsEdgeHub()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        var secondDevice = AddSecondDevice(builder);

        // Act
        secondDevice.WithEdgeHub(mqttPort: 18883, amqpPort: 15671, httpsPort: null);

        // Assert
        secondDevice.Resource.EdgeHub.Should().NotBeNull();
    }

    [Fact]
    public void WithEdgeHub_HttpsPortNull_PublishesOnlyMqttAndAmqp()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        device.WithEdgeHub(httpsPort: null);

        // Assert
        device.Resource.EdgeHub!.Annotations
            .OfType<EndpointAnnotation>()
            .Select(x => $"{x.Name}:{x.Port}->{x.TargetPort}")
            .Should().Equal("mqtt:8883->8883", "amqp:5671->5671");
    }

    [Fact]
    public void WithEdgeHub_ModuleAttachedBefore_WaitsUntilEdgeHubIsHealthy()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        // Act
        device.WithEdgeHub();

        // Assert
        IoTEdgeTestHelpers.GetEdgeHubWaitTypes(module).Should().Equal(WaitType.WaitUntilHealthy);
    }

    [Fact]
    public async Task WithEdgeHub_ProvisionedDevice_PassesTheIdentityCertificatesAndTwinConfigurationSource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(device.Resource.EdgeHub!);

        // Assert
        environment.Should()
            .Contain("configSource", "twin")
            .And.Contain("IotHubConnectionString", "HostName=myhub.azure-devices.net;DeviceId=edge-device;ModuleId=$edgeHub;SharedAccessKey=edgehubkey")
            .And.Contain("EdgeModuleHubServerCertificateFile", $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.ServerCertificateFileName}")
            .And.Contain("EdgeModuleHubServerCAChainCertificateFile", $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.ChainCertificateFileName}")
            .And.NotContainKey("StorageFolder");

        // The routes come from the $edgeHub twin, not from the environment.
        GetRoutes(environment).Should().BeEmpty();
    }

    [Fact]
    public Task WithEdgeHub_DeviceDeleted_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);
        device.Resource.SetDeleted();

        // Act
        var act = () => IoTEdgeTestHelpers.GetEnvironmentVariables(device.Resource.EdgeHub!);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was deleted from IoT Hub, so 'edge-device-edgehub' can't connect*");
    }

    [Fact]
    public async Task WithDataVolume_DefaultName_StoresTheEdgeHubStateInAVolume()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<IoTEdgeHubResource>? edgeHub = null;
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub(x => edgeHub = x);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var result = edgeHub!.WithDataVolume();

        // Assert
        var mount = result.Resource.Annotations
            .OfType<ContainerMountAnnotation>()
            .Should().ContainSingle(x => x.Type == ContainerMountType.Volume)
            .Subject;

        mount.Target.Should().Be(IoTEdgeHubResource.ContainerStorageDirectory);
        mount.Source.Should().NotBeNullOrEmpty();

        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(result.Resource);
        environment.Should().Contain("StorageFolder", IoTEdgeHubResource.ContainerStorageDirectory);
    }

    private static IResourceBuilder<IoTEdgeDeviceResource> AddSecondDevice(
        IDistributedApplicationBuilder builder)
        => builder.AddIoTEdgeDevice(
            "second-device",
            builder.AddParameter("second-connection-string", secret: true));

    private static List<string> GetReferencedResourceNames(
        IResourceBuilder<IoTEdgeDeviceResource> device)
        => device.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Select(x => x.Resource.Name)
            .ToList();

    private static List<string> GetRoutes(
        Dictionary<string, string> environment)
        => environment
            .Where(x => x.Key.StartsWith("routes__", StringComparison.Ordinal))
            .Select(x => $"{x.Key}={x.Value}")
            .Order(StringComparer.Ordinal)
            .ToList();
}