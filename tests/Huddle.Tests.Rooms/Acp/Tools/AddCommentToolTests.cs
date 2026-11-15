using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Tests for <see cref="AddCommentTool"/> (#586): the comment it writes to a Task's timeline under the caller's name, and its refusals.</summary>
public sealed class AddCommentToolTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the tool over the harness, calling as Nova.</summary>
    private static AddCommentTool CreateTool(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return new AddCommentTool(harness.Service, harness.Store, harness.Directory, novaId, new FakePromptSource());
    }

    /// <summary>The Task as stored now.</summary>
    private static TaskItem Stored(TaskToolHarness harness, TaskItem task)
    {
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        return stored;
    }

    /// <summary>The tool is named <c>add_comment</c> and requires <c>taskId</c> and <c>text</c>, with the Markdown hint on <c>text</c>.</summary>
    [Fact]
    public async Task NameAndSchema_NameTheToolAndRequireTaskIdAndText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        AddCommentTool tool = CreateTool(harness);

        Assert.Equal("add_comment", tool.Name);
        JsonArray required = Assert.IsType<JsonArray>(tool.InputSchema["required"]);
        Assert.Equal(["taskId", "text"], required.Select(static node => node?.GetValue<string>()));
        JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);
        Assert.Equal("The comment, in Markdown. Several lines are fine.", properties["text"]?["description"]?.GetValue<string>());
    }

    /// <summary>A comment is saved with the caller as author and the clock as time, and its lines are kept as given.</summary>
    [Fact]
    public async Task InvokeAsync_MultiLineText_IsSavedWithCallerAsAuthor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["text"] = "Found it.\n\n- the cookie expires\n- the retry is missing" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Added a comment to {task.Id}.", result);
        TimelineEntry entry = Assert.Single(Stored(harness, task).Comments);
        Assert.Equal(TimelineEntryKind.Comment, entry.Kind);
        Assert.Equal("Nova", entry.Author);
        Assert.Equal(Noon, entry.At);
        Assert.Equal("Found it.\n\n- the cookie expires\n- the retry is missing", entry.Text);
    }

    /// <summary>A comment leaves the Task's description exactly as it was.</summary>
    [Fact]
    public async Task InvokeAsync_Comment_LeavesDescriptionUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        string before = Stored(harness, task).Description;
        AddCommentTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["text"] = "A remark." };

        _ = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(before, Stored(harness, task).Description);
        Assert.Single(Stored(harness, task).Comments);
    }

    /// <summary>Blank text is refused naming <c>text</c>, and nothing is written.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public async Task InvokeAsync_BlankText_IsRefusedNamingText(string? text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString() };
        if (text is not null)
        {
            arguments["text"] = text;
        }

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'text' is a required argument. Nothing was added.", result);
        Assert.Empty(Stored(harness, task).Comments);
    }

    /// <summary>A comment over the limit gets the service's refusal text and nothing is written.</summary>
    [Fact]
    public async Task InvokeAsync_OverLongText_IsRefusedWithTheServiceText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["text"] = new string('x', TaskService.MaxCommentLength + 1) };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("A comment can be at most 4000 characters; this one has 4001.", result);
        Assert.Empty(Stored(harness, task).Comments);
    }

    /// <summary>An unknown Task id is refused and nothing is written.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownTask_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        AddCommentTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = "PLAT-9999", ["text"] = "Hello." };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown task 'PLAT-9999'.", result);
    }
}
