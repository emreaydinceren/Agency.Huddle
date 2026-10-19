using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that also counts the timers armed on it and not yet disposed, so
/// a test can wait until the code under test has actually registered a <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/>
/// before it advances the clock. Advancing first would move time past a delay that does not exist yet
/// and the delay, once armed, would wait for time that never comes.
/// </summary>
internal sealed class TrackedFakeTimeProvider : FakeTimeProvider
{
    private int pendingTimers;

    /// <summary>How many timers have been created on this clock and not yet disposed.</summary>
    public int PendingTimers => Volatile.Read(ref this.pendingTimers);

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer inner = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref this.pendingTimers);
        return new CountedTimer(this, inner);
    }

    /// <summary>
    /// Waits, in real time, until at least <paramref name="pending"/> timers are armed, then advances
    /// this clock by <paramref name="by"/>. The wait is bounded by <paramref name="ct"/>, so a delay
    /// that is never armed fails the test through cancellation instead of hanging it.
    /// </summary>
    /// <param name="pending">How many timers must be armed before time moves.</param>
    /// <param name="by">How far to move this clock.</param>
    /// <param name="ct">Bounds the wait.</param>
    /// <returns>A task that completes once the clock has been advanced.</returns>
    public async Task AdvanceWhenArmedAsync(int pending, TimeSpan by, CancellationToken ct)
    {
        await this.WaitForPendingAsync(pending, ct);
        this.Advance(by);
    }

    /// <summary>Waits, in real time, until at least <paramref name="pending"/> timers are armed on this clock.</summary>
    /// <param name="pending">The armed-timer count to wait for.</param>
    /// <param name="ct">Bounds the wait.</param>
    /// <returns>A task that completes once enough timers are armed.</returns>
    public async Task WaitForPendingAsync(int pending, CancellationToken ct)
    {
        while (this.PendingTimers < pending)
        {
            await Task.Delay(1, ct);
        }
    }

    /// <summary>Forwards to the wrapped timer and counts its disposal exactly once.</summary>
    private sealed class CountedTimer(TrackedFakeTimeProvider owner, ITimer inner) : ITimer
    {
        private int disposed;

        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Release();
            inner.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            this.Release();
            return inner.DisposeAsync();
        }

        private void Release()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 0)
            {
                Interlocked.Decrement(ref owner.pendingTimers);
            }
        }
    }
}
