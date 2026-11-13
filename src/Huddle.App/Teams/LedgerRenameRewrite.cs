using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Step-6 rewrite of the ledgers (Spec §4): the AGENTS.md ledger, every Agent's file state and the raw baselines follow the Team folder's
/// new name. A Subscribed entry under the folder can be written <c>Teams/Old</c>, <c>./Teams/Old</c> or as an absolute path with <c>/</c>
/// separators, so each form is swapped; entries with <c>\</c> separators are left to the read-time journal redirect.
/// </summary>
/// <param name="ledger">The AGENTS.md ledger.</param>
/// <param name="files">The Agents' file state.</param>
/// <param name="baselines">The raw baselines.</param>
/// <param name="options">Supplies the data folder and the Teams folder name.</param>
internal sealed class LedgerRenameRewrite(AgentsMdLedger ledger, FileStateStore files, RawBaselineStore baselines, IOptions<TeamOptions> options) : ITeamRenameRewrite
{
    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

    /// <inheritdoc />
    public Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        TeamOptions team = options.Value;
        bool project = rename.Kind == TeamRenameKind.Project;
        string teamsDir = team.Teams.Dir.Replace('\\', '/').Trim('/');
        string oldInTeams = project ? $"{rename.Team}/{rename.OldName}" : rename.OldName;
        string newInTeams = project ? $"{rename.Team}/{rename.NewName}" : rename.NewName;
        string oldRelative = $"{teamsDir}/{oldInTeams}";
        string newRelative = $"{teamsDir}/{newInTeams}";
        string oldAbsolute = Path.GetFullPath(Path.Combine(team.DataDir, team.Teams.Dir, oldInTeams)).Replace('\\', '/');
        string newAbsolute = Path.GetFullPath(Path.Combine(team.DataDir, team.Teams.Dir, newInTeams)).Replace('\\', '/');

        ledger.MovePrefix(oldInTeams, newInTeams);
        (string Old, string New)[] forms = [(oldRelative, newRelative), ("./" + oldRelative, "./" + newRelative), (oldAbsolute, newAbsolute)];
        foreach ((string oldPrefix, string newPrefix) in forms)
        {
            if (project)
            {
                files.RenamePrefix(oldPrefix, newPrefix);
            }
            else
            {
                files.RenameTeam(rename.OldName, rename.NewName, oldPrefix, newPrefix);
            }
        }

        baselines.RenamePrefix(oldRelative, newRelative);
        return Task.CompletedTask;
    }
}
