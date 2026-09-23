namespace Agency.Huddle.Tests.Acp;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Golden-output safety net for task T1.1. Every model-facing string produced by
/// <see cref="SystemPromptComposer"/>, <see cref="GetHelpTool"/>, the six real chat tools, and
/// <see cref="PersonaRunner.BuildPrompt(PersonaRunner.WorkItem, Agency.Huddle.App.Prompts.IPromptSource)"/> is captured here as committed text
/// under <c>Acp/Golden</c>, so the later move of these strings into a JSON config file can prove it
/// changed no behaviour, byte-for-byte.
/// </summary>
/// <remarks>
/// <para>
/// These tests, and the golden files beside them, are permanent: a byte-level regression net on every
/// model-facing string this application composes, sitting above the per-prompt checks in
/// <c>Prompts/PromptCatalogTests.cs</c> and <c>Prompts/PromptValidatorTests.cs</c> and the shipped-default
/// checks in <c>Prompts/PromptDefaultsTests.cs</c>. Those other suites can each pass while a change to how
/// several prompts are joined, wrapped or spaced still alters what a model actually reads; only a full
/// composed-output diff catches that. (Task T1.11 originally scheduled these for deletion once
/// equivalent assertions existed elsewhere; that decision was reversed — the per-prompt and
/// per-default checks turned out to complement this byte-level net rather than replace it.)
/// </para>
/// <para>
/// A test whose golden file is missing writes it and fails with a message explaining that, so the
/// first run of a freshly added golden test seeds its own file for inspection before it is committed.
/// </para>
/// <para>
/// <b>Regenerating a golden file on purpose.</b> A red test here after a prompt's <see cref="Agency.Huddle.App.Prompts.PromptDefinition.Default"/>
/// changes is not automatically a bug — it may simply mean the composed output was meant to change.
/// To accept a deliberate wording change: delete the affected file(s) under
/// <c>tests/Huddle.Tests/Acp/Golden</c>, re-run the test project so each now-missing golden file is
/// reseeded from the current (deliberately changed) output, inspect every reseeded file by hand to
/// confirm it reads the way you intended, and commit the updated <c>.txt</c> files alongside the
/// production change that caused them to move. Never hand-edit a golden file directly — always let a
/// test reseed it, so what is committed is provably what the code actually produces today.
/// </para>
/// </remarks>
public sealed class PromptGoldenTests
{
    /// <summary>
    /// The seven real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
        "mcp__team__follow_room",
        "mcp__team__unfollow_room",
    ];

    /// <summary>
    /// The same seven tool names as <see cref="ToolNames"/>, bare, for an Adapter profile whose
    /// <see cref="AdapterProfile.UsesToolNamePrefix"/> is <see langword="false"/> (Spec §6.4).
    /// </summary>
    private static readonly IReadOnlyList<string> UnprefixedToolNames =
    [
        "get_help",
        "list_agents",
        "create_room",
        "invite_agent",
        "post_message",
        "follow_room",
        "unfollow_room",
    ];

    /// <summary>Pins <see cref="SystemPromptComposer.Compose(Persona, IPromptSource, string, IReadOnlyList{string})"/>'s output for a plain Persona.</summary>
    [Fact]
    public void SystemPrompt_MatchesGolden()
    {
        var persona = new Persona("Nova", "You are Nova.");

        var actual = SystemPromptComposer.Compose(persona, new FakePromptSource(), "mcp__team__get_help", ToolNames);

        AssertMatchesGolden("systemPrompt.txt", actual);
    }

    /// <summary>
    /// Pins <see cref="SystemPromptComposer.Compose(Persona, IPromptSource, string, IReadOnlyList{string})"/>'s output for an Adapter profile whose
    /// <see cref="AdapterProfile.UsesToolNamePrefix"/> is <see langword="false"/>: every tool name,
    /// including the help tool, is bare. Spec §4 (P6) requires <see cref="SystemPrompt_MatchesGolden"/>'s
    /// golden to stay byte-identical alongside this one.
    /// </summary>
    [Fact]
    public void SystemPrompt_Unprefixed_MatchesGolden()
    {
        var persona = new Persona("Nova", "You are Nova.");

        var actual = SystemPromptComposer.Compose(persona, new FakePromptSource(), "get_help", UnprefixedToolNames);

        AssertMatchesGolden("systemPrompt.unprefixed.txt", actual);
        Assert.DoesNotContain("mcp__", actual, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins <see cref="SystemPromptComposer.Compose(Persona, IPromptSource, string, IReadOnlyList{string}, IReadOnlyList{Skill}, string)"/>'s Skills overload for a Persona holding one real
    /// shipped Skill (Spec §6.4). The Skill's description is read from <see cref="SkillCatalog.All"/>
    /// via <see cref="SkillValidator.Validate"/> rather than pasted in, so this test does not pin
    /// <c>team-building</c>'s wording — only the seeded golden file does, and only that file needs
    /// updating if the shipped description ever changes.
    /// </summary>
    [Fact]
    public void SystemPrompt_WithSkills_MatchesGolden()
    {
        var persona = new Persona("Nova", "You are Nova.");
        var skill = TeamBuildingSkill();

        var actual = SystemPromptComposer.Compose(
            persona, new FakePromptSource(), "mcp__team__get_help", ToolNames, [skill], "mcp__team__read_skill");

        AssertMatchesGolden("systemPrompt.skills.txt", actual);
    }

    /// <summary>
    /// Pins that <see cref="SystemPromptComposer.Compose(Persona, IPromptSource, string, IReadOnlyList{string}, IReadOnlyList{Skill}, string)"/>'s Skills overload, called with an empty
    /// Skill list, produces exactly today's <c>systemPrompt.txt</c> golden (Spec §6.4: the skills block
    /// is omitted entirely for a Persona with no resolved Skills).
    /// </summary>
    [Fact]
    public void SystemPrompt_NoSkills_UnchangedFromExistingGolden()
    {
        var persona = new Persona("Nova", "You are Nova.");

        var actual = SystemPromptComposer.Compose(
            persona, new FakePromptSource(), "mcp__team__get_help", ToolNames, [], "mcp__team__read_skill");

        AssertMatchesGolden("systemPrompt.txt", actual);
    }

    /// <summary>
    /// Pins <see cref="GetHelpTool"/>'s rendered body when constructed with the six real chat tools,
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

    /// <summary>Pins every <see cref="IAppTool.Description"/> across get_help and the six chat tools.</summary>
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

        var actual = PersonaRunner.BuildPrompt(item, new FakePromptSource());

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

        var actual = PersonaRunner.BuildPrompt(item, new FakePromptSource());

        AssertMatchesGolden("turnPromptCatchUp.txt", actual);
    }

    /// <summary>
    /// Pins <see cref="PersonaRunner.BuildPrompt"/> for a Greeting Turn (Spec §6.14): no triggering
    /// Message and no catch-up context, built through the same path the runner uses to queue one.
    /// </summary>
    [Fact]
    public void TurnPromptGreeting_MatchesGolden()
    {
        var item = new PersonaRunner.WorkItem(
            "room-3", "Chief of Staff", string.Empty, string.Empty, [], PersonaRunner.WorkItemKind.Greeting);

        var actual = PersonaRunner.BuildPrompt(item, new FakePromptSource());

        AssertMatchesGolden("turnPromptGreeting.txt", actual);
    }

    /// <summary>
    /// Pins <see cref="PersonaRunner.BuildPrompt"/> for a Turn carrying a non-empty File Changes
    /// report together with Catch-up context, per FC §6.8: the File Changes block goes first, ahead
    /// of Catch-up, then exactly what today's Catch-up path already writes. The golden file was
    /// written by hand from FC §6.8's sample, not seeded from this test's own output.
    /// </summary>
    [Fact]
    public void BuildPrompt_WithFileChangesAndCatchUp_MatchesGolden()
    {
        FileChangesReport report = new(
            [
                new FileChange(FileChangeKind.Changed, @"E:\Huddle\App_Data\work\Nova\memory\launch-date.md"),
                new FileChange(FileChangeKind.Added, @"E:\Huddle\App_Data\Shared\pricing\2026.md"),
                new FileChange(FileChangeKind.Deleted, @"E:\Huddle\App_Data\work\Nova\notes\old.md"),
            ],
            NotListed: 12,
            Unchecked: [@"E:\Huddle\App_Data\Shared\big-folder"],
            MaxFilesPerFolder: 5000);
        PersonaRunner.CaughtUpMessage[] missed =
        [
            new("Alice", "did anyone see the release notes?"),
            new("Bob", "I have not, checking now"),
        ];
        var item = new PersonaRunner.WorkItem("room-2", "Nova & Friends", "Bob", "@Nova are you there?", missed) with { FileChanges = report };

        var actual = PersonaRunner.BuildPrompt(item, new FakePromptSource());

        AssertMatchesGolden("turnPromptFileChanges.txt", actual);
    }

    /// <summary>
    /// An empty (or absent) File Changes report leaves <c>turnPromptPlain.txt</c> and
    /// <c>turnPromptCatchUp.txt</c> byte-identical to today's output: "absent means unchanged".
    /// </summary>
    [Fact]
    public void BuildPrompt_EmptyReport_ByteIdenticalToExistingGoldens()
    {
        var plainWithEmpty = new PersonaRunner.WorkItem("room-1", "Nova & You", "You", "hello there", []) with { FileChanges = FileChangesReport.Empty };
        AssertMatchesGolden("turnPromptPlain.txt", PersonaRunner.BuildPrompt(plainWithEmpty, new FakePromptSource()));

        var plainWithNull = new PersonaRunner.WorkItem("room-1", "Nova & You", "You", "hello there", []) with { FileChanges = null };
        AssertMatchesGolden("turnPromptPlain.txt", PersonaRunner.BuildPrompt(plainWithNull, new FakePromptSource()));

        PersonaRunner.CaughtUpMessage[] missed =
        [
            new("Alice", "did anyone see the release notes?"),
            new("Bob", "I have not, checking now"),
        ];
        var catchUpWithEmpty = new PersonaRunner.WorkItem("room-2", "Nova & Friends", "Bob", "@Nova are you there?", missed) with { FileChanges = FileChangesReport.Empty };
        AssertMatchesGolden("turnPromptCatchUp.txt", PersonaRunner.BuildPrompt(catchUpWithEmpty, new FakePromptSource()));

        var catchUpWithNull = new PersonaRunner.WorkItem("room-2", "Nova & Friends", "Bob", "@Nova are you there?", missed) with { FileChanges = null };
        AssertMatchesGolden("turnPromptCatchUp.txt", PersonaRunner.BuildPrompt(catchUpWithNull, new FakePromptSource()));
    }

    /// <summary>A Greeting Turn never carries a File Changes block, even with a non-empty report.</summary>
    [Fact]
    public void BuildPrompt_Greeting_HasNoFileChangesBlock()
    {
        FileChangesReport report = new(
            [new FileChange(FileChangeKind.Changed, @"E:\Huddle\App_Data\work\Nova\memory\launch-date.md")],
            NotListed: 0,
            Unchecked: []);
        var item = new PersonaRunner.WorkItem(
            "room-3", "Chief of Staff", string.Empty, string.Empty, [], PersonaRunner.WorkItemKind.Greeting) with { FileChanges = report };

        var actual = PersonaRunner.BuildPrompt(item, new FakePromptSource());

        AssertMatchesGolden("turnPromptGreeting.txt", actual);
    }

    /// <summary>
    /// Builds <see cref="GetHelpTool"/> together with the six real chat tools it reports, using the
    /// same narrow construction <c>GetHelpToolTests</c> uses: each tool's <see cref="IAppTool.Description"/>
    /// is a plain property, so nothing here needs to actually invoke a tool, only resolve its
    /// constructor dependencies.
    /// </summary>
    /// <param name="ct">Cancels directory initialisation.</param>
    /// <returns>The get_help tool, and the six tools it was built from, in the order it reports them.</returns>
    private static async Task<(GetHelpTool GetHelp, IReadOnlyList<IAppTool> Others)> BuildToolsAsync(CancellationToken ct)
    {
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource();
        var proposals = new ProposalStore(events);
        var options = Options.Create(new TeamOptions());
        var chat = new ChatService(directory, store, events, aliasSource, options, proposals, NullLogger<ChatService>.Instance);

        var gateway = new FakeAgentGateway();
        var checker = new CandidateChecker(personaStore, directory, gateway);
        var follows = new RoomFollows();
        IAppTool[] others =
        [
            new ListAgentsTool(directory, gateway, personaStore, new FakePromptSource()),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource, new FakePromptSource()),
            new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource()),
            new PostMessageTool(chat, "caller-id", new FakePromptSource()),
            new FollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new ValidateTeammateTool(checker, new FakePromptSource()),
            new ProposeTeammatesTool(proposals, checker, personaStore, directory, options, TimeProvider.System, "test-agent", new FakePromptSource()),
        ];

        // GetHelpTool lists only the ungated tools (those not in SkillGrants.Grantable).
        // Skill-gated tools (validate_teammate, propose_teammates) are only offered to a Persona holding a Skill that lists them.
        // A Persona with no Skills should not see them in get_help.
        var ungatedTools = others.Where(tool => !SkillGrants.Grantable.Contains(tool.Name, StringComparer.Ordinal)).ToList();
        var getHelp = new GetHelpTool(ungatedTools, new FakePromptSource(), "mcp__team__");

        return (getHelp, others);
    }

    /// <summary>
    /// Resolves the real shipped <c>team-building</c> Skill's Name, Description and Tools from
    /// <see cref="SkillCatalog.All"/> through <see cref="SkillValidator.Validate"/> - the same path
    /// <see cref="SkillStore"/> uses at run time - rather than pasting its description into this test,
    /// so <c>Golden/systemPrompt.skills.txt</c> is the only place that wording is frozen.
    /// </summary>
    /// <returns>The resolved <c>team-building</c> Skill, with no on-disk override.</returns>
    private static Skill TeamBuildingSkill()
    {
        SkillValidation validation = SkillValidator.Validate("team-building", SkillCatalog.All["team-building"]);
        Assert.NotNull(validation.Description);

        return new Skill("team-building", validation.Description, validation.Tools, validation.Files, SkillSource.Default, null);
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
