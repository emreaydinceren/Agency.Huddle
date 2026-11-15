using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The timeline half of <see cref="TaskFileFormat"/> (Spec §7.7, ADR-0086): the <c>## Timeline</c> grammar, how a file written
/// before it is read, and the conversion of such a file on its first write.
/// </summary>
internal static partial class TaskFileFormat
{
    /// <summary>The Markdown heading, matched case-insensitively, that starts a Task file's timeline.</summary>
    public const string TimelineHeading = "## Timeline";

    private const string TimelineTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string EvidenceMinuteTimeFormat = "yyyy-MM-dd'T'HH:mm'Z'";

    /// <summary>The headings only Huddle writes: a description may not contain one outside a fenced code block.</summary>
    private static readonly string[] ReservedHeadings = [TimelineHeading, ChangeLogHeading, EvidenceHeading];

    /// <summary>What a body is made of once read: the description and the two lists the timeline merges.</summary>
    private sealed record ParsedBody(string Description, IReadOnlyList<ChangeLogEntry> ChangeLog, IReadOnlyList<TimelineEntry> Comments);

    /// <summary>The lines under an older file's Change log heading: the events, each one's own text, and the lines that never parsed.</summary>
    private sealed record LegacyLog(List<ChangeLogEntry> Events, List<string> EventLines, List<string> LeadingJunk, List<List<string>> JunkAfter);

    /// <summary>An older file's body, read by Spec §7.7.3.</summary>
    private sealed record LegacyBody(string Description, LegacyLog Log, List<TimelineEntry> Comments);

