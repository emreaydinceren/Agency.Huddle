using System.Text.Json;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The record a running desktop instance publishes so that a second launch and <c>huddle stop</c> can find it.
/// </summary>
/// <param name="Url">The loopback URL the instance listens on.</param>
/// <param name="StopToken">The token that authenticates <c>POST /_desktop/stop</c>.</param>
/// <param name="Pid">The operating-system process id of the instance.</param>
internal sealed record InstanceInfo(string Url, string StopToken, int Pid);

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
                Publish(temp, final);
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
    /// Moves <paramref name="temp"/> over <paramref name="final"/>. When <paramref name="final"/> exists it is
    /// replaced with <see cref="File.Replace(string, string, string?)"/> rather than
    /// <c>File.Move(..., overwrite: true)</c>, because on Windows the move is refused with "access denied" while a
    /// reader holds the destination open, even a reader that shares delete; the replace API is not.
    /// </summary>
    /// <param name="temp">The fully written temporary file.</param>
    /// <param name="final">The published file.</param>
    private static void Publish(string temp, string final)
    {
        if (File.Exists(final))
        {
            File.Replace(temp, final, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temp, final);
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
        try
        {
            File.Delete(Path.Combine(dataDir, FileName));
        }
        catch (DirectoryNotFoundException)
        {
            // The data directory does not exist, so there is no record to delete.
        }
    }
}
