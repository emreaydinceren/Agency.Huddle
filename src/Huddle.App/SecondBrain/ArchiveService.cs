using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// Archives a Team (Spec 6.7, 8.4, as settled by corrections-12): it writes the roster and the Team's Room ids into
/// <c>_team.md</c>, moves <c>Teams/&lt;T&gt;</c> to <c>Archive/&lt;T&gt;</c> (merging Projects already archived there),
/// archives the Team's Rooms, and only then removes the member labels. One operation runs at a time.
/// </summary>
/// <remarks>
/// Task 109 does the same-volume path. The staging folder, verification, cross-volume copy and start-up recovery
/// (items 7, 8, 4) are Task 113's and plug into <see cref="IArchiveFileSystem"/>; pausing Task writes during the
/// move (item 5) is Tasks 115 and 117's.
/// </remarks>
internal sealed class ArchiveService : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly LibraryRootStore roots;
    private readonly ITeamCatalog catalog;
    private readonly ITeamMembership membership;
    private readonly ITeamFolders folders;
    private readonly TaskStore tasks;
    private readonly PersonaStore personas;
    private readonly ITeamDirectory directory;
    private readonly IArchiveFileSystem files;
    private readonly ILogger<ArchiveService> logger;

    /// <summary>Creates the service over the stores it reads and the folders it moves.</summary>
    /// <param name="roots">Locates the Teams and Archive roots.</param>
    /// <param name="catalog">The Teams, their members and manager.</param>
    /// <param name="membership">Removes the labels once the folder has moved.</param>
    /// <param name="folders">Writes <c>_team.md</c> and holds the folder gate during the move.</param>
    /// <param name="tasks">Counts the Team's open Tasks for the confirmation.</param>
    /// <param name="personas">Reads each member's Team labels for the confirmation.</param>
    /// <param name="directory">Archives the Team's Rooms.</param>
    /// <param name="files">The disk operations.</param>
    /// <param name="feed">Reserved for the Team-archived Feed item, which has no source type yet.</param>
    /// <param name="logger">Logs a label that could not be removed.</param>
    public ArchiveService(
        LibraryRootStore roots,
        ITeamCatalog catalog,
        ITeamMembership membership,
        ITeamFolders folders,
        TaskStore tasks,
        PersonaStore personas,
        ITeamDirectory directory,
        IArchiveFileSystem files,
        IFeedPublisher feed,
        ILogger<ArchiveService> logger)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(logger);

        this.roots = roots;
        this.catalog = catalog;
        this.membership = membership;
        this.folders = folders;
        this.tasks = tasks;
        this.personas = personas;
        this.directory = directory;
        this.files = files;
        this.logger = logger;
    }

    /// <summary>What archiving the Team takes off the Boards; reads the stores and writes nothing.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    public ArchiveConfirmation Confirm(string team)
    {
        ArgumentNullException.ThrowIfNull(team);

        List<TaskItem> own = [.. this.tasks.All.Where(t => string.Equals(t.Location.Team, team, StringComparison.OrdinalIgnoreCase))];
        int open = own.Count(static t => t.Status is TaskState.Backlog or TaskState.ToDo or TaskState.InProgress or TaskState.Review or TaskState.NeedsYou);
        int waiting = own.Count(static t => t.Status == TaskState.NeedsYou);
        return new ArchiveConfirmation(open, waiting, this.MembersWithoutAnotherTeam(team));
    }

    /// <summary>Archives the Team.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done, or the reason nothing was changed.</returns>
    public async Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);

        await this.gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await this.ArchiveLockedAsync(team, ct).ConfigureAwait(false);
        }
        finally
        {
            _ = this.gate.Release();
        }
    }

    /// <summary>Unarchives the Team: the folder moves back, the recorded members are added again, the recorded Rooms are restored.</summary>
    /// <param name="team">The Team, compared ignoring case.</param>
    /// <param name="ct">Cancels the wait for the gate.</param>
    /// <returns>Done (with the members skipped because their Persona is gone), or the reason nothing was changed.</returns>
    public async Task<ArchiveOutcome> UnarchiveTeamAsync(string team, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);

        await this.gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await this.UnarchiveLockedAsync(team, ct).ConfigureAwait(false);
        }
        finally
        {
            _ = this.gate.Release();
        }
    }

    /// <summary>
    /// Finishes or undoes an archive or unarchive a crash interrupted: deletes the Huddle dot folders, removes labels an
    /// archived Team still has, and restores a live Team whose roster was written but whose move never finished.
    /// </summary>
    /// <param name="ct">Cancels the wait for the gate.</param>
    public async Task RecoverAsync(CancellationToken ct)
    {
        await this.gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await this.RecoverLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _ = this.gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.gate.Dispose();

    private async Task<ArchiveOutcome> UnarchiveLockedAsync(string request, CancellationToken ct)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        string? archived = this.Spelling(archiveRoot, request);
        if (archived is null || !this.files.FileExists(Path.Combine(archiveRoot, archived, TeamPaths.TeamFileName)))
        {
            return Refuse($"There is no archived Team named \"{request}\".");
        }

        if (this.Spelling(teamsRoot, archived) is not null || this.catalog.Find(request) is not null || this.catalog.Find(archived) is not null)
        {
            return Refuse($"A Team named {request} exists now. Rename one of them first.");
        }

        string live = Path.Combine(teamsRoot, archived);
        ArchiveMoveResult moved;
        try
        {
            moved = this.folders.RunExclusive(
                () => ArchiveMove.Move(Path.Combine(archiveRoot, archived), live, ArchiveMove.UnarchivingPrefix, this.files));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The copy, its verification or a rename failed; the archive folder is whole.
            return Refuse($"{archived} could not be moved: {ex.Message}");
        }

        if (moved != ArchiveMoveResult.Moved)
        {
            return Refuse($"{archived} has files open in another program. Close them and try again.");
        }

        TeamFile? file = this.folders.ReadTeamFile(archived);
        IReadOnlyList<string> skipped = this.AddMembersBack(archived, file?.ArchivedMembers ?? []);
        foreach (string id in file?.ArchivedRooms ?? [])
        {
            await this.directory.SetRoomArchivedAsync(id, false, ct).ConfigureAwait(false);
        }

        string? manager = file?.ArchivedManager;
        if (manager is not null && this.personas.Get(manager) is null)
        {
            manager = null;
            skipped = [.. skipped.Append(file?.ArchivedManager ?? string.Empty).Distinct(StringComparer.Ordinal)];
        }

        _ = this.folders.UpdateTeamFile(
            archived,
            f => f with { ArchivedMembers = [], ArchivedRooms = [], ArchivedManager = null, Manager = manager ?? f.Manager });
        return new ArchiveOutcome(true, null) { Skipped = skipped };
    }

    private List<string> AddMembersBack(string team, IReadOnlyList<string> recorded)
    {
        List<string> skipped = [];
        foreach (string member in recorded)
        {
            if (this.personas.Get(member) is null)
            {
                skipped.Add(member);
                continue;
            }

            MembershipResult result = this.membership.Add(team, member);
            if (result.Outcome is not (MembershipOutcome.Added or MembershipOutcome.AlreadyMember))
            {
                this.logger.LogWarning("Unarchived {Team}, but {Member} could not be added back.", team, member);
            }
        }

        return skipped;
    }

    private async Task<ArchiveOutcome> ArchiveLockedAsync(string request, CancellationToken ct)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        TeamSummary? summary = this.catalog.Find(request);
        string? onDisk = this.Spelling(teamsRoot, summary?.Name ?? request);
        if (onDisk is null && summary is null)
        {
            return Refuse($"There is no Team named \"{request}\".");
        }

        string name = onDisk ?? summary?.Name ?? request;
        string? archived = this.Spelling(archiveRoot, name);
        string destination = Path.Combine(archiveRoot, archived ?? name);
        if (archived is not null && this.files.FileExists(Path.Combine(destination, TeamPaths.TeamFileName)))
        {
            return Refuse($"{name} is already archived.");
        }

        string source = Path.Combine(teamsRoot, name);
        string? clash = archived is null ? null : this.FindClash(source, destination);
        if (clash is not null)
        {
            return Refuse($"{name} can't be archived: the archive already holds \"{clash}\" for this Team. Rename one of them first.");
        }

        IReadOnlyList<string> roster = RosterOf(summary);
        IReadOnlyList<string> roomIds = await this.OpenRoomIdsAsync(name, ct).ConfigureAwait(false);
        TeamFile? original = this.folders.ReadTeamFile(name);
        ArchiveMove.Begin(source, destination, ArchiveMove.ArchivingPrefix, this.files, teamFileCreated: original is null);
        if (!this.WriteRoster(name, original, roster, roomIds))
        {
            ArchiveMove.Abandon(source, destination, ArchiveMove.ArchivingPrefix, this.files);
            return Refuse($"{name} can't be archived: its team file couldn't be written.");
        }

        string? failure = this.MoveWithTasksPaused(name, source, destination, original);
        if (failure is not null)
        {
            return Refuse(failure);
        }

        foreach (string id in roomIds)
        {
            await this.directory.SetRoomArchivedAsync(id, true, ct).ConfigureAwait(false);
        }

        this.RemoveLabels(name, roster);
        return new ArchiveOutcome(true, null);
    }

    private static ArchiveOutcome Refuse(string reason) => new(false, reason);

    /// <summary>Moves the Team folder under the provisioner's gate with the Team's Task writes paused (corrections-12 item 5); on failure the team file goes back as it was.</summary>
    /// <returns>The refusal text, or <see langword="null"/> when the folder moved.</returns>
    private string? MoveWithTasksPaused(string name, string source, string destination, TeamFile? original)
    {
        using IDisposable pause = this.tasks.PauseWrites(name);
        ArchiveMoveResult moved;
        try
        {
            moved = this.folders.RunExclusive(() => ArchiveMove.Move(source, destination, ArchiveMove.ArchivingPrefix, this.files, teamFileCreated: original is null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The copy, its verification or a rename failed; the source is whole, so the team file goes back as it was.
            this.RestoreTeamFile(name, source, original);
            return $"{name} could not be moved: {ex.Message}";
        }

        if (moved == ArchiveMoveResult.Moved)
        {
            return null;
        }

        this.RestoreTeamFile(name, source, original);
        return $"{name} has files open in another program. Close them and try again.";
    }

    /// <summary>The members to record: everyone the catalog lists plus the manager, ordinal, without duplicates.</summary>
    private static List<string> RosterOf(TeamSummary? summary)
    {
        HashSet<string> names = new(summary?.Members ?? [], StringComparer.Ordinal);
        if (summary?.Manager is { Length: > 0 } manager)
        {
            _ = names.Add(manager);
        }

        List<string> roster = [.. names];
        roster.Sort(StringComparer.Ordinal);
        return roster;
    }

    private string RootPath(LibraryRootKind kind) => this.roots.Roots.First(r => r.Kind == kind).FullPath;

    /// <summary>The spelling of the folder under <paramref name="parent"/> that matches <paramref name="name"/> ignoring case, or <see langword="null"/>. Lists the parent: an exists-check is case-sensitive on Linux.</summary>
    private string? Spelling(string parent, string name) =>
        this.files.ListEntries(parent).FirstOrDefault(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The first top-level entry of the live Team folder that the archived Team folder already holds, ignoring case, or <see langword="null"/>.</summary>
    private string? FindClash(string source, string destination)
    {
        IReadOnlyList<string> held = this.files.ListEntries(destination);
        return this.files.ListEntries(source).FirstOrDefault(e => held.Any(h => string.Equals(h, e, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<IReadOnlyList<string>> OpenRoomIdsAsync(string team, CancellationToken ct)
    {
        IReadOnlyList<Room> rooms = await this.directory.GetRoomsAsync(ct).ConfigureAwait(false);
        return [.. rooms.Where(r => !r.Archived && string.Equals(r.Team, team, StringComparison.OrdinalIgnoreCase)).Select(static r => r.Id)];
    }

    /// <summary>Records the roster and Room ids in the Team's <c>_team.md</c>, creating the file first for a Team that has none.</summary>
    private bool WriteRoster(string name, TeamFile? original, IReadOnlyList<string> roster, IReadOnlyList<string> roomIds)
    {
        string? manager = original?.Manager ?? this.catalog.Find(name)?.Manager;
        TeamFile Mark(TeamFile file) => file with { Manager = null } with { ArchivedMembers = roster, ArchivedRooms = roomIds, ArchivedManager = manager };
        if (original is null)
        {
            TeamFile fresh = Mark(new TeamFile(VersionedRef.None, null, null, null, string.Empty));
            return this.folders.CreateTeam(name, fresh, leaveExistingAlone: true).Outcome == TeamCreateOutcome.Created;
        }

        return this.folders.UpdateTeamFile(name, Mark);
    }

    /// <summary>Puts the Team's <c>_team.md</c> back as it was after a refused move.</summary>
    private void RestoreTeamFile(string name, string source, TeamFile? original)
    {
        if (original is not null)
        {
            _ = this.folders.UpdateTeamFile(name, _ => original);
        }
        else
        {
            this.files.DeleteFile(Path.Combine(source, TeamPaths.TeamFileName));
        }
    }

    /// <summary>Whether the member's Persona still carries the Team label.</summary>
    private bool HasLabel(string member, string team)
    {
        Persona? persona = this.personas.Get(member);
        return persona is not null
            && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
            && identity.Teams.Any(t => string.Equals(t, team, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RecoverLockedAsync(CancellationToken ct)
    {
        string teamsRoot = this.RootPath(LibraryRootKind.Teams);
        string archiveRoot = this.RootPath(LibraryRootKind.Archive);
        _ = this.folders.RunExclusive(
            () =>
            {
                ArchiveMove.Recover(archiveRoot, teamsRoot, this.files);
                return true;
            });

        IReadOnlyList<string> live = [.. this.files.ListEntries(teamsRoot).Where(static e => !e.StartsWith('.'))];
        IReadOnlyList<string> archived = [.. this.files.ListEntries(archiveRoot).Where(static e => !e.StartsWith('.'))];
        foreach (string team in archived.Where(a => !live.Contains(a, StringComparer.OrdinalIgnoreCase)))
        {
            await this.FinishArchiveAsync(archiveRoot, team, ct).ConfigureAwait(false);
        }

        foreach (string team in live)
        {
            string? archivedSpelling = archived.FirstOrDefault(a => string.Equals(a, team, StringComparison.OrdinalIgnoreCase));
            if (archivedSpelling is not null && this.files.FileExists(Path.Combine(archiveRoot, archivedSpelling, TeamPaths.TeamFileName)))
            {
                this.logger.LogWarning("{Team} exists in both Teams and Archive; archive recovery left both alone.", team);
                continue;
            }

            await this.RestoreLiveTeamAsync(team, ct).ConfigureAwait(false);
        }
    }

    /// <summary>An archived Team the crash left short of the post-move steps: archive its recorded Rooms and remove the labels that remain.</summary>
    private async Task FinishArchiveAsync(string archiveRoot, string team, CancellationToken ct)
    {
        string path = Path.Combine(archiveRoot, team, TeamPaths.TeamFileName);
        string? text = this.files.ReadText(path);
        if (text is null || !TeamFile.TryParse(text, out TeamFile? file))
        {
            return;
        }

        foreach (string id in file.ArchivedRooms)
        {
            await this.directory.SetRoomArchivedAsync(id, true, ct).ConfigureAwait(false);
        }

        foreach (string member in file.ArchivedMembers.Where(m => this.HasLabel(m, team)))
        {
            _ = this.membership.RemoveForArchive(team, member);
        }
    }

    /// <summary>A live Team whose <c>_team.md</c> still holds an archive roster: the crash came before the move finished (or an unarchive stopped short), so put it back.</summary>
    private async Task RestoreLiveTeamAsync(string team, CancellationToken ct)
    {
        TeamFile? file = this.folders.ReadTeamFile(team);
        if (file is null || file.ArchivedMembers.Count == 0)
        {
            return;
        }

        _ = this.AddMembersBack(team, file.ArchivedMembers);
        foreach (string id in file.ArchivedRooms)
        {
            await this.directory.SetRoomArchivedAsync(id, false, ct).ConfigureAwait(false);
        }

        string? manager = file.ArchivedManager is { } recorded && this.personas.Get(recorded) is not null ? recorded : null;
        _ = this.folders.UpdateTeamFile(
            team,
            f => f with { ArchivedMembers = [], ArchivedRooms = [], ArchivedManager = null, Manager = manager ?? f.Manager });
    }

    private void RemoveLabels(string team, IReadOnlyList<string> roster)
    {
        foreach (string member in roster)
        {
            MembershipResult result = this.membership.RemoveForArchive(team, member);
            if (result.Outcome is not (MembershipOutcome.Removed or MembershipOutcome.NotMember))
            {
                this.logger.LogWarning("Archived {Team}, but {Member}'s Team label could not be removed.", team, member);
            }
        }
    }

    /// <summary>The members whose labels, less this Team, are empty. Read before any label is removed.</summary>
    private List<string> MembersWithoutAnotherTeam(string team)
    {
        TeamSummary? summary = this.catalog.Find(team);
        List<string> names = [];
        foreach (string member in RosterOf(summary))
        {
            Persona? persona = this.personas.Get(member);
            if (persona is not null
                && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
                && !identity.Teams.Any(t => !string.Equals(t, team, StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(member);
            }
        }

        return names;
    }
}
