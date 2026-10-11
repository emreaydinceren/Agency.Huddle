using System.Text.Json;

namespace Agency.Huddle.App.Watching;

/// <summary>
/// The retry policy a store applies when it re-reads its file after a watcher event. A watcher event can
/// fire while an editor is still mid-write, so the read may see a half-written file (a
/// <see cref="JsonException"/>) or a file held open with a sharing lock (an
/// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>). Retrying a few times with a
/// short pause lets an in-place write settle; only then does the caller decide what failing means.
/// </summary>
internal static class RetryingFileRead
{
    /// <summary>How many times the read is attempted in all.</summary>
    public const int Attempts = 3;

    /// <summary>The pause between attempts.</summary>
    public const int DelayMilliseconds = 20;

    /// <summary>
    /// Runs <paramref name="read"/> up to <see cref="Attempts"/> times, pausing <see cref="DelayMilliseconds"/>
    /// after each transient failure. If the final attempt also fails, <paramref name="onGaveUp"/> maps that
    /// last exception to the result (logging its own warning); a store that keeps its previous snapshot
    /// returns <see langword="null"/>, one that surfaces the failure returns a snapshot that carries it.
    /// </summary>
    /// <typeparam name="T">The rebuilt snapshot or document.</typeparam>
    /// <param name="read">Reads and parses the file. May throw the transient exception types above; any other exception propagates.</param>
    /// <param name="onGaveUp">Called with the last failure once every attempt has failed.</param>
    /// <returns>What <paramref name="read"/> returned, or what <paramref name="onGaveUp"/> returned.</returns>
    public static T? Run<T>(Func<T> read, Func<Exception, T?> onGaveUp)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(onGaveUp);

        for (int attempt = 1; attempt < Attempts; attempt++)
        {
            try
            {
                return read();
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // JsonException: caught mid-write, the partial text does not parse yet.
                // IOException / UnauthorizedAccessException: an editor can hold the file open with a sharing lock while it writes.
                Thread.Sleep(DelayMilliseconds);
            }
        }

        try
        {
            return read();
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            return onGaveUp(ex);
        }
    }

    private static bool IsTransient(Exception ex) => ex is JsonException or IOException or UnauthorizedAccessException;
}
