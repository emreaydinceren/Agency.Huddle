using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The one implementation of the <c>reported.md</c> line rule (Memory Tidy spec §6.1 and §6.3): the file name, the line
/// <c>- yyyy-MM-dd · text</c>, the age rule, the drop of old lines and the making of room at the size cap. The memory tool and
/// the weekly pass both call it. Every method works on the file's whole text, keeps the frontmatter block byte for byte and
/// keeps the file's own line ending; none does IO.
/// </summary>
internal static partial class ReportedFile
{
    /// <summary>The file's name, in an Agent's own Memory only. Compared ignoring case.</summary>
    internal const string FileName = "reported.md";

    private const string Separator = " · ";

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The outcome of <see cref="BuildLine"/>.</summary>
    internal enum LineProblem
    {
        /// <summary>The text made one line.</summary>
        None,

        /// <summary>The text holds more than one line.</summary>
        SeveralLines,

        /// <summary>Nothing is left once the leading marker and date are removed.</summary>
        Empty,
    }

    /// <summary>Whether <paramref name="file"/> is the reported file's name, ignoring case.</summary>
    /// <param name="file">A file name, without a folder.</param>
    /// <returns><see langword="true"/> for <c>reported.md</c> in any case.</returns>
    internal static bool IsName(string file) => string.Equals(file, FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the date of a line written as <c>- yyyy-MM-dd · …</c>; a trailing <c>\r</c> is ignored.</summary>
    /// <param name="line">One line, without its line break.</param>
    /// <param name="date">The date the line starts with.</param>
    /// <returns><see langword="true"/> when the line has the shape; any other line is unreadable.</returns>
    internal static bool TryReadDate(string line, out DateOnly date)
    {
        date = default;
        string prefix = "- ";
        if (line.Length < prefix.Length + DateFormat.Length + Separator.Length
            || !line.StartsWith(prefix, StringComparison.Ordinal)
            || !string.Equals(line.Substring(prefix.Length + DateFormat.Length, Separator.Length), Separator, StringComparison.Ordinal))
        {
            return false;
        }

        return DateOnly.TryParseExact(line.AsSpan(prefix.Length, DateFormat.Length), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Whether a line dated <paramref name="date"/> is older than the rule allows. A future date never is.</summary>
    /// <param name="date">The line's date.</param>
    /// <param name="today">The Human's local date.</param>
    /// <param name="keepDays">How many days a line is kept; zero or less keeps every line.</param>
    internal static bool IsOld(DateOnly date, DateOnly today, int keepDays) =>
        keepDays > 0 && today.DayNumber - date.DayNumber > keepDays;

    /// <summary>Makes one line of the Agent's text: the date is always <paramref name="today"/>, whatever the text begins with.</summary>
    /// <param name="text">What the Agent sent.</param>
    /// <param name="today">The Human's local date.</param>
    /// <param name="line">The line, without a line break, when the result is <see cref="LineProblem.None"/>.</param>
    internal static LineProblem BuildLine(string? text, DateOnly today, out string line)
    {
        line = string.Empty;
        string trimmed = (text ?? string.Empty).TrimEnd('\r', '\n');
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return LineProblem.SeveralLines;
        }

        string rest = LeadingMarker().Replace(trimmed.TrimStart(), string.Empty, 1);
        rest = LeadingDate().Replace(rest, string.Empty, 1).Trim();
        if (rest.Length == 0)
        {
            return LineProblem.Empty;
        }

        line = string.Concat("- ", today.ToString(DateFormat, CultureInfo.InvariantCulture), Separator, rest);
        return LineProblem.None;
    }

    /// <summary>Drops every line older than the rule allows and nothing else.</summary>
    /// <param name="text">The file's whole text.</param>
    /// <param name="today">The Human's local date.</param>
    /// <param name="keepDays">How many days a line is kept; zero or less drops nothing.</param>
    /// <returns>The new text and the number of lines dropped; <c>Dropped</c> is 0 and the text is the same when there is nothing to drop.</returns>
    internal static (string Text, int Dropped) PruneByAge(string text, DateOnly today, int keepDays)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (keepDays <= 0)
        {
            return (text, 0);
        }

        int bodyStart = BodyStart(text);
        List<Entry> entries = Split(text, bodyStart);
        List<Entry> kept = [.. entries.Where(entry => !(entry.Date is { } date && IsOld(date, today, keepDays)))];
        int dropped = entries.Count - kept.Count;
        return dropped == 0 ? (text, 0) : (Join(text[..bodyStart], kept), dropped);
    }

    /// <summary>
    /// Adds <paramref name="line"/> at the end: first the lines older than the rule allows are dropped, then, when the file would
    /// pass <paramref name="maxBytes"/>, the oldest dated lines until it fits. A line that is not dated is never dropped.
    /// </summary>
    /// <param name="text">The file's whole text.</param>
    /// <param name="line">The new line, without a line break (from <see cref="BuildLine"/>).</param>
    /// <param name="today">The Human's local date.</param>
    /// <param name="keepDays">How many days a line is kept; zero or less drops nothing by age.</param>
    /// <param name="maxBytes">The size cap in bytes; zero or less means none.</param>
    internal static ReportedAppend Append(string text, string line, DateOnly today, int keepDays, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(line);

        (string pruned, int aged) = PruneByAge(text, today, keepDays);
        int bodyStart = BodyStart(pruned);
        string eol = pruned.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<Entry> entries = Split(pruned, bodyStart);
        if (entries.Count > 0 && entries[^1].Terminator.Length == 0)
        {
            entries[^1] = entries[^1] with { Terminator = eol };
        }

        entries.Add(new Entry(line, eol, null));

        string head = pruned[..bodyStart];
        int forRoom = 0;
        int size = Utf8.GetByteCount(head) + entries.Sum(static entry => entry.Bytes);
        if (maxBytes > 0 && size > maxBytes)
        {
            foreach (Entry oldest in entries.Take(entries.Count - 1).Where(static entry => entry.Date is not null).OrderBy(static entry => entry.Date).ToList())
            {
                _ = entries.Remove(oldest);
                size -= oldest.Bytes;
                forRoom++;
                if (size <= maxBytes)
                {
                    break;
                }
            }
        }

        bool fits = maxBytes <= 0 || size <= maxBytes;
        return new ReportedAppend(fits ? Join(head, entries) : null, aged, forRoom, size);
    }

    /// <summary>Where the body starts: after the frontmatter block, or 0 when the file has none.</summary>
    /// <param name="text">The file's text.</param>
    private static int BodyStart(string text) => NoteFrontmatter.Locate(text).BodyStart;

    /// <summary>Cuts the body into lines, each keeping its own line break (the last may have none; a missing one is supplied on the way out).</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="bodyStart">Where the body starts.</param>
    private static List<Entry> Split(string text, int bodyStart)
    {
        List<Entry> entries = [];
        int cursor = bodyStart;
        while (cursor < text.Length)
        {
            int feed = text.IndexOf('\n', cursor);
            int end = feed < 0 ? text.Length : feed + 1;
            string raw = text[cursor..end];
            string terminator = string.Empty;
            if (raw.EndsWith('\n'))
            {
                terminator = raw.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            }

            string content = raw[..^terminator.Length];
            entries.Add(new Entry(content, terminator, TryReadDate(content, out DateOnly date) ? date : null));
            cursor = end;
        }

        return entries;
    }

    /// <summary>Puts the frontmatter and the lines back, giving the last line a break when it had none.</summary>
    /// <param name="head">The frontmatter block, byte for byte.</param>
    /// <param name="entries">The lines, in order.</param>
    private static string Join(string head, List<Entry> entries)
    {
        StringBuilder builder = new(head);
        string eol = head.Contains("\r\n", StringComparison.Ordinal) || entries.Any(static entry => entry.Terminator == "\r\n") ? "\r\n" : "\n";
        for (int i = 0; i < entries.Count; i++)
        {
            string terminator = entries[i].Terminator.Length == 0 && i < entries.Count - 1 ? eol : entries[i].Terminator;
            _ = builder.Append(entries[i].Content).Append(terminator);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^[-*]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingMarker();

    [GeneratedRegex(@"^\d{4}[-/]\d{2}[-/]\d{2}(?:\s*[·\-–—:|,]\s*|\s+|$)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingDate();

    /// <summary>One line of the body with its break and, when it is a dated line, its date.</summary>
    /// <param name="Content">The line without its break.</param>
    /// <param name="Terminator">The line's break: empty only for a last line that had none.</param>
    /// <param name="Date">The date, or <see langword="null"/> for a line Huddle cannot read.</param>
    private sealed record Entry(string Content, string Terminator, DateOnly? Date)
    {
        /// <summary>The bytes the line takes in the file.</summary>
        public int Bytes => Utf8.GetByteCount(this.Content) + Utf8.GetByteCount(this.Terminator);
    }
}
