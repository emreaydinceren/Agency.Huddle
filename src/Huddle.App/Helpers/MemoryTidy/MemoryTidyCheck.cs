namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What <see cref="MemoryTidyChecks.Run"/> decided about one step.</summary>
/// <param name="Refusal">The refusal text of the first check the step failed, or <see langword="null"/> when A-1 to A-7 accepted it.</param>
/// <param name="Files">The files the step reads as the input spells them, the kept file first; empty when A-1 refused.</param>
/// <param name="CountsAsDrop">Whether the step counts against the Verdict's drops: a drop, or a rewrite that removes most of a file (A-6).</param>
/// <param name="Description">The description to write, or <see langword="null"/> to keep the kept file's own (A-7).</param>
/// <param name="Note">Words for the list when the step keeps the file's own description although it gave one; <see langword="null"/> otherwise.</param>
internal sealed record MemoryTidyCheck(string? Refusal, IReadOnlyList<MemoryTidyFile> Files, bool CountsAsDrop, string? Description, string? Note)
{
    /// <summary>A step a check refused.</summary>
    /// <param name="refusal">The refusal text.</param>
    /// <param name="files">The files found so far.</param>
    internal static MemoryTidyCheck Refused(string refusal, IReadOnlyList<MemoryTidyFile>? files = null) => new(refusal, files ?? [], false, null, null);
}
