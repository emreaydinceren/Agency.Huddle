using Agency.Huddle.App.SecondBrain;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>An <see cref="IArchiveService"/> with nothing archived and every move done, for UI tests that render the archive-aware components but do not test archiving.</summary>
internal sealed class EmptyArchiveService : IArchiveService
{
    /// <inheritdoc />
    public ArchiveConfirmation Confirm(string team) => new(0, 0, []);

    /// <inheritdoc />
    public Task<ArchiveOutcome> ArchiveTeamAsync(string team, CancellationToken ct) => Task.FromResult(new ArchiveOutcome(true, null));

    /// <inheritdoc />
    public Task<ArchiveOutcome> UnarchiveTeamAsync(string team, CancellationToken ct) => Task.FromResult(new ArchiveOutcome(true, null));

    /// <inheritdoc />
    public Task<ArchiveOutcome> ArchiveProjectAsync(string team, string project, CancellationToken ct) => Task.FromResult(new ArchiveOutcome(true, null));

    /// <inheritdoc />
    public Task<ArchiveOutcome> UnarchiveProjectAsync(string team, string project, CancellationToken ct) => Task.FromResult(new ArchiveOutcome(true, null));

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedTeams() => [];

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedProjects(string team) => [];
}
