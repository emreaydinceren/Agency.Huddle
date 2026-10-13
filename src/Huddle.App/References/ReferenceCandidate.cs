using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>A reference the scanner found in a Message's text, before the resolver checks it exists.</summary>
/// <param name="Kind">What the text names.</param>
/// <param name="Key">A Task id (canonical), a Room id, a Team Name (decoded) or a path as written.</param>
/// <param name="Position">Where it starts in the text, for first-seen order.</param>
internal sealed record ReferenceCandidate(ReferenceKind Kind, string Key, int Position);
