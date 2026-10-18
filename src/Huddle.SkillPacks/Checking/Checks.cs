using System.Globalization;
using System.Text;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>Runs every check once (§6.3). Huddle and <c>huddle-packs</c> call the same method, so their findings cannot
/// drift. Reads files and writes nothing.</summary>
public static class Checks
{
    /// <summary>The longest <c>consultWhenAdds</c> (P007).</summary>
    public const int MaxConsultWhen = 200;

    /// <summary>The largest NOTES.md (O004).</summary>
    public const int MaxNotesBytes = 16 * 1024;

    /// <summary>Runs every check once and returns the findings, row results, states and summary.</summary>
    /// <param name="registry">What the resolver produced.</param>
    /// <param name="context">What this Huddle knows about itself.</param>
    /// <param name="options">Strict, the path filter and the caller's version.</param>
    /// <param name="files">The same reader the resolver used.</param>
    /// <param name="cancellationToken">Cancels the run; no partial result is returned.</param>
    public static CheckResult Run(
        ResolvedRegistry registry,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(files);
        return new CheckRun(registry, context, options, files, cancellationToken).Execute();
    }

    /// <summary>Runs the Skill checks on one folder that no row reaches (<c>huddle-packs validate &lt;folder&gt;</c>): a
    /// virtual Skill, so the result has no rows and one Skill.</summary>
    public static CheckResult RunSkillFolder(
        string folder,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(files);
        return new CheckRun(ResolvedRegistry.Empty, context, options, files, cancellationToken).ExecuteFolder(folder);
    }

    /// <summary>Whether a row's resolved file is <paramref name="path"/> or under it, or the row reaches a Skill folder
    /// that is (the rows <c>validate &lt;path&gt;</c> checks, §6.4).</summary>
    public static bool RowReaches(ResolvedRegistry registry, RegistryRow row, string path)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(path);
        string? file = row.Kind == RowKind.Pack ? registry.Packs[row.Index].FilePath : registry.Catalogs[row.Index].FilePath;
        if (file is not null && IsUnder(file, path))
        {
            return true;
        }

        return registry.Skills.Any(skill => skill.FolderPath is not null
            && IsUnder(skill.FolderPath, path)
            && skill.Claims.Any(claim => claim.Kind == row.Kind && claim.RowIndex == row.Index));
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or lies under it, ignoring case.</summary>
    public static bool IsUnder(string path, string root)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(root);
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string top = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, top, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>The state of one <see cref="Checks.Run"/>.</summary>
internal sealed class CheckRun(ResolvedRegistry registry, CheckContext context, CheckOptions options, IFileReader files, CancellationToken cancellationToken)
{
    private const int RegistryLine = 0;
    private const int VirtualLine = int.MaxValue;

    private static readonly string[] LicenceFiles = ["LICENSE", "LICENSE.md", "LICENSE.txt", "COPYING"];

    private readonly List<Placed> placed = [];
    private readonly HashSet<Finding> seen = [];
    private readonly Dictionary<string, ItemCheckState> states = new(StringComparer.Ordinal);
    private readonly HashSet<RegistryRow> checkedRows = [];

    /// <summary>A finding with what it is filed under: the row line it sorts by and the item keys whose state it sets.</summary>
    private sealed record Placed(Finding Finding, int Line, string? RowKey, IReadOnlyList<string> Items);

    /// <summary>Runs the checks.</summary>
    public CheckResult Execute()
    {
        this.ChooseRows();
        this.RegistryChecks();
        this.RowChecks();
        this.SourceChecks();
        this.SkillChecks();
        this.PackAndCatalogStates();
        return this.Result();
    }

    /// <summary>The rows to check: all, or those whose resolved file is <see cref="CheckOptions.OnlyPath"/> or under it.</summary>
    private void ChooseRows()
    {
        foreach (RegistryRow row in registry.Registry.AllRows)
        {
            if (options.OnlyPath is null || Checks.RowReaches(registry, row, options.OnlyPath))
            {
                this.checkedRows.Add(row);
            }
        }
    }

