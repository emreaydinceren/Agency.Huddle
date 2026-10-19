namespace Agency.Huddle.App.Search;

/// <summary>Everything the dropdown draws for one search: the rows, and the lines that sit outside the groups.</summary>
/// <param name="Rows">The group headings, cards and notices, in render order.</param>
/// <param name="Header">The sticky header, or null.</param>
/// <param name="QuietLine">The line shown when the box does not search, or null.</param>
/// <param name="EmptyLine">The line shown when every expected update arrived and no group has a hit, or null.</param>
/// <param name="Announcement">The text for the polite live region, once every expected update arrived, or null.</param>
public sealed record SearchDropdownView(
    IReadOnlyList<SearchDropdownRow> Rows,
    string? Header,
    string? QuietLine,
    string? EmptyLine,
    string? Announcement);
