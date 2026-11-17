namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Finishes the passes a stop or a crash left without a <c>PassFinished</c> entry, so the runner that owns it can be tested without the disk.</summary>
internal interface IMemoryTidyRecovery
{
    /// <summary>Finishes every unfinished pass folder.</summary>
    /// <param name="cancellationToken">Cancels the work; a pass folder left alone is finished at the next start.</param>
    /// <returns>A task that ends when every unfinished pass has been looked at.</returns>
    Task RecoverAsync(CancellationToken cancellationToken);
}
