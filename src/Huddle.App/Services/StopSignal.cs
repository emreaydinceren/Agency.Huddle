namespace Agency.Huddle.App.Services;

/// <summary>
/// A hosted service's lifetime token, safe to stop and dispose from two threads in either order. Under
/// <c>WebApplicationFactory</c> the factory stops the host on one thread while <c>Program</c>'s <c>RunAsync</c> disposes it on
/// another, so a bare <see cref="CancellationTokenSource"/> field was cancelled after it was disposed four times
/// (<c>TailscaleRuntime</c>, <c>TaskTriggerService</c>, <c>TeamRoomReconciler</c>, <c>HelperDispatcher</c>). Analyzer
/// <c>HUD0002</c> requires this type in a hosted service.
/// </summary>
internal sealed class StopSignal : IDisposable
{
    private readonly CancellationTokenSource source = new();

    /// <summary>Serialises <see cref="Stop"/> against <see cref="Dispose"/> and guards <see cref="disposed"/>.</summary>
    private readonly Lock gate = new();
    private bool disposed;

    /// <summary>Creates a signal that has not stopped.</summary>
    public StopSignal()
    {
        this.Token = this.source.Token;
    }

    /// <summary>
    /// Cancelled by <see cref="Stop"/> or <see cref="Dispose"/>. Captured once, so it stays readable after the source is
    /// disposed, where <see cref="CancellationTokenSource.Token"/> would throw.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>Whether <see cref="Stop"/> or <see cref="Dispose"/> has run.</summary>
    public bool IsStopped => this.Token.IsCancellationRequested;

    /// <summary>
    /// Cancels <see cref="Token"/>. Idempotent, and does nothing once disposed, because <see cref="Dispose"/> cancelled first.
    /// Synchronous on purpose: <c>CancelAsync</c> cannot run inside the lock.
    /// </summary>
    public void Stop()
    {
        lock (this.gate)
        {
            if (!this.disposed)
            {
                this.source.Cancel();
            }
        }
    }

    /// <summary>Cancels <see cref="Token"/>, then disposes the source. A <see cref="Stop"/> that loses the race does nothing.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.source.Cancel();
            this.source.Dispose();
            this.disposed = true;
        }
    }
}
