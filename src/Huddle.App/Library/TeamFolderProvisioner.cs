using System.Globalization;
using System.Text;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Creates a <c>Teams/&lt;Label&gt;/</c> folder for every <see cref="PersonaStore.Teams"/> label
/// (Spec §6.2's trigger table), on start and on every <see cref="PersonaStore.PersonasChanged"/>. It
/// never renames or deletes a Team folder - a label that no longer has a Persona keeps its folder,
/// shown as an orphan by <see cref="TeamFolderCatalog"/>.
/// </summary>
/// <remarks>
/// <see cref="PersonaStore.PersonasChanged"/> is raised SYNCHRONOUSLY, from inside
/// <see cref="PersonaStore.Add"/>/<see cref="PersonaStore.Update"/>/<see cref="PersonaStore.Remove"/>,
/// strictly after the Persona file is written (corrections-B4 item 34/35) - so this class needs no
/// watcher wait, and its handler must never let an exception escape back into the caller: an
/// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> creating one label's
/// folder is caught, logged, and does not stop the remaining labels from being created (item 34).
/// </remarks>
internal sealed class TeamFolderProvisioner : IHostedService, IDisposable, ITeamFolders
{
    private const string TeamsRootId = "teams";

    /// <summary>How many times <see cref="MoveOver"/> tries before it lets the failure through.</summary>
    private const int MoveAttempts = 10;

    /// <summary>How long <see cref="MoveOver"/> waits between tries.</summary>
    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(30);

    private static readonly UTF8Encoding TeamFileEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock teamFileGate = new();

    // Serialises label-driven folder creation with an archive move. Held by SyncFolders, CreateTeamFolder and
    // RunExclusive; never held while a membership change is made (PersonasChanged runs SyncFolders under the membership gate).
    private readonly Lock folderGate = new();
    private readonly PersonaStore personas;
    private readonly LibraryRootStore roots;
    private readonly LibraryPathResolver resolver;
    private readonly ILogger<TeamFolderProvisioner> logger;
    private readonly ITeamCatalog catalog;
    private bool subscribed = true;

