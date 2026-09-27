using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
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

    /// <summary>The most of a file read up front to detect its kind (Spec §6.4).</summary>
    private const int DetectionHeadBytes = 8192;

    /// <summary>Settled text (corrections-B4 item 11) for a text file over <see cref="LibraryOptions.MaxEditableBytes"/>.</summary>
    private const string TooLargeToEditReason = "This file is too large to edit here.";

    /// <summary>Settled text for content that fails to decode as text (Spec §10 E-5).</summary>
    private const string UnsupportedTextFormatReason = "Unsupported text format";

    /// <summary>Settled text (Plan 6.5.i; corrections-B4 item 13) for a write re-check that finds the file no
    /// longer editable: not Markdown, or grown past <see cref="LibraryOptions.MaxEditableBytes"/> since it was
    /// opened for editing.</summary>
    private const string ViewOnlyWriteRefusalReason = "This file can't be edited here.";

    /// <summary>Settled text (corrections-B4 item 15) for an atomic write whose final <see cref="File.Move(string, string, bool)"/>
    /// failed: the target was held open without <see cref="FileShare.Delete"/>, or is read-only.</summary>
    private static readonly CompositeFormat SaveFailedReasonFormat = CompositeFormat.Parse("Couldn't save {0}: it's in use or read-only.");

    /// <summary>Settled text (Spec §6.12; corrections-B4 item 18): a Teammate definition's frontmatter Name
    /// changed, and the Teammates page is the only rename path.</summary>
    private const string RenameOnTeammatesPageReason = "Rename teammates on the Teammates page.";

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

    /// <summary>
    /// Reads <paramref name="file"/> (Spec §6.4 <c>ReadTextAsync</c>): decodes text kinds with
    /// <see cref="TextFileCodec"/>, records the length and last-write time from the open handle
    /// before reading any content (§6.8), and refuses to read a text file in full above
    /// <see cref="LibraryOptions.MaxEditableBytes"/>.
    /// </summary>
    /// <param name="file">The file to read; re-resolved against the current tree before use (corrections-B4 item 12).</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The document's content, kind and editability.</returns>
    /// <exception cref="FileNotFoundException">The file, or a parent folder, no longer exists (Spec §10 E-3).</exception>
    internal async Task<LibraryDocumentContent> ReadAsync(LibraryPath file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(file.Root.Id, file.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            throw new FileNotFoundException(resolveError, file.FullPath);
        }

        FileStream stream;
        try
        {
            stream = new FileStream(fresh.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new FileNotFoundException(ex.Message, fresh.FullPath, ex);
        }

        await using (stream)
        {
            long length = stream.Length;
            DateTimeOffset lastWriteUtc = new(File.GetLastWriteTimeUtc(stream.SafeFileHandle), TimeSpan.Zero);

            string fileName = Path.GetFileName(fresh.FullPath);
            (byte[] head, LibraryFileKind kind) = await DetectKindAsync(stream, fileName, length, ct);

            if (kind is LibraryFileKind.Image or LibraryFileKind.Other)
            {
                return new LibraryDocumentContent(fresh, kind, null, null, false, null, length, lastWriteUtc);
            }

            if (length > this.options.Value.Library.MaxEditableBytes)
            {
                return new LibraryDocumentContent(fresh, kind, null, null, false, TooLargeToEditReason, length, lastWriteUtc);
            }

            byte[] content;
            if (length == head.Length)
            {
                content = head;
            }
            else
            {
                content = new byte[length];
                Array.Copy(head, content, head.Length);
                await stream.ReadExactlyAsync(content.AsMemory(head.Length), ct);
            }

            if (!TextFileCodec.TryDecode(content, LineEnding.CrLf, out string? text, out TextFileFormat? format))
            {
                return new LibraryDocumentContent(fresh, kind, null, null, false, UnsupportedTextFormatReason, length, lastWriteUtc);
            }

            bool editable = kind == LibraryFileKind.Markdown;
            return new LibraryDocumentContent(fresh, kind, text, format, editable, null, length, lastWriteUtc);
        }
    }

    /// <summary>
    /// Writes <paramref name="editorText"/> to <paramref name="file"/> (Spec §6.4 <c>WriteTextAsync</c>;
    /// ADR-0028): re-resolves the path first (corrections-B4 item 12, never trusting the caller's
    /// <c>FullPath</c>), re-checks editability at write time (item 13), refuses a Teammate definition's
    /// frontmatter Name change (Spec §6.12, item 18), then writes atomically through
    /// <see cref="AtomicFile"/> (item 15). A missing parent folder is created lazily only when it resolves as
    /// a Team or Project folder (Spec §6.2; item 17).
    /// </summary>
    /// <param name="file">The file to write; re-resolved against the current tree before use.</param>
    /// <param name="editorText">The editor's text, with <c>\n</c>-only newlines.</param>
    /// <param name="format">The recorded encoding, BOM and line ending from <see cref="ReadAsync"/>.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The fresh <see cref="LibraryEntry"/> (item 14) on success, or a refusal reason.</returns>
    /// <exception cref="FileNotFoundException">A missing parent folder is not a Team or Project folder.</exception>
    internal async Task<LibraryResult<LibraryEntry>> WriteTextAsync(LibraryPath file, string editorText, TextFileFormat format, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(editorText);
        ArgumentNullException.ThrowIfNull(format);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(file.Root.Id, file.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return new LibraryResult<LibraryEntry>(null, resolveError);
        }

        string fileName = Path.GetFileName(fresh.FullPath);

        if (!File.Exists(fresh.FullPath))
        {
            this.EnsureParentFolderCreated(fresh);
        }
        else
        {
            LibraryFileKind kind;
            long currentLength;
            using (FileStream probe = new(fresh.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                currentLength = probe.Length;
                (_, kind) = await DetectKindAsync(probe, fileName, currentLength, ct);
            }

            bool editable = kind == LibraryFileKind.Markdown;
            if (!editable || currentLength > this.options.Value.Library.MaxEditableBytes)
            {
                return new LibraryResult<LibraryEntry>(null, ViewOnlyWriteRefusalReason);
            }

            if (fresh.Role == LibraryNodeRole.TeammateDefinition)
            {
                string oldText = await File.ReadAllTextAsync(fresh.FullPath, ct);
                if (TeammateNameChanged(oldText, editorText))
                {
                    return new LibraryResult<LibraryEntry>(null, RenameOnTeammatesPageReason);
                }
            }
        }

        byte[] bytes;
        try
        {
            bytes = TextFileCodec.Encode(editorText, format);
        }
        catch (NotSupportedException ex)
        {
            return new LibraryResult<LibraryEntry>(null, ex.Message);
        }

        bool saved = await AtomicFile.TryWriteAsync(fresh.FullPath, bytes, ct);
        if (!saved)
        {
            return new LibraryResult<LibraryEntry>(null, string.Format(CultureInfo.InvariantCulture, SaveFailedReasonFormat, fileName));
        }

        DateTimeOffset lastWriteUtc = new(File.GetLastWriteTimeUtc(fresh.FullPath), TimeSpan.Zero);
        LibraryEntry entry = new(fresh, false, bytes.Length, lastWriteUtc);
        return new LibraryResult<LibraryEntry>(entry, null);
    }

    /// <summary>Reads the first <see cref="DetectionHeadBytes"/> of <paramref name="stream"/> (already
    /// positioned at its start) and detects its <see cref="LibraryFileKind"/>. Shared by <see cref="ReadAsync"/>
    /// and <see cref="WriteTextAsync"/>'s write-time re-check, so the two never duplicate the sniff.</summary>
    private static async Task<(byte[] Head, LibraryFileKind Kind)> DetectKindAsync(FileStream stream, string fileName, long length, CancellationToken ct)
    {
        int headLength = (int)Math.Min(DetectionHeadBytes, length);
        byte[] head = new byte[headLength];
        await stream.ReadExactlyAsync(head, ct);
        LibraryFileKind kind = LibraryFileKinds.Detect(fileName, head);
        return (head, kind);
    }

    /// <summary>
    /// Creates <paramref name="file"/>'s parent folder lazily, but only when the parent itself resolves as a
    /// <see cref="LibraryNodeRole.TeamFolder"/> or <see cref="LibraryNodeRole.ProjectFolder"/> (corrections-B4
    /// item 17: the parent's role, resolved fresh, decides this - never the caller's).
    /// </summary>
    /// <param name="file">The file whose parent folder is checked and, if eligible, created.</param>
    /// <exception cref="FileNotFoundException">The parent does not resolve, or is not a Team or Project folder.</exception>
    private void EnsureParentFolderCreated(LibraryPath file)
    {
        string parentRelativePath = ParentRelativePath(file.RelativePath);
        if (!this.resolver.TryResolve(file.Root.Id, parentRelativePath, out LibraryPath? parent, out string? parentError))
        {
            throw new FileNotFoundException(parentError, file.FullPath);
        }

        if (Directory.Exists(parent.FullPath))
        {
            return;
        }

        if (parent.Role is not (LibraryNodeRole.TeamFolder or LibraryNodeRole.ProjectFolder))
        {
            throw new FileNotFoundException($"'{parent.FullPath}' does not exist.", file.FullPath);
        }

        Directory.CreateDirectory(parent.FullPath);
    }

    /// <summary>The relative path one folder up from <paramref name="relativePath"/>, or the root
    /// (<see cref="string.Empty"/>) when <paramref name="relativePath"/> names a direct child of the root.</summary>
    private static string ParentRelativePath(string relativePath)
    {
        int lastSeparator = relativePath.LastIndexOf('/');
        return lastSeparator < 0 ? string.Empty : relativePath[..lastSeparator];
    }

    /// <summary>
    /// Whether a Teammate definition's frontmatter Name differs between its current on-disk text and the text
    /// about to be saved, compared <see cref="StringComparison.Ordinal"/> (corrections-B4 item 18). Either text
    /// failing to parse an identity is not treated as a change here; <see cref="TextFileCodec"/> and the caller
    /// have already accepted the bytes as text.
    /// </summary>
    private static bool TeammateNameChanged(string oldText, string newText)
    {
        if (!PersonaFrontmatter.TryReadIdentity(oldText, out PersonaIdentity? oldIdentity, out _)
            || !PersonaFrontmatter.TryReadIdentity(newText, out PersonaIdentity? newIdentity, out _))
        {
            return false;
        }

        return !string.Equals(oldIdentity.Name, newIdentity.Name, StringComparison.Ordinal);
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
