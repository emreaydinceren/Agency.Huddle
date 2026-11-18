namespace Agency.Huddle.App.DocHistory;

/// <summary>One line of a document's History, as its author reported it (DocHistory spec §6.1). Never checked against the file.</summary>
/// <param name="At">When the entry was written, from the injected <see cref="TimeProvider"/>.</param>
/// <param name="Actor">The Agent's or the Human's Name at the time of writing.</param>
/// <param name="ActorKind">Whether <paramref name="Actor"/> is an Agent or the Human.</param>
/// <param name="Kind">What the author says they did.</param>
/// <param name="Summary">One line in the author's words; empty for a Human save or create.</param>
/// <param name="Goldfish">Whether the Agent wrote the entry as a goldfish (goldfish spec §6.8).</param>
public sealed record DocHistoryEntry(DateTimeOffset At, string Actor, DocActorKind ActorKind, DocChangeKind Kind, string Summary, bool Goldfish = false);
