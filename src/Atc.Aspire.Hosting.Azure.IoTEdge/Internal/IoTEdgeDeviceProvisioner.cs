namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Provisions the IoT Hub identities an emulated IoT Edge device needs, using an IoT Hub service connection string
/// with registry read/write permissions.
/// </summary>
internal sealed partial class IoTEdgeDeviceProvisioner
{
    private const string CheckConnectionString = " Check the connection string's key and its registry write permissions.";

    private readonly IIoTHubService iotHubService;
    private readonly string iotHubHostName;

    /// <summary>
    /// Initializes a new instance of the <see cref="IoTEdgeDeviceProvisioner"/> class.
    /// </summary>
    /// <param name="iotHubService">The IoT Hub service, created from a connection string with registry read/write permissions.</param>
    /// <param name="iotHubHostName">The IoT Hub host name, e.g. <c>myhub.azure-devices.net</c>.</param>
    /// <param name="logger">An optional logger, e.g. the device's log in the dashboard.</param>
    public IoTEdgeDeviceProvisioner(
        IIoTHubService iotHubService,
        string iotHubHostName,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(iotHubService);
        ArgumentException.ThrowIfNullOrEmpty(iotHubHostName);

        this.iotHubService = iotHubService;
        this.iotHubHostName = iotHubHostName;
        this.logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Ensures an edge-enabled device and the given module identities, with symmetric keys, exist, and replaces the
    /// module twin desired properties with those from the deployment manifest when one is given, as a deployment does.
    /// Existing identities are reused, so their keys stay stable between runs.
    /// </summary>
    /// <param name="deviceId">The edge device id.</param>
    /// <param name="moduleIds">The custom module ids to provision.</param>
    /// <param name="deploymentManifest">An optional deployment manifest with module twin desired properties.</param>
    /// <param name="edgeHubConfiguration">
    /// The edgeHub configuration (the desired properties of the <c>$edgeHub</c> twin, as JSON) to apply as a single-device
    /// deployment, or <see langword="null"/> when the device has no edgeHub.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The device identity.</returns>
    /// <exception cref="InvalidOperationException">
    /// An identity or the edgeHub configuration could not be written; the message includes IoT Hub's error.
    /// </exception>
    public async Task<IoTEdgeDeviceIdentity> Provision(
        string deviceId,
        IEnumerable<string> moduleIds,
        DeploymentManifest? deploymentManifest,
        string? edgeHubConfiguration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ArgumentNullException.ThrowIfNull(moduleIds);

        await EnsureEdgeDevice(deviceId, cancellationToken);

        var provisionedModuleIds = await EnsureModules(
            deviceId,
            [.. moduleIds.Distinct(StringComparer.Ordinal)],
            cancellationToken);

        if (deploymentManifest is not null)
        {
            await ApplyModuleDesiredProperties(deviceId, provisionedModuleIds, deploymentManifest, cancellationToken);
        }

        // edgeHub reads its routes from its twin, so they must be in place before it starts. IoT Hub only lets a deployment
        // write the twins of the reserved $edgeAgent and $edgeHub modules, so they're applied as a single-device deployment.
        if (edgeHubConfiguration is not null)
        {
            try
            {
                await iotHubService.ApplyConfigurationContentOnDevice(
                    deviceId,
                    EdgeHubConfiguration.CreateDeploymentContent(edgeHubConfiguration),
                    cancellationToken);
            }
            catch (IotHubException ex)
            {
                throw Failed($"Could not apply the edgeHub configuration to IoT Edge device '{deviceId}'", ex);
            }

            LogEdgeHubConfigurationApplied(iotHubHostName, deviceId);
        }

        LogDeviceProvisioned(iotHubHostName, deviceId, provisionedModuleIds.Count);
        return new IoTEdgeDeviceIdentity(iotHubHostName, deviceId);
    }

    /// <summary>
    /// Deletes the device, including its module identities, from IoT Hub. A device that is already gone counts as deleted.
    /// </summary>
    /// <param name="deviceId">The edge device id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the device is gone.</returns>
    /// <exception cref="IotHubException">IoT Hub refused the delete, e.g. because of the connection string's permissions.</exception>
    public async Task Deprovision(
        string deviceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        if (await iotHubService.DeleteDevice(deviceId, cancellationToken))
        {
            LogDeviceDeprovisioned(iotHubHostName, deviceId);
        }
        else
        {
            LogDeviceAlreadyGone(iotHubHostName, deviceId);
        }
    }

    private async Task EnsureEdgeDevice(
        string deviceId,
        CancellationToken cancellationToken)
    {
        Device? device;

        try
        {
            device = await iotHubService.GetDevice(deviceId, cancellationToken);
            if (device is null)
            {
                await iotHubService.CreateDevice(deviceId, edgeEnabled: true, cancellationToken);
                LogDeviceCreated(iotHubHostName, deviceId);
                return;
            }
        }
        catch (IotHubException ex)
        {
            throw Failed($"Could not create IoT Edge device '{deviceId}' in IoT Hub '{iotHubHostName}'", ex);
        }

        if (device.Capabilities?.IotEdge != true)
        {
            throw new InvalidOperationException($"Device '{deviceId}' already exists in IoT Hub '{iotHubHostName}' but is not an IoT Edge device. Delete it or choose another device id.");
        }

        // Provisioning applies a deployment with only the system modules, and a real IoT Edge runtime would remove every
        // other module to follow it. A real runtime reports its status in the $edgeAgent twin; the emulator never runs edgeAgent.
        if (await IsUsedByIoTEdgeRuntime(deviceId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Device '{deviceId}' in IoT Hub '{iotHubHostName}' is used by a real IoT Edge runtime ($edgeAgent reports its status). " +
                "Provisioning would replace its deployment and remove its modules, so nothing was changed. Choose another device id.");
        }
    }

    private async Task<bool> IsUsedByIoTEdgeRuntime(
        string deviceId,
        CancellationToken cancellationToken)
    {
        Twin? edgeAgentTwin;

        try
        {
            edgeAgentTwin = await iotHubService.GetModuleTwin(deviceId, EdgeAgentConstants.ModuleId, cancellationToken);
        }
        catch (IotHubException ex)
        {
            throw Failed($"Could not read the $edgeAgent twin of IoT Edge device '{deviceId}'", ex);
        }

        if (edgeAgentTwin is null)
        {
            return false;
        }

        return JsonNode.Parse(edgeAgentTwin.Properties.Reported.ToJson()) is JsonObject reported &&
               reported.Any(x => !x.Key.StartsWith('$'));
    }

    private async Task<IReadOnlyCollection<string>> EnsureModules(
        string deviceId,
        IReadOnlyCollection<string> moduleIds,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Module> existingModules;

        try
        {
            existingModules = (await iotHubService.GetModulesOnIotEdgeDevice(deviceId, cancellationToken))
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
        }
        catch (IotHubException ex)
        {
            throw Failed($"Could not read the module identities of IoT Edge device '{deviceId}'", ex);
        }

        foreach (var moduleId in moduleIds)
        {
            if (existingModules.ContainsKey(moduleId))
            {
                continue;
            }

            try
            {
                existingModules[moduleId] = await iotHubService.AddModuleToDevice(deviceId, moduleId, cancellationToken);
            }
            catch (IotHubException ex)
            {
                throw Failed($"Could not create module identity '{moduleId}' on IoT Edge device '{deviceId}'", ex);
            }

            LogModuleCreated(iotHubHostName, deviceId, moduleId);
        }

        // The modules get their connection strings, with the primary key, from IoT Hub when they start.
        foreach (var moduleId in moduleIds.Append(EdgeHubConstants.ModuleId))
        {
            if (!existingModules.TryGetValue(moduleId, out var module))
            {
                throw new InvalidOperationException($"Module identity '{moduleId}' does not exist on IoT Edge device '{deviceId}'.");
            }

            // IoT Hub creates $edgeAgent and $edgeHub without credentials; the IoT Edge identity service assigns them.
            // There is no identity service here, so the keys are assigned the same way.
            if (!HasPrimaryKey(module))
            {
                await AssignSymmetricKeys(deviceId, module, cancellationToken);
            }
        }

        return moduleIds;
    }

    private async Task AssignSymmetricKeys(
        string deviceId,
        Module module,
        CancellationToken cancellationToken)
    {
        module.Authentication = new AuthenticationMechanism
        {
            Type = AuthenticationType.Sas,
            SymmetricKey = new SymmetricKey
            {
                PrimaryKey = GenerateSymmetricKey(),
                SecondaryKey = GenerateSymmetricKey(),
            },
        };

        try
        {
            await iotHubService.UpdateModule(module, cancellationToken);
        }
        catch (IotHubException ex)
        {
            throw Failed($"Could not assign symmetric keys to module identity '{module.Id}' on IoT Edge device '{deviceId}'", ex);
        }

        LogModuleKeysAssigned(iotHubHostName, deviceId, module.Id);
    }

    private static bool HasPrimaryKey(Module module)
        => module.Authentication?.SymmetricKey?.PrimaryKey is { Length: > 0 };

    private static string GenerateSymmetricKey()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private async Task ApplyModuleDesiredProperties(
        string deviceId,
        IEnumerable<string> provisionedModuleIds,
        DeploymentManifest deploymentManifest,
        CancellationToken cancellationToken)
    {
        foreach (var moduleId in provisionedModuleIds)
        {
            if (!deploymentManifest.ModuleDesiredProperties.TryGetValue(moduleId, out var desiredProperties))
            {
                continue;
            }

            try
            {
                await ReplaceDesiredProperties(deviceId, moduleId, desiredProperties, cancellationToken);
                LogModuleDesiredPropertiesApplied(iotHubHostName, deviceId, moduleId);
            }
            catch (IotHubException ex)
            {
                // The module still starts, with the desired properties its twin already has.
                LogModuleDesiredPropertiesNotApplied(ex, iotHubHostName, deviceId, moduleId, ex.GetErrorMessage());
            }
        }
    }

    private async Task ReplaceDesiredProperties(
        string deviceId,
        string moduleId,
        string desiredProperties,
        CancellationToken cancellationToken)
    {
        // The module identity was just provisioned, so its twin exists; a missing one would be IoT Hub's error.
        var currentTwin = await iotHubService.GetModuleTwin(deviceId, moduleId, cancellationToken)
                          ?? throw new ModuleNotFoundException(deviceId, moduleId);

        var patch = DesiredPropertiesPatch.Create(currentTwin.Properties.Desired.ToJson(), desiredProperties);

        await iotHubService.UpdateDesiredProperties(
            deviceId,
            moduleId,
            new TwinCollection(patch),
            cancellationToken);
    }

    /// <summary>
    /// Wraps an IoT Hub error with what provisioning was doing, so the device's log shows both.
    /// </summary>
    private static InvalidOperationException Failed(
        string action,
        IotHubException exception)
        => new(
            $"{action}: {exception.GetErrorMessage()}" + (exception is UnauthorizedException ? CheckConnectionString : string.Empty),
            exception);
}