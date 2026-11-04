using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Resolves, once at startup, the Task Notifications whose condition ended while Huddle was stopped (Feed spec §6.10):
/// a Task file edited by hand, a Task accepted or moved on elsewhere. It only calls <c>Resolve</c>; nothing is posted.
/// </summary>
internal sealed class TaskFeedStartup(
    FeedStore feedStore,
    TaskStore tasks,
    IFeedPublisher feed,
    IRequestConfirmers confirmers,
    IHumanIdentity human,
    ILogger<TaskFeedStartup> logger) : BackgroundService
{
    /// <summary>The Notification types that have a resolution rule.</summary>
    private static readonly FeedSourceType[] ResolvableTypes =
    [
        FeedSourceType.TaskGateOn,
        FeedSourceType.TaskNeedsYou,
        FeedSourceType.TaskAssignedToYou,
        FeedSourceType.RequestAwaitsYou,
        FeedSourceType.GateDecisionDue,

        // A wake budget is in memory and starts fresh, so TaskFeedRules.Holds says a pause from before the restart is over.
        FeedSourceType.TaskWakesPaused,
    ];

    /// <summary>Resolves every unresolved Task Notification whose Task is gone or no longer meets its condition.</summary>
    /// <param name="stoppingToken">Stops the pass when the host shuts down.</param>
    /// <returns>A task that completes when every type has been checked.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            this.Reconcile();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The Feed could not reconcile its Task Notifications at startup.");
        }
    }

    /// <summary>Checks each unresolved Notification against the Task as it is now.</summary>
    internal void Reconcile()
    {
        string humanName = human.Name;
        foreach (FeedSourceType type in ResolvableTypes)
        {
            foreach (FeedItem item in feedStore.Unresolved(type))
            {
                TaskItem? task = TaskId.TryParse(item.TargetKey, out TaskId id) ? tasks.Get(id) : null;
                if (task is null || !TaskFeedRules.Holds(type, task, humanName, confirmers.TeamHasConfirmer, item.Payload))
                {
                    feed.Resolve(item.TargetKey, type);
                }
            }
        }
    }
}
