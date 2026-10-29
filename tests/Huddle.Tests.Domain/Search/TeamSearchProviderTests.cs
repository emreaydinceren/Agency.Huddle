using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="TeamSearchProvider"/>: the Team matches of spec §8.1 and the match tiers of §7.4 (GS-T7).</summary>
public sealed class TeamSearchProviderTests
{
    /// <summary>An empty phrase lists every Team at the ExactName tier, in catalog order.</summary>
    [Fact]
    public async Task Browse_ListsEveryTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Platform"), Team("Marketing")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query(""), ct);

        List<SearchHit> expected =
        [
            new TeamHit("Platform", null, 0, MatchTier.ExactName),
            new TeamHit("Marketing", null, 0, MatchTier.ExactName),
        ];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>The name decides the tier: an exact name, a name prefix and a name that contains the phrase; a non-match is left out.</summary>
    [Fact]
    public async Task Name_ExactPrefixContains_Tiers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Platform"), Team("Platform Ops"), Team("Web Platform"), Team("Sales")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query("platform"), ct);

        List<SearchHit> expected =
        [
            new TeamHit("Platform", null, 0, MatchTier.ExactName),
            new TeamHit("Platform Ops", null, 0, MatchTier.NamePrefix),
            new TeamHit("Web Platform", null, 0, MatchTier.NameContains),
        ];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>A Team whose name misses is found by its team type's display name, and the hit carries that label.</summary>
    [Fact]
    public async Task TeamType_MatchesByDisplayName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Delivery", typeId: "web")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query("web"), ct);

        List<SearchHit> expected = [new TeamHit("Delivery", "Web", 0, MatchTier.ExactName)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>A team type that no valid type has is labelled with its id, and that id is what matches.</summary>
    [Fact]
    public async Task TeamType_UnknownId_FallsBackToId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Delivery", typeId: "mystery")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query("myst"), ct);

        List<SearchHit> expected = [new TeamHit("Delivery", "mystery", 0, MatchTier.NamePrefix)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>The Archived scope has no Teams side, so it returns an empty result at once.</summary>
    [Fact]
    public async Task Archived_ReturnsEmptyAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Platform")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        Task<SearchResult> pending = provider.SearchAsync(Query("", SearchScope.Archived), ct);

        Assert.True(pending.IsCompletedSuccessfully);
        SearchResult result = await pending;
        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>A Team hit carries its Agent count (the Team's Members) and its type label, and its TeamName is its own name.</summary>
    [Fact]
    public async Task Hit_CarriesAgentCountAndTypeLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Platform", members: 3, typeId: "web")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query("platform"), ct);

        List<SearchHit> expected = [new TeamHit("Platform", "Web", 3, MatchTier.ExactName)];
        Assert.Equal(expected, result.Hits);
        Assert.Equal("Platform", result.Hits[0].TeamName);
    }

    /// <summary>Builds a Team with the given Agent count and team type id.</summary>
    /// <param name="name">The Team's name.</param>
    /// <param name="members">How many Agents the Team has.</param>
    /// <param name="typeId">The team type id, or null for a Team with no type.</param>
    /// <returns>The Team summary.</returns>
    private static TeamSummary Team(string name, int members = 0, string? typeId = null) =>
        new(name, [], Enumerable.Repeat("Agent", members).ToList(), true)
        {
            TeamType = typeId is null ? null : new VersionedRef(typeId, "1.0.0"),
        };

    /// <summary>Builds a query over the Teams filter with no open Team.</summary>
    /// <param name="text">The phrase.</param>
    /// <param name="scope">The Active or Archived pass.</param>
    /// <returns>The query.</returns>
    private static SearchQuery Query(string text, SearchScope scope = SearchScope.Active) =>
        new(text, SearchFilter.Teams, null, scope, 50);

    /// <summary>A hit carries its Team's avatar from the catalog, so the card can draw it.</summary>
    [Fact]
    public async Task Hit_CarriesTheTeamsAvatar()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Avatar avatar = new("PL", null, "#1E90FF");
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Platform") with { Avatar = avatar }, Team("Marketing")],
        };
        TeamSearchProvider provider = new(catalog, RoleFixtures.Types(dir));

        SearchResult result = await provider.SearchAsync(Query(""), ct);

        Assert.Equal([avatar, Avatar.None], result.Hits.Cast<TeamHit>().Select(static hit => hit.Avatar));
    }
}
