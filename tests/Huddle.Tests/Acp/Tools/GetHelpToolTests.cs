namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Covers the progressive-discovery entry point: the system prompt names one tool, and this tool has
/// to be able to name the rest, prefixed, or the discovery chain breaks at its first link.
/// </summary>
public sealed class GetHelpToolTests
{
    [Fact]
    public async Task GetHelp_NamesEveryOtherToolWithItsMcpPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource();
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);

        var follows = new RoomFollows();
        IAppTool[] others =
        [
            new ListAgentsTool(directory, new FakeAgentGateway(), personaStore, new FakePromptSource()),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource, new FakePromptSource()),
            new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource()),
            new PostMessageTool(chat, "caller-id", new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions()))),
            new FollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
        ];
        var tool = new GetHelpTool(others, new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("mcp__team__get_help", help, StringComparison.Ordinal);
        foreach (var other in others)
        {
            Assert.Contains($"mcp__team__{other.Name}", help, StringComparison.Ordinal);
            Assert.Contains(other.Description, help, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <c>get_help</c> is built from the tools it is handed, so when <c>watch_folder</c> and
    /// <c>unwatch_folder</c> are offered (FC §6.9, §6.11), it lists both by their prefixed model-facing
    /// name - the same guarantee <see cref="GetHelp_NamesEveryOtherToolWithItsMcpPrefix"/> pins for the
    /// original tool set, needed separately because <c>PromptGoldenTests</c> builds its tool roster from
    /// a fixed list that never includes these two, so <c>getHelp.txt</c> and <c>toolDescriptions.txt</c>
    /// cannot cover this.
    /// </summary>
    [Fact]
    public async Task GetHelp_WithWatchTools_ListsBothByPrefixedName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new Agency.Huddle.Tests.TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var store = new Agency.Huddle.App.FileChanges.FileStateStore(dir.Options(), NullLogger<Agency.Huddle.App.FileChanges.FileStateStore>.Instance);
        var resolver = new Agency.Huddle.App.FileChanges.WatchedFolderResolver(dir.Options());
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var tracker = new Agency.Huddle.App.FileChanges.FileChangeTracker(store, personaStore, directory, resolver, dir.Options(), NullLogger<Agency.Huddle.App.FileChanges.FileChangeTracker>.Instance);

        IAppTool[] watchTools =
        [
            new WatchFolderTool(tracker, "Nova", new FakePromptSource()),
            new UnwatchFolderTool(tracker, "Nova", new FakePromptSource()),
        ];
        var tool = new GetHelpTool(watchTools, new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        foreach (var other in watchTools)
        {
            Assert.Contains($"mcp__team__{other.Name}", help, StringComparison.Ordinal);
            Assert.Contains(other.Description, help, StringComparison.Ordinal);
        }
    }

    /// <summary>An override configured on a prompt <see cref="GetHelpTool"/> renders must actually reach its output.</summary>
    [Fact]
    public async Task GetHelp_OverriddenBudgetPrompt_AppearsInHelp()
    {
        var ct = TestContext.Current.CancellationToken;
        var prompts = new FakePromptSource();
        prompts.SetOverride("getHelp.budget", "BUDGET\nCustom budget wording for this test.");
        var tool = new GetHelpTool([], prompts, "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("Custom budget wording for this test.", help, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>tool.*.description</c> prompt is badged <b>Next session</b>, so an edit made after a
    /// session's tool list was built must not reach that session's help output — while the help
    /// prose around it, which carries no badge, must. Both halves are asserted together on purpose:
    /// caching the whole help text would satisfy the first and silently break the second.
    /// </summary>
    [Fact]
    public async Task GetHelp_ToolDescriptionEditedAfterConstruction_DoesNotReachThisSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var prompts = new FakePromptSource();
        var tool = new GetHelpTool([], prompts, "mcp__team__");

        // Both overrides land after construction, which is what "after the session started" means:
        // GetHelpTool is built once per session, beside the tool server and the system prompt.
        prompts.SetOverride("tool.getHelp.description", "Explains how Team works. MANGO.");
        prompts.SetOverride("getHelp.budget", "BUDGET\nPINEAPPLE is the safe word.");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.DoesNotContain("MANGO", help, StringComparison.Ordinal);
        Assert.Contains("PINEAPPLE is the safe word.", help, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHelp_ExplainsTheReplyRuleAndHowARoomIdArrives()
    {
        var ct = TestContext.Current.CancellationToken;
        var tool = new GetHelpTool([], new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        // The Reply Gate, stated in the words ReplyGate actually implements.
        Assert.Contains("two members", help, StringComparison.Ordinal);
        Assert.Contains("three or more", help, StringComparison.Ordinal);
        Assert.Contains("@-mentioned", help, StringComparison.Ordinal);

        // The room id is only ever learned from the prompt's Room label, so help has to say so.
        // This literal is doing double duty by design (task T1.11): it pins the documented Room-label
        // format the getHelp.messages default advertises, AND it is the regression test proving
        // PromptRenderer's {{...}} substitution does not eat literal angle brackets. Do not "modernise"
        // this into "{{roomName}}" wording — that would destroy both purposes at once.
        //
        // TODO(follow-up, out of scope for T1.11): nothing yet asserts that this documented format —
        // "[Room: <name> (id: <id>)]" — actually matches what turn.roomLabel's default renders in
        // RoomSession.BuildPrompt. The two prompts (getHelp.messages and turn.roomLabel) can drift
        // apart with no test noticing, now that each is independently overridable.
        Assert.Contains("[Room: <name> (id: <id>)]", help, StringComparison.Ordinal);
    }

    // A model that reads a Budget refusal as transient retries, spending the Turn the refusal exists to
    // save - and one that treats it as a routing problem moves to another Room, which is worse. Both
    // are addressed in words here rather than only in the refusal itself.
    [Fact]
    public async Task GetHelp_MentionsTheRoomBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        var tool = new GetHelpTool([], new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("budget", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not retry", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("another Room", help, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHelp_ReportsAToolListTakenAtConstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        var others = new List<IAppTool>();
        var tool = new GetHelpTool(others, new FakePromptSource(), "mcp__team__");

        // The caller's list is copied on the way in, so a later mutation cannot change what an
        // Agent is told exists: the tool server was handed a fixed set at the same moment.
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        others.Add(new ListAgentsTool(directory, new FakeAgentGateway(), personaStore, new FakePromptSource()));

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.DoesNotContain("mcp__team__list_agents", help, StringComparison.Ordinal);
    }

    /// <summary>
    /// An Adapter Profile whose <see cref="AdapterProfile.UsesToolNamePrefix"/> is
    /// <see langword="false"/> passes <see cref="string.Empty"/> as the tool name prefix (Spec §6.4;
    /// ADR-0014). Construction must succeed, and every tool name in the rendered help text — this
    /// tool's own and every other tool's — must be bare, with no <c>mcp__</c> prefix anywhere.
    /// </summary>
    [Fact]
    public async Task GetHelp_EmptyToolNamePrefix_ReportsBareNamesWithNoMcpPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        IAppTool[] others = [new ListAgentsTool(directory, new FakeAgentGateway(), personaStore, new FakePromptSource())];
        var tool = new GetHelpTool(others, new FakePromptSource(), string.Empty);

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("get_help", help, StringComparison.Ordinal);
        Assert.Contains("list_agents", help, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp__", help, StringComparison.Ordinal);
    }

    /// <summary>A <see langword="null"/> tool name prefix is still rejected: the loosened guard must not become no guard.</summary>
    [Fact]
    public void Constructor_NullToolNamePrefix_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GetHelpTool([], new FakePromptSource(), null!));
    }

    /// <summary>
    /// Corrections-B4 D10 item 6 (overrides the plan's "constructor flag"): <c>get_help</c> takes no
    /// new constructor parameter for Tasks. Instead, <c>BuildHelp</c> adds the <c>getHelp.tasks</c>
    /// section, right after <c>getHelp.budget</c>, whenever the tool catalog it was already handed
    /// contains <c>create_task</c> - the same "read the catalog it already has" shape
    /// <see cref="GetHelpTool"/>'s remarks describe for every other section.
    /// </summary>
    [Fact]
    public async Task GetHelp_CatalogContainsCreateTask_IncludesTasksSectionAfterBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        IAppTool[] others = [new StubTool("create_task")];
        var tool = new GetHelpTool(others, new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        var tasksText = PromptCatalog.Get("getHelp.tasks").Default;
        var budgetText = PromptCatalog.Get("getHelp.budget").Default;
        Assert.Contains(tasksText, help, StringComparison.Ordinal);
        Assert.True(
            help.IndexOf(budgetText, StringComparison.Ordinal) < help.IndexOf(tasksText, StringComparison.Ordinal),
            "The getHelp.tasks section must appear after getHelp.budget.");
    }

    /// <summary>The other half of corrections-B4 D10 item 6: with no <c>create_task</c> in the catalog (Tasks disabled, or not yet offered), the <c>getHelp.tasks</c> section is omitted entirely.</summary>
    [Fact]
    public async Task GetHelp_CatalogWithoutCreateTask_OmitsTasksSection()
    {
        var ct = TestContext.Current.CancellationToken;
        IAppTool[] others = [new StubTool("list_agents")];
        var tool = new GetHelpTool(others, new FakePromptSource(), "mcp__team__");

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        var tasksText = PromptCatalog.Get("getHelp.tasks").Default;
        Assert.DoesNotContain(tasksText, help, StringComparison.Ordinal);
    }

    /// <summary>A minimal <see cref="IAppTool"/> stand-in identified only by its <see cref="Name"/>, mirroring <c>SkillGrantsTests.StubTool</c> for the same purpose here.</summary>
    /// <param name="name">The tool's name, and its whole behaviour for this stub.</param>
    private sealed class StubTool(string name) : IAppTool
    {
        public string Name => name;

        public string Description => name;

        public JsonObject InputSchema => new JsonObject { ["type"] = "object" };

        public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
        {
            return Task.FromResult(name);
        }
    }
}