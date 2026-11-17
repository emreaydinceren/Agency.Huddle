using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what a <see cref="RoomSession"/> tells the server about each Turn it owns (Turn Queue spec, section 5.5): a
/// <c>Started</c> ack just before the prompt, a <c>Finished</c> ack for every Turn that ends with nothing posted, no ack at
/// all when its run token is cancelled, and <c>InReplyTo</c> on a reply instead of any <c>Finished</c>.
/// </summary>
public sealed class RoomSessionQueueAckTests
{
    private const string RoomId = "room-1";
    private const string Trigger = "trig-1";

    private static readonly Persona Nova = new("nova", "You are Nova.");

    private static WorkItem MessageItem() =>
        new(RoomId, "Room 1", "You", "hello", [], TriggerMessageId: Trigger);

    private static IReadOnlyList<QueueAck> Acks(FakeRoomSessionOwner owner) => [.. owner.Written.OfType<QueueAck>()];

    private static Task WaitForAckAsync(FakeRoomSessionOwner owner, QueueAckKind kind, CancellationToken ct) =>
        Eventually.UntilAsync(() => Acks(owner).Any(ack => ack.Kind == kind), ct, timeout: TestBudgets.Standard);

    private static Task WaitForFinalDeltaAsync(FakeRoomSessionOwner owner, CancellationToken ct) =>
        Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

    private static void AssertFinished(QueueAck ack, QueueOutcome outcome)
    {
        Assert.Equal(RoomId, ack.RoomId);
        Assert.Equal([Trigger], ack.MessageIds);
        Assert.Equal(QueueAckKind.Finished, ack.Kind);
        Assert.Equal(outcome, ack.Outcome);
        Assert.Null(ack.ReplyMessageId);
    }

    /// <summary>A Turn that posts its reply sends Started with the reply id before the prompt, then the reply naming its trigger, and no Finished.</summary>
    [Fact]
    public async Task Reply_SendsStartedThenReplyWithInReplyTo_AndNoFinished()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForFinalDeltaAsync(owner, ct);

