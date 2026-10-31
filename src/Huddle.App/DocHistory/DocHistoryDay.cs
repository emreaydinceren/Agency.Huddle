namespace Agency.Huddle.App.DocHistory;

/// <summary>One day of the History at the bottom of a document (DocHistory spec §6.10).</summary>
/// <param name="Label">"Today", "Yesterday", or the full date.</param>
/// <param name="Entries">That day's entries, newest first.</param>
public sealed record DocHistoryDay(string Label, IReadOnlyList<DocHistoryEntry> Entries);
