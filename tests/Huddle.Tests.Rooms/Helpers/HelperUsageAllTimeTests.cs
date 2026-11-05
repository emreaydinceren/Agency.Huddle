using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// Pins <see cref="HelperUsageStore.AllTime"/>: the all-time cost and calls of every Helper that the Spending panel
/// shows on its Helpers row.
/// </summary>
public sealed class HelperUsageAllTimeTests : IDisposable
{
    private static readonly HelperId NeedsHuman = new("NeedsHuman");
    private static readonly HelperId Other = new("Other");

    private readonly TempDataDir dir = new();
    private readonly HelperUsageStore store;

    /// <summary>Opens a <see cref="HelperUsageStore"/> over a temp data folder.</summary>
    public HelperUsageAllTimeTests() => this.store = new HelperUsageStore(this.dir.Options());

    /// <summary>Deletes the temp data folder.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>A store with nothing in it has no calls and no cost.</summary>
    [Fact]
    public void AllTime_NothingStored_IsZeroCallsAndNoCost()
    {
        HelperSpendTotal total = this.store.AllTime();

        Assert.Equal(0, total.Calls);
        Assert.Empty(total.Cost);
    }

    /// <summary>Calls are summed over every Helper and every day, and so is the cost in one currency.</summary>
    [Fact]
    public void AllTime_SeveralHelpersAndDays_SumsCallsAndCost()
    {
        this.store.AddCall(NeedsHuman, "2026-11-01");
        this.store.AddCall(NeedsHuman, "2026-11-01");
        this.store.AddCall(NeedsHuman, "2026-11-02");
        this.store.AddCall(Other, "2026-11-02");
        this.store.AddCost(NeedsHuman, "2026-11-01", 0.25m, "USD");
        this.store.AddCost(NeedsHuman, "2026-11-02", 0.5m, "USD");
        this.store.AddCost(Other, "2026-11-02", 0.125m, "USD");

        HelperSpendTotal total = this.store.AllTime();

        Assert.Equal(4, total.Calls);
        StoredSpend cost = Assert.Single(total.Cost);
        Assert.Equal("USD", cost.Currency);
        Assert.Equal(0.875m, cost.Amount);
    }

    /// <summary>Two currencies stay as two amounts; they are never added together.</summary>
    [Fact]
    public void AllTime_TwoCurrencies_StayApart()
    {
        this.store.AddCost(NeedsHuman, "2026-11-01", 1m, "USD");
        this.store.AddCost(Other, "2026-11-01", 2m, "EUR");

        Assert.Equal(
            ["EUR|2", "USD|1"],
            [.. this.store.AllTime().Cost
                .OrderBy(static c => c.Currency, StringComparer.Ordinal)
                .Select(static c => $"{c.Currency}|{c.Amount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}")]);
    }

    /// <summary>A Helper that made calls but reported no cost adds to the call count and not to the cost, so no zero amount appears.</summary>
    [Fact]
    public void AllTime_CallsWithoutCost_CountsCallsOnly()
    {
        this.store.AddCall(NeedsHuman, "2026-11-01");
        this.store.AddSkipped(NeedsHuman, "2026-11-01");

        HelperSpendTotal total = this.store.AllTime();

        Assert.Equal(1, total.Calls);
        Assert.Empty(total.Cost);
    }
}
