namespace Agency.Huddle.Tests.Acp.Tools;

using System.Reflection;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Covers how an Agent sets and reads a Sprint through the task tools (Sprints spec §10, SP-T9, E-S16):
/// <c>create_task</c>, <c>update_task</c>, <c>list_tasks</c> and <c>get_task</c> with <c>current</c>, an id, a
/// name, <c>""</c>, <c>none</c>, an unknown name and an empty catalogue, and that no task tool can create a Sprint.
/// </summary>
public sealed class SprintToolTests
{
    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    private const string KnownList = "Known sprints: S1 Sprint 1, S2 Auth hardening.";

    private static readonly SprintId S1 = new(1);

    private static readonly SprintId S2 = new(2);

    /// <summary>Narrows <see cref="TaskToolHarness.NovaId"/>, seeded by <see cref="TaskToolHarness.CreateAsync"/>, to a non-null caller id.</summary>
    /// <param name="harness">The harness built by <see cref="TaskToolHarness.CreateAsync"/>.</param>
    private static string NovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    /// <summary>Creates a Task through the harness's real <see cref="TaskService"/>, as the Human actor.</summary>
    /// <param name="harness">The harness whose service creates the Task.</param>
    /// <param name="title">The Task's title.</param>
    /// <param name="sprint">The Sprint to hold, or <see langword="null"/> for none.</param>
    /// <param name="assignee">The assignee, or <see langword="null"/>.</param>
    private static TaskItem CreateTask(TaskToolHarness harness, string title = "Support SAML login", SprintId? sprint = null, string? assignee = null)
    {
        TaskActor actor = TaskActors.Human(harness.Options.Value);
        TaskResult result = harness.Service.Create(new TaskDraft(title, "Platform", null, Assignee: assignee, Sprint: sprint), actor);
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Builds a <see cref="CreateTaskTool"/> over the harness, calling as Nova.</summary>
    /// <param name="harness">The harness to build over.</param>
    private static CreateTaskTool CreateTool(TaskToolHarness harness) =>
        new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), NovaId(harness));

    /// <summary>Builds an <see cref="UpdateTaskTool"/> over the harness, calling as Nova.</summary>
    /// <param name="harness">The harness to build over.</param>
    private static UpdateTaskTool UpdateTool(TaskToolHarness harness) =>
        new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), NovaId(harness));

    /// <summary>Builds a <see cref="ListTasksTool"/> over the harness, calling as Nova.</summary>
    /// <param name="harness">The harness to build over.</param>
    private static ListTasksTool ListTool(TaskToolHarness harness) =>
        new(harness.Store, harness.Directory, harness.Personas, harness.Sprints, new FakePromptSource(), NovaId(harness));

    /// <summary>The arguments of a <c>create_task</c> call for a Platform Task, with a Sprint argument when one is given.</summary>
    /// <param name="sprint">The <c>sprint</c> argument, or <see langword="null"/> to leave it out.</param>
    private static JsonObject CreateArguments(string? sprint)
    {
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform" };
        if (sprint is not null)
        {
            arguments["sprint"] = sprint;
        }

        return arguments;
    }

    /// <summary>An agent's <c>create_task</c> with <c>current</c>, an id of either case, or a name of any case sets the Sprint, and the file carries <c>sprint:</c>.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The id of the Sprint it must resolve to.</param>
    [Theory]
    [InlineData("current", "S2")]
    [InlineData("CURRENT", "S2")]
    [InlineData("S1", "S1")]
    [InlineData("s1", "S1")]
    [InlineData("auth HARDENING", "S2")]
    public async Task CreateTask_SprintArgument_SetsItAndTheFileHasIt(string argument, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);

        string result = await CreateTool(harness).InvokeAsync(CreateArguments(argument), ct);

        TaskItem task = Assert.Single(harness.Store.All);
        Assert.StartsWith("Created ", result, StringComparison.Ordinal);
        Assert.Equal(expected, task.Sprint?.ToString());
        Assert.Contains($"sprint: {expected}", await File.ReadAllTextAsync(task.Path, ct), StringComparison.Ordinal);
    }

    /// <summary>An empty <c>sprint</c> on <c>create_task</c> means no Sprint, and the file has no <c>sprint:</c> line.</summary>
    [Fact]
    public async Task CreateTask_EmptySprint_MeansNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);

        string result = await CreateTool(harness).InvokeAsync(CreateArguments(""), ct);

        TaskItem task = Assert.Single(harness.Store.All);
        Assert.StartsWith("Created ", result, StringComparison.Ordinal);
        Assert.Null(task.Sprint);
        Assert.DoesNotContain("sprint:", await File.ReadAllTextAsync(task.Path, ct), StringComparison.Ordinal);
    }

    /// <summary>An unknown name on <c>create_task</c> is refused with the known sprints, and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_UnknownSprint_RefusedListingKnownSprintsAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);

        string result = await CreateTool(harness).InvokeAsync(CreateArguments("Nope"), ct);

        Assert.Equal($"Unknown sprint 'Nope'. {KnownList}", result);
        Assert.Empty(harness.Store.All);
        Assert.Equal(TwoSprints, await File.ReadAllTextAsync(harness.SprintsFilePath, ct));
    }

    /// <summary>With no sprints, <c>create_task</c> says so for <c>current</c> (E-S16) and for a name, and writes nothing.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The refusal text.</param>
    [Theory]
    [InlineData("current", "There is no current sprint; no sprints exist yet.")]
    [InlineData("Nope", "Unknown sprint 'Nope'. There are no sprints yet.")]
    public async Task CreateTask_EmptyCatalogue_RefusedSayingNoSprintsExist(string argument, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness).InvokeAsync(CreateArguments(argument), ct);

        Assert.Equal(expected, result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A <c>sprint</c> that is not a string is refused naming the argument, like every other argument.</summary>
    [Fact]
    public async Task CreateTask_SprintNotAString_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        JsonObject arguments = CreateArguments(null);
        arguments["sprint"] = 2;

        string result = await CreateTool(harness).InvokeAsync(arguments, ct);

        Assert.Contains("sprint", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An agent's <c>update_task</c> with only <c>sprint</c> is a change, not "Nothing to change", for <c>current</c>, an id and a name.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The id of the Sprint it must resolve to.</param>
    [Theory]
    [InlineData("current", "S2")]
    [InlineData("S2", "S2")]
    [InlineData("auth hardening", "S2")]
    [InlineData("sprint 1", "S1")]
    public async Task UpdateTask_OnlySprint_CountsAsAChange(string argument, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness, sprint: expected == "S1" ? S2 : null);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["sprint"] = argument };

        string result = await UpdateTool(harness).InvokeAsync(arguments, ct);

        Assert.StartsWith($"Updated {task.Id}: sprint: ", result, StringComparison.Ordinal);
        TaskItem saved = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("task missing");
        Assert.Equal(expected, saved.Sprint?.ToString());
    }

    /// <summary>An empty <c>sprint</c> on <c>update_task</c> clears the Sprint and the file loses its <c>sprint:</c> line.</summary>
    [Fact]
    public async Task UpdateTask_EmptySprint_ClearsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness, sprint: S1);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["sprint"] = "" };

        string result = await UpdateTool(harness).InvokeAsync(arguments, ct);

        Assert.StartsWith($"Updated {task.Id}: sprint: S1 (Sprint 1) → —", result, StringComparison.Ordinal);
        TaskItem saved = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("task missing");
        Assert.Null(saved.Sprint);
        string[] fileLines = (await File.ReadAllTextAsync(saved.Path, ct)).Split('\n');
        Assert.DoesNotContain(fileLines, static line => line.StartsWith("sprint:", StringComparison.Ordinal));
    }

    /// <summary>An unknown name on <c>update_task</c> is refused with the known sprints, and the Task is unchanged.</summary>
    [Fact]
    public async Task UpdateTask_UnknownSprint_RefusedListingKnownSprints()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness, sprint: S1);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["sprint"] = "Nope" };

        string result = await UpdateTool(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"Unknown sprint 'Nope'. {KnownList}", result);
        Assert.Equal(S1, harness.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>With no sprints, <c>update_task</c> says so for <c>current</c> (E-S16) and for a name.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The refusal text.</param>
    [Theory]
    [InlineData("current", "There is no current sprint; no sprints exist yet.")]
    [InlineData("Nope", "Unknown sprint 'Nope'. There are no sprints yet.")]
    public async Task UpdateTask_EmptyCatalogue_RefusedSayingNoSprintsExist(string argument, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["sprint"] = argument };

        string result = await UpdateTool(harness).InvokeAsync(arguments, ct);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// The agent's success text and the Change log entry (the text a wake-up Message is built from) both carry the
    /// id-and-name form, <c>sprint: S1 (Sprint 1) → S2 (Auth hardening)</c>.
    /// </summary>
    [Fact]
    public async Task UpdateTask_MovingSprint_ChangeLogAndSuccessTextCarryIdAndName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness, sprint: S1, assignee: "Kai");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["sprint"] = "Auth hardening" };

        string result = await UpdateTool(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"Updated {task.Id}: sprint: S1 (Sprint 1) → S2 (Auth hardening). Kai will be notified.", result);
        TaskItem saved = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("task missing");
        ChangeLogEntry entry = saved.ChangeLog[^1];
        Assert.Equal("Nova", entry.Actor);
        Assert.Equal("sprint: S1 (Sprint 1) → S2 (Auth hardening)", entry.Summary);
    }

    /// <summary>The seeded Tasks for the list tests: one in S1, two in S2, one with no Sprint.</summary>
    /// <param name="harness">The harness to seed.</param>
    private static void SeedListTasks(TaskToolHarness harness)
    {
        _ = CreateTask(harness, "In one", S1);
        _ = CreateTask(harness, "In two A", S2);
        _ = CreateTask(harness, "In two B", S2);
        _ = CreateTask(harness, "In none");
    }

    /// <summary>Runs <c>list_tasks</c> with a <c>sprint</c> argument and returns the titles it lists, in line order.</summary>
    /// <param name="harness">The seeded harness.</param>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="ct">Cancels the call.</param>
    private static async Task<IReadOnlyList<string>> ListTitlesAsync(TaskToolHarness harness, string argument, CancellationToken ct)
    {
        string result = await ListTool(harness).InvokeAsync(new JsonObject { ["sprint"] = argument }, ct);
        return [.. result.Split('\n').Select(static line => line.Split(" | ")[^1])];
    }

    /// <summary><c>list_tasks</c> with <c>current</c>, an id of either case, or a name of any case lists only that Sprint's Tasks.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The titles it must list.</param>
    [Theory]
    [InlineData("current", new[] { "In two A", "In two B" })]
    [InlineData("S2", new[] { "In two A", "In two B" })]
    [InlineData("s1", new[] { "In one" })]
    [InlineData("SPRINT 1", new[] { "In one" })]
    public async Task ListTasks_SprintArgument_ListsOnlyThatSprint(string argument, string[] expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        SeedListTasks(harness);

        IReadOnlyList<string> titles = await ListTitlesAsync(harness, argument, ct);

        Assert.Equal(expected.Order(StringComparer.Ordinal), titles.Order(StringComparer.Ordinal));
    }

    /// <summary><c>list_tasks</c> with <c>none</c> lists only Tasks with no Sprint.</summary>
    [Fact]
    public async Task ListTasks_None_ListsOnlyTasksWithNoSprint()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        SeedListTasks(harness);

        IReadOnlyList<string> titles = await ListTitlesAsync(harness, "none", ct);

        Assert.Equal(["In none"], titles);
    }

    /// <summary><c>list_tasks</c> with an unknown name is refused with the known sprints, and lists nothing.</summary>
    [Fact]
    public async Task ListTasks_UnknownSprint_RefusedListingKnownSprints()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        SeedListTasks(harness);

        string result = await ListTool(harness).InvokeAsync(new JsonObject { ["sprint"] = "Nope" }, ct);

        Assert.Equal($"Unknown sprint 'Nope'. {KnownList}", result);
    }

    /// <summary>With no sprints, <c>list_tasks</c> says so for <c>current</c> (E-S16) and for a name, and <c>none</c> still works.</summary>
    /// <param name="argument">The <c>sprint</c> argument.</param>
    /// <param name="expected">The refusal text.</param>
    [Theory]
    [InlineData("current", "There is no current sprint; no sprints exist yet.")]
    [InlineData("Nope", "Unknown sprint 'Nope'. There are no sprints yet.")]
    public async Task ListTasks_EmptyCatalogue_RefusedSayingNoSprintsExist(string argument, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness);

        string result = await ListTool(harness).InvokeAsync(new JsonObject { ["sprint"] = argument }, ct);

        Assert.Equal(expected, result);
    }

    /// <summary>With no sprints at all, <c>list_tasks</c> with <c>none</c> lists every Task, since none holds a Sprint.</summary>
    [Fact]
    public async Task ListTasks_NoneWithEmptyCatalogue_ListsEveryTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness, "Only task");

        IReadOnlyList<string> titles = await ListTitlesAsync(harness, "none", ct);

        Assert.Equal(["Only task"], titles);
    }

    /// <summary><c>get_task</c> prints <c>sprint: S2 (Auth hardening)</c> after the other <c>key: value</c> lines, and the task line is unchanged.</summary>
    [Fact]
    public async Task GetTask_TaskInSprint_PrintsIdAndName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness, sprint: S2);
        GetTaskTool tool = new(harness.Store, harness.Sprints, new FakePromptSource());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        string[] lines = result.Split('\n');
        Assert.Equal(TaskToolText.Line(task), lines[0]);
        Assert.Contains("sprint: S2 (Auth hardening)", lines);
    }

    /// <summary><c>get_task</c> prints <c>sprint: S9 (missing)</c> for a Sprint that is not in the file.</summary>
    [Fact]
    public async Task GetTask_SprintNotInFile_PrintsMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        TaskItem task = harness.SeedOnDisk(Agency.Huddle.Tests.Tasks.TestTasks.Make(id: "PLAT-0042", sprint: new SprintId(9)));
        GetTaskTool tool = new(harness.Store, harness.Sprints, new FakePromptSource());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.Contains("sprint: S9 (missing)", result.Split('\n'));
    }

    /// <summary><c>get_task</c> prints no sprint line for a Task that holds none, and none for a catalogue that is empty.</summary>
    [Fact]
    public async Task GetTask_TaskWithNoSprint_PrintsNoSprintLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness);
        GetTaskTool tool = new(harness.Store, harness.Sprints, new FakePromptSource());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.DoesNotContain("sprint:", result, StringComparison.Ordinal);
    }

    /// <summary><c>create_task</c>, <c>update_task</c> and <c>list_tasks</c> advertise an optional string <c>sprint</c> property in their schema.</summary>
    [Fact]
    public async Task Schemas_CreateUpdateAndList_HaveAnOptionalStringSprintProperty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IAppTool[] tools = [CreateTool(harness), UpdateTool(harness), ListTool(harness)];

        foreach (IAppTool tool in tools)
        {
            JsonObject schema = tool.InputSchema;
            Assert.Equal("string", schema["properties"]?["sprint"]?["type"]?.GetValue<string>());
            Assert.DoesNotContain(schema["required"]?.AsArray().Select(static item => item?.GetValue<string>()) ?? [], static name => name == "sprint");
        }
    }

    /// <summary>
    /// D-S5: no task tool can create a Sprint, because none is constructed with the <see cref="SprintStore"/>. The four that
    /// read Sprints take the read-only <see cref="ISprintCatalog"/> instead.
    /// </summary>
    [Fact]
    public void TaskTools_ConstructorsNeverTakeASprintStore()
    {
        Type[] tools = [typeof(CreateTaskTool), typeof(UpdateTaskTool), typeof(ListTasksTool), typeof(GetTaskTool), typeof(CloseTaskTool), typeof(ReopenTaskTool)];

        foreach (Type tool in tools)
        {
            Type[] parameterTypes = [.. tool.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(static constructor => constructor.GetParameters())
                .Select(static parameter => parameter.ParameterType)];

            Assert.DoesNotContain(typeof(SprintStore), parameterTypes);
            Assert.DoesNotContain(parameterTypes, static type => type.IsAssignableTo(typeof(SprintStore)));
        }

        foreach (Type tool in tools.Take(4))
        {
            Assert.Contains(
                tool.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single().GetParameters(),
                static parameter => parameter.ParameterType == typeof(ISprintCatalog));
        }
    }
}
