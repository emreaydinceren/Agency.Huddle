using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Tells the Feed about one Helper's health (Helpers in the Feed spec §5.3): a Notification when its daily cap pauses it, one when it
/// fails several times in a row, and the resolution of each when the condition ends. It decides when to post and when to resolve, so
/// the dispatcher and the Helpers card only report what happened. No member throws: a Feed that fails never takes a Helper down.
/// </summary>
/// <param name="feed">Where the items go.</param>
/// <param name="status">The Helpers' last results, for the failure count after a restart.</param>
/// <param name="budget">Today's usage, for the pause after a restart.</param>
/// <param name="registry">Finds a Helper by id.</param>
/// <param name="settings">Resolves a Helper's effective settings.</param>
/// <param name="logger">Records a Feed call that failed.</param>
internal sealed class HelperFeed(
    IFeedPublisher feed,
    HelperStatusStore status,
    HelperBudget budget,
    HelperRegistry registry,
    HelperSettingsResolver settings,
    ILogger<HelperFeed> logger)
{
    /// <summary>How many failures in a row make a Notification; a constant, not a setting (HF-D11).</summary>
    internal const int FailureThreshold = 3;

    /// <summary>The most characters of the last error an item carries.</summary>
    internal const int ErrorLimit = 120;

    private const string TargetPrefix = "helper:";

    private readonly Lock gate = new();
    private readonly HashSet<string> pausedOpen = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> failingOpen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The target key of the items about a Helper, such as <c>helper:NeedsHuman</c>.</summary>
    /// <param name="id">The Helper.</param>
    public static string TargetOf(HelperId id) => TargetPrefix + id.Value;

    /// <summary>A run ended over budget: posts the pause item, once per pause.</summary>
    /// <param name="helper">The Helper.</param>
    /// <param name="effective">Its settings at the time.</param>
    public void Paused(IHelperAgent helper, HelperSettings effective)
    {
        this.Safely(() =>
        {
            if (!this.Open(this.pausedOpen, helper.Id))
            {
                return;
            }

            HelperUsage usage = budget.Today(helper.Id);
            feed.Post(new FeedMessage(
                FeedSourceType.HelperPaused,
                TargetOf(helper.Id),
                FeedActor.Huddle,
                true,
                new HelperPausedPayload(helper.Id.Value, helper.DisplayName, effective.DailyCap, usage.Granted)));
        });
    }

    /// <summary>The Human pressed Continue: the pause item is done.</summary>
    /// <param name="id">The Helper.</param>
    public void Continued(HelperId id) => this.Safely(() => this.Close(this.pausedOpen, id, FeedSourceType.HelperPaused));

    /// <summary>A run went ahead and ended, so the cap has reset if a pause was open, and a failure streak ends or may now qualify.</summary>
    /// <param name="helper">The Helper.</param>
    /// <param name="kind">How the run ended; <see cref="HelperRunKind.OverBudget"/> is reported through <see cref="Paused"/> instead.</param>
    /// <param name="current">The Helper's status after the run was recorded.</param>
    public void Ran(IHelperAgent helper, HelperRunKind kind, HelperStatus current)
    {
        ArgumentNullException.ThrowIfNull(helper);
        ArgumentNullException.ThrowIfNull(current);
        this.Safely(() =>
        {
            switch (kind)
            {
                case HelperRunKind.Applied:
                    this.Close(this.pausedOpen, helper.Id, FeedSourceType.HelperPaused);
                    this.Close(this.failingOpen, helper.Id, FeedSourceType.HelperFailing);
                    break;
                case HelperRunKind.Failed:
                    this.Close(this.pausedOpen, helper.Id, FeedSourceType.HelperPaused);
                    this.PostFailing(helper, current);
                    break;
                default:
                    break;
            }
        });
    }

    /// <summary>The Human turned the Helper off: nothing is left to watch, so both items are done.</summary>
    /// <param name="id">The Helper.</param>
    public void TurnedOff(HelperId id) => this.Safely(() =>
    {
        this.Close(this.pausedOpen, id, FeedSourceType.HelperPaused);
        this.Close(this.failingOpen, id, FeedSourceType.HelperFailing);
    });

    /// <summary>
    /// At startup, remembers the items that are still unresolved and resolves those whose condition ended while Huddle was stopped:
    /// a cap that reset, a streak a good run ended, a Helper turned off or removed.
    /// </summary>
    /// <param name="store">The Feed, read for its unresolved Helper items.</param>
    public void Reconcile(FeedStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        foreach (FeedItem item in store.Unresolved(FeedSourceType.HelperPaused))
        {
            this.ReconcileItem(item, FeedSourceType.HelperPaused, this.pausedOpen, (id, effective) => budget.IsPaused(id, effective.DailyCap));
        }

        foreach (FeedItem item in store.Unresolved(FeedSourceType.HelperFailing))
        {
            this.ReconcileItem(item, FeedSourceType.HelperFailing, this.failingOpen, (id, _) => status.Get(id).ConsecutiveFailures >= FailureThreshold);
        }
    }

    /// <summary>Keeps one unresolved item when its Helper is on and the condition still holds, otherwise resolves it.</summary>
    /// <param name="item">The unresolved item.</param>
    /// <param name="type">Its source type.</param>
    /// <param name="open">The set to remember it in.</param>
    /// <param name="holds">Whether the condition still holds for the Helper.</param>
    private void ReconcileItem(FeedItem item, FeedSourceType type, HashSet<string> open, Func<HelperId, HelperSettings, bool> holds)
    {
        this.Safely(() =>
        {
            HelperId id = new(item.TargetKey.StartsWith(TargetPrefix, StringComparison.Ordinal) ? item.TargetKey[TargetPrefix.Length..] : item.TargetKey);
            IHelperAgent? helper = registry.Find(id);
            HelperSettings? effective = helper is null ? null : settings.For(helper);
            if (effective is { Enabled: true } && holds(id, effective))
            {
                lock (this.gate)
                {
                    _ = open.Add(id.Value);
                }

                return;
            }

            feed.Resolve(item.TargetKey, type);
        });
    }

    /// <summary>Posts the failing item when the streak has reached the threshold and none is open.</summary>
    /// <param name="helper">The Helper.</param>
    /// <param name="current">Its status after the failure.</param>
    private void PostFailing(IHelperAgent helper, HelperStatus current)
    {
        if (current.ConsecutiveFailures < FailureThreshold || !this.Open(this.failingOpen, helper.Id))
        {
            return;
        }

        string error = current.LastError ?? string.Empty;
        string cut = error.Length > ErrorLimit ? error[..ErrorLimit] : error;
        feed.Post(new FeedMessage(
            FeedSourceType.HelperFailing,
            TargetOf(helper.Id),
            FeedActor.Huddle,
            true,
            new HelperFailingPayload(helper.Id.Value, helper.DisplayName, current.ConsecutiveFailures, cut)));
    }

    /// <summary>Marks an item open.</summary>
    /// <param name="open">The set.</param>
    /// <param name="id">The Helper.</param>
    /// <returns><see langword="true"/> when it was not open before.</returns>
    private bool Open(HashSet<string> open, HelperId id)
    {
        lock (this.gate)
        {
            return open.Add(id.Value);
        }
    }

    /// <summary>Resolves an item when one is open.</summary>
    /// <param name="open">The set.</param>
    /// <param name="id">The Helper.</param>
    /// <param name="type">The source type.</param>
    private void Close(HashSet<string> open, HelperId id, FeedSourceType type)
    {
        bool wasOpen;
        lock (this.gate)
        {
            wasOpen = open.Remove(id.Value);
        }

        if (wasOpen)
        {
            feed.Resolve(TargetOf(id), type);
        }
    }

    /// <summary>Runs a Feed call and logs a failure instead of letting it escape.</summary>
    /// <param name="action">The call.</param>
    private void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed is a report on the Helper and never part of its work: a failing Feed is logged and the Helper carries on.
            logger.LogWarning(ex, "A Helper's Feed item could not be posted or resolved.");
        }
    }
}
