namespace Agency.Huddle.Tests.Ui;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;

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
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await cut.InvokeAsync(() => cut.Find("div.hover-reveal-row").ContextMenuAsync());

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
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);

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
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);
        await ClickMenuItemAsync(cut, "Delete");

        Assert.NotNull(await directory.GetRoomAsync(room.Id, ct));
        Assert.True(HasButton(cut, "Delete"));
    }

    /// <summary>Confirming the inline Delete step actually removes the Room, proven against the real <see cref="ITeamDirectory"/>.</summary>
    [Fact]
    public async Task Delete_Confirmed_RemovesTheRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);
        await ClickMenuItemAsync(cut, "Delete");
        await ClickButtonAsync(cut, "Delete");

        Assert.Null(await directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>Archiving through the row menu is the mirror of the sidebar filter test above - it must take the Room out of this same live list.</summary>
    [Fact]
    public async Task Archive_RemovesTheRoomFromTheSidebarList()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);
        await ClickMenuItemAsync(cut, "Archive");

        cut.WaitForAssertion(() => Assert.DoesNotContain(room.Name, cut.Markup, StringComparison.Ordinal));
        Assert.True((await directory.GetRoomAsync(room.Id, ct))?.Archived);
    }

    /// <summary>The All list is one "All" folder with no "Rooms" header or "Needs you" folder while nothing waits, and individual room links carry no icons.</summary>
    [Fact]
    public async Task Rooms_AreListedWithoutAHeader_AndWithoutIndividualIcons()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.Equal(["All"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".sidebar-show-all"));

        var roomLink = cut.Find(".hover-reveal-link");
        Assert.Equal(room.Name, NameText(roomLink));
        Assert.Empty(roomLink.QuerySelectorAll(".mud-nav-link-icon")); // UX-016: a 1:1 row has an avatar, never the icon.
        Assert.Single(roomLink.QuerySelectorAll(".mud-avatar"));
    }

    /// <summary>When there are no rooms, the list shows the empty state.</summary>
    [Fact]
    public async Task Rooms_WhenEmpty_RendersTheEmptyState()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.Contains("No rooms yet", cut.Find(".mud-nav-group").TextContent, StringComparison.Ordinal);
    }

    /// <summary>A Room scoped to a Team in the catalog belongs under that Team in the sidebar, so the root Rooms group does not list it (ADR-0052).</summary>
    [Fact]
    public async Task LoadAsync_RoomScopedToAKnownTeam_IsNotListedInRooms()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        _ = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, agent.Id], "Marketing", teamRoom: true, ct);
        var rootRoom = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: KnownTeam("Marketing")).Chat;

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.DoesNotContain("Marketing", cut.Find(".mud-nav-group").TextContent, StringComparison.Ordinal);
        Assert.Equal(rootRoom.Name, NameText(Assert.Single(cut.FindAll(".hover-reveal-link a"))));
    }

    /// <summary>T12: a Room whose Team has left the catalog is listed in Rooms with a muted "(Team)" after its name, and still opens /rooms/{id}.</summary>
    [Fact]
    public async Task LoadAsync_RoomOfAVanishedTeam_IsListedWithMutedTeamLabel()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var orphan = await directory.CreateRoomAsync("Launch plan", [KnownIds.Human, agent.Id], "Marketing", teamRoom: false, ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: KnownTeam("Sales")).Chat;

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        var link = cut.Find(".hover-reveal-link a");
        Assert.Equal($"/rooms/{orphan.Id}", link.GetAttribute("href"));
        Assert.Equal("Launchplan(Marketing)", NameText(link).Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Equal("(Marketing)", link.QuerySelector(".room-team-label")?.TextContent.Trim());
    }

    /// <summary>An archived Room of a vanished Team stays out of the sidebar like any archived Room: the orphan rule only rescues unarchived ones.</summary>
    [Fact]
    public async Task LoadAsync_ArchivedRoomOfAVanishedTeam_IsNotListed()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var orphan = await directory.CreateRoomAsync("Launch plan", [KnownIds.Human, agent.Id], "Marketing", teamRoom: false, ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: KnownTeam("Sales")).Chat;
        _ = await chat.SetRoomArchivedAsync(orphan.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.DoesNotContain("Launch plan", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The orphan row stops being listed at once when its Team returns to the catalog: the list follows <see cref="Agency.Huddle.App.Teams.ITeamCatalog.Changed"/>, with Team names compared ignoring case.</summary>
    [Fact]
    public async Task CatalogChanged_TeamReturns_RemovesTheOrphanRow()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        _ = await directory.CreateRoomAsync("Launch plan", [KnownIds.Human, agent.Id], "Marketing", teamRoom: false, ct);
        var catalog = KnownTeam("Sales");
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: catalog).Chat;
        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        Assert.Single(cut.FindAll(".hover-reveal-link a"));

        catalog.Teams = [new Agency.Huddle.App.Teams.TeamSummary("marketing", [], ["coo"], true)];
        catalog.Raise();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".hover-reveal-link a")));
    }

    /// <summary>Disposing the list unsubscribes it from the catalog, which is a singleton that would otherwise throw into a dead component.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesFromTheCatalog()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var catalog = KnownTeam("Sales");
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: catalog).Chat;
        await using var ctx = NewContext(directory, chat, events);
        _ = RenderRoomList(ctx);
        Assert.Equal(1, catalog.ChangedSubscriberCount);

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(0, catalog.ChangedSubscriberCount);
    }

    /// <summary>The Delete confirm of a Room with uploads adds the Recycle Bin sentence with the file count (ChatUploads spec 7).</summary>
    [Theory]
    [InlineData(1, "Its uploaded file goes to the Recycle Bin.")]
    [InlineData(3, "Its 3 uploaded files go to the Recycle Bin.")]
    public async Task Delete_RoomWithUploads_ConfirmMentionsUploadCount(int count, string sentence)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = count };
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);
        await ClickMenuItemAsync(cut, "Delete");

        Assert.Equal("Delete?", cut.Find(".inline-confirm-prompt").TextContent.Trim());
        Assert.Equal($"Delete \"{room.Name}\"? {sentence}", cut.Find(".room-list-confirm-host").GetAttribute("title"));
    }

    /// <summary>The Delete confirm of a Room without uploads keeps the plain prompt.</summary>
    [Fact]
    public async Task Delete_RoomWithoutUploads_ConfirmHasNoUploadSentence()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = 0 };
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await OpenRowMenuAsync(cut);
        await ClickMenuItemAsync(cut, "Delete");

        Assert.Equal("Delete?", cut.Find(".inline-confirm-prompt").TextContent.Trim());
        Assert.Equal($"Delete \"{room.Name}\"?", cut.Find(".room-list-confirm-host").GetAttribute("title"));
    }

    /// <summary>The Chats view offers "Group by: All | Team", with All selected until the Human picks Team.</summary>
    [Fact]
    public async Task GroupBy_OffersAllAndTeam_WithAllSelected()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);

        Assert.Equal("Group by:", cut.Find(".room-list-grouping-label").TextContent.Trim());
        Assert.Equal(["All", "Team"], cut.FindAll(".room-list-grouping-control .mud-toggle-item").Select(item => item.TextContent.Trim()));
        Assert.Equal(["All"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim()));
    }

    /// <summary>Picking Team lists every Room under its Team's heading, the Rooms of no Team first, and remembers the choice.</summary>
    [Fact]
    public async Task GroupBy_Team_ListsEveryRoomUnderItsTeamHeading_AndRemembersTheChoice()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var teamRoom = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, agent.Id], "Marketing", teamRoom: true, ct);
        var rootRoom = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events, teams: KnownTeam("Marketing")).Chat;

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        await cut.InvokeAsync(() => cut.FindAll(".room-list-grouping-control .mud-toggle-item").First(item => item.TextContent.Trim() == "Team").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["No team", "Marketing"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim())));
        Assert.Equal([rootRoom.Name, teamRoom.Name], cut.FindAll(".hover-reveal-link a").Select(NameText));
        Assert.Contains(ctx.JSInterop.Invocations, call => call.Identifier == "huddleStorage.set" && call.Arguments.Count == 2 && Equals(call.Arguments[1], "Team"));
    }

    /// <summary>A Room waiting on the Human wears the "Needs you" pill on its row, and loses it when the wait ends.</summary>
    [Fact]
    public async Task Row_WaitingOnTheHuman_ShowsTheNeedsYouPill()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        Assert.Empty(cut.FindAll(".needs-you-pill"));
        var tracker = ctx.Services.GetRequiredService<Agency.Huddle.App.NeedsYou.NeedsYouTracker>();

        tracker.Seed(room.Id, DateTimeOffset.UtcNow, "coo");

        cut.WaitForAssertion(() => Assert.Equal("Needs you", Assert.Single(cut.FindAll(".needs-you-pill")).TextContent.Trim()));
        tracker.Dismiss(room.Id);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-pill")));
    }

    /// <summary>In All, the chats waiting on the Human sit in a "Needs you" folder above the "All" folder, and a long list shows every chat with no "Show all" row.</summary>
    [Fact]
    public async Task GroupBy_All_PutsWaitingRoomsInANeedsYouFolderAboveTheRest_AndNeverFolds()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        List<Room> made = [];
        for (int i = 0; i < 10; i++)
        {
            made.Add(await directory.CreateRoomAsync($"Chat {i}", [KnownIds.Human, agent.Id], ct));
        }

        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        await using var ctx = NewContext(directory, chat, events);
        var cut = RenderRoomList(ctx);
        Assert.Equal(["All"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim()));

        ctx.Services.GetRequiredService<Agency.Huddle.App.NeedsYou.NeedsYouTracker>().Seed(made[7].Id, DateTimeOffset.UtcNow, "coo");

        cut.WaitForAssertion(() => Assert.Equal(["Needs you", "All"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim())));
        var folders = cut.FindAll(".room-folder");
        Assert.Equal([made[7].Name], folders[0].QuerySelectorAll(".sidebar-row-label").Select(label => label.TextContent.Trim()));
        Assert.Equal(9, folders[1].QuerySelectorAll(".hover-reveal-link a").Length);
        Assert.Empty(cut.FindAll(".sidebar-show-all"));
    }

    /// <summary>A Team's heading is a folder: clicking it closes it, hiding its Rooms, and the closed set is remembered.</summary>
    [Fact]
    public async Task GroupBy_Team_ClickingAFolder_ClosesIt()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var rootRoom = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;

        await using var ctx = NewContext(directory, chat, events);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "roomList:grouping").SetResult("Team");
        var cut = RenderRoomList(ctx);
        cut.WaitForAssertion(() => Assert.Contains(rootRoom.Name, cut.Markup, StringComparison.Ordinal));
        await cut.InvokeAsync(() => cut.Find(".room-folder > .mud-nav-link").ClickAsync());

        cut.WaitForAssertion(() => Assert.Contains(ctx.JSInterop.Invocations, call => call.Identifier == "huddleStorage.set" && call.Arguments.Count == 2 && Equals(call.Arguments[0], "roomList:closedFolders") && Equals(call.Arguments[1], "[\"\"]")));
    }

    /// <summary>A remembered Team grouping is applied when the list first renders.</summary>
    [Fact]
    public async Task GroupBy_RememberedTeam_IsAppliedOnFirstRender()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        _ = await directory.CreateRoomAsync("Lunch plans", [KnownIds.Human, agent.Id], ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;

        await using var ctx = NewContext(directory, chat, events);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "roomList:grouping").SetResult("Team");
        var cut = RenderRoomList(ctx);

        cut.WaitForAssertion(() => Assert.Equal(["No team"], cut.FindAll(".room-folder > .mud-nav-link .mud-nav-link-text").Select(header => header.TextContent.Trim())));
    }

    /// <summary>Groups put the Rooms of no Team first, then the Teams by name ignoring case, each with its Team Room first; an empty Team makes no group.</summary>
    [Fact]
    public void RoomGroups_ByTeam_OrdersGroupsAndRooms()
    {
        DateTimeOffset start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        Room later = new("a", "Later", start.AddDays(2));
        Room earlier = new("b", "Earlier", start);
        Room sales = new("c", "Sales chat", start) { Team = "sales" };
        Room marketing = new("d", "Plan", start.AddDays(1)) { Team = "Marketing" };
        Room marketingTeamRoom = new("e", "Marketing", start.AddDays(3)) { Team = "Marketing", IsTeamRoom = true };

        var groups = RoomGroups.ByTeam([later, sales, marketing, earlier, marketingTeamRoom]);

        Assert.Equal([null, "Marketing", "sales"], groups.Select(group => group.Team));
        Assert.Equal(["b", "a"], groups[0].Rooms.Select(room => room.Id));
        Assert.Equal(["e", "d"], groups[1].Rooms.Select(room => room.Id));
    }

    private static Agency.Huddle.Tests.Teams.FakeTeamCatalog KnownTeam(string name) =>
        new() { Teams = [new Agency.Huddle.App.Teams.TeamSummary(name, [], ["coo"], true)] };

    /// <summary>When there are rooms, the collapsible "Rooms" group also offers the "New chat" control inside it.</summary>
    [Fact]
    public async Task Rooms_OffersNewChatControlInsideNavGroup()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;

        await using var ctx = NewContext(directory, chat, events, dir);
        var cut = RenderRoomList(ctx);

        var group = cut.Find(".mud-nav-group");
        Assert.Contains("New chat", group.TextContent, StringComparison.Ordinal);
    }

    /// <summary>The link's text without the 1:1 avatar's monogram (UX-016), trimmed.</summary>
    /// <param name="link">The row's link element.</param>
    private static string NameText(IElement link)
    {
        IElement copy = (IElement)link.Clone(deep: true);
        foreach (IElement avatar in copy.QuerySelectorAll(".mud-avatar"))
        {
            avatar.Remove();
        }

        return copy.TextContent.Trim();
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir? dir = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(chat.TeamCatalog ??new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddNeedsYouTracker();
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir?.Options() ?? TestOptions.Create(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderRoomList(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });

    /// <summary>
    /// Clicks the row's "..." button, opening its MudMenu popover - a real click on a real <c>button</c>, so this doubles as proof the trigger is keyboard-reachable rather than right-click-only.
    /// Awaits the click's own dispatch: bUnit's synchronous <c>Click()</c> discards the dispatch task, so under load a render already holding the renderer dispatcher defers the handler past the return.
    /// </summary>
    private static async Task OpenRowMenuAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find("button[aria-label^='Actions for ']").ClickAsync());
    }

    /// <summary>Every open <c>MudMenu</c> item - rendered with <c>role="menuitem"</c> and class <c>mud-menu-item</c>, not <c>mud-list-item</c> (that class belongs to <c>MudSelect</c>'s own popover).</summary>
    private static IReadOnlyList<IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    /// <summary>Clicks the open menu's item labelled <paramref name="text"/> and waits for its handler to finish, so a handler that awaits the directory has written before the test asserts.</summary>
    private static async Task ClickMenuItemAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        await cut.InvokeAsync(() => MenuItems(cut).First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)).ClickAsync());
    }

    /// <summary>Finds the button labelled <paramref name="text"/> and clicks it in one dispatcher turn, waiting for its handler to finish.</summary>
    private static async Task ClickButtonAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        await cut.InvokeAsync(() => FindButton(cut, text).ClickAsync());
    }

    private static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));
}
