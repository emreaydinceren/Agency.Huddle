using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

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
        return await directory.CreateRoomAsync(name, [KnownIds.Human, agent.Id], null, teamRoom: false, ct: ct);
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

    /// <summary>The Room page context from <see cref="ChatPageContext"/>, with loose JS interop for the scripts the page calls.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    /// <returns>The context.</returns>
    private static MudBunitContext NewChatContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = ChatPageContext.Create(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }
}
