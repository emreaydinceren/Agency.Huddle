using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The pure core of <c>sprints.md</c> (Sprints spec §6): parse it, build the text after an append, build
/// the text after removing one line, and validate a name. It does no I/O. Nothing here rewrites a line
/// that is not the one being added or removed.
/// </summary>
internal static partial class SprintFileFormat
{
    private const int MaxNameLength = 60;
    private const string Heading = "# Sprints";
    private const string LengthProblem = "A sprint name must be 1-60 characters on one line.";
    private const string ReservedProblem = "A sprint name can't be 'current' or 'none'.";

    /// <summary>Parses the text of <c>sprints.md</c>. Any line that is not a Sprint line is ignored; a line that looks like one and is invalid is a problem.</summary>
    /// <param name="text">The file's text, with LF or CRLF line endings.</param>
    public static SprintFile ParseFile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<Sprint> sprints = [];
        List<SprintProblem> problems = [];
        foreach (ScannedLine line in Scan(text))
        {
            if (line.Sprint is { } sprint)
            {
                sprints.Add(sprint);
            }
            else if (line.Problem is not null)
            {
                problems.Add(new SprintProblem(line.Number, line.Problem));
            }
        }

        sprints.Sort(static (left, right) => left.Id.Number.CompareTo(right.Id.Number));
        return new SprintFile(sprints, problems);
    }

    /// <summary>
    /// The text of the file after appending <c>- S{n} | {name}</c> (Sprints spec §6). Nothing already in
    /// the file changes. A missing file (<see langword="null"/>) becomes a heading, a blank line and the
    /// line; line endings follow the existing text, LF when there is none to follow.
    /// </summary>
    /// <param name="existingText">The current text, or <see langword="null"/> when the file does not exist.</param>
    /// <param name="id">The new Sprint's id.</param>
    /// <param name="name">The new Sprint's name, trimmed before it is written. Validate it first.</param>
    public static string AppendLine(string? existingText, SprintId id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string line = string.Create(CultureInfo.InvariantCulture, $"- {id} | {name.Trim()}");
        if (existingText is null)
        {
            return $"{Heading}\n\n{line}\n";
        }

        string lineEnding = existingText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        StringBuilder builder = new(existingText);
        if (existingText.Length > 0 && existingText[^1] != '\n')
        {
            builder.Append(lineEnding);
        }

        return builder.Append(line).Append(lineEnding).ToString();
    }

    /// <summary>
    /// The text of the file after removing the one line that defines <paramref name="id"/>, with its line
    /// ending (Sprints spec §6). Every other byte stays. When it is the last line and has no line ending,
    /// the line ending before it goes instead. An id that has no line returns the text unchanged.
    /// </summary>
    /// <param name="text">The file's current text.</param>
    /// <param name="id">The Sprint whose line to remove.</param>
    public static string RemoveLine(string text, SprintId id)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (ScannedLine line in Scan(text))
        {
            if (line.Sprint is { } sprint && sprint.Id == id)
            {
                int start = line.Start;
                int end = line.End;
                if (line.EndsWithLineEnding)
                {
                    return text.Remove(start, end - start);
                }

                if (start >= 2 && text[start - 2] == '\r' && text[start - 1] == '\n')
                {
                    start -= 2;
                }
                else if (start >= 1 && text[start - 1] == '\n')
                {
                    start -= 1;
                }

                return text.Remove(start, end - start);
            }
        }

        return text;
    }

    /// <summary>The next Sprint number: one above the higher of the file's highest and the highest any Task references, and at least 1 (Sprints spec §6.2).</summary>
    /// <param name="highestInFile">The highest number among the file's Sprints, or 0 when there are none.</param>
    /// <param name="highestReferenced">The highest number any Task, Active or Closed, holds, or 0 when none does.</param>
    public static int NextNumber(int highestInFile, int highestReferenced) =>
        Math.Max(Math.Max(highestInFile, highestReferenced), 0) + 1;

    /// <summary>Checks a name against Sprints spec §6.1, after trimming it. Returns the problem text, or <see langword="null"/> when the name is valid.</summary>
    /// <param name="name">The name to check.</param>
    public static string? ValidateName(string? name)
    {
        string trimmed = name?.Trim() ?? "";
        if (trimmed.Length is 0 or > MaxNameLength || trimmed.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            return LengthProblem;
        }

        if (string.Equals(trimmed, "current", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            return ReservedProblem;
        }

        if (SprintId.TryParse(trimmed, out SprintId id))
        {
            return $"A sprint name can't look like an id ({id}).";
        }

        return null;
    }

    /// <summary>Walks the text line by line, deciding for each Sprint-shaped line whether it is a Sprint or a problem.</summary>
    private static List<ScannedLine> Scan(string text)
    {
        List<ScannedLine> lines = [];
        HashSet<SprintId> ids = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        int start = 0;
        int number = 0;
        while (start < text.Length)
        {
            number++;
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline + 1;
            int contentEnd = newline < 0 ? text.Length : newline;
            if (contentEnd > start && text[contentEnd - 1] == '\r' && newline >= 0)
            {
                contentEnd--;
            }

            string content = text[start..contentEnd];
            (Sprint? sprint, string? problem) = Classify(content, number, ids, names);
            lines.Add(new ScannedLine(number, start, end, newline >= 0, sprint, problem));
            start = end;
        }

        return lines;
    }

    private static (Sprint? Sprint, string? Problem) Classify(string content, int lineNumber, HashSet<SprintId> ids, HashSet<string> names)
    {
        Match match = SprintLineRegex().Match(content);
        if (!match.Success)
        {
            return (null, null);
        }

        string idText = match.Groups[1].Value;
        if (!SprintId.TryParse(idText, out SprintId id))
        {
            return (null, $"Line {lineNumber}: the id '{idText}' is not valid; ignored.");
        }

        if (ids.Contains(id))
        {
            return (null, $"Line {lineNumber}: duplicate id {id}; the first one is used.");
        }

        string name = match.Groups[2].Value.Trim();
        if (ValidateName(name) is { } reason)
        {
            return (null, $"Line {lineNumber}: the name is not valid ({reason}); ignored.");
        }

        if (!names.Add(name))
        {
            return (null, $"Line {lineNumber}: duplicate name '{name}'; ignored.");
        }

        ids.Add(id);
        return (new Sprint(id, name), null);
    }

    /// <summary>A Sprint line: <c>- S2 | Auth hardening</c>.</summary>
    [GeneratedRegex(@"\A- ([Ss][0-9]{1,9}) \| (.+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex SprintLineRegex();

    /// <summary>One line of the file: where it is, and what it turned out to be.</summary>
    private sealed record ScannedLine(int Number, int Start, int End, bool EndsWithLineEnding, Sprint? Sprint, string? Problem);
}
