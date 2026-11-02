namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Starts, pauses and resumes the bridge of a resource, and shows its state and counts on the dashboard.
/// </summary>
internal sealed partial class EventBridgeController : IAsyncDisposable
{
    public const string PausedState = "Paused";
    public const string FailedState = "Failed";
    public const string WaitingForSetupState = "Waiting for IoT Hub setup";

    // Events of devices that start with the AppHost may be raised just before the bridge reads its first event.
    private static readonly TimeSpan StartTimeMargin = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CountsInterval = TimeSpan.FromSeconds(1);

    // Signing in and reading the hub take a few seconds; an unreachable Azure mustn't hold up the start for long.
    private static readonly TimeSpan SetupCheckTimeout = TimeSpan.FromSeconds(30);

    private readonly IoTHubEventBridgeResource resource;
    private readonly Func<string, CancellationToken, Task<IoTHubSettingsReadResult>> readSettings;
    private readonly ResourceNotificationService notifications;
    private readonly ILogger logger;
    private readonly CancellationToken stopping;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<AzureEventHubResource, IEventHubSender> sendersByEventHub = [];
    private IIoTHubEventReader? reader;
    private EventBridge? bridge;
    private ITimer? countsTimer;
    private CancellationTokenSource? run;
    private Task? runTask;
    private volatile bool runFailed;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventBridgeController"/> class.
    /// </summary>
    /// <param name="resource">The bridge resource.</param>
    /// <param name="readSettings">Reads the settings of an IoT Hub by name, to check its setup.</param>
    /// <param name="notifications">The notification service that publishes the resource's state.</param>
    /// <param name="logger">The resource logger.</param>
    /// <param name="stopping">Cancelled when the AppHost stops.</param>
    public EventBridgeController(
        IoTHubEventBridgeResource resource,
        Func<string, CancellationToken, Task<IoTHubSettingsReadResult>> readSettings,
        ResourceNotificationService notifications,
        ILogger logger,
        CancellationToken stopping)
    {
        this.resource = resource;
        this.readSettings = readSettings;
        this.notifications = notifications;
        this.logger = logger;
        this.stopping = stopping;

        StartTime = resource.TimeProvider.GetUtcNow() - StartTimeMargin;
    }

