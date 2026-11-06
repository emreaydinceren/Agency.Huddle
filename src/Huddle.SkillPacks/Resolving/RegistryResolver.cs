using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Reading;
using System.Globalization;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>Turns a <see cref="Registry"/> into a <see cref="ResolvedRegistry"/> (§8.3). Structural only: it loads files,
/// resolves paths, catalog references and names, and hashes folders; it assigns no state.</summary>
public static class RegistryResolver
{
    /// <summary>How long after its last write a file that fails to parse is given before it is reported (§8.6).</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    /// <summary>The name of the provenance file in a copy.</summary>
    public const string ProvenanceFileName = ".source.json";

    /// <summary>Resolves every row of <paramref name="registry"/> against this computer.</summary>
    /// <remarks>Raises only the findings it cannot avoid while loading (R003, the parsers' findings and the Source-id
    /// check). <paramref name="context"/> supplies the computer name, the <c>Skills/</c> folder and the time.</remarks>
    public static ResolvedRegistry Resolve(Registry registry, IFileReader files, CheckContext context, ResolveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(context);
        Resolution resolution = new(registry, files, context, options ?? new ResolveOptions());
        return resolution.Run();
    }

    /// <summary>The state of one resolve.</summary>
    private sealed class Resolution(Registry registry, IFileReader files, CheckContext context, ResolveOptions options)
    {
        private readonly List<string> retry = [];
        private readonly HashSet<string> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ResolvedCatalog> catalogsById = new(StringComparer.Ordinal);
        private readonly HashSet<string> packIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sourceByFile = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Runs the resolve.</summary>
        public ResolvedRegistry Run()
        {
            List<PackEntry> packs = [.. registry.Packs.Select(this.LoadPack)];
            List<ResolvedCatalog> catalogs = [.. registry.Catalogs.Select(this.LoadCatalog)];
            List<ResolvedSource> sources = this.LoadSources(packs, catalogs);
            List<SkillEntry> skills = this.ResolveSkills(packs, catalogs);

            Dictionary<string, SkillEntry> byName = skills.ToDictionary(static skill => skill.Name, StringComparer.Ordinal);
            packs = [.. packs.Select(pack => pack with { MissingSkills = MissingOf(pack, byName) })];
            catalogs = [.. catalogs.Select(catalog => catalog with
            {
                DownloadedCount = catalog.SkillNames.Count(name => byName.TryGetValue(name, out SkillEntry? skill) && skill.PathState == SkillPathState.Present),
            })];

            List<Finding> findings = [.. registry.Findings];
            findings.AddRange(packs.SelectMany(static pack => pack.Findings));
            findings.AddRange(catalogs.SelectMany(static catalog => catalog.Findings));
            findings.AddRange(sources.SelectMany(static source => source.Findings));
            findings.AddRange(skills.SelectMany(static skill => skill.Findings));
            return new ResolvedRegistry(registry, packs, catalogs, skills, sources, findings, context.Written, this.retry);
        }

        /// <summary>The Pack's Skills that are not downloaded here.</summary>
        private static List<string> MissingOf(PackEntry pack, Dictionary<string, SkillEntry> byName)
        {
            return [.. pack.SkillNames.Where(name => byName.TryGetValue(name, out SkillEntry? skill) && skill.PathState != SkillPathState.Present)];
        }

        /// <summary>Loads one Packs row.</summary>
        private PackEntry LoadPack(RegistryRow row)
        {
            (string? path, string? text, List<Finding> findings) = this.LoadRowFile(row, PackFile.Suffix, "P001");
            string fallbackTitle = path is null ? row.LocalPath.FirstText : Path.GetFileName(path);
            PackEntry empty = new(row, path, null, string.Empty, string.Empty, fallbackTitle, string.Empty, [], null, [], [], findings);
            if (path is null || text is null)
            {
                return empty;
            }

            ParseResult<PackFile> parsed = PackFile.Parse(text, path);
            if (this.Defer(path, parsed.Ok))
            {
                return empty;
            }

            findings.AddRange(parsed.Findings);
            if (parsed.Value is not { } pack)
            {
                return empty with { Findings = findings };
            }

            bool first = this.packIds.Add(pack.Id);
            this.sourceByFile[path] = pack.Source;
            return new PackEntry(
                row,
                path,
                pack,
                pack.Id,
                pack.Version,
                pack.Title,
                pack.Source,
                pack.AppliesTo,
                pack.ConsultWhenAdds,
                first ? [.. pack.Skills.Select(static skill => skill.Name)] : [],
                [],
                findings,
                DuplicateId: !first);
        }

