using System.Text;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// The line-level difference between two <c>AGENTS.md</c> bodies (Spec §8.2, D-13), written in-house as a line LCS with
/// no package. Line endings are normalised first, so they never count. Bodies can reach <c>MaxLedgerChars</c> (256000
/// characters), so the LCS first trims the common leading and trailing lines, which keeps its table small: only the
/// changed middle is tabled, and a middle too large for the table (<see cref="MaxTableCells"/>) is reported as one
/// hunk that removes every old line and adds every new line, rather than allocating without bound.
/// </summary>
internal sealed class AgentsMdComparison
{
    /// <summary>The most cells the LCS table may hold (16 MB of integers); a larger middle becomes one whole-middle hunk.</summary>
    private const int MaxTableCells = 4_000_000;

    /// <summary>How many unchanged lines <see cref="AgentsMdComparisonHunk.Before"/> and <see cref="AgentsMdComparisonHunk.After"/> hold.</summary>
    private const int ContextLines = 2;

    private AgentsMdComparison(IReadOnlyList<AgentsMdComparisonHunk> hunks)
    {
        this.Hunks = hunks;
    }

    /// <summary>The runs of changed lines, in file order; empty when the texts match.</summary>
    public IReadOnlyList<AgentsMdComparisonHunk> Hunks { get; }

    /// <summary>
    /// The characters that changed, counted per hunk so a one-letter fix in a long line is small: each side of a hunk is
    /// joined, the common prefix and suffix are trimmed, the longer middle counts, and the hunks are summed.
    /// </summary>
    public int ChangedChars
    {
        get
        {
            int total = 0;
            foreach (AgentsMdComparisonHunk hunk in this.Hunks)
            {
                total += HunkChars(string.Join('\n', hunk.Removed), string.Join('\n', hunk.Added));
            }

            return total;
        }
    }

