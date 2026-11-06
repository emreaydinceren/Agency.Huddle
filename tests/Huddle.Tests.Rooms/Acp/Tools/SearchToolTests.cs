using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Search;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="SearchTool"/>: the argument refusals, the Room scope (Members and the caller's Teams by default,
/// every Room on request), a failing provider emptying only its own group, and the output cap.
/// </summary>
public sealed class SearchToolTests
{
    private const string Team = "Platform";

    private static readonly DateTimeOffset T0 = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Quinn is in Platform. Three Rooms hold the phrase: one Quinn is in, one scoped to Platform, one scoped to Design.</summary>
    private sealed class Scene : IDisposable
    {
        /// <summary>The Task tool harness.</summary>
        public required TaskToolHarness Harness { get; init; }

        /// <summary>The caller's Agent id.</summary>
        public required string QuinnId { get; init; }

        /// <summary>The Room the caller is a Member of.</summary>
        public required Room Own { get; init; }

        /// <summary>The Room scoped to the caller's Team that the caller is not in.</summary>
        public required Room TeamScoped { get; init; }

        /// <summary>The Room scoped to another Team that the caller is not in.</summary>
        public required Room Elsewhere { get; init; }

        /// <summary>Builds the tool over the real Rooms provider and any extra providers.</summary>
        /// <param name="extra">Further providers, such as a failing one.</param>
        public SearchTool Tool(params ISearchProvider[] extra)
        {
            return this.Tool(NullLogger<SearchTool>.Instance, extra);
        }

        /// <summary>Builds the tool over the real Rooms provider, any extra providers and a given logger.</summary>
        /// <param name="logger">Receives the tool's log entries.</param>
        /// <param name="extra">Further providers, such as a failing one.</param>
        public SearchTool Tool(ILogger<SearchTool> logger, params ISearchProvider[] extra)
        {
            RoomSearchProvider rooms = new(this.Harness.Directory, this.Harness.Options.CreateChatStore(), NullLogger<RoomSearchProvider>.Instance);
            return new SearchTool([rooms, .. extra], this.Harness.Directory, this.Harness.Personas, logger, new FakePromptSource(), this.QuinnId);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Harness.Dispose();
        }
    }

    /// <summary>A provider that always throws, standing in for a locked folder.</summary>
    private sealed class ThrowingProvider(SearchFilter group, Exception? failure = null) : ISearchProvider
    {
        /// <inheritdoc />
        public SearchFilter Group => group;

        /// <inheritdoc />
        public bool IsExpensive => false;

        /// <inheritdoc />
        public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
        {
            throw failure ?? new InvalidOperationException("locked");
        }
    }

    /// <summary>A provider with one Team whose name is longer than the output cap and made of surrogate pairs.</summary>
    private sealed class EmojiTeamsProvider : ISearchProvider
    {
        /// <inheritdoc />
        public SearchFilter Group => SearchFilter.Teams;

        /// <inheritdoc />
        public bool IsExpensive => false;

        /// <inheritdoc />
        public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
        {
            if (query.Scope == SearchScope.Archived)
            {
                return Task.FromResult(SearchResult.Empty);
            }

            string name = string.Concat(Enumerable.Repeat("\U0001F600", 7000));
            return Task.FromResult(new SearchResult([new TeamHit(name, null, 1, MatchTier.NameContains)], false));
        }
    }

    /// <summary>A logger that keeps every entry's level and rendered message.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>The entries, in order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    /// <summary>A provider that returns many long Team hits, to push the output past the cap.</summary>
    private sealed class ManyTeamsProvider(int count) : ISearchProvider
    {
        /// <inheritdoc />
        public SearchFilter Group => SearchFilter.Teams;

        /// <inheritdoc />
        public bool IsExpensive => false;

        /// <inheritdoc />
        public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
        {
            if (query.Scope == SearchScope.Archived)
            {
                return Task.FromResult(SearchResult.Empty);
            }

            List<SearchHit> hits = [.. Enumerable.Range(0, count).Select(i => (SearchHit)new TeamHit(new string('x', 400) + i, "web", 1, MatchTier.NameContains))];
            return Task.FromResult(new SearchResult(hits, false));
        }
    }

    /// <summary>With no scope given, the caller finds the Room it is in and its Team's Room, and not another Team's Room.</summary>
    [Fact]
    public async Task DefaultScope_FindsOwnAndTeamRooms_NotOthers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("secret plan"), ct);

        Assert.Contains(scene.Own.Id, result, StringComparison.Ordinal);
        Assert.Contains(scene.TeamScoped.Id, result, StringComparison.Ordinal);
        Assert.DoesNotContain(scene.Elsewhere.Id, result, StringComparison.Ordinal);
        Assert.Contains("Rooms searched: yours and your Teams' only.", result, StringComparison.Ordinal);
    }

    /// <summary>Scope "all" also finds the Room scoped to another Team, and the header says every Room was searched.</summary>
    [Fact]
    public async Task ScopeAll_FindsEveryRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("secret plan", scope: "all"), ct);

        Assert.Contains(scene.Elsewhere.Id, result, StringComparison.Ordinal);
        Assert.StartsWith("Rooms searched: every Room.", result, StringComparison.Ordinal);
    }

    /// <summary>A hit gives the Room id, the match count, the newest Message id and a snippet, and no UI link.</summary>
    [Fact]
    public async Task RoomHit_CarriesIdsAndSnippet_AndNoUiLink()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("secret plan", filter: "rooms"), ct);

        Assert.Contains($"{scene.Own.Id} | Own | 1 match | message own-1 | Quinn | 2026-10-28 09:00 UTC", result, StringComparison.Ordinal);
        Assert.DoesNotContain("/rooms/", result, StringComparison.Ordinal);
    }

    /// <summary>A query under three characters is refused with a sentence, not an exception.</summary>
    [Fact]
    public async Task ShortQuery_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("ab"), ct);

        Assert.Equal("The query needs at least 3 characters. Only an empty query for teams or agents browses.", result);
    }

    /// <summary>A missing query and a query starting with '>' are each refused with their own sentence.</summary>
    [Fact]
    public async Task MissingQuery_AndAngleBracket_AreRefusedSeparately()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        SearchTool tool = scene.Tool();

        string missing = await tool.InvokeAsync([], ct);
        string angle = await tool.InvokeAsync(Args(">abc"), ct);

        Assert.Equal("'query' is required.", missing);
        Assert.Equal("A query cannot start with '>'.", angle);
    }

    /// <summary>An Archived Room scoped to another Team is absent under the default scope, its Transcript is never read, and scope "all" finds it.</summary>
    [Fact]
    public async Task ArchivedRoomOutsideScope_IsNeverReadByDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room archived = await scene.Harness.Directory.CreateRoomAsync("Old design", [], "Design", false, ct);
        await scene.Harness.Options.CreateChatStore().AppendAsync(archived.Id, new ChatMessage("old-1", T0, "kai-id", "Kai", "secret plan from long ago"), ct);
        await scene.Harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        SearchTool tool = scene.Tool();

        string byDefault = await tool.InvokeAsync(Args("secret plan", filter: "rooms"), ct);
        string everything = await tool.InvokeAsync(Args("secret plan", filter: "rooms", scope: "all"), ct);

        Assert.DoesNotContain(archived.Id, byDefault, StringComparison.Ordinal);
        Assert.Contains($"Archived Rooms:\n- {archived.Id}", everything, StringComparison.Ordinal);
    }

    /// <summary>A search with scope "all" is logged once as a warning with the caller and the phrase; the default scope logs nothing.</summary>
    [Fact]
    public async Task ScopeAll_IsLogged_AndTheDefaultIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        RecordingLogger<SearchTool> logger = new();
        SearchTool tool = scene.Tool(logger);

        _ = await tool.InvokeAsync(Args("secret plan"), ct);
        Assert.Empty(logger.Entries);

        _ = await tool.InvokeAsync(Args("secret plan", scope: "all"), ct);
        Assert.Equal(new[] { (LogLevel.Warning, "Agent Quinn searched every Room for 'secret plan'.") }, logger.Entries);
    }

    /// <summary>A provider that was cancelled without the caller asking is one failed group, not a failed call.</summary>
    [Fact]
    public async Task ProviderCancelledOnItsOwn_IsOneFailedGroup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool(new ThrowingProvider(SearchFilter.Tasks, new TaskCanceledException())).InvokeAsync(Args("secret plan"), ct);

        Assert.Contains("Couldn't search Tasks.", result, StringComparison.Ordinal);
        Assert.Contains(scene.Own.Id, result, StringComparison.Ordinal);
    }

    /// <summary>A failed group is reported once even though both passes failed, and apart from the group before it.</summary>
    [Fact]
    public async Task FailedGroup_IsReportedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool(new ThrowingProvider(SearchFilter.Library)).InvokeAsync(Args("secret plan"), ct);

        Assert.Equal(1, result.Split("Couldn't search Library.").Length - 1);
        Assert.Contains("\n\nCouldn't search Library.", result, StringComparison.Ordinal);
    }

    /// <summary>A cut in the middle of an emoji does not leave half of it.</summary>
    [Fact]
    public async Task Cap_DoesNotSplitASurrogatePair()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool(new EmojiTeamsProvider()).InvokeAsync(Args("secret plan", filter: "teams"), ct);

        string beforeNote = result[..^"\n... truncated".Length];
        Assert.False(char.IsHighSurrogate(beforeNote[^1]));
    }

    /// <summary>An unknown filter, an unknown scope and an out-of-range limit are each refused with their own sentence.</summary>
    [Fact]
    public async Task BadArguments_AreRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        SearchTool tool = scene.Tool();

        string filter = await tool.InvokeAsync(Args("secret plan", filter: "files"), ct);
        string scope = await tool.InvokeAsync(Args("secret plan", scope: "everything"), ct);
        string limit = await tool.InvokeAsync(Args("secret plan", limit: 51), ct);

        Assert.Equal("'filter' must be one of: all, teams, agents, library, tasks, rooms.", filter);
        Assert.Equal("'scope' must be 'mine' or 'all'.", scope);
        Assert.Equal("'limit' must be between 1 and 50.", limit);
    }

    /// <summary>A phrase that matches nothing says so.</summary>
    [Fact]
    public async Task NothingMatches_SaysSo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("zzzzzz", filter: "rooms"), ct);

        Assert.EndsWith("No results for 'zzzzzz'.", result, StringComparison.Ordinal);
    }

    /// <summary>ZERO-25: a multi-word query that finds nothing says the whole phrase must match and tells the Agent to try one or two words.</summary>
    [Fact]
    public async Task MultiWordNothingMatches_TellsTheAgentToTryFewerWords()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("Science Museum trip permission slip", filter: "rooms"), ct);

        Assert.Equal(
            "No results for 'Science Museum trip permission slip'. Search matches the whole phrase, in order; it does not look for each word on its own. Don't conclude nothing exists: try one or two words.",
            result[result.IndexOf("No results", StringComparison.Ordinal)..]);
    }

    /// <summary>A single-word miss keeps the plain reply: there is no phrase to shorten.</summary>
    [Fact]
    public async Task SingleWordNothingMatches_GivesNoPhraseHint()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("zzzzzz", filter: "rooms"), ct);

        Assert.DoesNotContain("whole phrase", result, StringComparison.Ordinal);
    }

    /// <summary>One provider throwing empties only its own group; the Rooms group still renders.</summary>
    [Fact]
    public async Task FailingProvider_EmptiesOnlyItsGroup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool(new ThrowingProvider(SearchFilter.Library)).InvokeAsync(Args("secret plan"), ct);

        Assert.Contains("Couldn't search Library.", result, StringComparison.Ordinal);
        Assert.Contains(scene.Own.Id, result, StringComparison.Ordinal);
    }

    /// <summary>Output past 12,000 characters is cut and marked.</summary>
    [Fact]
    public async Task LongOutput_IsCapped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool(new ManyTeamsProvider(50)).InvokeAsync(Args("secret plan", filter: "teams", limit: 50), ct);

        Assert.EndsWith("\n... truncated", result, StringComparison.Ordinal);
        Assert.Equal(12000 + "\n... truncated".Length, result.Length);
    }

    private static async Task<Scene> CreateSceneAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Personas.Add(new PersonaIdentity("Quinn", "Dev", "quinn", [Team]), "You are Quinn.");
        User nova = await harness.Directory.UpsertAgentUserAsync("Quinn", null, ct) ?? throw new InvalidOperationException("Directory refused a user.");
        FileChatStore chats = harness.Options.CreateChatStore();
        Room own = await harness.Directory.CreateRoomAsync("Own", [nova.Id], ct);
        Room teamScoped = await harness.Directory.CreateRoomAsync("Platform notes", [], Team, false, ct);
        Room elsewhere = await harness.Directory.CreateRoomAsync("Design notes", [], "Design", false, ct);
        await chats.AppendAsync(own.Id, new ChatMessage("own-1", T0, nova.Id, "Quinn", "the secret plan is ready"), ct);
        await chats.AppendAsync(teamScoped.Id, new ChatMessage("team-1", T0, "kai-id", "Kai", "secret plan for platform"), ct);
        await chats.AppendAsync(elsewhere.Id, new ChatMessage("else-1", T0, "kai-id", "Kai", "secret plan for design"), ct);
        return new Scene { Harness = harness, QuinnId = nova.Id, Own = own, TeamScoped = teamScoped, Elsewhere = elsewhere };
    }

    private static JsonObject Args(string query, string? filter = null, string? scope = null, int? limit = null)
    {
        JsonObject arguments = new() { ["query"] = query };
        if (filter is not null)
        {
            arguments["filter"] = filter;
        }

        if (scope is not null)
        {
            arguments["scope"] = scope;
        }

        if (limit is not null)
        {
            arguments["limit"] = limit;
        }

        return arguments;
    }
}
