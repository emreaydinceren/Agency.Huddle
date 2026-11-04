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

        int? exitCode = null;
        string? logPath = null;
        int? logLine = null;
        string note = string.Empty;
        for (int index = 3; index < fields.Length; index++)
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
                evidence = null;
                return false;
            }
        }

        evidence = new EvidenceLine(time, fields[1], fields[2], exitCode, logPath, logLine, text, note);
        return true;
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

            string text = raw.Trim();
            if (!inSection || text.Length == 0)
            {
                continue;
            }

            lines.Add(TryParse(text, out EvidenceLine? evidence) && evidence is not null
                ? evidence
                : new EvidenceLine(default, string.Empty, string.Empty, null, null, null, text));
        }

        return lines;
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
