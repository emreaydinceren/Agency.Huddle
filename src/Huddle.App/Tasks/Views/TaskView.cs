using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The layout a View renders as: a data grid or a Kanban board.</summary>
public enum ViewKind
{
    /// <summary>A data grid, one row per Task.</summary>
    List,

    /// <summary>A Kanban board, one column per <see cref="BoardColumn"/>.</summary>
    Board,
}

/// <summary>Which Tasks a View considers: the open ones, or the closed ones.</summary>
public enum ViewScope
{
    /// <summary>Tasks that are not closed.</summary>
    Active,

    /// <summary>Tasks that are closed.</summary>
    Closed,
}

/// <summary>A field a View can group Tasks by. Board views cannot group by <see cref="TaskGroupField.State"/>.</summary>
public enum TaskGroupField
{
    /// <summary>Group by <see cref="TaskLocation.Team"/>.</summary>
    Team,

    /// <summary>Group by <see cref="TaskLocation.Project"/>.</summary>
    Project,

    /// <summary>Group by <see cref="TaskItem.Assignee"/>.</summary>
    Assignee,

    /// <summary>Group by <see cref="TaskItem.Status"/>. Invalid on a Board.</summary>
    State,
}

/// <summary>The direction a sort key is applied in.</summary>
public enum SortDirection
{
    /// <summary>Lowest to highest.</summary>
    Ascending,

    /// <summary>Highest to lowest.</summary>
    Descending,
}

/// <summary>Whether a View keeps Tasks held up by an open blocker (<see cref="TaskItem.BlockedBy"/>), the ones that are not, or both.</summary>
public enum BlockedFilter
{
    /// <summary>Blocked and unblocked Tasks alike.</summary>
    All,

    /// <summary>Only Tasks with at least one blocker that exists and is not terminal.</summary>
    Blocked,

    /// <summary>Only Tasks with no blockers, only terminal blockers, or only blockers that no longer exist.</summary>
    Unblocked,
}

/// <summary>A saved arrangement of Tasks: a filter, a grouping, a sort, and either a List's columns or a Board's.</summary>
public sealed record TaskView
{
    /// <summary>The View's unique identifier. A Guid "N" string; built-ins use fixed ids.</summary>
    public required string Id { get; init; }

    /// <summary>The View's display name.</summary>
    public required string Name { get; init; }

    /// <summary>An optional description of what the View is for.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the View renders as a List or a Board.</summary>
    public required ViewKind Kind { get; init; }

    /// <summary>Whether the View shows open or closed Tasks.</summary>
    public ViewScope Scope { get; init; } = ViewScope.Active;

    /// <summary>The field keys (see <c>ViewFieldKeys</c>) shown, in order. <c>id</c> and <c>title</c> are implicit.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>The filter applied before sorting and grouping.</summary>
    public TaskFilter Filter { get; init; } = new();

    /// <summary>The fields Tasks are nested by. Board swimlanes only; the List uses its own grid grouping.</summary>
    public IReadOnlyList<TaskGroupField> Grouping { get; init; } = [];

    /// <summary>The sort keys, applied in order.</summary>
    public IReadOnlyList<SortKey> Sort { get; init; } = [];

    /// <summary>The Board's columns. Empty for a List.</summary>
    public IReadOnlyList<BoardColumn> Columns { get; init; } = [];

    /// <summary>Whether the List uses compact rows. <see langword="null"/> (the default, not written to <c>views.json</c>, so a View that never set it stays byte-identical) reads as dense.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Dense { get; init; }

    /// <summary>Whether this is one of the built-in Views. Not persisted; set by <c>ViewStore</c>.</summary>
    public bool BuiltIn { get; init; }
}

/// <summary>The filter dimensions applied to a View. Values within one dimension are OR'd; dimensions are AND'd.</summary>
public sealed record TaskFilter
{
    /// <summary>Matches Tasks whose Team is one of these, ignoring case. Empty matches every Team.</summary>
    public IReadOnlyList<string> Teams { get; init; } = [];

    /// <summary>Matches Tasks whose Team/Project is one of these. Empty matches every Project.</summary>
    public IReadOnlyList<ProjectRef> Projects { get; init; } = [];

    /// <summary>Matches Tasks whose Assignee is one of these names, or <c>"@me"</c>, or <c>"@unassigned"</c>. Empty matches every Assignee.</summary>
    public IReadOnlyList<string> Assignees { get; init; } = [];

    /// <summary>Matches Tasks whose Status is one of these. Empty matches every Status.</summary>
    public IReadOnlyList<TaskState> States { get; init; } = [];

    /// <summary>Matches Tasks whose Priority is one of these. Empty matches every Priority.</summary>
    public IReadOnlyList<TaskPriority> Priorities { get; init; } = [];

    /// <summary>Whether to keep only blocked or only unblocked Tasks. <see cref="BlockedFilter.All"/> (the default) is not written to <c>views.json</c>, so a View that never used it stays byte-identical.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BlockedFilter Blocked { get; init; } = BlockedFilter.All;
}

/// <summary>A Team, and optionally a Project within it. A null Project matches Tasks directly under the Team ("No project").</summary>
public sealed record ProjectRef(string Team, string? Project);

/// <summary>One key in a View's sort order.</summary>
public sealed record SortKey(string Field, SortDirection Direction);

/// <summary>One column of a Board View, mapped to the Task states shown in it.</summary>
public sealed record BoardColumn(string Label, IReadOnlyList<TaskState> States, bool Hidden = false);

/// <summary>The shape of <c>views.json</c>: a version number and the Views it holds.</summary>
public sealed record ViewsDocument(int Version, IReadOnlyList<TaskView> Views);

/// <summary>A View entry that failed to load, kept so a hand edit doesn't make it disappear silently.</summary>
public sealed record InvalidView(string? Id, string? Name, IReadOnlyList<string> Problems);

/// <summary>The result of saving or deleting a View.</summary>
public sealed record ViewSaveResult(bool Saved, IReadOnlyList<string> Problems);
