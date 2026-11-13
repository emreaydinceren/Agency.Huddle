using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>A recording <see cref="ITeamRenamer"/>: every rename is kept, and the checks, the outcome and the mid-Turn members are set by the test.</summary>
internal sealed class RecordingTeamRenamer : ITeamRenamer
{
    /// <summary>What <see cref="CheckTeamName"/> returns for an old and a new name; the default accepts everything.</summary>
    public Func<string, string, string?> TeamCheck { get; init; } = static (_, _) => null;

    /// <summary>What <see cref="CheckProjectName"/> returns for a Team, an old and a new name; the default accepts everything.</summary>
    public Func<string, string, string, string?> ProjectCheck { get; init; } = static (_, _, _) => null;

    /// <summary>What both rename methods return.</summary>
    public TeamRenameOutcome Outcome { get; init; } = new(true, null);

    /// <summary>What <see cref="MidTurnMembersAsync"/> returns.</summary>
    public IReadOnlyList<string> MidTurn { get; init; } = [];

    /// <summary>The (old, new) pairs <see cref="RenameTeamAsync"/> was called with.</summary>
    public List<(string Old, string New)> TeamsRenamed { get; } = [];

    /// <summary>The (Team, old, new) triples <see cref="RenameProjectAsync"/> was called with.</summary>
    public List<(string Team, string Old, string New)> ProjectsRenamed { get; } = [];

    /// <summary>The (old, new) pairs <see cref="CheckTeamName"/> was asked about.</summary>
    public List<(string Old, string New)> TeamChecks { get; } = [];

    /// <inheritdoc />
    public Task<TeamRenameOutcome> RenameTeamAsync(string oldName, string newName, CancellationToken ct)
    {
        this.TeamsRenamed.Add((oldName, newName));
        return Task.FromResult(this.Outcome);
    }

    /// <inheritdoc />
    public Task<TeamRenameOutcome> RenameProjectAsync(string team, string oldProject, string newProject, CancellationToken ct)
    {
        this.ProjectsRenamed.Add((team, oldProject, newProject));
        return Task.FromResult(this.Outcome);
    }

    /// <inheritdoc />
    public string? CheckTeamName(string oldName, string newName)
    {
        this.TeamChecks.Add((oldName, newName));
        return this.TeamCheck(oldName, newName);
    }

    /// <inheritdoc />
    public string? CheckProjectName(string team, string oldProject, string newProject) => this.ProjectCheck(team, oldProject, newProject);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> MidTurnMembersAsync(string team, CancellationToken ct) => Task.FromResult(this.MidTurn);
}
