namespace Agency.Huddle.App.FileChanges;

/// <summary>One Memory file's indexed summary, per FC §6.15.</summary>
/// <param name="Summary">The file's first non-blank line, with a leading <c>#</c> and spaces removed and cut at 200 characters, or the file name when there is no non-blank line (E-15).</param>
/// <param name="FullPath">The file's full path.</param>
internal sealed record MemoryEntry(string Summary, string FullPath);

/// <summary>
/// The Memory index built at session start for <c>SystemPromptComposer.Compose</c>'s memory
/// overload, per FC §6.15.
/// </summary>
/// <param name="MemoryPath">The Agent's Memory folder's full path, <c>{{memoryPath}}</c> in <c>systemPrompt.memory</c>.</param>
/// <param name="Entries">The listed entries, already capped at <c>MaxMemoryEntries</c>.</param>
/// <param name="NotListed">How many more Memory files exist beyond what <see cref="Entries"/> lists.</param>
internal sealed record MemorySnapshot(string MemoryPath, IReadOnlyList<MemoryEntry> Entries, int NotListed);

/// <summary>
/// Builds the Memory index shown in the system prompt, per FC §6.15: one entry per <c>*.md</c>
/// file directly inside <c>{WorkDir}/memory/</c>, ordered by file name.
/// </summary>
internal static class MemoryIndex
{
    /// <summary>The most characters a summary may carry before it is cut, with a trailing ellipsis added.</summary>
    private const int MaxSummaryLength = 200;

    /// <summary>
    /// Builds the index of every <c>*.md</c> file directly inside <paramref name="memoryDir"/>,
    /// ordered by file name (<see cref="StringComparer.Ordinal"/>), capped at
    /// <paramref name="maxEntries"/>.
    /// </summary>
    /// <param name="memoryDir">The Agent's Memory folder, <c>{WorkDir}/memory/</c>. A missing folder gives an empty index.</param>
    /// <param name="maxEntries">The most entries to list; the rest are counted in <c>NotListed</c> (E-17).</param>
    /// <returns>The listed entries, and how many more files exist beyond <paramref name="maxEntries"/>.</returns>
    internal static (IReadOnlyList<MemoryEntry> Entries, int NotListed) Build(string memoryDir, int maxEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryDir);

        if (!Directory.Exists(memoryDir))
        {
            return ([], 0);
        }

        List<string> files = [.. Directory.EnumerateFiles(memoryDir, "*.md", SearchOption.TopDirectoryOnly)];
        files.Sort(StringComparer.Ordinal);

        List<string> listedFiles = files.Count > maxEntries ? files[..maxEntries] : files;
        int notListed = files.Count - listedFiles.Count;

        List<MemoryEntry> entries = [.. listedFiles.Select(file => new MemoryEntry(SummaryFor(file), file))];

        return (entries, notListed);
    }

    /// <summary>
    /// Reads <paramref name="filePath"/>'s first non-blank line as its summary, with a leading
    /// <c>#</c> and spaces removed and cut at <see cref="MaxSummaryLength"/> characters. Falls back
    /// to the file name, without its extension, when the file is blank or empty (E-15), or when it
    /// cannot be read (correction item 13).
    /// </summary>
    private static string SummaryFor(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            foreach (string line in File.ReadLines(filePath))
            {
                string trimmed = line.TrimStart('#').Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                return trimmed.Length > MaxSummaryLength
                    ? trimmed[..MaxSummaryLength] + "…"
                    : trimmed;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return fileName;
        }

        return fileName;
    }
}
