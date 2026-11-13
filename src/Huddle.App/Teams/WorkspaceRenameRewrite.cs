using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>Step-6 rewrite of the workspace (Spec §4): an absolute <c>workspace:</c> inside the moved Team folder follows it; one outside is left alone.</summary>
/// <param name="folders">Reads and writes <c>_team.md</c>.</param>
/// <param name="options">Supplies the data folder and the Teams folder name.</param>
internal sealed class WorkspaceRenameRewrite(ITeamFolders folders, IOptions<TeamOptions> options) : ITeamRenameRewrite
{
    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

    /// <inheritdoc />
    public Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        TeamOptions team = options.Value;
        bool project = rename.Kind == TeamRenameKind.Project;
        string oldFolder = Path.GetFullPath(Path.Combine(team.DataDir, team.Teams.Dir, project ? Path.Combine(rename.Team, rename.OldName) : rename.OldName));
        string newFolder = Path.GetFullPath(Path.Combine(team.DataDir, team.Teams.Dir, project ? Path.Combine(rename.Team, rename.NewName) : rename.NewName));
        _ = folders.UpdateTeamFile(project ? rename.Team : rename.NewName, file => file with { Workspace = Moved(file.Workspace, oldFolder, newFolder) });
        return Task.CompletedTask;
    }

    /// <summary>The workspace under the new folder when it lies inside the old one; otherwise as it was.</summary>
    private static string? Moved(string? workspace, string oldFolder, string newFolder)
    {
        if (string.IsNullOrWhiteSpace(workspace) || !Path.IsPathFullyQualified(workspace))
        {
            return workspace;
        }

        string full = Path.GetFullPath(workspace);
        if (!PathContainment.IsInside(full, oldFolder, includeRoot: true, PathContainment.Comparison))
        {
            return workspace;
        }

        string relative = Path.GetRelativePath(oldFolder, full);
        return relative == "." ? newFolder : Path.Combine(newFolder, relative);
    }
}
