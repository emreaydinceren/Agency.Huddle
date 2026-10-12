using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchSnippet.Around"/>: the first matching line, trimmed and cut to a fixed width around the match.</summary>
public sealed class SearchSnippetTests
{
    /// <summary>A line that fits the width comes back trimmed and whole, with no ellipses.</summary>
    [Fact]
    public void Around_ShortLine_ReturnsTheTrimmedLine()
    {
        string? snippet = SearchSnippet.Around("  hello world  ", "world", 120);

        Assert.Equal("hello world", snippet);
    }

    /// <summary>The first line containing the phrase is the snippet, even when a later line also matches.</summary>
    [Fact]
    public void Around_FirstMatchingLineWins()
    {
        string? snippet = SearchSnippet.Around("alpha\nbeta match\ngamma match", "match", 120);

        Assert.Equal("beta match", snippet);
    }

    /// <summary>A long line with the match in the middle is cut around it and ellipsed at both ends.</summary>
    [Fact]
    public void Around_LongLineMatchInMiddle_EllipsesBothEnds()
    {
        string line = new string('a', 100) + "NEEDLE" + new string('b', 100);

        string? snippet = SearchSnippet.Around(line, "NEEDLE", 20);

        Assert.Equal("…" + new string('a', 7) + "NEEDLE" + new string('b', 7) + "…", snippet);
    }

    /// <summary>A match at the start of a long line has no leading ellipsis.</summary>
    [Fact]
    public void Around_MatchAtStart_NoLeadingEllipsis()
    {
        string line = "NEEDLE" + new string('b', 200);

        string? snippet = SearchSnippet.Around(line, "NEEDLE", 20);

        Assert.Equal("NEEDLE" + new string('b', 14) + "…", snippet);
    }

    /// <summary>A match at the end of a long line has no trailing ellipsis, and the window is pulled back inside the line.</summary>
    [Fact]
    public void Around_MatchAtEnd_NoTrailingEllipsis()
    {
        string line = new string('a', 200) + "NEEDLE";

        string? snippet = SearchSnippet.Around(line, "NEEDLE", 20);

        Assert.Equal("…" + new string('a', 14) + "NEEDLE", snippet);
    }

    /// <summary>No line containing the phrase gives no snippet at all.</summary>
    [Fact]
    public void Around_NoMatch_ReturnsNull()
    {
        string? snippet = SearchSnippet.Around("nothing here", "needle", 120);

        Assert.Null(snippet);
    }

    /// <summary>The phrase matches ignoring case, and the snippet keeps the line's own casing.</summary>
    [Fact]
    public void Around_IgnoresCase()
    {
        string? snippet = SearchSnippet.Around("Hello NEEDLE there", "needle", 120);

        Assert.Equal("Hello NEEDLE there", snippet);
    }
}
