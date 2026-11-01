namespace Agency.Huddle.App.Library;

/// <summary>
/// The one rule for where a Markdown file's YAML frontmatter opens and closes, shared by every reader that has to decide it
/// (notes, the Memory index, Personas, Tasks, Team files, Process files and the Candidate check). The rule is lenient: the first
/// line opens a block when, after one optional byte-order mark and surrounding whitespace, it is exactly <c>---</c>; a later
/// line closes it when, trimmed, it is exactly <c>---</c> or <c>...</c>. A block that never closes is not frontmatter.
/// Only the rule is shared. Each reader keeps its own field grammar and its own line representation.
/// </summary>
internal static class FrontmatterFence
{
    /// <summary>The Unicode byte-order mark (U+FEFF) a Windows editor puts in front of the first line.</summary>
    internal const char ByteOrderMark = '\uFEFF';

    private const string Dashes = "---";
    private const string Dots = "...";

    /// <summary>Whether <paramref name="line"/>, meant to be a file's first line, opens a frontmatter block.</summary>
    /// <param name="line">The first line, with or without its line break.</param>
    /// <returns><see langword="true"/> when, after dropping one leading byte-order mark and trimming whitespace, the line is exactly <c>---</c>.</returns>
    internal static bool IsOpening(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return IsOpening(line.AsSpan());
    }

    /// <summary>Whether <paramref name="line"/>, meant to be a file's first line, opens a frontmatter block.</summary>
    /// <param name="line">The first line, with or without its line break.</param>
    /// <returns><see langword="true"/> when, after dropping one leading byte-order mark and trimming whitespace, the line is exactly <c>---</c>.</returns>
    internal static bool IsOpening(ReadOnlySpan<char> line)
    {
        if (!line.IsEmpty && line[0] == ByteOrderMark)
        {
            line = line[1..];
        }

        return line.Trim().SequenceEqual(Dashes);
    }

    /// <summary>Whether <paramref name="line"/> closes a frontmatter block.</summary>
    /// <param name="line">A line after the opening one, with or without its line break.</param>
    /// <returns><see langword="true"/> when the trimmed line is exactly <c>---</c> or <c>...</c>.</returns>
    internal static bool IsClosing(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return IsClosing(line.AsSpan());
    }

    /// <summary>Whether <paramref name="line"/> closes a frontmatter block.</summary>
    /// <param name="line">A line after the opening one, with or without its line break.</param>
    /// <returns><see langword="true"/> when the trimmed line is exactly <c>---</c> or <c>...</c>.</returns>
    internal static bool IsClosing(ReadOnlySpan<char> line)
    {
        ReadOnlySpan<char> trimmed = line.Trim();
        return trimmed.SequenceEqual(Dashes) || trimmed.SequenceEqual(Dots);
    }

    /// <summary>Finds the line that closes a block whose opening line the caller has already checked.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <param name="from">The index to start at, normally 1.</param>
    /// <returns>The index of the first closing line at or after <paramref name="from"/>, or -1 when there is none.</returns>
    internal static int FindClosing(IReadOnlyList<string> lines, int from)
    {
        ArgumentNullException.ThrowIfNull(lines);

        for (int index = Math.Max(from, 0); index < lines.Count; index++)
        {
            if (IsClosing(lines[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
