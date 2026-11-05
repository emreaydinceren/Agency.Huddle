using Agency.Huddle.App.AgentsMdChanges;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdComparison"/> (Spec §8.2, D-13): a line comparison whose hunks list removed lines then added
/// lines, a cap with a count of what was cut, and a <c>ChangedChars</c> that counts per hunk so a one-letter fix is small.
/// </summary>
public sealed class AgentsMdComparisonTests
{
    /// <summary>Each hunk lists its removed lines (prefix minus) and then its added lines (prefix plus), hunks in file order.</summary>
    [Fact]
    public void Between_Hunks_MinusThenPlus()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("a\nb\nc\nd\ne\nf\ng", "a\nB\nc\nd\ne\nF\ng");

        Assert.Equal(2, comparison.Hunks.Count);
        Assert.Equal(["b"], comparison.Hunks[0].Removed);
        Assert.Equal(["B"], comparison.Hunks[0].Added);
        Assert.Equal(["f"], comparison.Hunks[1].Removed);
        Assert.Equal(["F"], comparison.Hunks[1].Added);
        (IReadOnlyList<string> lines, int notShown) = comparison.Lines(1000);
        Assert.Equal(["-b", "+B", "-f", "+F"], lines);
        Assert.Equal(0, notShown);
    }

    /// <summary>Identical texts have no hunks, no lines and no changed characters.</summary>
    [Fact]
    public void Between_NoChange_NoLines()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("a\nb", "a\nb");

        Assert.Empty(comparison.Hunks);
        (IReadOnlyList<string> lines, int notShown) = comparison.Lines(1000);
        Assert.Empty(lines);
        Assert.Equal(0, notShown);
        Assert.Equal(0, comparison.ChangedChars);
    }

    /// <summary>A file that was empty and now has text: every line is added.</summary>
    [Fact]
    public void Between_Empty_AllAdded()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between(string.Empty, "x\ny");

        (IReadOnlyList<string> lines, int notShown) = comparison.Lines(1000);
        Assert.Equal(["+x", "+y"], lines);
        Assert.Equal(0, notShown);
    }

    /// <summary>A file that now has no text: every line is removed.</summary>
    [Fact]
    public void Between_ToEmpty_AllRemoved()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("x\ny", string.Empty);

        (IReadOnlyList<string> lines, int notShown) = comparison.Lines(1000);
        Assert.Equal(["-x", "-y"], lines);
        Assert.Equal(0, notShown);
    }

    /// <summary>The cap counts the text of the lines (not the prefix); lines past it are counted in NotShown, not listed.</summary>
    [Fact]
    public void Lines_CappedAtMaxChars_ReportsNotShown()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between(string.Empty, "aaaa\nbbbb\ncccc");

        (IReadOnlyList<string> lines, int notShown) = comparison.Lines(8);

        Assert.Equal(["+aaaa", "+bbbb"], lines);
        Assert.Equal(1, notShown);
    }

    /// <summary>A one-word fix counts 2, not the length of the line: "recieve" to "receive" differs by a swap.</summary>
    [Fact]
    public void ChangedChars_OneWordFix_Counts2()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("Always recieve the token.", "Always receive the token.");

        Assert.Equal(2, comparison.ChangedChars);
    }

    /// <summary>Two hunks are counted apart and summed.</summary>
    [Fact]
    public void ChangedChars_TwoHunks_Sums()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between(
            "recieve\nsame one\nsame two\nteh",
            "receive\nsame one\nsame two\nthe");

        Assert.Equal(4, comparison.ChangedChars);
    }

    /// <summary>With nothing shared at either end, a hunk counts the longer of its two sides.</summary>
    [Fact]
    public void ChangedChars_WholeRewrite_LongerSide()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("abc", "xyzuvw");

        Assert.Equal(6, comparison.ChangedChars);
    }

    /// <summary>
    /// A changed middle too big for the LCS table (2100 by 2100 lines) becomes one hunk of the whole middle, even though
    /// one line is shared; the characters count the joined sides less the shared suffix "2099".
    /// </summary>
    [Fact]
    public void Between_HugeMiddle_OneHunk()
    {
        string[] oldLines = Enumerable.Range(0, 2100).Select(static i => i == 1000 ? "same" : "a" + i).ToArray();
        string[] newLines = Enumerable.Range(0, 2100).Select(static i => i == 1000 ? "same" : "b" + i).ToArray();

        AgentsMdComparison comparison = AgentsMdComparison.Between(string.Join('\n', oldLines), string.Join('\n', newLines));

        AgentsMdComparisonHunk hunk = Assert.Single(comparison.Hunks);
        Assert.Equal(oldLines, hunk.Removed);
        Assert.Equal(newLines, hunk.Added);
        Assert.Equal(11484, comparison.ChangedChars);
    }

    /// <summary>Line endings never count: a CR LF text against the same text with LF has no change.</summary>
    [Fact]
    public void ChangedChars_LineEndingsIgnored()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between("a\r\nb\r\nc", "a\nb\nc");

        Assert.Empty(comparison.Hunks);
        Assert.Equal(0, comparison.ChangedChars);
    }

    /// <summary>The Helper's context holds the changed lines with two unchanged lines before and after.</summary>
    [Fact]
    public void ContextText_IncludesTwoLinesAround()
    {
        AgentsMdComparison comparison = AgentsMdComparison.Between(
            "l1\nl2\nl3\nl4\nl5\nl6\nl7\nl8\nl9",
            "l1\nl2\nl3\nl4\nL5\nl6\nl7\nl8\nl9");

        Assert.Equal(" l3\n l4\n-l5\n+L5\n l6\n l7", comparison.ContextText(1000));
    }
}
