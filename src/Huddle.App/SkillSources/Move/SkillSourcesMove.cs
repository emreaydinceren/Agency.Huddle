using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Reading;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>
/// Moves today's Packs, catalog and imported Skills into the Skill Sources folder, once, on the first start of this version
/// (§6.13). It writes exactly the files an AI tool following <c>InstallSkillPack.md</c> would have written: everything is staged
/// in <c>SkillSources/.move-&lt;random&gt;/</c>, placed, and SKILLS.md last. It changes no Agent, writes nothing in
/// <c>Skills/</c>, and opens no network connection.
/// </summary>
internal sealed partial class SkillSourcesMove : ISkillSourcesMove
{
    /// <summary>The two Packs Huddle used to ship, in row order.</summary>
    public static readonly IReadOnlyList<string> ShippedPacks = ["msbuild-authoring", "code-review-and-quality"];

    /// <summary>How old a <c>.move-*</c> folder must be before it is treated as abandoned.</summary>
    public static readonly TimeSpan StaleStaging = TimeSpan.FromMinutes(10);

    private const string SkillsRepoSource = "local/skills";
    private const string MyPacksSource = "local/my-packs";
    private const string GitHubPrefix = "https://github.com/";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly TeamOptions options;
    private readonly SkillSourcesSettings settings;
    private readonly IMoveFiles files;
    private readonly MoveReportStore reports;
    private readonly SourceLog log;
    private readonly TimeProvider clock;
    private readonly ILogger<SkillSourcesMove> logger;

    /// <summary>Creates the move. It has no parameter that could reach the network.</summary>
    public SkillSourcesMove(IOptions<TeamOptions> options, SkillSourcesSettings settings, IMoveFiles files, MoveReportStore reports, SourceLog log, ILogger<SkillSourcesMove> logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
        this.settings = settings;
        this.files = files;
        this.reports = reports;
        this.log = log;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    async Task ISkillSourcesMove.RunAsync(CancellationToken ct)
    {
        _ = await this.RunAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Runs the move unless SKILLS.md exists.</summary>
    /// <returns>The report, or null when the move did not run (or another Huddle won the race).</returns>
    public async Task<MoveReport?> RunAsync(CancellationToken ct)
    {
        string dir = this.settings.Dir;
        string skillsMd = Path.Combine(dir, RegistryReader.FileName);
        this.CleanStaleStaging(dir);
        if (File.Exists(skillsMd))
        {
            this.logger.LogDebug("SKILLS.md exists; move skipped");
            return null;
        }

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation("Moving Packs and Skills into {Dir}", dir);
        }
        string staging = Path.Combine(dir, $".move-{Guid.NewGuid().ToString("N")[..8]}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "sources", "local", "my-packs"));
            Plan plan = await Task.Run(() => this.Stage(staging, ct), ct).ConfigureAwait(false);
            if (!this.Place(staging, dir, skillsMd))
            {
                return null;
            }

            MoveReport report = plan.Report(this.clock.GetLocalNow());
            this.reports.Save(report);
            foreach (MoveProblem problem in report.Problems)
            {
                this.logger.LogWarning("Move: {Subject}: {Message}", problem.Subject, problem.Message);
            }

            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.logger.LogInformation(
                    "Moved {Catalogs} catalogs ({Skills} Skills, {Omitted} omitted), {Packs} Pack rows, {Pointed} Skills pointed at, {Left} left in place",
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                    report.Catalogs,
                    report.CatalogSkills,
                    report.OmittedDuplicates,
                    report.ShippedPackRows + report.UserPacksMoved,
                    report.SkillsPointedAt,
                    report.SkillsLeftInPlace);
#pragma warning restore HAA0601
            }
            return report;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            this.logger.LogError(exception, "The move to {Dir} failed; it runs again at the next start.", dir);
            MoveReport failed = MoveReport.Failure(this.clock.GetLocalNow(), [new MoveProblem(dir, exception.Message)]);
            TrySave(this.reports, failed);
            return failed;
        }
        finally
        {
            TryDelete(staging);
        }
    }

