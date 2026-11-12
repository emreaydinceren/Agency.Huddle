using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.SecondBrain;
using MudBlazor;

namespace Agency.Huddle.App.Components.Teams;

/// <summary>
/// The Archive and Restore of a Team or a Project (Spec 6.9, D-46), shared by the Team's Details tab, the Project page,
/// the sidebar's row menus and the Archived page, so T16's question and the refusal's warning are worded in one place.
/// A Team asks first, because its Agents can be left with no Team; a Project only moves, so it does not.
/// </summary>
internal static class TeamArchiveActions
{
    /// <summary>The snackbar key every refusal shares, so a second refusal replaces the first rather than stacking.</summary>
    public const string SnackbarKey = "team-archive";

    /// <summary>Asks T16's question with the counts and the Agents left without a Team, then archives the Team once on Archive.</summary>
    /// <param name="archive">The archive service.</param>
    /// <param name="dialogs">Shows the confirm.</param>
    /// <param name="snackbar">Shows a refusal.</param>
    /// <param name="team">The Team, in the catalog's spelling.</param>
    /// <param name="menu">The menu whose item asked, closed before the confirm opens; <see langword="null"/> for a button.</param>
    /// <returns>Whether the Team was archived.</returns>
    public static async Task<bool> ArchiveTeamAsync(IArchiveService archive, IDialogService dialogs, ISnackbar snackbar, string team, MudMenu? menu = null)
    {
        ArchiveConfirmation counts = archive.Confirm(team);
        string title = $"Archive {team}?";
        string message = counts.Text(team)[(title.Length + 1)..];
        if (!await MenuDialogs.AfterClosingAsync(menu, () => dialogs.ConfirmAsync(title, message, "Archive")))
        {
            return false;
        }

        return Report(await archive.ArchiveTeamAsync(team, CancellationToken.None), snackbar);
    }

    /// <summary>Archives the Project with no confirmation: its files and Tasks only move, and Restore brings them back.</summary>
    /// <param name="archive">The archive service.</param>
    /// <param name="snackbar">Shows a refusal.</param>
    /// <param name="team">The live Team.</param>
    /// <param name="project">The Project.</param>
    /// <returns>Whether the Project was archived.</returns>
    public static async Task<bool> ArchiveProjectAsync(IArchiveService archive, ISnackbar snackbar, string team, string project)
    {
        return Report(await archive.ArchiveProjectAsync(team, project, CancellationToken.None), snackbar);
    }

    /// <summary>Shows a refusal as a warning snackbar.</summary>
    /// <param name="outcome">What the archive service did.</param>
    /// <param name="snackbar">Shows the refusal.</param>
    /// <returns>Whether the move was done.</returns>
    public static bool Report(ArchiveOutcome outcome, ISnackbar snackbar)
    {
        if (!outcome.Done && outcome.Refusal is { } refusal)
        {
            _ = snackbar.Add(refusal, Severity.Warning, key: SnackbarKey);
        }

        return outcome.Done;
    }
}
