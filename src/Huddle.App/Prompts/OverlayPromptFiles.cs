namespace Agency.Huddle.App.Prompts;

/// <summary>
/// Reads the Prompts shipped as Markdown files under <c>Overlays/second-brain/prompts/</c> (Spec §7.3,
/// D-40): a frontmatter block describing the <see cref="PromptDefinition"/>, then the default text.
/// </summary>
internal static class OverlayPromptFiles
{
    private const string ResourcePrefix = "Overlays/second-brain/prompts/";
    private const string Fence = "---";
    private const string ReliedText = "Agents rely on this wording; an edit can stop them behaving as intended.";

    /// <summary>Parses every embedded overlay prompt file.</summary>
    /// <returns>One definition per file, in file-name order.</returns>
    /// <exception cref="InvalidOperationException">A file is malformed.</exception>
    internal static IReadOnlyList<PromptDefinition> Load()
    {
        Dictionary<string, string> files = EmbeddedResources.ReadFolder(
            typeof(OverlayPromptFiles).Assembly,
            ResourcePrefix,
            static remainder => remainder.EndsWith(".md", StringComparison.Ordinal) && !remainder.Contains('/'));

        return [.. files
            .OrderBy(static file => file.Key, StringComparer.Ordinal)
            .Select(static file => Parse(file.Key, file.Value))];
    }

    /// <summary>Parses one overlay prompt file.</summary>
    /// <param name="fileName">The file's name, used in error messages.</param>
    /// <param name="text">The file's text, with any line endings.</param>
    /// <returns>The definition; <see cref="PromptDefinition.Default"/> is the body, LF-normalised and trimmed.</returns>
    /// <exception cref="InvalidOperationException">The file has no frontmatter, or lacks or misreads a key.</exception>
    internal static PromptDefinition Parse(string fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(text);
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || !string.Equals(lines[0].TrimEnd(), Fence, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Overlay prompt '{fileName}' must start with a '---' frontmatter block.");
        }

        int close = Array.FindIndex(lines, 1, static line => string.Equals(line.TrimEnd(), Fence, StringComparison.Ordinal));
        if (close < 0)
        {
            throw new InvalidOperationException($"Overlay prompt '{fileName}' has no closing '---' after its frontmatter.");
        }

        Dictionary<string, string> values = ReadKeys(fileName, lines[1..close]);
        string body = string.Join('\n', lines[(close + 1)..]).Trim('\n');
        return new PromptDefinition(
            Required(fileName, values, "key"),
            Required(fileName, values, "label"),
            Required(fileName, values, "helperText"),
            body,
            ReadList(values, "placeholders"),
            ReadList(values, "required"),
            ReadTiming(fileName, Required(fileName, values, "timing")),
            values.TryGetValue("relied", out string? relied) && string.Equals(relied, "true", StringComparison.OrdinalIgnoreCase)
                ? ReliedText
                : null);
    }

    private static Dictionary<string, string> ReadKeys(string fileName, string[] lines)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                throw new InvalidOperationException($"Overlay prompt '{fileName}' has a frontmatter line without a key: '{line}'.");
            }

            values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return values;
    }

    private static string Required(string fileName, Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"Overlay prompt '{fileName}' is missing '{key}' in its frontmatter.");

    private static string[] ReadList(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? raw))
        {
            return [];
        }

        return [.. raw.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => item.Trim('\'', '"'))
            .Where(static item => item.Length > 0)];
    }

    private static PromptTiming ReadTiming(string fileName, string raw) =>
        Enum.TryParse(raw, ignoreCase: false, out PromptTiming timing) && Enum.IsDefined(timing)
            ? timing
            : throw new InvalidOperationException($"Overlay prompt '{fileName}' has timing '{raw}'; expected NextSession or Live.");
}
