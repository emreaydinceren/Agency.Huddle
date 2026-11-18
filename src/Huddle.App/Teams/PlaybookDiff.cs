namespace Agency.Huddle.App.Teams;

/// <summary>Computes the line diff the Playbook tab's "Show changes" shows: only the lines that changed, with a little context.</summary>
internal static class PlaybookDiff
{
    /// <summary>Writes the unified diff of two texts, line by line.</summary>
    /// <param name="fileName">The file name for the header.</param>
    /// <param name="before">The old text.</param>
    /// <param name="after">The new text.</param>
    /// <param name="context">How many unchanged lines to show around each changed run.</param>
    /// <returns>The diff in the shape the proposal card writes, with an <c>@@</c> line before each hunk; empty when the texts match.</returns>
    public static string Lines(string fileName, string before, string after, int context = 2)
    {
        string[] oldLines = Split(before);
        string[] newLines = Split(after);
        List<(char Prefix, string Text)> ops = Align(oldLines, newLines);
        if (ops.TrueForAll(static op => op.Prefix == ' '))
        {
            return string.Empty;
        }

        bool[] keep = new bool[ops.Count];
        for (int i = 0; i < ops.Count; i++)
        {
            if (ops[i].Prefix == ' ')
            {
                continue;
            }

            int from = Math.Max(0, i - context);
            int to = Math.Min(ops.Count - 1, i + context);
            for (int j = from; j <= to; j++)
            {
                keep[j] = true;
            }
        }

        List<string> lines = ["--- " + fileName, "+++ " + fileName];
        bool inHunk = false;
        for (int i = 0; i < ops.Count; i++)
        {
            if (!keep[i])
            {
                inHunk = false;
                continue;
            }

            if (!inHunk)
            {
                lines.Add("@@");
                inHunk = true;
            }

            lines.Add(ops[i].Prefix + ops[i].Text);
        }

        return string.Join('\n', lines);
    }

    /// <summary>Splits a text into lines after folding CRLF to LF, dropping the empty piece a final line feed leaves.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines.</returns>
    private static string[] Split(string text)
    {
        string lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (lf.Length == 0)
        {
            return [];
        }

        if (lf.EndsWith('\n'))
        {
            lf = lf[..^1];
        }

        return lf.Split('\n');
    }

    /// <summary>Aligns two line lists by longest common subsequence.</summary>
    /// <param name="oldLines">The old lines.</param>
    /// <param name="newLines">The new lines.</param>
    /// <returns>Each line with a space (kept), <c>-</c> (removed) or <c>+</c> (added).</returns>
    private static List<(char Prefix, string Text)> Align(string[] oldLines, string[] newLines)
    {
        int[,] table = new int[oldLines.Length + 1, newLines.Length + 1];
        for (int i = oldLines.Length - 1; i >= 0; i--)
        {
            for (int j = newLines.Length - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        List<(char Prefix, string Text)> ops = [];
        int a = 0;
        int b = 0;
        while (a < oldLines.Length && b < newLines.Length)
        {
            if (string.Equals(oldLines[a], newLines[b], StringComparison.Ordinal))
            {
                ops.Add((' ', oldLines[a++]));
                b++;
            }
            else if (table[a + 1, b] >= table[a, b + 1])
            {
                ops.Add(('-', oldLines[a++]));
            }
            else
            {
                ops.Add(('+', newLines[b++]));
            }
        }

        while (a < oldLines.Length)
        {
            ops.Add(('-', oldLines[a++]));
        }

        while (b < newLines.Length)
        {
            ops.Add(('+', newLines[b++]));
        }

        return ops;
    }
}
