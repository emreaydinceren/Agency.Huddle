using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
using FakeAgentGateway = Agency.Huddle.Tests.Acp.Tools.FakeAgentGateway;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Constants, builders and doubles shared by the <c>TaskTrigger*Tests</c> classes.</summary>
internal static class TaskTriggerTestSupport
{
    /// <summary>The Prompt key the wake-up Message is rendered from (Spec §10.5).</summary>
    internal const string WakeMessageKey = "task.wake.message";

    /// <summary>A <c>task.wake.message</c> override naming every placeholder once, bar-separated, so one literal comparison pins what each resolved to.</summary>
    internal const string ProbeTemplate = "@{{assignee}}|{{taskId}}|{{title}}|{{status}}|{{team}}|{{actor}}|{{changes}}";

    /// <summary>Rounds for <see cref="TaskTriggerConcurrencyTests.Concurrent_TwoBatchesSameTask_SerialisedByGate"/> (facts R3: ≥25 rounds of real threads).</summary>
    internal const int ConcurrencyRounds = 25;

    /// <summary>The Human, acting in the interface.</summary>
    internal static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>A hand edit to the file while Huddle runs (Spec §8.4: <c>TaskActor(OutsideHuddle, humanName, KnownIds.Human)</c>).</summary>
    internal static readonly TaskActor OutsideActor = new(TaskActorKind.OutsideHuddle, "You", KnownIds.Human);

    /// <summary>An <c>@</c> followed by U+2060 (word joiner), spelled as a code point so the source holds no invisible character.</summary>
    internal static readonly string NeutralisedAt = "@" + (char)0x2060;

    /// <summary>The default <c>Tasks.WakeCoalesceSeconds</c> window (Spec §10.3).</summary>
    internal static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    /// <summary>How long <see cref="OverlapProbe"/> holds the first entrant open waiting for a second, concurrent one.</summary>
    internal static readonly TimeSpan OverlapWindow = TimeSpan.FromMilliseconds(100);

    /// <summary>Raises one change and lets its window elapse, awaiting the fire.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="task">The Task changed.</param>
    /// <param name="actor">Who changed it.</param>
    /// <param name="summary">The change's summary.</param>
    /// <returns>A task that completes once the fire is done.</returns>
    internal static async Task RaiseAndFireAsync(Harness harness, TaskItem task, TaskActor actor, string summary)
    {
        harness.Raise(task, actor, summary);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>Registers Nova and Kai as Personas and Agent users, creates no Room, and starts the service - the arrangement the Room-choice tests (Spec §10.4) build their own Rooms on.</summary>
    /// <param name="harness">The harness to arrange.</param>
    /// <param name="ct">Cancels the directory writes and <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <returns>The two Agents.</returns>
    internal static async Task<Agents> RegisterAgentsAsync(Harness harness, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        User? nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct);
        User? kai = await harness.Directory.UpsertAgentUserAsync("Kai", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(kai);
        await harness.Trigger.StartAsync(ct);
        return new Agents(nova, kai);
    }

    /// <summary>Sorts user ids ordinally, so two Member sets compare as sets.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The ids, sorted.</returns>
    internal static string[] SortedIds(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];

    /// <summary>Sorts users' ids ordinally, so two Member sets compare as sets.</summary>
    /// <param name="users">The users.</param>
    /// <returns>Their ids, sorted.</returns>
    internal static string[] SortedIds(IEnumerable<User> users) => [.. users.Select(u => u.Id).Order(StringComparer.Ordinal)];

    /// <summary>Seeds <paramref name="count"/> Tasks <c>PLAT-0001</c> onwards, each assigned to Nova with <paramref name="cast"/>'s Room as origin.</summary>
    /// <param name="harness">The harness whose store receives the Tasks.</param>
    /// <param name="cast">Supplies the origin Room.</param>
    /// <param name="count">How many Tasks to seed.</param>
    /// <returns>The seeded Tasks, as written.</returns>
    internal static TaskItem[] SeedNumbered(Harness harness, Cast cast, int count)
    {
        TaskItem[] tasks = new TaskItem[count];
        for (int i = 0; i < count; i++)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"PLAT-{i + 1:D4}");
            tasks[i] = harness.Seed(TestTasks.Make(id: id, assignee: "Nova", originRoomId: cast.Room.Id));
        }

