using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>The state a Pack, catalog or Skill is shown in (§8.3, §8.4).</summary>
public enum ItemState
{
    /// <summary>Passes the checks and is downloaded; no Teammate holds it and nothing changed.</summary>
    Available,

    /// <summary>The files at the pointer differ from the copy in Skills/ (§8.4).</summary>
    UpdateAvailable,

    /// <summary>The file is fine but a Skill's path is empty, leads nowhere, or has no entry for this computer.</summary>
    NotDownloaded,

    /// <summary>The Source marks it deprecated. Still choosable.</summary>
    Deprecated,

    /// <summary>The Source pulled it. New hires cannot take it.</summary>
    Withdrawn,

    /// <summary>The pointer leads nowhere or an error check fails.</summary>
    Broken,

    /// <summary>Held by one or more Teammates. Never produced by the library: it needs Teammate files.</summary>
    InUse,

    /// <summary>The row appeared since the Human last opened its tab. Never produced by the library: a UI overlay.</summary>
    New,
}

/// <summary>Where a Skill's path left it on this computer, so the checks can tell R009, S001 and silence apart.</summary>
public enum SkillPathState
{
    /// <summary>The path leads to a folder.</summary>
    Present,

    /// <summary>The path is filled in but leads to no folder (S001).</summary>
    Missing,

    /// <summary>An entry applies to this computer but is empty: not fetched yet.</summary>
    Empty,

    /// <summary>The path is per computer and has no entry for this one and no <c>*</c> (R009).</summary>
    NoEntryForComputer,
}

/// <summary>A Pack as resolved against this computer.</summary>
/// <param name="Row">The Packs row that led to it.</param>
/// <param name="FilePath">The full path the row resolved to; null when it has no path on this computer.</param>
/// <param name="File">The parsed file; null when the file is missing, unreadable, invalid or a duplicate of an earlier row's file.</param>
/// <param name="Id">The Pack id, or an empty string when the file gave none.</param>
/// <param name="Version">The Pack version.</param>
/// <param name="Title">The title, or the file name when the file gave none.</param>
/// <param name="Source">The Source id from the file's <c>source</c> field.</param>
/// <param name="AppliesTo">Role ids.</param>
/// <param name="ConsultWhenAdds">The phrase added to <c>consult_when</c>.</param>
/// <param name="SkillNames">The names of the Pack's Skills, in file order.</param>
/// <param name="MissingSkills">Skills that are Not downloaded, so a card can name them.</param>
/// <param name="Findings">The loading findings about this Pack's file.</param>
/// <param name="DuplicateId">True when an earlier Packs row already has this id (P004): this row keeps a row key.</param>
public sealed record PackEntry(
    RegistryRow Row,
    string? FilePath,
    PackFile? File,
    string Id,
    string Version,
    string Title,
    string Source,
    IReadOnlyList<string> AppliesTo,
    string? ConsultWhenAdds,
    IReadOnlyList<string> SkillNames,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<Finding> Findings,
    bool DuplicateId = false)
{
    /// <summary>The key of this Pack in <see cref="CheckResult.States"/>: <c>pack:&lt;id&gt;</c>, or <c>row:pack:&lt;index&gt;</c>
    /// for a file too broken to give an id.</summary>
    public string StateKey => this.File is null || this.Id.Length == 0 || this.DuplicateId
        ? $"row:pack:{this.Row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : $"pack:{this.Id}";
}

/// <summary>A catalog as resolved against this computer.</summary>
/// <param name="Row">The Catalogs row.</param>
/// <param name="FilePath">The full path the row resolved to.</param>
/// <param name="File">The parsed file, or null.</param>
/// <param name="Id">The catalog id, or an empty string.</param>
/// <param name="Title">The title, or the file name.</param>
/// <param name="Source">The Source id.</param>
/// <param name="SkillNames">The names it lists, in file order.</param>
/// <param name="DownloadedCount">How many of them have a folder on this computer.</param>
/// <param name="Findings">The loading findings about this catalog's file.</param>
/// <param name="DuplicateId">True when an earlier Catalogs row already has this id (D005).</param>
public sealed record ResolvedCatalog(
    RegistryRow Row,
    string? FilePath,
    CatalogFile? File,
    string Id,
    string Title,
    string Source,
    IReadOnlyList<string> SkillNames,
    int DownloadedCount,
    IReadOnlyList<Finding> Findings,
    bool DuplicateId = false)
{
    /// <summary>The key of this catalog in <see cref="CheckResult.States"/>.</summary>
    public string StateKey => this.File is null || this.Id.Length == 0 || this.DuplicateId
        ? $"row:catalog:{this.Row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : $"catalog:{this.Id}";
}

