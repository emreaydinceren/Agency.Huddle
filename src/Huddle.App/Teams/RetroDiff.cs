namespace Agency.Huddle.App.Teams;

/// <summary>Computes the unified diff the proposal card shows. The agent never supplies a diff; Huddle derives it from the current file.</summary>
internal static class RetroDiff
{
    /// <summary>Counts the places where <paramref name="find"/> starts in <paramref name="text"/>, overlapping starts included.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="find">The text to look for; empty counts as no match.</param>
    /// <returns>The number of starts.</returns>
    public static int CountMatches(string text, string find)
    {
        if (find.Length == 0)
        {
            return 0;
        }

        int count = 0;
        int index = text.IndexOf(find, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(find, index + 1, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Gives <paramref name="value"/> the line endings of <paramref name="text"/>: CRLF when the file uses them, else LF.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="value">The agent's find or replace text.</param>
    /// <returns>The text with matching line endings.</returns>
    public static string FitLineEndings(string text, string value)
    {
        string lf = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Contains("\r\n", StringComparison.Ordinal) ? lf.Replace("\n", "\r\n", StringComparison.Ordinal) : lf;
    }

    /// <summary>Writes the diff of replacing the one occurrence of <paramref name="find"/>: a header, one line of context either side, then the old lines with <c>-</c> and the new lines with <c>+</c>.</summary>
    /// <param name="file">The file name for the header.</param>
    /// <param name="text">The current file text.</param>
    /// <param name="find">The text to replace; it must occur in <paramref name="text"/>.</param>
    /// <param name="replace">The replacement.</param>
    /// <returns>The diff, lines joined with a line feed and no trailing line feed.</returns>
    public static string Unified(string file, string text, string find, string replace)
    {
        int start = text.IndexOf(find, StringComparison.Ordinal);
        int end = start + find.Length;
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        int lineEnd = text.IndexOf('\n', end);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        string oldBlock = text[lineStart..lineEnd];
        string newBlock = string.Concat(text.AsSpan(lineStart, start - lineStart), replace, text.AsSpan(end, lineEnd - end));
        List<string> lines = ["--- " + file, "+++ " + file, "@@"];
        if (lineStart > 0)
        {
            int previousEnd = lineStart - 1;
            int previousStart = previousEnd == 0 ? 0 : text.LastIndexOf('\n', previousEnd - 1) + 1;
            lines.Add(" " + text[previousStart..previousEnd].TrimEnd('\r'));
        }

        foreach (string line in oldBlock.Split('\n'))
        {
            lines.Add("-" + line.TrimEnd('\r'));
        }

        if (newBlock.Length > 0)
        {
            foreach (string line in newBlock.Split('\n'))
            {
                lines.Add("+" + line.TrimEnd('\r'));
            }
        }

        if (lineEnd + 1 < text.Length)
        {
            int nextStart = lineEnd + 1;
            int nextEnd = text.IndexOf('\n', nextStart);
            if (nextEnd < 0)
            {
                nextEnd = text.Length;
            }

            lines.Add(" " + text[nextStart..nextEnd].TrimEnd('\r'));
        }

        return string.Join('\n', lines);
    }
}
