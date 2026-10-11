using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.HiringPool;

/// <summary>A pool entry that could not be offered.</summary>
/// <param name="Id">The job or team type id, or the file name when the whole file was rejected.</param>
/// <param name="Source">Where the entry came from.</param>
/// <param name="Message">Why it was rejected.</param>
internal sealed record HiringPoolProblem(string Id, string Source, string Message);

/// <summary>
/// Loads and serves the hiring pool: the global catalog of jobs a team type draws its roles from.
/// </summary>
/// <remarks>
/// The built-in pool is the embedded <c>HiringPool/hiringpool.json</c> and its <c>personas/*.md</c>. A user pool,
/// <c>{DataDir}/HiringPool/hiringpool.json</c>, is merged by id on top (a user job replaces a shipped job of the same id and
/// never hides the rest), and a persona resolves the user folder first. A bad job is excluded and listed in
/// <see cref="Problems"/>; loading never throws for bad content.
/// </remarks>
internal sealed partial class HiringPoolCatalog
{
    private const string ResourcePrefix = "HiringPool/";
    private const string PoolFile = "hiringpool.json";

    private readonly string userDir;
    private readonly ILogger<HiringPoolCatalog> logger;
    private readonly IReadOnlyDictionary<string, string> builtins;
    private volatile Snapshot snapshot = new([], [], [], new Dictionary<string, PoolJob>(StringComparer.Ordinal));

    /// <summary>Creates the catalog over the assembly's embedded built-in pool.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected entry.</param>
    public HiringPoolCatalog(IOptions<TeamOptions> options, ILogger<HiringPoolCatalog> logger)
        : this(options, logger, ReadEmbedded())
    {
    }

