// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

/// <summary>
/// Extension methods for running Aspire resources as IoT Edge modules on an emulated IoT Edge device.
/// </summary>
public static partial class IoTEdgeModuleBuilderExtensions
{
    private const string ConnectionPropertyName = "iotedge.connection";
    private const string ConnectionChecking = "Checking";
    private const string ConnectionConnected = "Connected";
    private const string ConnectionNotConnected = "Not connected";
    private const int ConnectionCheckAttempts = 6;
    private const string MethodNameArgument = "methodName";
    private const string PayloadArgument = "payload";
    private const string DesiredPropertiesArgument = "desiredProperties";
    private const int MethodNotImplementedStatus = 501;
    private const string StandaloneEnvironmentName = "Standalone";
    private const string DevelopmentEnvironmentName = "Development";
    private static readonly string[] EnvironmentNameVariables = ["DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT"];

    // A module start costs at most a few IoT Hub reads: one per round, shared by the modules of a device.
    private static readonly TimeSpan ConnectionCheckInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the resource as an IoT Edge module on the device. A module identity is created in IoT Hub, and the module gets
    /// the <c>EdgeHubConnectionString</c> (and <c>EdgeModuleCACertificateFile</c> when the device has an edgeHub)
    /// environment variables that <c>ModuleClient.CreateFromEnvironmentAsync</c> reads.
    /// The resource also gets dashboard commands to invoke direct methods, update desired properties and show the module twin.
    /// </summary>
    /// <typeparam name="T">The resource type, e.g. a project or a container.</typeparam>
    /// <param name="builder">The module resource builder.</param>
    /// <param name="device">The IoT Edge device the module runs on.</param>
    /// <param name="moduleId">The module id in IoT Hub. Defaults to the resource name.</param>
    /// <returns>The resource builder.</returns>
    /// <remarks>
    /// After the module starts, the device checks for about a minute whether the module connected, and shows the result
    /// as the <c>iotedge.connection</c> property and in the module log. The module also gets a "Check connection" command.
    /// </remarks>
    /// <exception cref="ArgumentException">The module id isn't a valid IoT Hub module id, or starts with <c>$</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Another module on the device already uses the module id, or the resource is already an IoT Edge module.
    /// </exception>
    public static IResourceBuilder<T> AsIoTEdgeModule<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<IoTEdgeDeviceResource> device,
        string? moduleId = null)
        where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(device);

        moduleId = string.IsNullOrWhiteSpace(moduleId)
            ? builder.Resource.Name
            : moduleId;

        IoTHubIdentifier.EnsureValidModuleId(moduleId, nameof(moduleId));

        if (builder.Resource.TryGetLastAnnotation<IoTEdgeModuleAnnotation>(out var existingModule))
        {
            throw new InvalidOperationException(
                $"Resource '{builder.Resource.Name}' is already module '{existingModule.ModuleId}' on IoT Edge device '{existingModule.Device.Name}'.");
        }

        var deviceResource = device.Resource;
        var module = new IoTEdgeModuleAnnotation(deviceResource, builder.Resource, moduleId);
        deviceResource.AddModule(module);

        builder
            .WithAnnotation(module)
            .WaitFor(device);

        // When the edgeHub is added after the module, WithEdgeHub adds the wait itself.
        if (deviceResource.EdgeHub is not null)
        {
            builder.WithAnnotation(new WaitAnnotation(deviceResource.EdgeHub, WaitType.WaitUntilHealthy));
        }

        if (builder.Resource is ContainerResource)
        {
            builder.WithAnnotation(
                new ContainerMountAnnotation(
                    deviceResource.CertificateDirectory,
                    IoTEdgeHubResource.ContainerCertificateDirectory,
                    ContainerMountType.BindMount,
                    isReadOnly: true));
        }

        var connectionWatch = new ConnectionWatch();

        return builder
            .WithEnvironment(context => ConfigureModuleEnvironment(context, module))
            .WithModuleCommands(module)
            .OnBeforeResourceStarted((_, startedEvent, _) =>
            {
                // A new start shouldn't show the connection state of the previous run.
                return startedEvent.Services
                    .GetRequiredService<ResourceNotificationService>()
                    .PublishUpdateAsync(module.ModuleResource, snapshot => snapshot with
                    {
                        Properties = [.. snapshot.Properties.Where(x => x.Name != ConnectionPropertyName)],
                    });
            })
            .OnResourceReady((_, readyEvent, _) =>
            {
                StartConnectionCheck(module, connectionWatch, readyEvent.Services);
                return Task.CompletedTask;
            })
            .OnResourceStopped((_, stoppedEvent, _) =>
            {
                // A stopped module isn't connected, and its check would only end with a misleading warning.
                connectionWatch.Stop();

                return PublishConnectionState(
                    stoppedEvent.Services.GetRequiredService<ResourceNotificationService>(),
                    module,
                    ConnectionNotConnected);
            });
    }

    private static async Task ConfigureModuleEnvironment(
        EnvironmentCallbackContext context,
        IoTEdgeModuleAnnotation module)
    {
        if (!context.ExecutionContext.IsRunMode)
        {
            return;
        }

        var device = module.Device;
        var identity = await device.WaitForProvisioningAsync(context.CancellationToken);
        device.EnsureNotDeleted(module.ModuleResource.Name);

        var isContainer = module.ModuleResource is ContainerResource;

        var gatewayHostName = device.EdgeHub switch
        {
            null => null,
            _ when isContainer => device.EdgeHub.ContainerHostName,
            _ => "localhost",
        };

        var environment = context.EnvironmentVariables;
        environment[EdgeHubEnvironmentVariableNames.EdgeHubConnectionString] =
            await device.GetModuleConnectionStringAsync(identity, module.ModuleId, gatewayHostName, context.CancellationToken);
        environment["IOTEDGE_IOTHUBHOSTNAME"] = identity.IotHubHostName;
        environment["IOTEDGE_DEVICEID"] = identity.DeviceId;
        environment["IOTEDGE_MODULEID"] = module.ModuleId;

        if (!isContainer)
        {
            ReplaceStandaloneEnvironment(environment);
        }

        if (device.EdgeHub is null)
        {
            return;
        }

        environment["IOTEDGE_GATEWAYHOSTNAME"] = gatewayHostName!;

        if (isContainer)
        {
            environment[EdgeHubEnvironmentVariableNames.EdgeModuleCaCertificateFile] =
                $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.DeviceCaCertificateFileName}";
        }
        else if (!OperatingSystem.IsMacOS())
        {
            // The SDK installs this CA into the current user's root store, which macOS does not allow without a prompt.
            // On macOS, a module running on the host must accept the edgeHub test certificate itself.
            environment[EdgeHubEnvironmentVariableNames.EdgeModuleCaCertificateFile] = device.EdgeHub.Certificates.DeviceCaCertificateFile;
        }
    }

    private static void ReplaceStandaloneEnvironment(
        Dictionary<string, object> environment)
    {
        // Modules built on Atc.Azure.IoTEdge use a mock ModuleClient when the host environment is Standalone (IsStandaloneMode),
        // and their launch profiles often set it. Aspire applies the launch profile before this callback runs,
        // so without this the module would start but never connect.
        foreach (var name in EnvironmentNameVariables)
        {
            if (environment.TryGetValue(name, out var value) &&
                value is string environmentName &&
                environmentName.Equals(StandaloneEnvironmentName, StringComparison.Ordinal))
            {
                environment[name] = DevelopmentEnvironmentName;
            }
        }
    }

    private static IResourceBuilder<T> WithModuleCommands<T>(
        this IResourceBuilder<T> builder,
        IoTEdgeModuleAnnotation module)
        where T : IResource
    {
        var device = module.Device;

        ResourceCommandState UpdateState(UpdateCommandStateContext context)
            => device.IsProvisioned && context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                ? ResourceCommandState.Enabled
                : ResourceCommandState.Disabled;

        return builder
            .WithCommand(
                name: "iotedge-invoke-method",
                displayName: "Invoke direct method",
                executeCommand: context => InvokeDirectMethod(context, module),
                commandOptions: new CommandOptions
                {
                    Description = $"Invokes a direct method on module '{module.ModuleId}' through IoT Hub.",
                    IconName = "Play",
                    UpdateState = UpdateState,
                    Arguments =
                    [
                        new InteractionInput
                        {
                            Name = MethodNameArgument,
                            Label = "Method name",
                            InputType = InputType.Text,
                            Required = true,
                        },
                        new InteractionInput
                        {
                            Name = PayloadArgument,
                            Label = "JSON payload",
                            InputType = InputType.Text,
                            Placeholder = "{}",
                        },
                    ],
                    ValidateArguments = context => ValidateJsonArgument(context, PayloadArgument, requireObject: false),
                    Progress = new CommandProgressOptions
                    {
                        Message = $"Invoking the direct method on '{module.ModuleId}'. IoT Hub waits for the module to answer.",
                    },
                })
            .WithCommand(
                name: "iotedge-update-desired-properties",
                displayName: "Update desired properties",
                executeCommand: context => UpdateDesiredProperties(context, module),
                commandOptions: new CommandOptions
                {
                    Description = $"Merges a JSON object into the desired properties of the '{module.ModuleId}' module twin.",
                    IconName = "Edit",
                    UpdateState = _ => device.IsProvisioned
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Disabled,
                    Arguments =
                    [
                        new InteractionInput
                        {
                            Name = DesiredPropertiesArgument,
                            Label = "Desired properties (JSON object)",
                            InputType = InputType.Text,
                            Required = true,
                            Placeholder = "{ \"setting\": \"value\" }",
                        },
                    ],
                    ValidateArguments = context => ValidateJsonArgument(context, DesiredPropertiesArgument, requireObject: true),
                })
            .WithCommand(
                name: "iotedge-show-module-twin",
                displayName: "Show module twin",
                executeCommand: context => ShowModuleTwin(context, module),
                commandOptions: new CommandOptions
                {
                    Description = $"Shows the '{module.ModuleId}' module twin from IoT Hub.",
                    IconName = "DocumentText",
                    UpdateState = _ => device.IsProvisioned
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Disabled,
                })
            .WithCommand(
                name: "iotedge-check-connection",
                displayName: "Check connection",
                executeCommand: context => CheckConnection(context, module),
                commandOptions: new CommandOptions
                {
                    Description = $"Checks whether '{module.ModuleId}' is connected. Reads from IoT Hub, so it counts against the daily quota.",
                    IconName = "PlugConnected",

                    // A stopped module can't be connected, and IoT Hub may still report a direct connection for minutes.
                    UpdateState = UpdateState,
                });
    }

    private static void StartConnectionCheck(
        IoTEdgeModuleAnnotation module,
        ConnectionWatch connectionWatch,
        IServiceProvider services)
    {
        var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(module.ModuleResource);

        // A restart replaces a check that is still running.
        var cancellationToken = connectionWatch.Restart(stopping);

        // Runs in the background, so the resource isn't held up. Stops when the module or the AppHost stops.
        _ = Task.Run(() => WatchConnection(module, notifications, logger, cancellationToken), CancellationToken.None);
    }

    private static async Task WatchConnection(
        IoTEdgeModuleAnnotation module,
        ResourceNotificationService notifications,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishConnectionState(notifications, module, ConnectionChecking);

            IotHubException? lastError = null;

            for (var attempt = 0; attempt < ConnectionCheckAttempts; attempt++)
            {
                await Task.Delay(ConnectionCheckInterval, module.Device.TimeProvider, cancellationToken);

                if (module.Device.IsDeleted)
                {
                    return;
                }

                bool connected;

                try
                {
                    connected = await IsConnected(module, cancellationToken);
                }
                catch (IotHubException ex)
                {
                    // E.g. throttling: the next attempt may get an answer.
                    lastError = ex;
                    continue;
                }

                // The module may have stopped while IoT Hub answered.
                cancellationToken.ThrowIfCancellationRequested();

                if (connected)
                {
                    await PublishConnectionState(notifications, module, ConnectionConnected);
                    logger.LogModuleConnected(module.ModuleId, GetConnectionTarget(module));
                    return;
                }
            }

            await PublishConnectionState(notifications, module, ConnectionNotConnected);

            if (lastError is not null)
            {
                logger.LogConnectionCheckFailed(lastError, module.ModuleId, lastError.GetErrorMessage());
                return;
            }

            var waitedSeconds = (int)(ConnectionCheckInterval * ConnectionCheckAttempts).TotalSeconds;
            if (module.Device.EdgeHub is not null)
            {
                logger.LogModuleNotConnectedToEdgeHub(module.ModuleId, waitedSeconds);
            }
            else
            {
                // IoT Hub can take minutes to report a direct connection, so this isn't a warning.
                logger.LogModuleConnectionNotReportedYet(module.ModuleId, waitedSeconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The AppHost is stopping.
        }
        catch (Exception ex)
        {
            logger.LogConnectionCheckFailed(ex, module.ModuleId, ex is IotHubException iotHubException ? iotHubException.GetErrorMessage() : ex.Message);
        }
    }

    private static async Task<ExecuteCommandResult> CheckConnection(
        ExecuteCommandContext context,
        IoTEdgeModuleAnnotation module)
    {
        if (!module.Device.IsProvisioned)
        {
            return CommandResults.Failure("The device has not been provisioned.");
        }

        bool connected;

        try
        {
            connected = await IsConnected(module, context.CancellationToken);
        }
        catch (IotHubException ex)
        {
            return CommandResults.Failure($"Could not read whether '{module.ModuleId}' is connected: {ex.GetErrorMessage()}");
        }

        var notifications = context.Services.GetRequiredService<ResourceNotificationService>();
        await PublishConnectionState(notifications, module, connected ? ConnectionConnected : ConnectionNotConnected);

        var target = GetConnectionTarget(module);
        return connected
            ? CommandResults.Success($"'{module.ModuleId}' is connected to {target}.")
            : CommandResults.Failure($"'{module.ModuleId}' is not connected to {target}.");
    }

    private static async Task<bool> IsConnected(
        IoTEdgeModuleAnnotation module,
        CancellationToken cancellationToken)
    {
        var states = await module.Device.GetModuleConnectionStatesAsync(cancellationToken);
        return states.TryGetValue(module.ModuleId, out var state) &&
               string.Equals(state, EdgeHubClients.ConnectedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConnectionTarget(IoTEdgeModuleAnnotation module)
        => module.Device.EdgeHub is null
            ? "IoT Hub"
            : "edgeHub";

    private static Task PublishConnectionState(
        ResourceNotificationService notifications,
        IoTEdgeModuleAnnotation module,
        string state)
        => notifications.PublishUpdateAsync(module.ModuleResource, snapshot => snapshot with
        {
            Properties = ResourceProperties.Set(snapshot.Properties, new ResourcePropertySnapshot(ConnectionPropertyName, state)),
        });

    private static async Task<ExecuteCommandResult> InvokeDirectMethod(
        ExecuteCommandContext context,
        IoTEdgeModuleAnnotation module)
    {
        var moduleService = module.Device.IoTHubModuleService;
        if (!module.Device.IsProvisioned || moduleService is null)
        {
            return CommandResults.Failure("The device has not been provisioned.");
        }

        var methodName = context.Arguments.GetString(MethodNameArgument);
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return CommandResults.Failure("A method name is required.");
        }

        var payload = context.Arguments.GetString(PayloadArgument);
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = "{}";
        }

        if (JsonArgument.GetError(payload, requireObject: false) is { } payloadError)
        {
            return CommandResults.Failure($"The payload is invalid. {payloadError}");
        }

        MethodResultModel result;

        try
        {
            result = await moduleService.CallMethod(
                module.Device.DeviceId,
                module.ModuleId,
                new MethodParameterModel(methodName, payload),
                requestOptions: null,
                context.CancellationToken);
        }
        catch (IotHubException ex)
        {
            // E.g. the module isn't connected or didn't answer in time. Transient errors were already retried.
            return CommandResults.Failure($"'{methodName}' on '{module.ModuleId}' failed: {ex.GetErrorMessage()}");
        }

        var message = result.Status == MethodNotImplementedStatus
            ? $"'{methodName}' on '{module.ModuleId}' returned status {result.Status}: the module has no handler for this method."
            : $"'{methodName}' on '{module.ModuleId}' returned status {result.Status}.";

        // A method without a handler answers with no payload ("null"), which isn't worth a dialog.
        if (string.IsNullOrWhiteSpace(result.JsonPayload) || result.JsonPayload.Trim() == "null")
        {
            return result.Status is >= 200 and < 300
                ? CommandResults.Success(message)
                : CommandResults.Failure(message);
        }

        var data = new CommandResultData
        {
            Value = result.JsonPayload,
            Format = CommandResultFormat.Json,
            DisplayImmediately = true,
        };

        return result.Status is >= 200 and < 300
            ? CommandResults.Success(message, data)
            : CommandResults.Failure(message, data);
    }

    private static async Task<ExecuteCommandResult> UpdateDesiredProperties(
        ExecuteCommandContext context,
        IoTEdgeModuleAnnotation module)
    {
        var iotHubService = module.Device.IoTHubService;
        if (!module.Device.IsProvisioned || iotHubService is null)
        {
            return CommandResults.Failure("The device has not been provisioned.");
        }

        var json = context.Arguments.GetString(DesiredPropertiesArgument);
        if (string.IsNullOrWhiteSpace(json) || JsonArgument.GetError(json, requireObject: true) is not null)
        {
            return CommandResults.Failure("The desired properties are invalid. Enter a JSON object, e.g. { \"setting\": \"value\" }.");
        }

        Twin twin;

        try
        {
            twin = await iotHubService.UpdateDesiredProperties(
                module.Device.DeviceId,
                module.ModuleId,
                new TwinCollection(json),
                context.CancellationToken);
        }
        catch (IotHubException ex)
        {
            return CommandResults.Failure($"Could not update the desired properties of '{module.ModuleId}': {ex.GetErrorMessage()}");
        }

        return CommandResults.Success(GetDesiredPropertiesUpdatedMessage(context, module), twin.Properties.Desired.ToJson(), CommandResultFormat.Json);
    }

    private static string GetDesiredPropertiesUpdatedMessage(
        ExecuteCommandContext context,
        IoTEdgeModuleAnnotation module)
    {
        var message = $"Updated the desired properties of '{module.ModuleId}'.";

        var notifications = context.Services.GetService<ResourceNotificationService>();
        var isRunning = notifications is not null &&
                        notifications.TryGetCurrentState(module.ModuleResource.Name, out var resourceEvent) &&
                        resourceEvent.Snapshot.State?.Text == KnownResourceStates.Running;

        if (!isRunning)
        {
            message += " The module gets them when it starts.";
        }

        // Provisioning replaces the desired properties with those from the manifest, as a deployment does.
        if (module.Device.DeploymentManifest?.ModuleDesiredProperties.ContainsKey(module.ModuleId) == true)
        {
            message += " The deployment manifest replaces them when the AppHost restarts.";
        }

        return message;
    }

    private static async Task<ExecuteCommandResult> ShowModuleTwin(
        ExecuteCommandContext context,
        IoTEdgeModuleAnnotation module)
    {
        var iotHubService = module.Device.IoTHubService;
        if (!module.Device.IsProvisioned || iotHubService is null)
        {
            return CommandResults.Failure("The device has not been provisioned.");
        }

        Twin? twin;

        try
        {
            twin = await iotHubService.GetModuleTwin(module.Device.DeviceId, module.ModuleId, context.CancellationToken);
        }
        catch (IotHubException ex)
        {
            return CommandResults.Failure($"Could not get the twin of '{module.ModuleId}': {ex.GetErrorMessage()}");
        }

        return twin is null
            ? CommandResults.Failure($"Module identity '{module.ModuleId}' doesn't exist on IoT Edge device '{module.Device.DeviceId}'.")
            : CommandResults.Success($"Module twin of '{module.ModuleId}'.", twin.ToJson(), CommandResultFormat.Json, displayImmediately: true);
    }

    private static Task ValidateJsonArgument(
        InputsDialogValidationContext context,
        string argumentName,
        bool requireObject)
    {
        // An empty value is left to the Required flag; an empty payload means {}.
        var json = context.Inputs.GetString(argumentName);
        if (!string.IsNullOrWhiteSpace(json) &&
            JsonArgument.GetError(json, requireObject) is { } error)
        {
            context.AddValidationError(argumentName, error);
        }

        return Task.CompletedTask;
    }
}