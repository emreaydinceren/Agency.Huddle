using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using UiFakeAgentGateway = Agency.Huddle.Tests.Ui.FakeAgentGateway;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// A real, in-process Tasks stack over one <see cref="TempDataDir"/>: <see cref="TaskStore"/>,
/// <see cref="TaskService"/>, <see cref="TaskEvents"/>, <see cref="ViewStore"/>,
/// <see cref="TaskActivity"/>, <see cref="TurnActivity"/>, <see cref="TaskIdAllocator"/>,
/// <see cref="AvatarStore"/>, a <see cref="PersonaStore"/> seeded with Nova and Kai, a real
/// <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/>, <see cref="OwnPosts"/>, and a
/// never-started <see cref="TaskTriggerService"/>, plus the Ui <see cref="UiFakeAgentGateway"/>.
/// Every App Tool test (D10) and every Tasks-page/component test (D11/D12/D14) that needs a fully
/// wired, disk-backed Tasks feature without composing the whole application shares this one harness
/// rather than hand-rolling the same constructors (corrections-B4 D10 item 2, corrections-B5
/// "Upstream additions"). <see cref="AddTo(IServiceCollection)"/> registers exactly the services a
/// Razor component under test resolves through <c>@inject</c> - see each test file's own comment for
/// which of this harness's members its component actually needs.
/// </summary>
/// <remarks>
/// Everything above is built by the constructor itself and works with the plain
/// <c>new TaskToolHarness()</c> that D10's and D11's tests already use. <see cref="Directory"/>'s
/// on-disk <c>team.db</c> is seeded (the Human row, plus a Nova and Kai agent User) only by
/// <see cref="CreateAsync"/>, because that seeding is asynchronous; a harness built through the plain
/// constructor has a real but unseeded <see cref="Directory"/>, and <see cref="NovaId"/>/
/// <see cref="KaiId"/> stay <see langword="null"/>. D10 tools that resolve a caller or an assignee
/// through <see cref="Directory"/>, and D12/D14 tests that call <see cref="Triggers"/>'s
/// <see cref="TaskTriggerService.Preview"/>, construct the harness through <see cref="CreateAsync"/>.
/// </remarks>
internal sealed class TaskToolHarness : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly Agency.Huddle.App.Teams.ProcessGateService? gates;
    private readonly TaskFeedPublisher? feedPublisher;

    /// <summary>
    /// Builds the whole stack: a fresh <see cref="TempDataDir"/>, a real <see cref="PersonaStore"/>
    /// seeded with Nova and Kai, and every Tasks service constructed over it.
    /// </summary>
    /// <param name="clock">
    /// The <see cref="TimeProvider"/> every clock-taking service above shares, exposed as
    /// <see cref="Clock"/>; defaults to <see cref="TimeProvider.System"/> when omitted. Pass a fake
    /// clock (e.g. <see cref="Agency.Huddle.Tests.Acp.Fakes.ManualTimeProvider"/>) for a test that
    /// needs a deterministic Change log time (R8 facts "TaskToolHarness.Clock").
    /// </param>
    /// <param name="sprintsText">
    /// The text of <c>sprints.md</c> to start with. When given, it is written and <see cref="Sprints"/> is a real
    /// <see cref="SprintStore"/> over it (which subscribes to <see cref="TaskEvents.TaskChanged"/>); <see langword="null"/>
    /// (the default) means an <see cref="Agency.Huddle.Tests.Tasks.EmptySprintCatalog"/>, so tests that don't use Sprints
    /// see no extra subscriber.
    /// </param>
    /// <param name="gates">The process gates the Task service enforces on a status change; <see langword="null"/> (the default) enforces none.</param>
    /// <param name="proof">Supplies a Task's Proof to the Human gate rules; <see langword="null"/> (the default) means an empty, complete Proof.</param>
    /// <param name="feed">Receives the Feed items the Task service produces; <see langword="null"/> (the default) posts nothing.</param>
    /// <param name="levels">The Needs You levels the Task service records each created Task's scope in; <see langword="null"/> (the default) records none.</param>
    public TaskToolHarness(TimeProvider? clock = null, string? sprintsText = null, Agency.Huddle.App.Teams.ProcessGateService? gates = null, Agency.Huddle.App.Tasks.HumanGate.IProofSource? proof = null, Agency.Huddle.App.Feed.IFeedPublisher? feed = null, Agency.Huddle.App.Teams.NeedsYouLevels? levels = null)
    {
        this.Options = this.dir.Options();
        this.gates = gates;
        if (sprintsText is not null)
        {
            File.WriteAllText(this.SprintsFilePath, sprintsText);
        }

        this.Clock = clock ?? TimeProvider.System;

        this.Personas = this.Options.CreatePersonaStore();
        // "Platform" is the same default Team TestTasks.Make and TaskStoreTests fixtures already
        // assume, so a test creating a Task through TaskService (which refuses an unknown Team,
        // TaskService.cs:461) needs no team of its own just to pick a Location.
        this.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Platform"]), "You are Nova.");
        this.Personas.Add(new PersonaIdentity("Kai", "Kai", "Kai", []), "You are Kai.");

        this.Store = new TaskStore(this.Options, this.Personas, this.Clock, NullLogger<TaskStore>.Instance);
        this.Ids = new TaskIdAllocator(this.Options);
        this.Events = new TaskEvents();
        this.Sprints = sprintsText is null
            ? new Agency.Huddle.Tests.Tasks.EmptySprintCatalog()
            : new SprintStore(this.Options, this.Store, this.Events, NullLogger<SprintStore>.Instance);
        this.Service = new TaskService(
            this.Store,
            this.Ids,
            this.Events,
            this.Personas,
            this.Sprints,
            this.Options,
            this.Clock,
            NullLogger<TaskService>.Instance,
            gates,
            proof: proof,
            levels: levels);
        this.feedPublisher = feed is null ? null : Agency.Huddle.Tests.Tasks.TaskFeedPublishers.Over(this.Events, feed, this.Options, proof);
        this.Views = new ViewStore(this.Options, NullLogger<ViewStore>.Instance);
        this.TurnActivity = new TurnActivity();
        this.TaskActivity = new TaskActivity(this.Options);
        this.Health = new PersonaHealth(this.Clock, NullLogger<PersonaHealth>.Instance);
        this.Avatars = new AvatarStore(this.Options, NullLogger<AvatarStore>.Instance);
        this.Gateway = new UiFakeAgentGateway();

        this.Directory = this.Options.CreateDirectory();
        ChatStack stack = ChatServiceFactory.Create(this.Directory, this.Options);
        this.RoomEvents = stack.Events;
        this.Chat = stack.Chat;
        this.OwnPosts = new OwnPosts(this.Options);
        this.Triggers = new TaskTriggerService(
            this.Events,
            this.Store,
            this.TaskActivity,
            this.TurnActivity,
            this.Chat,
            this.Directory,
            this.Personas,
            this.Gateway,
            this.Health,
            new FakePromptSource(),
            this.Options,
            this.Clock,
            NullLogger<TaskTriggerService>.Instance,
            this.OwnPosts);
    }

    /// <summary>
    /// Builds the harness (via the constructor), then seeds its real <see cref="Directory"/>: the
    /// Human row (<see cref="SqliteTeamDirectory.InitializeAsync"/>) and one agent User each for Nova
    /// and Kai (<see cref="SqliteTeamDirectory.UpsertAgentUserAsync"/>), exposing their ids as
    /// <see cref="NovaId"/> and <see cref="KaiId"/>. D10 tools that resolve a caller or an assignee
    /// through <see cref="Directory"/>, and D12/D14 tests driving <see cref="Triggers"/>, must build
    /// the harness through this factory rather than <c>new TaskToolHarness()</c> - the plain
    /// constructor leaves <see cref="Directory"/>'s on-disk <c>team.db</c> unseeded, because seeding
    /// is asynchronous.
    /// </summary>
    /// <param name="ct">Cancels the seeding calls.</param>
    /// <param name="clock">Forwarded to the constructor; see its own parameter doc.</param>
    /// <param name="sprintsText">Forwarded to the constructor; see its own parameter doc.</param>
    /// <param name="gates">Forwarded to the constructor; see its own parameter doc.</param>
    /// <param name="proof">Forwarded to the constructor; see its own parameter doc.</param>
    /// <param name="levels">The Needs You levels the Task service records each created Task's scope in; <see langword="null"/> records none, so every Task is under Ask me.</param>
    /// <returns>A harness whose <see cref="Directory"/> already knows the Human, Nova and Kai.</returns>
    public static async Task<TaskToolHarness> CreateAsync(CancellationToken ct, TimeProvider? clock = null, string? sprintsText = null, Agency.Huddle.App.Teams.ProcessGateService? gates = null, Agency.Huddle.App.Tasks.HumanGate.IProofSource? proof = null, Agency.Huddle.App.Teams.NeedsYouLevels? levels = null)
    {
        TaskToolHarness harness = new(clock, sprintsText, gates, proof, levels: levels);

        await harness.Directory.InitializeAsync("You", ct);
        User nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct)
            ?? throw new InvalidOperationException("Failed to seed the Nova user.");
        User kai = await harness.Directory.UpsertAgentUserAsync("Kai", null, ct)
            ?? throw new InvalidOperationException("Failed to seed the Kai user.");

        harness.NovaId = nova.Id;
        harness.KaiId = kai.Id;

        return harness;
    }

    /// <summary>The options every service above was constructed with, pointing at <see cref="TempDataDir"/>'s path.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The real <see cref="TimeProvider"/> every clock-taking service above shares.</summary>
    public TimeProvider Clock { get; }

    /// <summary>A real <see cref="PersonaStore"/> seeded with Nova and Kai.</summary>
    public PersonaStore Personas { get; }

    /// <summary>The Task store scanning <see cref="TempDataDir"/>'s Tasks folder.</summary>
    public TaskStore Store { get; }

    /// <summary>Allocates new Task ids.</summary>
    public TaskIdAllocator Ids { get; }

    /// <summary>The plain hub every Tasks service and UI component subscribes to.</summary>
    public TaskEvents Events { get; }

    /// <summary>Creates and updates Tasks, raising <see cref="TaskEvents.TaskChanged"/> as it goes.</summary>
    public TaskService Service { get; }

    /// <summary>The read-only Sprint catalogue a tool receives: a real <see cref="SprintStore"/> when the harness was given <c>sprints.md</c> text, otherwise empty.</summary>
    public ISprintCatalog Sprints { get; }

    /// <summary>The absolute path of <c>sprints.md</c>, whether or not it exists.</summary>
    public string SprintsFilePath => System.IO.Path.Combine(this.Options.Value.DataDir, "sprints.md");

    /// <summary>The saved Views over <see cref="TempDataDir"/>'s <c>views.json</c>.</summary>
    public ViewStore Views { get; }

    /// <summary>Tracks which Agent has a Turn running in which Room.</summary>
    public TurnActivity TurnActivity { get; }

    /// <summary>Tracks per-Task wake history and the Agent-wake budget.</summary>
    public TaskActivity TaskActivity { get; }

    /// <summary>Tracks each Persona's degraded/healthy status.</summary>
    public PersonaHealth Health { get; }

    /// <summary>The shell's Tabs, which hold the breadcrumb trail the Tasks page sets.</summary>
    public Agency.Huddle.App.Tabs.TabService Tabs { get; } = new();

    /// <summary>The chosen avatar per Agent, over <see cref="TempDataDir"/>'s <c>avatars.json</c>.</summary>
    public AvatarStore Avatars { get; }

    /// <summary>The Ui fake in place of a real, pipe-connected <see cref="IAgentGateway"/>.</summary>
    public UiFakeAgentGateway Gateway { get; }

    /// <summary>
    /// A real, disk-backed <see cref="ITeamDirectory"/> over <see cref="TempDataDir"/>'s <c>team.db</c>.
    /// Only <see cref="CreateAsync"/> seeds it (the Human row and a Nova and Kai agent User); a harness
    /// built with the plain constructor has a real but empty directory. D10 tools resolve the caller
    /// and the assignee through this.
    /// </summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary><see cref="Directory"/>'s Nova user id, set by <see cref="CreateAsync"/>; <see langword="null"/> when the harness was built with the plain constructor.</summary>
    public string? NovaId { get; private set; }

    /// <summary><see cref="Directory"/>'s Kai user id, set by <see cref="CreateAsync"/>; <see langword="null"/> when the harness was built with the plain constructor.</summary>
    public string? KaiId { get; private set; }

    /// <summary>A real <see cref="ChatService"/> over <see cref="Directory"/>, for D10/D12/D14 tests whose tool or trigger posts a Message.</summary>
    public ChatService Chat { get; }

    /// <summary>The hub <see cref="Chat"/> raises Room events on (e.g. <c>MessagePosted</c>), for tests that watch a wake Message being posted.</summary>
    public RoomEvents RoomEvents { get; }

    /// <summary>Remembers an Agent's own posts made from a Turn in a different Room, exactly as production DI supplies it to <see cref="Triggers"/>.</summary>
    public OwnPosts OwnPosts { get; }

    /// <summary>
    /// A real <see cref="TaskTriggerService"/> built from this harness's own services, for D10 tools
    /// and D12/D14 tests that need <see cref="TaskTriggerService.Preview"/> to phrase who a Task change
    /// would notify. Never started - <see cref="Microsoft.Extensions.Hosting.IHostedService.StartAsync"/>
    /// is never called, so it exists to call <see cref="TaskTriggerService.Preview"/>, not to actually
    /// wake anyone.
    /// </summary>
    public TaskTriggerService Triggers { get; }

    /// <summary>The Tasks scan root on disk, i.e. <see cref="TaskStore.RootDirectory"/>.</summary>
    public string TasksDirPath => this.Store.RootDirectory;

    /// <summary>
    /// Writes <paramref name="task"/> to disk at the path its <see cref="TaskItem.Location"/> and
    /// <see cref="TaskItem.Id"/> resolve to (<see cref="TaskLayout.PathFor"/>), through
    /// <see cref="TaskStore.Create"/> rather than a raw file write. <see cref="TaskStore.Create"/>
    /// records the written version as already seen before returning, so the next
    /// <see cref="TaskStore.RebuildFromWatcher"/> finds nothing changed at that path: <see cref="Service"/>
    /// (subscribed to <see cref="TaskStore.OutsideEditDetected"/> since construction) never sees this
    /// write as an edit made outside Huddle, and never appends the "edited outside Huddle" Change log
    /// entry that would otherwise overwrite the seeded <see cref="TaskItem.Updated"/> - the trap a raw
    /// <c>TestTaskStore.WriteTask</c> + <see cref="TaskStore.RebuildFromWatcher"/> falls into under this
    /// harness, because <see cref="Service"/> is live from the moment the harness is constructed (R8
    /// facts "Harness trap"). A genuine edit made to the file afterwards, outside this method, is still
    /// picked up as an outside edit on the following rebuild, exactly as before.
    /// </summary>
    /// <param name="task">
    /// The Task to seed. Its own <see cref="TaskItem.Path"/>, if any, is ignored and replaced with the
    /// path <see cref="TaskItem.Location"/> and <see cref="TaskItem.Id"/> resolve to.
    /// </param>
    /// <returns>The re-parsed Task as written to disk.</returns>
    internal TaskItem SeedOnDisk(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        string path = TaskLayout.PathFor(this.Store.RootDirectory, task.Location, task.Id);
        TaskItem toWrite = task with { Path = path };
        string text = TaskFileFormat.Compose(toWrite);

        return this.Store.Create(toWrite, text)
            ?? throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"SeedOnDisk: a file already exists at '{path}'."));
    }

    /// <summary>
    /// Registers every service above into <paramref name="services"/>, so a Razor component
    /// resolves the exact same instances this harness exposes for assertions. Keep this list in
    /// step with what the Tasks page and its children actually <c>@inject</c> - it exists to be
    /// small and documented (corrections-B5), not to mirror the production composition root.
    /// </summary>
    /// <param name="services">The bUnit or host service collection to register into.</param>
    internal void AddTo(IServiceCollection services)
    {
        services.AddSingleton(this.Options);

        // The Tasks page sets the shell breadcrumb (S1a) through it. TryAdd: a test that hosts the page in a TabHost brings its own.
        services.TryAddSingleton(this.Tabs);
        services.AddSingleton<Agency.Huddle.App.Profile.IHumanIdentity>(new Agency.Huddle.App.Profile.ConfiguredHumanIdentity(this.Options.Value.HumanName));
        services.AddSingleton(this.Clock);
        services.AddSingleton(this.Personas);
        services.AddSingleton(this.Store);
        services.AddSingleton(this.Ids);
        services.AddSingleton(this.Events);
        services.AddSingleton(this.Sprints);
        if (this.Sprints is SprintStore sprintStore)
        {
            services.AddSingleton(sprintStore);
        }
        else
        {
            // A component that injects the concrete SprintStore (TaskDetail) gets an own, lazily built, empty one,
            // so a harness without sprints.md text adds no TaskChanged subscriber until such a component renders.
            services.AddSingleton(sp => new SprintStore(
                sp.GetRequiredService<IOptions<TeamOptions>>(),
                sp.GetRequiredService<TaskStore>(),
                sp.GetRequiredService<TaskEvents>(),
                NullLogger<SprintStore>.Instance));
        }

        services.AddSingleton(this.Service);

        // The Task panel resolves the Proof; a harness built without a source gets one that answers with no Proof items.
        services.TryAddSingleton<Agency.Huddle.App.Tasks.HumanGate.IProofSource>(new EmptyProofSource());
        if (this.gates is not null)
        {
            services.AddSingleton(this.gates);
        }

        services.AddSingleton(this.Views);
        services.AddSingleton(this.TurnActivity);
        services.AddSingleton(this.TaskActivity);
        services.AddSingleton(this.Health);
        services.AddSingleton(this.Avatars);
        services.AddSingleton<IAgentGateway>(this.Gateway);
        services.AddSingleton(this.Directory);
        services.AddSingleton<ITeamDirectory>(this.Directory);
        services.AddSingleton(this.Chat);
        services.AddSingleton(this.RoomEvents);
        services.AddSingleton(this.OwnPosts);
        services.AddSingleton(this.Triggers);

        // TaskDetail injects the attach services (TaskAttachments spec 6.5). TryAdd: a test of attaching registers its own.
        services.TryAddSingleton<Agency.Huddle.App.Library.ILibraryReferenceResolver>(new Agency.Huddle.Tests.Library.FakeLibraryNoteResolver());
        services.TryAddSingleton(_ =>
        {
            Agency.Huddle.Tests.Library.LibraryFileServiceFixture library = Agency.Huddle.Tests.Library.LibraryFileServiceFixture.Attach(this.Options.Value.DataDir);
            return new Agency.Huddle.App.Uploads.TaskUploadService(
                this.Store, new Agency.Huddle.App.Uploads.TaskUploadFolders(library.Resolver, this.Store), library.CreateService(), this.Options, new Agency.Huddle.App.Data.RawBaselineStore(this.Options));
        });
    }

    /// <summary>Disposes every disposable service above, then the temp directory itself.</summary>
    public void Dispose()
    {
        this.Triggers.Dispose();
        this.feedPublisher?.Dispose();
        (this.Sprints as IDisposable)?.Dispose();
        this.Service.Dispose();
        this.Store.Dispose();
        this.Views.Dispose();
        this.Personas.Dispose();
        this.Avatars.Dispose();
        this.dir.Dispose();
    }

    /// <summary>An <see cref="Agency.Huddle.App.Tasks.HumanGate.IProofSource"/> that answers every Task with an empty Proof.</summary>
    private sealed class EmptyProofSource : Agency.Huddle.App.Tasks.HumanGate.IProofSource
    {
        /// <summary>Returns the empty Proof.</summary>
        /// <param name="task">The Task, ignored.</param>
        /// <returns>The empty Proof.</returns>
        public Agency.Huddle.App.Tasks.HumanGate.Proof For(TaskItem task) => Agency.Huddle.App.Tasks.HumanGate.Proof.Empty;
    }
}
