namespace Agency.Huddle.Contracts;

/// <summary>What one reference in a Message named at send time (spec §8.1). Metadata: never drawn as text.</summary>
/// <param name="Kind">What was referenced.</param>
/// <param name="Key">The stable key: a Task id, a Room id, a Team Name or an absolute path.</param>
/// <param name="Label">What the sender saw: the Task id, the Room or Team Name, or the file name.</param>
/// <param name="Title">A Task's title at send time; otherwise null.</param>
/// <param name="Status">A Task's status in wire form at send time; otherwise null.</param>
public sealed record MessageReference(ReferenceKind Kind, string Key, string Label, string? Title = null, string? Status = null);
