namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A settable <see cref="TimeProvider"/> for driving <c>RoomSessionPool</c>'s idle sweep (RS §6.2)
/// deterministically in a test, without waiting out real wall-clock minutes. Two private copies of
/// this idea already exist, in <c>PersonaHealthTests</c> and <c>FileLoggerTests</c>; this one is the
/// shared version Room Session tests use.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow;

    /// <summary>Creates a clock fixed at <see cref="TimeProvider.System"/>'s current instant.</summary>
    public ManualTimeProvider()
    {
        this.utcNow = TimeProvider.System.GetUtcNow();
    }

    /// <summary>This clock's current instant. Settable directly, as an alternative to <see cref="Advance"/>.</summary>
    public DateTimeOffset UtcNow
    {
        get => this.utcNow;
        set => this.utcNow = value;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this.utcNow;

    /// <summary>Moves this clock forward by <paramref name="delta"/>.</summary>
    /// <param name="delta">How far forward to move.</param>
    public void Advance(TimeSpan delta) => this.utcNow += delta;

    /// <summary>
    /// Returns a no-op <see cref="ITimer"/> instead of a real one (D23 correction 15): the base
    /// <see cref="TimeProvider.CreateTimer"/> implementation starts a genuine background timer bound
    /// to wall-clock time, which a test using this manual clock never wants running - the test drives
    /// <c>RoomSessionPool</c>'s sweep directly, through its own test seam, instead. A no-op timer
    /// also makes the sweep trivially safe to invoke concurrently with itself: nothing here ever
    /// fires a second, overlapping call on its own.
    /// </summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoOpTimer();

    /// <summary>An <see cref="ITimer"/> that never fires and does nothing on <see cref="Dispose"/>.</summary>
    private sealed class NoOpTimer : ITimer
    {
        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        /// <inheritdoc/>
        public void Dispose()
        {
            // Nothing to release: this timer never scheduled any real work.
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
