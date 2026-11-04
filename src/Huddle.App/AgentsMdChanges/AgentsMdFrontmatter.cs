namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>Strips the frontmatter block from an <c>AGENTS.md</c> file's text, shared by the reader and <see cref="AgentsMdBody"/> (Spec §6.1).</summary>
internal static class AgentsMdFrontmatter
{
    /// <summary>Strips a leading frontmatter block: the first line is exactly <c>---</c> and a later line is too.</summary>
    /// <param name="text">The file's whole text.</param>
    /// <returns>The body after the closing line without leading blank lines, and whether a block was stripped.</returns>
    internal static (string Body, bool Had) Strip(string text)
    {
        string[] lines = text.Split('\n');
        if (lines[0].TrimEnd('\r') != "---")
        {
            return (text, false);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == "---")
            {
                string body = string.Join('\n', lines[(i + 1)..]);
                return (body.TrimStart('\r', '\n'), true);
            }
        }

        return (text, false);
    }
}
