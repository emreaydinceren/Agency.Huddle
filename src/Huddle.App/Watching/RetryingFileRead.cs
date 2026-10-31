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

    /// <summary>
    /// The watcher rebuild a store that keeps its previous snapshot runs: a missing file is a
    /// legitimate delete and yields <paramref name="whenMissing"/>; otherwise <paramref name="read"/>
    /// runs through <see cref="Run{T}"/>, and when every attempt fails the standard warning is logged
    /// and <see langword="null"/> returned, so the caller keeps what it had rather than flickering to
    /// a default over what may be a mid-write race.
    /// </summary>
    /// <typeparam name="T">The parsed document.</typeparam>
    /// <param name="path">The store's file.</param>
    /// <param name="whenMissing">Builds the empty document a deleted file stands for.</param>
    /// <param name="read">Reads and parses <paramref name="path"/>.</param>
    /// <param name="logger">The store's logger.</param>
    /// <param name="fileKind">What the file is, for the warning, such as <c>appearance file</c>.</param>
    /// <param name="contents">What the store keeps, for the warning, such as <c>appearance</c>.</param>
    /// <returns>The document, or <see langword="null"/> when the caller should keep its current snapshot.</returns>
    public static T? RereadKeepingPrevious<T>(string path, Func<T> whenMissing, Func<T> read, ILogger logger, string fileKind, string contents)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(whenMissing);

        if (!File.Exists(path))
        {
            return whenMissing();
        }

        return Run(read, ex => LogGaveUp<T>(ex, path, logger, fileKind, contents));
    }

    /// <summary>Logs why <see cref="RereadKeepingPrevious{T}"/> gave up; the "keeping the previously resolved" wording is what the locked-file tests wait for.</summary>
    /// <typeparam name="T">The parsed document.</typeparam>
    /// <param name="ex">The last failure.</param>
    /// <param name="path">The store's file.</param>
    /// <param name="logger">The store's logger.</param>
    /// <param name="fileKind">What the file is.</param>
    /// <param name="contents">What the store keeps.</param>
    /// <returns><see langword="null"/>, always.</returns>
    private static T? LogGaveUp<T>(Exception ex, string path, ILogger logger, string fileKind, string contents)
        where T : class
    {
        if (ex is JsonException)
        {
            logger.LogWarning(
                ex,
                "Could not parse {FileKind} '{Path}' after a filesystem change, even after retrying; keeping the previously resolved {Contents} rather than reverting to a default over what may be a mid-write race.",
                fileKind,
                path,
                contents);
        }
        else
        {
            logger.LogWarning(
                ex,
                "Could not read {FileKind} '{Path}' after {Attempts} attempts; keeping the previously resolved {Contents}.",
                fileKind,
                path,
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                Attempts,
#pragma warning restore HAA0601
                contents);
        }

        return null;
    }

    private static bool IsTransient(Exception ex) => ex is JsonException or IOException or UnauthorizedAccessException;
}
