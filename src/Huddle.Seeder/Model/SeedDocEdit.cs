using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Seeder.Model;

/// <summary>One History entry of a Library Markdown document, as an Agent's <c>log_change</c> or the Human's save would have written it.</summary>
/// <param name="RelativePath">The document's path below the data folder, using <c>/</c>; it must start with <c>Teams/</c>, <c>Teammates/</c> or <c>Uploads/</c>.</param>
/// <param name="DaysAgo">How many days before the run date the entry was written; at least 1, so it is in the past.</param>
/// <param name="Time">The UTC time of day, <c>HH:mm</c>.</param>
/// <param name="Actor">The Teammate's or the Human's Name.</param>
/// <param name="ActorKind">Agent or Human.</param>
/// <param name="Kind">What the author says they did.</param>
/// <param name="Summary">The author's words; empty for a Human save or create.</param>
internal sealed record SeedDocEdit(string RelativePath, int DaysAgo, string Time, string Actor, DocActorKind ActorKind, DocChangeKind Kind, string Summary = "");
