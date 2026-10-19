namespace Agency.Huddle.App.Search;

/// <summary>A line of text under a group: a failure, a truncation note, or a no-match line.</summary>
/// <param name="Text">The text, exactly as spec §6.3 decides it.</param>
/// <param name="Kind">Which notice this is.</param>
public sealed record SearchNoticeRow(string Text, SearchNoticeKind Kind) : SearchDropdownRow;
