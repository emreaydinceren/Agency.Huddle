using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the runner's half of goldfish mode (Goldfish corrections slice 3, step 6): <c>GoldfishBegin</c> opens the pool's goldfish slot and
/// queues the brief; a Message owed to the Agent in the possession Room goes to the goldfish and rides into the warm Catch-up; every other
/// Message goes where it always went; <c>GoldfishEnd</c> closes the slot; and a clean session reports Opened, TurnStarted, TurnEnded and Failed.
/// Runner-level rows run a real <see cref="PersonaRunner"/> against a scripted pipe; session-level rows use <see cref="RoomSessionHarness"/>.
/// </summary>
public sealed class GoldfishRoutingTests
{
    private const string GroupId = "room-g";
    private const string DirectId = "room-d";
    private const string OtherId = "room-o";

    private static readonly RoomInfo Group = new(
        GroupId,
        "Group",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo("agent-1", "nova", UserKind.Agent),
            new MemberInfo("friend", "Friend", UserKind.Agent),
        ]);

    private static readonly RoomInfo Direct = new(
        DirectId,
        "Direct",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo("agent-1", "nova", UserKind.Agent),
        ]);

    private static readonly RoomInfo Other = new(
        OtherId,
        "Other",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo("agent-1", "nova", UserKind.Agent),
            new MemberInfo("friend", "Friend", UserKind.Agent),
        ]);

    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>The first prompt of a goldfish is the brief and nothing else, the slot opens with the possession's spec, and the Room's earlier Catch-up is not in it.</summary>
    [Fact]
    public async Task Begin_OpensTheSlotWithTheSpec_AndTheFirstPromptIsTheBriefOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "earlier chatter", mentioned: false), ct);
        await rig.WaitDeclinedAsync(ct);

        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden", "task-1"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        Assert.Equal(new CleanSessionSpec("p1", GroupId, "task-1"), Assert.Single(rig.Host.CleanSpecs));
        Assert.Equal(Rig.BriefPrompt(Group, "tidy the garden"), Assert.Single(rig.Host.CleanSessions[0].Prompts));
    }

    /// <summary>After the slot is opened and the brief queued the runner writes Opened, naming the possession.</summary>
    [Fact]
    public async Task Begin_WritesOpened()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);

        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitReportAsync(GoldfishReportKind.Opened, ct);

        Assert.Equal("p1", Assert.Single(rig.Reports(GoldfishReportKind.Opened)).PossessionId);
    }

    /// <summary>A Mention in the possession Room goes to the goldfish as a goldfish Message: the sender and the text, with no Catch-up and nothing for the warm session.</summary>
    [Fact]
    public async Task Mention_InThePossessionRoom_GoesToTheGoldfishWithoutCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "earlier chatter", mentioned: false), ct);
        await rig.WaitDeclinedAsync(ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(Posted(Group, "human", "Human", "ping", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        Assert.Equal("Human writes:\n\nping", rig.Host.CleanSessions[0].Prompts[1]);
        Assert.Empty(rig.WarmPrompts);
    }

    /// <summary>The Mention the goldfish was given is also added to the warm Agent's Catch-up, so the warm Agent reads it on its next Turn in that Room.</summary>
    [Fact]
    public async Task Mention_InThePossessionRoom_IsAddedToTheWarmCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "ping", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);
        await rig.SendAsync(new GoldfishEnd("p1", GoldfishExit.Finished), ct);
        await rig.WaitCleanDisposedAsync(ct);

        await rig.SendAsync(Posted(Group, "human", "Human", "welcome back", mentioned: true), ct);
        await rig.WaitWarmPromptsAsync(1, ct);

        // contains-ok: prompt text, not markup; the Catch-up block's layout is pinned by the Catch-up tests.
        Assert.Contains("ping", Assert.Single(rig.WarmPrompts), StringComparison.Ordinal);
    }

    /// <summary>In a one-to-one Room every Message is owed to the Agent, so one without a Mention goes to the goldfish too.</summary>
    [Fact]
    public async Task OneToOne_AnyMessage_GoesToTheGoldfish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync([Direct], ct);
        await rig.SendAsync(new GoldfishBegin("p1", DirectId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(Posted(Direct, "human", "Human", "hello there", mentioned: false), ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        Assert.Equal("Human writes:\n\nhello there", rig.Host.CleanSessions[0].Prompts[1]);
        Assert.Empty(rig.WarmPrompts);
    }

    /// <summary>In a group Room a Message without a Mention is owed to nobody: it is read as context only and declined, and the goldfish is not prompted.</summary>
    [Fact]
    public async Task Group_MessageWithoutMention_IsOwedToNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(Posted(Group, "human", "Human", "just chatting", mentioned: false), ct);
        await rig.WaitDeclinedAsync(ct);

        QueueAck ack = Assert.Single(rig.Server.Received.OfType<QueueAck>(), static ack => ack.Kind == QueueAckKind.Finished);
        Assert.Equal(QueueOutcome.Declined, ack.Outcome);
        Assert.Single(rig.Host.CleanSessions[0].Prompts);
        Assert.Empty(rig.WarmPrompts);
    }

    /// <summary>A Mention in a Room other than the possession's goes to the warm session, and the goldfish is untouched.</summary>
    [Fact]
    public async Task Mention_InAnotherRoom_GoesToTheWarmSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync([Group, Other], ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(Posted(Other, "human", "Human", "elsewhere", mentioned: true), ct);
        await rig.WaitWarmPromptsAsync(1, ct);

        Assert.Single(rig.Host.CleanSessions[0].Prompts);
        // contains-ok: prompt text, not markup; the framing is pinned by the framer tests.
        Assert.Contains("elsewhere", Assert.Single(rig.WarmPrompts), StringComparison.Ordinal);
    }

    /// <summary><c>GoldfishEnd</c> closes the slot, and a Mention in the same Room afterwards goes to the warm session, not to a goldfish.</summary>
    [Fact]
    public async Task GoldfishEnd_ClosesTheSlot_AndLaterMentionsGoToTheWarmSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(new GoldfishEnd("p1", GoldfishExit.Finished), ct);
        await rig.WaitCleanDisposedAsync(ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "after the end", mentioned: true), ct);
        await rig.WaitWarmPromptsAsync(1, ct);

        Assert.Single(rig.Host.CleanSessions[0].Prompts);
        // contains-ok: prompt text, not markup; the framing is pinned by the framer tests.
        Assert.Contains("after the end", Assert.Single(rig.WarmPrompts), StringComparison.Ordinal);
    }

    /// <summary>A second <c>GoldfishBegin</c> while the slot is taken is answered Failed for the new possession and leaves the first goldfish open and serving.</summary>
    [Fact]
    public async Task SecondBegin_IsRejected_WithoutDisturbingTheFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync([Group, Other], ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(new GoldfishBegin("p2", OtherId, "another job"), ct);
        await rig.WaitReportAsync(GoldfishReportKind.Failed, ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "still here?", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        GoldfishReport failed = Assert.Single(rig.Reports(GoldfishReportKind.Failed));
        Assert.Equal("p2", failed.PossessionId);
        Assert.False(string.IsNullOrWhiteSpace(failed.Detail));
        Assert.Single(rig.Host.CleanSpecs);
        Assert.False(rig.Host.CleanSessions[0].Disposed);
    }

    /// <summary>A host that has one session for every Room cannot hold a goldfish: the runner answers Failed with a reason and opens nothing.</summary>
    [Fact]
    public async Task Begin_WithASharedSessionHost_WritesFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartSharedAsync(ct);

        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitReportAsync(GoldfishReportKind.Failed, ct);

        GoldfishReport failed = Assert.Single(rig.Reports(GoldfishReportKind.Failed));
        Assert.Equal("p1", failed.PossessionId);
        Assert.False(string.IsNullOrWhiteSpace(failed.Detail));
        Assert.Empty(rig.Host.CleanSpecs);
    }

    /// <summary>
    /// A goldfish whose session cannot open reports Failed with the reason; once the server ends the possession the slot is free, and a new
    /// possession opens and gets its brief.
    /// </summary>
    [Fact]
    public async Task FailingOpen_WritesFailed_AndTheSlotFreesOnEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        rig.Host.FailNextOpenWith(new InvalidOperationException("no adapter today"));

        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitReportAsync(GoldfishReportKind.Failed, ct);
        await rig.SendAsync(new GoldfishEnd("p1", GoldfishExit.Failed), ct);
        await rig.SendAsync(new GoldfishBegin("p2", GroupId, "try again"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        GoldfishReport failed = Assert.Single(rig.Reports(GoldfishReportKind.Failed));
        Assert.Equal("p1", failed.PossessionId);
        // contains-ok: the reason is the exception's own text inside the runner's wording.
        Assert.Contains("no adapter today", failed.Detail, StringComparison.Ordinal);
        Assert.Equal("p2", rig.Host.CleanSpecs[^1].PossessionId);
    }

    /// <summary>The goldfish reports TurnStarted and TurnEnded around each Turn; TurnEnded carries the session's running spend (1.0, then 1.5) and never claims Done.</summary>
    [Fact]
    public async Task Turns_WriteStartedAndEnded_WithTheRunningSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartScriptedAsync(
            static session =>
            {
                session.EnqueueReplyWithUsageAndCost([10], [new UsageCost(1.0m, "USD")], "first");
                session.EnqueueReplyWithUsageAndCost([20], [new UsageCost(1.5m, "USD")], "second");
            },
            ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);
        await rig.SendAsync(Posted(Group, "human", "Human", "ping", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);
        await Eventually.UntilAsync(() => rig.Reports(GoldfishReportKind.TurnEnded).Count == 2, ct, timeout: TestBudgets.Standard);

        (GoldfishReportKind, decimal?, string?, bool?)[] turnReports =
        [
            .. rig.Server.Received.OfType<GoldfishReport>()
                .Where(static report => report.Kind is GoldfishReportKind.TurnStarted or GoldfishReportKind.TurnEnded)
                .Select(static report => (report.Kind, report.RunningSpend, report.Currency, report.Done)),
        ];
        Assert.Equal(
            [
                (GoldfishReportKind.TurnStarted, null, null, null),
                (GoldfishReportKind.TurnEnded, 1.0m, "USD", false),
                (GoldfishReportKind.TurnStarted, null, null, null),
                (GoldfishReportKind.TurnEnded, 1.5m, "USD", false),
            ],
            turnReports);
        Assert.All(rig.Server.Received.OfType<GoldfishReport>(), static report => Assert.Equal("p1", report.PossessionId));
    }

    /// <summary>A Command in the possession Room is an ordinary Message to the goldfish: its text arrives as written, framed like any other, and is not run as a command.</summary>
    [Fact]
    public async Task Command_InThePossessionRoom_IsAnOrdinaryMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.StartAsync(ct);
        await rig.SendAsync(new GoldfishBegin("p1", GroupId, "tidy the garden"), ct);
        await rig.WaitCleanPromptsAsync(1, ct);

        await rig.SendAsync(Posted(Group, "human", "Human", "@nova /compact", mentioned: true), ct);
        await rig.WaitCleanPromptsAsync(2, ct);

        Assert.Equal("Human writes:\n\n@nova /compact", rig.Host.CleanSessions[0].Prompts[1]);
    }

    /// <summary>A brief item reaches the Agent as the brief alone: the Catch-up and own-post lines it was built with are not in the prompt.</summary>
    [Fact]
    public async Task BriefItem_PromptIsTheBriefOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = GoldfishHarness(owner, session);
        WorkItem item = new(
            "room-1", "Room 1", "Huddle", "tidy the garden", [new CaughtUpMessage("Friend", "old news")],
            WorkItemKind.GoldfishBrief, OwnPostLines: ["I said a thing"]);

        harness.Room.Enqueue(new QueuedWork(1, item));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        string label = new FakePromptSource().Render(
            "turn.roomLabel",
            new Dictionary<string, string> { ["{{roomName}}"] = "Room 1", ["{{roomId}}"] = "room-1", ["{{teamSuffix}}"] = string.Empty });
        Assert.Equal("Brief for the work in " + label + ":\n\ntidy the garden", session.Prompts[0]);
    }

    /// <summary>A goldfish session whose open fails writes Failed with the reason and the possession's id; it writes no TurnEnded for a Turn that never ran.</summary>
    [Fact]
    public async Task LazyOpenFailure_WritesFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            static _ => throw new IOException("adapter gone"),
            GoldfishOptions());

        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Huddle", "tidy the garden", [], WorkItemKind.GoldfishBrief)));
        await Eventually.UntilAsync(() => owner.Written.OfType<GoldfishReport>().Any(static report => report.Kind == GoldfishReportKind.Failed), ct, timeout: TestBudgets.Standard);

        GoldfishReport failed = Assert.Single(owner.Written.OfType<GoldfishReport>(), static report => report.Kind == GoldfishReportKind.Failed);
        Assert.Equal("p1", failed.PossessionId);
        // contains-ok: the reason is the exception's own text inside the runner's wording.
        Assert.Contains("adapter gone", failed.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(owner.Written.OfType<GoldfishReport>(), static report => report.Kind == GoldfishReportKind.TurnEnded);
    }

    /// <summary>An ordinary warm session writes no goldfish report at all: the reports belong to a possession's session.</summary>
    [Fact]
    public async Task WarmSession_WritesNoGoldfishReport()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new RoomSessionHarnessOptions { Persona = Nova, AgentId = "agent-1" });

        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "hello", [], TriggerMessageId: "trig-1")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1 && owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        Assert.Empty(owner.Written.OfType<GoldfishReport>());
    }

    private static RoomSessionHarnessOptions GoldfishOptions() => new()
    {
        Persona = Nova,
        AgentId = "agent-1",
        Clean = true,
        Goldfish = new CleanSessionSpec("p1", "room-1", "task-1"),
    };

    private static RoomSessionHarness GoldfishHarness(FakeRoomSessionOwner owner, FakeAgentSession session) =>
        RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), GoldfishOptions());

    private static MessagePosted Posted(RoomInfo room, string senderId, string senderName, string text, bool mentioned) =>
        new(
            room.Id,
            room.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, senderId, senderName, text),
            mentioned,
            Mentions: [],
            Members: room.Members,
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

        /// <summary>The prompts every warm (non-goldfish) session of the host has received, in no particular session order.</summary>
        public IReadOnlyList<string> WarmPrompts =>
            [.. this.Host.Sessions.Where(session => !this.Host.CleanSessions.Contains(session)).SelectMany(static session => session.Prompts)];

        public static async Task<Rig> StartAsync(CancellationToken ct) =>
            await StartCoreAsync([Group], sessionPerRoom: true, onOpen: null, ct);

        public static async Task<Rig> StartAsync(RoomInfo[] rooms, CancellationToken ct) =>
            await StartCoreAsync(rooms, sessionPerRoom: true, onOpen: null, ct);

        public static async Task<Rig> StartSharedAsync(CancellationToken ct) =>
            await StartCoreAsync([Group], sessionPerRoom: false, onOpen: null, ct);

        public static async Task<Rig> StartScriptedAsync(Action<FakeAgentSession> onOpen, CancellationToken ct) =>
            await StartCoreAsync([Group], sessionPerRoom: true, onOpen, ct);

        private static async Task<Rig> StartCoreAsync(RoomInfo[] rooms, bool sessionPerRoom, Action<FakeAgentSession>? onOpen, CancellationToken ct)
        {
            PipeRunnerServer server = new("runner-goldfish-tests");
            FakeAgentHostFactory factory = new() { SessionPerRoom = sessionPerRoom };
            PersonaRunner runner = new(
                TestPersonas.Nova,
                TestOptions.Create(opts => opts.PipeName = server.PipeName),
                factory,
                new FakePromptSource(),
                new RoomFollows(),
                NullLogger<PersonaRunner>.Instance);
            Rig rig = new(server, factory, runner);
            await server.HandshakeAsync(runner, rooms.Length == 0 ? [Group] : rooms, ct);
            if (onOpen is not null)
            {
                FakePersonaHost host = rig.Host;
                host.OnOpen = session =>
                {
                    if (host.CleanSessions.Contains(session))
                    {
                        onOpen(session);
                    }
                };
            }

            return rig;
        }

        public Task SendAsync(ProtocolMessage message, CancellationToken ct) => this.Server.SendAsync(message, ct);

        public IReadOnlyList<GoldfishReport> Reports(GoldfishReportKind kind) =>
            [.. this.Server.Received.OfType<GoldfishReport>().Where(report => report.Kind == kind)];

        public static string BriefPrompt(RoomInfo room, string brief) =>
            new FakePromptSource().Render(
                "turn.goldfishBrief",
                new Dictionary<string, string>
                {
                    ["{{roomLabel}}"] = new FakePromptSource().Render(
                        "turn.roomLabel",
                        new Dictionary<string, string> { ["{{roomName}}"] = room.Name, ["{{roomId}}"] = room.Id, ["{{teamSuffix}}"] = string.Empty }),
                    ["{{brief}}"] = brief,
                });

        public Task WaitReportAsync(GoldfishReportKind kind, CancellationToken ct) =>
            Eventually.UntilAsync(() => this.Reports(kind).Count > 0, ct, timeout: TestBudgets.Standard);

        public Task WaitDeclinedAsync(CancellationToken ct) =>
            Eventually.UntilAsync(() => this.Server.Received.OfType<QueueAck>().Any(static ack => ack.Kind == QueueAckKind.Finished), ct, timeout: TestBudgets.Standard);

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
