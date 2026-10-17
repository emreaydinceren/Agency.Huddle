using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Profile;

/// <summary>
/// The Human's rename end to end over real temp-directory stores: <see cref="HumanProfileValidator"/>
/// refuses a Teammate's Name or Alias, <see cref="SqliteTeamDirectory.RenameHuman"/> renames the Human's
/// row in place, <see cref="HumanRenameCascade"/> moves the Avatar, Task assignee, View filter and row,
/// <see cref="CompositeMentionAliasSource"/> adds the Human's Alias, and <see cref="SenderDisplay"/>
/// shows the current Name on an old Message.
/// </summary>
public sealed class HumanRenameTests
{
    /// <summary>Saving a new Name moves the Avatar, Task assignee, View filter and directory row, keeping the row's id, and repaints Rooms.</summary>
    [Fact]
    public async Task Save_NewName_CascadesEverywhere()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);
        scene.Avatars.Save("You", new Avatar("ME", null, null));
        TaskItem seed = TestTasks.Make(id: "PLAT-0001", assignee: "You", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]);
        TaskItem toWrite = seed with { Path = TaskLayout.PathFor(scene.TaskStore.RootDirectory, seed.Location, seed.Id) };
        TaskId id = (scene.TaskStore.Create(toWrite, TaskFileFormat.Compose(toWrite)) ?? throw new InvalidOperationException("fixture task exists")).Id;
        scene.Views.Save(new TaskView { Id = "mine", Name = "Mine", Kind = ViewKind.List, Filter = new TaskFilter { Assignees = ["You"] } });
        int roomsChanged = 0;
        scene.RoomEvents.RoomsChanged += () => roomsChanged++;

        scene.Store.Save(new HumanProfile("Alan", null, null, null));

        Assert.Equal("ME", scene.Avatars.Get("Alan").Label);
        Assert.Equal("Alan", scene.TaskStore.Get(id)?.Assignee);
        Assert.Equal(["Alan"], scene.Views.Get("mine")?.Filter.Assignees);
        User human = await scene.Directory.GetHumanAsync(ct);
        Assert.Equal(KnownIds.Human, human.Id);
        Assert.Equal("Alan", human.Name);
        Assert.True(roomsChanged > 0);
    }

    /// <summary>A case-only rename is not mistaken for a collision with the Human's own row.</summary>
    [Fact]
    public async Task RenameHuman_CaseOnly_Succeeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);

        Assert.True(directory.RenameHuman("YOU"));
        Assert.Equal("YOU", (await directory.GetHumanAsync(ct)).Name);
    }

    /// <summary>The Human cannot take an Agent's Name: the rename is refused and the row is unchanged.</summary>
    [Fact]
    public async Task RenameHuman_AgentsName_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        _ = await directory.UpsertAgentUserAsync("echo", null, ct);

        Assert.False(directory.RenameHuman("Echo"));
        Assert.Equal("You", (await directory.GetHumanAsync(ct)).Name);
    }

    /// <summary>Startup with a Human Name an Agent already holds keeps the old Name instead of failing on the unique index.</summary>
    [Fact]
    public async Task InitializeAsync_NameHeldByAnAgent_KeepsTheOldNameAndDoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        _ = await directory.UpsertAgentUserAsync("echo", null, ct);

        await directory.InitializeAsync("echo", ct);

        Assert.Equal("You", (await directory.GetHumanAsync(ct)).Name);
    }

    /// <summary><see cref="SqliteTeamDirectory.ReadHumanNameAsync"/> is null before the database exists and the stored Name after.</summary>
    [Fact]
    public async Task ReadHumanNameAsync_NullOnAFreshDirectory_ThenTheStoredName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());

        Assert.Null(await directory.ReadHumanNameAsync(ct));
        await directory.InitializeAsync("You", ct);
        Assert.Equal("You", await directory.ReadHumanNameAsync(ct));
    }

    /// <summary>The validator refuses a Teammate's Name, a Teammate's Alias, an Alias equal to the Name, and an invalid Name.</summary>
    [Theory]
    [InlineData("coo", null, "Name 'coo' already belongs to the Teammate coo.")]
    [InlineData("Alan", "COO", "Alias 'COO' already belongs to the Teammate coo.")]
    [InlineData("Alan", "alan", "Alias must be different from your Name.")]
    [InlineData("../x", null, "Name must be 1-64 letters, digits, '-' or '_', starting with a letter or digit, with single spaces only between words.")]
    [InlineData("Alan", "al.", "Alias must be 1-64 letters, digits, '-' or '_', starting with a letter or digit, with single spaces only between words.")]
    public async Task Validate_RefusesCollisionsAndBadNames(string name, string? alias, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);
        scene.Personas.Add(new PersonaIdentity("coo", "Chief Operating Officer", "coo", []), "You run things.");

        IReadOnlyList<string> problems = new HumanProfileValidator(scene.Personas, scene.Directory).Validate(new HumanProfile(name, alias, null, null));

        Assert.Equal(expected, Assert.Single(problems));
    }

    /// <summary>A Name held by an Agent with no Persona (a pipe client or demo agent) is refused too.</summary>
    [Fact]
    public async Task Validate_NameOfAnAgentRow_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);
        _ = await scene.Directory.UpsertAgentUserAsync("echo", null, ct);

        IReadOnlyList<string> problems = new HumanProfileValidator(scene.Personas, scene.Directory).Validate(new HumanProfile("Echo", null, null, null));

        Assert.Equal("Name 'Echo' already belongs to the Agent echo.", Assert.Single(problems));
    }

    /// <summary>A valid, unclaimed Name and Alias pass.</summary>
    [Fact]
    public async Task Validate_UnclaimedNameAndAlias_HasNoProblems()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);

        Assert.Empty(new HumanProfileValidator(scene.Personas, scene.Directory).Validate(new HumanProfile("Alan Smith", "al", "SRE", null)));
    }

    /// <summary>The Human's Alias joins the Mention aliases, paired with the Human's current Name.</summary>
    [Fact]
    public async Task Aliases_IncludeTheHumansAlias()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);
        scene.Store.Save(new HumanProfile("Alan", "al", null, null));

        CompositeMentionAliasSource source = new(scene.Personas, scene.Store, NullLogger<CompositeMentionAliasSource>.Instance);

        // contains-ok: the Persona aliases beside it come from the built-in seed and are not under test.
        Assert.Contains(new MentionAlias("al", "Alan"), source.Aliases);
    }

    /// <summary>A Human Alias a Teammate later takes is left out, so a Mention never resolves to two Members.</summary>
    [Fact]
    public async Task Aliases_HumanAliasTakenByATeammate_IsLeftOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Scene scene = await Scene.CreateAsync(dir, ct);
        scene.Store.Save(new HumanProfile("Alan", "coo", null, null));
        scene.Personas.Add(new PersonaIdentity("coo", "Chief Operating Officer", "coo", []), "You run things.");

        CompositeMentionAliasSource source = new(scene.Personas, scene.Store, NullLogger<CompositeMentionAliasSource>.Instance);

        Assert.DoesNotContain(source.Aliases, alias => alias.Name == "Alan");
    }

    /// <summary>A Message the Human sent under an old Name shows the current Name; another sender's keeps its own.</summary>
    [Fact]
    public void SenderDisplay_HumanMessage_ShowsTheCurrentName()
    {
        ChatMessage mine = new("m1", DateTimeOffset.UnixEpoch, KnownIds.Human, "You", "hi");
        ChatMessage theirs = new("m2", DateTimeOffset.UnixEpoch, "agent-1", "coo", "hello");

        Assert.Equal("Alan", SenderDisplay.NameOf(mine, "Alan"));
        Assert.Equal("coo", SenderDisplay.NameOf(theirs, "Alan"));
        Assert.Equal("You", SenderDisplay.NameOf(mine, null));
    }

    /// <summary>The real stores a Human rename touches, wired as the app wires them, with the cascade subscribed.</summary>
    private sealed class Scene : IDisposable
    {
        /// <summary>Builds the scene over <paramref name="dir"/>, with the directory initialised for the Human <c>You</c>.</summary>
        /// <param name="dir">The data directory.</param>
        /// <param name="ct">Cancels the setup.</param>
        public static async Task<Scene> CreateAsync(TempDataDir dir, CancellationToken ct)
        {
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            PersonaStore personas = new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
            HumanProfileStore store = new(dir.Options(), NullLogger<HumanProfileStore>.Instance);
            AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
            TaskStore taskStore = new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance, store);
            TaskService tasks = new(taskStore, new TaskIdAllocator(dir.Options()), new TaskEvents(), personas, new EmptySprintCatalog(), dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance, human: store);
            ViewStore views = new(dir.Options(), NullLogger<ViewStore>.Instance);
            RoomEvents roomEvents = new(NullLogger<RoomEvents>.Instance);
            HumanRenameCascade cascade = new(store, avatars, tasks, views, directory, roomEvents, NullLogger<HumanRenameCascade>.Instance);
            await cascade.StartAsync(ct);
            return new Scene(directory, personas, store, avatars, taskStore, tasks, views, roomEvents, cascade);
        }

        /// <summary>Holds the scene's parts.</summary>
        private Scene(SqliteTeamDirectory directory, PersonaStore personas, HumanProfileStore store, AvatarStore avatars, TaskStore taskStore, TaskService tasks, ViewStore views, RoomEvents roomEvents, HumanRenameCascade cascade)
        {
            this.Directory = directory;
            this.Personas = personas;
            this.Store = store;
            this.Avatars = avatars;
            this.TaskStore = taskStore;
            this.Tasks = tasks;
            this.Views = views;
            this.RoomEvents = roomEvents;
            this.Cascade = cascade;
        }

        /// <summary>The Team Directory.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The Persona store.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The Human's profile store.</summary>
        public HumanProfileStore Store { get; }

        /// <summary>The avatar store.</summary>
        public AvatarStore Avatars { get; }

        /// <summary>The Task store.</summary>
        public TaskStore TaskStore { get; }

        /// <summary>The Task service.</summary>
        public TaskService Tasks { get; }

        /// <summary>The saved Views.</summary>
        public ViewStore Views { get; }

        /// <summary>The Room events hub.</summary>
        public RoomEvents RoomEvents { get; }

        /// <summary>The cascade under test.</summary>
        public HumanRenameCascade Cascade { get; }

        /// <summary>Unsubscribes the cascade and releases every watcher.</summary>
        public void Dispose()
        {
            this.Cascade.Dispose();
            this.Tasks.Dispose();
            this.Views.Dispose();
            this.TaskStore.Dispose();
            this.Avatars.Dispose();
            this.Store.Dispose();
            this.Personas.Dispose();
        }
    }
}
