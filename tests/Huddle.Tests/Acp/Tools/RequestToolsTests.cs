using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="ListTeamsTool"/> and <see cref="FileRequestTool"/> (Task 69, RQ-1, RQ-8): the team cards a manager reads
/// and the cross-Team write that files a Request into another Team's backlog.
/// </summary>
public sealed class RequestToolsTests
{
    private const string BugBody = "## Steps to reproduce\nOpen the page.\n\n## Expected and actual\nIt should load; it does not.";

    /// <summary>Platform (manager Nova, specialist Kai) and Design (manager Dana).</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova", Owns = "The build", TeamType = new VersionedRef("development", "1") },
            new TeamSummary("Design", [], ["Dana"], true) { Manager = "Dana", Owns = "The look", TeamType = new VersionedRef("design", "2") },
        ],
    };

    /// <summary>Builds the harness with Dana in Design so the Design Team exists.</summary>
    private static async Task<TaskToolHarness> CreateHarnessAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Personas.Add(new PersonaIdentity("Dana", "Dana", "Dana", ["Design"]), "You are Dana.");
        return harness;
    }

    /// <summary>Builds the file_request tool over the harness, calling as the given caller.</summary>
    private static FileRequestTool CreateFileTool(TaskToolHarness harness, FakeRequestNotifier notifier, string? callerId) =>
        new(Catalog(), harness.Service, harness.Store, notifier, harness.Directory, callerId ?? throw new InvalidOperationException("Harness not seeded."));

    /// <summary>Builds the call arguments of a bug Request to Design.</summary>
    private static JsonObject BugArguments(string toTeam = "Design", string body = BugBody) => new()
    {
        ["toTeam"] = toTeam,
        ["kind"] = "bug",
        ["title"] = "Page does not load",
        ["body"] = body,
    };

    /// <summary>Reads one frontmatter value of a Task's extra fields.</summary>
    private static string Field(TaskItem task, string key) => task.UnknownFields.Single(pair => pair.Key == key).Value;

    /// <summary>Request frontmatter for a bug filed by Platform.</summary>
    private static List<KeyValuePair<string, string>> RequestPairs() =>
        [.. new RequestFields(RequestKind.Bug, "Platform", "Nova", TriageState.None, string.Empty, string.Empty).ToFrontmatter()];

    /// <summary>Writes a Task straight into the store.</summary>
    private static void Put(TaskToolHarness harness, TaskItem task)
    {
        TaskItem toWrite = task with { Path = TaskLayout.PathFor(harness.Store.RootDirectory, task.Location, task.Id) };
        _ = harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite)) ?? throw new InvalidOperationException("Fixture collided with an existing file.");
    }

    /// <summary>list_teams shows every Team's card and counts only its open Requests.</summary>
    [Fact]
    public async Task ListTeams_TwoTeams_ShowsCardsAndOpenRequestCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        TaskLocation design = new("Design", null, false);
        Put(harness, TestTasks.Make(id: "DES-0001", location: design, unknownFields: RequestPairs()));
        Put(harness, TestTasks.Make(id: "DES-0002", status: TaskState.Rejected, location: design, unknownFields: RequestPairs()));
        Put(harness, TestTasks.Make(id: "DES-0003", location: design));
        ListTeamsTool tool = new(Catalog(), harness.Store, harness.Personas, harness.Directory, harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."));

        string result = await tool.InvokeAsync([], ct);

        JsonArray teams = Assert.IsType<JsonArray>(JsonNode.Parse(result));
        Assert.Equal(2, teams.Count);
        JsonNode? designCard = Assert.Single(teams, node => node?["name"]?.GetValue<string>() == "Design");
        Assert.NotNull(designCard);
        Assert.Equal("design", designCard["type"]?.GetValue<string>());
        Assert.Equal("Dana", designCard["manager"]?.GetValue<string>());
        Assert.Equal("The look", designCard["owns"]?.GetValue<string>());
        Assert.Equal(1, designCard["openRequests"]?.GetValue<int>());
        JsonNode? platformCard = Assert.Single(teams, node => node?["name"]?.GetValue<string>() == "Platform");
        Assert.NotNull(platformCard);
        Assert.Equal(0, platformCard["openRequests"]?.GetValue<int>());
    }

    /// <summary>A specialist is refused and nothing is written.</summary>
    [Fact]
    public async Task FileRequest_Specialist_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.KaiId);

        string result = await tool.InvokeAsync(BugArguments(), ct);

        Assert.Equal("Only managers file Requests; ask your own manager.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An unknown receiving Team is refused.</summary>
    [Fact]
    public async Task FileRequest_UnknownTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);

        string result = await tool.InvokeAsync(BugArguments("Ghost"), ct);

        Assert.Equal("There is no Team named 'Ghost'; call list_teams.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>The caller's own Team is refused.</summary>
    [Fact]
    public async Task FileRequest_OwnTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);

        string result = await tool.InvokeAsync(BugArguments("platform"), ct);

        Assert.Equal("A Request goes to another Team; use create_task for your own Team's work.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A blank title is refused as a missing argument.</summary>
    [Fact]
    public async Task FileRequest_BlankTitle_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);
        JsonObject arguments = BugArguments();
        arguments["title"] = " ";

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'toTeam', 'kind', 'title' and 'body' are all required.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An unknown kind is refused.</summary>
    [Fact]
    public async Task FileRequest_UnknownKind_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);
        JsonObject arguments = BugArguments();
        arguments["kind"] = "wish";

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'kind' must be one of bug, feature, feedback, question.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A body missing a required section's content is refused, naming the section.</summary>
    [Fact]
    public async Task FileRequest_MissingRequiredSection_NamesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);

        string result = await tool.InvokeAsync(BugArguments(body: "## Steps to reproduce\nOpen the page.\n\n## Expected and actual\n\n"), ct);

        Assert.Equal("A bug Request needs content under the '## Expected and actual' heading.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A valid Request lands in the receiving Team's backlog with all six fields and the caller as requester, and the receiving manager is woken.</summary>
    [Fact]
    public async Task FileRequest_Valid_LandsInReceivingTeamBacklogAndNotifies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FakeRequestNotifier notifier = new();
        FileRequestTool tool = CreateFileTool(harness, notifier, harness.NovaId);

        string result = await tool.InvokeAsync(BugArguments(), ct);

        TaskItem request = Assert.Single(harness.Store.All);
        Assert.Equal($"Filed {request.Id} in Design.", result);
        Assert.Equal("Design", request.Location.Team);
        Assert.Equal(TaskState.Backlog, request.Status);
        Assert.Equal(BugBody, request.Description);
        Assert.Equal("bug", Field(request, "request"));
        Assert.Equal("Platform", Field(request, "from_team"));
        Assert.Equal("Nova", Field(request, "requester"));
        Assert.Equal(string.Empty, Field(request, "triage"));
        Assert.Equal(string.Empty, Field(request, "decline_reason"));
        Assert.Equal(string.Empty, Field(request, "confirmed_by"));
        FakeRequestNotifier.Call call = Assert.Single(notifier.Calls);
        Assert.Equal(request.Id.ToString(), call.TaskId);
        Assert.Equal(RequestEvent.Filed, call.Event);
        Assert.Equal([WakeTarget.ReceivingManager], call.Targets);
        Assert.Equal("Nova", call.Actor);
    }

    /// <summary>The related Task in the requesting Team gains blocked_by the new Request id.</summary>
    [Fact]
    public async Task FileRequest_RelatedTask_GainsBlockedBy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem related = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Ship login", "Platform", null), human)).Task;
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);
        JsonObject arguments = BugArguments();
        arguments["relatedTaskId"] = related.Id.ToString();

        _ = await tool.InvokeAsync(arguments, ct);

        TaskItem request = Assert.Single(harness.Store.All, task => task.Location.Team == "Design");
        TaskItem? updated = harness.Store.Get(related.Id);
        Assert.NotNull(updated);
        Assert.Equal([request.Id], updated.BlockedBy);
    }

    /// <summary>A related Task from another Team is refused and no Request is filed.</summary>
    [Fact]
    public async Task FileRequest_RelatedTaskOfOtherTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem foreign = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Their task", "Design", null), human)).Task;
        FileRequestTool tool = CreateFileTool(harness, new FakeRequestNotifier(), harness.NovaId);
        JsonObject arguments = BugArguments();
        arguments["relatedTaskId"] = foreign.Id.ToString();

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("The related Task must be one of your own Team's Tasks.", result);
        Assert.Single(harness.Store.All);
    }

    /// <summary>Review fix 7: a caller who manages no Team cannot list Teams.</summary>
    [Fact]
    public async Task ListTeams_NonManager_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        ListTeamsTool tool = new(Catalog(), harness.Store, harness.Personas, harness.Directory, harness.KaiId ?? throw new InvalidOperationException("Harness not seeded."));

        string result = await tool.InvokeAsync([], ct);

        Assert.Equal("Only managers list Teams; ask your own manager.", result);
    }

    /// <summary>Task 79 (OR-3): the built-in Chief of Staff manages no Team and may still list every Team to roll them up.</summary>
    [Fact]
    public async Task ListTeams_ChiefOfStaff_ListsEveryTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        _ = harness.Personas.Add(new PersonaIdentity("Jarvis", "Chief of Staff", "jar", [], Builtin: BuiltinTeammate.ChiefOfStaffMarker), "You are Jarvis.");
        User cos = await harness.Directory.UpsertAgentUserAsync("Jarvis", null, ct) ?? throw new InvalidOperationException("Directory refused a user.");
        ListTeamsTool tool = new(Catalog(), harness.Store, harness.Personas, harness.Directory, cos.Id);

        string result = await tool.InvokeAsync([], ct);

        JsonArray teams = Assert.IsType<JsonArray>(JsonNode.Parse(result));
        Assert.Equal(2, teams.Count);
    }

    /// <summary>Builds a catalog where Nova manages Platform and Ops, and Dana manages Design.</summary>
    private static FakeTeamCatalog TwoTeamCatalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova"], true) { Manager = "Nova" },
            new TeamSummary("Ops", [], ["Nova"], true) { Manager = "Nova" },
            new TeamSummary("Design", [], ["Dana"], true) { Manager = "Dana" },
        ],
    };

    /// <summary>Review fix 7: a manager of several Teams must say which Team files the Request.</summary>
    [Fact]
    public async Task FileRequest_ManagerOfSeveralTeams_NeedsFromTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = new(TwoTeamCatalog(), harness.Service, harness.Store, new FakeRequestNotifier(), harness.Directory, harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."));

        string result = await tool.InvokeAsync(BugArguments(), ct);

        Assert.Equal("You manage several Teams (Ops, Platform); name the Team filing this Request in 'fromTeam'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>Review fix 7: with <c>fromTeam</c> a manager of several Teams files as that Team, and a Team they do not manage is refused.</summary>
    [Fact]
    public async Task FileRequest_FromTeam_FilesAsThatTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await CreateHarnessAsync(ct);
        FileRequestTool tool = new(TwoTeamCatalog(), harness.Service, harness.Store, new FakeRequestNotifier(), harness.Directory, harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."));
        JsonObject foreign = BugArguments();
        foreign["fromTeam"] = "Design";
        JsonObject own = BugArguments();
        own["fromTeam"] = "Ops";

        string refused = await tool.InvokeAsync(foreign, ct);
        string filed = await tool.InvokeAsync(own, ct);

        Assert.Equal("You do not manage a Team named 'Design'.", refused);
        Assert.Equal("Filed DESI-0001 in Design.", filed);
        TaskItem request = Assert.Single(harness.Store.All);
        Assert.Equal("Ops", Field(request, "from_team"));
    }
}
