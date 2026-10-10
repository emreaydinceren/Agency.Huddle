using System.Text.Json;

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

/// <summary>A layout node in a <see cref="TabSnapshot"/>: a group of Tabs, or a split of groups drawn side by side.</summary>
/// <param name="Kind">The node kind: <see cref="GroupKind"/> or <see cref="SplitKind"/>.</param>
/// <param name="ActiveId">For a group, the id of its active Tab, or <see langword="null"/>.</param>
/// <param name="Tabs">For a group, the Tabs, left to right. Empty for a split.</param>
/// <param name="Children">For a split, the groups left to right; <see langword="null"/> for a group.</param>
/// <param name="Focused">For a split, the index of the child that has focus.</param>
internal sealed record TabGroupSnapshot(
    string Kind,
    string? ActiveId,
    IReadOnlyList<TabSnapshotEntry> Tabs,
    IReadOnlyList<TabGroupSnapshot>? Children = null,
    int Focused = 0)
{
    /// <summary>The <see cref="Kind"/> of a group node.</summary>
    public const string GroupKind = "group";

    /// <summary>The <see cref="Kind"/> of a split node: its children sit side by side.</summary>
    public const string SplitKind = "split";
}

/// <summary>One Tab in a <see cref="TabGroupSnapshot"/>. The key is not stored: it is derived from the URL on restore.</summary>
/// <param name="Id">The Tab id.</param>
/// <param name="Url">The site-relative address.</param>
/// <param name="Title">The label.</param>
/// <param name="Icon">The symbolic icon name.</param>
/// <param name="Preview">Whether the Tab is still a preview.</param>
/// <param name="Pinned">Whether the Tab is pinned; absent in a version 1 snapshot.</param>
internal sealed record TabSnapshotEntry(string Id, string Url, string Title, string Icon, bool Preview, bool Pinned = false);
