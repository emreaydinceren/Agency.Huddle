using Microsoft.Extensions.Options;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The Library's file operations service (Spec §6.4): reads and writes documents and lists
/// folders, every operation going through <see cref="LibraryPathResolver"/> rather than a
/// hand-built path.
/// </summary>
/// <param name="resolver">Resolves and re-resolves every <see cref="LibraryPath"/> this service is given.</param>
/// <param name="recycleBin">Sends a file or folder to the OS recycle bin (Spec §6.4 <c>RecycleAsync</c>).</param>
/// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="TeamOptions.FileChanges"/>'s ignore list.</param>
/// <param name="logger">Logs unexpected failures.</param>
internal sealed class LibraryFileService(LibraryPathResolver resolver, IRecycleBin recycleBin, IOptions<TeamOptions> options, ILogger<LibraryFileService> logger)
{
    /// <summary>Folder names hidden everywhere in the Library tree (Spec §6.4), beyond <see cref="FileChangesOptions.EffectiveIgnore"/>.</summary>
    private static readonly string[] AlwaysHiddenFolders = [".obsidian", ".trash", ".git"];

    private readonly LibraryPathResolver resolver = resolver;
    private readonly IRecycleBin recycleBin = recycleBin;
    private readonly IOptions<TeamOptions> options = options;
    private readonly ILogger<LibraryFileService> logger = logger;

    /// <summary>
    /// Lists <paramref name="folder"/>'s children (Spec §6.4): folders first, then
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> by name. Hides <c>.obsidian/</c>, <c>.trash/</c>
    /// and <c>.git/</c>, honours <see cref="FileChangesOptions.EffectiveIgnore"/> (folders only), and,
    /// under the Teams root only, hides <c>_</c>-prefixed folders (Spec §6.16). A folder that does not
    /// exist on disk (a Team folder not yet created) returns an empty list rather than throwing.
    /// </summary>
    /// <param name="folder">The already-resolved folder to list.</param>
    /// <param name="ct">Cancels the listing.</param>
    /// <returns>The visible children, in display order.</returns>
    internal Task<IReadOnlyList<LibraryEntry>> ListAsync(LibraryPath folder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ct.ThrowIfCancellationRequested();

        _ = this.recycleBin;

        IReadOnlyList<string> ignoredFolders = this.options.Value.FileChanges.EffectiveIgnore;
        bool hideUnderscoreFolders = folder.Root.Kind == LibraryRootKind.Teams;

        List<FileSystemInfo> children;
        try
        {
            children = [.. new DirectoryInfo(folder.FullPath).EnumerateFileSystemInfos()];
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<IReadOnlyList<LibraryEntry>>([]);
        }

        List<LibraryEntry> entries = [];
        foreach (FileSystemInfo child in children)
        {
            bool isFolder = child is DirectoryInfo;
            if (isFolder && IsHiddenFolder(child.Name, ignoredFolders, hideUnderscoreFolders))
            {
                continue;
            }

            if (!this.resolver.TryResolveChild(folder, child, out LibraryPath? childPath, out string? error))
            {
                if (this.logger.IsEnabled(LogLevel.Debug))
                {
                    this.logger.LogDebug("Library child '{ChildName}' under '{ParentPath}' skipped: {Reason}", child.Name, folder.FullPath, error);
                }

                continue;
            }

            long length = child is FileInfo file ? file.Length : 0;
            entries.Add(new LibraryEntry(childPath, isFolder, length, new DateTimeOffset(child.LastWriteTimeUtc, TimeSpan.Zero)));
        }

        entries.Sort(CompareEntries);
        return Task.FromResult<IReadOnlyList<LibraryEntry>>(entries);
    }

    /// <summary>Folders before files, then <see cref="StringComparer.OrdinalIgnoreCase"/> by name.</summary>
    private static int CompareEntries(LibraryEntry left, LibraryEntry right)
    {
        if (left.IsFolder != right.IsFolder)
        {
            return left.IsFolder ? -1 : 1;
        }

        return string.Compare(EntryName(left), EntryName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string EntryName(LibraryEntry entry) => Path.GetFileName(entry.Path.FullPath);

    /// <summary>Whether a folder name is hidden: always-hidden, in the effective ignore list, or (Teams root only) underscore-prefixed.</summary>
    private static bool IsHiddenFolder(string name, IReadOnlyList<string> ignoredFolders, bool hideUnderscoreFolders)
    {
        if (Array.Exists(AlwaysHiddenFolders, hidden => string.Equals(hidden, name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (hideUnderscoreFolders && name.StartsWith('_'))
        {
            return true;
        }

        foreach (string ignored in ignoredFolders)
        {
            if (string.Equals(ignored, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
