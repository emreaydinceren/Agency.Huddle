using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using MudBlazor.Utilities;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed <c>Teams/&lt;Team&gt;/_team.md</c> file (Spec §TeamFile): a frontmatter block of
/// single-quoted YAML scalars followed by free-form notes. <see cref="Owns"/> and
/// <see cref="Manager"/> form the team card other managers read.
/// </summary>
/// <param name="TeamType">The team type and version the Team was created from.</param>
/// <param name="Manager">The manager Agent's name, or <see langword="null"/> when none is set.</param>
/// <param name="Workspace">The workspace folder the Team works in, or <see langword="null"/>.</param>
/// <param name="Owns">The one-line statement of what the Team owns, or <see langword="null"/>.</param>
/// <param name="Notes">The text after the frontmatter, LF-only, kept verbatim.</param>
/// <remarks>
/// The Process document is always <see cref="TeamPaths.AgentsMdFileName"/> (see <see cref="TeamProcessFile"/>). Files
/// written before issue #312 carry a <c>process:</c> line; it is read as a known key and dropped, so the next rewrite
/// removes it rather than keeping a setting nothing reads.
/// </remarks>
internal sealed record TeamFile(VersionedRef TeamType, string? Manager, string? Workspace, string? Owns, string Notes)
{
    private const string Delimiter = "---";
    private const string TeamTypeKey = "teamType";
    private const string ManagerKey = "manager";
    private const string WorkspaceKey = "workspace";
    private const string OwnsKey = "owns";
    private const string RetiredProcessKey = "process";
    private const string OnboardedKey = "onboarded";
    private const string AvatarLabelKey = "avatarLabel";
    private const string AvatarImageKey = "avatarImage";
    private const string AvatarBackgroundKey = "avatarBackground";
    private const string GatesKey = "gates";
    private const string WipKey = "wip";
    private const string RetroKey = "retro";
    private const string DigestKey = "digest";
    private const string CheckCommandKey = "checkCommand";
    private const string TemplateKey = "template";
    private const string ArchivedMembersKey = "archivedMembers";
    private const string ArchivedRoomsKey = "archivedRooms";
    private const string ArchivedManagerKey = "archivedManager";

    /// <summary>
    /// The file's first line as read: <c>---</c>, or the lenient spelling a hand edit left (a leading byte-order mark, surrounding
    /// spaces). <see cref="Compose"/> writes it back unchanged so rewriting a hand-edited file touches only its field lines.
    /// </summary>
    public string OpeningFence { get; init; } = Delimiter;

    /// <summary>
    /// The line that closed the frontmatter as read: <c>---</c>, or a spelling a hand edit left (<c>...</c>, surrounding spaces).
    /// <see cref="Compose"/> writes it back unchanged.
    /// </summary>
    public string ClosingFence { get; init; } = Delimiter;

    /// <summary>
    /// Frontmatter lines whose key this type does not know, in file order and verbatim. They are
    /// written back by <see cref="Compose"/> so a hand edit survives a rewrite.
    /// </summary>
    public IReadOnlyList<string> UnknownLines { get; init; } = [];

    /// <summary>
    /// Whether the manager has been sent the onboarding prompt (PR-5). Stored as an <c>onboarded: true</c>
    /// line so a restart does not repeat the post.
    /// </summary>
    public bool Onboarded { get; init; }

    /// <summary>The process settings (Spec §7.2): gates, wip, retro, digest, check command and template.</summary>
    public ProcessSettings Process { get; init; } = ProcessSettings.Empty;

    /// <summary>The Names of members archived out of the Team; the <c>archivedMembers</c> key is written only while any are.</summary>
    public IReadOnlyList<string> ArchivedMembers { get; init; } = [];

    /// <summary>The ids of the Rooms archived with the Team; the <c>archivedRooms</c> key is written only while any are, and unarchive restores exactly these.</summary>
    public IReadOnlyList<string> ArchivedRooms { get; init; } = [];

