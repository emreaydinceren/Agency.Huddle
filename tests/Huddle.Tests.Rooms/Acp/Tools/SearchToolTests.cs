using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Agents;
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

        /// <summary>The built-in Chief of Staff's Agent id; it is in no Team and no Room.</summary>
        public required string ChiefId { get; init; }

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
            return this.ToolFor(this.QuinnId, logger, extra);
        }

        /// <summary>Builds the tool for a given caller, over the real Rooms provider, any extra providers and a given logger.</summary>
        /// <param name="agentId">The caller's Agent id.</param>
        /// <param name="logger">Receives the tool's log entries.</param>
        /// <param name="extra">Further providers, such as a failing one.</param>
        public SearchTool ToolFor(string agentId, ILogger<SearchTool> logger, params ISearchProvider[] extra)
        {
            FileChatStore chats = this.Harness.Options.CreateChatStore();
            RoomSearchProvider rooms = new(this.Harness.Directory, chats, NullLogger<RoomSearchProvider>.Instance);
            return new SearchTool([rooms, .. extra], this.Harness.Directory, chats, this.Harness.Personas, logger, new FakePromptSource(), agentId);
        }

        /// <summary>Adds a Room Quinn is a Member of, holding the given Messages.</summary>
        /// <param name="name">The Room's name.</param>
        /// <param name="ct">Cancels the setup.</param>
        /// <param name="messages">The Messages to append, in order.</param>
        public async Task<Room> AddRoomAsync(string name, CancellationToken ct, params ChatMessage[] messages)
        {
            Room room = await this.Harness.Directory.CreateRoomAsync(name, [this.QuinnId], ct);
            FileChatStore chats = this.Harness.Options.CreateChatStore();
            foreach (ChatMessage message in messages)
            {
                await chats.AppendAsync(room.Id, message, ct);
            }

            return room;
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

    /// <summary>A hit gives the Room id and name, the Message id, sender and time, and a snippet, and no UI link.</summary>
    [Fact]
    public async Task RoomHit_CarriesIdsAndSnippet_AndNoUiLink()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Args("secret plan", filter: "rooms"), ct);

        Assert.Contains($"{scene.Own.Id} | Own\n  message own-1 | Quinn | 2026-10-28 09:00 UTC\n    the secret plan is ready", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
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
        Room archived = await scene.Harness.Directory.CreateRoomAsync("Old design", [], "Design", false, ct: ct);
        await scene.Harness.Options.CreateChatStore().AppendAsync(archived.Id, new ChatMessage("old-1", T0, "kai-id", "Kai", "secret plan from long ago"), ct);
        await scene.Harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        SearchTool tool = scene.Tool();

        string byDefault = await tool.InvokeAsync(Args("secret plan", filter: "rooms"), ct);
        string everything = await tool.InvokeAsync(Args("secret plan", filter: "rooms", scope: "all"), ct);

        Assert.DoesNotContain(archived.Id, byDefault, StringComparison.Ordinal);
        Assert.Contains($"{archived.Id} | Old design (archived)\n  message old-1", everything, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
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

    /// <summary>The Chief of Staff, with no scope, finds a Room of a Team it is not in; an ordinary Agent with no scope does not.</summary>
    [Fact]
    public async Task ChiefOfStaff_NoScope_FindsEveryRoom_AndAnotherAgentDoesNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string chief = await scene.ToolFor(scene.ChiefId, NullLogger<SearchTool>.Instance).InvokeAsync(Args("secret plan"), ct);
        string ordinary = await scene.Tool().InvokeAsync(Args("secret plan"), ct);

        Assert.Contains(scene.Elsewhere.Id, chief, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains(scene.Own.Id, chief, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.StartsWith("Rooms searched: every Room.", chief, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain(scene.Elsewhere.Id, ordinary, StringComparison.Ordinal);
    }

    /// <summary>The Chief of Staff may narrow itself to its own Rooms with scope "mine".</summary>
    [Fact]
    public async Task ChiefOfStaff_ScopeMine_IsHonoured()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.ToolFor(scene.ChiefId, NullLogger<SearchTool>.Instance).InvokeAsync(Args("secret plan", scope: "mine"), ct);

        Assert.DoesNotContain(scene.Elsewhere.Id, result, StringComparison.Ordinal);
        Assert.Contains("Rooms searched: yours and your Teams' only.", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
    }

    /// <summary>The Chief of Staff's every-Room search is logged once at Information, and scope "mine" logs nothing.</summary>
    [Fact]
    public async Task ChiefOfStaff_EveryRoomSearch_IsLoggedAtInformation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        RecordingLogger<SearchTool> logger = new();
        SearchTool tool = scene.ToolFor(scene.ChiefId, logger);

        _ = await tool.InvokeAsync(Args("secret plan"), ct);
        Assert.Equal(new[] { (LogLevel.Information, "Chief of Staff Jarvis searched every Room for 'secret plan'.") }, logger.Entries);

        logger.Entries.Clear();
        _ = await tool.InvokeAsync(Args("secret plan", scope: "mine"), ct);
        Assert.Empty(logger.Entries);
    }

    /// <summary>Two matching Messages in one Room both come back, each with its own id, not only the newest.</summary>
    [Fact]
    public async Task TwoMatchesInOneRoom_BothComeBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync("Grep", ct, Msg("g-1", 1, "Kai", "we hold the release"), Msg("g-2", 2, "Kai", "nothing here"), Msg("g-3", 3, "Kai", "hold again please"));

        string result = await scene.Tool().InvokeAsync(Args("hold", filter: "rooms"), ct);

        Assert.Contains("message g-1 |", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message g-3 |", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message g-2 |", result, StringComparison.Ordinal);
    }

    /// <summary>Context 1 shows the Message before and after each match, and overlapping windows are merged so a shared neighbour is shown once.</summary>
    [Fact]
    public async Task Context_ShowsNeighbours_AndMergesOverlappingWindows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync(
            "Grep",
            ct,
            Msg("c-1", 1, "Kai", "alpha one"),
            Msg("c-2", 2, "Kai", "needle first"),
            Msg("c-3", 3, "Kai", "bravo shared"),
            Msg("c-4", 4, "Kai", "needle second"),
            Msg("c-5", 5, "Kai", "charlie after"),
            Msg("c-6", 6, "Kai", "delta far away"));

        string result = await scene.Tool().InvokeAsync(Json("needle", ("context", 1)), ct);

        Assert.Contains("  2026-10-28 09:01 Kai: alpha one", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("> 2026-10-28 09:02 Kai: needle first", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("> 2026-10-28 09:04 Kai: needle second", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("charlie after", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Equal(1, result.Split("bravo shared").Length - 1);
        Assert.DoesNotContain("delta far away", result, StringComparison.Ordinal);
    }

    /// <summary>A regular expression that backtracks without end is stopped by its timeout and refused, not left to hang.</summary>
    [Fact]
    public async Task CatastrophicRegex_ReturnsTheTimeoutRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync("Grep", ct, Msg("r-1", 1, "Kai", new string('a', 40000) + "!"));

        string result = await scene.Tool().InvokeAsync(Json("(a+)+$", ("regex", true)), ct);

        Assert.Equal("The regular expression took too long to run; simplify it, for example by removing nested repeats.", result);
    }

    /// <summary>A regex query with alternatives finds both Messages, and an invalid pattern is refused with the parser's reason.</summary>
    [Fact]
    public async Task Regex_FindsAlternatives_AndRefusesAnInvalidPattern()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync("Grep", ct, Msg("x-1", 1, "Kai", "please HOLD it"), Msg("x-2", 2, "Kai", "wait for me"), Msg("x-3", 3, "Kai", "go ahead"));
        SearchTool tool = scene.Tool();

        string found = await tool.InvokeAsync(Json("hold|wait for", ("regex", true)), ct);
        string invalid = await tool.InvokeAsync(Json("(unclosed", ("regex", true)), ct);

        Assert.Contains("message x-1 |", found, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message x-2 |", found, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message x-3 |", found, StringComparison.Ordinal);
        Assert.StartsWith("'query' is not a valid regular expression: ", invalid, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
    }

    /// <summary>Count gives one line per Room with its number of matches and a total, and no snippets.</summary>
    [Fact]
    public async Task Count_GivesMatchesPerRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room room = await scene.AddRoomAsync("Grep", ct, Msg("n-1", 1, "Kai", "needle a"), Msg("n-2", 2, "Kai", "other"), Msg("n-3", 3, "Kai", "needle b"));

        string result = await scene.Tool().InvokeAsync(Json("needle", ("count", true)), ct);

        Assert.Contains($"{room.Id} | Grep | 2 matches", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("Total: 2 matches in 1 Rooms.", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message n-1", result, StringComparison.Ordinal);
    }

    /// <summary>A Room with no query returns its newest Messages oldest first, and says how many earlier ones there are and how to read them.</summary>
    [Fact]
    public async Task RoomWithNoQuery_ReturnsTheTail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room room = await scene.AddRoomAsync("Grep", ct, Msg("t-1", 0, "Kai", "one"), Msg("t-2", 1, "Kai", "two"), Msg("t-3", 2, "Kai", "three"), Msg("t-4", 3, "Kai", "four"), Msg("t-5", 4, "Kai", "five"));

        string result = await scene.Tool().InvokeAsync(Json(null, ("room", room.Id), ("limit", 2)), ct);

        Assert.Contains("3 earlier Messages; pass until 2026-10-28T09:03:00Z to read further back.", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.True(result.IndexOf("Kai: four", StringComparison.Ordinal) < result.IndexOf("Kai: five", StringComparison.Ordinal));
        Assert.DoesNotContain("Kai: three", result, StringComparison.Ordinal);
    }

    /// <summary>The sender filter keeps only that sender's Messages.</summary>
    [Fact]
    public async Task Sender_FiltersMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync("Grep", ct, Msg("s-1", 1, "Kai", "needle from kai"), Msg("s-2", 2, "Nova", "needle from nova"));

        string result = await scene.Tool().InvokeAsync(Json("needle", ("sender", "nova")), ct);

        Assert.Contains("message s-2 |", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message s-1 |", result, StringComparison.Ordinal);
    }

    /// <summary>Since includes its day and until, given as a date, runs through the end of that day.</summary>
    [Fact]
    public async Task SinceAndUntil_BoundTheMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync(
            "Grep",
            ct,
            new ChatMessage("d-1", new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero), "kai-id", "Kai", "needle monday"),
            new ChatMessage("d-2", new DateTimeOffset(2026, 10, 28, 23, 30, 0, TimeSpan.Zero), "kai-id", "Kai", "needle tuesday"),
            new ChatMessage("d-3", new DateTimeOffset(2026, 10, 29, 0, 30, 0, TimeSpan.Zero), "kai-id", "Kai", "needle wednesday"));
        SearchTool tool = scene.Tool();

        string since = await tool.InvokeAsync(Json("needle", ("since", "2026-10-28")), ct);
        string until = await tool.InvokeAsync(Json("needle", ("until", "2026-10-28")), ct);

        Assert.DoesNotContain("message d-1 |", since, StringComparison.Ordinal);
        Assert.Contains("message d-2 |", since, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message d-3 |", since, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message d-1 |", until, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message d-2 |", until, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message d-3 |", until, StringComparison.Ordinal);
    }

    /// <summary>The team filter keeps Rooms scoped to that Team and drops root Rooms and other Teams' Rooms.</summary>
    [Fact]
    public async Task Team_KeepsOnlyThatTeamsRooms()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Json("secret plan", ("team", "platform"), ("scope", "all")), ct);

        Assert.Contains(scene.TeamScoped.Id, result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain(scene.Own.Id, result, StringComparison.Ordinal);
        Assert.DoesNotContain(scene.Elsewhere.Id, result, StringComparison.Ordinal);
    }

    /// <summary>A Room outside the caller's scope is refused for an ordinary Agent, an unknown Room is refused, and the Chief of Staff reads it.</summary>
    [Fact]
    public async Task Room_OutsideScopeOrUnknown_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string outside = await scene.Tool().InvokeAsync(Json("secret plan", ("room", scene.Elsewhere.Id)), ct);
        string unknown = await scene.Tool().InvokeAsync(Json("secret plan", ("room", "nope")), ct);
        string chief = await scene.ToolFor(scene.ChiefId, NullLogger<SearchTool>.Instance).InvokeAsync(Json("secret plan", ("room", scene.Elsewhere.Id)), ct);

        Assert.Equal($"Room '{scene.Elsewhere.Id}' is outside the Rooms you searched; pass scope \"all\".", outside);
        Assert.Equal("There is no Room 'nope'.", unknown);
        Assert.Contains("message else-1 |", chief, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
    }

    /// <summary>When more Messages match than the limit, the newest are kept and the rest are counted honestly.</summary>
    [Fact]
    public async Task LimitOverflow_KeepsTheNewest_AndSaysHowManyWereLeftOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = await scene.AddRoomAsync("Grep", ct, Msg("o-1", 1, "Kai", "needle 1"), Msg("o-2", 2, "Kai", "needle 2"), Msg("o-3", 3, "Kai", "needle 3"), Msg("o-4", 4, "Kai", "needle 4"));

        string result = await scene.Tool().InvokeAsync(Json("needle", ("limit", 2)), ct);

        Assert.Contains("message o-3 |", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.Contains("message o-4 |", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain("message o-1 |", result, StringComparison.Ordinal);
        Assert.Contains("2 more matching Messages not shown. Narrow with room, sender, since or until, or raise limit (at most 50).", result, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
    }

    /// <summary>A Room-only argument together with another filter, a bad time, or count with context is refused with its own sentence.</summary>
    [Fact]
    public async Task RoomOnlyArguments_AreValidated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        SearchTool tool = scene.Tool();

        string filter = await tool.InvokeAsync(Json("secret plan", ("sender", "Kai"), ("filter", "tasks")), ct);
        string since = await tool.InvokeAsync(Json("secret plan", ("since", "yesterday-ish")), ct);
        string both = await tool.InvokeAsync(Json("secret plan", ("count", true), ("context", 1)), ct);
        string range = await tool.InvokeAsync(Json("secret plan", ("context", 11)), ct);
        string missing = await tool.InvokeAsync(Json(null, ("sender", "Kai")), ct);

        Assert.Equal("regex, context, count, room, team, sender, since and until apply to Rooms only; use filter \"rooms\".", filter);
        Assert.Equal("'since' must be a date like 2026-10-01 or an ISO 8601 time.", since);
        Assert.Equal("'count' and 'context' cannot be combined.", both);
        Assert.Equal("'context' must be between 0 and 10.", range);
        Assert.Equal("'query' is required.", missing);
    }

    /// <summary>A regular expression query that is only blank is refused with its own sentence.</summary>
    [Fact]
    public async Task BlankRegexQuery_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool().InvokeAsync(Json("   ", ("regex", true)), ct);

        Assert.Equal("The query needs at least 1 non-blank character.", result);
    }

    /// <summary>A Room name shared by two Rooms is refused, listing both ids.</summary>
    [Fact]
    public async Task RoomNameSharedByTwoRooms_IsRefusedWithBothIds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room first = await scene.AddRoomAsync("Twin", ct);
        Room second = await scene.AddRoomAsync("twin", ct);

        string result = await scene.Tool().InvokeAsync(Json(null, ("room", "TWIN")), ct);

        string[] either = [$"More than one Room is named 'TWIN': {first.Id}, {second.Id}. Pass one of these ids as room.", $"More than one Room is named 'TWIN': {second.Id}, {first.Id}. Pass one of these ids as room."];
        Assert.Contains(result, either);
    }

    /// <summary>A Room tail whose sender filter matches nothing says so under the Room's heading.</summary>
    [Fact]
    public async Task RoomTail_WithNoMatchingMessage_SaysSo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room room = await scene.AddRoomAsync("Grep", ct, Msg("m-1", 1, "Kai", "hello"));

        string result = await scene.Tool().InvokeAsync(Json(null, ("room", room.Id), ("sender", "Nobody")), ct);

        Assert.Equal($"Rooms searched: yours and your Teams' only. Pass scope \"all\" to search every Room.\n\nRooms:\n{room.Id} | Grep\n  No Messages match.", result);
    }

    /// <summary>A Room whose name matches but whose Messages do not still gets one line.</summary>
    [Fact]
    public async Task RoomNameMatch_WithNoMessageMatch_GetsOneLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room room = await scene.AddRoomAsync("Zebra crossing", ct, Msg("z-1", 1, "Kai", "unrelated"));

        string result = await scene.Tool().InvokeAsync(Json("zebra"), ct);

        Assert.Equal($"Rooms searched: yours and your Teams' only. Pass scope \"all\" to search every Room.\n\nRooms:\n{room.Id} | Zebra crossing | name matches", result);
    }

    /// <summary>An Archived Room's tail shows its heading marked archived and one Message as a time, sender and text line.</summary>
    [Fact]
    public async Task ArchivedRoomTail_ShowsTheHeadingAndTheMessageLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        Room room = await scene.AddRoomAsync("Old", ct, Msg("a-1", 1, "Kai", "hello\nthere"));
        await scene.Harness.Directory.SetRoomArchivedAsync(room.Id, true, ct);

        string result = await scene.Tool().InvokeAsync(Json(null, ("room", room.Id), ("scope", "all")), ct);

        Assert.Equal($"Rooms searched: every Room.\n\nRooms:\n{room.Id} | Old (archived)\n  2026-10-28 09:01 Kai: hello there", result);
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
        Room teamScoped = await harness.Directory.CreateRoomAsync("Platform notes", [], Team, false, ct: ct);
        Room elsewhere = await harness.Directory.CreateRoomAsync("Design notes", [], "Design", false, ct: ct);
        await chats.AppendAsync(own.Id, new ChatMessage("own-1", T0, nova.Id, "Quinn", "the secret plan is ready"), ct);
        await chats.AppendAsync(teamScoped.Id, new ChatMessage("team-1", T0, "kai-id", "Kai", "secret plan for platform"), ct);
        await chats.AppendAsync(elsewhere.Id, new ChatMessage("else-1", T0, "kai-id", "Kai", "secret plan for design"), ct);
        harness.Personas.Add(new PersonaIdentity("Jarvis", "Chief of Staff", "jar", [], Builtin: BuiltinAgent.ChiefOfStaffMarker), "You are Jarvis.");
        User jarvis = await harness.Directory.UpsertAgentUserAsync("Jarvis", null, ct) ?? throw new InvalidOperationException("Directory refused a user.");
        return new Scene { Harness = harness, QuinnId = nova.Id, Own = own, TeamScoped = teamScoped, Elsewhere = elsewhere, ChiefId = jarvis.Id };
    }

    private static ChatMessage Msg(string id, int minute, string sender, string text)
    {
        return new ChatMessage(id, T0.AddMinutes(minute), sender.ToLowerInvariant() + "-id", sender, text);
    }

    private static JsonObject Json(string? query, params (string Key, JsonNode Value)[] more)
    {
        JsonObject arguments = [];
        if (query is not null)
        {
            arguments["query"] = query;
        }

        foreach ((string key, JsonNode value) in more)
        {
            arguments[key] = value;
        }

        return arguments;
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
 // contains-ok: one line or fragment of plain-text tool output