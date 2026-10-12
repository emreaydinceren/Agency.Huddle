using System.Threading.Channels;
using Agency.Huddle.App.Search;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Search;

/// <summary>
/// Pins <see cref="SearchOrchestrator"/> (spec §7.3 and §7.6, GS-T8 to GS-T13): group-by-group arrival,
/// the Archived pass after every Active call, cancellation and dropping of late results, the debounce on
/// the injected clock, failure isolation, and one update per provider per pass.
/// </summary>
public sealed class SearchOrchestratorTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly FiringTimeProvider clock = new();
    private readonly Channel<SearchGroupUpdate> updates = Channel.CreateUnbounded<SearchGroupUpdate>();
    private readonly List<SearchOrchestrator> created = [];

    /// <summary>Disposes every orchestrator the test created.</summary>
    public void Dispose()
    {
        foreach (SearchOrchestrator orchestrator in this.created)
        {
            orchestrator.Dispose();
        }
    }

    private static SearchParse Parse(SearchFilter filter, string phrase = "abc") => new(filter, phrase, true);

    private SearchOrchestrator Create(params GatedSearchProvider[] providers)
    {
        SearchOrchestrator orchestrator = new(providers, NullLogger<SearchOrchestrator>.Instance, this.clock);
        orchestrator.GroupUpdated += update => this.updates.Writer.TryWrite(update);
        this.created.Add(orchestrator);
        return orchestrator;
    }

    private async Task<SearchGroupUpdate> NextAsync(CancellationToken ct) =>
        await this.updates.Reader.ReadAsync(ct).AsTask().WaitAsync(Wait, ct);

    private static async Task EndAsync(Task search, CancellationToken ct) => await search.WaitAsync(Wait, ct);

    /// <summary>A cheap group is raised while the expensive one is still behind its debounce, and the caps and the open Team are passed on.</summary>
    [Fact]
    public async Task CheapGroup_ArrivesWhileExpensiveIsHeld()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider rooms = new(SearchFilter.Rooms, isExpensive: true);
        SearchOrchestrator orchestrator = this.Create(teams, rooms);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.All), "alpha");
        IReadOnlyList<GatedCall> teamCalls = await teams.WaitForCallsAsync(1, ct);
        teamCalls[0].Complete(new SearchResult([], Truncated: true));
        SearchGroupUpdate first = await this.NextAsync(ct);

        Assert.Equal(SearchFilter.Teams, first.Group);
        Assert.Equal(SearchScope.Active, first.Scope);
        Assert.Equal(orchestrator.CurrentSearchId, first.SearchId);
        SearchGroupDone done = Assert.IsType<SearchGroupDone>(first.State);
        Assert.Empty(done.Result.Hits);
        Assert.True(done.Result.Truncated);
        Assert.Equal(new SearchQuery("abc", SearchFilter.All, "alpha", SearchScope.Active, SearchLimits.GroupCap), teamCalls[0].Query);
        Assert.Empty(rooms.Calls);
        Assert.False(this.updates.Reader.TryRead(out _));

        this.clock.Advance(SearchLimits.Debounce);
        IReadOnlyList<GatedCall> roomCalls = await rooms.WaitForCallsAsync(1, ct);
        roomCalls[0].Complete();
        SearchGroupUpdate second = await this.NextAsync(ct);

        Assert.Equal(SearchFilter.Rooms, second.Group);
        Assert.Equal(SearchScope.Active, second.Scope);

        // Unblock the Archived pass so the search can end.
        (await teams.WaitForCallsAsync(2, ct))[1].Complete();
        (await rooms.WaitForCallsAsync(2, ct))[1].Complete();
        await EndAsync(search, ct);
    }

    /// <summary>The Archived pass starts only after every Active call has finished, and then asks for the Archived scope.</summary>
    [Fact]
    public async Task ArchivedPass_WaitsForEveryActiveCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider tasks = new(SearchFilter.Tasks, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams, tasks);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.All), null);
        (await teams.WaitForCallsAsync(1, ct))[0].Complete();
        SearchGroupUpdate first = await this.NextAsync(ct);

        Assert.Equal(SearchFilter.Teams, first.Group);
        Assert.Single(teams.Calls);
        Assert.Single(tasks.Calls);
        Assert.Equal(SearchScope.Active, tasks.Calls[0].Query.Scope);

        (await tasks.WaitForCallsAsync(1, ct))[0].Complete();
        SearchGroupUpdate second = await this.NextAsync(ct);
        IReadOnlyList<GatedCall> teamCalls = await teams.WaitForCallsAsync(2, ct);
        IReadOnlyList<GatedCall> taskCalls = await tasks.WaitForCallsAsync(2, ct);

        Assert.Equal(SearchFilter.Tasks, second.Group);
        Assert.Equal(SearchScope.Archived, teamCalls[1].Query.Scope);
        Assert.Equal(SearchScope.Archived, taskCalls[1].Query.Scope);

        teamCalls[1].Complete();
        taskCalls[1].Complete();
        SearchGroupUpdate third = await this.NextAsync(ct);
        SearchGroupUpdate fourth = await this.NextAsync(ct);
        await EndAsync(search, ct);

        Assert.Equal(SearchScope.Archived, third.Scope);
        Assert.Equal(SearchScope.Archived, fourth.Scope);
    }

    /// <summary>A new search cancels the old token, and the old search's late result is never raised.</summary>
    [Fact]
    public async Task NewQuery_CancelsOldToken_AndDropsItsLateResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams);

        Task first = orchestrator.SearchAsync(Parse(SearchFilter.Teams, "abc"), null);
        GatedCall oldCall = (await teams.WaitForCallsAsync(1, ct))[0];
        long oldId = orchestrator.CurrentSearchId;
        Task second = orchestrator.SearchAsync(Parse(SearchFilter.Teams, "abcd"), null);
        GatedCall newCall = (await teams.WaitForCallsAsync(2, ct))[1];

        Assert.True(oldCall.Token.IsCancellationRequested);
        Assert.False(newCall.Token.IsCancellationRequested);
        Assert.NotEqual(oldId, orchestrator.CurrentSearchId);

        oldCall.Complete(new SearchResult([], Truncated: true));
        await EndAsync(first, ct);

        Assert.False(this.updates.Reader.TryRead(out _));

        newCall.Complete();
        SearchGroupUpdate update = await this.NextAsync(ct);
        (await teams.WaitForCallsAsync(3, ct))[2].Complete();
        await EndAsync(second, ct);

        Assert.Equal(orchestrator.CurrentSearchId, update.SearchId);
        Assert.Equal(SearchScope.Active, update.Scope);
    }

    /// <summary>An expensive provider starts only when the debounce has elapsed on the injected clock; a cheap one starts at once.</summary>
    [Fact]
    public async Task Debounce_ExpensiveWaits200Ms_CheapStartsAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider rooms = new(SearchFilter.Rooms, isExpensive: true);
        SearchOrchestrator orchestrator = this.Create(teams, rooms);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.All), null);

        Assert.Single(teams.Calls);
        Assert.Empty(rooms.Calls);

        this.clock.Advance(TimeSpan.FromMilliseconds(199));

        Assert.Empty(rooms.Calls);

        this.clock.Advance(TimeSpan.FromMilliseconds(1));
        IReadOnlyList<GatedCall> roomCalls = await rooms.WaitForCallsAsync(1, ct);

        Assert.Single(roomCalls);

        orchestrator.Cancel();
        await EndAsync(search, ct);
    }

    /// <summary>A keystroke inside the debounce cancels the first wait, so the expensive provider runs once, for the last text.</summary>
    [Fact]
    public async Task KeystrokeInsideDebounce_ExpensiveNeverRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider rooms = new(SearchFilter.Rooms, isExpensive: true);
        SearchOrchestrator orchestrator = this.Create(rooms);

        Task first = orchestrator.SearchAsync(Parse(SearchFilter.Rooms, "abc"), null);
        this.clock.Advance(TimeSpan.FromMilliseconds(100));
        Task second = orchestrator.SearchAsync(Parse(SearchFilter.Rooms, "abcd"), null);
        await EndAsync(first, ct);
        this.clock.Advance(TimeSpan.FromMilliseconds(199));

        Assert.Empty(rooms.Calls);

        this.clock.Advance(TimeSpan.FromMilliseconds(1));
        IReadOnlyList<GatedCall> calls = await rooms.WaitForCallsAsync(1, ct);

        Assert.Single(calls);
        Assert.Equal("abcd", calls[0].Query.Text);

        orchestrator.Cancel();
        await EndAsync(second, ct);
    }

    /// <summary>One provider throwing fails only its own group; the others complete and the search still ends.</summary>
    [Fact]
    public async Task OneProviderThrows_OnlyThatGroupFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider tasks = new(SearchFilter.Tasks, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams, tasks);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.All), null);
        (await teams.WaitForCallsAsync(1, ct))[0].Fault(new IOException("locked"));
        (await tasks.WaitForCallsAsync(1, ct))[0].Complete();
        SearchGroupUpdate[] active = [await this.NextAsync(ct), await this.NextAsync(ct)];
        (await teams.WaitForCallsAsync(2, ct))[1].Complete();
        (await tasks.WaitForCallsAsync(2, ct))[1].Complete();
        await EndAsync(search, ct);

        Assert.IsType<SearchGroupFailed>(active.Single(u => u.Group == SearchFilter.Teams).State);
        Assert.IsType<SearchGroupDone>(active.Single(u => u.Group == SearchFilter.Tasks).State);
    }

    /// <summary>Every provider raises exactly one update in each pass, six in all for three providers.</summary>
    [Fact]
    public async Task ExactlyOneUpdatePerProviderPerPass()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider teammates = new(SearchFilter.Teammates, isExpensive: false);
        GatedSearchProvider rooms = new(SearchFilter.Rooms, isExpensive: true);
        GatedSearchProvider[] providers = [teams, teammates, rooms];
        SearchOrchestrator orchestrator = this.Create(providers);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.All), null);
        this.clock.Advance(SearchLimits.Debounce);
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (GatedSearchProvider provider in providers)
            {
                (await provider.WaitForCallsAsync(pass + 1, ct))[pass].Complete();
            }
        }

        await EndAsync(search, ct);
        List<(SearchFilter Group, SearchScope Scope)> seen = [];
        while (this.updates.Reader.TryRead(out SearchGroupUpdate? update))
        {
            seen.Add((update.Group, update.Scope));
        }

        Assert.Equal(
            [
                (SearchFilter.Teams, SearchScope.Active),
                (SearchFilter.Teammates, SearchScope.Active),
                (SearchFilter.Rooms, SearchScope.Active),
                (SearchFilter.Teams, SearchScope.Archived),
                (SearchFilter.Teammates, SearchScope.Archived),
                (SearchFilter.Rooms, SearchScope.Archived),
            ],
            seen.OrderBy(pair => pair.Scope).ThenBy(pair => pair.Group).ToList());
    }

    /// <summary>Disposing the orchestrator cancels the search in flight and raises nothing.</summary>
    [Fact]
    public async Task Dispose_CancelsTheSearch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.Teams), null);
        GatedCall call = (await teams.WaitForCallsAsync(1, ct))[0];
        orchestrator.Dispose();
        call.Complete();
        await EndAsync(search, ct);

        Assert.True(call.Token.IsCancellationRequested);
        Assert.False(this.updates.Reader.TryRead(out _));
    }

    /// <summary>A single filter runs only its own provider, with the single-filter cap.</summary>
    [Fact]
    public async Task FilterTeams_RunsOnlyTheTeamsProvider()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        GatedSearchProvider teammates = new(SearchFilter.Teammates, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams, teammates);

        Task search = orchestrator.SearchAsync(Parse(SearchFilter.Teams), null);
        IReadOnlyList<GatedCall> calls = await teams.WaitForCallsAsync(1, ct);

        Assert.Equal(SearchLimits.SingleFilterCap, calls[0].Query.MaxHits);
        Assert.Equal(SearchFilter.Teams, calls[0].Query.Filter);
        Assert.Empty(teammates.Calls);

        orchestrator.Cancel();
        await EndAsync(search, ct);
    }

    /// <summary>A parse that is not searchable cancels the search in flight and starts and raises nothing.</summary>
    [Fact]
    public async Task NotSearchable_CancelsAndRaisesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider teams = new(SearchFilter.Teams, isExpensive: false);
        SearchOrchestrator orchestrator = this.Create(teams);

        Task first = orchestrator.SearchAsync(Parse(SearchFilter.Teams), null);
        GatedCall call = (await teams.WaitForCallsAsync(1, ct))[0];
        Task second = orchestrator.SearchAsync(new SearchParse(SearchFilter.All, "ab", false), null);
        await EndAsync(second, ct);
        call.Complete();
        await EndAsync(first, ct);

        Assert.True(call.Token.IsCancellationRequested);
        Assert.Single(teams.Calls);
        Assert.False(this.updates.Reader.TryRead(out _));
    }
}
