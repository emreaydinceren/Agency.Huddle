using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="AddEvidenceTool"/> (Task 50): the Evidence line it builds from the caller, the clock and the
/// caller's tool-call log, and its refusals.
/// </summary>
public sealed class AddEvidenceToolTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the tool over the harness, calling as Nova.</summary>
    private static AddEvidenceTool CreateTool(TaskToolHarness harness, ToolCallLogWriter log)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return new AddEvidenceTool(harness.Service, harness.Store, log, harness.Directory, harness.Clock, novaId, new FakePromptSource());
    }

    /// <summary>Appends one tool call to Nova's log at <paramref name="time"/>.</summary>
    private static ToolCallLogRef Log(ToolCallLogWriter log, DateTimeOffset time, string tool, string arguments) =>
        log.Append("Nova", new ToolCallLogEntry(time, "room", null, tool, arguments, "completed", 5));

    /// <summary>The Evidence lines now on the Task.</summary>
    private static IReadOnlyList<EvidenceLine> Evidence(TaskToolHarness harness, TaskItem task)
    {
        string? text = harness.Store.ReadText(task.Id);
        Assert.NotNull(text);
        return EvidenceLine.ParseSection(text);
    }

    /// <summary>A command with a matching log entry gets the caller's name, the clock time, the exit code and the log reference.</summary>
    [Fact]
    public async Task InvokeAsync_CommandWithLoggedCall_FillsInLogReference()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        ToolCallLogWriter log = new(harness.Options);
        _ = Log(log, Noon.AddMinutes(-5), "Bash", "dotnet test Huddle.slnx");
        ToolCallLogRef second = Log(log, Noon.AddMinutes(-2), "Bash", "dotnet test Huddle.slnx --");
        _ = Log(log, Noon.AddMinutes(-1), "Bash", "git status");
        AddEvidenceTool tool = CreateTool(harness, log);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["exitCode"] = 0 };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Added evidence to {task.Id}.", result);
        EvidenceLine line = Assert.Single(Evidence(harness, task));
        Assert.Equal("Nova", line.Author);
        Assert.Equal(Noon, line.Time);
        Assert.Equal("dotnet test", line.Command);
        Assert.Equal(0, line.ExitCode);
        Assert.Equal(second.RelativePath, line.LogPath);
        Assert.Equal(second.Line, line.LogLine);
    }

    /// <summary>A command nothing in the log matches gets no log reference.</summary>
    [Fact]
    public async Task InvokeAsync_CommandWithoutLoggedCall_HasNoLogReference()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        ToolCallLogWriter log = new(harness.Options);
        _ = Log(log, Noon.AddMinutes(-1), "Bash", "git status");
        AddEvidenceTool tool = CreateTool(harness, log);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["exitCode"] = 1 };

        _ = await tool.InvokeAsync(arguments, ct);

        EvidenceLine line = Assert.Single(Evidence(harness, task));
        Assert.Null(line.LogPath);
        Assert.Null(line.LogLine);
        Assert.Equal(1, line.ExitCode);
    }

    /// <summary>An explicit logRef is used as given and the log is not searched.</summary>
    [Fact]
    public async Task InvokeAsync_ExplicitLogRef_IsUsedAsGiven()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        ToolCallLogWriter log = new(harness.Options);
        _ = Log(log, Noon.AddMinutes(-1), "Bash", "dotnet test");
        AddEvidenceTool tool = CreateTool(harness, log);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["logRef"] = "logs/tool-calls/Nova/2026-10-01.jsonl#L7" };

        _ = await tool.InvokeAsync(arguments, ct);

        EvidenceLine line = Assert.Single(Evidence(harness, task));
        Assert.Equal("logs/tool-calls/Nova/2026-10-01.jsonl", line.LogPath);
        Assert.Equal(7, line.LogLine);
    }

    /// <summary>A note with no command is kept as a free-text note line.</summary>
    [Fact]
    public async Task InvokeAsync_NoteOnly_AddsNoteLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["note"] = "Checked on staging." };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Added evidence to {task.Id}.", result);
        EvidenceLine line = Assert.Single(Evidence(harness, task));
        Assert.Equal("Checked on staging.", line.RawText);
        Assert.Equal(string.Empty, line.Command);
    }

    /// <summary>A command with a note carries the note in the command field, with any field separator in it replaced.</summary>
    [Fact]
    public async Task InvokeAsync_CommandAndNote_NoteJoinsCommand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["note"] = "green | on CI" };

        _ = await tool.InvokeAsync(arguments, ct);

        EvidenceLine line = Assert.Single(Evidence(harness, task));
        Assert.Equal("dotnet test (green / on CI)", line.Command);
    }

    /// <summary>An unknown Task id is refused and nothing is written.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownTask_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        JsonObject arguments = new() { ["taskId"] = "PLAT-9999", ["command"] = "dotnet test" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-9999'.", result);
    }

    /// <summary>A Task file edited on disk after the store last indexed it is reported as changed by someone else, with nothing added.</summary>
    [Fact]
    public async Task InvokeAsync_FileChangedOnDisk_ReportsConflict()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        await File.AppendAllTextAsync(task.Path, "\n- 2026-10-08T11:00:00Z | You | edited by hand\n", ct);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} was changed by someone else; please try again.", result);
    }

    /// <summary>A call with neither a command nor a note is refused.</summary>
    [Fact]
    public async Task InvokeAsync_NoCommandOrNote_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Give a command or a note.", result);
        Assert.Empty(Evidence(harness, task));
    }

    /// <summary>A logRef that is not path#Lnumber is refused.</summary>
    [Fact]
    public async Task InvokeAsync_MalformedLogRef_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["logRef"] = "somewhere" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'logRef' must look like path#L12.", result);
        Assert.Empty(Evidence(harness, task));
    }

    /// <summary>The tool is named add_evidence and requires only the Task id.</summary>
    [Fact]
    public async Task Metadata_NamesToolAndRequiresTaskId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddEvidenceTool tool = CreateTool(harness, new ToolCallLogWriter(harness.Options));

        Assert.Equal("add_evidence", tool.Name);
        Assert.Equal("[\"taskId\"]", tool.InputSchema["required"]?.ToJsonString());
    }
}