    /// <summary>Runs the Skill checks on one folder, as a virtual Skill.</summary>
    public CheckResult ExecuteFolder(string folder)
    {
        string name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        SkillEntry skill = new(name, string.Empty, null, null, null, null, [], string.Empty, null, folder, [], null, null, [], SkillPathState.Present, []);
        string key = skill.StateKey;
        this.states[key] = ItemCheckState.Usable;
        this.FolderChecks(skill, folder, null, key);
        this.PackAndCatalogStates();
        CheckResult result = this.Result();
        return result with { Summary = result.Summary with { Skills = 1 } };
    }

    /// <summary>The resolved file of a row.</summary>
    private string? FileOf(RegistryRow row)
    {
        return row.Kind == RowKind.Pack ? registry.Packs[row.Index].FilePath : registry.Catalogs[row.Index].FilePath;
    }

    /// <summary>The state key of a row's item.</summary>
    private string KeyOf(RegistryRow row)
    {
        return row.Kind == RowKind.Pack ? registry.Packs[row.Index].StateKey : registry.Catalogs[row.Index].StateKey;
    }

    /// <summary>The line a row sorts by.</summary>
    private static int LineOf(RegistryRow row)
    {
        return row.IsVirtual ? VirtualLine : row.Span.FirstLine;
    }

    /// <summary>The row at a table and index.</summary>
    private RegistryRow RowAt(RowKind kind, int index)
    {
        return kind == RowKind.Pack ? registry.Registry.Packs[index] : registry.Registry.Catalogs[index];
    }

    /// <summary>Files a finding once, under a row (or the registry) and the items whose state it sets.</summary>
    private void Add(Finding finding, RegistryRow? row, params string[] items)
    {
        if (row is not null && !this.checkedRows.Contains(row))
        {
            return;
        }

        Finding adjusted = options.Strict && finding.Code is "R009" or "S001" ? finding with { Severity = Severity.Error } : finding;
        if (!this.seen.Add(adjusted))
        {
            return;
        }

        this.placed.Add(new Placed(adjusted, row is null ? RegistryLine : LineOf(row), row is null ? null : this.KeyOf(row), items));
    }

    /// <summary>Registry-level findings: the reader's, X002 and R008.</summary>
    private void RegistryChecks()
    {
        HashSet<Finding> rowFindings = [.. registry.Registry.FindingsOfRows.Select(static item => item.Finding)];
        foreach (Finding finding in registry.Registry.Findings.Where(finding => !rowFindings.Contains(finding)))
        {
            this.Add(finding, null);
        }

        foreach (RowFinding item in registry.Registry.FindingsOfRows)
        {
            RegistryRow row = this.RowAt(item.Kind, item.Index);
            this.Add(item.Finding, row, this.KeyOf(row));
        }

        string file = registry.Registry.FilePath;
        if (options.ToolVersion is not null && !string.Equals(options.ToolVersion, context.HuddleVersion, StringComparison.Ordinal))
        {
            this.Add(Finding.Warning("X002", file, $"huddle-packs {options.ToolVersion} is checking data written by Huddle {context.HuddleVersion}; use the command line in InstallSkillPack.md."), null);
        }

        if (options.OnlyPath is null)
        {
            foreach (RoleInfo role in context.Roles)
            {
                foreach (string packId in role.SuggestedPacks.Where(id => registry.FindPack(id) is null))
                {
                    this.Add(Finding.Warning("R008", file, $"Role \"{role.Id}\" suggests Pack \"{packId}\", which no Packs row provides."), null);
                }
            }
        }
    }

