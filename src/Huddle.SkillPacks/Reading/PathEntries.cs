using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>One entry of a path cell: a plain path, or a path for one computer.</summary>
/// <param name="Computer"><see langword="null"/> for a plain entry (every computer); otherwise the computer's name as written.</param>
/// <param name="Path">The text of the path, trimmed, not yet resolved or checked.</param>
public sealed record PathEntry(string? Computer, string Path);

/// <summary>All the entries a path cell or a JSON <c>path</c> holds (§8.2).</summary>
/// <param name="Entries">The entries in the order they were written.</param>
public sealed partial record PathEntries(IReadOnlyList<PathEntry> Entries)
{
    /// <summary>The JSON key that stands for every other computer.</summary>
    public const string AnyComputer = "*";

    /// <summary>No entry at all.</summary>
    public static PathEntries Empty { get; } = new([]);

    /// <summary>True when there is no entry at all.</summary>
    public bool IsEmpty => this.Entries.Count == 0;

    /// <summary>Two entries name the same computer, ignoring case (R010).</summary>
    public bool HasDuplicateComputer =>
        this.Entries.Where(static entry => entry.Computer is not null)
            .GroupBy(static entry => entry.Computer, StringComparer.OrdinalIgnoreCase)
            .Any(static group => group.Count() > 1);

    /// <summary>Two entries apply to every computer (R013).</summary>
    public bool HasTwoPlainEntries => this.Entries.Count(static entry => entry.Computer is null) > 1;

    /// <summary>True when at least one entry names a computer.</summary>
    public bool IsPerComputer => this.Entries.Any(static entry => entry.Computer is not null);

    /// <summary>Reads a SKILLS.md cell: one entry per line; a line <c>NAME: path</c> is an entry for computer <c>NAME</c>.</summary>
    public static PathEntries Parse(string cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        List<PathEntry> entries = [];
        foreach (string raw in cell.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Match match = ComputerLine().Match(line);
            entries.Add(match.Success
                ? new PathEntry(match.Groups["computer"].Value, match.Groups["path"].Value.Trim())
                : new PathEntry(null, line));
        }

        return entries.Count == 0 ? Empty : new PathEntries(entries);
    }

    /// <summary>Reads a JSON <c>path</c> value: a string is one plain entry; an object is one entry per key, with
    /// <c>"*"</c> as the plain entry. Returns <see langword="null"/> for any other JSON type.</summary>
    public static PathEntries? FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return new PathEntries([new PathEntry(null, (element.GetString() ?? string.Empty).Trim())]);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return Empty;
            case JsonValueKind.Object:
                List<PathEntry> entries = [];
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        return null;
                    }

                    string? computer = string.Equals(property.Name, AnyComputer, StringComparison.Ordinal) ? null : property.Name;
                    entries.Add(new PathEntry(computer, (property.Value.GetString() ?? string.Empty).Trim()));
                }

                return new PathEntries(entries);
            default:
                return null;
        }
    }

    /// <summary>Whether a SKILLS.md line looks like a computer entry whose name is one character (R003 message).</summary>
    public static bool IsOneLetterComputerLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return OneLetterComputerLine().IsMatch(line.Trim());
    }

    /// <summary>The entry that applies to <paramref name="computer"/>: its own entry, else the plain entry, else null.</summary>
    public PathEntry? EntryFor(string computer)
    {
        ArgumentNullException.ThrowIfNull(computer);
        return this.Entries.FirstOrDefault(entry => string.Equals(entry.Computer, computer, StringComparison.OrdinalIgnoreCase))
            ?? this.Entries.FirstOrDefault(static entry => entry.Computer is null);
    }

    /// <summary>The path text for <paramref name="computer"/>: its own entry, else the plain/"*" entry, else null.
    /// An entry whose text is empty also gives null (a Skill not fetched yet).</summary>
    public string? Resolve(string computer)
    {
        PathEntry? entry = this.EntryFor(computer);
        return entry is null || entry.Path.Length == 0 ? null : entry.Path;
    }

    /// <summary>Resolve, then make absolute against <paramref name="baseDirectory"/>; accepts / or \.</summary>
    public string? ResolveFull(string computer, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        string? path = this.Resolve(computer);
        return path is null ? null : MakeFull(path, baseDirectory);
    }

    /// <summary>True if this computer has its own entry or a plain entry exists, even if empty (R009 is "false").</summary>
    public bool HasEntryFor(string computer)
    {
        return this.EntryFor(computer) is not null;
    }

    /// <summary>The text of the first entry, for display and the row key.</summary>
    public string FirstText => this.Entries.Count == 0 ? string.Empty : this.Entries[0].Path;

    /// <summary>Makes <paramref name="path"/> absolute against <paramref name="baseDirectory"/>, accepting / or \.</summary>
    public static string MakeFull(string path, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        string normalised = path.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
        try
        {
            return System.IO.Path.GetFullPath(normalised, baseDirectory);
        }
        catch (ArgumentException)
        {
            return System.IO.Path.Combine(baseDirectory, normalised);
        }
        catch (NotSupportedException)
        {
            return System.IO.Path.Combine(baseDirectory, normalised);
        }
    }

    [GeneratedRegex(@"\A(?<computer>[A-Za-z0-9][A-Za-z0-9-]{1,14}): (?<path>.+)\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ComputerLine();

    [GeneratedRegex(@"\A[A-Za-z0-9]: \S", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OneLetterComputerLine();
}
