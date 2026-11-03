using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Uploads;

/// <summary>Records the <c>brain-check</c> baseline (Spec 7.6) of a file an upload service just saved.</summary>
internal static class RawBaselineRecorder
{
    /// <summary>
    /// Records the size and last-write time of <paramref name="fullPath"/> under its <c>/</c>-separated path relative to
    /// <paramref name="dataDir"/>. A failure is logged and swallowed: the upload has already succeeded.
    /// </summary>
    /// <param name="baselines">The store to record into.</param>
    /// <param name="dataDir">The data folder the path is made relative to.</param>
    /// <param name="fullPath">The saved file's full path.</param>
    /// <param name="logger">Logs a failure; nothing is logged when <see langword="null"/>.</param>
    public static void Record(RawBaselineStore baselines, string dataDir, string fullPath, ILogger? logger)
    {
        try
        {
            string relative = Path.GetRelativePath(Path.GetFullPath(dataDir), fullPath).Replace('\\', '/');
            long size = new FileInfo(fullPath).Length;
            DateTimeOffset modified = new(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero);
            baselines.Record(relative, size, modified);
        }
        catch (Exception ex) when (ex is SqliteException or IOException)
        {
            logger?.LogWarning(ex, "Could not record the raw baseline of {Path}; the upload itself was saved.", fullPath);
        }
    }
}
