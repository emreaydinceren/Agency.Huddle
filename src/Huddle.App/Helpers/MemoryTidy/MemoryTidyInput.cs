namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Everything the Memory Tidy Helper sees for one folder, and what its applier needs from the pass.</summary>
/// <param name="FolderKey"><c>agent:&lt;Name&gt;</c> or <c>huddle</c>.</param>
/// <param name="Owner">Whose Memory it is, in words for the prompt.</param>
/// <param name="Today">The Human's local date.</param>
/// <param name="Index">One line per topic file: name, description, the day it was written, and <c>kept</c> for a file marked <c>tidy: keep</c>.</param>
/// <param name="Files">The files shown in full, the only ones a step may name.</param>
/// <param name="Limits">The caps the Verdict must keep.</param>
/// <param name="Context">The pass this call belongs to. Never shown to the model and never part of the fingerprint.</param>
internal sealed record MemoryTidyInput(
    string FolderKey,
    string Owner,
    DateOnly Today,
    IReadOnlyList<string> Index,
    IReadOnlyList<MemoryTidyFile> Files,
    MemoryTidyLimits Limits,
    MemoryTidyPassContext Context);
