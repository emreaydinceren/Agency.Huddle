using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>What a Pack card and its Details draw (Skill Packs spec §6.9.4, §6.9.5, §6.9.10), worked out once from the
/// registry so the components only lay it out.</summary>
/// <param name="Pack">The registry's entry.</param>
/// <param name="Title">The Pack title, or the file name for a row too broken to give one.</param>
/// <param name="State">The display state.</param>
/// <param name="Holders">The Agents whose <c>_packs</c> names it.</param>
/// <param name="Reason">The Source's <c>statusReason</c>, or null.</param>
/// <param name="RoleTitles">The <c>appliesTo</c> roles by title, in file order; an unknown role by its id.</param>
/// <param name="Cause">The cause line under the title, or null when the state needs none.</param>
/// <param name="Findings">Every finding for the Pack and its Skills, errors first.</param>
/// <param name="Hidden">Whether <c>hidden.json</c> hides it.</param>
internal sealed record PackView(
    PackEntry Pack,
    string Title,
    ItemState State,
    IReadOnlyList<string> Holders,
    string? Reason,
    IReadOnlyList<string> RoleTitles,
    string? Cause,
    IReadOnlyList<Finding> Findings,
    bool Hidden)
{
    /// <summary>The roles line: three titles, then <c>+N</c>.</summary>
    public string RolesLine => this.RoleTitles.Count <= 3
        ? string.Join(", ", this.RoleTitles)
        : $"{string.Join(", ", this.RoleTitles.Take(3))} +{(this.RoleTitles.Count - 3).ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Whether the card offers <b>Copy fix prompt</b>.</summary>
    public bool CanCopyFix => this.State is ItemState.Broken or ItemState.NotDownloaded;

    /// <summary>The Pack as the registry and the Human's roles show it now.</summary>
    /// <param name="registry">The registry.</param>
    /// <param name="pack">The Pack row.</param>
    /// <param name="roles">Every role in this Huddle, for the titles.</param>
    public static PackView From(SkillRegistry registry, PackEntry pack, IReadOnlyList<RoleInfo> roles)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(roles);
        RegistrySnapshot snapshot = registry.Current;
        ItemState state = registry.DisplayState(pack);
        IReadOnlyList<string> holders = registry.HolderNames(pack);
        string? reason = snapshot.Resolved.FindSource(pack.Source)?.File?.StatusReason;
        string title = pack.File is null || pack.Title.Length == 0 ? Path.GetFileName(pack.FilePath ?? pack.Row.LocalPath.FirstText) : pack.Title;
        IReadOnlyList<string> roleTitles = [.. pack.AppliesTo.Select(id => roles.FirstOrDefault(role => string.Equals(role.Id, id, StringComparison.Ordinal))?.Title ?? id)];
        IReadOnlyList<Finding> findings = FindingsOf(snapshot, pack);
        string? cause = CauseOf(registry, snapshot, pack, state, holders, reason, findings);
        return new PackView(pack, title, state, holders, reason, roleTitles, cause, findings, snapshot.HiddenPacks.Contains(pack.Id));
    }

    /// <summary>The Pack's own loading findings, then the checks' findings about its file and its Skills' folders, errors first.</summary>
    private static List<Finding> FindingsOf(RegistrySnapshot snapshot, PackEntry pack)
    {
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
        if (pack.FilePath is not null)
        {
            files.Add(pack.FilePath);
        }

        List<string> folders = [.. pack.SkillNames.Select(name => snapshot.Resolved.FindSkill(name)?.FolderPath).OfType<string>()];
        IEnumerable<Finding> checkedFindings = snapshot.Checked.Findings.Where(finding => files.Contains(finding.File)
            || folders.Any(folder => finding.File.StartsWith(folder, StringComparison.OrdinalIgnoreCase)));
        return [.. pack.Findings.Concat(checkedFindings).Distinct().OrderBy(static finding => finding.Severity == Severity.Error ? 0 : 1)];
    }

    /// <summary>The cause line of §6.9.4.</summary>
    private static string? CauseOf(SkillRegistry registry, RegistrySnapshot snapshot, PackEntry pack, ItemState state, IReadOnlyList<string> holders, string? reason, IReadOnlyList<Finding> findings)
    {
        switch (state)
        {
            case ItemState.Broken:
                Finding? first = findings.FirstOrDefault(static finding => finding.Severity == Severity.Error);
                if (first is null)
                {
                    return SkillStateText.For(ItemState.Broken).Sentence;
                }

                string file = Path.GetRelativePath(registry.SkillSourcesDir, first.File).Replace('\\', '/');
                return $"{file}: {SkillProblemText.Compose(first, NoEntryFor(snapshot, pack, registry.ComputerName))}";
            case ItemState.NotDownloaded:
                List<string> missing = [.. pack.SkillNames.Where(name => snapshot.Resolved.FindSkill(name) is not { PathState: SkillPathState.Present })];
                string noEntry = NoEntryFor(snapshot, pack, registry.ComputerName) is { } computer ? $" There is no entry for {computer}." : string.Empty;
                return missing.Count switch
                {
                    0 => null,
                    1 when pack.SkillNames.Count == 1 => $"{missing[0]} has no folder on this computer.{noEntry}",
                    _ => $"{missing.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} of {pack.SkillNames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} Skills have no folder here: {SkillStateText.AndList(missing)}.{noEntry}",
                };
            case ItemState.UpdateAvailable:
                List<string> changed = [.. pack.SkillNames.Where(name => snapshot.Resolved.FindSkill(name)?.HasUpdate == true)];
                return changed.Count switch
                {
                    0 => "The version Agents hold differs from this Pack file's.",
                    1 => $"{changed[0]} has new files.",
                    _ => $"{SkillStateText.AndList(changed)} have new files.",
                };
            case ItemState.Deprecated:
            case ItemState.Withdrawn:
                string why = string.IsNullOrWhiteSpace(reason) ? "No reason given." : reason.Trim();
                string seen = Agency.Huddle.App.Tasks.HumanDate.Full(DateOnly.FromDateTime(snapshot.Resolved.ReadAt.LocalDateTime));
                return $"{why} (from the Source's source.json, seen {seen})";
            case ItemState.InUse:
                return string.Join(", ", holders);
            default:
                return null;
        }
    }

    /// <summary>The computer name when one of the Pack's Skills has a per-computer path with no entry for this computer.</summary>
    private static string? NoEntryFor(RegistrySnapshot snapshot, PackEntry pack, string computer)
    {
        return pack.SkillNames.Any(name => snapshot.Resolved.FindSkill(name)?.PathState == SkillPathState.NoEntryForComputer) ? computer : null;
    }
}
