using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.App.Teams;

/// <summary>Step-6 rewrite of the Views (Spec §4): the Team and Project entries of every saved View's filter follow the new Team name.</summary>
/// <param name="views">The View store.</param>
internal sealed class ViewsRenameRewrite(ViewStore views) : ITeamRenameRewrite
{
    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

    /// <inheritdoc />
    public Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        views.RenameTeam(rename.OldName, rename.NewName);
        return Task.CompletedTask;
    }
}
