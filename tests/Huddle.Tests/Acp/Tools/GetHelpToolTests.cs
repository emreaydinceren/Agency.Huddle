namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

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
        var chat = new ChatService(directory, store, events, aliasSource, NullLogger<ChatService>.Instance);

        IAppTool[] others =
        [
            new ListAgentsTool(directory, new FakeAgentGateway(), personaStore),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource),
            new InviteAgentTool(chat, directory, aliasSource),
            new PostMessageTool(chat, "caller-id"),
        ];
        var tool = new GetHelpTool(others);

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("mcp__team__get_help", help, StringComparison.Ordinal);
        foreach (var other in others)
        {
            Assert.Contains($"mcp__team__{other.Name}", help, StringComparison.Ordinal);
            Assert.Contains(other.Description, help, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetHelp_ExplainsTheReplyRuleAndHowARoomIdArrives()
    {
        var ct = TestContext.Current.CancellationToken;
        var tool = new GetHelpTool([]);

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        // The Reply Gate, stated in the words ReplyGate actually implements.
        Assert.Contains("two members", help, StringComparison.Ordinal);
        Assert.Contains("three or more", help, StringComparison.Ordinal);
        Assert.Contains("@-mentioned", help, StringComparison.Ordinal);

        // The room id is only ever learned from the prompt's Room label, so help has to say so.
        Assert.Contains("[Room: <name> (id: <id>)]", help, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHelp_ReportsAToolListTakenAtConstruction()
    {
        var ct = TestContext.Current.CancellationToken;
        var others = new List<IAppTool>();
        var tool = new GetHelpTool(others);

        // The caller's list is copied on the way in, so a later mutation cannot change what an
        // Agent is told exists: the tool server was handed a fixed set at the same moment.
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        others.Add(new ListAgentsTool(directory, new FakeAgentGateway(), personaStore));

        var help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.DoesNotContain("mcp__team__list_agents", help, StringComparison.Ordinal);
    }
}
