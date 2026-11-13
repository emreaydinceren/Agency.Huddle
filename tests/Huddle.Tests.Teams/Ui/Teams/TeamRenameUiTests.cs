using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the rename UI (Team Rename Spec §5.1, §7, corrections-tr12): a Rename button on the Details tab, "Rename team…" above
/// "Archive team…" in both sidebar Team menus, "Rename project…" above "Archive project" in the Project menu, the name dialog
/// (current name, live check through the service), the confirmation, one service call on Rename, nothing on Cancel, and a refusal
/// as a Warning snackbar. The UI talks to <see cref="ITeamRenamer"/>, which <see cref="TeamRenameService"/> implements.
/// </summary>
public sealed class TeamRenameUiTests
{
    /// <summary>The Details tab has one Rename button, beside Archive in the same row.</summary>
    [Fact]
    public async Task DetailsTab_ShowsRenameButtonBesideArchive()
    {
        await using MudBunitContext ctx = NewDetailsContext(new RecordingTeamRenamer());

        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);

        IElement rename = Assert.Single(cut.FindAll("button.team-details-rename"));
        Assert.Equal("Rename", rename.TextContent.Trim());
        Assert.Same(cut.Find("button.team-details-archive").ParentElement, rename.ParentElement);
    }

    /// <summary>Clicking Rename opens the name dialog "Rename team", labelled Name, holding the current name, with Cancel and Rename.</summary>
    [Fact]
    public async Task DetailsRename_OpensNameDialogWithCurrentName()
    {
        await using MudBunitContext ctx = NewDetailsContext(new RecordingTeamRenamer());
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);

        await ClickDetailsRenameAsync(cut);

        Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Home", cut.Find(".mud-dialog input").GetAttribute("value"));
        Assert.Equal("Name", cut.Find(".mud-dialog label").TextContent.Trim());
        Assert.Equal(["Cancel", "Rename"], [.. cut.FindAll(".mud-dialog-actions button").Select(static b => b.TextContent.Trim())]);
    }

    /// <summary>Typing a name asks the service's check live: its sentence shows under the field and Rename is disabled.</summary>
    [Fact]
    public async Task NameDialog_TypedNameRefusedByTheCheck_ShowsItLiveAndDisablesRename()
    {
        RecordingTeamRenamer renamer = new() { TeamCheck = static (_, name) => name == "Sales" ? "A Team named \"Sales\" already exists." : null };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);

        await cut.InvokeAsync(() => cut.Find(".mud-dialog input").InputAsync("Sales"));

        cut.WaitForAssertion(() => Assert.Equal("A Team named \"Sales\" already exists.", cut.Find(".mud-dialog .mud-input-helper-text").TextContent.Trim()));
        Assert.True(ArchiveTeamUiTests.DialogButton(cut, "Rename").HasAttribute("disabled"));
        Assert.Contains(("Home", "Sales"), renamer.TeamChecks);
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>Cancel in the name dialog renames nothing and opens no confirmation.</summary>
    [Fact]
    public async Task NameDialog_Cancel_RenamesNothingAndAsksNothing()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>
    /// Renaming from the name dialog opens the confirmation: the title names both names, the message is the Team's, the points are
    /// the builder's (here with the one member mid-Turn), and the actions are Cancel then an Error-coloured Rename. Nothing is renamed yet.
    /// </summary>
    [Fact]
    public async Task NameDialog_Rename_OpensTheConfirmationWithThePoints_RenamingNothingYet()
    {
        RecordingTeamRenamer renamer = new() { MidTurn = ["Nova"] };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);

        await SubmitNameAsync(cut, "Growth", "Rename Home to Growth?");

        Assert.Equal(TeamRenameConfirmation.Message(TeamRenameKind.Team), cut.Find(".mud-dialog-content p").TextContent.Trim());
        IReadOnlyList<IElement> items = cut.FindAll(".mud-dialog-content li");
        Assert.Equal(5, items.Count);
        Assert.Equal(
            "1 member restarts and starts a new conversation. Each one keeps a summary of recent Messages but loses its working context. Nova is in the middle of a Turn and loses that Turn's work.",
            items[0].TextContent.Trim());
        Assert.Equal("1 member restarts and starts a new conversation.", items[0].QuerySelector("strong, b")?.TextContent);
        Assert.Equal(["Cancel", "Rename"], [.. cut.FindAll(".mud-dialog-actions button").Select(static b => b.TextContent.Trim())]);
        Assert.Contains("mud-button-filled-error", ArchiveTeamUiTests.DialogButton(cut, "Rename").ClassList);
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>Cancel in the confirmation renames nothing.</summary>
    [Fact]
    public async Task Confirmation_Cancel_RenamesNothing()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);
        await SubmitNameAsync(cut, "Growth", "Rename Home to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>Rename in the confirmation calls the service once with the old and the new name, and the Team page follows to the new Team.</summary>
    [Fact]
    public async Task Confirmation_Rename_CallsTheServiceOnce_AndGoesToTheNewTeam()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Home/details");
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);
        await SubmitNameAsync(cut, "Growth", "Rename Home to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Home", "Growth")], renamer.TeamsRenamed));
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/teams/Growth/details", navigation.Uri));
    }

    /// <summary>A refused rename shows the service's sentence as a Warning snackbar and stays on the Team's page.</summary>
    [Fact]
    public async Task Confirmation_Refused_ShowsAWarningSnackbarAndStays()
    {
        const string Reason = "Home has files open in another program. Close them and try again.";
        RecordingTeamRenamer renamer = new() { Outcome = new TeamRenameOutcome(false, Reason) };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Home/details");
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await ClickDetailsRenameAsync(cut);
        await SubmitNameAsync(cut, "Growth", "Rename Home to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(Reason, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
        Assert.Equal("http://localhost/teams/Home/details", navigation.Uri);
    }

    /// <summary>The Team row's menu (every Team listed) has Rename team… directly above Archive team….</summary>
    [Fact]
    public async Task Sidebar_TeamRowMenu_RenameTeamSitsAboveArchiveTeam()
    {
        await using MudBunitContext ctx = NewNavContext(new RecordingTeamRenamer());
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());

        List<string> items = [.. cut.FindAll(".mud-menu-item").Select(static i => i.TextContent.Trim())];
        Assert.Equal(items.IndexOf("Archive team…") - 1, items.IndexOf("Rename team…"));
        Assert.True(items.IndexOf("Rename team…") >= 0);
    }

    /// <summary>The Team header's menu in the Teams view has Rename team… directly above Archive team….</summary>
    [Fact]
    public async Task Sidebar_TeamsViewHeaderMenu_RenameTeamSitsAboveArchiveTeam()
    {
        await using MudBunitContext ctx = NewNavContext(new RecordingTeamRenamer());
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-header button[aria-label='Actions for Live']").ClickAsync());

        Assert.Equal(["Rename team…", "Archive team…"], [.. cut.FindAll(".mud-menu-item").Select(static i => i.TextContent.Trim())]);
    }

    /// <summary>A Project row's menu has Rename project… directly above Archive project.</summary>
    [Fact]
    public async Task Sidebar_ProjectRowMenu_RenameProjectSitsAboveArchiveProject()
    {
        await using MudBunitContext ctx = NewNavContext(new RecordingTeamRenamer(), ProjectCatalog());
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());

        Assert.Equal(["Rename project…", "Archive project"], [.. cut.FindAll(".mud-menu-item").Select(static i => i.TextContent.Trim())]);
    }

    /// <summary>
    /// Rename team… closes its menu before the name dialog opens, starts from the current name, and the whole flow renames the Team
    /// once; the page stays put when the address is not the old Team's.
    /// </summary>
    [Fact]
    public async Task Sidebar_RenameTeam_ClosesTheMenu_AsksTheName_ThenConfirms_ThenRenamesOnce()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());

        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal("Live", cut.Find(".mud-dialog input").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".mud-popover-open .teams-nav-rename-team"));
        await SubmitNameAsync(cut, "Growth", "Rename Live to Growth?");
        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Live", "Growth")], renamer.TeamsRenamed));
        Assert.Equal(before, navigation.Uri);
        Assert.Empty(navigation.History);
    }

    /// <summary>The Team header's Rename team… (Teams view) runs the same flow for its Team.</summary>
    [Fact]
    public async Task Sidebar_TeamsViewHeader_RenameTeam_RenamesOnce()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);
        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-header button[aria-label='Actions for Live']").ClickAsync());

        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Growth", "Rename Live to Growth?");
        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Live", "Growth")], renamer.TeamsRenamed));
    }

    /// <summary>Cancel in the sidebar's confirmation renames nothing.</summary>
    [Fact]
    public async Task Sidebar_RenameTeam_CancelInTheConfirmation_RenamesNothing()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Growth", "Rename Live to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>A refusal from the sidebar shows as a Warning snackbar.</summary>
    [Fact]
    public async Task Sidebar_RenameTeam_Refused_ShowsAWarningSnackbar()
    {
        const string Reason = "Another rename is still finishing. Try again after Huddle restarts.";
        RecordingTeamRenamer renamer = new() { Outcome = new TeamRenameOutcome(false, Reason) };
        await using MudBunitContext ctx = NewNavContext(renamer);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Growth", "Rename Live to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(Reason, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
    }

    /// <summary>When the address is the old Team's page, a sidebar rename replaces it with the new Team's page.</summary>
    [Fact]
    public async Task Sidebar_RenameTeam_OnTheOldTeamsPage_NavigatesToTheNewOneWithReplace()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Live");
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for Live']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-team").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Growth", "Rename Live to Growth?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("/teams/Growth", navigation.ToAbsoluteUri(navigation.History.First().Uri).AbsolutePath));
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    /// <summary>
    /// Rename project… opens "Rename project" with the Project's name and the service's live check for the Project; its
    /// confirmation is the Project's (No Agent restarts), and Rename calls the service once with Team, old and new.
    /// </summary>
    [Fact]
    public async Task Sidebar_RenameProject_AsksTheName_ConfirmsWithTheProjectPoints_ThenRenamesOnce()
    {
        RecordingTeamRenamer renamer = new() { ProjectCheck = static (_, _, name) => name == "Other" ? "A Project named \"Other\" already exists in Live." : null };
        await using MudBunitContext ctx = NewNavContext(renamer, ProjectCatalog());
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);
        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());

        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-project").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename project", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal("Marketing", cut.Find(".mud-dialog input").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find(".mud-dialog input").InputAsync("Other"));
        cut.WaitForAssertion(() => Assert.Equal("A Project named \"Other\" already exists in Live.", cut.Find(".mud-dialog .mud-input-helper-text").TextContent.Trim()));
        await SubmitNameAsync(cut, "Promo", "Rename project Marketing to Promo?");
        Assert.Equal(TeamRenameConfirmation.Message(TeamRenameKind.Project), cut.Find(".mud-dialog-content p").TextContent.Trim());
        Assert.Equal("No Agent restarts.", cut.FindAll(".mud-dialog-content li")[0].QuerySelector("strong, b")?.TextContent);
        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Live", "Marketing", "Promo")], renamer.ProjectsRenamed));
    }

    /// <summary>When the address is the old Project's page, a sidebar Project rename replaces it with the same tab of the renamed Project.</summary>
    [Fact]
    public async Task Sidebar_RenameProject_OnTheOldProjectsPage_NavigatesToTheSameTabOfTheNewOneWithReplace()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer, ProjectCatalog());
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Live/projects/Marketing/files");
        IRenderedComponent<ContainerFragment> cut = await StartProjectRenameAsync(ctx);

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("/teams/Live/projects/Promo/files", navigation.ToAbsoluteUri(navigation.History.First().Uri).AbsolutePath));
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    /// <summary>When the address is elsewhere, a sidebar Project rename leaves the page where it is.</summary>
    [Fact]
    public async Task Sidebar_RenameProject_OnAnotherPage_StaysPut()
    {
        RecordingTeamRenamer renamer = new();
        await using MudBunitContext ctx = NewNavContext(renamer, ProjectCatalog());
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/teams/Live/projects/Other/files");
        string before = navigation.Uri;
        int historyBefore = navigation.History.Count;
        IRenderedComponent<ContainerFragment> cut = await StartProjectRenameAsync(ctx);

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([("Live", "Marketing", "Promo")], renamer.ProjectsRenamed));
        Assert.Equal(before, navigation.Uri);
        Assert.Equal(historyBefore, navigation.History.Count);
    }

    /// <summary>Renders the Teams sidebar, opens Rename project… for Marketing and submits Promo, leaving the confirmation open.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> StartProjectRenameAsync(MudBunitContext ctx)
    {
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);
        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-project").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename project", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Promo", "Rename project Marketing to Promo?");
        return cut;
    }

    /// <summary>Cancel in the Project confirmation renames nothing, and a refused Project rename shows a Warning snackbar.</summary>
    [Fact]
    public async Task Sidebar_RenameProject_CancelRenamesNothing_RefusalShowsAWarning()
    {
        const string Reason = "The Project folder \"Marketing\" has files open in another program. Close them and try again.";
        RecordingTeamRenamer renamer = new() { Outcome = new TeamRenameOutcome(false, Reason) };
        await using MudBunitContext ctx = NewNavContext(renamer, ProjectCatalog());
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderNav(ctx, teamsView: true);
        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-project").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename project", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Promo", "Rename project Marketing to Promo?");

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Cancel").ClickAsync());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(renamer.ProjectsRenamed);

        await cut.InvokeAsync(() => cut.Find(".teams-nav-scope-row[data-scope='project'] button[aria-label='Actions for Marketing']").ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find(".mud-menu-item.teams-nav-rename-project").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename project", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await SubmitNameAsync(cut, "Promo", "Rename project Marketing to Promo?");
        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(Reason, Assert.Single(snackbar.ShownSnackbars).Message));
        Assert.Equal(Severity.Warning, snackbar.ShownSnackbars.Single().Severity);
    }

    /// <summary>Starts the Details Rename click without awaiting it (the handler awaits the dialogs) and waits for the name dialog.</summary>
    private static async Task ClickDetailsRenameAsync(IRenderedComponent<ContainerFragment> cut)
    {
        _ = cut.InvokeAsync(() => cut.Find("button.team-details-rename").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>Types <paramref name="name"/> into the open name dialog, presses Rename and waits for the confirmation titled <paramref name="confirmTitle"/>.</summary>
    private static async Task SubmitNameAsync(IRenderedComponent<ContainerFragment> cut, string name, string confirmTitle)
    {
        await cut.InvokeAsync(() => cut.Find(".mud-dialog input").InputAsync(name));
        _ = cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal(confirmTitle, cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>A catalog with Live holding the Projects Marketing and Other.</summary>
    private static FakeTeamCatalog ProjectCatalog() => new() { Teams = [new TeamSummary("Live", ["Marketing", "Other"], ["Nova"], true)] };

    /// <summary>The Details context of the archive tests, with the rename service registered.</summary>
    private static MudBunitContext NewDetailsContext(RecordingTeamRenamer renamer)
    {
        MudBunitContext ctx = ArchiveTeamUiTests.NewDetailsContext(new RecordingArchiveService());
        ctx.Services.AddSingleton<ITeamRenamer>(renamer);
        return ctx;
    }

    /// <summary>The sidebar context of the archive tests, with the rename service registered.</summary>
    private static MudBunitContext NewNavContext(RecordingTeamRenamer renamer, FakeTeamCatalog? catalog = null)
    {
        MudBunitContext ctx = ArchiveTeamUiTests.NewNavContext(new RecordingArchiveService(), catalog);
        ctx.Services.AddSingleton<ITeamRenamer>(renamer);
        return ctx;
    }
}
