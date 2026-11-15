using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Start-up step (#538): adds <c>blockers-done-before-start</c> to the <c>_team.md</c> of every installed development Team, once
/// per Team; see <see cref="DevelopmentGateMigration"/> for how a Team is handled and never re-touched. A failure is logged and
/// never stops the host. New Teams get the gate from their process template instead.
/// </summary>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="catalog">Lists the Teams.</param>
/// <param name="folders">Reads and writes each Team's <c>_team.md</c>.</param>
/// <param name="logger">Receives a Team that could not be handled.</param>
internal sealed class BlockersDoneBeforeStartMigration(IOptions<TeamOptions> options, ITeamCatalog catalog, ITeamFolders folders, ILogger<BlockersDoneBeforeStartMigration> logger) : IHostedService
{
    private readonly DevelopmentGateMigration migration = new(options, catalog, folders, logger, GateId.BlockersDoneBeforeStart, "blockers-done-before-start.migrated");

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.migration.Run();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
