using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Puts Tasks into a Sprint, for the Human's approval of a proposal (<see cref="SprintProposalService"/>) and for the approval Huddle gives
/// itself when every Task runs under the level Run inside the Team (ADR-0088). One place holds the create-and-add rules, so the two paths
/// cannot drift apart. It has no chat dependency, so the manager's <c>propose_sprint</c> tool can hold it without a cycle.
/// </summary>
/// <param name="sprints">Creates the Sprint.</param>
/// <param name="tasks">Sets the Sprint on a Task and writes the Change log entry.</param>
/// <param name="taskStore">Finds a Task by id.</param>
/// <param name="levels">Resolves a Task's Needs You level; <see langword="null"/> means Ask me everywhere, so nothing is approved without the Human.</param>
/// <param name="feed">Receives the one Feed item of an automatic approval; <see langword="null"/> posts nothing.</param>
internal sealed class SprintApprover(SprintStore sprints, TaskService tasks, TaskStore taskStore, NeedsYouLevels? levels = null, IFeedPublisher? feed = null)
{
    private const string MissingReason = "no longer exists";
    private const string DoneReason = "already Done";
    private const string NotUpdatedReason = "could not be updated";

    /// <summary>Which Tasks now carry a Sprint and which were skipped, and why.</summary>
    /// <param name="Applied">The ids, as the manager wrote them, of the Tasks that now carry the Sprint.</param>
    /// <param name="Skipped">The Tasks that were left out.</param>
    internal sealed record Additions(IReadOnlyList<string> Applied, IReadOnlyList<SkippedTask> Skipped);

    /// <summary>The <c>Added: ...</c> and <c>Skipped: ...</c> sentences that close the text about an approval.</summary>
    /// <param name="additions">What was added and skipped.</param>
    public static string Tail(Additions additions)
    {
        ArgumentNullException.ThrowIfNull(additions);

        string added = additions.Applied.Count == 0 ? "none" : string.Join(", ", additions.Applied);
        string text = $"Added: {added}.";
        if (additions.Skipped.Count > 0)
        {
            string skippedList = string.Join(", ", additions.Skipped.Select(static each => string.Concat(each.Id, " (", each.Reason, ")")));
            text = string.Concat(text, " Skipped: ", skippedList, ".");
        }

        return text;
    }

    /// <summary>Sets the Sprint on each named Task that still exists and is not Done.</summary>
    /// <param name="sprint">The Sprint to set.</param>
    /// <param name="idTexts">The Task ids as the manager wrote them.</param>
    /// <param name="actor">The actor recorded in the Change log: the Human for an approval, Huddle for an automatic one.</param>
    /// <param name="proceededUnderLevel">
    /// <see langword="true"/> for an automatic approval: each Task added then also gets Huddle's one <see cref="NeedsYouRelease.SprintSummaryFor"/> entry.
    /// </param>
    public Additions AddTasks(Sprint sprint, IReadOnlyList<string> idTexts, TaskActor actor, bool proceededUnderLevel = false)
    {
        ArgumentNullException.ThrowIfNull(sprint);
        ArgumentNullException.ThrowIfNull(idTexts);
        ArgumentNullException.ThrowIfNull(actor);

        List<string> applied = [];
        List<SkippedTask> skipped = [];
        foreach (string idText in idTexts)
        {
            string? reason = this.AddToSprint(idText, sprint, actor, proceededUnderLevel);
            if (reason is null)
            {
                applied.Add(idText);
            }
            else
            {
                skipped.Add(new SkippedTask(idText, reason));
            }
        }

        return new Additions(applied, skipped);
    }

    /// <summary>
    /// Approves a proposed Sprint at once when every Task it names runs under the level Run inside the Team: creates the Sprint (an existing
    /// name is accepted), sets it on each Task that still exists and is not Done as Huddle, writes one Change log entry per Task added and
    /// posts one Feed item.
    /// </summary>
    /// <param name="name">The Sprint's name.</param>
    /// <param name="found">The Tasks the proposal names.</param>
    /// <returns>The text for the manager, or <see langword="null"/> when some Task is under Ask me (or no levels are wired), so the Human decides.</returns>
    public string? ApproveWithoutHuman(string name, IReadOnlyList<TaskItem> found)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(found);

        if (levels is not { } source || found.Count == 0 || found.Any(task => source.ForTask(task).Level != NeedsYouLevel.RunInsideTheTeam))
        {
            return null;
        }

        Sprint sprint;
        switch (sprints.Create(name))
        {
            case SprintCreateResult.Created created:
                sprint = created.Sprint;
                break;
            case SprintCreateResult.Existing existing:
                sprint = existing.Sprint;
                break;
            case SprintCreateResult.Refused refused:
                return $"The Sprint '{name}' could not be created: {refused.Problem}";
            default:
                throw new InvalidOperationException("Unknown Sprint create result.");
        }

        Additions additions = this.AddTasks(sprint, [.. found.Select(static each => each.Id.ToString())], TaskActors.Huddle, proceededUnderLevel: true);
        if (additions.Applied.Count > 0)
        {
            feed?.Post(new FeedMessage(
                FeedSourceType.SprintProceeded,
                sprint.Id.ToString(),
                FeedActor.Huddle,
                false,
                new SprintProceededPayload(sprint.Name, found[0].Location.Team, source.ForTask(found[0]).Scope.ToDisplay(), additions.Applied.Count)));
        }

        return $"Sprint '{sprint.Name}' approved: every Task runs inside the Team, so it needed no Human approval. {Tail(additions)}";
    }

    /// <summary>Sets the Sprint on one Task.</summary>
    private string? AddToSprint(string idText, Sprint sprint, TaskActor actor, bool proceededUnderLevel)
    {
        if (!TaskId.TryParse(idText, out TaskId id) || taskStore.Get(id) is not { } task)
        {
            return MissingReason;
        }

        if (task.Status == TaskState.Done)
        {
            return DoneReason;
        }

        TaskPatch patch = new() { Sprint = Optional<SprintId?>.Set(sprint.Id) };
        TaskResult result = tasks.Update(id, patch, null, actor);
        if (proceededUnderLevel && result is TaskResult.Saved && levels is { } source)
        {
            _ = tasks.RecordHuddleStep(id, NeedsYouRelease.SprintSummaryFor(source.ForTask(task).Scope, sprint.Name));
        }

        return result switch
        {
            TaskResult.Saved or TaskResult.Unchanged => null,
            TaskResult.NotFound => MissingReason,
            TaskResult.GateRefused gate => gate.Message,
            TaskResult.Refused refused => string.Join("; ", refused.Problems),
            _ => NotUpdatedReason,
        };
    }
}
