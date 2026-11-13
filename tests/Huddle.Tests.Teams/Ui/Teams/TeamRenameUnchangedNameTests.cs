using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the name dialog's handling of the unchanged name (Team Rename Spec §5.1): it opens holding the current name, so that name is
/// not refused as "already named" on opening, and pressing Rename with it closes the dialog without a confirmation or a service call.
/// A change of case only is still checked and confirmed.
/// </summary>
public sealed class TeamRenameUnchangedNameTests
{
    private const string AlreadyNamed = "The Team is already named \"Home\".";

    /// <summary>A service check that, like the real one, refuses the Team's own name exactly.</summary>
    private static readonly Func<string, string, string?> RefusesOwnName = static (oldName, newName) => string.Equals(oldName, newName, StringComparison.Ordinal) ? AlreadyNamed : null;

    /// <summary>Opening the name dialog shows no refusal, leaves Rename enabled and never asks the service's check about the current name.</summary>
    [Fact]
    public async Task NameDialog_OpensWithCurrentName_ShowsNoRefusal()
    {
        RecordingTeamRenamer renamer = new() { TeamCheck = RefusesOwnName };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);

        await OpenNameDialogAsync(cut);

        Assert.Empty(cut.FindAll(".mud-dialog .mud-input-helper-text"));
        Assert.False(ArchiveTeamUiTests.DialogButton(cut, "Rename").HasAttribute("disabled"));
        Assert.DoesNotContain(("Home", "Home"), renamer.TeamChecks);
    }

    /// <summary>Pressing Rename with the name unchanged closes the dialog, opens no confirmation and renames nothing.</summary>
    [Fact]
    public async Task NameDialog_RenameWithUnchangedName_ClosesWithNoConfirmationAndNoServiceCall()
    {
        RecordingTeamRenamer renamer = new() { TeamCheck = RefusesOwnName };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await OpenNameDialogAsync(cut);

        await cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>A name that differs only in case is checked by the service and confirmed like any other.</summary>
    [Fact]
    public async Task NameDialog_CaseOnlyChange_IsCheckedAndConfirmed()
    {
        RecordingTeamRenamer renamer = new() { TeamCheck = RefusesOwnName };
        await using MudBunitContext ctx = NewDetailsContext(renamer);
        IRenderedComponent<ContainerFragment> cut = ArchiveTeamUiTests.RenderDetails(ctx);
        await OpenNameDialogAsync(cut);

        await cut.InvokeAsync(() => cut.Find(".mud-dialog input").InputAsync("home"));
        _ = cut.InvokeAsync(() => ArchiveTeamUiTests.DialogButton(cut, "Rename").ClickAsync());

        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename Home to home?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Contains(("Home", "home"), renamer.TeamChecks);
        Assert.Empty(renamer.TeamsRenamed);
    }

    /// <summary>Starts the Details Rename click without awaiting it (the handler awaits the dialogs) and waits for the name dialog.</summary>
    private static async Task OpenNameDialogAsync(IRenderedComponent<ContainerFragment> cut)
    {
        _ = cut.InvokeAsync(() => cut.Find("button.team-details-rename").ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.Equal("Rename team", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>The Details context of the archive tests, with the rename service registered.</summary>
    private static MudBunitContext NewDetailsContext(RecordingTeamRenamer renamer)
    {
        MudBunitContext ctx = ArchiveTeamUiTests.NewDetailsContext(new RecordingArchiveService());
        ctx.Services.AddSingleton<ITeamRenamer>(renamer);
        return ctx;
    }
}
