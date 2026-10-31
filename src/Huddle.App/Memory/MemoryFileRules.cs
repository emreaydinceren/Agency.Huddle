using System.Globalization;
using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The mechanical rules for a memory file's name and description, and how a written file is composed (Instructions spec
/// §4.1, §4.4, §5.2, §5.3). The Prompt teaches the rest of what makes a description good.
/// </summary>
internal static class MemoryFileRules
{
    /// <summary>The fewest characters a description may have.</summary>
    internal const int MinDescriptionLength = 20;

    /// <summary>The most characters a description may have.</summary>
    internal const int MaxDescriptionLength = 200;

    /// <summary>
    /// The hand-kept index name some Adapters use. Huddle builds the index from each file's description (ADR-0071, D-6),
    /// so this name is reserved: the tool refuses it and the index skips it, leaving any such file on disk untouched.
    /// </summary>
    internal const string IndexFileName = "MEMORY.md";

    /// <summary>The extension every memory file has.</summary>
    private const string Extension = ".md";

    /// <summary>The Windows device names, which are reserved however many extensions follow.</summary>
    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Characters that make a plain YAML scalar mean something else when they start it.</summary>
    private static readonly char[] UnsafeStarts = ['-', '?', ':', ',', '[', ']', '{', '}', '#', '&', '*', '!', '|', '>', '\'', '"', '%', '@', '`'];

    /// <summary>Whether <paramref name="file"/> is one acceptable memory file name: a bare name ending in <c>.md</c>.</summary>
    /// <param name="file">The name as the Agent wrote it.</param>
    /// <returns><see langword="true"/> when the name may be used inside a memory folder.</returns>
    public static bool IsValidFileName(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || LibraryNames.Validate(file) is not null)
        {
            return false;
        }

        string stem = file[..^Extension.Length];
        return stem.Length > 0
            && !stem.EndsWith(' ')
            && !stem.EndsWith('.')
            && !stem.StartsWith('_')
            && !DeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(file, TeamPaths.InstructionsFileName, StringComparison.OrdinalIgnoreCase)
            && !IsIndexFileName(file)
            && !TeamNames.IsReservedProjectName(stem);
    }

    /// <summary>Whether <paramref name="file"/> is <see cref="IndexFileName"/>, ignoring case.</summary>
    /// <param name="file">A file name, without a folder.</param>
    /// <returns><see langword="true"/> when it is the reserved index name, which is never written and never listed.</returns>
    public static bool IsIndexFileName(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return string.Equals(file, IndexFileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks a description against the mechanical rules of §4.4.</summary>
    /// <param name="description">The description as the Agent wrote it, or <see langword="null"/> for none.</param>
    /// <param name="file">The file it describes, so a description that only restates the name can be refused.</param>
    /// <returns>What is wrong with it, or <see langword="null"/> when it is acceptable.</returns>
    public static string? DescriptionProblem(string? description, string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(description))
        {
            return "it is missing.";
        }

        string trimmed = description.Trim();
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return "it spans several lines.";
        }

        if (string.Equals(Squash(trimmed), Squash(file), StringComparison.OrdinalIgnoreCase))
        {
            return "it only repeats the file name.";
        }

        if (trimmed.Length < MinDescriptionLength)
        {
            return string.Create(CultureInfo.InvariantCulture, $"it is shorter than {MinDescriptionLength} characters.");
        }

        if (trimmed.Length > MaxDescriptionLength)
        {
            return string.Create(CultureInfo.InvariantCulture, $"it is longer than {MaxDescriptionLength} characters.");
        }

        return null;
    }

    /// <summary>Composes a written file: the frontmatter with the description, a blank line, then the body, ending in a line break.</summary>
    /// <param name="description">The accepted, trimmed description.</param>
    /// <param name="text">The body, without frontmatter.</param>
    /// <returns>The file's whole text.</returns>
    public static string Compose(string description, string text)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(text);

        string body = text.EndsWith('\n') ? text : text + "\n";
        return $"---\ndescription: {ToYamlScalar(description)}\n---\n\n{body}";
    }

    /// <summary>
    /// Writes <paramref name="value"/> as a YAML scalar: plain when that reads back as the same text, single-quoted
    /// (a quote doubled) when a plain scalar would not, such as one containing a colon followed by a space.
    /// </summary>
    /// <param name="value">The one-line text.</param>
    /// <returns>The scalar as it goes after <c>description: </c>.</returns>
    public static string ToYamlScalar(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        bool needsQuotes = value.Length == 0
            || UnsafeStarts.Contains(value[0])
            || value.Contains(": ", StringComparison.Ordinal)
            || value.EndsWith(':')
            || value.Contains(" #", StringComparison.Ordinal);

        return needsQuotes ? "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'" : value;
    }

    /// <summary>Lower-cases and drops the extension, dashes and spaces, so a name and its restatement compare equal.</summary>
    /// <param name="value">A name or a description.</param>
    private static string Squash(string value)
    {
        string withoutExtension = value.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? value[..^Extension.Length] : value;
        StringBuilder builder = new(withoutExtension.Length);
        foreach (char c in withoutExtension)
        {
            if (c is not ('-' or ' '))
            {
                _ = builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
