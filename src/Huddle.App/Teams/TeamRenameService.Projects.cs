using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The Project rename (Team Rename Spec R12, §5.5 Project rule, corrections-batch3 tr11): one journal row of kind Project, a pause of the Team's Task
/// writes, the move of <c>Teams/&lt;Team&gt;/&lt;Project&gt;</c> only, the Room rows, the step-6 rewrites and the Team Room line. The Team is neither
/// restarted nor suspended in the reconciler, and no Persona label changes.
/// </summary>
internal sealed partial class TeamRenameService
{
    /// <summary>Renames a Project of a Team.</summary>
    /// <param name="team">The Team's name, compared ignoring case.</param>
    /// <param name="oldProject">The Project's current name, compared ignoring case.</param>
    /// <param name="newProject">The new name; only its case may differ from the old one.</param>
    /// <param name="ct">Cancels the wait for the gate and the awaited work.</param>
    /// <returns>Renamed, or the plain sentence saying why not.</returns>
    public async Task<TeamRenameOutcome> RenameProjectAsync(string team, string oldProject, string newProject, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(oldProject);
        ArgumentNullException.ThrowIfNull(newProject);

        using IDisposable entered = await gate.EnterAsync(ct).ConfigureAwait(false);
        TeamSummary? summary = catalog.Find(team);
        if (summary is null)
        {
            return Refuse($"There is no Team named \"{team}\".");
        }

        string? current = summary.Projects.FirstOrDefault(p => string.Equals(p, oldProject, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return Refuse($"{summary.Name} has no Project named \"{oldProject}\".");
        }

        string? problem = this.ValidateProject(summary, current, newProject);
        return problem is not null
            ? Refuse(problem)
            : await this.RenameProjectLockedAsync(summary.Name, current, newProject, ct).ConfigureAwait(false);
    }

    /// <summary>The name rules for a Project (§5.5): the shared Project rules, the journal and a rename that is still unfinished.</summary>
    private string? ValidateProject(TeamSummary summary, string oldProject, string newProject)
    {
        if (string.Equals(oldProject, newProject, StringComparison.Ordinal))
        {
            return $"The Project is already named \"{oldProject}\".";
        }

        if (renames.IsProjectPending(summary.Name, oldProject) || renames.IsProjectPending(summary.Name, newProject))
        {
            return "A rename of this Project is still unfinished. Huddle will finish it at the next start.";
        }

        if (this.PendingRefusal() is { } pending)
        {
            return pending;
        }

        bool caseOnly = CaseOnly(oldProject, newProject);
        TeamSummary others = summary with { Projects = [.. summary.Projects.Where(p => !caseOnly || !string.Equals(p, oldProject, StringComparison.OrdinalIgnoreCase))] };
        string? problem = TeamNames.ValidateProjectName(newProject, others, this.ArchivedProjectNames(summary.Name));
        if (problem is not null || caseOnly)
        {
            return problem;
        }

        return renames.IsProjectReserved(summary.Name, newProject) && !string.Equals(renames.ResolveProject(summary.Name, newProject), oldProject, StringComparison.OrdinalIgnoreCase)
            ? $"The name \"{newProject}\" was used by an earlier Project rename and is kept for its old links. Pick another name."
            : null;
    }

    /// <summary>The Project folder names archived under <c>Archive/&lt;team&gt;/</c>, in their on-disk spelling.</summary>
    private List<string> ArchivedProjectNames(string team)
    {
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        string? folder = this.Spelling(archiveRoot, team);
        if (folder is null)
        {
            return [];
        }

        string teamFolder = Path.Combine(archiveRoot, folder);
        return [.. files.ListEntries(teamFolder).Where(e => !TeamNames.IsReservedProjectName(e) && !files.FileExists(Path.Combine(teamFolder, e)))];
    }

    private async Task<TeamRenameOutcome> RenameProjectLockedAsync(string team, string oldProject, string newProject, CancellationToken ct)
    {
        long id = renames.Begin(TeamRenameKind.Project, team, oldProject, newProject);
        TeamRename row = renames.Pending().First(r => r.Id == id);
        List<IDisposable> pauses = [];
        try
        {
            this.PauseWrites(row, pauses);
        }
        catch (Exception)
        {
            // Nothing has moved yet, so the row is dropped and whatever was paused is lifted again.
            LiftPauses(pauses);
            renames.Discard(id);
            throw;
        }

        TeamRenameOutcome outcome;
        try
        {
            outcome = await this.FinishProjectAsync(row, ct).ConfigureAwait(false);
        }
        finally
        {
            LiftPauses(pauses);
        }

        if (outcome.Renamed)
        {
            await this.PostRenamedLineAsync(row, ct).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>Moves the Project folder, then rolls forward; the pauses are lifted by the caller.</summary>
    private async Task<TeamRenameOutcome> FinishProjectAsync(TeamRename row, CancellationToken ct)
    {
        string? moveFailure = this.MoveProjectFolder(row);
        if (moveFailure is not null)
        {
            renames.Discard(row.Id);
            return Refuse(moveFailure);
        }

        string? error = await this.RollForwardProjectAsync(row, ct).ConfigureAwait(false);
        if (error is null)
        {
            renames.Complete(row.Id);
        }
        else
        {
            this.NotifyUnfinished(row, error);
        }

        return new TeamRenameOutcome(error is null, error);
    }

    /// <summary>The Room rows of the Project, then the rewrites; there is no label to rewrite.</summary>
    private async Task<string?> RollForwardProjectAsync(TeamRename row, CancellationToken ct)
    {
        string? error = await this.RetryAsync(
            "the Room rows",
            async () => _ = await directory.RenameProjectScopeAsync(row.Team, row.OldName, row.NewName, ct).ConfigureAwait(false)).ConfigureAwait(false);
        return error ?? await this.RunRewritesAsync(row, ct).ConfigureAwait(false);
    }

    private static void LiftPauses(List<IDisposable> pauses)
    {
        foreach (IDisposable pause in pauses)
        {
            pause.Dispose();
        }
    }

    /// <summary>Step 4 for a Project: moves <c>Teams/&lt;Team&gt;/&lt;Old&gt;</c> only.</summary>
    /// <returns>The refusal sentence, or <see langword="null"/> when the folder moved.</returns>
    private string? MoveProjectFolder(TeamRename row)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string? teamFolder = this.Spelling(teamsRoot, row.Team);
        string? onDisk = teamFolder is null ? null : this.Spelling(Path.Combine(teamsRoot, teamFolder), row.OldName);
        if (teamFolder is null || onDisk is null)
        {
            return $"{row.OldName} has no folder to rename.";
        }

        string parent = Path.Combine(teamsRoot, teamFolder);
        return folders.RunExclusive(() => this.MoveOne(parent, onDisk, row.NewName, CaseOnly(row.OldName, row.NewName), "The Project folder"));
    }

    /// <summary>Reads the Team's folder and finishes the Project move that a crash cut short.</summary>
    /// <param name="row">The Pending Project row.</param>
    /// <param name="neverMoved">Set when the old folder is still there with no temporary folder and no new one.</param>
    /// <returns>The problem sentence, or <see langword="null"/> when the folder is as the row says.</returns>
    private string? FinishProjectMoveFromDisk(TeamRename row, out bool neverMoved)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string? teamFolder = this.Spelling(teamsRoot, row.Team);
        neverMoved = false;
        if (teamFolder is null)
        {
            return $"{row.Team} has no folder to finish the rename of {row.OldName} in.";
        }

        string parent = Path.Combine(teamsRoot, teamFolder);
        (string? tempOnDisk, bool newThere, bool oldThere) = this.ReadDisk(parent, row.OldName, row.NewName);
        neverMoved = oldThere && tempOnDisk is null && !newThere;
        if (neverMoved || (tempOnDisk is null && newThere))
        {
            return null;
        }

        return tempOnDisk is null
            ? $"Neither {row.OldName} nor {row.NewName} has a folder to finish."
            : folders.RunExclusive(() => this.MoveOne(parent, tempOnDisk, row.NewName, false, "The Project folder"));
    }

    /// <summary>Step 8: tells the Team Room, after a rename or a recovery, what was renamed.</summary>
    /// <param name="row">The finished row.</param>
    /// <param name="ct">Cancels the post.</param>
    private Task PostRenamedLineAsync(TeamRename row, CancellationToken ct) => row.Kind == TeamRenameKind.Project
        ? this.PostLineAsync(row.Team, "room.projectRenamed", [("oldProject", row.OldName), ("newProject", row.NewName)], ct)
        : this.PostRoomLineAsync(row.OldName, row.NewName, ct);
}
