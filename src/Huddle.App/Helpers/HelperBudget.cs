using System.Globalization;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// The daily call cap of every Helper (Helpers Spec §6.4). A call is counted when it is reserved, before the process
/// starts, so a run that fails after launching still counts. The day is the Human's local calendar day.
/// </summary>
internal sealed class HelperBudget
{
    private readonly Lock gate = new();
    private readonly HelperUsageStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the budget over the usage store.</summary>
    /// <param name="store">Where the counts live.</param>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    public HelperBudget(HelperUsageStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Takes one call when today's count is under the cap plus today's grant; otherwise counts a skipped input.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="dailyCap">Calls a day before it pauses.</param>
    /// <returns><see langword="true"/> when the call may start.</returns>
    public bool TryReserve(HelperId id, int dailyCap)
    {
        lock (this.gate)
        {
            string day = this.Today();
            HelperUsage usage = this.store.Get(id, day);
            if (usage.Calls < dailyCap + usage.Granted)
            {
                this.store.AddCall(id, day);
                return true;
            }

            this.store.AddSkipped(id, day);
            return false;
        }
    }

    /// <summary>Whether today's calls have reached the cap plus today's grant.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="dailyCap">Calls a day before it pauses.</param>
    public bool IsPaused(HelperId id, int dailyCap)
    {
        HelperUsage usage = this.Today(id);
        return usage.Calls >= dailyCap + usage.Granted;
    }

    /// <summary>Today's usage.</summary>
    /// <param name="id">The Helper.</param>
    public HelperUsage Today(HelperId id)
    {
        lock (this.gate)
        {
            return this.store.Get(id, this.Today());
        }
    }

    /// <summary>Continue: adds calls for today only.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="calls">How many more calls to allow today.</param>
    public void Grant(HelperId id, int calls)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(calls);
        lock (this.gate)
        {
            this.store.AddGrant(id, this.Today(), calls);
        }
    }

    /// <summary>Adds a cost the adapter reported for a call.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="amount">The cost.</param>
    /// <param name="currency">Its currency.</param>
    public void RecordCost(HelperId id, decimal amount, string currency)
    {
        lock (this.gate)
        {
            this.store.AddCost(id, this.Today(), amount, currency);
        }
    }

    /// <summary>The Human's local day as <c>yyyy-MM-dd</c>.</summary>
    private string Today() => this.timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
