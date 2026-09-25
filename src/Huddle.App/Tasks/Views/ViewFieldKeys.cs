using System.Collections.Frozen;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The field keys a View's <see cref="TaskView.Fields"/> and <see cref="SortKey.Field"/> can name (Spec §12.2).</summary>
internal static class ViewFieldKeys
{
    /// <summary>Every known field key. <c>id</c> and <c>title</c> are always shown and are implicit in <see cref="TaskView.Fields"/>.</summary>
    public static readonly FrozenSet<string> All = new[]
    {
        "id", "title", "status", "priority", "assignee", "creator", "team", "project", "parent",
        "blocked_by", "tags", "start_date", "due_date", "created", "updated", "closed", "origin",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Every key except <c>blocked_by</c> and <c>tags</c>, which cannot be sorted on.</summary>
    public static readonly FrozenSet<string> Sortable = All
        .Where(key => key is not ("blocked_by" or "tags"))
        .ToFrozenSet(StringComparer.Ordinal);
}
