using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins what goldfish mode takes from the six existing tools (corrections-slice3 step 4): the readers refuse while the caller's Turn
/// is the goldfish's, <c>post_message</c> keeps the warm Agent out of the possessed Room, <c>ask_human</c> is stamped, and
/// <see cref="GoldfishStamp"/>. The <c>goldfish</c> tool's own refusal while possessed is pinned by
/// <c>GoldfishToolTests.InvokeAsync_CallerIsPossessed_IsRefusedAsNested</c>.
/// </summary>
public sealed class GoldfishToolRulesTests
{
    private const string Refused = "Not in goldfish mode: this tool reads what you must not know. Work from your brief and the files it names.";

    /// <summary>The tool, with what it needs to stay alive.</summary>
    private sealed class Built(IAppTool tool, params IDisposable[] owned) : IDisposable
    {
        public IAppTool Tool { get; } = tool;

        public void Dispose()
        {
            foreach (IDisposable item in owned)
            {
                item.Dispose();
            }
        }
    }

    private static Built Build(string toolName, GoldfishToolRig rig)
    {
        TaskToolHarness harness = rig.Harness;
        string nova = rig.NovaId;
        switch (toolName)
        {
            case "memory":
                MemoryFolderGate gate = new();
                return new Built(
                    new MemoryTool(
                        "Nova",
                        new AgentPaths(harness.Options),
                        new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false),
                        gate,
                        new NotAvailableRecycleBin(),
                        rig.Prompts,
                        harness.Options,
                        new MemoryWarnings(NullLogger<MemoryWarnings>.Instance),
                        NullLogger<MemoryTool>.Instance,
                        callerAgentId: nova,
                        goldfish: rig.Guard),
                    gate);
            case "propose_team_memory":
                TeamMemoryHarness memory = new();
                return new Built(
                    new ProposeTeamMemoryTool("Leo", memory.Access, memory.Trigger, memory.Registry, memory.Settings, memory.Budget, rig.Prompts, memory.Options, callerAgentId: nova, goldfish: rig.Guard),
                    memory);
            case "search":
                return new Built(
                    new SearchTool([], harness.Directory, harness.Options.CreateChatStore(), harness.Personas, NullLogger<SearchTool>.Instance, rig.Prompts, nova, goldfish: rig.Guard));
            default:
                FakeTeamCatalog catalog = new();
                TeamTypeCatalog types = new(harness.Options, NullLogger<TeamTypeCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
                RecordsBundleBuilder builder = new(harness.Store, catalog, new PersonaSpendStore(harness.Options), harness.Options);
                return new Built(
                    new ReadRecordsTool(catalog, harness.Store, harness.Personas, types, builder, harness.Options.CreateChatStore(), new FakeTeamFolders(), harness.Directory, harness.Clock, nova, rig.Prompts, goldfish: rig.Guard));
        }
    }

    /// <summary>While the caller's Turn is a goldfish's, each reader refuses with the goldfish Prompt, before anything else.</summary>
    /// <param name="toolName">The tool.</param>
    [Theory]
    [InlineData("memory")]
    [InlineData("propose_team_memory")]
    [InlineData("search")]
    [InlineData("read_records")]
    public async Task Reader_WhilePossessed_RefusesWithTheGoldfishPrompt(string toolName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();
        using Built built = Build(toolName, rig);

        string result = await built.Tool.InvokeAsync([], ct);

        Assert.Equal(Refused, result);
    }

    /// <summary>The same work in another Room, while the possession is elsewhere, is not refused.</summary>
    /// <param name="toolName">The tool.</param>
    [Theory]
    [InlineData("memory")]
    [InlineData("propose_team_memory")]
    [InlineData("search")]
    [InlineData("read_records")]
    public async Task Reader_InAnotherRoom_IsNotRefused(string toolName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);
        using Built built = Build(toolName, rig);

        string result = await built.Tool.InvokeAsync([], ct);

