using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Resolving;

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
