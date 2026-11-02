// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding an IoT Hub event bridge to an Aspire application.
/// </summary>
public static class IoTHubEventBridgeBuilderExtensions
{
    private const string BridgeResourceType = "IoTHubEventBridge";

    /// <summary>
    /// Adds a bridge that forwards the events of a real Azure IoT Hub to Event Hubs of the AppHost, e.g. in the
    /// Event Hubs emulator, so code that reads IoT Hub events runs locally. Choose what to forward with
    /// <see cref="ForwardDeviceLifecycleEvents"/>, <see cref="ForwardTwinChangeEvents"/> and <see cref="ForwardTelemetry"/>.
    /// The bridge reads from the time the AppHost started, and starts when the Event Hubs are healthy.
    /// </summary>
    /// <remarks>
    /// IoT Hub only delivers device lifecycle and twin change events to its built-in endpoint when routes send them there,
    /// and the bridge reads with its own consumer group (see <see cref="WithConsumerGroup"/>), which must exist.
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="iotHubConnectionString">
    /// A parameter holding an IoT Hub service connection string whose policy has service connect permission,
    /// e.g. the service or iothubowner policy.
    /// </param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> AddIoTHubEventBridge(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> iotHubConnectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(iotHubConnectionString);

        var resource = new IoTHubEventBridgeResource(name, iotHubConnectionString.Resource);

        return builder
            .AddResource(resource)
            .WithReferenceRelationship(iotHubConnectionString)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = BridgeResourceType,
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .OnInitializeResource(InitializeBridge)
            .WithBridgeCommands()
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Sets the consumer group of IoT Hub's built-in endpoint the bridge reads with.
    /// It must exist, and should be used by nothing else: a consumer group allows 5 readers per partition,
    /// and a reader that owns a partition, e.g. an Azure Function or an <c>EventProcessorClient</c>, disconnects the bridge.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="consumerGroup">The consumer group. Defaults to <see cref="IoTHubEventBridgeResource.DefaultConsumerGroup"/>.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> WithConsumerGroup(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        string consumerGroup)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);

