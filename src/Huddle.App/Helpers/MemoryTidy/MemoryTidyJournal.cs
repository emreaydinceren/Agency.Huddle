using System.Text.Json;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The file-backed journal of one pass: <c>pass.jsonl</c> in the pass folder, one JSON object per line. Each <see cref="Append"/> opens the file,
/// writes the line and flushes it to disk, under a lock, so an entry is on disk when the call returns.
/// </summary>
internal sealed class MemoryTidyJournal : IMemoryTidyJournal
{
    /// <summary>The journal's file name inside a pass folder.</summary>
    public const string FileName = "pass.jsonl";

    private const byte LineFeed = (byte)'\n';

    private readonly string path;
    private readonly Lock gate = new();

    /// <summary>Creates the journal of one pass; the file is made by the first <see cref="Append"/>.</summary>
    /// <param name="passFolder">The pass folder.</param>
    public MemoryTidyJournal(string passFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);
        this.path = Path.Combine(passFolder, FileName);
    }

    /// <inheritdoc />
    public void Append(MemoryTidyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        byte[] line = JsonSerializer.SerializeToUtf8Bytes(entry, MemoryTidyJsonContext.Default.MemoryTidyEntry);
        lock (this.gate)
        {
            using FileStream stream = new(this.path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            _ = stream.Seek(0, SeekOrigin.End);
            if (stream.Length > 0)
            {
                // A torn last line from a killed process has no line end; end it, or this entry would be glued to it and lost.
                _ = stream.Seek(-1, SeekOrigin.End);
                bool endsLine = stream.ReadByte() == LineFeed;
                if (!endsLine)
                {
                    stream.WriteByte(LineFeed);
                }
            }

            stream.Write(line);
            stream.WriteByte(LineFeed);
            stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>Reads a pass folder's journal back. A line that cannot be read, such as the torn last line of a killed process, is ignored.</summary>
    /// <param name="passFolder">The pass folder.</param>
    /// <returns>The entries in the order they were written; empty when the folder has no journal.</returns>
    public static IReadOnlyList<MemoryTidyEntry> Read(string passFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);

        string file = Path.Combine(passFolder, FileName);
        if (!File.Exists(file))
        {
            return [];
        }

        byte[] bytes;
        using (FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }

        List<MemoryTidyEntry> entries = [];
        int start = 0;
        while (start < bytes.Length)
        {
            int end = Array.IndexOf(bytes, LineFeed, start);
            int length = (end < 0 ? bytes.Length : end) - start;
            if (length > 0 && TryParse(bytes.AsSpan(start, length)) is { } entry)
            {
                entries.Add(entry);
            }

            start += length + 1;
        }

        return entries;
    }

    private static MemoryTidyEntry? TryParse(ReadOnlySpan<byte> line)
    {
        try
        {
            return JsonSerializer.Deserialize(line, MemoryTidyJsonContext.Default.MemoryTidyEntry);
        }
        catch (JsonException)
        {
            // A torn or damaged line is skipped: the entries before it are still the record of what the pass did.
            return null;
        }
    }
}
