using Bunit;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Components.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins U20 T20.4 and T20.5 (UX-020, UX-021, UX-024, UX-025, UX-055, UX-R10, UX-R11): the Team
/// dialogs follow the dialog anatomy (<c>HuddleDialogOptions.SmallForm</c>, Cancel before the Filled
/// primary, the single-field name dialog is <c>NameDialog</c>) and <c>TeamTabToolbar</c> renders the
/// shared <c>SearchField</c>.
/// </summary>
public sealed class TeamsDialogConsistencyTests
{
    /// <summary>The three remaining Team dialogs open with the shared small-form options, so the backdrop does not dismiss them.</summary>
    [Fact]
    public void TeamDialogs_UseSmallFormOptions()
    {
        Assert.Same(HuddleDialogOptions.SmallForm, DuplicateTeamDialog.Options);
        Assert.Same(HuddleDialogOptions.SmallForm, CustomRoleDialog.Options);
        Assert.Same(HuddleDialogOptions.SmallForm, AddMemberDialog.Options);
    }

    /// <summary>In each dialog the Cancel button comes first and the primary action is the Filled one after it.</summary>
    [Theory]
    [InlineData("DuplicateTeamDialog")]
    [InlineData("CustomRoleDialog")]
    [InlineData("AddMemberDialog")]
    public void TeamDialogs_ActionsAreCancelThenFilledPrimary(string dialog)
    {
        string actions = DialogActions(dialog);

        int cancel = actions.IndexOf(">Cancel<", StringComparison.Ordinal);
        int primary = actions.IndexOf("Variant.Filled", StringComparison.Ordinal);

        Assert.True(cancel >= 0, "no Cancel button");
        Assert.True(primary > cancel, "the Filled primary must follow Cancel");
        Assert.Contains("Variant.Text", actions, StringComparison.Ordinal); // contains-ok: haystack is the dialog's razor source
    }

    /// <summary>The single-field New team and New project name dialog is the shared <c>NameDialog</c>; the bespoke dialog is gone.</summary>
    [Fact]
    public void NewTeam_UsesNameDialog()
    {
        string nav = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "TeamsNav.razor"));

        Assert.Contains("ShowAsync<NameDialog>", nav, StringComparison.Ordinal); // contains-ok: haystack is TeamsNav's razor source
        Assert.Contains("HuddleDialogOptions.SmallForm", nav, StringComparison.Ordinal); // contains-ok: haystack is TeamsNav's razor source
        Assert.False(File.Exists(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "NewTeamDialog.razor")));
    }

    /// <summary>The toolbar's search is the shared <c>SearchField</c> inside the <c>team-tab-toolbar-search</c> slot.</summary>
    [Fact]
    public async Task Toolbar_RendersSharedSearchField()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<TeamTabToolbar> cut = ctx.Render<TeamTabToolbar>(parameters => parameters
            .Add(t => t.ActionText, "Add member")
            .Add(t => t.SearchPlaceholder, "Search members"));

        IRenderedComponent<SearchField> field = cut.FindComponent<SearchField>();
        Assert.Equal("Search members", field.Instance.Placeholder);
        Assert.Equal("Search members", field.Instance.AriaLabel);
        Assert.Single(cut.FindAll(".team-tab-toolbar-search input"));
    }

    private static string DialogActions(string dialog)
    {
        string razor = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", dialog + ".razor"));
        int start = razor.IndexOf("<DialogActions>", StringComparison.Ordinal);
        int end = razor.IndexOf("</DialogActions>", StringComparison.Ordinal);
        return razor[start..end];
    }
}
