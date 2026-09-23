namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

public sealed class ToolNamesTests
{
    [Fact]
    public void Tools_HaveTheExpectedMcpNames()
    {
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        var gateway = new FakeAgentGateway();
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource();
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);

        var follows = new RoomFollows();
        var checker = new CandidateChecker(personaStore, directory, gateway);
        var tools = new IAppTool[]
        {
            new ListAgentsTool(directory, gateway, personaStore, new FakePromptSource()),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource, new FakePromptSource()),
            new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource()),
            new PostMessageTool(chat, "caller-id", new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions()))),
            new FollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new ValidateTeammateTool(checker, new FakePromptSource()),
            new ProposeTeammatesTool(proposals, checker, personaStore, directory, Options.Create(new TeamOptions()), TimeProvider.System, "caller-id", new FakePromptSource()),
            new GetHelpTool([], new FakePromptSource(), "mcp__team__"),
        };

        Assert.Equal("list_agents", tools[0].Name);
        Assert.Equal("create_room", tools[1].Name);
        Assert.Equal("invite_agent", tools[2].Name);
        Assert.Equal("post_message", tools[3].Name);
        Assert.Equal("follow_room", tools[4].Name);
        Assert.Equal("unfollow_room", tools[5].Name);
        Assert.Equal("validate_teammate", tools[6].Name);
        Assert.Equal("propose_teammates", tools[7].Name);
        Assert.Equal("get_help", tools[8].Name);
    }
}