using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// One install for the Team Memory Helper (Team Memory Helper spec §8): Research, managed by Ada, with Leo and Miriam as members and
/// the Project Atlas, over a temp data folder; the real memory writer, History, Library resolver, settings, budget, runner and
/// dispatcher; and a fake adapter process whose answers the test scripts.
/// </summary>
internal sealed class TeamMemoryHarness : IDisposable
{
    private readonly TempDataDir dir;
    private readonly MemoryFolderGate gate = new();
    private readonly HelperDispatcher dispatcher;

    /// <summary>Wires the install.</summary>
    /// <param name="configure">Changes the options before anything is built, or <see langword="null"/>.</param>
    /// <param name="supportsReparsePoints"><see langword="true"/> for a test that creates a junction, so the data folder is on the system temp folder.</param>
    public TeamMemoryHarness(Action<TeamOptions>? configure = null, bool supportsReparsePoints = false)
    {
        this.dir = new TempDataDir(supportsReparsePoints);
        this.Options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false }];
            configure?.Invoke(team);
        });
        this.Catalog.Teams = [new("Research", ["Atlas"], ["Leo", "Miriam", "Ada"], true) { Manager = "Ada" }, new("Marketing", [], ["Zed"], true)];
        this.Access = new TeamMemoryAccess(this.Catalog, name => string.Equals(name, "Chief", StringComparison.OrdinalIgnoreCase));
        AgentPaths agentPaths = new(this.Options);
        this.Paths = new MemoryPaths(agentPaths, this.Access, this.Options);
        this.Writer = new MemoryWriter(this.gate, this.Options);

        DocHistoryStore historyStore = new(this.Options, NullLogger<DocHistoryStore>.Instance, this.Feed);
        this.History = historyStore;
        DocHistoryRecorder recorder = new(historyStore, this.Options, NullLogger<DocHistoryRecorder>.Instance);
        LibraryRootStore roots = new(this.Options, agentPaths, NullLogger<LibraryRootStore>.Instance);
        this.Resolver = new LibraryPathResolver(roots, this.Options, NullLogger<LibraryPathResolver>.Instance);

        this.SettingsStore = new HelperSettingsStore(this.Options);
        this.Settings = new HelperSettingsResolver(this.SettingsStore, this.Options, NullLogger<HelperSettingsResolver>.Instance);
        this.Status = new HelperStatusStore(this.Options);
        this.Budget = new HelperBudget(new HelperUsageStore(this.Options), this.Clock);
        this.Builder = new TeamMemoryInputBuilder(this.Access, this.Paths, new MemoryWarnings(NullLogger<MemoryWarnings>.Instance), NullLogger<TeamMemoryInputBuilder>.Instance);
        this.Trigger = new TeamMemoryTrigger(this.Builder, () => this.dispatcher ?? throw new InvalidOperationException("The dispatcher is built last."), NullLogger<TeamMemoryTrigger>.Instance);
        this.Helper = new TeamMemoryHelper(this.Paths, this.Writer, recorder, this.Resolver, this.Feed, this.Settings, this.Trigger, NullLogger<TeamMemoryHelper>.Instance);
        this.Registry = new HelperRegistry([this.Helper]);
        HelperRunner runner = new(
            this.Process,
            new AdapterCatalog(this.Options),
            new FakePromptSource(),
            this.Budget,
            this.Options,
            NullLogger<HelperRunner>.Instance,
            this.Clock);
        this.dispatcher = new HelperDispatcher(
            [new HelperInvoker<TeamMemoryInput, TeamMemoryVerdict>(this.Helper, runner, NullLogger.Instance)],
            this.Settings,
            this.Status,
            this.Options,
            NullLogger<HelperDispatcher>.Instance,
            this.Clock);
    }

    /// <summary>The options of this install.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The Teams.</summary>
    public FakeTeamCatalog Catalog { get; } = new();

    /// <summary>Who reads and writes which Team folder.</summary>
    public TeamMemoryAccess Access { get; }

    /// <summary>Resolves memory folders.</summary>
    public MemoryPaths Paths { get; }

    /// <summary>The shared memory writer.</summary>
    public MemoryWriter Writer { get; }

    /// <summary>The documents' History.</summary>
    public DocHistoryStore History { get; }

    /// <summary>Finds a file in the Library.</summary>
    public LibraryPathResolver Resolver { get; }

    /// <summary>Receives every Feed item.</summary>
    public RecordingFeedPublisher Feed { get; } = new();

    /// <summary>The Human's Helper settings.</summary>
    public HelperSettingsStore SettingsStore { get; }

    /// <summary>Resolves the Helper's effective settings.</summary>
    public HelperSettingsResolver Settings { get; }

    /// <summary>The Helper's last result and error.</summary>
    public HelperStatusStore Status { get; }

    /// <summary>The daily cap.</summary>
    public HelperBudget Budget { get; }

    /// <summary>Builds inputs.</summary>
    public TeamMemoryInputBuilder Builder { get; }

    /// <summary>Queues proposals.</summary>
    public TeamMemoryTrigger Trigger { get; }

    /// <summary>The Helper.</summary>
    public TeamMemoryHelper Helper { get; }

    /// <summary>The registered Helpers.</summary>
    public HelperRegistry Registry { get; }

    /// <summary>The fake adapter process; set <see cref="FakeHelperProcessRunner.Text"/> or its handler.</summary>
    public FakeHelperProcessRunner Process { get; } = new();

    /// <summary>The clock the dispatcher waits on.</summary>
    public FiringTimeProvider Clock { get; } = new();

    /// <summary>The memory folder of Research, or of one of its Projects.</summary>
    /// <param name="project">The Project, or <see langword="null"/> for the Team-wide folder.</param>
    public string Folder(string? project = null)
    {
        string teams = Path.GetFullPath(Path.Combine(this.Options.Value.DataDir, this.Options.Value.Teams.Dir));
        return project is null ? Path.Combine(teams, "Research", "memory") : Path.Combine(teams, "Research", project, "memory");
    }

    /// <summary>Writes a memory file straight to disk, with a description, as the Human's editor would.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="description">The description.</param>
    /// <param name="body">The body.</param>
    /// <param name="project">The Project, or <see langword="null"/> for the Team-wide folder.</param>
    /// <returns>The file's full path.</returns>
    public string Put(string file, string description, string body, string? project = null)
    {
        string folder = this.Folder(project);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, file);
        File.WriteAllText(path, MemoryFileRules.Compose(description, body));
        return path;
    }

    /// <summary>The tool as <paramref name="persona"/> would be offered it.</summary>
    /// <param name="persona">The calling Persona's Name.</param>
    public ProposeTeamMemoryTool ToolFor(string persona) =>
        new(persona, this.Access, this.Trigger, this.Registry, this.Settings, this.Budget, new FakePromptSource(), this.Options);

    /// <summary>Calls the tool as <paramref name="persona"/>.</summary>
    /// <param name="persona">The caller.</param>
    /// <param name="team">The <c>team</c> argument.</param>
    /// <param name="fact">The <c>fact</c> argument.</param>
    /// <param name="why">The <c>why</c> argument, or <see langword="null"/>.</param>
    public Task<string> ProposeAsync(string persona, string team, string fact, string? why = null)
    {
        JsonObject arguments = new() { ["team"] = team, ["fact"] = fact };
        if (why is not null)
        {
            arguments["why"] = why;
        }

        return this.ToolFor(persona).InvokeAsync(arguments, TestContext.Current.CancellationToken);
    }

    /// <summary>A proposal from Leo to Research.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="why">Where it came from, or <see langword="null"/>.</param>
    public static TeamMemoryProposal Proposal(string fact = "Interviews are recorded only with written consent.", string? why = "Agreed with the Human") =>
        new(Guid.NewGuid(), "Research", fact, why, "Leo");

    /// <summary>Lets the dispatcher's wait pass.</summary>
    public void PassTheWait() => this.Clock.Advance(TimeSpan.FromSeconds(11));

    /// <summary>Waits until the fake process has been called <paramref name="count"/> times.</summary>
    /// <param name="count">The number of calls.</param>
    public Task CallsAsync(int count) =>
        Eventually.UntilAsync(() => this.Process.Calls >= count, TestContext.Current.CancellationToken, TestBudgets.Standard);

    /// <summary>Waits until the Helper has recorded a result or an error.</summary>
    public Task RecordedAsync() =>
        Eventually.UntilAsync(
            () => this.Status.Get(TeamMemoryHelper.HelperId) is { LastResult: not null } or { LastError: not null },
            TestContext.Current.CancellationToken,
            TestBudgets.Standard);

    /// <summary>Applies a Verdict to an input directly, as the dispatcher would after a run.</summary>
    /// <param name="input">The input.</param>
    /// <param name="verdict">The Verdict.</param>
    public Task<HelperApplied> ApplyAsync(TeamMemoryInput input, TeamMemoryVerdict verdict) =>
        this.Helper.ApplyAsync(input.Proposal.Id.ToString(), input, verdict, TestContext.Current.CancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.dispatcher.Dispose();
        this.gate.Dispose();
        this.dir.Dispose();
    }
}
