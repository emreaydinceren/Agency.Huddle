using System.Text;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The one write path for a whole memory file (Team Memory Helper spec §6.6): checks the name and the description, composes the
/// frontmatter (keeping the other keys a replaced file had), checks the size cap and the version, and writes atomically, all under
/// the folder's <see cref="MemoryFolderGate"/>.
/// The <c>memory</c> tool's <c>write</c> and the Team Memory Helper's applier both call it, so no rule exists twice. It logs nothing:
/// each caller reports the result in its own words.
/// </summary>
/// <param name="gate">Serialises the writers of one folder.</param>
/// <param name="options">Supplies the size cap.</param>
internal sealed class MemoryWriter(MemoryFolderGate gate, IOptions<TeamOptions> options)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

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
}
