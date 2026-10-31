using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Teams;

/// <summary>The one rule for adding and removing a Pack on an Agent (Skill Packs spec §6.10.4): the card's chip, the
/// Remove Pack flow and the update card all use it, so one rule decides which Skills leave with a Pack. Pure: text in, text
/// out; nothing is written.</summary>
internal static class PackMembership
{
    /// <summary>The <c>consult_when</c> key.</summary>
    private const string ConsultWhenKey = "consult_when";

    /// <summary>Adds the Pack: its <c>_packs</c> entry (replacing one of the same id), the Skills not already listed, and
    /// its clause when <c>consult_when</c> does not already carry it.</summary>
    public static string Add(string text, PackContents pack)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pack);
        (IReadOnlyList<string> skills, IReadOnlyList<VersionedRef> packs) = Read(text);

        List<VersionedRef> active = [.. packs.Where(entry => !string.Equals(entry.Id, pack.Id, StringComparison.Ordinal)), new VersionedRef(pack.Id, pack.Version)];
        List<string> names = [.. skills, .. pack.SkillNames.Where(name => !skills.Contains(name, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];

        List<string> clauses = Clauses(text, out bool hasField);
        string clause = pack.ConsultWhenAdds?.Trim() ?? string.Empty;
        if (clause.Length > 0 && !clauses.Contains(clause, StringComparer.Ordinal))
        {
            clauses.Add(clause);
        }

        return Write(text, names, active, clauses, hasField);
    }

    /// <summary>Removes the Pack: its <c>_packs</c> entry, its clause, and each of its Skills that neither the role nor
    /// another of <paramref name="otherPacks"/> provides.</summary>
    /// <param name="text">The Agent file.</param>
    /// <param name="pack">The Pack to remove.</param>
    /// <param name="roleSkills">The Skills the Agent's role carries; they stay.</param>
    /// <param name="otherPacks">The Agent's other Packs; their Skills stay.</param>
    public static string Remove(string text, PackContents pack, IReadOnlyCollection<string> roleSkills, IReadOnlyList<PackContents> otherPacks)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(roleSkills);
        ArgumentNullException.ThrowIfNull(otherPacks);
        (IReadOnlyList<string> skills, IReadOnlyList<VersionedRef> packs) = Read(text);

        HashSet<string> kept = new(roleSkills, StringComparer.Ordinal);
        foreach (PackContents other in otherPacks.Where(other => !string.Equals(other.Id, pack.Id, StringComparison.Ordinal)))
        {
            kept.UnionWith(other.SkillNames);
        }

        HashSet<string> leaving = new(pack.SkillNames.Where(name => !kept.Contains(name)), StringComparer.Ordinal);
        List<string> names = [.. skills.Where(name => !leaving.Contains(name))];
        List<VersionedRef> active = [.. packs.Where(entry => !string.Equals(entry.Id, pack.Id, StringComparison.Ordinal))];

        List<string> clauses = Clauses(text, out bool hasField);
        string clause = pack.ConsultWhenAdds?.Trim() ?? string.Empty;
        clauses.RemoveAll(entry => string.Equals(entry, clause, StringComparison.Ordinal));
        return Write(text, names, active, clauses, hasField);
    }

    /// <summary>Drops only the <c>_packs</c> entry, leaving <c>skills:</c> and <c>consult_when:</c> as they are (the Keep
    /// choice of Remove Pack).</summary>
    public static string DropRecord(string text, string packId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(packId);
        IReadOnlyList<VersionedRef> packs = Read(text).Packs;
        return PersonaFrontmatter.WriteListField(text, "_packs", [.. packs.Where(entry => !string.Equals(entry.Id, packId, StringComparison.Ordinal)).Select(static entry => entry.ToString())]);
    }

    /// <summary>The Skills and Packs the file records.</summary>
    private static (IReadOnlyList<string> Skills, IReadOnlyList<VersionedRef> Packs) Read(string text)
    {
        return PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _)
            ? (identity.Skills ?? [], identity.Packs ?? [])
            : ([], []);
    }

    /// <summary>The <c>consult_when</c> clauses, split on <c>"; "</c>.</summary>
    private static List<string> Clauses(string text, out bool hasField)
    {
        string? existing = PersonaFrontmatter.Parse(text).Fields
            .FirstOrDefault(static field => string.Equals(field.Key, ConsultWhenKey, StringComparison.OrdinalIgnoreCase))?.Value;
        hasField = existing is not null;
        return [.. (existing ?? string.Empty).Split("; ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>Writes the three fields. A <c>consult_when</c> left with no clause is removed rather than written empty, so
    /// adding a Pack and removing it again gives back the file it started from.</summary>
    private static string Write(string text, IReadOnlyList<string> skills, IReadOnlyList<VersionedRef> packs, List<string> clauses, bool hasField)
    {
        string updated = PersonaFrontmatter.WriteListField(text, "skills", skills);
        updated = PersonaFrontmatter.WriteListField(updated, "_packs", [.. packs.Select(static entry => entry.ToString())]);
        if (clauses.Count > 0)
        {
            return PersonaFrontmatter.WriteScalarField(updated, ConsultWhenKey, string.Join("; ", clauses));
        }

        return hasField ? PersonaFrontmatter.WriteListField(updated, ConsultWhenKey, []) : updated;
    }
}
