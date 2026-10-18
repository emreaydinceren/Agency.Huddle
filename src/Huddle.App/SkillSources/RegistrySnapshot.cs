using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>One published picture of the registry. Replaced whole, never edited (§6.5).</summary>
/// <param name="Version">Starts at 1, +1 per publish; lets a component tell "same picture" cheaply.</param>
/// <param name="Resolved">What the resolver produced.</param>
/// <param name="Checked">What <c>Checks.Run</c> produced: every finding, and the state of every Pack, catalog and Skill.</param>
/// <param name="NewKeys">Row keys and <c>skill:&lt;name&gt;</c> keys in this snapshot and not in <c>seen</c>.</param>
/// <param name="PackHolders">Pack id to the names of the Teammates whose <c>_packs</c> names it.</param>
/// <param name="SkillHolders">Skill name to the names of the Teammates whose <c>skills:</c> holds it.</param>
/// <param name="HiddenPacks">Pack ids in <c>hidden.json</c>.</param>
/// <param name="HiddenSkills">Skill names in <c>hidden.json</c>.</param>
/// <param name="PackHeldVersions">Pack id to the versions its holders' <c>_packs</c> entries name.</param>
internal sealed record RegistrySnapshot(
    long Version,
    ResolvedRegistry Resolved,
    CheckResult Checked,
    IReadOnlySet<string> NewKeys,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PackHolders,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SkillHolders,
    IReadOnlySet<string> HiddenPacks,
    IReadOnlySet<string> HiddenSkills,
    IReadOnlyDictionary<string, IReadOnlySet<string>> PackHeldVersions)
{
    /// <summary>The empty picture before the first read.</summary>
    public static RegistrySnapshot Empty { get; } = new(
        0,
        ResolvedRegistry.Empty,
        CheckResult.Empty,
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>What <see cref="SkillRegistry.Changed"/> carries.</summary>
/// <param name="Snapshot">The snapshot just published.</param>
/// <param name="AppearedKeys">Keys present now and not in the previous snapshot.</param>
/// <param name="ChangedSkills">Skill names whose state or pointer hash differs from the previous snapshot.</param>
internal sealed record RegistryChange(RegistrySnapshot Snapshot, IReadOnlyList<string> AppearedKeys, IReadOnlyList<string> ChangedSkills);

/// <summary>Which part of the registry to re-read: which cached file texts and folder hashes to drop (§6.5, §9).</summary>
/// <param name="Everything">Drop every cache entry.</param>
/// <param name="Files">Pack, catalog and Source files whose cached text is dropped.</param>
/// <param name="SkillFolders">Skill folders whose cached hash is dropped.</param>
internal sealed record RebuildScope(bool Everything, IReadOnlyList<string> Files, IReadOnlyList<string> SkillFolders)
{
    /// <summary>Everything, as at start-up or on a change to SKILLS.md.</summary>
    public static RebuildScope All { get; } = new(true, [], []);

    /// <summary>True for <b>Reload</b>: a file that fails to parse is reported even within its two-second grace.</summary>
    public bool IgnoreGrace { get; init; }

    /// <summary>The union of two scopes; a burst of requests merges into one follow-up.</summary>
    public RebuildScope Union(RebuildScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new RebuildScope(
            this.Everything || other.Everything,
            [.. this.Files.Union(other.Files, StringComparer.OrdinalIgnoreCase)],
            [.. this.SkillFolders.Union(other.SkillFolders, StringComparer.OrdinalIgnoreCase)])
        {
            IgnoreGrace = this.IgnoreGrace || other.IgnoreGrace,
        };
    }
}

/// <summary>Whether a set of Packs and Skills can be handed to Teammates right now.</summary>
/// <param name="Ready">Registry Skills, Pack Skills expanded, that are usable and downloaded.</param>
/// <param name="Missing">Items that are not.</param>
internal sealed record SkillReadiness(IReadOnlyList<SkillEntry> Ready, IReadOnlyList<MissingItem> Missing)
{
    /// <summary>True when nothing is missing.</summary>
    public bool IsReady => this.Missing.Count == 0;
}

/// <summary>A Pack or Skill that is not ready.</summary>
/// <param name="Name">The Pack id or Skill name.</param>
/// <param name="IsPack">True for a Pack.</param>
/// <param name="State">The state that makes it not ready.</param>
/// <param name="Reason">A one-line reason for a card.</param>
/// <param name="Title">The Pack title, or the Skill name again.</param>
/// <param name="CatalogId">The catalog that lists a missing Skill, or null.</param>
internal sealed record MissingItem(string Name, bool IsPack, ItemState State, string Reason, string Title, string? CatalogId);
