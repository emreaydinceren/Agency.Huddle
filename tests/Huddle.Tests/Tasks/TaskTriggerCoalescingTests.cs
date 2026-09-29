using Microsoft.Extensions.Logging;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The coalescing and posting of the wake-up Message (Spec §10.3, §10.5, D-11; corrections-B3 D9
/// items 7-11 and 16-18). Split from the former single <c>TaskTriggerServiceTests</c> class; the
/// shared harness lives in <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerCoalescingTests
{
    /// <summary>Spec §10.3: three changes by one actor to one Task inside the window become one Message listing all three Change log summaries, in order, one <c>- </c> bullet each.</summary>
    [Fact]
    public async Task ThreeChangesWithinWindow_OneMessageListingThree()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        harness.Raise(task, HumanActor, "Priority: Medium → High");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        harness.Raise(task, HumanActor, "Tags: added auth");
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Status: To Do → In Progress\n- Priority: Medium → High\n- Tags: added auth", post.Message.Text);
    }

    /// <summary>Spec §10.3: the batch waits the configured <c>WakeCoalesceSeconds</c> - nothing is posted a moment before the window closes, and one Message is posted once it has.</summary>
    [Fact]
    public async Task WindowNotYetElapsed_PostsNothing_ThenPostsWhenItElapses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 3;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromMilliseconds(1));
        await harness.Trigger.WhenIdleAsync();
        Assert.Empty(harness.Posts);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await harness.Trigger.WhenIdleAsync();
        Assert.Single(harness.Posts);
    }

    /// <summary>Spec §10.3: a later change by the same actor joins the batch but does not restart the timer, which caps the delay at one window from the first change.</summary>
    [Fact]
    public async Task LaterChangeWithinWindow_DoesNotRestartTimer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "First");
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        harness.Raise(task, HumanActor, "Second");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- First\n- Second", post.Message.Text);
    }

    /// <summary>Spec §10.3: a change by a different actor to the same Task starts its own batch, so each actor's changes arrive as a separate Message from that actor.</summary>
    [Fact]
    public async Task ChangeByAnotherActor_SeparateMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "By the Human");
        harness.Raise(task, cast.KaiActor, "By Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, harness.Posts.Count);
        MessagePostedEvent fromHuman = Assert.Single(harness.Posts, p => p.Message.SenderId == KnownIds.Human);
        MessagePostedEvent fromKai = Assert.Single(harness.Posts, p => p.Message.SenderId == cast.Kai.Id);
        Assert.Equal("- By the Human", fromHuman.Message.Text);
        Assert.Equal("- By Kai", fromKai.Message.Text);
    }

    /// <summary>Spec §10.3: batches are keyed by Task as well as actor, so changes to two Tasks inside one window give two Messages, one per Task.</summary>
    [Fact]
    public async Task ChangesToTwoTasks_TwoMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}:{{changes}}");
        TaskItem first = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem second = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(first, HumanActor, "One");
        harness.Raise(second, HumanActor, "Two");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string[] expected = ["PLAT-0001:- One", "PLAT-0002:- Two"];
        string[] texts = [.. harness.Posts.Select(p => p.Message.Text).Order(StringComparer.Ordinal)];
        Assert.Equal(expected, texts);
    }

    /// <summary>Spec §10.4 and D-11: a change the Human made is posted as the Human.</summary>
    [Fact]
    public async Task HumanChange_PostedAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(cast.Room.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4: an edit made outside Huddle (an <see cref="TaskActorKind.OutsideHuddle"/> actor) is posted as the Human too.</summary>
    [Fact]
    public async Task OutsideHuddleChange_PostedAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, OutsideActor, "Edited outside Huddle");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
    }

    /// <summary>
    /// Spec §10.4, D-11 and corrections-B3 D9 item 17: an Agent's change is posted as that Agent -
    /// never as the Human - and is recorded through <see cref="OwnPosts"/> as the Agent's own post, so
    /// the actor's session in that Room gets its catch-up line.
    /// </summary>
    [Fact]
    public async Task AgentChange_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(cast.Kai.Id, post.Message.SenderId);
        Assert.StartsWith("@Nova ", post.Message.Text);
        string recorded = Assert.Single(harness.OwnPosts.Take(cast.Kai.Id, cast.Room.Id));
        Assert.Equal(post.Message.Text, recorded);
    }

    /// <summary>Spec §10.5: the default Message starts with the assignee's Mention (which <see cref="MentionParser"/> resolves), names the Task, and tells the assignee to call <c>get_task</c>.</summary>
    [Fact]
    public async Task Message_StartsWithMentionOfAssignee_AndCallsGetTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(
            title: "Fix login", status: TaskState.InProgress, assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(
            "@Nova Task PLAT-0001 \"Fix login\" (In Progress, Platform) was changed by You:\n" +
            "- Status: To Do → In Progress\n" +
            "Call get_task with taskId PLAT-0001 for the full task.",
            post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>
    /// Spec §10.5 and corrections-B3 D9 item 10: every placeholder is rendered - <c>{{assignee}}</c>
    /// and <c>{{title}}</c> from the latest Task, <c>{{taskId}}</c>, <c>{{status}}</c> as
    /// <c>Status.ToWire()</c>, <c>{{team}}</c> as <c>Location.Team</c>, <c>{{actor}}</c> as the
    /// actor's Name, and <c>{{changes}}</c> from each batched <c>Entry.Summary</c>.
    /// </summary>
    [Fact]
    public async Task Message_RendersEveryPlaceholder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, ProbeTemplate);
        TaskItem task = harness.Seed(TestTasks.Make(
            id: "SEC-0007",
            title: "Rotate keys",
            status: TaskState.Review,
            assignee: "Nova",
            originRoomId: cast.Room.Id,
            location: new TaskLocation("Security", null, false)));

        harness.Raise(task, cast.KaiActor, "Status: In Progress → Review");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Nova|SEC-0007|Rotate keys|Review|Security|Kai|- Status: In Progress → Review", post.Message.Text);
    }

    /// <summary>Spec §10.3: when the assignee changes inside the window, the latest assignee gets one Message listing every change in the batch, and the previous assignee isn't Mentioned.</summary>
    [Fact]
    public async Task AssigneeChangedDuringWindow_NewAssigneeGetsAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "@{{assignee}}|{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Priority: Medium → High");
        TaskItem reassigned = harness.Replace(task with { Assignee = "Kai" });
        harness.Raise(reassigned, HumanActor, "Assignee: Nova → Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Kai|- Priority: Medium → High\n- Assignee: Nova → Kai", post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Kai.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 7: <c>WakeCoalesceSeconds &lt;= 0</c> fires directly, with no timer and no clock advance.</summary>
    /// <param name="seconds">A non-positive coalesce window.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task WakeCoalesceSecondsZero_PostsImmediately(int seconds)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = seconds;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.StartsWith("@Nova ", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 item 16: an <c>@</c> in <c>{{title}}</c> is followed by U+2060, so Task text can't Mention a third Teammate who happens to be a Room Member.</summary>
    [Fact]
    public async Task TitleWithAtSign_Neutralised_NoThirdTeammateMentioned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(title: "Pair with @Kai", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(
            "@Nova Task PLAT-0001 \"Pair with " + NeutralisedAt + "Kai\" (To Do, Platform) was changed by You:\n" +
            "- Status: To Do → In Progress\n" +
            "Call get_task with taskId PLAT-0001 for the full task.",
            post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 16: an <c>@</c> in a Change log summary rendered into <c>{{changes}}</c> is neutralised the same way.</summary>
    [Fact]
    public async Task ChangesWithAtSign_Neutralised_NoThirdTeammateMentioned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Description: asked @Kai to review");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(
            "@Nova Task PLAT-0001 \"T\" (To Do, Platform) was changed by You:\n" +
            "- Description: asked " + NeutralisedAt + "Kai to review\n" +
            "Call get_task with taskId PLAT-0001 for the full task.",
            post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 10: a Task deleted inside the window (its latest state is gone) is skipped and logged; another Task's batch in the same window still posts.</summary>
    [Fact]
    public async Task TaskDeletedDuringWindow_SkippedAndLogged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem deleted = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(deleted, HumanActor, "Doomed");
        harness.Raise(control, HumanActor, "Kept");
        File.Delete(deleted.Path);
        harness.Store.RebuildFromWatcher();
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        (LogLevel Level, string Message, Exception? Exception) logged = Assert.Single(harness.Logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Equal("Not waking anyone for Task PLAT-0001: it no longer exists.", logged.Message);
    }
}