using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Slice 1 of #669 (decision D-13): a tool call that names the removed Review state is refused with one message that teaches the new model.</summary>
public sealed class ReviewStateRefusalToolTests
{
    /// <summary>The exact refusal every tool returns for Review (D-13).</summary>
    private const string Refusal =
        "Huddle has no Review state. To ask for a review, create a Task tagged review, assigned to the reviewer, as a child of this Task, and add it to this Task's blocked_by.";

    /// <summary><c>update_task</c> naming Review, in any case, returns the D-13 refusal and leaves the status alone.</summary>
    [Theory]
    [InlineData("Review")]
    [InlineData("review")]
    [InlineData("REVIEW")]
    public async Task UpdateTask_StatusReview_ReturnsReviewRefusal(string status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = harness.CreateTask("Support SAML login", status: TaskState.InProgress, assignee: "Kai");
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = status };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(Refusal, result);
    }

    /// <summary><c>create_task</c> naming Review, in any case, returns the D-13 refusal and writes nothing.</summary>
    [Theory]
    [InlineData("Review")]
    [InlineData("review")]
    public async Task CreateTask_StatusReview_ReturnsReviewRefusalAndWritesNothing(string status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new() { ["title"] = "Support SAML login", ["team"] = "Platform", ["status"] = status };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(Refusal, result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary><c>create_task</c>'s status enum no longer offers Review (D-13).</summary>
    [Fact]
    public async Task CreateTask_InputSchema_StatusEnumHasNoReview()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());

        JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);
        JsonObject status = Assert.IsType<JsonObject>(properties["status"]);
        JsonArray allowed = Assert.IsType<JsonArray>(status["enum"]);

        Assert.Equal(["Backlog", "To Do", "In Progress", "Done"], allowed.Select(node => node?.GetValue<string>()).ToArray());
    }

    /// <summary><c>list_tasks</c> with a status filter naming Review, in any case, returns the D-13 refusal.</summary>
    [Theory]
    [InlineData("Review")]
    [InlineData("review")]
    public async Task ListTasks_StatusFilterReview_ReturnsReviewRefusal(string status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.CreateTask("Support SAML login", status: TaskState.InProgress);
        ListTasksTool tool = new(harness.Store, harness.Directory, harness.Personas, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new() { ["status"] = new JsonArray(status) };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(Refusal, result);
    }
}
