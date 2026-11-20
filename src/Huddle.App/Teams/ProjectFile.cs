using System.Text;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed <c>Teams/&lt;Team&gt;/&lt;Project&gt;/_project.md</c> file (ADR-0083): a frontmatter block of single-quoted YAML
/// scalars followed by free-form notes. Today it holds only the Project's avatar, in the optional <c>avatarLabel</c> and
/// <c>avatarBackground</c> keys; a Project's avatar has no picture, so an <c>avatarImage</c> line is not read.
/// </summary>
/// <param name="Avatar">The Project's avatar: a label (initials or an emoji) and a background, never an image.</param>
/// <param name="Notes">The text after the frontmatter, LF-only, kept verbatim; the whole text when the file has no frontmatter.</param>
internal sealed record ProjectFile(Avatar Avatar, string Notes)
{
    private const string Delimiter = "---";
    private const string AvatarLabelKey = "avatarLabel";
    private const string AvatarBackgroundKey = "avatarBackground";

    /// <summary>A Project with no file: no avatar and no notes.</summary>
    public static ProjectFile Empty { get; } = new(Avatar.None, string.Empty);

    /// <summary>
    /// Frontmatter lines whose key this type does not know, in file order and verbatim, written back by <see cref="Compose"/> so a hand
    /// edit survives a rewrite.
    /// </summary>
    public IReadOnlyList<string> UnknownLines { get; init; } = [];

    /// <summary>The Project's <c>oneInProgressPerProject</c> value (ADR-0103); <see cref="RuleSetting.Absent"/> inherits the Team's and is never written.</summary>
    public RuleSetting OneInProgressPerProject { get; init; }

    /// <summary>
    /// Parses the text of a <c>_project.md</c> file. It never fails: a file with no frontmatter, or one that never closes, is all notes
    /// and no avatar, so a rewrite puts the avatar in front of the text rather than losing it.
    /// </summary>
    /// <param name="text">The file text, with LF or CRLF line endings.</param>
    /// <returns>The parsed file.</returns>
    public static ProjectFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        int closing = FrontmatterFence.IsOpening(lines[0]) ? FrontmatterFence.FindClosing(lines, 1) : -1;
        if (closing < 0)
        {
            return new ProjectFile(Avatar.None, normalized);
        }

        string? label = null;
        string? background = null;
        RuleSetting rule = RuleSetting.Absent;
        List<string> unknown = [];
        for (int index = 1; index < closing; index++)
        {
            string line = lines[index].TrimEnd();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            string key = colon > 0 && !char.IsWhiteSpace(line[0]) ? line[..colon].Trim() : string.Empty;
            if (string.Equals(key, AvatarLabelKey, StringComparison.OrdinalIgnoreCase))
            {
                label = Unquote(line[(colon + 1)..]);
            }
            else if (string.Equals(key, AvatarBackgroundKey, StringComparison.OrdinalIgnoreCase))
            {
                background = Unquote(line[(colon + 1)..]);
            }
            else if (string.Equals(key, OneInProgressRule.Key, StringComparison.OrdinalIgnoreCase))
            {
                rule = OneInProgressRule.Parse(Unquote(line[(colon + 1)..]));
            }
            else if (line.Length > 0)
            {
                unknown.Add(line);
            }
        }

        Avatar avatar = new(Avatar.TrimLabel(label), null, TeamFile.ValidColour(string.IsNullOrWhiteSpace(background) ? null : background));
        return new ProjectFile(avatar, string.Join('\n', lines[(closing + 1)..]))
        {
            UnknownLines = unknown,
            OneInProgressPerProject = rule,
        };
    }

    /// <summary>
    /// Composes the file text: the frontmatter, then <see cref="Notes"/>. LF line endings only; an image is never written. The rewrite
    /// tidies a hand-edited frontmatter: the avatar keys come first, blank lines inside it are dropped and trailing spaces trimmed. The
    /// other lines and the notes are kept as they are. A key is one line: a YAML block scalar (<c>avatarLabel: |</c>) reads as its marker, and its indented lines are kept as unknown lines.
    /// </summary>
    /// <returns>The text to write to <c>_project.md</c>.</returns>
    public string Compose()
    {
        StringBuilder builder = new();
        builder.Append(Delimiter).Append('\n');
        AppendOptional(builder, AvatarLabelKey, this.Avatar.Label);
        AppendOptional(builder, AvatarBackgroundKey, this.Avatar.Background);
        AppendOptional(builder, OneInProgressRule.Key, OneInProgressRule.TryToWire(this.OneInProgressPerProject, out string? wire) ? wire : null);
        foreach (string line in this.UnknownLines)
        {
            builder.Append(line).Append('\n');
        }

        builder.Append(Delimiter).Append('\n');
        builder.Append(this.Notes.Replace("\r\n", "\n", StringComparison.Ordinal));
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

    private static void AppendOptional(StringBuilder builder, string key, string? value)
    {
        if (value is not null)
        {
            builder.Append(key).Append(": '").Append(value.Replace("'", "''", StringComparison.Ordinal)).Append("'\n");
        }
    }
}
