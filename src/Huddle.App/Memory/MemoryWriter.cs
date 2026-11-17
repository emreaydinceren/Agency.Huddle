using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The one write path for a whole memory file (Team Memory Helper spec §6.6): checks the name and the description, composes the
/// frontmatter (keeping the other keys a replaced file had), checks the size cap and the version, and writes atomically, all under
/// the folder's <see cref="MemoryFolderGate"/>.
/// The <c>memory</c> tool's <c>write</c> and the Team Memory Helper's applier both call it, so no rule exists twice. <see cref="WriteAsync"/>
/// logs nothing: each caller reports the result in its own words. The two Memory Tidy methods (spec §6.11) log only a removal that failed.
/// </summary>
/// <param name="gate">Serialises the writers of one folder.</param>
/// <param name="options">Supplies the size cap.</param>
/// <param name="logger">Reports a removal that failed after the write; none by default.</param>
internal sealed class MemoryWriter(MemoryFolderGate gate, IOptions<TeamOptions> options, ILogger<MemoryWriter>? logger = null)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger log = logger ?? NullLogger<MemoryWriter>.Instance;

    /// <summary>
    /// A test seam, called inside the gate after every named file has been read and version-checked and before the copies are made,
    /// with the folder's path. A test replaces a file here, or holds the call to show a second one waits at the gate.
    /// </summary>
    internal Func<string, Task>? AfterReadForTests { get; set; }

    /// <summary>Writes one memory file, given the version the writer last read.</summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">The file name, not yet checked.</param>
    /// <param name="description">The description, not yet checked.</param>
    /// <param name="text">The body, without frontmatter.</param>
    /// <param name="expectedVersion">The version the writer last read, or <see langword="null"/> when the file must not exist yet.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate and the write.</param>
    /// <returns>What happened.</returns>
    public async Task<MemoryWriteResult> WriteAsync(
        MemoryFolder folder,
        string file,
        string? description,
        string? text,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(file);

        string? path = MemoryPaths.FilePath(folder, file);
        if (path is null)
        {
            return new MemoryWriteResult.BadName();
        }

        try
        {
            return await gate.RunAsync(folder.Path, () => this.WriteUnderGateAsync(folder, file, path, description, text, expectedVersion, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MemoryWriteResult.Failed(path, ex);
        }
    }

    /// <summary>The checks and the write, run while holding the folder's gate.</summary>
    private async Task<MemoryWriteResult> WriteUnderGateAsync(
        MemoryFolder folder,
        string file,
        string path,
        string? description,
        string? text,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (MemoryFileRules.DescriptionProblem(description, file) is { } problem)
        {
            return new MemoryWriteResult.BadDescription(problem);
        }

        // A writer that read the file and wrote it back whole echoes its header: strip the echo, keep the keys it carries, and refuse
        // a text that was nothing but the echo rather than write an empty body.
        string body = MemoryFileRules.SplitEcho(text ?? string.Empty, out string? echoedKeys);
        if (body.Trim().Length == 0 && !string.IsNullOrWhiteSpace(text))
        {
            return new MemoryWriteResult.OnlyFrontmatter();
        }

        // Read first, so a replaced file keeps the frontmatter keys other than its description that it already had.
        byte[]? current = File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
        string? otherKeys = MemoryFileRules.MergeKeys(current is null ? null : MemoryFileRules.OtherKeys(Utf8.GetString(current)), echoedKeys);
        byte[] bytes = Utf8.GetBytes(MemoryFileRules.Compose((description ?? string.Empty).Trim(), body, otherKeys));
        int max = options.Value.Memory.MaxFileBytes;
        if (max > 0 && bytes.Length > max)
        {
            return new MemoryWriteResult.TooLarge(bytes.Length, max);
        }

        if (current is null && expectedVersion is not null)
        {
            return new MemoryWriteResult.Missing();
        }

        if (current is not null)
        {
            if (expectedVersion is null)
            {
                return new MemoryWriteResult.Exists();
            }

            string token = MemoryVersion.Of(current);
            if (!string.Equals(expectedVersion, token, StringComparison.Ordinal))
            {
                return new MemoryWriteResult.Stale(token, Utf8.GetString(current));
            }
        }

        _ = Directory.CreateDirectory(folder.Path);
        return await AtomicFile.TryWriteAsync(path, bytes, cancellationToken).ConfigureAwait(false)
            ? new MemoryWriteResult.Written(path, MemoryVersion.Of(bytes), current is null)
            : new MemoryWriteResult.Failed(path, null);
    }

    /// <summary>
    /// Applies one Helper step to a folder as one step under the folder's gate: checks every version, copies each file it will change or
    /// remove into the backup, writes, then removes. It always refuses <c>reported.md</c> and <c>MEMORY.md</c>.
    /// </summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="change">The file to write, if any, and the files to remove, each with the version its reader saw.</param>
    /// <param name="backupFolder">Where the files as they were are copied first.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate and the work.</param>
    /// <returns>What happened.</returns>
    public Task<MemoryTidyResult> ReplaceAndRemoveAsync(MemoryFolder folder, MemoryTidyChange change, string backupFolder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Remove);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFolder);

        List<MemoryTidyRemoval> named = [];
        if (change.Write is { } write)
        {
            named.Add(new MemoryTidyRemoval(write.File, write.ExpectedVersion));
        }

        named.AddRange(change.Remove);
        foreach (MemoryTidyRemoval entry in named)
        {
            if (ReportedFile.IsName(entry.File) || MemoryFileRules.IsIndexFileName(entry.File))
            {
                return Task.FromResult<MemoryTidyResult>(new MemoryTidyResult.Refused($"{entry.File} is never changed by a tidy step."));
            }
        }

        Func<byte[], (byte[]? Bytes, string? Why)>? compose = change.Write is { } step ? current => this.ComposeStep(step, current) : null;
        return this.ApplyAsync(folder, named, compose, backupFolder, cancellationToken);
    }

    /// <summary>
    /// Replaces the body of the folder's <c>reported.md</c>, and nothing else: the frontmatter is kept byte for byte and no description
    /// rule is run. It can name no other file.
    /// </summary>
    /// <param name="folder">An Agent's own folder.</param>
    /// <param name="body">The new body: everything after the frontmatter.</param>
    /// <param name="expectedVersion">The version the reader saw.</param>
    /// <param name="backupFolder">Where the file as it was is copied first.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate and the work.</param>
    /// <returns>What happened.</returns>
    public Task<MemoryTidyResult> PruneReportedAsync(MemoryFolder folder, string body, string expectedVersion, string backupFolder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFolder);

        return this.ApplyAsync(
            folder,
            [new MemoryTidyRemoval(ReportedFile.FileName, expectedVersion)],
            current =>
            {
                string text = Utf8.GetString(current);
                return (Utf8.GetBytes(string.Concat(text.AsSpan(0, NoteFrontmatter.Locate(text).BodyStart), body)), null);
            },
            backupFolder,
            cancellationToken);
    }

    /// <summary>The core both tidy methods share: the names, then everything else inside one hold of the folder's gate.</summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="named">Every file the step touches with its expected version; when <paramref name="compose"/> is given the first is the one written.</param>
    /// <param name="compose">Builds the new bytes of the first file from its current bytes, or says why it refuses; <see langword="null"/> for a step that only removes.</param>
    /// <param name="backupFolder">Where the files as they were are copied first.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate and the work.</param>
    private async Task<MemoryTidyResult> ApplyAsync(
        MemoryFolder folder,
        List<MemoryTidyRemoval> named,
        Func<byte[], (byte[]? Bytes, string? Why)>? compose,
        string backupFolder,
        CancellationToken cancellationToken)
    {
        if (named.Count == 0)
        {
            return new MemoryTidyResult.Refused("The step names no file.");
        }

        if (named.Select(static entry => entry.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != named.Count)
        {
            return new MemoryTidyResult.Refused("The step names the same file twice.");
        }

        List<LoadedFile> targets = [];
        foreach (MemoryTidyRemoval entry in named)
        {
            string? path = MemoryPaths.FilePath(folder, entry.File);
            if (path is null)
            {
                return new MemoryTidyResult.Refused($"{entry.File} is not a memory file name.");
            }

            targets.Add(new LoadedFile(entry.File, path, entry.ExpectedVersion, []));
        }

        try
        {
            return await gate.RunAsync(folder.Path, () => this.ApplyUnderGateAsync(folder, targets, compose, backupFolder, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MemoryTidyResult.Failed(named[0].File, ex);
        }
    }

    /// <summary>Steps 2 to 7 of the core: read and check, compose, copy, write, remove.</summary>
    private async Task<MemoryTidyResult> ApplyUnderGateAsync(
        MemoryFolder folder,
        List<LoadedFile> targets,
        Func<byte[], (byte[]? Bytes, string? Why)>? compose,
        string backupFolder,
        CancellationToken cancellationToken)
    {
        List<LoadedFile> loaded = [];
        foreach (LoadedFile target in targets)
        {
            if (!File.Exists(target.Path))
            {
                return new MemoryTidyResult.Stale(target.File);
            }

            byte[] bytes = await File.ReadAllBytesAsync(target.Path, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(target.Version, MemoryVersion.Of(bytes), StringComparison.Ordinal))
            {
                return new MemoryTidyResult.Stale(target.File);
            }

            loaded.Add(target with { Bytes = bytes });
        }

        if (this.AfterReadForTests is { } hook)
        {
            await hook(folder.Path).ConfigureAwait(false);
        }

        byte[]? newBytes = null;
        if (compose is not null)
        {
            (newBytes, string? why) = compose(loaded[0].Bytes);
            if (why is not null)
            {
                return new MemoryTidyResult.Refused(why);
            }
        }

        (List<string>? copies, string? uncopied) = await CopyAllAsync(loaded, backupFolder, cancellationToken).ConfigureAwait(false);
        if (copies is null)
        {
            return new MemoryTidyResult.CopyFailed(uncopied ?? loaded[0].File);
        }

        string? writtenVersion = null;
        if (newBytes is not null)
        {
            if (!await AtomicFile.TryWriteAsync(loaded[0].Path, newBytes, cancellationToken).ConfigureAwait(false))
            {
                return new MemoryTidyResult.Failed(loaded[0].File, null);
            }

            writtenVersion = MemoryVersion.Of(newBytes);
        }

        return new MemoryTidyResult.Done(writtenVersion, copies, this.RemoveAll(loaded.Skip(compose is null ? 0 : 1)));
    }

    /// <summary>
    /// Writes each file as it was read into the backup folder, never over a copy that is already there (a file is copied once per pass).
    /// The bytes are the ones that were version-checked, not what is on disk now.
    /// </summary>
    /// <param name="loaded">The files with the bytes read for them.</param>
    /// <param name="backupFolder">The folder the copies go in.</param>
    /// <param name="cancellationToken">Cancels the copies.</param>
    /// <returns>The copies' full paths, or no copies and the file that could not be copied.</returns>
    private static async Task<(List<string>? Copies, string? Failed)> CopyAllAsync(List<LoadedFile> loaded, string backupFolder, CancellationToken cancellationToken)
    {
        List<string> copies = [];
        try
        {
            _ = Directory.CreateDirectory(backupFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, loaded[0].File);
        }

        foreach (LoadedFile file in loaded)
        {
            string target = Path.Combine(backupFolder, file.File);
            if (Directory.Exists(target))
            {
                return (null, file.File);
            }

            if (!File.Exists(target) && !await AtomicFile.TryWriteAsync(target, file.Bytes, cancellationToken).ConfigureAwait(false))
            {
                return (null, file.File);
            }

            copies.Add(target);
        }

        return (copies, null);
    }

    /// <summary>Deletes each file; one that cannot be deleted is logged and listed, not an error: its content is in the kept file.</summary>
    /// <param name="files">The files to remove.</param>
    /// <returns>The full paths that are still there.</returns>
    private List<string> RemoveAll(IEnumerable<LoadedFile> files)
    {
        List<string> leftBehind = [];
        foreach (LoadedFile file in files)
        {
            try
            {
                File.Delete(file.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.log.LogWarning(ex, "A memory tidy step wrote its replacement but could not remove {Path}.", file.Path);
                leftBehind.Add(file.Path);
            }
        }

        return leftBehind;
    }

    /// <summary>
    /// Composes the written file of a Helper step: the file's own other frontmatter keys, its own description line when the step gives
    /// none, and the new body or the old one byte for byte. A frontmatter block echoed in the text is thrown away, keys and all.
    /// </summary>
    /// <param name="write">The step's write.</param>
    /// <param name="currentBytes">The file as it is on disk.</param>
    /// <returns>The new bytes, or the reason a memory rule refuses them.</returns>
    private (byte[]? Bytes, string? Why) ComposeStep(MemoryTidyWrite write, byte[] currentBytes)
    {
        string current = Utf8.GetString(currentBytes);
        string? ownDescription = MemoryFileRules.DescriptionOf(current);
        string? problem = MemoryFileRules.DescriptionProblem(write.Description, write.File);
        if (write.Description is null)
        {
            // A step with no new description keeps the file's own, which a Human may have written however they like; only a file with none is refused.
            problem = ownDescription is null ? "it is missing." : null;
        }
        if (problem is not null)
        {
            return (null, $"The description of {write.File} was refused: {problem}");
        }

        string body;
        if (write.Text is null)
        {
            NoteFrontmatterBlock block = NoteFrontmatter.Locate(current);
            body = (block.Text is null ? current : current[block.BodyStart..]).TrimStart('\r', '\n');
        }
        else
        {
            body = MemoryFileRules.SplitEcho(write.Text, out _);
            if (body.Trim().Length == 0 && !string.IsNullOrWhiteSpace(write.Text))
            {
                return (null, $"The text for {write.File} was only a frontmatter block.");
            }

            body = body.EndsWith('\n') ? body : body + "\n";
        }

        string descriptionLines = write.Description is { } given
            ? $"description: {MemoryFileRules.ToYamlScalar(given.Trim())}\n"
            : MemoryFileRules.DescriptionLinesOf(current) ?? $"description: {MemoryFileRules.ToYamlScalar(ownDescription ?? string.Empty)}\n";
        byte[] bytes = Utf8.GetBytes($"---\n{descriptionLines}{MemoryFileRules.OtherKeys(current)}---\n\n{body}");
        int max = options.Value.Memory.MaxFileBytes;
        return max > 0 && bytes.Length > max
            ? (null, $"{write.File} would be {bytes.Length} bytes, over the {max} byte limit.")
            : (bytes, null);
    }

    /// <summary>A named file with the version its reader saw and, once read, the bytes that were checked.</summary>
    private sealed record LoadedFile(string File, string Path, string Version, byte[] Bytes);
}
