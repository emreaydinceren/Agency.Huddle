using Agency.Huddle.App.SecondBrain;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>A recording <see cref="IArchiveService"/>: every call is kept, and the outcomes and lists are set by the test.</summary>
internal sealed class RecordingArchiveService : IArchiveService
{
    /// <summary>What <see cref="Confirm"/> returns.</summary>
    public ArchiveConfirmation Confirmation { get; init; } = new(0, 0, []);

    /// <summary>What <see cref="ArchiveTeamAsync"/> returns.</summary>
    public ArchiveOutcome ArchiveOutcome { get; init; } = new(true, null);

    /// <summary>What <see cref="UnarchiveTeamAsync"/> returns; a success removes the Team from <see cref="ArchivedTeamNames"/>.</summary>
    public ArchiveOutcome UnarchiveOutcome { get; init; } = new(true, null);

    /// <summary>What <see cref="ArchiveProjectAsync"/> returns.</summary>
    public ArchiveOutcome ArchiveProjectOutcome { get; init; } = new(true, null);

    /// <summary>What <see cref="UnarchiveProjectAsync"/> returns; a success removes the Project from its Team's list.</summary>
    public ArchiveOutcome UnarchiveProjectOutcome { get; init; } = new(true, null);

    /// <summary>The archived Teams the service's <c>ArchivedTeams</c> returns.</summary>
    public List<string> ArchivedTeamNames { get; init; } = [];

    /// <summary>The archived Projects the service's <c>ArchivedProjects</c> method returns for a Team not in <see cref="ArchivedProjectsByTeam"/>.</summary>
    public List<string> ArchivedProjectNames { get; init; } = [];

    /// <summary>Each Team's own archived Projects, by Team name ignoring case; a Team listed here ignores <see cref="ArchivedProjectNames"/>.</summary>
    public Dictionary<string, List<string>> ArchivedProjectsByTeam { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Teams <see cref="Confirm"/> was asked about.</summary>
    public List<string> Confirmed { get; } = [];

    /// <summary>The Teams archived.</summary>
    public List<string> Archived { get; } = [];

    /// <summary>The Teams unarchived.</summary>
    public List<string> Unarchived { get; } = [];

    /// <summary>The (Team, Project) pairs archived.</summary>
    public List<(string Team, string Project)> ProjectsArchived { get; } = [];

    /// <summary>The (Team, Project) pairs unarchived.</summary>
    public List<(string Team, string Project)> UnarchivedProjects { get; } = [];

    /// <inheritdoc />
    public ArchiveConfirmation Confirm(string team)
    {
        this.Confirmed.Add(team);
        return this.Confirmation;
    }

    /// <inheritdoc />
    public Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct)
    {
        this.Archived.Add(team);
        return Task.FromResult(this.ArchiveOutcome);
    }

    /// <inheritdoc />
    public Task<ArchiveOutcome> UnarchiveTeamAsync(string team, CancellationToken ct)
    {
        this.Unarchived.Add(team);
        if (this.UnarchiveOutcome.Done)
        {
            _ = this.ArchivedTeamNames.Remove(team);
        }

        return Task.FromResult(this.UnarchiveOutcome);
    }

    /// <inheritdoc />
    public Task<ArchiveOutcome> ArchiveProjectAsync(string team, string project, CancellationToken ct)
    {
        this.ProjectsArchived.Add((team, project));
        return Task.FromResult(this.ArchiveProjectOutcome);
    }

    /// <inheritdoc />
    public Task<ArchiveOutcome> UnarchiveProjectAsync(string team, string project, CancellationToken ct)
    {
        this.UnarchivedProjects.Add((team, project));
        if (this.UnarchiveProjectOutcome.Done)
        {
            _ = this.ProjectsOf(team).Remove(project);
        }

        return Task.FromResult(this.UnarchiveProjectOutcome);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedTeams() => [.. this.ArchivedTeamNames];

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedProjects(string team) => [.. this.ProjectsOf(team)];

    /// <summary>The list that holds <paramref name="team"/>'s archived Projects.</summary>
    private List<string> ProjectsOf(string team) =>
        this.ArchivedProjectsByTeam.TryGetValue(team, out List<string>? own) ? own : this.ArchivedProjectNames;
}