    /// <param name="personas">The source of Team labels (<see cref="PersonaStore.Teams"/>) and the
    /// synchronous change notification this class subscribes to, starting immediately (item 35: the
    /// change can arrive before <see cref="StartAsync"/> runs).</param>
    /// <param name="roots">Locates the Teams root's absolute path.</param>
    /// <param name="resolver">Validates and resolves every folder this class creates, so a label never
    /// bypasses the Library's path boundary (corrections-B4 item 36).</param>
    /// <param name="logger">Logs a warning for a skipped label or a failed create.</param>
    /// <param name="catalog">The Teams as the pages see them, read by <see cref="EnsureTeam"/> and
    /// <see cref="EnsureProjectIn"/>. It lags the disk by the Task store's ~500 ms rebuild.</param>
    public TeamFolderProvisioner(PersonaStore personas, LibraryRootStore roots, LibraryPathResolver resolver, ILogger<TeamFolderProvisioner> logger, ITeamCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(catalog);

        this.personas = personas;
        this.roots = roots;
        this.resolver = resolver;
        this.logger = logger;
        this.catalog = catalog;
        this.personas.PersonasChanged += this.OnPersonasChanged;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.SyncFolders();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Ensures <c>Teams/&lt;Team&gt;/&lt;Project&gt;/</c> exists for the Library Pane's "New Project"
    /// trigger (Spec §6.2). <paramref name="teamFolder"/> is re-resolved through the injected
    /// resolver before use, so a tampered <see cref="LibraryPath.FullPath"/> is ignored
    /// (corrections-B4 item 36). Refused when the Team folder does not already exist yet, or when
    /// <paramref name="project"/> is not a valid folder name.
    /// </summary>
    /// <param name="teamFolder">The already-resolved Team folder to create the Project folder under.</param>
    /// <param name="project">The new Project folder's name, validated as in Tasks spec §9.2.</param>
    /// <returns>The fresh <see cref="LibraryPath"/> on success, or a refusal reason.</returns>
    internal LibraryResult<LibraryPath> EnsureProject(LibraryPath teamFolder, string project)
    {
        ArgumentNullException.ThrowIfNull(teamFolder);
        ArgumentNullException.ThrowIfNull(project);

        string? projectNameError = ValidateProjectName(project);
        if (projectNameError is not null)
        {
            return new LibraryResult<LibraryPath>(null, projectNameError);
        }

        if (!this.resolver.TryResolve(teamFolder.Root.Id, teamFolder.RelativePath, out LibraryPath? freshTeamFolder, out string? teamResolveError) ||
            freshTeamFolder.Role != LibraryNodeRole.TeamFolder)
        {
            return new LibraryResult<LibraryPath>(null, teamResolveError ?? "This folder isn't available in the Library.");
        }

        if (!Directory.Exists(freshTeamFolder.FullPath))
        {
            return new LibraryResult<LibraryPath>(null, "That Team folder doesn't exist yet.");
        }

        // A FILE already occupying the Project's name resolves with Role.File, not Role.ProjectFolder
        // (LibraryPathResolver classifies by what's already on disk) - accepted here too, so
        // Directory.CreateDirectory below is what refuses it (as an IOException, caught just below),
        // not this check (7.2 fix card).
        string projectRelativePath = freshTeamFolder.RelativePath.Length == 0 ? project : $"{freshTeamFolder.RelativePath}/{project}";
        if (!this.resolver.TryResolve(freshTeamFolder.Root.Id, projectRelativePath, out LibraryPath? projectPath, out string? projectResolveError) ||
            projectPath.Role is not (LibraryNodeRole.ProjectFolder or LibraryNodeRole.File))
        {
            return new LibraryResult<LibraryPath>(null, projectResolveError ?? "This folder isn't available in the Library.");
        }

        try
        {
            Directory.CreateDirectory(projectPath.FullPath);
            SecondBrainFolders.Ensure(projectPath.FullPath);
        }
        catch (IOException)
        {
            return new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, LibraryFileService.CouldNotCreateReasonFormat, project));
        }
        catch (UnauthorizedAccessException)
        {
            return new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, LibraryFileService.CouldNotCreateReasonFormat, project));
        }

        if (!this.resolver.TryResolve(freshTeamFolder.Root.Id, projectRelativePath, out LibraryPath? created, out string? reResolveError))
        {
            return new LibraryResult<LibraryPath>(null, reResolveError);
        }

        return new LibraryResult<LibraryPath>(created, null);
    }

    /// <summary>Validates a new Project folder name: a valid folder name that is not the reserved Memory or Uploads folder.</summary>
    /// <param name="project">The proposed Project folder name.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the name is acceptable.</returns>
    private static string? ValidateProjectName(string project)
    {
        string? projectNameError = LibraryNames.Validate(project);
        if (projectNameError is not null)
        {
            return projectNameError;
        }

        return TeamNames.ReservedProjectProblem(project);
    }

    /// <inheritdoc/>
    /// <remarks>Refused when <see cref="TeamNames.ValidateTeamName"/> refuses the name against the catalog, or
    /// when a folder already on disk matches ignoring case (the catalog lags the disk by ~500 ms, so a
    /// second call right after the first must still see the first folder). The new Team reaches the
    /// catalog only after that lag: callers wait on <c>ITeamCatalog.Find</c> before navigating.</remarks>
    public LibraryResult<LibraryPath> EnsureTeam(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string? problem = TeamNames.ValidateTeamName(name, this.catalog.Teams, this.ArchivedTeamNames());
        if (problem is not null)
        {
            return new LibraryResult<LibraryPath>(null, problem);
        }

        if (this.TeamFolderExistsOnDisk(name))
        {
            return new LibraryResult<LibraryPath>(null, $"A Team named \"{name}\" already exists.");
        }

        return this.CreateTeamFolder(name);
    }

    /// <inheritdoc/>
    /// <remarks>Reads the disk only: a Team folder the catalog has not noticed yet is still found.</remarks>
    public LibraryResult<LibraryPath> FindTeam(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (LibraryNames.Validate(name) is not null
            || TaskLayout.IsReservedFolderName(name)
            || !this.resolver.TryResolve(TeamsRootId, name, out LibraryPath? teamPath, out _)
            || teamPath.Role != LibraryNodeRole.TeamFolder
            || !Directory.Exists(teamPath.FullPath))
        {
            return new LibraryResult<LibraryPath>(null, $"There is no Team named \"{name}\".");
        }

        return new LibraryResult<LibraryPath>(teamPath, null);
    }

    /// <inheritdoc/>
    /// <remarks>A Team that exists only as a Persona label first gets its folder, after the same name
    /// checks the start-up sync applies to a label; then <see cref="EnsureProject"/> creates the Project.</remarks>
    public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(project);

        TeamSummary? summary = this.catalog.Find(team);
        if (summary is null)
        {
            return new LibraryResult<LibraryPath>(null, $"There is no Team named \"{team}\".");
        }

        string? projectProblem = TeamNames.ValidateProjectName(project, summary, this.ArchivedProjectNames(summary.Name));
        if (projectProblem is not null)
        {
            return new LibraryResult<LibraryPath>(null, projectProblem);
        }

        if (!summary.HasFolder)
        {
            string? labelProblem = LibraryNames.Validate(summary.Name);

            if (labelProblem is not null)
            {
                return new LibraryResult<LibraryPath>(null, labelProblem);
            }
        }

        // Idempotent when the folder exists already: also covers a catalog that has not yet noticed a
        // folder EnsureTeam just created.
        LibraryResult<LibraryPath> teamFolder = this.CreateTeamFolder(summary.Name);
        if (teamFolder.Value is null)
        {
            return teamFolder;
        }

        return this.EnsureProject(teamFolder.Value, project);
    }

    /// <inheritdoc/>
    /// <remarks>Reuses the folder creation of <see cref="EnsureTeam"/> but, unlike it, accepts a folder that
    /// already exists without a <c>_team.md</c>. The file is written to a temporary name beside the target and
    /// moved into place without overwriting, so a concurrent writer can never be clobbered. An I/O failure while
    /// writing propagates to the caller.</remarks>
    public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(file);

        // A Team with exactly this name keeps its AlreadyExists path below; only a name that differs by case is refused here.
        IReadOnlyList<TeamSummary> others = [.. this.catalog.Teams.Where(t => !string.Equals(t.Name, team, StringComparison.Ordinal))];
        if (TeamNames.ValidateTeamName(team, others, this.ArchivedTeamNames()) is not null)
        {
            return new TeamCreateResult(TeamCreateOutcome.InvalidName, null);
        }

        // A case-sensitive file system would otherwise let "alpha" sit beside "Alpha"; refuse it the way Windows does.
        if (this.TeamFolderDiffersOnlyByCase(team))
        {
            return new TeamCreateResult(TeamCreateOutcome.AlreadyExists, null);
        }

        LibraryResult<LibraryPath> folder = this.CreateTeamFolder(team, leaveExistingAlone);
        if (folder.Value is null)
        {
            return new TeamCreateResult(TeamCreateOutcome.InvalidName, null);
        }

        string target = Path.Combine(folder.Value.FullPath, TeamPaths.TeamFileName);
        if (File.Exists(target))
        {
            return new TeamCreateResult(TeamCreateOutcome.AlreadyExists, null);
        }

        string temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, file.Compose(), TeamFileEncoding);
            File.Move(temp, target, overwrite: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            return new TeamCreateResult(TeamCreateOutcome.AlreadyExists, null);
        }
        finally
        {
            DeleteQuietly(temp);
        }

        return new TeamCreateResult(TeamCreateOutcome.Created, target);
    }

    /// <summary>Deletes a leftover temporary file; a failure must not hide the outcome of the write that preceded it.</summary>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The temporary file is harmless debris; the Team file's outcome is already decided.
        }
    }

    /// <inheritdoc/>
    /// <remarks>Updates in this process are serialised; the new text is written to a temporary file and moved over
    /// the old one, so a reader sees either the old or the new file, never a partial one.</remarks>
    public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(change);

        lock (this.teamFileGate)
        {
            string? path = this.TeamFilePath(team);
            if (path is null || !TryReadTeamFile(path, out TeamFile? current))
            {
                return false;
            }

            TeamFile updated = change(current);
            ArgumentNullException.ThrowIfNull(updated);

            string temp = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temp, updated.Compose(), TeamFileEncoding);
                MoveOver(temp, path);
            }
            finally
            {
                File.Delete(temp);
            }

            return true;
        }
    }

    /// <summary>Moves <paramref name="temp"/> over <paramref name="path"/>, retrying briefly while a reader (the catalog's watcher) still holds the target open.</summary>
    /// <param name="temp">The finished temporary file.</param>
    /// <param name="path">The file to replace.</param>
    private static void MoveOver(string temp, string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MoveAttempts)
            {
                // The target is open in a reader for a moment: wait and try again.
                Thread.Sleep(MoveRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MoveAttempts)
            {
                // Windows reports a file open in a reader as access denied: wait and try again.
                Thread.Sleep(MoveRetryDelay);
            }
        }
    }

    /// <inheritdoc/>
    public TeamFile? ReadTeamFile(string team)
    {
        ArgumentNullException.ThrowIfNull(team);

        string? path = this.TeamFilePath(team);
        return path is not null && TryReadTeamFile(path, out TeamFile? file) ? file : null;
    }

    /// <summary>The path of an existing Team's <c>_team.md</c>, or <see langword="null"/> when the name is invalid, the Team folder is missing or the file is absent.</summary>
    private string? TeamFilePath(string team)
    {
        if (LibraryNames.Validate(team) is not null || TaskLayout.IsReservedFolderName(team))
        {
            return null;
        }

        if (!this.resolver.TryResolve(TeamsRootId, team, out LibraryPath? teamPath, out _) || teamPath.Role != LibraryNodeRole.TeamFolder)
        {
            return null;
        }

        string path = Path.Combine(teamPath.FullPath, TeamPaths.TeamFileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Reads and parses <paramref name="path"/>; an unreadable or malformed file is a miss.</summary>
    private static bool TryReadTeamFile(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TeamFile? file)
    {
        file = null;
        try
        {
            return TeamFile.TryParse(File.ReadAllText(path, TeamFileEncoding), out file);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Whether a directory directly under the Teams root already carries <paramref name="name"/>, ignoring case (the <see cref="SyncFolders"/> pattern).</summary>
    private bool TeamFolderExistsOnDisk(string name)
    {
        LibraryRoot? teamsRoot = this.roots.Roots.FirstOrDefault(r => string.Equals(r.Id, TeamsRootId, StringComparison.Ordinal));
        if (teamsRoot is null || !Directory.Exists(teamsRoot.FullPath))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateDirectories(teamsRoot.FullPath)
                .Any(d => string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
            return false;
        }
    }

    /// <summary>Whether a directory directly under the Teams root matches <paramref name="name"/> ignoring case but not exactly.</summary>
    private bool TeamFolderDiffersOnlyByCase(string name)
    {
        LibraryRoot? teamsRoot = this.roots.Roots.FirstOrDefault(r => string.Equals(r.Id, TeamsRootId, StringComparison.Ordinal));
        if (teamsRoot is null || !Directory.Exists(teamsRoot.FullPath))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateDirectories(teamsRoot.FullPath)
                .Select(Path.GetFileName)
                .Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase) && !string.Equals(f, name, StringComparison.Ordinal));
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
            return false;
        }
    }

    /// <summary>Creates <c>Teams/&lt;name&gt;/</c> through the resolver (which must classify it as a Team folder) and re-resolves it; never renames or deletes.</summary>
    private LibraryResult<LibraryPath> CreateTeamFolder(string name, bool leaveExistingAlone = false)
    {
        lock (this.folderGate)
        {
            return this.ArchivedTeamExists(name)
                ? new LibraryResult<LibraryPath>(null, $"The Team \"{name}\" is archived.")
                : this.CreateTeamFolderLocked(name, leaveExistingAlone);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Holds the gate <see cref="SyncFolders"/> and the folder creation also take, so a label sync cannot recreate the folder mid-move.</remarks>
    public T RunExclusive<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (this.folderGate)
        {
            return action();
        }
    }

    /// <summary>Whether <c>Archive/&lt;name&gt;/_team.md</c> exists, the folder matched ignoring case by listing the Archive root. A Project archive alone (no <c>_team.md</c>) does not count.</summary>
    /// <param name="name">The Team's name.</param>
    private bool ArchivedTeamExists(string name) =>
        this.ArchivedTeamNames().Any(archived => string.Equals(archived, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The folder names under the Archive root that hold a <c>_team.md</c>, in their on-disk spelling.</summary>
    private List<string> ArchivedTeamNames() =>
        this.ListArchive(null, static folder => File.Exists(Path.Combine(folder, TeamPaths.TeamFileName)));

    /// <summary>The Project folder names under <c>Archive/&lt;team&gt;/</c> (the Team matched ignoring case), in their on-disk spelling.</summary>
    /// <param name="team">The Team's name.</param>
    private List<string> ArchivedProjectNames(string team)
    {
        string? folder = this.ListArchive(null, static _ => true)
            .FirstOrDefault(d => string.Equals(d, team, StringComparison.OrdinalIgnoreCase));
        return folder is null ? [] : this.ListArchive(folder, static f => !TaskLayout.IsReservedFolderName(Path.GetFileName(f)));
    }

    /// <summary>Lists the sub-folder names of the Archive root (or of one folder in it) that satisfy <paramref name="keep"/>; empty when the root is missing or unreadable.</summary>
    /// <param name="inside">A folder directly under the Archive root, or null for the root itself.</param>
    /// <param name="keep">Receives a candidate folder's full path.</param>
    private List<string> ListArchive(string? inside, Func<string, bool> keep)
    {
        LibraryRoot? archive = this.roots.Roots.FirstOrDefault(r => r.Kind == LibraryRootKind.Archive);
        if (archive is null)
        {
            return [];
        }

        string parent = inside is null ? archive.FullPath : Path.Combine(archive.FullPath, inside);
        if (!Directory.Exists(parent))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateDirectories(parent).Where(keep).Select(static d => Path.GetFileName(d))];
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
            return [];
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
            return [];
        }
    }

    private LibraryResult<LibraryPath> CreateTeamFolderLocked(string name, bool leaveExistingAlone)
    {
        if (!this.resolver.TryResolve(TeamsRootId, name, out LibraryPath? teamPath, out string? resolveError) ||
            teamPath.Role != LibraryNodeRole.TeamFolder)
        {
            return new LibraryResult<LibraryPath>(null, resolveError ?? "This folder isn't available in the Library.");
        }

        try
        {
            // A folder that exists already was set up when it was made (or is the Human's); the archive adds a
            // _team.md to such a folder and must not add folders beside it.
            bool existed = Directory.Exists(teamPath.FullPath);
            Directory.CreateDirectory(teamPath.FullPath);
            if (!existed || !leaveExistingAlone)
            {
                SecondBrainFolders.Ensure(teamPath.FullPath);
            }
        }
        catch (IOException)
        {
            return new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, LibraryFileService.CouldNotCreateReasonFormat, name));
        }
        catch (UnauthorizedAccessException)
        {
            return new LibraryResult<LibraryPath>(null, string.Format(CultureInfo.InvariantCulture, LibraryFileService.CouldNotCreateReasonFormat, name));
        }

        if (!this.resolver.TryResolve(TeamsRootId, name, out LibraryPath? created, out string? reResolveError))
        {
            return new LibraryResult<LibraryPath>(null, reResolveError);
        }

        return new LibraryResult<LibraryPath>(created, null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.subscribed)
        {
            this.personas.PersonasChanged -= this.OnPersonasChanged;
            this.subscribed = false;
        }
    }

    /// <summary>Handles a synchronous <see cref="PersonaStore.PersonasChanged"/> raised from inside
    /// <see cref="PersonaStore.Add"/>/<see cref="PersonaStore.Update"/>/<see cref="PersonaStore.Remove"/>:
    /// a failure here must never propagate back into that call (corrections-B4 item 34).</summary>
    private void OnPersonasChanged()
    {
        try
        {
            this.SyncFolders();
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
        }
    }

    /// <summary>Creates a folder for every distinct (ordinal-ignore-case) Team label that does not
    /// already have one on disk, skipping a label that <see cref="LibraryNames.Validate"/> or
    /// <see cref="TaskLayout.IsReservedFolderName"/> refuses (corrections-B4 item 37). Never renames
    /// or deletes.</summary>
    private void SyncFolders()
    {
        lock (this.folderGate)
        {
            this.SyncFoldersLocked();
        }
    }

    /// <summary>Whether a label can be a Team folder name; logs the reason when it cannot.</summary>
    /// <param name="label">The Team label.</param>
    private bool LabelIsFolderName(string label)
    {
        string? nameError = LibraryNames.Validate(label);
        if (nameError is not null)
        {
            this.LogLabelSkipped(label, nameError);
            return false;
        }

        if (TaskLayout.IsReservedFolderName(label) || TeamNames.IsReservedTeamName(label))
        {
            this.LogLabelSkipped(label, "That folder is reserved.");
            return false;
        }

        return true;
    }

    /// <summary><see cref="SyncFolders"/> with <see cref="folderGate"/> already held.</summary>
    private void SyncFoldersLocked()
    {
        LibraryRoot? teamsRoot = this.roots.Roots.FirstOrDefault(r => string.Equals(r.Id, TeamsRootId, StringComparison.Ordinal));
        if (teamsRoot is null)
        {
            return;
        }

        this.TryEnsureSharedMemory(teamsRoot.FullPath);
        string[]? existing = this.TryListTeamFolders(teamsRoot.FullPath);
        if (existing is null)
        {
            return;
        }

        HashSet<string> seenLabels = new(StringComparer.OrdinalIgnoreCase);
        foreach (string label in this.personas.Teams)
        {
            if (!seenLabels.Add(label))
            {
                continue;
            }

            if (!this.LabelIsFolderName(label)
                || Array.Exists(existing, d => string.Equals(Path.GetFileName(d), label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (this.ArchivedTeamExists(label))
            {
                this.LogLabelSkipped(label, "That Team is archived.");
                continue;
            }

            try
            {
                string labelFolder = Path.Combine(teamsRoot.FullPath, label);
                Directory.CreateDirectory(labelFolder);
                SecondBrainFolders.Ensure(labelFolder);
            }
            catch (IOException ex)
            {
                this.LogCreateFailed(label, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                this.LogCreateFailed(label, ex);
            }
        }
    }

    /// <summary>Creates <c>Teams/memory/</c>, logging (not throwing) a failure.</summary>
    /// <param name="teamsRootPath">The Teams root's full path.</param>
    private void TryEnsureSharedMemory(string teamsRootPath)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(teamsRootPath, TeamNames.MemoryFolder));
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
        }
    }

    /// <summary>Lists the directories directly under the Teams root, logging (not throwing) a failure.</summary>
    /// <param name="teamsRootPath">The Teams root's full path.</param>
    /// <returns>The directory paths (empty when the root doesn't exist yet), or <see langword="null"/> when listing failed.</returns>
    private string[]? TryListTeamFolders(string teamsRootPath)
    {
        try
        {
            return Directory.Exists(teamsRootPath) ? Directory.GetDirectories(teamsRootPath) : [];
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
            return null;
        }
    }

    private void LogLabelSkipped(string label, string reason) =>
        this.logger.LogWarning("Team label '{Label}' was skipped: {Reason}", label, reason);

    private void LogCreateFailed(string label, Exception exception) =>
        this.logger.LogWarning(exception, "Couldn't create the folder for Team label '{Label}'.", label);

    private void LogSyncFailed(Exception exception) =>
        this.logger.LogWarning(exception, "Team folder sync failed.");
}
