namespace Atc.Aspire.Hosting.Azure.IoTEdge;

/// <summary>
/// Represents an emulated IoT Edge device: an edge-enabled device identity in a real Azure IoT Hub,
/// with a module identity for every Aspire resource attached through <c>AsIoTEdgeModule</c>.
/// </summary>
public sealed class IoTEdgeDeviceResource : Resource, IResourceWithWaitSupport
{
    // Every IoT Hub read counts against the daily quota, so the modules of a device share one read per check round.
    private static readonly TimeSpan ConnectionStatesCacheDuration = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource<IoTEdgeDeviceIdentity> provisioning = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<IoTEdgeModuleAnnotation> modules = [];
    private readonly Lock connectionStatesLock = new();
    private (DateTimeOffset ReadAt, Task<IReadOnlyDictionary<string, string>> States)? connectionStates;
    private volatile bool deleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="IoTEdgeDeviceResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="iotHubConnectionString">The parameter holding the IoT Hub service connection string.</param>
    /// <param name="deviceId">The id of the edge device in IoT Hub.</param>
    /// <param name="certificateDirectory">The directory holding the edgeHub test certificates.</param>
    public IoTEdgeDeviceResource(
        string name,
        ParameterResource iotHubConnectionString,
        string deviceId,
        string certificateDirectory)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(iotHubConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ArgumentException.ThrowIfNullOrEmpty(certificateDirectory);

        IotHubConnectionString = iotHubConnectionString;
        DeviceId = deviceId;
        CertificateDirectory = certificateDirectory;
    }

    /// <summary>
    /// Gets the parameter holding the IoT Hub service connection string used to provision the device.
    /// The policy needs registry read/write, and service connect for the dashboard commands.
    /// </summary>
    public ParameterResource IotHubConnectionString { get; }

    /// <summary>
    /// Gets the id of the edge device in IoT Hub.
    /// </summary>
    public string DeviceId { get; internal set; }

    /// <summary>
    /// Gets the directory holding the edgeHub test certificates.
    /// </summary>
    public string CertificateDirectory { get; }

    /// <summary>
    /// Gets the path of the deployment manifest whose routes and module twin desired properties are applied, if any.
    /// </summary>
    public string? DeploymentManifestPath { get; internal set; }

    /// <summary>
    /// Gets the local edgeHub container, or <see langword="null"/> when modules connect directly to IoT Hub.
    /// </summary>
    public IoTEdgeHubResource? EdgeHub { get; internal set; }

    /// <summary>
    /// Gets the modules attached to the device.
    /// </summary>
    public IReadOnlyList<IoTEdgeModuleAnnotation> Modules => modules;

    internal ParameterResource? DeviceIdParameter { get; set; }

    internal IIoTHubService? IoTHubService { get; set; }

    internal IIoTHubModuleService? IoTHubModuleService { get; set; }

    internal DeploymentManifest? DeploymentManifest { get; set; }

    internal IoTEdgeDeviceProvisioner? Provisioner { get; set; }

    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets whether the device and its module identities exist in IoT Hub: provisioned, and not deleted since.
    /// </summary>
    internal bool IsProvisioned
        => provisioning.Task.IsCompletedSuccessfully && !deleted;

    /// <summary>
    /// Gets whether the device was deleted from IoT Hub with the dashboard command.
    /// </summary>
    internal bool IsDeleted => deleted;

    /// <summary>
    /// Waits until the device and its module identities are provisioned in IoT Hub.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provisioned identities.</returns>
    public Task<IoTEdgeDeviceIdentity> WaitForProvisioningAsync(
        CancellationToken cancellationToken = default)
        => provisioning.Task.WaitAsync(cancellationToken);

