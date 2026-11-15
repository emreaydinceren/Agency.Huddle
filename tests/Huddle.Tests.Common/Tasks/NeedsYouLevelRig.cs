using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A Task service and a merge gate wired to the same Needs You levels (ADR-0087): Team Platform has the gates the caller names, its Project
/// <c>Site</c> and the Team itself may set a level, and the global level is the configured one. Shared by the Task service and merge gate tests.
/// </summary>
internal sealed class NeedsYouLevelRig : IDisposable
{
    /// <summary>The Team every Task lives in.</summary>
    public const string Team = TaskGateText.Team;

    /// <summary>The Project whose file may set a level.</summary>
    public const string Project = "Site";

    /// <summary>The first instant of every test.</summary>
    public static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir gateDir = new();
    private readonly TempDataDir levelDir = new();

    /// <summary>Builds the rig.</summary>
    /// <param name="teamLevel">The Team file's level, or <see langword="null"/> for none.</param>
    /// <param name="projectLevel">The Project file's level, or <see langword="null"/> for none.</param>
    /// <param name="globalLevel">The configured global level text, or <see langword="null"/> for none.</param>
    /// <param name="gates">The Team's gates, comma-joined and quoted.</param>
    /// <param name="globalCap">The configured global spend cap in USD, or <see langword="null"/> for none.</param>
    public NeedsYouLevelRig(NeedsYouLevel? teamLevel = null, NeedsYouLevel? projectLevel = null, string? globalLevel = null, string gates = "'accept-before-merge'", decimal? globalCap = null)
    {
        this.Clock = new ManualTimeProvider(Noon);
        this.Feed = new RecordingFeedPublisher();
        this.Harness = new TaskToolHarness(clock: this.Clock, proof: new FixedProofSource(FixedProofSource.Complete), feed: this.Feed);
        string levelLine = teamLevel is { } level ? $"needsYouLevel: {level.ToWire()}\n" : string.Empty;
        FakeTeamFolders folders = new();
        string teamText = TaskGateText.Process(gates).Replace("checkCommand:", levelLine + "checkCommand:", StringComparison.Ordinal);
        ProcessGateService gateService = this.Gates = TaskGateText.BuildGates(this.gateDir, teamText, sharedFolders: folders);
        if (projectLevel is { } projectValue)
        {
            folders.ProjectFiles[Project] = ProjectFile.Empty with { NeedsYouLevel = projectValue };
        }

        this.Catalog = new FakeTeamCatalog { Teams = [new TeamSummary(Team, [Project], [], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
        this.Spend = new PersonaSpendStore(this.levelDir.Options());
        this.Caps = new NeedsYouCaps(new NeedsYouCapStore(this.levelDir.Options()), this.Spend, this.Catalog, this.Feed);
        this.Levels = new NeedsYouLevels(
            this.Catalog,
            folders,
            new NeedsYouLevelStore(this.levelDir.Options()),
            this.levelDir.Options(options =>
            {
                options.NeedsYouLevel.Level = globalLevel ?? string.Empty;
                options.NeedsYouLevel.SpendCap = globalCap;
            }),
            caps: this.Caps);
        this.Service = new TaskService(
            this.Harness.Store,
            this.Harness.Ids,
            this.Harness.Events,
            this.Harness.Personas,
            this.Harness.Sprints,
            this.Harness.Options,
            this.Clock,
            NullLogger<TaskService>.Instance,
            gateService,
            proof: new FixedProofSource(FixedProofSource.Complete),
            levels: this.Levels);
        this.Gate = new ProcessMergeGate(this.Harness.Personas, this.Harness.Store, gateService, this.Catalog, this.Clock, this.Levels);
    }

    /// <summary>The clock every service shares.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>What the Feed was told.</summary>
    public RecordingFeedPublisher Feed { get; }

    /// <summary>The Task harness whose stores and events the rig's service writes through.</summary>
    public TaskToolHarness Harness { get; }

    /// <summary>The Team catalog.</summary>
    public FakeTeamCatalog Catalog { get; }

    /// <summary>The persisted spend the cap counts.</summary>
    public PersonaSpendStore Spend { get; }

    /// <summary>The spend cap's counting and question.</summary>
    public NeedsYouCaps Caps { get; }

    /// <summary>Resolves the level of a Task.</summary>
    public NeedsYouLevels Levels { get; }

    /// <summary>The Team's process gates, which say whether <c>accept-before-merge</c> is on.</summary>
    public ProcessGateService Gates { get; }

    /// <summary>The Task service under test, which knows the levels.</summary>
    public TaskService Service { get; }

    /// <summary>The merge gate under test, which knows the levels.</summary>
    public ProcessMergeGate Gate { get; }

    /// <summary>The Human's actor.</summary>
    public TaskActor Human => TaskActors.Human(this.Harness.Options.Value);

    /// <summary>Nova, the Team's Agent.</summary>
    public static TaskActor Nova { get; } = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Creates an ungated Task of Nova's in Review as the Human, in the Project when given; nothing moves because the Human wrote it.</summary>
    /// <param name="project">The Project, or <see langword="null"/> for work directly under the Team.</param>
    public TaskItem CreateInReview(string? project = null)
    {
        TaskDraft draft = new("Quote calculator", Team, project, TaskState.Review, Assignee: "Nova", Description: "Body");
        return Assert.IsType<TaskResult.Saved>(this.Service.Create(draft, this.Human)).Task;
    }

    /// <summary>Reads the Task as stored now.</summary>
    /// <param name="task">The Task.</param>
    public TaskItem Read(TaskItem task) => this.Harness.Store.Get(task.Id) ?? throw new InvalidOperationException("The Task is missing.");

    /// <summary>Adds one passing Evidence line to the Task as Nova, which is the write that completes the Proof.</summary>
    /// <param name="task">The Task.</param>
    public TaskResult AddEvidence(TaskItem task) =>
        this.Service.AddEvidence(task.Id, new EvidenceLine(this.Clock.GetUtcNow(), "Nova", "dotnet test", 0, null, null, string.Empty), Nova);

    /// <summary>The release entries the Task's Change log holds.</summary>
    /// <param name="task">The Task.</param>
    public IReadOnlyList<ChangeLogEntry> Releases(TaskItem task) => [.. this.Read(task).ChangeLog.Where(static entry => NeedsYouRelease.IsRelease(entry.Summary))];

    /// <inheritdoc />
    public void Dispose()
    {
        this.Service.Dispose();
        this.Harness.Dispose();
        this.gateDir.Dispose();
        this.levelDir.Dispose();
    }
}
