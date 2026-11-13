using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// The Project half of <see cref="ArchiveService"/> (corrections-12 item 10): a Project moves between
/// <c>Teams/&lt;T&gt;/&lt;P&gt;</c> and <c>Archive/&lt;T&gt;/&lt;P&gt;</c> through the same staged, journalled
/// <see cref="ArchiveMove"/> as a Team, with no roster, labels or Rooms. The journal and staging folders sit in
/// <c>Archive/&lt;T&gt;</c> (the parent of the archived side), and <see cref="RecoverProjectMoves"/> finishes them at start-up.
/// </summary>
internal sealed partial class ArchiveService
{
    /// <inheritdoc />
    public async Task<ArchiveOutcome> ArchiveProjectAsync(string team, string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(project);

        using IDisposable entered = await this.gate.EnterAsync(ct).ConfigureAwait(false);
        return this.MoveProject(team, project, archiving: true);
    }

    /// <inheritdoc />
    public async Task<ArchiveOutcome> UnarchiveProjectAsync(string team, string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(project);

        using IDisposable entered = await this.gate.EnterAsync(ct).ConfigureAwait(false);
        return this.MoveProject(team, project, archiving: false);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedTeams()
    {
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        return [.. this.files.ListEntries(archiveRoot)
            .Where(e => !TaskLayout.IsReservedFolderName(e) && this.files.FileExists(Path.Combine(archiveRoot, e, TeamPaths.TeamFileName)))
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedProjects(string team)
    {
        ArgumentNullException.ThrowIfNull(team);

        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        string? folder = this.Spelling(archiveRoot, this.catalog.Find(team)?.Name ?? team) ?? this.Spelling(archiveRoot, team);
        if (folder is null)
        {
            return [];
        }

        // An archived Team's whole folder moved, so it still holds its own raw, memory and outputs folders: skip every name a
        // live Project may not take, not only the _ and . ones.
        string teamFolder = Path.Combine(archiveRoot, folder);
        return [.. this.files.ListEntries(teamFolder)
            .Where(e => !TeamNames.IsReservedProjectName(e) && !this.files.FileExists(Path.Combine(teamFolder, e)))
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Finishes or undoes the Project moves a crash interrupted: every <c>Archive/&lt;T&gt;</c> folder is recovered against <c>Teams/&lt;T&gt;</c>.</summary>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="archiveRoot">The Archive root.</param>
    /// <param name="archivedEntries">The names directly under the Archive root.</param>
    private void RecoverProjectMoves(string teamsRoot, string archiveRoot, IReadOnlyList<string> archivedEntries)
    {
        foreach (string team in archivedEntries)
        {
            string live = this.Spelling(teamsRoot, team) ?? team;
            _ = this.folders.RunExclusive(
                () =>
                {
                    string archivedFolder = Path.Combine(archiveRoot, team);
                    ArchiveMove.Recover(archivedFolder, Path.Combine(teamsRoot, live), this.files);
                    if (!this.files.FileExists(Path.Combine(archivedFolder, TeamPaths.TeamFileName)))
                    {
                        // A Project move that never placed anything leaves an empty Archive folder with no _team.md.
                        this.files.DeleteEmptyFolder(archivedFolder);
                    }

                    return true;
                });
        }
    }

    private ArchiveOutcome MoveProject(string requestTeam, string requestProject, bool archiving)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        string? liveTeam = this.Spelling(teamsRoot, this.catalog.Find(requestTeam)?.Name ?? requestTeam);
        if (liveTeam is null)
        {
            return Refuse(this.Spelling(archiveRoot, requestTeam) is { } held
                ? $"{held} is archived. Unarchive it first."
                : $"There is no Team named \"{requestTeam}\".");
        }

        string liveFolder = Path.Combine(teamsRoot, liveTeam);
        string archivedTeam = this.Spelling(archiveRoot, liveTeam) ?? liveTeam;
        string archivedFolder = Path.Combine(archiveRoot, archivedTeam);
        string? liveProject = this.Spelling(liveFolder, requestProject);
        string? archivedProject = this.Spelling(archivedFolder, requestProject);
        string? problem = archiving
            ? ArchiveProblem(liveTeam, requestProject, liveProject, archivedProject)
            : UnarchiveProblem(liveTeam, requestProject, liveProject, archivedProject);
        if (problem is not null)
        {
            return Refuse(problem);
        }

        string source = archiving ? Path.Combine(liveFolder, liveProject ?? requestProject) : Path.Combine(archivedFolder, archivedProject ?? requestProject);
        string destination = archiving ? Path.Combine(archivedFolder, liveProject ?? requestProject) : Path.Combine(liveFolder, archivedProject ?? requestProject);
        string name = Path.GetFileName(source);
        string? failure = this.RunProjectMove(liveTeam, source, destination, archiving);
        if (failure is not null)
        {
            return Refuse($"{name} {failure}");
        }

        if (!archiving)
        {
            // The last Project coming back leaves an empty Archive folder with no _team.md; it holds nothing worth keeping.
            this.files.DeleteEmptyFolder(archivedFolder);
        }

        return new ArchiveOutcome(true, null);
    }

    private static string? ArchiveProblem(string team, string request, string? liveProject, string? archivedProject)
    {
        if (liveProject is null || TaskLayout.IsReservedFolderName(liveProject))
        {
            return $"There is no Project named \"{request}\" in {team}.";
        }

        return archivedProject is null
            ? null
            : $"A Project named {archivedProject} is already archived in {team}. Rename one of them first.";
    }

    private static string? UnarchiveProblem(string team, string request, string? liveProject, string? archivedProject)
    {
        if (archivedProject is null || TaskLayout.IsReservedFolderName(archivedProject))
        {
            return $"There is no archived Project named \"{request}\" in {team}.";
        }

        return liveProject is null
            ? null
            : $"A Project named {liveProject} exists in {team} now. Rename one of them first.";
    }

    /// <summary>Moves the folder under the provisioner's gate with the Team's Task writes paused.</summary>
    /// <returns>The reason it did not move (to follow the Project's name), or <see langword="null"/> when it moved.</returns>
    private string? RunProjectMove(string team, string source, string destination, bool archiving)
    {
        string prefix = archiving ? ArchiveMove.ArchivingPrefix : ArchiveMove.UnarchivingPrefix;
        using IDisposable pause = this.PauseTasks(team);
        ArchiveMoveResult moved;
        try
        {
            moved = this.folders.RunExclusive(() => ArchiveMove.Move(source, destination, prefix, this.files));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The copy, its verification or a rename failed; the source folder is whole.
            return $"could not be moved: {ex.Message}";
        }

        return moved == ArchiveMoveResult.Moved ? null : "has files open in another program. Close them and try again.";
    }
}
