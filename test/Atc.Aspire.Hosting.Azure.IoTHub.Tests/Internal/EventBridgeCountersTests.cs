namespace Atc.Aspire.Hosting.Azure.IoTHub.Tests.Internal;

public sealed class EventBridgeCountersTests
{
    [Fact]
    public void Get_CountsAdded_ReturnsTheCountsOfThatKindOnly()
    {
        // Arrange
        var counters = new EventBridgeCounters();

        // Act
        counters.AddForwarded(IoTHubEventSource.TwinChangeEvents);
        counters.AddForwarded(IoTHubEventSource.TwinChangeEvents);
        counters.AddSkipped(IoTHubEventSource.TwinChangeEvents);
        counters.AddFailed(IoTHubEventSource.Telemetry);

        // Assert
        counters.Get(IoTHubEventSource.TwinChangeEvents).Should().Be(new EventCounts(Forwarded: 2, Skipped: 1, Failed: 0));
        counters.Get(IoTHubEventSource.Telemetry).Should().Be(new EventCounts(Forwarded: 0, Skipped: 0, Failed: 1));
        counters.Get(IoTHubEventSource.DeviceLifecycleEvents).Should().Be(default(EventCounts));
    }

    [Fact]
    public void TakeChanged_AfterACountAndAgain_ReturnsTrueOnce()
    {
        // Arrange
        var counters = new EventBridgeCounters();
        counters.AddSkipped(IoTHubEventSource.Telemetry);

        // Act
        var first = counters.TakeChanged();
        var second = counters.TakeChanged();

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public void ToString_Counts_ReadsAsText()
        => new EventCounts(Forwarded: 3, Skipped: 2, Failed: 1).ToString()
            .Should().Be("3 forwarded, 2 skipped, 1 failed");
}