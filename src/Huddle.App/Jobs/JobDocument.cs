using System.Text;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Reads and writes an owner's <c>schedule.md</c>: a title line, then one <c>##</c> section per Job holding its fields as
/// <c>- key: value</c> lines, a blank line, and the instruction. Pure, so it is tested without a disk. A section that
/// cannot be read is reported with its reason and kept as written; the others still load.
/// </summary>
/// <example>
/// <code>
/// ## Morning digest
/// - id: job-7f3a2c
/// - when: weekdays at 09:00
/// - timezone: Europe/Istanbul
/// - by: Chief of Staff
/// - paused: no
///
/// Summarise yesterday's Room activity.
/// </code>
/// </example>
internal static class JobDocument
{
    /// <summary>The first line Huddle writes, and the line a reader skips.</summary>
    public const string Heading = "# Schedule";

    private const string IdKey = "id";
    private const string WhenKey = "when";
    private const string TimeZoneKey = "timezone";
    private const string ByKey = "by";
    private const string PausedKey = "paused";

    private static readonly string[] Keys = [IdKey, WhenKey, TimeZoneKey, ByKey, PausedKey];

    private static readonly string KeyList = string.Join(", ", Keys);

    /// <summary>Reads every section of a file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="owner">Whose file it is.</param>
    /// <returns>The sections in file order.</returns>
    public static IReadOnlyList<JobSection> Parse(string text, JobOwner owner)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(owner);

        List<JobSection> sections = [];
        List<string>? current = null;
        foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    sections.Add(ParseSection(current, owner));
                }

                current = [line];
            }
            else
            {
                current?.Add(line);
            }
        }

        if (current is not null)
        {
            sections.Add(ParseSection(current, owner));
        }

        return sections;
    }

    /// <summary>Writes a whole file: the heading, then each section, a read Job in Huddle's own layout and any other as written.</summary>
    /// <param name="sections">The sections in file order.</param>
    /// <returns>The file's text, with <c>\n</c> line endings.</returns>
    public static string Write(IReadOnlyList<JobSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        StringBuilder builder = new();
        _ = builder.Append(Heading).Append('\n');
        foreach (JobSection section in sections)
        {
            _ = builder.Append('\n').Append(section.Job is { } job ? Format(job) : section.Raw.TrimEnd()).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>One Job as its section.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The section's text, without a trailing newline.</returns>
    public static string Format(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        StringBuilder builder = new();
        _ = builder.Append("## ").Append(OneLine(job.Title)).Append('\n');
        _ = builder.Append("- ").Append(IdKey).Append(": ").Append(job.Id).Append('\n');
        _ = builder.Append("- ").Append(WhenKey).Append(": ").Append(job.When).Append('\n');
        if (job.TimeZoneId is { } zone)
        {
            _ = builder.Append("- ").Append(TimeZoneKey).Append(": ").Append(zone).Append('\n');
        }

        if (job.CreatedBy is { } by)
        {
            _ = builder.Append("- ").Append(ByKey).Append(": ").Append(OneLine(by)).Append('\n');
        }

        _ = builder.Append("- ").Append(PausedKey).Append(": ").Append(job.Paused ? "yes" : "no").Append('\n');
        _ = builder.Append('\n').Append(job.Instruction.ReplaceLineEndings("\n").Trim());
        return builder.ToString();
    }

    private static JobSection ParseSection(List<string> lines, JobOwner owner)
    {
        string raw = string.Join('\n', lines).TrimEnd();
        string title = lines[0][3..].Trim();
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        while (index < lines.Count && lines[index].Trim().Length == 0)
        {
            index++;
        }

        for (; index < lines.Count && lines[index].TrimStart().StartsWith("- ", StringComparison.Ordinal); index++)
        {
            string field = lines[index].TrimStart()[2..];
            int colon = field.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return Invalid(raw, title, $"'{lines[index].Trim()}' is not a 'key: value' line.");
            }

            string key = field[..colon].Trim();
            if (!Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                return Invalid(raw, title, $"'{key}' is not a Job field; use {KeyList}.");
            }

            fields[key] = field[(colon + 1)..].Trim();
        }

        string instruction = string.Join('\n', lines.Skip(index)).Trim();
        return Build(raw, title, fields, instruction, owner);
    }

    private static JobSection Build(string raw, string title, Dictionary<string, string> fields, string instruction, JobOwner owner)
    {
        if (title.Length == 0)
        {
            return Invalid(raw, title, "A Job needs a title after '## '.");
        }

        if (!fields.TryGetValue(IdKey, out string? id) || id.Length == 0)
        {
            return Invalid(raw, title, "A Job needs an 'id' line.");
        }

        if (!JobRecurrence.TryParse(fields.GetValueOrDefault(WhenKey), out JobRecurrence? when, out string? error))
        {
            return Invalid(raw, title, error);
        }

        string? zone = fields.GetValueOrDefault(TimeZoneKey) is { Length: > 0 } named ? named : null;
        if (zone is not null && !JobTimeZones.TryFind(zone, out _))
        {
            return Invalid(raw, title, $"'{zone}' is not a time zone this machine knows.");
        }

        if (instruction.Length == 0)
        {
            return Invalid(raw, title, "A Job needs an instruction below its fields.");
        }

        bool paused = fields.GetValueOrDefault(PausedKey) is { } pausedText
            && (string.Equals(pausedText, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(pausedText, "true", StringComparison.OrdinalIgnoreCase));
        string? by = fields.GetValueOrDefault(ByKey) is { Length: > 0 } author ? author : null;
        return new JobSection(new Job(id, owner, title, instruction, when, zone, by, paused), raw, title, null);
    }

    private static JobSection Invalid(string raw, string title, string problem) => new(null, raw, title, problem);

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
