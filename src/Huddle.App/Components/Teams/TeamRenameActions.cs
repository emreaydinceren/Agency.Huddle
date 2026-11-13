using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Teams;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Agency.Huddle.App.Components.Teams;

/// <summary>
/// The Rename of a Team or a Project (Team Rename Spec §5.1, §7), shared by the Details tab and the sidebar's row menus: the name
/// dialog, then the confirmation, then one call to the service, and a refusal shown as a Warning snackbar. Nothing changes
/// before the confirmation's Rename.
/// </summary>
internal static class TeamRenameActions
{
    /// <summary>The snackbar key every refusal shares, so a second refusal replaces the first rather than stacking.</summary>
    public const string SnackbarKey = "team-rename";

    private const string ConfirmText = "Rename";

    /// <summary>Asks for the new name, then for the confirmation, then renames the Team once.</summary>
    /// <param name="renamer">The rename service.</param>
    /// <param name="catalog">Gives the member count.</param>
    /// <param name="dialogs">Shows the dialogs.</param>
    /// <param name="snackbar">Shows a refusal.</param>
    /// <param name="team">The Team, in the catalog's spelling.</param>
    /// <param name="menu">The menu whose item asked, closed first; <see langword="null"/> for a button.</param>
    /// <returns>The new name, or <see langword="null"/> when nothing was renamed.</returns>
    public static async Task<string?> RenameTeamAsync(ITeamRenamer renamer, ITeamCatalog catalog, IDialogService dialogs, ISnackbar snackbar, string team, MudMenu? menu = null)
    {
        ArgumentNullException.ThrowIfNull(renamer);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(snackbar);

        string? name = await MenuDialogs.AfterClosingAsync(menu, () => AskNameAsync(dialogs, "Rename team", team, typed => renamer.CheckTeamName(team, typed)));
        if (name is null || string.Equals(name, team, StringComparison.Ordinal))
        {
            return null;
        }

        int members = catalog.Find(team)?.Members.Count ?? 0;
        IReadOnlyList<string> midTurn = await renamer.MidTurnMembersAsync(team, CancellationToken.None);
        bool confirmed = await dialogs.ConfirmAsync(
            TeamRenameConfirmation.Title(TeamRenameKind.Team, team, name),
            TeamRenameConfirmation.Message(TeamRenameKind.Team),
            TeamRenameConfirmation.Points(TeamRenameKind.Team, team, members, midTurn),
            ConfirmText,
            danger: true);
        return confirmed ? Report(await renamer.RenameTeamAsync(team, name, CancellationToken.None), snackbar, name) : null;
    }

    /// <summary>Asks for the new name, then for the confirmation, then renames the Project once.</summary>
    /// <param name="renamer">The rename service.</param>
    /// <param name="dialogs">Shows the dialogs.</param>
    /// <param name="snackbar">Shows a refusal.</param>
    /// <param name="team">The Project's Team.</param>
    /// <param name="project">The Project.</param>
    /// <param name="menu">The menu whose item asked, closed first.</param>
    /// <returns>The new name, or <see langword="null"/> when nothing was renamed.</returns>
    public static async Task<string?> RenameProjectAsync(ITeamRenamer renamer, IDialogService dialogs, ISnackbar snackbar, string team, string project, MudMenu? menu = null)
    {
        ArgumentNullException.ThrowIfNull(renamer);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(snackbar);

        string? name = await MenuDialogs.AfterClosingAsync(menu, () => AskNameAsync(dialogs, "Rename project", project, typed => renamer.CheckProjectName(team, project, typed)));
        if (name is null || string.Equals(name, project, StringComparison.Ordinal))
        {
            return null;
        }

        bool confirmed = await dialogs.ConfirmAsync(
            TeamRenameConfirmation.Title(TeamRenameKind.Project, project, name),
            TeamRenameConfirmation.Message(TeamRenameKind.Project),
            TeamRenameConfirmation.Points(TeamRenameKind.Project, project, 0, [], team),
            ConfirmText,
            danger: true);
        return confirmed ? Report(await renamer.RenameProjectAsync(team, project, name, CancellationToken.None), snackbar, name) : null;
    }

    /// <summary>
    /// When the address is the old Team's page (or, for a Project, the old Project's page), replaces it with the same page of the
    /// renamed one, keeping the tab and the query. Any other address is left alone.
    /// </summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <param name="oldTeam">The Team's name before the rename.</param>
    /// <param name="newTeam">The Team's name after it.</param>
    /// <param name="oldProject">The renamed Project's old name, or <see langword="null"/> for a Team rename.</param>
    /// <param name="newProject">The renamed Project's new name, or <see langword="null"/> for a Team rename.</param>
    public static void FollowRename(NavigationManager navigation, string oldTeam, string newTeam, string? oldProject = null, string? newProject = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        string relative = navigation.ToBaseRelativePath(navigation.Uri);
        int tail = relative.IndexOfAny(['?', '#']);
        string path = tail < 0 ? relative : relative[..tail];
        string suffix = tail < 0 ? string.Empty : relative[tail..];
        string[] segments = path.Split('/');
        if (segments.Length < 2 || !string.Equals(segments[0], "teams", StringComparison.OrdinalIgnoreCase) || !Names(segments[1], oldTeam))
        {
            return;
        }

        segments[1] = Uri.EscapeDataString(newTeam);
        if (oldProject is not null && newProject is not null)
        {
            if (segments.Length < 4 || !string.Equals(segments[2], "projects", StringComparison.OrdinalIgnoreCase) || !Names(segments[3], oldProject))
            {
                return;
            }

            segments[3] = Uri.EscapeDataString(newProject);
        }

        navigation.NavigateTo(string.Join('/', segments) + suffix, replace: true);
    }

    private static bool Names(string segment, string name) =>
        string.Equals(Uri.UnescapeDataString(segment), name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows the name dialog holding <paramref name="current"/>; the unchanged name is not a problem on opening, only a different one is checked.</summary>
    private static async Task<string?> AskNameAsync(IDialogService dialogs, string title, string current, Func<string, string?> check)
    {
        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, "Name" },
            { x => x.ConfirmText, ConfirmText },
            { x => x.InitialValue, current },
            { x => x.Validate, typed => string.Equals(typed, current, StringComparison.Ordinal) ? null : check(typed) },
            { x => x.SubmitAsync, static _ => Task.FromResult<string?>(null) },
        };
        IDialogReference reference = await dialogs.ShowAsync<NameDialog>(title, parameters, HuddleDialogOptions.SmallForm);
        DialogResult? result = await reference.Result;
        return result is { Canceled: false, Data: string name } ? name : null;
    }

    /// <summary>Shows a refusal as a Warning snackbar.</summary>
    private static string? Report(TeamRenameOutcome outcome, ISnackbar snackbar, string newName)
    {
        if (outcome.Renamed)
        {
            return newName;
        }

        if (outcome.Error is { } refusal)
        {
            _ = snackbar.Add(refusal, Severity.Warning, key: SnackbarKey);
        }

        return null;
    }
}
