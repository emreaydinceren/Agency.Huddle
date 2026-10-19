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

    /// <summary>Room for a formatted <see cref="TaskId"/> on the stack: a prefix of up to eight letters or digits, a dash and a number; a longer id falls back to <c>ToString</c>.</summary>
    private const int IdBufferLength = 32;

    /// <summary>Declaration-order index of every <see cref="TaskState"/>, used to sort by status.</summary>
    private static readonly Dictionary<TaskState, int> StateOrder = TaskStates.All
        .Select((state, index) => (state, index))
        .ToDictionary(pair => pair.state, pair => pair.index);

    /// <summary>
    /// Whether <paramref name="task"/> is waiting on the Human: it is open (not Done, Cancelled, Duplicate or Rejected)
    /// and assigned to them, matched ignoring case. The Tasks page's "need you" count and the Top bar's pill both use it.
    /// </summary>
    /// <param name="task">The Task.</param>
    /// <param name="humanName">The Human's current Name.</param>
    public static bool NeedsYou(TaskItem task, string humanName) =>
        !task.Status.IsTerminal() && string.Equals(task.Assignee, humanName, StringComparison.OrdinalIgnoreCase);

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
    /// <param name="currentSprint">The Sprint <c>"@current"</c> resolves to, or <see langword="null"/> when there is none (it then matches nothing).</param>
    public static IReadOnlyList<TaskItem> Filter(
        IReadOnlyList<TaskItem> all,
        ViewScope scope,
        TaskFilter filter,
        string? search,
        string humanName,
        SprintId? currentSprint = null)
    {
        // Blockers are looked up across every Task, not just the ones in scope: a Done blocker sits in
        // the Closed folder, and must still read as terminal while the Active scope is showing.
        Dictionary<TaskId, TaskItem>? byId = filter.Blocked == BlockedFilter.All ? null : IndexById(all);
        List<TaskItem> matches = [];
        foreach (TaskItem task in all)
        {
            if (MatchesScope(task, scope) &&
                MatchesTeams(task, filter.Teams) &&
                MatchesProjects(task, filter.Projects) &&
                MatchesAssignees(task, filter.Assignees, humanName) &&
                MatchesStates(task, filter.States) &&
                MatchesPriorities(task, filter.Priorities) &&
                MatchesBlocked(task, filter.Blocked, byId) &&
                MatchesSprints(task, filter.Sprints, currentSprint) &&
                MatchesRequests(task, filter.RequestsOnly) &&
                MatchesSearch(task, search))
            {
                matches.Add(task);
            }
        }

        return matches;
    }

    /// <summary>Indexes <paramref name="all"/> by id in one pre-sized dictionary, with no lambda or resizing. A duplicate id throws, as <c>ToDictionary</c> did.</summary>
    private static Dictionary<TaskId, TaskItem> IndexById(IReadOnlyList<TaskItem> all)
    {
        Dictionary<TaskId, TaskItem> byId = new(all.Count);
        foreach (TaskItem task in all)
        {
            byId.Add(task.Id, task);
        }

        return byId;
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

    /// <summary>Whether the Task's Sprint matches one of <paramref name="sprints"/>, or the dimension is empty.</summary>
    private static bool MatchesSprints(TaskItem task, IReadOnlyList<string> sprints, SprintId? currentSprint) =>
        sprints.Count == 0 || sprints.Any(value => MatchesSprint(task, value, currentSprint));

    /// <summary>Whether a single Sprint value matches the Task, resolving <c>"@current"</c> and <c>"@none"</c>. An id that is not in the file still matches.</summary>
    private static bool MatchesSprint(TaskItem task, string value, SprintId? currentSprint)
    {
        if (string.Equals(value, "@none", StringComparison.OrdinalIgnoreCase))
        {
            return task.Sprint is null;
        }

        if (string.Equals(value, "@current", StringComparison.OrdinalIgnoreCase))
        {
            return currentSprint is not null && task.Sprint == currentSprint;
        }

        return SprintId.TryParse(value, out SprintId id) && task.Sprint == id;
    }

    /// <summary>Whether the Task's Status is one of <paramref name="states"/>, or the dimension is empty.</summary>
    private static bool MatchesStates(TaskItem task, IReadOnlyList<TaskState> states) =>
        states.Count == 0 || states.Contains(task.Status);

    /// <summary>Whether the Task's Priority is one of <paramref name="priorities"/>, or the dimension is empty.</summary>
    private static bool MatchesPriorities(TaskItem task, IReadOnlyList<TaskPriority> priorities) =>
        priorities.Count == 0 || priorities.Contains(task.Priority);

    /// <summary>Whether the Task is a Request, or <paramref name="requestsOnly"/> is off.</summary>
    private static bool MatchesRequests(TaskItem task, bool requestsOnly) =>
        !requestsOnly || RequestFields.TryRead(task, out _);

    /// <summary>Whether the Task's blocked-ness matches <paramref name="blocked"/>. A Task is blocked when at least one of its <see cref="TaskItem.BlockedBy"/> exists in <paramref name="byId"/> and is not terminal - the rule the detail panel's "Blocked by N open tasks" line uses.</summary>
    private static bool MatchesBlocked(TaskItem task, BlockedFilter blocked, Dictionary<TaskId, TaskItem>? byId)
    {
        if (blocked == BlockedFilter.All || byId is null)
        {
            return true;
        }

        bool isBlocked = false;
        foreach (TaskId id in task.BlockedBy)
        {
            if (byId.TryGetValue(id, out TaskItem? blocker) && !blocker.Status.IsTerminal())
            {
                isBlocked = true;
                break;
            }
        }

        return isBlocked == (blocked == BlockedFilter.Blocked);
    }

    /// <summary>Whether the Task's id or title contains <paramref name="search"/>, case-insensitively.</summary>
    private static bool MatchesSearch(TaskItem task, string? search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        return MatchesId(task.Id, search) || task.Title.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the formatted <paramref name="id"/> contains <paramref name="search"/>, case-insensitively, formatting into a stack buffer so a search over every Task allocates no string per Task.</summary>
    private static bool MatchesId(TaskId id, string search)
    {
        Span<char> buffer = stackalloc char[IdBufferLength];
        if (!id.TryFormat(buffer, out int written))
        {
            return id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        ReadOnlySpan<char> formatted = buffer[..written];
        return formatted.Contains(search, StringComparison.OrdinalIgnoreCase);
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
    /// <param name="sprintName">Resolves a Sprint id to its name, or null when it is not in the file. When omitted, a Sprint is labelled by its bare id.</param>
    public static string GroupLabel(
        TaskItem task, TaskGroupField field, bool teamAlsoGrouped, Func<SprintId, string?>? sprintName = null) => field switch
    {
        TaskGroupField.Team => task.Location.Team,
        TaskGroupField.Project => ProjectLabel(task, teamAlsoGrouped),
        TaskGroupField.Assignee => task.Assignee ?? "Unassigned",
        TaskGroupField.State => task.Status.ToWire(),
        TaskGroupField.Sprint => SprintLabel(task, sprintName),
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown grouping field."),
#pragma warning restore HAA0601
    };

    /// <summary>The Sprint group label: "No sprint" when unset, the name when resolved, "S5 (missing)" when the resolver does not know it, else the bare id.</summary>
    private static string SprintLabel(TaskItem task, Func<SprintId, string?>? sprintName)
    {
        if (task.Sprint is not { } sprint)
        {
            return "No sprint";
        }

        if (sprintName is null)
        {
            return sprint.ToString();
        }

        return sprintName(sprint) ?? $"{sprint} (missing)";
    }

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
        TaskGroupField.Sprint => task.Sprint?.ToString() ?? NullGroupSentinel,
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown grouping field."),
#pragma warning restore HAA0601
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
    /// <param name="sprintName">Resolves a Sprint id to its name for a Sprint grouping; see <see cref="GroupLabel"/>.</param>
    public static TaskGroupNode Group(
        IReadOnlyList<TaskItem> sorted, IReadOnlyList<TaskGroupField> grouping, Func<SprintId, string?>? sprintName = null)
    {
        bool teamAlsoGrouped = grouping.Contains(TaskGroupField.Team);
        return BuildGroupLevel(sorted, grouping, 0, teamAlsoGrouped, null, sprintName);
    }

    /// <summary>Builds one level of the group tree, recursing until <paramref name="grouping"/> is exhausted.</summary>
    private static TaskGroupNode BuildGroupLevel(
        IReadOnlyList<TaskItem> items,
        IReadOnlyList<TaskGroupField> grouping,
        int level,
        bool teamAlsoGrouped,
        string? label,
        Func<SprintId, string?>? sprintName)
    {
        if (level >= grouping.Count)
        {
            return new TaskGroupNode(label, items.Count, [], items);
        }

        TaskGroupField field = grouping[level];
        IOrderedEnumerable<IGrouping<string, TaskItem>> groups = items
            .GroupBy(task => RawGroupKey(task, field, teamAlsoGrouped), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => string.Equals(group.Key, NullGroupSentinel, StringComparison.Ordinal) ? 1 : 0);

        // A Sprint grouping is the one exception to ordering by label: newest first, by number (spec D-S13).
        groups = field == TaskGroupField.Sprint
            ? groups.ThenByDescending(group => group.First().Sprint?.Number ?? 0)
            : groups.ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        List<TaskGroupNode> children = groups
            .Select(group => BuildGroupLevel(
                group.ToList(),
                grouping,
                level + 1,
                teamAlsoGrouped,
                GroupLabel(group.First(), field, teamAlsoGrouped, sprintName),
                sprintName))
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
            "sprint" => (left, right) => CompareNullableSprints(left.Sprint, right.Sprint, sign),
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

    /// <summary>Compares two optional Sprints by number, with a null sorting last regardless of <paramref name="sign"/>.</summary>
    private static int CompareNullableSprints(SprintId? left, SprintId? right, int sign)
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

        return sign * left.Value.Number.CompareTo(right.Value.Number);
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