    /// <summary>The Name of the manager the Team had when it was archived; the <c>archivedManager</c> key is written only while one is set, and unarchive puts the manager back.</summary>
    public string? ArchivedManager { get; init; }

    /// <summary>Returns a copy whose gates are <paramref name="gates"/>.</summary>
    /// <param name="gates">The gates the copy lists.</param>
    /// <returns>The new file; every other setting is unchanged.</returns>
    public TeamFile WithGates(IReadOnlyList<GateId> gates)
    {
        ArgumentNullException.ThrowIfNull(gates);
        return this with { Process = this.Process with { Gates = gates } };
    }

    /// <summary>
    /// The Team's avatar, kept in the optional <c>avatarLabel</c>, <c>avatarImage</c> and <c>avatarBackground</c> keys.
    /// It lives here and not in <c>avatars.json</c> because that file is keyed by Name and shared with Agents and the
    /// Human, so a Team named like an Agent would overwrite that Agent's avatar.
    /// </summary>
    public Avatar Avatar { get; init; } = Avatar.None;

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
        if (!FrontmatterFence.IsOpening(lines[0]))
        {
            return false;
        }

        int closing = FrontmatterFence.FindClosing(lines, 1);
        if (closing < 0)
        {
            return false;
        }

        (Dictionary<string, string> values, List<string> unknown) = ReadFrontmatter(lines, closing);

        VersionedRef? teamType = VersionedRef.None;
        if (values.TryGetValue(TeamTypeKey, out string? teamTypeText) && !string.IsNullOrWhiteSpace(teamTypeText)
            && !VersionedRef.TryParse(teamTypeText, out teamType))
        {
            return false;
        }

