using System.Diagnostics;
using Xunit.Sdk;

namespace Agency.Huddle.Tests.Waiting;

/// <summary>Pins <see cref="Eventually"/> and <see cref="TestBudgets"/> (test-split plan R1).</summary>
public sealed class EventuallyTests
{
    private static readonly TimeSpan shortTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>A condition that already holds returns without waiting for an interval.</summary>
    [Fact]
    public async Task UntilAsync_ConditionAlreadyMet_ReturnsWithoutDelay()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        long start = Stopwatch.GetTimestamp();

        await Eventually.UntilAsync(static () => true, ct, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Assert.True(Stopwatch.GetElapsedTime(start) < TimeSpan.FromMilliseconds(200));
    }

    /// <summary>An unmet condition fails with an assertion whose text contains the condition's source text.</summary>
    [Fact]
    public async Task UntilAsync_NeverMet_FailsNamingTheCondition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        bool neverTrue = false;

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.UntilAsync(() => neverTrue, ct, shortTimeout, TimeSpan.FromMilliseconds(5)));

        Assert.Contains("neverTrue", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The wait does not return before its timeout has elapsed when the condition never holds.</summary>
    [Fact]
    public async Task UntilAsync_NeverMet_WaitsAtLeastTheTimeout()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        long start = Stopwatch.GetTimestamp();

        _ = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.UntilAsync(static () => false, ct, shortTimeout, TimeSpan.FromMilliseconds(5)));

        Assert.True(Stopwatch.GetElapsedTime(start) >= (shortTimeout - TimeSpan.FromMilliseconds(15)));
    }

    /// <summary>Cancelling the test token fails the wait with an assertion naming the condition, not a cancellation exception.</summary>
    [Fact]
    public async Task UntilAsync_TokenCancelled_FailsNamingTheConditionNotCancellation()
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        bool neverTrue = false;

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.UntilAsync(() => neverTrue, cts.Token, interval: TimeSpan.FromMilliseconds(5)));

        Assert.Contains("neverTrue", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A condition that only becomes true once the deadline has passed still passes: the last poll happens before failing.</summary>
    [Fact]
    public async Task UntilAsync_TrueOnLastPollBeforeDeadline_Passes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        long start = Stopwatch.GetTimestamp();

        await Eventually.UntilAsync(
            () => Stopwatch.GetElapsedTime(start) >= shortTimeout, ct, shortTimeout, TimeSpan.FromMilliseconds(40));

        Assert.True(Stopwatch.GetElapsedTime(start) >= shortTimeout);
    }

    /// <summary>The text from <c>describe</c> is appended to the failure message.</summary>
    [Fact]
    public async Task UntilAsync_DescribeSupplied_AppendsDescriptionToMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.UntilAsync(
                static () => false, ct, shortTimeout, TimeSpan.FromMilliseconds(5), static () => "state-dump-42"));

        Assert.Contains("state-dump-42", ex.Message, StringComparison.Ordinal);
    }

    /// <summary><c>ValueAsync</c> returns the first value the <c>done</c> predicate accepts.</summary>
    [Fact]
    public async Task ValueAsync_ProbeEventuallyAccepted_ReturnsFirstAcceptedValue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int calls = 0;

        int result = await Eventually.ValueAsync(
            _ => Task.FromResult(++calls),
            static value => value >= 3,
            ct,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(5));

        Assert.Equal(3, result);
    }

    /// <summary><c>onTick</c> runs once for each poll whose value was not accepted.</summary>
    [Fact]
    public async Task ValueAsync_ProbeAcceptedOnThirdPoll_CallsOnTickOncePerRejectedPoll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int calls = 0;
        int ticks = 0;

        _ = await Eventually.ValueAsync(
            _ => Task.FromResult(++calls),
            static value => value >= 3,
            ct,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(5),
            onTick: () => ticks++);

        Assert.Equal(2, ticks);
    }

    /// <summary>A value that is never accepted fails with an assertion naming <c>what</c>.</summary>
    [Fact]
    public async Task ValueAsync_NeverAccepted_FailsNamingWhat()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.ValueAsync(
                static _ => Task.FromResult(0),
                static value => value > 0,
                ct,
                shortTimeout,
                TimeSpan.FromMilliseconds(5),
                what: "history count"));

        Assert.Contains("history count", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The named budgets keep the values the plan fixes.</summary>
    [Fact]
    public void TestBudgets_Values_MatchPlan()
    {
        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)],
            [TestBudgets.Short, TestBudgets.Standard, TestBudgets.Slow, TestBudgets.Slowest]);
    }
}
