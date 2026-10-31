using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.NeedsYou;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IProofSource = Agency.Huddle.App.Tasks.HumanGate.IProofSource;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;
using ProofItemSource = Agency.Huddle.App.Teams.ProofItemSource;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Everything a <c>FeedCard</c> reads when it expands, over real stores in temporary folders: the Feed store and queue, a Task store, the
/// Team directory, the Transcript, the Needs You tracker, the Library and the Document History. The clock stands at <see cref="FeedTestData.Now"/>.
/// </summary>
internal sealed class FeedCardHost : IDisposable
{
    private readonly FeedPanelHost feed = new();
    private readonly TaskToolHarness tasks;
    private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
    private readonly TempDataDir helperDir = new();

    private FeedCardHost(NeedsYouFixture needsYou)
    {
        this.NeedsYou = needsYou;
        this.Clock = new ManualTimeProvider(FeedTestData.Now, TimeZoneInfo.Utc);
        this.tasks = new TaskToolHarness(this.Clock);
        this.History = new DocHistoryStore(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        Microsoft.Extensions.Options.IOptions<TeamOptions> helperOptions = this.helperDir.Options();
        this.HelperStatuses = new HelperStatusStore(helperOptions);
        this.HelperBudget = new HelperBudget(new HelperUsageStore(helperOptions), this.Clock);
        this.HelperSettings = new HelperSettingsResolver(new HelperSettingsStore(helperOptions), helperOptions, NullLogger<HelperSettingsResolver>.Instance);
    }

    /// <summary>The Team catalog the card's Team chip reads its avatar from; empty until a test sets <see cref="FakeTeamCatalog.Teams"/>.</summary>
    public FakeTeamCatalog Teams { get; } = new();

    /// <summary>The one registered Helper, a scripted stand-in with the id <c>NeedsHuman</c>.</summary>
    public ScriptedHelper Helper { get; } = new("NeedsHuman");

    /// <summary>The Helpers' last results and errors, which the Helper preview reads.</summary>
    public HelperStatusStore HelperStatuses { get; }

    /// <summary>The Helpers' daily usage, which the Helper preview reads.</summary>
    public HelperBudget HelperBudget { get; }

    /// <summary>The Helpers' effective settings, which the Helper preview reads.</summary>
    public HelperSettingsResolver HelperSettings { get; }

    /// <summary>The Feed store, queue and events.</summary>
    public FeedPanelHost Feed => this.feed;

    /// <summary>The Task side.</summary>
    public TaskToolHarness Tasks => this.tasks;

    /// <summary>The Room side.</summary>
    public NeedsYouFixture NeedsYou { get; }

    /// <summary>The Library stack.</summary>
    public LibraryFileServiceFixture Library => this.library;

    /// <summary>The Document History.</summary>
    public DocHistoryStore History { get; }

    /// <summary>The clock every component reads.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>The Proof every Task answers with; two items, one met, by default.</summary>
    public Proof Proof { get; set; } = new(
    [
        new ProofItem("Draft written", true, "Nova", "done in the Room", ProofItemSource.Process),
        new ProofItem("Citation check", false, "Ruth", null, ProofItemSource.Process),
    ]);

    /// <summary>Builds the host.</summary>
    /// <param name="ct">Cancels the directory's initialisation.</param>
    /// <returns>The host; the caller disposes it.</returns>
    public static async Task<FeedCardHost> CreateAsync(CancellationToken ct) => new(await NeedsYouFixture.CreateAsync(ct));

    /// <summary>A bUnit context holding what the card, its previews and its related list inject.</summary>
    /// <returns>The context; the caller disposes it.</returns>
    public MudBunitContext NewContext()
    {
        MudBunitContext ctx = FeedTestData.NewContext(this.library);
        this.tasks.AddTo(ctx.Services);
        this.feed.AddTo(ctx.Services);
        ctx.Services.AddSingleton<TimeProvider>(this.Clock);
        ctx.Services.AddSingleton(this.NeedsYou.Tracker);
        ctx.Services.AddSingleton<ITeamDirectory>(this.NeedsYou.Team);
        ctx.Services.AddSingleton<IChatStore>(this.NeedsYou.Transcript);
        ctx.Services.AddSingleton(this.History);
        ctx.Services.AddSingleton<IProofSource>(new FixedProof(this));
        ctx.Services.AddSingleton<ITaskReferenceResolver>(this.tasks.Store);
        ctx.Services.AddSingleton(new HelperRegistry([this.Helper]));
        ctx.Services.AddSingleton(this.HelperStatuses);
        ctx.Services.AddSingleton(this.HelperBudget);
        ctx.Services.AddSingleton(this.HelperSettings);
        ctx.Services.AddSingleton<ITeamCatalog>(this.Teams);
        return ctx;
    }

    /// <summary>Seeds a Task on disk.</summary>
    /// <param name="id">The Task id.</param>
    /// <param name="status">Its status.</param>
    /// <param name="gated">Whether it needs the Human's acceptance.</param>
    /// <param name="assignee">Who holds it.</param>
    /// <param name="due">Its due date.</param>
    /// <param name="originRoomId">The Room it came from.</param>
    /// <param name="description">Its body.</param>
    /// <returns>The Task as written.</returns>
    public TaskItem SeedTask(string id, TaskState status = TaskState.InProgress, bool gated = false, string? assignee = "Nova", DateOnly? due = null, string? originRoomId = null, string? description = null) =>
        this.tasks.SeedOnDisk(TestTasks.Make(
            id: id,
            title: "Draft the pricing page",
            status: status,
            assignee: assignee,
            dueDate: due,
            originRoomId: originRoomId,
            description: description ?? "Body",
            location: new TaskLocation("Growth", null, false),
            changeLog: [TestTasks.Entry("2026-10-12T09:00:00Z", "Ada", "assignee: none → " + assignee)]) with { HumanGate = gated });

    /// <summary>Posts a Feed item straight into the store.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="target">The target key.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="notify">Whether it is a Notification.</param>
    /// <param name="minutesAgo">How long before now it happened.</param>
    /// <returns>The stored item.</returns>
    public FeedItem Post(FeedSourceType type, string target, FeedPayload payload, bool notify = false, int minutesAgo = 0)
    {
        FeedMessage message = new(type, target, new FeedActor(FeedActorKind.Agent, "Nova"), notify, payload);
        return this.feed.Store.Apply([new PostCommand(message, FeedTestData.Now.AddMinutes(-minutesAgo))]).Added.Single();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.tasks.Dispose();
        this.feed.Dispose();
        this.library.Dispose();
        this.helperDir.Dispose();
        this.NeedsYou.Dispose();
    }

    private sealed class FixedProof(FeedCardHost host) : IProofSource
    {
        public Proof For(TaskItem task) => host.Proof;
    }
}
