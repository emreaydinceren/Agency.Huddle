namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> whose <see cref="ITimer"/> instances actually fire, synchronously,
/// when <see cref="Advance"/> moves this clock's time forward far enough - unlike
/// <see cref="ManualTimeProvider"/>, whose timers are permanently inert <see cref="ITimer"/> stubs.
/// Used wherever a test needs to drive a coalescing timer (e.g. <c>TaskTriggerService</c>, Spec §10.3)
/// deterministically, without waiting out real wall-clock time.
/// </summary>
internal sealed class FiringTimeProvider : TimeProvider
{
    /// <summary>The largest number of timer callbacks a single <see cref="Advance"/> call will fire, guarding against an unbounded retrigger loop hanging a test.</summary>
    private const int MaxFiresPerAdvance = 10_000;

    /// <summary>Guards <see cref="utcNow"/> and <see cref="timers"/>. Never held while a callback runs.</summary>
    private readonly Lock gate = new();
    private readonly List<FiringTimer> timers = [];
    private DateTimeOffset utcNow = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private long nextSequence;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (this.gate)
        {
            return this.utcNow;
        }
    }

    /// <summary>
    /// Moves this clock forward by <paramref name="delta"/>, firing every timer due at or before the
    /// resulting instant. Advances stepwise: before each callback this clock's time is set to exactly
    /// that timer's own due time, and the timers due list is re-read after every callback, so a
    /// callback that creates or reschedules a timer due before the target instant is picked up within
    /// the same <see cref="Advance"/> call. Callbacks run outside <see cref="gate"/>, so one may safely
    /// create a timer, call <see cref="ITimer.Change"/>, or call <see cref="Advance"/> again.
    /// </summary>
    /// <param name="delta">How far forward to move this clock. May be <see cref="TimeSpan.Zero"/>, which still fires any timer already due.</param>
    public void Advance(TimeSpan delta)
    {
        DateTimeOffset target;
        lock (this.gate)
        {
            target = this.utcNow + delta;
        }

        var fireCount = 0;
        while (fireCount < MaxFiresPerAdvance)
        {
            FiringTimer? due;
            lock (this.gate)
            {
                due = this.timers
                    .Where(timer => timer.DueAt(target) is not null)
                    .OrderBy(timer => timer.DueAt(target)!.Value)
                    .ThenBy(timer => timer.Sequence)
                    .FirstOrDefault();

                if (due is null)
                {
                    break;
                }

                this.utcNow = due.DueAt(target)!.Value;
            }

            due.Fire();
            fireCount++;
        }

        lock (this.gate)
        {
            this.utcNow = target;
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (this.gate)
        {
            FiringTimer timer = new(this, callback, state, this.utcNow, dueTime, period, this.nextSequence);
            this.nextSequence++;
            this.timers.Add(timer);
            return timer;
        }
    }

    /// <summary>One timer created by <see cref="FiringTimeProvider.CreateTimer"/>: its schedule, and the callback <see cref="Fire"/> invokes.</summary>
    private sealed class FiringTimer : ITimer
    {
        private readonly FiringTimeProvider owner;
        private readonly TimerCallback callback;
        private readonly object? state;
        private TimeSpan period;
        private DateTimeOffset? scheduledAt;
        private bool disposed;

        /// <summary>Creates a timer already scheduled relative to <paramref name="createdAt"/>.</summary>
        public FiringTimer(FiringTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset createdAt, TimeSpan dueTime, TimeSpan period, long sequence)
        {
            this.owner = owner;
            this.callback = callback;
            this.state = state;
            this.period = period;
            this.Sequence = sequence;
            this.scheduledAt = dueTime == Timeout.InfiniteTimeSpan ? null : createdAt + dueTime;
        }

        /// <summary>This timer's creation order among every timer <see cref="FiringTimeProvider"/> has created, used to break due-time ties.</summary>
        public long Sequence { get; }

        /// <summary>
        /// This timer's due instant, or <see langword="null"/> if it is disposed, has no schedule
        /// (an infinite due time), or is scheduled after <paramref name="target"/>. Must be called
        /// while holding <see cref="FiringTimeProvider.gate"/>.
        /// </summary>
        public DateTimeOffset? DueAt(DateTimeOffset target) =>
            !this.disposed && this.scheduledAt is { } at && at <= target ? at : null;

        /// <summary>
        /// Runs this timer's callback, then reschedules it for its next period (or clears its
        /// schedule, for a one-shot timer). The disposed check happens immediately before invoking
        /// the callback, inside the lock, so a dispose issued by an earlier callback in the same
        /// <see cref="FiringTimeProvider.Advance"/> pass is honoured.
        /// </summary>
        public void Fire()
        {
            bool shouldRun;
            lock (this.owner.gate)
            {
                shouldRun = !this.disposed;
            }

            if (shouldRun)
            {
                this.callback(this.state);
            }

            lock (this.owner.gate)
            {
                if (this.disposed)
                {
                    return;
                }

                this.scheduledAt = this.period == TimeSpan.Zero || this.period == Timeout.InfiniteTimeSpan
                    ? null
                    : this.owner.utcNow + this.period;
            }
        }

        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (this.owner.gate)
            {
                if (this.disposed)
                {
                    return false;
                }

                this.period = period;
                this.scheduledAt = dueTime == Timeout.InfiniteTimeSpan ? null : this.owner.utcNow + dueTime;
                return true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (this.owner.gate)
            {
                this.disposed = true;
                this.scheduledAt = null;
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