/// <summary>A Source, as read from <c>sources/&lt;id&gt;/source.json</c>.</summary>
/// <param name="Id">The Source id as pack and catalog files write it.</param>
/// <param name="Folder">The Source's folder, or null when the id breaks the rule (nothing is read).</param>
/// <param name="File">Null when the Source has no valid <c>source.json</c> (O001).</param>
/// <param name="HasNotes">Whether <c>NOTES.md</c> exists.</param>
/// <param name="Findings">The loading findings about this Source.</param>
public sealed record ResolvedSource(string Id, string? Folder, SourceFile? File, bool HasNotes, IReadOnlyList<Finding> Findings)
{
    /// <summary>The Source's status, or active when it has no valid file.</summary>
    public SourceStatus Status => this.File?.Status ?? SourceStatus.Active;
}

/// <summary>One place that lists a Skill: a Pack (by its own path or through a catalog) or a catalog.</summary>
/// <param name="Provider"><c>pack:&lt;id&gt;</c> or <c>catalog:&lt;id&gt;</c>.</param>
/// <param name="Kind">The table of the row that reaches it.</param>
/// <param name="RowIndex">The index of that row in its table.</param>
/// <param name="ProviderFile">The pack or catalog file that lists the Skill.</param>
/// <param name="Origin">The origin this provider gives.</param>
/// <param name="License">The licence this provider's catalog entry declares, or null.</param>
/// <param name="Path">The path this provider gives, or null when it gives none.</param>
/// <param name="BaseDirectory">The folder relative paths resolve against: the folder of the file the path is written in.</param>
/// <param name="ViaCatalog">The catalog a Pack Skill names, or null.</param>
public sealed record SkillClaim(
    string Provider,
    RowKind Kind,
    int RowIndex,
    string ProviderFile,
    string Origin,
    string? License,
    PathEntries? Path,
    string BaseDirectory,
    string? ViaCatalog);

/// <summary>One Skill, once per distinct name in the whole registry (§8.3 step 4).</summary>
/// <param name="Name">The Skill name; also the folder name in <c>Skills/</c>.</param>
/// <param name="Description">From the catalog entry, else from the folder's SKILL.md, else empty.</param>
/// <param name="Publisher">From the catalog entry.</param>
/// <param name="License">From the catalog entry.</param>
/// <param name="Category">From the catalog entry.</param>
/// <param name="SubCategory">From the catalog entry.</param>
/// <param name="Requires">Skill names this one depends on.</param>
/// <param name="Origin">The pinned URL of the first provider; the identity two places must share (D003).</param>
/// <param name="Source">The Source id of the first provider's file.</param>
/// <param name="FolderPath">The full path of the Skill's folder on this computer; null when it has no path here.</param>
/// <param name="Providers">Where the Skill is listed, in registry order. The first provider's data wins.</param>
/// <param name="PointerSha256">The hash of the folder at the pointer; null when not downloaded.</param>
/// <param name="InstalledSha256">The <c>sha256</c> of <c>Skills/&lt;name&gt;/.source.json</c> when that copy exists and has the same origin.</param>
/// <param name="Findings">The loading findings about this Skill.</param>
/// <param name="PathState">Where the first provider's path leaves the Skill on this computer.</param>
/// <param name="Claims">Every provider's claim, the first first, including ones whose origin differs (D003).</param>
public sealed record SkillEntry(
    string Name,
    string Description,
    string? Publisher,
    string? License,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> Requires,
    string Origin,
    string? Source,
    string? FolderPath,
    IReadOnlyList<string> Providers,
    string? PointerSha256,
    string? InstalledSha256,
    IReadOnlyList<Finding> Findings,
    SkillPathState PathState,
    IReadOnlyList<SkillClaim> Claims)
{
    /// <summary>The key of this Skill in <see cref="CheckResult.States"/>.</summary>
    public string StateKey => $"skill:{this.Name}";

    /// <summary>True when the pointer's files differ from the copy in <c>Skills/</c> (§8.4).</summary>
    public bool HasUpdate => this.PointerSha256 is not null && this.InstalledSha256 is not null
        && !string.Equals(this.PointerSha256, this.InstalledSha256, StringComparison.Ordinal);
}