        QueueAck started = Assert.Single(Acks(owner));
        PostMessage reply = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal(QueueAckKind.Started, started.Kind);
        Assert.Equal([Trigger], started.MessageIds);
        Assert.Equal(RoomId, started.RoomId);
        Assert.Equal(reply.MessageId, started.ReplyMessageId);
        Assert.Equal([Trigger], reply.InReplyTo);
        Assert.True(owner.Written.ToList().IndexOf(started) < owner.Written.ToList().IndexOf(reply));
    }

    /// <summary>An ack write that throws <see cref="InvalidOperationException"/> never fails the Turn: the reply still posts.</summary>
    [Fact]
    public async Task AckWriteFault_InvalidOperation_DoesNotFailTheTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        FakeRoomSessionOwner owner = new() { WriteFault = message => message is QueueAck ? new InvalidOperationException("pipe not connected") : null };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForFinalDeltaAsync(owner, ct);

        Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportLoopEnded));
    }

    /// <summary>A Turn whose reply is only the silent token sends Finished(Silent) and posts nothing.</summary>
    [Fact]
    public async Task SilentReply_SendsFinishedSilent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply(SilentReply.Token);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        Assert.Empty(owner.Written.OfType<PostMessage>());
        AssertFinished(Acks(owner)[^1], QueueOutcome.Silent);
    }

    /// <summary>A refusal posts nothing and is reported as Finished(Silent).</summary>
    [Fact]
    public async Task Refusal_SendsFinishedSilent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyEndingIn(StopReason.Refusal, "no");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        Assert.Empty(owner.Written.OfType<PostMessage>());
        AssertFinished(Acks(owner)[^1], QueueOutcome.Silent);
    }

    /// <summary>A Turn the Human stops sends Finished(Stopped).</summary>
    [Fact]
    public async Task StoppedTurn_SendsFinishedStopped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "never");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        Assert.Empty(owner.Written.OfType<PostMessage>());
        AssertFinished(Acks(owner)[^1], QueueOutcome.Stopped);
    }

    /// <summary>A Turn the idle-timeout watchdog abandons sends Finished(Failed).</summary>
    [Fact]
    public async Task TimedOutTurn_SendsFinishedFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeAgentSession session = new(time: clock);
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(500), "never");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            RoomSessionHarness.FixedOpen(session),
            new() { Persona = Nova, Acp = new AcpOptions { TurnIdleTimeoutSeconds = 5 }, Time = clock });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        await Eventually.UntilAsync(
            () =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                return Acks(owner).Any(ack => ack.Kind == QueueAckKind.Finished);
            },
            ct,
            timeout: TestBudgets.Standard);

        AssertFinished(Acks(owner)[^1], QueueOutcome.Failed);
    }

    /// <summary>A Turn whose prompt fails sends Finished(Failed).</summary>
    [Fact]
    public async Task FailedPrompt_SendsFinishedFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom"));
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        Assert.Equal(QueueAckKind.Started, Acks(owner)[0].Kind);
        AssertFinished(Acks(owner)[^1], QueueOutcome.Failed);
    }

    /// <summary>An item behind a Stop mark is drained unrun and reported as Finished(Dropped), with no Started.</summary>
    [Fact]
    public async Task StopMark_SendsFinishedDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        await harness.Room.StopAsync(RoomId, 5, ct);
        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        QueueAck ack = Assert.Single(Acks(owner));
        AssertFinished(ack, QueueOutcome.Dropped);
        Assert.Empty(session.Prompts);
    }

    /// <summary>An item drained for a spent token Budget is reported as Finished(Dropped).</summary>
    [Fact]
    public async Task SpentTokenBudget_SendsFinishedDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new() { TokenBudgetSpent = true };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        AssertFinished(Assert.Single(Acks(owner)), QueueOutcome.Dropped);
        Assert.Empty(session.Prompts);
    }

    /// <summary>A Stop that lands while the first-Turn Transcript read is pending drops the item at publish time as Finished(Dropped).</summary>
    [Fact]
    public async Task StopDuringTranscriptRead_SendsFinishedDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = async (roomId, _, _, _, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return new TranscriptTail("fake", roomId, [], 0);
            },
        };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
            await entered.Task.WaitAsync(TestBudgets.Standard, ct);
            await harness.Room.StopAsync(RoomId, 1, ct);
        }
        finally
        {
            release.TrySetResult();
        }

        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        AssertFinished(Assert.Single(Acks(owner)), QueueOutcome.Dropped);
        Assert.Empty(session.Prompts);
    }

    /// <summary>A lazy open that fails is reported as Finished(Failed), with no Started.</summary>
    [Fact]
    public async Task FailedLazyOpen_SendsFinishedFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            _ => throw new InvalidOperationException("cannot open"),
            new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Finished, ct);

        AssertFinished(Assert.Single(Acks(owner)), QueueOutcome.Failed);
    }

    /// <summary>When the run token is cancelled mid-Turn the Turn sends no Finished: the server decides what becomes of its row.</summary>
    [Fact]
    public async Task CancelledRunToken_SendsNoFinished()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "never");
        FakeRoomSessionOwner owner = new();
        RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem()));
        await WaitForAckAsync(owner, QueueAckKind.Started, ct);
        await harness.DisposeAsync();

        QueueAck started = Assert.Single(Acks(owner));
        Assert.Equal(QueueAckKind.Started, started.Kind);
    }

    /// <summary>A Greeting has no trigger Message, so it sends no ack and its reply names nothing.</summary>
    [Fact]
    public async Task Greeting_SendsNoAck_AndItsReplyHasNoInReplyTo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("welcome");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, new WorkItem(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting)));
        await WaitForFinalDeltaAsync(owner, ct);

        Assert.Empty(Acks(owner));
        Assert.Null(Assert.Single(owner.Written.OfType<PostMessage>()).InReplyTo);
    }

    /// <summary>A Command Turn sends Started like any Turn, and its outcome line is a reply that names the trigger.</summary>
    [Fact]
    public async Task CommandTurn_SendsStarted_AndOutcomeLineHasInReplyTo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });
        WorkItem item = new(
            RoomId, "Room 1", "You", "@nova /compact", [], WorkItemKind.Command,
            TriggerMessageId: Trigger,
            Command: new AdapterCommandCall("compact", string.Empty));

        harness.Room.Enqueue(new QueuedWork(1, item));
        await WaitForFinalDeltaAsync(owner, ct);

        QueueAck started = Assert.Single(Acks(owner));
        PostMessage outcome = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal(QueueAckKind.Started, started.Kind);
        Assert.Equal([Trigger], started.MessageIds);
        Assert.Equal(outcome.MessageId, started.ReplyMessageId);
        Assert.Equal([Trigger], outcome.InReplyTo);
    }
}
