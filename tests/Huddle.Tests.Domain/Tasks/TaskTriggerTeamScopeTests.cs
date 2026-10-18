using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A Task wake's fallback Room is scoped to the Task's Team (Team chats spec §6.9, D-15): found and created
/// in that Team's Scope, with the origin Room still winning and an unknown Team staying at the root.
/// </summary>
public sealed class TaskTriggerTeamScopeTests
{
    private static readonly TaskLocation Marketing = new("Marketing", null, false);

    private static FakeTeamCatalog Catalog() => new()
    {
        Teams = [new TeamSummary("Marketing", [], ["Nova", "Kai"], true), new TeamSummary("Platform", [], ["Nova", "Kai"], true)],
    };

    private static async Task WakeAsync(Harness harness, TaskItem task, TaskActor actor)
    {
        harness.Raise(task, actor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>T17: the origin Room is gone and the root Nova Room exists; the wake lands in Marketing's existing one-to-one with Nova, not the root one.</summary>
    [Fact]
    public async Task Fallback_TeamTask_UsesExistingTeamScopedRoom_NotRootRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room root = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, agents.Nova.Id], ct);
        Room scoped = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, agents.Nova.Id], "Marketing", false, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: "gone", location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(scoped.Id, post.Room.Id);
        Assert.NotEqual(root.Id, post.Room.Id);
    }

    /// <summary>T17: no Marketing Room holds the set, so one is created scoped to Marketing with the Human, the creator and the assignee, and the root Rooms are untouched.</summary>
    [Fact]
    public async Task Fallback_TeamTask_NoScopedRoom_CreatesOneScopedToTheTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room rootTrio = await harness.Directory.CreateRoomAsync("Trio", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(rootTrio.Id, post.Room.Id);
        Assert.Equal("Marketing", post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
        Assert.Null((await harness.Directory.GetRoomsAsync(ct)).Single(r => r.Id == rootTrio.Id).Team);
    }

    /// <summary>T17: a single assignee with the Human as creator gets Marketing's one-to-one, created scoped when missing.</summary>
    [Fact]
    public async Task Fallback_TeamTask_HumanCreator_CreatesScopedOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Marketing", post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>A one-member Team's Team Room holds exactly {Human, Nova}, yet the wake's fallback is a separate scoped Room, never the Team Room.</summary>
    [Fact]
    public async Task Fallback_TeamTask_OneMemberTeam_DoesNotPostInTheTeamRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room teamRoom = await harness.Directory.CreateRoomAsync("Marketing", [KnownIds.Human, agents.Nova.Id], "Marketing", true, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: "gone", location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(teamRoom.Id, post.Room.Id);
        Assert.Equal("Marketing", post.Room.Team);
        Assert.False(post.Room.IsTeamRoom);
    }

    /// <summary>Spec §6.9: a usable origin Room still wins whatever its Scope, even for a Team Task.</summary>
    [Fact]
    public async Task Origin_RootRoom_StillWinsForTeamTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Nova.Id], ct);
        _ = await harness.Directory.CreateRoomAsync("Scoped", [KnownIds.Human, agents.Nova.Id], "Marketing", false, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: origin.Id, location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        Assert.Equal(origin.Id, Assert.Single(harness.Posts).Room.Id);
    }

    /// <summary>A Task whose Team the catalog does not know falls back to the root Room instead of throwing.</summary>
    [Fact]
    public async Task Fallback_TeamNotInCatalog_UsesRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, Catalog());
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", location: new("Ghosts", null, false)));

        await WakeAsync(harness, task, HumanActor);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Null(post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>A Task wake with no Team catalog at all stays at the root, as before.</summary>
    [Fact]
    public async Task Fallback_NoCatalog_UsesRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        _ = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", location: Marketing));

        await WakeAsync(harness, task, HumanActor);

        Assert.Null(Assert.Single(harness.Posts).Room.Team);
    }

    /// <summary>D-15 through ADR-0040: a Request wake reaches the same fallback, so it lands in the receiving Team's scope.</summary>
    [Fact]
    public async Task RequestWake_NoOriginRoom_LandsInReceivingTeamScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams =
            [
                new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
                new TeamSummary("Design", [], ["Mia"], true) { Manager = "Mia" },
            ],
        };
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        harness.Prompts.SetOverride(WakeMessageKey, "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}");
        TaskItem request = harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            creator: "Mia",
            unknownFields: [.. new RequestFields(RequestKind.Bug, "Design", "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()]));
        RequestNotifier notifier = new(harness.Trigger, catalog, harness.Personas, new RecordingLogger<RequestNotifier>());

        await notifier.NotifyAsync(request, RequestEvent.Filed, [WakeTarget.ReceivingManager], "Mia", ct);

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("Platform", post.Room.Team);
        Assert.Contains(cast.Nova.Id, post.Members.Select(m => m.Id));
        Assert.Contains(mia.Id, post.Members.Select(m => m.Id));
    }
}
