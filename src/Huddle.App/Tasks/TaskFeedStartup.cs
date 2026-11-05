using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Resolves, once at startup, the Task Notifications whose condition ended while Huddle was stopped (Feed spec §6.10):
/// a Task file edited by hand, a Task accepted or moved on elsewhere. It only calls <c>Resolve</c>; nothing is posted and no Agent is woken.
/// </summary>
internal sealed class TaskFeedStartup(
    FeedStore feedStore,
    TaskStore tasks,
    IFeedPublisher feed,
    IRequestConfirmers confirmers,
    IHumanIdentity human,
    ILogger<TaskFeedStartup> logger,
    IWakePauses? pauses = null) : BackgroundService
{
    /// <summary>The Notification types that have a resolution rule.</summary>
    private static readonly FeedSourceType[] ResolvableTypes =
    [
        FeedSourceType.TaskGateOn,
        FeedSourceType.TaskNeedsYou,
        FeedSourceType.TaskAssignedToYou,
        FeedSourceType.RequestAwaitsYou,
        FeedSourceType.GateDecisionDue,

        // The hand-off and the wake budget are in memory and start fresh, but the Notification is saved and stays while the Task is open and its Agent exists:
        // it is how the Human learns the hand-off was lost. Reconcile hands each one that stays back to the wake service, which delivers nothing.
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
        Func<string, bool>? agentExists = pauses is null ? null : pauses.AgentExists;
        foreach (FeedSourceType type in ResolvableTypes)
        {
            foreach (FeedItem item in feedStore.Unresolved(type))
            {
                TaskItem? task = TaskId.TryParse(item.TargetKey, out TaskId id) ? tasks.Get(id) : null;
                if (task is null || !TaskFeedRules.Holds(type, task, humanName, confirmers.TeamHasConfirmer, item.Payload, agentExists))
                {
                    feed.Resolve(item.TargetKey, type);
                }
                else if (type == FeedSourceType.TaskWakesPaused)
                {
                    // Only remembers the pause is open, so a later Human change or Allow more closes it. Nothing is delivered and no Agent wakes.
                    pauses?.RestorePaused(id);
                }
            }
        }
    }
}
