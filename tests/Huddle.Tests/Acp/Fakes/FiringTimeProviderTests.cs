namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Tests for <see cref="FiringTimeProvider"/>: the fake clock whose <see cref="ITimer"/> instances
/// actually fire, synchronously, when <see cref="FiringTimeProvider.Advance"/> moves its time far
/// enough forward. Unlike <see cref="ManualTimeProvider"/>, whose timers are permanently inert, this
/// one is what the coalescing tests of Spec §10.3 need to drive <c>TaskTriggerService</c>'s timer.
/// </summary>
public sealed class FiringTimeProviderTests
{
    /// <summary>Advancing past a timer's due time fires its callback exactly once.</summary>
    [Fact]
    public void Advance_PastDueTime_FiresOnce()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(1, fireCount);
    }

    /// <summary>Advancing to a point short of a timer's due time never invokes its callback.</summary>
    [Fact]
    public void Advance_BeforeDueTime_DoesNotFire()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(0, fireCount);
    }

    /// <summary><see cref="ITimer.Change"/> replaces a timer's due time, measured from the instant Change was called.</summary>
    [Fact]
    public void Change_Reschedules()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        var changed = timer.Change(TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(changed);
        Assert.Equal(0, fireCount);

        clock.Advance(TimeSpan.FromSeconds(6));

        Assert.Equal(1, fireCount);
    }

    /// <summary>Disposing a timer before it is due permanently prevents it from firing.</summary>
    [Fact]
    public void Dispose_PreventsFiring()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        timer.Dispose();
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(0, fireCount);
    }

    /// <summary>A periodic timer (a finite, non-zero period) fires once per elapsed period, not just once.</summary>
    [Fact]
    public void Periodic_FiresEachPeriod()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(3, 500).Duration());

        Assert.Equal(3, fireCount);
    }

    /// <summary>A timer whose callback creates another timer on the same clock does not deadlock: callbacks run outside the internal lock.</summary>
    [Fact]
    public void CallbackCreatesTimer_NoDeadlock()
    {
        FiringTimeProvider clock = new();
        var innerFireCount = 0;
        ITimer? inner = null;
        using var outer = clock.CreateTimer(
            _ => inner = clock.CreateTimer(__ => innerFireCount++, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan),
            null,
            TimeSpan.FromSeconds(1),
            Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.NotNull(inner);
        Assert.Equal(0, innerFireCount);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(1, innerFireCount);
        inner.Dispose();
    }

    /// <summary>
    /// When an earlier callback (fired at the same due time, by creation order) disposes a
    /// later-due timer before that timer's own turn in the same <see cref="FiringTimeProvider.Advance"/>
    /// call, the disposed timer does not fire: disposal is re-checked immediately before each callback.
    /// </summary>
    [Fact]
    public void DisposeInsideEarlierCallback_DoesNotFire()
    {
        FiringTimeProvider clock = new();
        ITimer? later = null;
        var laterFireCount = 0;
        using var earlier = clock.CreateTimer(_ => later?.Dispose(), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        later = clock.CreateTimer(_ => laterFireCount++, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, laterFireCount);
    }

    /// <summary>
    /// A timer created with <see cref="TimeSpan.Zero"/> as its due time does not fire during
    /// <see cref="TimeProvider.CreateTimer"/> itself; it only fires on the next
    /// <see cref="FiringTimeProvider.Advance"/> call, even one that advances by
    /// <see cref="TimeSpan.Zero"/>.
    /// </summary>
    [Fact]
    public void ZeroDueTime_FiresOnNextAdvanceNotOnCreate()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        Assert.Equal(0, fireCount);

        clock.Advance(TimeSpan.Zero);

        Assert.Equal(1, fireCount);
    }

    /// <summary>Two timers due at different instants fire in due-time order, earliest first, regardless of creation order.</summary>
    [Fact]
    public void Advance_TimersAtDifferentDueTimes_FireInDueTimeOrder()
    {
        FiringTimeProvider clock = new();
        List<string> order = [];
        using var later = clock.CreateTimer(_ => order.Add("later"), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        using var earlier = clock.CreateTimer(_ => order.Add("earlier"), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(["earlier", "later"], order);
    }

    /// <summary>Two timers due at the exact same instant fire in the order they were created.</summary>
    [Fact]
    public void Advance_TimersAtTheSameDueTime_FireInCreationOrder()
    {
        FiringTimeProvider clock = new();
        List<string> order = [];
        using var first = clock.CreateTimer(_ => order.Add("first"), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        using var second = clock.CreateTimer(_ => order.Add("second"), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(["first", "second"], order);
    }

    /// <summary>A period of <see cref="TimeSpan.Zero"/> or <see cref="Timeout.InfiniteTimeSpan"/> means "once": the timer never re-fires, however far the clock advances afterward.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Advance_ZeroOrInfinitePeriod_FiresOnlyOnce(int periodMilliseconds)
    {
        FiringTimeProvider clock = new();
        var period = periodMilliseconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(periodMilliseconds);
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(1), period);

        clock.Advance(TimeSpan.FromSeconds(100));

        Assert.Equal(1, fireCount);
    }

    /// <summary>A timer created with an infinite due time never fires, no matter how far the clock advances.</summary>
    [Fact]
    public void Advance_InfiniteDueTime_NeverFires()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromDays(365));

        Assert.Equal(0, fireCount);
    }

    /// <summary>Calling <see cref="ITimer.Change"/> on an already-disposed timer returns false and does not resurrect it.</summary>
    [Fact]
    public void Change_AfterDispose_ReturnsFalse()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        timer.Dispose();

        var changed = timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.False(changed);
        Assert.Equal(0, fireCount);
    }

    /// <summary>The clock starts at a fixed instant in the past, not at the real wall clock, so tests are reproducible.</summary>
    [Fact]
    public void GetUtcNow_OnConstruction_IsAFixedPastInstant()
    {
        FiringTimeProvider clock = new();

        Assert.True(clock.GetUtcNow() < TimeProvider.System.GetUtcNow());
    }

    /// <summary>
    /// A single <see cref="FiringTimeProvider.Advance"/> call fires at most 10,000 timer callbacks,
    /// guarding against an unbounded retrigger loop (e.g. a periodic timer whose own callback keeps
    /// rescheduling itself immediately) hanging the test run.
    /// </summary>
    [Fact]
    public void Advance_PeriodicTimerRetriggersImmediately_CapsAtTenThousandFires()
    {
        FiringTimeProvider clock = new();
        var fireCount = 0;
        using var timer = clock.CreateTimer(_ => fireCount++, null, TimeSpan.Zero, TimeSpan.FromTicks(1));

        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(10_000, fireCount);
    }
}
