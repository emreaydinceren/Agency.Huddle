using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the Archived page (<c>/archived</c>): three tabs, Teams | Projects | Chats, each a single-select list
/// (<c>.archived-row</c>, the selected one <c>.selected-shape</c>) under a toolbar whose <c>button.archived-restore</c>
/// restores the selected row. The Chats tab also has <c>button.archived-delete</c>, asked first with the inline
/// confirm. The page replaced the Archived chats dialog, so the dialog's tests moved here; the chat tests run
/// against the real <see cref="ITeamDirectory"/> and <see cref="ChatService"/>, the Team and Project tests against
/// a <see cref="RecordingArchiveService"/>.
/// </summary>
public sealed class ArchivedPageTests
{
    /// <summary>An archived Room is invisible in the sidebar, so it must be listed here, with a Restore control.</summary>
    [Fact]
    public async Task ChatsTab_ListsAnArchivedRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        Assert.Equal([room.Name], RowNames(cut));
        Assert.Equal("Restore", cut.Find("button.archived-restore").TextContent.Trim());
    }

    /// <summary>Restore stays disabled until a row is selected; a click selects the row and marks it with the Selected shape.</summary>
    [Fact]
    public async Task ChatsTab_Restore_IsDisabledUntilARowIsSelected()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        Assert.True(cut.Find("button.archived-restore").HasAttribute("disabled"));

        await cut.InvokeAsync(() => Row(cut, room.Name).ClickAsync());

        Assert.False(cut.Find("button.archived-restore").HasAttribute("disabled"));
        Assert.Equal("true", Row(cut, room.Name).GetAttribute("aria-selected"));
        Assert.Contains("selected-shape", Row(cut, room.Name).ClassList);
    }

    /// <summary>A Room archived just now reads "Archived today": the relative day word is lower case in the middle of a phrase (#472).</summary>
    [Fact]
    public async Task ChatsTab_RoomArchivedToday_ReadsArchivedTodayInLowerCase()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, _) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        Assert.Equal("Archived today", cut.Find(".archived-row-date").TextContent.Trim());
    }

    /// <summary>A Room that was never archived is not listed, and an empty tab shows its empty state with no toolbar.</summary>
    [Fact]
    public async Task ChatsTab_NonArchivedRoom_IsNotListed()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        Assert.Empty(cut.FindAll(".archived-row"));
        Assert.Empty(cut.FindAll(".archived-toolbar"));
        Assert.Equal("No archived chats", cut.Find(".empty-state-title").TextContent.Trim());
    }

    /// <summary>
    /// The live end-to-end path: Restore on the page makes the Room reappear in the sidebar's own <see cref="RoomList"/>,
    /// rendered alongside in the same fragment, and the page's row goes; both react to the same
    /// <see cref="RoomEvents.RoomsChanged"/> publish.
    /// </summary>
    [Fact]
    public async Task ChatsTab_Restore_ReturnsTheRoomToTheSidebar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
            builder.OpenComponent<ArchivedPage>(2);
            builder.AddAttribute(3, nameof(ArchivedPage.Tab), "chats");
            builder.CloseComponent();
        });
        Assert.Empty(SidebarRoomNames(cut));

        await cut.InvokeAsync(() => Row(cut, room.Name).ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([room.Name], SidebarRoomNames(cut)));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".archived-row")));
        Assert.False((await directory.GetRoomAsync(room.Id, ct))?.Archived);
    }

    /// <summary>A double-click restores the row at once, with no Restore press.</summary>
    [Fact]
    public async Task ChatsTab_DoubleClick_RestoresTheRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        await cut.InvokeAsync(() => Row(cut, room.Name).DoubleClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".archived-row")));
        Assert.False((await directory.GetRoomAsync(room.Id, ct))?.Archived);
    }

    /// <summary>Delete is guarded by the same inline confirm the sidebar's row menu uses: one click must not remove the Room.</summary>
    [Fact]
    public async Task ChatsTab_Delete_OneClick_DoesNotRemoveTheRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        await BeginDeleteAsync(cut, room.Name);

        Assert.NotNull(await directory.GetRoomAsync(room.Id, ct));
        Assert.Equal("Delete", cut.Find("button.inline-confirm-confirm").TextContent.Trim());
    }

    /// <summary>Confirming Delete permanently removes the Room, proven against the real <see cref="ITeamDirectory"/>.</summary>
    [Fact]
    public async Task ChatsTab_Delete_Confirmed_RemovesTheRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        await BeginDeleteAsync(cut, room.Name);
        await cut.InvokeAsync(() => cut.Find("button.inline-confirm-confirm").ClickAsync());

        Assert.Null(await directory.GetRoomAsync(room.Id, ct));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".archived-row")));
        Assert.Empty(cut.FindAll(".inline-confirm-confirm"));
    }

    /// <summary>A chat restored elsewhere while its Delete confirm is open drops its row, its selection and the confirm, so Delete cannot reach a Room the page no longer lists.</summary>
    [Fact]
    public async Task ChatsTab_RowRestoredElsewhere_DropsTheSelectionAndTheConfirm()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);
        Room other = await directory.CreateRoomAsync("Other", [KnownIds.Human], ct);
        _ = await chat.SetRoomArchivedAsync(other.Id, archived: true, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        await BeginDeleteAsync(cut, room.Name);

        await cut.InvokeAsync(() => chat.SetRoomArchivedAsync(room.Id, archived: false, ct));

        cut.WaitForAssertion(() => Assert.Equal(["Other"], RowNames(cut)));
        Assert.Empty(cut.FindAll(".inline-confirm-confirm"));
        Assert.Empty(cut.FindAll(".archived-row.selected-shape"));
        Assert.True(cut.Find("button.archived-restore").HasAttribute("disabled"));
    }

    /// <summary>Spec section 7: a Team-scoped Room's row reads "Room · Team", and a root Room's row keeps just its name.</summary>
    [Fact]
    public async Task ChatsTab_TeamScopedRoom_ReadsRoomDotTeam_AndRootRoomIsUnchanged()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room rootRoom) = await ArchivedRoomAsync(dir, ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        Room teamRoom = await directory.CreateRoomAsync("Launch plan", [KnownIds.Human, agent.Id], "Marketing", false, ct: ct);
        _ = await chat.SetRoomArchivedAsync(teamRoom.Id, archived: true, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        string[] rows = [.. cut.FindAll(".archived-row .archived-row-text").Select(element => element.TextContent.Trim())];
        Assert.Contains("Launch plan · Marketing", rows);
        Assert.Contains(rootRoom.Name, rows);
        Assert.DoesNotContain(rows, row => row.StartsWith(rootRoom.Name, StringComparison.Ordinal) && row.Contains('·'));
    }

    /// <summary>A Project's Room reads "Room · Team / Project", so it is told apart from a Room of the Team itself (ADR-0082).</summary>
    [Fact]
    public async Task ChatsTab_ProjectRoom_ReadsRoomDotTeamSlashProject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        Room projectRoom = await directory.CreateRoomAsync("Kickoff", [KnownIds.Human, agent.Id], "Marketing", false, project: "Q3 launch", ct: ct);
        _ = await chat.SetRoomArchivedAsync(projectRoom.Id, archived: true, ct);

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");

        Assert.Equal("Kickoff · Marketing / Q3 launch", cut.Find(".archived-row .archived-row-text").TextContent.Trim());
    }

    /// <summary>The Delete confirm adds the Recycle Bin sentence with the file count when the Room has uploads (ChatUploads spec 7).</summary>
    /// <param name="count">The Room's upload count.</param>
    /// <param name="sentence">The sentence the confirm must add.</param>
    [Theory]
    [InlineData(1, "Its uploaded file goes to the Recycle Bin.")]
    [InlineData(2, "Its 2 uploaded files go to the Recycle Bin.")]
    public async Task ChatsTab_Delete_RoomWithUploads_ConfirmMentionsUploadCount(int count, string sentence)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = count };

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        await BeginDeleteAsync(cut, room.Name);

        Assert.Equal($"Delete \"{room.Name}\"? {sentence}", cut.Find(".inline-confirm-prompt").TextContent.Trim());
    }

    /// <summary>The Delete confirm keeps the plain prompt when the Room has no uploads.</summary>
    [Fact]
    public async Task ChatsTab_Delete_RoomWithoutUploads_ConfirmHasNoUploadSentence()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ITeamDirectory directory, ChatService chat, RoomEvents events, Room room) = await ArchivedRoomAsync(dir, ct);
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = 0 };

        await using MudBunitContext ctx = NewContext(directory, chat, events);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chats");
        await BeginDeleteAsync(cut, room.Name);

        Assert.Equal($"Delete \"{room.Name}\"?", cut.Find(".inline-confirm-prompt").TextContent.Trim());
    }

    /// <summary>The Teams tab lists the archived Teams; Restore on the selected one unarchives that Team once, and its row goes.</summary>
    [Fact]
    public async Task TeamsTab_Restore_UnarchivesTheSelectedTeam()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["Old", "Older"] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, new FakeTeamCatalog(), ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, null);
        Assert.Equal(["Old", "Older"], RowNames(cut));

        await cut.InvokeAsync(() => Row(cut, "Older").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Older"], archive.Unarchived));
        cut.WaitForAssertion(() => Assert.Equal(["Old"], RowNames(cut)));
    }

    /// <summary>A refused restore (T17) shows the service's reason as a warning snackbar, and the row stays.</summary>
    [Fact]
    public async Task TeamsTab_RefusedRestore_ShowsTheReasonAndKeepsTheRow()
    {
        const string T17 = "A Team named Home exists now. Rename one of them first.";
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["Home"], UnarchiveOutcome = new ArchiveOutcome(false, T17) };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, new FakeTeamCatalog(), ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await cut.InvokeAsync(() => Row(cut, "Home").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(T17, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
        Assert.Equal(["Home"], RowNames(cut));
    }

    /// <summary>With no archived Team the Teams tab is its empty state.</summary>
    [Fact]
    public async Task TeamsTab_NoArchivedTeams_ShowsTheEmptyState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = await NewTeamsContextAsync(new RecordingArchiveService(), new FakeTeamCatalog(), ct);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");

        Assert.Equal("No archived Teams", cut.Find(".empty-state-title").TextContent.Trim());
    }

    /// <summary>The Projects tab lists each live Team's archived Projects as "Project · Team", and Restore unarchives the selected one in its Team.</summary>
    [Fact]
    public async Task ProjectsTab_Restore_UnarchivesTheSelectedProjectInItsTeam()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedProjectNames = ["Beta", "Alpha"] };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", [], ["Nova"], true)] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, catalog, ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "projects");
        Assert.Equal(["Alpha · Business", "Beta · Business"], [.. cut.FindAll(".archived-row .archived-row-text").Select(element => element.TextContent.Trim())]);

        await cut.InvokeAsync(() => Row(cut, "Beta").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Business", "Beta")], archive.UnarchivedProjects));
        cut.WaitForAssertion(() => Assert.Equal(["Alpha"], RowNames(cut)));
    }

    /// <summary>Two Teams may each have an archived Project of the same name: both are listed, each with its Team, and Restore unarchives the selected one in its own Team only.</summary>
    [Fact]
    public async Task ProjectsTab_SameNameInTwoTeams_RestoresTheOneInTheSelectedTeam()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new()
        {
            ArchivedProjectsByTeam = new(StringComparer.OrdinalIgnoreCase) { ["Business"] = ["Alpha"], ["Ops"] = ["Alpha"] },
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Ops", [], ["Nova"], true), new TeamSummary("Business", [], ["Nova"], true)] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, catalog, ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "projects");
        Assert.Equal(["Alpha · Business", "Alpha · Ops"], RowTexts(cut));

        await cut.InvokeAsync(() => cut.FindAll(".archived-row")[1].ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Ops", "Alpha")], archive.UnarchivedProjects));
        cut.WaitForAssertion(() => Assert.Equal(["Alpha · Business"], RowTexts(cut)));
    }

    /// <summary>A refused Project restore (a live Project took the name) shows the reason as a warning snackbar, and the row stays.</summary>
    [Fact]
    public async Task ProjectsTab_RefusedRestore_ShowsTheReasonAndKeepsTheRow()
    {
        const string Reason = "A Project named Alpha exists in Business now. Rename one of them first.";
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedProjectNames = ["Alpha"], UnarchiveProjectOutcome = new ArchiveOutcome(false, Reason) };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", [], ["Nova"], true)] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, catalog, ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "projects");
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await cut.InvokeAsync(() => Row(cut, "Alpha").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-restore").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(Reason, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
        Assert.Equal(["Alpha"], RowNames(cut));
    }

    /// <summary>A selected Team that leaves the list (restored from elsewhere) is no longer selected, so Restore turns off.</summary>
    [Fact]
    public async Task TeamsTab_SelectedRowGone_DropsTheSelection()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["Old", "Older"] };
        FakeTeamCatalog catalog = new();
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, catalog, ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");
        await cut.InvokeAsync(() => Row(cut, "Older").ClickAsync());

        _ = archive.ArchivedTeamNames.Remove("Older");
        await cut.InvokeAsync(catalog.Raise);

        cut.WaitForAssertion(() => Assert.Equal(["Old"], RowNames(cut)));
        Assert.Empty(cut.FindAll(".archived-row.selected-shape"));
        Assert.True(cut.Find("button.archived-restore").HasAttribute("disabled"));
    }

    /// <summary>The list is one Tab stop: only the selected row (else the first) is tabbable; Down, Up, End and Home move the selection.</summary>
    [Fact]
    public async Task Keyboard_ArrowsHomeAndEnd_MoveTheSelection_AndTheTabStop()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["A", "B", "C"] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, new FakeTeamCatalog(), ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");
        Assert.Equal(["0", "-1", "-1"], cut.FindAll(".archived-row").Select(row => row.GetAttribute("tabindex")));

        await cut.InvokeAsync(() => Row(cut, "A").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.Equal(["B"], SelectedNames(cut));
        Assert.Equal(["-1", "0", "-1"], cut.FindAll(".archived-row").Select(row => row.GetAttribute("tabindex")));

        await cut.InvokeAsync(() => Row(cut, "B").KeyDownAsync(new KeyboardEventArgs { Key = "End" }));
        Assert.Equal(["C"], SelectedNames(cut));

        await cut.InvokeAsync(() => Row(cut, "C").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.Equal(["C"], SelectedNames(cut));

        await cut.InvokeAsync(() => Row(cut, "C").KeyDownAsync(new KeyboardEventArgs { Key = "Home" }));
        Assert.Equal(["A"], SelectedNames(cut));

        await cut.InvokeAsync(() => Row(cut, "A").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" }));
        Assert.Equal(["A"], SelectedNames(cut));
    }

    /// <summary>Enter on a focused row restores it, and the selection moves to the row that took its place, so the keyboard stays in the list.</summary>
    [Fact]
    public async Task Keyboard_Enter_RestoresTheRow_AndSelectsTheNextOne()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new() { ArchivedTeamNames = ["A", "B", "C"] };
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, new FakeTeamCatalog(), ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");

        await cut.InvokeAsync(() => Row(cut, "B").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        cut.WaitForAssertion(() => Assert.Equal(["A", "C"], RowNames(cut)));
        Assert.Equal(["B"], archive.Unarchived);
        Assert.Equal(["C"], SelectedNames(cut));
    }

    /// <summary>
    /// Inside a Tab the page reads its tab from the Tab's own address, not the route value (which a new address does not
    /// clear), and a tab click points that Tab at the new address.
    /// </summary>
    [Fact]
    public async Task InATab_ReadsTheTabsAddress_AndATabClickNavigatesTheTab()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = await NewTeamsContextAsync(new RecordingArchiveService(), new FakeTeamCatalog(), ct);
        Agency.Huddle.App.Tabs.TabService tabs = ctx.Services.GetRequiredService<Agency.Huddle.App.Tabs.TabService>();
        Agency.Huddle.App.Tabs.Tab tab = tabs.Open("/archived/chats", "Archived");
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<CascadingValue<Agency.Huddle.App.Tabs.TabPageContext>>(0);
            builder.AddAttribute(1, "Value", new Agency.Huddle.App.Tabs.TabPageContext(tab.Url, tab.Id));
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<ArchivedPage>(0);
                inner.AddAttribute(1, nameof(ArchivedPage.Tab), "projects");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal("Chats", cut.Find(".mud-tab.mud-tab-active").TextContent.Trim());

        await cut.InvokeAsync(() => cut.FindAll(".mud-tab").Single(header => header.TextContent.Trim() == "Teams").ClickAsync());

        Assert.Equal("/archived/teams", Assert.Single(tabs.Tabs).Url);
    }

    /// <summary>A Team archived elsewhere (the sidebar) appears on the open page when the catalog changes, with no reload.</summary>
    [Fact]
    public async Task TeamsTab_CatalogChange_ReadsTheListAgain()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        RecordingArchiveService archive = new();
        FakeTeamCatalog catalog = new();
        await using MudBunitContext ctx = await NewTeamsContextAsync(archive, catalog, ct);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "teams");
        Assert.Empty(cut.FindAll(".archived-row"));

        archive.ArchivedTeamNames.Add("Later");
        await cut.InvokeAsync(catalog.Raise);

        cut.WaitForAssertion(() => Assert.Equal(["Later"], RowNames(cut)));
    }

    /// <summary>The page draws the three tabs in order, the route's tab is the active one, and clicking another tab navigates to its address.</summary>
    [Fact]
    public async Task Tabs_FollowTheRoute_AndAClickNavigates()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = await NewTeamsContextAsync(new RecordingArchiveService(), new FakeTeamCatalog(), ct);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "projects");

        Assert.Equal(["Teams", "Projects", "Chats"], [.. cut.FindAll(".mud-tab").Select(tab => tab.TextContent.Trim())]);
        Assert.Equal("Projects", cut.Find(".mud-tab.mud-tab-active").TextContent.Trim());

        await cut.InvokeAsync(() => cut.FindAll(".mud-tab").Single(tab => tab.TextContent.Trim() == "Chats").ClickAsync());

        Assert.Equal("http://localhost/archived/chats", navigation.Uri);
    }

    /// <summary>The names of the Rooms the sidebar's <see cref="RoomList"/> lists.</summary>
    private static string[] SidebarRoomNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("div.room-list .room-nav-row .sidebar-row-label").Select(element => element.TextContent.Trim())];

    /// <summary>Each listed row's name and detail as shown ("Alpha · Business"), in order.</summary>
    private static string[] RowTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".archived-row .archived-row-text").Select(element => element.TextContent.Trim())];

    /// <summary>The names of the selected rows: one at most.</summary>
    private static string[] SelectedNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".archived-row[aria-selected='true'] .archived-row-name").Select(element => element.TextContent.Trim())];

    /// <summary>The names of the listed rows, in order.</summary>
    private static string[] RowNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".archived-row .archived-row-name").Select(element => element.TextContent.Trim())];

    /// <summary>The listed row whose name is <paramref name="name"/>.</summary>
    private static IElement Row(IRenderedComponent<ContainerFragment> cut, string name) =>
        cut.FindAll(".archived-row").Single(row => string.Equals(row.QuerySelector(".archived-row-name")?.TextContent.Trim(), name, StringComparison.Ordinal));

    /// <summary>Selects the Room's row and presses Delete…, which swaps the toolbar for the inline confirm.</summary>
    private static async Task BeginDeleteAsync(IRenderedComponent<ContainerFragment> cut, string room)
    {
        await cut.InvokeAsync(() => Row(cut, room).ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.archived-delete").ClickAsync());
    }

    /// <summary>A real directory and chat service holding one archived one-to-one Room.</summary>
    private static async Task<(ITeamDirectory Directory, ChatService Chat, RoomEvents Events, Room Room)> ArchivedRoomAsync(TempDataDir dir, CancellationToken ct)
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        Room room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);
        return (directory, chat, events, room);
    }

    /// <summary>Renders the page on <paramref name="tab"/> (<see langword="null"/> for <c>/archived</c>), with popovers and the snackbar provider.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? tab) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ArchivedPage>(0);
            builder.AddAttribute(1, nameof(ArchivedPage.Tab), tab);
            builder.CloseComponent();
        });

    /// <summary>A context for the Team and Project tabs: no archived Room, and the given archive service and catalog.</summary>
    private static async Task<MudBunitContext> NewTeamsContextAsync(RecordingArchiveService archive, FakeTeamCatalog catalog, CancellationToken ct)
    {
        TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        MudBunitContext ctx = NewContext(directory, chat, events, catalog);

        // Registered through a factory, so the container owns it and deletes the folder when the context is disposed.
        ctx.Services.AddSingleton(_ => dir);
        ctx.Services.AddSingleton<IArchiveService>(archive);
        return ctx;
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, ITeamCatalog? catalog = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton(catalog ?? chat.TeamCatalog ?? new FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton(TimeProvider.System);
        ctx.Services.AddNeedsYouTracker();
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(TestOptions.Create(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }
}
