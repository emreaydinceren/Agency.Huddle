namespace Agency.Huddle.App.Search;

/// <summary>What the dropdown needs to draw one view: the parsed box, the state of each expected group update, and the sticky term (spec §6.3, §6.4).</summary>
/// <param name="Parse">The parse of the box text.</param>
/// <param name="States">The state of each group and side, keyed by filter and scope. A missing key is an update that has not arrived.</param>
/// <param name="StickyTerm">The term of the last search, or null when there was none.</param>
/// <param name="BoxIsEmpty">Whether the box is empty.</param>
public sealed record SearchDropdownInput(
    SearchParse Parse,
    IReadOnlyDictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> States,
    string? StickyTerm,
    bool BoxIsEmpty);
