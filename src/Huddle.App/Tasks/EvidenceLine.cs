using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// One line of a Task's "## Evidence" section: when, who, which command, its exit code and where the tool-call log entry is.
/// A line that is not a structured Evidence line (a human note, or a line with a malformed time) is kept with an empty
/// <see cref="Command"/> and its text in <see cref="RawText"/>, and its <see cref="Time"/> is the default value.
/// <see cref="Note"/> is what the author said about a structured line's run; it is a field of its own because a Proof compares
/// <see cref="Command"/> to the Definition of Done's command exactly.
/// </summary>
internal sealed partial record EvidenceLine(DateTimeOffset Time, string Author, string Command, int? ExitCode, string? LogPath, int? LogLine, string RawText, string Note = "")
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm'Z'";
    private const string Separator = " | ";
    private const string NotePrefix = "note: ";
    private const string EvidenceHeading = "## Evidence";
    private static readonly string[] BulletMarkers = ["- ", "* ", "+ "];

    /// <summary>Parses one Evidence line. Free text without a separator becomes a note; a line with separators must have a valid time, an author and a command.</summary>
    /// <param name="line">The line to parse, without its line break.</param>
    /// <param name="evidence">The parsed line, or null when the text is a structured line that does not parse.</param>
    /// <returns>True when the line is a structured Evidence line or a note; false for a malformed structured line.</returns>
    public static bool TryParse(string line, out EvidenceLine? evidence)
    {
        string text = line.Trim();
        if (text.Length == 0)
        {
            evidence = null;
            return false;
        }

        if (!text.Contains(Separator, StringComparison.Ordinal))
        {
            evidence = new EvidenceLine(default, string.Empty, string.Empty, null, null, null, text);
            return true;
        }

        string[] fields = text.Split(Separator);
        if (fields.Length < 3 || !TryParseTime(fields[0], out DateTimeOffset time) || fields[1].Length == 0 || fields[2].Length == 0)
        {
            evidence = null;
            return false;
        }

        if (!TryParseExtras(fields, 3, out int? exitCode, out string? logPath, out int? logLine, out string note))
        {
            evidence = null;
            return false;
        }

        evidence = new EvidenceLine(time, fields[1], fields[2], exitCode, logPath, logLine, text, note);
        return true;
    }

    /// <summary>Parses the optional fields of a structured line (exit code, log reference, note), by the rules and order limits <see cref="TryParse"/> has always had.</summary>
    /// <param name="fields">The line split on the field separator.</param>
    /// <param name="start">The index of the first optional field.</param>
    /// <param name="exitCode">The exit code, or null.</param>
    /// <param name="logPath">The log file path, or null.</param>
    /// <param name="logLine">The log line number, or null.</param>
    /// <param name="note">The note, or empty.</param>
    /// <returns>False when a field is none of the three or repeats one.</returns>
    private static bool TryParseExtras(string[] fields, int start, out int? exitCode, out string? logPath, out int? logLine, out string note)
    {
        exitCode = null;
        logPath = null;
        logLine = null;
        note = string.Empty;
        for (int index = start; index < fields.Length; index++)
        {
            Match exit = ExitField().Match(fields[index]);
            Match log = LogField().Match(fields[index]);
            if (note.Length == 0 && fields[index].StartsWith(NotePrefix, StringComparison.Ordinal))
            {
                note = fields[index][NotePrefix.Length..];
            }
            else if (exit.Success && exitCode is null && logPath is null && TryParseNumber(exit.Groups[1].Value, out int code))
            {
                exitCode = code;
            }
            else if (log.Success && logPath is null && TryParseNumber(log.Groups[2].Value, out int number))
            {
                logPath = log.Groups[1].Value;
                logLine = number;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads an <c>evidence</c> timeline entry as an Evidence line (Spec §7.7.1). Text that starts with <c>note: </c> is a note
    /// line; otherwise the first field is the command and the rest are read as <see cref="TryParse"/> reads them. Text that does
    /// not parse is kept as a note, so no text is lost.
    /// </summary>
    /// <param name="entry">The entry, of any kind; only its time, author and text are read.</param>
    /// <returns>The Evidence line, timed and authored as the entry is.</returns>
    internal static EvidenceLine FromEntry(TimelineEntry entry)
    {
        string text = entry.Text;
        if (text.StartsWith(NotePrefix, StringComparison.Ordinal))
        {
            return NoteLine(entry, text[NotePrefix.Length..]);
        }

        string[] fields = text.Split(Separator);
        if (fields[0].Length == 0 || !TryParseExtras(fields, 1, out int? exitCode, out string? logPath, out int? logLine, out string note))
        {
            return NoteLine(entry, text);
        }

        EvidenceLine line = new(entry.At, entry.Author, fields[0], exitCode, logPath, logLine, string.Empty, note);
        return line with { RawText = line.Format() };
    }

    /// <summary>A note line (no command) carrying <paramref name="entry"/>'s time and author.</summary>
    private static EvidenceLine NoteLine(TimelineEntry entry, string rawText) =>
        new(entry.At, entry.Author, string.Empty, null, null, null, rawText);

    /// <summary>
    /// The line as an <c>evidence</c> timeline entry's text (Spec §7.7.1): the fields after the time and author, joined by the
    /// field separator, or <c>note: </c> and the text for a note line.
    /// </summary>
    /// <returns>The entry text, without a line break.</returns>
    internal string FormatEntryText()
    {
        if (this.Command.Length == 0)
        {
            return NotePrefix + this.RawText;
        }

        List<string> parts = [this.Command];
        if (this.ExitCode is int code)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"exit {code}"));
        }

        if (this.LogPath is not null && this.LogLine is int line)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{this.LogPath}#L{line}"));
        }

        if (this.Note.Length > 0)
        {
            parts.Add(NotePrefix + this.Note);
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// Whether this line records a run of <paramref name="command"/>. Before #340, <c>add_evidence</c> folded a note into the
    /// command, so a stored line reads <c>dotnet test (note)</c>; those lines are never rewritten, so this accepts that form
    /// as well as the exact command. It exists for those older lines and can be removed once they no longer matter.
    /// </summary>
    /// <param name="command">The command to look for, such as a Definition of Done command or a Team's check command.</param>
    /// <returns>True when the trimmed command equals the trimmed <paramref name="command"/>, or starts with it followed by a space and a bracketed note that ends the text.</returns>
    public bool RunsCommand(string command)
    {
        string wanted = command.Trim();
        string stored = this.Command.Trim();
        return string.Equals(stored, wanted, StringComparison.Ordinal)
            || (stored.StartsWith(wanted + " (", StringComparison.Ordinal) && stored.EndsWith(')'));
    }

    /// <summary>Formats the line as time, author, command, then the optional exit code and log reference, joined by the field separator. Notes return their original text.</summary>
    /// <returns>The line text, without a line break.</returns>
    public string Format()
    {
        if (this.Command.Length == 0)
        {
            return this.RawText;
        }

        List<string> parts =
        [
            this.Time.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
            this.Author,
            this.Command,
        ];
        if (this.ExitCode is int code)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"exit {code}"));
        }

        if (this.LogPath is not null && this.LogLine is int line)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{this.LogPath}#L{line}"));
        }

        if (this.Note.Length > 0)
        {
            parts.Add(NotePrefix + this.Note);
        }

        return string.Join(Separator, parts);
    }

    /// <summary>Parses every line under the "## Evidence" heading of a Task document, in order. Other sections are ignored.</summary>
    /// <param name="markdown">The whole Task document.</param>
    /// <returns>The Evidence lines; a line that does not parse is kept as a note so no human text is lost.</returns>
    public static IReadOnlyList<EvidenceLine> ParseSection(string markdown)
    {
        List<EvidenceLine> lines = [];
        bool inSection = false;
        foreach (string raw in markdown.Split('\n'))
        {
            if (raw.StartsWith('#'))
            {
                inSection = string.Equals(raw.Trim(), EvidenceHeading, StringComparison.Ordinal);
                continue;
            }

            if (inSection && ParseSectionLine(raw) is { } evidence)
            {
                lines.Add(evidence);
            }
        }

        return lines;
    }

    /// <summary>Reads one line found inside the "## Evidence" section, as <see cref="ParseSection"/> reads each of its lines.</summary>
    /// <param name="raw">The line, with its leading and trailing spaces and any list marker.</param>
    /// <returns>The Evidence line or note, or null for a blank line.</returns>
    internal static EvidenceLine? ParseSectionLine(string raw)
    {
        string text = StripBullet(raw.Trim());
        if (text.Length == 0)
        {
            return null;
        }

        return TryParse(text, out EvidenceLine? evidence) && evidence is not null
            ? evidence
            : new EvidenceLine(default, string.Empty, string.Empty, null, null, null, text);
    }

    /// <summary>Removes one leading Markdown bullet marker (<c>- </c>, <c>* </c> or <c>+ </c>) and the spaces after it.</summary>
    private static string StripBullet(string text)
    {
        foreach (string marker in BulletMarkers)
        {
            if (text.StartsWith(marker, StringComparison.Ordinal))
            {
                return text[marker.Length..].Trim();
            }
        }

        return text;
    }

    private static bool TryParseTime(string text, out DateTimeOffset time) =>
        DateTimeOffset.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time);

    private static bool TryParseNumber(string digits, out int number) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    [GeneratedRegex("^exit ([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ExitField();

    [GeneratedRegex("^(.+)#L([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LogField();
}
