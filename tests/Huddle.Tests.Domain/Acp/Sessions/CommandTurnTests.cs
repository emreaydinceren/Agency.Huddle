using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what a <see cref="RoomSession"/> does with an Adapter command Turn (Commands spec, sections 6.6
/// and 6.7): it posts one outcome Message because <c>/compact</c> sends no text of its own, posts nothing
/// for a Stop, and stops the context falling after a compaction from being counted as spend.
/// </summary>
public sealed class CommandTurnTests
{
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

    /// <summary>The tool-call events <c>claude-agent-acp</c> was observed to send for a bare <c>/compact</c>.</summary>
    private static AgentEvent[] ObservedCompactEvents() =>
    [
        new ToolCallStarted("s", "call-1", "Compact conversation", ToolKind.Think, ToolCallStatus.InProgress, null),
        new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Completed, null),
        new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Completed, """{"trigger":"manual","preTokens":50624,"postTokens":2964,"durationMs":9453}"""),
    ];

    /// <summary>The observed wire events produce exactly one Message, worded from the figures, posted into the command's Room.</summary>
    [Fact]
    public async Task ObservedCompactEvents_PostOneOutcomeMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(ObservedCompactEvents());
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
            Assert.Equal("Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", posted.Text);
            Assert.Equal("room-1", posted.RoomId);
            Assert.Equal("/compact", Assert.Single(session.Prompts));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A command Turn with no tool output still says it ran, so a Room can tell silence from a finished command.</summary>
    [Fact]
    public async Task NoFigures_PostsThePlainLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("Ran /compact.", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Malformed tool output degrades to the plain line, never to an error or a silent Room.</summary>
    [Fact]
    public async Task MalformedOutput_PostsThePlainLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(
            new ToolCallStarted("s", "call-1", "Compact conversation", ToolKind.Think, ToolCallStatus.InProgress, null),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Completed, """{"preTokens":"lots"}"""));
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("Ran /compact.", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A failed tool call says the command did not finish, even if figures also arrived.</summary>
    [Fact]
    public async Task FailedToolCall_PostsDidNotFinish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(
            new ToolCallStarted("s", "call-1", "Compact conversation", ToolKind.Think, ToolCallStatus.InProgress, null),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Think, ToolCallStatus.Failed, null));
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("/compact did not finish.", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// An update for a tool call this Turn never started belongs to an earlier, aborted Turn, whose tail can
    /// land after a Stop. It must not turn a finished compaction into "did not finish".
    /// </summary>
    [Fact]
    public async Task LateUpdateFromAnotherTurnsToolCall_IsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(
        [
            new ToolCallUpdated("s", "stale-call", null, ToolKind.Execute, ToolCallStatus.Failed, """{"preTokens":1,"postTokens":1}"""),
            .. ObservedCompactEvents(),
        ]);
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop or a refusal posts nothing, exactly as for any other Turn.</summary>
    [Theory]
    [InlineData(StopReason.Cancelled)]
    [InlineData(StopReason.Refusal)]
    public async Task StoppedOrRefused_PostsNothing(StopReason reason)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(ObservedCompactEvents());
        session.EnqueueReplyEndingIn(reason);
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Empty(owner.Written.OfType<PostMessage>());
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>When the Adapter's command does reply with text, that text is the Message: the fixed line is only for a command that says nothing.</summary>
    [Fact]
    public async Task CommandThatRepliesWithText_PostsTheAdaptersTextNotTheLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(ObservedCompactEvents());
        session.EnqueueReply("Context is now 2,964 tokens.");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, CompactItem));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("Context is now 2,964 tokens.", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>An ordinary Turn is never given an outcome line, whatever shape its tool output has.</summary>
    [Fact]
    public async Task OrdinaryTurn_PostsOnlyItsOwnReply()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(ObservedCompactEvents());
        session.EnqueueReply("here you go");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "You", "hello", [], TriggerMessageId: "trig-1")));

            await WaitUntilFinalDeltaAsync(owner, ct);

            Assert.Equal("here you go", Assert.Single(owner.Written.OfType<PostMessage>()).Text);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// After a compaction the context falls and then re-fills with the session's fixed overhead. The
    /// re-fill is not a cost of the next Turn, so only the rise after it is counted: 50,624 for the first
    /// Turn, nothing for the fall, nothing for the re-fill to 43,908, and 192 for the growth after it.
    /// </summary>
    [Fact]
    public async Task UsageAfterACommandTurn_TheRefillIsTheBaselineNotSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([50_624], "first");
        session.EnqueueToolActivity();
        session.EnqueueToolActivity(ObservedCompactEvents());
        session.EnqueueReplyWithUsage([2_964]);
        session.EnqueueReplyWithUsage([43_908, 44_100], "third");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "You", "hello", [], TriggerMessageId: "trig-1")));
            await WaitUntilPostedAsync(owner, "first", ct);
            room.Enqueue(new QueuedWork(2, CompactItem));
            await WaitUntilPostedAsync(owner, "Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", ct);
            room.Enqueue(new QueuedWork(3, new WorkItem("room-1", "Room 1", "You", "go on", [], TriggerMessageId: "trig-3")));
            await WaitUntilPostedAsync(owner, "third", ct);

            Assert.Equal(50_624 + 192, owner.TokensAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The control for the test above: the same usage levels without a command Turn count the whole re-fill.</summary>
    [Fact]
    public async Task UsageWithoutACommandTurn_CountsTheWholeRise()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([50_624], "first");
        session.EnqueueReplyWithUsage([2_964], "second");
        session.EnqueueReplyWithUsage([43_908, 44_100], "third");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "You", "hello", [], TriggerMessageId: "trig-1")));
            await WaitUntilPostedAsync(owner, "first", ct);
            room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "You", "again", [], TriggerMessageId: "trig-2")));
            await WaitUntilPostedAsync(owner, "second", ct);
            room.Enqueue(new QueuedWork(3, new WorkItem("room-1", "Room 1", "You", "go on", [], TriggerMessageId: "trig-3")));
            await WaitUntilPostedAsync(owner, "third", ct);

            Assert.Equal(50_624 + 40_944 + 192, owner.TokensAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop during a command Turn leaves the baseline alone: only a completed command resets it.</summary>
    [Fact]
    public async Task UsageAfterACancelledCommandTurn_IsCountedAsBefore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([50_624], "first");
        session.EnqueueReplyEndingIn(StopReason.Cancelled);
        session.EnqueueReplyWithUsage([60_000], "third");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "You", "hello", [], TriggerMessageId: "trig-1")));
            await WaitUntilPostedAsync(owner, "first", ct);
            room.Enqueue(new QueuedWork(2, CompactItem));
            room.Enqueue(new QueuedWork(3, new WorkItem("room-1", "Room 1", "You", "go on", [], TriggerMessageId: "trig-3")));
            await WaitUntilPostedAsync(owner, "third", ct);

            Assert.Equal(60_000, owner.TokensAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    private static (RoomSession Session, CancellationTokenSource RunCts) CreateSession(
        FakeRoomSessionOwner owner, Func<CancellationToken, Task<IAgentSession>> open)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: open,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova);
        return (session, runCts);
    }

    private static Func<CancellationToken, Task<IAgentSession>> FixedOpen(FakeAgentSession session) =>
        _ => Task.FromResult<IAgentSession>(session);

    private static async Task DisposeSessionAsync(RoomSession session, CancellationTokenSource runCts)
    {
        await runCts.CancelAsync();
        await session.DisposeAsync();
        runCts.Dispose();
    }

    /// <summary>Waits until the Turn has written its final Draft terminator, which every Turn path ends with.</summary>
    private static Task WaitUntilFinalDeltaAsync(FakeRoomSessionOwner owner, CancellationToken ct) =>
        WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

    private static Task WaitUntilPostedAsync(FakeRoomSessionOwner owner, string text, CancellationToken ct) =>
        WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(post => post.Text == text), ct);

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }
}
