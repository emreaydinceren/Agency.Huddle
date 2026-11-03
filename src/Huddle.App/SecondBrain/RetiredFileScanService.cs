using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>One-shot start-up scan: logs a warning for each retired file or folder (Spec §6.8, correction C7).</summary>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="logger">The logger.</param>
internal sealed class RetiredFileScanService(IOptions<TeamOptions> options, ILogger<RetiredFileScanService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (string path in RetiredFileScan.Find(options.Value.DataDir))
        {
            logger.LogWarning("{Path} is no longer read by Huddle. The Second Brain layout replaced it; see Huddle.SecondBrain.md.", path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
