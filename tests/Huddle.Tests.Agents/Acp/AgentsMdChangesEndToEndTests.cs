using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Conformance;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// AGENTS.md changes end to end through the real host: the mock-adapter fixture builds the whole Turn pipeline from the container
/// and answers the Helper from a fake process runner, with no child process and no token spent.
/// </summary>
public sealed class AgentsMdChangesEndToEndTests
{
    /// <summary>The fixture resolves the fake in place of the real runner and the AGENTS.md hook from the container, and a real Helper call reaches the fake with a reply valid against the verdict.</summary>
    [Fact]
    public async Task FixtureWiring_ResolvesTheFakeRunner_AndAFullPipeline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartFullPipelineAsync(persona, cancellationToken: bound.Token);

        Assert.Same(fixture.HelperProcess, fixture.Services.GetRequiredService<IHelperProcessRunner>());
        Assert.NotNull(fixture.Services.GetService<AgentsMdTurnHook>());
        Assert.Equal(0, fixture.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Agency.Huddle.App.TeamOptions>>().Value.AgentsMd.NoticeWaitSeconds);

        const string OldText = "Use tabs.";
        const string NewText = "Use spaces.";
        fixture.ScriptVerdict(NewText);
        AgentsMdChangeInput input = new("AGENTS.md", "Teams", "a", "b", OldText, NewText, AgentsMdComparison.Between(OldText, NewText));
        fixture.Services.GetRequiredService<HelperDispatcher>().Enqueue(AgentsMdChangeHelper.HelperId, "wiring-proof", input);

        await Eventually.UntilAsync(() => fixture.HelperProcess.Calls > 0, bound.Token, TestBudgets.Slowest);

