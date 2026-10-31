using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchResultRanker.Rank"/>: open Team first, then tier, active before archived, the alphabetical tie-break and the cap (spec §7.4; GS-T2).</summary>
public sealed class SearchResultRankerTests
{
    /// <summary>The open Team's hit comes before a better-matching hit from another Team.</summary>
    [Fact]
    public void Rank_OpenTeamFirst()
    {
        SearchHit[] hits =
        [
            new TeamHit("Alpha", null, 0, MatchTier.ExactName),
            new LibraryHit("lib", "Library", "Marketing/plan.md", "Marketing", false, null, "Marketing", MatchTier.ContentMatch),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query("Marketing"));

        Assert.Equal(["plan.md", "Alpha"], Titles(result));
    }

    /// <summary>With no open Team, the order is the tier order and the name is only a tie-break.</summary>
    [Fact]
    public void Rank_NoOpenTeam_TierOrderOnly()
    {
        SearchHit[] hits =
        [
            new TeamHit("Beta", null, 0, MatchTier.NameContains),
            new TeamHit("Gamma", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query(null));

        Assert.Equal(["Gamma", "Beta"], Titles(result));
    }

    /// <summary>The four tiers come out best first, whatever order they went in.</summary>
    [Fact]
    public void Rank_TiersInOrder()
    {
        SearchHit[] hits =
        [
            new LibraryHit("lib", "Library", "d.md", string.Empty, false, "snippet", null, MatchTier.ContentMatch),
            new TeamHit("c", null, 0, MatchTier.NameContains),
            new TeamHit("b", null, 0, MatchTier.NamePrefix),
            new TeamHit("a", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query(null));

        Assert.Equal(["a", "b", "c", "d.md"], Titles(result));
    }

    /// <summary>Among equal tiers, an active Task comes before a Closed one even when the Closed one's title sorts first.</summary>
    [Fact]
    public void Rank_ActiveBeforeArchived()
    {
        SearchHit[] hits =
        [
            new TaskHit(new TaskId("PLAT", 1), "a-closed", TaskState.Done, null, "Platform", true, MatchTier.ExactName),
            new TaskHit(new TaskId("PLAT", 2), "b-open", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query(null));

        Assert.Equal(["b-open", "a-closed"], Titles(result));
    }

    /// <summary>Equal keys are ordered by title, ignoring case, so <c>alpha</c> sorts before <c>alpha2</c> and both before <c>beta</c>.</summary>
    [Fact]
    public void Rank_AlphabeticalTieBreak_IgnoresCase()
    {
        SearchHit[] hits =
        [
            new TeamHit("beta", null, 0, MatchTier.ExactName),
            new TeamHit("Alpha", null, 0, MatchTier.ExactName),
            new TeamHit("alpha2", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query(null));

        Assert.Equal(["Alpha", "alpha2", "beta"], Titles(result));
    }

    /// <summary>More hits than the cap are cut to the cap, and the result says it was truncated.</summary>
    [Fact]
    public void Rank_CutsToMaxHits_SetsTruncated()
    {
        SearchHit[] hits =
        [
            new TeamHit("a", null, 0, MatchTier.ExactName),
            new TeamHit("b", null, 0, MatchTier.ExactName),
            new TeamHit("c", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query(null, maxHits: 2));

        Assert.Equal(["a", "b"], Titles(result));
        Assert.True(result.Truncated);
    }

    /// <summary>A provider that already truncated its search keeps the result truncated, even when nothing is cut here.</summary>
    [Fact]
    public void Rank_ProviderTruncated_StaysTruncated()
    {
        SearchHit[] hits = [new TeamHit("a", null, 0, MatchTier.ExactName)];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, true), Query(null));

        Assert.Equal(["a"], Titles(result));
        Assert.True(result.Truncated);
    }

    /// <summary>No hits in, no hits out, and not truncated.</summary>
    [Fact]
    public void Rank_EmptyInput_EmptyNotTruncated()
    {
        SearchResult result = SearchResultRanker.Rank(SearchResult.Empty, Query(null));

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>An Agent ranks first when any one of its Teams is the open Team, not only its first Team.</summary>
    [Fact]
    public void Rank_AgentInAnyOpenTeam_RanksFirst()
    {
        SearchHit[] hits =
        [
            new TeamHit("Aardvark", null, 0, MatchTier.ExactName),
            new AgentHit("Nova", "Router", "nova", ["Marketing", "Platform"], MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query("Platform"));

        Assert.Equal(["Nova", "Aardvark"], Titles(result));
    }

    /// <summary>A Room never ranks by Team: a Room titled like the open Team gets no open-Team boost.</summary>
    [Fact]
    public void Rank_RoomHitNeverRanksByTeam()
    {
        SearchHit[] hits =
        [
            new RoomHit("room-1", "Platform", 1, null, null, "snippet", null, false, MatchTier.ContentMatch),
            new TeamHit("Alpha", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(new SearchResult(hits, false), Query("Platform"));

        Assert.Equal(["Alpha", "Platform"], Titles(result));
    }

    /// <summary>Builds a query with the given open Team and cap, Active scope, All filter.</summary>
    /// <param name="openTeam">The open Team, or null.</param>
    /// <param name="maxHits">The per-group cap.</param>
    private static SearchQuery Query(string? openTeam, int maxHits = 50)
    {
        return new SearchQuery("phrase", SearchFilter.All, openTeam, SearchScope.Active, maxHits);
    }

    /// <summary>The titles of a result's hits, in order.</summary>
    /// <param name="result">The result to read.</param>
    private static string[] Titles(SearchResult result)
    {
        return [.. result.Hits.Select(hit => hit.Title)];
    }
}
