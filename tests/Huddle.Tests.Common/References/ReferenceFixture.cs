using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teammates;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Real stores over one temp <c>DataDir</c> for the resolver and snapshot tests: a SQLite directory, a Task store
/// seeded from Task files, the Library resolver, a hand-driven Team catalog and the <c>web</c> Team type.
/// </summary>
internal sealed class ReferenceFixture : IDisposable
{
    private readonly TempDataDir dir;
    private readonly LibraryFileServiceFixture library;
    private readonly LibraryReferenceResolver libraryResolver;
    private readonly TeamTypeCatalog types;

    private ReferenceFixture(
        TempDataDir dir,
        LibraryFileServiceFixture library,
        TaskStore tasks,
        SqliteTeamDirectory directory,
        User nova,
        IOptions<TeamOptions> options)
    {
        this.dir = dir;
        this.library = library;
        this.Tasks = tasks;
        this.Directory = directory;
        this.Nova = nova;
        this.Options = options;
        this.libraryResolver = new LibraryReferenceResolver(library.Resolver, library.Index, options);
        this.types = RoleFixtures.Types(dir);
    }

    /// <summary>The temp <c>DataDir</c>.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The Task store, seeded when the fixture was built.</summary>
    public TaskStore Tasks { get; }

    /// <summary>The Rooms and their Members.</summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary>An Agent user to put in test Rooms.</summary>
    public User Nova { get; }

    /// <summary>The options the resolver and the chat service read.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The hand-driven Team list the resolver looks Teams up in.</summary>
    public FakeTeamCatalog Catalog { get; } = new();

    /// <summary>The Library resolver over this <c>DataDir</c>.</summary>
    public ILibraryReferenceResolver LibraryResolver => this.libraryResolver;

    /// <summary>Builds the fixture.</summary>
    /// <param name="ct">Cancels the directory setup.</param>
    /// <param name="configure">Changes the options (for example to disable Tasks), or null.</param>
    /// <param name="seedTasks">Tasks written under <c>Teams/Platform/_tasks</c> before the store scans.</param>
    /// <returns>The fixture.</returns>
    public static async Task<ReferenceFixture> BuildAsync(CancellationToken ct, Action<TeamOptions>? configure = null, params TaskItem[] seedTasks)
    {
        TempDataDir dir = new();
        TeamOptions teamOptions = new() { DataDir = dir.Path };
        configure?.Invoke(teamOptions);
        IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(teamOptions);

        LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path, configure);
        foreach (TaskItem task in seedTasks)
        {
            TestTaskStore.WriteTask(TestTaskStore.Root(dir), TestTaskStore.RelativePath(task.Location.Team, task.Location.Project, false, task.Id + ".md"), task);
        }

        TaskStore tasks = TestTaskStore.CreateTaskStore(dir, dir.CreatePersonaStore());
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        return new ReferenceFixture(dir, library, tasks, directory, nova, options);
    }

    /// <summary>Builds a resolver over this fixture's stores.</summary>
    /// <param name="catalog">A Team catalog to use instead of <see cref="Catalog"/>, or null.</param>
    /// <param name="libraryOverride">A Library resolver to use instead of the real one, or null.</param>
    /// <returns>The resolver.</returns>
    public ReferenceResolver Resolver(ITeamCatalog? catalog = null, ILibraryReferenceResolver? libraryOverride = null) =>
        new(this.Tasks, this.Directory, catalog ?? this.Catalog, this.types, libraryOverride ?? this.libraryResolver, this.Options);

    /// <summary>Builds the snapshotter over <see cref="Resolver"/>.</summary>
    /// <param name="catalog">A Team catalog to use instead of <see cref="Catalog"/>, or null.</param>
    /// <returns>The snapshotter.</returns>
    public ReferenceSnapshotter Snapshotter(ITeamCatalog? catalog = null) =>
        new(this.Resolver(catalog), NullLogger<ReferenceSnapshotter>.Instance);

    /// <summary>Builds a chat service over this fixture's directory and a file transcript store.</summary>
    /// <param name="snapshotter">The snapshotter to give it, or null for none.</param>
    /// <param name="events">The hub it publishes on.</param>
    /// <returns>The service.</returns>
    public ChatService Chat(IReferenceSnapshotter? snapshotter, RoomEvents events) =>
        new(
            this.Directory,
            new FileChatStore(this.dir.Options(), NullLogger<FileChatStore>.Instance),
            events,
            new FakeMentionAliasSource(),
            this.Options,
            new ProposalStore(events),
            new QuestionStore(events),
            NullLogger<ChatService>.Instance,
            references: snapshotter);

    /// <summary>The path of a Room's transcript file.</summary>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>The JSONL path.</returns>
    public string TranscriptPath(string roomId) => Path.Combine(this.dir.Path, "rooms", roomId + ".jsonl");

    /// <inheritdoc />
    public void Dispose()
    {
        this.Tasks.Dispose();
        this.library.Dispose();
        this.dir.Dispose();
    }
}