        AgentsMdChangeVerdict? verdict = JsonSerializer.Deserialize<AgentsMdChangeVerdict>(fixture.HelperProcess.Text, JsonSerializerOptions.Web);
        Assert.NotNull(verdict);
        Assert.True(verdict.Meaningful);
        Assert.Equal(NewText, Assert.Single(verdict.Changes ?? []).Quote);
    }

    /// <summary>
    /// Spec §13: an edit between two Turns reaches the Room with the Helper's summary; a second Room of the same Agent hears the same
    /// words without a second Helper call; the next Turn in the first Room hears nothing.
    /// </summary>
    [Fact]
    public async Task Edit_RoomGetsTheSummary_SecondRoomSameWords_NoSecondCall_ThirdTurnNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: true, bound.Token);
        scenario.Fixture.ScriptVerdict(Scenario.NewRule);
        await scenario.OpenBothSessionsAsync(bound.Token);

        scenario.Edit();
        string first = await scenario.TurnAsync(scenario.RoomA, "after the edit", bound.Token);
        string second = await scenario.TurnAsync(scenario.RoomB, "after the edit, other room", bound.Token);
        string third = await scenario.TurnAsync(scenario.RoomA, "once more", bound.Token);

        Assert.Equal(scenario.ExpectedNotice, Scenario.NoticeOf(first));
        Assert.Equal(scenario.ExpectedNotice, Scenario.NoticeOf(second));
        Assert.DoesNotContain("AGENTS.md changed", third, StringComparison.Ordinal);
        Assert.DoesNotContain("these lines changed", third, StringComparison.Ordinal);
        Assert.Equal(1, scenario.Fixture.HelperProcess.Calls);
    }

    /// <summary>
    /// An AGENTS.md edit and another file's change in one Turn reach the Agent in one order: the AGENTS.md block, then the File
    /// Changes block (which lists the other file and not the AGENTS.md), then the Room frame.
    /// </summary>
    [Fact]
    public async Task SameTurn_AgentsMdEditAndAnotherFile_BlocksInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: false, bound.Token);
        await scenario.OpenBothSessionsAsync(bound.Token);

        scenario.Edit();
        string note = scenario.AddNote();
        string prompt = await scenario.TurnAsync(scenario.RoomA, "after both changes", bound.Token);

        string fileChanges = "Since your last Turn in this Room, these files changed. Read one only if it matters to what you are doing now:\nadded " + note;
        Assert.Equal(0, prompt.IndexOf(scenario.ExpectedComparison, StringComparison.Ordinal));
        Assert.Equal(scenario.ExpectedComparison.Length, prompt.IndexOf(fileChanges, StringComparison.Ordinal));
        Assert.Equal(scenario.ExpectedComparison.Length + fileChanges.Length + 2, prompt.IndexOf("[Room:", StringComparison.Ordinal));
    }

    /// <summary>With the Helper off, the Turn does not wait and the Agent is told by a plain comparison of the lines; no Helper call is made.</summary>
    [Fact]
    public async Task HelperOff_Comparison()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: false, bound.Token);
        await scenario.OpenBothSessionsAsync(bound.Token);

        scenario.Edit();
        string prompt = await scenario.TurnAsync(scenario.RoomA, "after the edit", bound.Token);

        Assert.Equal(scenario.ExpectedComparison, Scenario.NoticeOf(prompt));
        Assert.Equal(0, scenario.Fixture.HelperProcess.Calls);
    }

    /// <summary>A Helper verdict of "not meaningful" tells the Agent nothing and leaves a skipped record in the Feed.</summary>
    [Fact]
    public async Task Typo_Skipped_InTheFeed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: true, bound.Token);
        scenario.Fixture.HelperProcess.Text = """{"meaningful": false, "reason": "A spelling fix."}""";
        await scenario.OpenBothSessionsAsync(bound.Token);

        scenario.Edit();
        string prompt = await scenario.TurnAsync(scenario.RoomA, "after the edit", bound.Token);

        Assert.DoesNotContain("AGENTS.md changed", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("these lines changed", prompt, StringComparison.Ordinal);
        FeedStore feed = scenario.Fixture.Services.GetRequiredService<FeedStore>();
        await Eventually.UntilAsync(
            () => feed.Page(new FeedQuery(null, 20, null, null)).Any(item => item.SourceType == FeedSourceType.AgentsMdChangeSkipped),
            bound.Token,
            TestBudgets.Slowest);
        FeedItem skipped = feed.Page(new FeedQuery(null, 20, null, null)).First(item => item.SourceType == FeedSourceType.AgentsMdChangeSkipped);
        AgentsMdChangeSkippedPayload payload = Assert.IsType<AgentsMdChangeSkippedPayload>(skipped.Payload);
        Assert.Equal("A spelling fix.", payload.Reason);
        Assert.Equal(1, scenario.Fixture.HelperProcess.Calls);
    }

    /// <summary>
    /// O-12: a Turn waiting on the Helper holds the Agent's one Turn slot, so the Agent's other Room hears nothing until the wait ends;
    /// once the Helper answers, both Rooms are served and the first carries the summary.
    /// </summary>
    [Fact]
    public async Task WaitingTurn_HoldsThatAgentsOtherRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: true, bound.Token);
        TaskCompletionSource hold = await scenario.EditWithHeldHelperAsync(bound.Token);
        try
        {
            int prompts = scenario.PromptCount;
            int repliesA = await scenario.PostAsync(scenario.RoomA, "waits for the summary", bound.Token);
            int repliesB = await scenario.PostAsync(scenario.RoomB, "queued behind it", bound.Token);

            await Task.Delay(TestBudgets.Short, bound.Token);
            Assert.Equal(prompts, scenario.PromptCount);

            hold.SetResult();
            await scenario.ReplyAsync(scenario.RoomA, repliesA, bound.Token);
            await scenario.ReplyAsync(scenario.RoomB, repliesB, bound.Token);
            Assert.Equal(scenario.ExpectedNotice, Scenario.NoticeOf(scenario.PromptAt(prompts)));
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    /// <summary>
    /// O-12 and E-7: Stop in the waiting Room ends the wait at once, so the Agent's one Turn slot is free again. The wait is 120 s and the
    /// Helper never answers; a third Room, which has nothing to wait for, is served within <see cref="TestBudgets.Standard"/> after the
    /// Stop, which is only possible if the waiting Turn left the wait. The waiting Turn told nothing.
    /// </summary>
    [Fact]
    public async Task StopDuringTheWait_FreesTheOtherRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 120, helperEnabled: true, bound.Token);
        TaskCompletionSource hold = await scenario.EditWithHeldHelperAsync(bound.Token);
        try
        {
            string side = await scenario.CreateSideRoomAsync(bound.Token);
            int prompts = scenario.PromptCount;
            int repliesA = await scenario.PostAsync(scenario.RoomA, "stopped while it waits", bound.Token);
            await Eventually.UntilAsync(() => scenario.Fixture.HelperProcess.Calls > 0, bound.Token, TestBudgets.Slowest);
            Assert.Equal(prompts, scenario.PromptCount);
            int repliesSide = await scenario.PostAsync(side, "queued behind the wait", bound.Token);
            await Task.Delay(TestBudgets.Short, bound.Token);
            Assert.Equal(prompts, scenario.PromptCount);

            await scenario.StopAsync(scenario.RoomA, bound.Token);

            await Eventually.UntilAsync(() => scenario.ReplyCount(side) > repliesSide, bound.Token, TestBudgets.Standard);
            Assert.Equal(repliesA, scenario.ReplyCount(scenario.RoomA));
            Assert.Equal(prompts + 1, scenario.PromptCount);
            Assert.Equal(string.Empty, Scenario.NoticeOf(scenario.PromptAt(prompts)));
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    /// <summary>
    /// A Turn dropped by a Stop in the wait (120 s, the Helper held) never told the Agent anything, so the next Turn in that Room still
    /// tells it. A third Room served within <see cref="TestBudgets.Standard"/> of the Stop proves the wait really ended.
    /// </summary>
    [Fact]
    public async Task StopDuringTheWait_NextTurnStillTells()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 120, helperEnabled: true, bound.Token);
        TaskCompletionSource hold = await scenario.EditWithHeldHelperAsync(bound.Token);
        try
        {
            string side = await scenario.CreateSideRoomAsync(bound.Token);
            int prompts = scenario.PromptCount;
            _ = await scenario.PostAsync(scenario.RoomA, "stopped while it waits", bound.Token);
            await Eventually.UntilAsync(() => scenario.Fixture.HelperProcess.Calls > 0, bound.Token, TestBudgets.Slowest);
            Assert.Equal(prompts, scenario.PromptCount);
            int repliesSide = await scenario.PostAsync(side, "queued behind the wait", bound.Token);
            await Task.Delay(TestBudgets.Short, bound.Token);
            await scenario.StopAsync(scenario.RoomA, bound.Token);
            await Eventually.UntilAsync(() => scenario.ReplyCount(side) > repliesSide, bound.Token, TestBudgets.Standard);
            prompts++;
            hold.SetResult();

            string next = await scenario.TurnAsync(scenario.RoomA, "ask again", bound.Token);

            Assert.Equal(scenario.ExpectedNotice, Scenario.NoticeOf(next));
            Assert.Equal(prompts + 1, scenario.PromptCount);
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    /// <summary>A Stop in a Room that is not waiting leaves the waiting Room's wait alone: nothing is sent until the Helper answers.</summary>
    [Fact]
    public async Task StopInOtherRoom_WaitContinues()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: true, bound.Token);
        TaskCompletionSource hold = await scenario.EditWithHeldHelperAsync(bound.Token);
        try
        {
            int prompts = scenario.PromptCount;
            int repliesA = await scenario.PostAsync(scenario.RoomA, "waits for the summary", bound.Token);
            await Task.Delay(TestBudgets.Short, bound.Token);

            await scenario.StopAsync(scenario.RoomB, bound.Token);
            await Task.Delay(TestBudgets.Short, bound.Token);
            Assert.Equal(prompts, scenario.PromptCount);

            hold.SetResult();
            await scenario.ReplyAsync(scenario.RoomA, repliesA, bound.Token);
            Assert.Equal(scenario.ExpectedNotice, Scenario.NoticeOf(scenario.PromptAt(prompts)));
        }
        finally
        {
            _ = hold.TrySetResult();
        }
    }

    /// <summary>A session that starts fresh reads the current files itself, so a change made while the host was down is not told again.</summary>
    [Fact]
    public async Task FreshStart_HearsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: false, bound.Token);
        _ = await scenario.TurnAsync(scenario.RoomA, "hello", bound.Token);

        await scenario.RestartAsync(
            () =>
            {
                scenario.Edit();
                File.Delete(scenario.StorePath);
            },
            bound.Token);
        string prompt = await scenario.TurnAsync(scenario.RoomA, "after a fresh start", bound.Token);

        Assert.Equal(string.Empty, Scenario.NoticeOf(prompt));
    }

    /// <summary>
    /// A session closed by the idle sweep and resumed in the same host did not see the change made while it was closed, so it is told. The
    /// resume is proved by the <c>session/resume</c> the peer received. A real restart cannot resume in this fixture (the fake peer is new
    /// per host and knows no stored id); it is on the manual page.
    /// </summary>
    [Fact]
    public async Task IdleCloseThenResume_StillTells()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: false, bound.Token, new ManualTimeProvider());
        _ = await scenario.TurnAsync(scenario.RoomA, "hello", bound.Token);
        await scenario.StoreWrittenAsync(bound.Token);

        scenario.Edit();
        await scenario.CloseIdleSessionAsync(bound.Token);
        string prompt = await scenario.TurnAsync(scenario.RoomA, "after the close", bound.Token);

        Assert.True(scenario.Resumed());
        Assert.Equal(scenario.ExpectedComparison, Scenario.NoticeOf(prompt));
    }

    /// <summary>
    /// A failed write of the session store does not fail the finished Turn, and the stored entry stays as it was, so a RESUMED session
    /// is told again. The same session in memory has already heard it and does not hear it a second time: that is asserted too.
    /// </summary>
    [Fact]
    public async Task After_StoreWriteFails_TurnSucceeds_NextTurnRetells()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));

        await using Scenario scenario = await Scenario.StartAsync(waitSeconds: 20, helperEnabled: false, bound.Token, new ManualTimeProvider());
        _ = await scenario.TurnAsync(scenario.RoomA, "hello", bound.Token);
        await scenario.StoreWrittenAsync(bound.Token);
        byte[] stored = File.ReadAllBytes(scenario.StorePath);

        // A folder where the store's file was: reading it finds no file, and the replacing move onto it fails on Windows and Linux alike.
        File.Delete(scenario.StorePath);
        Directory.CreateDirectory(scenario.StorePath);
        scenario.Edit();
        string told = await scenario.TurnAsync(scenario.RoomA, "the write fails after this", bound.Token);
        string again = await scenario.TurnAsync(scenario.RoomA, "and the loop still runs", bound.Token);

        Assert.Equal(scenario.ExpectedComparison, Scenario.NoticeOf(told));
        Assert.Equal(string.Empty, Scenario.NoticeOf(again));

        // The sweep closes the session only once its Turn has ended, store attempt included; then the old file comes back and the session resumes from it.
        await scenario.CloseIdleSessionAsync(bound.Token);
        Directory.Delete(scenario.StorePath);
        File.WriteAllBytes(scenario.StorePath, stored);
        string resumed = await scenario.TurnAsync(scenario.RoomA, "after the close", bound.Token);

        Assert.True(scenario.Resumed());
        Assert.Equal(scenario.ExpectedComparison, Scenario.NoticeOf(resumed));
    }

    /// <summary>
    /// One Agent in two Rooms on the real host: the Huddle-wide <c>Teams/AGENTS.md</c> exists before the host starts, so the watcher's
    /// first sweep records it, and each Room's session opens hearing it. A Turn is posted and its prompt read back from the fake peer.
    /// </summary>
    private sealed class Scenario : IAsyncDisposable
    {
        /// <summary>The rule the first version of the file holds.</summary>
        internal const string OldRule = "Use tabs.";

        /// <summary>The rule the edit puts in its place, which the scripted verdict quotes.</summary>
        internal const string NewRule = "Use spaces.";

        /// <summary>The notice a summarised edit renders, in a prompt of an Adapter without file tools.</summary>
        private const string Header =
            "The rules in AGENTS.md changed. What follows applies from now on, in place of what you\nstarted with. Each level still adds to the one before it, and the more specific rule still wins.\n\n";

        private readonly TempDataDir dataDir;
        private readonly Dictionary<string, string?> config;
        private readonly ManualTimeProvider? clock;
        private readonly List<(string RoomId, string SenderId)> posted = [];
        private RoomEvents events;

        private Scenario(TempDataDir dataDir, Dictionary<string, string?> config, ManualTimeProvider? clock, MockAdapterFixture fixture, string agentId, string roomA, string roomB)
        {
            this.dataDir = dataDir;
            this.clock = clock;
            this.config = config;
            this.Fixture = fixture;
            this.AgentId = agentId;
            this.RoomA = roomA;
            this.RoomB = roomB;
            this.events = fixture.Services.GetRequiredService<RoomEvents>();
            this.events.MessagePosted += this.OnMessagePosted;
        }

        /// <summary>The started mock-adapter fixture, on the full Turn pipeline.</summary>
        internal MockAdapterFixture Fixture { get; private set; }

        /// <summary>The file the Room Session store keeps for the Agent.</summary>
        internal string StorePath => Path.Combine(this.dataDir.Path, "room-sessions", "nova.json");

        /// <summary>How many prompts the Agent has been sent since the host last started.</summary>
        internal int PromptCount => this.PromptTexts().Count;

        /// <summary>The prompt at a position among those the Agent has been sent since the host last started.</summary>
        /// <param name="index">The position.</param>
        internal string PromptAt(int index) => this.PromptTexts()[index];

        /// <summary>Whether the Agent has been asked to resume a session since the host last started.</summary>
        internal bool Resumed() => this.Fixture.Agent.Received.Any(message => string.Equals((string?)message["method"], "session/resume", StringComparison.Ordinal));

        /// <summary>Scripts the verdict, holds the Helper, opens both sessions and edits the file. The next Turn finds the change and calls the held Helper.</summary>
        /// <param name="cancellationToken">Bounds the waits.</param>
        internal async Task<TaskCompletionSource> EditWithHeldHelperAsync(CancellationToken cancellationToken)
        {
            this.Fixture.ScriptVerdict(NewRule);
            TaskCompletionSource hold = this.HoldHelper();
            await this.OpenBothSessionsAsync(cancellationToken);
            this.Edit();
            return hold;
        }

        /// <summary>
        /// Moves the runner's clock past the idle threshold and sweeps, again and again, until the peer has received <c>session/close</c>.
        /// A session closes only when idle, which is after its Turn has stored its entry.
        /// </summary>
        /// <param name="cancellationToken">Bounds the sweep.</param>
        internal async Task CloseIdleSessionAsync(CancellationToken cancellationToken)
        {
            ManualTimeProvider time = this.clock ?? throw new InvalidOperationException("The scenario has no manual clock.");
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TestBudgets.Slow);
            while (!this.Fixture.Agent.Received.Any(static message => string.Equals((string?)message["method"], "session/close", StringComparison.Ordinal)))
            {
                time.Advance(TimeSpan.FromMinutes(2));
                await this.Fixture.Runner.SweepIdleSessionsAsync();
                timeout.Token.ThrowIfCancellationRequested();
                await Task.Delay(20, timeout.Token);
            }
        }

        /// <summary>Waits until the Room Session store has written the Agent's file, which follows the reply of a Turn.</summary>
        /// <param name="cancellationToken">Bounds the wait.</param>
        internal Task StoreWrittenAsync(CancellationToken cancellationToken)
            => Eventually.UntilAsync(() => File.Exists(this.StorePath), cancellationToken, TestBudgets.Slowest);

        /// <summary>Makes the fake Helper wait for the returned source before it answers.</summary>
        internal TaskCompletionSource HoldHelper()
        {
            TaskCompletionSource hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.Fixture.HelperProcess.Hold = hold.Task;
            return hold;
        }

        /// <summary>Creates a third Room of the Human and the Agent, opened after the edit, so its first Turn has nothing to wait for.</summary>
        /// <param name="cancellationToken">Bounds the call.</param>
        internal async Task<string> CreateSideRoomAsync(CancellationToken cancellationToken)
        {
            ITeamDirectory directory = this.Fixture.Services.GetRequiredService<ITeamDirectory>();
            Room side = await directory.CreateRoomAsync("side", [KnownIds.Human, this.AgentId], cancellationToken);
            return side.Id;
        }

        /// <summary>Posts a Human message and returns at once with the number of replies the Room had, for <see cref="ReplyAsync"/>.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="text">The message.</param>
        /// <param name="cancellationToken">Bounds the post.</param>
        internal async Task<int> PostAsync(string roomId, string text, CancellationToken cancellationToken)
        {
            int before = this.Replies(roomId);
            ChatService chat = this.Fixture.Services.GetRequiredService<ChatService>();
            await chat.PostAsync(roomId, KnownIds.Human, text, ct: cancellationToken);
            return before;
        }

        /// <summary>Waits until the Agent has replied in a Room more often than <paramref name="before"/>.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="before">The count <see cref="PostAsync"/> returned.</param>
        /// <param name="cancellationToken">Bounds the wait.</param>
        internal Task ReplyAsync(string roomId, int before, CancellationToken cancellationToken)
            => Eventually.UntilAsync(() => this.Replies(roomId) > before, cancellationToken, TestBudgets.Slowest);

        /// <summary>How many replies the Agent has posted in a Room.</summary>
        /// <param name="roomId">The Room.</param>
        internal int ReplyCount(string roomId) => this.Replies(roomId);

        /// <summary>Presses Stop in a Room, as the Chat page does.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="cancellationToken">Bounds the call.</param>
        internal Task StopAsync(string roomId, CancellationToken cancellationToken)
            => this.Fixture.Services.GetRequiredService<IAgentGateway>().StopTurnAsync(this.AgentId, roomId, cancellationToken);

        /// <summary>Stops the host, runs <paramref name="whileDown"/>, and starts a new host on the same data directory.</summary>
        /// <param name="whileDown">What happens to the files while no host runs.</param>
        /// <param name="cancellationToken">Bounds the start.</param>
        internal async Task RestartAsync(Action whileDown, CancellationToken cancellationToken)
        {
            this.events.MessagePosted -= this.OnMessagePosted;
            await this.Fixture.DisposeAsync();
            whileDown();
            this.Fixture = await MockAdapterFixture.StartFullPipelineAsync(new Persona("nova", "You are Nova."), this.dataDir.Path, this.config, cancellationToken: cancellationToken);
            this.events = this.Fixture.Services.GetRequiredService<RoomEvents>();
            this.events.MessagePosted += this.OnMessagePosted;
        }

        /// <summary>The Agent's user id.</summary>
        internal string AgentId { get; }

        /// <summary>The Human's direct Room with the Agent.</summary>
        internal string RoomA { get; }

        /// <summary>A second Room of the same Agent: its one-to-one in the Team Home.</summary>
        internal string RoomB { get; }

        /// <summary>The block a summarised edit renders, for an Adapter that reads files.</summary>
        internal string ExpectedNotice =>
            Header + "In Teams/AGENTS.md:\n- Changed: A rule changed.\n  The file says: \"Use spaces.\"\nThis is a summary. The file's text wins: " + this.AgentsMdPath + "\n\n";

        /// <summary>The block an edit with no summary renders: the changed lines.</summary>
        internal string ExpectedComparison =>
            Header + "In Teams/AGENTS.md, these lines changed. Read the file before you rely on this: " + this.AgentsMdPath + "\n~~~diff\n-Use tabs.\n+Use spaces.\n~~~\n\n";

        private string AgentsMdPath =>Path.Combine(this.dataDir.Path, "Teams", "AGENTS.md");

        /// <summary>Writes the file, starts the host and the Agent, and finds both Rooms.</summary>
        /// <param name="waitSeconds">The Turn's wait for a summary (<c>NoticeWaitSeconds</c>).</param>
        /// <param name="helperEnabled">Whether the AGENTS.md Helper is on.</param>
        /// <param name="cancellationToken">Cancels startup.</param>
        /// <param name="clock">The runner's clock, which drives the idle sweep; <see langword="null"/> for the real one.</param>
        internal static async Task<Scenario> StartAsync(int waitSeconds, bool helperEnabled, CancellationToken cancellationToken, ManualTimeProvider? clock = null)
        {
            TempDataDir dir = new();
            string home = Path.Combine(dir.Path, "Teams", "Home");
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home, "_team.md"), "---\nteamType: 'development@1.0.0'\nmanager: 'nova'\n---\n");
            File.WriteAllText(Path.Combine(dir.Path, "Teams", "AGENTS.md"), "# Rules\n" + OldRule + "\n");
            Dictionary<string, string?> config = new(StringComparer.Ordinal)
            {
                ["Team:AgentsMd:NoticeWaitSeconds"] = waitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Team:Helpers:AgentsMdChange:Enabled"] = helperEnabled ? "true" : "false",
            };

            config["Team:Acp:SessionIdleMinutes"] = "1";
            MockAdapterFixture fixture = await MockAdapterFixture.StartFullPipelineAsync(new Persona("nova", "You are Nova."), dir.Path, config, clock, cancellationToken: cancellationToken);
            try
            {
                ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
                User agent = await directory.FindUserByNameAsync("nova", cancellationToken) ?? throw new InvalidOperationException("No nova.");
                Room direct = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, cancellationToken) ?? throw new InvalidOperationException("No direct Room.");
                ChatService chat = fixture.Services.GetRequiredService<ChatService>();
                Room team = await chat.EnsureTeamOneToOneAsync("Home", agent, cancellationToken);
                AgentsMdLedger ledger = fixture.Services.GetRequiredService<AgentsMdLedger>();
                await Eventually.UntilAsync(() => ledger.FirstSightHash("AGENTS.md") is not null, cancellationToken, TestBudgets.Slowest);
                return new Scenario(dir, config, clock, fixture, agent.Id, direct.Id, team.Id);
            }
            catch
            {
                await fixture.DisposeAsync();
                dir.Dispose();
                throw;
            }
        }

        /// <summary>Runs one Turn in each Room, so both sessions are open and have heard the first version.</summary>
        /// <param name="cancellationToken">Bounds the waits.</param>
        internal async Task OpenBothSessionsAsync(CancellationToken cancellationToken)
        {
            _ = await this.TurnAsync(this.RoomA, "hello", cancellationToken);
            _ = await this.TurnAsync(this.RoomB, "hello", cancellationToken);
        }

        /// <summary>Replaces the file with its second version.</summary>
        internal void Edit() => File.WriteAllText(this.AgentsMdPath, "# Rules\n" + NewRule + "\n");

        /// <summary>Adds a note to the Agent's own Work Dir, which File Changes always watches, and returns its path.</summary>
        internal string AddNote()
        {
            string workDir = this.Fixture.Services.GetRequiredService<AgentPaths>().WorkDir("nova");
            Directory.CreateDirectory(workDir);
            string path = Path.Combine(workDir, "notes.md");
            File.WriteAllText(path, "A note.\n");
            return path;
        }

        /// <summary>Posts a Human message to a Room, waits for the Agent's reply, and returns the prompt the Agent was sent for it.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="text">The message.</param>
        /// <param name="cancellationToken">Bounds the wait.</param>
        internal async Task<string> TurnAsync(string roomId, string text, CancellationToken cancellationToken)
        {
            int before = this.Replies(roomId);
            int prompts = this.PromptTexts().Count;
            ChatService chat = this.Fixture.Services.GetRequiredService<ChatService>();
            await chat.PostAsync(roomId, KnownIds.Human, text, ct: cancellationToken);
            await Eventually.UntilAsync(() => this.Replies(roomId) > before, cancellationToken, TestBudgets.Slowest);
            return this.PromptTexts()[prompts];
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.events.MessagePosted -= this.OnMessagePosted;
            await this.Fixture.DisposeAsync();
            this.dataDir.Dispose();
        }

        /// <summary>Everything in a prompt before the Room's own line: the blocks the hooks added, or nothing.</summary>
        /// <param name="prompt">The prompt text.</param>
        internal static string NoticeOf(string prompt) => prompt[..prompt.IndexOf("[Room:", StringComparison.Ordinal)];

        private static string PromptText(JsonObject message)
        {
            JsonArray? blocks = (message["params"] as JsonObject)?["prompt"] as JsonArray;
            return blocks is null ? string.Empty : string.Concat(blocks.Select(block => (string?)block?["text"]));
        }

        private List<string> PromptTexts() => [.. this.Fixture.Agent.Received
            .Where(message => string.Equals((string?)message["method"], "session/prompt", StringComparison.Ordinal))
            .Select(PromptText)];

        private int Replies(string roomId)
        {
            lock (this.posted)
            {
                return this.posted.Count(item => string.Equals(item.RoomId, roomId, StringComparison.Ordinal) && string.Equals(item.SenderId, this.AgentId, StringComparison.Ordinal));
            }
        }

        private void OnMessagePosted(MessagePostedEvent messagePosted)
        {
            lock (this.posted)
            {
                this.posted.Add((messagePosted.Room.Id,messagePosted.Message.SenderId));
            }
        }
    }
}
