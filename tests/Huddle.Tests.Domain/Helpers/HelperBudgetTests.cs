using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperBudget"/> and <see cref="HelperUsageStore"/>: a daily call cap per Helper, a bounded Continue
/// grant, the Human's local day, and skipped inputs counted while paused (Helpers Spec §6.4, plan HL-5).
/// </summary>
public sealed class HelperBudgetTests
{
    private static readonly HelperId NeedsHuman = new("NeedsHuman");
    private static readonly HelperId Other = new("Other");
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    /// <summary>Builds a budget over a temp data directory on <paramref name="clock"/>.</summary>
    /// <param name="dir">The temp data directory.</param>
    /// <param name="clock">The clock.</param>
    private static HelperBudget Build(TempDataDir dir, ManualTimeProvider clock) => new(new HelperUsageStore(dir.Options()), clock);

    /// <summary>Calls up to the cap are reserved; the next is refused and counted as skipped.</summary>
    [Fact]
    public void TryReserve_AtCap_RefusesAndCountsSkipped()
    {
        using TempDataDir dir = new();
        HelperBudget budget = Build(dir, new ManualTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone));

        Assert.True(budget.TryReserve(NeedsHuman, 2));
        Assert.True(budget.TryReserve(NeedsHuman, 2));
        Assert.False(budget.TryReserve(NeedsHuman, 2));
        Assert.False(budget.TryReserve(NeedsHuman, 2));

        HelperUsage usage = budget.Today(NeedsHuman);
        Assert.Equal(2, usage.Calls);
        Assert.Equal(2, usage.Skipped);
        Assert.True(budget.IsPaused(NeedsHuman, 2));
    }

    /// <summary>Reaching one Helper's cap does not pause another Helper.</summary>
    [Fact]
    public void TryReserve_OneHelperAtCap_DoesNotPauseAnother()
    {
        using TempDataDir dir = new();
        HelperBudget budget = Build(dir, new ManualTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone));
        Assert.True(budget.TryReserve(NeedsHuman, 1));

        Assert.False(budget.TryReserve(NeedsHuman, 1));

        Assert.True(budget.TryReserve(Other, 1));
        Assert.False(budget.IsPaused(Other, 5));
    }

    /// <summary>Continue adds the grant to today only: it allows that many more calls, then pauses again.</summary>
    [Fact]
    public void Grant_AddsBoundedCallsForToday()
    {
        using TempDataDir dir = new();
        HelperBudget budget = Build(dir, new ManualTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone));
        Assert.True(budget.TryReserve(NeedsHuman, 1));
        Assert.False(budget.TryReserve(NeedsHuman, 1));

        budget.Grant(NeedsHuman, 2);

        Assert.False(budget.IsPaused(NeedsHuman, 1));
        Assert.True(budget.TryReserve(NeedsHuman, 1));
        Assert.True(budget.TryReserve(NeedsHuman, 1));
        Assert.False(budget.TryReserve(NeedsHuman, 1));
        Assert.Equal(2, budget.Today(NeedsHuman).Granted);
    }

    /// <summary>The count starts again at midnight in the Human's local zone, and the grant does not carry over.</summary>
    [Fact]
    public void TryReserve_NewLocalDay_ResetsCountAndGrant()
    {
        using TempDataDir dir = new();
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 20, 21, 0, 0, TimeSpan.Zero), Zone);
        HelperBudget budget = Build(dir, clock);
        Assert.True(budget.TryReserve(NeedsHuman, 1));
        budget.Grant(NeedsHuman, 5);

        // 21:00 UTC is 23:00 local; two hours later it is 01:00 local on the next day.
        clock.Advance(TimeSpan.FromHours(2));

        HelperUsage usage = budget.Today(NeedsHuman);
        Assert.Equal(0, usage.Calls);
        Assert.Equal(0, usage.Granted);
        Assert.True(budget.TryReserve(NeedsHuman, 1));
    }

    /// <summary>Within the same local day, an instant after UTC midnight still counts as the same day.</summary>
    [Fact]
    public void TryReserve_AfterUtcMidnightButSameLocalDay_KeepsCount()
    {
        using TempDataDir dir = new();
        TimeZoneInfo west = TimeZoneInfo.CreateCustomTimeZone("west", TimeSpan.FromHours(-5), "west", "west");

        // 22:00 UTC is 17:00 local; 01:30 UTC the next day is 20:30 local, the same local day.
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 20, 22, 0, 0, TimeSpan.Zero), west);
        HelperBudget budget = Build(dir, clock);
        Assert.True(budget.TryReserve(NeedsHuman, 1));

        clock.Advance(TimeSpan.FromHours(3.5));

        Assert.False(budget.TryReserve(NeedsHuman, 1));
    }

    /// <summary>Two concurrent reservations of the last call cannot both succeed.</summary>
    [Fact]
    public async Task TryReserve_Concurrent_NeverExceedsCap()
    {
        using TempDataDir dir = new();
        HelperBudget budget = Build(dir, new ManualTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone));

        Task<bool>[] attempts = [.. Enumerable.Range(0, 40).Select(_ => Task.Run(() => budget.TryReserve(NeedsHuman, 5), TestContext.Current.CancellationToken))];
        bool[] results = await Task.WhenAll(attempts);

        Assert.Equal(5, results.Count(static reserved => reserved));
        Assert.Equal(5, budget.Today(NeedsHuman).Calls);
        Assert.Equal(35, budget.Today(NeedsHuman).Skipped);
    }

    /// <summary>Usage survives a new store instance over the same database, as after a restart.</summary>
    [Fact]
    public void Today_NewStoreOverSameDatabase_SeesEarlierCalls()
    {
        using TempDataDir dir = new();
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone);
        Assert.True(Build(dir, clock).TryReserve(NeedsHuman, 3));

        HelperBudget restarted = Build(dir, clock);

        Assert.Equal(1, restarted.Today(NeedsHuman).Calls);
    }

    /// <summary>A reported cost is summed with its currency.</summary>
    [Fact]
    public void RecordCost_SumsReportedCosts()
    {
        using TempDataDir dir = new();
        HelperBudget budget = Build(dir, new ManualTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), Zone));

        budget.RecordCost(NeedsHuman, 0.01m, "USD");
        budget.RecordCost(NeedsHuman, 0.02m, "USD");

        HelperUsage usage = budget.Today(NeedsHuman);
        Assert.Equal(0.03m, usage.Cost);
        Assert.Equal("USD", usage.Currency);
    }
}
