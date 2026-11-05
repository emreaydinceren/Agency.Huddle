using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins LR-R7, LR-R9 and LR-R11 inside <see cref="RoomSession"/>: the first Turn asks for the Message cap
/// from <see cref="ISessionSettings"/>, trims the tail to the token budget, and a changed value applies
/// from the next fresh open without a restart (Spec 5.1).
/// </summary>
public sealed class CatchUpBudgetTests
{
    private const string AgentId = "nova-id";

    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>The first Turn's Transcript read asks for as many Messages as the settings' <c>CatchUpMessages</c>, not the <see cref="AcpOptions"/> cap.</summary>
    [Fact]
    public async Task FirstTurn_MaxEqualsSettingsCatchUpMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        FakeSessionSettings settings = new(new SessionSettings(13, 100_000));
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova, SessionSettings = settings });
        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-1")));

        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        Assert.Equal(13, Assert.Single(owner.TranscriptReadCalls).Max);
    }

    /// <summary>
    /// A tail costing more than the token budget is trimmed: five Messages of 10 tokens each against a budget of 30 and one
    /// protected own Message drop the own m2 (pass 1) and the oldest m1 (pass 2), so the prompt keeps m3, m4 and m5, the omitted
    /// line counts the tail's 7 plus the 2 trimmed, and the own line counts the 1 own Message dropped.
    /// </summary>
    [Fact]
    public async Task FirstTurn_TailOverBudget_KeepsNewestAndCountsTrimmed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail(
                "req",
                roomId,
                [
                    Message("m1", "hu", "Human", "human message number 01"),
                    Message("m2", AgentId, "Nova", "nova message number 0002"),
                    Message("m3", AgentId, "Nova", "nova message number 0003"),
                    Message("m4", "hu", "Human", "human message number 04"),
                    Message("m5", "hu", "Human", "human message number 05"),
                ],
                Omitted: 7));
        FakeSessionSettings settings = new(new SessionSettings(20, 30));
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            RoomSessionHarness.FixedOpen(session),
            new() { Persona = Nova, AgentId = AgentId, SessionSettings = settings, Acp = new AcpOptions { CatchUpKeepOwnMessages = 1 } });
        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-1")));

        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        string[] lines = Assert.Single(session.Prompts).Split('\n');
        Assert.Contains("…9 earlier Messages are not shown.", lines);
        Assert.Contains("…1 of those are your own.", lines);
        Assert.Contains("Nova: nova message number 0003", lines);
        Assert.Contains("Human: human message number 04", lines);
        Assert.Contains("Human: human message number 05", lines);
        Assert.DoesNotContain("Human: human message number 01", lines);
        Assert.DoesNotContain("Nova: nova message number 0002", lines);
    }

    /// <summary>The trim applies to a resumed first Turn too: the resumed header is used and the oldest Message over the budget is counted as omitted.</summary>
    [Fact]
    public async Task ResumedFirstTurn_TailOverBudget_IsTrimmed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));
        FakeAgentSession resumedSession = new();
        resumedSession.EnqueueReply("ok");
        FakePersonaHost host = new(new FakeAgentSession(), ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-session-id", StringComparison.Ordinal) ? resumedSession : null;
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail(
                "req",
                roomId,
                [
                    Message("m1", "hu", "Human", "human message number 01"),
                    Message("m2", "hu", "Human", "human message number 02"),
                    Message("m3", "hu", "Human", "human message number 03"),
                ],
                Omitted: 0));
        FakeSessionSettings settings = new(new SessionSettings(20, 20));
        await using RoomSessionHarness harness = CreateRoomSession(owner, host, store, settings);
        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-1")));

        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        string prompt = Assert.Single(resumedSession.Prompts);
        string[] lines = prompt.Split('\n');
        Assert.Contains("While this session was closed, these Messages were posted here:", prompt, StringComparison.Ordinal);
        Assert.Contains("…1 earlier Messages are not shown.", lines);
        Assert.DoesNotContain("Human: human message number 01", lines);
        Assert.Contains("Human: human message number 02", lines);
        Assert.Contains("Human: human message number 03", lines);
    }

    /// <summary>
    /// Changing the settings between two fresh opens changes the second open's <c>max</c>, and the change forgets nothing:
    /// a stored entry for another Room is still there afterwards (LR-R10).
    /// </summary>
    [Fact]
    public async Task ChangedSettings_AppliesToNextFreshOpen_AndForgetsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-2", new RoomSessionEntry("other-room-session", "claude", "m1", "e1", "msg-xyz", DateTimeOffset.UtcNow));
        FakeSessionSettings settings = new(new SessionSettings(11, 100_000));
        FakePersonaHost host = new(new FakeAgentSession(), ClaudeProfile());
        FakeRoomSessionOwner owner = new();
        Func<CancellationToken, Task<IAgentSession>> open = static _ =>
        {
            FakeAgentSession created = new();
            created.EnqueueReply("ok");
            return Task.FromResult<IAgentSession>(created);
        };
        await using RoomSessionHarness harness = CreateRoomSession(owner, host, store, settings, open);
        RoomSession room = harness.Room;
        room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "one", [], TriggerMessageId: "trig-1")));
        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 1, ct, timeout: TestBudgets.Standard);
        await Eventually.UntilAsync(() => room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);
        await room.CloseAsync();

        settings.Current = new SessionSettings(29, 100_000);
        room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "two", [], TriggerMessageId: "trig-2")));
        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, owner.TranscriptReadCalls.Count);
        Assert.Equal(11, owner.TranscriptReadCalls[0].Max);
        Assert.Equal(29, owner.TranscriptReadCalls[1].Max);
        Assert.NotNull(store.Get("nova", "room-2"));
    }

    /// <summary>With no <see cref="ISessionSettings"/> the first Turn's <c>max</c> is <see cref="AcpOptions.TranscriptCatchUpMessages"/> (today's behaviour).</summary>
    [Fact]
    public async Task FirstTurn_NoSettings_MaxIsAcpOptionsCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova, Acp = new AcpOptions { TranscriptCatchUpMessages = 37 } });
        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-1")));

        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct, timeout: TestBudgets.Standard);

        Assert.Equal(37, Assert.Single(owner.TranscriptReadCalls).Max);
    }

    /// <summary>Builds one Transcript Message.</summary>
    private static ChatMessage Message(string id, string senderId, string senderName, string text) =>
        new(id, DateTimeOffset.UtcNow, senderId, senderName, text);

    /// <summary>Builds a per-Room <see cref="RoomSession"/> with a real <paramref name="store"/> and the given <paramref name="settings"/>.</summary>
    private static RoomSessionHarness CreateRoomSession(
        FakeRoomSessionOwner owner,
        FakePersonaHost host,
        RoomSessionStore store,
        FakeSessionSettings settings,
        Func<CancellationToken, Task<IAgentSession>>? open = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: open ?? host.OpenAsync,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova,
            roomSessions: store,
            host: host,
            sessionSettings: settings);
        return RoomSessionHarness.Adopt(session, runCts);
    }

    /// <summary>A Claude-like Adapter Profile that supports a session per Room.</summary>
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
}
