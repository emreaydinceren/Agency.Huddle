namespace Agency.Huddle.App.Tabs;

/// <summary>An immutable view of one group of Tabs: a row with one active Tab, drawn as one pane.</summary>
/// <param name="Id">The group id, unique for the circuit.</param>
/// <param name="Tabs">The Tabs, left to right, pinned ones first.</param>
/// <param name="ActiveId">The id of the active Tab, or <see langword="null"/> when the group is empty.</param>
public sealed record TabGroup(string Id, IReadOnlyList<Tab> Tabs, string? ActiveId)
{
    /// <summary>The active Tab, or <see langword="null"/> when the group is empty.</summary>
    public Tab? Active => this.ActiveId is null ? null : this.Tabs.FirstOrDefault(tab => string.Equals(tab.Id, this.ActiveId, StringComparison.Ordinal));
}
