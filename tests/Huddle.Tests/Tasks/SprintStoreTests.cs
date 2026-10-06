using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Functional tests for <see cref="SprintStore"/> (Sprints spec §8, SP-T3): a real
/// <see cref="TaskStore"/>, <see cref="TaskService"/> and <see cref="TaskEvents"/> over a
/// <see cref="TempDataDir"/>, no mocks.
/// </summary>
public sealed class SprintStoreTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    private static readonly SprintId S1 = new(1);
    private static readonly SprintId S2 = new(2);
    private static readonly SprintId S3 = new(3);

    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    /// <summary>No file is the normal first-run case: an empty catalogue, no problems, and nothing is created just to read it.</summary>
    [Fact]
    public void AbsentFile_EmptyCatalogue_CreatesNothing()
    {
        using Harness h = new(null);

        Assert.Empty(h.Sprints.All);
        Assert.Null(h.Sprints.Current);
        Assert.Empty(h.Sprints.Problems);
        Assert.Equal(Path.Combine(h.Dir.Path, "sprints.md"), h.Sprints.Path);
        Assert.False(File.Exists(h.Sprints.Path));
    }

    /// <summary>The first create writes the heading, a blank line and the Sprint line, and the catalogue shows it.</summary>
    [Fact]
    public void Create_FirstSprint_WritesFile()
    {
        using Harness h = new(null);

        SprintCreateResult result = h.Sprints.Create("  Sprint 1 ");

        SprintCreateResult.Created created = Assert.IsType<SprintCreateResult.Created>(result);
        Assert.Equal(new Sprint(S1, "Sprint 1"), created.Sprint);
        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", File.ReadAllText(h.Sprints.Path));
        Assert.Equal(created.Sprint, h.Sprints.Current);
        Assert.Equal(created.Sprint, h.Sprints.Get(S1));
        Assert.False(File.Exists(h.Sprints.Path + ".tmp"));
    }

    /// <summary>A second create of the same name, ignoring case, returns the existing Sprint and writes nothing.</summary>
    [Fact]
    public void Create_SameNameTwice_ReturnsExisting()
    {
        using Harness h = new(null);
        SprintCreateResult.Created first = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Auth hardening"));
        string textAfterFirst = File.ReadAllText(h.Sprints.Path);

        SprintCreateResult second = h.Sprints.Create("AUTH HARDENING");

        SprintCreateResult.Existing existing = Assert.IsType<SprintCreateResult.Existing>(second);
        Assert.Equal(first.Sprint, existing.Sprint);
        Assert.Equal(textAfterFirst, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Two different names get consecutive numbers.</summary>
    [Fact]
    public void Create_TwoNames_ConsecutiveNumbers()
    {
        using Harness h = new(null);

        SprintCreateResult.Created a = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Alpha"));
        SprintCreateResult.Created b = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Beta"));

        Assert.Equal(1, a.Sprint.Id.Number);
        Assert.Equal(2, b.Sprint.Id.Number);
        Assert.Equal(["Alpha", "Beta"], h.Sprints.All.Select(static s => s.Name));
    }

    /// <summary>An invalid name is refused with the validator's problem and nothing is written.</summary>
    [Fact]
    public void Create_InvalidName_Refused_WritesNothing()
    {
        using Harness h = new(null);

        SprintCreateResult.Refused refused = Assert.IsType<SprintCreateResult.Refused>(h.Sprints.Create("current"));

        Assert.Equal("A sprint name can't be 'current' or 'none'.", refused.Problem);
        Assert.False(File.Exists(h.Sprints.Path));
    }

    /// <summary>A number a Task still references is never given to a new Sprint, even when its line was hand-deleted.</summary>
    [Fact]
    public void Create_HonoursHighestNumberATaskReferences()
    {
        using TempDataDir dir = new();
        SeedTaskFile(dir, closed: false, new SprintId(5));
        using Harness h = new(TwoSprints, dir);

        SprintCreateResult.Created created = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Later"));

        Assert.Equal(6, created.Sprint.Id.Number);
    }

    /// <summary>A Closed Task's Sprint number also counts towards the next number.</summary>
    [Fact]
    public void Create_HonoursClosedTaskReferences()
    {
        using TempDataDir dir = new();
        SeedTaskFile(dir, closed: true, new SprintId(7));
        using Harness h = new(TwoSprints, dir);

        SprintCreateResult.Created created = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Later"));

        Assert.Equal(8, created.Sprint.Id.Number);
    }

    /// <summary>An append changes no existing byte: headings, notes, odd order and CRLF all stay.</summary>
    [Fact]
    public void Create_LeavesExistingLinesByteIdentical()
    {
        const string original = "# Sprints\r\n\r\nSome notes.\r\n- S2 | Second\r\n- S1 | First\r\n\r\nTrailing note";
        using Harness h = new(original);

        _ = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Third"));

        Assert.Equal(original + "\r\n- S3 | Third\r\n", File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Create re-reads the file, not the snapshot, so a hand edit a moment ago counts for the next number and for the name check.</summary>
    [Fact]
    public void Create_ReadsDiskNotSnapshot()
    {
        using Harness h = new(null);
        File.WriteAllText(h.Sprints.Path, "- S4 | Hand written\n");

        SprintCreateResult existing = h.Sprints.Create("hand WRITTEN");
        SprintCreateResult.Created created = Assert.IsType<SprintCreateResult.Created>(h.Sprints.Create("Fresh"));

        Assert.Equal(new Sprint(new SprintId(4), "Hand written"), Assert.IsType<SprintCreateResult.Existing>(existing).Sprint);
        Assert.Equal(5, created.Sprint.Id.Number);
    }

    /// <summary>An <c>Existing</c> result adopts a Sprint the watcher has not reported yet, so the Task save that follows can find it.</summary>
    [Fact]
    public void Create_ExistingFromUnreportedWrite_IsVisibleInCatalogue()
    {
        using Harness h = new(null);
        File.WriteAllText(h.Sprints.Path, "- S4 | Hand written\n");

        _ = Assert.IsType<SprintCreateResult.Existing>(h.Sprints.Create("Hand written"));

        Assert.NotNull(h.Sprints.Get(new SprintId(4)));
    }

    /// <summary>The current Sprint is the highest number, whatever the line order.</summary>
    [Fact]
    public void Current_IsHighestNumber_WhateverTheLineOrder()
    {
        using Harness h = new("- S3 | Three\n- S1 | One\n- S2 | Two\n");

        Assert.Equal(new Sprint(S3, "Three"), h.Sprints.Current);
        Assert.Equal([1, 2, 3], h.Sprints.All.Select(static s => s.Id.Number));
    }

    /// <summary>A line that looks like a Sprint and is invalid is a problem with its line number, and is skipped.</summary>
    [Fact]
    public void Problems_ReportDuplicateIdWithLine()
    {
        using Harness h = new("- S1 | One\n- S1 | Again\n");

        SprintProblem problem = Assert.Single(h.Sprints.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Equal("Line 2: duplicate id S1; the first one is used.", problem.Text);
        Assert.Single(h.Sprints.All);
    }

    /// <summary>A hand edit is seen by the watcher and raises SprintsChanged exactly once.</summary>
    [Fact]
    public async Task HandEdit_RaisesSprintsChangedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness h = new(TwoSprints);
        int raised = 0;
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sprints.SprintsChanged += () =>
        {
            _ = Interlocked.Increment(ref raised);
            _ = tcs.TrySetResult();
        };

        await File.WriteAllTextAsync(h.Sprints.Path, TwoSprints + "- S3 | Hand added\n", ct);
        await WaitAsync(tcs.Task, ct);
        await Task.Delay(1200, ct);

        Assert.Equal(1, Volatile.Read(ref raised));
        Assert.Equal("Hand added", h.Sprints.Get(S3)?.Name);
    }

    /// <summary>The store's own append raises SprintsChanged once: the watcher's reload of the same write finds the same snapshot.</summary>
    [Fact]
    public async Task OwnAppend_RaisesSprintsChangedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness h = new(TwoSprints);
        int raised = 0;
        h.Sprints.SprintsChanged += () => Interlocked.Increment(ref raised);

        _ = h.Sprints.Create("Third");
        await Task.Delay(1500, ct);

        Assert.Equal(1, Volatile.Read(ref raised));
    }

    /// <summary>A file that cannot be read keeps the last snapshot and raises nothing; a later readable edit is then picked up.</summary>
    [Fact]
    public async Task UnreadableFile_KeepsLastSnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux does not mandatory-lock files opened with FileShare.None");
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness h = new(TwoSprints);
        int raised = 0;
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sprints.SprintsChanged += () =>
        {
            _ = Interlocked.Increment(ref raised);
            _ = tcs.TrySetResult();
        };

        await using (FileStream locked = new(h.Sprints.Path, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = "- S9 | Never seen\n"u8.ToArray();
            await locked.WriteAsync(bytes, ct);
            await locked.FlushAsync(ct);
            await Task.Delay(1500, ct);

            Assert.Equal(0, Volatile.Read(ref raised));
            Assert.Equal(["Sprint 1", "Auth hardening"], h.Sprints.All.Select(static s => s.Name));
        }

        await WaitAsync(tcs.Task, ct);
        Assert.Equal("Never seen", h.Sprints.Current?.Name);
    }

    /// <summary>Moving the last Task out of a Sprint removes its line and leaves every other line, note and ending byte-identical.</summary>
    [Fact]
    public void Removal_LastTaskLeaves_RemovesOnlyThatLine()
    {
        const string original = "# Sprints\r\n\r\nNotes here.\r\n- S1 | Sprint 1\r\n- S2 | Auth hardening\r\nMore notes\r\n- S3 | Third\r\n";
        using Harness h = new(original);
        TaskItem task = h.CreateTask("T", S2);

        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Update(task.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S3) }, null, HumanActor));

        Assert.Equal("# Sprints\r\n\r\nNotes here.\r\n- S1 | Sprint 1\r\nMore notes\r\n- S3 | Third\r\n", File.ReadAllText(h.Sprints.Path));
        Assert.Null(h.Sprints.Get(S2));
        Assert.Equal(2, h.Sprints.All.Count);
    }

    /// <summary>Removing a Sprint raises SprintsChanged once, synchronously, and after the write.</summary>
    [Fact]
    public void Removal_RaisesSprintsChangedOnce()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);
        int raised = 0;
        bool removedWhenRaised = false;
        h.Sprints.SprintsChanged += () =>
        {
            raised++;
            removedWhenRaised = h.Sprints.Get(S2) is null;
        };

        _ = h.Tasks.Update(task.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S1) }, null, HumanActor);

        Assert.Equal(1, raised);
        Assert.True(removedWhenRaised);
    }

    /// <summary>With a second Task still in the Sprint, its line stays.</summary>
    [Fact]
    public void Removal_SecondTaskStillHolds_KeepsLine()
    {
        using Harness h = new(TwoSprints);
        TaskItem first = h.CreateTask("A", S2);
        _ = h.CreateTask("B", S2);

        _ = h.Tasks.Update(first.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S1) }, null, HumanActor);

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
        Assert.NotNull(h.Sprints.Get(S2));
    }

    /// <summary>A Closed Task still holds its Sprint, so the line stays when the last Active Task leaves.</summary>
    [Fact]
    public void Removal_ClosedTaskStillHolds_KeepsLine()
    {
        using Harness h = new(TwoSprints);
        TaskItem closed = h.CreateTask("Done", S2);
        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Close(closed.Id, HumanActor));
        TaskItem open = h.CreateTask("Open", S2);

        _ = h.Tasks.Update(open.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S1) }, null, HumanActor);

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Clearing the Sprint of the last Task also removes the line.</summary>
    [Fact]
    public void Removal_ClearingTheSprint_RemovesLine()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);

        _ = h.Tasks.Update(task.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(null) }, null, HumanActor);

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>A Task file that fails to load might hold the Sprint, so nothing is removed while one is rejected.</summary>
    [Fact]
    public void Removal_RejectedTaskFile_KeepsLine()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string broken = Path.Combine(root, "Platform", "_tasks", "PLAT-0099.md");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        File.WriteAllText(broken, "this is not a task file\n");
        using Harness h = new(TwoSprints, dir);
        Assert.NotEmpty(h.Store.RejectedFiles);
        TaskItem task = h.CreateTask("T", S2);

        _ = h.Tasks.Update(task.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S1) }, null, HumanActor);

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>An outside edit that vacates a Sprint removes its line, as a save does.</summary>
    [Fact]
    public void Removal_OutsideEditVacates_RemovesLine()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);
        TaskItem current = h.Store.Get(task.Id) ?? throw new InvalidOperationException("task missing");
        File.WriteAllText(current.Path, TaskFileFormat.Compose(current with { Sprint = S1 }));

        h.Store.RebuildFromWatcher();

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Creating a Task, changing other fields, moving Project, closing and reopening never remove a Sprint.</summary>
    [Fact]
    public void Removal_NonVacatingChanges_NeverRemove()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);
        _ = h.CreateTask("Another", S2);

        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Update(task.Id, new TaskPatch { Priority = TaskPriority.Urgent, Title = "Renamed" }, null, HumanActor));
        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Update(task.Id, new TaskPatch { Project = Optional<string?>.Set("Auth v2") }, null, HumanActor));
        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Close(task.Id, HumanActor));
        _ = Assert.IsType<TaskResult.Saved>(h.Tasks.Reopen(task.Id, HumanActor));

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
        Assert.Equal(S2, h.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>The same non-vacating rule holds when the only Task in the Sprint is the one changed: other fields, Project move and close leave the line.</summary>
    [Fact]
    public void Removal_OnlyTaskChangedOtherwise_KeepsLine()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);

        _ = h.Tasks.Update(task.Id, new TaskPatch { Priority = TaskPriority.High }, null, HumanActor);
        _ = h.Tasks.Update(task.Id, new TaskPatch { Project = Optional<string?>.Set("Auth v2") }, null, HumanActor);
        _ = h.Tasks.Close(task.Id, HumanActor);

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Deleting a Task file by hand, and a rebuilt index with fewer Tasks, remove nothing.</summary>
    [Fact]
    public void Removal_DeletedTaskFileAndShrunkIndex_RemoveNothing()
    {
        using Harness h = new(TwoSprints);
        TaskItem first = h.CreateTask("A", S1);
        TaskItem second = h.CreateTask("B", S2);
        int raised = 0;
        h.Sprints.SprintsChanged += () => raised++;

        File.Delete(first.Path);
        h.Store.RebuildFromWatcher();
        Directory.Delete(Path.GetDirectoryName(second.Path)!, recursive: true);
        h.Store.RebuildFromWatcher();

        Assert.Empty(h.Store.All);
        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
        Assert.Equal(0, raised);
    }

    /// <summary>RemoveIfUnused is false for an id the catalogue does not have, and writes nothing.</summary>
    [Fact]
    public void RemoveIfUnused_UnknownId_False()
    {
        using Harness h = new(TwoSprints);

        Assert.False(h.Sprints.RemoveIfUnused(new SprintId(9)));

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>RemoveIfUnused is false while a Task holds the Sprint and true once nothing does (the Task panel's undo of a Sprint it just created).</summary>
    [Fact]
    public void RemoveIfUnused_HeldThenUnheld()
    {
        using Harness h = new(TwoSprints);
        _ = h.CreateTask("T", S1);

        Assert.False(h.Sprints.RemoveIfUnused(S1));
        Assert.True(h.Sprints.RemoveIfUnused(S2));

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>A removal re-reads the file, so a hand edit a moment ago survives.</summary>
    [Fact]
    public void RemoveIfUnused_ReadsDiskNotSnapshot()
    {
        using Harness h = new(TwoSprints);
        File.WriteAllText(h.Sprints.Path, TwoSprints + "- S3 | Hand added\n");

        Assert.True(h.Sprints.RemoveIfUnused(S2));

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n- S3 | Hand added\n", File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>After Dispose the store no longer reacts to TaskChanged, and disposing twice is harmless.</summary>
    [Fact]
    public void Dispose_UnsubscribesFromTaskChanged()
    {
        using Harness h = new(TwoSprints);
        TaskItem task = h.CreateTask("T", S2);

        h.Sprints.Dispose();
        h.Sprints.Dispose();
        _ = h.Tasks.Update(task.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(S1) }, null, HumanActor);

        Assert.Equal(TwoSprints, File.ReadAllText(h.Sprints.Path));
    }

    /// <summary>Writes a Platform Task file holding <paramref name="sprint"/> before any store is built, so the Sprint need not exist in <c>sprints.md</c>.</summary>
    /// <param name="dir">The data directory the stores will scan.</param>
    /// <param name="closed">Whether the Task lives in the closed folder.</param>
    /// <param name="sprint">The Sprint the Task holds.</param>
    private static void SeedTaskFile(TempDataDir dir, bool closed, SprintId sprint) =>
        _ = TestTaskStore.WriteTask(
            TestTaskStore.Root(dir),
            TestTaskStore.RelativePath("Platform", null, closed, "PLAT-0050.md"),
            TestTasks.Make(id: "PLAT-0050", creator: "You", sprint: sprint, location: new("Platform", null, closed)));

    /// <summary>Waits for a task for at most ten seconds.</summary>
    /// <param name="task">The task to wait for.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task WaitAsync(Task task, CancellationToken ct)
    {
        await task.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    /// <summary>Real stores over one data directory, wired as the app wires them.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir? ownedDir;
        private readonly PersonaStore personas;

        /// <summary>Builds the stores. A non-null <paramref name="sprintsText"/> is written to <c>sprints.md</c> first.</summary>
        /// <param name="sprintsText">The initial file text, or <see langword="null"/> for no file.</param>
        /// <param name="dir">A data directory to use, or <see langword="null"/> to own a new one.</param>
        public Harness(string? sprintsText, TempDataDir? dir = null)
        {
            this.ownedDir = dir is null ? new TempDataDir() : null;
            this.Dir = dir ?? this.ownedDir!;
            if (sprintsText is not null)
            {
                File.WriteAllText(Path.Combine(this.Dir.Path, "sprints.md"), sprintsText);
            }

            ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
            this.personas = TestTaskStore.CreatePersonaStore(this.Dir);
            _ = this.personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
            this.Store = new TaskStore(this.Dir.Options(), this.personas, clock, NullLogger<TaskStore>.Instance);
            this.Events = new TaskEvents();
            this.Sprints = new SprintStore(this.Dir.Options(), this.Store, this.Events, NullLogger<SprintStore>.Instance);
            this.Tasks = new TaskService(this.Store, new TaskIdAllocator(this.Dir.Options()), this.Events, this.personas, this.Sprints, this.Dir.Options(), clock, NullLogger<TaskService>.Instance);
        }

        public TempDataDir Dir { get; }

        public TaskStore Store { get; }

        public TaskEvents Events { get; }

        public TaskService Tasks { get; }

        public SprintStore Sprints { get; }

        /// <summary>Creates a Platform Task, then sets its Sprint with an update (a vacating change is never a create), and returns it as stored.</summary>
        /// <param name="title">The Task's title.</param>
        /// <param name="sprint">The Sprint it holds.</param>
        public TaskItem CreateTask(string title, SprintId sprint)
        {
            TaskItem created = Assert.IsType<TaskResult.Saved>(this.Tasks.Create(new TaskDraft(title, "Platform", null), HumanActor)).Task;
            return Assert.IsType<TaskResult.Saved>(this.Tasks.Update(created.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(sprint) }, null, HumanActor)).Task;
        }

        public void Dispose()
        {
            this.Sprints.Dispose();
            this.Tasks.Dispose();
            this.Store.Dispose();
            this.personas.Dispose();
            this.ownedDir?.Dispose();
        }
    }
}
