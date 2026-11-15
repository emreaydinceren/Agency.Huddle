namespace Agency.Huddle.App.Tasks;

/// <summary>One immutable entry in a Task's timeline: when, who, what kind, and the text (Spec §7.7, ADR-0086).</summary>
/// <param name="At">When the entry was written, in whole seconds.</param>
/// <param name="Author">Who wrote it; an Event's actor.</param>
/// <param name="Kind">Whether it is a comment, Evidence or an event.</param>
/// <param name="Text">The entry's text: Markdown for a comment, the Evidence fields for Evidence, the summary for an event.</param>
public sealed record TimelineEntry(DateTimeOffset At, string Author, TimelineEntryKind Kind, string Text);
