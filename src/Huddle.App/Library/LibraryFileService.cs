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
/// <param name="index">The D8 wikilink index, invalidated after every mutation and queried by
/// <see cref="PreviewLinkChangesAsync"/>, <see cref="RenameAsync"/> and <see cref="MoveAsync"/>.</param>
/// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="TeamOptions.FileChanges"/>'s ignore list.</param>
/// <param name="logger">Logs unexpected failures.</param>
/// <param name="beforeRewriteWriteForTests">The D8 rewrite-loop test seam (8.5.i-b): stored here, invoked
/// by the rewrite loop after a linking note's rewritten text is computed and before it is written.
/// <see langword="null"/> for the ordinary path.</param>
internal sealed class LibraryFileService(
    LibraryPathResolver resolver,
    IRecycleBin recycleBin,
    WikiLinkIndex index,
    IOptions<TeamOptions> options,
    ILogger<LibraryFileService> logger,
    Func<LibraryPath, Task>? beforeRewriteWriteForTests = null) : IDisposable
{
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

    /// <summary>Settled text (corrections-B4 item 19) when a create target's name is already taken by a file
    /// or a folder.</summary>
    private static readonly CompositeFormat AlreadyExistsReasonFormat = CompositeFormat.Parse("\"{0}\" already exists here.");

    /// <summary>Settled text (corrections-B4 item 19) for a create failure that is not an existence clash.
    /// Internal, not private: <see cref="TeamFolderProvisioner.EnsureProject"/> reuses this single
    /// definition rather than a copy.</summary>
    internal static readonly CompositeFormat CouldNotCreateReasonFormat = CompositeFormat.Parse("Couldn't create {0}.");

    /// <summary>Settled text (corrections-B4 item 21) refusing a create directly at the Teammates root.</summary>
    private const string CreateAtTeammatesRootReason = "Add teammates on the Teammates page.";

    /// <summary>Settled text (corrections-B4 item 21) refusing a <c>.md</c> file created directly in a
    /// Teammate folder.</summary>
    private const string CreateMdInTeammateFolderReason = "A teammate folder holds only its definition; use work/.";

    /// <summary>The default extension for a new note whose name has no known Spec §6.11 extension (corrections-B4 item 23).</summary>
    private const string DefaultNoteExtension = ".md";

    /// <summary>Settled text (Spec §6.4, settled here) refusing a move across Library roots.</summary>
    private const string MoveAcrossRootsReason = "Items can't be moved between Library roots.";

    /// <summary>Settled text (Spec §6.4, settled here; corrections-B4 item 25) refusing a folder moved into
    /// its own subtree, including into itself.</summary>
    private const string MoveIntoOwnSubfolderReason = "A folder can't be moved into itself.";

    /// <summary>Settled text (corrections-B4 item 28) refusing a move that would turn a subtree's <c>_tasks</c>
    /// folder into live Tasks at the wrong depth.</summary>
    private const string MoveTasksSubtreeReason = "Folders that hold tasks can't be moved here.";

    /// <summary>Settled text (corrections-B4 item 27) for a rename or move whose final
    /// <see cref="File.Move(string, string)"/>/<see cref="Directory.Move(string, string)"/> failed because
    /// something inside the source was held open without <see cref="FileShare.Delete"/>.</summary>
    private static readonly CompositeFormat CouldNotMoveReasonFormat = CompositeFormat.Parse("Couldn't move {0}: something inside it is in use.");

    /// <summary>The reserved folder name that marks a Team or Project folder's Tasks subtree (corrections-B4 item 28).</summary>
    private const string TasksFolderName = "_tasks";

    /// <summary>Settled text (corrections-B5 D8 item 10) for a linking note over
    /// <see cref="LibraryOptions.MaxEditableBytes"/>, or otherwise no longer editable, found by the rewrite loop.</summary>
    private static readonly CompositeFormat NoteTooLargeToUpdateReasonFormat = CompositeFormat.Parse("{0} is too large to update.");

    /// <summary>Settled text (corrections-B5 D8 item 10) for a linking note whose Length or LastWriteUtc no
    /// longer match what the rewrite loop read, so it is left unwritten.</summary>
    private static readonly CompositeFormat NoteChangedDuringRenameReasonFormat = CompositeFormat.Parse("{0} changed during the rename; update its links by hand.");

    /// <summary>Settled text (corrections-B5 D8 items 10-11) for a linking note with <see cref="TextFileFormat.MixedLineEndings"/>: ADR-0028 byte-exactness wins, so it is skipped rather than normalised.</summary>
    private static readonly CompositeFormat NoteMixedLineEndingsReasonFormat = CompositeFormat.Parse("{0} has mixed line endings; update its links by hand.");

    /// <summary>Settled text (corrections-B5 D8 items 10, 12) for a linking note that is a Teammate definition: rewriting it would restart the teammate and clear its memory.</summary>
    private static readonly CompositeFormat NoteIsTeammateDefinitionReasonFormat = CompositeFormat.Parse("{0} is a teammate definition; update its links by hand.");

    private readonly LibraryPathResolver resolver = resolver;
    private readonly IRecycleBin recycleBin = recycleBin;
    private readonly WikiLinkIndex index = index;
    private readonly IOptions<TeamOptions> options = options;
    private readonly ILogger<LibraryFileService> logger = logger;

    /// <summary>The D8 rewrite-loop test seam (corrections-B5 D8 item 10): invoked by
    /// <see cref="ApplyNoteRewriteAsync"/> after a linking note's rewritten text is computed and before it is
    /// written, so a test can mutate the note on disk to prove the stamp check without a race.</summary>
    private readonly Func<LibraryPath, Task>? beforeRewriteWriteForTests = beforeRewriteWriteForTests;

    /// <summary>Serialises rename and move (with their D8 rewrite loop) so two never interleave
    /// (corrections-B5 D8 item 10). An instance field: <see cref="LibraryFileService"/> owns it and disposes
    /// it via <see cref="Dispose"/>; <c>Write_OnlyLibraryPaths_EveryMethodTakesLibraryPathFirst</c>'s
    /// LibraryPath-first invariant carves out <see cref="Dispose"/> by name and arity.</summary>
    private readonly SemaphoreSlim moveGate = new(1, 1);

    private bool disposed;

    /// <summary>Disposes <see cref="moveGate"/>. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.moveGate.Dispose();
        this.disposed = true;
    }

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
            if (isFolder && LibraryHiddenFolders.IsHidden(child.Name, ignoredFolders, hideUnderscoreFolders))
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
    /// Files and folders under <paramref name="scope"/> whose name contains <paramref name="term"/>, ignoring
    /// case (Spec §6.8). Re-resolves <paramref name="scope"/> first, then walks breadth-first through
    /// <see cref="ListAsync"/>, so every hiding rule the tree applies applies here too. Stops after
    /// <paramref name="max"/> hits, or after <see cref="LibraryOptions.MaxIndexedFiles"/> entries are visited.
    /// </summary>
    /// <param name="scope">The folder to search under; never itself a hit.</param>
    /// <param name="term">The text to look for in a name; trimmed, and a blank term finds nothing.</param>
    /// <param name="max">The most hits to return; zero or less finds nothing.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The hits in walk order, and whether more could have been found.</returns>
    internal async Task<LibrarySearchResult> FindAsync(LibraryPath scope, string term, int max, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(term);

        string needle = term.Trim();
        if (needle.Length == 0 || max <= 0 || !this.resolver.TryResolve(scope.Root.Id, scope.RelativePath, out LibraryPath? fresh, out _))
        {
            return new LibrarySearchResult([], false);
        }

        int visitLimit = this.options.Value.Library.MaxIndexedFiles;
        int visited = 0;
        List<LibraryEntry> hits = [];
        Queue<LibraryPath> pending = new();
        HashSet<string> visitedFolders = new(FolderSnapshot.PathComparer) { fresh.FullPath };
        pending.Enqueue(fresh);
        while (pending.TryDequeue(out LibraryPath? folder))
        {
            IReadOnlyList<LibraryEntry> children;
            try
            {
                children = await this.ListAsync(folder, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be listed (locked, or vanished mid-walk) is skipped so one bad folder does not fail the whole search.
                continue;
            }

            foreach (LibraryEntry entry in children)
            {
                if (visited >= visitLimit)
                {
                    return new LibrarySearchResult(hits, true);
                }

                visited++;
                if (IsNameMatch(entry.Path, needle))
                {
                    hits.Add(entry);
                    if (hits.Count > max)
                    {
                        return new LibrarySearchResult(hits.GetRange(0, max), true);
                    }
                }

                if (entry.IsFolder && visitedFolders.Add(entry.Path.FullPath))
                {
                    pending.Enqueue(entry.Path);
                }
            }
        }

        return new LibrarySearchResult(hits, false);
    }

    /// <summary>Whether the last segment of <paramref name="path"/>'s <c>RelativePath</c> (the name the tree shows, never the link target's <c>FullPath</c>) contains <paramref name="needle"/>, ignoring case.</summary>
    /// <param name="path">The entry's path.</param>
    /// <param name="needle">The already-trimmed, non-empty text to look for.</param>
    private static bool IsNameMatch(LibraryPath path, string needle)
    {
        string relative = path.RelativePath;
        return relative[(relative.LastIndexOf('/') + 1)..].Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stats <paramref name="path"/> without reading its content (corrections-B6 item 11: the
    /// save-time freshness check). Re-resolves the path first, never trusting the caller's
    /// <c>FullPath</c>; a missing file, or an <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/>
    /// while stating it, both report as "not there" rather than throwing.
    /// </summary>
    /// <param name="path">The file to stat; re-resolved against the current tree before use.</param>
    /// <param name="ct">Cancels the stat.</param>
    /// <returns>The file's current entry, or <see langword="null"/> when it cannot be stated.</returns>
    internal Task<LibraryEntry?> StatAsync(LibraryPath path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(path);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(path.Root.Id, path.RelativePath, out LibraryPath? fresh, out _))
        {
            return Task.FromResult<LibraryEntry?>(null);
        }

        try
        {
            FileInfo file = new(fresh.FullPath);
            if (!file.Exists)
            {
                return Task.FromResult<LibraryEntry?>(null);
            }

            LibraryEntry entry = new(fresh, false, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));
            return Task.FromResult<LibraryEntry?>(entry);
        }
        catch (IOException)
        {
            return Task.FromResult<LibraryEntry?>(null);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<LibraryEntry?>(null);
        }
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

            if (kind == LibraryFileKind.Image
                || (kind == LibraryFileKind.Other && !LibraryFileKinds.IsTextExtension(fileName)))
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
    /// Reads an image for a Prompt block (design §6.3): re-resolves the path against the current
    /// roots, confirms by magic bytes that it is a raster image, refuses it over
    /// <paramref name="maxBytes"/> before reading it and again while reading, and refuses a header whose
    /// longest side is over <paramref name="maxEdgePixels"/>. SVG is never an image here.
    /// </summary>
    /// <param name="file">The file to read; re-resolved before use, so a stale path is never trusted.</param>
    /// <param name="maxBytes">The most bytes accepted; zero or less accepts nothing.</param>
    /// <param name="maxEdgePixels">The longest side accepted, in pixels.</param>
    /// <param name="ct">Cancels the read; cancellation propagates and is not a refusal.</param>
    /// <returns>The image, or a refusal saying why it will not be sent.</returns>
    internal async Task<LibraryImageResult> ReadImageAsync(LibraryPath file, int maxBytes, int maxEdgePixels, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ct.ThrowIfCancellationRequested();

        if (maxBytes < 1)
        {
            return new LibraryImageRefused(LibraryImageRefusal.TooLarge);
        }

        if (!this.resolver.TryResolve(file.Root.Id, file.RelativePath, out LibraryPath? fresh, out _) || !fresh.IsFile)
        {
            return new LibraryImageRefused(LibraryImageRefusal.Unreadable);
        }

        try
        {
            await using FileStream stream = new(fresh.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maxBytes)
            {
                return new LibraryImageRefused(LibraryImageRefusal.TooLarge);
            }

            using MemoryStream buffer = new((int)stream.Length);
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return new LibraryImageRefused(LibraryImageRefusal.TooLarge);
                }

                buffer.Write(chunk, 0, read);
            }

            byte[] bytes = buffer.ToArray();
            string? mimeType = LibraryFileKinds.ImageContentType(bytes);
            if (mimeType is null || !ImageHeader.TryReadSize(bytes, out int width, out int height))
            {
                return new LibraryImageRefused(LibraryImageRefusal.NotAnImage);
            }

            return Math.Max(width, height) > maxEdgePixels
                ? new LibraryImageRefused(LibraryImageRefusal.TooManyPixels)
                : new LibraryImageRead(mimeType, bytes, width, height);
        }
        catch (IOException)
        {
            return new LibraryImageRefused(LibraryImageRefusal.Unreadable);
        }
        catch (UnauthorizedAccessException)
        {
            return new LibraryImageRefused(LibraryImageRefusal.Unreadable);
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

        InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);

        DateTimeOffset lastWriteUtc = new(File.GetLastWriteTimeUtc(fresh.FullPath), TimeSpan.Zero);
        LibraryEntry entry = new(fresh, false, bytes.Length, lastWriteUtc);
        return new LibraryResult<LibraryEntry>(entry, null);
    }

    /// <summary>
    /// Creates a new note under <paramref name="folder"/> (Spec §6.4 create row): a name with no known
    /// Spec §6.11 extension gets <c>.md</c> appended (corrections-B4 item 23), otherwise it is kept as-is.
    /// Validated with <see cref="LibraryNames.Validate"/>, refused directly at the Teammates root or for a
    /// <c>.md</c> file directly in a Teammate folder (item 21). Uses <see cref="FileMode.CreateNew"/> so a
    /// race becomes an <see cref="IOException"/>, resolved to the settled exists text (item 19). The returned
    /// path is re-resolved after creation (item 22).
    /// </summary>
    /// <param name="folder">The already-resolved folder to create the file in; re-resolved before use.</param>
    /// <param name="name">The new file's name, without regard to its final extension.</param>
    /// <param name="ct">Cancels the create.</param>
    /// <returns>The fresh <see cref="LibraryPath"/> on success, or a refusal reason.</returns>
    internal async Task<LibraryResult<LibraryPath>> CreateFileAsync(LibraryPath folder, string name, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        ct.ThrowIfCancellationRequested();

        string? nameError = LibraryNames.Validate(name);
        if (nameError is not null)
        {
            return new LibraryResult<LibraryPath>(null, nameError);
        }

        if (!this.resolver.TryResolve(folder.Root.Id, folder.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return new LibraryResult<LibraryPath>(null, resolveError);
        }

        string finalName = LibraryFileKinds.HasKnownExtension(name) ? name : name + DefaultNoteExtension;

        string? teammatesError = TeammatesDestinationRefusal(fresh, finalName);
        if (teammatesError is not null)
        {
            return new LibraryResult<LibraryPath>(null, teammatesError);
        }

        if (!this.TryResolveCreateTarget(fresh, finalName, out LibraryPath? target, out string? targetError))
        {
            return new LibraryResult<LibraryPath>(null, targetError);
        }

        try
        {
            using FileStream stream = new(target.FullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.FlushAsync(ct);
        }
        catch (IOException)
        {
            return new LibraryResult<LibraryPath>(null, CreateFailureReason(finalName, target.FullPath));
        }
        catch (UnauthorizedAccessException)
        {
            return new LibraryResult<LibraryPath>(null, CreateFailureReason(finalName, target.FullPath));
        }

        InvalidateOverlapping(this.resolver, this.index, target.FullPath);
        return this.ReResolveCreated(fresh, finalName);
    }

    /// <summary>
    /// Creates a new folder under <paramref name="folder"/> (Spec §6.4 create row). Existence is checked
    /// BEFORE <see cref="Directory.CreateDirectory(string)"/> (corrections-B4 item 20: it is idempotent and
    /// can't signal "exists" on its own). Refused directly at the Teammates root (item 21). The returned path
    /// is re-resolved after creation (item 22).
    /// </summary>
    /// <param name="folder">The already-resolved folder to create the new folder in; re-resolved before use.</param>
    /// <param name="name">The new folder's name.</param>
    /// <param name="ct">Cancels the create.</param>
    /// <returns>The fresh <see cref="LibraryPath"/> on success, or a refusal reason.</returns>
    internal Task<LibraryResult<LibraryPath>> CreateFolderAsync(LibraryPath folder, string name, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);
        ct.ThrowIfCancellationRequested();

        string? nameError = LibraryNames.Validate(name);
        if (nameError is not null)
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, nameError));
        }

        if (!this.resolver.TryResolve(folder.Root.Id, folder.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, resolveError));
        }

        string? teammatesError = TeammatesDestinationRefusal(fresh, null);
        if (teammatesError is not null)
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, teammatesError));
        }

        if (!this.TryResolveCreateTarget(fresh, name, out LibraryPath? target, out string? targetError))
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, targetError));
        }

        if (Directory.Exists(target.FullPath) || File.Exists(target.FullPath))
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, name)));
        }

        try
        {
            Directory.CreateDirectory(target.FullPath);
        }
        catch (IOException)
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, CouldNotCreateReasonFormat, name)));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, CouldNotCreateReasonFormat, name)));
        }

        InvalidateOverlapping(this.resolver, this.index, target.FullPath);
        return Task.FromResult(this.ReResolveCreated(fresh, name));
    }

    /// <summary>
    /// Renames <paramref name="item"/> within its own folder (Spec §6.4 rename/move row, minus the link
    /// rewrite (D8)). Checked in order: re-resolve, <see cref="LibraryNames.Validate"/>,
    /// <see cref="LibraryProtection.For"/>, the Teammates destination rules (item 21/26, shared with
    /// <see cref="CreateFileAsync"/>), then existence. A rename that only changes case
    /// (<see cref="FolderSnapshot.PathComparer"/> treats old and new as equal, item 24) routes through a
    /// temporary sibling so the platform's case-insensitive filesystem doesn't treat it as a no-op; the
    /// rename back out of the temporary sibling is itself guarded so a failure there leaves a known state.
    /// </summary>
    /// <param name="item">The already-resolved item to rename; re-resolved before use.</param>
    /// <param name="newName">The item's new name, in the same folder.</param>
    /// <param name="ct">Cancels the rename.</param>
    /// <returns>The move result (D8 fills its lists) on success, or a refusal reason.</returns>
    internal async Task<LibraryResult<LibraryMoveResult>> RenameAsync(LibraryPath item, string newName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(newName);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return new LibraryResult<LibraryMoveResult>(null, resolveError);
        }

        string parentRelativePath = ParentRelativePath(fresh.RelativePath);
        if (!this.resolver.TryResolve(fresh.Root.Id, parentRelativePath, out LibraryPath? parent, out string? parentError))
        {
            return new LibraryResult<LibraryMoveResult>(null, parentError);
        }

        string? nameError = LibraryNames.Validate(newName);
        if (nameError is not null)
        {
            return new LibraryResult<LibraryMoveResult>(null, nameError);
        }

        LibraryProtection protection = LibraryProtection.For(fresh);
        if (!protection.CanRename)
        {
            return new LibraryResult<LibraryMoveResult>(null, protection.Reason);
        }

        string? teammatesError = TeammatesDestinationRefusal(parent, newName);
        if (teammatesError is not null)
        {
            return new LibraryResult<LibraryMoveResult>(null, teammatesError);
        }

        if (!this.TryResolveCreateTarget(parent, newName, out LibraryPath? target, out string? targetError))
        {
            return new LibraryResult<LibraryMoveResult>(null, targetError);
        }

        bool caseOnly = !string.Equals(fresh.FullPath, target.FullPath, StringComparison.Ordinal)
            && FolderSnapshot.PathComparer.Equals(fresh.FullPath, target.FullPath);

        if (!caseOnly && (Directory.Exists(target.FullPath) || File.Exists(target.FullPath)))
        {
            return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, newName));
        }

        bool isFolder = fresh.Role != LibraryNodeRole.File;
        string name = Path.GetFileName(fresh.FullPath);

        await this.moveGate.WaitAsync(ct);
        try
        {
            InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);
            bool indexAvailable = this.index.IsAvailable(fresh.Root.Id);
            IReadOnlyList<string> preMoveFiles = indexAvailable ? this.index.FilesUnder(fresh.Root.Id, string.Empty) : [];

            try
            {
                if (caseOnly)
                {
                    string tempPath = fresh.FullPath + ".renaming-" + Guid.NewGuid().ToString("N");
                    MoveEntry(fresh.FullPath, tempPath, isFolder);
                    try
                    {
                        MoveEntry(tempPath, target.FullPath, isFolder);
                    }
                    catch (IOException)
                    {
                        MoveEntry(tempPath, fresh.FullPath, isFolder);
                        throw;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        MoveEntry(tempPath, fresh.FullPath, isFolder);
                        throw;
                    }
                }
                else
                {
                    MoveEntry(fresh.FullPath, target.FullPath, isFolder);
                }
            }
            catch (IOException)
            {
                return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name));
            }
            catch (UnauthorizedAccessException)
            {
                return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name));
            }

            InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);
            InvalidateOverlapping(this.resolver, this.index, target.FullPath);

            if (!indexAvailable)
            {
                return WithLinksNotUpdated(this.ReResolveMoved(parent, newName, [], []), true);
            }

            (IReadOnlyList<string> rewrittenNotes, IReadOnlyList<LibraryNoteFailure> failedNotes) = await this.RewriteLinksAfterMoveAsync(
                target, fresh.RelativePath, target.RelativePath, isFolder, preMoveFiles);

            return this.ReResolveMoved(parent, newName, rewrittenNotes, failedNotes);
        }
        finally
        {
            this.moveGate.Release();
        }
    }

    /// <summary>
    /// Moves <paramref name="item"/> into <paramref name="targetFolder"/> (Spec §6.4 rename/move row, minus
    /// the link rewrite (D8)). Checked in order: re-resolve both paths, <see cref="LibraryProtection.For"/>,
    /// same root (settled here), into its own subtree (settled here; corrections-B4 item 25, compared on
    /// resolved full paths with <see cref="FolderSnapshot.PathComparer"/>), the Teammates destination rules
    /// (item 21/26, shared with <see cref="CreateFileAsync"/>), the <c>_tasks</c> subtree rule under the Teams
    /// root (item 28), then existence.
    /// </summary>
    /// <param name="item">The already-resolved item to move; re-resolved before use.</param>
    /// <param name="targetFolder">The already-resolved destination folder; re-resolved before use.</param>
    /// <param name="ct">Cancels the move.</param>
    /// <returns>The move result (D8 fills its lists) on success, or a refusal reason.</returns>
    internal async Task<LibraryResult<LibraryMoveResult>> MoveAsync(LibraryPath item, LibraryPath targetFolder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(targetFolder);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return new LibraryResult<LibraryMoveResult>(null, resolveError);
        }

        if (!this.resolver.TryResolve(targetFolder.Root.Id, targetFolder.RelativePath, out LibraryPath? freshTarget, out string? targetResolveError))
        {
            return new LibraryResult<LibraryMoveResult>(null, targetResolveError);
        }

        LibraryProtection protection = LibraryProtection.For(fresh);
        if (!protection.CanMove)
        {
            return new LibraryResult<LibraryMoveResult>(null, protection.Reason);
        }

        if (!string.Equals(fresh.Root.Id, freshTarget.Root.Id, StringComparison.Ordinal))
        {
            return new LibraryResult<LibraryMoveResult>(null, MoveAcrossRootsReason);
        }

        string itemFullPath = Path.TrimEndingDirectorySeparator(fresh.FullPath);
        string targetFullPath = Path.TrimEndingDirectorySeparator(freshTarget.FullPath);

        if (fresh.Role == LibraryNodeRole.Folder && IsSameOrWithin(targetFullPath, itemFullPath))
        {
            return new LibraryResult<LibraryMoveResult>(null, MoveIntoOwnSubfolderReason);
        }

        string name = Path.GetFileName(itemFullPath);

        string? teammatesError = TeammatesDestinationRefusal(freshTarget, name);
        if (teammatesError is not null)
        {
            return new LibraryResult<LibraryMoveResult>(null, teammatesError);
        }

        if (fresh.Role == LibraryNodeRole.Folder && fresh.Root.Kind == LibraryRootKind.Teams && ContainsTasksSubtree(itemFullPath))
        {
            return new LibraryResult<LibraryMoveResult>(null, MoveTasksSubtreeReason);
        }

        if (!this.TryResolveCreateTarget(freshTarget, name, out LibraryPath? target, out string? targetError))
        {
            return new LibraryResult<LibraryMoveResult>(null, targetError);
        }

        if (Directory.Exists(target.FullPath) || File.Exists(target.FullPath))
        {
            return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, name));
        }

        bool isFolder = fresh.Role != LibraryNodeRole.File;

        await this.moveGate.WaitAsync(ct);
        try
        {
            InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);
            bool indexAvailable = this.index.IsAvailable(fresh.Root.Id);
            IReadOnlyList<string> preMoveFiles = indexAvailable ? this.index.FilesUnder(fresh.Root.Id, string.Empty) : [];

            try
            {
                MoveEntry(fresh.FullPath, target.FullPath, isFolder);
            }
            catch (IOException)
            {
                return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name));
            }
            catch (UnauthorizedAccessException)
            {
                return new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name));
            }

            InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);
            InvalidateOverlapping(this.resolver, this.index, target.FullPath);

            if (!indexAvailable)
            {
                return WithLinksNotUpdated(this.ReResolveMoved(freshTarget, name, [], []), true);
            }

            (IReadOnlyList<string> rewrittenNotes, IReadOnlyList<LibraryNoteFailure> failedNotes) = await this.RewriteLinksAfterMoveAsync(
                target, fresh.RelativePath, target.RelativePath, isFolder, preMoveFiles);

            return this.ReResolveMoved(freshTarget, name, rewrittenNotes, failedNotes);
        }
        finally
        {
            this.moveGate.Release();
        }
    }

    /// <summary>
    /// Sends <paramref name="item"/> to the OS recycle bin (Spec §6.4 recycle row, §10 E-8). Checked in
    /// order: re-resolve, <see cref="LibraryProtection.For"/>, then <see cref="IRecycleBin.TrySend"/>. The
    /// service never deletes anything itself; a refusal from the bin passes its <c>error</c> through
    /// unchanged, since <see cref="IRecycleBin"/> implementations already return the settled,
    /// user-facing text for each of their own refusal reasons.
    /// </summary>
    /// <param name="item">The already-resolved item to recycle; re-resolved before use.</param>
    /// <param name="ct">Cancels the recycle.</param>
    /// <returns>The resolved path that was sent to the bin, or a refusal reason.</returns>
    internal Task<LibraryResult<LibraryPath>> RecycleAsync(LibraryPath item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, resolveError));
        }

        LibraryProtection protection = LibraryProtection.For(fresh);
        if (!protection.CanDelete)
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, protection.Reason));
        }

        if (!this.recycleBin.TrySend(fresh.FullPath, out string? recycleError))
        {
            return Task.FromResult(new LibraryResult<LibraryPath>(null, recycleError));
        }

        InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);
        return Task.FromResult(new LibraryResult<LibraryPath>(fresh, null));
    }

    /// <summary>The Teammates destination rules (corrections-B4 item 21, applied to rename/move destinations
    /// too by item 26): refuses a destination directly at the Teammates root, and a <c>.md</c>
    /// <paramref name="finalName"/> landing directly in a Teammate folder. Shared by
    /// <see cref="CreateFileAsync"/>, <see cref="CreateFolderAsync"/>, <see cref="RenameAsync"/> and
    /// <see cref="MoveAsync"/> so the rule lives in one place.</summary>
    /// <param name="destinationFolder">The already-resolved destination folder.</param>
    /// <param name="finalName">The final file name being placed in <paramref name="destinationFolder"/>, or
    /// <see langword="null"/> when creating a folder (the <c>.md</c> check does not apply).</param>
    private static string? TeammatesDestinationRefusal(LibraryPath destinationFolder, string? finalName)
    {
        if (destinationFolder.Root.Kind == LibraryRootKind.Teammates && destinationFolder.RelativePath.Length == 0)
        {
            return CreateAtTeammatesRootReason;
        }

        if (finalName is not null
            && destinationFolder.Role == LibraryNodeRole.TeammateFolder
            && string.Equals(Path.GetExtension(finalName), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return CreateMdInTeammateFolderReason;
        }

        return null;
    }

    /// <summary>Whether <paramref name="candidate"/> equals <paramref name="folder"/> or lies under it
    /// (corrections-B4 item 25: a separator-terminated prefix, not a raw <see cref="string.StartsWith(string)"/>),
    /// compared with <see cref="FolderSnapshot.PathComparer"/> on already-trimmed, resolved full paths.</summary>
    private static bool IsSameOrWithin(string candidate, string folder)
    {
        if (FolderSnapshot.PathComparer.Equals(candidate, folder))
        {
            return true;
        }

        string prefix = folder + Path.DirectorySeparatorChar;
        return candidate.Length > prefix.Length && FolderSnapshot.PathComparer.Equals(candidate[..prefix.Length], prefix);
    }

    /// <summary>Whether <paramref name="folderFullPath"/>'s subtree holds a <c>_tasks</c> folder at any depth
    /// (corrections-B4 item 28).</summary>
    private static bool ContainsTasksSubtree(string folderFullPath)
    {
        if (!Directory.Exists(folderFullPath))
        {
            return false;
        }

        return Directory.EnumerateDirectories(folderFullPath, TasksFolderName, SearchOption.AllDirectories).Any();
    }

    /// <summary>Moves a file or folder without overwrite, dispatching to <see cref="File.Move(string, string)"/>
    /// or <see cref="Directory.Move(string, string)"/> by <paramref name="isFolder"/>.</summary>
    private static void MoveEntry(string sourcePath, string destinationPath, bool isFolder)
    {
        if (isFolder)
        {
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            File.Move(sourcePath, destinationPath, overwrite: false);
        }
    }

    /// <summary>Re-resolves a just-moved or just-renamed item so its <see cref="LibraryPath.Role"/> reflects
    /// the disk (corrections-B4 item 22), wrapped in a <see cref="LibraryMoveResult"/> carrying the D8 rewrite
    /// loop's outcome.</summary>
    /// <param name="parent">The item's (new) parent folder.</param>
    /// <param name="finalName">The item's final name under <paramref name="parent"/>.</param>
    /// <param name="rewrittenNotes">The notes the rewrite loop updated, in ordinal order.</param>
    /// <param name="failedNotes">The notes the rewrite loop couldn't update, in ordinal order.</param>
    private LibraryResult<LibraryMoveResult> ReResolveMoved(LibraryPath parent, string finalName, IReadOnlyList<string> rewrittenNotes, IReadOnlyList<LibraryNoteFailure> failedNotes)
    {
        if (!this.TryResolveCreateTarget(parent, finalName, out LibraryPath? reResolved, out string? error))
        {
            return new LibraryResult<LibraryMoveResult>(null, error);
        }

        return new LibraryResult<LibraryMoveResult>(new LibraryMoveResult(reResolved, rewrittenNotes, failedNotes, false), null);
    }

    /// <summary>Sets <see cref="LibraryMoveResult.LinksNotUpdated"/> on a successful <paramref name="result"/>,
    /// leaving a refusal untouched.</summary>
    private static LibraryResult<LibraryMoveResult> WithLinksNotUpdated(LibraryResult<LibraryMoveResult> result, bool linksNotUpdated)
    {
        if (result.Value is null)
        {
            return result;
        }

        return new LibraryResult<LibraryMoveResult>(result.Value with { LinksNotUpdated = linksNotUpdated }, null);
    }

    /// <summary>
    /// The D8 rewrite loop (corrections-B5 D8 items 9-10), run after a rename or move has already succeeded,
    /// with <see cref="CancellationToken.None"/> (item 9d): the move is never rolled back for a rewrite
    /// failure. Remaps <paramref name="preMoveFiles"/> to the post-move file list, then visits every Markdown
    /// note that existed before the move (at its own, possibly remapped, new path) so a note over
    /// <see cref="LibraryOptions.MaxEditableBytes"/> is still discovered and listed even though the index never
    /// parsed its links.
    /// </summary>
    /// <param name="movedTarget">The moved item's fresh, post-move <see cref="LibraryPath"/> (supplies the root).</param>
    /// <param name="oldRelative">The moved item's pre-move root-relative path.</param>
    /// <param name="newRelative">The moved item's post-move root-relative path.</param>
    /// <param name="wasFolder">Whether the moved item was a folder: every path under it remaps too.</param>
    /// <param name="preMoveFiles">Every file in the root, before the move (corrections-B5 D8 item 9).</param>
    /// <returns>The notes rewritten and the notes that failed, both in ordinal order.</returns>
    private async Task<(IReadOnlyList<string> Rewritten, IReadOnlyList<LibraryNoteFailure> Failed)> RewriteLinksAfterMoveAsync(
        LibraryPath movedTarget,
        string oldRelative,
        string newRelative,
        bool wasFolder,
        IReadOnlyList<string> preMoveFiles)
    {
        List<string> postMoveFiles = [.. preMoveFiles.Select(file => RemapPath(file, oldRelative, newRelative, wasFolder))];

        List<string> rewritten = [];
        List<LibraryNoteFailure> failed = [];

        foreach (string oldNote in preMoveFiles)
        {
            if (!LibraryFileKinds.IsMarkdown(oldNote))
            {
                continue;
            }

            string newNote = RemapPath(oldNote, oldRelative, newRelative, wasFolder);
            await this.ApplyNoteRewriteAsync(movedTarget, oldNote, newNote, oldRelative, newRelative, wasFolder, preMoveFiles, postMoveFiles, rewritten, failed);
        }

        rewritten.Sort(StringComparer.Ordinal);
        failed.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return (rewritten, failed);
    }

    /// <summary>
    /// Reads, rewrites and writes back the single note at <paramref name="newNote"/> (corrections-B5 D8
    /// items 9b, 10, and the 8.5.i-b listing/exception fix): the note is PLANNED first, from its raw bytes,
    /// so a Teammate definition, a note no longer editable or over
    /// <see cref="LibraryOptions.MaxEditableBytes"/>, or one with <see cref="TextFileFormat.MixedLineEndings"/>
    /// is listed only when it actually has an edit to make - never merely because it matches one of those
    /// shapes. Once an edit is confirmed, the note's text is re-read through <see cref="ReadAsync"/> and
    /// re-planned against THAT text (never the plan text's positions), then a stamp check (Length and
    /// LastWriteUtc) runs between the read and the write to catch a concurrent edit. A note that vanished
    /// between the list and either read is skipped silently; a locked or access-denied read is listed with
    /// the settled save-failure text; a note deleted between the read and the stamp check is listed as
    /// changed during the rename.
    /// </summary>
    /// <param name="movedTarget">The moved item's fresh, post-move <see cref="LibraryPath"/> (supplies the root).</param>
    /// <param name="oldNote">The note's pre-move root-relative path (used to recompute its links' pre-move resolution).</param>
    /// <param name="newNote">The note's post-move root-relative path (where it is read and written).</param>
    /// <param name="oldRelative">The moved item's pre-move root-relative path.</param>
    /// <param name="newRelative">The moved item's post-move root-relative path.</param>
    /// <param name="wasFolder">Whether the moved item was a folder.</param>
    /// <param name="preMoveFiles">Every file in the root, before the move.</param>
    /// <param name="postMoveFiles">Every file in the root, after the move.</param>
    /// <param name="rewritten">Appended with <paramref name="newNote"/> on a successful write.</param>
    /// <param name="failed">Appended with a failure reason when the note can't be updated.</param>
    private async Task ApplyNoteRewriteAsync(
        LibraryPath movedTarget,
        string oldNote,
        string newNote,
        string oldRelative,
        string newRelative,
        bool wasFolder,
        IReadOnlyList<string> preMoveFiles,
        IReadOnlyList<string> postMoveFiles,
        List<string> rewritten,
        List<LibraryNoteFailure> failed)
    {
        if (!this.resolver.TryResolve(movedTarget.Root.Id, newNote, out LibraryPath? notePath, out _))
        {
            return;
        }

        string fileName = Path.GetFileName(newNote);

        if (!TryReadPlanText(notePath.FullPath, fileName, newNote, failed, out string? planText) || planText is null)
        {
            return;
        }

        if (PlanEdits(planText, preMoveFiles, oldNote, newNote, oldRelative, newRelative, wasFolder, postMoveFiles).Count == 0)
        {
            return;
        }

        if (notePath.Role == LibraryNodeRole.TeammateDefinition)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, NoteIsTeammateDefinitionReasonFormat, fileName)));
            return;
        }

        LibraryDocumentContent content;
        try
        {
            content = await this.ReadAsync(notePath, CancellationToken.None);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, SaveFailedReasonFormat, fileName)));
            return;
        }

        if (!content.Editable || content.Text is null || content.Format is null)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, NoteTooLargeToUpdateReasonFormat, fileName)));
            return;
        }

        TextFileFormat format = content.Format;
        if (format.MixedLineEndings)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, NoteMixedLineEndingsReasonFormat, fileName)));
            return;
        }

        List<(WikiLink Link, string NewTarget)> edits = PlanEdits(content.Text, preMoveFiles, oldNote, newNote, oldRelative, newRelative, wasFolder, postMoveFiles);
        if (edits.Count == 0)
        {
            return;
        }

        string rewrittenText = WikiLinkRewriter.Rewrite(content.Text, edits);

        if (this.beforeRewriteWriteForTests is not null)
        {
            await this.beforeRewriteWriteForTests(notePath);
        }

        long currentLength;
        DateTimeOffset currentLastWriteUtc;
        try
        {
            currentLength = new FileInfo(notePath.FullPath).Length;
            currentLastWriteUtc = new(File.GetLastWriteTimeUtc(notePath.FullPath), TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, NoteChangedDuringRenameReasonFormat, fileName)));
            return;
        }

        if (currentLength != content.Length || currentLastWriteUtc != content.LastWriteUtc)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, NoteChangedDuringRenameReasonFormat, fileName)));
            return;
        }

        LibraryResult<LibraryEntry> writeResult = await this.WriteTextAsync(notePath, rewrittenText, format, CancellationToken.None);
        if (writeResult.Error is not null)
        {
            failed.Add(new LibraryNoteFailure(newNote, writeResult.Error));
            return;
        }

        rewritten.Add(newNote);
    }

    /// <summary>Reads <paramref name="fullPath"/>'s raw bytes for PLANNING only (no
    /// <see cref="LibraryOptions.MaxEditableBytes"/> cap, so a too-large note is still discovered), decoding
    /// them with <see cref="TextFileCodec.TryDecode"/>. A note that vanished between the list and this read
    /// is skipped silently (returns <see langword="false"/>, nothing added to <paramref name="failed"/>); a
    /// locked or access-denied read is listed with the settled save-failure text. A successful read that
    /// can't be decoded as text returns <see langword="true"/> with a <see langword="null"/>
    /// <paramref name="planText"/> (nothing to plan; never listed).</summary>
    /// <param name="fullPath">The note's current full path.</param>
    /// <param name="fileName">The note's file name, for the failure text.</param>
    /// <param name="newNote">The note's post-move root-relative path, for the failure entry.</param>
    /// <param name="failed">Appended with a failure reason for a locked or access-denied read.</param>
    /// <param name="planText">The decoded text, or <see langword="null"/> when nothing should be planned.</param>
    /// <returns>Whether the caller should continue (no unrecoverable read failure occurred).</returns>
    private static bool TryReadPlanText(string fullPath, string fileName, string newNote, List<LibraryNoteFailure> failed, out string? planText)
    {
        planText = null;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed.Add(new LibraryNoteFailure(newNote, string.Format(CultureInfo.InvariantCulture, SaveFailedReasonFormat, fileName)));
            return false;
        }

        _ = TextFileCodec.TryDecode(bytes, LineEnding.CrLf, out planText, out _);
        return true;
    }

    /// <summary>Parses every wikilink in <paramref name="text"/> and plans the edit each needs, via
    /// <see cref="TryPlanEdit"/>, against the note's pre- and post-move resolution (corrections-B5 D8 item
    /// 9c). Shared between the plan pass (raw bytes) and the final pass (the text <see cref="ReadAsync"/>
    /// returned), so both use the exact same rule.</summary>
    /// <param name="text">The note's text to scan for links.</param>
    /// <param name="preMoveFiles">Every file in the root, before the move.</param>
    /// <param name="oldNote">The note's pre-move root-relative path.</param>
    /// <param name="newNote">The note's post-move root-relative path.</param>
    /// <param name="oldRelative">The moved item's pre-move root-relative path.</param>
    /// <param name="newRelative">The moved item's post-move root-relative path.</param>
    /// <param name="wasFolder">Whether the moved item was a folder.</param>
    /// <param name="postMoveFiles">Every file in the root, after the move.</param>
    /// <returns>Every link that needs rewriting, paired with its new target text.</returns>
    private static List<(WikiLink Link, string NewTarget)> PlanEdits(
        string text,
        IReadOnlyList<string> preMoveFiles,
        string oldNote,
        string newNote,
        string oldRelative,
        string newRelative,
        bool wasFolder,
        IReadOnlyList<string> postMoveFiles)
    {
        List<(WikiLink Link, string NewTarget)> edits = [];
        foreach (WikiLink link in WikiLinkParser.Parse(text))
        {
            string? oldResolved = WikiLinkResolver.Resolve(preMoveFiles, oldNote, link.Target).Path;
            if (TryPlanEdit(link.Target, oldResolved, newNote, oldRelative, newRelative, wasFolder, postMoveFiles, out string? newTarget))
            {
                edits.Add((link, newTarget));
            }
        }

        return edits;
    }

    /// <summary>Whether <paramref name="relativePath"/> equals <paramref name="rootRelativePath"/>, or (for a
    /// folder) lies under it, comparing root-relative paths with <see cref="FolderSnapshot.PathComparer"/>
    /// (the same comparer <see cref="IsSameOrWithin"/> uses for full paths).</summary>
    private static bool IsWithinRelative(string relativePath, string rootRelativePath, bool wasFolder)
    {
        if (FolderSnapshot.PathComparer.Equals(relativePath, rootRelativePath))
        {
            return true;
        }

        if (!wasFolder)
        {
            return false;
        }

        string prefix = rootRelativePath.Length == 0 ? string.Empty : rootRelativePath + "/";
        return relativePath.Length > prefix.Length && FolderSnapshot.PathComparer.Equals(relativePath[..prefix.Length], prefix);
    }

    /// <summary>Remaps <paramref name="relativePath"/> from before a rename or move to after it: a path inside
    /// (or equal to, for a folder) <paramref name="oldRelative"/> is rebased under
    /// <paramref name="newRelative"/>; every other path is unchanged.</summary>
    /// <param name="relativePath">The root-relative path to remap.</param>
    /// <param name="oldRelative">The moved item's pre-move root-relative path.</param>
    /// <param name="newRelative">The moved item's post-move root-relative path.</param>
    /// <param name="wasFolder">Whether the moved item was a folder.</param>
    private static string RemapPath(string relativePath, string oldRelative, string newRelative, bool wasFolder)
    {
        if (!IsWithinRelative(relativePath, oldRelative, wasFolder))
        {
            return relativePath;
        }

        if (FolderSnapshot.PathComparer.Equals(relativePath, oldRelative))
        {
            return newRelative;
        }

        string oldPrefix = oldRelative.Length == 0 ? string.Empty : oldRelative + "/";
        string newPrefix = newRelative.Length == 0 ? string.Empty : newRelative + "/";
        return newPrefix + relativePath[oldPrefix.Length..];
    }

    /// <summary>
    /// Whether a link with text <paramref name="target"/>, previously resolving to
    /// <paramref name="oldResolved"/> from a note now at <paramref name="newNote"/>, needs rewriting
    /// after the move (corrections-B5 D8 item 9c). Always rewrites a link whose old target was the moved item
    /// itself (or, for a folder, anything that lived inside it) so it reflects the item's new name, even when
    /// case-insensitive resolution alone would still find it (a case-only rename). Also always rewrites a link
    /// whose old target shares its bare file name with the item's new name: the rename just created a second
    /// file with that name, so the existing qualified spelling must be kept even though it still resolves to
    /// the same place today. Otherwise, rewrites only when re-resolving <paramref name="target"/> from
    /// <paramref name="newNote"/> against <paramref name="postMoveFiles"/> no longer lands on where the
    /// original target itself ended up (a silent re-point). A link that resolved to nothing before
    /// (<paramref name="oldResolved"/> is <see langword="null"/>) is left alone.
    /// </summary>
    /// <param name="target">The link's target text, as written.</param>
    /// <param name="oldResolved">The link's pre-move resolved root-relative path, or <see langword="null"/> when it resolved to nothing.</param>
    /// <param name="newNote">The linking note's post-move root-relative path.</param>
    /// <param name="oldRelative">The moved item's pre-move root-relative path.</param>
    /// <param name="newRelative">The moved item's post-move root-relative path.</param>
    /// <param name="wasFolder">Whether the moved item was a folder.</param>
    /// <param name="postMoveFiles">Every file in the root, after the move.</param>
    /// <param name="newTarget">The shortest unique target text to rewrite to, when this returns <see langword="true"/>.</param>
    private static bool TryPlanEdit(
        string target,
        string? oldResolved,
        string newNote,
        string oldRelative,
        string newRelative,
        bool wasFolder,
        IReadOnlyList<string> postMoveFiles,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? newTarget)
    {
        newTarget = null;
        if (oldResolved is null)
        {
            return false;
        }

        bool targetIsMovedItem = IsWithinRelative(oldResolved, oldRelative, wasFolder);
        bool targetNameCollidesWithNewName = !targetIsMovedItem
            && LibraryFileKinds.IsMarkdown(newRelative)
            && StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(oldResolved), Path.GetFileName(newRelative));

        string originalTargetNewPath = RemapPath(oldResolved, oldRelative, newRelative, wasFolder);

        if (!targetIsMovedItem && !targetNameCollidesWithNewName)
        {
            WikiLinkResolution newResolution = WikiLinkResolver.Resolve(postMoveFiles, newNote, target);
            if (newResolution.Path is not null && FolderSnapshot.PathComparer.Equals(newResolution.Path, originalTargetNewPath))
            {
                return false;
            }
        }

        newTarget = WikiLinkResolver.ShortestTarget(postMoveFiles, newNote, originalTargetNewPath);
        return true;
    }

    /// <summary>Invalidates every Library Root whose <see cref="LibraryRoot.FullPath"/> overlaps
    /// <paramref name="changedFullPath"/> (corrections-B5 D8 item 7, Spec §10 E-2): the root contains the
    /// changed path, or the changed path contains the root (a pinned root reaching inside a Team folder
    /// that was itself just renamed or moved). Reuses <see cref="IsSameOrWithin"/> both ways rather than a
    /// second prefix check. Static (not an instance method) so it stays outside
    /// <c>Write_OnlyLibraryPaths_EveryMethodTakesLibraryPathFirst</c>'s LibraryPath-first invariant, which
    /// only walks instance methods.</summary>
    /// <param name="resolver">Supplies every configured Library Root.</param>
    /// <param name="index">Invalidated for each overlapping root.</param>
    /// <param name="changedFullPath">The full path that was written, created, recycled, renamed or moved.</param>
    private static void InvalidateOverlapping(LibraryPathResolver resolver, WikiLinkIndex index, string changedFullPath)
    {
        string changed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(changedFullPath));

        foreach (LibraryRoot root in resolver.AllRoots)
        {
            string rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.FullPath));
            if (IsSameOrWithin(changed, rootPath) || IsSameOrWithin(rootPath, changed))
            {
                index.Invalidate(root.Id);
            }
        }
    }

    /// <summary>
    /// Previews the wikilink rewrite a rename or move of <paramref name="item"/> to
    /// <paramref name="destination"/> would perform (corrections-B5 D8 item 8): invalidates <paramref name="item"/>'s
    /// overlapping roots FIRST (item 7, an agent may have written outside the service), then counts, for a
    /// file, every link that resolves to it, or for a folder, every link that resolves to any file inside it
    /// (same hidden-folder walk as <see cref="ListAsync"/>, via <see cref="WikiLinkIndex.FilesUnder"/>).
    /// </summary>
    /// <param name="item">The already-resolved item that would move; re-resolved before use.</param>
    /// <param name="destination">The already-resolved destination; re-resolved before use.</param>
    /// <param name="ct">Cancels the preview.</param>
    /// <returns>The link and note counts, and whether the item's root's index is available.</returns>
    internal Task<LibraryLinkPreview> PreviewLinkChangesAsync(LibraryPath item, LibraryPath destination, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(destination);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out _)
            || !this.resolver.TryResolve(destination.Root.Id, destination.RelativePath, out LibraryPath? _, out _))
        {
            return Task.FromResult(new LibraryLinkPreview(0, 0, false));
        }

        InvalidateOverlapping(this.resolver, this.index, fresh.FullPath);

        HashSet<string> notes = new(StringComparer.Ordinal);
        int linkCount = 0;

        if (fresh.Role == LibraryNodeRole.Folder)
        {
            foreach (string filePath in this.index.FilesUnder(fresh.Root.Id, fresh.RelativePath))
            {
                if (!this.resolver.TryResolve(fresh.Root.Id, filePath, out LibraryPath? child, out _))
                {
                    continue;
                }

                foreach ((string notePath, _) in this.index.LinksTo(child))
                {
                    linkCount++;
                    notes.Add(notePath);
                }
            }
        }
        else
        {
            foreach ((string notePath, _) in this.index.LinksTo(fresh))
            {
                linkCount++;
                notes.Add(notePath);
            }
        }

        bool available = this.index.IsAvailable(fresh.Root.Id);
        return Task.FromResult(new LibraryLinkPreview(linkCount, notes.Count, available));
    }

    /// <summary>Resolves the not-yet-existing child <paramref name="finalName"/> of <paramref name="fresh"/>,
    /// without trusting the caller's <see cref="LibraryPath.FullPath"/> (shares the resolver boundary with
    /// <see cref="ReadAsync"/> and <see cref="WriteTextAsync"/>).</summary>
    private bool TryResolveCreateTarget(LibraryPath fresh, string finalName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LibraryPath? target, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        string childRelativePath = fresh.RelativePath.Length == 0 ? finalName : $"{fresh.RelativePath}/{finalName}";
        return this.resolver.TryResolve(fresh.Root.Id, childRelativePath, out target, out error);
    }

    /// <summary>Re-resolves the just-created child so its <see cref="LibraryPath.Role"/> reflects the disk,
    /// not a missing-path depth guess (corrections-B4 item 22). Shares <see cref="TryResolveCreateTarget"/>'s
    /// child-path computation rather than recomputing it.</summary>
    private LibraryResult<LibraryPath> ReResolveCreated(LibraryPath fresh, string finalName)
    {
        if (!this.TryResolveCreateTarget(fresh, finalName, out LibraryPath? reResolved, out string? error))
        {
            return new LibraryResult<LibraryPath>(null, error);
        }

        return new LibraryResult<LibraryPath>(reResolved, null);
    }

    /// <summary>Maps a <see cref="FileMode.CreateNew"/> failure to the settled create-failure text
    /// (corrections-B4 item 19): an existence clash decides the exists text, anything else the generic
    /// "couldn't create" text.</summary>
    private static string CreateFailureReason(string name, string fullPath)
    {
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, name);
        }

        return string.Format(CultureInfo.InvariantCulture, CouldNotCreateReasonFormat, name);
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
}
