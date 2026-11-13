using System.Text.Json;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// The stored form of the Tabs (section 5 of <c>docs/Huddle.Tabs-Specifications.md</c>). The root is
/// a layout node: one group, or a split of groups side by side. Version 1 stored a lone group and is
/// still read; version 2 adds the split and the pinned flag.
/// </summary>
/// <param name="Version">The format version. A snapshot of a version not in <see cref="ReadableVersions"/> is ignored.</param>
/// <param name="Root">The layout node: a group, or a split whose children are groups.</param>
internal sealed record TabSnapshot(int Version, TabGroupSnapshot Root)
{
    /// <summary>The version this build writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>The versions this build reads: version 1 is a lone group with no pinned Tabs.</summary>
    public static readonly IReadOnlyList<int> ReadableVersions = [1, CurrentVersion];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Writes this snapshot as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Rewrites the Team (and Project) names in every stored Tab address through the rename journal, so a Tab saved before a rename restores under today's name.</summary>
    /// <param name="renames">The rename journal.</param>
    /// <returns>The snapshot with today's names; this snapshot when nothing changed.</returns>
    public TabSnapshot MapTeamNames(ITeamRenames renames)
    {
        ArgumentNullException.ThrowIfNull(renames);


        // Storage is hand-editable and this runs before TabService.Restore validates: whatever is missing or empty is left as it is.
        return this.Root is null ? this : this with { Root = MapGroup(this.Root, renames) ?? this.Root };
    }

    private static TabGroupSnapshot? MapGroup(TabGroupSnapshot? group, ITeamRenames renames)
    {
        if (group is null)
        {
            return null;
        }

        IReadOnlyList<TabSnapshotEntry>? stored = group.Tabs;
        IReadOnlyList<TabSnapshotEntry> tabs = stored is null ? group.Tabs : [.. stored.Select(entry => MapEntry(entry, renames) ?? entry)];
        IReadOnlyList<TabGroupSnapshot>? children = group.Children is null ? null : [.. group.Children.Select(child => MapGroup(child, renames) ?? child)];
        return group with { Tabs = tabs, Children = children };
    }

    private static TabSnapshotEntry? MapEntry(TabSnapshotEntry? entry, ITeamRenames renames)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Url))
        {
            return entry;
        }

        string url = MapUrl(entry.Url, renames);
        if (string.Equals(url, entry.Url, StringComparison.Ordinal))
        {
            return entry;
        }

        string title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Title : renames.Resolve(entry.Title);
        return entry with { Url = url, Title = title };
    }

    private static string MapUrl(string url, ITeamRenames renames)
    {
        const string Marker = "/teams/";
        int start = url.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || (start > 0 && !Uri.TryCreate(url, UriKind.Absolute, out _)))
        {
            return url;
        }

        int teamStart = start + Marker.Length;
        int teamEnd = url.IndexOfAny(['/', '?', '#'], teamStart);
        if (teamEnd < 0)
        {
            teamEnd = url.Length;
        }

        string oldTeam = Uri.UnescapeDataString(url[teamStart..teamEnd]);
        if (string.IsNullOrWhiteSpace(oldTeam))
        {
            return url;
        }

        string rest = url[teamEnd..];
        const string ProjectsMarker = "/projects/";
        if (rest.StartsWith(ProjectsMarker, StringComparison.OrdinalIgnoreCase))
        {
            int projectEnd = rest.IndexOfAny(['/', '?', '#'], ProjectsMarker.Length);
            projectEnd = projectEnd < 0 ? rest.Length : projectEnd;
            string oldProject = Uri.UnescapeDataString(rest[ProjectsMarker.Length..projectEnd]);
            if (!string.IsNullOrWhiteSpace(oldProject))
            {
                string project = renames.ResolveProject(oldTeam, oldProject);
                rest = string.Concat(ProjectsMarker, Uri.EscapeDataString(project), rest.AsSpan(projectEnd));
            }
        }

        string team = renames.Resolve(oldTeam);
        return string.Concat(url.AsSpan(0, teamStart), Uri.EscapeDataString(team), rest);
    }

    /// <summary>Reads a snapshot, or returns <see langword="null"/> for text that is absent or not a snapshot; it never throws on bad storage.</summary>
    /// <param name="json">The stored text, or <see langword="null"/>.</param>
    public static TabSnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TabSnapshot>(json, Options);
        }
        catch (JsonException)
        {
            // Browser storage is user-editable; a damaged value means "start with no Tabs".
            return null;
        }
    }
}
