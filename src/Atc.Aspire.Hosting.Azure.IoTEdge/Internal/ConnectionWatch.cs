namespace Atc.Aspire.Hosting.Azure.IoTEdge.Internal;

/// <summary>
/// Keeps at most one connection check running for a module: starting a new check or stopping cancels the running one.
/// </summary>
internal sealed class ConnectionWatch
{
    private readonly Lock gate = new();
    private CancellationTokenSource? current;

    /// <summary>
    /// Cancels the running check, if any, and returns the token for a new one.
    /// </summary>
    /// <param name="stopping">The token that stops all checks, e.g. when the AppHost stops.</param>
    /// <returns>The token for the new check.</returns>
    public CancellationToken Restart(CancellationToken stopping)
    {
        lock (gate)
        {
            CancelCurrent();
            current = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            return current.Token;
        }
    }

    /// <summary>
    /// Cancels the running check, if any.
    /// </summary>
    public void Stop()
    {
        lock (gate)
        {
            CancelCurrent();
        }
    }

    private void CancelCurrent()
    {
        if (current is null)
        {
            return;
        }

        current.Cancel();
        current.Dispose();
        current = null;
    }
}