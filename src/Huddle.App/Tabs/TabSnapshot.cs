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
