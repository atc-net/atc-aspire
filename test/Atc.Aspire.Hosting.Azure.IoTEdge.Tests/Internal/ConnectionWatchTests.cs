namespace Atc.Aspire.Hosting.Azure.IoTEdge.Tests.Internal;

public sealed class ConnectionWatchTests
{
    private readonly ConnectionWatch connectionWatch = new();

    [Fact]
    public void Restart_CheckRunning_CancelsTheRunningCheck()
    {
        // Arrange
        var first = connectionWatch.Restart(CancellationToken.None);

        // Act
        var second = connectionWatch.Restart(CancellationToken.None);

        // Assert
        first.IsCancellationRequested.Should().BeTrue();
        second.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Stop_CheckRunning_CancelsTheRunningCheck()
    {
        // Arrange
        var check = connectionWatch.Restart(CancellationToken.None);

        // Act
        connectionWatch.Stop();

        // Assert
        check.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Restart_AppHostStopping_CancelsTheCheck()
    {
        // Arrange
        using var stopping = new CancellationTokenSource();
        var check = connectionWatch.Restart(stopping.Token);

        // Act
        await stopping.CancelAsync();

        // Assert
        check.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Stop_NoCheckRunning_DoesNothing()
    {
        // Act
        var act = () => connectionWatch.Stop();

        // Assert
        act.Should().NotThrow();
    }
}