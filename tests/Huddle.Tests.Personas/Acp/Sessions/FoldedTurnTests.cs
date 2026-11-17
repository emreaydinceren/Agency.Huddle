using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins how a <see cref="RoomSession"/> folds the Messages queued behind its head into one Turn (Turn Queue spec, section 6):
/// which items fold, where the run ends, what the consumer's count does afterwards, and what the acks and the stored last
/// Message say about a Folded Turn.
/// </summary>
public sealed class FoldedTurnTests
{
    private const string RoomId = "room-1";

    private const string Label = "[Room: Room 1 (id: room-1)] ";

    private static readonly Persona Nova = new("nova", "You are Nova.");

    private static WorkItem Msg(string id, string text, bool foldable = true, string sender = "Human", string roomId = RoomId) =>
        new(roomId, "Room 1", sender, text, [], TriggerMessageId: id, Foldable: foldable);

    private static WorkItem Cmd(string id) =>
        new(RoomId, "Room 1", "Human", "@nova /compact", [], WorkItemKind.Command, TriggerMessageId: id, Foldable: true, Command: new AdapterCommandCall("compact", string.Empty));

    private static string Fold(params (string Sender, string Text)[] lines) =>
        Label + "Since your last reply the Human sent " + lines.Length + " Messages. Answer them together, once:"
        + string.Concat(lines.Select(line => "\n" + line.Sender + ": " + line.Text));

    private static Task WaitForPromptsAsync(FakeAgentSession session, int count, CancellationToken ct) =>
        Eventually.UntilAsync(() => session.Prompts.Count >= count, ct, timeout: TestBudgets.Standard);

    private static IReadOnlyList<QueueAck> Acks(FakeRoomSessionOwner owner) => [.. owner.Written.OfType<QueueAck>()];

    /// <summary>Runs a gated first Turn, queues <paramref name="behind"/> behind it, releases it, and waits for <paramref name="prompts"/> prompts.</summary>
    private static async Task<(FakeAgentSession Session, FakeRoomSessionOwner Owner)> RunBehindAsync(
        IReadOnlyList<WorkItem> behind, int prompts, CancellationToken ct, AcpOptions? acp = null)
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueGatedReply(release.Task, "first");
        for (int index = 1; index < prompts; index++)
        {
            session.EnqueueReply("again");
        }

