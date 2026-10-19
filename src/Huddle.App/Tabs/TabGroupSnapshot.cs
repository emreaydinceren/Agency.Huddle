namespace Agency.Huddle.App.Tabs;

/// <summary>A layout node in a <see cref="TabSnapshot"/>: a group of Tabs, or a split of groups drawn side by side.</summary>
/// <param name="Kind">The node kind: <see cref="GroupKind"/> or <see cref="SplitKind"/>.</param>
/// <param name="ActiveId">For a group, the id of its active Tab, or <see langword="null"/>.</param>
/// <param name="Tabs">For a group, the Tabs, left to right. Empty for a split.</param>
/// <param name="Children">For a split, the groups left to right; <see langword="null"/> for a group.</param>
/// <param name="Focused">For a split, the index of the child that has focus.</param>
internal sealed record TabGroupSnapshot(
    string Kind,
    string? ActiveId,
    IReadOnlyList<TabSnapshotEntry> Tabs,
    IReadOnlyList<TabGroupSnapshot>? Children = null,
    int Focused = 0)
{
    /// <summary>The <see cref="Kind"/> of a group node.</summary>
    public const string GroupKind = "group";

    /// <summary>The <see cref="Kind"/> of a split node: its children sit side by side.</summary>
    public const string SplitKind = "split";
}
