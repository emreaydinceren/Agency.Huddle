using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// Builds the Memory index shown in the system prompt, per FC §6.15: one entry per <c>*.md</c>
/// file directly inside <c>{WorkDir}/memory/</c>, ordered by file name. An entry's summary is the file's
/// frontmatter <c>description</c> when it has one, else its first body line (Instructions and Memory §4.2).
/// </summary>
internal static class MemoryIndex
{
    /// <summary>The most characters a summary may carry before it is cut, with a trailing ellipsis added.</summary>
    private const int MaxSummaryLength = 200;

    /// <summary>The most lines read from a file to find its summary: a frontmatter block and the first body line, never the whole file.</summary>
    private const int MaxScannedLines = 200;

    /// <summary>The most characters read from a file to find its summary.</summary>
    private const int MaxScannedCharacters = 16 * 1024;

    /// <summary>The stray marker line <see cref="FirstLine"/> skips when it falls back to the first line of a file. Where a block opens and closes is <see cref="FrontmatterFence"/>'s rule.</summary>
    private const string FrontmatterMarker = "---";

    /// <summary>
    /// Builds the index of every <c>*.md</c> file directly inside <paramref name="memoryDir"/>,
    /// ordered by file name (<see cref="StringComparer.Ordinal"/>), capped at
    /// <paramref name="maxEntries"/>.
    /// </summary>
    /// <param name="memoryDir">The Agent's Memory folder, <c>{WorkDir}/memory/</c>. A missing folder gives an empty index.</param>
    /// <param name="maxEntries">The most entries to list; the rest are counted in <c>NotListed</c> (E-17).</param>
    /// <param name="warnings">Warns once per file and content about malformed frontmatter; <see langword="null"/> warns of nothing.</param>
    /// <returns>The listed entries, and how many more files exist beyond <paramref name="maxEntries"/>.</returns>
    internal static (IReadOnlyList<MemoryEntry> Entries, int NotListed) Build(string memoryDir, int maxEntries, MemoryWarnings? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryDir);

        if (!Directory.Exists(memoryDir))
        {
            return ([], 0);
        }

        List<string> files = [.. Directory.EnumerateFiles(memoryDir, "*.md", SearchOption.TopDirectoryOnly)
            .Where(static file => !MemoryFileRules.IsIndexFileName(Path.GetFileName(file)))];
        files.Sort(StringComparer.Ordinal);

        List<string> listedFiles = files.Count > maxEntries ? files[..maxEntries] : files;
        int notListed = files.Count - listedFiles.Count;

        List<MemoryEntry> entries = [.. listedFiles.Select(file => new MemoryEntry(SummaryFor(file, warnings), file))];

