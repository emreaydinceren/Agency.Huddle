using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Ui.References;

/// <summary>
/// The Copy link entry points (spec §6.1, D-31): the Room header button on <c>Chat.razor</c>, and the
/// <c>Copy link</c> item first in <c>RoomNavRow</c>'s menu. Both call <c>huddleClipboard.copy</c> with the
/// Room's reference text, and both report through the snackbar with the spec's exact wording.
/// </summary>
public sealed class RoomCopyLinkTests
{
    /// <summary>The clipboard helper both entry points call.</summary>
    private const string CopyHelper = "huddleClipboard.copy";

    /// <summary>The header button's accessible name.</summary>
    private const string HeaderButton = "button[aria-label='Copy link to this Room']";

    /// <summary>The header's copy returns the Markdown reference the picker would insert for each Room scope, with <c>@</c> made fullwidth and Markdown specials escaped.</summary>
    /// <param name="name">The Room's Name.</param>
    /// <param name="team">The Team the Room is scoped to, or null for a root Room.</param>
    /// <param name="teamRoom">Whether the Room is its Team's Team Room.</param>
    /// <param name="label">The label the reference must carry.</param>
    [Theory]
    [InlineData("Nova", null, false, "Nova")]
    [InlineData("Nova", "Marketing", false, "Nova (Marketing)")]
    [InlineData("Marketing", "Marketing", true, "Marketing Team Room")]
    [InlineData("Q@A [x]", null, false, "Q＠A \\[x\\]")]
    public async Task Header_CopiesThePickersInsertText(string name, string? team, bool teamRoom, string label)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await RoomCopyLinkTests.CreateRoomAsync(factory, name, team, teamRoom, ct);
        string expected = $"[{label}](/rooms/{room.Id})";
        await using MudBunitContext ctx = RoomCopyLinkTests.NewChatContext(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>(RoomCopyLinkTests.CopyHelper, expected).SetResult(true);

        var cut = RoomCopyLinkTests.RenderChat(ctx, room.Id);
        await cut.InvokeAsync(() => cut.Find(RoomCopyLinkTests.HeaderButton).ClickAsync());

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke(RoomCopyLinkTests.CopyHelper);
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    /// <summary>The copy button is shown on a Team Room too, which has no Rename pencil.</summary>
    [Fact]
    public async Task Header_ButtonShownOnATeamRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await RoomCopyLinkTests.CreateRoomAsync(factory, "Marketing", "Marketing", teamRoom: true, ct);
        await using MudBunitContext ctx = RoomCopyLinkTests.NewChatContext(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RoomCopyLinkTests.RenderChat(ctx, room.Id);

        Assert.Single(cut.FindAll(RoomCopyLinkTests.HeaderButton));
    }

    /// <summary>A successful copy from the header shows the success snackbar with the plain Room Name, not the Markdown label.</summary>
    [Fact]
    public async Task Header_Success_Snackbar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await RoomCopyLinkTests.CreateRoomAsync(factory, "Nova", null, false, ct);
        await using MudBunitContext ctx = RoomCopyLinkTests.NewChatContext(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>(RoomCopyLinkTests.CopyHelper, $"[Nova](/rooms/{room.Id})").SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        var cut = RoomCopyLinkTests.RenderChat(ctx, room.Id);
        await cut.InvokeAsync(() => cut.Find(RoomCopyLinkTests.HeaderButton).ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal("Copied link to Nova", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
    }

    /// <summary>A refused copy from the header shows the warning snackbar with the spec's wording.</summary>
    [Fact]
    public async Task Header_Failure_Snackbar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await RoomCopyLinkTests.CreateRoomAsync(factory, "Nova", null, false, ct);
        await using MudBunitContext ctx = RoomCopyLinkTests.NewChatContext(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>(RoomCopyLinkTests.CopyHelper, $"[Nova](/rooms/{room.Id})").SetResult(false);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        var cut = RoomCopyLinkTests.RenderChat(ctx, room.Id);
        await cut.InvokeAsync(() => cut.Find(RoomCopyLinkTests.HeaderButton).ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal("Couldn't copy the link.", shown.Message);
        Assert.Equal(Severity.Warning, shown.Severity);
    }

    /// <summary>One click on the header button calls the clipboard helper exactly once.</summary>
    [Fact]
    public async Task Copy_CallsTheHelperExactlyOnce()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        Room room = await RoomCopyLinkTests.CreateRoomAsync(factory, "Nova", null, false, ct);
        await using MudBunitContext ctx = RoomCopyLinkTests.NewChatContext(factory);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RoomCopyLinkTests.RenderChat(ctx, room.Id);
        await cut.InvokeAsync(() => cut.Find(RoomCopyLinkTests.HeaderButton).ClickAsync());

        Assert.Single(ctx.JSInterop.Invocations, invocation => string.Equals(invocation.Identifier, RoomCopyLinkTests.CopyHelper, StringComparison.Ordinal));
    }

    /// <summary>The sidebar row's menu offers Copy link first, above Archive and Delete.</summary>
    [Fact]
    public async Task NavRow_MenuHasCopyLinkFirst()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = RoomCopyLinkTests.CreateChatService(dir, directory, events);
        _ = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);

        await using MudBunitContext ctx = RoomCopyLinkTests.NewListContext(directory, chat, events);
        var cut = RoomCopyLinkTests.RenderRoomList(ctx);
        await RoomCopyLinkTests.OpenRowMenuAsync(cut);

        Assert.Equal("Copy link", RoomCopyLinkTests.MenuItems(cut)[0].TextContent.Trim());
    }

    /// <summary>The sidebar row's Copy link copies the same reference text as the header, for the same Room.</summary>
    [Fact]
    public async Task NavRow_CopyLink_CopiesTheSameText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = RoomCopyLinkTests.CreateChatService(dir, directory, events);
        Room room = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);
        string expected = $"[Lunch plans](/rooms/{room.Id})";

