using Agency.Huddle.App.Acp;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>A Skill whose files changed at the pointer: shared by every holder, so it has no per-Teammate choice (§8.4).</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="FromCommit">The copy's commit, short.</param>
/// <param name="ToCommit">The pointer's commit, short, or empty when unknown.</param>
/// <param name="FilesChanged">Files whose content differs plus files on one side only.</param>
/// <param name="ModifiedLocally">Whether the copy was edited on this computer.</param>
/// <param name="AffectedTeammates">The Teammates that hold it.</param>
internal sealed record SkillFileChange(string Name, string FromCommit, string ToCommit, int FilesChanged, bool ModifiedLocally, IReadOnlyList<string> AffectedTeammates);

/// <summary>A Skill the Pack now lists that some holder does not have.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Source">The Source id.</param>
/// <param name="Commit">The pointer's commit, short.</param>
/// <param name="License">The licence the catalog declares.</param>
internal sealed record SkillMembershipChange(string Name, string Source, string Commit, string? License);

/// <summary>What one holder's file would change.</summary>
/// <param name="Name">The Teammate.</param>
/// <param name="Role">Its title.</param>
/// <param name="AddSkills">Skills added to its <c>skills:</c>.</param>
/// <param name="RemoveSkills">Skills taken off.</param>
/// <param name="ConsultWhenChanges">Whether its <c>consult_when</c> gains the Pack's phrase.</param>
/// <param name="VersionChanges">Whether its <c>_packs</c> version changes.</param>
internal sealed record TeammateChange(string Name, string Role, IReadOnlyList<string> AddSkills, IReadOnlyList<string> RemoveSkills, bool ConsultWhenChanges, bool VersionChanges)
{
    /// <summary>What the row says changes: "adds a and b; restarts".</summary>
    public string Summary
    {
        get
        {
            List<string> parts = [];
            if (this.AddSkills.Count > 0)
            {
                parts.Add($"adds {SkillStateText.AndList(this.AddSkills)}");
            }

            if (this.RemoveSkills.Count > 0)
            {
                parts.Add($"removes {SkillStateText.AndList(this.RemoveSkills)}");
            }

            if (this.ConsultWhenChanges)
            {
                parts.Add("updates consult when");
            }

            if (parts.Count == 0 && this.VersionChanges)
            {
                parts.Add("records the new version");
            }

            parts.Add("restarts");
            return string.Join("; ", parts);
        }
    }
}

/// <summary>Everything the update card (G7) draws.</summary>
/// <param name="PackId">The Pack id, or the Skill name for a Skill outside a Pack.</param>
/// <param name="Title">The Pack title or the Skill name.</param>
/// <param name="FromVersion">The version the holders record, or empty.</param>
/// <param name="ToVersion">The Pack file's version, or empty.</param>
/// <param name="FileChanges">Skills whose files changed.</param>
/// <param name="Added">Skills the Pack lists that a holder lacks.</param>
/// <param name="Removed">Skills the Pack no longer lists. Always empty: Huddle keeps no earlier copy of a Pack file.</param>
/// <param name="ConsultWhenFrom">The phrase before, when known.</param>
/// <param name="ConsultWhenTo">The Pack's phrase now.</param>
/// <param name="Teammates">The holders whose file would change.</param>
internal sealed record PackUpdatePlan(
    string PackId,
    string Title,
    string FromVersion,
    string ToVersion,
    IReadOnlyList<SkillFileChange> FileChanges,
    IReadOnlyList<SkillMembershipChange> Added,
    IReadOnlyList<SkillMembershipChange> Removed,
    string? ConsultWhenFrom,
    string? ConsultWhenTo,
    IReadOnlyList<TeammateChange> Teammates)
{
    /// <summary>Whether nothing would change.</summary>
    public bool IsEmpty => this.FileChanges.Count == 0 && this.Teammates.Count == 0;
}

