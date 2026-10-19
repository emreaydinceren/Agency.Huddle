using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins that the Room page publishes a <see cref="RoomSubject"/> for its own Tab (Spec D-2).</summary>
public sealed class ChatSubjectTests
{
    private const string TabId = "t1";

    /// <summary>A loaded Room publishes <see cref="RoomSubject"/> with its id.</summary>
    [Fact]
    public async Task Chat_LoadedRoom_PublishesRoomSubject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await ChatSubjectTests.CreateRoomAsync(factory, "Nova", ct);
        await using MudBunitContext ctx = ChatSubjectTests.NewChatContext(factory);

        _ = ChatSubjectTests.RenderChat(ctx, room.Id);

        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        Assert.Equal(new RoomSubject(room.Id), sidebar.SubjectFor(ChatSubjectTests.TabId));
    }

    /// <summary>Navigating the same page instance to another Room publishes the new Room.</summary>
    [Fact]
    public async Task Chat_SwitchRoom_PublishesTheNewRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room first = await ChatSubjectTests.CreateRoomAsync(factory, "Nova", ct);
        Room second = await ChatSubjectTests.CreateRoomAsync(factory, "Orion", ct);
        await using MudBunitContext ctx = ChatSubjectTests.NewChatContext(factory);
        IRenderedComponent<CascadingValue<TabPageContext>> cut = ChatSubjectTests.RenderChat(ctx, first.Id);

        await cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(p => p.Value, new TabPageContext("/rooms/" + second.Id, ChatSubjectTests.TabId))
            .Add(p => p.ChildContent, ChatSubjectTests.ChatFragment(second.Id))));

        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        Assert.Equal(new RoomSubject(second.Id), sidebar.SubjectFor(ChatSubjectTests.TabId));
    }

    private static async Task<Room> CreateRoomAsync(TeamWebApplicationFactory factory, string name, CancellationToken ct)
    {
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        return await directory.CreateRoomAsync(name, [KnownIds.Human, agent.Id], null, teamRoom: false, ct);
    }

    private static RenderFragment ChatFragment(string roomId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<Chat>(2);
        builder.AddComponentParameter(3, nameof(Chat.RoomId), roomId);
        builder.CloseComponent();
    };

    private static IRenderedComponent<CascadingValue<TabPageContext>> RenderChat(MudBunitContext ctx, string roomId) =>
        ctx.Render<CascadingValue<TabPageContext>>(parameters => parameters
            .Add(p => p.Value, new TabPageContext("/rooms/" + roomId, ChatSubjectTests.TabId))
            .Add(p => p.ChildContent, ChatSubjectTests.ChatFragment(roomId)));

    private static MudBunitContext NewChatContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        IServiceProvider source = factory.Services;
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(source.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(source.GetRequiredService<IChatStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(source.GetRequiredService<ChatService>());
        // Chat navigates through ServerNavigator, which needs the circuit connection tracker the app registers.
        Agency.Huddle.App.Circuits.CircuitServiceCollectionExtensions.AddCircuitConnection(ctx.Services);
        ctx.Services.AddSingleton(source.GetRequiredService<Drafts>());
        ctx.Services.AddSingleton(source.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomFollows>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Profile.IHumanIdentity>());
        ctx.Services.AddSingleton(source.GetRequiredService<IMentionAliasSource>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<HireLimitGuard>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<TaskStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionService>());
        ctx.Services.AddSingleton(source.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationService>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddSingleton(new TabService());

        // Chat navigates through ServerNavigator when a Rooms event moves the Human off a Room.
        ctx.Services.AddFakeCircuitConnection();
        return ctx;
    }
}
