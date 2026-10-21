using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Runs <see cref="HelperFeed.Reconcile"/> once at startup (Helpers in the Feed spec §5.3): it resolves the Helper items whose
/// condition ended while Huddle was stopped and remembers the ones that still hold. Registered only while the Feed is on.
/// </summary>
internal sealed class HelperFeedStartup(FeedStore feedStore, HelperFeed helperFeed, ILogger<HelperFeedStartup> logger) : BackgroundService
{
    /// <summary>Reconciles once and ends.</summary>
    /// <param name="stoppingToken">Stops the pass when the host shuts down.</param>
    /// <returns>A task that completes when the pass has run.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            helperFeed.Reconcile(feedStore);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The Feed could not reconcile its Helper items at startup.");
        }
    }
}
