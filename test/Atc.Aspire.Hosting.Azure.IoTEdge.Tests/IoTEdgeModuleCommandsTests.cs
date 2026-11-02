namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests;

public sealed class IoTEdgeModuleCommandsTests : IAsyncDisposable
{
    private const string InvokeMethodCommand = "iotedge-invoke-method";
    private const string UpdateDesiredPropertiesCommand = "iotedge-update-desired-properties";
    private const string ShowModuleTwinCommand = "iotedge-show-module-twin";
    private const string CheckConnectionCommand = "iotedge-check-connection";

    private readonly IIoTHubService iotHubService = Substitute.For<IIoTHubService>();
    private readonly IIoTHubModuleService moduleService = Substitute.For<IIoTHubModuleService>();
    private readonly DistributedApplication app;
    private readonly IServiceProvider services;
    private readonly IResourceBuilder<IoTEdgeDeviceResource> device;
    private readonly IResourceBuilder<ContainerResource> module;

    public IoTEdgeModuleCommandsTests()
    {
        var builder = DistributedApplication.CreateBuilder();

        device = builder
            .AddIoTEdgeDevice("edge-device", builder.AddParameter("iothub-connection-string", secret: true))
            .WithDeviceId("edge-device");

        module = builder
            .AddContainer("module", "some/image", "1.0")
            .AsIoTEdgeModule(device);

        device.Resource.IoTHubService = iotHubService;
        device.Resource.IoTHubModuleService = moduleService;

        // Built, not started: the commands read the module state from its ResourceNotificationService.
        app = builder.Build();
        services = app.Services;
    }

    public ValueTask DisposeAsync()
        => app.DisposeAsync();

    [Theory]
    [InlineData(InvokeMethodCommand, "Running", ResourceCommandState.Enabled)]
    [InlineData(InvokeMethodCommand, "Exited", ResourceCommandState.Disabled)]
    [InlineData(UpdateDesiredPropertiesCommand, "Exited", ResourceCommandState.Enabled)]
    [InlineData(ShowModuleTwinCommand, "Exited", ResourceCommandState.Enabled)]
    [InlineData(CheckConnectionCommand, "Running", ResourceCommandState.Enabled)]
    [InlineData(CheckConnectionCommand, "Exited", ResourceCommandState.Disabled)]
    public void UpdateState_ProvisionedDevice_RequiresARunningModuleOnlyForMethodsAndConnectionChecks(
        string commandName,
        string moduleState,
        ResourceCommandState expected)
    {
        // Arrange
        SetProvisioned();

        // Act
        var state = GetCommand(commandName).UpdateState(CreateStateContext(moduleState));

        // Assert
        state.Should().Be(expected);
    }

    [Theory]
    [InlineData(InvokeMethodCommand)]
    [InlineData(UpdateDesiredPropertiesCommand)]
    [InlineData(ShowModuleTwinCommand)]
    [InlineData(CheckConnectionCommand)]
    public void UpdateState_DeviceNotProvisioned_ReturnsDisabled(
        string commandName)
    {
        // Act
        var state = GetCommand(commandName).UpdateState(CreateStateContext("Running"));

        // Assert
        state.Should().Be(ResourceCommandState.Disabled);
    }

