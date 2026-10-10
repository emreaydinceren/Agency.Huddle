using System.Text.Json;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// The stored form of the Tabs (section 5 of <c>docs/Huddle.Tabs-Specifications.md</c>). The root is
/// a layout node so version 2 can add splits without a migration; version 1 writes one group.
/// </summary>
/// <param name="Version">The format version. A snapshot of any other version is ignored.</param>
/// <param name="Root">The layout node. Only a group is understood.</param>
internal sealed record TabSnapshot(int Version, TabGroupSnapshot Root)
{
    /// <summary>The only version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

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

/// <summary>A group of Tabs in a <see cref="TabSnapshot"/>.</summary>
/// <param name="Kind">The node kind; <see cref="GroupKind"/> in version 1.</param>
/// <param name="ActiveId">The id of the active Tab, or <see langword="null"/>.</param>
/// <param name="Tabs">The Tabs, left to right.</param>
internal sealed record TabGroupSnapshot(string Kind, string? ActiveId, IReadOnlyList<TabSnapshotEntry> Tabs)
{
    /// <summary>The <see cref="Kind"/> of a group node.</summary>
    public const string GroupKind = "group";
}

/// <summary>One Tab in a <see cref="TabGroupSnapshot"/>. The key is not stored: it is derived from the URL on restore.</summary>
/// <param name="Id">The Tab id.</param>
/// <param name="Url">The site-relative address.</param>
/// <param name="Title">The label.</param>
/// <param name="Icon">The symbolic icon name.</param>
/// <param name="Preview">Whether the Tab is still a preview.</param>
internal sealed record TabSnapshotEntry(string Id, string Url, string Title, string Icon, bool Preview);
