namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// Pins <see cref="RoomSession"/> (RS §6.1) in isolation, before <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> is
/// split to use it (Task 22.1.t). Every test builds its own <see cref="RoomSession"/> directly, with
/// a hand-written <see cref="FakeRoomSessionOwner"/> standing in for the runner and, where the
/// ticket protocol itself is under test, a <see cref="RecordingTurnScheduler"/> standing in for the
/// pool's <c>TurnGate</c> (not built until D23).
/// </summary>
public sealed class RoomSessionTests
{
    private static readonly WorkItem RoomAItem = new("room-a", "Room A", "Bob", "hello a", []);
    private static readonly WorkItem RoomBItem = new("room-b", "Room B", "Bob", "hello b", []);

    /// <summary>An edit tool call carries its path, its line and a preview of the change onto the wire, so the Room view can show what the Agent is changing.</summary>
    [Fact]
    public async Task EditToolCall_IsWrittenWithPathLineAndPreview()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted(
                "s", "call-1", "Edit notes.md", ToolKind.Edit, ToolCallStatus.InProgress, null,
                new ToolCallLocationInfo("E:\\work\\notes.md", 12),
                new ToolCallDiffInfo("E:\\work\\notes.md", "one", "two")));

        ToolActivity activity = Assert.Single(activities);
        Assert.Equal("E:\\work\\notes.md", activity.Path);
        Assert.Equal(12, activity.Line);
        Assert.Equal(new EditChange("one", "two"), activity.Edit);
    }

    /// <summary>The Adapter repeats one diff on a later update of the same call; the second update carries the line but not the preview again.</summary>
    [Fact]
    public async Task SamePreviewOnASecondUpdate_IsNotSentAgain()
    {
        ToolCallDiffInfo diff = new("E:\\work\\notes.md", "one", "two");

        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Edit notes.md", ToolKind.Edit, ToolCallStatus.InProgress, null, null, diff),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Edit, ToolCallStatus.InProgress, null, null, new ToolCallLocationInfo("E:\\work\\notes.md", 12), diff));

        Assert.Equal(2, activities.Count);
        Assert.NotNull(activities[0].Edit);
        Assert.Null(activities[1].Edit);
        Assert.Equal(12, activities[1].Line);
    }

    /// <summary>A second update whose change differs from the last one sent is sent, so the view never shows a stale preview.</summary>
    [Fact]
    public async Task ChangedPreview_IsSentAgain()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Edit a", ToolKind.Edit, ToolCallStatus.InProgress, null, null, new ToolCallDiffInfo("a", "1", "2")),
            new ToolCallUpdated("s", "call-1", null, ToolKind.Edit, ToolCallStatus.InProgress, null, null, null, new ToolCallDiffInfo("a", "1", "3")));

        Assert.Equal(new EditChange("1", "3"), activities[1].Edit);
    }

    /// <summary>The same preview on two different calls is sent for each, because the last-sent record is per call.</summary>
    [Fact]
    public async Task SamePreviewOnTwoCalls_IsSentForEach()
    {
        ToolCallDiffInfo diff = new("a", "1", "2");

        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Edit a", ToolKind.Edit, ToolCallStatus.InProgress, null, null, diff),
            new ToolCallStarted("s", "call-2", "Edit a", ToolKind.Edit, ToolCallStatus.InProgress, null, null, diff));

        Assert.All(activities, activity => Assert.NotNull(activity.Edit));
    }

    /// <summary>An edit longer than the limit is cut to it on both sides and marked truncated, so the pipe never carries an unbounded preview.</summary>
    [Fact]
    public async Task OversizePreview_IsClippedAndMarkedTruncated()
    {
        string huge = new('x', ToolActivityLimits.MaxEditSideLength + 500);

        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Write big", ToolKind.Edit, ToolCallStatus.InProgress, null, null, new ToolCallDiffInfo("big.txt", huge, huge, 3)));

        EditChange? edit = Assert.Single(activities).Edit;
        Assert.NotNull(edit);
        Assert.Equal(ToolActivityLimits.MaxEditSideLength, edit.OldText?.Length);
        Assert.Equal(ToolActivityLimits.MaxEditSideLength, edit.NewText?.Length);
        Assert.True(edit.Truncated);
        Assert.Equal(3, edit.OmittedChanges);
    }

    /// <summary>A new file whose content alone is over the limit is marked truncated even though it has no old side.</summary>
    [Fact]
    public async Task OversizeNewFile_IsMarkedTruncated()
    {
        string huge = new('x', ToolActivityLimits.MaxEditSideLength + 1);

        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Write big", ToolKind.Edit, ToolCallStatus.InProgress, null, null, new ToolCallDiffInfo("big.txt", null, huge)));

        EditChange? edit = Assert.Single(activities).Edit;
        Assert.NotNull(edit);
        Assert.Null(edit.OldText);
        Assert.True(edit.Truncated);
    }

    /// <summary>A call with no diff, such as a read, carries a path but no preview.</summary>
    [Fact]
    public async Task NonEditToolCall_CarriesNoPreview()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Read notes.md", ToolKind.Read, ToolCallStatus.InProgress, null, new ToolCallLocationInfo("E:\\work\\notes.md", null)));

        ToolActivity activity = Assert.Single(activities);
        Assert.Equal("E:\\work\\notes.md", activity.Path);
        Assert.Null(activity.Edit);
    }

    /// <summary>An update that omits its diff sends no preview, which the Room view reads as "unchanged".</summary>
    [Fact]
    public async Task UpdateWithoutADiff_SendsNoEdit()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallUpdated("s", "call-1", "Edit a", ToolKind.Edit, ToolCallStatus.Completed, null));

        ToolActivity activity = Assert.Single(activities);
        Assert.Null(activity.Edit);
        Assert.Null(activity.Path);
        Assert.Null(activity.Line);
    }

    /// <summary>When a call has both, the location's path is the one sent, because it is what the Adapter says the call touches.</summary>
    [Fact]
    public async Task LocationPathWinsOverDiffPath()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted(
                "s", "call-1", "Edit", ToolKind.Edit, ToolCallStatus.InProgress, null,
                new ToolCallLocationInfo("location.txt", 3),
                new ToolCallDiffInfo("diff.txt", "1", "2")));

        Assert.Equal("location.txt", Assert.Single(activities).Path);
    }

    /// <summary>The path is taken from the diff when the call has no location.</summary>
    [Fact]
    public async Task DiffPath_IsUsedWhenThereIsNoLocation()
    {
        IReadOnlyList<ToolActivity> activities = await RunToolTurnAsync(
            new ToolCallStarted("s", "call-1", "Edit", ToolKind.Edit, ToolCallStatus.InProgress, null, null, new ToolCallDiffInfo("diff.txt", "1", "2")));

        Assert.Equal("diff.txt", Assert.Single(activities).Path);
    }

    /// <summary>A usage update that carries a cost reaches the owner with the Adapter session's id, which is what the Spend memory is keyed by.</summary>
    [Fact]
    public async Task UsageWithCost_CallsAddSpendWithTheSessionId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsageAndCost([100], [new UsageCost(0.0548m, "USD")], "done");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal([(session.SessionId, 0.0548m, "USD")], owner.SpendAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>An advertised command list reaches the owner with the Adapter session's id, whole, as the Adapter sent it.</summary>
    [Fact]
    public async Task AvailableCommandsUpdate_CallsSetCommandsWithTheAdvertisedList()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        AvailableCommandInfo compact = new("compact", "Free up context", "<hint>");
        AvailableCommandInfo init = new("init", "Initialize", null);
        session.EnqueueToolActivity(new AvailableCommandsUpdated(session.SessionId, [compact, init]));
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var call = Assert.Single(owner.CommandsSet);
            Assert.Equal(session.SessionId, call.SessionId);
            Assert.Equal([compact, init], call.Advertised);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A usage update with no cost reports no Spend, so a local model never shows a zero.</summary>
    [Fact]
    public async Task UsageWithoutCost_DoesNotCallAddSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsage([100, 250], "done");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Empty(owner.SpendAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Spend is not a Budget: the tokens counted, and so the token Budget, are the same with a cost on the updates as without one.</summary>
    [Fact]
    public async Task TokenBudget_IsUnaffectedByACost()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsageAndCost([100, 300], [new UsageCost(0.01m, "USD"), new UsageCost(9.99m, "USD")], "done");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(300, owner.TokensAdded);
            Assert.False(owner.TokenBudgetSpent);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The first enqueued item opens the session exactly once, then prompts and posts the reply.</summary>
    [Fact]
    public async Task Enqueue_FirstItem_OpensOnceThenPromptsAndPostsReply()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        var openCalls = 0;
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, CountingOpen(() => session, () => openCalls++));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(1, openCalls);
            PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
            Assert.Equal("hi there", posted.Text);
            Assert.Contains(owner.Written, message => message is MessageDelta { IsFinal: true });
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Two queued items run one after another, never overlapping on the shared session.</summary>
    [Fact]
    public async Task Enqueue_TwoItems_RunSeriallyNoOverlap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("first");
        session.EnqueueReply("second");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            room.Enqueue(new QueuedWork(2, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 2, ct);

            Assert.False(session.OverlapDetected);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop naming a different Room than the one running never cancels the active Turn (finding P-6).</summary>
    [Fact]
    public async Task Stop_OtherRoomInSharedSession_DoesNotCancelActiveTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "a reply");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-b", mark: 1, ct);

            Assert.Equal(0, session.CancelCallCount);
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop clears only its own Room's queued items; another Room's queued item still runs.</summary>
    [Fact]
    public async Task Stop_SameRoom_ClearsItsQueuedItemsOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(200), "a1 reply");
        session.EnqueueReply("b1 reply");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            var a1 = RoomAItem with { Text = "a1" };
            var a2 = RoomAItem with { Text = "a2" };
            var b1 = RoomBItem with { Text = "b1" };

            room.Enqueue(new QueuedWork(1, a1));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);
            room.Enqueue(new QueuedWork(2, a2));
            room.Enqueue(new QueuedWork(3, b1));

            await room.StopAsync("room-a", mark: 2, ct);

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(message => message.Text == "b1 reply"), ct);

            Assert.DoesNotContain(session.Prompts, prompt => prompt.Contains("a2", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>TRAP 1 at unit level: a Stop latches before it cancels, so the stopped Turn reports no failure.</summary>
    [Fact]
    public async Task Stop_LatchWrittenBeforeCancel_ReportsNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(500), "never posted");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-a", mark: 1, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportIncompleteStop));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// D25 correction 24: strengthens TRAP 1 with the idle-timeout watchdog actually armed, unlike
    /// <see cref="Stop_LatchWrittenBeforeCancel_ReportsNoFailure"/> (<see cref="AcpOptions.TurnIdleTimeoutSeconds"/>
    /// defaults to 0 there, so no watchdog ever starts). Mutation-tested: moving
    /// <c>RoomSession.StopAsync</c>'s <c>MarkStopRequested()</c> call after its <c>Cancel()</c> call
    /// does NOT turn this test red, reliably, across repeated runs - <c>Cancel()</c> runs
    /// synchronously and the catch clause that reads <c>StopRequested</c> only runs once the
    /// exception has propagated back up through the awaited Task, by which time the reordered write
    /// has long since landed. A genuine regression here needs the idle-timeout watchdog's own
    /// <c>MarkTimedOut()</c> to race <em>the same window</em> in real wall-clock time - the identical
    /// difficulty this correction already documents for TRAP 2's <c>CancelAsync</c> callbacks being
    /// deferred. Kept anyway: it still pins that a Stop landing well inside an ARMED bound is
    /// reported as Stopped, not merely as "no bound configured at all".
    /// </summary>
    [Fact]
    public async Task Stop_WhileWatchdogArmed_LatchBeforeCancel_ReportedAsStopped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never posted");
        FakeRoomSessionOwner owner = new();
        AcpOptions options = new() { TurnIdleTimeoutSeconds = 5 };
        var (room, runCts) = CreateSession(owner, FixedOpen(session), options: options);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-a", mark: 1, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportOffline));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>TRAP 2 at unit level: the idle-timeout watchdog cancels the far side before it cancels its own Turn token.</summary>
    [Fact]
    public async Task IdleTimeout_CancelsFarSideFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(2), "too late");
        FakeRoomSessionOwner owner = new();
        AcpOptions options = new() { TurnIdleTimeoutSeconds = 1 };
        var (room, runCts) = CreateSession(owner, FixedOpen(session), options: options);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => session.CancelObservedPromptInFlight, ct, TimeSpan.FromSeconds(5));

            // The far-side cancel above is the FIRST thing the watchdog does; the Turn only fails -
            // and reports it - once that cancel has returned and its own token has fired, so the
            // report is waited for rather than read straight after the cancel was observed.
            await WaitUntilAsync(() => owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTurnFailure)), ct);

            Assert.Contains(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>An item queued while the Persona-wide token Budget is spent drains without ever prompting or opening.</summary>
    [Fact]
    public async Task TokenBudgetSpent_ItemDrainedWithoutPrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        var openCalls = 0;
        FakeRoomSessionOwner owner = new() { TokenBudgetSpent = true };
        var (room, runCts) = CreateSession(owner, CountingOpen(() => session, () => openCalls++));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTokenBudgetSpent)), ct);

            Assert.Empty(session.Prompts);
            Assert.Equal(0, openCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A closed session reopens on its next enqueued item, calling the opener again.</summary>
    [Fact]
    public async Task Close_ThenEnqueue_OpensAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var openCalls = 0;
        Func<CancellationToken, Task<IAgentSession>> open = _ =>
        {
            openCalls++;
            FakeAgentSession created = new();
            created.EnqueueReply("reply " + openCalls.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.FromResult<IAgentSession>(created);
        };
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, open);
        try
        {
            await room.OpenAsync(ct);
            Assert.Equal(RoomSessionState.Idle, room.State);

            await room.CloseAsync();
            Assert.Equal(RoomSessionState.Closed, room.State);

            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => openCalls == 2, ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Closing an Idle session never reports it Offline: the reader's own end is deliberate, not a crash.</summary>
    [Fact]
    public async Task Close_DoesNotReportOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            await room.OpenAsync(ct);
            await room.CloseAsync();

            // Give the (already-ended) event reader a moment to have run its finally, if it was ever
            // going to report anything.
            await Task.Delay(50, ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportLoopEnded));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The first item enqueued for an idle session is offered to the scheduler exactly once.</summary>
    [Fact]
    public async Task FirstEnqueue_OffersHeadOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(1, scheduler.OfferCount);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop-marked head is still completed on the scheduler, so no admitted slot leaks.</summary>
    [Fact]
    public async Task StoppedHead_StillCompletedNoLeak()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            // The mark is set before the item is even queued, so it is dropped deterministically
            // rather than racing the consumer.
            await room.StopAsync("room-a", mark: 100, ct);
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 1, ct);

            Assert.Equal(1, scheduler.OfferCount);
            Assert.Empty(session.Prompts);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The second queued item is offered before the first one's ticket is completed (RS §6.1 rule 4, finding P-4).</summary>
    [Fact]
    public async Task SecondItem_OfferedBeforeFirstCompleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(200), "first");
        session.EnqueueReply("second");
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);
            room.Enqueue(new QueuedWork(2, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 2, ct);

            List<string> calls = [.. scheduler.Calls];
            var offerSecondIndex = calls.IndexOf("Offer(2)");
            var completeFirstIndex = calls.IndexOf("Complete(1)");
            Assert.True(offerSecondIndex >= 0 && completeFirstIndex >= 0 && offerSecondIndex < completeFirstIndex);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Turn whose prompt throws still completes its ticket, so a failing Turn cannot leak a slot.</summary>
    [Fact]
    public async Task TurnThrows_TicketStillCompleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom"));
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 1, ct);

            Assert.Contains(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// A completed Turn calls <see cref="TurnActivity.Begin"/> before it prompts and
    /// <see cref="TurnActivity.End"/> once it finishes: busy while the reply is held open, not busy
    /// once it is posted (Spec §10.8).
    /// </summary>
    [Fact]
    public async Task Turn_Completes_BeginThenEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueGatedReply(release.Task, "held reply");
        FakeRoomSessionOwner owner = new();
        TurnActivity turnActivity = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), agentId: "agent-1", turnActivity: turnActivity);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => turnActivity.IsBusyIn("agent-1", "room-a"), ct);

            release.SetResult();

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);
            await WaitUntilAsync(() => !turnActivity.IsBusyIn("agent-1", "room-a"), ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// A Turn whose prompt throws still calls <see cref="TurnActivity.End"/>, so a failure never
    /// leaves the Agent marked busy forever - and it must have called <see cref="TurnActivity.Begin"/>
    /// first, not merely never having marked it busy at all.
    /// </summary>
    [Fact]
    public async Task Turn_Throws_EndStillCalled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom"));
        FakeRoomSessionOwner owner = new();
        TurnActivity turnActivity = new();
        var busyObserved = false;
        turnActivity.Changed += () => busyObserved |= turnActivity.IsBusyIn("agent-1", "room-a");
        var (room, runCts) = CreateSession(owner, FixedOpen(session), agentId: "agent-1", turnActivity: turnActivity);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTurnFailure)), ct);

            Assert.True(busyObserved);
            Assert.False(turnActivity.IsBusyIn("agent-1", "room-a"));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A stopped Turn is marked busy while it runs, and <see cref="TurnActivity.End"/> still runs once it is stopped.</summary>
    [Fact]
    public async Task Turn_Stopped_EndStillCalled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(500), "never posted");
        FakeRoomSessionOwner owner = new();
        TurnActivity turnActivity = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), agentId: "agent-1", turnActivity: turnActivity);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            Assert.True(turnActivity.IsBusyIn("agent-1", "room-a"));

            await room.StopAsync("room-a", mark: 1, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.False(turnActivity.IsBusyIn("agent-1", "room-a"));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// D8 correction 1: a shared session's <see cref="RoomSession.RoomId"/> is <see langword="null"/>,
    /// so the Turn must record the Turn's OWN Room - <c>item.RoomId</c> - not <c>this.RoomId</c>. Proven
    /// with a Turn in Room A that completes, followed by one in Room B that is held open: while Room
    /// B's Turn is in flight, only Room B is reported busy, never Room A.
    /// </summary>
    [Fact]
    public async Task Turn_SharedSession_RecordsItemRoomId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAgentSession session = new();
        session.EnqueueReply("a done");
        session.EnqueueGatedReply(release.Task, "b held");
        FakeRoomSessionOwner owner = new();
        TurnActivity turnActivity = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), agentId: "agent-1", turnActivity: turnActivity);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(message => message.Text == "a done"), ct);
            await WaitUntilAsync(() => !turnActivity.IsBusyIn("agent-1", "room-a"), ct);

            room.Enqueue(new QueuedWork(2, RoomBItem));
            await WaitUntilAsync(() => turnActivity.IsBusyIn("agent-1", "room-b"), ct);

            Assert.False(turnActivity.IsBusyIn("agent-1", "room-a"));

            release.SetResult();

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(message => message.Text == "b held"), ct);
            await WaitUntilAsync(() => !turnActivity.IsBusyIn("agent-1", "room-b"), ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>LastActivity moves forward once a Turn ends.</summary>
    [Fact]
    public async Task LastActivity_UpdatedAtTurnEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            var before = room.LastActivity;
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);
            await WaitUntilAsync(() => room.LastActivity > before, ct);

            Assert.True(room.LastActivity > before);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>D23 correction 17 / D24 correction 23: opening a session reports its advertised Models to the owner, so the Model-not-in-catalog warning can fire from any Room Session's open, not only a runner's start-up one.</summary>
    [Fact]
    public async Task Open_ReportsModelsToOwner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var reported = Assert.Single(owner.ReportedModels);
            Assert.Same(session.Models, reported);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// Builds a <see cref="RoomSession"/> with fakes standing in for every collaborator, and its own
    /// <see cref="CancellationTokenSource"/> as the run token - the consumer's loop only ever ends
    /// when that token is cancelled, exactly as <see cref="Agency.Huddle.App.Acp.PersonaRunner"/>'s
    /// own <c>runCts</c> ends it in production, so a test disposes with
    /// <see cref="DisposeSessionAsync"/> rather than a bare <c>await using</c>.
    /// </summary>
    private static (RoomSession Session, CancellationTokenSource RunCts) CreateSession(
        FakeRoomSessionOwner owner,
        Func<CancellationToken, Task<IAgentSession>> open,
        ITurnScheduler? scheduler = null,
        AcpOptions? options = null,
        string? agentId = null,
        TurnActivity? turnActivity = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: null,
            open: open,
            owner: owner,
            scheduler: scheduler ?? new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: options ?? new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            agentId: agentId,
            turnActivity: turnActivity);
        return (session, runCts);
    }

    /// <summary>
    /// Cancels <paramref name="runCts"/> - the run token <paramref name="session"/>'s consumer and
    /// event-reader loops watch - before disposing <paramref name="session"/>, so teardown ends its
    /// loops instead of hanging on them forever.
    /// </summary>
    private static async Task DisposeSessionAsync(RoomSession session, CancellationTokenSource runCts)
    {
        await runCts.CancelAsync();
        await session.DisposeAsync();
        runCts.Dispose();
    }

    /// <summary>
    /// Runs one Turn whose session reports <paramref name="toolEvents"/> before its reply, and returns
    /// every <see cref="ToolActivity"/> the runner wrote for it, in order.
    /// </summary>
    private static async Task<IReadOnlyList<ToolActivity>> RunToolTurnAsync(params AgentEvent[] toolEvents)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueToolActivity(toolEvents);
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            return [.. owner.Written.OfType<ToolActivity>()];
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>An open delegate that always returns the same session.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> FixedOpen(FakeAgentSession session) =>
        _ => Task.FromResult<IAgentSession>(session);

    /// <summary>An open delegate that counts its own calls through <paramref name="onOpen"/>, returning <paramref name="session"/>'s current value each time.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> CountingOpen(Func<FakeAgentSession> session, Action onOpen) =>
        _ =>
        {
            onOpen();
            return Task.FromResult<IAgentSession>(session());
        };

    /// <summary>Polls <paramref name="condition"/> until it is true, or fails the test after <paramref name="timeout"/>.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
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
