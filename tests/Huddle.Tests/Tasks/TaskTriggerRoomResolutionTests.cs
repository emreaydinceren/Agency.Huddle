using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Which Room a wake-up Message is posted into (Spec §10.4): the origin Room, the creator's Room, the
/// actor-set Room, or a new one. Split from the former single <c>TaskTriggerServiceTests</c> class;
/// the shared harness lives in <see cref="TaskTriggerTestSupport"/>.
/// </summary>
public sealed class TaskTriggerRoomResolutionTests
{
    /// <summary>
    /// Spec §10.4 step 1 and D-12: the origin Room is used when both the sender and the assignee are
    /// Members - even Archived, and even though a non-Archived Room with step 2's exact set also
    /// exists. Green on arrival (9.4 implemented step 1).
    /// </summary>
    [Fact]
    public async Task Origin_BothMembers_UsesOrigin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(origin.Id, true, ct);
        _ = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(origin.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 1 → 2: the origin Room doesn't hold the assignee, so the wake falls through to the creator Room {Human, creator, assignee}.</summary>
    [Fact]
    public async Task Origin_AssigneeNotMember_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id], ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 1 → 2: the origin Room doesn't hold the Agent sender, so the wake falls through to the creator Room, which does.</summary>
    [Fact]
    public async Task Origin_SenderNotMember_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Nova.Id], ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 1 → 2: an origin id that names no Room (deleted) falls through to the creator Room.</summary>
    [Fact]
    public async Task Origin_RoomGone_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: "no-such-room"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 2: with no origin, the Room whose Members are <b>exactly</b> {Human, creator, assignee} is used - not a larger Room that merely contains them.</summary>
    [Fact]
    public async Task CreatorRoom_ExactSetExists_UsesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        User? zeta = await harness.Directory.UpsertAgentUserAsync("Zeta", null, ct);
        Assert.NotNull(zeta);
        _ = await harness.Directory.CreateRoomAsync("Superset", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id, zeta.Id], ct);
        Room exact = await harness.Directory.CreateRoomAsync("Exact", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(exact.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 2 with an Agent sender: the creator is the actor, so the sender is in S and the wake is posted as that Agent in the creator Room.</summary>
    [Fact]
    public async Task CreatorRoom_AgentSenderIsCreator_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room exact = await harness.Directory.CreateRoomAsync("Exact", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(exact.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 2: when the creator is the Human, S is {Human, assignee} - the direct Room - rather than a larger Room holding the assignee.</summary>
    [Fact]
    public async Task CreatorIsHuman_UsesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = await harness.Directory.CreateRoomAsync("Trio", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(direct.Id, post.Room.Id);
    }

    /// <summary>Corrections-B3 D9 item 13: a creator with no user row (never registered, or removed) gives S = {Human, assignee}, so the direct Room is used.</summary>
    [Fact]
    public async Task CreatorUserUnknown_UsesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Ghost", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(direct.Id, post.Room.Id);
    }

    /// <summary>
    /// Spec §10.4 step 3 and D-13: a third Agent (neither creator nor assignee) isn't in step 2's set,
    /// so step 2 is skipped even though its Room exists, and {Human, actor, assignee} is used, posted
    /// as that Agent.
    /// </summary>
    [Fact]
    public async Task ThirdAgentActor_UsesActorSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        Room actorRoom = await harness.Directory.CreateRoomAsync("Actor", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(actorRoom.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 3 → 4 and corrections-B3 D9 item 13: no Room for step 3's set, so one is created with that set - the last step tried's - which contains the Agent sender.</summary>
    [Fact]
    public async Task ThirdAgentActor_NoActorSetRoom_CreatesActorSetRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(direct.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 4: no Room fits, so exactly one Room is created for {creator, assignee}, the Human added automatically, and the wake is posted there Mentioning the assignee.</summary>
    [Fact]
    public async Task NoRoom_CreatesOne_HumanAutoAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
        Assert.Equal(roomsBefore + 1, (await harness.Directory.GetRoomsAsync(ct)).Count);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(agents.Nova.Id, mentioned.Id);
    }

    /// <summary>Spec §10.4 step 4 when the creator is the Human: S is {Human, assignee}, so the direct Room is created.</summary>
    [Fact]
    public async Task NoRoom_CreatorHuman_CreatesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>Spec E-12 and D-12: an Archived Room with step 2's exact set is skipped, and a new Room with that set is created.</summary>
    [Fact]
    public async Task ArchivedExactSet_Skipped_CreatesNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room archived = await harness.Directory.CreateRoomAsync("Old", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(archived.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>Spec E-12 and D-12 for the two-Member set: an Archived direct Room is skipped too, and a new direct Room is created.</summary>
    [Fact]
    public async Task ArchivedDirectRoom_Skipped_CreatesNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room archived = await harness.Directory.CreateRoomAsync("Old", [KnownIds.Human, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(archived.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>
    /// Corrections-B3 D9 item 9 (<c>CreateRoomForAsync</c> isn't idempotent): two batches for the same
    /// Task fire together and both need a new Room for the same set; the gate serialises them, so
    /// exactly one Room is created and both wakes land in it.
    /// </summary>
    [Fact]
    public async Task TwoFiresNeedingSameNewRoom_CreateExactlyOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(task, HumanActor, "By the Human");
        harness.Raise(task, agents.KaiActor, "By Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, harness.Posts.Count);
        Assert.Single(harness.Posts.Select(p => p.Room.Id).Distinct(StringComparer.Ordinal));
        Assert.Equal(roomsBefore + 1, (await harness.Directory.GetRoomsAsync(ct)).Count);
    }
}