        /// <summary>Loads one Catalogs row; the first catalog with an id keeps it.</summary>
        private ResolvedCatalog LoadCatalog(RegistryRow row)
        {
            (string? path, string? text, List<Finding> findings) = this.LoadRowFile(row, CatalogFile.Suffix, "D001");
            string fallbackTitle = path is null ? row.LocalPath.FirstText : Path.GetFileName(path);
            ResolvedCatalog empty = new(row, path, null, string.Empty, fallbackTitle, string.Empty, [], 0, findings);
            if (path is null || text is null)
            {
                return empty;
            }

            ParseResult<CatalogFile> parsed = CatalogFile.Parse(text, path);
            if (this.Defer(path, parsed.Ok))
            {
                return empty;
            }

            findings.AddRange(parsed.Findings);
            if (parsed.Value is not { } catalog)
            {
                return empty with { Findings = findings };
            }

            bool first = !this.catalogsById.ContainsKey(catalog.Id);
            this.sourceByFile[path] = catalog.Source;
            ResolvedCatalog resolved = new(row, path, catalog, catalog.Id, catalog.Title, catalog.Source, first ? [.. catalog.Skills.Select(static entry => entry.Name)] : [], 0, findings, DuplicateId: !first);
            if (first)
            {
                this.catalogsById[catalog.Id] = resolved;
            }

            return resolved;
        }

        /// <summary>Resolves a row's path and reads its text when it is the right kind of file and not a repeat.</summary>
        /// <returns>The full path (null when the row has none here), the text (null when not read) and the findings so far.</returns>
        private (string? Path, string? Text, List<Finding> Findings) LoadRowFile(RegistryRow row, string suffix, string readCode)
        {
            List<Finding> findings = [];
            string? path = row.LocalPath.ResolveFull(context.ComputerName, registry.Directory);
            if (path is null)
            {
                return (null, null, findings);
            }

            string where = row.IsVirtual ? path : string.Create(CultureInfo.InvariantCulture, $"{row.TableName} row {row.Index + 1}: {path}");
            if (!SafeFileExists(path))
            {
                findings.Add(Finding.Error("R003", row.IsVirtual ? path : registry.FilePath, $"{where} does not exist."));
                return (path, null, findings);
            }

            if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || !this.loadedFiles.Add(path))
            {
                return (path, null, findings);
            }

            try
            {
                return (path, files.ReadAllText(path), findings);
            }
            catch (IOException exception)
            {
                findings.Add(Finding.Error(readCode, path, $"cannot read: {exception.Message}"));
            }
            catch (UnauthorizedAccessException exception)
            {
                findings.Add(Finding.Error(readCode, path, $"cannot read: {exception.Message}"));
            }

            return (path, null, findings);
        }

        /// <summary>Whether a file that failed to parse is within its grace and must be read again, not reported.</summary>
        private bool Defer(string path, bool parsedOk)
        {
            if (parsedOk || options.IgnoreGrace)
            {
                return false;
            }

            DateTimeOffset written;
            try
            {
                written = files.LastWriteTime(path);
            }
            catch (IOException)
            {
                return false;
            }

            if (context.Written - written >= Grace)
            {
                return false;
            }

            this.retry.Add(path);
            return true;
        }

