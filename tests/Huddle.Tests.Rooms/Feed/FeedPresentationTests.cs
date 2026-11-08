using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>The words, ages, day headings and grouping of a Feed item (Feed spec §6.12, §7), pinned without rendering anything.</summary>
public sealed class FeedPresentationTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static FeedItem Item(
        FeedSourceType? type,
        FeedPayload? payload,
        bool notify = false,
        DateTimeOffset? at = null,
        DateTimeOffset? resolved = null,
        string actor = "Nova",
        long id = 1) =>
        new(id, at ?? FeedTestSupport.T0, type, "target", FeedTestSupport.Agent(actor), notify, null, null, resolved, payload);

    private static string Flatten(FeedSummaryLine line) => string.Concat(line.Segments.Select(segment => segment.Text));

    /// <summary>An AGENTS.md skip reads as its card text: the Helper, the file it did not tell Agents about, and the reason (spec 6.8, U-6).</summary>
    [Fact]
    public void Of_AgentsMdChangeSkipped_ReadsAsTheCardText()
    {
        FeedSummaryLine line = FeedSummary.Of(Item(FeedSourceType.AgentsMdChangeSkipped, new AgentsMdChangeSkippedPayload("AgentsMdChanges", "AGENTS.md Changes", "Teams/Home/AGENTS.md", "Fixes a spelling mistake")));

        Assert.Equal("AGENTS.md Changes didn't tell Agents about Teams/Home/AGENTS.md: Fixes a spelling mistake", Flatten(line));
    }

    /// <summary>Every source type reads as the sentence of §7, with its context after the dot.</summary>
    [Fact]
    public void Of_EachSourceType_ReadsAsTheSpecSentence()
    {
        Assert.Equal("created Draft the pricing page", Flatten(FeedSummary.Of(Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-41", "Draft the pricing page", "Growth", null)))));
        Assert.Equal("in Growth", FeedSummary.Of(Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-41", "T", "Growth", null))).Context);
        Assert.Equal("turned on Requires your acceptance for T", Flatten(FeedSummary.Of(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "T", "Growth", null)))));
        Assert.Equal("set the Action \"Publish\" on T", Flatten(FeedSummary.Of(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "T", "Growth", "Publish")))));
        Assert.Equal("stopped to wait for your approval on T", Flatten(FeedSummary.Of(Item(FeedSourceType.TaskNeedsYou, new TaskNeedsYouPayload("GRO-1", "T", "Growth", 1, 2), true))));
        Assert.Equal("assigned you T", Flatten(FeedSummary.Of(Item(FeedSourceType.TaskAssignedToYou, new TaskAssignedPayload("GRO-1", "T", "Growth", null), true))));
        FeedSummaryLine request = FeedSummary.Of(Item(FeedSourceType.RequestAwaitsYou, new RequestAwaitsPayload("GRO-1", "T", "Growth", "Design", "bug"), true));
        Assert.Equal("finished the Request T from Design", Flatten(request));
        Assert.Equal("only you can confirm it", request.Context);
        FeedSummaryLine gate = FeedSummary.Of(Item(FeedSourceType.GateDecisionDue, new GateDecisionPayload("GRO-1", "T", "Growth", "g", "No evidence", "Backlog", "Done"), true));
        Assert.Equal("was stopped by a gate moving T to Done", Flatten(gate));
        Assert.Equal("No evidence", gate.Context);
    }

    /// <summary>Room items name the Room, a deleted Room reads as such, and a two-Member Room reads as "A ↔ B".</summary>
    [Fact]
    public void Of_RoomItems_NameTheRoom()
    {
        Assert.Equal("mentioned you in Pricing", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomMention, new RoomWaitPayload("r1", "Pricing", null), true))));
        Assert.Equal("asked you a Question in Pricing", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomQuestion, new RoomWaitPayload("r1", "Pricing", null), true))));
        Assert.Equal("is waiting for you to fill in a Form in Pricing", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomForm, new RoomWaitPayload("r1", "Pricing", null), true))));
        Assert.Equal("Pricing reached its Budget", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r1", "Pricing", null), true))));
        Assert.Equal("mentioned you in a Room that was deleted", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomMention, new RoomWaitPayload("r1", null, null), true))));
        FeedSummaryLine opened = FeedSummary.Of(Item(FeedSourceType.RoomCreated, new RoomCreatedPayload("r1", "Nova", null, ["You", "Nova"])));
        Assert.Equal("opened You ↔ Nova", Flatten(opened));
        Assert.Equal("1 Agent", opened.Context);
        Assert.Equal("3 Agents", FeedSummary.Of(Item(FeedSourceType.RoomCreated, new RoomCreatedPayload("r1", "Plan", null, ["You", "A", "B", "C"]))).Context);
        FeedSummaryLine joined = FeedSummary.Of(Item(FeedSourceType.RoomJoined, new RoomJoinedPayload("r1", "Plan", null, "Sage")));
        Assert.Equal("Sage joined Plan", Flatten(joined));
        Assert.True(joined.OmitActor);
        Assert.Equal("archived Plan", Flatten(FeedSummary.Of(Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Plan", null)))));
    }

    /// <summary>A document line uses the kind's verb and the relative path; an unreadable item says only that something happened.</summary>
    [Fact]
    public void Of_DocumentsAndUnreadable()
    {
        FeedSummaryLine reviewed = FeedSummary.Of(Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload("C:/lib/a.md", "Lib", "docs/a.md", "reviewed", "Checked")));
        Assert.Equal("reviewed docs/a.md", Flatten(reviewed));
        Assert.Equal("Checked", reviewed.Context);
        Assert.Equal("objected to docs/a.md", Flatten(FeedSummary.Of(Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload("C:/lib/a.md", "Lib", "docs/a.md", "objected", "")))));
        FeedSummaryLine unknown = FeedSummary.Of(Item(null, null));
        Assert.Equal("Something happened", Flatten(unknown));
        Assert.True(unknown.OmitActor);
    }

    /// <summary>A long Task title is cut inside its chip with an ellipsis and kept whole in the tooltip.</summary>
    [Fact]
    public void Of_LongTaskTitle_IsCutInTheChip()
    {
        string title = new('x', 60);

        FeedSegment chip = FeedSummary.Of(Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-1", title, "Growth", null))).Segments[1];

        Assert.Equal(FeedSegmentKind.Task, chip.Kind);
        Assert.Equal(FeedSummary.TaskTitleLimit, chip.Text.Length);
        Assert.EndsWith("…", chip.Text, StringComparison.Ordinal);
        Assert.Equal(title, chip.Tooltip);
        Assert.Equal("GRO-1", chip.TargetId);
    }

    /// <summary>A Notification's status line follows the spec per type, in the Human's clock, and ends "done" once resolved.</summary>
    [Fact]
    public void StatusOf_PerType_FollowsTheSpec()
    {
        DateTimeOffset at = new(2026, 10, 20, 14, 31, 0, TimeSpan.Zero);
        DateTimeOffset resolved = new(2026, 10, 20, 14, 50, 0, TimeSpan.Zero);

        Assert.Equal("Asked 14:31 · waiting for you", FeedSummary.StatusOf(Item(FeedSourceType.RoomMention, new RoomWaitPayload("r", "P", null), true, at), Utc)?.Text);
        Assert.Equal("Asked 14:31 · proof 3/4 · waiting for you", FeedSummary.StatusOf(Item(FeedSourceType.TaskNeedsYou, new TaskNeedsYouPayload("G-1", "T", "G", 3, 4), true, at), Utc)?.Text);
        Assert.Equal("Asked 14:31 · only you can move it anyway", FeedSummary.StatusOf(Item(FeedSourceType.GateDecisionDue, new GateDecisionPayload("G-1", "T", "G", "g", "m", "a", "b"), true, at), Utc)?.Text);
        Assert.Equal("Paused 14:31 · waiting for you to continue or wrap up", FeedSummary.StatusOf(Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r", "P", null), true, at), Utc)?.Text);
        FeedStatusLine? done = FeedSummary.StatusOf(Item(FeedSourceType.RoomMention, new RoomWaitPayload("r", "P", null), true, at, resolved), Utc);
        Assert.Equal("Asked 14:31 · done 14:50", done?.Text);
        Assert.True(done?.Resolved);
        Assert.Null(FeedSummary.StatusOf(Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("G-1", "T", "G", null)), Utc));
    }

    /// <summary>A Helper's gate-on shows its reason after the dot, an Agent's still shows the Team; a Helper's pause and failing items read as the §7 sentences with a Helper chip.</summary>
    [Fact]
    public void Of_HelperItems_ReadAsTheSpecSentences()
    {
        FeedSummaryLine helperGate = FeedSummary.Of(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-41", "Email the pricing sheet", "Growth", null, "Sends a document outside the Team"), true));
        FeedSummaryLine agentGate = FeedSummary.Of(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-41", "T", "Growth", null)));
        FeedSummaryLine paused = FeedSummary.Of(Item(FeedSourceType.HelperPaused, new HelperPausedPayload("NeedsHuman", "Needs Human", 50, 0), true));
        FeedSummaryLine failing = FeedSummary.Of(Item(FeedSourceType.HelperFailing, new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "The adapter isn't signed in"), true));

        Assert.Equal("turned on Requires your acceptance for Email the pricing sheet", Flatten(helperGate));
        Assert.Equal("Sends a document outside the Team", helperGate.Context);
        Assert.Equal("in Growth", agentGate.Context);
        Assert.Equal("paused Needs Human: its daily cap of 50 calls is reached", Flatten(paused));
        Assert.Equal("Agents' Tasks are not judged until you continue or the day ends", paused.Context);
        Assert.Equal(new FeedSegment(FeedSegmentKind.Helper, "Needs Human", "NeedsHuman"), paused.Segments[1]);
        Assert.Equal("Needs Human failed 3 times in a row", Flatten(failing));
        Assert.Equal("The adapter isn't signed in", failing.Context);
        Assert.Equal(new FeedSegment(FeedSegmentKind.Helper, "Needs Human", "NeedsHuman"), failing.Segments[0]);
    }

    /// <summary>The Helper items' status lines follow §7, and a Helper's gate-on tells the Human where to turn it off.</summary>
    [Fact]
    public void StatusOf_HelperItems_FollowTheSpec()
    {
        DateTimeOffset at = new(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset resolved = new(2026, 10, 20, 16, 20, 0, TimeSpan.Zero);

        Assert.Equal("Asked 16:00 · you can turn it off on the Task", FeedSummary.StatusOf(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("G-1", "T", "G", null, "r"), true, at), Utc)?.Text);
        Assert.Equal("Paused 16:00 · Continue is on the Helper's card", FeedSummary.StatusOf(Item(FeedSourceType.HelperPaused, new HelperPausedPayload("H", "H", 50, 0), true, at), Utc)?.Text);
        Assert.Equal("Since 16:00 · Tasks are not being judged", FeedSummary.StatusOf(Item(FeedSourceType.HelperFailing, new HelperFailingPayload("H", "H", 3, "x"), true, at), Utc)?.Text);
        Assert.Equal("Paused 16:00 · after a restart, Wake is on this card", FeedSummary.StatusOf(Item(FeedSourceType.TaskWakesPaused, new TaskWakesPausedPayload("G-1", "T", "G", "Nova"), true, at), Utc)?.Text);
        Assert.Equal("Paused 16:00 · done 16:20", FeedSummary.StatusOf(Item(FeedSourceType.HelperPaused, new HelperPausedPayload("H", "H", 50, 0), true, at, resolved), Utc)?.Text);
        Assert.Null(FeedSummary.StatusOf(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("G-1", "T", "G", null)), Utc));
    }

    /// <summary>Issue #452: a Helper's gate-on that also set an Action cannot be turned off, so its status line says to decide the Task.</summary>
    [Fact]
    public void StatusOf_HelperGateWithAnAction_SaysToDecide()
    {
        DateTimeOffset at = new(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);

        string? status = FeedSummary.StatusOf(Item(FeedSourceType.TaskGateOn, new TaskGatePayload("G-1", "T", "G", "Email the sheet", "r"), true, at), Utc)?.Text;

        Assert.Equal("Asked 16:00 · accept or decline it on the Task", status);
    }

    /// <summary>A hand-off held by the Task's own pause tells the Human what to do: press Allow more on the Task or change it, with no number the Task page does not show.</summary>
    [Fact]
    public void TaskWakesPaused_TaskPaused_SaysWhatToDo()
    {
        FeedItem item = Item(FeedSourceType.TaskWakesPaused, new TaskWakesPausedPayload("G-1", "T", "G", "Nova"), true);

        FeedSummaryLine line = FeedSummary.Of(item);

        Assert.Equal("paused wake-ups on T: Agents changed it too many times", Flatten(line));
        Assert.Equal("The hand-off to Nova waits. Open the Task and press Allow more, or change the Task yourself", line.Context);
    }

    /// <summary>A hand-off held by a paused Room sends the Human to Continue, not to Allow more, and says Wake takes over after a restart, when the Budget card is gone.</summary>
    [Fact]
    public void TaskWakesPaused_RoomPaused_NamesTheRoomAndContinue()
    {
        DateTimeOffset at = new(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        FeedItem item = Item(FeedSourceType.TaskWakesPaused, new TaskWakesPausedPayload("G-1", "T", "G", "Nova", "room-1"), true, at);

        FeedSummaryLine line = FeedSummary.Of(item);

        Assert.Equal("held a hand-off on T: its Room is paused", Flatten(line));
        Assert.Equal("The hand-off to Nova waits. Press Continue on the Room's Budget card", line.Context);
        Assert.Equal("Paused 16:00 · after a restart, Wake is on this card", FeedSummary.StatusOf(item, Utc)?.Text);
    }

    /// <summary>A request held by a spent Budget names Continue and, for after a restart when that card is gone, Wake; it never claims the Room is paused.</summary>
    [Fact]
    public void RoomMentionHeld_TextHoldsBeforeAndAfterARestart()
    {
        DateTimeOffset at = new(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        FeedItem item = Item(FeedSourceType.RoomMentionHeld, new RoomMentionHeldPayload("room-1", "Plan", null, "Bo", "u-bo", "m-1"), true, at);

        FeedSummaryLine line = FeedSummary.Of(item);

        Assert.Equal("held a request to Bo in Plan: the Room's Budget ran out", Flatten(line));
        Assert.Equal("Nothing was sent to Bo. Continue on the Room's Budget card sends it; after a restart that card is gone, so use Wake Bo", line.Context);
        Assert.Equal("Paused 16:00 · after a restart, Wake is on this card", FeedSummary.StatusOf(item, Utc)?.Text);
    }

    /// <summary>The Helper items wear a gauge (paused) and an alert (failing) badge, say "Helpers", and open the Helper's settings anchor.</summary>
    [Fact]
    public void HelperItems_BadgeContextWordAndTarget()
    {
        FeedItem paused = Item(FeedSourceType.HelperPaused, new HelperPausedPayload("NeedsHuman", "Needs Human", 50, 0), true);
        FeedItem failing = Item(FeedSourceType.HelperFailing, new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "x"), true);

        FeedTarget target = FeedTarget.Of(paused);

        Assert.Equal("gauge", FeedCardText.BadgeOf(FeedSourceType.HelperPaused).Name);
        Assert.Equal("alert", FeedCardText.BadgeOf(FeedSourceType.HelperFailing).Name);
        Assert.Equal(new FeedTarget(FeedTargetKind.Helper, "NeedsHuman"), target);
        Assert.Equal(target, FeedTarget.Of(failing));
        Assert.Equal("Helpers", FeedCardText.ContextWord(FeedTargetKind.Helper));
        Assert.Equal("Helpers", FeedSummary.NounOf(FeedSourceType.HelperPaused));
        Assert.Equal("/settings/helpers#helper-NeedsHuman", FeedTarget.HelperAddress("NeedsHuman"));
        Assert.Null(FeedSummary.TeamOf(paused.Payload));
    }

    /// <summary>A skipped AGENTS.md edit wears the document badge and is counted as "AGENTS.md files".</summary>
    [Fact]
    public void AgentsMdChangeSkipped_BadgeNounAndSummary()
    {
        Assert.Equal("document", FeedCardText.BadgeOf(FeedSourceType.AgentsMdChangeSkipped).Name);
        Assert.Equal("AGENTS.md files", FeedSummary.NounOf(FeedSourceType.AgentsMdChangeSkipped));
    }

    /// <summary>Ages are compact on a row and spelled on a card.</summary>
    [Fact]
    public void Age_RowAndCard()
    {
        DateTimeOffset now = FeedTestSupport.T0;

        Assert.Equal("now", FeedTime.Age(now, now.AddSeconds(-30)));
        Assert.Equal("19m", FeedTime.Age(now, now.AddMinutes(-19)));
        Assert.Equal("1h", FeedTime.Age(now, now.AddMinutes(-61)));
        Assert.Equal("3d", FeedTime.Age(now, now.AddDays(-3)));
        Assert.Equal("16 min", FeedTime.CardAge(now, now.AddMinutes(-16)));
        Assert.Equal("2 h", FeedTime.CardAge(now, now.AddHours(-2)));
        Assert.Equal("1 d", FeedTime.CardAge(now, now.AddDays(-1)));
    }

    /// <summary>Day headings run Today, Yesterday, the weekday for the last week, then the date alone.</summary>
    [Fact]
    public void DayHeading_ByLocalDay()
    {
        DateTimeOffset now = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal(new FeedDayHeading("Today", "Tue, Oct 13"), FeedTime.DayHeading(now, now.AddHours(-2), Utc));
        Assert.Equal(new FeedDayHeading("Yesterday", "Mon, Oct 12"), FeedTime.DayHeading(now, now.AddDays(-1), Utc));
        Assert.Equal(new FeedDayHeading("Friday", "Oct 9"), FeedTime.DayHeading(now, now.AddDays(-4), Utc));
        Assert.Equal(new FeedDayHeading("Oct 3", null), FeedTime.DayHeading(now, now.AddDays(-10), Utc));
    }

    /// <summary>Neighbours join only with the same actor, type and Team within ten minutes; Notifications never join Activity.</summary>
    [Fact]
    public void Group_JoinsOnlyLikeNeighbours()
    {
        DateTimeOffset t = FeedTestSupport.T0;
        FeedItem a1 = Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("G-3", "T", "Growth", null), at: t, id: 3);
        FeedItem a2 = Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("G-2", "T", "Growth", null), at: t.AddMinutes(-5), id: 2);
        FeedItem a3 = Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("G-1", "T", "Growth", null), at: t.AddMinutes(-20), id: 1);
        FeedItem other = Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("S-1", "T", "Sales", null), at: t.AddMinutes(-21), id: 0);
        FeedItem byKai = Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("S-2", "T", "Sales", null), at: t.AddMinutes(-22), actor: "Kai", id: -1);

        IReadOnlyList<FeedGroup> groups = FeedGrouping.Group([a1, a2, a3, other, byKai], Utc);

        Assert.Equal([2, 1, 1, 1], groups.Select(group => group.Items.Count));
        Assert.Equal(1, groups[0].More);
        Assert.Equal(a1, groups[0].Lead);
    }

    /// <summary>A Notification is not grouped with an Activity item of the same type, and a group keeps its dot while any member is unresolved.</summary>
    [Fact]
    public void Group_NotificationsStayApart_AndKeepTheDotWhileAnyIsUnresolved()
    {
        DateTimeOffset t = FeedTestSupport.T0;
        RoomWaitPayload payload = new("r1", "P", "Growth");
        FeedItem open = Item(FeedSourceType.RoomMention, payload, true, t, id: 2);
        FeedItem done = Item(FeedSourceType.RoomMention, payload, true, t.AddMinutes(-1), t, id: 1);
        FeedItem activity = Item(FeedSourceType.RoomMention, payload, false, t.AddMinutes(-2), id: 0);

        IReadOnlyList<FeedGroup> groups = FeedGrouping.Group([open, done, activity], Utc);

        Assert.Equal([2, 1], groups.Select(group => group.Items.Count));
        Assert.True(groups[0].AnyUnresolved);
    }

    /// <summary>An unreadable item never joins another.</summary>
    [Fact]
    public void Group_UnreadableItems_NeverJoin()
    {
        FeedItem one = Item(null, null, id: 2);
        FeedItem two = Item(null, null, id: 1);

        Assert.Equal(2, FeedGrouping.Group([one, two], Utc).Count);
    }
}
