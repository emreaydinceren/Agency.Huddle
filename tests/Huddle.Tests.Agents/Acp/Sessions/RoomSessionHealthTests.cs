namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Pipes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Pins RS §6.8's health and token-Budget rules across Room Sessions: one Persona-wide failure
/// streak whose reason names the Room (finding P-14), the summed token Budget, and a per-Room
/// session closing and forgetting itself after two consecutive failures. Three harnesses, one per
/// group of tests: (A) a bare <see cref="PersonaRunner"/> cast to <see cref="IRoomSessionOwner"/>
/// directly, with no pipe at all, for the wording and streak rules that live entirely in
/// <see cref="PersonaRunner"/>; (B) a single <see cref="RoomSession"/> built exactly as
/// <c>RoomSessionResumeTests</c> does, for the per-Room close-and-forget rule; (C) a real
/// <see cref="PersonaRunner"/> over <see cref="PipeHostFixture"/>'s pipe and <see cref="ChatService"/>,
/// for the Budget rules, which need two genuinely open Room Sessions.
/// </summary>
public sealed class RoomSessionHealthTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    // --- Group A: wording and the one cross-Room streak -------------------------------------

    /// <summary>A single failure's reason names the Room it happened in.</summary>
    [Fact]
    public void ReportTurnFailure_Once_NamesTheRoom()
    {
        PersonaRunner runner = CreateBareRunner();
        StatusRecorder statuses = new();
        runner.StatusChanged += statuses.Record;
        IRoomSessionOwner owner = runner;

        owner.ReportTurnFailure("Porto trip", "the adapter said no");

        var status = Assert.Single(statuses.Snapshot());
        Assert.Equal(PersonaState.Degraded, status.State);
        Assert.Equal("A Turn in Room 'Porto trip' failed — the adapter said no", status.Reason);
    }

    /// <summary>The third consecutive failure escalates the wording, keeping the live count and naming the last Room and reason.</summary>
    [Fact]
    public void ReportTurnFailure_Third_Escalates_NamingTheLastRoomAndReason()
    {
        PersonaRunner runner = CreateBareRunner();
        StatusRecorder recorder = new();
        runner.StatusChanged += recorder.Record;
        IRoomSessionOwner owner = runner;

        owner.ReportTurnFailure("Room A", "one");
        owner.ReportTurnFailure("Room A", "two");
        owner.ReportTurnFailure("Porto trip", "three");

        var statuses = recorder.Snapshot();
        Assert.Equal(3, statuses.Count);
        Assert.Equal(
            "3 consecutive Turns have failed; this is unlikely to be transient — the last, in Room 'Porto trip': three",
            statuses[2].Reason);
    }

    /// <summary>RS §6.8: failures in different Rooms feed the SAME streak - two failures in Room A and one in Room B still escalate on the third.</summary>
    [Fact]
    public void ReportTurnFailure_AcrossTwoRooms_OneStreak_ThirdEscalates()
    {
        PersonaRunner runner = CreateBareRunner();
        StatusRecorder recorder = new();
        runner.StatusChanged += recorder.Record;
        IRoomSessionOwner owner = runner;

        owner.ReportTurnFailure("Room A", "boom a1");
        owner.ReportTurnFailure("Room B", "boom b1");
        owner.ReportTurnFailure("Room A", "boom a2");

        var statuses = recorder.Snapshot();
        Assert.Equal(3, statuses.Count);
        Assert.DoesNotContain("consecutive Turns have failed", statuses[0].Reason);
        Assert.DoesNotContain("consecutive Turns have failed", statuses[1].Reason);
        Assert.Contains("3 consecutive Turns have failed", statuses[2].Reason, StringComparison.Ordinal);
    }

    /// <summary>A completed Turn resets the streak: a failure right after one is reported as the first again, never escalated.</summary>
    [Fact]
    public void ReportTurnCompleted_ResetsStreak()
    {
        PersonaRunner runner = CreateBareRunner();
        StatusRecorder statuses = new();
        runner.StatusChanged += statuses.Record;
        IRoomSessionOwner owner = runner;

        owner.ReportTurnFailure("Room A", "one");
        owner.ReportTurnFailure("Room A", "two");
        owner.ReportTurnCompleted();
        owner.ReportTurnFailure("Room A", "three");

        var lastFailure = Assert.Single(
            statuses.Snapshot(),
            s => s.State == PersonaState.Degraded && s.Reason is not null && s.Reason.Contains("three", StringComparison.Ordinal));
        Assert.Equal("A Turn in Room 'Room A' failed — three", lastFailure.Reason);
    }

    // --- Group B: a per-Room session closes and forgets itself after two failures -----------

    /// <summary>RS §6.8, finding P-14: a second consecutive failure in one per-Room session closes it and forgets its stored entry, so its next Turn opens fresh rather than resuming.</summary>
    [Fact]
    public async Task TwoFailuresInOneRoom_ClosesAndForgetsThatSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("old-session", "claude", "m1", "e1", null, DateTimeOffset.UtcNow));

        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom 1"));
        session.EnqueueFailure(new InvalidOperationException("boom 2"));
        FakePersonaHost host = new(session, ClaudeProfile()) { CanResume = true };
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = CreateRoomSession(owner, host, store);
        RoomSession room = harness.Room;
        room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "one", [], TriggerMessageId: "trig-1")));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportTurnFailure)) == 1, ct, timeout: TestBudgets.Standard);

        Assert.NotNull(store.Get("nova", "room-1"));

        room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "two", [], TriggerMessageId: "trig-2")));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportTurnFailure)) == 2, ct, timeout: TestBudgets.Standard);

        await Eventually.UntilAsync(() => room.State == RoomSessionState.Closed, ct, timeout: TestBudgets.Standard);
        Assert.Null(store.Get("nova", "room-1"));

        // A third item reopens - since the entry was forgotten, the resume lookup finds nothing,
        // so ResumeAsync is never called even though this host is otherwise resume-capable.
        session.EnqueueReply("fresh reply");
        room.Enqueue(new QueuedWork(3, new WorkItem("room-1", "Room 1", "Human", "three", [], TriggerMessageId: "trig-3")));
        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        // ResumeCalls already holds "old-session" from item 1's own legitimate resume, before
        // either failure - what this proves is that item 3, AFTER the forget, adds no new one.
        Assert.Single(host.ResumeCalls);
        Assert.Equal(2, host.Sessions.Count);
    }

    /// <summary>Finding P-14: close-after-two applies in per-Room mode only - a shared session's two failures leave it open.</summary>
    [Fact]
    public async Task SharedMode_TwoFailures_DoesNotClose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom 1"));
        session.EnqueueFailure(new InvalidOperationException("boom 2"));
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { RoomId = null });
        RoomSession room = harness.Room;
        room.Enqueue(new QueuedWork(1, new WorkItem("room-a", "Room A", "Human", "one", [])));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportTurnFailure)) == 1, ct, timeout: TestBudgets.Standard);
        room.Enqueue(new QueuedWork(2, new WorkItem("room-a", "Room A", "Human", "two", [])));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportTurnFailure)) == 2, ct, timeout: TestBudgets.Standard);

        // Give a wrongly-closing implementation every chance to have closed by now.
        await Task.Delay(200, ct);

        Assert.NotEqual(RoomSessionState.Closed, room.State);
    }

    /// <summary>RS §9 E-10: the Adapter process disconnecting reports Offline, never counts toward the close-after-two streak, and forgets nothing.</summary>
    [Fact]
    public async Task AdapterDisconnected_ReportsOffline_EntriesKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("old-session", "claude", "m1", "e1", null, DateTimeOffset.UtcNow));

        FakeAgentSession session = new();
        session.EnqueueFailure(new AgentDisconnectedException());
        session.EnqueueFailure(new AgentDisconnectedException());
        FakePersonaHost host = new(session, ClaudeProfile());
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = CreateRoomSession(owner, host, store);
        RoomSession room = harness.Room;
        room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "one", [], TriggerMessageId: "trig-1")));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportOffline)) == 1, ct, timeout: TestBudgets.Standard);
        room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "two", [], TriggerMessageId: "trig-2")));
        await Eventually.UntilAsync(() => owner.ReportCalls.Count(call => call == nameof(IRoomSessionOwner.ReportOffline)) == 2, ct, timeout: TestBudgets.Standard);

        // Give a wrongly-counting implementation every chance to have closed and forgotten by now.
        await Task.Delay(200, ct);

        Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
        Assert.NotNull(store.Get("nova", "room-1"));
    }

    // --- Group C: the summed token Budget, over two real, open Room Sessions ----------------
    //
    // Every Turn below is triggered by "friend" Mentioning Nova, deliberately never by the Human:
    // PersonaRunner resets the token counter the instant ANY Human Message arrives (RS §6.8), before
    // the very Turn it triggers is even built - so a Turn meant to prove tokens ACCUMULATE, or that a
    // spent Budget BLOCKS one, can never itself be Human-triggered. Only HumanMessageInAnyRoom_ResetsBudget
    // wants that reset, and asks for it on purpose.

    /// <summary>Context-window rises in two different Room Sessions add to the SAME Persona-wide token counter.</summary>
    [Fact]
    public async Task UsageRisesInTwoSessions_Sum()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var acp = new AcpOptions { TokenBudget = 1200 };
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };

        // Queued for Nova's own direct Room Session (session[0]) but never spent: this test never
        // posts there, only into Room P and Room Q below, each scripted through OnOpen.
        novaFactory.Session.EnqueueReply("hello!");

        await using PersonaRunner runner = CreateRunner(fixture, Nova, novaFactory, acp);
        await runner.StartAsync(ct);

        var (novaId, _) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        var friendId = await RegisterLightweightAgentAsync(fixture, "friend", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomP = await chat.CreateRoomForAsync([novaId, friendId], ct);
        var roomQ = await chat.CreateRoomForAsync([novaId, friendId], ct);

        novaFactory.Host!.OnOpen = session => session.EnqueueReplyWithUsage([1000], "p1");
        await chat.PostAsync(roomP.Id, friendId, "@nova p1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomP.Id, 2, ct);

        novaFactory.Host.OnOpen = session => session.EnqueueReplyWithUsage([500], "q1");
        await chat.PostAsync(roomQ.Id, friendId, "@nova q1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomQ.Id, 2, ct);

        // 1000 + 500 = 1500, over the 1200 budget: a further Turn in Room P must be dropped without
        // ever prompting again - proof the two sessions' rises share one counter.
        await chat.PostAsync(roomP.Id, friendId, "@nova p2", ct: ct);
        await Task.Delay(300, ct);

        Assert.Single(novaFactory.Host.Sessions[1].Prompts);
    }

    /// <summary>A lower context-window reading than the one before it is a compaction, not spend, and must not count against another Room Session's own rises.</summary>
    [Fact]
    public async Task CompactionInOneSession_NotCountedAgainstAnother()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        // Naive (wrong) summing of every Used value would read 1000 + 5000 + 800 = 6800 - over this
        // budget. The correct sum, dropping the compaction, is 6000 - under it, so a third Room P
        // Turn must still be admitted.
        var acp = new AcpOptions { TokenBudget = 6500 };
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };
        novaFactory.Session.EnqueueReply("hello!");

        await using PersonaRunner runner = CreateRunner(fixture, Nova, novaFactory, acp);
        await runner.StartAsync(ct);

        var (novaId, _) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        var friendId = await RegisterLightweightAgentAsync(fixture, "friend", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomP = await chat.CreateRoomForAsync([novaId, friendId], ct);
        var roomQ = await chat.CreateRoomForAsync([novaId, friendId], ct);

        novaFactory.Host!.OnOpen = session => session.EnqueueReplyWithUsage([1000], "p1");
        await chat.PostAsync(roomP.Id, friendId, "@nova p1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomP.Id, 2, ct);

        novaFactory.Host.OnOpen = session => session.EnqueueReplyWithUsage([5000], "q1");
        await chat.PostAsync(roomQ.Id, friendId, "@nova q1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomQ.Id, 2, ct);

        // Room P's own session (Sessions[1]) reports a LOWER Used than its own last rise: a
        // compaction, not a spend.
        novaFactory.Host.Sessions[1].EnqueueReplyWithUsage([800], "p2");
        await chat.PostAsync(roomP.Id, friendId, "@nova p2", ct: ct);
        await Eventually.HistoryCountAsync(store, roomP.Id, 4, ct);

        novaFactory.Host.Sessions[1].EnqueueReply("p3");
        await chat.PostAsync(roomP.Id, friendId, "@nova p3", ct: ct);

        await Eventually.UntilAsync(() => novaFactory.Host.Sessions[1].Prompts.Count == 3, ct, timeout: TestBudgets.Standard);
    }

    /// <summary>A spent Persona-wide Budget blocks a Turn in a Room whose own session has not even opened yet.</summary>
    [Fact]
    public async Task BudgetSpent_BlocksTurnsInEveryRoom()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var acp = new AcpOptions { TokenBudget = 1000 };
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };
        novaFactory.Session.EnqueueReply("hello!");

        await using PersonaRunner runner = CreateRunner(fixture, Nova, novaFactory, acp);
        await runner.StartAsync(ct);

        var (novaId, _) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        var friendId = await RegisterLightweightAgentAsync(fixture, "friend", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomP = await chat.CreateRoomForAsync([novaId, friendId], ct);
        var roomQ = await chat.CreateRoomForAsync([novaId, friendId], ct);

        novaFactory.Host!.OnOpen = session => session.EnqueueReplyWithUsage([1200], "spends it all");
        await chat.PostAsync(roomP.Id, friendId, "@nova p1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomP.Id, 2, ct);

        // Room Q's session has never opened. The Budget check (D22 correction 11) runs before the
        // open, so this Mention must never open one at all - only Nova's own direct Room's session
        // (opened at start, RS §6.2's exception) and Room P's own session ever exist.
        await chat.PostAsync(roomQ.Id, friendId, "@nova please open", ct: ct);
        await Task.Delay(300, ct);

        Assert.Equal(2, novaFactory.Host!.Sessions.Count);
    }

    /// <summary>A Human Message in ANY Room resets the Persona-wide Budget, unblocking every Room's Turns again.</summary>
    [Fact]
    public async Task HumanMessageInAnyRoom_ResetsBudget()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var acp = new AcpOptions { TokenBudget = 1000 };
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };

        // "nova" is not the built-in Chief of Staff, so this runner queues no Greeting (Spec §6.14
        // gates it on that marker): session[0]'s own first Turn is "reset it" below, not a Greeting.
        novaFactory.Session.EnqueueReply("hello!");

        await using PersonaRunner runner = CreateRunner(fixture, Nova, novaFactory, acp);
        await runner.StartAsync(ct);

        var (novaId, roomXId) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        var friendId = await RegisterLightweightAgentAsync(fixture, "friend", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomP = await chat.CreateRoomForAsync([novaId, friendId], ct);
        var roomQ = await chat.CreateRoomForAsync([novaId, friendId], ct);

        novaFactory.Host!.OnOpen = session => session.EnqueueReplyWithUsage([1200], "spends it all");
        await chat.PostAsync(roomP.Id, friendId, "@nova p1", ct: ct);
        await Eventually.HistoryCountAsync(store, roomP.Id, 2, ct);

        // Confirms the Budget really is spent first: Room Q's first Mention never opens a session.
        await chat.PostAsync(roomQ.Id, friendId, "@nova please open", ct: ct);
        await Task.Delay(300, ct);
        Assert.Equal(2, novaFactory.Host!.Sessions.Count);

        // A Human Message, in Nova's OWN direct Room this time - resets the Persona-wide counter.
        // This is the one Turn in this file deliberately triggered by the Human: the Human's own
        // Message, then Nova's reply, is roomX's first-ever history, so the count is 2, not 3.
        await chat.PostAsync(roomXId, KnownIds.Human, "reset it", ct: ct);
        await Eventually.HistoryCountAsync(store, roomXId, 2, ct);

        novaFactory.Host.OnOpen = session => session.EnqueueReply("finally open");
        await chat.PostAsync(roomQ.Id, friendId, "@nova try again", ct: ct);

        await Eventually.UntilAsync(() => novaFactory.Host.Sessions.Count == 3 && novaFactory.Host.Sessions[2].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
    }

    /// <summary>Builds a bare <see cref="PersonaRunner"/> with no pipe connection at all, for a test that only calls its <see cref="IRoomSessionOwner"/> members directly.</summary>
    /// <summary>
    /// An Adapter that has disconnected offers nothing: its list goes when the runner reports Offline, even
    /// though the runner itself has not stopped, so the card and the command routing stop offering
    /// commands that cannot run.
    /// </summary>
    [Fact]
    public void ReportOffline_ForgetsTheOfferedCommands()
    {
        PersonaCommands commands = new(NullLogger<PersonaCommands>.Instance);
        commands.Set("nova", [new AdapterCommand("compact", "Free up context", null)]);
        PersonaRunner runner = new(
            TestPersonas.Nova,
            TestOptions.Create(),
            new FakeAgentHostFactory(),
            new FakePromptSource(),
            new RoomFollows(),
            NullLogger<PersonaRunner>.Instance,
            commands: commands);
        IRoomSessionOwner owner = runner;

        owner.ReportOffline("The Adapter process disconnected.");

        Assert.Empty(commands.Get("nova"));
    }

    private static PersonaRunner CreateBareRunner() =>
        new(
            TestPersonas.Nova,
            TestOptions.Create(),
            new FakeAgentHostFactory(),
            new FakePromptSource(),
            new RoomFollows(),
            NullLogger<PersonaRunner>.Instance);

    /// <summary>Builds a per-Room <see cref="RoomSession"/> with a real <paramref name="store"/> and a resume-capable <paramref name="host"/> - the same shape <c>RoomSessionResumeTests</c> uses.</summary>
    private static RoomSessionHarness CreateRoomSession(
        FakeRoomSessionOwner owner, FakePersonaHost host, RoomSessionStore store)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: host.OpenAsync,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova,
            roomSessions: store,
            host: host);
        return RoomSessionHarness.Adopt(session, runCts);
    }

    private static AdapterProfile ClaudeProfile() => new(
        Id: "claude",
        DisplayName: "Claude",
        Description: null,
        Command: "claude-agent-acp",
        Args: null,
        AdapterPath: null,
        UsesToolNamePrefix: true,
        EnvironmentOverrides: null,
        ReadsFiles: true,
        IsolateUserSettings: true,
        SessionPerRoom: true);

    private static PersonaRunner CreateRunner(PipeHostFixture fixture, Persona persona, FakeAgentHostFactory factory, AcpOptions acp)
    {
        var options = TestOptions.Create(opts =>
        {
            opts.PipeName = fixture.PipeName;
            opts.Acp = acp;
        });
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();
        return new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger);
    }


    /// <summary>Registers <paramref name="agentName"/> as an Agent over a bare Hello/Welcome handshake, with no <see cref="PersonaRunner"/> behind it - only to exist and be a Member of a group Room.</summary>
    private static async Task<string> RegisterLightweightAgentAsync(PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello(agentName, null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        return welcome.AgentId;
    }


}
