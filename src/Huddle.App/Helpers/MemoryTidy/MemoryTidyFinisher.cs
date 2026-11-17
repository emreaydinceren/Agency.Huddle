using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The end of a pass, the same whether the pass's own <c>finally</c> or start-up recovery runs it (Memory Tidy spec §6.12): settles the steps that
/// were started and never got an outcome, writes <c>changes.md</c> when there is something to list, posts one <c>MemoryTidied</c> Notification when a
/// topic file changed, and writes <c>PassFinished</c>. A pass that already has <c>PassFinished</c> is left alone, so an item is never posted twice.
/// </summary>
/// <param name="paths">Resolves an Agent's own memory folder.</param>
/// <param name="resolver">Makes the Library links of the list.</param>
/// <param name="feed">Where the item goes.</param>
/// <param name="backups">Names the Memory backups root, where earlier passes are looked up.</param>
/// <param name="options">Supplies <c>reported.md</c>'s age for the list.</param>
internal sealed class MemoryTidyFinisher(
    MemoryPaths paths,
    LibraryPathResolver resolver,
    IFeedPublisher feed,
    MemoryTidyBackups backups,
    IOptions<TeamOptions> options)
{
    private const string HelperId = "memory-tidy";
    private const string HelperName = "Memory Tidy";
    private const string AgentKeyPrefix = "agent:";

    /// <summary>Finishes a pass folder; does nothing when its journal is empty or already finished.</summary>
    /// <param name="passFolder">The pass folder.</param>
    /// <param name="journal">The pass's journal, which settled steps, the end and the finish are appended to.</param>
    /// <param name="cancellationToken">Checked between the parts; the pass's own token is never passed here.</param>
    /// <returns>Whether the pass was finished by this call.</returns>
    public bool Finish(string passFolder, IMemoryTidyJournal journal, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);
        ArgumentNullException.ThrowIfNull(journal);

        IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(passFolder);
        if (entries.Count == 0 || entries.Any(static entry => entry is MemoryTidyEntry.PassFinished))
        {
            return false;
        }

        if (!entries.Any(static entry => entry is MemoryTidyEntry.PassEnded))
        {
            journal.Append(new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.Stopped));
        }

        _ = MemoryTidyRecovery.Settle(MemoryTidyJournal.Read(passFolder), journal, passFolder, this.FolderOf);
        cancellationToken.ThrowIfCancellationRequested();

        entries = MemoryTidyJournal.Read(passFolder);
        MemoryTidyCounts counts = MemoryTidyList.Count(entries);
        if (entries.Any(static entry => HasListLine(entry)))
        {
            _ = MemoryTidyList.Write(passFolder, options.Value.Memory.Reported.KeepDays, this.FolderOf, this.HrefOf);
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool post = counts.ChangedAnyFile;
        if (post)
        {
            this.PostItem(passFolder, entries, counts);
        }

        journal.Append(new MemoryTidyEntry.PassFinished(post));
        return true;
    }

    private static bool HasListLine(MemoryTidyEntry entry) =>
        entry is not (MemoryTidyEntry.PassStarted or MemoryTidyEntry.PassEnded or MemoryTidyEntry.PassFinished or MemoryTidyEntry.FolderJudged);

    private void PostItem(string passFolder, IReadOnlyList<MemoryTidyEntry> entries, MemoryTidyCounts counts)
    {
        MemoryTidyEntry.PassEnded? ended = entries.OfType<MemoryTidyEntry.PassEnded>().LastOrDefault();
        bool stopped = ended is null || ended.End != MemoryTidyPassEnd.Done;
        string list = MemoryTidyList.PathIn(passFolder);
        feed.Post(new FeedMessage(
            FeedSourceType.MemoryTidied,
            list,
            FeedActor.Huddle,
            true,
            new MemoryTidiedPayload(HelperId, HelperName, list, counts.Tidied, counts.Dropped, counts.Folders, stopped, !this.EarlierPassPostedAnItem(passFolder))));
    }

    /// <summary>Whether another pass folder's journal says an item was posted: the item says it is the first only when none did.</summary>
    private bool EarlierPassPostedAnItem(string passFolder)
    {
        string root = backups.RootPath;
        if (!Directory.Exists(root))
        {
            return false;
        }

        string self = Path.TrimEndingDirectorySeparator(Path.GetFullPath(passFolder));
        return Directory.EnumerateDirectories(root)
            .Any(folder => !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), self, StringComparison.OrdinalIgnoreCase)
                && MemoryTidyJournal.Read(folder).Any(static entry => entry is MemoryTidyEntry.PassFinished { ItemPosted: true }));
    }

    private string? FolderOf(string folderKey) =>
        folderKey.StartsWith(AgentKeyPrefix, StringComparison.Ordinal) && folderKey.Length > AgentKeyPrefix.Length
            ? paths.OwnFolderOf(folderKey[AgentKeyPrefix.Length..])?.Path
            : null;

    private string? HrefOf(string absolutePath) => MemoryTidyList.LibraryHref(resolver, absolutePath);
}
