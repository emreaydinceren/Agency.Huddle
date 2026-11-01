namespace Agency.Huddle.App.Components.Shared;

/// <summary>The cancellation for "only the newest load counts": each <see cref="Next"/> cancels the load before it.</summary>
internal sealed class LatestLoad : IDisposable
{
    private CancellationTokenSource? source;
    private bool disposed;

    /// <summary>Cancels and disposes the previous load's source and returns the token for a new one.</summary>
    /// <returns>
    /// The token of the new load; an already-cancelled token once disposed, because a redraw queued on the renderer
    /// before the component's Dispose can still start a load, and that load must end quietly rather than throw.
    /// </returns>
    internal CancellationToken Next()
    {
        if (this.disposed)
        {
            return new CancellationToken(canceled: true);
        }

        this.Cancel();
        CancellationTokenSource next = new();
        this.source = next;
        return next.Token;
    }

    /// <summary>Cancels the current load without starting another (for a component that clears its state, such as an empty search).</summary>
    internal void Cancel()
    {
        this.source?.Cancel();
        this.source?.Dispose();
        this.source = null;
    }

    /// <summary>Cancels and disposes the current source; <see cref="Next"/> after this returns a cancelled token.</summary>
    public void Dispose()
    {
        this.disposed = true;
        this.Cancel();
    }
}
