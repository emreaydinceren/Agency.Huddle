namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// One Turn's own cancellation, linked to the run token, safe to cancel and dispose from two threads in either order. A Stop
/// (a Human's, or the goldfish slot closing) reads the live Turn under the session's gate and cancels it outside, while the
/// Turn that ended on its own disposes its source in teardown; with a bare <see cref="CancellationTokenSource"/> the cancel
/// landed on a disposed source (issue 677).
/// </summary>
/// <remarks>
/// The Turn's counterpart of <see cref="Services.StopSignal"/>, with one difference: <see cref="Dispose"/> does not cancel. A Turn
/// that ended on its own must not look stopped: the prompt call it handed the token to, and every registration on it, end
/// quietly rather than see a late cancel.
/// </remarks>
internal sealed class TurnCancellation : IDisposable
{
    private readonly CancellationTokenSource source;

    /// <summary>Serialises <see cref="Cancel"/> against <see cref="Dispose"/> and guards <see cref="disposed"/>.</summary>
    private readonly Lock gate = new();
    private bool disposed;

    /// <summary>Creates a Turn cancellation that is also cancelled when <paramref name="linkedTo"/> is.</summary>
    /// <param name="linkedTo">The run's own token.</param>
    public TurnCancellation(CancellationToken linkedTo)
    {
        this.source = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        this.Token = this.source.Token;
    }

    /// <summary>
    /// Cancelled by <see cref="Cancel"/> or by the run token. Captured once, so it stays readable after the source is
    /// disposed, where <see cref="CancellationTokenSource.Token"/> would throw.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Cancels <see cref="Token"/>, running its callbacks before it returns. Idempotent, and does nothing once the Turn's
    /// teardown has disposed it: that Turn is already over, so there is nothing left to stop. Synchronous on purpose: a Stop
    /// must release the consumer's await on the Turn before it sends the far side's cancel, and <c>CancelAsync</c> cannot
    /// run inside the lock.
    /// </summary>
    public void Cancel()
    {
        lock (this.gate)
        {
            if (!this.disposed)
            {
                this.source.Cancel();
            }
        }
    }

    /// <summary>Disposes the source without cancelling it. A <see cref="Cancel"/> that loses the race does nothing.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.source.Dispose();
            this.disposed = true;
        }
    }
}