        builder.Resource.ConsumerGroup = consumerGroup;
        return builder;
    }

    /// <summary>
    /// Connects to IoT Hub over AMQP over WebSockets on port 443 instead of AMQP on port 5671,
    /// for networks that only allow HTTPS. It uses the system's proxy settings.
    /// The Event Hubs of the AppHost are reached as before.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> WithWebSockets(
        this IResourceBuilder<IoTHubEventBridgeResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.UseWebSockets = true;
        return builder;
    }

    /// <summary>
    /// Forwards device lifecycle events (device and module identities created or deleted) to an Event Hub.
    /// IoT Hub needs a route that sends the source <c>DeviceLifecycleEvents</c> to the built-in endpoint <c>events</c>.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="eventHub">The Event Hub, e.g. one added to the Event Hubs emulator with <c>AddHub</c>.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForwardDeviceLifecycleEvents(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        IResourceBuilder<AzureEventHubResource> eventHub)
        => builder.Forward(IoTHubEventSource.DeviceLifecycleEvents, eventHub);

    /// <summary>
    /// Forwards twin change events (tags, desired or reported properties of device and module twins) to an Event Hub.
    /// IoT Hub needs a route that sends the source <c>TwinChangeEvents</c> to the built-in endpoint <c>events</c>.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="eventHub">The Event Hub, e.g. one added to the Event Hubs emulator with <c>AddHub</c>.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForwardTwinChangeEvents(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        IResourceBuilder<AzureEventHubResource> eventHub)
        => builder.Forward(IoTHubEventSource.TwinChangeEvents, eventHub);

    /// <summary>
    /// Forwards device-to-cloud messages of devices and modules to an Event Hub.
    /// IoT Hub delivers them to the built-in endpoint through the fallback route, or a route for the source <c>DeviceMessages</c>.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="eventHub">The Event Hub, e.g. one added to the Event Hubs emulator with <c>AddHub</c>.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForwardTelemetry(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        IResourceBuilder<AzureEventHubResource> eventHub)
        => builder.Forward(IoTHubEventSource.Telemetry, eventHub);

    /// <summary>
    /// Forwards only the events of the given devices, e.g. when the IoT Hub is shared with other developers.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="deviceIds">The device ids.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForDevices(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        params string[] deviceIds)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(deviceIds);

        if (deviceIds.Length == 0 || deviceIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Pass at least one device id, and no empty ones.", nameof(deviceIds));
        }

        RemoveDeviceIdParameters(builder.Resource);
        builder.Resource.DeviceIds = deviceIds.ToFrozenSet(StringComparer.Ordinal);
        return builder;
    }

    /// <summary>
    /// Forwards only the events of the devices whose ids the parameters hold, e.g. the parameter an IoT Edge device
    /// takes its id from, so the bridge forwards the events of that device. The values are read when the bridge starts.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <param name="deviceIds">The parameters holding the device ids.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForDevices(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        params IResourceBuilder<ParameterResource>[] deviceIds)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(deviceIds);

        if (deviceIds.Length == 0 || deviceIds.Any(x => x is null))
        {
            throw new ArgumentException("Pass at least one parameter, and no null ones.", nameof(deviceIds));
        }

        RemoveDeviceIdParameters(builder.Resource);
        builder.Resource.DeviceIds = null;
        builder.Resource.DeviceIdParameters = deviceIds.Select(x => x.Resource).ToArray();

        foreach (var deviceId in deviceIds)
        {
            builder.WithReferenceRelationship(deviceId);
        }

        return builder;
    }

    /// <summary>
    /// Forwards the events of all devices in the IoT Hub.
    /// </summary>
    /// <param name="builder">The bridge resource builder.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<IoTHubEventBridgeResource> ForAllDevices(
        this IResourceBuilder<IoTHubEventBridgeResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        RemoveDeviceIdParameters(builder.Resource);
        builder.Resource.DeviceIds = null;
        return builder;
    }

    private static void RemoveDeviceIdParameters(
        IoTHubEventBridgeResource resource)
    {
        // Otherwise the dashboard keeps showing references to parameters that are no longer used.
        var relationships = resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(x => resource.DeviceIdParameters.Contains(x.Resource))
            .ToList();

        foreach (var relationship in relationships)
        {
            resource.Annotations.Remove(relationship);
        }

        resource.DeviceIdParameters = [];
    }

    private static IResourceBuilder<IoTHubEventBridgeResource> Forward(
        this IResourceBuilder<IoTHubEventBridgeResource> builder,
        IoTHubEventSource source,
        IResourceBuilder<AzureEventHubResource> eventHub)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventHub);

        builder.Resource.SetTarget(source, eventHub.Resource);

        // The bridge starts when the Event Hub is healthy, e.g. when the emulator has started.
        return builder
            .WithReferenceRelationship(eventHub)
            .WaitFor(eventHub);
    }

    private static async Task InitializeBridge(
        IoTHubEventBridgeResource resource,
        InitializeResourceEvent initializeEvent,
        CancellationToken cancellationToken)
    {
        await initializeEvent.Notifications.PublishUpdateAsync(resource, snapshot => snapshot with
        {
            Properties = ResourceProperties.Set(
                snapshot.Properties,
                new ResourcePropertySnapshot(EventBridgeController.PropertyNames.ConsumerGroup, resource.ConsumerGroup)),
        });

        var lifetime = initializeEvent.Services.GetRequiredService<IHostApplicationLifetime>();
        var configuration = initializeEvent.Services.GetRequiredService<IConfiguration>();

        var interaction = initializeEvent.Services.GetRequiredService<IInteractionService>();

        // The same tenant setting Aspire uses when it provisions Azure resources.
        var tenantId = configuration["Azure:TenantId"];
        var signingIn = AzureSignIn.ExistingOrBrowser(tenantId);

        var setup = new IoTHubSetupClient(
            resource.ReadSettings ?? new ArmIoTHubSettingsReader(AzureSignIn.Existing(tenantId)).ReadAsync,
            resource.ReadSettings ?? new ArmIoTHubSettingsReader(signingIn).ReadAsync,
            resource.ApplySetup ?? new ArmIoTHubSetupWriter(signingIn).ApplyAsync,
            resource.ConfirmSetup ?? ((title, message, commandCancellationToken) => ConfirmSetupAsync(interaction, title, message, commandCancellationToken)));

        var controller = new EventBridgeController(
            resource,
            setup,
            initializeEvent.Notifications,
            initializeEvent.Logger,
            lifetime.ApplicationStopping);

        resource.Controller = controller;
        lifetime.ApplicationStopped.Register(() => _ = controller.DisposeAsync().AsTask());

        // Aspire applies WithExplicitStart() only to containers, projects and executables,
        // so the bridge checks it itself and has its own Start command.
        if (!resource.HasAnnotationOfType<ExplicitStartupAnnotation>())
        {
            _ = controller.StartAsync();
        }
    }

    private static IResourceBuilder<IoTHubEventBridgeResource> WithBridgeCommands(
        this IResourceBuilder<IoTHubEventBridgeResource> builder)
        => builder
            .WithCommand(
                name: "iothub-bridge-start",
                displayName: "Start forwarding",
                executeCommand: _ => StartBridge(builder.Resource),
                commandOptions: new CommandOptions
                {
                    Description = "Checks the IoT Hub's setup, connects to it and forwards its events from the time the AppHost started.",
                    IconName = "Play",
                    IsHighlighted = true,
                    UpdateState = context => IsInState(context, KnownResourceStates.NotStarted, KnownResourceStates.FailedToStart, EventBridgeController.WaitingForSetupState)
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden,
                })
            .WithCommand(
                name: "iothub-bridge-setup",
                displayName: "Set up IoT Hub",
                executeCommand: context => SetUpBridge(builder.Resource, context.CancellationToken),
                commandOptions: new CommandOptions
                {
                    Description = "Signs in to Azure if needed, shows what the IoT Hub lacks for the bridge and adds it once you confirm. Never changes a hub that others use.",
                    IconName = "Wrench",
                    IsHighlighted = true,
                    UpdateState = context => NeedsSetup(context)
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden,
                })
            .WithCommand(
                name: "iothub-bridge-pause",
                displayName: "Pause forwarding",
                executeCommand: _ => PauseBridge(builder.Resource),
                commandOptions: new CommandOptions
                {
                    Description = "Stops reading IoT Hub events. Resume continues after the last event read.",
                    IconName = "Pause",
                    IsHighlighted = true,
                    UpdateState = context => IsInState(context, KnownResourceStates.Running)
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden,
                })
            .WithCommand(
                name: "iothub-bridge-resume",
                displayName: "Resume forwarding",
                executeCommand: _ => ResumeBridge(builder.Resource),
                commandOptions: new CommandOptions
                {
                    Description = "Continues forwarding after the last event read.",
                    IconName = "Play",
                    IsHighlighted = true,
                    UpdateState = context => IsInState(context, EventBridgeController.PausedState, EventBridgeController.FailedState)
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden,
                });

    private static async Task<bool> ConfirmSetupAsync(
        IInteractionService interaction,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        // Without the dashboard, e.g. when the command is run from the Aspire CLI, nobody can be asked.
        if (!interaction.IsAvailable)
        {
            throw new InvalidOperationException("Set up IoT Hub needs the Aspire dashboard to ask for confirmation.");
        }

        var result = await interaction.PromptConfirmationAsync(
            title,
            message,
            new MessageBoxInteractionOptions
            {
                Intent = MessageIntent.Confirmation,
                PrimaryButtonText = "Set up",
                EnableMessageMarkdown = true,
            },
            cancellationToken);

        return result is { Canceled: false, Data: true };
    }

    // The setup helps when the hub misses something, or when the check at start couldn't read the hub.
    private static bool NeedsSetup(UpdateCommandStateContext context)
        => IsInState(context, EventBridgeController.WaitingForSetupState) ||
           (context.ResourceSnapshot.Properties.FirstOrDefault(x => x.Name == EventBridgeController.PropertyNames.Setup)?.Value is string setup &&
            setup.StartsWith(EventBridgeController.NotVerifiedPrefix, StringComparison.Ordinal));

    private static Task<ExecuteCommandResult> SetUpBridge(
        IoTHubEventBridgeResource resource,
        CancellationToken cancellationToken)
        => resource.Controller is { } controller
            ? controller.SetUpAsync(cancellationToken)
            : Task.FromResult(CommandResults.Failure("The bridge has not been initialized."));

    private static bool IsInState(
        UpdateCommandStateContext context,
        params string[] states)
        => context.ResourceSnapshot.State?.Text is { } state &&
           states.Contains(state, StringComparer.Ordinal);

    private static Task<ExecuteCommandResult> StartBridge(
        IoTHubEventBridgeResource resource)
    {
        if (resource.Controller is not { } controller)
        {
            return Task.FromResult(CommandResults.Failure("The bridge has not been initialized."));
        }

        // Starting waits for the Event Hubs, which may take a while; the state shows the progress.
        _ = controller.StartAsync();
        return Task.FromResult(CommandResults.Success("Starting the bridge."));
    }

    private static async Task<ExecuteCommandResult> PauseBridge(
        IoTHubEventBridgeResource resource)
        => resource.Controller is { } controller && await controller.PauseAsync()
            ? CommandResults.Success("Paused the bridge.")
            : CommandResults.Failure("The bridge isn't running.");

    private static async Task<ExecuteCommandResult> ResumeBridge(
        IoTHubEventBridgeResource resource)
        => resource.Controller is { } controller && await controller.ResumeAsync()
            ? CommandResults.Success("Resumed the bridge.")
            : CommandResults.Failure("The bridge isn't paused or failed.");
}