        Assert.NotEqual(Refused, result);
        Assert.NotEmpty(result);
    }

    /// <summary>While the possession is only Starting, the warm calling Turn is not stamped and so is not refused.</summary>
    /// <param name="toolName">The tool.</param>
    [Theory]
    [InlineData("memory")]
    [InlineData("propose_team_memory")]
    [InlineData("search")]
    [InlineData("read_records")]
    public async Task Reader_WhileOnlyStarting_IsNotRefused(string toolName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession(PossessionState.Starting);
        rig.NovaTurnInRoom();
        using Built built = Build(toolName, rig);

        string result = await built.Tool.InvokeAsync([], ct);

        Assert.NotEqual(Refused, result);
        Assert.NotEmpty(result);
    }

    private static PostMessageTool PostTool(GoldfishToolRig rig) =>
        new(rig.Harness.Chat, rig.NovaId, rig.Prompts, rig.Harness.OwnPosts, possessions: rig.Supervisor, activity: rig.Activity, directory: rig.Harness.Directory);

    private static JsonObject PostArguments(string roomId) => new() { ["roomId"] = roomId, ["text"] = "hello" };

    /// <summary>The goldfish posts into the Room it is in: allowed, and the Message is stored.</summary>
    [Fact]
    public async Task PostMessage_FromThePossessedRoom_IsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();

        string result = await PostTool(rig).InvokeAsync(PostArguments(rig.Room.Id), ct);

        Assert.Equal($"Posted to room '{rig.Room.Id}'.", result);
        ChatMessage posted = Assert.Single(await rig.Harness.Options.CreateChatStore().ReadAllAsync(rig.Room.Id, ct));
        Assert.Equal("hello", posted.Text);
    }

    /// <summary>The warm Agent, working in another Room, may not post into the possessed Room; nothing is stored.</summary>
    [Fact]
    public async Task PostMessage_FromAnotherRoomIntoThePossessedRoom_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);

        string result = await PostTool(rig).InvokeAsync(PostArguments(rig.Room.Id), ct);

        Assert.Equal("Nova is in goldfish mode in Pricing; post there after they are back.", result);
        Assert.Empty(await rig.Harness.Options.CreateChatStore().ReadAllAsync(rig.Room.Id, ct));
    }

    /// <summary>Posting into another Room, which holds no possession, is not touched by the guard.</summary>
    [Fact]
    public async Task PostMessage_WhilePossessed_IntoAnotherRoom_IsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);

        string result = await PostTool(rig).InvokeAsync(PostArguments(other.Id), ct);

        Assert.Equal($"Posted to room '{other.Id}'.", result);
    }

    /// <summary>While the possession is only Starting, the warm Agent's own calling Turn still posts into the Room.</summary>
    [Fact]
    public async Task PostMessage_WhileOnlyStarting_IsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession(PossessionState.Starting);
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);

        string result = await PostTool(rig).InvokeAsync(PostArguments(rig.Room.Id), ct);

        Assert.Equal($"Posted to room '{rig.Room.Id}'.", result);
    }

    private static AskHumanTool AskTool(GoldfishToolRig rig, QuestionStore questions) =>
        new(questions, rig.Harness.Chat, rig.Harness.Directory, TimeProvider.System, rig.NovaId, rig.Prompts, goldfish: rig.Guard);

    private static JsonObject AskArguments(string roomId) => new()
    {
        ["roomId"] = roomId,
        ["questions"] = new JsonArray(new JsonObject { ["question"] = "Which one?", ["options"] = new JsonArray("A", "B") }),
    };

    /// <summary>The goldfish's question card names it "Nova · goldfish".</summary>
    [Fact]
    public async Task AskHuman_WhilePossessed_StampsTheAskerName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();
        QuestionStore questions = new(rig.Harness.RoomEvents);

        _ = await AskTool(rig, questions).InvokeAsync(AskArguments(rig.Room.Id), ct);

        PendingQuestions? stored = questions.Get(rig.Room.Id);
        Assert.NotNull(stored);
        Assert.Equal("Nova · goldfish", stored.AskerName);
        Assert.Equal(rig.NovaId, stored.AskerAgentId);
    }

    /// <summary>Without a possession the card keeps the plain name.</summary>
    [Fact]
    public async Task AskHuman_NotPossessed_KeepsThePlainName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();
        QuestionStore questions = new(rig.Harness.RoomEvents);

        _ = await AskTool(rig, questions).InvokeAsync(AskArguments(rig.Room.Id), ct);

        Assert.Equal("Nova", questions.Get(rig.Room.Id)?.AskerName);
    }

    /// <summary>While the possession is only Starting, the warm Agent's question is not stamped.</summary>
    [Fact]
    public async Task AskHuman_WhileOnlyStarting_KeepsThePlainName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession(PossessionState.Starting);
        rig.NovaTurnInRoom();
        QuestionStore questions = new(rig.Harness.RoomEvents);

        _ = await AskTool(rig, questions).InvokeAsync(AskArguments(rig.Room.Id), ct);

        Assert.Equal("Nova", questions.Get(rig.Room.Id)?.AskerName);
    }

    /// <summary>Apply appends the suffix to the Agent's name.</summary>
    [Fact]
    public void GoldfishStamp_Apply_AppendsTheSuffix()
    {
        Assert.Equal("Ruth · goldfish", GoldfishStamp.Apply("Ruth"));
        Assert.Equal(" · goldfish", GoldfishStamp.Suffix);
    }

    /// <summary>AgentName undoes Apply, and leaves an unstamped author as it was.</summary>
    [Fact]
    public void GoldfishStamp_AgentName_StripsOnlyTheSuffix()
    {
        Assert.Equal("Ruth", GoldfishStamp.AgentName("Ruth · goldfish"));
        Assert.Equal("Ruth", GoldfishStamp.AgentName("Ruth"));
        Assert.Equal("Ruth · goldfish team", GoldfishStamp.AgentName("Ruth · goldfish team"));
    }
}
