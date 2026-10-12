using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchContentPolicy.Decide"/>: which Library files have their contents read (spec §8.3).</summary>
public sealed class SearchContentPolicyTests
{
    /// <summary>A small Markdown file is read.</summary>
    [Fact]
    public void Decide_SmallMarkdown_IsRead()
    {
        Assert.Equal(ContentDecision.Read, SearchContentPolicy.Decide("notes.md", 10, new()));
    }

    /// <summary>The extension is compared ignoring case.</summary>
    [Fact]
    public void Decide_UpperCaseExtension_IsRead()
    {
        Assert.Equal(ContentDecision.Read, SearchContentPolicy.Decide("NOTES.MD", 10, new()));
    }

    /// <summary>An extension outside the text list is matched by name only.</summary>
    [Fact]
    public void Decide_NotATextExtension_IsNameOnlyNotText()
    {
        Assert.Equal(ContentDecision.NameOnlyNotText, SearchContentPolicy.Decide("picture.png", 10, new()));
    }

    /// <summary>A file with no extension is matched by name only.</summary>
    [Fact]
    public void Decide_NoExtension_IsNameOnlyNotText()
    {
        Assert.Equal(ContentDecision.NameOnlyNotText, SearchContentPolicy.Decide("README", 10, new()));
    }

    /// <summary>A text file one byte over the limit is matched by name only.</summary>
    [Fact]
    public void Decide_TextFileOverTheLimit_IsNameOnlyTooLarge()
    {
        SearchOptions options = new();

        Assert.Equal(
            ContentDecision.NameOnlyTooLarge,
            SearchContentPolicy.Decide("big.md", options.MaxContentFileBytes + 1, options));
    }

    /// <summary>A text file exactly at the limit is still read.</summary>
    [Fact]
    public void Decide_TextFileExactlyAtTheLimit_IsRead()
    {
        SearchOptions options = new();

        Assert.Equal(
            ContentDecision.Read,
            SearchContentPolicy.Decide("edge.md", options.MaxContentFileBytes, options));
    }

    /// <summary>A large file that is not text is matched by name only, and the size does not change that.</summary>
    [Fact]
    public void Decide_LargeNonTextFile_IsNameOnlyNotText()
    {
        SearchOptions options = new();

        Assert.Equal(
            ContentDecision.NameOnlyNotText,
            SearchContentPolicy.Decide("big.png", options.MaxContentFileBytes + 1, options));
    }

    /// <summary>A configured extension list replaces the default, so Markdown is no longer read.</summary>
    [Fact]
    public void Decide_ConfiguredListWithoutMarkdown_IsNameOnlyNotText()
    {
        SearchOptions options = new() { TextExtensions = [".rst"] };

        Assert.Equal(ContentDecision.NameOnlyNotText, SearchContentPolicy.Decide("notes.md", 10, options));
    }
}
