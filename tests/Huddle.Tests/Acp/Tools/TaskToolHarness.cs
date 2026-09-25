using Microsoft.Extensions.DependencyInjection;
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
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
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

    /// <summary>Builds the whole stack: a fresh <see cref="TempDataDir"/>, a real <see cref="PersonaStore"/> seeded with Nova and Kai, and every Tasks service constructed over it.</summary>
    public TaskToolHarness()
    {
        this.Options = this.dir.Options();
        this.Clock = TimeProvider.System;

        this.Personas = new PersonaStore(
            this.Options,
            new PersonaModelStore(this.Options),
            new PersonaEffortStore(this.Options),
            NullLogger<PersonaStore>.Instance);
        // "Platform" is the same default Team TestTasks.Make and TaskStoreTests fixtures already
        // assume, so a test creating a Task through TaskService (which refuses an unknown Team,
        // TaskService.cs:461) needs no team of its own just to pick a Location.
        this.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Platform"]), "You are Nova.");
        this.Personas.Add(new PersonaIdentity("Kai", "Kai", "Kai", []), "You are Kai.");

        this.Store = new TaskStore(this.Options, this.Personas, this.Clock, NullLogger<TaskStore>.Instance);
        this.Ids = new TaskIdAllocator(this.Options);
        this.Events = new TaskEvents();
        this.Service = new TaskService(
            this.Store,
            this.Ids,
            this.Events,
            this.Personas,
            this.Options,
            this.Clock,
            NullLogger<TaskService>.Instance);
        this.Views = new ViewStore(this.Options, NullLogger<ViewStore>.Instance);
        this.TurnActivity = new TurnActivity();
        this.TaskActivity = new TaskActivity(this.Options);
        this.Health = new PersonaHealth(this.Clock, NullLogger<PersonaHealth>.Instance);
        this.Avatars = new AvatarStore(this.Options, NullLogger<AvatarStore>.Instance);
        this.Gateway = new UiFakeAgentGateway();

        this.Directory = new SqliteTeamDirectory(this.Options);
        FileChatStore chatStore = new(this.Options, NullLogger<FileChatStore>.Instance);
        this.RoomEvents = new RoomEvents(NullLogger<RoomEvents>.Instance);
        ProposalStore proposals = new(this.RoomEvents);
        this.Chat = new ChatService(
            this.Directory,
            chatStore,
            this.RoomEvents,
            new FakeMentionAliasSource(),
            this.Options,
            proposals,
            NullLogger<ChatService>.Instance);
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
    /// <returns>A harness whose <see cref="Directory"/> already knows the Human, Nova and Kai.</returns>
    public static async Task<TaskToolHarness> CreateAsync(CancellationToken ct)
    {
        TaskToolHarness harness = new();

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

    /// <summary>The saved Views over <see cref="TempDataDir"/>'s <c>views.json</c>.</summary>
    public ViewStore Views { get; }

    /// <summary>Tracks which Agent has a Turn running in which Room.</summary>
    public TurnActivity TurnActivity { get; }

    /// <summary>Tracks per-Task wake history and the Agent-wake budget.</summary>
    public TaskActivity TaskActivity { get; }

    /// <summary>Tracks each Persona's degraded/healthy status.</summary>
    public PersonaHealth Health { get; }

    /// <summary>The chosen avatar per Teammate, over <see cref="TempDataDir"/>'s <c>avatars.json</c>.</summary>
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
    /// Registers every service above into <paramref name="services"/>, so a Razor component
    /// resolves the exact same instances this harness exposes for assertions. Keep this list in
    /// step with what the Tasks page and its children actually <c>@inject</c> - it exists to be
    /// small and documented (corrections-B5), not to mirror the production composition root.
    /// </summary>
    /// <param name="services">The bUnit or host service collection to register into.</param>
    internal void AddTo(IServiceCollection services)
    {
        services.AddSingleton(this.Options);
        services.AddSingleton(this.Clock);
        services.AddSingleton(this.Personas);
        services.AddSingleton(this.Store);
        services.AddSingleton(this.Ids);
        services.AddSingleton(this.Events);
        services.AddSingleton(this.Service);
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
    }

    /// <summary>Disposes every disposable service above, then the temp directory itself.</summary>
    public void Dispose()
    {
        this.Triggers.Dispose();
        this.Service.Dispose();
        this.Store.Dispose();
        this.Views.Dispose();
        this.Personas.Dispose();
        this.Avatars.Dispose();
        this.dir.Dispose();
    }
}
