using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <see cref="TaskService.Update"/> enforces the Team's Task gates on a status change, lets the Human override
/// them with a Change log entry, and leaves Teams without a <c>Process.md</c> untouched (PR-7, PR-8, Spec §Gates).
/// </summary>
public sealed class TaskServiceGateTests
{
    private const string Team = "Platform";
    private const string Check = "dotnet test";
    internal const string QaDone ="Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done.";
    private const string SpecFirst = "Gate spec-approved-before-build: the parent spec Task must be approved before this Task can move to In Progress.";
    private const string FailFirst = "Gate failing-test-before-fix: an Evidence line showing a failing test before the fix is needed before this bug can move to Review.";
    private const string HumanOnly = "Only the Human can set approved_by.";
    internal const string QaGate ="'qa-evidence-before-done'";
    private const string SpecGate = "'spec-approved-before-build'";
    private const string FailGate = "'failing-test-before-fix'";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "qa", "title": "QA Engineer", "candidate": "candidates/qa.md", "covers": ["verify"] }
          ] }
        """;

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");
    private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

    /// <summary>The Human's own actor, as the Task panel builds it.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>A Process.md frontmatter enabling the given comma-joined, quoted gate ids.</summary>
    internal static string Process(string gates) =>
        $"---\ntitle: 'Flow'\ngates: [{gates}]\ncheckCommand: '{Check}'\n---\n# Flow\n";

    /// <summary>Builds a gate service for Team Platform whose Process.md holds <paramref name="processText"/>, or no file when null.</summary>
    internal static ProcessGateService BuildGates(TempDataDir dir, string? processText)
    {
        if (processText is not null)
        {
            File.WriteAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName), processText);
        }

        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = new TeamFile(type, null, null, null, TeamPaths.ProcessFileName, string.Empty),
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/qa.md"] = "---\ntitle: QA Engineer\nconsult_when: verifying\n---\nYou verify.",
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        return new ProcessGateService(catalog, folders, types);
    }

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

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness));

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

    /// <summary>A Teammate other than the Human cannot set approved_by.</summary>
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

    /// <summary>A Task labelled bug is refused Review without a failing test followed by a passing one.</summary>
    [Fact]
    public void Update_BugTagToReviewWithoutFailingTest_IsGateRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(FailGate)));
        TaskItem bug = CreateTask(harness, tags: ["bug"]);

        TaskResult result = harness.Service.Update(bug.Id, MoveTo(TaskState.Review), null, Nova);

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