        string notes = string.Join('\n', lines[(closing + 1)..]);
        file = new TeamFile(
            teamType,
            NullIfBlank(values.GetValueOrDefault(ManagerKey)),
            NullIfBlank(values.GetValueOrDefault(WorkspaceKey)),
            NullIfBlank(values.GetValueOrDefault(OwnsKey)),
            notes)
        {
            OpeningFence = lines[0],
            ClosingFence = lines[closing],
            UnknownLines = unknown,
            Onboarded = string.Equals(values.GetValueOrDefault(OnboardedKey), "true", StringComparison.OrdinalIgnoreCase),
            Avatar = ReadAvatar(values),
            Process = ProcessSettings.Parse(values),
            ArchivedMembers = values.TryGetValue(ArchivedMembersKey, out string? archived) ? ProcessDocument.SplitList(archived) : [],
            ArchivedRooms = values.TryGetValue(ArchivedRoomsKey, out string? archivedRooms) ? ProcessDocument.SplitList(archivedRooms) : [],
            ArchivedManager = NullIfBlank(values.GetValueOrDefault(ArchivedManagerKey)),
        };
        return true;
    }

    /// <summary>
    /// Reads the three avatar keys. The label is trimmed as <see cref="Avatar.TrimLabel(string?)"/> trims every label. An image
    /// name that is not a bare file name, and a background that is not a colour, are dropped: the first becomes a path and a
    /// delete target and the second goes into an inline style, and this file is hand-edited.
    /// </summary>
    /// <param name="values">The known frontmatter values by key.</param>
    /// <returns>The avatar, or <see cref="Avatar.None"/> when no key survives.</returns>
    private static Avatar ReadAvatar(Dictionary<string, string> values)
    {
        string? label = Avatar.TrimLabel(values.GetValueOrDefault(AvatarLabelKey));
        string? image = NullIfBlank(values.GetValueOrDefault(AvatarImageKey));
        if (image is not null && !Avatar.IsBareImageName(image))
        {
            image = null;
        }

        return new Avatar(label, image, ValidColour(NullIfBlank(values.GetValueOrDefault(AvatarBackgroundKey))));
    }

    /// <summary>Returns <paramref name="raw"/> when MudBlazor parses it as a colour, else <see langword="null"/>. <see cref="ProjectFile"/> reads its background through it too.</summary>
    /// <param name="raw">The stored background text, or <see langword="null"/>.</param>
    internal static string? ValidColour(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            _ = new MudColor(raw);
            return raw;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Reads the frontmatter lines into the known keys and the lines kept verbatim.</summary>
    /// <param name="lines">The document lines.</param>
    /// <param name="closing">The index of the closing delimiter line.</param>
    /// <returns>The known values by key, and the lines that are not a known <c>key: value</c> pair.</returns>
    private static (Dictionary<string, string> Values, List<string> Unknown) ReadFrontmatter(string[] lines, int closing)
    {
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

        return (values, unknown);
    }

    /// <summary>Composes the file text: the frontmatter, then <see cref="Notes"/>. LF line endings only.</summary>
    /// <returns>The text to write to <c>_team.md</c>.</returns>
    public string Compose()
    {
        StringBuilder builder = new();
        builder.Append(this.OpeningFence).Append('\n');
        if (this.TeamType != VersionedRef.None)
        {
            AppendScalar(builder, TeamTypeKey, this.TeamType.ToString());
        }

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

        if (this.Onboarded)
        {
            builder.Append(OnboardedKey).Append(": true\n");
        }

        AppendOptional(builder, AvatarLabelKey, this.Avatar.Label);
        AppendOptional(builder, AvatarImageKey, this.Avatar.Image);
        AppendOptional(builder, AvatarBackgroundKey, this.Avatar.Background);
        this.AppendProcess(builder);

        foreach (string line in this.UnknownLines)
        {
            builder.Append(line).Append('\n');
        }

        builder.Append(this.ClosingFence).Append('\n');
        builder.Append(this.Notes.Replace("\r\n", "\n", StringComparison.Ordinal));
        return builder.ToString();
    }

    private void AppendProcess(StringBuilder builder)
    {
        ProcessSettings process = this.Process;
        if (process.Gates.Count > 0 || process.UnknownGates.Count > 0)
        {
            AppendList(builder, GatesKey, process.Gates.Select(ProcessDocument.ToId).Concat(process.UnknownGates));
        }

        if (process.Wip is int wip)
        {
            builder.Append(WipKey).Append(": ").Append(wip.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        AppendRaw(builder, RetroKey, process.Retro);
        AppendRaw(builder, DigestKey, process.Digest);
        AppendOptional(builder, CheckCommandKey, process.CheckCommand);
        AppendOptional(builder, TemplateKey, process.Template);
        if (this.ArchivedMembers.Count > 0)
        {
            AppendList(builder, ArchivedMembersKey, this.ArchivedMembers);
        }

        if (this.ArchivedRooms.Count > 0)
        {
            AppendList(builder, ArchivedRoomsKey, this.ArchivedRooms);
        }

        AppendOptional(builder, ArchivedManagerKey, this.ArchivedManager);
    }

    private static void AppendRaw(StringBuilder builder, string key, string? value)
    {
        if (value is not null)
        {
            builder.Append(key).Append(": ").Append(value).Append('\n');
        }
    }

    private static void AppendList(StringBuilder builder, string key, IEnumerable<string> items) =>
        builder.Append(key).Append(": [")
            .Append(string.Join(", ", items.Select(static item => "'" + item.Replace("'", "''", StringComparison.Ordinal) + "'")))
            .Append("]\n");

    private static bool IsKnownKey(string key) =>
        string.Equals(key, GatesKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, WipKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, RetroKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, DigestKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, CheckCommandKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, TemplateKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ArchivedMembersKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ArchivedRoomsKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ArchivedManagerKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, TeamTypeKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, ManagerKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, WorkspaceKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, OwnsKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, RetiredProcessKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, OnboardedKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, AvatarLabelKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, AvatarImageKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, AvatarBackgroundKey, StringComparison.OrdinalIgnoreCase);

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

    private static void AppendOptional(StringBuilder builder, string key, string? value)
    {
        if (value is not null)
        {
            AppendScalar(builder, key, value);
        }
    }

    private static void AppendScalar(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append(": '").Append(value.Replace("'", "''", StringComparison.Ordinal)).Append("'\n");
}
