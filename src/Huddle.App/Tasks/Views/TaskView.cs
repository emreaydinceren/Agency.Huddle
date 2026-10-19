using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A saved arrangement of Tasks: a filter, a grouping, a sort, and either a List's columns or a Board's.</summary>
public sealed record TaskView
{
    /// <summary>The View's unique identifier. A Guid "N" string; built-ins use fixed ids.</summary>
    public required string Id { get; init; }

    /// <summary>The View's display name.</summary>
    public required string Name { get; init; }

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

    /// <summary>The List's column widths in CSS pixels, by field key, as the Human last dragged them. <see langword="null"/> (not written to <c>views.json</c>) leaves every column to size itself.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? ColumnWidths { get; init; }

    /// <summary>Whether this is one of the built-in Views. Not persisted; set by <c>ViewStore</c>.</summary>
    public bool BuiltIn { get; init; }
}
