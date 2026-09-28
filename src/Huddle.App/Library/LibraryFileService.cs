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
    internal Task<LibraryResult<LibraryMoveResult>> RenameAsync(LibraryPath item, string newName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(newName);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, resolveError));
        }

        string parentRelativePath = ParentRelativePath(fresh.RelativePath);
        if (!this.resolver.TryResolve(fresh.Root.Id, parentRelativePath, out LibraryPath? parent, out string? parentError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, parentError));
        }

        string? nameError = LibraryNames.Validate(newName);
        if (nameError is not null)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, nameError));
        }

        LibraryProtection protection = LibraryProtection.For(fresh);
        if (!protection.CanRename)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, protection.Reason));
        }

        string? teammatesError = TeammatesDestinationRefusal(parent, newName);
        if (teammatesError is not null)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, teammatesError));
        }

        if (!this.TryResolveCreateTarget(parent, newName, out LibraryPath? target, out string? targetError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, targetError));
        }

        bool caseOnly = !string.Equals(fresh.FullPath, target.FullPath, StringComparison.Ordinal)
            && FolderSnapshot.PathComparer.Equals(fresh.FullPath, target.FullPath);

        if (!caseOnly && (Directory.Exists(target.FullPath) || File.Exists(target.FullPath)))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, newName)));
        }

        bool isFolder = fresh.Role != LibraryNodeRole.File;
        string name = Path.GetFileName(fresh.FullPath);

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
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name)));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name)));
        }

        return Task.FromResult(this.ReResolveMoved(parent, newName));
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
    internal Task<LibraryResult<LibraryMoveResult>> MoveAsync(LibraryPath item, LibraryPath targetFolder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(targetFolder);
        ct.ThrowIfCancellationRequested();

        if (!this.resolver.TryResolve(item.Root.Id, item.RelativePath, out LibraryPath? fresh, out string? resolveError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, resolveError));
        }

        if (!this.resolver.TryResolve(targetFolder.Root.Id, targetFolder.RelativePath, out LibraryPath? freshTarget, out string? targetResolveError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, targetResolveError));
        }

        LibraryProtection protection = LibraryProtection.For(fresh);
        if (!protection.CanMove)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, protection.Reason));
        }

        if (!string.Equals(fresh.Root.Id, freshTarget.Root.Id, StringComparison.Ordinal))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, MoveAcrossRootsReason));
        }

        string itemFullPath = Path.TrimEndingDirectorySeparator(fresh.FullPath);
        string targetFullPath = Path.TrimEndingDirectorySeparator(freshTarget.FullPath);

        if (fresh.Role == LibraryNodeRole.Folder && IsSameOrWithin(targetFullPath, itemFullPath))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, MoveIntoOwnSubfolderReason));
        }

        string name = Path.GetFileName(itemFullPath);

        string? teammatesError = TeammatesDestinationRefusal(freshTarget, name);
        if (teammatesError is not null)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, teammatesError));
        }

        if (fresh.Role == LibraryNodeRole.Folder && fresh.Root.Kind == LibraryRootKind.Teams && ContainsTasksSubtree(itemFullPath))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, MoveTasksSubtreeReason));
        }

        if (!this.TryResolveCreateTarget(freshTarget, name, out LibraryPath? target, out string? targetError))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, targetError));
        }

        if (Directory.Exists(target.FullPath) || File.Exists(target.FullPath))
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, AlreadyExistsReasonFormat, name)));
        }

        bool isFolder = fresh.Role != LibraryNodeRole.File;

        try
        {
            MoveEntry(fresh.FullPath, target.FullPath, isFolder);
        }
        catch (IOException)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name)));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new LibraryResult<LibraryMoveResult>(null, string.Format(CultureInfo.InvariantCulture, CouldNotMoveReasonFormat, name)));
        }

        return Task.FromResult(this.ReResolveMoved(freshTarget, name));
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
    /// the disk (corrections-B4 item 22), wrapped in a <see cref="LibraryMoveResult"/> with empty lists (D8
    /// fills them).</summary>
    private LibraryResult<LibraryMoveResult> ReResolveMoved(LibraryPath parent, string finalName)
    {
        if (!this.TryResolveCreateTarget(parent, finalName, out LibraryPath? reResolved, out string? error))
        {
            return new LibraryResult<LibraryMoveResult>(null, error);
        }

        return new LibraryResult<LibraryMoveResult>(new LibraryMoveResult(reResolved, [], []), null);
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
