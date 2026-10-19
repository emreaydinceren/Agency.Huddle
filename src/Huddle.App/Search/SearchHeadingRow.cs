namespace Agency.Huddle.App.Search;

/// <summary>A group heading. <see cref="IsSearching"/> is true while an active group has no result yet.</summary>
/// <param name="Id">The element id of the group, <c>search-group-{group}-{scope}</c> in lower case.</param>
/// <param name="Text">The heading text, as spec §6.3 names it.</param>
/// <param name="IsArchived">Whether the heading is the archived side of its category.</param>
/// <param name="IsSearching">Whether the group is still searching.</param>
public sealed record SearchHeadingRow(string Id, string Text, bool IsArchived, bool IsSearching) : SearchDropdownRow;
