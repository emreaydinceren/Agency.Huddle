using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.App.Tasks;

/// <summary>What one Task change means for the Feed (Feed spec §6.5.1). Pure: no I/O, no clock, no queue.</summary>
internal static class TaskFeedRules
{
    /// <summary>The items to post and the Notifications to resolve for <paramref name="change"/>.</summary>
    /// <param name="change">The change that was just written.</param>
    /// <param name="humanName">The Human's current Name.</param>
    /// <param name="teamHasConfirmer">Whether a Team has a member who can call <c>confirm_request</c>.</param>
    /// <param name="proofOf">Evaluates a Task's Proof, for the count on a Needs You item.</param>
    /// <returns>The effects, which may be empty.</returns>
    public static FeedEffects For(TaskChange change, string humanName, Func<string, bool> teamHasConfirmer, Func<TaskItem, Proof> proofOf)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(humanName);
        ArgumentNullException.ThrowIfNull(teamHasConfirmer);
        ArgumentNullException.ThrowIfNull(proofOf);

        TaskItem? before = change.Before;
        TaskItem after = change.After;
        string id = after.Id.ToString();
        string team = after.Location.Team;
        bool byHuman = change.Actor.Kind == TaskActorKind.Human;
        FeedActor actor = ActorOf(change.Actor);
        List<FeedMessage> posts = [];
        List<FeedResolution> resolutions = [];

        if (before is null)
        {
            posts.Add(new(FeedSourceType.TaskCreated, id, actor, false, new TaskCreatedPayload(id, after.Title, team, after.Assignee)));
        }

        if (after.IsGated && before?.IsGated != true && !byHuman)
        {
            posts.Add(new(FeedSourceType.TaskGateOn, id, actor, false, new TaskGatePayload(id, after.Title, team, after.Action)));
        }

        bool waits = Waits(after);
        bool waited = before is not null && Waits(before);
        if (waits && !waited && !byHuman)
        {
            Proof proof = proofOf(after);
            posts.Add(new(FeedSourceType.TaskNeedsYou, id, actor, true, new TaskNeedsYouPayload(id, after.Title, team, proof.MetCount, proof.Total)));
        }

        if (waited && !waits)
        {
            resolutions.Add(new(id, FeedSourceType.TaskNeedsYou));
        }

        AddHumanAsks(change, humanName, teamHasConfirmer, actor, posts, resolutions);

        if (before is not null && (before.Status != after.Status || after.Location.Closed))
        {
            resolutions.Add(new(id, FeedSourceType.GateDecisionDue));
        }

        return new FeedEffects(posts, resolutions);
    }

    /// <summary>Adds the Notifications about a Task assigned to the Human and about a Request only the Human can confirm.</summary>
    /// <param name="change">The change.</param>
    /// <param name="humanName">The Human's current Name.</param>
    /// <param name="teamHasConfirmer">Whether a Team has a member who can call <c>confirm_request</c>.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="posts">Receives the items to post.</param>
    /// <param name="resolutions">Receives the Notifications to resolve.</param>
    private static void AddHumanAsks(TaskChange change, string humanName, Func<string, bool> teamHasConfirmer, FeedActor actor, List<FeedMessage> posts, List<FeedResolution> resolutions)
    {
        TaskItem? before = change.Before;
        TaskItem after = change.After;
        string id = after.Id.ToString();
        bool byHuman = change.Actor.Kind == TaskActorKind.Human;

        bool assigned = TaskQuery.NeedsYou(after, humanName);
        bool wasAssigned = before is not null && TaskQuery.NeedsYou(before, humanName);
        if (assigned && !wasAssigned && !byHuman)
        {
            posts.Add(new(FeedSourceType.TaskAssignedToYou, id, actor, true, new TaskAssignedPayload(id, after.Title, after.Location.Team, before?.Assignee)));
        }

        if (wasAssigned && !assigned)
        {
            resolutions.Add(new(id, FeedSourceType.TaskAssignedToYou));
        }

        bool awaits = AwaitsConfirmation(after, teamHasConfirmer, out RequestFields? fields);
        bool awaited = before is not null && AwaitsConfirmation(before, teamHasConfirmer, out _);
        if (awaits && !awaited && fields is not null)
        {
            posts.Add(new(FeedSourceType.RequestAwaitsYou, id, actor, true, new RequestAwaitsPayload(id, after.Title, after.Location.Team, fields.FromTeam, KindWord(fields.Kind))));
        }

        if (awaited && !awaits)
        {
            resolutions.Add(new(id, FeedSourceType.RequestAwaitsYou));
        }
    }

    /// <summary>Whether the Notification of <paramref name="type"/> about <paramref name="task"/> still holds; used by the startup reconciler.</summary>
    /// <param name="type">The Notification's source type.</param>
    /// <param name="task">The Task now.</param>
    /// <param name="humanName">The Human's current Name.</param>
    /// <param name="teamHasConfirmer">Whether a Team has a member who can call <c>confirm_request</c>.</param>
    /// <param name="payload">The payload the Notification was written with.</param>
    /// <returns><see langword="true"/> when the Notification should stay unresolved.</returns>
    public static bool Holds(FeedSourceType type, TaskItem task, string humanName, Func<string, bool> teamHasConfirmer, FeedPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(humanName);
        ArgumentNullException.ThrowIfNull(teamHasConfirmer);

        return type switch
        {
            FeedSourceType.TaskNeedsYou => Waits(task),
            FeedSourceType.TaskAssignedToYou => TaskQuery.NeedsYou(task, humanName),
            FeedSourceType.RequestAwaitsYou => AwaitsConfirmation(task, teamHasConfirmer, out _),
            FeedSourceType.GateDecisionDue => !task.Location.Closed
                && payload is GateDecisionPayload gate
                && string.Equals(task.Status.ToWire(), gate.FromStatus, StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary>The Feed actor for a Task actor.</summary>
    /// <param name="actor">Who changed the Task.</param>
    private static FeedActor ActorOf(TaskActor actor) => new(
        actor.Kind switch
        {
            TaskActorKind.Human => FeedActorKind.Human,
            TaskActorKind.Agent => FeedActorKind.Agent,
            _ => FeedActorKind.OutsideHuddle,
        },
        actor.Name);

    /// <summary>The rule <c>NeedsYouTaskSource</c> lists by: gated, in Needs You, acceptance not valid.</summary>
    /// <param name="task">The Task.</param>
    private static bool Waits(TaskItem task) =>
        task.IsGated && task.Status == TaskState.NeedsYou && Acceptance.Of(task).State != AcceptanceState.Valid;

    /// <summary>A Request in Done that is neither declined nor confirmed and that no Agent of the requesting Team can confirm.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="teamHasConfirmer">Whether a Team has a member who can call <c>confirm_request</c>.</param>
    /// <param name="fields">The Request fields when the Task is a Request.</param>
    private static bool AwaitsConfirmation(TaskItem task, Func<string, bool> teamHasConfirmer, out RequestFields? fields)
    {
        if (!RequestFields.TryRead(task, out fields) || fields is null)
        {
            return false;
        }

        return task.Status == TaskState.Done
            && !task.Location.Closed
            && fields.Triage != TriageState.Declined
            && fields.ConfirmedBy.Length == 0
            && !teamHasConfirmer(fields.FromTeam);
    }

    /// <summary>The lower-case word for a Request kind.</summary>
    /// <param name="kind">The kind.</param>
    private static string KindWord(RequestKind kind) => kind switch
    {
        RequestKind.Bug => "bug",
        RequestKind.Feature => "feature",
        RequestKind.Feedback => "feedback",
        _ => "question",
    };
}
