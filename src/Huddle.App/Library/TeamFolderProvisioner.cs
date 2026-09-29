using System.Globalization;
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
internal sealed class TeamFolderProvisioner : IHostedService, IDisposable
{
    private const string TeamsRootId = "teams";

    private readonly PersonaStore personas;
    private readonly LibraryRootStore roots;
    private readonly LibraryPathResolver resolver;
    private readonly ILogger<TeamFolderProvisioner> logger;
    private bool subscribed = true;

    /// <param name="personas">The source of Team labels (<see cref="PersonaStore.Teams"/>) and the
    /// synchronous change notification this class subscribes to, starting immediately (item 35: the
    /// change can arrive before <see cref="StartAsync"/> runs).</param>
    /// <param name="roots">Locates the Teams root's absolute path.</param>
    /// <param name="resolver">Validates and resolves every folder this class creates, so a label never
    /// bypasses the Library's path boundary (corrections-B4 item 36).</param>
    /// <param name="logger">Logs a warning for a skipped label or a failed create.</param>
    public TeamFolderProvisioner(PersonaStore personas, LibraryRootStore roots, LibraryPathResolver resolver, ILogger<TeamFolderProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);

        this.personas = personas;
        this.roots = roots;
        this.resolver = resolver;
        this.logger = logger;
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

        string? projectNameError = LibraryNames.Validate(project);
        if (projectNameError is not null)
        {
            return new LibraryResult<LibraryPath>(null, projectNameError);
        }

        if (string.Equals(project, TeamNames.MemoryFolder, StringComparison.OrdinalIgnoreCase))
        {
            return new LibraryResult<LibraryPath>(null, TeamNames.MemoryReservedProblem);
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
        LibraryRoot? teamsRoot = this.roots.Roots.FirstOrDefault(r => string.Equals(r.Id, TeamsRootId, StringComparison.Ordinal));
        if (teamsRoot is null)
        {
            return;
        }

        string[] existing;
        try
        {
            existing = Directory.Exists(teamsRoot.FullPath) ? Directory.GetDirectories(teamsRoot.FullPath) : [];
        }
        catch (IOException ex)
        {
            this.LogSyncFailed(ex);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.LogSyncFailed(ex);
            return;
        }

        HashSet<string> seenLabels = new(StringComparer.OrdinalIgnoreCase);
        foreach (string label in this.personas.Teams)
        {
            if (!seenLabels.Add(label))
            {
                continue;
            }

            string? nameError = LibraryNames.Validate(label);
            if (nameError is not null)
            {
                this.LogLabelSkipped(label, nameError);
                continue;
            }

            if (TaskLayout.IsReservedFolderName(label))
            {
                this.LogLabelSkipped(label, "That folder is reserved.");
                continue;
            }

            if (Array.Exists(existing, d => string.Equals(Path.GetFileName(d), label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.Combine(teamsRoot.FullPath, label));
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

    private void LogLabelSkipped(string label, string reason) =>
        this.logger.LogWarning("Team label '{Label}' was skipped: {Reason}", label, reason);

    private void LogCreateFailed(string label, Exception exception) =>
        this.logger.LogWarning(exception, "Couldn't create the folder for Team label '{Label}'.", label);

    private void LogSyncFailed(Exception exception) =>
        this.logger.LogWarning(exception, "Team folder sync failed.");
}
