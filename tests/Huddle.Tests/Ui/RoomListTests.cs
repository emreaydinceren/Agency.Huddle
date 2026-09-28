namespace Agency.Huddle.Tests.Ui;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

/// <summary>
/// Renders the sidebar's <see cref="RoomList"/> through <see cref="MudBunitContext.RenderWithPopovers"/> -
/// its row menu is a <c>MudMenu</c>, which paints its popover as a sibling rather than a descendant
/// (see <see cref="MudBunitContext"/>'s own remarks), so a plain <c>Render&lt;T&gt;</c> could never see
/// it open. Real <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/> over a
/// <see cref="TempDataDir"/> throughout - no mocking framework, matching <see cref="InviteTeammateTests"/>.
/// The seeded Agent is deliberately named "coo" rather than "agent" - the empty-state copy itself
/// contains the word "agent" ("Start an agent to create one"), which would make a Room named "agent"
/// pass a DoesNotContain assertion for the wrong reason.
/// </summary>
public sealed class RoomListTests
{
    /// <summary>Archiving is a display filter, and this is the one behaviour the whole feature rests on: an archived Room must not be offered in the sidebar at all.</summary>
    [Fact]
    public async Task LoadAsync_ArchivedRoom_DoesNotAppearInTheSidebar()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.DoesNotContain(room.Name, cut.Markup, StringComparison.Ordinal);
        Assert.Contains("No rooms yet", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The sibling of the test above: a Room that was never archived is offered normally.</summary>
    [Fact]
    public async Task LoadAsync_NonArchivedRoom_AppearsInTheSidebar()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.Contains(room.Name, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Right-clicking the row is the second door onto the same menu - the row's own
    /// <c>@oncontextmenu</c> opens the identical <c>MudMenu</c> the "..." button does, proven here by
    /// the same Archive/Delete items appearing. The "..." button itself stays the REQUIRED door,
    /// since a right-click is not reachable by keyboard - this test only proves the second door is
    /// wired, not that it replaces the first.
    /// </summary>
    [Fact]
    public async Task RightClickingTheRow_OpensTheSameMenu()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        cut.Find("div.hover-reveal-row").ContextMenu();

        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Archive", StringComparison.Ordinal));
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete", StringComparison.Ordinal));
    }

    /// <summary>The row's "..." menu is the one door onto both actions - it must offer Archive and Delete together, not just one of the two.</summary>
    [Fact]
    public async Task RowMenu_OffersArchiveAndDelete()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        OpenRowMenu(cut);

        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Archive", StringComparison.Ordinal));
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete", StringComparison.Ordinal));
    }

    /// <summary>Delete is guarded by an inline confirm step - one click on the menu's Delete item must not remove the Room yet.</summary>
    [Fact]
    public async Task Delete_OneClick_DoesNotRemoveTheRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        OpenRowMenu(cut);
        ClickMenuItem(cut, "Delete");

        Assert.NotNull(await directory.GetRoomAsync(room.Id, ct));
        Assert.True(HasButton(cut, "Confirm"));
    }

    /// <summary>Confirming the inline Delete step actually removes the Room, proven against the real <see cref="ITeamDirectory"/>.</summary>
    [Fact]
    public async Task Delete_Confirmed_RemovesTheRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        OpenRowMenu(cut);
        ClickMenuItem(cut, "Delete");
        FindButton(cut, "Confirm").Click();

        Assert.Null(await directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>Archiving through the row menu is the mirror of the sidebar filter test above - it must take the Room out of this same live list.</summary>
    [Fact]
    public async Task Archive_RemovesTheRoomFromTheSidebarList()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        OpenRowMenu(cut);
        ClickMenuItem(cut, "Archive");

        cut.WaitForAssertion(() => Assert.DoesNotContain(room.Name, cut.Markup, StringComparison.Ordinal));
        Assert.True((await directory.GetRoomAsync(room.Id, ct))?.Archived);
    }

    /// <summary>Chats are grouped under a collapsible "Chats" MudNavGroup, and individual room links carry no icons.</summary>
    [Fact]
    public async Task Chats_AreGroupedUnderCollapsibleChatsNavGroup_WithoutIndividualIcons()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory, events);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        var groupHeader = cut.Find(".mud-nav-group .mud-nav-link-text");
        Assert.Equal("Chats", groupHeader.TextContent.Trim());

        var roomLink = cut.Find(".hover-reveal-link");
        Assert.Equal(room.Name, roomLink.TextContent.Trim());
        Assert.Empty(roomLink.QuerySelectorAll(".mud-nav-link-icon"));
    }

    /// <summary>When there are no rooms, the collapsible "Chats" group is still present and displays the empty state inside it.</summary>
    [Fact]
    public async Task Chats_WhenEmpty_RendersChatsNavGroupWithEmptyState()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var chat = CreateChatService(dir, directory, events);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        var groupHeader = cut.Find(".mud-nav-group .mud-nav-link-text");
        Assert.Equal("Chats", groupHeader.TextContent.Trim());
        Assert.Contains("No rooms yet", cut.Find(".mud-nav-group").TextContent, StringComparison.Ordinal);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, RoomEvents events)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var proposals = new ProposalStore(events);
        return new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
    }

    /// <summary>When there are rooms, the collapsible "Chats" group also offers the "New chat" control inside it.</summary>
    [Fact]
    public async Task Chats_OffersNewChatControlInsideNavGroup()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var chat = CreateChatService(dir, directory, events);

        await using var ctx = NewContext(directory, chat, events, dir);
        var cut = RenderRoomList(ctx);

        var group = cut.Find(".mud-nav-group");
        Assert.Contains("New chat", group.TextContent, StringComparison.Ordinal);
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir? dir = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir?.Options() ?? Options.Create(new TeamOptions()), NullLogger<AvatarStore>.Instance));
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderRoomList(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });

    /// <summary>Clicks the row's "..." button, opening its MudMenu popover - a real click on a real <c>button</c>, so this doubles as proof the trigger is keyboard-reachable rather than right-click-only.</summary>
    private static void OpenRowMenu(IRenderedComponent<ContainerFragment> cut)
    {
        cut.Find("button[aria-label='Room actions']").Click();
    }

    /// <summary>Every open <c>MudMenu</c> item - rendered with <c>role="menuitem"</c> and class <c>mud-menu-item</c>, not <c>mud-list-item</c> (that class belongs to <c>MudSelect</c>'s own popover).</summary>
    private static IReadOnlyList<IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    private static void ClickMenuItem(IRenderedComponent<ContainerFragment> cut, string text)
    {
        MenuItems(cut).First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)).Click();
    }

    private static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));
}