        await using MudBunitContext ctx = RoomCopyLinkTests.NewListContext(directory, chat, events);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>(RoomCopyLinkTests.CopyHelper, expected).SetResult(true);
        var cut = RoomCopyLinkTests.RenderRoomList(ctx);
        await RoomCopyLinkTests.OpenRowMenuAsync(cut);
        await RoomCopyLinkTests.ClickMenuItemAsync(cut, "Copy link");

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke(RoomCopyLinkTests.CopyHelper);
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    /// <summary>The Team Room's row under its Team in the Teams nav copies the same reference text as the header button (spec §6.1, D-31).</summary>
    [Fact]
    public async Task NavRow_CopyLink_CopiesTheSameText_UnderATeam()
    {
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("room-marketing", "Marketing", DateTimeOffset.UnixEpoch) { Team = "Marketing", IsTeamRoom = true });
        Agency.Huddle.Tests.Teams.FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Marketing", [], ["Nova"], true)] };
        ChatService chat = RoomCopyLinkTests.CreateChatService(dir, directory, events);
        string expected = "[Marketing Team Room](/rooms/room-marketing)";

        await using MudBunitContext ctx = RoomCopyLinkTests.NewTeamsNavContext(directory, catalog, chat, events);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>(RoomCopyLinkTests.CopyHelper, expected).SetResult(true);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/room-marketing");
        IRenderedComponent<ContainerFragment> cut = RoomCopyLinkTests.RenderTeamsNav(ctx);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href='/rooms/room-marketing']")));
        await RoomCopyLinkTests.OpenRowMenuAsync(cut);
        await RoomCopyLinkTests.ClickMenuItemAsync(cut, "Copy link");

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke(RoomCopyLinkTests.CopyHelper);
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    /// <summary>Registers the services the Teams nav injects, over the scripted Room directory and the given Team catalog.</summary>
    /// <param name="directory">The Room directory the nav reads.</param>
    /// <param name="catalog">The Team catalog the nav lists.</param>
    /// <param name="chat">The chat service the Room rows use.</param>
    /// <param name="events">The hub the rows listen on.</param>
    /// <returns>The context.</returns>
    private static MudBunitContext NewTeamsNavContext(ITeamDirectory directory, Agency.Huddle.Tests.Teams.FakeTeamCatalog catalog, ChatService chat, RoomEvents events)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(Options.Create(new TeamOptions()), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
        ctx.Services.AddSingleton<TabService>();
        return ctx;
    }

    /// <summary>Renders the Teams nav with its menu popovers.</summary>
    /// <param name="ctx">The context to render in.</param>
    /// <returns>The rendered nav.</returns>
    private static IRenderedComponent<ContainerFragment> RenderTeamsNav(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.CloseComponent();
        });

    /// <summary>Creates a Room with an Agent as a Member, scoped to <paramref name="team"/> when one is given.</summary>
    /// <param name="factory">The factory whose directory holds the Room.</param>
    /// <param name="name">The Room's Name.</param>
    /// <param name="team">The Team to scope the Room to, or null.</param>
    /// <param name="teamRoom">Whether the Room is its Team's Team Room.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>The created Room.</returns>
    private static async Task<Room> CreateRoomAsync(TeamWebApplicationFactory factory, string name, string? team, bool teamRoom, CancellationToken ct)
    {
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        return await directory.CreateRoomAsync(name, [KnownIds.Human, agent.Id], team, teamRoom: teamRoom, ct);
    }

    /// <summary>Registers the services the Room page injects, taken from the composed factory.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    /// <returns>The context.</returns>
    private static MudBunitContext NewChatContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        IServiceProvider source = factory.Services;
        ctx.Services.AddSingleton(source.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(source.GetRequiredService<IChatStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(source.GetRequiredService<ChatService>());
        ctx.Services.AddSingleton(source.GetRequiredService<Drafts>());
        ctx.Services.AddSingleton(source.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomFollows>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<AvatarStore>());
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
        ctx.Services.AddFakeCircuitConnection();
        return ctx;
    }

    /// <summary>Renders the Room page for <paramref name="roomId"/> beside the popover provider its menus need.</summary>
    /// <param name="ctx">The context to render in.</param>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>The rendered page.</returns>
    private static IRenderedComponent<ContainerFragment> RenderChat(MudBunitContext ctx, string roomId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Chat>(0);
            builder.AddComponentParameter(1, nameof(Chat.RoomId), roomId);
            builder.CloseComponent();
        });

    /// <summary>Builds a chat service over the temp directory, for the sidebar tests.</summary>
    /// <param name="dir">The temp data directory.</param>
    /// <param name="directory">The Room directory.</param>
    /// <param name="events">The hub it publishes on.</param>
    /// <returns>The service.</returns>
    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, RoomEvents events)
    {
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        ProposalStore proposals = new(events);
        return new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
    }

    /// <summary>Registers the services the sidebar's list injects.</summary>
    /// <param name="directory">The Room directory.</param>
    /// <param name="chat">The chat service.</param>
    /// <param name="events">The hub the rows listen on.</param>
    /// <returns>The context.</returns>
    private static MudBunitContext NewListContext(ITeamDirectory directory, ChatService chat, RoomEvents events)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(chat.TeamCatalog ?? new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(Options.Create(new TeamOptions()), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<TabService>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    /// <summary>Renders the sidebar's <see cref="RoomList"/> with its menu popovers.</summary>
    /// <param name="ctx">The context to render in.</param>
    /// <returns>The rendered list.</returns>
    private static IRenderedComponent<ContainerFragment> RenderRoomList(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });

    /// <summary>Clicks the row's "..." button, opening its menu.</summary>
    /// <param name="cut">The rendered list.</param>
    private static async Task OpenRowMenuAsync(IRenderedComponent<ContainerFragment> cut)
    {
        IElement row = cut.FindAll("div.hover-reveal-row").First(candidate => candidate.QuerySelector("a[href^='/rooms/']") is not null);
        IElement button = row.QuerySelectorAll("button[aria-label^='Actions for ']").Single();
        await cut.InvokeAsync(() => button.ClickAsync());
    }

    /// <summary>Every open menu item of a <c>MudMenu</c>.</summary>
    /// <param name="cut">The rendered list.</param>
    /// <returns>The menu items, in order.</returns>
    private static IReadOnlyList<AngleSharp.Dom.IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    /// <summary>Clicks the open menu's item labelled <paramref name="text"/> and waits for its handler to finish.</summary>
    /// <param name="cut">The rendered list.</param>
    /// <param name="text">The item's text.</param>
    private static async Task ClickMenuItemAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        await cut.InvokeAsync(() => RoomCopyLinkTests.MenuItems(cut).First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)).ClickAsync());
    }
}
