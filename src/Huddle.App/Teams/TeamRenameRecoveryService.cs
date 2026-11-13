namespace Agency.Huddle.App.Teams;

/// <summary>
/// Runs <see cref="TeamRenameService.RecoverAsync"/> once when the host starts, before archive recovery, the folder provisioner
/// and the Room reconciler, to finish a Team rename that a crash interrupted. It holds no lifetime state, so there is nothing to stop.
/// </summary>
/// <param name="rename">The service that knows how to recover.</param>
internal sealed class TeamRenameRecoveryService(TeamRenameService rename) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => rename.RecoverAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