    /// <summary>Per-row findings: the loading findings, R003 to R005, R009, P004 to P009 and D004, D005, X001.</summary>
    private void RowChecks()
    {
        Dictionary<string, RegistryRow> byFile = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> packIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> catalogIds = new(StringComparer.Ordinal);
        foreach (RegistryRow row in registry.Registry.AllRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = this.KeyOf(row);
            string? path = this.FileOf(row);
            IReadOnlyList<Finding> loading = row.Kind == RowKind.Pack ? registry.Packs[row.Index].Findings : registry.Catalogs[row.Index].Findings;
            foreach (Finding finding in loading)
            {
                this.Add(finding, row, key);
            }

            string where = row.IsVirtual ? path ?? row.LocalPath.FirstText : string.Create(CultureInfo.InvariantCulture, $"{row.TableName} row {row.Index + 1}");
            string rowFile = row.IsVirtual ? path ?? registry.Registry.FilePath : registry.Registry.FilePath;
            if (!row.LocalPath.IsEmpty && path is null && !row.LocalPath.HasEntryFor(context.ComputerName))
            {
                this.Add(Finding.Warning("R009", rowFile, $"{where}: no entry for this computer ({context.ComputerName}) and no plain entry."), row, key);
            }

            if (path is not null && SafeExists(path))
            {
                string suffix = row.Kind == RowKind.Pack ? PackFile.Suffix : CatalogFile.Suffix;
                if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    this.Add(Finding.Error("R005", rowFile, $"{where}: {Path.GetFileName(path)} is not a {suffix} file."), row, key);
                }
                else if (byFile.TryGetValue(path, out RegistryRow? earlier))
                {
                    this.Add(Finding.Error("R004", rowFile, $"{where} points at the same file as {earlier.TableName} row {earlier.Index + 1}."), row, key);
                }
                else
                {
                    byFile[path] = row;
                }
            }

            if (row.Kind == RowKind.Pack)
            {
                this.PackChecks(row, registry.Packs[row.Index], packIds);
            }
            else
            {
                this.CatalogChecks(row, registry.Catalogs[row.Index], catalogIds);
            }
        }
    }

    /// <summary>P004 to P007, P009 (catalog part), D004 and X001 for one Pack.</summary>
    private void PackChecks(RegistryRow row, PackEntry pack, HashSet<string> ids)
    {
        if (pack.File is not { } file || pack.FilePath is not { } path)
        {
            return;
        }

        string key = pack.StateKey;
        if (pack.DuplicateId || !ids.Add(pack.Id))
        {
            this.Add(Finding.Error("P004", path, $"Another Pack already uses the id \"{pack.Id}\"."), row, key);
            return;
        }

        HashSet<string> roles = new(context.Roles.Select(static role => role.Id), StringComparer.OrdinalIgnoreCase);
        foreach (string role in file.AppliesTo.Where(role => !roles.Contains(role)))
        {
            this.Add(Finding.Error("P005", path, $"appliesTo: \"{role}\" is not a role in this Huddle; see huddle-packs roles"), row, key);
        }

        if (file.AppliesTo.Count == 0)
        {
            this.Add(Finding.Warning("P006", path, "appliesTo is empty, so no Teammate card will offer this Pack."), row, key);
        }

        if (string.IsNullOrWhiteSpace(file.ConsultWhenAdds))
        {
            this.Add(Finding.Warning("P007", path, "consultWhenAdds is empty."), row, key);
        }
        else if (file.ConsultWhenAdds.Length > Checks.MaxConsultWhen)
        {
            this.Add(Finding.Warning("P007", path, string.Create(CultureInfo.InvariantCulture, $"consultWhenAdds is {file.ConsultWhenAdds.Length} characters; the limit is {Checks.MaxConsultWhen}.")), row, key);
        }

        foreach (PackSkill skill in file.Skills)
        {
            if (skill.Catalog is not null)
            {
                ResolvedCatalog? catalog = registry.FindCatalog(skill.Catalog);
                if (catalog?.File is null)
                {
                    this.Add(Finding.Error("P009", path, $"Skill \"{skill.Name}\": no catalog \"{skill.Catalog}\" in SKILLS.md."), row, key);
                }
                else if (catalog.File.Find(skill.Name) is null)
                {
                    this.Add(Finding.Error("P009", path, $"Skill \"{skill.Name}\": catalog \"{skill.Catalog}\" has no Skill of that name."), row, key);
                }
            }
            else if (!string.IsNullOrWhiteSpace(skill.Origin) && !OriginIdentity.IsPinned(skill.Origin))
            {
                this.Add(Finding.Warning("D004", path, $"Skill \"{skill.Name}\": origin {skill.Origin} is not pinned to a commit."), row, key);
            }

            if (skill.Path is not null && skill.Path.HasEntryFor(context.ComputerName) && skill.Path.Resolve(context.ComputerName) is null)
            {
                this.Add(Finding.Warning("S001", path, $"Skill \"{skill.Name}\" has an empty path: it is not fetched on this computer yet."), row, key, $"skill:{skill.Name}");
            }
        }

        this.ScanSecrets(path, row, key);
    }

