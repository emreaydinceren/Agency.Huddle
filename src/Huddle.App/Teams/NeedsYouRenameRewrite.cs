using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Step-6 rewrite that keeps a Team's or Project's Needs You level and spend cap through a rename (ADR-0088). Both are stored under the
/// scope's id, so only the name the id is looked up by changes.
/// </summary>
/// <param name="ids">Holds the Team and Project ids.</param>
internal sealed class NeedsYouRenameRewrite(ScopeIdStore ids) : ITeamRenameRewrite
{
    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

    /// <inheritdoc />
    public Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        if (rename.Kind == TeamRenameKind.Project)
        {
            ids.RenameProject(rename.Team, rename.OldName, rename.NewName);
        }
        else
        {
            ids.RenameTeam(rename.OldName, rename.NewName);
        }

        return Task.CompletedTask;
    }
}
