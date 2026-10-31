using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests;

/// <summary>
/// Shared support for the tests that prove a store's filesystem-watcher rebuild survives a file it
/// cannot read: the file is held open with <see cref="FileShare.None"/> so every read attempt throws
/// <see cref="IOException"/>, which on a timer thread would otherwise terminate the test host.
/// </summary>
internal static class LockedWatcherFileSupport
{
    /// <summary>The phrase shared by every store's "could not read, keeping the previous snapshot" warning.</summary>
    private const string GaveUpPhrase = "keeping the previously resolved";

    /// <summary>How long to wait for the store to exhaust its retries before failing the test.</summary>
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Opens <paramref name="path"/> exclusively, replaces its content with <paramref name="content"/> and flushes it,
    /// so the store's watcher fires while the store cannot read the file. Disposing the result releases the lock.
    /// </summary>
    /// <param name="path">The existing backing file.</param>
    /// <param name="content">The new text to write while holding the lock.</param>
    /// <returns>The open, exclusive stream.</returns>
    public static FileStream WriteAndHold(string path, string content)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.SetLength(0);
        stream.Write(Encoding.UTF8.GetBytes(content));
        stream.Flush(true);
        return stream;
    }

    /// <summary>
    /// Waits until <paramref name="logger"/> has recorded the Warning the rebuild logs when it gives up and keeps
    /// its snapshot. Matching the phrase every store's give-up message shares, rather than any Warning, keeps an
    /// unrelated warning from ending the wait early.
    /// </summary>
    /// <typeparam name="T">The logger's category.</typeparam>
    /// <param name="logger">The logger handed to the store under test.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public static async Task WaitForWarningAsync<T>(RecordingLogger<T> logger, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!logger.Snapshot().Any(static entry => entry.Level == LogLevel.Warning && entry.Message.Contains(GaveUpPhrase, StringComparison.Ordinal)))
        {
            if (elapsed.Elapsed > WaitTimeout)
            {
                Assert.Fail("Timed out waiting for the store to give up on the locked file.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