    internal void AddModule(IoTEdgeModuleAnnotation module)
    {
        if (modules.Exists(x => string.Equals(x.ModuleId, module.ModuleId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Module '{module.ModuleId}' is already attached to IoT Edge device '{Name}'.");
        }

        modules.Add(module);
    }

    /// <summary>
    /// Gets the connection status of each module, as edgeHub reports it in its twin, or as IoT Hub reports it for modules
    /// that connect directly. Reads are shared for a few seconds across the modules of the device.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status keyed by module id.</returns>
    internal Task<IReadOnlyDictionary<string, string>> GetModuleConnectionStatesAsync(
        CancellationToken cancellationToken)
    {
        lock (connectionStatesLock)
        {
            var now = TimeProvider.GetUtcNow();
            if (connectionStates is { } cached &&
                now - cached.ReadAt < ConnectionStatesCacheDuration &&
                !cached.States.IsFaulted &&
                !cached.States.IsCanceled)
            {
                return cached.States.WaitAsync(cancellationToken);
            }

            // The read is shared, so it doesn't take the caller's token: a caller that gives up only stops its own wait.
            var states = ReadModuleConnectionStatesAsync(CancellationToken.None);
            connectionStates = (now, states);
            return states.WaitAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadModuleConnectionStatesAsync(
        CancellationToken cancellationToken)
    {
        var iotHubService = IoTHubService
                            ?? throw new InvalidOperationException($"IoT Edge device '{Name}' has not been provisioned.");

        if (EdgeHub is not null)
        {
            var twin = await iotHubService.GetModuleTwin(DeviceId, EdgeHubConstants.ModuleId, cancellationToken);
            return twin is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : EdgeHubClients.GetConnectionStates(twin.Properties.Reported.ToJson(), DeviceId);
        }

        var deviceModules = await iotHubService.GetModulesOnIotEdgeDevice(DeviceId, cancellationToken);
        return deviceModules.ToDictionary(x => x.Id, x => x.ConnectionState.ToString(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a module's connection string, with its primary key, from IoT Hub.
    /// </summary>
    /// <param name="identity">The provisioned device identity.</param>
    /// <param name="moduleId">The module id, e.g. <c>$edgeHub</c>.</param>
    /// <param name="gatewayHostName">
    /// The host name of the edgeHub the module connects through, or <see langword="null"/> to connect directly to IoT Hub.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The module connection string.</returns>
    /// <exception cref="InvalidOperationException">IoT Hub refused the read, or the module identity doesn't exist.</exception>
    internal async Task<string> GetModuleConnectionStringAsync(
        IoTEdgeDeviceIdentity identity,
        string moduleId,
        string? gatewayHostName,
        CancellationToken cancellationToken)
    {
        var iotHubService = IoTHubService
                            ?? throw new InvalidOperationException($"IoT Edge device '{Name}' has not been provisioned.");

        string? connectionString;

        try
        {
            connectionString = await iotHubService.GetModuleConnectionString(identity.DeviceId, moduleId, gatewayHostName, cancellationToken);
        }
        catch (IotHubException ex)
        {
            throw new InvalidOperationException($"Could not read the connection string of module '{moduleId}' on IoT Edge device '{identity.DeviceId}': {ex.GetErrorMessage()}", ex);
        }

        return connectionString
               ?? throw new InvalidOperationException(
                   $"Module identity '{moduleId}' doesn't exist on IoT Edge device '{identity.DeviceId}'. Restart the AppHost to provision it again.");
    }

    internal void SetProvisioned(IoTEdgeDeviceIdentity identity)
        => provisioning.TrySetResult(identity);

    internal void SetProvisioningFailed(Exception exception)
        => provisioning.TrySetException(exception);

    internal void SetDeleted()
        => deleted = true;

    internal void EnsureNotDeleted(string resourceName)
    {
        if (deleted)
        {
            throw new InvalidOperationException(
                $"IoT Edge device '{DeviceId}' was deleted from IoT Hub, so '{resourceName}' can't connect. Restart the AppHost to provision the device again.");
        }
    }
}