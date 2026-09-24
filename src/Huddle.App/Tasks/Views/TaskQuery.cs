namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Pure querying over Tasks: filtering, and (later) sorting and grouping, for Spec §12.5.</summary>
internal static class TaskQuery
{
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
}
