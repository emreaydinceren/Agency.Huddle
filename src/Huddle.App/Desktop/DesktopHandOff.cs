using System.Diagnostics;

namespace Agency.Huddle.App.Desktop;

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
    internal static Task<HandOffResult> AcquireOrHandOffAsync(
        string dataDir,
        IBrowserOpener opener,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
        => AcquireCoreAsync(dataDir, opener, static (info, _) => Task.FromResult(info.Url), timeout, pollInterval, ct);

    /// <summary>
    /// As the overload without a handler, but on a running copy it asks <c>POST /_desktop/bootstrap</c> (instance token from <c>instance.json</c>) for a
    /// fresh admin bootstrap URL and opens that; when the call fails (401, 404, 405, a connection error) it opens <c>{info.Url}/auth</c> (C-12).
    /// </summary>
    /// <param name="dataDir">The data directory shared with the running instance.</param>
    /// <param name="opener">Opens the URL.</param>
    /// <param name="handler">The HTTP handler for the bootstrap call, or <see langword="null"/> for a real network handler.</param>
    /// <param name="timeout">How long to wait for either the lock or a record.</param>
    /// <param name="pollInterval">The pause between rounds.</param>
    /// <param name="ct">Cancels the wait.</param>
    internal static Task<HandOffResult> AcquireOrHandOffAsync(
        string dataDir,
        IBrowserOpener opener,
        HttpMessageHandler? handler,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
        => AcquireCoreAsync(dataDir, opener, (info, token) => BootstrapUrlAsync(info, handler, token), timeout, pollInterval, ct);

    private static async Task<string> BootstrapUrlAsync(InstanceInfo info, HttpMessageHandler? handler, CancellationToken ct)
        => await DesktopBootstrapClient.RequestUrlAsync(info, handler, ct).ConfigureAwait(false) ?? info.Url.TrimEnd('/') + "/auth";

    private static async Task<HandOffResult> AcquireCoreAsync(
        string dataDir,
        IBrowserOpener opener,
        Func<InstanceInfo, CancellationToken, Task<string>> urlFor,
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
                opener.Open(await urlFor(info, ct).ConfigureAwait(false));
                return new HandOffResult(null, true);
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return new HandOffResult(null, false);
            }

            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
        }
    }
}
