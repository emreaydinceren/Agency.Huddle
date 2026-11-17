using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Memory;

/// <summary>
/// One install for the Memory Tidy pass, the recovery and the runner: a temp data folder, a real Persona catalog with the Agents it is given, the real
/// writer, backups, finisher, pass and recovery, a recording Feed and a clock fixed at 2026-11-16 noon UTC in a UTC zone.
/// </summary>
internal sealed class TidyRig : IDisposable
{
    private readonly TempDataDir dir;
    private readonly MemoryFolderGate gate = new();

    /// <summary>Builds the install with one Persona file per name.</summary>
    /// <param name="agents">The Agents in the catalog.</param>
    public TidyRig(params string[] agents)
        : this(null, agents)
    {
    }

    /// <summary>Builds the install with one Persona file per name and the options changed.</summary>
    /// <param name="configure">Changes the options first, or <see langword="null"/>.</param>
    /// <param name="agents">The Agents in the catalog.</param>
    public TidyRig(Action<TeamOptions>? configure, params string[] agents)
        : this(configure, false, agents)
    {
    }

    /// <summary>Builds the install with one Persona file per name, in a data folder where directory links can be made and followed.</summary>
    /// <param name="supportsReparsePoints"><see langword="true"/> for a test that creates a junction or link.</param>
    /// <param name="agents">The Agents in the catalog.</param>
    public TidyRig(bool supportsReparsePoints, params string[] agents)
        : this(null, supportsReparsePoints, agents)
    {
    }

    private TidyRig(Action<TeamOptions>? configure, bool supportsReparsePoints, string[] agents)
    {
        this.dir = new TempDataDir(supportsReparsePoints);
        this.Options = this.dir.Options(configure);
        foreach (string agent in agents)
        {
            PersonaSupervisorTestSupport.WritePersonaFile(this.Options, agent);
        }

        this.Paths = new AgentPaths(this.Options);
        this.Personas = this.Options.CreatePersonaStore();
        MemoryPaths memoryPaths = new(this.Paths, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), this.Options);
        this.MemoryPaths = memoryPaths;
        this.Writer = new MemoryWriter(this.gate, this.Options);
        this.Backups = new MemoryTidyBackups(this.Options, this.Clock);
        LibraryRootStore roots = new(this.Options, this.Paths, NullLogger<LibraryRootStore>.Instance);
        LibraryPathResolver resolver = new(roots, this.Options, NullLogger<LibraryPathResolver>.Instance);
        this.Finisher = new MemoryTidyFinisher(memoryPaths, resolver, this.Feed, this.Backups, this.Options);
        this.Pass = new MemoryTidyPass(this.Personas, memoryPaths, this.Writer, this.Backups, this.Finisher, this.Options, NullLogger<MemoryTidyPass>.Instance, this.Clock);
        this.Recovery = new MemoryTidyRecovery(this.Backups, this.Finisher, NullLogger<MemoryTidyRecovery>.Instance);
    }

    /// <summary>The options of the install.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The data folder.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The clock the pass, the backups and the finisher read.</summary>
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    /// <summary>The Feed every item goes to.</summary>
    public RecordingFeedPublisher Feed { get; } = new();

    /// <summary>Where the Agents live.</summary>
    public AgentPaths Paths { get; }

    /// <summary>The Persona catalog.</summary>
    public PersonaStore Personas { get; }

    /// <summary>Resolves an Agent's own memory folder.</summary>
    public MemoryPaths MemoryPaths { get; }

    /// <summary>The one write path of a memory file; a test sets its seam to write between the read and the prune.</summary>
    public MemoryWriter Writer { get; }

    /// <summary>The backups root.</summary>
    public MemoryTidyBackups Backups { get; }

    /// <summary>Settles, lists, posts and finishes a pass.</summary>
    public MemoryTidyFinisher Finisher { get; }

    /// <summary>The pass under test.</summary>
    public MemoryTidyPass Pass { get; }

    /// <summary>The start-up recovery under test.</summary>
    public MemoryTidyRecovery Recovery { get; }

    /// <summary>The path of an Agent's <c>reported.md</c>.</summary>
    /// <param name="agent">The Agent's Name.</param>
    public string ReportedPath(string agent) => Path.Combine(this.MemoryDir(agent), ReportedFile.FileName);

    /// <summary>The path of an Agent's memory folder.</summary>
    /// <param name="agent">The Agent's Name.</param>
    public string MemoryDir(string agent) => Path.Combine(this.Paths.WorkDir(agent), "memory");

    /// <summary>Writes a file into an Agent's memory folder and returns its version.</summary>
    /// <param name="agent">The Agent's Name.</param>
    /// <param name="file">The file name.</param>
    /// <param name="text">The whole text.</param>
    public string Seed(string agent, string file, string text)
    {
        Directory.CreateDirectory(this.MemoryDir(agent));
        string path = Path.Combine(this.MemoryDir(agent), file);
        File.WriteAllText(path, text);
        return MemoryVersion.Of(File.ReadAllBytes(path));
    }

    /// <summary>Makes a pass folder with a journal, as a stopped or killed pass leaves it.</summary>
    /// <param name="id">The pass folder's name.</param>
    /// <param name="entries">The journal's entries, in order.</param>
    /// <returns>The pass folder's path.</returns>
    public string SeedPass(string id, params MemoryTidyEntry[] entries)
    {
        string folder = Path.Combine(this.Backups.RootPath, id);
        Directory.CreateDirectory(folder);
        MemoryTidyJournal journal = new(folder);
        foreach (MemoryTidyEntry entry in entries)
        {
            journal.Append(entry);
        }

        return folder;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Personas.Dispose();
        this.gate.Dispose();
        this.dir.Dispose();
    }
}
