namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Pure querying over Tasks: filtering, sorting and grouping, for Spec §12.5.</summary>
internal static class TaskQuery
{
    /// <summary>The final tiebreaker, always applied after any explicit sort keys: priority descending, then due date ascending with nulls last, then id ascending.</summary>
    private static readonly SortKey[] DefaultSortKeys =
    [
        new("priority", SortDirection.Descending),
        new("due_date", SortDirection.Ascending),
        new("id", SortDirection.Ascending),
    ];

    /// <summary>Declaration-order index of every <see cref="TaskState"/>, used to sort by status.</summary>
    private static readonly Dictionary<TaskState, int> StateOrder = TaskStates.All
        .Select((state, index) => (state, index))
        .ToDictionary(pair => pair.state, pair => pair.index);

    /// <summary>
    /// Returns the Tasks in <paramref name="scope"/> that match every dimension of <paramref name="filter"/>
    /// and <paramref name="search"/>. Values within one dimension are OR'd; dimensions are AND'd; an empty
    /// dimension matches everything.
    /// </summary>
    /// <param name="all">Every Task to consider.</param>
    /// <param name="scope">Whether to consider Active or Closed Tasks.</param>
    /// <param name="filter">The filter dimensions to apply.</param>
    /// <param name="search">An optional case-insensitive search over id and title.</param>
    /// <param name="humanName">The Human's Name, which <c>"@me"</c> resolves to.</param>
    public static IReadOnlyList<TaskItem> Filter(
        IReadOnlyList<TaskItem> all,
        ViewScope scope,
        TaskFilter filter,
        string? search,
        string humanName)
    {
        List<TaskItem> matches = [];
        foreach (TaskItem task in all)
        {
            if (MatchesScope(task, scope) &&
                MatchesTeams(task, filter.Teams) &&
                MatchesProjects(task, filter.Projects) &&
                MatchesAssignees(task, filter.Assignees, humanName) &&
                MatchesStates(task, filter.States) &&
                MatchesPriorities(task, filter.Priorities) &&
                MatchesSearch(task, search))
            {
                matches.Add(task);
            }
        }

        return matches;
    }

    /// <summary>Whether the Task's closed-ness matches the requested scope.</summary>
    private static bool MatchesScope(TaskItem task, ViewScope scope) =>
        scope == ViewScope.Closed ? task.Location.Closed : !task.Location.Closed;

