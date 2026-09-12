namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

public sealed class ToolNamesTests
{
    [Fact]
    public void Tools_HaveTheExpectedMcpNames()
    {
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        var gateway = new FakeAgentGateway();
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()));
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var chat = new ChatService(directory, store, events, NullLogger<ChatService>.Instance);

        var tools = new IAppTool[]
        {
            new ListAgentsTool(directory, gateway, personaStore),
            new CreateRoomTool(chat, directory, "caller-id"),
            new InviteAgentTool(chat, directory),
            new PostMessageTool(chat, "caller-id"),
            new GetHelpTool([]),
        };

        Assert.Equal("list_agents", tools[0].Name);
        Assert.Equal("create_room", tools[1].Name);
        Assert.Equal("invite_agent", tools[2].Name);
        Assert.Equal("post_message", tools[3].Name);
        Assert.Equal("get_help", tools[4].Name);
    }
}