namespace Agency.Huddle.App.FileChanges;

/// <summary>The File Changes block for one Turn: already-capped changes, plus what the cap left out.</summary>
/// <param name="Changes">The changes to report, already capped.</param>
/// <param name="NotListed">How many changes the cap left out.</param>
/// <param name="Unchecked">Full paths of folders over the file cap, so they were not scanned.</param>
/// <param name="MaxFilesPerFolder">
/// The cap in force, carried only so the pure <c>BuildPrompt</c> can render <c>turn.folderUnchecked</c>'s
/// <c>{{max}}</c> placeholder.
/// </param>
internal sealed record FileChangesReport(
    IReadOnlyList<FileChange> Changes,
    int NotListed,
    IReadOnlyList<string> Unchecked,
    int MaxFilesPerFolder = 0)
{
    /// <summary>An empty report: no changes, nothing left out, nothing unchecked.</summary>
    internal static FileChangesReport Empty { get; } = new([], 0, []);

    /// <summary><see langword="true"/> when there is nothing to report: no changes, nothing left out, nothing unchecked.</summary>
    internal bool IsEmpty => this.Changes.Count == 0 && this.NotListed == 0 && this.Unchecked.Count == 0;
}