    /// <summary>Compares two bodies line by line.</summary>
    /// <param name="oldText">The text before the change.</param>
    /// <param name="newText">The text after the change.</param>
    /// <returns>The comparison; its <see cref="Hunks"/> are empty when only line endings differ.</returns>
    public static AgentsMdComparison Between(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        string[] oldLines = SplitLines(oldText);
        string[] newLines = SplitLines(newText);

        int prefix = 0;
        while (prefix < oldLines.Length && prefix < newLines.Length && string.Equals(oldLines[prefix], newLines[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
            && string.Equals(oldLines[^(suffix + 1)], newLines[^(suffix + 1)], StringComparison.Ordinal))
        {
            suffix++;
        }

        return new AgentsMdComparison(BuildHunks(oldLines, newLines, prefix, suffix));
    }

    /// <summary>Lists the changed lines as <c>-old</c> then <c>+new</c> per hunk, stopping before the cap is passed.</summary>
    /// <param name="maxChars">The cap on the text of the listed lines, the <c>-</c> and <c>+</c> not counted.</param>
    /// <returns>The lines shown and how many changed lines were left out.</returns>
    public (IReadOnlyList<string> Lines, int NotShown) Lines(int maxChars)
    {
        List<string> all = [];
        List<int> lengths = [];
        foreach (AgentsMdComparisonHunk hunk in this.Hunks)
        {
            foreach (string line in hunk.Removed)
            {
                all.Add("-" + line);
                lengths.Add(line.Length);
            }

            foreach (string line in hunk.Added)
            {
                all.Add("+" + line);
                lengths.Add(line.Length);
            }
        }

        int used = 0;
        int shown = 0;
        while (shown < all.Count && used + lengths[shown] <= maxChars)
        {
            used += lengths[shown];
            shown++;
        }

        return (all.GetRange(0, shown), all.Count - shown);
    }

    /// <summary>The changed lines with two unchanged lines either side, for the Helper to quote from.</summary>
    /// <param name="maxChars">The cap on the whole text; whole lines past it are left out.</param>
    /// <returns>Unified-style lines (space, minus, plus), hunks separated by a line of three dots.</returns>
    public string ContextText(int maxChars)
    {
        StringBuilder text = new();
        bool first = true;
        foreach (AgentsMdComparisonHunk hunk in this.Hunks)
        {
            List<string> lines = [];
            lines.AddRange(hunk.Before.Select(static line => " " + line));
            lines.AddRange(hunk.Removed.Select(static line => "-" + line));
            lines.AddRange(hunk.Added.Select(static line => "+" + line));
            lines.AddRange(hunk.After.Select(static line => " " + line));
            if (!first)
            {
                lines.Insert(0, "...");
            }

            foreach (string line in lines)
            {
                int added = line.Length + (text.Length == 0 ? 0 : 1);
                if (text.Length + added > maxChars)
                {
                    return text.ToString();
                }

                text.Append(text.Length == 0 ? string.Empty : "\n").Append(line);
            }

            first = false;
        }

        return text.ToString();
    }

    private static string[] SplitLines(string text)
    {
        string normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        return normalised.Length == 0 ? [] : normalised.Split('\n');
    }

    private static int HunkChars(string removed, string added)
    {
        int shortest = Math.Min(removed.Length, added.Length);
        int prefix = 0;
        while (prefix < shortest && removed[prefix] == added[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < shortest - prefix && removed[^(suffix + 1)] == added[^(suffix + 1)])
        {
            suffix++;
        }

        return Math.Max(removed.Length, added.Length) - prefix - suffix;
    }

    private static List<AgentsMdComparisonHunk> BuildHunks(string[] oldLines, string[] newLines, int prefix, int suffix)
    {
        int oldMiddle = oldLines.Length - prefix - suffix;
        int newMiddle = newLines.Length - prefix - suffix;
        List<AgentsMdComparisonHunk> hunks = [];
        if (oldMiddle == 0 && newMiddle == 0)
        {
            return hunks;
        }

        if ((long)(oldMiddle + 1) * (newMiddle + 1) > MaxTableCells)
        {
            hunks.Add(MakeHunk(oldLines, prefix, oldMiddle, newLines, prefix, newMiddle));
            return hunks;
        }

        int[,] table = BuildTable(oldLines, newLines, prefix, oldMiddle, newMiddle);
        int i = 0;
        int j = 0;
        int removedFrom = -1;
        int addedFrom = -1;
        while (i < oldMiddle || j < newMiddle)
        {
            bool match = i < oldMiddle && j < newMiddle
                && string.Equals(oldLines[prefix + i], newLines[prefix + j], StringComparison.Ordinal);
            if (match)
            {
                if (removedFrom >= 0)
                {
                    hunks.Add(MakeHunk(oldLines, prefix + removedFrom, i - removedFrom, newLines, prefix + addedFrom, j - addedFrom));
                    removedFrom = -1;
                }

                i++;
                j++;
                continue;
            }

            if (removedFrom < 0)
            {
                removedFrom = i;
                addedFrom = j;
            }

            if (j >= newMiddle || (i < oldMiddle && table[i + 1, j] >= table[i, j + 1]))
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        if (removedFrom >= 0)
        {
            hunks.Add(MakeHunk(oldLines, prefix + removedFrom, i - removedFrom, newLines, prefix + addedFrom, j - addedFrom));
        }

        return hunks;
    }

    private static int[,] BuildTable(string[] oldLines, string[] newLines, int offset, int oldMiddle, int newMiddle)
    {
        int[,] table = new int[oldMiddle + 1, newMiddle + 1];
        for (int i = oldMiddle - 1; i >= 0; i--)
        {
            for (int j = newMiddle - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(oldLines[offset + i], newLines[offset + j], StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static AgentsMdComparisonHunk MakeHunk(string[] oldLines, int oldStart, int oldCount, string[] newLines, int newStart, int newCount)
    {
        int beforeFrom = Math.Max(0, oldStart - ContextLines);
        int afterFrom = oldStart + oldCount;
        int afterCount = Math.Min(ContextLines, oldLines.Length - afterFrom);
        return new AgentsMdComparisonHunk(
            oldLines[oldStart..(oldStart + oldCount)],
            newLines[newStart..(newStart + newCount)],
            oldLines[beforeFrom..oldStart],
            oldLines[afterFrom..(afterFrom + afterCount)]);
    }
}
