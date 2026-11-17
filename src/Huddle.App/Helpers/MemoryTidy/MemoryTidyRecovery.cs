using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Finishes what a stop or a crash left behind (Memory Tidy spec §6.12). <see cref="Settle"/> is the one method that decides what an open step
/// became, called by the pass's own <c>finally</c> and by <see cref="RecoverAsync"/> alike; recovery finishes every pass folder whose journal has entries
/// and no <c>PassFinished</c>.
/// </summary>
/// <param name="backups">Names the Memory backups root.</param>
/// <param name="finisher">Writes the list, posts the item and finishes the pass.</param>
/// <param name="logger">Reports a pass folder that could not be finished.</param>
internal sealed class MemoryTidyRecovery(MemoryTidyBackups backups, MemoryTidyFinisher finisher, ILogger<MemoryTidyRecovery> logger) : IMemoryTidyRecovery
{
    /// <summary>
    /// Settles every <c>StepStarted</c> that has no outcome. When every file the step named still has the version in the entry, the step did not happen;
    /// otherwise the files that differ are named, and those with a copy in the pass folder are marked as copied. Each settled step is appended to the
    /// journal, so a second call finds an outcome and settles nothing.
    /// </summary>
    /// <param name="entries">The pass's journal as read.</param>
    /// <param name="journal">Where the settled entries are appended.</param>
    /// <param name="passFolder">The pass folder, where the copies are.</param>
    /// <param name="folderOf">The absolute path of a memory folder from its key; <see langword="null"/> when it is not known, which reads as changed.</param>
    /// <returns>The entries this call appended.</returns>
    public static IReadOnlyList<MemoryTidyEntry.StepSettled> Settle(
        IReadOnlyList<MemoryTidyEntry> entries,
        IMemoryTidyJournal journal,
        string passFolder,
        Func<string, string?> folderOf)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);
        ArgumentNullException.ThrowIfNull(folderOf);

        HashSet<(string FolderKey, int Step)> decided = [];
        foreach (MemoryTidyEntry entry in entries)
        {
            _ = entry switch
            {
                MemoryTidyEntry.StepApplied applied => decided.Add((applied.FolderKey, applied.Step)),
                MemoryTidyEntry.StepRefused refused => decided.Add((refused.FolderKey, refused.Step)),
                MemoryTidyEntry.StepSkipped skipped => decided.Add((skipped.FolderKey, skipped.Step)),
                MemoryTidyEntry.StepFaulted faulted => decided.Add((faulted.FolderKey, faulted.Step)),
                MemoryTidyEntry.StepSettled settled => decided.Add((settled.FolderKey, settled.Step)),
                _ => false,
            };
        }

        List<MemoryTidyEntry.StepSettled> appended = [];
        foreach (MemoryTidyEntry.StepStarted started in entries.OfType<MemoryTidyEntry.StepStarted>())
        {
            if (!decided.Add((started.FolderKey, started.Step)))
            {
                continue;
            }

            string? folder = folderOf(started.FolderKey);
            List<string> changed = [.. started.Files.Where(file => !HasVersion(folder, file)).Select(static file => file.Name)];
            List<string> copied = [.. changed.Where(file => HasCopy(passFolder, started.FolderKey, file))];
            MemoryTidyEntry.StepSettled settledStep = new(started.Step, started.FolderKey, changed.Count == 0, changed, copied);
            journal.Append(settledStep);
            appended.Add(settledStep);
        }

        return appended;
    }

    /// <inheritdoc />
    public Task RecoverAsync(CancellationToken cancellationToken) => Task.Run(() => this.FinishEveryOpenPass(cancellationToken), cancellationToken);

    private void FinishEveryOpenPass(CancellationToken cancellationToken)
    {
        string root = backups.RootPath;
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string folder in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _ = finisher.Finish(folder, new MemoryTidyJournal(folder), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One pass folder that cannot be finished (a disk error or damaged data) must not stop the others: log it, leave it for the next start, and go on.
                logger.LogWarning(ex, "Memory Tidy could not finish the pass in {PassFolder}; the next start tries again.", folder);
            }
        }
    }

    private static bool HasVersion(string? folder, MemoryTidyEntry.StepFile file)
    {
        if (folder is null || !MemoryFileRules.IsValidFileName(file.Name))
        {
            return false;
        }

        string path = Path.Combine(folder, file.Name);
        try
        {
            return File.Exists(path) && string.Equals(MemoryVersion.Of(File.ReadAllBytes(path)), file.Version, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            // A file that cannot be read is not the file the step saw.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // A file that cannot be read is not the file the step saw.
            return false;
        }
    }

    private static bool HasCopy(string passFolder, string folderKey, string file)
    {
        try
        {
            return MemoryFileRules.IsValidFileName(file) && File.Exists(Path.Combine(MemoryTidyBackups.FolderFor(passFolder, folderKey), file));
        }
        catch (ArgumentException)
        {
            // A folder key that names no backup folder has no copies.
            return false;
        }
    }
}
