namespace Atc.Aspire.Hosting.Azure.IoTHub;

/// <summary>
/// Represents a bridge that reads the events of a real Azure IoT Hub's built-in endpoint and forwards them to
/// Event Hubs of the AppHost, e.g. in the Event Hubs emulator: device lifecycle events, twin change events and telemetry,
/// each to its own Event Hub. It runs in the AppHost process.
/// </summary>
public sealed class IoTHubEventBridgeResource : Resource, IResourceWithWaitSupport
{
    /// <summary>
    /// The consumer group of the built-in endpoint the bridge reads with, unless set with <c>WithConsumerGroup</c>.
    /// </summary>
    public const string DefaultConsumerGroup = "aspire-bridge";

    private readonly Dictionary<IoTHubEventSource, AzureEventHubResource> targets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="IoTHubEventBridgeResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="iotHubConnectionString">The parameter holding the IoT Hub service connection string.</param>
    public IoTHubEventBridgeResource(
        string name,
        ParameterResource iotHubConnectionString)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(iotHubConnectionString);

        IotHubConnectionString = iotHubConnectionString;
    }

    /// <summary>
    /// Gets the parameter holding the IoT Hub service connection string. Its policy needs service connect permission.
    /// </summary>
    public ParameterResource IotHubConnectionString { get; }

    /// <summary>
    /// Gets the consumer group of the built-in endpoint the bridge reads with.
    /// </summary>
    public string ConsumerGroup { get; internal set; } = DefaultConsumerGroup;

    /// <summary>
    /// Gets the Event Hub each kind of event is forwarded to.
    /// </summary>
    public IReadOnlyDictionary<IoTHubEventSource, AzureEventHubResource> Targets
        => targets;

    /// <summary>
    /// Gets the devices whose events are forwarded, or <see langword="null"/> to forward the events of all devices
    /// or of the devices whose ids parameters hold.
    /// </summary>
    public IReadOnlySet<string>? DeviceIds { get; internal set; }

    // The parameters holding the ids of the devices whose events are forwarded; read when the bridge starts.
    internal IReadOnlyList<ParameterResource> DeviceIdParameters { get; set; } = [];

    /// <summary>
    /// Gets whether the bridge connects to IoT Hub over AMQP over WebSockets (port 443) instead of AMQP (port 5671).
    /// </summary>
    public bool UseWebSockets { get; internal set; }

    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    internal Func<IoTHubConnectionInfo, IoTHubReaderOptions, CancellationToken, Task<IIoTHubEventReader>> CreateReader { get; set; }
        = EventBridgeClients.CreateReaderAsync;

    internal Func<AzureEventHubResource, CancellationToken, Task<IEventHubSender>> CreateSender { get; set; }
        = EventBridgeClients.CreateSenderAsync;

    // Null reads the hub's settings through Azure Resource Manager, with the developer's Azure sign-in.
    internal Func<string, CancellationToken, Task<IoTHubSettingsReadResult>>? ReadSettings { get; set; }

    internal EventBridgeController? Controller { get; set; }

    internal void SetTarget(
        IoTHubEventSource source,
        AzureEventHubResource eventHub)
    {
        if (targets.TryGetValue(source, out var existing))
        {
            throw new InvalidOperationException(
                $"IoT Hub event bridge '{Name}' already forwards {source} to Event Hub '{existing.Name}'.");
        }

        targets[source] = eventHub;
    }
}