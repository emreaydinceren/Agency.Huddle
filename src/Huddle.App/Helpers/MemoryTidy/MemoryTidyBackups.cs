using System.Globalization;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The Memory backups root (Memory Tidy spec §6.12): makes one folder per pass, names where a memory folder's copies go inside it, and deletes
/// pass folders past <c>Team:Memory:Tidy:KeepBackupDays</c>. This is the one place Huddle deletes folders on a timer, so the deletion is guarded.
/// </summary>
/// <param name="options">Supplies the data folder, the root's folder name and the retention.</param>
/// <param name="timeProvider">The clock; the system clock when <see langword="null"/>.</param>
internal sealed class MemoryTidyBackups(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string AgentKeyPrefix = "agent:";
    private const string HuddleKey = "huddle";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>The backup root's full path, <c>{DataDir}/{Team:Library:MemoryBackups:Dir}</c>; the folder the Library root points at.</summary>
    public string RootPath => Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Library.MemoryBackups.Dir));

    /// <summary>
    /// Makes the folder of a pass, named by the pass's local date, then <c>-2</c>, <c>-3</c> when that name is taken.
    /// </summary>
    /// <returns>The pass id (the folder's name) and the folder's full path.</returns>
    public (string Id, string Path) CreatePassFolder()
    {
        string root = this.RootPath;
        string baseName = this.clock.GetLocalNow().Date.ToString(DateFormat, CultureInfo.InvariantCulture);
        string id = baseName;
        int number = 1;
        while (Directory.Exists(Path.Combine(root, id)) || File.Exists(Path.Combine(root, id)))
        {
            number++;
            id = string.Create(CultureInfo.InvariantCulture, $"{baseName}-{number}");
        }

        string folder = Path.Combine(root, id);
        _ = Directory.CreateDirectory(folder);
        return (id, folder);
    }

    /// <summary>
    /// The folder inside a pass folder where one memory folder's copies go: <c>Agents/&lt;Name&gt;</c> for <c>agent:&lt;Name&gt;</c>,
    /// <c>Huddle</c> for <c>huddle</c>. The folder is not created; the writer makes it with the copy.
    /// </summary>
    /// <param name="passFolder">The pass folder.</param>
    /// <param name="folderKey">The folder key.</param>
    /// <returns>The full path of the subfolder.</returns>
    /// <exception cref="ArgumentException">The key is neither <c>huddle</c> nor <c>agent:</c> and a single plain name.</exception>
    public static string FolderFor(string passFolder, string folderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);

        if (string.Equals(folderKey, HuddleKey, StringComparison.Ordinal))
        {
            return Path.Combine(passFolder, "Huddle");
        }

        if (folderKey.StartsWith(AgentKeyPrefix, StringComparison.Ordinal))
        {
            string name = folderKey[AgentKeyPrefix.Length..];
            if (name.Length > 0 && name is not "." and not ".." && name.IndexOfAny(['/', '\\', ':']) < 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            {
                return Path.Combine(passFolder, "Agents", name);
            }
        }

        throw new ArgumentException($"'{folderKey}' is not a memory folder key.", nameof(folderKey));
    }

    /// <summary>
    /// Deletes pass folders older than <c>KeepBackupDays</c>. A folder goes only when it is directly under the root, is named
    /// <c>yyyy-MM-dd</c> or <c>yyyy-MM-dd-N</c>, is older by that date <b>and</b> by the write time of its <c>pass.jsonl</c>, and its journal is finished.
    /// A root that is a junction or link deletes nothing; zero or less keeps every backup.
    /// </summary>
    /// <returns>How many pass folders were deleted.</returns>
    public int DeleteOld()
    {
        int keepDays = options.Value.Memory.Tidy.KeepBackupDays;
        string root = this.RootPath;
        if (keepDays <= 0 || !Directory.Exists(root) || IsLink(new DirectoryInfo(root)))
        {
            return 0;
        }

        DateOnly today = DateOnly.FromDateTime(this.clock.GetLocalNow().Date);
        DateTimeOffset now = this.clock.GetUtcNow();
        int deleted = 0;
        foreach (DirectoryInfo child in new DirectoryInfo(root).EnumerateDirectories())
        {
            if (IsPassName(child.Name, out DateOnly passDay)
                && !IsLink(child)
                && today.DayNumber - passDay.DayNumber > keepDays
                && IsOldByJournal(child.FullName, now, keepDays)
                && TryDelete(child))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static bool IsOldByJournal(string passFolder, DateTimeOffset now, int keepDays)
    {
        string journal = Path.Combine(passFolder, MemoryTidyJournal.FileName);
        if (!File.Exists(journal) || now - new DateTimeOffset(File.GetLastWriteTimeUtc(journal), TimeSpan.Zero) <= TimeSpan.FromDays(keepDays))
        {
            return false;
        }

        IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(passFolder);
        bool unfinished = entries.Count > 0 && !entries.Any(static entry => entry is MemoryTidyEntry.PassFinished);
        return !unfinished;
    }

    private static bool IsPassName(string name, out DateOnly day)
    {
        day = default;
        if (name.Length < DateFormat.Length || !DateOnly.TryParseExact(name[..DateFormat.Length], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return false;
        }

        string rest = name[DateFormat.Length..];
        return rest.Length == 0 || (rest.Length > 1 && rest[0] == '-' && rest.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0);
    }

    private static bool IsLink(FileSystemInfo info) => (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null;

    private static bool TryDelete(DirectoryInfo folder)
    {
        try
        {
            folder.Delete(recursive: true);
            return true;
        }
        catch (IOException)
        {
            // In use or half gone: the folder stays and the next pass tries again.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Not ours to delete right now: the folder stays and the next pass tries again.
            return false;
        }
    }
}
