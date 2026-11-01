using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Components.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins U20 T20.4 and T20.5 (UX-020, UX-021, UX-024, UX-025, UX-055, UX-R10, UX-R11): the Team
/// dialogs follow the dialog anatomy (<c>HuddleDialogOptions.SmallForm</c>, Cancel before the Filled
/// primary, the single-field name dialog is <c>NameDialog</c>).
/// </summary>
public sealed class TeamsDialogConsistencyTests
{
    /// <summary>The Team dialogs open with the shared small-form options, so the backdrop does not dismiss them; the retired Add member and custom role dialogs are gone.</summary>
    [Fact]
    public void TeamDialogs_UseSmallFormOptions()
    {
        Assert.Same(HuddleDialogOptions.SmallForm, DuplicateTeamDialog.Options);
        Assert.False(File.Exists(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "AddMemberDialog.razor")));
        Assert.False(File.Exists(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "CustomRoleDialog.razor")));
    }

    /// <summary>In each dialog the Cancel button comes first and the primary action is the Filled one after it.</summary>
    [Theory]
    [InlineData("DuplicateTeamDialog")]
    [InlineData("RoleFormDialog")]
    public void TeamDialogs_ActionsAreCancelThenFilledPrimary(string dialog)
    {
        string actions = DialogActions(dialog);

        int cancel = actions.IndexOf(">Cancel<", StringComparison.Ordinal);
        int primary = actions.IndexOf("Variant.Filled", StringComparison.Ordinal);

        Assert.True(cancel >= 0, "no Cancel button");
        Assert.True(primary > cancel, "the Filled primary must follow Cancel");
        Assert.Contains("Variant.Text", actions, StringComparison.Ordinal); // contains-ok: haystack is the dialog's razor source
    }

    /// <summary>The single-field New project name dialog is the shared <c>NameDialog</c>, opened by the nav through <c>NewProjectPrompt</c>; the bespoke dialog is gone.</summary>
    [Fact]
    public void NewProject_UsesNameDialog()
    {
        string nav = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "TeamsNav.razor"));
        string prompt = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "Dialogs", "NewProjectPrompt.cs"));

        Assert.Contains("NewProjectPrompt.AskAsync", nav, StringComparison.Ordinal); // contains-ok: haystack is TeamsNav's razor source
        Assert.Contains("ShowAsync<NameDialog>", prompt, StringComparison.Ordinal); // contains-ok: haystack is the prompt's C# source
        Assert.Contains("HuddleDialogOptions.SmallForm", prompt, StringComparison.Ordinal); // contains-ok: haystack is the prompt's C# source
        Assert.False(File.Exists(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", "NewTeamDialog.razor")));
    }

    private static string DialogActions(string dialog)
    {
        string razor = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Teams", dialog + ".razor"));
        int start = razor.IndexOf("<DialogActions>", StringComparison.Ordinal);
        int end = razor.IndexOf("</DialogActions>", StringComparison.Ordinal);
        return razor[start..end];
    }
}
