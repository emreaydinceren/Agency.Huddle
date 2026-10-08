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

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string dataDir = options.Value.DataDir;
    private readonly Lock gate = new();
    private readonly Dictionary<string, (int Lines, long Length)> lineCounts = new(StringComparer.Ordinal);

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

            using FileStream stream = new(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            long length = stream.Length;
            int existing;
            bool needsNewline = false;
            if (this.lineCounts.TryGetValue(fullPath, out (int Lines, long Length) cached) && cached.Length == length)
            {
                existing = cached.Lines;
            }
            else
            {
                (existing, needsNewline) = CountLines(stream);
            }

            _ = stream.Seek(0, SeekOrigin.End);
            if (needsNewline)
            {
                stream.WriteByte((byte)'\n');
                existing++;
            }

            stream.Write(Encoding.UTF8.GetBytes(json));
            stream.Flush();

            int line = existing + 1;
            this.lineCounts[fullPath] = (line, stream.Length);
            return new ToolCallLogRef(relativePath, line);
        }
    }

    /// <summary>
    /// Finds the newest entry in the Persona's log whose tool name or arguments contain <paramref name="text"/>
    /// (ordinal, ignoring case), so Evidence can cite it.
    /// </summary>
    /// <param name="personaName">The Persona whose log is searched.</param>
    /// <param name="text">The text to look for.</param>
    /// <returns>The file (relative, forward slashes) and 1-based line of the match, or <see langword="null"/> when nothing matches.</returns>
    public ToolCallLogRef? FindLatest(string personaName, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        string folderName = SanitiseName(personaName);
        string folder = Path.Combine(this.dataDir, "logs", "tool-calls", folderName);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        lock (this.gate)
        {
            foreach (string file in Directory.GetFiles(folder, "*.jsonl").OrderByDescending(static name => name, StringComparer.Ordinal))
            {
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new(stream, Encoding.UTF8);
                string[] lines = reader.ReadToEnd().Split('\n');
                for (int index = lines.Length - 1; index >= 0; index--)
                {
                    if (IsMatch(lines[index], text))
                    {
                        return new ToolCallLogRef($"logs/tool-calls/{folderName}/{Path.GetFileName(file)}", index + 1);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>True when the JSON log line's tool or arguments contain <paramref name="text"/>; a line that is not JSON never matches.</summary>
    private static bool IsMatch(string jsonLine, string text)
    {
        if (jsonLine.Length == 0)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonLine);
            return IsMatch(document.RootElement, "tool", text) || IsMatch(document.RootElement, "arguments", text);
        }
        catch (JsonException)
        {
            // A torn or hand-edited line is skipped; the search carries on with the earlier lines.
            return false;
        }
    }

    /// <summary>True when the named string property of <paramref name="root"/> contains <paramref name="text"/>.</summary>
    private static bool IsMatch(JsonElement root, string property, string text) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && (value.GetString()?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>The shape of a log line; <see cref="DateTime"/> of kind UTC serialises as ISO-8601 with a <c>Z</c> suffix.</summary>
    private sealed record WireEntry(DateTime Time, string Room, string? Task, string Tool, string Arguments, string Status, long DurationMs);

    /// <summary>Counts the lines already in the open log file and whether its last line lacks a newline.</summary>
    private static (int Lines, bool MissingFinalNewline) CountLines(FileStream stream)
    {
        _ = stream.Seek(0, SeekOrigin.Begin);
        byte[] buffer = new byte[8192];
        int count = 0;
        int last = '\n';
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == '\n')
                {
                    count++;
                }
            }

            last = buffer[read - 1];
        }

        return (count, last != '\n');
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

    /// <summary>
    /// Replaces every character invalid in a file name by <c>_</c>, a name made only of dots by underscores,
    /// a trailing dot or space (which Windows strips) by <c>_</c>, and prefixes a Windows reserved device name with <c>_</c>.
    /// </summary>
    private static string SanitiseName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            _ = builder.Append(invalid.Contains(c) ? '_' : c);
        }

        string sanitised = builder.ToString();
        if (sanitised.All(static c => c == '.'))
        {
            return new string('_', sanitised.Length);
        }

        if (sanitised[^1] is '.' or ' ')
        {
            sanitised = sanitised[..^1] + "_";
        }

        int dot = sanitised.IndexOf('.');
        string stem = (dot < 0 ? sanitised : sanitised[..dot]).TrimEnd(' ');
        return ReservedNames.Contains(stem) ? "_" + sanitised : sanitised;
    }
}
