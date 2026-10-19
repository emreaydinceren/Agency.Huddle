namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>One entry of a path cell: a plain path, or a path for one computer.</summary>
/// <param name="Computer"><see langword="null"/> for a plain entry (every computer); otherwise the computer's name as written.</param>
/// <param name="Path">The text of the path, trimmed, not yet resolved or checked.</param>
public sealed record PathEntry(string? Computer, string Path);
