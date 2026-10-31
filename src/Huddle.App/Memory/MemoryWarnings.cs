using System.Collections.Concurrent;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// Warns about a memory file with malformed frontmatter once per file and content (Instructions and Memory §4.3), so a bad file
/// is reported when it appears and not at every session start. An injected singleton that owns its own memory of what it has said;
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

        if (this.warned.Count >= MaxRemembered)
        {
            this.warned.Clear();
        }

        if (!this.warned.TryAdd($"{filePath}|{MemoryVersion.Of(System.Text.Encoding.UTF8.GetBytes(content))}", 0))
        {
            return;
        }

        logger.LogWarning(
            "The memory file '{Path}' has malformed frontmatter ({Problem}); its first line is used as the summary instead.",
            filePath,
            problem);
    }
}