/// <summary>Works out an update (Skill Packs spec §6.9.8, §8.4). Pure apart from reading through <see cref="IFileReader"/>.</summary>
internal static class PackUpdatePlanner
{
    /// <summary>The plan for one Pack; its Teammates are the holders whose file would change.</summary>
    public static PackUpdatePlan Plan(string packId, SkillRegistry registry, SkillStore skills, IReadOnlyList<Persona> teammates, IFileReader files)
    {
        ArgumentNullException.ThrowIfNull(packId);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(teammates);
        ArgumentNullException.ThrowIfNull(files);
        PackEntry? pack = registry.FindPack(packId);
        if (pack is null)
        {
            return new PackUpdatePlan(packId, packId, string.Empty, string.Empty, [], [], [], null, null, []);
        }

        RegistrySnapshot snapshot = registry.Current;
        IReadOnlyList<SkillFileChange> fileChanges = [.. pack.SkillNames.Select(snapshot.Resolved.FindSkill).OfType<SkillEntry>().Where(static entry => entry.HasUpdate)
            .Select(entry => FileChange(entry, registry, skills, files))];

        List<TeammateChange> changes = [];
        HashSet<string> added = new(StringComparer.Ordinal);
        string? fromVersion = null;
        foreach (Persona persona in teammates.Where(persona => HoldsPack(persona.Text, pack.Id)))
        {
            if (!PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _))
            {
                continue;
            }

            string? version = (identity.Packs ?? []).FirstOrDefault(entry => string.Equals(entry.Id, pack.Id, StringComparison.OrdinalIgnoreCase))?.Version;
            fromVersion ??= version;
            IReadOnlyList<string> add = [.. pack.SkillNames.Where(name => !(identity.Skills ?? []).Contains(name, StringComparer.Ordinal))];
            bool consult = !string.IsNullOrWhiteSpace(pack.ConsultWhenAdds) && !ConsultWhen(persona.Text).Contains(pack.ConsultWhenAdds.Trim(), StringComparison.Ordinal);
            bool versionChanges = !string.Equals(version, pack.Version, StringComparison.Ordinal);
            if (add.Count > 0 || consult || versionChanges)
            {
                changes.Add(new TeammateChange(persona.Name, identity.Title, add, [], consult, versionChanges));
                added.UnionWith(add);
            }
        }

        IReadOnlyList<SkillMembershipChange> addedSkills = [.. added.Order(StringComparer.Ordinal).Select(name => Membership(name, snapshot))];
        return new PackUpdatePlan(pack.Id, pack.Title, fromVersion ?? string.Empty, pack.Version, fileChanges, addedSkills, [], null, pack.ConsultWhenAdds, changes);
    }

    /// <summary>The plan for a Skill outside a Pack: <see cref="PackUpdatePlan.FileChanges"/> only.</summary>
    public static PackUpdatePlan PlanSkill(string skillName, SkillRegistry registry, SkillStore skills, IFileReader files)
    {
        ArgumentNullException.ThrowIfNull(skillName);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(files);
        SkillEntry? entry = registry.Current.Resolved.FindSkill(skillName);
        IReadOnlyList<SkillFileChange> changes = entry is { HasUpdate: true } ? [FileChange(entry, registry, skills, files)] : [];
        return new PackUpdatePlan(skillName, skillName, string.Empty, string.Empty, changes, [], [], null, null, []);
    }

    /// <summary>The short commit of a <c>…/tree/{commit}/…</c> origin, or empty.</summary>
    public static string CommitOf(string? origin)
    {
        return origin is null ? string.Empty : SkillOriginText.Parts(origin).ShortCommit;
    }

    /// <summary>One Skill's file change.</summary>
    private static SkillFileChange FileChange(SkillEntry entry, SkillRegistry registry, SkillStore skills, IFileReader files)
    {
        Skill? copy = skills.Get(entry.Name);
        string from = copy?.Provenance?.Commit is { Length: > 0 } commit ? commit[..Math.Min(7, commit.Length)] : string.Empty;
        string to = CommitOf(entry.Origin);
        if (to.Length == 0 && entry.Source is { } source && registry.Current.Resolved.FindSource(source)?.File?.Commit is { Length: > 0 } sourceCommit)
        {
            to = sourceCommit[..Math.Min(7, sourceCommit.Length)];
        }

        int changed = copy?.FolderPath is { } copyFolder && entry.FolderPath is { } pointer
            ? SkillFolderHash.ChangedFiles(SkillFolderHash.FileHashes(files, copyFolder), SkillFolderHash.FileHashes(files, pointer))
            : 0;
        return new SkillFileChange(entry.Name, from, to, changed, copy?.ModifiedLocally == true, registry.HolderNames(entry));
    }

    /// <summary>A Skill the Pack lists, as the membership section names it.</summary>
    private static SkillMembershipChange Membership(string name, RegistrySnapshot snapshot)
    {
        SkillEntry? entry = snapshot.Resolved.FindSkill(name);
        return new SkillMembershipChange(name, entry?.Source ?? string.Empty, CommitOf(entry?.Origin), entry?.License);
    }

    /// <summary>Whether the file's <c>_packs</c> names the Pack.</summary>
    private static bool HoldsPack(string text, string packId)
    {
        return PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _)
            && (identity.Packs ?? []).Any(entry => string.Equals(entry.Id, packId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The file's <c>consult_when</c>, or empty.</summary>
    private static string ConsultWhen(string text)
    {
        return PersonaFrontmatter.Parse(text).Fields.FirstOrDefault(static field => string.Equals(field.Key, "consult_when", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
    }
}