        FakeRoomSessionOwner owner = new();
        RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova, Acp = acp });
        try
        {
            long sequence = 1;
            harness.Room.Enqueue(new QueuedWork(sequence++, Msg("t0", "zero")));
            await WaitForPromptsAsync(session, 1, ct);
            foreach (WorkItem item in behind)
            {
                harness.Room.Enqueue(new QueuedWork(sequence++, item));
            }

            release.TrySetResult();
            await WaitForPromptsAsync(session, prompts, ct);
            await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Count() >= prompts, ct, timeout: TestBudgets.Standard);
        }
        finally
        {
            release.TrySetResult();
            await harness.DisposeAsync();
        }

        return (session, owner);
    }

    /// <summary>One-to-one Messages queued behind a running Turn are answered as one Turn (U6), with the Started ack and the reply naming every id.</summary>
    [Fact]
    public async Task Fold_ThreeBehindARunningTurn_IsOneTurnNamingEveryId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, FakeRoomSessionOwner owner) = await RunBehindAsync([Msg("t1", "a"), Msg("t2", "b"), Msg("t3", "c")], 2, ct);

        Assert.Equal(2, session.Prompts.Count);
        Assert.Equal(Fold(("Human", "a"), ("Human", "b"), ("Human", "c")), session.Prompts[1]);
        QueueAck started = Acks(owner).Last(ack => ack.Kind == QueueAckKind.Started);
        Assert.Equal(["t1", "t2", "t3"], started.MessageIds);
        PostMessage reply = owner.Written.OfType<PostMessage>().Last();
        Assert.Equal(["t1", "t2", "t3"], reply.InReplyTo);
    }

    /// <summary>A group Room's items are not Foldable, so each is its own Turn (U7).</summary>
    [Fact]
    public async Task GroupRoom_NeverFolds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "a", false), Msg("t2", "b", false), Msg("t3", "c", false)], 4, ct);

        Assert.Equal([Label + "Human: zero", Label + "Human: a", Label + "Human: b", Label + "Human: c"], session.Prompts);
    }

    /// <summary>A Command between two Messages ends the run: a and b fold, the Command is alone, c is alone (E-8).</summary>
    [Fact]
    public async Task Command_EndsTheRun_InOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "a"), Msg("t2", "b"), Cmd("t3"), Msg("t4", "c")], 4, ct);

        Assert.Equal(4, session.Prompts.Count);
        Assert.Equal(Fold(("Human", "a"), ("Human", "b")), session.Prompts[1]);
        Assert.Equal("/compact", session.Prompts[2]);
        Assert.Equal(Label + "Human: c", session.Prompts[3]);
    }

    /// <summary>A Stop mark that covers the items queued behind an admitted head keeps them out of its fold, and each is dropped on its own (E-7).</summary>
    [Fact]
    public async Task StopMark_CoveringItemsBehindTheHead_KeepsThemOutOfTheFold()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueReply("final");
        TaskCompletionSource opening = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            async openCt =>
            {
                opening.TrySetResult();
                await openGate.Task.WaitAsync(openCt);
                return session;
            },
            new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, Msg("t0", "zero")));
        await opening.Task.WaitAsync(ct);
        harness.Room.Enqueue(new QueuedWork(2, Msg("t1", "a")));
        harness.Room.Enqueue(new QueuedWork(3, Msg("t2", "b")));
        await harness.Room.StopAsync(RoomId, 3, ct);
        openGate.TrySetResult();
        harness.Room.Enqueue(new QueuedWork(4, Msg("t3", "final")));
        await WaitForPromptsAsync(session, 1, ct);

        Assert.Equal([Label + "Human: final"], session.Prompts);
        string[][] dropped = [.. Acks(owner).Where(ack => ack.Kind == QueueAckKind.Finished && ack.Outcome == QueueOutcome.Dropped).Select(ack => ack.MessageIds.ToArray())];
        Assert.Equal([["t0"], ["t1"], ["t2"]], dropped);
    }

    /// <summary>A cut-off re-send among the folded items makes the Folded Turn a cut-off one, so the note is kept.</summary>
    [Fact]
    public async Task Fold_ACutOffResendBehindTheHead_KeepsTheCutOffNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WorkItem head = Msg("t1", "a") with { Redelivery = Redelivery.Waiting };
        WorkItem resent = Msg("t2", "b") with { Redelivery = Redelivery.CutOff };

        (FakeAgentSession session, _) = await RunBehindAsync([head, resent], 2, ct);

        string note = PromptCatalog.Get("turn.cutOffNote").Default;
        Assert.Equal(note + "\n\n" + Fold(("Human", "a"), ("Human", "b")), session.Prompts[1]);
    }

    /// <summary>A Folded Turn whose first Message starts with a slash still opens with the Room label, never with the slash.</summary>
    [Fact]
    public async Task Fold_FirstMessageStartsWithASlash_OpensWithTheRoomLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "/status"), Msg("t2", "/compact now")], 2, ct);

        Assert.Equal(Fold(("Human", "/status"), ("Human", "/compact now")), session.Prompts[1]);
        Assert.Equal("[", session.Prompts[1][..1]);
    }

    /// <summary>The Catch-up trim never touches the folded block: with a tiny budget the Transcript shrinks and every folded line stays whole.</summary>
    [Fact]
    public async Task Fold_TinyCatchUpBudget_NeverTrimsTheFoldedBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string longA = "alpha " + new string('a', 600);
        string longB = "beta " + new string('b', 600);
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (room, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail(
                "fake",
                room,
                [
                    new ChatMessage("p1", DateTimeOffset.UnixEpoch, "x", "Friend", "oldest-line " + new string('o', 600)),
                    new ChatMessage("p2", DateTimeOffset.UnixEpoch, "x", "Friend", "newest-line " + new string('n', 600)),
                ],
                0)),
        };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova, Acp = new AcpOptions { TranscriptCatchUpTokens = 20 } });
        WorkItem folded = Msg("t1", longA) with { Folded = [new FoldedMessage("t1", "Human", longA, null), new FoldedMessage("t2", "Human", longB, null)] };

        harness.Room.Enqueue(new QueuedWork(1, folded));
        await WaitForPromptsAsync(session, 1, ct);

        string prompt = session.Prompts[0];
        Assert.DoesNotContain("oldest-line", prompt, StringComparison.Ordinal);
        string block = Fold(("Human", longA), ("Human", longB))[Label.Length..];
        Assert.Equal(block, prompt[^block.Length..]);
    }

    /// <summary>A Huddle post (not Foldable) in the middle ends the run, and Order is kept: a, post, b are three Turns.</summary>
    [Fact]
    public async Task HuddlePost_EndsTheRun()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "a"), Msg("t2", "wake", false, "Huddle"), Msg("t3", "b")], 4, ct);

        Assert.Equal([Label + "Human: zero", Label + "Human: a", Label + "Huddle: wake", Label + "Human: b"], session.Prompts);
    }

    /// <summary>The Greeting ends the run like any other item that is not an ordinary Message.</summary>
    [Fact]
    public async Task Greeting_EndsTheRun()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WorkItem greeting = new(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting, Foldable: true);

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "a"), greeting, Msg("t2", "b")], 4, ct);

        Assert.Equal(Label + "Human: a", session.Prompts[1]);
        Assert.Equal(Label + "Human: b", session.Prompts[3]);
    }

    /// <summary>In shared mode an item of another Room ends the run, though it is Foldable.</summary>
    [Fact]
    public async Task OtherRoomsItem_EndsTheRun()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        (FakeAgentSession session, _) = await RunBehindAsync([Msg("t1", "a"), Msg("t2", "x", roomId: "room-2"), Msg("t3", "b")], 4, ct);

        Assert.Equal(Label + "Human: a", session.Prompts[1]);
        Assert.Equal(Label + "Human: b", session.Prompts[3]);
    }

    /// <summary>A Message that arrives during the Folded Turn joins the next Turn, and after a fold of three a fourth Message is still served.</summary>
    [Fact]
    public async Task ArrivalDuringTheFold_JoinsTheNextTurn_AndTheConsumerStaysAlive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueGatedReply(first.Task, "one");
        session.EnqueueGatedReply(second.Task, "two");
        session.EnqueueReply("three");
        session.EnqueueReply("four");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, Msg("t0", "zero")));
        await WaitForPromptsAsync(session, 1, ct);
        harness.Room.Enqueue(new QueuedWork(2, Msg("t1", "a")));
        harness.Room.Enqueue(new QueuedWork(3, Msg("t2", "b")));
        harness.Room.Enqueue(new QueuedWork(4, Msg("t3", "c")));
        first.TrySetResult();
        await WaitForPromptsAsync(session, 2, ct);
        harness.Room.Enqueue(new QueuedWork(5, Msg("t4", "d")));
        second.TrySetResult();
        await WaitForPromptsAsync(session, 3, ct);
        harness.Room.Enqueue(new QueuedWork(6, Msg("t5", "e")));
        await WaitForPromptsAsync(session, 4, ct);

        Assert.Equal(Fold(("Human", "a"), ("Human", "b"), ("Human", "c")), session.Prompts[1]);
        Assert.Equal(Label + "Human: d", session.Prompts[2]);
        Assert.Equal(Label + "Human: e", session.Prompts[3]);
    }

    /// <summary>A Stop that clears the queue behind a running Turn drops the folded candidates, and a Message queued after it is answered alone (E-7).</summary>
    [Fact]
    public async Task StopMark_DropsTheQueuedItems_AndALaterMessageRunsAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueGatedReply(release.Task, "one");
        session.EnqueueReply("two");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, Msg("t0", "zero")));
        await WaitForPromptsAsync(session, 1, ct);
        harness.Room.Enqueue(new QueuedWork(2, Msg("t1", "a")));
        harness.Room.Enqueue(new QueuedWork(3, Msg("t2", "b")));
        await harness.Room.StopAsync(RoomId, 3, ct);
        release.TrySetResult();
        harness.Room.Enqueue(new QueuedWork(4, Msg("t3", "c")));
        await WaitForPromptsAsync(session, 2, ct);

        Assert.Equal(Label + "Human: c", session.Prompts[1]);
        Assert.Equal(2, session.Prompts.Count);
    }

    /// <summary>A lazy open that fails drops only the head: the items behind it are served next and fold together.</summary>
    [Fact]
    public async Task FailedLazyOpen_DropsOnlyTheHead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        int opens = 0;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            async openCt =>
            {
                await openGate.Task.WaitAsync(openCt);
                if (Interlocked.Increment(ref opens) == 1)
                {
                    throw new InvalidOperationException("cannot open");
                }

                return session;
            },
            new() { Persona = Nova });

        harness.Room.Enqueue(new QueuedWork(1, Msg("t1", "a")));
        harness.Room.Enqueue(new QueuedWork(2, Msg("t2", "b")));
        harness.Room.Enqueue(new QueuedWork(3, Msg("t3", "c")));
        openGate.TrySetResult();
        await WaitForPromptsAsync(session, 1, ct);

        QueueAck failed = Acks(owner).First(ack => ack.Kind == QueueAckKind.Finished);
        Assert.Equal(["t1"], failed.MessageIds);
        Assert.Equal(QueueOutcome.Failed, failed.Outcome);
        Assert.Equal(Fold(("Human", "b"), ("Human", "c")), session.Prompts[0]);
    }

    /// <summary>A Folded Turn that ends silent stores the last folded Message's id as the session's last Message.</summary>
    [Fact]
    public async Task SilentFold_StoresTheLastFoldedId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueGatedReply(release.Task, "one");
        session.EnqueueReply(SilentReply.Token);
        FakeRoomSessionOwner owner = new();
        FakePersonaHost host = new(session, new AdapterProfile(
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
            SessionPerRoom: true));
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, host.OpenAsync, new() { Persona = Nova, RoomSessions = store, Host = host });

        harness.Room.Enqueue(new QueuedWork(1, Msg("t0", "zero")));
        await WaitForPromptsAsync(session, 1, ct);
        harness.Room.Enqueue(new QueuedWork(2, Msg("t1", "a")));
        harness.Room.Enqueue(new QueuedWork(3, Msg("t2", "b")));
        release.TrySetResult();
        await Eventually.UntilAsync(() => store.Get("nova", RoomId)?.LastMessageId == "t2", ct, timeout: TestBudgets.Standard);

        Assert.Equal("t2", store.Get("nova", RoomId)?.LastMessageId);
    }

    /// <summary>The buffered Catch-up of every folded item goes into the Folded Turn's prompt, in order, ahead of the folded block.</summary>
    [Fact]
    public async Task Fold_BufferedCatchUpOfEveryItem_GoesFirstInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WorkItem second = Msg("t1", "a") with { MissedMessages = [new CaughtUpMessage("Friend", "missed-one")] };
        WorkItem third = Msg("t2", "b") with { MissedMessages = [new CaughtUpMessage("Friend", "missed-two")] };

        (FakeAgentSession session, _) = await RunBehindAsync([second, third], 2, ct);

        string prompt = session.Prompts[1];
        int one = prompt.IndexOf("missed-one", StringComparison.Ordinal);
        int two = prompt.IndexOf("missed-two", StringComparison.Ordinal);
        Assert.InRange(one, 0, int.MaxValue);
        Assert.True(two > one);
        Assert.True(prompt.IndexOf("Human: a", StringComparison.Ordinal) > two);
    }

    /// <summary>The first Turn's Transcript read ends before the first folded Message, so Messages two to n appear once.</summary>
    [Fact]
    public async Task TranscriptRead_EndsBeforeTheFirstFoldedMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> before = [];
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (room, _, beforeId, _, _) =>
            {
                before.Add(beforeId);
                return Task.FromResult<TranscriptTail?>(new TranscriptTail("fake", room, [], 0));
            },
        };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });
        WorkItem folded = Msg("t1", "a") with { Folded = [new FoldedMessage("t1", "Human", "a", null), new FoldedMessage("t2", "Human", "b", null)] };

        harness.Room.Enqueue(new QueuedWork(1, folded));
        await WaitForPromptsAsync(session, 1, ct);

        Assert.Equal(["t1"], before);
        Assert.Equal(Fold(("Human", "a"), ("Human", "b")), session.Prompts[0]);
    }

    /// <summary>A re-sent Folded Turn reads past its trigger, and the later block leaves out the folded ids.</summary>
    [Fact]
    public async Task ResentFold_LaterBlockDropsFoldedIds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (room, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail(
                "fake",
                room,
                [
                    new ChatMessage("t1", DateTimeOffset.UnixEpoch, "h", "Human", "a"),
                    new ChatMessage("t2", DateTimeOffset.UnixEpoch, "h", "Human", "b-unique"),
                    new ChatMessage("p1", DateTimeOffset.UnixEpoch, "x", "Huddle", "posted-after"),
                ],
                0)),
        };
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { Persona = Nova });
        WorkItem folded = Msg("t1", "a") with
        {
            Redelivery = Redelivery.Waiting,
            Folded = [new FoldedMessage("t1", "Human", "a", null), new FoldedMessage("t2", "Human", "b-unique", null)],
        };

        harness.Room.Enqueue(new QueuedWork(1, folded));
        await WaitForPromptsAsync(session, 1, ct);

        string prompt = session.Prompts[0];
        Assert.Equal(prompt.IndexOf("b-unique", StringComparison.Ordinal), prompt.LastIndexOf("b-unique", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("posted-after", StringComparison.Ordinal) > prompt.IndexOf("b-unique", StringComparison.Ordinal));
    }
}
