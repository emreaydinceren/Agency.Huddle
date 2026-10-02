using System.Diagnostics;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The second-launch path: when another instance already owns the data directory, wait for it to publish its
/// <c>instance.json</c> and open its URL in the browser instead of starting a second server.
/// </summary>
internal static class DesktopHandOff
{
    /// <summary>
    /// Polls <see cref="InstanceFile.TryRead"/> until a record appears, then opens its URL.
    /// </summary>
    /// <param name="dataDir">The data directory shared with the running instance.</param>
    /// <param name="opener">Opens the running instance's URL.</param>
    /// <param name="timeout">How long to wait for a record before giving up.</param>
    /// <param name="pollInterval">The pause between reads.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns><see langword="true"/> when a record was found and opened; <see langword="false"/> on timeout.</returns>
    internal static async Task<bool> OpenExistingAsync(
        string dataDir,
        IBrowserOpener opener,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (true)
        {
            InstanceInfo? info = InstanceFile.TryRead(dataDir);

            if (info is not null)
            {
                opener.Open(info.Url);
                return true;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return false;
            }

            await Task.Delay(pollInterval, ct);
        }
    }
}