    [Theory]
    [InlineData(InvokeMethodCommand)]
    [InlineData(UpdateDesiredPropertiesCommand)]
    public void ValidateArguments_CommandWithJsonInput_IsConfigured(
        string commandName)
    {
        // Act
        var command = GetCommand(commandName);

        // Assert
        command.ValidateArguments.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeDirectMethod_EmptyPayload_SendsAnEmptyObject()
    {
        // Arrange
        SetProvisioned();

        moduleService
            .CallMethod(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MethodParameterModel>(), Arg.Any<IoTHubRequestOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new MethodResultModel(200, "{}"));

        // Act
        await ExecuteAsync(InvokeMethodCommand, ("methodName", "GetEndpoints"), ("payload", string.Empty));

        // Assert
        await moduleService
            .Received(1)
            .CallMethod("edge-device", "module", new MethodParameterModel("GetEndpoints", "{}"), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeDirectMethod_Status200_SucceedsAndShowsTheResponse()
    {
        // Arrange
        SetProvisioned();

        moduleService
            .CallMethod(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MethodParameterModel>(), Arg.Any<IoTHubRequestOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new MethodResultModel(200, """{"endpoints":[]}"""));

        // Act
        var result = await ExecuteAsync(InvokeMethodCommand, ("methodName", "GetEndpoints"), ("payload", """{ "pageIndex": 0 }"""));

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("'GetEndpoints' on 'module' returned status 200.");
        result.Data.Should().BeEquivalentTo(new CommandResultData
        {
            Value = """{"endpoints":[]}""",
            Format = CommandResultFormat.Json,
            DisplayImmediately = true,
        });
    }

    [Fact]
    public async Task InvokeDirectMethod_Status501_FailsAndShowsTheResponse()
    {
        // Arrange
        SetProvisioned();

        moduleService
            .CallMethod(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MethodParameterModel>(), Arg.Any<IoTHubRequestOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new MethodResultModel(501, """{"message":"Method not found"}"""));

        // Act
        var result = await ExecuteAsync(InvokeMethodCommand, ("methodName", "Reboot"), ("payload", "{}"));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("'Reboot' on 'module' returned status 501: the module has no handler for this method.");
        result.Data!.Value.Should().Be("""{"message":"Method not found"}""");
    }

    [Fact]
    public async Task InvokeDirectMethod_ResponseWithoutPayload_FailsWithoutResultData()
    {
        // Arrange
        SetProvisioned();

        moduleService
            .CallMethod(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MethodParameterModel>(), Arg.Any<IoTHubRequestOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new MethodResultModel(501, "null"));

        // Act
        var result = await ExecuteAsync(InvokeMethodCommand, ("methodName", "Reboot"), ("payload", "{}"));

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task InvokeDirectMethod_IoTHubError_FailsWithTheReason()
    {
        // Arrange
        SetProvisioned();

        moduleService
            .CallMethod(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MethodParameterModel>(), Arg.Any<IoTHubRequestOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IotHubException("The module is not online."));

        // Act
        var result = await ExecuteAsync(InvokeMethodCommand, ("methodName", "GetEndpoints"), ("payload", "{}"));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("'GetEndpoints' on 'module' failed: The module is not online.");
    }

    [Fact]
    public async Task UpdateDesiredProperties_ValidObject_SucceedsWithTheUpdatedDesiredProperties()
    {
        // Arrange
        SetProvisioned();

        iotHubService
            .UpdateDesiredProperties("edge-device", "module", Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>())
            .Returns(new Twin { Properties = new TwinProperties { Desired = new TwinCollection("""{"interval":5}""") } });

        // Act
        var result = await ExecuteAsync(UpdateDesiredPropertiesCommand, ("desiredProperties", """{ "interval": 5 }"""));

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("""{"interval":5}""");
    }

    [Fact]
    public async Task UpdateDesiredProperties_ModuleNotRunning_SaysTheModuleGetsThemWhenItStarts()
    {
        // Arrange
        SetProvisioned();
        SetUpDesiredPropertiesUpdate();

        await SetModuleStateAsync(KnownResourceStates.Exited);

        // Act
        var result = await ExecuteAsync(UpdateDesiredPropertiesCommand, ("desiredProperties", """{ "interval": 1 }"""));

        // Assert
        result.Message.Should().Be("Updated the desired properties of 'module'. The module gets them when it starts.");
    }

    [Fact]
    public async Task UpdateDesiredProperties_RunningModuleInTheManifest_SaysTheManifestReplacesThem()
    {
        // Arrange
        SetProvisioned();
        SetUpDesiredPropertiesUpdate();

        device.Resource.DeploymentManifest = DeploymentManifest.Parse(
            """{ "modulesContent": { "module": { "properties.desired": { "interval": 5 } } } }""");

        await SetModuleStateAsync(KnownResourceStates.Running);

        // Act
        var result = await ExecuteAsync(UpdateDesiredPropertiesCommand, ("desiredProperties", """{ "interval": 1 }"""));

        // Assert
        result.Message.Should().Be("Updated the desired properties of 'module'. The deployment manifest replaces them when the AppHost restarts.");
    }

    [Fact]
    public async Task UpdateDesiredProperties_IoTHubError_FailsWithTheReason()
    {
        // Arrange
        SetProvisioned();

        iotHubService
            .UpdateDesiredProperties("edge-device", "module", Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        var result = await ExecuteAsync(UpdateDesiredPropertiesCommand, ("desiredProperties", """{ "interval": 5 }"""));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Could not update the desired properties of 'module': Too many requests");
    }

    [Fact]
    public async Task ShowModuleTwin_IoTHubError_FailsWithTheReason()
    {
        // Arrange
        SetProvisioned();

        iotHubService
            .GetModuleTwin("edge-device", "module", Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException("Unauthorized"));

        // Act
        var result = await ExecuteAsync(ShowModuleTwinCommand);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Could not get the twin of 'module': Unauthorized");
    }

    [Fact]
    public async Task CheckConnection_IoTHubError_FailsWithTheReason()
    {
        // Arrange
        SetProvisioned();

        iotHubService
            .GetModulesOnIotEdgeDevice("edge-device", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ThrottlingException("Too many requests"));

        // Act
        var result = await ExecuteAsync(CheckConnectionCommand);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Could not read whether 'module' is connected: Too many requests");
    }

    [Fact]
    public async Task ShowModuleTwin_ModuleIdentityMissing_SaysItDoesNotExist()
    {
        // Arrange
        SetProvisioned();

        iotHubService
            .GetModuleTwin("edge-device", "module", Arg.Any<CancellationToken>())
            .Returns((Twin?)null);

        // Act
        var result = await ExecuteAsync(ShowModuleTwinCommand);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Module identity 'module' doesn't exist on IoT Edge device 'edge-device'.");
    }

    private void SetProvisioned()
        => device.Resource.SetProvisioned(
            new IoTEdgeDeviceIdentity("myhub.azure-devices.net", "edge-device"));

    private void SetUpDesiredPropertiesUpdate()
        => iotHubService
            .UpdateDesiredProperties("edge-device", "module", Arg.Any<TwinCollection>(), Arg.Any<CancellationToken>())
            .Returns(new Twin { Properties = new TwinProperties { Desired = new TwinCollection("""{"interval":1}""") } });

    private Task SetModuleStateAsync(string state)
        => services
            .GetRequiredService<ResourceNotificationService>()
            .PublishUpdateAsync(module.Resource, snapshot => snapshot with { State = state });

    private ResourceCommandAnnotation GetCommand(string commandName)
        => module.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(x => x.Name == commandName);

    private UpdateCommandStateContext CreateStateContext(string moduleState)
        => new()
        {
            ResourceSnapshot = new CustomResourceSnapshot
            {
                ResourceType = "Container",
                Properties = [],
                State = moduleState,
            },
            Services = services,
        };

    private static InteractionInputCollection CreateInputs(
        ResourceCommandAnnotation command,
        params (string Name, string Value)[] values)
        => new(
            command.Arguments
                .Select(x => new InteractionInput
                {
                    Name = x.Name,
                    Label = x.Label,
                    InputType = x.InputType,
                    Required = x.Required,
                    Value = values.FirstOrDefault(v => v.Name == x.Name).Value,
                })
                .ToList());

    private Task<ExecuteCommandResult> ExecuteAsync(
        string commandName,
        params (string Name, string Value)[] values)
    {
        var command = GetCommand(commandName);

        return command.ExecuteCommand(new ExecuteCommandContext
        {
            Services = services,
            Logger = NullLogger.Instance,
            ResourceName = module.Resource.Name,
            CancellationToken = TestContext.Current.CancellationToken,
            Arguments = CreateInputs(command, values),
        });
    }
}