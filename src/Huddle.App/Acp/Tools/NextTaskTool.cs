namespace Agency.Huddle.App.Acp.Tools;

using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Ordering;
using Agency.Huddle.App.Teams;

/// <summary>
/// Tells the calling Agent which of its own Tasks to start next (TaskOrder spec §5.1, C-1). The scope is the caller's
/// active Tasks in every Team, or in the one Team named by the optional <c>team</c> argument; the caller is resolved
/// as <c>assignee: "me"</c> is in <see cref="ListTasksTool"/>. <see cref="TaskOrder.Compute"/> does the ordering and
/// this tool only words the result, with every sentence taken from the <c>tool.nextTask.*</c> prompts. It writes nothing.
/// </summary>
internal sealed class NextTaskTool(
    TaskStore store,
    ITeamDirectory directory,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
{
    /// <summary>How many startable Tasks the answer names after the next one.</summary>
    private const int AfterLimit = 5;

    /// <inheritdoc />
    public string Name => "next_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.nextTask.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["team"] = new JsonObject { ["type"] = "string" },
        },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "team", out string? team, out string teamRefusal))
        {
            return teamRefusal;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        return Render(prompts, Compute(store, actor.Name, string.IsNullOrWhiteSpace(team) ? null : team));
    }

    /// <summary>
    /// Orders the active Tasks <paramref name="assignee"/> holds with <see cref="TaskOrder"/>.
    /// </summary>
    /// <param name="taskStore">Where the Tasks and their blockers are looked up.</param>
    /// <param name="assignee">The Name the Tasks are assigned to.</param>
    /// <param name="team">The one Team to order, or <see langword="null"/> for every Team.</param>
    internal static TaskOrderResult Compute(TaskStore taskStore, string assignee, string? team)
    {
        List<TaskItem> scope = taskStore.All
            .Where(task => !task.Location.Closed
                && string.Equals(task.Assignee, assignee, StringComparison.OrdinalIgnoreCase)
                && (team is null || string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return TaskOrder.Compute(scope, taskStore.Get);
    }

    /// <summary>The line <c>update_task</c> adds to a refused start: the caller's next startable Task, or that it has none.</summary>
    /// <param name="taskStore">Where the Tasks are looked up.</param>
    /// <param name="promptSource">Where the wording comes from.</param>
    /// <param name="assignee">The refused caller's Name.</param>
    internal static string RefusalLine(TaskStore taskStore, IPromptSource promptSource, string assignee)
    {
        TaskOrderEntry? next = Compute(taskStore, assignee, null).Next;
        return next is null
            ? promptSource.Render("tool.nextTask.refusalNone")
            : promptSource.Render("tool.nextTask.refusalNext", Values(("{{id}}", next.Task.Id.ToString()), ("{{title}}", next.Task.Title)));
    }

    /// <summary>Words an order as the Started, Next, After it and Waiting lines (§5.1).</summary>
    private static string Render(IPromptSource promptSource, TaskOrderResult order)
    {
        List<string> lines = [];
        TaskOrderEntry? next = order.Next;
        if (next is null)
        {
            lines.Add(promptSource.Render("tool.nextTask.nothing"));
        }

        foreach (TaskOrderEntry started in order.Entries.Where(static entry => entry.Group == TaskOrderGroup.Started))
        {
            lines.Add(promptSource.Render(
                "tool.nextTask.started",
                Values(("{{id}}", started.Task.Id.ToString()), ("{{title}}", started.Task.Title), ("{{status}}", started.Task.Status.ToWire()))));
        }

        if (next is not null)
        {
            lines.Add(promptSource.Render("tool.nextTask.next", Values(("{{entry}}", Describe(next.Task)))));
            List<string> after = order.Entries
                .Where(entry => entry.IsStartable && entry != next)
                .Take(AfterLimit)
                .Select(static entry => Describe(entry.Task))
                .ToList();
            if (after.Count > 0)
            {
                lines.Add(promptSource.Render("tool.nextTask.after", Values(("{{entries}}", string.Join("; ", after)))));
            }
        }

        List<string> reasons = WaitingReasons(promptSource, order);
        if (reasons.Count > 0)
        {
            lines.Add(promptSource.Render("tool.nextTask.waiting", Values(("{{reasons}}", string.Join(' ', reasons)))));
        }

        return string.Join('\n', lines);
    }

    /// <summary>One sentence for every Next-group entry that cannot start: blocked, in a cycle (named once) or in Backlog.</summary>
    private static List<string> WaitingReasons(IPromptSource promptSource, TaskOrderResult order)
    {
        List<string> reasons = [];
        HashSet<string> cyclesSeen = new(StringComparer.Ordinal);
        foreach (TaskOrderEntry entry in order.Entries.Where(static entry => entry.Group == TaskOrderGroup.Next && !entry.IsStartable))
        {
            string id = entry.Task.Id.ToString();
            if (entry.Cycle.Count > 0)
            {
                string key = string.Join(',', entry.Cycle.Select(static cycleId => cycleId.ToString()).Order(StringComparer.Ordinal));
                if (cyclesSeen.Add(key))
                {
                    reasons.Add(promptSource.Render("tool.nextTask.waitCycle", Values(("{{ids}}", JoinIds(entry.Cycle)))));
                }
            }
            else if (entry.BlockedBy.Count > 0)
            {
                string blockers = string.Join(", ", entry.BlockedBy.Select(DescribeBlocker));
                reasons.Add(promptSource.Render("tool.nextTask.waitBlocked", Values(("{{id}}", id), ("{{title}}", entry.Task.Title), ("{{blockers}}", blockers))));
            }
            else
            {
                reasons.Add(promptSource.Render("tool.nextTask.waitBacklog", Values(("{{id}}", id), ("{{title}}", entry.Task.Title), ("{{blocks}}", entry.Blocks?.ToString() ?? string.Empty))));
            }
        }

        return reasons;
    }

    /// <summary>A Task as <c>PLAT-0002 "Add export" (To Do, rank 2)</c>; the rank is left out when the Task has none.</summary>
    private static string Describe(TaskItem task)
    {
        string rank = task.Rank is { } value ? string.Create(CultureInfo.InvariantCulture, $", rank {value}") : string.Empty;
        return $"{task.Id} \"{task.Title}\" ({task.Status.ToWire()}{rank})";
    }

    /// <summary>A blocker as <c>PLAT-0005 (To Do, Kai)</c>; the assignee is left out when it has none.</summary>
    private static string DescribeBlocker(OpenBlocker blocker)
    {
        string assignee = string.IsNullOrEmpty(blocker.Assignee) ? string.Empty : $", {blocker.Assignee}";
        return $"{blocker.Id} ({blocker.Status.ToWire()}{assignee})";
    }

    /// <summary>Joins ids as <c>A and B</c> or <c>A, B and C</c>.</summary>
    private static string JoinIds(IReadOnlyList<TaskId> ids)
    {
        List<string> texts = ids.Select(static id => id.ToString()).ToList();
        if (texts.Count == 1)
        {
            return texts[0];
        }

        string head = string.Join(", ", texts.Take(texts.Count - 1));
        return string.Concat(head, " and ", texts[^1]);
    }

    /// <summary>Builds the placeholder values of one prompt.</summary>
    private static Dictionary<string, string> Values(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
}