    /// <summary>D005, D004 and X001 for one catalog.</summary>
    private void CatalogChecks(RegistryRow row, ResolvedCatalog catalog, HashSet<string> ids)
    {
        if (catalog.File is not { } file || catalog.FilePath is not { } path)
        {
            return;
        }

        string key = catalog.StateKey;
        if (catalog.DuplicateId || !ids.Add(catalog.Id))
        {
            this.Add(Finding.Error("D005", path, $"Another catalog already uses the id \"{catalog.Id}\"."), row, key);
            return;
        }

        foreach (CatalogEntry entry in file.Skills.Where(static entry => !OriginIdentity.IsPinned(entry.Origin)))
        {
            this.Add(Finding.Warning("D004", path, $"Skill \"{entry.Name}\": origin {entry.Origin} is not pinned to a commit."), row, key);
        }

        this.ScanSecrets(path, row, key);
    }

    /// <summary>O001 to O005 and X001 for each Source; filed under the first row that names it.</summary>
    private void SourceChecks()
    {
        foreach (ResolvedSource source in registry.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegistryRow? row = this.FirstRowOfSource(source.Id);
            string[] items = [.. this.ItemsOfSource(source.Id)];
            foreach (Finding finding in source.Findings)
            {
                this.Add(finding, row, items);
            }

            if (source.Folder is null)
            {
                continue;
            }

            string sourcePath = Path.Combine(source.Folder, SourceFile.FileName);
            if (source.File is null && source.Findings.Count == 0 && !registry.Retry.Contains(sourcePath, StringComparer.OrdinalIgnoreCase))
            {
                this.Add(Finding.Error("O001", sourcePath, $"Source \"{source.Id}\" has no source.json."), row, items);
            }

            if (source.File is { Kind: SourceKind.Github or SourceKind.Git, Commit: null } && this.SourceHasUnpinnedEntry(source.Id))
            {
                this.Add(Finding.Warning("O002", sourcePath, $"Source \"{source.Id}\" has no commit, and some of its entries are not pinned."), row);
            }

            string notes = Path.Combine(source.Folder, "NOTES.md");
            if (!source.HasNotes)
            {
                this.Add(Finding.Warning("O004", notes, $"Source \"{source.Id}\" has no NOTES.md."), row);
            }
            else
            {
                this.NotesChecks(notes, row);
            }

            if (source.File is not null)
            {
                this.ScanSecrets(sourcePath, row, items);
            }
        }
    }

    /// <summary>O004 (size), O005 and X001 for one NOTES.md.</summary>
    private void NotesChecks(string notes, RegistryRow? row)
    {
        try
        {
            if (files.Length(notes) > Checks.MaxNotesBytes)
            {
                this.Add(Finding.Warning("O004", notes, "NOTES.md is over 16 KB."), row);
            }

            string text = files.ReadAllText(notes);
            if (SkillFrontmatter.Get(SkillFrontmatter.Parse(text), "layout") is null)
            {
                this.Add(Finding.Warning("O005", notes, "NOTES.md has no front matter naming its layout."), row);
            }

            this.ScanSecretsText(notes, text, row);
        }
        catch (IOException exception)
        {
            this.Add(Finding.Warning("O004", notes, $"cannot read: {exception.Message}"), row);
        }
    }

    /// <summary>The first row, in file order, whose file names the Source.</summary>
    private RegistryRow? FirstRowOfSource(string sourceId)
    {
        return registry.Registry.AllRows.FirstOrDefault(row => row.Kind == RowKind.Pack
            ? registry.Packs[row.Index].File is not null && string.Equals(registry.Packs[row.Index].Source, sourceId, StringComparison.Ordinal)
            : registry.Catalogs[row.Index].File is not null && string.Equals(registry.Catalogs[row.Index].Source, sourceId, StringComparison.Ordinal));
    }

    /// <summary>The state keys of the Packs, catalogs and Skills a Source's error makes Broken.</summary>
    private IEnumerable<string> ItemsOfSource(string sourceId)
    {
        foreach (PackEntry pack in registry.PacksOfSource(sourceId).Where(static pack => pack.File is not null))
        {
            yield return pack.StateKey;
        }

        foreach (ResolvedCatalog catalog in registry.CatalogsOfSource(sourceId).Where(static catalog => catalog.File is not null))
        {
            yield return catalog.StateKey;
        }
    }

