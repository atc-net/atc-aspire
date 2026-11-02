namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class IoTHubEventTests
{
    [Theory]
    [InlineData("deviceLifecycleEvents", IoTHubEventSource.DeviceLifecycleEvents)]
    [InlineData("twinChangeEvents", IoTHubEventSource.TwinChangeEvents)]
    [InlineData("Telemetry", IoTHubEventSource.Telemetry)]
    public void GetSource_KnownMessageSource_ReturnsTheKind(
        string messageSource,
        IoTHubEventSource expected)
        => IoTHubEvent.GetSource(TestEvents.Create(messageSource, "device1", moduleId: null, operationType: null, sequenceNumber: 1))
            .Should().Be(expected);

    [Theory]
    [InlineData("deviceConnectionStateEvents")]
    [InlineData("telemetry")]
    [InlineData(null)]
    public void GetSource_OtherOrNoMessageSource_ReturnsNull(
        string? messageSource)
        => IoTHubEvent.GetSource(TestEvents.Create(messageSource, "device1", moduleId: null, operationType: null, sequenceNumber: 1))
            .Should().BeNull();

    [Fact]
    public void GetDeviceId_ConnectionDeviceIdAnnotation_ReturnsIt()
        => IoTHubEvent.GetDeviceId(TestEvents.Telemetry("device1", "module1", sequenceNumber: 1))
            .Should().Be("device1");

    [Fact]
    public void GetDeviceId_OnlyDeviceIdProperty_ReturnsTheProperty()
    {
        // Arrange
        var eventData = TestEvents.Create("deviceLifecycleEvents", deviceId: null, moduleId: null, operationType: null, sequenceNumber: 1);
        eventData.Properties["deviceId"] = "device1";

        // Act
        var result = IoTHubEvent.GetDeviceId(eventData);

        // Assert
        result.Should().Be("device1");
    }

    [Fact]
    public void GetModuleIdAndOperationType_TwinChangeOfAModule_ReturnsModuleAndOperation()
    {
        // Arrange
        var eventData = TestEvents.TwinChange("device1", "$edgeAgent", sequenceNumber: 1);

        // Act
        var moduleId = IoTHubEvent.GetModuleId(eventData);
        var operationType = IoTHubEvent.GetOperationType(eventData);

        // Assert
        moduleId.Should().Be("$edgeAgent");
        operationType.Should().Be("updateTwin");
    }

    [Fact]
    public void GetModuleIdAndOperationType_TelemetryOfADevice_ReturnsNull()
    {
        // Arrange
        var eventData = TestEvents.Create("Telemetry", "device1", moduleId: null, operationType: null, sequenceNumber: 1);

        // Act
        var moduleId = IoTHubEvent.GetModuleId(eventData);
        var operationType = IoTHubEvent.GetOperationType(eventData);

        // Assert
        moduleId.Should().BeNull();
        operationType.Should().BeNull();
    }

    [Fact]
    public void Copy_EventFromIoTHub_CopiesBodyPropertiesAndIoTHubAnnotations()
    {
        // Arrange
        var enqueuedTime = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        var eventData = TestEvents.TwinChange("device1", "module1", sequenceNumber: 42);
        eventData.MessageId = "message-1";
        eventData.CorrelationId = "correlation-1";

        var message = eventData.GetRawAmqpMessage();
        message.Properties.UserId = Encoding.UTF8.GetBytes("myhub");
        message.MessageAnnotations["iothub-enqueuedtime"] = enqueuedTime;
        message.MessageAnnotations["iothub-connection-auth-generation-id"] = "638000000000000000";

        // Act
        var copy = IoTHubEvent.Copy(eventData);

        // Assert
        copy.EventBody.ToString().Should().Be("""{"version":2}""");
        copy.ContentType.Should().Be("application/json");
        copy.MessageId.Should().Be("message-1");
        copy.CorrelationId.Should().Be("correlation-1");
        copy.Properties.Should().BeEquivalentTo(eventData.Properties);

        var copied = copy.GetRawAmqpMessage();
        copied.Properties.ContentEncoding.Should().Be("utf-8");
        Encoding.UTF8.GetString(copied.Properties.UserId!.Value.Span).Should().Be("myhub");
        copied.MessageAnnotations["iothub-enqueuedtime"].Should().Be(enqueuedTime);
        copied.MessageAnnotations.Keys.Should().BeEquivalentTo(
            "iothub-message-source",
            "iothub-connection-device-id",
            "iothub-connection-module-id",
            "iothub-enqueuedtime",
            "iothub-connection-auth-generation-id");
    }

    [Fact]
    public void Copy_EventFromIoTHub_LeavesOutTheBrokerAnnotations()
    {
        // Arrange
        var eventData = TestEvents.Telemetry("device1", "module1", sequenceNumber: 42);

        // Act
        var copy = IoTHubEvent.Copy(eventData);

        // Assert
        copy.GetRawAmqpMessage().MessageAnnotations.Keys.Should().NotContain(x => x.StartsWith("x-opt-", StringComparison.Ordinal));
        eventData.SequenceNumber.Should().Be(42);
    }
}