        return (entries, notListed);
    }

    /// <summary>
    /// The index line of <paramref name="filePath"/>: its frontmatter <c>description</c> when it has a usable one, else its
    /// first non-blank body line, with a leading <c>#</c> and spaces removed, cut at <see cref="MaxSummaryLength"/>
    /// characters. A frontmatter block is never read as the summary. Only the start of the file is read: the frontmatter block
    /// and the first body line, within <see cref="MaxScannedLines"/> lines and <see cref="MaxScannedCharacters"/> characters.
    /// Falls back to the file name, without its extension, when the file is blank or empty (E-15), when it
    /// cannot be read (correction item 13), or when its frontmatter block is still open at the scan bound (too long to read).
    /// </summary>
    /// <param name="filePath">The memory file.</param>
    /// <param name="warnings">Warns once per file and content about malformed frontmatter; <see langword="null"/> warns of nothing.</param>
    /// <returns>The summary.</returns>
    internal static string SummaryFor(string filePath, MemoryWarnings? warnings = null)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        string text;
        try
        {
            bool tooLong;
            (text, tooLong) = ReadHead(filePath);
            if (tooLong)
            {
                // A frontmatter block that is still open at the scan bound is too long to read, not unclosed: nothing is known about its
                // description, a key line is not a summary, and the file is not malformed, so there is no warning.
                return fileName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return fileName;
        }

        NoteFrontmatterBlock block = NoteFrontmatter.Locate(text);
        if (block.Text is null)
        {
            WarnIfUnclosed(text, filePath, warnings);
            return FirstLine(text) ?? fileName;
        }

        IReadOnlyList<NoteProperty> properties = NoteProperties.Read(block);
        if (MemoryFileRules.DescriptionIn(properties) is { } description)
        {
            return Clip(description);
        }

        if (properties.Any(MemoryFileRules.IsDescriptionKey))
        {
            warnings?.Malformed(filePath, text, "its description is not a single line of text");
        }

        return FirstLine(text[block.BodyStart..]) ?? fileName;
    }

    /// <summary>
    /// Reads the start of the file as lines: a frontmatter block and the first non-blank line after it, or the first non-blank line of a
    /// file without one. Stops at the bounds, so a large file is never read whole.
    /// </summary>
    /// <param name="filePath">The memory file.</param>
    /// <returns>
    /// The lines read, joined by line feeds, and whether the bound was hit with a frontmatter block still open and more of the file unread: the
    /// block is then too long to read, which is not the same as a block that never closes.
    /// </returns>
    private static (string Text, bool TooLong) ReadHead(string filePath)
    {
        using StreamReader reader = new(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        StringBuilder head = new();
        bool inFrontmatter = false;
        bool closed = false;
        int lineNumber = 0;

        while (lineNumber < MaxScannedLines && head.Length < MaxScannedCharacters && reader.ReadLine() is { } line)
        {
            lineNumber++;
            _ = head.Append(line).Append('\n');

            if (lineNumber == 1 && FrontmatterFence.IsOpening(line))
            {
                inFrontmatter = true;
            }
            else if (inFrontmatter && !closed)
            {
                closed = FrontmatterFence.IsClosing(line);
            }
            else if (line.Trim().Length > 0 && (closed || !inFrontmatter) && !line.TrimStart().StartsWith('{'))
            {
                // A line opening with "{" may start the JSON form of frontmatter (Library spec D-9), which has no marker line: keep
                // reading so NoteFrontmatter.Locate sees the whole object and the summary comes from its description or the body after it.
                break;
            }
        }

        bool tooLong = inFrontmatter && !closed && reader.Peek() >= 0;
        return (head.ToString(), tooLong);
    }

    /// <summary>The first non-blank line of <paramref name="text"/>, minus a leading <c>#</c> and spaces and a stray marker line, cut to the summary length.</summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The summary line, or <see langword="null"/> when there is none.</returns>
    private static string? FirstLine(string text)
    {
        using StringReader reader = new(text);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.TrimStart('#').Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, FrontmatterMarker, StringComparison.Ordinal))
            {
                continue;
            }

            return Clip(trimmed);
        }

        return null;
    }

    /// <summary>Cuts <paramref name="value"/> at <see cref="MaxSummaryLength"/> characters and adds an ellipsis when it was longer.</summary>
    /// <param name="value">The text.</param>
    private static string Clip(string value) =>
        value.Length > MaxSummaryLength ? value[..MaxSummaryLength] + "…" : value;

    /// <summary>Warns when the text opens a frontmatter block that never closes.</summary>
    /// <param name="text">The part of the file that was read.</param>
    /// <param name="filePath">The file.</param>
    /// <param name="warnings">The warnings sink, or <see langword="null"/>.</param>
    private static void WarnIfUnclosed(string text, string filePath, MemoryWarnings? warnings)
    {
        int lineEnd = text.IndexOfAny(['\r', '\n']);
        string firstLine = lineEnd < 0 ? text : text[..lineEnd];
        if (FrontmatterFence.IsOpening(firstLine))
        {
            warnings?.Malformed(filePath, text, "its frontmatter block never closes");
        }
    }
}
