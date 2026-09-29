using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

public sealed class LibrarySearchResultTests
{
    /// <summary>A search result holds its hits and truncated state.</summary>
    [Fact]
    public void LibrarySearchResult_Holds_HitsAndTruncated()
    {
        IReadOnlyList<LibraryEntry> hits = [];
        bool truncated = true;

        LibrarySearchResult result = new(hits, truncated);

        Assert.Same(hits, result.Hits);
        Assert.True(result.Truncated);
    }

    /// <summary>A search result's truncated state can be changed via `with`.</summary>
    [Fact]
    public void LibrarySearchResult_With_ChangesTruncated()
    {
        LibrarySearchResult original = new([], true);

        LibrarySearchResult modified = original with { Truncated = false };

        Assert.False(modified.Truncated);
        Assert.True(original.Truncated);
    }
}
