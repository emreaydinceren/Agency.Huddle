using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>Loads, validates and serves team types from built-ins and the user's TeamTypes folder (TT-1 to TT-6).</summary>
/// <remarks>
/// Built-ins are embedded resources <c>TeamTypes/Builtin/{id}/...</c>. A user folder <c>{DataDir}/TeamTypes/{id}/</c> with a
/// manifest of the same id replaces the built-in manifest; candidate and process files resolve the user folder first, then
/// the built-in, so a user can add a file without copying the type. A role that names a hiring-pool job takes its persona from the
/// pool (whose own user folder overrides a shipped persona), not from a <c>candidates</c> folder of the type. A manifest value that is rooted or contains <c>..</c> is never
/// resolved. Invalid types are excluded from <see cref="Types"/> and listed in <see cref="Problems"/>; loading never throws for bad content.
/// </remarks>
internal sealed class TeamTypeCatalog
{
    private const string ResourcePrefix = "TeamTypes/Builtin/";
    private const string SkillPacksFolder = "SkillPacks/";
    private const string ManifestFile = "team-type.json";

    private readonly string userDir;
    private readonly ILogger<TeamTypeCatalog> logger;
    private readonly IReadOnlyDictionary<string, string> builtins;
    private readonly HiringPoolCatalog pool;
    private volatile Snapshot snapshot = new([], [], new Dictionary<string, TeamTypeManifest>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Creates the catalog over the assembly's embedded built-in types and the embedded hiring pool.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected type.</param>
    public TeamTypeCatalog(IOptions<TeamOptions> options, ILogger<TeamTypeCatalog> logger)
        : this(options, logger, ReadEmbedded(), new HiringPoolCatalog(options, NullLogger<HiringPoolCatalog>.Instance))
    {
    }

    /// <summary>Creates the catalog over the assembly's embedded built-in types and a shared hiring pool.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected type.</param>
    /// <param name="pool">The hiring pool the roles of a type refer to.</param>
    public TeamTypeCatalog(IOptions<TeamOptions> options, ILogger<TeamTypeCatalog> logger, HiringPoolCatalog pool)
        : this(options, logger, ReadEmbedded(), pool)
    {
    }

    /// <summary>Creates the catalog over a given set of built-in files and no hiring pool, for tests.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected type.</param>
    /// <param name="builtins">Built-in path, such as <c>development/team-type.json</c>, to its text.</param>
    internal TeamTypeCatalog(IOptions<TeamOptions> options, ILogger<TeamTypeCatalog> logger, IReadOnlyDictionary<string, string> builtins)
        : this(options, logger, builtins, new HiringPoolCatalog(options, NullLogger<HiringPoolCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal)))
    {
    }

