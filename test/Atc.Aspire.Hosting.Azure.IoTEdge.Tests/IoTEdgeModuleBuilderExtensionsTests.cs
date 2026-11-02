namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeModuleBuilderExtensionsTests
{
    [Fact]
    public void AsIoTEdgeModule_SampleOpcPublisherContainer_AttachesTheModule()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        builder
            .AddContainer("opcpublisher", "iotedge/opc-publisher", "2.9.18")
            .WithImageRegistry("mcr.microsoft.com")
            .AsIoTEdgeModule(device);

        // Assert
        device.Resource.Modules.Should().ContainSingle()
            .Which.ModuleId.Should().Be("opcpublisher");
    }

    [Theory]
    [InlineData("$edgeHub")]
    [InlineData("my module")]
    [InlineData("plus+sign")]
    public void AsIoTEdgeModule_InvalidModuleId_ThrowsArgumentException(
        string moduleId)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var container = builder.AddContainer("module", "some/image", "1.0");

        // Act
        var act = () => container.AsIoTEdgeModule(device, moduleId);

        // Assert
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("moduleId");
        device.Resource.Modules.Should().BeEmpty();
    }

    [Fact]
    public void AsIoTEdgeModule_ModuleIdUsedTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        builder.AddContainer("first", "some/image", "1.0").AsIoTEdgeModule(device, "samemodule");
        var second = builder.AddContainer("second", "some/image", "1.0");

        // Act
        var act = () => second.AsIoTEdgeModule(device, "samemodule");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'samemodule'*already attached*");
    }

    [Fact]
    public void AsIoTEdgeModule_ResourceAttachedTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var firstDevice = IoTEdgeTestHelpers.AddDevice(builder);
        var secondDevice = builder.AddIoTEdgeDevice("second-device", builder.AddParameter("second-connection-string", secret: true));
        var container = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(firstDevice);

        // Act
        var act = () => container.AsIoTEdgeModule(secondDevice, "othermodule");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'module'*already module*'edge-device'*");
        secondDevice.Resource.Modules.Should().BeEmpty();
    }

    [Fact]
    public void AsIoTEdgeModule_AfterWithEdgeHub_WaitsUntilEdgeHubIsHealthy()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();

        // Act
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        // Assert
        IoTEdgeTestHelpers.GetEdgeHubWaitTypes(module).Should().Equal(WaitType.WaitUntilHealthy);
    }

    [Fact]
    public async Task AsIoTEdgeModule_WithoutEdgeHub_PassesAConnectionStringWithoutGateway()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        environment.Should()
            .Contain("EdgeHubConnectionString", "HostName=myhub.azure-devices.net;DeviceId=edge-device;ModuleId=module;SharedAccessKey=modulekey")
            .And.Contain("IOTEDGE_IOTHUBHOSTNAME", "myhub.azure-devices.net")
            .And.Contain("IOTEDGE_DEVICEID", "edge-device")
            .And.Contain("IOTEDGE_MODULEID", "module")
            .And.NotContainKey("IOTEDGE_GATEWAYHOSTNAME")
            .And.NotContainKey("EdgeModuleCACertificateFile");
    }

    [Fact]
    public Task AsIoTEdgeModule_IoTHubRefusesTheConnectionStringRead_FailsWithTheIoTHubError()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);
        var iotHubService = IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        iotHubService
            .GetModuleConnectionString("edge-device", "module", null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        var act = () => IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not read the connection string of module 'module' on IoT Edge device 'edge-device': Too many requests");
    }

    [Fact]
    public Task AsIoTEdgeModule_ModuleIdentityMissingInIoTHub_FailsAndSaysToRestartTheAppHost()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);
        var iotHubService = IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        iotHubService
            .GetModuleConnectionString("edge-device", "module", null, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var act = () => IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Module identity 'module' doesn't exist on IoT Edge device 'edge-device'. Restart the AppHost to provision it again.");
    }

    [Fact]
    public async Task AsIoTEdgeModule_ContainerWithEdgeHub_PassesTheEdgeHubContainerAsGatewayAndTheMountedCa()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        environment.Should()
            .Contain("EdgeHubConnectionString", "HostName=myhub.azure-devices.net;GatewayHostName=edge-device-edgehub;DeviceId=edge-device;ModuleId=module;SharedAccessKey=modulekey")
            .And.Contain("IOTEDGE_GATEWAYHOSTNAME", "edge-device-edgehub")
            .And.Contain("EdgeModuleCACertificateFile", $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.DeviceCaCertificateFileName}");
    }

    [Fact]
    public async Task AsIoTEdgeModule_HostProcessWithEdgeHub_PassesLocalhostAsGatewayAndTheHostCa()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        var module = builder.AddExecutable("module", "dotnet", ".").AsIoTEdgeModule(device);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        environment.Should()
            .Contain("EdgeHubConnectionString", "HostName=myhub.azure-devices.net;GatewayHostName=localhost;DeviceId=edge-device;ModuleId=module;SharedAccessKey=modulekey")
            .And.Contain("IOTEDGE_GATEWAYHOSTNAME", "localhost");

        // On macOS the SDK can't install the CA without a prompt, so host modules get no CA file there.
        if (OperatingSystem.IsMacOS())
        {
            environment.Should().NotContainKey("EdgeModuleCACertificateFile");
        }
        else
        {
            environment.Should().Contain("EdgeModuleCACertificateFile", device.Resource.EdgeHub!.Certificates.DeviceCaCertificateFile);
        }
    }

    [Fact]
    public async Task AsIoTEdgeModule_HostProcessInStandaloneEnvironment_ReplacesStandaloneWithDevelopment()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder
            .AddExecutable("module", "dotnet", ".")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Standalone")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Standalone")
            .AsIoTEdgeModule(device);

        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        environment.Should()
            .Contain("DOTNET_ENVIRONMENT", "Development")
            .And.Contain("ASPNETCORE_ENVIRONMENT", "Development");
    }

    [Fact]
    public async Task AsIoTEdgeModule_ContainerInStandaloneEnvironment_KeepsStandalone()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder
            .AddContainer("module", "some/image", "1.0")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Standalone")
            .AsIoTEdgeModule(device);

        IoTEdgeTestHelpers.SetProvisioned(device.Resource);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        environment.Should().Contain("DOTNET_ENVIRONMENT", "Standalone");
    }

    [Fact]
    public async Task AsIoTEdgeModule_PublishMode_SetsNoModuleEnvironment()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder).WithEdgeHub();
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        // Act
        var environment = await IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource, DistributedApplicationOperation.Publish);

        // Assert
        environment.Keys.Should().NotContain(["EdgeHubConnectionString", "EdgeModuleCACertificateFile", "IOTEDGE_MODULEID"]);
    }

    [Fact]
    public void AsIoTEdgeModule_Container_MountsTheCertificateDirectory()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        // Assert
        var mount = module.Resource.Annotations
            .OfType<ContainerMountAnnotation>()
            .Should().ContainSingle()
            .Subject;

        mount.Source.Should().Be(device.Resource.CertificateDirectory);
        mount.Target.Should().Be(IoTEdgeHubResource.ContainerCertificateDirectory);
        mount.Type.Should().Be(ContainerMountType.BindMount);
        mount.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void AsIoTEdgeModule_HostProcess_MountsNothing()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);

        // Act
        var module = builder.AddExecutable("module", "dotnet", ".").AsIoTEdgeModule(device);

        // Assert
        module.Resource.Annotations.OfType<ContainerMountAnnotation>().Should().BeEmpty();
    }

    [Fact]
    public async Task AsIoTEdgeModule_ModuleStopped_SetsTheConnectionToNotConnected()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        await using var app = builder.Build();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();

        var snapshot = new CustomResourceSnapshot
        {
            ResourceType = "Container",
            Properties = [new ResourcePropertySnapshot("iotedge.connection", "Connected")],
            State = KnownResourceStates.Exited,
        };

        // Act
        await eventing.PublishAsync(
            new ResourceStoppedEvent(module.Resource, app.Services, new ResourceEvent(module.Resource, module.Resource.Name, snapshot)),
            TestContext.Current.CancellationToken);

        // Assert
        notifications.TryGetCurrentState(module.Resource.Name, out var resourceEvent).Should().BeTrue();
        resourceEvent!.Snapshot.Properties.Should().ContainSingle(x => x.Name == "iotedge.connection")
            .Which.Value.Should().Be("Not connected");
    }

    [Fact]
    public Task AsIoTEdgeModule_DeviceDeleted_FailsToStartWithTheReason()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);
        IoTEdgeTestHelpers.SetProvisioned(device.Resource);
        device.Resource.SetDeleted();

        // Act
        var act = () => IoTEdgeTestHelpers.GetEnvironmentVariables(module.Resource);

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was deleted from IoT Hub, so 'module' can't connect*");
    }

    [Fact]
    public async Task AsIoTEdgeModule_ModuleStarting_RemovesTheConnectionOfThePreviousRun()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var device = IoTEdgeTestHelpers.AddDevice(builder);
        var module = builder.AddContainer("module", "some/image", "1.0").AsIoTEdgeModule(device);

        await using var app = builder.Build();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await notifications.PublishUpdateAsync(module.Resource, snapshot => snapshot with
        {
            Properties = [.. snapshot.Properties, new ResourcePropertySnapshot("iotedge.connection", "Not connected")],
        });

        // Act
        await app.Services
            .GetRequiredService<IDistributedApplicationEventing>()
            .PublishAsync(new BeforeResourceStartedEvent(module.Resource, app.Services), TestContext.Current.CancellationToken);

        // Assert
        notifications.TryGetCurrentState(module.Resource.Name, out var resourceEvent).Should().BeTrue();
        resourceEvent!.Snapshot.Properties.Should().NotContain(x => x.Name == "iotedge.connection");
    }
}