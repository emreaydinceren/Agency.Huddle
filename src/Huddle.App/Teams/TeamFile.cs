using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed <c>Teams/&lt;Team&gt;/_team.md</c> file (Spec §TeamFile): a frontmatter block of
/// single-quoted YAML scalars followed by free-form notes. <see cref="Owns"/> and
/// <see cref="Manager"/> form the team card other managers read.
/// </summary>
/// <param name="TeamType">The team type and version the Team was created from.</param>
/// <param name="Manager">The manager Teammate's name, or <see langword="null"/> when none is set.</param>
/// <param name="Workspace">The workspace folder the Team works in, or <see langword="null"/>.</param>
/// <param name="Owns">The one-line statement of what the Team owns, or <see langword="null"/>.</param>
/// <param name="Process">The process document file name, normally <c>Process.md</c>.</param>
/// <param name="Notes">The text after the frontmatter, LF-only, kept verbatim.</param>
internal sealed record TeamFile(VersionedRef TeamType, string? Manager, string? Workspace, string? Owns, string Process, string Notes)
{
    private const string Delimiter = "---";
    private const string TeamTypeKey = "teamType";
    private const string ManagerKey = "manager";
    private const string WorkspaceKey = "workspace";
    private const string OwnsKey = "owns";
    private const string ProcessKey = "process";

    /// <summary>
    /// Frontmatter lines whose key this type does not know, in file order and verbatim. They are
    /// written back by <see cref="Compose"/> so a hand edit survives a rewrite.
    /// </summary>
    public IReadOnlyList<string> UnknownLines { get; init; } = [];

    /// <summary>Parses the text of a <c>_team.md</c> file.</summary>
    /// <param name="text">The file text, with LF or CRLF line endings.</param>
    /// <param name="file">The parsed file, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the frontmatter or a well-formed <c>teamType</c> is missing.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out TeamFile? file)
    {
        file = null;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        if (!string.Equals(lines[0].TrimEnd(), Delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        int closing = -1;
        for (int index = 1; index < lines.Length; index++)
        {
            if (string.Equals(lines[index].TrimEnd(), Delimiter, StringComparison.Ordinal))
            {
                closing = index;
                break;
            }
        }

        if (closing < 0)
        {
            return false;
        }

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        List<string> unknown = [];
        for (int index = 1; index < closing; index++)
        {
            string line = lines[index].TrimEnd();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || char.IsWhiteSpace(line[0]))
            {
                if (line.Length > 0)
                {
                    unknown.Add(line);
                }

                continue;
            }

            string key = line[..colon].Trim();
            if (IsKnownKey(key))
            {
                values[key] = Unquote(line[(colon + 1)..]);
            }
            else
            {
                unknown.Add(line);
            }
        }

        if (!values.TryGetValue(TeamTypeKey, out string? teamTypeText) || !VersionedRef.TryParse(teamTypeText, out VersionedRef? teamType))
        {
            return false;
        }

        string process = values.GetValueOrDefault(ProcessKey, string.Empty);
        string notes = string.Join('\n', lines[(closing + 1)..]);
        file = new TeamFile(
            teamType,
            NullIfBlank(values.GetValueOrDefault(ManagerKey)),
            NullIfBlank(values.GetValueOrDefault(WorkspaceKey)),
            NullIfBlank(values.GetValueOrDefault(OwnsKey)),
            string.IsNullOrWhiteSpace(process) ? TeamPaths.ProcessFileName : process,
            notes)
        {
            UnknownLines = unknown,
        };
        return true;
    }

    /// <summary>Composes the file text: the frontmatter, then <see cref="Notes"/>. LF line endings only.</summary>
    /// <returns>The text to write to <c>_team.md</c>.</returns>
    public string Compose()
    {
        StringBuilder builder = new();
        builder.Append(Delimiter).Append('\n');
        AppendScalar(builder, TeamTypeKey, this.TeamType.ToString());
        if (this.Manager is not null)
        {
            AppendScalar(builder, ManagerKey, this.Manager);
        }

        if (this.Workspace is not null)
        {
            AppendScalar(builder, WorkspaceKey, this.Workspace);
        }

        if (this.Owns is not null)
        {
            AppendScalar(builder, OwnsKey, this.Owns);
        }

        AppendScalar(builder, ProcessKey, this.Process);
        foreach (string line in this.UnknownLines)
        {
            builder.Append(line).Append('\n');
        }

        builder.Append(Delimiter).Append('\n');
        builder.Append(this.Notes.Replace("\r\n", "\n", StringComparison.Ordinal));
        return builder.ToString();
    }

    private static bool IsKnownKey(string key) =>
        string.Equals(key, TeamTypeKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ManagerKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, WorkspaceKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, OwnsKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ProcessKey, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Unquote(string raw)
    {
        string value = raw.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }

    private static void AppendScalar(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append(": '").Append(value.Replace("'", "''", StringComparison.Ordinal)).Append("'\n");
}
