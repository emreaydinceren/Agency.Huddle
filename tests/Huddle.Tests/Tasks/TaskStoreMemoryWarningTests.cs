using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskStore"/>'s start-up warning about Tasks stranded under a Team's
/// reserved <c>memory/_tasks/</c> folder (Spec §12 E-2; Task 3.3.t; corrections-D3 items 12-15).
/// </summary>
public sealed class TaskStoreMemoryWarningTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>The deliverable row: a Task in <c>memory/_tasks</c> and one in <c>memory/_tasks/_closed</c> log ONE Warning
    /// with the settled text, neither Task is loaded, and both files stay on disk byte for byte.</summary>
    [Fact]
    public void Start_StrandedTasksUnderMemory_LogOneWarning_LoadNothing_AndLeaveFilesUntouched()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string open = TestTaskStore.WriteTask(root, Path.Combine("Business", "memory", "_tasks", "BUS-0009.md"), TestTasks.Make(id: "BUS-0009", location: new("Business", "memory", false)));
        string closed = TestTaskStore.WriteTask(root, Path.Combine("Business", "memory", "_tasks", "_closed", "BUS-0010.md"), TestTasks.Make(id: "BUS-0010", location: new("Business", "memory", true)));
        byte[] openBefore = File.ReadAllBytes(open);
        byte[] closedBefore = File.ReadAllBytes(closed);
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Equal(
            "Team folder 'Business' has Tasks under 'memory/_tasks/' (BUS-0009.md, BUS-0010.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project.",
            Assert.Single(Warnings(logger)));
        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
        Assert.Equal(openBefore, File.ReadAllBytes(open));
        Assert.Equal(closedBefore, File.ReadAllBytes(closed));
    }

    /// <summary>One file is listed by its bare file name.</summary>
    [Fact]
    public void Start_OneStrandedFile_NamesItByFileName()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "X.md"), "stranded");
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Equal(
            "Team folder 'Business' has Tasks under 'memory/_tasks/' (X.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project.",
            Assert.Single(Warnings(logger)));
        Assert.Empty(store.All);
    }

    /// <summary>Several files are listed by file name, sorted Ordinal (uppercase before lowercase) rather than in
    /// folder order, joined with a comma and a space, and files from <c>_tasks</c> and <c>_closed</c> are merged into one sorted list.</summary>
    [Fact]
    public void Start_SeveralStrandedFiles_ListsFileNamesSortedOrdinal()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "c.md"), "c");
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "a.md"), "a");
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "_closed", "B.md"), "b");
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Equal(
            "Team folder 'Business' has Tasks under 'memory/_tasks/' (B.md, a.md, c.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project.",
            Assert.Single(Warnings(logger)));
        Assert.Empty(store.All);
    }

    /// <summary>A capitalised <c>Memory</c> folder is the reserved folder too (the name compares ignoring case on every
    /// OS), so it warns, and the text still says <c>memory/_tasks/</c> as settled.</summary>
    [Fact]
    public void Start_CapitalisedMemoryFolder_AlsoWarns()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "Memory", "_tasks", "X.md"), "stranded");
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Equal(
            "Team folder 'Business' has Tasks under 'memory/_tasks/' (X.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project.",
            Assert.Single(Warnings(logger)));
    }

    /// <summary>Each Team with stranded files gets its own Warning naming that Team and only its files; a Team without
    /// a <c>memory</c> folder beside them adds none.</summary>
    [Fact]
    public void Start_TwoTeamsWithStrandedFiles_LogOneWarningPerTeam()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "BUS-0001.md"), "b");
        _ = WriteRawFile(root, Path.Combine("Sales", "memory", "_tasks", "SAL-0001.md"), "s");
        _ = WriteRawFile(root, Path.Combine("Sales", "memory", "_tasks", "_closed", "SAL-0002.md"), "s");
        Directory.CreateDirectory(Path.Combine(root, "Ops", "Launch"));
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        List<string> warnings = Warnings(logger).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "Team folder 'Business' has Tasks under 'memory/_tasks/' (BUS-0001.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project.",
                "Team folder 'Sales' has Tasks under 'memory/_tasks/' (SAL-0001.md, SAL-0002.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Sales/_tasks/' or into a Project.",
            ],
            warnings);
    }

    /// <summary>The warning is emitted once, at start-up: a watcher rebuild and a Task created through the service
    /// (which refreshes the Teams) add no second Warning.</summary>
    [Fact]
    public void Start_ThenRebuildAndCreate_StillOneWarning()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "_tasks", "X.md"), "stranded");
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);
        TaskService service = new(store, new TaskIdAllocator(dir.Options()), new TaskEvents(), personas, dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
        Assert.Single(Warnings(logger));

        store.RebuildFromWatcher();
        TaskResult result = service.Create(new TaskDraft("Ship it", "Business", null), HumanActor);

        _ = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(
            ["Team folder 'Business' has Tasks under 'memory/_tasks/' (X.md); 'memory' is reserved for Team Memory, so they are not loaded. Move them to 'Business/_tasks/' or into a Project."],
            Warnings(logger));
    }

    /// <summary>A <c>memory</c> folder holding only a note, or an empty <c>_tasks</c> folder, logs nothing.</summary>
    [Fact]
    public void Start_MemoryWithNoTaskFiles_LogsNothing()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, Path.Combine("Business", "memory", "notes.md"), "a note");
        Directory.CreateDirectory(Path.Combine(root, "Business", "memory", "_tasks"));
        Directory.CreateDirectory(Path.Combine(root, "Sales", "memory", "_tasks", "_closed"));
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Empty(Warnings(logger));
    }

    /// <summary>A Team with its own Team-level and Project Tasks but no <c>memory</c> folder logs nothing.</summary>
    [Fact]
    public void Start_TeamWithoutMemoryFolder_LogsNothing()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Business", "_tasks", "BUS-0001.md"), TestTasks.Make(id: "BUS-0001", location: new("Business", null, false)));
        TestTaskStore.WriteTask(root, Path.Combine("Business", "Auth", "_tasks", "BUS-0002.md"), TestTasks.Make(id: "BUS-0002", location: new("Business", "Auth", false)));
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Empty(Warnings(logger));
        Assert.Equal(2, store.All.Count);
    }

    /// <summary>A Project merely named <c>Memory-notes</c> is an ordinary Project: its Task loads and nothing warns.</summary>
    [Fact]
    public void Start_ProjectNamedMemoryNotes_LogsNothing_AndLoadsItsTask()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Business", "Memory-notes", "_tasks", "X.md"), TestTasks.Make(id: "BUS-0001", location: new("Business", "Memory-notes", false)));
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Empty(Warnings(logger));
        Assert.Single(store.All);
    }

    /// <summary>A Team that is itself named <c>memory</c> keeps its own <c>_tasks</c>: only a <c>memory</c> folder
    /// INSIDE a Team is reserved, so this logs nothing and the Task loads.</summary>
    [Fact]
    public void Start_TeamNamedMemory_WithItsOwnTasks_LogsNothing()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("memory", "_tasks", "MEM-0001.md"), TestTasks.Make(id: "MEM-0001", location: new("memory", null, false)));
        RecordingLogger<TaskStore> logger = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = new(dir.Options(), personas, TimeProvider.System, logger);

        Assert.Empty(Warnings(logger));
        Assert.Single(store.All);
    }

    /// <summary>Every Warning the recorder captured, as its rendered message.</summary>
    private static List<string> Warnings(RecordingLogger<TaskStore> logger) =>
#pragma warning disable S2971 // Entries is the live list the logger appends to from other threads; ToList() is the snapshot taken before filtering.
        logger.Entries.ToList().Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).ToList();
#pragma warning restore S2971

    /// <summary>Writes arbitrary raw text under <paramref name="root"/>, for fixtures that need no real Task.</summary>
    private static string WriteRawFile(string root, string relativePath, string text)
    {
        string path = Path.Combine(root, relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>An <see cref="ILogger{TCategoryName}"/> that records every call made to it.</summary>
    /// <typeparam name="T">The logger's category type.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly Lock gate = new();
        private readonly List<(LogLevel Level, string Message)> entries = [];

        /// <summary>Every call made so far, in call order.</summary>
        public IReadOnlyList<(LogLevel Level, string Message)> Entries => this.entries;

        /// <summary>Scoping is irrelevant to this fake, so this returns no scope.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns><see langword="null"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call is recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused.</param>
        /// <param name="state">The call's structured state.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/>.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            lock (this.gate)
            {
                this.entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
