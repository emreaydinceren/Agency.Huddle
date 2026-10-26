using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what a <see cref="RoomSession"/> feeds the <see cref="ContextFill"/> store from its usage
/// updates: the latest gauge reading under its (Agent, Room) key, never a sum, and never a change to
/// the token Budget.
/// </summary>
public sealed class RoomSessionContextFillTests
{
    private const string AgentId = "agent-1";
    private const long WindowSize = 200_000;

    private static readonly Persona Nova = new("nova", "You are Nova.");

    private static readonly WorkItem CompactItem = new(
        "room-1",
        "Room 1",
        "You",
        "@nova /compact",
        [],
        WorkItemKind.Command,
        TriggerMessageId: "trig-1",
        Command: new AdapterCommandCall("compact", string.Empty));

    /// <summary>The latest usage update is the reading, not the first and not a sum: [100, 250] reads 250.</summary>
    [Fact]
    public async Task Usage_ReportsTheLatestValueUnderTheRoomKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([100, 250], "done");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { AgentId = AgentId, ContextFill = fill });
        harness.Room.Enqueue(new QueuedWork(1, Item("hello")));

        await WaitUntilFinishedAsync(owner, ct);

        ContextFillReading? reading = fill.Get(AgentId, "room-1");
        Assert.NotNull(reading);
        Assert.Equal(250, reading.Used);
        Assert.Equal(WindowSize, reading.Size);
        Assert.False(reading.Shared);
    }

    /// <summary>After a compaction the reading falls to the new level: the meter shows the gauge, 44,100, and not a sum.</summary>
    [Fact]
    public async Task Usage_AfterACompaction_TheReadingFalls()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([50_624], "first");
        session.EnqueueToolActivity();
        session.EnqueueToolActivity(
        [
            new ToolCallStarted("s", "call-1", "Compact conversation", ToolKind.Think, ToolCallStatus.InProgress, null),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Completed, null),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Completed, """{"trigger":"manual","preTokens":50624,"postTokens":2964,"durationMs":9453}"""),
        ]);
        session.EnqueueReplyWithUsage([2_964]);
        session.EnqueueReplyWithUsage([43_908, 44_100], "third");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova, AgentId = AgentId, ContextFill = fill });
        RoomSession room = harness.Room;
        room.Enqueue(new QueuedWork(1, Item("hello")));
        await WaitUntilPostedAsync(owner, "first", ct);
        Assert.Equal(50_624, fill.Get(AgentId, "room-1")?.Used);
        room.Enqueue(new QueuedWork(2, CompactItem));
        await WaitUntilPostedAsync(owner, "Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", ct);
        await Eventually.UntilAsync(() => fill.Get(AgentId, "room-1")?.Used == 2_964, ct, timeout: TestBudgets.Standard);
        room.Enqueue(new QueuedWork(3, Item("go on")));
        await WaitUntilPostedAsync(owner, "third", ct);

        Assert.Equal(44_100, fill.Get(AgentId, "room-1")?.Used);
    }

    /// <summary>A shared session (no Room) reports under the shared key, and every Room reads it as shared.</summary>
    [Fact]
    public async Task SharedSession_ReportsUnderTheSharedKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([300], "done");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { RoomId = null, AgentId = AgentId, ContextFill = fill });
        harness.Room.Enqueue(new QueuedWork(1, Item("hello")));

        await WaitUntilFinishedAsync(owner, ct);

        ContextFillReading? reading = fill.Get(AgentId, "any-room");
        Assert.NotNull(reading);
        Assert.Equal(300, reading.Used);
        Assert.True(reading.Shared);
    }

    /// <summary>A session built without an agent id reports nothing, even with a store attached.</summary>
    [Fact]
    public async Task NoAgentId_ReportsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([300], "done");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        bool raised = false;
        fill.Changed += () => raised = true;
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { ContextFill = fill });
        harness.Room.Enqueue(new QueuedWork(1, Item("hello")));

        await WaitUntilFinishedAsync(owner, ct);

        Assert.False(raised);
        Assert.Null(fill.Get(AgentId, "room-1"));
    }

    /// <summary>A fresh open forgets a reading left by an earlier session, before any usage update arrives.</summary>
    [Fact]
    public async Task FreshOpen_ForgetsAStaleReading()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        fill.Report(AgentId, "room-1", 90_000, WindowSize);
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { AgentId = AgentId, ContextFill = fill });
        harness.Room.Enqueue(new QueuedWork(1, Item("hello")));

        await WaitUntilFinishedAsync(owner, ct);

        Assert.Null(fill.Get(AgentId, "room-1"));
    }

    /// <summary>A resumed open keeps the earlier reading while no usage has arrived yet.</summary>
    [Fact]
    public async Task ResumedOpen_KeepsTheReading()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));
        FakeAgentSession resumed = new();
        resumed.EnqueueReply("resumed reply");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        fill.Report(AgentId, "room-1", 90_000, WindowSize);
        FakePersonaHost host = ResumableHost(resumed);
        await using RoomSessionHarness harness = CreateResumingRoomSession(owner, host, store, fill);
        harness.Room.Enqueue(new QueuedWork(1, Item("go")));

        await WaitUntilFinishedAsync(owner, ct);

        Assert.Contains("stored-session-id", host.ResumeCalls);
        Assert.Equal(90_000, fill.Get(AgentId, "room-1")?.Used);
    }

    /// <summary>On a resume the first usage update is the restored context: it is reported to the meter and still not counted as spend.</summary>
    [Fact]
    public async Task ResumedOpen_ReportsTheBaselineUpdate_ButCountsNoSpendForIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));
        FakeAgentSession resumed = new();
        resumed.EnqueueReplyWithUsage([1_000], "resumed reply");
        FakeRoomSessionOwner owner = new();
        ContextFill fill = new();
        FakePersonaHost host = ResumableHost(resumed);
        await using RoomSessionHarness harness = CreateResumingRoomSession(owner, host, store, fill);
        harness.Room.Enqueue(new QueuedWork(1, Item("go")));

        await WaitUntilFinishedAsync(owner, ct);

        Assert.Equal(1_000, fill.Get(AgentId, "room-1")?.Used);
        Assert.Equal(0, owner.TokensAdded);
    }

    /// <summary>Reporting the meter leaves the token Budget alone: only the rises count, as before.</summary>
    [Fact]
    public async Task TokenBudget_IsUnchangedByTheMeter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([100, 250], "done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { AgentId = AgentId, ContextFill = new ContextFill() });
        harness.Room.Enqueue(new QueuedWork(1, Item("hello")));

        await WaitUntilFinishedAsync(owner, ct);

        Assert.Equal(250, owner.TokensAdded);
    }

    private static WorkItem Item(string text) =>
        new("room-1", "Room 1", "You", text, [], TriggerMessageId: "trig-" + text);

    private static FakePersonaHost ResumableHost(FakeAgentSession resumed)
    {
        FakePersonaHost host = new(new FakeAgentSession(), ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-session-id", StringComparison.Ordinal) ? resumed : null;
        return host;
    }

    private static RoomSessionHarness CreateResumingRoomSession(FakeRoomSessionOwner owner, FakePersonaHost host, RoomSessionStore store, ContextFill fill)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: host.OpenAsync,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: new Persona("nova", "You are Nova.", Model: "m1", Effort: "e1"),
            roomSessions: store,
            host: host,
            agentId: AgentId,
            contextFill: fill);
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

    /// <summary>Waits until the Turn has written its final Draft terminator, which every Turn path ends with.</summary>
    private static Task WaitUntilFinishedAsync(FakeRoomSessionOwner owner, CancellationToken ct) =>
        Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

    private static Task WaitUntilPostedAsync(FakeRoomSessionOwner owner, string text, CancellationToken ct) =>
        Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(post => post.Text == text), ct, timeout: TestBudgets.Standard);
}
