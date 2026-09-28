namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Covers FC §6.9's <c>unwatch_folder</c> tool: a thin wrapper around <see cref="FileChangeTracker.Unsubscribe"/>,
/// following <see cref="Agency.Huddle.App.Acp.Tools.UnfollowRoomTool"/>'s pattern. Each test asserts the tool
/// returns the tracker's own result text verbatim.
/// </summary>
public sealed class UnwatchFolderToolTests
{
    /// <summary>A missing <c>folder</c> argument is reported as text, never thrown - checked by the tool itself, before ever reaching the tracker.</summary>
    [Fact]
    public async Task Invoke_MissingArgument_ReturnsRequiredArgumentText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Equal("'folder' is a required argument.", result);
    }

    /// <summary>The tool's model-facing name is <c>unwatch_folder</c>.</summary>
    [Fact]
    public async Task Name_IsUnwatchFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        Assert.Equal("unwatch_folder", tool.Name);
    }

    /// <summary>The input schema requires a single string <c>folder</c> property, per FC §6.9.</summary>
    [Fact]
    public async Task InputSchema_RequiresFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var schema = tool.InputSchema;

        Assert.Equal("object", (string?)schema["type"]);
        Assert.Equal("string", (string?)schema["properties"]?["folder"]?["type"]);
        var required = Assert.IsType<JsonArray>(schema["required"]);
        Assert.Equal("folder", (string?)Assert.Single(required));
    }

    /// <summary>The caller's own Work Dir cannot be unwatched, and the tool says so verbatim.</summary>
    [Fact]
    public async Task Invoke_OwnWorkDir_ReturnsAlwaysWatchedText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Nova" }, ct);

        Assert.Equal("Your own folder is always watched.", result);
    }

    /// <summary>A folder that was never subscribed is reported "You are not watching '...'." verbatim.</summary>
    [Fact]
    public async Task Invoke_NotWatched_ReturnsNotWatchingTextVerbatim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Shared" }, ct);

        Assert.Equal("You are not watching 'Shared'.", result);
    }

    /// <summary>A previously subscribed folder is unsubscribed and the tool reports the tracker's own "Stopped watching" text, verbatim.</summary>
    [Fact]
    public async Task Invoke_Subscribed_StopsWatchingVerbatim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await Fixture.CreateAsync(ct);
        fixture.Tracker.Subscribe("Nova", "Shared");
        var tool = new UnwatchFolderTool(fixture.Tracker, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["folder"] = "Shared" }, ct);

        Assert.Equal("Stopped watching 'Shared'.", result);
    }

    /// <summary>Bundles a real <see cref="FileChangeTracker"/> and its collaborators over an isolated <see cref="TempDataDir"/>, matching <c>WatchFolderToolTests.Fixture</c>'s shape.</summary>
    private sealed class Fixture(TempDataDir dataDir, PersonaStore personas, FileChangeTracker tracker, IOptions<TeamOptions> options) : IDisposable
    {
        /// <summary>The <see cref="FileChangeTracker"/> under test.</summary>
        public FileChangeTracker Tracker { get; } = tracker;

        /// <summary>The bound options this fixture's tracker resolves paths against.</summary>
        public IOptions<TeamOptions> Options { get; } = options;

        /// <summary>Builds a fixture with a real <see cref="SqliteTeamDirectory"/>, <see cref="PersonaStore"/>, <see cref="FileStateStore"/> and <see cref="WatchedFolderResolver"/>.</summary>
        /// <param name="ct">Cancels the Team Directory's initialisation.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct)
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = dataDir.Options();
            SqliteTeamDirectory directory = new(options);
            await directory.InitializeAsync("You", ct);

            TeammatePaths teammatePaths = new(options);
            PersonaStore personas = new(
                teammatePaths,
                new PersonaModelStore(options),
                new PersonaEffortStore(options),
                NullLogger<PersonaStore>.Instance);
            FileStateStore store = new(options, NullLogger<FileStateStore>.Instance);
            WatchedFolderResolver resolver = new(options, teammatePaths);
            FileChangeTracker tracker = new(store, personas, directory, resolver, options, teammatePaths, NullLogger<FileChangeTracker>.Instance);

            // Registered so "Nova" resolves via the Teammate-Name rule (FC §6.3) to its own Work Dir,
            // matching the caller Name every tool test below binds at construction.
            personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", []), "You are Nova.");

            return new Fixture(dataDir, personas, tracker, options);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            personas.Dispose();
            dataDir.Dispose();
        }
    }
}
