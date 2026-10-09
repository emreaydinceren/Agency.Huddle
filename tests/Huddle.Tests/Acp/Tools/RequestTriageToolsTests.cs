using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="TriageRequestTool"/> and <see cref="ConfirmRequestTool"/> (Task 70, RQ-3, RQ-5): the receiving manager's
/// triage of a Request and the requesting Team's QA confirming it before it closes.
/// </summary>
public sealed class RequestTriageToolsTests
{
    private const string TaskId = "PLAT-0001";

    /// <summary>Platform (manager Nova, Kai, QA Rae) receives; Design (manager Dana, QA Quinn, Pat) files.</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai", "Rae"], true) { Manager = "Nova", TeamType = new VersionedRef("web", "1.2.0") },
            new TeamSummary("Design", [], ["Dana", "Quinn", "Pat"], true) { Manager = "Dana", TeamType = new VersionedRef("web", "1.2.0") },
        ],
    };

    /// <summary>Request frontmatter for a bug filed by Design's Dana.</summary>
    private static List<KeyValuePair<string, string>> RequestPairs(TriageState triage = TriageState.None) =>
        [.. new RequestFields(RequestKind.Bug, "Design", "Dana", triage, string.Empty, string.Empty).ToFrontmatter()];

    /// <summary>Reads one frontmatter value of a Task's extra fields.</summary>
    private static string Field(TaskItem task, string key) => task.UnknownFields.Single(pair => pair.Key == key).Value;

    /// <summary>Builds the call arguments of a triage.</summary>
    private static JsonObject Triage(string decision, string? reason = null, string? assignee = null, string taskId = TaskId)
    {
        JsonObject arguments = new() { ["taskId"] = taskId, ["decision"] = decision };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (assignee is not null)
        {
            arguments["assignee"] = assignee;
        }

        return arguments;
    }

    /// <summary>The harness, the gate service, the notifier and the caller ids for the Teams above.</summary>
    private sealed class Scene : IDisposable
    {
        /// <summary>The Task tool harness.</summary>
        public required TaskToolHarness Harness { get; init; }

        /// <summary>The directory that backs the gate service.</summary>
        public required TempDataDir GateDir { get; init; }

        /// <summary>The caller ids by name.</summary>
        public Dictionary<string, string> Ids { get; } = new(StringComparer.Ordinal);

        /// <summary>The recording notifier.</summary>
        public FakeRequestNotifier Notifier { get; } = new();

        /// <summary>The gate service that knows the <c>web</c> type and its QA role.</summary>
        public required ProcessGateService Gates { get; init; }

        /// <summary>Writes a Platform Task into the store.</summary>
        /// <param name="id">The Task id.</param>
        /// <param name="status">The status.</param>
        /// <param name="pairs">The extra frontmatter fields.</param>
        public void Seed(string id, TaskState status, List<KeyValuePair<string, string>> pairs)
        {
            TaskItem task = TestTasks.Make(id: id, status: status, description: "Body", location: new TaskLocation("Platform", null, false), unknownFields: pairs);
            TaskItem toWrite = task with { Path = TaskLayout.PathFor(this.Harness.Store.RootDirectory, task.Location, task.Id) };
            _ = this.Harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite)) ?? throw new InvalidOperationException("Fixture collided with an existing file.");
        }

        /// <summary>Builds the triage tool for a caller.</summary>
        /// <param name="caller">The caller's name.</param>
        public TriageRequestTool TriageTool(string caller) =>
            new(Catalog(), this.Harness.Service, this.Harness.Store, this.Notifier, this.Harness.Directory, this.Ids[caller]);

        /// <summary>Builds the confirm tool for a caller.</summary>
        /// <param name="caller">The caller's name.</param>
        public ConfirmRequestTool ConfirmTool(string caller) =>
            new(Catalog(), this.Harness.Service, this.Harness.Store, this.Harness.Personas, this.Gates, this.Notifier, this.Harness.Directory, this.Ids[caller]);

        /// <summary>The seeded Request as the store holds it now.</summary>
        public TaskItem Current() => this.Harness.Store.All.Single(task => task.Id.ToString() == TaskId);

        /// <inheritdoc />
        public void Dispose()
        {
            this.Harness.Dispose();
            this.GateDir.Dispose();
        }
    }

    /// <summary>Builds the scene: personas Dana, Pat, Quinn and Rae, one directory user per caller, and a Request in Platform.</summary>
    /// <param name="ct">Cancels the setup.</param>
    /// <param name="status">The seeded Request's status.</param>
    /// <param name="triage">The seeded Request's triage state.</param>
    private static async Task<Scene> CreateSceneAsync(CancellationToken ct, TaskState status = TaskState.Backlog, TriageState triage = TriageState.None)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Personas.Add(new PersonaIdentity("Dana", "Dana", "Dana", ["Design"]), "You are Dana.");
        harness.Personas.Add(new PersonaIdentity("Pat", "Designer", "Pat", ["Design"]), "You are Pat.");
        harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", ["Design"], Role: new RoleRef("web", "qa", "1.2.0")), "You are Quinn.");
        harness.Personas.Add(new PersonaIdentity("Rae", "QA Engineer", "rae", ["Platform"], Role: new RoleRef("web", "qa", "1.2.0")), "You are Rae.");

        TempDataDir gateDir = new();
        Scene scene = new() { Harness = harness, GateDir = gateDir, Gates = TaskServiceGateTests.BuildGates(gateDir, null) };
        foreach (string name in new[] { "Nova", "Kai", "Dana", "Pat", "Quinn", "Rae" })
        {
            Agency.Huddle.App.Data.User? user = await harness.Directory.UpsertAgentUserAsync(name, null, ct);
            scene.Ids[name] = user?.Id ?? throw new InvalidOperationException("Directory refused a user.");
        }

        scene.Seed(TaskId, status, RequestPairs(triage));
        return scene;
    }

    /// <summary>Accepting moves the Request to To Do, marks it accepted, sets the assignee and wakes the requesting manager.</summary>
    [Fact]
    public async Task Triage_Accept_MovesToToDoAssignsAndWakes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("accept", assignee: "Kai"), ct);

        Assert.Equal("Accepted PLAT-0001; assigned to Kai.", result);
        TaskItem task = scene.Current();
        Assert.Equal(TaskState.ToDo, task.Status);
        Assert.Equal("Kai", task.Assignee);
        Assert.Equal("accepted", Field(task, "triage"));
        FakeRequestNotifier.Call call = Assert.Single(scene.Notifier.Calls);
        Assert.Equal(RequestEvent.Accept, call.Event);
        Assert.Equal([WakeTarget.RequestingManager], call.Targets);
        Assert.Equal("Nova", call.Actor);
    }

    /// <summary>Declining with a reason rejects the Request, stores the reason and wakes the requesting manager and the Chief of Staff.</summary>
    [Fact]
    public async Task Triage_Decline_RejectsStoresReasonAndWakes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("decline", reason: "Out of scope."), ct);

        Assert.Equal("Declined PLAT-0001.", result);
        TaskItem task = scene.Current();
        Assert.Equal(TaskState.Rejected, task.Status);
        Assert.Equal("declined", Field(task, "triage"));
        Assert.Equal("Out of scope.", Field(task, "decline_reason"));
        FakeRequestNotifier.Call call = Assert.Single(scene.Notifier.Calls);
        Assert.Equal(RequestEvent.Decline, call.Event);
        Assert.Equal([WakeTarget.RequestingManager, WakeTarget.ChiefOfStaff], call.Targets);
    }

    /// <summary>Needs-info keeps the Request in Backlog, marks it needs-info and adds the question under a Triage heading.</summary>
    [Fact]
    public async Task Triage_NeedsInfo_AddsTriageNoteAndStaysInBacklog()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("needs_info", reason: "Which page?"), ct);

        Assert.Equal("Asked for more information on PLAT-0001.", result);
        TaskItem task = scene.Current();
        Assert.Equal(TaskState.Backlog, task.Status);
        Assert.Equal("needs-info", Field(task, "triage"));
        Assert.Equal("Body\n\n## Triage\nWhich page?", task.Description);
        FakeRequestNotifier.Call call = Assert.Single(scene.Notifier.Calls);
        Assert.Equal(RequestEvent.MarkNeedsInfo, call.Event);
        Assert.Equal([WakeTarget.RequestingManager], call.Targets);
    }

    /// <summary>A decline or a needs-info without a reason is refused and nothing changes.</summary>
    [Theory]
    [InlineData("decline", "A decline needs a 'reason'.")]
    [InlineData("needs_info", "A needs_info triage needs a 'reason' saying what is missing.")]
    public async Task Triage_WithoutReason_IsRefused(string decision, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage(decision), ct);

        Assert.Equal(expected, result);
        Assert.Equal(TaskState.Backlog, scene.Current().Status);
        Assert.Equal(string.Empty, Field(scene.Current(), "triage"));
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>Only the manager of the Request's Team may triage it.</summary>
    [Fact]
    public async Task Triage_NonManager_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Kai").InvokeAsync(Triage("accept"), ct);

        Assert.Equal("Only the manager of Platform can triage its Requests.", result);
        Assert.Equal(TaskState.Backlog, scene.Current().Status);
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>The requesting Team's manager is not the receiving manager and is refused.</summary>
    [Fact]
    public async Task Triage_RequestingManager_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Dana").InvokeAsync(Triage("accept"), ct);

        Assert.Equal("Only the manager of Platform can triage its Requests.", result);
    }

    /// <summary>An ordinary Task is not a Request and cannot be triaged.</summary>
    [Fact]
    public async Task Triage_PlainTask_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("PLAT-0002", TaskState.Backlog, []);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("accept", taskId: "PLAT-0002"), ct);

        Assert.Equal("PLAT-0002 is not a Request.", result);
    }

    /// <summary>An unknown decision is refused with the allowed values.</summary>
    [Fact]
    public async Task Triage_UnknownDecision_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("maybe"), ct);

        Assert.Equal("'decision' must be one of accept, decline, needs_info.", result);
    }

    /// <summary>A Request that is already accepted cannot be accepted again; the lifecycle's refusal comes back.</summary>
    [Fact]
    public async Task Triage_AlreadyAccepted_IsRefusedByLifecycle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct, TaskState.ToDo, TriageState.Accepted);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("accept"), ct);

        Assert.Equal("A Request in To Do (triage Accepted) cannot take the Accept event.", result);
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>The requesting Team's QA confirms a Done Request: confirmed_by is set, the Task closes and the receiving manager is woken.</summary>
    [Fact]
    public async Task Confirm_RequesterQa_SetsConfirmedByAndCloses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct, TaskState.Done, TriageState.Accepted);
        JsonObject arguments = new() { ["taskId"] = TaskId, ["note"] = "Verified on staging." };

        string result = await scene.ConfirmTool("Quinn").InvokeAsync(arguments, ct);

        Assert.Equal("Confirmed PLAT-0001; it is closed.", result);
        TaskItem task = scene.Current();
        Assert.True(task.Location.Closed);
        Assert.Equal("Quinn", Field(task, "confirmed_by"));
        Assert.Equal("Body\n\n## Confirmation\nVerified on staging.", task.Description);
        FakeRequestNotifier.Call call = Assert.Single(scene.Notifier.Calls);
        Assert.Equal(RequestEvent.Confirm, call.Event);
        Assert.Equal([WakeTarget.ReceivingManager], call.Targets);
        Assert.Equal("Quinn", call.Actor);
    }

    /// <summary>A member of the requesting Team without the verify role, and the receiving Team's own QA, are both refused.</summary>
    [Theory]
    [InlineData("Pat")]
    [InlineData("Rae")]
    public async Task Confirm_NotRequesterQa_IsRefused(string caller)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct, TaskState.Done, TriageState.Accepted);

        string result = await scene.ConfirmTool(caller).InvokeAsync(new JsonObject { ["taskId"] = TaskId }, ct);

        Assert.Equal("Only the requesting Team's QA can confirm a Request.", result);
        Assert.False(scene.Current().Location.Closed);
        Assert.Equal(string.Empty, Field(scene.Current(), "confirmed_by"));
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>Confirming a Request that is not Done is refused by the lifecycle and changes nothing.</summary>
    [Fact]
    public async Task Confirm_NotDone_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct, TaskState.Review, TriageState.Accepted);

        string result = await scene.ConfirmTool("Quinn").InvokeAsync(new JsonObject { ["taskId"] = TaskId }, ct);

        Assert.Equal("A Request in Review (triage Accepted) cannot take the Confirm event.", result);
        Assert.False(scene.Current().Location.Closed);
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>A plain Task cannot be confirmed.</summary>
    [Fact]
    public async Task Confirm_PlainTask_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("PLAT-0002", TaskState.Done, []);

        string result = await scene.ConfirmTool("Quinn").InvokeAsync(new JsonObject { ["taskId"] = "PLAT-0002" }, ct);

        Assert.Equal("PLAT-0002 is not a Request.", result);
    }

    /// <summary>Review fix 7: an accept naming an assignee outside the receiving Team is refused and the Request stays in Backlog.</summary>
    [Fact]
    public async Task Triage_AssigneeOutsideReceivingTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.TriageTool("Nova").InvokeAsync(Triage("accept", assignee: "Dana"), ct);

        Assert.Equal("Dana is not a member of Platform.", result);
        Assert.Equal(TaskState.Backlog, scene.Current().Status);
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>Review fix 7: when closing is refused after confirmed_by was written, confirmed_by is put back and nobody is woken.</summary>
    [Fact]
    public async Task Confirm_CloseRefused_RollsConfirmedByBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct, TaskState.Done, TriageState.Accepted);
        _ = Assert.IsType<TaskResult.Saved>(scene.Harness.Service.Close(scene.Current().Id, TaskActors.Human(scene.Harness.Options.Value)));

        string result = await scene.ConfirmTool("Quinn").InvokeAsync(new JsonObject { ["taskId"] = TaskId, ["note"] = "Verified." }, ct);

        Assert.Equal("PLAT-0001 is already closed.", result);
        Assert.Equal(string.Empty, Field(scene.Current(), "confirmed_by"));
        Assert.Empty(scene.Notifier.Calls);
    }

    /// <summary>A QA holding the team-management Skill can confirm, but file_request, triage_request, propose_sprint and list_teams still refuse her because she manages no Team.</summary>
    [Fact]
    public async Task Confirm_QaWithSkill_OtherRequestToolsRefuseNonManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        string quinn = scene.Ids["Quinn"];
        JsonObject fileArguments = new() { ["toTeam"] = "Platform", ["kind"] = "bug", ["title"] = "Broken", ["body"] = "Body" };
        FileRequestTool fileTool = new(Catalog(), scene.Harness.Service, scene.Harness.Store, scene.Notifier, scene.Harness.Directory, quinn);
        ListTeamsTool listTool = new(Catalog(), scene.Harness.Store, scene.Harness.Personas, scene.Harness.Directory, quinn);
        SprintProposalStore proposals = new(new DiskTeamFolders(Path.Combine(scene.Harness.Options.Value.DataDir, "design-folder")));
        ProposeSprintTool proposeTool = new(scene.Harness.Store, scene.Harness.Sprints, proposals, Catalog(), scene.Harness.Directory, quinn);

        string filed = await fileTool.InvokeAsync(fileArguments, ct);
        string triaged = await scene.TriageTool("Quinn").InvokeAsync(Triage("accept"), ct);
        string listed = await listTool.InvokeAsync(new JsonObject(), ct);
        string proposed = await proposeTool.InvokeAsync(new JsonObject { ["name"] = "Launch", ["taskIds"] = new JsonArray(TaskId) }, ct);

        Assert.Equal("Only managers file Requests; ask your own manager.", filed);
        Assert.Equal("Only the manager of Platform can triage its Requests.", triaged);
        Assert.Equal("Only managers list Teams; ask your own manager.", listed);
        Assert.Equal("Only the manager of a Team can propose a Sprint.", proposed);
        Assert.Single(scene.Harness.Store.All);
        Assert.Equal(TaskState.Backlog, scene.Current().Status);
        Assert.Empty(proposals.Pending("Design"));
        Assert.Empty(scene.Notifier.Calls);
    }
}
