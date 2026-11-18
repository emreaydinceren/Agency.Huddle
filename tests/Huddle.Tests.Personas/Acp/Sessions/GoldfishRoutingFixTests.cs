using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Review fixes to the runner's goldfish routing (Slice 3): a followed Room's Message never starts a warm Turn while the Agent is possessed,
/// a re-sent Message is not added to the Catch-up twice, and a <c>GoldfishEnd</c> closes only the slot of its own possession.
/// </summary>
public sealed class GoldfishRoutingFixTests
{
    private const string GroupId = "room-g";
    private const string AgentId = "agent-1";

    private static readonly RoomInfo Group = new(
        GroupId,
        "Group",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo(AgentId, "nova", UserKind.Agent),
            new MemberInfo("friend", "Friend", UserKind.Agent),
        ]);

    /// <summary>A Message in a followed group Room that is not a Mention is owed to nobody while possessed: no warm Turn, no goldfish Turn, and the line waits in the warm Catch-up.</summary>
    [Fact]
    public async Task FollowedRoom_NonMention_WhilePossessed_StartsNoTurn_AndJoinsTheCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RoomFollows follows = new();
        await using Rig rig = await Rig.StartAsync(follows, ct);

        // After the handshake: the runner clears its Agent's follows when it registers.
        _ = follows.Follow(AgentId, GroupId);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        MessagePosted chatter = Posted("human", "Human", "followedchatter77", mentioned: false);
        await rig.SendAsync(chatter, ct);
        await Eventually.UntilAsync(
            () => rig.Server.Received.OfType<QueueAck>().Any(ack => ack.Kind == QueueAckKind.Finished && ack.Outcome == QueueOutcome.Declined && ack.MessageIds.Contains(chatter.Message.Id)),
            ct,
            timeout: TestBudgets.Standard);
        Assert.Empty(rig.WarmPrompts);
        await rig.SendAsync(Posted("human", "Human", "ping", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);
        await rig.SendAsync(new GoldfishEnd("p1", GoldfishExit.Finished), ct);
        await rig.WaitCleanDisposedAsync(ct);

        await rig.SendAsync(Posted("human", "Human", "welcome back", mentioned: true), ct);
        await rig.WaitWarmPromptsAsync(1, ct);

        // The first warm Turn is the welcome-back one (a warm Turn for the chatter would come first and lack it), and it read the chatter as Catch-up.
        string firstWarm = rig.WarmPrompts[0];
        Assert.Equal(2, rig.Host.CleanSessions[0].Prompts.Count);
        // contains-ok: prompt text, not markup; the Catch-up block's layout is pinned by the Catch-up tests.
        Assert.Contains("welcome back", firstWarm, StringComparison.Ordinal);
        // contains-ok: prompt text, not markup; the Catch-up block's layout is pinned by the Catch-up tests.
        Assert.Contains("followedchatter77", firstWarm, StringComparison.Ordinal);
    }

    /// <summary>The same Message sent again while possessed gives the goldfish one Turn and the warm Catch-up one line.</summary>
    [Fact]
    public async Task Redelivery_WhilePossessed_IsNotDuplicated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(new RoomFollows(), ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);
        MessagePosted ping = Posted("human", "Human", "uniqueping42", mentioned: true);
        await rig.SendAsync(ping, ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        await rig.SendAsync(ping with { Redelivery = Redelivery.Waiting }, ct);
        await rig.SendAsync(Posted("human", "Human", "marker", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(3, ct);
        await rig.SendAsync(new GoldfishEnd("p1", GoldfishExit.Finished), ct);
        await rig.WaitCleanDisposedAsync(ct);
        await rig.SendAsync(Posted("human", "Human", "welcome back", mentioned: true), ct);
        await rig.WaitWarmPromptsAsync(1, ct);

        Assert.Equal(["Human writes:\n\nuniqueping42", "Human writes:\n\nmarker"], rig.Host.CleanSessions[0].Prompts.Skip(1));
        Assert.Equal(2, Assert.Single(rig.WarmPrompts).Split("uniqueping42", StringSplitOptions.None).Length);
    }

    /// <summary>An End for a possession that was refused ("slot taken") leaves the open possession serving.</summary>
    [Fact]
    public async Task End_OfARefusedPossession_LeavesTheOpenSlotServing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(new RoomFollows(), ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);
        await rig.SendAsync(new GoldfishBegin("p2", GroupId, "another job"), ct);
        await rig.WaitReportAsync(GoldfishReportKind.Failed, ct);

        await rig.SendAsync(new GoldfishEnd("p2", GoldfishExit.Failed), ct);
        await rig.SendAsync(Posted("human", "Human", "still here?", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        Assert.Equal("Human writes:\n\nstill here?", rig.Host.CleanSessions[0].Prompts[1]);
        Assert.False(rig.Host.CleanSessions[0].Disposed);
        Assert.Empty(rig.WarmPrompts);
    }

    private static MessagePosted Posted(string senderId, string senderName, string text, bool mentioned) =>
        new(
            GroupId,
            Group.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, senderId, senderName, text),
            mentioned,
            Mentions: [],
            Members: Group.Members,
            AgentMessagesSinceHuman: 0,
            Budget: 10);

    /// <summary>A real runner on a scripted pipe over a fake host that keeps one session per Room.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly PersonaRunner runner;

        private Rig(PipeRunnerServer server, FakeAgentHostFactory factory, PersonaRunner runner)
        {
            this.Server = server;
            this.Factory = factory;
            this.runner = runner;
        }

        public PipeRunnerServer Server { get; }

        public FakeAgentHostFactory Factory { get; }

        public FakePersonaHost Host => this.Factory.Host ?? throw new InvalidOperationException("The host has not started.");

        public IReadOnlyList<string> WarmPrompts =>
            [.. this.Host.Sessions.Where(session => !this.Host.CleanSessions.Contains(session)).SelectMany(static session => session.Prompts)];

        public static async Task<Rig> StartAsync(RoomFollows follows, CancellationToken ct)
        {
            PipeRunnerServer server = new("runner-goldfish-fix-tests");
            FakeAgentHostFactory factory = new() { SessionPerRoom = true };
            PersonaRunner runner = new(
                TestPersonas.Nova,
                TestOptions.Create(opts => opts.PipeName = server.PipeName),
                factory,
                new FakePromptSource(),
                follows,
                NullLogger<PersonaRunner>.Instance);
            Rig rig = new(server, factory, runner);
            await server.HandshakeAsync(runner, [Group], ct);
            return rig;
        }

        public Task SendAsync(ProtocolMessage message, CancellationToken ct) => this.Server.SendAsync(message, ct);

        public Task WaitReportAsync(GoldfishReportKind kind, CancellationToken ct) =>
            Eventually.UntilAsync(() => this.Server.Received.OfType<GoldfishReport>().Any(report => report.Kind == kind), ct, timeout: TestBudgets.Standard);

        public Task WaitCleanPromptsAsync(int count, CancellationToken ct) =>
            Eventually.UntilAsync(() => this.Host.CleanSessions.Count > 0 && this.Host.CleanSessions[^1].Prompts.Count >= count, ct, timeout: TestBudgets.Standard);

        public Task WaitWarmPromptsAsync(int count, CancellationToken ct) =>
            Eventually.UntilAsync(() => this.WarmPrompts.Count >= count, ct, timeout: TestBudgets.Standard);

        public Task WaitCleanDisposedAsync(CancellationToken ct) =>
            Eventually.UntilAsync(() => this.Host.CleanSessions.Count > 0 && this.Host.CleanSessions[0].Disposed, ct, timeout: TestBudgets.Standard);

        public async ValueTask DisposeAsync()
        {
            await this.runner.DisposeAsync();
            await this.Server.DisposeAsync();
        }
    }
}
