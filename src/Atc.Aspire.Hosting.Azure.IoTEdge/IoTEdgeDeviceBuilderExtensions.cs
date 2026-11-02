// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding an emulated IoT Edge device to an Aspire application.
/// </summary>
public static partial class IoTEdgeDeviceBuilderExtensions
{
    private const string DeviceResourceType = "IoTEdgeDevice";
    private const string DeletedState = "Deleted";

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Adds an emulated IoT Edge device. On start, the device and a module identity for every resource attached with
    /// <see cref="IoTEdgeModuleBuilderExtensions.AsIoTEdgeModule{T}"/> are created in the IoT Hub (or reused if they exist).
    /// Without <see cref="WithEdgeHub"/>, modules connect directly to IoT Hub.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="iotHubConnectionString">
    /// A parameter holding an IoT Hub service connection string with registry read/write permissions
    /// (and service connect for the dashboard commands), e.g. the iothubowner policy.
    /// </param>
    /// <param name="deviceId">The device id in IoT Hub. Defaults to the resource name followed by the machine name.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeDeviceResource> AddIoTEdgeDevice(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> iotHubConnectionString,
        string? deviceId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(iotHubConnectionString);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = SanitizeDeviceId($"{name}-{Environment.MachineName}");
        }
        else
        {
            IoTHubIdentifier.EnsureValidDeviceId(deviceId, nameof(deviceId));
        }

        var certificateDirectory = Path.Combine(
            Path.GetTempPath(),
            "atc-aspire-iotedge",
            SanitizeDeviceId($"{builder.Environment.ApplicationName}-{name}"));

        Directory.CreateDirectory(certificateDirectory);

        var resource = new IoTEdgeDeviceResource(name, iotHubConnectionString.Resource, deviceId, certificateDirectory);

        return builder
            .AddResource(resource)
            .WithReferenceRelationship(iotHubConnectionString)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = DeviceResourceType,
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, deviceId),
                    new ResourcePropertySnapshot("iotedge.deviceId", deviceId),
                    new ResourcePropertySnapshot("iotedge.certificates", certificateDirectory),
                ],
            })
            .OnInitializeResource(InitializeDevice)
            .WithDeleteDeviceCommand()
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Sets the device id used in IoT Hub.
    /// </summary>
    /// <param name="builder">The device resource builder.</param>
    /// <param name="deviceId">The device id.</param>
    /// <returns>The resource builder.</returns>
    /// <exception cref="ArgumentException">The device id isn't a valid IoT Hub device id.</exception>
    public static IResourceBuilder<IoTEdgeDeviceResource> WithDeviceId(
        this IResourceBuilder<IoTEdgeDeviceResource> builder,
        string deviceId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        IoTHubIdentifier.EnsureValidDeviceId(deviceId, nameof(deviceId));

        builder.Resource.DeviceId = deviceId;
        RemoveDeviceIdParameter(builder.Resource);
        return builder;
    }

    /// <summary>
    /// Sets the device id used in IoT Hub from a parameter, e.g. from user secrets (<c>Parameters:&lt;name&gt;</c>),
    /// so each developer can use their own device id without changing code. The value is read when the device starts.
    /// </summary>
    /// <param name="builder">The device resource builder.</param>
    /// <param name="deviceId">The parameter holding the device id.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeDeviceResource> WithDeviceId(
        this IResourceBuilder<IoTEdgeDeviceResource> builder,
        IResourceBuilder<ParameterResource> deviceId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(deviceId);

        RemoveDeviceIdParameter(builder.Resource);
        builder.Resource.DeviceIdParameter = deviceId.Resource;
        return builder.WithReferenceRelationship(deviceId);
    }

    /// <summary>
    /// Uses an IoT Edge deployment manifest (or deployment template), as a deployment does: the edgeHub configuration in it
    /// (routes with priority and time to live, store and forward) is applied to the <c>$edgeHub</c> twin with a single-device
    /// deployment, and the local edgeHub reads it from there; the desired properties of each module twin replace those of the
    /// matching module identities.
    /// </summary>
    /// <param name="builder">The device resource builder.</param>
    /// <param name="path">The manifest path, relative to the AppHost directory or absolute.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeDeviceResource> WithDeploymentManifest(
        this IResourceBuilder<IoTEdgeDeviceResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var fullPath = Path.GetFullPath(path, builder.ApplicationBuilder.AppHostDirectory);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The deployment manifest '{fullPath}' does not exist.", fullPath);
        }

        builder.Resource.DeploymentManifestPath = fullPath;
        return builder;
    }

    /// <summary>
    /// Runs a local edgeHub container that the modules connect through, like on a real IoT Edge device.
    /// Test certificates are generated for it, and its MQTT, AMQP and HTTPS ports are published on the host,
    /// because the IoT device SDK always connects on the standard ports.
    /// </summary>
    /// <param name="builder">The device resource builder.</param>
    /// <param name="configure">An optional callback to configure the edgeHub container.</param>
    /// <param name="mqttPort">The host port for MQTT. Must be 8883 for modules running on the host to connect over MQTT.</param>
    /// <param name="amqpPort">The host port for AMQP. Must be 5671 for modules running on the host to connect over AMQP.</param>
    /// <param name="httpsPort">
    /// The host port for MQTT/AMQP over WebSockets. Must be 443 for modules running on the host to connect over WebSockets.
    /// Set it to <see langword="null"/> to not publish it, e.g. when port 443 is taken.
    /// </param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeDeviceResource> WithEdgeHub(
        this IResourceBuilder<IoTEdgeDeviceResource> builder,
        Action<IResourceBuilder<IoTEdgeHubResource>>? configure = null,
        int mqttPort = IoTEdgeHubResource.MqttPort,
        int amqpPort = IoTEdgeHubResource.AmqpPort,
        int? httpsPort = IoTEdgeHubResource.HttpsPort)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var device = builder.Resource;
        if (device.EdgeHub is not null)
        {
            throw new InvalidOperationException($"IoT Edge device '{device.Name}' already has an edgeHub.");
        }

        int[] hostPorts = httpsPort is null
            ? [mqttPort, amqpPort]
            : [mqttPort, amqpPort, httpsPort.Value];

        EnsureHostPortsAreFree(builder.ApplicationBuilder, device, hostPorts);

        var name = $"{device.Name}-edgehub";
        var certificates = EdgeHubCertificateGenerator.EnsureCertificates(
            device.CertificateDirectory,
            [name, "localhost", "host.docker.internal", Environment.MachineName.ToLowerInvariant()]);

        var edgeHub = new IoTEdgeHubResource(name, device, certificates);
        device.EdgeHub = edgeHub;

        var healthCheckKey = $"{name}_check";
        builder.ApplicationBuilder.Services
            .AddHealthChecks()
            .Add(new HealthCheckRegistration(healthCheckKey, _ => new EdgeHubHealthCheck(mqttPort, certificates), failureStatus: null, tags: null));

        var edgeHubBuilder = builder.ApplicationBuilder
            .AddResource(edgeHub)
            .WithImage(IoTEdgeHubResource.DefaultImage, IoTEdgeHubResource.DefaultTag)
            .WithImageRegistry(IoTEdgeHubResource.DefaultRegistry)
            .WithContainerNetworkAlias(edgeHub.ContainerHostName)
            .WithBindMount(certificates.DirectoryPath, IoTEdgeHubResource.ContainerCertificateDirectory, isReadOnly: true)
            .WithEndpoint(port: mqttPort, targetPort: IoTEdgeHubResource.MqttPort, scheme: "tcp", name: IoTEdgeHubResource.MqttEndpointName, isProxied: false)
            .WithEndpoint(port: amqpPort, targetPort: IoTEdgeHubResource.AmqpPort, scheme: "tcp", name: IoTEdgeHubResource.AmqpEndpointName, isProxied: false)
            .WithEnvironment(EdgeHubEnvironmentVariableNames.EdgeModuleHubServerCertificateFile, $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.ServerCertificateFileName}")
            .WithEnvironment(EdgeHubEnvironmentVariableNames.EdgeModuleHubServerCaChainCertificateFile, $"{IoTEdgeHubResource.ContainerCertificateDirectory}/{EdgeHubCertificateFiles.ChainCertificateFileName}")
            .WithEnvironment(EdgeHubEnvironmentVariableNames.ConfigSource, EdgeHubEnvironmentVariableNames.ConfigSourceTwin)
            .WithEnvironment(async context =>
            {
                if (!context.ExecutionContext.IsRunMode)
                {
                    return;
                }

                var identity = await device.WaitForProvisioningAsync(context.CancellationToken);
                device.EnsureNotDeleted(edgeHub.Name);

                // The routes and store and forward settings are in the $edgeHub twin, which provisioning wrote.
                context.EnvironmentVariables[EdgeHubEnvironmentVariableNames.IotHubConnectionString] =
                    await device.GetModuleConnectionStringAsync(identity, EdgeHubConstants.ModuleId, gatewayHostName: null, context.CancellationToken);
            })
            .WithHealthCheck(healthCheckKey)
            .WithParentRelationship(builder)
            .WaitFor(builder);

        if (httpsPort is not null)
        {
            edgeHubBuilder.WithEndpoint(port: httpsPort, targetPort: IoTEdgeHubResource.HttpsPort, scheme: "tcp", name: IoTEdgeHubResource.HttpsEndpointName, isProxied: false);
        }

        foreach (var module in device.Modules)
        {
            module.ModuleResource.Annotations.Add(new WaitAnnotation(edgeHub, WaitType.WaitUntilHealthy));
        }

        configure?.Invoke(edgeHubBuilder);

        return builder;
    }

    /// <summary>
    /// Sets the protocol edgeHub uses to connect to IoT Hub.
    /// </summary>
    /// <param name="builder">The edgeHub resource builder.</param>
    /// <param name="protocol">The upstream protocol.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeHubResource> WithUpstreamProtocol(
        this IResourceBuilder<IoTEdgeHubResource> builder,
        EdgeHubUpstreamProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment(EdgeHubEnvironmentVariableNames.UpstreamProtocol, protocol.ToString());
    }

    /// <summary>
    /// Stores the edgeHub state in a Docker volume, so the messages it queues while IoT Hub is unreachable (store and forward)
    /// and its cache of the module identities survive a restart of the AppHost.
    /// </summary>
    /// <param name="builder">The edgeHub resource builder.</param>
    /// <param name="name">The volume name. Defaults to a name generated from the AppHost and the resource name.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTEdgeHubResource> WithDataVolume(
        this IResourceBuilder<IoTEdgeHubResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The edgeHub start script hands the storage folder to the non-root edgeHub user, so a new volume needs no setup.
        return builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), IoTEdgeHubResource.ContainerStorageDirectory)
            .WithEnvironment(EdgeHubEnvironmentVariableNames.StorageFolder, IoTEdgeHubResource.ContainerStorageDirectory);
    }

    private static async Task InitializeDevice(
        IoTEdgeDeviceResource resource,
        InitializeResourceEvent initializeEvent,
        CancellationToken cancellationToken)
    {
        var logger = initializeEvent.Logger;
        var notifications = initializeEvent.Notifications;

        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = KnownResourceStates.Starting,
            StartTimeStamp = DateTime.UtcNow,
        });

        try
        {
            await ResolveDeviceId(resource, cancellationToken);

            var connectionString = await resource.IotHubConnectionString.GetValueAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"The parameter '{resource.IotHubConnectionString.Name}' has no IoT Hub connection string.");
            }

            // The IoT Hub services throw IoT Hub's errors, which the provisioning and the commands show with what they were doing.
            var loggerFactory = initializeEvent.Services.GetRequiredService<ILoggerFactory>();
            var options = new IotHubOptions { ConnectionString = connectionString };

            var moduleService = new IoTHubModuleService(options, loggerFactory);
            var iotHubService = new IoTHubService(moduleService, options, loggerFactory);

            // Stopped, not stopping: commands and connection checks may still call IoT Hub while the AppHost stops.
            initializeEvent.Services
                .GetRequiredService<IHostApplicationLifetime>()
                .ApplicationStopped
                .Register(() =>
                {
                    iotHubService.Dispose();
                    moduleService.Dispose();
                });

            resource.IoTHubModuleService = moduleService;
            resource.IoTHubService = iotHubService;

            if (resource.DeploymentManifestPath is not null)
            {
                resource.DeploymentManifest = await LoadDeploymentManifest(resource, resource.DeploymentManifestPath, logger, cancellationToken);
            }

            WarnAboutModulesThatMayNotConnect(resource, logger);

            var iotHubHostName = IotHubConnectionStringBuilder.Create(connectionString).HostName;
            logger.LogProvisioningDevice(resource.DeviceId, iotHubHostName);

            var provisioner = new IoTEdgeDeviceProvisioner(iotHubService, iotHubHostName, logger);
            resource.Provisioner = provisioner;

            var edgeHubConfiguration = resource.EdgeHub is null
                ? null
                : resource.DeploymentManifest?.EdgeHubDesiredProperties ?? EdgeHubConfiguration.Default;

            var identity = await provisioner.Provision(
                resource.DeviceId,
                resource.Modules.Select(x => x.ModuleId),
                resource.DeploymentManifest,
                edgeHubConfiguration,
                cancellationToken);

            resource.SetProvisioned(identity);

            var mode = resource.EdgeHub is null
                ? "Direct to IoT Hub"
                : $"Through edgeHub ({resource.EdgeHub.Name})";

            var moduleIds = string.Join(", ", resource.Modules.Select(x => x.ModuleId));

            logger.LogDeviceReady(identity.DeviceId, moduleIds, mode);

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = KnownResourceStates.Running,
                Properties = ResourceProperties.Set(
                    snapshot.Properties,
                    new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, identity.DeviceId),
                    new ResourcePropertySnapshot("iotedge.deviceId", identity.DeviceId),
                    new ResourcePropertySnapshot("iothub.hostName", identity.IotHubHostName),
                    new ResourcePropertySnapshot("iotedge.mode", mode),
                    new ResourcePropertySnapshot("iotedge.modules", moduleIds)),
            });

            await RefreshModuleCommands(resource, notifications);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogProvisioningFailed(ex, resource.DeviceId, ex.Message);
            resource.SetProvisioningFailed(ex);

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = KnownResourceStates.FailedToStart,
            });
        }
    }

    private static async Task<DeploymentManifest> LoadDeploymentManifest(
        IoTEdgeDeviceResource resource,
        string deploymentManifestPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var deploymentManifest = await DeploymentManifest.LoadAsync(deploymentManifestPath, cancellationToken);

        logger.LogDeploymentManifestLoaded(
            deploymentManifestPath,
            deploymentManifest.Routes.Count,
            deploymentManifest.ModuleDesiredProperties.Count);

        EnsureModulesAreInManifest(resource, deploymentManifestPath, deploymentManifest);

        if (resource.EdgeHub is not null &&
            deploymentManifest.EdgeHubDesiredProperties is { } edgeHubConfiguration &&
            !EdgeHubConfiguration.HasSchemaVersion(edgeHubConfiguration))
        {
            throw new InvalidOperationException(
                $"The desired properties of $edgeHub in the deployment manifest '{deploymentManifestPath}' have no schemaVersion. " +
                "edgeHub rejects a configuration without it and would run without routes. Add e.g. \"schemaVersion\": \"1.1\".");
        }

        return deploymentManifest;
    }

    private static void EnsureHostPortsAreFree(
        IDistributedApplicationBuilder applicationBuilder,
        IoTEdgeDeviceResource device,
        int[] hostPorts)
    {
        var otherEdgeHub = applicationBuilder.Resources
            .OfType<IoTEdgeHubResource>()
            .FirstOrDefault(x => x.Annotations
                .OfType<EndpointAnnotation>()
                .Any(endpoint => endpoint.Port is { } port && hostPorts.Contains(port)));

        if (otherEdgeHub is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"IoT Edge device '{device.Name}' can't run an edgeHub on the host ports of '{otherEdgeHub.Name}' (device '{otherEdgeHub.Device.Name}'). " +
            $"Pass other ports to WithEdgeHub. Modules that run on the host can only reach an edgeHub on the standard ports {IoTEdgeHubResource.MqttPort}, {IoTEdgeHubResource.AmqpPort} and {IoTEdgeHubResource.HttpsPort}.");
    }

    private static void RemoveDeviceIdParameter(IoTEdgeDeviceResource resource)
    {
        if (resource.DeviceIdParameter is not { } parameter)
        {
            return;
        }

        resource.DeviceIdParameter = null;

        // Otherwise the dashboard keeps showing a reference to a parameter that is no longer used.
        var relationships = resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(x => ReferenceEquals(x.Resource, parameter))
            .ToList();

        foreach (var relationship in relationships)
        {
            resource.Annotations.Remove(relationship);
        }
    }

    private static async Task ResolveDeviceId(
        IoTEdgeDeviceResource resource,
        CancellationToken cancellationToken)
    {
        if (resource.DeviceIdParameter is not { } parameter)
        {
            return;
        }

        var deviceId = await parameter.GetValueAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException($"The parameter '{parameter.Name}' has no device id.");
        }

        IoTHubIdentifier.EnsureValidDeviceId(deviceId, parameter.Name);
        resource.DeviceId = deviceId;
    }

    private static void EnsureModulesAreInManifest(
        IoTEdgeDeviceResource resource,
        string deploymentManifestPath,
        DeploymentManifest deploymentManifest)
    {
        var missingModuleIds = IoTEdgeModuleValidator.GetModulesMissingFromManifest(
            deploymentManifest,
            resource.Modules.Select(x => x.ModuleId));

        if (missingModuleIds.Count == 0)
        {
            return;
        }

        var moduleIds = string.Join(", ", missingModuleIds.Select(x => $"'{x}'"));
        throw new InvalidOperationException(
            $"The module(s) {moduleIds} are not in the deployment manifest '{deploymentManifestPath}', so its routes and desired properties can't apply to them. " +
            "Add them to the modules of $edgeAgent in the manifest, or pass the module id from the manifest to AsIoTEdgeModule.");
    }

    private static void WarnAboutModulesThatMayNotConnect(
        IoTEdgeDeviceResource resource,
        ILogger logger)
    {
        foreach (var moduleAnnotation in resource.Modules)
        {
            var moduleResource = moduleAnnotation.ModuleResource;

            if (resource.DeploymentManifest is not null &&
                moduleResource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image))
            {
                var containerImage = ContainerImageName.GetReference(image);
                var manifestImage = IoTEdgeModuleValidator.GetDifferingManifestImage(
                    resource.DeploymentManifest,
                    moduleAnnotation.ModuleId,
                    containerImage);

                if (manifestImage is not null)
                {
                    logger.LogModuleImageDiffersFromManifest(moduleAnnotation.ModuleId, containerImage, manifestImage);
                }
            }

            if (moduleResource.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata) &&
                IoTEdgeModuleValidator.ReferencesDeviceSdk(projectMetadata.ProjectPath) == false)
            {
                logger.LogModuleWithoutDeviceSdk(moduleAnnotation.ModuleId, projectMetadata.ProjectPath);
            }
        }
    }

    private static IResourceBuilder<IoTEdgeDeviceResource> WithDeleteDeviceCommand(
        this IResourceBuilder<IoTEdgeDeviceResource> builder)
        => builder.WithCommand(
            name: "iotedge-delete-device",
            displayName: "Delete device from IoT Hub",
            executeCommand: context => DeleteDevice(context, builder.Resource),
            commandOptions: new CommandOptions
            {
                Description = "Deletes the device and its module identities from IoT Hub, and stops its edgeHub and modules.",
                ConfirmationMessage = "Delete the device and all its module identities from IoT Hub, and stop its edgeHub and modules?",
                IconName = "Delete",
                UpdateState = _ => builder.Resource.IsProvisioned
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
            });

    private static async Task<ExecuteCommandResult> DeleteDevice(
        ExecuteCommandContext context,
        IoTEdgeDeviceResource resource)
    {
        if (!resource.IsProvisioned || resource.Provisioner is null)
        {
            return CommandResults.Failure("The device has not been provisioned.");
        }

        try
        {
            // A device that is already gone counts as deleted.
            await resource.Provisioner.Deprovision(resource.DeviceId, context.CancellationToken);
        }
        catch (IotHubException ex)
        {
            return CommandResults.Failure($"Could not delete device '{resource.DeviceId}': {ex.GetErrorMessage()}");
        }

        resource.SetDeleted();

        var notifications = context.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = new ResourceStateSnapshot(DeletedState, KnownResourceStateStyles.Warn),
        });

        await RefreshModuleCommands(resource, notifications);

        // The keys of the device and its modules no longer work, so its edgeHub and modules are stopped.
        var (stopped, notStopped) = await StopDependentResources(resource, notifications, context.Services, context.CancellationToken);
        var stoppedText = stopped.Count == 0
            ? "Nothing was running."
            : $"Stopped {string.Join(", ", stopped)}.";

        if (notStopped.Count > 0)
        {
            stoppedText += $" Could not stop {string.Join(", ", notStopped)} within {(int)StopTimeout.TotalSeconds} seconds.";
        }

        var logger = context.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
        logger.LogDeviceDeleted(resource.DeviceId, stoppedText);

        return CommandResults.Success($"Deleted device '{resource.DeviceId}'. {stoppedText} Restart the AppHost to provision it again.");
    }

    private static async Task<(List<string> Stopped, List<string> NotStopped)> StopDependentResources(
        IoTEdgeDeviceResource resource,
        ResourceNotificationService notifications,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // The modules first, so they don't log connection errors while edgeHub stops.
        List<IResource> dependents = [.. resource.Modules.Select(x => x.ModuleResource)];

        if (resource.EdgeHub is not null)
        {
            dependents.Add(resource.EdgeHub);
        }

        var commands = services.GetRequiredService<ResourceCommandService>();
        var stopped = new List<string>();
        var notStopped = new List<string>();
        foreach (var dependent in dependents)
        {
            // Aspire holds a Stop for a Waiting resource until the wait is over, which may be never.
            // A Waiting module fails to start instead, because its environment callback sees the deleted device.
            if (!notifications.TryGetCurrentState(dependent.Name, out var resourceEvent) ||
                !IsActive(resourceEvent.Snapshot.State?.Text))
            {
                continue;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(StopTimeout);

            try
            {
                var result = await commands.ExecuteCommandAsync(dependent, KnownResourceCommands.StopCommand, timeout.Token);
                if (result.Success)
                {
                    stopped.Add(dependent.Name);
                }
                else
                {
                    notStopped.Add(dependent.Name);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                notStopped.Add(dependent.Name);
            }
        }

        return (stopped, notStopped);
    }

    private static bool IsActive(string? state)
        => state == KnownResourceStates.Running ||
           state == KnownResourceStates.Starting;

    private static async Task RefreshModuleCommands(
        IoTEdgeDeviceResource resource,
        ResourceNotificationService notifications)
    {
        // Command states are evaluated when a resource's snapshot changes. Provisioning and deleting change the device,
        // not the modules, so an unchanged update makes the modules' commands follow the device.
        foreach (var module in resource.Modules)
        {
            await notifications.PublishUpdateAsync(module.ModuleResource, snapshot => snapshot);
        }
    }

    private static string SanitizeDeviceId(string value)
        => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9\\-._:]", "-", RegexOptions.None, TimeSpan.FromSeconds(1));
}