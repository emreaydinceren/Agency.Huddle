using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins Global Search by alias (front matter properties spec D-7): a note is found by any alias as it is by its file name, and the hit names the alias that matched.</summary>
public sealed class LibrarySearchAliasTests
{
    private static LibrarySearchProvider Provider(LibraryFileServiceFixture fixture) =>
        new(fixture.RootStore, fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions { DataDir = fixture.DataDir }), null, fixture.Index);

    private static SearchQuery Query(string text, SearchPurpose purpose) =>
        new(text, SearchFilter.Library, null, SearchScope.Active, SearchLimits.SingleFilterCap, purpose);

    private static void Seed(LibraryFileServiceFixture fixture)
    {
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "AllTypes.md"), "---\naliases: [Plan, Q1 plan]\n---\n\n# Types\n");
        File.WriteAllText(Path.Combine(root, "Other.md"), "# Other\n");
        fixture.Reload();
    }

    /// <summary>FP-T12: a names-only search finds the note by an alias, shows the file name and carries the alias that matched.</summary>
    [Fact]
    public async Task Search_ByAlias_FindsTheNote_NamesOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Seed(fixture);

        SearchResult result = await Provider(fixture).SearchAsync(Query("q1 plan", SearchPurpose.Reference), ct);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal("AllTypes.md", hit.RelativePath);
        Assert.Equal("AllTypes.md", hit.Title);
        Assert.Equal("Alias: Q1 plan", hit.Snippet);
        Assert.Equal(MatchTier.ExactName, hit.Tier);
    }

    /// <summary>FP-T12: an alias that matches as well as the file contents does, ranks as a name, once, not as a content match.</summary>
    [Fact]
    public async Task Search_ByAlias_RanksAsAName_AndIsListedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Seed(fixture);

        SearchResult result = await Provider(fixture).SearchAsync(Query("Plan", SearchPurpose.Box), ct);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal(MatchTier.ExactName, hit.Tier);
    }

    /// <summary>FP-T12: a phrase that is no alias and no name finds nothing.</summary>
    [Fact]
    public async Task Search_NoMatch_FindsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Seed(fixture);

        SearchResult result = await Provider(fixture).SearchAsync(Query("zzz-none", SearchPurpose.Reference), ct);

        Assert.Empty(result.Hits);
    }

    private static SearchQuery Capped(string text, int max) =>
        new(text, SearchFilter.Library, null, SearchScope.Active, max, SearchPurpose.Reference);

    /// <summary>Fix 5b: alias hits count toward the per-root cap and mark the result truncated, as content hits do.</summary>
    [Fact]
    public async Task Search_AliasHits_RespectTheCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        foreach (string name in new[] { "A", "B", "C" })
        {
            File.WriteAllText(Path.Combine(root, name + ".md"), "---\naliases: [zzz-" + name + "]\n---\n");
        }

        fixture.Reload();

        SearchResult result = await Provider(fixture).SearchAsync(Capped("zzz", 2), ct);

        Assert.Equal(2, result.Hits.Count);
        Assert.True(result.Truncated);
    }

    /// <summary>Fix 5b: alias hits fill only what the name hits leave of the cap.</summary>
    [Fact]
    public async Task Search_AliasHits_ShareTheCapWithNameHits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "zzz-name.md"), "# Name\n");
        File.WriteAllText(Path.Combine(root, "A.md"), "---\naliases: [zzz-a]\n---\n");
        File.WriteAllText(Path.Combine(root, "B.md"), "---\naliases: [zzz-b]\n---\n");
        fixture.Reload();

        SearchResult result = await Provider(fixture).SearchAsync(Capped("zzz", 2), ct);

        Assert.Equal(2, result.Hits.Count);
        Assert.True(result.Truncated);
    }

    /// <summary>Fix 5a: a search cancelled before the alias pass stops instead of building the link index.</summary>
    [Fact]
    public async Task Search_Cancelled_Throws()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Seed(fixture);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(fixture).SearchAsync(Query("plan", SearchPurpose.Reference), cancelled.Token));
    }
}