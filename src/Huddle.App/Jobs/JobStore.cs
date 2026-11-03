using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Watching;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Every Job, read from each owner's <c>schedule.md</c>: beside each Agent's definition, and in <c>{DataDir}/Human/</c> for
/// the Human. The files are the source of truth, so a hand edit is picked up by a watcher and every change Huddle makes is a
/// rewrite of the owner's file. A section that cannot be read is listed in <see cref="Problems"/> and kept in the file.
/// </summary>
internal sealed class JobStore : IDisposable
{
    private readonly PersonaStore personas;
    private readonly string humanFolder;
    private readonly ILogger<JobStore> logger;
    private readonly Lock gate = new();
    private readonly DebouncedFileWatcher agentsWatcher;
    private readonly DebouncedFileWatcher humanWatcher;
    private Snapshot snapshot = Snapshot.Empty;
    private bool disposed;

    /// <summary>Reads every <c>schedule.md</c> and starts watching for edits.</summary>
    /// <param name="personas">Lists the Agents, whose folders hold their files, and says when they change.</param>
    /// <param name="options">Supplies the data folder, which holds the Human's file.</param>
    /// <param name="logger">Reports a file that could not be read.</param>
    public JobStore(PersonaStore personas, IOptions<TeamOptions> options, ILogger<JobStore> logger)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.personas = personas;
        this.logger = logger;
        this.humanFolder = Path.Combine(options.Value.DataDir, JobFiles.HumanFolder);
        _ = Directory.CreateDirectory(this.humanFolder);
        _ = Directory.CreateDirectory(personas.Paths.DefinitionsRoot);

