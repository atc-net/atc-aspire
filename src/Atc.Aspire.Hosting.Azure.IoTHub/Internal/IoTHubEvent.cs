namespace Atc.Aspire.Hosting.Azure.IoTHub.Internal;

/// <summary>
/// Reads and copies the events IoT Hub delivers on its built-in endpoint.
/// </summary>
internal static class IoTHubEvent
{
    public const string MessageSourceKey = "iothub-message-source";
    public const string ConnectionDeviceIdKey = "iothub-connection-device-id";
    public const string ConnectionModuleIdKey = "iothub-connection-module-id";
    public const string DeviceIdPropertyName = "deviceId";
    public const string ModuleIdPropertyName = "moduleId";
    public const string OperationTypePropertyName = "opType";

    // Annotations an Event Hub sets on the events it holds: their position there (sequence number, offset, enqueued time).
    private const string BrokerAnnotationPrefix = "x-opt-";

    /// <summary>
    /// Gets the kind of an event from its <c>iothub-message-source</c> annotation.
    /// </summary>
    /// <param name="eventData">The event.</param>
    /// <returns>The kind, or <see langword="null"/> for events the bridge doesn't forward, e.g. connection state events.</returns>
    public static IoTHubEventSource? GetSource(EventData eventData)
        => GetAnnotation(eventData, MessageSourceKey) switch
        {
            "deviceLifecycleEvents" => IoTHubEventSource.DeviceLifecycleEvents,
            "twinChangeEvents" => IoTHubEventSource.TwinChangeEvents,
            "Telemetry" => IoTHubEventSource.Telemetry,
            _ => null,
        };

    /// <summary>
    /// Gets the device id: the <c>iothub-connection-device-id</c> annotation, which every kind of event has,
    /// otherwise the <c>deviceId</c> application property of lifecycle and twin change events.
    /// </summary>
    /// <param name="eventData">The event.</param>
    /// <returns>The device id, or <see langword="null"/>.</returns>
    public static string? GetDeviceId(EventData eventData)
        => GetAnnotation(eventData, ConnectionDeviceIdKey) ?? GetProperty(eventData, DeviceIdPropertyName);

    /// <summary>
    /// Gets the module id, for events of a module.
    /// </summary>
    /// <param name="eventData">The event.</param>
    /// <returns>The module id, or <see langword="null"/> for events of a device.</returns>
    public static string? GetModuleId(EventData eventData)
        => GetAnnotation(eventData, ConnectionModuleIdKey) ?? GetProperty(eventData, ModuleIdPropertyName);

    /// <summary>
    /// Gets the operation of a lifecycle or twin change event, e.g. <c>createDeviceIdentity</c> or <c>updateTwin</c>.
    /// </summary>
    /// <param name="eventData">The event.</param>
    /// <returns>The operation, or <see langword="null"/> for telemetry.</returns>
    public static string? GetOperationType(EventData eventData)
        => GetProperty(eventData, OperationTypePropertyName);

    /// <summary>
    /// Makes the event to send to an Event Hub: the event as IoT Hub delivered it, without its <c>x-opt-*</c> annotations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is kept: everything a consumer reads. That is the body, the application properties, the properties section
    /// (message id, correlation id, content type, ...) and IoT Hub's <c>iothub-*</c> annotations. So a consumer of the
    /// Event Hub sees the same system properties as a consumer of IoT Hub, e.g. the device id and the time IoT Hub
    /// received the event (<c>iothub-enqueuedtime</c>).
    /// </para>
    /// <para>
    /// What is left out: the <c>x-opt-*</c> annotations. They are the event's position in IoT Hub's built-in endpoint
    /// (sequence number, offset, the time it was put there) and mean nothing in another Event Hub, which numbers the events
    /// it receives itself. Sending an event sends all its annotations, so these would at best be overwritten there, and at
    /// worst reach its consumers, whose checkpoints would then point at positions that don't exist in that Event Hub.
    /// </para>
    /// <para>
    /// Why a copy: the bridge reads the sequence number of the event read from IoT Hub after sending it, to continue after
    /// it on Resume, so that event is left unchanged.
    /// </para>
    /// </remarks>
    /// <param name="eventData">The event read from IoT Hub.</param>
    /// <returns>The event to send.</returns>
    public static EventData Copy(EventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var source = eventData.GetRawAmqpMessage();
        var copy = new EventData(eventData.EventBody);
        var target = copy.GetRawAmqpMessage();

        CopyProperties(source.Properties, target.Properties);

        foreach (var (key, value) in source.ApplicationProperties)
        {
            target.ApplicationProperties[key] = value;
        }

        foreach (var (key, value) in source.MessageAnnotations)
        {
            if (!key.StartsWith(BrokerAnnotationPrefix, StringComparison.Ordinal))
            {
                target.MessageAnnotations[key] = value;
            }
        }

        return copy;
    }

    private static void CopyProperties(
        AmqpMessageProperties source,
        AmqpMessageProperties target)
    {
        target.MessageId = source.MessageId;
        target.UserId = source.UserId;
        target.To = source.To;
        target.Subject = source.Subject;
        target.ReplyTo = source.ReplyTo;
        target.CorrelationId = source.CorrelationId;
        target.ContentType = source.ContentType;
        target.ContentEncoding = source.ContentEncoding;
        target.AbsoluteExpiryTime = source.AbsoluteExpiryTime;
        target.CreationTime = source.CreationTime;
        target.GroupId = source.GroupId;
        target.GroupSequence = source.GroupSequence;
        target.ReplyToGroupId = source.ReplyToGroupId;
    }

    private static string? GetAnnotation(
        EventData eventData,
        string key)
        => eventData.GetRawAmqpMessage().MessageAnnotations.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private static string? GetProperty(
        EventData eventData,
        string name)
        => eventData.Properties.TryGetValue(name, out var value)
            ? value?.ToString()
            : null;
}