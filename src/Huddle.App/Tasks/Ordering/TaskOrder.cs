using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>
/// Puts a set of Tasks in the order they should be worked: what is started, then what to do next with its blockers
/// listed ahead of what they block, then the Backlog. A pure function of its inputs (Spec: TaskOrder §4).
/// </summary>
internal static class TaskOrder
{
    /// <summary>Orders <paramref name="scope"/>; terminal Tasks in it are ignored and duplicate ids collapse to the first.</summary>
    /// <param name="scope">The Tasks to order.</param>
    /// <param name="lookup">Finds any Task by id, in or out of <paramref name="scope"/>, closed ones included; null when it does not exist.</param>
    /// <param name="groupRule">Says which Projects admit one In Progress Task at a time (ADR-0103); <see langword="null"/> means none does.</param>
    public static TaskOrderResult Compute(IReadOnlyList<TaskItem> scope, Func<TaskId, TaskItem?> lookup, ITaskGroupRule? groupRule = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lookup);

        Walk walk = new(scope, lookup);
        List<TaskOrderEntry> entries = [];
        foreach (TaskItem task in walk.Ordered.Where(task => IsStarted(task.Status)))
        {
            entries.Add(walk.Entry(task, TaskOrderGroup.Started, blocks: null));
        }

        List<TaskOrderEntry> next = walk.Place(TaskOrderGroup.Next);
        entries.AddRange(groupRule is null ? next : HoldBehindGroupRule(next, groupRule));
        entries.AddRange(walk.Place(TaskOrderGroup.Backlog));
        return new TaskOrderResult(entries);
    }

    /// <summary>
    /// Applies "one Task In Progress per Project": in a group whose rule is on, the group's In Progress Task (or else its first
    /// startable entry) is the only one that may start; each later startable entry keeps its place, stops being startable and
    /// names that Task in <see cref="TaskOrderEntry.WaitsFor"/>.
    /// </summary>
    /// <param name="next">The Next group in placement order.</param>
    /// <param name="groupRule">The rule of each group.</param>
    private static List<TaskOrderEntry> HoldBehindGroupRule(List<TaskOrderEntry> next, ITaskGroupRule groupRule)
    {
        Dictionary<string, GroupSlot> slots = new(StringComparer.Ordinal);
        List<TaskOrderEntry> result = new(next.Count);
        foreach (TaskOrderEntry entry in next)
        {
            if (!entry.IsStartable)
            {
                result.Add(entry);
                continue;
            }

            TaskLocation location = entry.Task.Location;
            string key = $"{location.Team.ToUpperInvariant()}|{location.Project?.ToUpperInvariant()}";
            if (!slots.TryGetValue(key, out GroupSlot? slot))
            {
                slot = groupRule.IsOn(location.Team, location.Project)
                    ? new GroupSlot(true, groupRule.InProgressTask(location.Team, location.Project)?.Id)
                    : new GroupSlot(false, null);
                slots[key] = slot;
            }

            if (!slot.IsOn || slot.Holder is null)
            {
                slot.Holder = entry.Task.Id;
                result.Add(entry);
            }
            else
            {
                result.Add(entry with { IsStartable = false, WaitsFor = slot.Holder });
            }
        }

        return result;
    }

    /// <summary>One group during <see cref="HoldBehindGroupRule"/>: whether its rule is on and the Task that holds it.</summary>
    private sealed class GroupSlot(bool isOn, TaskId? holder)
    {
        /// <summary>Whether the group admits one In Progress Task at a time.</summary>
        public bool IsOn { get; } = isOn;

        /// <summary>The Task In Progress in the group, or else the first startable one placed.</summary>
        public TaskId? Holder { get; set; } = holder;
    }

    /// <summary>In Progress and Needs You are the started states.</summary>
    private static bool IsStarted(TaskState status) => status is TaskState.InProgress or TaskState.NeedsYou;

    /// <summary>The state of one computation: the live scope, each Task's open blockers, its cycle, and what is placed.</summary>
    private sealed class Walk
    {
        private readonly Dictionary<TaskId, TaskItem> inScope = [];
        private readonly Dictionary<TaskId, IReadOnlyList<OpenBlocker>> open = [];
        private readonly Dictionary<TaskId, IReadOnlyList<TaskId>> cycles = [];
        private readonly HashSet<TaskId> placed = [];

        /// <summary>Starts a walk over the live Tasks of <paramref name="scope"/> and finds every blocker cycle.</summary>
        /// <param name="scope">The Tasks to order.</param>
        /// <param name="lookup">Finds any Task by id.</param>
        public Walk(IReadOnlyList<TaskItem> scope, Func<TaskId, TaskItem?> lookup)
        {
            foreach (TaskItem task in scope.Where(task => !task.Status.IsTerminal()))
            {
                _ = this.inScope.TryAdd(task.Id, task);
            }

            List<TaskItem> ordered = [.. this.inScope.Values];
            ordered.Sort(TaskQuery.DefaultOrder);
            this.Ordered = ordered;

            foreach (TaskItem task in ordered)
            {
                this.open[task.Id] = TaskBlockers.Open(task, lookup);
            }

            this.FindCycles();
        }

        /// <summary>The live Tasks in <see cref="TaskQuery.DefaultOrder"/>.</summary>
        public IReadOnlyList<TaskItem> Ordered { get; }

        /// <summary>Builds the entry for <paramref name="task"/> in <paramref name="group"/>.</summary>
        /// <param name="task">The Task.</param>
        /// <param name="group">Where it is listed.</param>
        /// <param name="blocks">The Task it was listed ahead of, or null.</param>
        public TaskOrderEntry Entry(TaskItem task, TaskOrderGroup group, TaskId? blocks)
        {
            IReadOnlyList<OpenBlocker> blockedBy = [.. this.open[task.Id].OrderBy(static blocker => blocker.Id.ToString(), StringComparer.Ordinal)];
            IReadOnlyList<TaskId> cycle = this.cycles.GetValueOrDefault(task.Id) ?? [];
            bool startable = task.Status == TaskState.ToDo && blockedBy.Count == 0 && cycle.Count == 0;
            return new TaskOrderEntry(task, group, startable, blocks, blockedBy, cycle);
        }

        /// <summary>Places the group by a depth-first post-order walk seeded in comparer order.</summary>
        /// <param name="group">Next or Backlog.</param>
        public List<TaskOrderEntry> Place(TaskOrderGroup group)
        {
            List<TaskOrderEntry> entries = [];
            TaskState seedState = group == TaskOrderGroup.Next ? TaskState.ToDo : TaskState.Backlog;
            foreach (TaskItem seed in this.Ordered.Where(task => task.Status == seedState))
            {
                this.Visit(seed, null, group, entries);
            }

            return entries;
        }

        /// <summary>The in-scope open blockers of <paramref name="task"/> in comparer order.</summary>
        private List<TaskItem> ScopedBlockers(TaskItem task)
        {
            List<TaskItem> blockers = [];
            foreach (OpenBlocker blocker in this.open[task.Id])
            {
                if (this.inScope.TryGetValue(blocker.Id, out TaskItem? found))
                {
                    blockers.Add(found);
                }
            }

            blockers.Sort(TaskQuery.DefaultOrder);
            return blockers;
        }

        private void Visit(TaskItem task, TaskId? blocks, TaskOrderGroup group, List<TaskOrderEntry> entries)
        {
            if (!this.placed.Add(task.Id))
            {
                return;
            }

            foreach (TaskItem blocker in this.ScopedBlockers(task))
            {
                this.Visit(blocker, task.Id, group, entries);
            }

            if (Belongs(task.Status, group))
            {
                entries.Add(this.Entry(task, group, blocks));
            }
        }

        /// <summary>Whether a Task in <paramref name="status"/> is listed in <paramref name="group"/>: Next takes To Do and the Backlog blockers reached from it, Backlog takes the Backlog.</summary>
        private static bool Belongs(TaskState status, TaskOrderGroup group) => group == TaskOrderGroup.Next
            ? status is TaskState.ToDo or TaskState.Backlog
            : status == TaskState.Backlog;

        private void FindCycles()
        {
            Dictionary<TaskId, bool> finished = [];
            List<TaskItem> stack = [];
            foreach (TaskItem task in this.Ordered)
            {
                this.Probe(task, finished, stack);
            }
        }

        private void Probe(TaskItem task, Dictionary<TaskId, bool> finished, List<TaskItem> stack)
        {
            if (finished.TryGetValue(task.Id, out bool done))
            {
                if (!done)
                {
                    this.Record(stack, task);
                }

                return;
            }

            finished[task.Id] = false;
            stack.Add(task);
            foreach (TaskItem blocker in this.ScopedBlockers(task))
            {
                this.Probe(blocker, finished, stack);
            }

            stack.RemoveAt(stack.Count - 1);
            finished[task.Id] = true;
        }

        /// <summary>Gives every Task on the stack from <paramref name="start"/> up the same cycle list, unless it already has one.</summary>
        private void Record(List<TaskItem> stack, TaskItem start)
        {
            int from = stack.FindIndex(item => item.Id == start.Id);
            List<TaskId> ids = [.. stack.Skip(from).Select(item => item.Id)];
            foreach (TaskId id in ids)
            {
                _ = this.cycles.TryAdd(id, ids);
            }
        }
    }
}
