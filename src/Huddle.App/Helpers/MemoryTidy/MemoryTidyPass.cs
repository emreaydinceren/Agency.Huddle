using System.Text;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// One Memory Tidy pass (Memory Tidy spec §6.7). It makes the pass folder, deletes the backups past their retention, and for every Persona in the
/// catalog, running or not, drops the <c>reported.md</c> lines older than <c>KeepDays</c> (§6.6) with no model call. Whatever happens, the pass's
/// <c>finally</c> finishes the list under its own ten-second token, so a stopped pass still has its <c>changes.md</c>.
/// </summary>
/// <param name="personas">The catalog; every Persona's own folder is looked at.</param>
/// <param name="paths">Resolves an Agent's own memory folder.</param>
/// <param name="writer">Applies the prune, copying the file first.</param>
/// <param name="backups">Makes the pass folder and deletes the old ones.</param>
/// <param name="finisher">Settles open steps, writes the list, posts the item and finishes the pass.</param>
/// <param name="options">Supplies <c>KeepDays</c>.</param>
/// <param name="logger">Reports a prune that failed and a finish that could not run.</param>
/// <param name="timeProvider">The clock; the system clock when <see langword="null"/>.</param>
internal sealed class MemoryTidyPass(
    PersonaStore personas,
    MemoryPaths paths,
    MemoryWriter writer,
    MemoryTidyBackups backups,
    MemoryTidyFinisher finisher,
    IOptions<TeamOptions> options,
    ILogger<MemoryTidyPass> logger,
    TimeProvider? timeProvider = null) : IMemoryTidyPass
{
    private static readonly TimeSpan FinishBudget = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// A test seam, awaited after the pass's journal has its first entry and before any folder is looked at. A test throws here to show a pass that
    /// threw still has its list, or holds it to show what a stop does.
    /// </summary>
    internal Func<string, Task>? AfterStartedForTests { get; set; }

    /// <summary>
    /// A test seam, awaited with a file's full path after <c>reported.md</c> was read and found to have lines to drop, and before the prune is asked for.
    /// A test appends a line here, as an Agent's <c>append</c> in that instant would, to show the stale version skips the file.
    /// </summary>
    internal Func<string, Task>? AfterReportedReadForTests { get; set; }

    /// <inheritdoc />
    public async Task<MemoryTidyPassEnd> RunAsync(MemoryTidyStart start, CancellationToken cancellationToken)
    {
        (string id, string folder) = backups.CreatePassFolder();
        MemoryTidyJournal journal = new(folder);
        journal.Append(new MemoryTidyEntry.PassStarted(id, this.clock.GetUtcNow(), start));
        MemoryTidyPassEnd end = MemoryTidyPassEnd.Done;
        try
        {
            if (this.AfterStartedForTests is { } hook)
            {
                await hook(folder).ConfigureAwait(false);
            }

            _ = backups.DeleteOld();
            await this.DropOldReportedLinesAsync(folder, journal, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            end = MemoryTidyPassEnd.Stopped;
        }
        catch (Exception)
        {
            end = MemoryTidyPassEnd.Threw;
            throw;
        }
        finally
        {
            this.Finish(folder, journal, end);
        }

        return end;
    }

    /// <summary>Writes the end, settles, lists and posts under the pass's own ten seconds; never the pass's token, which may be the very thing that stopped it.</summary>
    private void Finish(string folder, MemoryTidyJournal journal, MemoryTidyPassEnd end)
    {
        using CancellationTokenSource finishing = new(FinishBudget, this.clock);
        try
        {
            journal.Append(new MemoryTidyEntry.PassEnded(end));
            _ = finisher.Finish(folder, journal, finishing.Token);
        }
        catch (Exception ex)
        {
            // The pass has ended either way, and the unfinished journal is what start-up recovery looks for: log it and let the caller's outcome stand.
            logger.LogError(ex, "Memory Tidy could not finish the pass in {PassFolder}; the next start finishes it.", folder);
        }
    }

    private async Task DropOldReportedLinesAsync(string passFolder, MemoryTidyJournal journal, CancellationToken cancellationToken)
    {
        int keepDays = options.Value.Memory.Reported.KeepDays;
        if (keepDays <= 0)
        {
            return;
        }

        DateOnly today = DateOnly.FromDateTime(this.clock.GetLocalNow().Date);
        foreach (string name in personas.Entries.Select(static entry => entry.Name).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.DropOldLinesOfAsync(name, passFolder, journal, today, keepDays, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DropOldLinesOfAsync(string name, string passFolder, MemoryTidyJournal journal, DateOnly today, int keepDays, CancellationToken cancellationToken)
    {
        MemoryFolder? folder = paths.OwnFolderOf(name);
        string? file = folder is null ? null : MemoryPaths.FilePath(folder, ReportedFile.FileName);
        if (folder is null || file is null || !File.Exists(file) || TeamMemoryIndex.IsSkipped(file))
        {
            return;
        }

        byte[] bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        (string pruned, int dropped) = ReportedFile.PruneByAge(Utf8.GetString(bytes), today, keepDays);
        if (dropped == 0)
        {
            return;
        }

        if (this.AfterReportedReadForTests is { } hook)
        {
            await hook(file).ConfigureAwait(false);
        }

        string backup = MemoryTidyBackups.FolderFor(passFolder, "agent:" + name);
        string body = pruned[NoteFrontmatter.Locate(pruned).BodyStart..];
        MemoryTidyResult result = await writer.PruneReportedAsync(folder, body, MemoryVersion.Of(bytes), backup, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case MemoryTidyResult.Done done:
                string copy = done.Copies.Count > 0 ? Path.GetRelativePath(passFolder, done.Copies[0]).Replace('\\', '/') : string.Empty;
                journal.Append(new MemoryTidyEntry.LinesDropped(name, dropped, copy));
                break;
            case MemoryTidyResult.Stale:
                // The Agent appended between the read and the write; the file is next week's.
                break;
            case MemoryTidyResult.Failed failed:
                logger.LogWarning(failed.Error, "Memory Tidy could not drop old lines of {Agent}'s reported.md.", name);
                break;
            default:
                logger.LogWarning("Memory Tidy did not drop old lines of {Agent}'s reported.md: {Result}.", name, result);
                break;
        }
    }
}