        return tasks;
    }

    /// <summary>
    /// Registers Nova (the usual assignee) and Kai (a third Teammate) as Personas and Agent users,
    /// creates the origin Room {Human, Nova, Kai} every waking test uses (Spec §10.4 step 1 - both the
    /// sender and the assignee are Members; choosing among Rooms is Task 9.5's), and starts the service.
    /// </summary>
    /// <param name="harness">The harness to arrange.</param>
    /// <param name="ct">Cancels the directory writes and <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <param name="start">Whether to call <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <returns>The two Agents and the Room.</returns>
    internal static async Task<Cast> StartWakingAsync(Harness harness, CancellationToken ct, bool start = true)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        User? nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct);
        User? kai = await harness.Directory.UpsertAgentUserAsync("Kai", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(kai);
        Room room = await harness.Directory.CreateRoomAsync("Platform", [KnownIds.Human, nova.Id, kai.Id], ct);

        if (start)
        {
            await harness.Trigger.StartAsync(ct);
        }

        return new Cast(nova, kai, room);
    }

    /// <summary>Counts <paramref name="source"/>'s current subscribers to the event named <paramref name="eventName"/>, via the compiler-generated backing field (facts R4).</summary>
    /// <param name="source">The hub instance to inspect.</param>
    /// <param name="eventName">The event's own name.</param>
    /// <returns>The number of delegates in the event's invocation list.</returns>
    internal static int SubscriberCount(object source, string eventName)
    {
        FieldInfo field = source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' has no backing field for '{eventName}'.");

        return (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>Builds a real <see cref="TaskTriggerService"/> over a real <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/>, following the constructor snippet in the delivery facts.</summary>
    /// <param name="dir">The temporary data directory backing every store this harness builds.</param>
    /// <param name="options">The Team options the trigger service, the Task activity budget and the Chat service all share.</param>
    /// <param name="ct">Cancels the directory's async setup.</param>
    internal static async Task<Harness> CreateHarnessAsync(TempDataDir dir, TeamOptions options, CancellationToken ct)
    {
        IOptions<TeamOptions> teamOptions = Options.Create(options);

        SqliteTeamDirectory sqliteDirectory = new(dir.Options());
        await sqliteDirectory.InitializeAsync(options.HumanName, ct);
        HoldingTeamDirectory directory = new(sqliteDirectory);

        FileChatStore chatStore = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        RoomEvents roomEvents = new(NullLogger<RoomEvents>.Instance);
        ProposalStore proposals = new(roomEvents);
        ChatService chat = new(
            directory, chatStore, roomEvents, new FakeMentionAliasSource(), teamOptions, proposals, new QuestionStore(roomEvents), NullLogger<ChatService>.Instance);

        PersonaStore personas = new(
            new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        TaskStore store = new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        TaskEvents events = new();
        TaskActivity activity = new(teamOptions);
        TurnActivity turns = new();
        FakeAgentGateway gateway = new();
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        FakePromptSource prompts = new();
        OwnPosts ownPosts = new(teamOptions);
        FiringTimeProvider clock = new();
        RecordingLogger<TaskTriggerService> logger = new();

        TaskTriggerService trigger = new(
            events, store, activity, turns, chat, directory, personas, gateway, health, prompts,
            teamOptions, clock, logger, ownPosts: ownPosts);

        Harness harness = new()
        {
            Trigger = trigger,
            Activity = activity,
            Gateway = gateway,
            Directory = directory,
            Personas = personas,
            Store = store,
            Events = events,
            Prompts = prompts,
            OwnPosts = ownPosts,
            Clock = clock,
            Logger = logger,
            Chat = chat,
        };
        roomEvents.MessagePosted += harness.OnMessagePosted;
        return harness;
    }

    /// <summary>The two registered Agents a Room-choice test arranges (<see cref="RegisterAgentsAsync"/>), with no Room.</summary>
    /// <param name="Nova">The usual assignee.</param>
    /// <param name="Kai">A second Teammate: creator, third-Agent actor, or neither.</param>
    internal sealed record Agents(User Nova, User Kai)
    {
        /// <summary>Kai, acting through an App Tool.</summary>
        public TaskActor KaiActor => new(TaskActorKind.Agent, this.Kai.Name, this.Kai.Id);
    }

    /// <summary>The two Agents and the origin Room a waking test arranges (<see cref="StartWakingAsync"/>).</summary>
    /// <param name="Nova">The usual assignee.</param>
    /// <param name="Kai">A third Teammate, the usual Agent actor.</param>
    /// <param name="Room">The origin Room {Human, Nova, Kai}.</param>
    internal sealed record Cast(User Nova, User Kai, Room Room)
    {
        /// <summary>Kai, acting through an App Tool.</summary>
        public TaskActor KaiActor => new(TaskActorKind.Agent, this.Kai.Name, this.Kai.Id);

        /// <summary>Nova, acting through an App Tool.</summary>
        public TaskActor NovaActor => new(TaskActorKind.Agent, this.Nova.Name, this.Nova.Id);
    }

    /// <summary>The pieces of a composed <see cref="TaskTriggerService"/> a test needs to arrange against, and the disposable ones it owns.</summary>
    internal sealed class Harness : IDisposable
    {
        private readonly ConcurrentQueue<MessagePostedEvent> posts = new();

        /// <summary>The service under test.</summary>
        public required TaskTriggerService Trigger { get; init; }

        /// <summary>The wake budget backing <see cref="Trigger"/>.</summary>
        public required TaskActivity Activity { get; init; }

        /// <summary>The fake Agent connection registry backing <see cref="Trigger"/>.</summary>
        public required FakeAgentGateway Gateway { get; init; }

        /// <summary>The Team directory backing <see cref="Trigger"/> and its <see cref="ChatService"/>, with a hook to hold or fail a fire.</summary>
        public required HoldingTeamDirectory Directory { get; init; }

        /// <summary>The Persona store backing <see cref="Trigger"/>.</summary>
        public required PersonaStore Personas { get; init; }

        /// <summary>The Task store <see cref="Trigger"/> reads the latest Task from, on <see cref="TimeProvider.System"/> (corrections-B3 D9 item 8).</summary>
        public required TaskStore Store { get; init; }

        /// <summary>The hub <see cref="Trigger"/> subscribes to.</summary>
        public required TaskEvents Events { get; init; }

        /// <summary>The Chat service <see cref="Trigger"/> posts through, for a test to post its own Messages.</summary>
        public required ChatService Chat { get; init; }

        /// <summary>The Prompt source <see cref="Trigger"/> renders <c>task.wake.message</c> through.</summary>
        public required FakePromptSource Prompts { get; init; }

        /// <summary>Where an Agent-posted wake is recorded as that Agent's own post.</summary>
        public required OwnPosts OwnPosts { get; init; }

        /// <summary>The clock whose timers drive coalescing.</summary>
        public required FiringTimeProvider Clock { get; init; }

        /// <summary>The logger handed to <see cref="Trigger"/>.</summary>
        public required RecordingLogger<TaskTriggerService> Logger { get; init; }

        /// <summary>Every Message posted in any Room since the harness was built, in publish order.</summary>
        public IReadOnlyList<MessagePostedEvent> Posts => [.. this.posts];

        /// <summary>Records one posted Message; subscribed to <see cref="RoomEvents.MessagePosted"/>.</summary>
        /// <param name="posted">The published Message.</param>
        public void OnMessagePosted(MessagePostedEvent posted) => this.posts.Enqueue(posted);

        /// <summary>Writes <paramref name="task"/> as a new file at its layout path, so <see cref="TaskStore.Get"/> returns it.</summary>
        /// <param name="task">The Task to create.</param>
        /// <returns>The Task as written.</returns>
        public TaskItem Seed(TaskItem task)
        {
            TaskItem placed = task with { Path = TaskLayout.PathFor(this.Store.RootDirectory, task.Location, task.Id) };
            TaskItem? written = this.Store.Create(placed, TaskFileFormat.Compose(placed));
            Assert.NotNull(written);
            return written;
        }

        /// <summary>Overwrites the stored Task with <paramref name="updated"/>, so the latest state <see cref="TaskStore.Get"/> returns changes.</summary>
        /// <param name="updated">The Task's new state; its path and location are taken from the stored Task.</param>
        /// <returns>The Task as written.</returns>
        public TaskItem Replace(TaskItem updated)
        {
            TaskItem? current = this.Store.Get(updated.Id);
            Assert.NotNull(current);
            TaskItem placed = updated with { Path = current.Path, Location = current.Location };
            TaskItem? written = this.Store.Write(placed, current.Version, TaskFileFormat.Compose(placed));
            Assert.NotNull(written);
            return written;
        }

        /// <summary>Raises <see cref="TaskEvents.TaskChanged"/> for <paramref name="after"/>, with one Change log entry carrying <paramref name="summary"/>.</summary>
        /// <param name="after">The Task after the change.</param>
        /// <param name="actor">Who made the change.</param>
        /// <param name="summary">The entry's summary, which the Message's <c>{{changes}}</c> lists.</param>
        public void Raise(TaskItem after, TaskActor actor, string summary) =>
            this.Events.RaiseTaskChanged(new TaskChange(
                null, after, [], actor, new ChangeLogEntry(this.Clock.GetUtcNow(), actor.Name, summary)));

        /// <summary>Disposes <see cref="Trigger"/>, the Task store and the Persona store.</summary>
        public void Dispose()
        {
            this.Trigger.Dispose();
            this.Store.Dispose();
            this.Personas.Dispose();
        }
    }

    /// <summary>
    /// Records whether two fires ever overlap: the first entrant of a round waits up to
    /// <see cref="OverlapWindow"/> for a second, concurrent entrant, and <see cref="MaxConcurrent"/>
    /// keeps the most entrants ever seen at once.
    /// </summary>
    internal sealed class OverlapProbe
    {
        private readonly Lock gate = new();
        private int inFlight;
        private bool heldThisRound;
        private TaskCompletionSource secondEntrant = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The most entrants ever inside <see cref="EnterAsync"/> at once.</summary>
        public int MaxConcurrent { get; private set; }

        /// <summary>Starts a round: its first entrant will be held open again.</summary>
        public void NextRound()
        {
            lock (this.gate)
            {
                this.heldThisRound = false;
                this.secondEntrant = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>Counts one entrant; holds the round's first one open briefly so a concurrent second can be seen.</summary>
        /// <returns>A task that completes when this entrant leaves.</returns>
        public async Task EnterAsync()
        {
            Task? hold = null;
            lock (this.gate)
            {
                this.inFlight++;
                this.MaxConcurrent = Math.Max(this.MaxConcurrent, this.inFlight);
                if (this.inFlight == 1 && !this.heldThisRound)
                {
                    this.heldThisRound = true;
                    hold = this.secondEntrant.Task;
                }
                else
                {
                    this.secondEntrant.TrySetResult();
                }
            }

            try
            {
                if (hold is not null)
                {
                    await WaitBrieflyAsync(hold);
                }
            }
            finally
            {
                lock (this.gate)
                {
                    this.inFlight--;
                }
            }
        }

        /// <summary>Waits for <paramref name="hold"/> up to <see cref="OverlapWindow"/>.</summary>
        /// <param name="hold">Completes when a second entrant arrives.</param>
        /// <returns>A task that completes when the second entrant arrived or the window passed.</returns>
        private static async Task WaitBrieflyAsync(Task hold)
        {
            try
            {
                await hold.WaitAsync(OverlapWindow);
            }
            catch (TimeoutException)
            {
                // No second entrant arrived inside the window: the fires were serialised, which is
                // the passing case - nothing to record.
            }
        }
    }

    /// <summary>A thread-safe fake <see cref="ILogger{T}"/> that records every call (this repo has no mocking framework).</summary>
    /// <typeparam name="T">The category type the logger stands in for.</typeparam>
    internal sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> entries = new();

        /// <summary>Every call made so far, in call order.</summary>
        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => [.. this.entries];

        /// <summary>Scoping is irrelevant to these tests, so this returns no scope.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns><see langword="null"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call is recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one call's level, formatted message and exception.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused.</param>
        /// <param name="state">The call's structured state.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/>.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            this.entries.Enqueue((logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>
    /// Forwards every <see cref="ITeamDirectory"/> member to a real directory, except that
    /// <see cref="GetRoomMembersAsync"/> first awaits <see cref="BeforeGetRoomMembers"/> when set -
    /// the seam a test uses to hold a fire open (it runs inside every post, <c>ChatService.PostAsync</c>)
    /// or make it throw. The hook deliberately ignores the call's token, so cancelling the service's
    /// lifetime does not release a hold the test owns.
    /// </summary>
    /// <param name="inner">The real directory.</param>
    internal sealed class HoldingTeamDirectory(ITeamDirectory inner) : ITeamDirectory
    {
        /// <summary>Awaited before every <see cref="GetRoomMembersAsync"/>; <see langword="null"/> passes straight through.</summary>
        public Func<Task>? BeforeGetRoomMembers { get; set; }

        /// <inheritdoc />
        public Task InitializeAsync(string humanName, CancellationToken ct = default) => inner.InitializeAsync(humanName, ct);

        /// <inheritdoc />
        public Task<User> GetHumanAsync(CancellationToken ct = default) => inner.GetHumanAsync(ct);

        /// <inheritdoc />
        public Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default) =>
            inner.UpsertAgentUserAsync(name, description, ct);

        /// <inheritdoc />
        public bool RenameUser(string userId, string newName) => inner.RenameUser(userId, newName);

        /// <inheritdoc />
        public Task<User?> GetUserAsync(string id, CancellationToken ct = default) =>
            this.HideUserId is { } hidden && string.Equals(hidden, id, StringComparison.Ordinal)
                ? Task.FromResult<User?>(null)
                : inner.GetUserAsync(id, ct);

        /// <summary>When set, <see cref="GetUserAsync"/> reports this user id as unknown - a stale Agent, as <c>ChatService.CreateRoomForAsync</c> sees one.</summary>
        public string? HideUserId { get; set; }

        /// <inheritdoc />
        public Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default) => inner.FindUserByNameAsync(name, ct);

        /// <inheritdoc />
        public User? FindUserByName(string name) => inner.FindUserByName(name);

        /// <inheritdoc />
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default) => inner.GetUsersAsync(ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default) => inner.GetRoomsAsync(ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default) =>
            inner.GetRoomsForUserAsync(userId, ct);

        /// <inheritdoc />
        public Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default) => inner.GetRoomAsync(roomId, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default)
        {
            if (this.BeforeGetRoomMembers is { } hook)
            {
                await hook();
            }

            return await inner.GetRoomMembersAsync(roomId, ct);
        }

        /// <inheritdoc />
        public Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default) =>
            inner.CreateRoomAsync(name, memberIds, ct);

        /// <inheritdoc />
        public Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default) =>
            inner.AddMemberAsync(roomId, userId, ct);

        /// <inheritdoc />
        public Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default) => inner.RenameRoomAsync(roomId, name, ct);

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default) =>
            inner.FindRoomWithExactMembersAsync(humanId, agentId, ct);

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default) =>
            inner.FindRoomWithExactMemberSetAsync(memberIds, ct);

        /// <inheritdoc />
        public Task SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default) =>
            inner.SetRoomArchivedAsync(roomId, archived, ct);

        /// <inheritdoc />
        public Task DeleteRoomAsync(string roomId, CancellationToken ct = default) => inner.DeleteRoomAsync(roomId, ct);
    }
}