    /// <summary>Deletes every <c>.move-*</c> folder older than ten minutes; a younger one may belong to a running Huddle.</summary>
    private void CleanStaleStaging(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string folder in Directory.GetDirectories(dir, ".move-*"))
        {
            if (this.clock.GetUtcNow() - Directory.GetLastWriteTimeUtc(folder) > StaleStaging)
            {
                TryDelete(folder);
            }
        }
    }

    /// <summary>Builds the whole result under <paramref name="staging"/> (§6.13 steps 1 to 6).</summary>
    private Plan Stage(string staging, CancellationToken ct)
    {
        Plan plan = new(staging, this.clock.GetLocalNow(), this.log);
        IReadOnlyList<LegacyCatalogEntry> catalog = this.ReadCatalog(plan);
        List<ImportedSkill> imported = this.ReadImported();
        ct.ThrowIfCancellationRequested();

        Dictionary<string, string> claims = new(StringComparer.Ordinal);
        foreach (ImportedSkill skill in imported)
        {
            claims.TryAdd(skill.Name, LegacyCatalogReader.RepoPathKey(skill.Provenance.Repo, skill.Provenance.Path));
        }

        List<CatalogBuild> catalogs = [.. catalog.GroupBy(static entry => entry.Repo.Trim().TrimEnd('/'), StringComparer.OrdinalIgnoreCase)
            .Select(static group => new CatalogBuild(group.Key, [.. group]))
            .OrderByDescending(static build => build.Legacy.Count)
            .ThenBy(static build => build.Id, StringComparer.Ordinal)];
        UniqueIds(catalogs);
        foreach (CatalogBuild build in catalogs)
        {
            foreach (LegacyCatalogEntry entry in build.Legacy)
            {
                string key = LegacyCatalogReader.RepoPathKey(entry.Repo, entry.Path);
                if (claims.TryGetValue(entry.Name, out string? owner) && !string.Equals(owner, key, StringComparison.Ordinal))
                {
                    string otherCatalog = catalogs.FirstOrDefault(other => other.Legacy.Any(e => string.Equals(LegacyCatalogReader.RepoPathKey(e.Repo, e.Path), owner, StringComparison.Ordinal)))?.Id ?? "Skills/";
                    build.Omitted.Add((entry.Name, otherCatalog));
                    plan.Line(build.SourceId, "duplicate-omitted", $"skill:{entry.Name}", $"'{entry.Name}' not listed: the name belongs to {otherCatalog}.");
                    plan.OmittedDuplicates++;
                    continue;
                }

                claims.TryAdd(entry.Name, key);
                build.Entries.Add(MapEntry(entry));
            }
        }

        MoveImported(plan, imported, catalogs, ct);
        foreach (CatalogBuild build in catalogs)
        {
            this.WriteCatalog(plan, build);
        }

        List<TemplateRow> packRows = [];
        HashSet<string> userIds = this.MoveUserPacks(plan, catalogs, imported, packRows);
        packRows.InsertRange(0, this.ShippedRows(plan, userIds));
        List<TemplateRow> catalogRows = [.. catalogs.Where(static build => build.Entries.Count > 0)
            .OrderByDescending(static build => build.Entries.Count)
            .ThenBy(static build => build.Id, StringComparer.Ordinal)
            .Select(build => new TemplateRow($"catalogs/{build.Id}.skillcatalog.json", build.Repo, plan.Date, build.Notes()))];
        plan.Write(RegistryReader.FileName, SkillsMdTemplate.Build(packRows, catalogRows));
        foreach (string source in plan.SourcesWritten)
        {
            plan.Line(source, "completed", "file:SKILLS.md", "Move finished.");
        }

        plan.FlushLogs();
        return plan;
    }

    /// <summary>The catalog to split: the Human's <c>SkillsCatalog.json</c> when it parses, else the embedded one.</summary>
    private IReadOnlyList<LegacyCatalogEntry> ReadCatalog(Plan plan)
    {
        string user = Path.Combine(this.settings.DataDir, TeamPaths.SkillsCatalogFile);
        if (File.Exists(user))
        {
            try
            {
                if (LegacyCatalogReader.Parse(File.ReadAllText(user)) is { } entries)
                {
                    return entries;
                }
            }
            catch (IOException)
            {
                // Read failure is reported below, exactly like a malformed file.
            }

            plan.Problems.Add(new MoveProblem(user, $"{user} could not be read, so the catalog Huddle shipped was used."));
        }

        return LegacyCatalogReader.ReadEmbedded();
    }

    /// <summary>The folders in <c>Skills/</c> with a valid <c>.source.json</c>, by name.</summary>
    private List<ImportedSkill> ReadImported()
    {
        string skillsDir = this.settings.SkillsDir;
        if (!Directory.Exists(skillsDir))
        {
            return [];
        }

        List<ImportedSkill> result = [];
        foreach (string folder in Directory.GetDirectories(skillsDir).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(folder);
            if (name.StartsWith('.') || !SkillProvenance.TryRead(folder, out SkillProvenance? provenance) || provenance is null)
            {
                continue;
            }

            result.Add(new ImportedSkill(name, folder, provenance));
        }

        return result;
    }

    /// <summary>Step 4: copy each unmodified imported Skill to <c>fetched/</c> and point its catalog entry at it.</summary>
    private static void MoveImported(Plan plan, List<ImportedSkill> imported, List<CatalogBuild> catalogs, CancellationToken ct)
    {
        foreach (ImportedSkill skill in imported)
        {
            ct.ThrowIfCancellationRequested();
            string repo = skill.Provenance.Repo.Trim().TrimEnd('/');
            CatalogBuild build = catalogs.FirstOrDefault(other => string.Equals(other.Repo, repo, StringComparison.OrdinalIgnoreCase))
                ?? AddCatalog(catalogs, repo);
            string origin = $"{repo}/tree/{skill.Provenance.Commit}/{skill.Provenance.Path.Trim('/')}";
            string? reason = CopyImported(plan, skill);
            if (FrontmatterTools(skill.Folder) is { Length: > 0 })
            {
                plan.IgnoredTools.Add(skill.Name);
            }

            if (reason is not null)
            {
                plan.SkillsLeftInPlace++;
                plan.Line(build.SourceId, "skill-left", $"skill:{skill.Name}", $"{skill.Name} stays in Skills/ only: {reason}.");
                continue;
            }

            string key = LegacyCatalogReader.RepoPathKey(skill.Provenance.Repo, skill.Provenance.Path);
            JsonObject? entry = build.Entries.FirstOrDefault(item => string.Equals((string?)item["name"], skill.Name, StringComparison.Ordinal)
                && build.Legacy.Any(legacy => legacy.Name == skill.Name && LegacyCatalogReader.RepoPathKey(legacy.Repo, legacy.Path) == key));
            if (entry is null)
            {
                entry = new JsonObject
                {
                    ["name"] = skill.Name,
                    ["description"] = DescriptionOf(skill.Folder),
                    ["publisher"] = skill.Provenance.Publisher,
                    ["origin"] = origin,
                    ["license"] = skill.Provenance.License,
                    ["category"] = string.Empty,
                    ["subCategory"] = string.Empty,
                    ["requires"] = new JsonArray(),
                    ["path"] = string.Empty,
                };
                build.Entries.Add(entry);
            }

            entry["origin"] = origin;
            entry["path"] = $"../fetched/{skill.Name}";
            build.Downloaded++;
            plan.Pointed[skill.Name] = origin;
            plan.SkillsPointedAt++;
            plan.Line(build.SourceId, "skill-pointed", $"skill:{skill.Name}", $"{skill.Name} points at fetched/{skill.Name}, copied from Skills/{skill.Name} at {skill.Provenance.Commit}.");
        }
    }

    /// <summary>Copies one imported Skill into <c>fetched/</c> in the staging folder, or returns why it stays in place.</summary>
    private static string? CopyImported(Plan plan, ImportedSkill skill)
    {
        try
        {
            if (!string.Equals(SkillFolderHash.ComputeFolder(skill.Folder), skill.Provenance.Sha256, StringComparison.Ordinal))
            {
                return "edited locally; no pointer written";
            }

            List<string> sourceFiles = [.. Directory.GetFiles(skill.Folder, "*", SearchOption.AllDirectories)
                .Where(file => !IsExcluded(skill.Folder, file))];
            if (sourceFiles.Count > SkillFolderRules.MaxFiles || sourceFiles.Sum(static file => new FileInfo(file).Length) > SkillFolderRules.MaxTotalBytes)
            {
                return "too large to copy";
            }

            string target = Path.Combine(plan.Staging, "fetched", skill.Name);
            foreach (string file in sourceFiles)
            {
                string to = Path.Combine(target, Path.GetRelativePath(skill.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to) ?? target);
                File.Copy(file, to);
            }

            if (!string.Equals(SkillFolderHash.ComputeFolder(target), skill.Provenance.Sha256, StringComparison.Ordinal))
            {
                Directory.Delete(target, recursive: true);
                return "changed while copying";
            }

            plan.Fetched.Add(skill.Name);
            return null;
        }
        catch (IOException exception)
        {
            return $"could not be read ({exception.Message})";
        }
        catch (UnauthorizedAccessException exception)
        {
            return $"could not be read ({exception.Message})";
        }
    }

    /// <summary>Writes one catalog file, its Source's <c>source.json</c> and <c>NOTES.md</c>, and the split line.</summary>
    private void WriteCatalog(Plan plan, CatalogBuild build)
    {
        if (build.Entries.Count == 0)
        {
            return;
        }

        JsonObject file = new()
        {
            ["schemaVersion"] = 1,
            ["id"] = build.Id,
            ["title"] = build.Title,
            ["source"] = build.SourceId,
            ["skills"] = new JsonArray([.. build.Entries.Select(static entry => entry.DeepClone())]),
        };
        plan.Write($"catalogs/{build.Id}.skillcatalog.json", file.ToJsonString(JsonOptions) + "\n");
        JsonObject source = new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = build.IsGitHub ? "github" : "git",
            ["url"] = build.Repo,
            ["fetchedAt"] = plan.Time,
            ["fetchedBy"] = "Huddle catalog",
            ["signIn"] = "none",
            ["status"] = "active",
        };
        plan.WriteSource(build.SourceId, source.ToJsonString(JsonOptions) + "\n", this.CatalogNotes(plan, build));
        if (build.Legacy.Count > 0)
        {
            plan.Line(build.SourceId, "catalog-split", $"catalog:{build.Id}", string.Create(CultureInfo.InvariantCulture, $"Split {build.Entries.Count} Skills out of SkillsCatalog.json (catalog {build.Id})."));
        }

        plan.Catalogs++;
        plan.CatalogSkills += build.Entries.Count;
    }

    /// <summary>The <c>NOTES.md</c> of a catalog's Source.</summary>
    private string CatalogNotes(Plan plan, CatalogBuild build)
    {
        string omitted = build.Omitted.Count == 0 ? "none" : string.Join(", ", build.Omitted.Select(static item => $"{item.Name} (kept in {item.Kept})"));
        string found = build.Legacy.Count > 0
            ? $"the catalog Huddle {HuddleVersion()} shipped inside the app (`SkillsCatalog.json`). Huddle split it into this file on {plan.Date}. Nobody has cloned this repository for Huddle; the Skills here are listed, not downloaded."
            : $"Skills the Human imported into Huddle {HuddleVersion()} from this repository. Huddle listed them here on {plan.Date}.";
        _ = this;
        return $"""
            ---
            layout: huddle-catalog
            manifest: catalogs/{build.Id}.skillcatalog.json
            ---
            # {build.Title}

            **Found:** {found}

            **Layout:** one entry per Skill, each pinned to a commit in its `origin`. `path` is empty until a Skill is fetched. Skills that share a name with one in another catalog and were left out here: {omitted}.

            **To update:** fetch a Skill into any folder, set its `path`, and run `huddle-packs validate`. To refresh the whole list, read the repository's manifest at a newer commit and rewrite this file.

            """.ReplaceLineEndings("\n");
    }

    /// <summary>Step 3, the Human's own Packs: each old file becomes a Pack in the Source <c>local/my-packs</c>.</summary>
    /// <returns>The ids moved, so a shipped Pack with the same id gets no row.</returns>
    private HashSet<string> MoveUserPacks(Plan plan, List<CatalogBuild> catalogs, List<ImportedSkill> imported, List<TemplateRow> rows)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        string folder = Path.Combine(this.settings.DataDir, TeamPaths.SkillPacksDir);
        List<LegacyPack> packs = ReadLegacyPacks(plan, folder, ids);

        foreach (LegacyPack pack in packs.OrderBy(static pack => pack.Id, StringComparer.Ordinal))
        {
            JsonArray skills = [.. pack.Skills.Select(skill => (JsonNode)MapPackSkill(skill, catalogs, imported, plan))];
            JsonObject file = new()
            {
                ["schemaVersion"] = 1,
                ["id"] = pack.Id,
                ["version"] = pack.Version,
                ["title"] = pack.Title,
                ["source"] = MyPacksSource,
                ["appliesTo"] = new JsonArray([.. pack.AppliesTo.Select(static role => (JsonNode?)JsonValue.Create(role))]),
                ["consultWhenAdds"] = pack.ConsultWhenAdds,
                ["skills"] = skills,
            };
            plan.Write($"packs/{pack.Id}.skillpack.json", file.ToJsonString(JsonOptions) + "\n");
            rows.Add(new TemplateRow($"packs/{pack.Id}.skillpack.json", string.Empty, plan.Date, "Your own Pack; moved from SkillPacks/"));
            plan.UserPacksMoved++;
            plan.Line(MyPacksSource, "pack-moved", $"pack:{pack.Id}", $"Pack {pack.Id} {pack.Version} moved from SkillPacks/{pack.File}.");
            if (ShippedPacks.Contains(pack.Id, StringComparer.Ordinal))
            {
                plan.Line(MyPacksSource, "pack-replaced", $"pack:{pack.Id}", $"Pack {pack.Id} replaces the one Huddle shipped; no row was added for the shipped one.");
            }
        }

        WriteMyPacksSource(plan, folder);
        return [.. packs.Select(static pack => pack.Id)];
    }

    /// <summary>Reads the old Pack files, recording a problem and a log line for each one that cannot move.</summary>
    /// <param name="plan">The plan receiving problems and log lines.</param>
    /// <param name="folder">The folder holding the old Pack files.</param>
    /// <param name="ids">The ids accepted so far; each accepted Pack's id is added.</param>
    /// <returns>The Packs that can move.</returns>
    private static List<LegacyPack> ReadLegacyPacks(Plan plan, string folder, HashSet<string> ids)
    {
        List<LegacyPack> packs = [];
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(file);
                (LegacyPack? pack, string? problem) = LegacyPackReader.Parse(File.ReadAllText(file), name);
                string? reason = SkipReason(pack, problem, ids);
                if (reason is not null || pack is null)
                {
                    if (pack is not null && reason is not null && !reason.StartsWith("another", StringComparison.Ordinal))
                    {
                        ids.Remove(pack.Id);
                    }

                    plan.Problems.Add(new MoveProblem(name, $"{name}: {reason}."));
                    plan.Line(MyPacksSource, "pack-skipped", $"file:{name}", $"{name} not moved: {reason}.");
                    continue;
                }

                packs.Add(pack);
            }
        }

        return packs;
    }

    /// <summary>Writes <c>source.json</c> and <c>NOTES.md</c> for the <c>local/my-packs</c> Source.</summary>
    /// <param name="plan">The plan receiving the Source files.</param>
    /// <param name="folder">The folder the old Pack files were kept in.</param>
    private static void WriteMyPacksSource(Plan plan, string folder)
    {
        JsonObject source = new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = "local",
            ["localPath"] = folder,
            ["fetchedAt"] = plan.Time,
            ["fetchedBy"] = "Huddle move",
            ["signIn"] = "none",
            ["status"] = "active",
        };
        string notes = $"---\nlayout: huddle-my-packs\nmanifest: packs/*.skillpack.json\n---\n# Your own Packs\n\nThe Packs you kept in {folder}, moved here on {plan.Date}. Edit them here; Huddle no longer reads SkillPacks/.\n";
        plan.WriteSource(MyPacksSource, source.ToJsonString(JsonOptions) + "\n", notes);
    }

    /// <summary>Why an old Pack file is not moved, or null when it is (§6.13, "A Pack that does not move"). Adds a new id to <paramref name="ids"/>.</summary>
    private static string? SkipReason(LegacyPack? pack, string? problem, HashSet<string> ids)
    {
        if (pack is null)
        {
            return $"not a valid Pack file ({problem})";
        }

        if (!Agency.Huddle.SkillPacks.Files.Names.IsId(pack.Id))
        {
            return $"id '{pack.Id}' must be lowercase letters, digits and single hyphens";
        }

        if (!ids.Add(pack.Id))
        {
            return $"another file already defines Pack '{pack.Id}'";
        }

        return pack.Skills.FirstOrDefault(static skill => string.Equals(skill.Source, "builtin", StringComparison.Ordinal)) is { Name: { } builtIn }
            ? $"Skill '{builtIn}' is built in and cannot be in a Pack"
            : null;
    }

    /// <summary>A Pack Skill in the new form, by the first of the three rules of Step 3 that applies.</summary>
    private static JsonObject MapPackSkill((string Name, string Source, string? Path) skill, List<CatalogBuild> catalogs, List<ImportedSkill> imported, Plan plan)
    {
        string repo = GitHubPrefix + skill.Source.Trim('/');
        string key = LegacyCatalogReader.RepoPathKey(repo, skill.Path ?? string.Empty);
        CatalogBuild? catalog = catalogs.FirstOrDefault(build => build.Legacy.Any(entry => entry.Name == skill.Name && LegacyCatalogReader.RepoPathKey(entry.Repo, entry.Path) == key)
            && build.Entries.Any(entry => (string?)entry["name"] == skill.Name));
        if (catalog is not null)
        {
            return new JsonObject { ["name"] = skill.Name, ["catalog"] = catalog.Id };
        }

        ImportedSkill? copied = imported.FirstOrDefault(item => item.Name == skill.Name && LegacyCatalogReader.RepoPathKey(item.Provenance.Repo, item.Provenance.Path) == key);
        if (copied is not null && plan.Pointed.TryGetValue(skill.Name, out string? origin))
        {
            return new JsonObject { ["name"] = skill.Name, ["path"] = $"../fetched/{skill.Name}", ["origin"] = origin };
        }

        return new JsonObject { ["name"] = skill.Name, ["path"] = string.Empty, ["origin"] = $"{repo}/tree/HEAD/{(skill.Path ?? string.Empty).Trim('/')}" };
    }

    /// <summary>Rows for the two shipped Packs, from <c>Team:SkillsRepositoryDir</c>, which is read here and nowhere else.</summary>
    private List<TemplateRow> ShippedRows(Plan plan, HashSet<string> userIds)
    {
        List<TemplateRow> rows = [];
        string configured = this.options.SkillsRepositoryDir;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return rows;
        }

        string dir = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(dir))
        {
            plan.Problems.Add(new MoveProblem(configured, $"Team:SkillsRepositoryDir '{configured}' does not exist."));
            return rows;
        }

        string computer = this.settings.ComputerName;
        bool validComputer = ComputerName().IsMatch(computer);
        if (!validComputer)
        {
            plan.Problems.Add(new MoveProblem("Team:ComputerName", $"The computer name '{computer}' is not 2 to 15 letters, digits or hyphens, so the Pack rows use a plain path that every computer reads."));
        }

        foreach (string id in ShippedPacks.Where(id => !userIds.Contains(id)))
        {
            string file = Path.Combine(dir, "packs", $"{id}.skillpack.json");
            if (!File.Exists(file))
            {
                plan.Problems.Add(new MoveProblem(id, $"{dir} has no packs/{id}.skillpack.json."));
                plan.Line(SkillsRepoSource, "pack-missing", $"pack:{id}", $"{dir} has no packs/{id}.skillpack.json.");
                continue;
            }

            string cell = CodeSpanIfNeeded(file);
            rows.Add(new TemplateRow(validComputer ? $"{computer}: {cell}" : cell, string.Empty, plan.Date, "Shipped with Huddle; moved from the old format"));
            plan.ShippedPackRows++;
            plan.Line(SkillsRepoSource, "pack-row", $"pack:{id}", $"Row added for {id} at {file}.");
        }

        JsonObject source = new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = "local",
            ["localPath"] = dir,
            ["fetchedAt"] = plan.Time,
            ["fetchedBy"] = "Huddle move",
            ["signIn"] = "none",
            ["status"] = "active",
        };
        string notesFile = Path.Combine(dir, "NOTES.md");
        string notes = File.Exists(notesFile)
            ? File.ReadAllText(notesFile)
            : $"---\nlayout: huddle-skillpacks\n---\n# skills\n\nThe Packs Huddle used to ship, in {dir}.\n";
        plan.WriteSource(SkillsRepoSource, source.ToJsonString(JsonOptions) + "\n", notes, copyNotes: File.Exists(notesFile) ? notesFile : null);
        return rows;
    }

    /// <summary>Place: <c>fetched/</c>, <c>sources/</c>, <c>catalogs/</c>, <c>packs/</c>, then SKILLS.md with no overwrite.</summary>
    /// <returns>False when another Huddle placed SKILLS.md first.</returns>
    private bool Place(string staging, string dir, string skillsMd)
    {
        foreach (string top in (string[])["fetched", "sources", "catalogs", "packs"])
        {
            string from = Path.Combine(staging, top);
            if (!Directory.Exists(from))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string to = Path.Combine(dir, Path.GetRelativePath(staging, file));
                if (string.Equals(Path.GetFileName(file), SourceLog.FileName, StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(to) ?? dir);
                    File.AppendAllText(to, File.ReadAllText(file));
                    continue;
                }

                bool overwrite = top == "fetched";
                if (!overwrite && File.Exists(to))
                {
                    string? sourceFolder = SourceOfStaged(staging, file);
                    if (sourceFolder is not null)
                    {
                        this.log.Append(sourceFolder, new LogLine("moved", $"file:{Path.GetRelativePath(dir, to).Replace('\\', '/')}", Action: "kept-existing", Detail: "The file existed already, so the move left it as it was."));
                    }

                    continue;
                }

                this.files.Place(file, to, overwrite);
            }
        }

        try
        {
            this.files.Place(Path.Combine(staging, RegistryReader.FileName), skillsMd, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(skillsMd))
        {
            this.logger.LogInformation("Another Huddle placed SKILLS.md first; this move is discarded.");
            return false;
        }
    }

    /// <summary>The Source id a staged file under <c>sources/</c> belongs to, or null.</summary>
    private static string? SourceOfStaged(string staging, string file)
    {
        string relative = Path.GetRelativePath(Path.Combine(staging, "sources"), file).Replace('\\', '/');
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return null;
        }

        int last = relative.LastIndexOf('/');
        return last <= 0 ? null : relative[..last];
    }

    /// <summary>One old catalog entry in the new form (§6.13, the field map).</summary>
    private static JsonObject MapEntry(LegacyCatalogEntry entry)
    {
        string repo = entry.Repo.Trim().TrimEnd('/');
        string commit = entry.Commit.Length > 0 ? entry.Commit : "HEAD";
        string origin = entry.Link.Length > 0 ? entry.Link : $"{repo}/tree/{commit}/{entry.Path.Trim('/')}";
        return new JsonObject
        {
            ["name"] = entry.Name,
            ["description"] = entry.Description,
            ["publisher"] = entry.Publisher,
            ["origin"] = origin,
            ["license"] = entry.License,
            ["category"] = entry.Category,
            ["subCategory"] = entry.SubCategory,
            ["requires"] = new JsonArray([.. entry.Requires.Select(static item => (JsonNode?)JsonValue.Create(item))]),
            ["path"] = string.Empty,
        };
    }

    /// <summary>Adds a catalog for a repository outside the six.</summary>
    private static CatalogBuild AddCatalog(List<CatalogBuild> catalogs, string repo)
    {
        CatalogBuild build = new(repo, []);
        catalogs.Add(build);
        UniqueIds(catalogs);
        return build;
    }

    /// <summary>Gives a second catalog with the same id <c>-2</c>, a third <c>-3</c>.</summary>
    private static void UniqueIds(List<CatalogBuild> catalogs)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (CatalogBuild build in catalogs)
        {
            string id = build.BaseId;
            for (int n = 2; !seen.Add(id); n++)
            {
                id = string.Create(CultureInfo.InvariantCulture, $"{build.BaseId}-{n}");
            }

            build.Id = id;
        }
    }

    /// <summary>A path as a SKILLS.md cell: in a code span when it holds a backslash before punctuation or a Markdown character.</summary>
    internal static string CodeSpanIfNeeded(string path)
    {
        bool needs = path.IndexOfAny(['`', '*', '_', '[', ']', '<', '>', '|', '&', '!']) >= 0 || BackslashPunctuation().IsMatch(path);
        return needs ? $"`{path.Replace("|", "\\|", StringComparison.Ordinal)}`" : path;
    }

    /// <summary>Whether a relative path is <c>.source.json</c> or in a <c>.git</c> folder.</summary>
    private static bool IsExcluded(string folder, string file)
    {
        string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
        return relative == SkillProvenance.FileName || relative.StartsWith(".git/", StringComparison.Ordinal) || relative.Contains("/.git/", StringComparison.Ordinal);
    }

    /// <summary>The <c>description</c> of a folder's SKILL.md, or empty.</summary>
    private static string DescriptionOf(string folder)
    {
        string skillMd = Path.Combine(folder, "SKILL.md");
        return File.Exists(skillMd) ? SkillFrontmatter.Get(SkillFrontmatter.Parse(File.ReadAllText(skillMd)), "description") ?? string.Empty : string.Empty;
    }

    /// <summary>The <c>tools</c> line of a folder's SKILL.md, or empty.</summary>
    private static string FrontmatterTools(string folder)
    {
        string skillMd = Path.Combine(folder, "SKILL.md");
        return File.Exists(skillMd) ? SkillFrontmatter.Get(SkillFrontmatter.Parse(File.ReadAllText(skillMd)), "tools") ?? string.Empty : string.Empty;
    }

    /// <summary>The app's version for the notes.</summary>
    private static string HuddleVersion()
    {
        string version = typeof(SkillSourcesMove).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    /// <summary>Saves a report; a failure is logged by the caller's error already.</summary>
    private static void TrySave(MoveReportStore reports, MoveReport report)
    {
        try
        {
            reports.Save(report);
        }
        catch (IOException)
        {
            // The data folder itself is not writable; Huddle's own log already has the error.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    /// <summary>Deletes a folder if it exists; a failure leaves it for the next start.</summary>
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // An orphan .move-* folder is cleaned at the next start once it is ten minutes old.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9-]{1,14}\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ComputerName();

    [GeneratedRegex(@"\\[!-/:-@\[-`{-~]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BackslashPunctuation();

    /// <summary>An imported Skill: a folder in <c>Skills/</c> with a valid record.</summary>
    /// <param name="Name">The folder name.</param>
    /// <param name="Folder">The full path.</param>
    /// <param name="Provenance">The record.</param>
    private sealed record ImportedSkill(string Name, string Folder, SkillProvenance Provenance);

    /// <summary>One catalog being built from a repository's entries.</summary>
    private sealed partial class CatalogBuild(string repo, List<LegacyCatalogEntry> legacy)
    {
        /// <summary>The repository URL.</summary>
        public string Repo { get; } = repo;

        /// <summary>The old entries, in file order.</summary>
        public List<LegacyCatalogEntry> Legacy { get; } = legacy;

        /// <summary>The new entries.</summary>
        public List<JsonObject> Entries { get; } = [];

        /// <summary>Names left out, with the catalog that kept them.</summary>
        public List<(string Name, string Kept)> Omitted { get; } = [];

        /// <summary>Entries pointed at <c>fetched/</c>.</summary>
        public int Downloaded { get; set; }

        /// <summary>The catalog id after de-duplication.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary><c>owner-repo</c>, lowercased, other characters as single hyphens.</summary>
        public string BaseId => Slug().Replace(this.OwnerRepo.ToUpperInvariant(), "-").Trim('-').ToLowerInvariant();

        /// <summary><c>owner/repo</c>.</summary>
        public string OwnerRepo
        {
            get
            {
                string rest = Uri.TryCreate(this.Repo, UriKind.Absolute, out Uri? uri) ? uri.AbsolutePath.Trim('/') : this.Repo;
                return rest.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? rest[..^4] : rest;
            }
        }

        /// <summary>The catalog's title, <c>owner/repo</c>.</summary>
        public string Title => this.OwnerRepo;

        /// <summary>Whether the repository is on github.com.</summary>
        public bool IsGitHub => Uri.TryCreate(this.Repo, UriKind.Absolute, out Uri? uri) && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);

        /// <summary>The Source id: <c>github.com/owner/repo</c>, or <c>host/owner/repo</c>.</summary>
        public string SourceId
        {
            get
            {
                string host = Uri.TryCreate(this.Repo, UriKind.Absolute, out Uri? uri) ? uri.Host : "github.com";
                return $"{host}/{this.OwnerRepo}".ToUpperInvariant().ToLowerInvariant();
            }
        }

        /// <summary>The Notes cell of the catalog's row.</summary>
        public string Notes()
        {
            string downloaded = this.Downloaded > 0 ? string.Create(CultureInfo.InvariantCulture, $", {this.Downloaded} downloaded") : string.Empty;
            if (this.Legacy.Count > 0)
            {
                return string.Create(CultureInfo.InvariantCulture, $"From SkillsCatalog.json; {this.Entries.Count} Skills{downloaded}");
            }

            string skills = this.Entries.Count == 1 ? "Skill" : "Skills";
            return string.Create(CultureInfo.InvariantCulture, $"Imported by Huddle; {this.Entries.Count} {skills}, {this.Downloaded} downloaded");
        }

        [GeneratedRegex("[^A-Z0-9]+", RegexOptions.CultureInvariant, 1000)]
        private static partial Regex Slug();
    }

    /// <summary>What one run stages, and the counts its report carries.</summary>
    private sealed class Plan(string staging, DateTimeOffset now, SourceLog log)
    {
        private readonly Dictionary<string, StringBuilder> logs = new(StringComparer.Ordinal);

        /// <summary>The staging folder.</summary>
        public string Staging { get; } = staging;

        /// <summary>The date written in rows, <c>yyyy-MM-dd</c>.</summary>
        public string Date { get; } = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>The time written in Source records.</summary>
        public string Time { get; } = now.ToString(Agency.Huddle.SkillPacks.InstallContext.TimeFormat, CultureInfo.InvariantCulture);

        /// <summary>The problems.</summary>
        public List<MoveProblem> Problems { get; } = [];

        /// <summary>Imported Skills copied to <c>fetched/</c>.</summary>
        public List<string> Fetched { get; } = [];

        /// <summary>Imported Skills whose tools line Huddle now ignores.</summary>
        public List<string> IgnoredTools { get; } = [];

        /// <summary>Skill name to the origin it was pointed at.</summary>
        public Dictionary<string, string> Pointed { get; } = new(StringComparer.Ordinal);

        /// <summary>The Sources written, in order.</summary>
        public List<string> SourcesWritten { get; } = [];

        /// <summary>Counts for the report.</summary>
        public int Catalogs { get; set; }

        /// <summary>Counts for the report.</summary>
        public int CatalogSkills { get; set; }

        /// <summary>Counts for the report.</summary>
        public int OmittedDuplicates { get; set; }

        /// <summary>Counts for the report.</summary>
        public int ShippedPackRows { get; set; }

        /// <summary>Counts for the report.</summary>
        public int UserPacksMoved { get; set; }

        /// <summary>Counts for the report.</summary>
        public int SkillsPointedAt { get; set; }

        /// <summary>Counts for the report.</summary>
        public int SkillsLeftInPlace { get; set; }

        /// <summary>Writes a staged file at a path relative to the Skill Sources folder.</summary>
        public void Write(string relative, string text)
        {
            string path = Path.Combine(this.Staging, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.Staging);
            File.WriteAllText(path, text.ReplaceLineEndings("\n"), new UTF8Encoding(false));
        }

        /// <summary>Writes a Source's <c>source.json</c> and <c>NOTES.md</c> (copied byte for byte when <paramref name="copyNotes"/> is set).</summary>
        public void WriteSource(string sourceId, string sourceJson, string notes, string? copyNotes = null)
        {
            string folder = $"sources/{sourceId}";
            this.Write($"{folder}/source.json", sourceJson);
            if (copyNotes is not null)
            {
                string target = Path.Combine(this.Staging, folder.Replace('/', Path.DirectorySeparatorChar), "NOTES.md");
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? this.Staging);
                File.Copy(copyNotes, target, overwrite: true);
            }
            else
            {
                this.Write($"{folder}/NOTES.md", notes);
            }

            if (!this.SourcesWritten.Contains(sourceId, StringComparer.Ordinal))
            {
                this.SourcesWritten.Add(sourceId);
            }
        }

        /// <summary>Queues a <c>moved</c> line for a Source's staged <c>log.jsonl</c>.</summary>
        public void Line(string sourceId, string action, string subject, string detail)
        {
            if (!this.logs.TryGetValue(sourceId, out StringBuilder? text))
            {
                text = new StringBuilder();
                this.logs[sourceId] = text;
            }

            text.Append(log.Format(new LogLine("moved", subject, Action: action, Detail: detail))).Append('\n');
        }

        /// <summary>Writes every queued log line into the staging copies of the Sources' <c>log.jsonl</c>.</summary>
        public void FlushLogs()
        {
            foreach ((string sourceId, StringBuilder text) in this.logs)
            {
                this.Write($"sources/{sourceId}/{SourceLog.FileName}", text.ToString());
            }
        }

        /// <summary>The report.</summary>
        public MoveReport Report(DateTimeOffset at)
        {
            return new MoveReport(at, false, this.Catalogs, this.CatalogSkills, this.OmittedDuplicates, this.ShippedPackRows, this.UserPacksMoved, this.SkillsPointedAt, this.SkillsLeftInPlace, [.. this.IgnoredTools], [.. this.Problems]);
        }
    }
}
