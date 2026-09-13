namespace Agency.Huddle.Tests.Acp;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Golden-output safety net for task T1.1. Every model-facing string produced by
/// <see cref="SystemPromptComposer"/>, <see cref="GetHelpTool"/>, the four real chat tools, and
/// <see cref="PersonaRunner.BuildPrompt(PersonaRunner.WorkItem)"/> is captured here as committed text
/// under <c>Acp/Golden</c>, so the later move of these strings into a JSON config file can prove it
/// changed no behaviour, byte-for-byte.
/// </summary>
/// <remarks>
/// These tests, and the golden files beside them, are temporary: task T1.11 deletes both once
/// equivalent assertions live elsewhere. A test whose golden file is missing writes it and fails with
/// a message explaining that, so the first run of a freshly added golden test seeds its own file for
/// inspection before it is committed.
/// </remarks>
public sealed class PromptGoldenTests
{
    /// <summary>
    /// The five real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
    ];

    /// <summary>Pins <see cref="SystemPromptComposer.Compose"/>'s output for a plain Persona.</summary>
    [Fact]
    public void SystemPrompt_MatchesGolden()
    {
        var persona = new Persona("Nova", "You are Nova.");

        var actual = SystemPromptComposer.Compose(persona, new FakeHookSource(), ToolNames);

        AssertMatchesGolden("systemPrompt.txt", actual);
    }

    /// <summary>
    /// Pins <see cref="GetHelpTool"/>'s rendered body when constructed with the four real chat tools,
    /// exactly as <c>GetHelpToolTests</c> constructs them.
    /// </summary>
    [Fact]
    public async Task GetHelp_MatchesGolden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (tool, _) = await BuildToolsAsync(ct);

        var actual = await tool.InvokeAsync(new JsonObject(), ct);

        AssertMatchesGolden("getHelp.txt", actual);
    }

    /// <summary>Pins every <see cref="IAppTool.Description"/> across get_help and the four chat tools.</summary>
    [Fact]
    public async Task ToolDescriptions_MatchGolden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (getHelp, others) = await BuildToolsAsync(ct);

        var lines = new List<string> { $"{getHelp.Name}: {getHelp.Description}" };
        lines.AddRange(others.Select(tool => $"{tool.Name}: {tool.Description}"));
        var actual = string.Join('\n', lines);

        AssertMatchesGolden("toolDescriptions.txt", actual);
    }

    /// <summary>Pins <see cref="PersonaRunner.BuildPrompt"/> for a turn with no catch-up messages.</summary>
    [Fact]
    public void BuildPrompt_NoCatchUp_MatchesGolden()
    {
        var item = new PersonaRunner.WorkItem("room-1", "Nova & You", "You", "hello there", []);

        var actual = PersonaRunner.BuildPrompt(item);

        AssertMatchesGolden("turnPromptPlain.txt", actual);
    }

    /// <summary>Pins <see cref="PersonaRunner.BuildPrompt"/> for a turn carrying two catch-up messages.</summary>
    [Fact]
    public void BuildPrompt_WithCatchUp_MatchesGolden()
    {
        PersonaRunner.CaughtUpMessage[] missed =
        [
            new("Alice", "did anyone see the release notes?"),
            new("Bob", "I have not, checking now"),
        ];
        var item = new PersonaRunner.WorkItem("room-2", "Nova & Friends", "Bob", "@Nova are you there?", missed);

        var actual = PersonaRunner.BuildPrompt(item);

        AssertMatchesGolden("turnPromptCatchUp.txt", actual);
    }

    /// <summary>
    /// Builds <see cref="GetHelpTool"/> together with the four real chat tools it reports, using the
    /// same narrow construction <c>GetHelpToolTests</c> uses: each tool's <see cref="IAppTool.Description"/>
    /// is a plain property, so nothing here needs to actually invoke a tool, only resolve its
    /// constructor dependencies.
    /// </summary>
    /// <param name="ct">Cancels directory initialisation.</param>
    /// <returns>The get_help tool, and the four tools it was built from, in the order it reports them.</returns>
    private static async Task<(GetHelpTool GetHelp, IReadOnlyList<IAppTool> Others)> BuildToolsAsync(CancellationToken ct)
    {
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource();
        var chat = new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), NullLogger<ChatService>.Instance);

        IAppTool[] others =
        [
            new ListAgentsTool(directory, new FakeAgentGateway(), personaStore, new FakeHookSource()),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource, new FakeHookSource()),
            new InviteAgentTool(chat, directory, aliasSource, new FakeHookSource()),
            new PostMessageTool(chat, "caller-id", new FakeHookSource()),
        ];
        var getHelp = new GetHelpTool(others, new FakeHookSource(), "mcp__team__");

        return (getHelp, others);
    }

    /// <summary>
    /// Compares <paramref name="actual"/>, byte-for-byte after line-ending normalisation, against the
    /// committed golden file named <paramref name="fileName"/> under <c>Acp/Golden</c>. A missing
    /// golden file is seeded from <paramref name="actual"/> and the test fails, so a first run
    /// produces something to inspect before it is committed.
    /// </summary>
    /// <param name="fileName">The golden file's name, under <c>tests/Huddle.Tests/Acp/Golden</c>.</param>
    /// <param name="actual">The freshly produced text to compare.</param>
    private static void AssertMatchesGolden(string fileName, string actual)
    {
        var normalizedActual = actual.Replace("\r\n", "\n", StringComparison.Ordinal);
        var path = GoldenFilePath(fileName);

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalizedActual);
            Assert.Fail($"Golden file '{fileName}' did not exist and has been seeded at '{path}'. Inspect it, then re-run.");
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(expected, normalizedActual);
    }

    /// <summary>
    /// Locates a golden file under the repository's <c>tests/Huddle.Tests/Acp/Golden</c> directory by
    /// walking up from the test binary's own directory until <c>Huddle.slnx</c> is found, the same
    /// approach <c>TeamWebApplicationFactory.FindAppContentRoot</c> uses to locate <c>src/Huddle.App</c>.
    /// </summary>
    /// <param name="fileName">The golden file's name.</param>
    /// <returns>The golden file's full path, whether or not it exists yet.</returns>
    private static string GoldenFilePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return Path.Combine(directory.FullName, "tests", "Huddle.Tests", "Acp", "Golden", fileName);
    }
}
