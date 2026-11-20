using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.Tests.Ui.Teams;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <see cref="TaskService"/> enforces the per-Project rule (ADR-0103) on update and create: the Team's or the Project's
/// <c>oneInProgressPerProject</c> value decides, and a second Task In Progress in one Project is refused.
/// </summary>
public sealed class TaskServiceOneInProgressPerProjectTests
{
    private const string TeamOn = "---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'on'\n---\n";
    private const string TeamUnset = "---\nteamType: 'web@1.2.0'\n---\n";

    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>A second move into In Progress in one Project is refused, names the first Task and the Team as the setter, and writes nothing.</summary>
    [Fact]
    public void Update_SecondInProgressInSameProject_IsRefusedNamingTheFirst()
    {
        using Rig rig = new(TeamOn);
        TaskItem first = rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.OneInProgressPerProject, refused.Gate);
        Assert.Equal($"One In Progress per Project: {first.Id} \"Alpha\" is already In Progress in API. The Team setting asks for one at a time. Finish it or move it out of In Progress before starting another.", refused.Message);
        Assert.Equal(TaskState.ToDo, rig.Harness.Store.Get(second.Id)?.Status);
    }

    /// <summary>A Project that sets the rule on over a Team that does not is named as the setter.</summary>
    [Fact]
    public void Update_ProjectSetsRuleOn_NamesTheProjectAsSetter()
    {
        using Rig rig = new(TeamUnset);
        rig.Folders.ProjectFiles["API"] = ProjectFile.Empty with { OneInProgressPerProject = RuleSetting.On };
        TaskItem first = rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal($"One In Progress per Project: {first.Id} \"Alpha\" is already In Progress in API. The Project setting asks for one at a time. Finish it or move it out of In Progress before starting another.", Assert.IsType<TaskResult.GateRefused>(result).Message);
    }

    /// <summary>A Project set to off beats a Team set to on: both Tasks start.</summary>
    [Fact]
    public void Update_ProjectParallelOverTeamOn_Saves()
    {
        using Rig rig = new(TeamOn);
        rig.Folders.ProjectFiles["API"] = ProjectFile.Empty with { OneInProgressPerProject = RuleSetting.Off };
        rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Nothing sets the rule, so a second Task starts.</summary>
    [Fact]
    public void Update_NothingSetsTheRule_Saves()
    {
        using Rig rig = new(TeamUnset);
        rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>An In Progress Task in another Project of the same Team does not count.</summary>
    [Fact]
    public void Update_OtherProject_Saves()
    {
        using Rig rig = new(TeamOn);
        rig.Seed("Alpha", TaskState.InProgress, "Docs");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>The Team's Tasks with no Project are one group: a second is refused, naming the Team, and a Project's Task does not count against it.</summary>
    [Fact]
    public void Update_NoProjectTasks_FormTheirOwnGroup()
    {
        using Rig rig = new(TeamOn);
        rig.Seed("InProject", TaskState.InProgress, "API");
        TaskItem first = rig.Seed("Alpha", TaskState.InProgress, null);
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, null);

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal($"One In Progress per Project: {first.Id} \"Alpha\" is already In Progress in the Team. The Team setting asks for one at a time. Finish it or move it out of In Progress before starting another.", Assert.IsType<TaskResult.GateRefused>(result).Message);
    }

    /// <summary>A Task of the group in Needs You or Done does not count, so the second Task starts.</summary>
    /// <param name="parked">The status the first Task is parked in.</param>
    [Theory]
    [InlineData(TaskState.NeedsYou)]
    [InlineData(TaskState.Done)]
    public void Update_OtherTaskNotInProgress_Saves(TaskState parked)
    {
        using Rig rig = new(TeamOn, new FixedProofSource(FixedProofSource.Complete));
        TaskItem first = rig.Seed("Alpha", TaskState.InProgress, "API", humanGate: parked == TaskState.NeedsYou);
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");
        Assert.IsType<TaskResult.Saved>(rig.Harness.Service.Update(first.Id, MoveTo(parked), null, rig.Human));

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal(TaskState.InProgress, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>A closed Task does not count, even while its status is still In Progress.</summary>
    [Fact]
    public void Update_OtherTaskClosed_Saves()
    {
        using Rig rig = new(TeamOn);
        TaskItem first = rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");
        Assert.IsType<TaskResult.Saved>(rig.Harness.Service.Close(first.Id, rig.Human));

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Moving an In Progress Task into a Project that already has one is refused, and the Task stays where it was.</summary>
    [Fact]
    public void Update_MoveInProgressTaskIntoFullProject_IsRefused()
    {
        using Rig rig = new(TeamOn);
        TaskItem occupant = rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem mover = rig.Seed("Beta", TaskState.InProgress, "Docs");

        TaskResult result = rig.Harness.Service.Update(mover.Id, new TaskPatch { Project = Optional<string?>.Set("API") }, null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.OneInProgressPerProject, refused.Gate);
        Assert.Contains(occupant.Id.ToString(), refused.Message, StringComparison.Ordinal); // contains-ok: the full text is pinned above; this checks the move path names the occupant
        Assert.Equal("Docs", rig.Harness.Store.Get(mover.Id)?.Location.Project);
    }

    /// <summary>Moving an In Progress Task into a Project with none In Progress saves.</summary>
    [Fact]
    public void Update_MoveInProgressTaskIntoFreeProject_Saves()
    {
        using Rig rig = new(TeamOn);
        TaskItem mover = rig.Seed("Beta", TaskState.InProgress, "Docs");

        TaskResult result = rig.Harness.Service.Update(mover.Id, new TaskPatch { Project = Optional<string?>.Set("API") }, null, Nova);

        Assert.Equal("API", Assert.IsType<TaskResult.Saved>(result).Task.Location.Project);
    }

    /// <summary>Creating a Task already In Progress in a Project that has one is refused and writes nothing new.</summary>
    [Fact]
    public void Create_InProgressInFullProject_IsRefused()
    {
        using Rig rig = new(TeamOn);
        rig.Seed("Alpha", TaskState.InProgress, "API");

        TaskResult result = rig.Harness.Service.Create(new TaskDraft("Beta", Team, "API", TaskState.InProgress), Nova);

        Assert.Equal(GateId.OneInProgressPerProject, Assert.IsType<TaskResult.GateRefused>(result).Gate);
        Assert.Single(rig.Harness.Store.All);
    }

    /// <summary>A move out of In Progress is never refused, even when two Tasks of the Project are already In Progress.</summary>
    /// <param name="to">The status the Task moves to.</param>
    [Theory]
    [InlineData(TaskState.ToDo)]
    [InlineData(TaskState.Done)]
    public void Update_MoveOutOfInProgress_Saves(TaskState to)
    {
        using Rig rig = new(TeamOn, new FixedProofSource(FixedProofSource.Complete));
        rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.InProgress, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(to), null, Nova);

        Assert.Equal(to, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>The Human can override one move, and the Change log records the refusal.</summary>
    [Fact]
    public void Update_HumanOverride_SavesAndLogs()
    {
        using Rig rig = new(TeamOn);
        rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, rig.Human, humanOverridesGates: true);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.EndsWith("(gate overridden by Human: one-in-progress-per-project)", saved.Change.Entry.Summary, StringComparison.Ordinal); // contains-ok: the suffix is the override marker; the summary's head is the diff text
    }

    /// <summary>The Human is refused too when they do not override.</summary>
    [Fact]
    public void Update_HumanWithoutOverride_IsRefused()
    {
        using Rig rig = new(TeamOn);
        rig.Seed("Alpha", TaskState.InProgress, "API");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, rig.Human);

        Assert.True(Assert.IsType<TaskResult.GateRefused>(result).CanOverride);
    }

    /// <summary>When both limits refuse, the per-person gate comes first.</summary>
    [Fact]
    public void Update_BothLimitsRefuse_PerPersonWins()
    {
        using Rig rig = new("---\nteamType: 'web@1.2.0'\ngates: ['one-in-progress-per-assignee']\noneInProgressPerProject: 'on'\n---\n");
        rig.Seed("Alpha", TaskState.InProgress, "API", "Nova");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API", "Nova");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal(GateId.OneInProgressPerAssignee, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>With the per-person gate off, the per-Project rule still refuses: it is not listed in <c>gates:</c>.</summary>
    [Fact]
    public void Update_PerPersonGateOff_PerProjectStillRefuses()
    {
        using Rig rig = new("---\nteamType: 'web@1.2.0'\ngates: ['qa-evidence-before-done']\noneInProgressPerProject: 'on'\n---\n");
        rig.Seed("Alpha", TaskState.InProgress, "API", "Kai");
        TaskItem second = rig.Seed("Beta", TaskState.ToDo, "API", "Nova");

        TaskResult result = rig.Harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal(GateId.OneInProgressPerProject, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>A patch moving a Task to <paramref name="status"/>.</summary>
    /// <param name="status">The status to move to.</param>
    private static TaskPatch MoveTo(TaskState status) => new() { Status = status };

    /// <summary>The Task service stack with a Team file and the Project files a test sets.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir gateDir = new();

        /// <summary>Builds the stack for Team Platform whose <c>_team.md</c> is <paramref name="teamFileText"/>.</summary>
        /// <param name="teamFileText">The Team file text.</param>
        /// <param name="proof">The Proof source, or <see langword="null"/> for the default.</param>
        public Rig(string teamFileText, Agency.Huddle.App.Tasks.HumanGate.IProofSource? proof = null)
        {
            this.Folders = new FakeTeamFolders();
            this.Harness = new TaskToolHarness(gates: BuildGates(this.gateDir, teamFileText, sharedFolders: this.Folders), proof: proof);
        }

        /// <summary>The folders fake, whose Project files the gate reads.</summary>
        public FakeTeamFolders Folders { get; }

        /// <summary>The Task service stack.</summary>
        public TaskToolHarness Harness { get; }

        /// <summary>The Human's actor.</summary>
        public TaskActor Human => TaskActors.Human(this.Harness.Options.Value);

        /// <summary>Creates a Platform Task, overriding gates so a test can stage any starting state.</summary>
        /// <param name="title">The Task's title.</param>
        /// <param name="status">The status to create it in.</param>
        /// <param name="project">The Project, or <see langword="null"/> for the Team root.</param>
        /// <param name="assignee">Who holds it, or <see langword="null"/>.</param>
        /// <param name="humanGate">Whether the Task is Human-gated: only a gated Task may move to Needs You.</param>
        public TaskItem Seed(string title, TaskState status, string? project, string? assignee = null, bool humanGate = false)
        {
            TaskResult result = this.Harness.Service.Create(new TaskDraft(title, Team, project, status, Assignee: assignee) { HumanGate = humanGate }, this.Human, humanOverridesGates: true);
            return Assert.IsType<TaskResult.Saved>(result).Task;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Harness.Dispose();
            this.gateDir.Dispose();
        }
    }
}
