using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// <c>propose_sprint</c> under the Needs You level (ADR-0088): when every Task it names runs inside the Team, Huddle approves the Sprint
/// at once and writes it down on each Task and in the Feed; when any Task is under Ask me, the proposal waits for the Human as before.
/// </summary>
public sealed class ProposeSprintNeedsYouLevelTests
{
    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";
    private const string Site = "Site";

    private static readonly string[] TwoIds = ["PLAT-0001", "PLAT-0002"];

    private static JsonObject Arguments(string name, params string[] taskIds) => new()
    {
        ["name"] = name,
        ["taskIds"] = new JsonArray([.. taskIds.Select(static id => (JsonNode?)JsonValue.Create(id))]),
    };

    /// <summary>Every Task runs inside the Team: the Sprint is created at once, each Task carries it, and nothing waits for the Human.</summary>
    [Fact]
    public async Task InvokeAsync_EveryTaskRunsInside_ApprovesTheSprintAtOnceAsHuddle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, teamLevel: NeedsYouLevel.RunInsideTheTeam, siteLevel: NeedsYouLevel.RunInsideTheTeam);
        rig.Put("PLAT-0001", null);
        rig.Put("PLAT-0002", Site);

        string result = await rig.Tool.InvokeAsync(Arguments("Launch", "PLAT-0001", "PLAT-0002"), ct);

        Assert.Equal("Sprint 'Launch' approved: every Task runs inside the Team, so it needed no Human approval. Added: PLAT-0001, PLAT-0002.", result);
        Sprint sprint = rig.Harness.Sprints.FindByName("Launch") ?? throw new InvalidOperationException("The Sprint was not created.");
        Assert.Empty(rig.Proposals.Pending("Platform"));
        TaskItem teamTask = rig.Read("PLAT-0001");
        TaskItem projectTask = rig.Read("PLAT-0002");
        Assert.Equal((sprint.Id, sprint.Id), (teamTask.Sprint, projectTask.Sprint));
        Assert.All([teamTask, projectTask], task =>
        {
            Assert.Equal(2, task.ChangeLog.Count);
            Assert.All(task.ChangeLog, entry => Assert.Equal("Huddle", entry.Actor));
            Assert.Null(NeedsYouRelease.LatestAt(task));
        });
        Assert.Equal("proceeded under Team level Run inside the Team: Sprint 'Launch' approved without the Human", Assert.Single(teamTask.ChangeLog, static entry => NeedsYouRelease.IsSprintApproval(entry.Summary)).Summary);
        Assert.Equal("proceeded under project level Run inside the Team: Sprint 'Launch' approved without the Human", Assert.Single(projectTask.ChangeLog, static entry => NeedsYouRelease.IsSprintApproval(entry.Summary)).Summary);
        FeedMessage post = Assert.Single(rig.Feed.Posts, static message => message.SourceType == FeedSourceType.SprintProceeded);
        Assert.False(post.Notify);
        Assert.Equal(new SprintProceededPayload("Launch", "Platform", "Team", 2), post.Payload);
        Assert.DoesNotContain(rig.Feed.Posts, static message => message.SourceType == FeedSourceType.TaskProceeded);
    }

    /// <summary>One Task under Ask me: exactly today's behaviour, the proposal waits, nothing is created or written and nothing is posted.</summary>
    [Fact]
    public async Task InvokeAsync_OneTaskUnderAskMe_StoresTheProposalAndChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, siteLevel: NeedsYouLevel.RunInsideTheTeam);
        rig.Put("PLAT-0001", Site);
        rig.Put("PLAT-0002", null);

        string result = await rig.Tool.InvokeAsync(Arguments("Launch", "PLAT-0001", "PLAT-0002"), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        Assert.Equal("Launch", Assert.Single(rig.Proposals.Pending("Platform")).Name);
        Assert.Null(rig.Harness.Sprints.FindByName("Launch"));
        Assert.All(TwoIds, id =>
        {
            TaskItem task = rig.Read(id);
            Assert.Null(task.Sprint);
            Assert.Empty(task.ChangeLog);
        });
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>Without the level service every Task is under Ask me, so the proposal waits even when the setting would let it run.</summary>
    [Fact]
    public async Task InvokeAsync_NoApproverWired_StoresTheProposal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, teamLevel: NeedsYouLevel.RunInsideTheTeam, wireApprover: false);
        rig.Put("PLAT-0001", null);

        string result = await rig.Tool.InvokeAsync(Arguments("Launch", "PLAT-0001"), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        _ = Assert.Single(rig.Proposals.Pending("Platform"));
        Assert.Empty(rig.Read("PLAT-0001").ChangeLog);
    }

    /// <summary>A Task that is Done by the time of the approval is skipped with its reason and gets neither the Sprint nor an entry.</summary>
    [Fact]
    public async Task ApproveWithoutHuman_TaskAlreadyDone_IsSkippedAndTheRestAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, teamLevel: NeedsYouLevel.RunInsideTheTeam);
        rig.Put("PLAT-0001", null);
        rig.Put("PLAT-0002", null, TaskState.Done);

        string? result = rig.Approver.ApproveWithoutHuman("Launch", [rig.Read("PLAT-0001"), rig.Read("PLAT-0002")]);

        Assert.Equal("Sprint 'Launch' approved: every Task runs inside the Team, so it needed no Human approval. Added: PLAT-0001. Skipped: PLAT-0002 (already Done).", result);
        TaskItem done = rig.Read("PLAT-0002");
        Assert.Null(done.Sprint);
        Assert.Empty(done.ChangeLog);
        Assert.NotNull(rig.Read("PLAT-0001").Sprint);
    }

    /// <summary>A Sprint that cannot be created is reported to the manager and no Task is touched.</summary>
    [Fact]
    public async Task ApproveWithoutHuman_SprintRefused_ReportsItAndChangesNoTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, teamLevel: NeedsYouLevel.RunInsideTheTeam);
        rig.Put("PLAT-0001", null);

        string? result = rig.Approver.ApproveWithoutHuman("current", [rig.Read("PLAT-0001")]);

        Assert.Equal($"The Sprint 'current' could not be created: {SprintFileFormat.ValidateName("current")}", result);
        Assert.Null(rig.Read("PLAT-0001").Sprint);
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>The name of the current Sprint is reused: no second Sprint is made and the Task joins it.</summary>
    [Fact]
    public async Task InvokeAsync_NameOfTheCurrentSprint_ReusesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, teamLevel: NeedsYouLevel.RunInsideTheTeam, sprintsText: TwoSprints);
        rig.Put("PLAT-0001", null);

        string result = await rig.Tool.InvokeAsync(Arguments("Auth hardening", "PLAT-0001"), ct);

        Assert.Equal("Sprint 'Auth hardening' approved: every Task runs inside the Team, so it needed no Human approval. Added: PLAT-0001.", result);
        Assert.Equal(["Sprint 1", "Auth hardening"], rig.Harness.Sprints.All.Select(static sprint => sprint.Name));
        Assert.Equal("S2", rig.Read("PLAT-0001").Sprint?.ToString());
        Assert.Empty(rig.Proposals.Pending("Platform"));
    }

    /// <summary>The Sprint entry is not a merge release: the merge gate and the wake rules still read it as nothing.</summary>
    [Fact]
    public void SprintSummaryFor_IsNotAMergeRelease()
    {
        string summary = NeedsYouRelease.SprintSummaryFor(NeedsYouScope.Project, "Launch");

        Assert.False(NeedsYouRelease.IsRelease(summary));
        Assert.Null(NeedsYouRelease.ScopeWordOf(summary));
        Assert.True(NeedsYouRelease.IsSprintApproval(summary));
        Assert.False(NeedsYouRelease.IsSprintApproval(NeedsYouRelease.SummaryFor(NeedsYouScope.Project)));
    }

    /// <summary>One Platform Team with Nova as its manager over a real Task stack, a real Sprint store and the levels the test names.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir levelDir = new();

        private Rig(TaskToolHarness harness, NeedsYouLevel? teamLevel, NeedsYouLevel? siteLevel, bool wireApprover)
        {
            this.Harness = harness;
            this.Feed = new RecordingFeedPublisher();
            this.Proposals = new SprintProposalStore(new DiskTeamFolders(Path.Combine(harness.Options.Value.DataDir, "platform-folder")));
            FakeTeamCatalog catalog = new()
            {
                Teams = [new TeamSummary("Platform", [Site], ["Nova", "Kai"], true) { Manager = "Nova" }],
            };
            NeedsYouLevels levels = new(catalog, new NeedsYouLevelStore(this.levelDir.Options()), new ScopeIdStore(this.levelDir.Options()), this.levelDir.Options());
            _ = levels.SetTeam("Platform", teamLevel, null);
            _ = levels.SetProject("Platform", Site, siteLevel, null);
            this.Approver = new SprintApprover((SprintStore)harness.Sprints, harness.Service, harness.Store, levels, this.Feed);
            this.Tool = new ProposeSprintTool(
                harness.Store,
                harness.Sprints,
                this.Proposals,
                catalog,
                harness.Directory,
                harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."),
                new FakePromptSource(),
                wireApprover ? this.Approver : null);
        }

        public TaskToolHarness Harness { get; }

        public RecordingFeedPublisher Feed { get; }

        public SprintProposalStore Proposals { get; }

        public SprintApprover Approver { get; }

        public ProposeSprintTool Tool { get; }

        public static async Task<Rig> CreateAsync(CancellationToken ct, NeedsYouLevel? teamLevel = null, NeedsYouLevel? siteLevel = null, string sprintsText = "# Sprints\n", bool wireApprover = true) =>
            new(await TaskToolHarness.CreateAsync(ct, sprintsText: sprintsText), teamLevel, siteLevel, wireApprover);

        public void Put(string id, string? project, TaskState status = TaskState.ToDo)
        {
            TaskItem task = TestTasks.Make(id: id, status: status, location: new TaskLocation("Platform", project, false));
            TaskItem toWrite = task with { Path = TaskLayout.PathFor(this.Harness.Store.RootDirectory, task.Location, task.Id) };
            _ = this.Harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite))
                ?? throw new InvalidOperationException("Fixture collided with an existing file.");
        }

        public TaskItem Read(string id) =>
            TaskId.TryParse(id, out TaskId taskId) && this.Harness.Store.Get(taskId) is { } task ? task : throw new InvalidOperationException("The Task is missing.");

        public void Dispose()
        {
            this.Harness.Dispose();
            this.levelDir.Dispose();
        }
    }
}