    /// <summary>
    /// The first of <c>## Timeline</c>, <c>## Change log</c> and <c>## Evidence</c> that a line of <paramref name="description"/> trims to
    /// (case-insensitively) outside a fenced code block: the headings a description may not contain (Spec §7.7.5).
    /// </summary>
    /// <param name="description">The text to search.</param>
    /// <returns>The heading in its canonical spelling, or null when there is none.</returns>
    public static string? FindReservedHeading(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        bool inFence = false;
        foreach (string line in Normalize(description).Split('\n'))
        {
            string trimmed = line.Trim();
            if (IsFenceLine(trimmed))
            {
                inFence = !inFence;
            }
            else if (!inFence)
            {
                foreach (string heading in ReservedHeadings)
                {
                    if (string.Equals(trimmed, heading, StringComparison.OrdinalIgnoreCase))
                    {
                        return heading;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The Evidence of a Task file's text, read as <see cref="TryParse"/> reads the body (the timeline's <c>evidence</c> entries, or
    /// an older file's Evidence sections). Reads no frontmatter, so it does not validate the Task.
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    /// <returns>The Evidence lines in file order; empty when the text has no frontmatter block.</returns>
    internal static IReadOnlyList<EvidenceLine> ReadEvidence(string fileText)
    {
        ArgumentNullException.ThrowIfNull(fileText);

        if (!HasFrontmatterBlock(fileText))
        {
            return [];
        }

        (_, string body) = PersonaFrontmatter.Parse(fileText);
        return [.. ParseBody(body).Comments.Where(static entry => entry.Kind == TimelineEntryKind.Evidence).Select(EvidenceLine.FromEntry)];
    }

    /// <summary>Formats one timeline entry as its file line (Spec §7.7.1).</summary>
    /// <param name="entry">The entry to format.</param>
    public static string FormatTimelineEntry(TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string at = entry.At.ToUniversalTime().ToString(TimelineTimeFormat, CultureInfo.InvariantCulture);
        string author = EscapeEntryField(entry.Author);
        (string kind, string text) = entry.Kind switch
        {
            TimelineEntryKind.Comment => ("comment", EscapeCommentText(entry.Text)),
            TimelineEntryKind.Evidence => ("evidence", entry.Text.ReplaceLineEndings(" ")),
            _ => ("event", EscapeEntryField(entry.Text)),
        };
        return string.Create(CultureInfo.InvariantCulture, $"- {at} | {author} | {kind} | {text}");
    }

    /// <summary>
    /// Appends a timeline entry to <paramref name="fileText"/>, converting an older file first (<see cref="Upgrade"/>) and adding the
    /// <see cref="TimelineHeading"/> when the file has none. Existing lines are never rewritten.
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    /// <param name="entry">The entry to append.</param>
    public static string AppendTimelineEntry(string fileText, TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        ArgumentNullException.ThrowIfNull(entry);

        string upgraded = Upgrade(fileText);
        string formatted = FormatTimelineEntry(entry);
        string trimmed = upgraded.TrimEnd('\n');
        return FindHeadingLineIndex(upgraded.Split('\n'), TimelineHeading) < 0
            ? trimmed + "\n\n" + TimelineHeading + "\n" + formatted + "\n"
            : trimmed + "\n" + formatted + "\n";
    }

    /// <summary>
    /// Converts a file written before the timeline to the timeline format (Spec §7.7.4): the frontmatter kept line for line, the
    /// description as <see cref="TryParse"/> reads it, then <c>## Timeline</c> with every event line copied verbatim (including
    /// lines that never parsed) and the Evidence as <c>evidence</c> entries, merged by time. A file that already has the heading is
    /// returned as it is, with '\n' line endings. Reading the result gives the same description, events and Evidence.
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    internal static string Upgrade(string fileText)
    {
        string normalized = Normalize(fileText);
        if (!HasFrontmatterBlock(normalized))
        {
            return normalized;
        }

        string[] lines = normalized.Split('\n');
        (_, string body) = PersonaFrontmatter.Parse(normalized);
        string[] bodyLines = body.Split('\n');
        if (FindHeadingLineIndex(bodyLines, TimelineHeading) >= 0)
        {
            return normalized;
        }

        LegacyBody legacy = ReadLegacyBody(bodyLines);
        List<string> output = [.. lines[..(FrontmatterFence.FindClosing(lines, 1) + 1)], legacy.Description, "", TimelineHeading, .. legacy.Log.LeadingJunk];
        foreach ((bool isEvent, int index) in TimelineMerge.Order(legacy.Comments, legacy.Log.Events))
        {
            if (isEvent)
            {
                output.Add(EventLineToKeep(legacy.Log.Events[index], legacy.Log.EventLines[index]));
                output.AddRange(legacy.Log.JunkAfter[index]);
            }
            else
            {
                output.Add(FormatTimelineEntry(legacy.Comments[index]));
            }
        }

        output.Add("");
        return string.Join('\n', output);
    }

    /// <summary>
    /// The text <see cref="Upgrade"/> writes for an older event line: the line itself, unless the timeline grammar would read it as
    /// something else (a hand-written summary of <c>comment | …</c>), when it is written in the timeline form instead.
    /// </summary>
    private static string EventLineToKeep(ChangeLogEntry entry, string line) =>
        TimelineLineRegex().IsMatch(line) ? FormatEntry(entry) : line;

    /// <summary>Reads a body: by the timeline grammar when it has a <c>## Timeline</c> heading, else as an older file (Spec §7.3 step 3, §7.7.3).</summary>
    private static ParsedBody ParseBody(string body)
    {
        string[] lines = body.Split('\n');
        int timelineIndex = FindHeadingLineIndex(lines, TimelineHeading);
        if (timelineIndex < 0)
        {
            LegacyBody legacy = ReadLegacyBody(lines);
            return new ParsedBody(legacy.Description, legacy.Log.Events, legacy.Comments);
        }

        List<ChangeLogEntry> events = [];
        List<TimelineEntry> comments = [];
        for (int i = timelineIndex + 1; i < lines.Length; i++)
        {
            if (!TryParseTimelineLine(lines[i], out TimelineEntry? entry))
            {
                continue;
            }

            if (entry.Kind == TimelineEntryKind.Event)
            {
                events.Add(new ChangeLogEntry(entry.At, entry.Author, entry.Text));
            }
            else
            {
                comments.Add(entry);
            }
        }

        return new ParsedBody(string.Join('\n', lines[..timelineIndex]).TrimEnd(), events, comments);
    }

    /// <summary>
    /// Reads a body that has no <c>## Timeline</c> heading (Spec §7.7.3): the lines after the last Change log heading are the events;
    /// in the part before it the Evidence sections leave the description and become Evidence entries.
    /// </summary>
    private static LegacyBody ReadLegacyBody(string[] lines)
    {
        int headingIndex = FindHeadingLineIndex(lines, ChangeLogHeading);
        string[] headLines = headingIndex < 0 ? lines : lines[..headingIndex];
        LegacyLog log = ReadLegacyLog(headingIndex < 0 ? [] : lines[(headingIndex + 1)..]);
        (string description, List<EvidenceLine> evidence) = ReadLegacyHead(headLines);
        return new LegacyBody(description, log, DateEvidence(evidence, log.Events));
    }

    /// <summary>Reads an older file's Change log lines: the ones that parse are events; the others are kept, each after the line before it.</summary>
    private static LegacyLog ReadLegacyLog(string[] logLines)
    {
        LegacyLog log = new([], [], [], []);
        foreach (string line in logLines)
        {
            if (TryParseLogLine(line, out ChangeLogEntry? entry))
            {
                log.Events.Add(entry);
                log.EventLines.Add(line);
                log.JunkAfter.Add([]);
            }
            else if (line.Trim().Length > 0)
            {
                (log.Events.Count == 0 ? log.LeadingJunk : log.JunkAfter[^1]).Add(line);
            }
        }

        return log;
    }

    /// <summary>
    /// One pass over the part of an older file before its Change log, tracking two rules at once so the order is exact: the
    /// fingerprint's rule that removes <c>## Evidence</c> sections from the description, and <see cref="EvidenceLine.ParseSection"/>'s
    /// rule that reads Evidence lines. A removed line the second rule did not read becomes an Evidence note.
    /// </summary>
    private static (string Description, List<EvidenceLine> Evidence) ReadLegacyHead(string[] headLines)
    {
        EvidenceSectionStripper stripper = new();
        List<string> kept = [];
        List<EvidenceLine> evidence = [];
        bool inSection = false;
        foreach (string raw in headLines)
        {
            bool removed = stripper.Removes(raw, out bool isHeading);
            EvidenceLine? parsed = null;
            if (raw.StartsWith('#'))
            {
                inSection = string.Equals(raw.Trim(), EvidenceHeading, StringComparison.Ordinal);
            }
            else if (inSection)
            {
                parsed = EvidenceLine.ParseSectionLine(raw);
            }

            if (!removed)
            {
                kept.Add(raw);
            }

            if (parsed is not null)
            {
                evidence.Add(parsed);
            }
            else if (removed && !isHeading && raw.Trim().Length > 0)
            {
                evidence.Add(new EvidenceLine(default, string.Empty, string.Empty, null, null, null, raw.Trim()));
            }
        }

        return (string.Join('\n', kept).TrimEnd(), evidence);
    }

    /// <summary>
    /// Turns the Evidence of an older file into timeline entries (Spec §7.7.3 step 4). A line with no time takes the time of the
    /// line before it, or the earliest dated time in the file when it is first (the epoch when nothing is dated), so the merge by
    /// time places it where it was written.
    /// </summary>
    private static List<TimelineEntry> DateEvidence(List<EvidenceLine> evidence, List<ChangeLogEntry> events)
    {
        DateTimeOffset previous = EarliestDated(evidence, events);
        List<TimelineEntry> entries = new(evidence.Count);
        foreach (EvidenceLine line in evidence)
        {
            DateTimeOffset at = line.Time == default ? previous : line.Time;
            entries.Add(new TimelineEntry(at, line.Author, TimelineEntryKind.Evidence, line.FormatEntryText()));
            previous = at;
        }

        return entries;
    }

    /// <summary>The earliest time among the dated Evidence lines and the events, or the epoch when there is none.</summary>
    private static DateTimeOffset EarliestDated(List<EvidenceLine> evidence, List<ChangeLogEntry> events)
    {
        DateTimeOffset? earliest = null;
        foreach (DateTimeOffset at in evidence.Where(static line => line.Time != default).Select(static line => line.Time).Concat(events.Select(static entry => entry.At)))
        {
            if (earliest is null || at < earliest)
            {
                earliest = at;
            }
        }

        return earliest ?? DateTimeOffset.UnixEpoch;
    }

    /// <summary>
    /// Reads one line under <c>## Timeline</c>: the timeline grammar, else an older Change log line (an event). Anything else is not
    /// an entry; it stays in the file and is ignored.
    /// </summary>
    private static bool TryParseTimelineLine(string line, [NotNullWhen(true)] out TimelineEntry? entry)
    {
        Match match = TimelineLineRegex().Match(line);
        if (match.Success && TryReadTimelineMatch(match, out entry))
        {
            return true;
        }

        if (TryParseLogLine(line, out ChangeLogEntry? logEntry))
        {
            entry = new TimelineEntry(logEntry.At, logEntry.Actor, TimelineEntryKind.Event, logEntry.Summary);
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>Builds the entry a <see cref="TimelineLineRegex"/> match describes; false when its time does not parse.</summary>
    private static bool TryReadTimelineMatch(Match match, [NotNullWhen(true)] out TimelineEntry? entry)
    {
        TimelineEntryKind kind = match.Groups[3].Value switch
        {
            "comment" => TimelineEntryKind.Comment,
            "evidence" => TimelineEntryKind.Evidence,
            _ => TimelineEntryKind.Event,
        };
        string[] formats = kind == TimelineEntryKind.Evidence ? [TimelineTimeFormat, EvidenceMinuteTimeFormat] : [TimelineTimeFormat];
        if (!DateTimeOffset.TryParseExact(match.Groups[1].Value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at))
        {
            entry = null;
            return false;
        }

        string text = kind == TimelineEntryKind.Evidence ? match.Groups[4].Value : UnescapeEntryField(match.Groups[4].Value);
        entry = new TimelineEntry(at, UnescapeEntryField(match.Groups[2].Value), kind, text);
        return true;
    }

    /// <summary>The timeline entry line grammar: "- {timestamp} | {author} | {kind} | {text}", the kind matched case-sensitively.</summary>
    [GeneratedRegex(@"\A- (\S+) \| (.*?) \| (comment|evidence|event) \| (.*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimelineLineRegex();

    /// <summary>Escapes a comment for writing: '\' becomes '\\', '|' becomes '\|', and each line break becomes the two characters '\n'.</summary>
    private static string EscapeCommentText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    /// <summary>The text with every line ending turned into '\n'.</summary>
    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>True when the trimmed line opens or closes a fenced code block.</summary>
    private static bool IsFenceLine(string trimmed) =>
        trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal);

    /// <summary>The index of the last line that trims (case-insensitively) to <paramref name="heading"/> outside a fenced code block, or -1.</summary>
    private static int FindHeadingLineIndex(string[] lines, string heading)
    {
        bool inFence = false;
        int found = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (IsFenceLine(trimmed))
            {
                inFence = !inFence;
            }
            else if (!inFence && string.Equals(trimmed, heading, StringComparison.OrdinalIgnoreCase))
            {
                found = i;
            }
        }

        return found;
    }

    /// <summary>
    /// Removes the <c>## Evidence</c> sections from <paramref name="text"/> and trims the end of what remains. The rule the content
    /// fingerprint has always used: fence-aware, the heading matched case-insensitively, a section running to the next <c>## </c> line.
    /// </summary>
    /// <param name="text">The text, such as an older file's description.</param>
    internal static string WithoutEvidenceSections(string text)
    {
        EvidenceSectionStripper stripper = new();
        return string.Join('\n', Normalize(text).Split('\n').Where(line => !stripper.Removes(line, out _))).TrimEnd();
    }

    /// <summary>The state of the rule that removes <c>## Evidence</c> sections, fed one line at a time.</summary>
    private sealed class EvidenceSectionStripper
    {
        private const string SectionPrefix = "## ";
        private bool inFence;
        private bool inEvidence;

        /// <summary>Whether <paramref name="line"/> is inside an Evidence section or is its heading, and so leaves the description.</summary>
        /// <param name="line">The next line.</param>
        /// <param name="isHeading">True when the line is the <c>## Evidence</c> heading that starts a section.</param>
        public bool Removes(string line, out bool isHeading)
        {
            isHeading = false;
            if (this.inEvidence)
            {
                if (!line.StartsWith(SectionPrefix, StringComparison.Ordinal))
                {
                    return true;
                }

                this.inEvidence = false;
            }

            string trimmed = line.Trim();
            if (IsFenceLine(trimmed))
            {
                this.inFence = !this.inFence;
            }
            else if (!this.inFence && string.Equals(trimmed, EvidenceHeading, StringComparison.OrdinalIgnoreCase))
            {
                this.inEvidence = true;
                isHeading = true;
                return true;
            }

            return false;
        }
    }
}
