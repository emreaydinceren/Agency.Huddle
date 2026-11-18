using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Conformance;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Goldfish mode end to end through the real host (Goldfish corrections slice 3, step 6): the supervisor sends <c>GoldfishBegin</c> down the
/// real pipe, the real runner opens a clean session on the mock Adapter, answers reach it, the possession ends and the Agent is called back.
/// </summary>
public sealed class GoldfishEndToEndTests
{
    private const string Brief = "read the plan";

    /// <summary>A possession's first prompt to the Adapter is the brief and nothing else, and the possession leaves Starting once that Turn has ended.</summary>
    [Fact]
    public async Task Begin_TheBriefIsTheFirstPrompt_AndTheRowWaitsWhenTheTurnEnds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));
        await using Scenario scenario = await Scenario.StartAsync(bound.Token);

        Possession row = await scenario.BeginAsync(bound.Token);
        string prompt = await scenario.PromptAsync(static text => text.StartsWith("Brief for the work in ", StringComparison.Ordinal), bound.Token);
        await Eventually.UntilAsync(() => scenario.Store.Get(row.Id)?.State == PossessionState.Waiting, bound.Token, TestBudgets.Slowest);

        Assert.Equal($"Brief for the work in [Room: nova (id: {scenario.RoomId})]:\n\n{Brief}", prompt);
        Assert.Equal(1, scenario.Store.Get(row.Id)?.Turns);
    }

    /// <summary>
    /// The whole cycle: begin, the goldfish's Turn (its reply is its question in the Room), the Human's answer reaches it as a goldfish Message,
    /// the possession ends Done with a result, and the Agent gets the return Turn carrying that result.
    /// </summary>
    [Fact]
    public async Task FullCycle_BeginQuestionAnswerDoneReturn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));
        await using Scenario scenario = await Scenario.StartAsync(bound.Token);
        Possession row = await scenario.BeginAsync(bound.Token);
        _ = await scenario.PromptAsync(static text => text.StartsWith("Brief for the work in ", StringComparison.Ordinal), bound.Token);
        await Eventually.UntilAsync(() => scenario.Store.Get(row.Id)?.State == PossessionState.Waiting, bound.Token, TestBudgets.Slowest);

        await scenario.Chat.PostAsync(scenario.RoomId, KnownIds.Human, "use tabs", ct: bound.Token);
        string answer = await scenario.PromptAsync(static text => text.StartsWith("You writes:", StringComparison.Ordinal), bound.Token);
        bool ended = await scenario.Supervisor.EndAsync(row.Id, GoldfishExit.Finished, "nova", bound.Token, new GoldfishResult("planted the tree", ["f1"]));
        string back = await scenario.PromptAsync(static text => text.Contains("planted the tree", StringComparison.Ordinal), bound.Token); // contains-ok: the return Turn's wording is slice 1's; the result text inside it is what this row pins

        Assert.Equal("You writes:\n\nuse tabs", answer);
        Assert.True(ended);
        Assert.Equal(GoldfishExit.Finished, scenario.Store.Get(row.Id)?.Exit);
        Assert.DoesNotContain("You writes:", back, StringComparison.Ordinal);
    }

    /// <summary>A Message in the possession Room after the possession has ended goes to the warm session, not to a goldfish.</summary>
    [Fact]
    public async Task AfterTheEnd_AMessageGoesToTheWarmSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(60));
        await using Scenario scenario = await Scenario.StartAsync(bound.Token);
        Possession row = await scenario.BeginAsync(bound.Token);
        _ = await scenario.PromptAsync(static text => text.StartsWith("Brief for the work in ", StringComparison.Ordinal), bound.Token);
        _ = await scenario.Supervisor.EndAsync(row.Id, GoldfishExit.Finished, "nova", bound.Token, new GoldfishResult("all done", []));
        _ = await scenario.PromptAsync(static text => text.Contains("all done", StringComparison.Ordinal), bound.Token); // contains-ok: waits for the return Turn by its result text

        await scenario.Chat.PostAsync(scenario.RoomId, KnownIds.Human, "welcome back", ct: bound.Token);
        string prompt = await scenario.PromptAsync(static text => text.Contains("welcome back", StringComparison.Ordinal), bound.Token); // contains-ok: the warm frame's layout is pinned by the framer tests

        Assert.DoesNotContain("writes:", prompt, StringComparison.Ordinal);
    }

    /// <summary>A pipe killed mid-possession ends the possession Failed; the return Turn is queued and arrives once the Agent reconnects.</summary>
    [Fact]
    public async Task PipeKilledMidPossession_EndsFailed_AndTheReturnTurnArrivesAfterReconnect()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(90));
        await using Scenario scenario = await Scenario.StartAsync(bound.Token);
        Possession row = await scenario.BeginAsync(bound.Token);
        _ = await scenario.PromptAsync(static text => text.StartsWith("Brief for the work in ", StringComparison.Ordinal), bound.Token);

        await scenario.Fixture.Runner.DisposeAsync();
        // The exit is claimed (state Returning) before the return is posted. Restarting the host in that window stops the return and the start-up pass
        // rightly ends the row Restarted, so wait for the row to be Ended, the point at which Failed is final.
        await Eventually.UntilAsync(() => scenario.Store.Get(row.Id) is { State: PossessionState.Ended, Exit: GoldfishExit.Failed }, bound.Token, TestBudgets.Slowest);
        int before = scenario.PromptCount;
        await scenario.RestartAsync(bound.Token);
        string back = await scenario.PromptAsync(static text => text.Contains(Brief, StringComparison.Ordinal) && !text.StartsWith("Brief for the work in ", StringComparison.Ordinal), bound.Token); // contains-ok: the return Turn names the brief the possession was given

        Assert.Equal(GoldfishExit.Failed, scenario.Store.Get(row.Id)?.Exit);
        Assert.True(scenario.PromptCount > before);
        Assert.NotEmpty(back);
    }

    /// <summary>One real host with the Agent on a Claude-shaped, isolating mock Adapter, and its direct Room with the Human.</summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly TempDataDir dataDir;
        private readonly Dictionary<string, string?> config;
        private int earlierPrompts;

        private Scenario(TempDataDir dataDir, Dictionary<string, string?> config, MockAdapterFixture fixture, string agentId, string roomId)
        {
            this.dataDir = dataDir;
            this.config = config;
            this.Fixture = fixture;
            this.AgentId = agentId;
            this.RoomId = roomId;
        }

        internal MockAdapterFixture Fixture { get; private set; }

        internal string AgentId { get; }

        internal string RoomId { get; }

        internal GoldfishSupervisor Supervisor => this.Fixture.Services.GetRequiredService<GoldfishSupervisor>();

        internal GoldfishStore Store => this.Fixture.Services.GetRequiredService<GoldfishStore>();

        internal ChatService Chat => this.Fixture.Services.GetRequiredService<ChatService>();

        /// <summary>Every prompt the Adapter has received, counting those of hosts already restarted away (each host's Adapter keeps its own list).</summary>
        internal int PromptCount => this.earlierPrompts + this.PromptTexts().Count;

        internal static async Task<Scenario> StartAsync(CancellationToken cancellationToken)
        {
            TempDataDir dir = new();
            Dictionary<string, string?> config = new(StringComparer.Ordinal)
            {
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "node",
                ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
                ["Team:Acp:Adapters:0:Args:1"] = "claude-agent-acp",
                ["Team:Acp:Adapters:0:IsolateUserSettings"] = "true",
            };
            Persona persona = new("nova", "You are Nova.", Adapter: "agency");
            MockAdapterFixture fixture = await MockAdapterFixture.StartFullPipelineAsync(persona, dir.Path, config, cancellationToken: cancellationToken);
            try
            {
                // The restarted host closes a row owed to a Persona the PersonaStore does not hold, so the Persona is stored for the second host to find.
                fixture.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity("nova", "Tester", "nova", [], "agency"), persona.Text);
                ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
                User agent = await directory.FindUserByNameAsync("nova", cancellationToken) ?? throw new InvalidOperationException("No nova.");
                Room direct = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, cancellationToken) ?? throw new InvalidOperationException("No direct Room.");
                return new Scenario(dir, config, fixture, agent.Id, direct.Id);
            }
            catch
            {
                await fixture.DisposeAsync();
                dir.Dispose();
                throw;
            }
        }

        internal async Task<Possession> BeginAsync(CancellationToken cancellationToken) =>
            await this.Supervisor.BeginAsync(this.AgentId, this.RoomId, null, Brief, cancellationToken) ?? throw new InvalidOperationException("The Agent was already possessed.");

        /// <summary>Waits for the first prompt the Adapter has received that satisfies <paramref name="match"/>, and returns its text.</summary>
        internal async Task<string> PromptAsync(Func<string, bool> match, CancellationToken cancellationToken)
        {
            await Eventually.UntilAsync(() => this.PromptTexts().Any(match), cancellationToken, TestBudgets.Slowest);
            return this.PromptTexts().First(match);
        }

        /// <summary>Starts a new host and runner on the same data directory, after the old ones are gone.</summary>
        internal async Task RestartAsync(CancellationToken cancellationToken)
        {
            this.earlierPrompts += this.PromptTexts().Count;
            await this.Fixture.DisposeAsync();
            this.Fixture = await MockAdapterFixture.StartFullPipelineAsync(new Persona("nova", "You are Nova.", Adapter: "agency"), this.dataDir.Path, this.config, cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Fixture.DisposeAsync();
            this.dataDir.Dispose();
        }

        private static string PromptText(JsonObject message)
        {
            JsonArray? blocks = (message["params"] as JsonObject)?["prompt"] as JsonArray;
            return blocks is null ? string.Empty : string.Concat(blocks.Select(static block => (string?)block?["text"]));
        }

        private List<string> PromptTexts() => [.. this.Fixture.Agent.Received
            .Where(static message => string.Equals((string?)message["method"], "session/prompt", StringComparison.Ordinal))
            .Select(PromptText)];
    }
}
