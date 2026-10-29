using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Search;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="AgentSearchProvider"/>: every Persona is an Agent result, matched by Name, Alias and Title (spec §8.2, §7.4; GS-T7).</summary>
public sealed class AgentSearchProviderTests
{
    /// <summary>An empty phrase lists every Persona, each at the ExactName tier.</summary>
    [Fact]
    public async Task Browse_ListsEveryAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AgentSearchProvider provider = new(harness.Personas);

        SearchResult result = await provider.SearchAsync(Query(""), ct);

        List<string> expected = ["Kai|Kai|Kai||ExactName", "Nova|Nova|Nova|Platform|ExactName"];
        Assert.Equal(expected, Describe(result).Order(StringComparer.Ordinal));
    }

    /// <summary>An Agent is found by its Name, at the NamePrefix tier, and a Persona that misses is left out.</summary>
    [Fact]
    public async Task Name_Matches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddAgent(harness, "Rhea", "Data Analyst", "Ri", "Platform");
        AgentSearchProvider provider = new(harness.Personas);

        SearchResult result = await provider.SearchAsync(Query("rhe"), ct);

        Assert.Equal(["Rhea|Data Analyst|Ri|Platform|NamePrefix"], Describe(result));
    }

    /// <summary>An Agent whose Name misses is found by its Alias, at the Alias's tier.</summary>
    [Fact]
    public async Task Alias_MatchesWhenNameDoesNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddAgent(harness, "Rhea", "Data Analyst", "Ri", "Platform");
        AgentSearchProvider provider = new(harness.Personas);

        SearchResult result = await provider.SearchAsync(Query("ri"), ct);

        Assert.Equal(["Rhea|Data Analyst|Ri|Platform|ExactName"], Describe(result));
    }

    /// <summary>An Agent whose Name and Alias miss is found by its Title, at the Title's tier.</summary>
    [Fact]
    public async Task Title_MatchesWhenNameAndAliasDoNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddAgent(harness, "Rhea", "Data Analyst", "Ri", "Platform");
        AddAgent(harness, "Mara", "Data Analyst", "Mr");
        AgentSearchProvider provider = new(harness.Personas);

        SearchResult result = await provider.SearchAsync(Query("analy"), ct);

        Assert.Equal(["Mara|Data Analyst|Mr||NameContains", "Rhea|Data Analyst|Ri|Platform|NameContains"], Describe(result));
    }

    /// <summary>A Persona with no Team is still an Agent result, with an empty Teams list.</summary>
    [Fact]
    public async Task PersonaWithoutTeam_IsStillAAgentResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AgentSearchProvider provider = new(harness.Personas);

        SearchResult result = await provider.SearchAsync(Query("kai"), ct);

        Assert.Equal(["Kai|Kai|Kai||ExactName"], Describe(result));
    }

    /// <summary>The Archived scope has no Agents side, so it returns an empty result at once.</summary>
    [Fact]
    public async Task Archived_ReturnsEmptyAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AgentSearchProvider provider = new(harness.Personas);

        Task<SearchResult> pending = provider.SearchAsync(Query("", SearchScope.Archived), ct);

        Assert.True(pending.IsCompletedSuccessfully);
        SearchResult result = await pending;
        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>Adds a Persona with the given identity to the harness's store.</summary>
    /// <param name="harness">The harness whose Personas receive the new one.</param>
    /// <param name="name">The Persona's Name.</param>
    /// <param name="title">The Persona's Title.</param>
    /// <param name="alias">The Persona's Alias.</param>
    /// <param name="teams">The Teams the Persona belongs to.</param>
    private static void AddAgent(TaskToolHarness harness, string name, string title, string alias, params string[] teams)
    {
        _ = harness.Personas.Add(new PersonaIdentity(name, title, alias, teams), $"You are {name}.");
    }

    /// <summary>Renders each Agent hit as one line, so a whole result compares by value (the Teams list is a reference type).</summary>
    /// <param name="result">The provider's result.</param>
    /// <returns>One line per hit: Name, Title, Alias, Teams and tier, separated by a bar.</returns>
    private static List<string> Describe(SearchResult result) =>
        result.Hits.Cast<AgentHit>()
            .Select(static hit => string.Join('|', hit.Name, hit.PersonaTitle, hit.Alias, string.Join(',', hit.Teams), hit.Tier))
            .ToList();

    /// <summary>Builds a query over the Agents filter with no open Team.</summary>
    /// <param name="text">The phrase.</param>
    /// <param name="scope">The Active or Archived pass.</param>
    /// <returns>The query.</returns>
    private static SearchQuery Query(string text, SearchScope scope = SearchScope.Active) =>
        new(text, SearchFilter.Agents, null, scope, 50);
}
