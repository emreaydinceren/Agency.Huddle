using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <see cref="TaskService.Update"/> enforces the Team's Task gates on a status change, lets the Human override
/// them with a Change log entry, and leaves Teams without a <c>Process.md</c> untouched (PR-7, PR-8, Spec §Gates).
/// </summary>
public sealed class TaskServiceGateTests
{
    private const string SpecFirst = "Gate spec-approved-before-build: the parent spec Task must be approved before this Task can move to In Progress.";
    private const string FailFirst = "Gate failing-test-before-fix: an Evidence line showing a failing test before the fix is needed before this bug can move on.";
    private const string HumanOnly = "Only the Human can set approved_by.";
    private const string SpecGate = "'spec-approved-before-build'";
    private const string FailGate = "'failing-test-before-fix'";

    /// <summary>The Human's plain Update, with no explicit override, gets the refusal like anyone else (review fix item 3).</summary>
    [Fact]
    public void Update_HumanDefault_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness));

        Assert.Equal(QaDone, Assert.IsType<TaskResult.GateRefused>(result).Message);
        Assert.Equal(TaskState.Backlog, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Creating a Task straight into Done is refused by the QA gate and writes nothing (review fix item 1).</summary>
    [Fact]
    public void Create_DoneWithoutQaEvidence_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));

        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", Team, null, TaskState.Done), Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.QaEvidenceBeforeDone, refused.Gate);
        Assert.Equal(QaDone, refused.Message);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>Creating a Task in a status no gate covers saves as before.</summary>
    [Fact]
    public void Create_BacklogWithGatesOn_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));

        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", Team, null), Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>The Human creating into Done is refused by default and passes only with the explicit override.</summary>
    [Fact]
    public void Create_HumanDone_RefusedUnlessOverrideIsPassed()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskDraft draft = new("Fix login", Team, null, TaskState.Done);

        TaskResult plain = harness.Service.Create(draft, HumanOf(harness));
        TaskResult forced = harness.Service.Create(draft, HumanOf(harness), humanOverridesGates: true);

        Assert.IsType<TaskResult.GateRefused>(plain);
        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(forced);
        Assert.Equal(TaskState.Done, saved.Task.Status);
        Assert.Equal("created (gate overridden by Human: qa-evidence-before-done)", saved.Change.Entry.Summary);
    }

    /// <summary>The create_task tool returns the gate message for a gated initial status.</summary>
    [Fact]
    public async Task CreateTaskTool_GatedStatus_ReturnsGateMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, gates: BuildGates(gateDir, Process(QaGate)));
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaId ?? string.Empty);
        JsonObject arguments = new() { ["title"] = "Fix login", ["team"] = Team, ["status"] = TaskState.Done.ToWire() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(QaDone, result);
    }

    /// <summary>An outside edit that moves a Task to a gated status the gate refuses is reverted and logged (review fix item 2).</summary>
    [Fact]
    public void OutsideEdit_GatedStatus_IsRestoredAndLogged()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);
        File.WriteAllText(task.Path, TaskFileFormat.Compose(task with { Status = TaskState.Done }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Backlog, after?.Status);
        Assert.Equal($"outside edit refused by gate: {QaDone}", after?.ChangeLog[^1].Summary);
    }

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");
    private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

    /// <summary>The Human's own actor, as the Task panel builds it.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);


    /// <summary>Hires Quinn into the QA role of the web type, in Team Platform.</summary>
    private static void HireQuinn(TaskToolHarness harness) =>
        harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You are Quinn.");

    /// <summary>Creates a Platform Task as the Human and returns it.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, TaskId? parent = null, IReadOnlyList<string>? tags = null)
    {
        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", Team, null, Parent: parent, Tags: tags), HumanOf(harness));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>A patch moving a Task to <paramref name="status"/>.</summary>
    private static TaskPatch MoveTo(TaskState status) => new() { Status = status };

    /// <summary>A passing Evidence line by <paramref name="author"/>.</summary>
    private static EvidenceLine Line(string author) =>
        new(Noon, author, Check, 0, null, null, string.Empty);

    /// <summary>Moving to Done without QA Evidence is refused with the gate id and message, and the Task is unchanged.</summary>
    [Fact]
    public void Update_DoneWithoutQaEvidence_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.QaEvidenceBeforeDone, refused.Gate);
        Assert.Equal(QaDone, refused.Message);
        Assert.Equal(TaskState.Backlog, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An Evidence line from a member whose role covers verify lets the Task reach Done.</summary>
    [Fact]
    public void Update_DoneWithQaEvidence_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        HireQuinn(harness);
        TaskItem task = CreateTask(harness);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, Line("Quinn"), Quinn));

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, Nova);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.Done, saved.Task.Status);
    }

    /// <summary>Evidence from a member who does not verify does not satisfy the gate.</summary>
    [Fact]
    public void Update_DoneWithNonVerifierEvidence_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, Line("Nova"), Nova));

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, Nova);

        Assert.Equal(QaDone, Assert.IsType<TaskResult.GateRefused>(result).Message);
    }

    /// <summary>The Human moves the Task anyway, and the Change log entry names the overridden gate.</summary>
    [Fact]
    public void Update_HumanOverridesGate_SavesAndLogsOverride()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.Done, saved.Task.Status);
        Assert.Equal("status: Backlog → Done (gate overridden by Human: qa-evidence-before-done)", saved.Change.Entry.Summary);
    }

    /// <summary>A Human move that no gate refuses carries no override note.</summary>
    [Fact]
    public void Update_HumanMoveNoGateRefuses_LogsNoOverride()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.InProgress), null, HumanOf(harness));

        Assert.Equal("status: Backlog → In Progress", Assert.IsType<TaskResult.Saved>(result).Change.Entry.Summary);
    }

    /// <summary>A Team with no Process.md behaves as before: any status change saves.</summary>
    [Fact]
    public void Update_NoProcessFile_DoneSaves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, null));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, Nova);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>A non-status edit is never gated, even when the gate would refuse a move to Done.</summary>
    [Fact]
    public void Update_NonStatusEdit_IsNotGated()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Title = "Fix login page" }, null, Nova);

        Assert.Equal("Fix login page", Assert.IsType<TaskResult.Saved>(result).Task.Title);
    }

    /// <summary>An Agent other than the Human cannot set approved_by.</summary>
    [Fact]
    public void Update_AgentSetsApprovedBy_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(SpecGate)));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { ApprovedBy = Optional<string?>.Set("Nova") }, null, Nova);

        Assert.Equal([HumanOnly], Assert.IsType<TaskResult.Refused>(result).Problems);
        Assert.DoesNotContain(harness.Store.Get(task.Id)?.UnknownFields ?? [], static field => field.Key == "approved_by");
    }

    /// <summary>The spec gate refuses In Progress until the Human approves the parent Task.</summary>
    [Fact]
    public void Update_ChildOfUnapprovedSpec_IsRefusedUntilHumanApproves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(SpecGate)));
        TaskItem spec = CreateTask(harness);
        TaskItem child = CreateTask(harness, spec.Id);

        TaskResult refused = harness.Service.Update(child.Id, MoveTo(TaskState.InProgress), null, Nova);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(spec.Id, new TaskPatch { ApprovedBy = Optional<string?>.Set("You") }, null, HumanOf(harness)));
        TaskResult allowed = harness.Service.Update(child.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal(SpecFirst, Assert.IsType<TaskResult.GateRefused>(refused).Message);
        Assert.Equal(TaskState.InProgress, Assert.IsType<TaskResult.Saved>(allowed).Task.Status);
    }

    /// <summary>A Task labelled bug is refused Done without a failing test followed by a passing one.</summary>
    [Fact]
    public void Update_BugTagToDoneWithoutFailingTest_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(FailGate)));
        TaskItem bug = CreateTask(harness, tags: ["bug"]);

        TaskResult result = harness.Service.Update(bug.Id, MoveTo(TaskState.Done), null, Nova);

        Assert.Equal(FailFirst, Assert.IsType<TaskResult.GateRefused>(result).Message);
    }

    /// <summary>The refusal text reaches the update_task tool result unchanged.</summary>
    [Fact]
    public async Task UpdateTaskTool_GateRefused_ReturnsGateMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = CreateTask(harness);
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaId ?? string.Empty);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = TaskState.Done.ToWire() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(QaDone, result);
    }
}
