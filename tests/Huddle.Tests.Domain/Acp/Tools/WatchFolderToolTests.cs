namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Covers FC §6.9's <c>watch_folder</c> tool: a thin wrapper around <see cref="FileChangeTracker.Subscribe"/>,
/// following <see cref="Agency.Huddle.App.Acp.Tools.FollowRoomTool"/>'s pattern. Each test asserts the tool
/// returns the tracker's own result text verbatim, since the texts themselves are pinned at the tracker
/// level in D6 (<see cref="FileChangeTracker"/>).
/// </summary>
public sealed class WatchFolderToolTests
{
    /// <summary>A missing <c>folder</c> argument is reported as text, never thrown - checked by the tool itself, before ever reaching the tracker.</summary>
    [Fact]
    public async Task Invoke_MissingArgument_ReturnsRequiredArgumentText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Equal("'folder' is a required argument.", result);
    }

    /// <summary>The tool's model-facing name is <c>watch_folder</c>.</summary>
    [Fact]
    public async Task Name_IsWatchFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        Assert.Equal("watch_folder", tool.Name);
    }

    /// <summary>The input schema requires a single string <c>folder</c> property, per FC §6.9.</summary>
    [Fact]
    public async Task InputSchema_RequiresFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var schema = tool.InputSchema;

        Assert.Equal("object", (string?)schema["type"]);
        Assert.Equal("string", (string?)schema["properties"]?["folder"]?["type"]);
        var required = Assert.IsType<JsonArray>(schema["required"]);
        Assert.Equal("folder", (string?)Assert.Single(required));
    }

    /// <summary>An entry that resolves to the caller's own Work Dir is reported "Already watching ... Nothing to do." - the same text <see cref="FileChangeTracker.Subscribe"/> itself returns.</summary>
    [Fact]
    public async Task Invoke_OwnWorkDir_ReturnsAlreadyWatchingNothingToDo()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());
        var ownFullPath = new AgentPaths(fixture.Options).WorkDir("Nova");

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Nova" }, ct);

        Assert.Equal($"Already watching 'Nova' ({ownFullPath}). Nothing to do.", result);
    }

    /// <summary>An entry naming one of Huddle's own reserved folders returns the resolver's own reason text, unchanged.</summary>
    [Fact]
    public async Task Invoke_DoesNotResolve_ReturnsResolverReasonVerbatim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "rooms" }, ct);

        Assert.Equal("'rooms' holds Huddle's own data, not working files.", result);
    }

    /// <summary>A new subscription returns the tracker's own "Now watching" text, verbatim.</summary>
    [Fact]
    public async Task Invoke_NewFolder_ReturnsNowWatchingTextVerbatim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());
        var expectedFullPath = Path.Combine(fixture.Options.Value.DataDir, "Shared");

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Shared" }, ct);

        Assert.Equal(
            $"Now watching 'Shared' ({expectedFullPath}). From your next Turn, files added, changed or deleted there are listed at the top of your prompt. This lasts until you call unwatch_folder, including after a restart.",
            result);
    }

    /// <summary>Watching the same folder a second time is a no-op that says so.</summary>
    [Fact]
    public async Task Invoke_AlreadySubscribed_ReturnsNothingToDo()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());
        _ = await tool.InvokeAsync(new JsonObject { ["folder"] = "Shared" }, ct);

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Shared" }, ct);

        Assert.Contains("Already watching 'Shared'", result, StringComparison.Ordinal);
        Assert.Contains("Nothing to do.", result, StringComparison.Ordinal);
    }

    /// <summary>A <c>folder</c> that is not JSON text is refused with the <c>'folder' must be text.</c> refusal rather than throwing an <see cref="InvalidCastException"/>.</summary>
    [Fact]
    public async Task Invoke_NonStringFolder_RefusesAsText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await FolderWatchToolFixture.CreateAsync(ct);
        var tool = new WatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = 42 }, ct);

        Assert.Equal("'folder' must be text.", result);
    }
}