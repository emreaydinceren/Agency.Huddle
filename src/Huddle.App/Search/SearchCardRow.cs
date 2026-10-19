namespace Agency.Huddle.App.Search;

/// <summary>One result card, in render order.</summary>
/// <param name="OptionId">The element id, <c>search-option-{n}</c>, counted from zero in render order.</param>
/// <param name="GroupId">The id of the group heading the card sits under.</param>
/// <param name="Hit">The result the card shows.</param>
public sealed record SearchCardRow(string OptionId, string GroupId, SearchHit Hit) : SearchDropdownRow;
