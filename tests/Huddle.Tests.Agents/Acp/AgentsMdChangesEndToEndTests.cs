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
using Agency.Huddle.App.Services;
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
        private readonly RoomEvents events;
        private readonly List<(string RoomId, string SenderId)> posted = [];

        private Scenario(TempDataDir dataDir, MockAdapterFixture fixture, string agentId, string roomA, string roomB)
        {
            this.dataDir = dataDir;
            this.Fixture = fixture;
            this.AgentId = agentId;
            this.RoomA = roomA;
            this.RoomB = roomB;
            this.events = fixture.Services.GetRequiredService<RoomEvents>();
            this.events.MessagePosted += this.OnMessagePosted;
        }

        /// <summary>The started mock-adapter fixture, on the full Turn pipeline.</summary>
        internal MockAdapterFixture Fixture { get; }

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
        internal static async Task<Scenario> StartAsync(int waitSeconds, bool helperEnabled, CancellationToken cancellationToken)
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

            MockAdapterFixture fixture = await MockAdapterFixture.StartFullPipelineAsync(new Persona("nova", "You are Nova."), dir.Path, config, cancellationToken: cancellationToken);
            try
            {
                ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
                User agent = await directory.FindUserByNameAsync("nova", cancellationToken) ?? throw new InvalidOperationException("No nova.");
                Room direct = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, cancellationToken) ?? throw new InvalidOperationException("No direct Room.");
                ChatService chat = fixture.Services.GetRequiredService<ChatService>();
                Room team = await chat.EnsureTeamOneToOneAsync("Home", agent, cancellationToken);
                AgentsMdLedger ledger = fixture.Services.GetRequiredService<AgentsMdLedger>();
                await Eventually.UntilAsync(() => ledger.FirstSightHash("AGENTS.md") is not null, cancellationToken, TestBudgets.Slowest);
                return new Scenario(dir, fixture, agent.Id, direct.Id, team.Id);
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
