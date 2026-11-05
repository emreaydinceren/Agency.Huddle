using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// The text an Agent receives from one <c>AGENTS.md</c> file, normalised for change detection (Spec §6.1), with the
/// upper-case hex SHA-256 of that text. Frontmatter, a byte-order mark, line endings and trailing whitespace are not part of it.
/// </summary>
/// <param name="Text">The normalised body; empty when the file has no text.</param>
/// <param name="Hash">The upper-case hex SHA-256 of <paramref name="Text"/>; empty when absent.</param>
internal sealed record AgentsMdBody(string Text, string Hash)
{
    /// <summary>The body of a file with no text: empty text and an empty hash.</summary>
    public static AgentsMdBody Absent => new(string.Empty, string.Empty);

    /// <summary>Normalises raw file text and hashes what remains.</summary>
    /// <param name="rawText">The file's whole text, as read from disk.</param>
    /// <returns>The body and its hash, or <see cref="Absent"/> when nothing but whitespace remains.</returns>
    public static AgentsMdBody Parse(string rawText)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        string text = rawText.StartsWith('\uFEFF') ? rawText[1..] : rawText;
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        (string body, _) = AgentsMdFrontmatter.Strip(text);
        string trimmed = body.TrimEnd();
        if (trimmed.Length == 0)
        {
            return Absent;
        }

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)));
        return new AgentsMdBody(trimmed, hash);
    }
}
