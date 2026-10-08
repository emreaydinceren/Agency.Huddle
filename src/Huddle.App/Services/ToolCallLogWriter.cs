using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>One tool call as it is written to the lasting tool-call log.</summary>
/// <param name="Time">When the call started or finished; its UTC date picks the log file.</param>
/// <param name="Room">The Room the Turn ran in.</param>
/// <param name="Task">The Task the Turn belonged to, or <see langword="null"/> when unknown.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Arguments">A summary of the arguments; cut to <see cref="ToolCallLogWriter.MaxArgumentsLength"/> characters when written.</param>
/// <param name="Status">The final status of the call.</param>
/// <param name="DurationMs">How long the call took, in milliseconds.</param>
internal sealed record ToolCallLogEntry(
    DateTimeOffset Time,
    string Room,
    string? Task,
    string Tool,
    string Arguments,
    string Status,
    long DurationMs);

/// <summary>Where an appended tool call landed, so Evidence can cite <c>RelativePath#L{Line}</c>.</summary>
/// <param name="RelativePath">The log file relative to the data folder, with forward slashes.</param>
/// <param name="Line">The 1-based line of the entry in that file.</param>
internal sealed record ToolCallLogRef(string RelativePath, int Line);

/// <summary>
/// Appends one JSON line per tool call to <c>logs/tool-calls/&lt;Name&gt;/&lt;yyyy-mm-dd&gt;.jsonl</c>
/// under the data folder (Spec Tool-call log, RT-8). The file is named from the entry's time in UTC.
/// A single lock serialises appends, so concurrent callers get distinct, correct line numbers.
/// </summary>
internal sealed class ToolCallLogWriter(IOptions<TeamOptions> options)
{
    /// <summary>The most characters of <see cref="ToolCallLogEntry.Arguments"/> kept, including the trailing ellipsis.</summary>
    internal const int MaxArgumentsLength = 200;

    private const string Ellipsis = "…";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string dataDir = options.Value.DataDir;
    private readonly Lock gate = new();
    private readonly Dictionary<string, int> lineCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Appends <paramref name="entry"/> to the Persona's log for the entry's UTC day.</summary>
    /// <param name="personaName">The Persona the call belongs to; characters invalid in a file name are replaced by <c>_</c>.</param>
    /// <param name="entry">The call to record.</param>
    /// <returns>The file (relative, forward slashes) and 1-based line the entry was written to.</returns>
    /// <exception cref="IOException">The log file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The log file is not writable.</exception>
    public ToolCallLogRef Append(string personaName, ToolCallLogEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);
        ArgumentNullException.ThrowIfNull(entry);

        DateTimeOffset utc = entry.Time.ToUniversalTime();
        string folderName = SanitiseName(personaName);
        string fileName = utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl";
        string relativePath = $"logs/tool-calls/{folderName}/{fileName}";
        string fullPath = Path.Combine(this.dataDir, "logs", "tool-calls", folderName, fileName);

        WireEntry written = new(utc.UtcDateTime, entry.Room, entry.Task, entry.Tool, Truncate(entry.Arguments), entry.Status, entry.DurationMs);
        string json = JsonSerializer.Serialize(written, JsonOptions) + "\n";

        lock (this.gate)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? this.dataDir);

            if (!this.lineCounts.TryGetValue(fullPath, out int existing))
            {
                existing = CountLines(fullPath);
            }

            using FileStream stream = new(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(Encoding.UTF8.GetBytes(json));

            int line = existing + 1;
            this.lineCounts[fullPath] = line;
            return new ToolCallLogRef(relativePath, line);
        }
    }

    /// <summary>The shape of a log line; <see cref="DateTime"/> of kind UTC serialises as ISO-8601 with a <c>Z</c> suffix.</summary>
    private sealed record WireEntry(DateTime Time, string Room, string? Task, string Tool, string Arguments, string Status, long DurationMs);

    /// <summary>Counts the lines already in a log file left by an earlier run; zero when it does not exist.</summary>
    private static int CountLines(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        int count = 0;
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int next;
        while ((next = stream.ReadByte()) >= 0)
        {
            if (next == '\n')
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Cuts the text to <see cref="MaxArgumentsLength"/> characters, the last being an ellipsis, without splitting a surrogate pair.</summary>
    private static string Truncate(string text)
    {
        if (text.Length <= MaxArgumentsLength)
        {
            return text;
        }

        int keep = MaxArgumentsLength - Ellipsis.Length;
        if (char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        return text[..keep] + Ellipsis;
    }

    /// <summary>Replaces every character invalid in a file name by <c>_</c>, and a name made only of dots by underscores, so the folder stays inside the log root.</summary>
    private static string SanitiseName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            _ = builder.Append(invalid.Contains(c) ? '_' : c);
        }

        string sanitised = builder.ToString();
        return sanitised.All(static c => c == '.') ? new string('_', sanitised.Length) : sanitised;
    }
}
