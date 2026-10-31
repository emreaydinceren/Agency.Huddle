using System.Collections.Concurrent;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// Warns about a memory file with malformed frontmatter once per file and content (Instructions and Memory §4.3), and about a
/// <c>systemPrompt.teamMemory</c> override in the old frame once per override text, so a problem is reported when it appears and
/// not at every session start. An injected singleton that owns its own memory of what it has said;
/// the memory is bounded, and is cleared when it fills, so a long run cannot grow it without limit.
/// </summary>
/// <param name="logger">Receives each Warning.</param>
internal sealed class MemoryWarnings(ILogger<MemoryWarnings> logger)
{
    /// <summary>The most file-and-content pairs remembered before the memory is cleared and may warn again.</summary>
    internal const int MaxRemembered = 1024;

    private readonly ConcurrentDictionary<string, byte> warned = new(StringComparer.Ordinal);

    /// <summary>Logs the Warning unless it was already logged for this file and content.</summary>
    /// <param name="filePath">The memory file.</param>
    /// <param name="content">The part of the file that was read; its hash tells one content from another.</param>
    /// <param name="problem">What is wrong with it.</param>
    public void Malformed(string filePath, string content, string problem)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(content);

        if (!this.FirstTime($"{filePath}|{MemoryVersion.Of(System.Text.Encoding.UTF8.GetBytes(content))}"))
        {
            return;
        }

        logger.LogWarning(
            "The memory file '{Path}' has malformed frontmatter ({Problem}); its first line is used as the summary instead.",
            filePath,
            problem);
    }

    /// <summary>Logs the Warning that a <c>systemPrompt.teamMemory</c> override still uses the old frame, unless it was already logged for this override text.</summary>
    /// <param name="frame">The override's text; its hash tells one override from another, so an edited override warns again.</param>
    /// <param name="oldPlaceholder">The placeholder the override still uses.</param>
    /// <param name="newPlaceholder">The placeholder that replaces it.</param>
    public void OldTeamMemoryFrame(string frame, string oldPlaceholder, string newPlaceholder)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!this.FirstTime($"team-memory-frame|{MemoryVersion.Of(System.Text.Encoding.UTF8.GetBytes(frame))}"))
        {
            return;
        }

        // The placeholder names are arguments, not part of the template: a template's doubled braces are an escape that a structured provider reads as one brace.
        logger.LogWarning(
            "The 'systemPrompt.teamMemory' override still uses {OldPlaceholder}; it is rendered the old way. Use {NewPlaceholder} to say who writes Team Memory.",
            oldPlaceholder,
            newPlaceholder);
    }

    /// <summary>Whether the key has not been warned about yet; remembers it. Clears the memory first when it is full.</summary>
    /// <param name="key">What was warned about.</param>
    private bool FirstTime(string key)
    {
        if (this.warned.Count >= MaxRemembered)
        {
            this.warned.Clear();
        }

        return this.warned.TryAdd(key, 0);
    }
}