    /// <summary>
    /// Gets the time of the first events the bridge reads: shortly before the AppHost started.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// Waits for the Event Hubs to be ready, connects to IoT Hub and the Event Hubs, and starts forwarding.
    /// Errors are logged and shown as the state Failed to start.
    /// </summary>
    /// <returns>A task that completes when the bridge runs or failed to start.</returns>
    public async Task StartAsync()
    {
        try
        {
            await gate.WaitAsync(stopping);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // The AppHost stops.
            return;
        }

        try
        {
            if (bridge is not null)
            {
                return;
            }

            await StartBridgeAsync();
        }
        catch (Exception ex) when (!stopping.IsCancellationRequested)
        {
            LogStartFailed(ex, ex.Message);
            await DisposeClientsAsync();

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = KnownResourceStates.FailedToStart,
            });
        }
        catch (Exception) when (stopping.IsCancellationRequested)
        {
            // The AppHost stops, and connecting may fail with more than a cancellation.
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Stops reading. The bridge remembers the last event read from each partition.
    /// </summary>
    /// <returns><see langword="true"/> when the bridge was running.</returns>
    public async Task<bool> PauseAsync()
    {
        await gate.WaitAsync(stopping);

        try
        {
            if (run is null || runTask is null || runTask.IsCompleted)
            {
                return false;
            }

            await StopRunAsync();
            PublishCounts();

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = new ResourceStateSnapshot(PausedState, KnownResourceStateStyles.Info),
            });

            LogPaused();
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Continues reading after the last event read from each partition, after a pause or a failure.
    /// </summary>
    /// <returns><see langword="true"/> when the bridge was paused or had failed.</returns>
    public async Task<bool> ResumeAsync()
    {
        await gate.WaitAsync(stopping);

        try
        {
            if (bridge is null)
            {
                return false;
            }

            if (runTask is { IsCompleted: false })
            {
                // A failed run completes once it has shown the state Failed.
                if (!runFailed)
                {
                    return false;
                }

                await runTask;
            }

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = KnownResourceStates.Running,
            });

            StartRun(bridge);
            LogResumed();
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        // A start in progress ends when the AppHost stops, so this waits for it before disposing what it created.
        await gate.WaitAsync(CancellationToken.None);

        try
        {
            await StopRunAsync();
            await DisposeClientsAsync();
        }
        finally
        {
            gate.Dispose();
        }
    }

    private async Task StartBridgeAsync()
    {
        if (resource.Targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"IoT Hub event bridge '{resource.Name}' forwards nothing. Call ForwardDeviceLifecycleEvents, ForwardTwinChangeEvents or ForwardTelemetry.");
        }

        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = KnownResourceStates.Starting,
        });

        // Shows the state Waiting until the Event Hubs (the emulator) are healthy.
        await notifications.WaitForDependenciesAsync(resource, stopping);

        // Aspire leaves the state at Waiting after the wait, so it is set to Starting again.
        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = KnownResourceStates.Starting,
        });

        var connectionString = await resource.IotHubConnectionString.GetValueAsync(stopping);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"The parameter '{resource.IotHubConnectionString.Name}' has no IoT Hub connection string.");
        }

        var iotHub = IoTHubConnectionInfo.Parse(connectionString);
        var deviceIds = await GetDeviceIdsAsync();
        if (!await CheckSetupAsync(iotHub))
        {
            return;
        }

        var readerOptions = new IoTHubReaderOptions(resource.ConsumerGroup, resource.UseWebSockets);
        LogStarting(iotHub.HostName, readerOptions.TransportDescription, resource.ConsumerGroup);

        reader = await resource.CreateReader(iotHub, readerOptions, stopping);

        // One sender per Event Hub, which kinds of events forwarded to the same Event Hub share.
        // Each is kept as soon as it is created, so a failed start closes the ones created before.
        var sendersBySource = new Dictionary<IoTHubEventSource, IEventHubSender>();
        foreach (var (source, eventHub) in resource.Targets)
        {
            if (!sendersByEventHub.TryGetValue(eventHub, out var sender))
            {
                sender = await resource.CreateSender(eventHub, stopping);
                sendersByEventHub[eventHub] = sender;
            }

            sendersBySource[source] = sender;
        }

        var startedBridge = new EventBridge(reader, sendersBySource, deviceIds, StartTime, logger);
        bridge = startedBridge;
        countsTimer = resource.TimeProvider.CreateTimer(_ => PublishCounts(), state: null, CountsInterval, CountsInterval);

        var devices = GetDevicesText(deviceIds);
        var startTime = StartTime.ToString("u", CultureInfo.InvariantCulture);

        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = KnownResourceStates.Running,
            StartTimeStamp = resource.TimeProvider.GetUtcNow().UtcDateTime,
            Properties = ResourceProperties.Set(
                snapshot.Properties,
                [
                    new ResourcePropertySnapshot(CustomResourceKnownProperties.Source, iotHub.HostName),
                    new ResourcePropertySnapshot(PropertyNames.HostName, iotHub.HostName),
                    new ResourcePropertySnapshot(PropertyNames.Transport, readerOptions.TransportDescription),
                    new ResourcePropertySnapshot(PropertyNames.ConsumerGroup, resource.ConsumerGroup),
                    new ResourcePropertySnapshot(PropertyNames.StartTime, startTime),
                    new ResourcePropertySnapshot(PropertyNames.Devices, devices),
                    .. GetCountProperties(startedBridge),
                ]),
        });

        StartRun(startedBridge);
        LogRunning(iotHub.HostName, startTime, devices);
    }

    // False when the hub lacks something the bridge needs: the bridge then waits for the hub's setup instead of starting.
    private async Task<bool> CheckSetupAsync(IoTHubConnectionInfo iotHub)
    {
        var read = await ReadSettingsAsync(iotHub.HubName);
        if (read.Settings is null)
        {
            // Without a check, e.g. without an Azure sign-in, the hub may well be set up, so the bridge forwards anyway.
            LogSetupNotVerified(iotHub.HostName, read.NotVerifiedReason!);
            await PublishSetupAsync($"Not verified: {read.NotVerifiedReason}");
            return true;
        }

        var result = IoTHubSetupCheck.Check(read.Settings, resource.ConsumerGroup, resource.Targets.Keys);
        if (result.IsComplete)
        {
            LogSetupVerified(iotHub.HostName);
            await PublishSetupAsync("Verified");
            return true;
        }

        var missing = string.Join("; ", result.Missing.Select(x => IoTHubSetupCheck.Describe(x, resource.ConsumerGroup)));
        var nextStep = result.BlockedReason is null
            ? "Add what's missing to the hub, then start the bridge again."
            : $"The bridge won't change this hub, because {result.BlockedReason}. Add what's missing yourself, then start the bridge again.";

        LogSetupMissing(iotHub.HostName, missing, nextStep);

        await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            State = new ResourceStateSnapshot(WaitingForSetupState, KnownResourceStateStyles.Warn),
            Properties = ResourceProperties.Set(snapshot.Properties, new ResourcePropertySnapshot(PropertyNames.Setup, $"Missing: {missing}")),
        });

        return false;
    }

    private async Task<IoTHubSettingsReadResult> ReadSettingsAsync(
        string hubName)
    {
        using var timeout = new CancellationTokenSource(SetupCheckTimeout, resource.TimeProvider);
        using var timeoutOrStopping = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, stopping);

        try
        {
            return await readSettings(hubName, timeoutOrStopping.Token);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        {
            return IoTHubSettingsReadResult.NotVerified($"Azure didn't answer within {SetupCheckTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (!stopping.IsCancellationRequested)
        {
            return IoTHubSettingsReadResult.NotVerified(ex.Message);
        }
    }

    private Task PublishSetupAsync(string setup)
        => notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            Properties = ResourceProperties.Set(snapshot.Properties, new ResourcePropertySnapshot(PropertyNames.Setup, setup)),
        });

    // The devices to forward, or null for all devices.
    // A parameter's value is read at start, because the dashboard may ask the developer for it only then.
    private async Task<IReadOnlySet<string>?> GetDeviceIdsAsync()
    {
        if (resource.DeviceIdParameters.Count == 0)
        {
            return resource.DeviceIds;
        }

        var deviceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in resource.DeviceIdParameters)
        {
            var deviceId = await parameter.GetValueAsync(stopping);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException($"The parameter '{parameter.Name}' has no device id.");
            }

            deviceIds.Add(deviceId);
        }

        return deviceIds.ToFrozenSet(StringComparer.Ordinal);
    }

    private void StartRun(EventBridge runningBridge)
    {
        run?.Dispose();
        run = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        runFailed = false;

        var cancellationToken = run.Token;
        runTask = Task.Run(() => RunAsync(runningBridge, cancellationToken), CancellationToken.None);
    }

    private async Task RunAsync(
        EventBridge runningBridge,
        CancellationToken cancellationToken)
    {
        try
        {
            await runningBridge.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Paused, or the AppHost stops.
        }
        catch (Exception ex)
        {
            runFailed = true;
            LogFailed(ex, ex.Message);
            PublishCounts();

            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = new ResourceStateSnapshot(FailedState, KnownResourceStateStyles.Error),
            });
        }
    }

    private async Task StopRunAsync()
    {
        if (run is null)
        {
            return;
        }

        await run.CancelAsync();

        if (runTask is not null)
        {
            await runTask;
        }

        run.Dispose();
        run = null;
    }

    private void PublishCounts()
    {
        if (bridge is not { } runningBridge ||
            !runningBridge.Counters.TakeChanged())
        {
            return;
        }

        _ = PublishCountsAsync(runningBridge);
    }

    private async Task PublishCountsAsync(EventBridge runningBridge)
    {
        try
        {
            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                Properties = ResourceProperties.Set(snapshot.Properties, GetCountProperties(runningBridge)),
            });
        }
        catch (Exception ex)
        {
            LogCountsNotPublished(ex, ex.Message);
        }
    }

    private ResourcePropertySnapshot[] GetCountProperties(
        EventBridge runningBridge)
        => resource.Targets
            .Select(x => new ResourcePropertySnapshot(
                PropertyNames.GetCounts(x.Key),
                $"{x.Value.Name}: {runningBridge.Counters.Get(x.Key)}"))
            .ToArray();

    private static string GetDevicesText(IReadOnlySet<string>? deviceIds)
        => deviceIds is null
            ? "All devices"
            : string.Join(", ", deviceIds.Order(StringComparer.Ordinal));

    private async Task DisposeClientsAsync()
    {
        if (countsTimer is not null)
        {
            await countsTimer.DisposeAsync();
            countsTimer = null;
        }

        foreach (var sender in sendersByEventHub.Values)
        {
            await sender.DisposeAsync();
        }

        sendersByEventHub.Clear();

        if (reader is not null)
        {
            await reader.DisposeAsync();
            reader = null;
        }

        bridge = null;
    }

    /// <summary>
    /// The names of the properties the dashboard shows for the bridge.
    /// </summary>
    internal static class PropertyNames
    {
        public const string HostName = "iothub.hostName";
        public const string Transport = "iothub.transport";
        public const string Setup = "iothub.setup";
        public const string ConsumerGroup = "iothub.consumerGroup";
        public const string StartTime = "bridge.startTime";
        public const string Devices = "bridge.devices";

        public static string GetCounts(IoTHubEventSource source)
            => source switch
            {
                IoTHubEventSource.DeviceLifecycleEvents => "bridge.deviceLifecycleEvents",
                IoTHubEventSource.TwinChangeEvents => "bridge.twinChangeEvents",
                IoTHubEventSource.Telemetry => "bridge.telemetry",
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, message: null),
            };
    }
}