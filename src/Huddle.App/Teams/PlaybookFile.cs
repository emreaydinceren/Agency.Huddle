using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed <c>Teams/&lt;Team&gt;/_member-playbook.md</c> or <c>_manager-playbook.md</c> file (Team Playbooks spec §5.1): an optional
/// frontmatter block with a <c>basedOn</c> key, then the body an Agent reads. The frontmatter never reaches a model.
/// </summary>
/// <param name="BasedOn">The whole <c>basedOn</c> value, for example <c>sha256:3f9a0c1e7b2d4a65</c>; <see langword="null"/> for an Unknown base.</param>
/// <param name="Body">The text after the frontmatter, LF-only, kept verbatim; the whole text when the file has no frontmatter.</param>
internal sealed record PlaybookFile(string? BasedOn, string Body)
{
    private const string Delimiter = "---";
    private const string BasedOnKey = "basedOn";

    /// <summary>
    /// Frontmatter lines whose key this type does not know, in file order and verbatim, written back by <see cref="Compose"/> so a hand
    /// edit survives a rewrite.
    /// </summary>
    public IReadOnlyList<string> UnknownLines { get; init; } = [];

    /// <summary>The file names a Team's Playbooks are kept under, one per kind; the leading underscore keeps the catalog from reading them as Projects.</summary>
    /// <param name="manager"><see langword="true"/> for the Manager Playbook, <see langword="false"/> for the Member one.</param>
    /// <returns>The file name.</returns>
    public static string FileName(bool manager) => manager ? "_manager-playbook.md" : "_member-playbook.md";

    /// <summary>
    /// Parses the text of a Playbook file. It never fails: a file with no frontmatter, or one that never closes, is all body with no
    /// base, and a block without <c>basedOn</c> is an Unknown base too.
    /// </summary>
    /// <param name="text">The file text, with LF or CRLF line endings.</param>
    /// <returns>The parsed file.</returns>
    public static PlaybookFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        int closing = FrontmatterFence.IsOpening(lines[0]) ? FrontmatterFence.FindClosing(lines, 1) : -1;
        if (closing < 0)
        {
            return new PlaybookFile(null, normalized);
        }

        string? basedOn = null;
        List<string> unknown = [];
        for (int index = 1; index < closing; index++)
        {
            string line = lines[index].TrimEnd();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            string key = colon > 0 && !char.IsWhiteSpace(line[0]) ? line[..colon].Trim() : string.Empty;
            if (string.Equals(key, BasedOnKey, StringComparison.OrdinalIgnoreCase))
            {
                string value = Unquote(line[(colon + 1)..]);
                basedOn = value.Length > 0 ? value : null;
            }
            else if (line.Length > 0)
            {
                unknown.Add(line);
            }
        }

        return new PlaybookFile(basedOn, string.Join('\n', lines[(closing + 1)..]))
        {
            UnknownLines = unknown,
        };
    }

    /// <summary>
    /// Composes the file text: the frontmatter when there is a base or an unknown line, then <see cref="Body"/>. LF line endings only. A
    /// file with neither is just its body, as a hand-made file is.
    /// </summary>
    /// <returns>The text to write to the Playbook file.</returns>
    public string Compose()
    {
        string body = this.Body.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (this.BasedOn is null && this.UnknownLines.Count == 0)
        {
            return body;
        }

        StringBuilder builder = new();
        builder.Append(Delimiter).Append('\n');
        if (this.BasedOn is not null)
        {
            builder.Append(BasedOnKey).Append(": '").Append(this.BasedOn.Replace("'", "''", StringComparison.Ordinal)).Append("'\n");
        }

        foreach (string line in this.UnknownLines)
        {
            builder.Append(line).Append('\n');
        }

        builder.Append(Delimiter).Append('\n').Append(body);
        return builder.ToString();
    }

    private static string Unquote(string raw)
    {
        string value = raw.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
