namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// One run of adjacent changed lines in an <see cref="AgentsMdComparison"/>, with up to two unchanged lines either side.
/// </summary>
/// <param name="Removed">The lines of the old text that are gone, in order.</param>
/// <param name="Added">The lines of the new text that are new, in order.</param>
/// <param name="Before">Up to two unchanged lines just before the hunk.</param>
/// <param name="After">Up to two unchanged lines just after the hunk.</param>
internal sealed record AgentsMdComparisonHunk(
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Before,
    IReadOnlyList<string> After);
