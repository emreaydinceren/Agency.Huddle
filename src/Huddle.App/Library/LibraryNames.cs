namespace Agency.Huddle.App.Library;

/// <summary>
/// Validates file and folder names in the Library. Every per-segment rule lives once in
/// <see cref="LibrarySegments.Check(string)"/>; this type only maps that single rule set to the
/// Library's settled name-refusal text (corrections-B4 item 1), so the two can never drift.
/// </summary>
internal static class LibraryNames
{
    /// <summary>
    /// Validates a file or folder name against Spec §6.4 rules.
    /// </summary>
    /// <param name="name">The name to validate (never a path).</param>
    /// <returns><see langword="null"/> when the name is acceptable, a user-facing refusal reason otherwise.</returns>
    internal static string? Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // A name is never a path: "/" and "\" are always invalid, on every OS, before the
        // shared per-segment rules even apply (a segment never contains a separator).
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A name can't be empty.";
        }

        if (name.Contains('/'))
        {
            return "A name can't contain /.";
        }

        if (name.Contains('\\'))
        {
            return "A name can't contain \\.";
        }

        SegmentProblem? problem = LibrarySegments.Check(name);
        if (problem is null)
        {
            return null;
        }

        return problem switch
        {
            { Kind: SegmentProblemKind.Empty or SegmentProblemKind.DotSegment } => "A name can't be empty.",
            { Kind: SegmentProblemKind.InvalidCharacter } => $"A name can't contain {problem.Character}.",
            { Kind: SegmentProblemKind.TrailingDotOrSpace } => "A name can't end with a dot or a space.",
            { Kind: SegmentProblemKind.ReservedDeviceName } => $"\"{name}\" is reserved by Windows.",
            { Kind: SegmentProblemKind.ShortNameAlias } => "A name can't use an 8.3 alias pattern.",
            _ => throw new InvalidOperationException($"Unknown segment problem kind: {problem.Kind}"),
        };
    }
}
