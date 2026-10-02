using System.Diagnostics;

namespace Agency.Huddle.App.Desktop;

/// <summary>The outcome of <see cref="DesktopHandOff.AcquireOrHandOffAsync"/>.</summary>
/// <param name="Lock">The instance lock when the caller became the owner; otherwise <see langword="null"/>.</param>
/// <param name="Opened">Whether the running instance's URL was opened.</param>
internal sealed record HandOffResult(InstanceLock? Lock, bool Opened);

/// <summary>
/// The second-launch path: when another instance already owns the data directory, wait for it to publish its
/// <c>instance.json</c> and open its URL in the browser instead of starting a second server.
/// </summary>
internal static class DesktopHandOff
{
    /// <summary>
    /// Becomes the owner of <paramref name="dataDir"/> when nobody holds the instance lock, and otherwise hands
    /// off to the running instance. Each round tries the lock first, so an owner that is shutting down and
    /// releases the lock while no record is published is taken over rather than waited on until the timeout.
    /// </summary>
    /// <param name="dataDir">The data directory shared with the running instance.</param>
    /// <param name="opener">Opens the running instance's URL.</param>
    /// <param name="timeout">How long to wait for either the lock or a record before giving up.</param>
    /// <param name="pollInterval">The pause between rounds.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>
    /// The held lock (the caller is the owner); or no lock and <c>Opened</c> true when the running instance was
    /// opened; or no lock and <c>Opened</c> false on timeout.
    /// </returns>
    internal static async Task<HandOffResult> AcquireOrHandOffAsync(
        string dataDir,
        IBrowserOpener opener,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            InstanceLock? acquired = InstanceLock.TryAcquire(dataDir);
            if (acquired is not null)
            {
                return new HandOffResult(acquired, false);
            }

            InstanceInfo? info = InstanceFile.TryRead(dataDir);
            if (info is not null)
            {
                opener.Open(info.Url);
                return new HandOffResult(null, true);
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return new HandOffResult(null, false);
            }

            await Task.Delay(pollInterval, ct);
        }
    }
}
