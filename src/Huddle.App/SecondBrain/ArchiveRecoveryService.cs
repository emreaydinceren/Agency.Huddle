namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// Runs <see cref="ArchiveService.RecoverAsync"/> once when the host starts, before the Team folders are synchronised, to
/// finish or undo an archive or unarchive that a crash interrupted. It holds no lifetime state, so there is nothing to stop.
/// </summary>
/// <param name="archive">The service that knows how to recover.</param>
internal sealed class ArchiveRecoveryService(ArchiveService archive) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => archive.RecoverAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
