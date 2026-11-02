namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Sends events to one Event Hub.
/// </summary>
internal interface IEventHubSender : IAsyncDisposable
{
    string EventHubName { get; }

    Task SendAsync(
        EventData eventData,
        string? partitionKey,
        CancellationToken cancellationToken);
}