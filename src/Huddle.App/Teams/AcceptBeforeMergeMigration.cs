using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Start-up step (#401): adds <c>accept-before-merge</c> to the <c>_team.md</c> of every installed development Team, once per
/// Team; see <see cref="DevelopmentGateMigration"/> for how a Team is handled and never re-touched. A failure is logged and
/// never stops the host. New Teams get the gate from their process template instead.
/// </summary>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="catalog">Lists the Teams.</param>
/// <param name="folders">Reads and writes each Team's <c>_team.md</c>.</param>
/// <param name="logger">Receives a Team that could not be handled.</param>
internal sealed class AcceptBeforeMergeMigration(IOptions<TeamOptions> options, ITeamCatalog catalog, ITeamFolders folders, ILogger<AcceptBeforeMergeMigration> logger) : IHostedService
{
    /// <summary>The team type whose Teams merge code.</summary>
    internal const string DevelopmentType = DevelopmentGateMigration.DevelopmentType;

    private readonly DevelopmentGateMigration migration = new(options, catalog, folders, logger, GateId.AcceptBeforeMerge, "accept-before-merge.migrated");

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.migration.Run();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
