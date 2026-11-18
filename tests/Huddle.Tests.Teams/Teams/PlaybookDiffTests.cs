using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="PlaybookDiff"/>, the line diff behind "Show changes".</summary>
public sealed class PlaybookDiffTests
{
    private static string Numbered(int count, params (int Line, string Text)[] replace)
    {
        List<string> lines = [];
        for (int i = 1; i <= count; i++)
        {
            string text = "l" + i;
            foreach ((int line, string value) in replace)
            {
                if (line == i)
                {
                    text = value;
                }
            }

            lines.Add(text);
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>One changed line in ten shows its removal and addition plus two lines of context either side.</summary>
    [Fact]
    public void Lines_OneChangedLine_ShowsOnlyItWithTwoLinesOfContext()
    {
        string diff = PlaybookDiff.Lines("Member.md", Numbered(10), Numbered(10, (5, "X")));

        Assert.Equal("--- Member.md\n+++ Member.md\n@@\n l3\n l4\n-l5\n+X\n l6\n l7", diff);
    }

    /// <summary>An added line shows as a single plus line among its context.</summary>
    [Fact]
    public void Lines_AddedLine_ShowsAPlusLine()
    {
        string diff = PlaybookDiff.Lines("f.md", "a\nb\nc\n", "a\nb\nNEW\nc\n");

        Assert.Equal("--- f.md\n+++ f.md\n@@\n a\n b\n+NEW\n c", diff);
    }

    /// <summary>A removed line shows as a single minus line among its context.</summary>
    [Fact]
    public void Lines_RemovedLine_ShowsAMinusLine()
    {
        string diff = PlaybookDiff.Lines("f.md", "a\nb\nc\nd\n", "a\nc\nd\n");

        Assert.Equal("--- f.md\n+++ f.md\n@@\n a\n-b\n c\n d", diff);
    }

    /// <summary>Two changes far apart give two hunks, each with its own <c>@@</c> line.</summary>
    [Fact]
    public void Lines_TwoFarApartChanges_GiveTwoHunks()
    {
        string diff = PlaybookDiff.Lines("f.md", Numbered(20), Numbered(20, (3, "X"), (18, "Y")));

        Assert.Equal(
            "--- f.md\n+++ f.md\n@@\n l1\n l2\n-l3\n+X\n l4\n l5\n@@\n l16\n l17\n-l18\n+Y\n l19\n l20",
            diff);
    }

    /// <summary>Identical texts give an empty string.</summary>
    [Fact]
    public void Lines_IdenticalTexts_AreEmpty()
    {
        string diff = PlaybookDiff.Lines("f.md", Numbered(5), Numbered(5));

        Assert.Equal(string.Empty, diff);
    }

    /// <summary>CRLF input gives the same diff as LF input.</summary>
    [Fact]
    public void Lines_CrlfInput_EqualsLfInput()
    {
        string lf = PlaybookDiff.Lines("f.md", Numbered(10), Numbered(10, (5, "X")));
        string crlf = PlaybookDiff.Lines(
            "f.md",
            Numbered(10).Replace("\n", "\r\n", StringComparison.Ordinal),
            Numbered(10, (5, "X")).Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(lf, crlf);
    }
}