    /// <summary>Whether some pack or catalog entry naming the Source fails D004.</summary>
    private bool SourceHasUnpinnedEntry(string sourceId)
    {
        bool packs = registry.PacksOfSource(sourceId).Any(static pack => pack.File?.Skills.Any(static skill => skill.Catalog is null && !string.IsNullOrWhiteSpace(skill.Origin) && !OriginIdentity.IsPinned(skill.Origin)) == true);
        bool catalogs = registry.CatalogsOfSource(sourceId).Any(static catalog => catalog.File?.Skills.Any(static entry => !OriginIdentity.IsPinned(entry.Origin)) == true);
        return packs || catalogs;
    }

    /// <summary>The Skill checks, once per Skill, filed under the first row that reaches it.</summary>
    private void SkillChecks()
    {
        foreach (SkillEntry skill in registry.Skills)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkillClaim first = skill.Claims[0];
            RegistryRow row = this.RowAt(first.Kind, first.RowIndex);
            if (!skill.Claims.Any(claim => this.checkedRows.Contains(this.RowAt(claim.Kind, claim.RowIndex))))
            {
                continue;
            }

            string key = skill.StateKey;
            foreach (SkillClaim later in skill.Claims.Skip(1).Where(claim => !string.Equals(claim.Origin, first.Origin, StringComparison.Ordinal)))
            {
                RegistryRow laterRow = this.RowAt(later.Kind, later.RowIndex);
                string laterKey = later.Kind == RowKind.Pack ? registry.Packs[later.RowIndex].StateKey : registry.Catalogs[later.RowIndex].StateKey;
                this.Add(Finding.Error("D003", later.ProviderFile, $"Skill \"{skill.Name}\": origin {later.Origin} differs from {first.Origin} in {first.Provider}; the first row keeps the name."), laterRow, laterKey);
            }

            foreach (Finding finding in skill.Findings)
            {
                this.Add(finding, row, key);
            }

            switch (skill.PathState)
            {
                case SkillPathState.Missing:
                    this.Add(Finding.Warning("S001", skill.FolderPath ?? first.ProviderFile, $"Skill \"{skill.Name}\": {skill.FolderPath} is not a folder on this computer."), row, key);
                    break;
                case SkillPathState.NoEntryForComputer:
                    this.Add(Finding.Warning("R009", first.ProviderFile, $"Skill \"{skill.Name}\": no path for this computer ({context.ComputerName}) and no \"*\" entry."), row, key);
                    break;
                case SkillPathState.Present when skill.FolderPath is not null:
                    this.FolderChecks(skill, skill.FolderPath, row, key);
                    break;
            }

            this.states[key] = skill.PathState == SkillPathState.Present ? ItemCheckState.Usable : ItemCheckState.NotDownloaded;
        }
    }

    /// <summary>S002 to S012 and X001 on a Skill folder.</summary>
    private void FolderChecks(SkillEntry skill, string folder, RegistryRow? row, string key)
    {
        IReadOnlyList<string> all;
        try
        {
            all = [.. files.EnumerateFiles(folder, recursive: true).Where(file => !IsIgnored(folder, file))];
        }
        catch (IOException exception)
        {
            this.Add(Finding.Error("S002", folder, $"cannot read: {exception.Message}"), row, key);
            return;
        }

        foreach (FolderViolation violation in SkillFolderRules.Check(files, folder))
        {
            this.Add(Finding.Error(violation.Limit == FolderLimit.Size ? "S003" : "S004", violation.Path, violation.Message), row, key);
        }

        Dictionary<string, string> topLevel = new(StringComparer.Ordinal);
        Dictionary<string, byte[]> bytes = new(StringComparer.Ordinal);
        List<string> unread = [];
        foreach (string file in all)
        {
            string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            byte[] content;
            try
            {
                content = files.Length(file) <= SecretScanner.MaxBytes ? files.ReadAllBytes(file) : [];
            }
            catch (IOException exception)
            {
                this.Add(Finding.Error("S002", file, $"cannot read: {exception.Message}"), row, key);
                continue;
            }

            bytes[relative] = content;
            bool isTopMarkdown = !relative.Contains('/', StringComparison.Ordinal) && relative.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
            if (isTopMarkdown)
            {
                topLevel[relative] = Encoding.UTF8.GetString(content);
            }
            else
            {
                unread.Add(relative);
            }

            if (content.Length > 0 && !SecretScanner.IsBinary(content))
            {
                this.ScanSecretsText(file, Encoding.UTF8.GetString(content), row, key);
            }
        }

        SkillFolderValidation validation = SkillFolderValidator.Validate(skill.Name, topLevel, new HashSet<string>(StringComparer.Ordinal));
        foreach (FolderIssue issue in validation.Issues)
        {
            switch (issue.Rule)
            {
                case FolderRule.NoSkillMd or FolderRule.Name or FolderRule.Description:
                    this.Add(Finding.Error("S002", Path.Combine(folder, "SKILL.md"), issue.Message), row, key);
                    break;
                case FolderRule.LongDescription:
                    this.Add(Finding.Warning("S012", Path.Combine(folder, "SKILL.md"), issue.Message), row, key);
                    break;
                case FolderRule.LargeFile:
                    this.Add(Finding.Warning("S007", Path.Combine(folder, issue.Message.Split('\'')[1]), issue.Message.Replace("(skipped)", "(Teammates skip it)", StringComparison.Ordinal)), row, key);
                    break;
            }
        }

        HashSet<string> appTools = new(context.AppToolNames, StringComparer.OrdinalIgnoreCase);
        foreach (string tool in validation.AllTools.Where(appTools.Contains))
        {
            this.Add(Finding.Error("S011", Path.Combine(folder, "SKILL.md"), $"tools: '{tool}' is an App Tool; only Huddle's built-in Skills can grant it."), row, key);
        }

        if (context.InstalledSkills.Any(installed => installed.BuiltIn && string.Equals(installed.Name, skill.Name, StringComparison.Ordinal)))
        {
            this.Add(Finding.Error("S005", folder, $"\"{skill.Name}\" is the name of a Skill that ships with Huddle."), row, key);
        }

        InstalledSkill? other = context.InstalledSkills.FirstOrDefault(installed => !installed.BuiltIn
            && string.Equals(installed.Name, skill.Name, StringComparison.Ordinal)
            && installed.Origin is not null
            && skill.Origin.Length > 0
            && !OriginIdentity.Same(installed.Origin, skill.Origin));
        if (other is not null)
        {
            this.Add(Finding.Error("S006", folder, $"Skills/{skill.Name} came from {other.Origin}, not {skill.Origin}."), row, key);
        }

        if (unread.Count > 0)
        {
            string firstUnread = unread[0].Contains('/', StringComparison.Ordinal) ? unread[0][..(unread[0].IndexOf('/', StringComparison.Ordinal) + 1)] : unread[0];
            int covered = firstUnread.EndsWith('/')
                ? unread.Count(item => item.StartsWith(firstUnread, StringComparison.Ordinal))
                : 1;
            int others = unread.Count - covered;
            string message = others == 0 ? $"{firstUnread} is never read by Teammates." : string.Create(CultureInfo.InvariantCulture, $"{firstUnread} and {others} other files are never read by Teammates.");
            this.Add(Finding.Warning("S008", folder, message), row, key);
        }

        this.LicenceCheck(skill, folder, validation, row, key);
        this.ReviewCheck(bytes, folder, row, key);
    }

    /// <summary>Whether a file is skipped by every check: <c>.source.json</c> and anything in a <c>.git</c> folder.</summary>
    private static bool IsIgnored(string folder, string file)
    {
        string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
        return string.Equals(relative, RegistryResolver.ProvenanceFileName, StringComparison.Ordinal)
            || relative.StartsWith(".git/", StringComparison.Ordinal)
            || relative.Contains("/.git/", StringComparison.Ordinal);
    }

    /// <summary>S009: the effective licence, in order catalog entry, SKILL.md, a licence file (§6.3, "S009 in full").</summary>
    private void LicenceCheck(SkillEntry skill, string folder, SkillFolderValidation validation, RegistryRow? row, string key)
    {
        string? catalogLicence = skill.Claims.Select(static claim => claim.License).FirstOrDefault(static licence => !string.IsNullOrWhiteSpace(licence));
        string? frontmatterLicence = SkillFrontmatter.Get(validation.Fields, "license");
        string? licenceFile = LicenceFiles.FirstOrDefault(name => SafeExists(Path.Combine(folder, name)));
        string? effective = catalogLicence ?? frontmatterLicence ?? (licenceFile is null ? null : $"see {licenceFile}");
        string? message = null;
        if (effective is null)
        {
            message = "no licence found";
        }
        else if (effective.StartsWith("Proprietary", StringComparison.OrdinalIgnoreCase))
        {
            message = licenceFile is null ? "licence is Proprietary" : $"licence is Proprietary ({licenceFile})";
        }
        else if (catalogLicence is not null && frontmatterLicence is not null && !string.Equals(catalogLicence, frontmatterLicence, StringComparison.OrdinalIgnoreCase))
        {
            message = $"licence {frontmatterLicence} in SKILL.md differs from {catalogLicence} in the catalog";
        }

        if (message is not null)
        {
            this.Add(Finding.Warning("S009", folder, $"Skill \"{skill.Name}\": {message}."), row, key);
        }
    }

    /// <summary>S010: one finding per kind of review flag, from the unchanged scanner.</summary>
    private void ReviewCheck(Dictionary<string, byte[]> bytes, string folder, RegistryRow? row, string key)
    {
        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(bytes, "declared");
        foreach (IGrouping<ReviewFlagKind, ReviewFlag> group in flags.Where(static flag => flag.Kind != ReviewFlagKind.NoLicense).GroupBy(static flag => flag.Kind))
        {
            string what = group.Key switch
            {
                ReviewFlagKind.Script => "ships scripts",
                ReviewFlagKind.ShellCommand => "runs shell commands",
                ReviewFlagKind.NetworkCall => "makes network calls",
                _ => "names paths outside the Work Dir",
            };
            List<ReviewFlag> list = [.. group];
            string first = list[0].File;
            string more = list.Count > 1 ? string.Create(CultureInfo.InvariantCulture, $" and {list.Count - 1} more") : string.Empty;
            this.Add(Finding.Warning("S010", folder, $"{what}: {first}{more}"), row, key);
        }
    }

    /// <summary>X001 on a file, read through the reader.</summary>
    private void ScanSecrets(string path, RegistryRow? row, params string[] items)
    {
        try
        {
            if (files.Length(path) > SecretScanner.MaxBytes)
            {
                return;
            }

            byte[] content = files.ReadAllBytes(path);
            if (!SecretScanner.IsBinary(content))
            {
                this.ScanSecretsText(path, Encoding.UTF8.GetString(content), row, items);
            }
        }
        catch (IOException)
        {
            // The file's own loading finding already says it cannot be read.
        }
    }

    /// <summary>X001 on text already read.</summary>
    private void ScanSecretsText(string path, string text, RegistryRow? row, params string[] items)
    {
        foreach (SecretHit hit in SecretScanner.Scan(text))
        {
            this.Add(Finding.Error("X001", path, SecretScanner.Describe(hit)), row, items);
        }
    }

    /// <summary>Whether a file exists, treating a reader that throws as "no".</summary>
    private bool SafeExists(string path)
    {
        try
        {
            return files.FileExists(path);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Applies every finding to the states it sets, then derives each Pack's and catalog's state (§8.3 step 6).</summary>
    private void PackAndCatalogStates()
    {
        Dictionary<string, ItemCheckState> fromFindings = new(StringComparer.Ordinal);
        foreach (Placed item in this.placed)
        {
            ItemCheckState? caused = Causes(item.Finding);
            if (caused is not { } state)
            {
                continue;
            }

            foreach (string key in item.Items)
            {
                fromFindings[key] = Worst(fromFindings.GetValueOrDefault(key, ItemCheckState.Usable), state);
            }
        }

        foreach ((string key, ItemCheckState state) in fromFindings.Where(pair => pair.Key.StartsWith("skill:", StringComparison.Ordinal)))
        {
            this.states[key] = Worst(this.states.GetValueOrDefault(key, ItemCheckState.Usable), state);
        }

        foreach (PackEntry pack in registry.Packs)
        {
            ItemCheckState state = this.RowState(pack.Row, pack.FilePath, pack.File is not null, fromFindings.GetValueOrDefault(pack.StateKey, ItemCheckState.Usable));
            if (pack.File is not null)
            {
                foreach (string name in pack.File.Skills.Select(static skill => skill.Name))
                {
                    state = Worst(state, this.states.GetValueOrDefault($"skill:{name}", ItemCheckState.Broken));
                }
            }

            this.states[pack.StateKey] = Worst(this.states.GetValueOrDefault(pack.StateKey, ItemCheckState.Usable), state);
        }

        foreach (ResolvedCatalog catalog in registry.Catalogs)
        {
            ItemCheckState state = this.RowState(catalog.Row, catalog.FilePath, catalog.File is not null, fromFindings.GetValueOrDefault(catalog.StateKey, ItemCheckState.Usable));
            this.states[catalog.StateKey] = Worst(this.states.GetValueOrDefault(catalog.StateKey, ItemCheckState.Usable), state);
        }
    }

    /// <summary>The state a row's file leaves its item in, before its Skills are counted.</summary>
    private ItemCheckState RowState(RegistryRow row, string? path, bool loaded, ItemCheckState fromFindings)
    {
        if (row.LocalPath.IsEmpty || row.LocalPath.HasDuplicateComputer || row.LocalPath.HasTwoPlainEntries)
        {
            return ItemCheckState.Broken;
        }

        if (path is null)
        {
            return Worst(ItemCheckState.NotDownloaded, fromFindings);
        }

        if (!loaded)
        {
            return registry.Retry.Contains(path, StringComparer.OrdinalIgnoreCase) ? Worst(ItemCheckState.NotDownloaded, fromFindings) : ItemCheckState.Broken;
        }

        return fromFindings;
    }

    /// <summary>The state a finding causes: R009 and S001 make Not downloaded; any other error makes Broken.</summary>
    private static ItemCheckState? Causes(Finding finding)
    {
        if (finding.Code is "R009" or "S001")
        {
            return ItemCheckState.NotDownloaded;
        }

        return finding.Severity == Severity.Error ? ItemCheckState.Broken : null;
    }

    /// <summary>The worse of two states: Broken over Not downloaded over Usable.</summary>
    private static ItemCheckState Worst(ItemCheckState first, ItemCheckState second)
    {
        return (ItemCheckState)Math.Max((int)first, (int)second);
    }

    /// <summary>Orders the findings, counts per row and builds the summary.</summary>
    private CheckResult Result()
    {
        List<Placed> ordered = [.. this.placed
            .OrderBy(static item => item.Line)
            .ThenBy(static item => item.Finding.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Finding.Code, StringComparer.Ordinal)
            .ThenBy(static item => item.Finding.Message, StringComparer.Ordinal)];

        List<RowCheck> rows = [];
        HashSet<string> reached = new(StringComparer.Ordinal);
        foreach (RegistryRow row in registry.Registry.AllRows.Where(this.checkedRows.Contains))
        {
            string key = this.KeyOf(row);
            List<Placed> mine = [.. ordered.Where(item => string.Equals(item.RowKey, key, StringComparison.Ordinal) && item.Line == LineOf(row))];
            string shown = this.FileOf(row) ?? row.LocalPath.FirstText;
            rows.Add(new RowCheck(
                row.Kind,
                row.Index,
                shown,
                row.IsVirtual ? 0 : row.Span.FirstLine,
                this.states.GetValueOrDefault(key, ItemCheckState.Usable),
                mine.Count(static item => item.Finding.Severity == Severity.Error),
                mine.Count(static item => item.Finding.Severity == Severity.Warning)));

            foreach (SkillEntry skill in registry.Skills.Where(skill => skill.Claims.Any(claim => claim.Kind == row.Kind && claim.RowIndex == row.Index)))
            {
                reached.Add(skill.Name);
            }
        }

        List<Finding> findings = [.. ordered.Select(static item => item.Finding)];
        CheckSummary summary = new(
            rows.Count,
            rows.Count(static row => row.Kind == RowKind.Pack),
            reached.Count,
            findings.Count(static finding => finding.Severity == Severity.Error),
            findings.Count(static finding => finding.Severity == Severity.Warning));
        return new CheckResult(findings, rows, new Dictionary<string, ItemCheckState>(this.states, StringComparer.Ordinal), summary);
    }
}
