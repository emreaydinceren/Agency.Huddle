using System.Text.Json;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// Persists an <see cref="InstanceInfo"/> as camelCase JSON in <c>instance.json</c> inside the data directory.
/// Writes are atomic (temporary file, then move) and reads tolerate a missing, corrupt or incomplete file.
/// </summary>
internal static class InstanceFile
{
    private const string FileName = "instance.json";

    private const string TempFileName = "instance.json.tmp";

    private const int MoveAttempts = 5;

    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(20);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Writes <paramref name="info"/> to <c>instance.json</c>, creating <paramref name="dataDir"/> if needed.
    /// The record is written to <c>instance.json.tmp</c> and moved over the final file, so a concurrent reader
    /// sees either the old record or the new one, never a partial one. A move that a reader briefly blocks is
    /// retried a few times before the exception is allowed to propagate.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="info">The record to publish.</param>
    internal static void Write(string dataDir, InstanceInfo info)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(dataDir);
        }
        else
        {
            Directory.CreateDirectory(dataDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        string temp = Path.Combine(dataDir, TempFileName);
        string final = Path.Combine(dataDir, FileName);
        File.WriteAllText(temp, JsonSerializer.Serialize(info, JsonOptions));

        // The record carries the stop token, so on Unix only the owner may read it.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        for (int attempt = 1; attempt <= MoveAttempts; attempt++)
        {
            try
            {
                AtomicFile.Publish(temp, final);
                return;
            }
            catch (IOException) when (attempt < MoveAttempts)
            {
                // Runs once at startup, so blocking the calling thread briefly is acceptable here.
                Thread.Sleep(MoveRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MoveAttempts)
            {
                // Runs once at startup, so blocking the calling thread briefly is acceptable here.
                Thread.Sleep(MoveRetryDelay);
            }
        }
    }

    /// <summary>
    /// Reads the published record.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    /// <returns>
    /// The record, or <see langword="null"/> when the file is missing, empty, malformed, unreadable, or lacks a
    /// url, a stop token or a positive process id.
    /// </returns>
    internal static InstanceInfo? TryRead(string dataDir)
    {
        string path = Path.Combine(dataDir, FileName);

        try
        {
            // Share Delete so a concurrent File.Move(..., overwrite: true) is not blocked by this reader.
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            InstanceInfo? info = JsonSerializer.Deserialize<InstanceInfo>(stream, JsonOptions);

            // System.Text.Json fills a missing field with null or 0 instead of failing, so check each one.
            if (info is null
                || string.IsNullOrWhiteSpace(info.Url)
                || string.IsNullOrWhiteSpace(info.StopToken)
                || info.Pid <= 0)
            {
                return null;
            }

            return info;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes the published record. Does nothing when there is none.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    internal static void Delete(string dataDir)
    {
        string path = Path.Combine(dataDir, FileName);
        for (int attempt = 1; attempt <= MoveAttempts; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                // The data directory does not exist, so there is no record to delete.
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < MoveAttempts)
            {
                // A scanner or reader still holds the freshly written record without delete sharing; shutdown may block briefly.
                Thread.Sleep(MoveRetryDelay);
            }
            catch (IOException) when (attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryDelay);
            }
        }
    }
}