        /// <summary>Whether a file exists, treating a reader that throws as "no".</summary>
        private bool SafeFileExists(string path)
        {
            try
            {
                return files.FileExists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Whether a folder exists, treating a reader that throws as "no".</summary>
        private bool SafeDirectoryExists(string path)
        {
            try
            {
                return files.DirectoryExists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Reads each distinct Source the Packs and catalogs name, in first-use order.</summary>
        private List<ResolvedSource> LoadSources(List<PackEntry> packs, List<ResolvedCatalog> catalogs)
        {
            List<(string Id, string File)> named = [];
            named.AddRange(packs.Where(static pack => pack.File is not null && pack.FilePath is not null).Select(static pack => (pack.Source, pack.FilePath ?? string.Empty)));
            named.AddRange(catalogs.Where(static catalog => catalog.File is not null && catalog.FilePath is not null).Select(static catalog => (catalog.Source, catalog.FilePath ?? string.Empty)));

            List<ResolvedSource> sources = [];
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach ((string id, string namingFile) in named)
            {
                if (seen.Add(id))
                {
                    sources.Add(this.LoadSource(id, namingFile));
                }
            }

            return sources;
        }

        /// <summary>Reads one Source's <c>source.json</c> and checks for <c>NOTES.md</c>.</summary>
        private ResolvedSource LoadSource(string id, string namingFile)
        {
            if (!SourceId.IsValid(id))
            {
                return new ResolvedSource(id, null, null, false, [Finding.Error("O001", namingFile, $"source '{id}' is not a Source id (host/owner/repo or local/name); nothing was read for it.")]);
            }

            string folder = SourceId.FolderOf(registry.Directory, id);
            string sourcePath = Path.Combine(folder, SourceFile.FileName);
            bool hasNotes = this.SafeFileExists(Path.Combine(folder, "NOTES.md"));
            if (!this.SafeFileExists(sourcePath))
            {
                return new ResolvedSource(id, folder, null, hasNotes, []);
            }

            string text;
            try
            {
                text = files.ReadAllText(sourcePath);
            }
            catch (IOException exception)
            {
                return new ResolvedSource(id, folder, null, hasNotes, [Finding.Error("O001", sourcePath, $"cannot read: {exception.Message}")]);
            }

            ParseResult<SourceFile> parsed = SourceFile.Parse(text, sourcePath);
            if (this.Defer(sourcePath, parsed.Ok))
            {
                return new ResolvedSource(id, folder, null, hasNotes, []);
            }

            return new ResolvedSource(id, folder, parsed.Value, hasNotes, parsed.Findings);
        }

        /// <summary>Builds one Skill per distinct name from every provider's claim, in registry order (§8.3 steps 3–4).</summary>
        private List<SkillEntry> ResolveSkills(List<PackEntry> packs, List<ResolvedCatalog> catalogs)
        {
            Dictionary<string, List<(SkillClaim Claim, CatalogEntry? Entry)>> claims = new(StringComparer.Ordinal);
            List<string> order = [];

            void Add(string name, SkillClaim claim, CatalogEntry? entry)
            {
                if (!claims.TryGetValue(name, out List<(SkillClaim, CatalogEntry?)>? list))
                {
                    list = [];
                    claims[name] = list;
                    order.Add(name);
                }

                list.Add((claim, entry));
            }

            foreach (RegistryRow row in registry.AllRows)
            {
                if (row.Kind == RowKind.Pack)
                {
                    this.AddPackClaims(row, packs[row.Index], Add);
                }
                else
                {
                    ResolvedCatalog catalog = catalogs[row.Index];
                    if (catalog.File is null || catalog.FilePath is null || catalog.SkillNames.Count == 0)
                    {
                        continue;
                    }

                    string baseDir = Path.GetDirectoryName(catalog.FilePath) ?? registry.Directory;
                    foreach (CatalogEntry entry in catalog.File.Skills)
                    {
                        Add(entry.Name, new SkillClaim($"catalog:{catalog.Id}", RowKind.Catalog, row.Index, catalog.FilePath, entry.Origin, entry.License, entry.Path, baseDir, null), entry);
                    }
                }
            }

            return [.. order.Select(name => this.BuildSkill(name, claims[name]))];
        }

        /// <summary>Hands <paramref name="add"/> a claim for each Skill a Pack lists, from its own path or from a Catalog entry.</summary>
        private void AddPackClaims(RegistryRow row, PackEntry pack, Action<string, SkillClaim, CatalogEntry?> add)
        {
            if (pack.File is null || pack.FilePath is null || pack.SkillNames.Count == 0)
            {
                return;
            }

            string baseDir = Path.GetDirectoryName(pack.FilePath) ?? registry.Directory;
            foreach (PackSkill skill in pack.File.Skills)
            {
                string provider = $"pack:{pack.Id}";
                if (skill.Path is not null)
                {
                    add(skill.Name, new SkillClaim(provider, RowKind.Pack, row.Index, pack.FilePath, skill.Origin ?? string.Empty, null, skill.Path, baseDir, null), null);
                }
                else if (skill.Catalog is not null
                    && this.catalogsById.TryGetValue(skill.Catalog, out ResolvedCatalog? catalog)
                    && catalog.File?.Find(skill.Name) is { } entry
                    && catalog.FilePath is not null)
                {
                    string catalogDir = Path.GetDirectoryName(catalog.FilePath) ?? registry.Directory;
                    add(skill.Name, new SkillClaim(provider, RowKind.Pack, row.Index, pack.FilePath, entry.Origin, entry.License, entry.Path, catalogDir, catalog.Id), entry);
                }
            }
        }

        /// <summary>One Skill from its claims; the first claim's origin decides which claims are the same Skill.</summary>
        private SkillEntry BuildSkill(string name, List<(SkillClaim Claim, CatalogEntry? Entry)> claims)
        {
            SkillClaim first = claims[0].Claim;
            List<(SkillClaim Claim, CatalogEntry? Entry)> same = [.. claims.Where(item => string.Equals(item.Claim.Origin, first.Origin, StringComparison.Ordinal))];
            CatalogEntry? entry = same.Select(static item => item.Entry).FirstOrDefault(static item => item is not null);

            (string? folder, SkillPathState state) = this.Locate(same.Select(static item => item.Claim));
            string? pointerHash = null;
            string description = entry?.Description ?? string.Empty;
            if (state == SkillPathState.Present && folder is not null)
            {
                pointerHash = this.Hash(folder);
                if (description.Length == 0)
                {
                    description = this.DescriptionFromFolder(folder);
                }
            }

            List<Finding> findings = [];
            string? catalogHash = state == SkillPathState.Present && pointerHash is not null && entry?.Sha256 is not null && folder is not null ? this.CatalogHash(folder) : null;
            if (catalogHash is not null && entry?.Sha256 is { } pinned
                && !string.Equals(catalogHash, pinned, StringComparison.OrdinalIgnoreCase))
            {
                state = SkillPathState.Altered;
                findings.Add(Finding.Error("S010", first.ProviderFile, $"Skill \"{name}\": the folder {folder} differs from the pinned commit (its content hash is {catalogHash}, the catalog pins {pinned}). Fetch the whole folder again byte for byte, for example with git at that commit."));
            }

            string? source = this.sourceByFile.GetValueOrDefault(first.ProviderFile);
            return new SkillEntry(
                name,
                description,
                entry?.Publisher,
                entry?.License,
                entry?.Category,
                entry?.SubCategory,
                entry?.Requires ?? [],
                first.Origin,
                source,
                folder,
                [.. claims.Select(static item => item.Claim.Provider).Distinct(StringComparer.Ordinal)],
                pointerHash,
                this.InstalledHash(name, first.Origin),
                findings,
                state,
                [.. claims.Select(static item => item.Claim)]);
        }

        /// <summary>The folder the claims point at on this computer and where that leaves the Skill.</summary>
        private (string? Folder, SkillPathState State) Locate(IEnumerable<SkillClaim> claims)
        {
            string? missing = null;
            bool noEntry = false;
            foreach (SkillClaim claim in claims)
            {
                if (claim.Path is null)
                {
                    continue;
                }

                string? full = claim.Path.ResolveFull(context.ComputerName, claim.BaseDirectory);
                if (full is not null)
                {
                    if (this.SafeDirectoryExists(full))
                    {
                        return (full, SkillPathState.Present);
                    }

                    missing ??= full;
                }
                else if (!claim.Path.HasEntryFor(context.ComputerName) && claim.Path.IsPerComputer)
                {
                    noEntry = true;
                }
            }

            if (missing is not null)
            {
                return (missing, SkillPathState.Missing);
            }

            return (null, noEntry ? SkillPathState.NoEntryForComputer : SkillPathState.Empty);
        }

        /// <summary>The folder's hash, through the reader's cache when it has one.</summary>
        private string? Hash(string folder)
        {
            try
            {
                return files is IFolderHasher hasher ? hasher.HashFolder(folder) : SkillFolderHash.ComputeFolder(files, folder);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>The folder's hash for the catalog's <c>sha256</c> check, which reads CRLF as LF in every text file so a clone
        /// made with <c>core.autocrlf=true</c> matches; null when the folder cannot be read. Not cached.</summary>
        private string? CatalogHash(string folder)
        {
            try
            {
                return SkillFolderHash.ComputeCatalogFolder(files, folder);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>The <c>description</c> of the folder's SKILL.md, or empty.</summary>
        private string DescriptionFromFolder(string folder)
        {
            string skillMd = Path.Combine(folder, "SKILL.md");
            try
            {
                return this.SafeFileExists(skillMd) ? SkillFrontmatter.Get(SkillFrontmatter.Parse(files.ReadAllText(skillMd)), "description") ?? string.Empty : string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        /// <summary>The <c>sha256</c> of the copy in <c>Skills/</c> when it exists and came from the same origin.</summary>
        private string? InstalledHash(string name, string origin)
        {
            string path = Path.Combine(context.SkillsDir, name, ProvenanceFileName);
            if (!this.SafeFileExists(path))
            {
                return null;
            }

            try
            {
                InstalledProvenance? provenance = InstalledProvenance.Parse(files.ReadAllText(path));
                return provenance is not null && OriginIdentity.Same(provenance.Origin, origin) ? provenance.Sha256 : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