    /// <summary>Creates the catalog over a given set of built-in files, for tests.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected entry.</param>
    /// <param name="builtins">Built-in path, such as <c>hiringpool.json</c> or <c>personas/tutor.md</c>, to its text.</param>
    internal HiringPoolCatalog(IOptions<TeamOptions> options, ILogger<HiringPoolCatalog> logger, IReadOnlyDictionary<string, string> builtins)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(builtins);
        this.userDir = Path.Combine(options.Value.DataDir, TeamPaths.HiringPoolDir);
        this.logger = logger;
        this.builtins = builtins;
        this.Reload();
    }

    /// <summary>Raised after <see cref="Reload"/> has replaced the loaded pool.</summary>
    public event Action? Changed;

    /// <summary>The valid jobs, ordered by title.</summary>
    public IReadOnlyList<PoolJob> Jobs => this.snapshot.Jobs;

    /// <summary>The team types the pool names, ordered by name.</summary>
    public IReadOnlyList<PoolTeamType> TeamTypes => this.snapshot.TeamTypes;

    /// <summary>The entries that were rejected, with the reason.</summary>
    public IReadOnlyList<HiringPoolProblem> Problems => this.snapshot.Problems;

    /// <summary>Finds a valid job by id.</summary>
    /// <param name="id">The job id.</param>
    /// <returns>The job, or null when no valid job has that id.</returns>
    public PoolJob? FindJob(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.snapshot.JobsById.GetValueOrDefault(id);
    }

    /// <summary>Finds the pool's entry for a team type.</summary>
    /// <param name="id">The type id.</param>
    /// <returns>The entry, or null when the pool does not name that type.</returns>
    public PoolTeamType? FindTeamType(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.snapshot.TeamTypes.FirstOrDefault(type => string.Equals(type.Id, id, StringComparison.Ordinal));
    }

    /// <summary>Reads and parses the persona of a job.</summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The template, or null when the job is unknown or its persona is missing or does not parse.</returns>
    public CandidateTemplate? ReadPersona(string jobId)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        PoolJob? job = this.FindJob(jobId);
        string? text = job is null ? null : this.ReadFile(job.Persona);
        return text is null ? null : CandidateTemplateParser.Parse(text).Template;
    }

    /// <summary>Rereads the built-in pool and the user pool, replacing what is loaded, then raises <see cref="Changed"/>.</summary>
    public void Reload()
    {
        List<HiringPoolProblem> problems = [];
        HiringPoolFile? builtin = ParseFile("builtin:" + PoolFile, this.builtins.GetValueOrDefault(PoolFile), problems);
        HiringPoolFile? user = this.LoadUser(problems);

        Dictionary<string, PoolJob> merged = new(StringComparer.Ordinal);
        foreach (PoolJob job in (builtin?.Jobs ?? []).Concat(user?.Jobs ?? []))
        {
            merged[job.Id] = job;
        }

        Dictionary<string, PoolTeamType> types = new(StringComparer.Ordinal);
        foreach (PoolTeamType type in (builtin?.TeamTypes ?? []).Concat(user?.TeamTypes ?? []))
        {
            types[type.Id] = type;
        }

        Dictionary<string, PoolJob> valid = new(StringComparer.Ordinal);
        foreach (PoolJob job in merged.Values)
        {
            string? why = this.Reject(job);
            if (why is null)
            {
                valid[job.Id] = job;
            }
            else
            {
                problems.Add(new HiringPoolProblem(job.Id, PoolFile, why));
            }
        }

        foreach (HiringPoolProblem problem in problems)
        {
            this.logger.LogWarning("Hiring pool entry {Id} was not loaded from {Source}: {Message}", problem.Id, problem.Source, problem.Message);
        }

        List<PoolJob> jobs = [.. valid.Values.OrderBy(static j => j.Title, StringComparer.Ordinal).ThenBy(static j => j.Id, StringComparer.Ordinal)];
        List<PoolTeamType> typeList = [.. types.Values.OrderBy(static t => t.Name, StringComparer.Ordinal)];
        this.snapshot = new Snapshot(jobs, typeList, problems, valid);
        this.Changed?.Invoke();
    }

    /// <summary>Tells whether a pool-relative path is a plain relative path with no parent segments.</summary>
    /// <param name="relativePath">The value from a job.</param>
    /// <returns>True when the path may be resolved.</returns>
    private static bool IsSafe(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && !relativePath.Contains("..", StringComparison.Ordinal)
            && !relativePath.Contains(':')
            && !relativePath.StartsWith('/')
            && !relativePath.StartsWith('\\')
            && !Path.IsPathRooted(relativePath);
    }

    /// <summary>Reads every embedded built-in pool file, keyed by path below the pool prefix.</summary>
    /// <returns>Path, such as <c>personas/tutor.md</c>, to its text with <c>\n</c> line endings.</returns>
    private static Dictionary<string, string> ReadEmbedded()
    {
        Assembly assembly = typeof(HiringPoolCatalog).Assembly;
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            string name = resourceName.Replace('\\', '/');
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using StreamReader reader = new(stream);
            files[name[ResourcePrefix.Length..]] = reader.ReadToEnd().ReplaceLineEndings("\n");
        }

        return files;
    }

    /// <summary>Matches a valid job id.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>Loads the user pool file, if there is one.</summary>
    /// <param name="problems">Receives the problem when the file is rejected.</param>
    /// <returns>The parsed file, or null when there is none or it was rejected.</returns>
    private HiringPoolFile? LoadUser(List<HiringPoolProblem> problems)
    {
        string path = Path.Combine(this.userDir, PoolFile);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return ParseFile(path, File.ReadAllText(path), problems);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(new HiringPoolProblem(PoolFile, path, "The user pool file could not be read."));
            return null;
        }
    }

    /// <summary>Deserialises a pool file.</summary>
    /// <param name="source">The source label used in a problem.</param>
    /// <param name="text">The file text, or null when there is none.</param>
    /// <param name="problems">Receives the problem when the file is rejected.</param>
    /// <returns>The parsed file, or null when there is none or it is not valid pool JSON.</returns>
    private static HiringPoolFile? ParseFile(string source, string? text, List<HiringPoolProblem> problems)
    {
        if (text is null)
        {
            return null;
        }

        HiringPoolFile? file;
        try
        {
            file = JsonSerializer.Deserialize<HiringPoolFile>(text, TeamTypeJson.Options);
        }
        catch (JsonException)
        {
            problems.Add(new HiringPoolProblem(PoolFile, source, "The file is not valid hiring pool JSON."));
            return null;
        }

        if (file is null || file.SchemaVersion != 1)
        {
            problems.Add(new HiringPoolProblem(PoolFile, source, "The hiring pool file needs schemaVersion 1."));
            return null;
        }

        return file;
    }

    /// <summary>Says why a job cannot be offered.</summary>
    /// <param name="job">The job to check.</param>
    /// <returns>The reason, or null when the job is sound.</returns>
    private string? Reject(PoolJob job)
    {
        if (job.Id is null || !IdPattern().IsMatch(job.Id))
        {
            return "The job id must be lower-case letters, digits and hyphens, and start with a letter or digit.";
        }

        if (string.IsNullOrWhiteSpace(job.Title))
        {
            return "The job has no title.";
        }

        string? text = this.ReadFile(job.Persona);
        if (text is null)
        {
            return $"The job has no persona file at '{job.Persona}'.";
        }

        CandidateParseResult parsed = CandidateTemplateParser.Parse(text);
        return parsed.Template is null ? "The persona does not parse: " + string.Join(" ", parsed.Problems) : null;
    }

    /// <summary>Reads a pool file from the user folder first, then the built-ins.</summary>
    /// <param name="relativePath">The path from a job, relative to the pool folder.</param>
    /// <returns>The text, or null when the path is unsafe or no file exists.</returns>
    private string? ReadFile(string relativePath)
    {
        if (!IsSafe(relativePath))
        {
            return null;
        }

        string root = Path.GetFullPath(this.userDir);
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
        {
            try
            {
                return File.ReadAllText(full);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger.LogWarning(ex, "The hiring pool file {Path} could not be read.", full);
                return null;
            }
        }

        return this.builtins.GetValueOrDefault(relativePath.Replace('\\', '/'));
    }

    /// <summary>One consistent view of the loaded pool.</summary>
    /// <param name="Jobs">The valid jobs in display order.</param>
    /// <param name="TeamTypes">The named team types in display order.</param>
    /// <param name="Problems">The rejected entries.</param>
    /// <param name="JobsById">The valid jobs by id.</param>
    private sealed record Snapshot(
        IReadOnlyList<PoolJob> Jobs,
        IReadOnlyList<PoolTeamType> TeamTypes,
        IReadOnlyList<HiringPoolProblem> Problems,
        IReadOnlyDictionary<string, PoolJob> JobsById);
}
