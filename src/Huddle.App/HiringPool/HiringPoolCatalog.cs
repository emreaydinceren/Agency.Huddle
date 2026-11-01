using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.HiringPool;

/// <summary>
/// Loads and serves the hiring pool: the global catalog of jobs a team type draws its roles from.
/// </summary>
/// <remarks>
/// The built-in pool is the embedded <c>HiringPool/hiringpool.json</c> and its <c>personas/*.md</c>. A user pool,
/// <c>{DataDir}/HiringPool/hiringpool.json</c>, is merged by id on top: a user job replaces a shipped job of the same id and
/// never hides the rest, and a user job that is rejected leaves the shipped job in place. A persona resolves the user folder
/// first, then the shipped file, and a user persona that does not parse falls back to the shipped one. A bad entry is excluded
/// and listed in <see cref="Problems"/>; loading never throws for bad content.
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
        return job is null ? null : this.ParsePersona(job.Persona).Result.Template;
    }

    /// <summary>Rereads the built-in pool and the user pool, replacing what is loaded, then raises <see cref="Changed"/>.</summary>
    public void Reload()
    {
        List<HiringPoolProblem> problems = [];
        string builtinSource = "builtin:" + PoolFile;
        string userSource = Path.Combine(this.userDir, PoolFile);
        Dictionary<string, PoolJob> valid = new(StringComparer.Ordinal);
        Dictionary<string, PoolTeamType> types = new(StringComparer.Ordinal);
        this.Accept(ParseFile(builtinSource, this.builtins.GetValueOrDefault(PoolFile), problems), builtinSource, valid, types, problems);
        this.Accept(LoadUser(userSource, problems), userSource, valid, types, problems);

        foreach (HiringPoolProblem problem in problems)
        {
            this.logger.LogWarning("Hiring pool entry {Id} was not loaded from {Source}: {Message}", problem.Id, problem.Source, problem.Message);
        }

        List<PoolJob> jobs = [.. valid.Values.OrderBy(static j => j.Title, StringComparer.Ordinal).ThenBy(static j => j.Id, StringComparer.Ordinal)];
        List<PoolTeamType> typeList = [.. types.Values.OrderBy(static t => t.Name, StringComparer.Ordinal)];
        this.snapshot = new Snapshot(jobs, typeList, problems, valid);
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>Reads every embedded built-in pool file, keyed by path below the pool prefix.</summary>
    /// <returns>Path, such as <c>personas/tutor.md</c>, to its text with <c>\n</c> line endings.</returns>
    private static Dictionary<string, string> ReadEmbedded()
    {
        return EmbeddedResources.ReadFolder(typeof(HiringPoolCatalog).Assembly, ResourcePrefix);
    }

    /// <summary>Matches a valid job id.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

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

    /// <summary>Adds the sound entries of a pool file on top of what is already accepted; a rejected job leaves an accepted job of the same id in place.</summary>
    /// <param name="file">The parsed file, or null when there is none.</param>
    /// <param name="source">The source label used in a problem.</param>
    /// <param name="valid">The accepted jobs by id.</param>
    /// <param name="types">The named team types by id.</param>
    /// <param name="problems">Receives one problem per rejected entry.</param>
    private void Accept(HiringPoolFile? file, string source, Dictionary<string, PoolJob> valid, Dictionary<string, PoolTeamType> types, List<HiringPoolProblem> problems)
    {
        if (file is null)
        {
            return;
        }

        foreach (PoolTeamType? type in file.TeamTypes)
        {
            if (type?.Id is null)
            {
                problems.Add(new HiringPoolProblem(PoolFile, source, "A team type entry has no id."));
                continue;
            }

            types[type.Id] = type;
        }

        foreach (PoolJob? job in file.Jobs)
        {
            if (job?.Id is null)
            {
                problems.Add(new HiringPoolProblem(PoolFile, source, "A job entry has no id."));
                continue;
            }

            string? why = this.Reject(job);
            if (why is null)
            {
                valid[job.Id] = job;
            }
            else
            {
                problems.Add(new HiringPoolProblem(job.Id, source, why));
            }
        }
    }

    /// <summary>Loads the user pool file, if there is one.</summary>
    /// <param name="path">The user pool file.</param>
    /// <param name="problems">Receives the problem when the file is rejected.</param>
    /// <returns>The parsed file, or null when there is none or it was rejected.</returns>
    private static HiringPoolFile? LoadUser(string path, List<HiringPoolProblem> problems)
    {
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

    /// <summary>Says why a job cannot be offered.</summary>
    /// <param name="job">The job to check.</param>
    /// <returns>The reason, or null when the job is sound.</returns>
    private string? Reject(PoolJob job)
    {
        if (!IdPattern().IsMatch(job.Id))
        {
            return "The job id must be lower-case letters, digits and hyphens, and start with a letter or digit.";
        }

        if (string.IsNullOrWhiteSpace(job.Title))
        {
            return "The job has no title.";
        }

        (bool found, CandidateParseResult result) = this.ParsePersona(job.Persona);
        if (!found)
        {
            return $"The job has no persona file at '{job.Persona}'.";
        }

        return result.Template is null ? "The persona does not parse: " + string.Join(' ', result.Problems) : null;
    }

    /// <summary>Parses a persona from the user folder, falling back to the shipped file when the user's does not parse.</summary>
    /// <param name="relativePath">The path from a job, relative to the pool folder.</param>
    /// <returns>Whether any file was found, and the first parse that produced a template (or the first failure when none did).</returns>
    private (bool Found, CandidateParseResult Result) ParsePersona(string relativePath)
    {
        CandidateParseResult? failure = null;
        if (TeamTypeCatalog.IsSafe(relativePath))
        {
            foreach (string? text in new[] { this.ReadUserFile(relativePath), this.builtins.GetValueOrDefault(relativePath.Replace('\\', '/')) })
            {
                if (text is null)
                {
                    continue;
                }

                CandidateParseResult parsed = CandidateTemplateParser.Parse(text);
                if (parsed.Template is not null)
                {
                    return (true, parsed);
                }

                failure ??= parsed;
            }
        }

        return failure is null ? (false, new CandidateParseResult(null, [])) : (true, failure);
    }

    /// <summary>Reads a file from the user pool folder, refusing a path that a junction or link takes outside it.</summary>
    /// <param name="relativePath">A path already checked as plain and relative.</param>
    /// <returns>The text, or null when there is no such file or it may not be read.</returns>
    private string? ReadUserFile(string relativePath)
    {
        string root = Path.GetFullPath(this.userDir);
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!PathContainment.IsInside(full, root, includeRoot: false, PathContainment.Comparison) || !File.Exists(full) || TeamTypeCatalog.LeavesTypeFolder(root, full))
        {
            return null;
        }

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