/// <summary>Everything one read of the registry produced.</summary>
/// <param name="Registry">What the reader produced.</param>
/// <param name="Packs">Each Packs row, in row order.</param>
/// <param name="Catalogs">Each Catalogs row, in row order.</param>
/// <param name="Skills">One entry per distinct Skill name, in the order of its first provider.</param>
/// <param name="Sources">Each distinct Source id a pack or catalog names, in first-use order.</param>
/// <param name="Findings">The loading findings, in a stable order.</param>
/// <param name="ReadAt">When the read happened, from <see cref="CheckContext.Written"/>.</param>
/// <param name="Retry">Files that failed to parse within two seconds of being written: not reported yet, read again later.</param>
public sealed record ResolvedRegistry(
    Registry Registry,
    IReadOnlyList<PackEntry> Packs,
    IReadOnlyList<ResolvedCatalog> Catalogs,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<ResolvedSource> Sources,
    IReadOnlyList<Finding> Findings,
    DateTimeOffset ReadAt,
    IReadOnlyList<string> Retry)
{
    /// <summary>A registry with nothing in it.</summary>
    public static ResolvedRegistry Empty { get; } = new(new Registry(string.Empty, [], [], []), [], [], [], [], [], DateTimeOffset.MinValue, []);

    /// <summary>The first Pack with this id, ignoring case; null when none.</summary>
    public PackEntry? FindPack(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.Packs.FirstOrDefault(pack => pack.File is not null && !pack.DuplicateId && string.Equals(pack.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The Skill with this name; null when none.</summary>
    public SkillEntry? FindSkill(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return this.Skills.FirstOrDefault(skill => string.Equals(skill.Name, name, StringComparison.Ordinal));
    }

    /// <summary>The first catalog with this id; null when none.</summary>
    public ResolvedCatalog? FindCatalog(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.Catalogs.FirstOrDefault(catalog => catalog.File is not null && !catalog.DuplicateId && string.Equals(catalog.Id, id, StringComparison.Ordinal));
    }

    /// <summary>The Packs whose <c>appliesTo</c> names <paramref name="roleId"/>, in any state, in registry order.</summary>
    public IReadOnlyList<PackEntry> PacksForRole(string roleId)
    {
        ArgumentNullException.ThrowIfNull(roleId);
        return [.. this.Packs.Where(pack => pack.AppliesTo.Contains(roleId, StringComparer.OrdinalIgnoreCase))];
    }

    /// <summary>The Source with this id; null when none.</summary>
    public ResolvedSource? FindSource(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.Sources.FirstOrDefault(source => string.Equals(source.Id, id, StringComparison.Ordinal));
    }

    /// <summary>The Packs with a Skill that names this catalog.</summary>
    public IReadOnlyList<PackEntry> PacksUsingCatalog(string catalogId)
    {
        ArgumentNullException.ThrowIfNull(catalogId);
        return [.. this.Packs.Where(pack => pack.File?.Skills.Any(skill => string.Equals(skill.Catalog, catalogId, StringComparison.Ordinal)) == true)];
    }

    /// <summary>The Packs whose file names this Source.</summary>
    public IReadOnlyList<PackEntry> PacksOfSource(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return [.. this.Packs.Where(pack => string.Equals(pack.Source, sourceId, StringComparison.Ordinal))];
    }

    /// <summary>The catalogs whose file names this Source.</summary>
    public IReadOnlyList<ResolvedCatalog> CatalogsOfSource(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return [.. this.Catalogs.Where(catalog => string.Equals(catalog.Source, sourceId, StringComparison.Ordinal))];
    }

    /// <summary>The row key of a Pack (§6.2, "Row identity").</summary>
    public string RowKey(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return RowKeyOf(pack.Row, this.Registry.Directory);
    }

    /// <summary>The row key of a catalog.</summary>
    public string RowKey(ResolvedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return RowKeyOf(catalog.Row, this.Registry.Directory);
    }

    /// <summary><c>packs:</c> or <c>catalogs:</c> plus the first entry's path, absolute, upper-cased, with <c>\</c> separators.</summary>
    public static string RowKeyOf(RegistryRow row, string registryDir)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(registryDir);
        string prefix = row.Kind == RowKind.Pack ? "packs:" : "catalogs:";
        string path = row.LocalPath.FirstText.Length == 0 ? string.Empty : PathEntries.MakeFull(row.LocalPath.FirstText, registryDir);
        return prefix + path.ToUpperInvariant().Replace('/', '\\');
    }
}
