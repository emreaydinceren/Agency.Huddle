using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

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

    /// <summary>Matches Tasks whose Sprint is one of these ids (<c>"S2"</c>), or <c>"@current"</c>, or <c>"@none"</c>. Empty matches every Task, and is not written to <c>views.json</c> (see <c>ViewJson</c>), so a View that never used it stays byte-identical.</summary>
    public IReadOnlyList<string> Sprints { get; init; } = [];

    /// <summary>Whether to keep only Request Tasks (those with a <c>request</c> key). <see langword="false"/> (the default) is not written to <c>views.json</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequestsOnly { get; init; }
}
