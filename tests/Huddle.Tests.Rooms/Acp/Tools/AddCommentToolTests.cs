using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Tests for <see cref="AddCommentTool"/> (#586): the comment it writes to a Task's timeline under the caller's name, and its refusals.</summary>
public sealed class AddCommentToolTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private const string NoOneWoken = "No one is woken: Mention an Agent with @Name to wake them.";

    /// <summary>Builds the tool over the harness, calling as Nova.</summary>
    private static AddCommentTool CreateTool(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return new AddCommentTool(harness.Service, harness.Store, harness.Triggers, harness.Directory, novaId, new FakePromptSource());
    }

    /// <summary>Registers an Agent Persona (with <paramref name="alias"/>) and its directory user, so a Mention of it resolves.</summary>
    private static async Task AddAgentAsync(TaskToolHarness harness, string name, string alias, CancellationToken ct)
    {
        harness.Personas.Add(new PersonaIdentity(name, name, alias, ["Platform"]), $"You are {name}.");
        _ = await harness.Directory.UpsertAgentUserAsync(name, null, ct);
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

        Assert.Equal($"Added a comment to {task.Id}. {NoOneWoken}", result);
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

    /// <summary>
    /// The reply names the Agents the comment Mentions, in first-mention order: one, two, and three or more joined with a comma and "and" (no Oxford comma),
    /// by Name in any case or by Alias, each Agent once, and without the author, who is never woken by their own comment (spec 5.6).
    /// </summary>
    /// <param name="text">The comment, written by Nova.</param>
    /// <param name="wakes">The wake sentence the reply ends with.</param>
    [Theory]
    [InlineData("@Rex please look.", "Wakes Rex.")]
    [InlineData("@REX please look.", "Wakes Rex.")]
    [InlineData("@rexy please look.", "Wakes Rex.")]
    [InlineData("@Rex and @Kai.", "Wakes Rex and Kai.")]
    [InlineData("@Quinn, then @Rex, then @Kai.", "Wakes Quinn, Rex and Kai.")]
    [InlineData("@Nova @Rex", "Wakes Rex.")]
    [InlineData("@Rex @rexy @REX", "Wakes Rex.")]
    public async Task InvokeAsync_Mentions_RepliesWithTheWokenNamesInMentionOrder(string text, string wakes)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        await AddAgentAsync(harness, "Rex", "rexy", ct);
        await AddAgentAsync(harness, "Quinn", "quinn", ct);
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = text }, ct);

        Assert.Equal($"Added a comment to {task.Id}. {wakes}", result);
        Assert.Equal(text, Assert.Single(Stored(harness, task).Comments).Text);
    }

    /// <summary>
    /// A comment that wakes no one says so and how to change that: no Mention at all, only the author, <c>@all</c> (which wakes no one in a Task comment),
    /// a name nobody has, a Human, and an <c>@</c> inside an address.
    /// </summary>
    /// <param name="text">The comment, written by Nova.</param>
    [Theory]
    [InlineData("Found it.")]
    [InlineData("@Nova, a note to myself.")]
    [InlineData("@all please read.")]
    [InlineData("@all and @ALL again.")]
    [InlineData("@Zed look at this.")]
    [InlineData("@You see the Tasks page.")]
    [InlineData("Write to rex@example.com.")]
    public async Task InvokeAsync_NoOneWoken_RepliesHowToMention(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        await AddAgentAsync(harness, "Rex", "rexy", ct);
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = text }, ct);

        Assert.Equal($"Added a comment to {task.Id}. {NoOneWoken}", result);
        _ = Assert.Single(Stored(harness, task).Comments);
    }

    /// <summary>
    /// With wake-ups turned off (read live) a Mention wakes no one, and the reply gives that reason instead of telling the Agent to Mention someone.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WakeUpsOff_RepliesThatWakeUpsAreOff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        await AddAgentAsync(harness, "Rex", "rexy", ct);
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = CreateTool(harness);
        harness.Options.Value.Tasks.WakeEnabled = false;

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = "@Rex please look." }, ct);

        Assert.Equal($"Added a comment to {task.Id}. No one is woken: wake-ups are off.", result);
        _ = Assert.Single(Stored(harness, task).Comments);
    }
}
