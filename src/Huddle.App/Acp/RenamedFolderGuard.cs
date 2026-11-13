using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's Edit or Write under <c>Teams/&lt;old name&gt;/</c> when that name belongs to a Team since renamed (Team Rename R10),
/// so a stale path cannot recreate the old folder.
/// </summary>
/// <remarks>
/// The permission reply carries no reason text, so the refusal is silent apart from the log; the turn notice carries the hint.
/// MultiEdit, NotebookEdit and Bash are not covered. A name that differs from today's only in case is not an old name.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's Work Dir, where a relative path resolves.</param>
/// <param name="renames">The rename journal.</param>
internal sealed class RenamedFolderGuard(string teamsRoot, string workDir, ITeamRenames renames) : IToolCallGuard
{
    /// <inheritdoc />
    string? IToolCallGuard.RefusalFor(ToolCallInfo toolCall, string personaName)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        string root = Path.GetFullPath(teamsRoot.Replace('\\', '/'));
        foreach (string resolved in ToolCallPaths.From(toolCall, workDir))
        {
            string path = Path.GetFullPath(resolved.Replace('\\', '/'));
            if (PathContainment.Below(path, root, StringComparison.OrdinalIgnoreCase) is not { Length: > 0 } below)
            {
                continue;
            }

            string team = below.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)[0];
            string today = renames.Resolve(team);
            if (!string.Equals(today, team, StringComparison.Ordinal))
            {
                return $"Team '{team}' was renamed to '{today}'. Use '{today}'.";
            }
        }

        return null;
    }
}