        this.Reload();
        this.agentsWatcher = new DebouncedFileWatcher(personas.Paths.DefinitionsRoot, JobFiles.FileName, this.OnSettled, logger, nameof(JobStore), includeSubdirectories: true);
        this.humanWatcher = new DebouncedFileWatcher(this.humanFolder, JobFiles.FileName, this.OnSettled, logger, nameof(JobStore));
        personas.PersonasChanged += this.OnSettled;
    }

    /// <summary>A Job was added, changed or removed, by Huddle or in a file. Raised outside the lock.</summary>
    public event Action? Changed;

    /// <summary>Every Job that could be read: the Human's first, then each Agent's in Name order, each file in its own order.</summary>
    public IReadOnlyList<Job> Jobs
    {
        get
        {
            lock (this.gate)
            {
                return this.snapshot.Jobs;
            }
        }
    }

    /// <summary>Every section that could not be read, with why.</summary>
    public IReadOnlyList<JobProblem> Problems
    {
        get
        {
            lock (this.gate)
            {
                return this.snapshot.Problems;
            }
        }
    }

    /// <summary>Finds a Job by id.</summary>
    /// <param name="id">The id, ignoring case.</param>
    /// <returns>The Job, or <see langword="null"/>.</returns>
    public Job? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return this.Jobs.FirstOrDefault(job => string.Equals(job.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds a Job to its owner's schedule, writing the file.</summary>
    /// <param name="job">The Job; its <see cref="Job.Id"/> is ignored and a new one given.</param>
    /// <returns>The Job as stored, with its id.</returns>
    /// <exception cref="KeyNotFoundException">The owner is an Agent that does not exist.</exception>
    public Job Add(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        Job stored = job with { Id = this.NewId() };
        this.Edit(stored.Owner, sections => [.. sections, Section(stored)]);
        return stored;
    }

    /// <summary>Replaces a Job by id, moving it to another owner's file when its owner changed.</summary>
    /// <param name="job">The Job's new form.</param>
    /// <returns><see langword="true"/> when the Job existed.</returns>
    public bool Update(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (this.Find(job.Id) is not { } current)
        {
            return false;
        }

        if (current.Owner == job.Owner)
        {
            this.Edit(job.Owner, sections => [.. sections.Select(section => IsJob(section, job.Id) ? Section(job) : section)]);
        }
        else
        {
            this.Edit(job.Owner, sections => [.. sections, Section(job)]);
            this.Edit(current.Owner, sections => [.. sections.Where(section => !IsJob(section, job.Id))]);
        }

        return true;
    }

    /// <summary>Removes a Job from its owner's file, deleting the file when it was the last section.</summary>
    /// <param name="id">The Job's id.</param>
    /// <returns><see langword="true"/> when the Job existed.</returns>
    public bool Remove(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (this.Find(id) is not { } current)
        {
            return false;
        }

        this.Edit(current.Owner, sections => [.. sections.Where(section => !IsJob(section, current.Id))]);
        return true;
    }

    /// <summary>The <c>schedule.md</c> of an owner.</summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The file's path; the file may not exist.</returns>
    /// <exception cref="KeyNotFoundException">The owner is an Agent that does not exist.</exception>
    /// <exception cref="InvalidOperationException">The Agent's folder is named <c>schedule</c>, so its definition is that file.</exception>
    public string FileOf(JobOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner.AgentName is not { } name)
        {
            return Path.Combine(this.humanFolder, JobFiles.FileName);
        }

        PersonaEntry entry = this.personas.ResolveByNameOrAlias(name) ?? throw new KeyNotFoundException($"No Agent is called '{name}'.");
        string file = Path.Combine(Path.GetDirectoryName(entry.Path) ?? this.personas.Paths.DefinitionsRoot, JobFiles.FileName);
        return JobFiles.IsSidecar(file)
            ? file
            : throw new InvalidOperationException($"{entry.Name}'s folder is named like its schedule file, so it cannot hold Jobs.");
    }

    /// <summary>Stops watching.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.personas.PersonasChanged -= this.OnSettled;
        this.agentsWatcher.Dispose();
        this.humanWatcher.Dispose();
    }

    private static JobSection Section(Job job) => new(job, string.Empty, job.Title, null);

    private static bool IsJob(JobSection section, string id) =>
        section.Job is { } job && string.Equals(job.Id, id, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the owner's file fresh, applies <paramref name="change"/>, writes or deletes it, then reloads every file.</summary>
    private void Edit(JobOwner owner, Func<IReadOnlyList<JobSection>, IReadOnlyList<JobSection>> change)
    {
        string file = this.FileOf(owner);
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            IReadOnlyList<JobSection> current = File.Exists(file) ? JobDocument.Parse(File.ReadAllText(file), owner) : [];
            IReadOnlyList<JobSection> changed = change(current);
            if (changed.Count == 0)
            {
                File.Delete(file);
            }
            else
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(file) ?? this.humanFolder);
                File.WriteAllText(file, JobDocument.Write(changed));
            }

            this.snapshot = this.Read() ?? this.snapshot;
        }

        this.RaiseChanged();
    }

    private void OnSettled()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }
        }

        this.Reload();
    }

    private void Reload()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.snapshot = this.Read() ?? this.snapshot;
        }

        this.RaiseChanged();
    }

    private void RaiseChanged() =>
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));

    /// <summary>Reads every owner's file, or returns <see langword="null"/> when one could not be read, keeping the current snapshot.</summary>
    private Snapshot? Read()
    {
        List<(JobOwner Owner, string File)> files = [(JobOwner.Human, Path.Combine(this.humanFolder, JobFiles.FileName))];
        foreach (PersonaEntry entry in this.personas.Entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            string file = Path.Combine(Path.GetDirectoryName(entry.Path) ?? string.Empty, JobFiles.FileName);
            if (JobFiles.IsSidecar(file))
            {
                files.Add((JobOwner.Agent(entry.Name), file));
            }
        }

        List<Job> jobs = [];
        List<JobProblem> problems = [];
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach ((JobOwner owner, string file) in files)
        {
            IReadOnlyList<JobSection>? sections = RetryingFileRead.RereadKeepingPrevious<IReadOnlyList<JobSection>>(
                file, static () => [], () => JobDocument.Parse(File.ReadAllText(file), owner), this.logger, "schedule file", "Jobs");
            if (sections is null)
            {
                return null;
            }

            foreach (JobSection section in sections)
            {
                if (section.Job is not { } job)
                {
                    problems.Add(new JobProblem(owner, file, section.Title, section.Problem ?? string.Empty));
                }
                else if (!ids.Add(job.Id))
                {
                    problems.Add(new JobProblem(owner, file, section.Title, string.Create(CultureInfo.InvariantCulture, $"Another Job already has the id '{job.Id}'.")));
                }
                else
                {
                    jobs.Add(job);
                }
            }
        }

        return new Snapshot(jobs, problems);
    }

    private string NewId()
    {
        string id;
        do
        {
            id = "job-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6];
        }
        while (this.Find(id) is not null);

        return id;
    }

    /// <summary>What the store read last.</summary>
    /// <param name="Jobs">The Jobs.</param>
    /// <param name="Problems">The sections that could not be read.</param>
    private sealed record Snapshot(IReadOnlyList<Job> Jobs, IReadOnlyList<JobProblem> Problems)
    {
        public static Snapshot Empty { get; } = new([], []);
    }
}