    /// <summary>Creates the catalog over a given set of built-in files and a given hiring pool.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected type.</param>
    /// <param name="builtins">Built-in path, such as <c>development/team-type.json</c>, to its text.</param>
    /// <param name="pool">The hiring pool the roles of a type refer to.</param>
    internal TeamTypeCatalog(IOptions<TeamOptions> options, ILogger<TeamTypeCatalog> logger, IReadOnlyDictionary<string, string> builtins, HiringPoolCatalog pool)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(builtins);
        ArgumentNullException.ThrowIfNull(pool);
        this.userDir = Path.Combine(options.Value.DataDir, TeamPaths.TeamTypesDir);
        this.logger = logger;
        this.builtins = builtins;
        this.pool = pool;
        this.Reload();
        pool.Changed += this.Reload;
    }

    /// <summary>Raised after <see cref="Reload"/> has replaced the loaded types.</summary>
    public event Action? Changed;

    /// <summary>The hiring pool the roles of the types refer to, so a picker can browse its jobs.</summary>
    public HiringPoolCatalog Pool => this.pool;

    /// <summary>The valid types, ordered by order then display name.</summary>
    public IReadOnlyList<TeamTypeManifest> Types => this.snapshot.Types;

    /// <summary>The folder the user's own team types live in, shown on the Settings Team Types tab.</summary>
    public string UserDirectory => this.userDir;

    /// <summary>The types that were rejected, with the reason.</summary>
    public IReadOnlyList<TeamTypeFileProblem> Problems => this.snapshot.Problems;

    /// <summary>Finds a valid type by id.</summary>
    /// <param name="id">The type id.</param>
    /// <returns>The manifest, or null when no valid type has that id.</returns>
    public TeamTypeManifest? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.snapshot.ById.GetValueOrDefault(id);
    }

    /// <summary>Tells whether a valid type's manifest comes from the user's folder rather than the built-ins.</summary>
    /// <param name="id">The type id.</param>
    /// <returns>True when the loaded manifest is the user's own (a new type or a replacement of a built-in).</returns>
    public bool IsUserDefined(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.snapshot.UserIds.Contains(id);
    }

    /// <summary>Reads and parses the candidate file of a role.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="roleId">The role id.</param>
    /// <returns>The template, or null when the type or role is unknown or the file is missing or does not parse.</returns>
    public CandidateTemplate? ReadCandidate(string typeId, string roleId)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        ArgumentNullException.ThrowIfNull(roleId);
        RoleSpec? role = this.Find(typeId)?.Roles.FirstOrDefault(r => string.Equals(r.Id, roleId, StringComparison.Ordinal));
        if (role?.Job is not null)
        {
            return this.pool.ReadPersona(role.Job);
        }

        string? text = role is null ? null : this.ReadFile(typeId, role.Candidate);
        if (text is null)
        {
            return null;
        }

        CandidateParseResult result = CandidateTemplateParser.Parse(text);
        if (result.Template is null)
        {
            this.logger.LogWarning("Candidate {TypeId}/{RoleId} does not parse: {Problems}", typeId, roleId, string.Join(' ', result.Problems));
        }

        return result.Template;
    }

    /// <summary>Reads a process document.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="processId">The process id.</param>
    /// <returns>The Markdown text, or null when the type or process is unknown or the file is missing.</returns>
    public string? ReadProcess(string typeId, string processId)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        ArgumentNullException.ThrowIfNull(processId);
        ProcessRef? process = this.Find(typeId)?.Processes.FirstOrDefault(p => string.Equals(p.Id, processId, StringComparison.Ordinal));
        return process is null ? null : this.ReadFile(typeId, process.File);
    }

    /// <summary>Rereads the built-ins and the user folder, replacing the loaded types and problems, then raises <see cref="Changed"/>.</summary>
    public void Reload()
    {
        List<TeamTypeFileProblem> problems = [];
        Dictionary<string, TeamTypeManifest> byId = new(StringComparer.Ordinal);

        HashSet<string> userIds = new(StringComparer.Ordinal);
        foreach (string id in this.ListTypeIds())
        {
            TeamTypeManifest? user = this.LoadUser(id, problems);
            TeamTypeManifest? manifest = user ?? this.LoadBuiltin(id, problems);
            if (manifest is not null)
            {
                byId[id] = manifest;
                if (user is not null)
                {
                    userIds.Add(id);
                }
            }
        }

        foreach (TeamTypeFileProblem problem in problems)
        {
            this.logger.LogWarning("Team type {TypeId} was not loaded from {Path}: {Message}", problem.TypeId, problem.Path, problem.Message);
        }

        List<TeamTypeManifest> types = [.. byId.Values.OrderBy(static t => t.Order).ThenBy(static t => t.DisplayName, StringComparer.Ordinal)];
        this.snapshot = new Snapshot(types, problems, byId, userIds);
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>Tells whether a type-relative path is a plain relative path with no parent segments.</summary>
    /// <param name="relativePath">The value from a manifest.</param>
    /// <returns>True when the path may be resolved.</returns>
    internal static bool IsSafe(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && !relativePath.Contains("..", StringComparison.Ordinal)
            && !relativePath.Contains(':')
            && !relativePath.StartsWith('/')
            && !relativePath.StartsWith('\\')
            && !Path.IsPathRooted(relativePath);
    }

    /// <summary>Reads every embedded built-in type file, keyed by path below the built-in prefix.</summary>
    /// <returns>Path, such as <c>development/team-type.json</c>, to its text with <c>\n</c> line endings.</returns>
    private static Dictionary<string, string> ReadEmbedded()
    {
        return EmbeddedResources.ReadFolder(
            typeof(TeamTypeCatalog).Assembly,
            ResourcePrefix,
            static relative => !relative.StartsWith(SkillPacksFolder, StringComparison.Ordinal));
    }

    /// <summary>Lists the ids of the built-in types and the user folders, without duplicates.</summary>
    /// <returns>The ids in ordinal order.</returns>
    private List<string> ListTypeIds()
    {
        SortedSet<string> ids = new(StringComparer.Ordinal);
        string suffix = "/" + ManifestFile;
        foreach (string key in this.builtins.Keys.Where(k => k.EndsWith(suffix, StringComparison.Ordinal) && k.IndexOf('/') == k.Length - suffix.Length))
        {
            ids.Add(key[..^suffix.Length]);
        }

        if (Directory.Exists(this.userDir))
        {
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(this.userDir))
                {
                    if (File.Exists(Path.Combine(folder, ManifestFile)))
                    {
                        ids.Add(Path.GetFileName(folder));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "The team types folder {Path} could not be listed.", this.userDir);
            }
        }

        return [.. ids];
    }

    /// <summary>Loads and validates the user manifest of a type, if there is one.</summary>
    /// <param name="id">The type id (its folder name).</param>
    /// <param name="problems">Receives the problem when the manifest is rejected.</param>
    /// <returns>The manifest, or null when there is none or it was rejected.</returns>
    private TeamTypeManifest? LoadUser(string id, List<TeamTypeFileProblem> problems)
    {
        string path = Path.Combine(this.userDir, id, ManifestFile);
        if (!File.Exists(path))
        {
            return null;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(new TeamTypeFileProblem(id, path, "The manifest could not be read."));
            return null;
        }

        return this.Parse(id, path, text, problems);
    }

    /// <summary>Loads and validates the built-in manifest of a type, if there is one.</summary>
    /// <param name="id">The type id.</param>
    /// <param name="problems">Receives the problem when the manifest is rejected.</param>
    /// <returns>The manifest, or null when there is none or it was rejected.</returns>
    private TeamTypeManifest? LoadBuiltin(string id, List<TeamTypeFileProblem> problems)
    {
        string key = id + "/" + ManifestFile;
        return this.builtins.TryGetValue(key, out string? text) ? this.Parse(id, "builtin:" + key, text, problems) : null;
    }

    /// <summary>Deserialises a manifest and validates it against the files visible to its type.</summary>
    /// <param name="id">The folder name of the type.</param>
    /// <param name="source">The source label used in a problem.</param>
    /// <param name="text">The manifest JSON.</param>
    /// <param name="problems">Receives one problem per rejection, joining all reasons.</param>
    /// <returns>The manifest, or null when rejected.</returns>
    private TeamTypeManifest? Parse(string id, string source, string text, List<TeamTypeFileProblem> problems)
    {
        TeamTypeManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<TeamTypeManifest>(text, TeamTypeJson.Options);
        }
        catch (JsonException)
        {
            problems.Add(new TeamTypeFileProblem(id, source, "The manifest is not valid team-type JSON."));
            return null;
        }

        if (manifest is null || manifest.Id is null || manifest.Version is null || manifest.Workspace is null)
        {
            problems.Add(new TeamTypeFileProblem(id, source, "The manifest lacks id, version or workspace."));
            return null;
        }

        PoolTeamType? named = this.pool.FindTeamType(id);
        string? displayName = named?.Name ?? manifest.DisplayName;
        if (displayName is null)
        {
            problems.Add(new TeamTypeFileProblem(id, source, "The manifest has no displayName and the hiring pool does not name the type."));
            return null;
        }

        if (!string.Equals(manifest.Id, id, StringComparison.Ordinal))
        {
            problems.Add(new TeamTypeFileProblem(id, source, $"The manifest id '{manifest.Id}' does not match its folder '{id}'."));
            return null;
        }

        manifest = this.ComposeRoles(manifest with { DisplayName = displayName, Description = named?.Description });
        IReadOnlyList<TeamTypeProblem> found = TeamTypeValidator.Validate(
            manifest, relative => this.ReadFile(id, relative) is not null, jobId => this.pool.FindJob(jobId) is not null);
        if (found.Count > 0)
        {
            problems.Add(new TeamTypeFileProblem(id, source, string.Join(' ', found.Select(static p => p.Message))));
            return null;
        }

        return this.AddPooledRoles(manifest);
    }

    /// <summary>Fills each role that is a hiring-pool job from its job, so every consumer keeps reading plain roles.</summary>
    /// <param name="manifest">The deserialised manifest.</param>
    /// <returns>The manifest with its job roles composed; a role naming an unknown job keeps its id and job so the validator can report it.</returns>
    private TeamTypeManifest ComposeRoles(TeamTypeManifest manifest)
    {
        List<RoleSpec> roles = [];
        foreach (RoleSpec role in manifest.Roles)
        {
            string? jobId = role.Job ?? (role.Candidate is null ? role.Id : this.MovedJobId(manifest.Id, role));
            if (jobId is null)
            {
                roles.Add(role);
                continue;
            }

            PoolJob? job = this.pool.FindJob(jobId);
            roles.Add(job is null
                ? role with { Job = jobId }
                : role with
                {
                    Job = jobId,
                    Title = job.Title,
                    Candidate = job.Persona,
                    Skills = job.SkillRefs,
                    Packs = job.Packs,
                    Requires = job.Requires,
                    SuggestedNames = job.SuggestedNames,
                    Model = new ModelSpec(job.Model.Tier, role.Model.DifferentVendorFrom),
                });
        }

        return manifest with { Roles = roles };
    }

    /// <summary>
    /// Finds the pool job of a role that an earlier version saved with a candidate file, such as the copy of a built-in type that
    /// Custom role writes into the user's folder. The built-in candidates moved into the pool, so when the file is gone and the
    /// pool holds a job of the role's id, the role is that job.
    /// </summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="role">A role with a candidate path and no job.</param>
    /// <returns>The job id, or null when the candidate file exists or the pool has no such job (the role stays as written).</returns>
    private string? MovedJobId(string typeId, RoleSpec role)
    {
        return this.ReadFile(typeId, role.Candidate) is null && this.pool.FindJob(role.Id) is not null ? role.Id : null;
    }

    /// <summary>Appends every pool job the type does not list as an optional role, so a Human can hire it with default settings.</summary>
    /// <param name="manifest">A validated manifest with its job roles composed.</param>
    /// <returns>The manifest with the extra roles, each marked <see cref="RoleSpec.Pooled"/>.</returns>
    private TeamTypeManifest AddPooledRoles(TeamTypeManifest manifest)
    {
        HashSet<string> listedJobs = new(manifest.Roles.Select(static role => role.Job ?? string.Empty), StringComparer.Ordinal);
        HashSet<string> roleIds = new(manifest.Roles.Select(static role => role.Id), StringComparer.Ordinal);
        List<RoleSpec> roles = [.. manifest.Roles];
        foreach (PoolJob job in this.pool.Jobs.Where(job => !listedJobs.Contains(job.Id) && !roleIds.Contains(job.Id)))
        {
            roles.Add(new RoleSpec(
                job.Id,
                job.Title,
                job.Persona,
                Model: job.Model,
                Skills: job.SkillRefs,
                Packs: job.Packs,
                Requires: job.Requires,
                SuggestedNames: job.SuggestedNames,
                Job: job.Id,
                Pooled: true));
        }

        return manifest with { Roles = roles };
    }

    /// <summary>Reads a type file from the user folder first, then the built-ins.</summary>
    /// <param name="typeId">The type id.</param>
    /// <param name="relativePath">The path from the manifest, relative to the type folder.</param>
    /// <returns>The text, or null when the path is unsafe or no file exists.</returns>
    private string? ReadFile(string typeId, string relativePath)
    {
        if (!IsSafe(relativePath))
        {
            return null;
        }

        string typeDir = Path.GetFullPath(Path.Combine(this.userDir, typeId));
        string full = Path.GetFullPath(Path.Combine(typeDir, relativePath));
        if (PathContainment.IsInside(full, typeDir, includeRoot: false, PathContainment.Comparison) && File.Exists(full) && !LeavesTypeFolder(typeDir, full))
        {
            try
            {
                return File.ReadAllText(full);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "The team type file {Path} could not be read.", full);
                return null;
            }
        }

        string key = typeId + "/" + relativePath.Replace('\\', '/');
        return this.builtins.GetValueOrDefault(key);
    }

    /// <summary>
    /// Whether a junction or symbolic link between the type folder and the file resolves outside the type
    /// folder; the string path check alone cannot see that.
    /// </summary>
    /// <param name="typeDir">The full path of the type folder.</param>
    /// <param name="full">The full path of the file, already checked to sit under <paramref name="typeDir"/>.</param>
    /// <returns><see langword="true"/> when the file must not be read.</returns>
    internal static bool LeavesTypeFolder(string typeDir, string full)
    {
        string? current = full;
        try
        {
            while (current is not null && current.Length > typeDir.Length)
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is null || !PathContainment.IsInside(target.FullName, typeDir, includeRoot: true, PathContainment.Comparison))
                    {
                        return true;
                    }
                }

                current = Path.GetDirectoryName(current);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A link that cannot be inspected is treated as leaving the folder; refusing it is the safe answer.
            return true;
        }

        return false;
    }

    /// <summary>One consistent view of the loaded types.</summary>
    /// <param name="Types">The valid types in display order.</param>
    /// <param name="Problems">The rejected types.</param>
    /// <param name="ById">The valid types by id.</param>
    /// <param name="UserIds">The ids of the valid types whose manifest came from the user's folder.</param>
    private sealed record Snapshot(
        IReadOnlyList<TeamTypeManifest> Types,
        IReadOnlyList<TeamTypeFileProblem> Problems,
        IReadOnlyDictionary<string, TeamTypeManifest> ById,
        IReadOnlySet<string> UserIds);
}
