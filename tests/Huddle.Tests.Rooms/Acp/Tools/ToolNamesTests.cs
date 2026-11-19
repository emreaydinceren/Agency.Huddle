namespace Agency.Huddle.Tests.Acp.Tools;

using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

public sealed class ToolNamesTests
{
    [Fact]
    public void Tools_HaveTheExpectedMcpNames()
    {
        using var dir = new TempDataDir();
        var directory = dir.CreateDirectory();
        var gateway = new FakeAgentGateway();
        using var personaStore = dir.CreatePersonaStore();
        var aliasSource = new FakeMentionAliasSource();
        var (chat, _, events, proposals, _) = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource);

        var follows = new RoomFollows();
        var checker = new CandidateChecker(personaStore, directory, gateway);
        using TaskToolHarness taskHarness = new();
        var tools = new IAppTool[]
        {
            new ListAgentsTool(directory, gateway, personaStore, new FakePromptSource()),
            new CreateRoomTool(chat, directory, "caller-id", aliasSource, new FakePromptSource()),
            new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource()),
            new PostMessageTool(chat, "caller-id", new FakePromptSource(), new OwnPosts(TestOptions.Create())),
            new FollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource()),
            new ValidateAgentTool(checker, new FakePromptSource()),
            new ProposeAgentsTool(proposals, checker, directory, new HireLimitGuard(new Agency.Huddle.Tests.Teams.FakeTeamCatalog(), TestOptions.Create(), static () => 0), TimeProvider.System, "caller-id", new FakePromptSource()),
            new GetHelpTool([], new FakePromptSource(), "mcp__team__"),
            new CreateTaskTool(taskHarness.Service, taskHarness.Triggers, taskHarness.Directory, taskHarness.Sprints, new FakePromptSource(), "caller-id"),
            new GetTaskTool(taskHarness.Store, taskHarness.Sprints, new FakePromptSource()),
            new ListTasksTool(taskHarness.Store, taskHarness.Directory, taskHarness.Personas, taskHarness.Sprints, new FakePromptSource(), "caller-id"),
            new NextTaskTool(taskHarness.Store, taskHarness.Directory, new FakePromptSource(), "caller-id"),
            new UpdateTaskTool(taskHarness.Service, taskHarness.Store, taskHarness.Triggers, taskHarness.Directory, taskHarness.Sprints, new FakePromptSource(), "caller-id"),
            new CloseTaskTool(taskHarness.Service, taskHarness.Store, taskHarness.Triggers, taskHarness.Directory, new FakePromptSource(), "caller-id"),
            new ReopenTaskTool(taskHarness.Service, taskHarness.Store, taskHarness.Triggers, taskHarness.Directory, new FakePromptSource(), "caller-id"),
            new AskHumanTool(new QuestionStore(events), chat, directory, TimeProvider.System, "caller-id", new FakePromptSource()),
        };

        Assert.Equal("list_agents", tools[0].Name);
        Assert.Equal("create_room", tools[1].Name);
        Assert.Equal("invite_agent", tools[2].Name);
        Assert.Equal("post_message", tools[3].Name);
        Assert.Equal("follow_room", tools[4].Name);
        Assert.Equal("unfollow_room", tools[5].Name);
        Assert.Equal("validate_agent", tools[6].Name);
        Assert.Equal("propose_agents", tools[7].Name);
        Assert.Equal("get_help", tools[8].Name);
        Assert.Equal("create_task", tools[9].Name);
        Assert.Equal("get_task", tools[10].Name);
        Assert.Equal("list_tasks", tools[11].Name);
        Assert.Equal("next_task", tools[12].Name);
        Assert.Equal("update_task", tools[13].Name);
        Assert.Equal("close_task", tools[14].Name);
        Assert.Equal("reopen_task", tools[15].Name);
        Assert.Equal("ask_human", tools[16].Name);
    }
}