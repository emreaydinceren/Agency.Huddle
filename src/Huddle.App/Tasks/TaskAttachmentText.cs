using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Builds the description line that attaches a Library file to a Task (TaskAttachments spec 6.4): the file's
/// absolute path in a code span, the form a chat upload sends and the <c>@</c> picker inserts.
/// </summary>
internal static class TaskAttachmentText
{
    /// <summary>
    /// Appends <paramref name="absolutePath"/> to <paramref name="description"/> as its own code-span line. A run of
    /// attachments forms one block: the first is separated from prose by a blank line, the next ones follow directly.
    /// A path already on a line of its own is not added again.
    /// </summary>
    /// <param name="description">The description as it stands, possibly empty.</param>
    /// <param name="absolutePath">The file's absolute path.</param>
    /// <returns>The description with the path line added, or unchanged when the path is already there.</returns>
    public static string AppendPathLine(string description, string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        string line = $"`{absolutePath}`";
        string body = description.TrimEnd('\r', '\n');
        if (body.Length == 0)
        {
            return line;
        }

        string[] lines = body.Split('\n');
        if (lines.Any(existing => string.Equals(existing.Trim(), line, StringComparison.Ordinal)))
        {
            return description;
        }

        string separator = TaskAttachmentText.IsPathLine(lines[^1]) ? "\n" : "\n\n";
        return body + separator + line;
    }

    /// <summary>Whether <paramref name="line"/> is exactly one code span holding a Library path.</summary>
    private static bool IsPathLine(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length > 2
            && trimmed[0] == '`'
            && trimmed[^1] == '`'
            && LibraryPathPatterns.TryMatchWhole(trimmed[1..^1], out _);
    }
}