    /// <summary>Whether the Task's Team is one of <paramref name="teams"/>, or the dimension is empty.</summary>
    private static bool MatchesTeams(TaskItem task, IReadOnlyList<string> teams) =>
        teams.Count == 0 ||
        teams.Any(team => string.Equals(team, task.Location.Team, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the Task's Team/Project matches one of <paramref name="projects"/>, or the dimension is empty.</summary>
    private static bool MatchesProjects(TaskItem task, IReadOnlyList<ProjectRef> projects) =>
        projects.Count == 0 || projects.Any(reference => MatchesProject(task, reference));

    /// <summary>Whether a single ProjectRef matches the Task's location. A null Project matches "no project".</summary>
    private static bool MatchesProject(TaskItem task, ProjectRef reference)
    {
        if (!string.Equals(reference.Team, task.Location.Team, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return reference.Project is null
            ? task.Location.Project is null
            : string.Equals(reference.Project, task.Location.Project, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the Task's Assignee matches one of <paramref name="assignees"/>, or the dimension is empty.</summary>
    private static bool MatchesAssignees(TaskItem task, IReadOnlyList<string> assignees, string humanName) =>
        assignees.Count == 0 || assignees.Any(assignee => MatchesAssignee(task, assignee, humanName));

    /// <summary>Whether a single assignee value matches the Task, resolving <c>"@me"</c> and <c>"@unassigned"</c>.</summary>
    private static bool MatchesAssignee(TaskItem task, string assignee, string humanName)
    {
        if (string.Equals(assignee, "@unassigned", StringComparison.OrdinalIgnoreCase))
        {
            return task.Assignee is null;
        }

        string resolved = string.Equals(assignee, "@me", StringComparison.OrdinalIgnoreCase) ? humanName : assignee;
        return string.Equals(resolved, task.Assignee, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the Task's Status is one of <paramref name="states"/>, or the dimension is empty.</summary>
    private static bool MatchesStates(TaskItem task, IReadOnlyList<TaskState> states) =>
        states.Count == 0 || states.Contains(task.Status);

    /// <summary>Whether the Task's Priority is one of <paramref name="priorities"/>, or the dimension is empty.</summary>
    private static bool MatchesPriorities(TaskItem task, IReadOnlyList<TaskPriority> priorities) =>
        priorities.Count == 0 || priorities.Contains(task.Priority);

    /// <summary>Whether the Task's id or title contains <paramref name="search"/>, case-insensitively.</summary>
    private static bool MatchesSearch(TaskItem task, string? search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        return task.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
            task.Title.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sorts <paramref name="items"/> by <paramref name="keys"/> in order, then always by the default tiebreaker
    /// (priority descending, due date ascending with nulls last, id ascending).
    /// </summary>
    /// <param name="items">The Tasks to sort.</param>
    /// <param name="keys">The explicit sort keys to apply first, in order.</param>
    public static IReadOnlyList<TaskItem> Sort(IReadOnlyList<TaskItem> items, IReadOnlyList<SortKey> keys)
    {
        List<Comparison<TaskItem>> comparisons = [];
        foreach (SortKey key in keys)
        {
            comparisons.Add(BuildComparison(key.Field, key.Direction));
        }

        foreach (SortKey key in DefaultSortKeys)
        {
            comparisons.Add(BuildComparison(key.Field, key.Direction));
        }

        Comparer<TaskItem> comparer = Comparer<TaskItem>.Create((left, right) =>
        {
            foreach (Comparison<TaskItem> comparison in comparisons)
            {
                int result = comparison(left, right);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        });

        return items.OrderBy(item => item, comparer).ToList();
    }

    /// <summary>
    /// The display label for <paramref name="task"/> under <paramref name="field"/>, shared by Board lanes and the
    /// List grid. Null Assignee is "Unassigned"; null Project is "No project"; Project grouped without Team is
    /// prefixed "Team / ".
    /// </summary>
    /// <param name="task">The Task to label.</param>
    /// <param name="field">The field being grouped by.</param>
    /// <param name="teamAlsoGrouped">Whether Team is also one of the grouping fields.</param>
    public static string GroupLabel(TaskItem task, TaskGroupField field, bool teamAlsoGrouped) => field switch
    {
        TaskGroupField.Team => task.Location.Team,
        TaskGroupField.Project => ProjectLabel(task, teamAlsoGrouped),
        TaskGroupField.Assignee => task.Assignee ?? "Unassigned",
        TaskGroupField.State => task.Status.ToWire(),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown grouping field."),
    };

    /// <summary>The Project group label: "No project" when unset, the bare name when Team is also grouped, else "Team / Project".</summary>
    private static string ProjectLabel(TaskItem task, bool teamAlsoGrouped)
    {
        if (task.Location.Project is null)
        {
            return "No project";
        }

        return teamAlsoGrouped ? task.Location.Project : $"{task.Location.Team} / {task.Location.Project}";
    }

    /// <summary>
    /// The sentinel a null grouping value maps to when grouping, so a real value that reads like the null group's
    /// label (a Teammate literally named "Unassigned", a Project literally named "No project") gets its own group
    /// instead of merging with it (corrections-B1 D7 item 11).
    /// </summary>
    private const string NullGroupSentinel = "\u0000";

    /// <summary>
    /// The raw grouping key for a Task under <paramref name="field"/>: the same shape as <see cref="GroupLabel"/>,
    /// except a null value maps to <see cref="NullGroupSentinel"/> instead of to display text. Grouping always uses
    /// this key, never the label, so the null group can never collide with a real value.
    /// </summary>
    private static string RawGroupKey(TaskItem task, TaskGroupField field, bool teamAlsoGrouped) => field switch
    {
        TaskGroupField.Team => task.Location.Team,
        TaskGroupField.Project => ProjectRawGroupKey(task, teamAlsoGrouped),
        TaskGroupField.Assignee => task.Assignee ?? NullGroupSentinel,
        TaskGroupField.State => task.Status.ToWire(),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown grouping field."),
    };

    /// <summary>The Project raw grouping key: <see cref="NullGroupSentinel"/> when unset, else the same text as <see cref="ProjectLabel"/>.</summary>
    private static string ProjectRawGroupKey(TaskItem task, bool teamAlsoGrouped)
    {
        if (task.Location.Project is null)
        {
            return NullGroupSentinel;
        }

        return teamAlsoGrouped ? task.Location.Project : $"{task.Location.Team} / {task.Location.Project}";
    }

    /// <summary>
    /// Nests <paramref name="sorted"/> by <paramref name="grouping"/>, one level per field. Groups are keyed by
    /// <see cref="RawGroupKey"/> (never by label), sorted ordinally by label, with the null group ("Unassigned" or
    /// "No project") last; empty groups are never produced.
    /// </summary>
    /// <param name="sorted">The already-sorted Tasks to group.</param>
    /// <param name="grouping">The fields to nest by, in order.</param>
    public static TaskGroupNode Group(IReadOnlyList<TaskItem> sorted, IReadOnlyList<TaskGroupField> grouping)
    {
        bool teamAlsoGrouped = grouping.Contains(TaskGroupField.Team);
        return BuildGroupLevel(sorted, grouping, 0, teamAlsoGrouped, null);
    }

    /// <summary>Builds one level of the group tree, recursing until <paramref name="grouping"/> is exhausted.</summary>
    private static TaskGroupNode BuildGroupLevel(
        IReadOnlyList<TaskItem> items, IReadOnlyList<TaskGroupField> grouping, int level, bool teamAlsoGrouped, string? label)
    {
        if (level >= grouping.Count)
        {
            return new TaskGroupNode(label, items.Count, [], items);
        }

        TaskGroupField field = grouping[level];
        List<TaskGroupNode> children = items
            .GroupBy(task => RawGroupKey(task, field, teamAlsoGrouped), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => string.Equals(group.Key, NullGroupSentinel, StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildGroupLevel(
                group.ToList(), grouping, level + 1, teamAlsoGrouped, GroupLabel(group.First(), field, teamAlsoGrouped)))
            .ToList();

        return new TaskGroupNode(label, items.Count, children, []);
    }

    /// <summary>Builds the comparison delegate for one sort field and direction.</summary>
    private static Comparison<TaskItem> BuildComparison(string field, SortDirection direction)
    {
        int sign = direction == SortDirection.Ascending ? 1 : -1;
        return field switch
        {
            "id" => (left, right) => sign * CompareIds(left.Id, right.Id),
            "title" => (left, right) => sign * string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase),
            "status" => (left, right) => sign * CompareStates(left.Status, right.Status),
            "priority" => (left, right) => sign * ((int)left.Priority).CompareTo((int)right.Priority),
            "assignee" => (left, right) => CompareNullableStrings(left.Assignee, right.Assignee, sign),
            "creator" => (left, right) => sign * string.Compare(left.Creator, right.Creator, StringComparison.OrdinalIgnoreCase),
            "team" => (left, right) => sign * string.Compare(left.Location.Team, right.Location.Team, StringComparison.OrdinalIgnoreCase),
            "project" => (left, right) => CompareNullableStrings(left.Location.Project, right.Location.Project, sign),
            "parent" => (left, right) => CompareNullableIds(left.Parent, right.Parent, sign),
            "start_date" => (left, right) => CompareNullableDates(left.StartDate, right.StartDate, sign),
            "due_date" => (left, right) => CompareNullableDates(left.DueDate, right.DueDate, sign),
            "created" => (left, right) => CompareNullableTimestamps(left.Created, right.Created, sign),
            "updated" => (left, right) => CompareNullableTimestamps(left.Updated, right.Updated, sign),
            "closed" => (left, right) => CompareNullableTimestamps(left.ClosedAt, right.ClosedAt, sign),
            "origin" => (left, right) => CompareNullableStrings(left.OriginRoomId, right.OriginRoomId, sign),
            _ => throw new NotSupportedException($"The field '{field}' cannot be sorted on."),
        };
    }

    /// <summary>Compares two Task ids: Prefix ordinally, case-insensitively, then Number numerically.</summary>
    private static int CompareIds(TaskId left, TaskId right)
    {
        int prefixResult = string.Compare(left.Prefix, right.Prefix, StringComparison.OrdinalIgnoreCase);
        return prefixResult != 0 ? prefixResult : left.Number.CompareTo(right.Number);
    }

    /// <summary>Compares two optional Task ids, with a null sorting last regardless of <paramref name="sign"/>.</summary>
    private static int CompareNullableIds(TaskId? left, TaskId? right, int sign)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        return sign * CompareIds(left.Value, right.Value);
    }

    /// <summary>Compares two Task states by declaration order.</summary>
    private static int CompareStates(TaskState left, TaskState right) =>
        StateOrder[left].CompareTo(StateOrder[right]);

    /// <summary>Compares two optional strings ordinally and case-insensitively, with a null sorting last regardless of <paramref name="sign"/>.</summary>
    private static int CompareNullableStrings(string? left, string? right, int sign)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        return sign * string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Compares two optional dates, with a null sorting last regardless of <paramref name="sign"/>.</summary>
    private static int CompareNullableDates(DateOnly? left, DateOnly? right, int sign)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        return sign * left.Value.CompareTo(right.Value);
    }

    /// <summary>Compares two optional timestamps, with a null sorting last regardless of <paramref name="sign"/>.</summary>
    private static int CompareNullableTimestamps(DateTimeOffset? left, DateTimeOffset? right, int sign)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        return sign * left.Value.CompareTo(right.Value);
    }

    /// <summary>
    /// The <c>#</c> Task picker's search (Spec §13.13.4). An empty query returns the most recently updated Active
    /// Tasks. Otherwise, matches an id starting with the query before a title containing it, both case-insensitive;
    /// Active Tasks rank before Closed ones, and ties break by <see cref="TaskItem.Updated"/> descending.
    /// </summary>
    /// <param name="all">Every Task to consider.</param>
    /// <param name="query">The typed query, with no leading <c>#</c>.</param>
    /// <param name="limit">The maximum number of Tasks to return.</param>
    public static IReadOnlyList<TaskItem> Suggest(IReadOnlyList<TaskItem> all, string query, int limit = 8)
    {
        if (string.IsNullOrEmpty(query))
        {
            return all
                .Where(task => !task.Location.Closed)
                .OrderByDescending(task => task.Updated)
                .Take(limit)
                .ToList();
        }

        return all
            .Select(task => (task, rank: SuggestRank(task, query)))
            .Where(pair => pair.rank is not null)
            .OrderBy(pair => pair.task.Location.Closed ? 1 : 0)
            .ThenBy(pair => pair.rank)
            .ThenByDescending(pair => pair.task.Updated)
            .Take(limit)
            .Select(pair => pair.task)
            .ToList();
    }

    /// <summary>0 for an id-prefix match, 1 for a title-contains match, or null when neither matches.</summary>
    private static int? SuggestRank(TaskItem task, string query)
    {
        if (task.Id.ToString().StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (task.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return null;
    }
}

/// <summary>One node of a Task grouping tree: a labelled group, its Task count, its child groups, and its leaf Tasks.</summary>
public sealed record TaskGroupNode(string? Label, int Count, IReadOnlyList<TaskGroupNode> Children, IReadOnlyList<TaskItem> Items);
