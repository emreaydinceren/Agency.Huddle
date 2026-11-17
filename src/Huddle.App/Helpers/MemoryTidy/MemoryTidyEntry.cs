using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// One line of a pass's journal (<c>pass.jsonl</c>): a closed set of records, written before and after each change. The journal sits in a
/// Library root for the backup retention, so no entry holds a step's new text or description.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "entry")]
[JsonDerivedType(typeof(PassStarted), "pass-started")]
[JsonDerivedType(typeof(LinesDropped), "lines-dropped")]
[JsonDerivedType(typeof(FolderJudged), "folder-judged")]
[JsonDerivedType(typeof(FolderNotJudged), "folder-not-judged")]
[JsonDerivedType(typeof(StepStarted), "step-started")]
[JsonDerivedType(typeof(StepApplied), "step-applied")]
[JsonDerivedType(typeof(StepRefused), "step-refused")]
[JsonDerivedType(typeof(StepSkipped), "step-skipped")]
[JsonDerivedType(typeof(StepFaulted), "step-faulted")]
[JsonDerivedType(typeof(StepSettled), "step-settled")]
[JsonDerivedType(typeof(PassEnded), "pass-ended")]
[JsonDerivedType(typeof(PassFinished), "pass-finished")]
internal abstract record MemoryTidyEntry
{
    private MemoryTidyEntry()
    {
    }

    /// <summary>A file a step names, with the version it had when the step was planned.</summary>
    /// <param name="Name">The file name, relative to its memory folder.</param>
    /// <param name="Version">The file's version token (<c>MemoryVersion.Of</c>).</param>
    internal sealed record StepFile(string Name, string Version);

    /// <summary>Written when the pass folder is made.</summary>
    /// <param name="PassId">The pass folder's name.</param>
    /// <param name="At">When the pass started.</param>
    /// <param name="Start">What started it.</param>
    internal sealed record PassStarted(string PassId, DateTimeOffset At, MemoryTidyStart Start) : MemoryTidyEntry;

    /// <summary>Written after a <c>reported.md</c> prune.</summary>
    /// <param name="Agent">The Agent whose file was pruned.</param>
    /// <param name="Count">How many lines went.</param>
    /// <param name="Copy">The backup copy, relative to the pass folder.</param>
    internal sealed record LinesDropped(string Agent, int Count, string Copy) : MemoryTidyEntry;

    /// <summary>Written when a folder's Verdict was read.</summary>
    /// <param name="FolderKey">The folder key, <c>agent:Name</c> or <c>huddle</c>.</param>
    internal sealed record FolderJudged(string FolderKey) : MemoryTidyEntry;

    /// <summary>Written when a folder got no Verdict.</summary>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="Reason">Why: the card's error, <c>failed twice</c> or <c>the Helper was turned off</c>.</param>
    internal sealed record FolderNotJudged(string FolderKey, string Reason) : MemoryTidyEntry;

    /// <summary>Written before the writer is called. Carries no new text and no description.</summary>
    /// <param name="Step">The step's number in this pass; every outcome of it carries the same number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="Kind">The step's kind (describe, rewrite, merge, drop).</param>
    /// <param name="Files">Each file the step names, with its version.</param>
    /// <param name="Reason">The Helper's reason for the step.</param>
    internal sealed record StepStarted(int Step, string FolderKey, string Kind, IReadOnlyList<StepFile> Files, string Reason) : MemoryTidyEntry;

    /// <summary>Written after the writer answered <c>Done</c>.</summary>
    /// <param name="Step">The step's number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="Written">The files written.</param>
    /// <param name="Removed">The files removed.</param>
    /// <param name="Copies">The backup copies, relative to the pass folder.</param>
    /// <param name="RemovedTokens">The numbers, dates and links a rewrite removed.</param>
    /// <param name="LeftBehind">Files the step meant to remove and could not.</param>
    internal sealed record StepApplied(
        int Step,
        string FolderKey,
        IReadOnlyList<string> Written,
        IReadOnlyList<string> Removed,
        IReadOnlyList<string> Copies,
        IReadOnlyList<string> RemovedTokens,
        IReadOnlyList<string> LeftBehind) : MemoryTidyEntry;

    /// <summary>Written when a check or a memory rule refused the step.</summary>
    /// <param name="Step">The step's number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="Refusal">The refusal.</param>
    internal sealed record StepRefused(int Step, string FolderKey, string Refusal) : MemoryTidyEntry;

    /// <summary>Written when a file had changed since it was shown (<c>Stale</c>).</summary>
    /// <param name="Step">The step's number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="File">The file.</param>
    internal sealed record StepSkipped(int Step, string FolderKey, string File) : MemoryTidyEntry;

    /// <summary>Written when the copy or the write failed.</summary>
    /// <param name="Step">The step's number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="File">The file.</param>
    internal sealed record StepFaulted(int Step, string FolderKey, string File) : MemoryTidyEntry;

    /// <summary>Written by recovery for a step that was started and never got an outcome, so a second recovery does not settle it again.</summary>
    /// <param name="Step">The step's number.</param>
    /// <param name="FolderKey">The folder key.</param>
    /// <param name="NotDone"><see langword="true"/> when every named file still had its version, so the step did not happen.</param>
    /// <param name="ChangedFiles">The named files that no longer have their version; empty when <paramref name="NotDone"/>.</param>
    /// <param name="CopiedFiles">The named files that have a copy in the pass folder.</param>
    internal sealed record StepSettled(int Step, string FolderKey, bool NotDone, IReadOnlyList<string> ChangedFiles, IReadOnlyList<string> CopiedFiles) : MemoryTidyEntry;

    /// <summary>Written when the loop ends.</summary>
    /// <param name="End">Why it ended.</param>
    internal sealed record PassEnded(MemoryTidyPassEnd End) : MemoryTidyEntry;

    /// <summary>Written after the list is written and the Feed item posted.</summary>
    /// <param name="ItemPosted">Whether a Feed item was posted.</param>
    internal sealed record PassFinished(bool ItemPosted) : MemoryTidyEntry;
}
