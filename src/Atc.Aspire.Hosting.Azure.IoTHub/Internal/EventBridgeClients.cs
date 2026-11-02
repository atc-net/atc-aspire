namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Creates the Event Hubs clients the bridge reads and sends with.
/// </summary>
internal static class EventBridgeClients
{
    private const string SharedAccessKeyPart = "SharedAccessKey=";

    /// <summary>
    /// Creates a reader for the IoT Hub's built-in endpoint, through its Event Hub-compatible endpoint.
    /// </summary>
    /// <param name="iotHub">The IoT Hub connection info.</param>
    /// <param name="options">The consumer group to read with, and whether to connect over WebSockets.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reader.</returns>
    public static async Task<IIoTHubEventReader> CreateReaderAsync(
        IoTHubConnectionInfo iotHub,
        IoTHubReaderOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = await IoTHubEventHubEndpoint.ResolveAsync(iotHub, options, TimeProvider.System, cancellationToken);
        return new ConsumerClientReader(options, endpoint.GetConnectionString(iotHub));
    }

    /// <summary>
    /// Creates a sender for an Event Hub of the AppHost, e.g. one in the Event Hubs emulator.
    /// </summary>
    /// <param name="eventHub">The Event Hub resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sender.</returns>
    /// <exception cref="InvalidOperationException">The Event Hub's connection string has no shared access key.</exception>
    public static async Task<IEventHubSender> CreateSenderAsync(
        AzureEventHubResource eventHub,
        CancellationToken cancellationToken)
    {
        var connectionString = await eventHub.ConnectionStringExpression.GetValueAsync(cancellationToken);
        if (connectionString is null ||
            !connectionString.Contains(SharedAccessKeyPart, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Event Hub '{eventHub.Name}' has no connection string with a shared access key. " +
                "The bridge sends to Event Hubs in the Event Hubs emulator (RunAsEmulator).");
        }

        return new ProducerClientSender(connectionString);
    }

    private sealed class ConsumerClientReader(
        IoTHubReaderOptions options,
        string connectionString) : IIoTHubEventReader
    {
        // EventHubConnectionOptions.Proxy is left unset on purpose: WebSockets then use the system's proxy settings
        // (the HTTPS_PROXY environment variable, otherwise Windows' proxy settings).
        private readonly EventHubConsumerClient client = new(
            options.ConsumerGroup,
            connectionString,
            new EventHubConsumerClientOptions
            {
                ConnectionOptions = new EventHubConnectionOptions
                {
                    TransportType = options.UseWebSockets
                        ? EventHubsTransportType.AmqpWebSockets
                        : EventHubsTransportType.AmqpTcp,
                },
            });

        public Task<string[]> GetPartitionIdsAsync(
            CancellationToken cancellationToken)
            => client.GetPartitionIdsAsync(cancellationToken);

        public async IAsyncEnumerable<EventData> ReadAsync(
            string partitionId,
            EventPosition position,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Without a maximum wait time, the read waits for the next event until it is cancelled.
            var partitionEvents = client
                .ReadEventsFromPartitionAsync(partitionId, position, cancellationToken)
                .Where(partitionEvent => partitionEvent.Data is not null);

            await foreach (var partitionEvent in partitionEvents)
            {
                yield return partitionEvent.Data;
            }
        }

        public ValueTask DisposeAsync()
            => client.DisposeAsync();
    }

    private sealed class ProducerClientSender(string connectionString) : IEventHubSender
    {
        private readonly EventHubProducerClient client = new(connectionString);

        public string EventHubName => client.EventHubName;

        public Task SendAsync(
            EventData eventData,
            string? partitionKey,
            CancellationToken cancellationToken)
            => client.SendAsync([eventData], new SendEventOptions { PartitionKey = partitionKey }, cancellationToken);

        public ValueTask DisposeAsync()
            => client.DisposeAsync();
    }
}