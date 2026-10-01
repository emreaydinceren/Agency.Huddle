using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskService"/>'s <see cref="TaskService.Create"/> and
/// <see cref="TaskService.Update"/> paths (Spec §9.1, §9.2, §9.4, and §9.5; Tasks 6.1-6.2).
/// </summary>
public sealed class TaskServiceTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>A valid Create writes the file at its Team/Project layout path with one "created" Change log entry by the actor.</summary>
    [Fact]
    public void Create_WritesFileAtLayoutPath_WithCreatedEntry()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("Ship it", "Platform", "Auth v2"), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        string expectedPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", "Auth v2", closed: false, "PLAT-0001.md"));
        Assert.Equal(expectedPath, saved.Task.Path);
        ChangeLogEntry entry = Assert.Single(saved.Task.ChangeLog);
        Assert.Equal("created", entry.Summary);
        Assert.Equal("You", entry.Actor);
    }

    /// <summary>TaskChanged is raised exactly once, and only after the file has actually been written.</summary>
    [Fact]
    public void Create_RaisesTaskChangedOnce_AfterWrite()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);

        int raiseCount = 0;
        bool fileExistedWhenRaised = false;
        events.TaskChanged += change =>
        {
            raiseCount++;
            fileExistedWhenRaised = File.Exists(change.After.Path);
        };

        TaskResult result = service.Create(new TaskDraft("Ship it", "Platform", null), HumanActor);

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(1, raiseCount);
        Assert.True(fileExistedWhenRaised);
    }

    /// <summary>An assignee given as an Alias is resolved and stored as the Persona's Name.</summary>
    [Fact]
    public void Create_AliasAssignee_StoredAsName()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", null, Assignee: "kai"), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Kai", saved.Task.Assignee);
    }

    /// <summary>An empty title, an unknown Team and an unknown assignee are all reported together, in one Refused.</summary>
    [Fact]
    public void Create_AllProblemsReportedTogether()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new(Title: "", Team: "Nonexistent", Project: null, Assignee: "Ghost");
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(3, refused.Problems.Count);
    }

    /// <summary>A description that contains the reserved Change log heading is refused with the exact Spec §9.2 text.</summary>
    [Fact]
    public void Create_DescriptionWithLogHeading_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("T", "Platform", null, Description: "Body\n## Change log\nmore");
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The description must not contain a '## Change log' heading; that heading is reserved for the task's history."], refused.Problems);
    }

    /// <summary>Updating a Task's Description to one that contains the reserved Change log heading is refused with the same exact text as Create (shared rule, second entry point).</summary>
    [Fact]
    public void Update_DescriptionWithLogHeading_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Description = "Body\n## Change log\nmore" }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The description must not contain a '## Change log' heading; that heading is reserved for the task's history."], refused.Problems);
    }

    /// <summary>An empty (or whitespace-only) title is refused with the exact Spec §9.2 text.</summary>
    [Fact]
    public void Create_EmptyTitle_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("   ", "Platform", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title is empty."], refused.Problems);
    }

    /// <summary>Updating a Task's Title to an empty (or whitespace-only) one is refused with the same exact text as Create (shared rule, second entry point).</summary>
    [Fact]
    public void Update_EmptyTitle_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Title = "   " }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title is empty."], refused.Problems);
    }

    /// <summary>A title over 200 characters is refused with the exact Spec §9.2 text, naming the trimmed length.</summary>
    [Fact]
    public void Create_TitleTooLong_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new(new string('A', 240), "Platform", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title is 240 characters; the limit is 200."], refused.Problems);
    }

    /// <summary>Updating a Task's Title to one over 200 characters is refused with the same exact text as Create (shared rule, second entry point).</summary>
    [Fact]
    public void Update_TitleTooLong_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Title = new string('A', 240) }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title is 240 characters; the limit is 200."], refused.Problems);
    }

    /// <summary>A title spanning more than one line is refused with the exact Spec §9.2 text.</summary>
    [Fact]
    public void Create_TitleMultiline_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("Line one\nLine two", "Platform", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title must be one line."], refused.Problems);
    }

    /// <summary>Updating a Task's Title to one spanning more than one line is refused with the same exact text as Create (shared rule, second entry point).</summary>
    [Fact]
    public void Update_TitleMultiline_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Title = "Line one\nLine two" }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Title must be one line."], refused.Problems);
    }

    /// <summary>A tag containing a comma is refused with the exact Spec §9.2/§7.2 text, naming the offending tag.</summary>
    [Fact]
    public void Create_TagWithComma_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("T", "Platform", null, Tags: ["a,b"]);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Tag 'a,b' must not contain ',' or ';'."], refused.Problems);
    }

    /// <summary>Updating a Task's Tags to one containing a semicolon is refused with the same wording as Create (shared rule, second entry point).</summary>
    [Fact]
    public void Update_TagWithSemicolon_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Tags = ["x;y"] }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Tag 'x;y' must not contain ',' or ';'."], refused.Problems);
    }

    /// <summary>Setting a Task's Parent to its own id is refused with the exact Spec §9.2 self-reference text (one of three call sites sharing this wording).</summary>
    [Fact]
    public void Update_ParentSelf_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Parent = Optional<TaskId?>.Set(task.Id) }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["A task cannot block itself."], refused.Problems);
    }

    /// <summary>Setting a Task's BlockedBy to include its own id is refused with the exact Spec §9.2 self-reference text (the second of three call sites sharing this wording).</summary>
    [Fact]
    public void Update_BlockedBySelf_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { BlockedBy = [task.Id] }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["A task cannot block itself."], refused.Problems);
    }

    /// <summary>Setting a Task's DuplicateOf to its own id (alongside Status Duplicate, so the coupling rule is satisfied) is refused with the exact Spec §9.2 self-reference text (the third of three call sites sharing this wording).</summary>
    [Fact]
    public void Update_DuplicateOfSelf_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(
            task.Id,
            new TaskPatch { Status = TaskState.Duplicate, DuplicateOf = Optional<TaskId?>.Set(task.Id) },
            null,
            HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["A task cannot block itself."], refused.Problems);
    }

    /// <summary>A Reason over 200 characters is refused with the exact length text, isolated from the status-coupling rule by pairing it with Cancelled.</summary>
    [Fact]
    public void Update_ReasonTooLong_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(
            task.Id,
            new TaskPatch { Status = TaskState.Cancelled, Reason = new string('B', 240) },
            null,
            HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Reason is 240 characters; the limit is 200."], refused.Problems);
    }

    /// <summary>A Team name that can't be a folder on this computer is refused, with a problem naming the offending character.</summary>
    [Fact]
    public void Create_TeamWithInvalidFolderChars_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("T", "Ops:Legal", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The team name 'Ops:Legal' cannot be a folder name on this computer (it contains ':')."], refused.Problems);
    }

    /// <summary>A BlockedBy id that doesn't exist in the index is refused, naming the unknown id.</summary>
    [Fact]
    public void Create_UnknownBlockedBy_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        _ = TaskId.TryParse("PLAT-0999", out TaskId missing);

        TaskDraft draft = new("T", "Platform", null, BlockedBy: [missing]);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Unknown task 'PLAT-0999'."], refused.Problems);
    }

    /// <summary>Creating a Task in a Team whose folder already exists under a different case reuses the existing folder's casing.</summary>
    [Fact]
    public void Create_ExistingFolderDifferentCase_UsesExistingFolder()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "platform", null), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Platform", saved.Task.Location.Team);
        Assert.Equal(Path.Combine(root, "Platform", "_tasks", "PLAT-0001.md"), saved.Task.Path);
    }

    /// <summary>Updating a Task's status logs a "status: ..." summary and raises exactly one TaskChanged.</summary>
    [Fact]
    public void Update_Status_LogsSummaryAndRaisesEvent()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskChange? raised = null;
        events.TaskChanged += change => raised = change;

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
        Assert.NotNull(raised);
        Assert.Equal("status: Backlog → In Progress", saved.Change.Entry.Summary);
    }

    /// <summary>Applying a patch whose values already match the current Task returns Unchanged without touching the file.</summary>
    [Fact]
    public void Update_SameValues_ReturnsUnchanged_NoWrite()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Priority: TaskPriority.Medium));
        byte[] before = File.ReadAllBytes(task.Path);
        DateTime mtime = File.GetLastWriteTimeUtc(task.Path);

        TaskResult result = service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Medium }, null, HumanActor);

        Assert.IsType<TaskResult.Unchanged>(result);
        Assert.Equal(before, File.ReadAllBytes(task.Path));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(task.Path));
    }

    /// <summary>Changing a Task's Team moves its file to the new Team's folder and logs a "moved:" summary.</summary>
    [Fact]
    public void Update_TeamChange_MovesFile_LogsMoved()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Marketing"));
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Team = "Marketing" }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Marketing", saved.Task.Location.Team);
        Assert.False(File.Exists(task.Path));
        Assert.True(File.Exists(saved.Task.Path));
        Assert.Equal("moved: Platform → Marketing", saved.Change.Entry.Summary);
    }

    /// <summary>Setting Project to null moves a Task from a Project sub-folder back to its Team root.</summary>
    [Fact]
    public void Update_ProjectSetNull_MovesToTeamRoot()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", "Auth v2"));

        TaskResult result = service.Update(task.Id, new TaskPatch { Project = Optional<string?>.Set(null) }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Location.Project);
        Assert.Equal(Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", null, closed: false, $"{task.Id}.md")), saved.Task.Path);
    }

    /// <summary>A reason given alongside a move to Cancelled is appended to the Change log summary.</summary>
    [Fact]
    public void Update_ReasonWithCancelled_AppendedToSummary()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.Cancelled, Reason = "no longer needed" }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("status: Backlog → Cancelled (reason: no longer needed)", saved.Change.Entry.Summary);
    }

    /// <summary>A reason given with a move to Done, which isn't Cancelled or Rejected, is refused with the exact Spec §9.2 text.</summary>
    [Fact]
    public void Update_ReasonWithDone_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.Done, Reason = "done early" }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Contains("A reason is only recorded when the status becomes Cancelled or Rejected.", refused.Problems);
    }

    /// <summary>Moving a Task to Duplicate without a duplicate_of is refused.</summary>
    [Fact]
    public void Update_DuplicateWithoutDuplicateOf_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.Duplicate }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Status Duplicate needs duplicate_of."], refused.Problems);
    }

    /// <summary>Setting a Task's parent to one of its own descendants is refused as a cycle.</summary>
    [Fact]
    public void Update_ParentCycle_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem a = SeedTask(service, new TaskDraft("A", "Platform", null));
        TaskItem b = SeedTask(service, new TaskDraft("B", "Platform", null, Parent: a.Id));

        TaskResult result = service.Update(a.Id, new TaskPatch { Parent = Optional<TaskId?>.Set(b.Id) }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"{a.Id} is already a descendant of {b.Id}, so it cannot be its parent."], refused.Problems);
    }

    /// <summary>Updating an id that isn't in the index returns NotFound.</summary>
    [Fact]
    public void Update_UnknownId_NotFound()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        _ = TaskId.TryParse("PLAT-9999", out TaskId missing);

        TaskResult result = service.Update(missing, new TaskPatch { Title = "x" }, null, HumanActor);

        Assert.IsType<TaskResult.NotFound>(result);
    }

    /// <summary>Setting Assignee to Optional.Set(null) unassigns the Task.</summary>
    [Fact]
    public void Update_Unassign_WithOptionalSetNull()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Assignee: "Kai"));

        TaskResult result = service.Update(task.Id, new TaskPatch { Assignee = Optional<string?>.Set(null) }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Assignee);
    }

    /// <summary>A hand-edited Change log line, with unusual spacing no formatter would produce, survives an unrelated Update byte for byte.</summary>
    [Fact]
    public void Update_HandEditedLogLine_Preserved()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        string original = File.ReadAllText(task.Path);
        const string WeirdLine = "-   2026-01-01T00:00:00Z   |   Someone   |   did   something   weird  ";
        string handEdited = original.TrimEnd('\n') + "\n" + WeirdLine + "\n";
        TaskItem? edited = store.Write(task, task.Version, handEdited);
        Assert.NotNull(edited);

        TaskResult result = service.Update(task.Id, new TaskPatch { Priority = TaskPriority.High }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        string[] handEditedLines = handEdited.Split('\n');
        int weirdLineIndex = Array.IndexOf(handEditedLines, WeirdLine);
        string[] finalLines = File.ReadAllText(saved.Task.Path).Split('\n');
        Assert.Equal(WeirdLine, finalLines[weirdLineIndex]);
    }

    /// <summary>25 concurrent Updates on the same Task, released together by one gate, all succeed and every one's Change log entry survives - proven against a stub via mutation testing (mutateGate removed).</summary>
    [Fact]
    public void Update_Concurrent_BothChangesSurvive()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        const int Rounds = 25;
        using ManualResetEventSlim gate = new(initialState: false);
        TaskResult[] results = new TaskResult[Rounds];
        Thread[] threads = new Thread[Rounds];

        for (int i = 0; i < Rounds; i++)
        {
            int index = i;
            threads[index] = new Thread(() =>
            {
                gate.Wait();
                results[index] = service.Update(
                    task.Id,
                    new TaskPatch { Tags = [string.Create(CultureInfo.InvariantCulture, $"round{index}")] },
                    null,
                    HumanActor);
            });
            threads[index].Start();
        }

        gate.Set();
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.All(results, result => Assert.IsType<TaskResult.Saved>(result));
        TaskItem? final = store.Get(task.Id);
        Assert.NotNull(final);
        Assert.Equal(Rounds + 1, final.ChangeLog.Count);
    }

    /// <summary>Create never overwrites: a file already sitting at the id it is about to allocate (a hand-made or rejected file, as if copied in outside Huddle) makes Create throw rather than clobber it.</summary>
    [Fact]
    public void Create_FileAlreadyExistsAtAllocatedPath_ThrowsAndDoesNotOverwrite()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        string existingPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", null, closed: false, "PLAT-0001.md"));
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath) ?? throw new InvalidOperationException("Expected a parent directory."));
        File.WriteAllText(existingPath, "not a task file");

        _ = Assert.Throws<InvalidOperationException>(() => service.Create(new TaskDraft("T", "Platform", null), HumanActor));

        Assert.Equal("not a task file", File.ReadAllText(existingPath));
    }

    /// <summary>A Task file present on disk with a higher number than any id the allocator itself has handed out (copied in by hand) still raises the next allocated number past it.</summary>
    [Fact]
    public void Create_HigherNumberedFileAlreadyOnDisk_AllocatesPastIt()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        string tasksDir = Path.Combine(TestTaskStore.Root(dir), "Platform", "_tasks");
        Directory.CreateDirectory(tasksDir);
        File.WriteAllText(Path.Combine(tasksDir, "PLAT-0005.md"), "not a real task file");
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", null), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("PLAT-0006", saved.Task.Id.ToString());
    }

    /// <summary>A status-only Update is validated only for the fields it touches: a Task whose Assignee no longer resolves (its Persona was removed after the Task was created) still accepts the status change.</summary>
    [Fact]
    public void Update_StatusOnly_DoesNotValidateUntouchedAssignee()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Assignee: "Kai"));
        personas.Remove("Kai");

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
    }

    /// <summary>Moving a Task to an existing Team folder under a different case reuses that folder's own casing, the same as Create does.</summary>
    [Fact]
    public void Update_TeamExistingFolderDifferentCase_UsesExistingFolder()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Marketing"));
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Team = "marketing" }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Marketing", saved.Task.Location.Team);
    }

    /// <summary>A Team name that is a reserved Windows device name is refused, naming the reservation, even on an OS where that name is otherwise legal.</summary>
    [Fact]
    public void Create_ReservedTeamName_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("T", "CON", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The team name 'CON' cannot be a folder name on this computer (it is reserved)."], refused.Problems);
    }

    /// <summary>A Team name that ends with a trailing space is refused, naming the offending character, even on an OS that would otherwise accept it.</summary>
    [Fact]
    public void Create_TeamWithTrailingSpace_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskDraft draft = new("T", "Ops ", null);
        TaskResult result = service.Create(draft, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The team name 'Ops ' cannot be a folder name on this computer (it ends with ' ')."], refused.Problems);
    }

    /// <summary>TaskChanged is raised exactly once for an Update, the same guarantee <see cref="Create_RaisesTaskChangedOnce_AfterWrite"/> proves for Create.</summary>
    [Fact]
    public void Update_RaisesTaskChangedExactlyOnce()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, HumanActor);

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(1, raiseCount);
    }

    /// <summary>A status change to a state other than Cancelled or Rejected, with no Reason offered, does not grow a "(reason: ...)" suffix on the Change log summary.</summary>
    [Fact]
    public void Update_StatusChangeWithoutReason_SummaryHasNoReasonSuffix()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.DoesNotContain("(reason:", saved.Change.Entry.Summary, StringComparison.Ordinal);
    }

    /// <summary>A current <c>baseVersion</c> (matching the Task's own <see cref="TaskItem.Version"/>) is applied the same as a null one.</summary>
    [Fact]
    public void Update_BaseCurrent_Applies()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, task.Version, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
    }

    /// <summary>A stale base whose intervening change (status) doesn't overlap this patch's field (priority) merges: both end up in the file.</summary>
    [Fact]
    public void Update_BaseStale_DisjointFields_Merges()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Priority: TaskPriority.Medium));
        string baseVersion = task.Version;

        TaskResult otherResult = service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, HumanActor);
        TaskResult.Saved otherSaved = Assert.IsType<TaskResult.Saved>(otherResult);

        TaskResult result = service.Update(task.Id, new TaskPatch { Priority = TaskPriority.High }, baseVersion, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
        Assert.Equal(TaskPriority.High, saved.Task.Priority);
        _ = otherSaved;
    }

    /// <summary>A stale base whose intervening change overlaps this patch's field (both touch Description) with a genuinely different value conflicts, naming the field and the newer Task as Current.</summary>
    [Fact]
    public void Update_BaseStale_OverlappingField_Conflict()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Description: "original"));
        string baseVersion = task.Version;

        TaskResult otherResult = service.Update(task.Id, new TaskPatch { Description = "changed by someone else" }, null, HumanActor);
        TaskResult.Saved otherSaved = Assert.IsType<TaskResult.Saved>(otherResult);

        TaskResult result = service.Update(task.Id, new TaskPatch { Description = "changed by me" }, baseVersion, HumanActor);

        TaskResult.Conflict conflict = Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal([TaskField.Description], conflict.Fields);
        Assert.Equal(otherSaved.Task.Version, conflict.Current.Version);
    }

    /// <summary>A stale base whose intervening change overlaps this patch's field, but this patch happens to set it to the same value the field already has, is not a conflict (corrections-B5 decision C).</summary>
    [Fact]
    public void Update_BaseStale_OverlapWithEqualValue_NotConflict()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Description: "original", Priority: TaskPriority.Medium));
        string baseVersion = task.Version;

        TaskResult otherResult = service.Update(task.Id, new TaskPatch { Description = "changed by someone else" }, null, HumanActor);
        TaskResult.Saved otherSaved = Assert.IsType<TaskResult.Saved>(otherResult);

        TaskResult result = service.Update(
            task.Id,
            new TaskPatch { Description = "changed by someone else", Priority = TaskPriority.High },
            baseVersion,
            HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("changed by someone else", saved.Task.Description);
        Assert.Equal(TaskPriority.High, saved.Task.Priority);
        _ = otherSaved;
    }

    /// <summary>A base version evicted from the last-20 history (21 intervening writes) conflicts on any patched field that genuinely differs from the current Task.</summary>
    [Fact]
    public void Update_BaseEvicted_AnyDifferingPatchedFieldConflicts()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        string baseVersion = task.Version;

        for (int i = 0; i < 21; i++)
        {
            TaskResult intervening = service.Update(
                task.Id,
                new TaskPatch { Tags = [string.Create(CultureInfo.InvariantCulture, $"round{i}")] },
                null,
                HumanActor);
            _ = Assert.IsType<TaskResult.Saved>(intervening);
        }

        Assert.Null(store.GetVersion(task.Id, baseVersion));

        TaskResult result = service.Update(task.Id, new TaskPatch { Title = "A brand new title" }, baseVersion, HumanActor);

        TaskResult.Conflict conflict = Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal([TaskField.Title], conflict.Fields);
    }

    /// <summary>A base version evicted from history, patched only with a field whose value already equals the current Task's, does not conflict.</summary>
    [Fact]
    public void Update_BaseEvicted_NonDifferingPatchedField_Applies()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        string baseVersion = task.Version;

        TaskItem? current = task;
        for (int i = 0; i < 21; i++)
        {
            TaskResult intervening = service.Update(
                task.Id,
                new TaskPatch { Tags = [string.Create(CultureInfo.InvariantCulture, $"round{i}")] },
                null,
                HumanActor);
            current = Assert.IsType<TaskResult.Saved>(intervening).Task;
        }

        Assert.Null(store.GetVersion(task.Id, baseVersion));
        Assert.NotNull(current);

        TaskResult result = service.Update(task.Id, new TaskPatch { Title = current.Title }, baseVersion, HumanActor);

        Assert.IsNotType<TaskResult.Conflict>(result);
    }

    /// <summary>A Conflict result writes nothing and raises nothing: the file's bytes and the TaskChanged count are unchanged.</summary>
    [Fact]
    public void Update_Conflict_WritesNothing_RaisesNothing()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Description: "original"));
        string baseVersion = task.Version;

        TaskResult otherResult = service.Update(task.Id, new TaskPatch { Description = "changed by someone else" }, null, HumanActor);
        TaskItem afterOther = Assert.IsType<TaskResult.Saved>(otherResult).Task;

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;
        byte[] before = File.ReadAllBytes(afterOther.Path);
        DateTime mtime = File.GetLastWriteTimeUtc(afterOther.Path);

        TaskResult result = service.Update(task.Id, new TaskPatch { Description = "changed by me" }, baseVersion, HumanActor);

        Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal(0, raiseCount);
        Assert.Equal(before, File.ReadAllBytes(afterOther.Path));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(afterOther.Path));
    }

    /// <summary>A Team-level Task moves into the Team's "_closed" folder, gains a "closed" Change log entry, and raises TaskChanged (Spec §9, §7.4).</summary>
    [Fact]
    public void Close_MovesToClosed_LogsClosed_RaisesEvent()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        TaskResult result = service.Close(task.Id, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        string expectedPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", null, closed: true, $"{task.Id}.md"));
        Assert.Equal(expectedPath, saved.Task.Path);
        Assert.True(saved.Task.Location.Closed);
        Assert.Equal("closed", Assert.Single(saved.Task.ChangeLog, entry => entry.Summary == "closed").Summary);
        Assert.Equal(1, raiseCount);
    }

    /// <summary>A Task under a Project moves into that Project's "_closed" folder, not the Team's (Spec §5.2, §9).</summary>
    [Fact]
    public void Close_ProjectTask_MovesToClosedUnderProject()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", "Auth v2"));

        TaskResult result = service.Close(task.Id, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        string expectedPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", "Auth v2", closed: true, $"{task.Id}.md"));
        Assert.Equal(expectedPath, saved.Task.Path);
    }

    /// <summary>Closing isn't restricted to Done: an In Progress Task can be closed too (Spec §9).</summary>
    [Fact]
    public void Close_InProgressTask_Allowed()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null, Status: TaskState.InProgress));

        TaskResult result = service.Close(task.Id, HumanActor);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Closing an already-Closed Task is refused, naming the Task by id (Spec §11.6, reused for TaskService).</summary>
    [Fact]
    public void Close_AlreadyClosed_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        _ = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor));

        TaskResult result = service.Close(task.Id, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([string.Create(CultureInfo.InvariantCulture, $"{task.Id} is already closed.")], refused.Problems);
    }

    /// <summary>A successful Close raises TaskChanged exactly once, with an empty Changes list (TaskDiff.Compare ignores Location.Closed - Settled corrections-B2 D6 item 7) and a "closed" entry summary.</summary>
    [Fact]
    public void Close_RaisesTaskChangedExactlyOnce_EmptyChanges_SummaryClosed()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        List<TaskChange> raised = [];
        events.TaskChanged += raised.Add;

        TaskResult result = service.Close(task.Id, HumanActor);

        Assert.IsType<TaskResult.Saved>(result);
        TaskChange change = Assert.Single(raised);
        Assert.Empty(change.Changes);
        Assert.Equal("closed", change.Entry.Summary);
    }

    /// <summary>Closing an id that isn't in the index reports the Task as not found (mirrors Update_UnknownId_NotFound; Spec §9.1's TaskResult.NotFound).</summary>
    [Fact]
    public void Close_UnknownId_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskId unknown = new("PLAT", 9999);

        TaskResult result = service.Close(unknown, HumanActor);

        TaskResult.NotFound notFound = Assert.IsType<TaskResult.NotFound>(result);
        Assert.Equal(unknown, notFound.Id);
    }

    /// <summary>
    /// Close has no baseVersion parameter, but still goes through TaskStore's version-checked Move
    /// (Spec §9.3's mechanism, reused): if the file on disk was changed since this TaskService last
    /// read it - simulated here by writing a valid outside edit straight to disk, bypassing the
    /// store, so its cached index still holds the old version - the Move's version check fails and
    /// TaskService reports a Conflict instead of silently overwriting the other change.
    /// </summary>
    [Fact]
    public void Close_DiskVersionChangedSinceRead_Conflict()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        string onDisk = File.ReadAllText(task.Path);
        string outsideEdit = TaskFileFormat.AppendEntry(onDisk, new ChangeLogEntry(DateTimeOffset.UtcNow, "Someone", "edited outside Huddle"));
        File.WriteAllText(task.Path, outsideEdit);

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        TaskResult result = service.Close(task.Id, HumanActor);

        Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal(0, raiseCount);
    }

    /// <summary>A Closed Task moves back out of "_closed", gains a "reopened" Change log entry, and raises TaskChanged (Spec §9, §7.4).</summary>
    [Fact]
    public void Reopen_MovesBack_LogsReopened()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", "Auth v2"));
        TaskItem closed = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor)).Task;

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        TaskResult result = service.Reopen(closed.Id, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        string expectedPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Platform", "Auth v2", closed: false, $"{task.Id}.md"));
        Assert.Equal(expectedPath, saved.Task.Path);
        Assert.False(saved.Task.Location.Closed);
        Assert.Equal("reopened", Assert.Single(saved.Task.ChangeLog, entry => entry.Summary == "reopened").Summary);
        Assert.Equal(1, raiseCount);
    }

    /// <summary>Reopening an Active (not Closed) Task is refused, mirroring Close_AlreadyClosed_Refused's wording (Spec §11.6: "reopening an Active one returns the mirror of that").</summary>
    [Fact]
    public void Reopen_Active_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Reopen(task.Id, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([string.Create(CultureInfo.InvariantCulture, $"{task.Id} is already active.")], refused.Problems);
    }

    /// <summary>Reopening an id that isn't in the index reports the Task as not found, mirroring Close_UnknownId_Refused.</summary>
    [Fact]
    public void Reopen_UnknownId_Refused()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskId unknown = new("PLAT", 9999);

        TaskResult result = service.Reopen(unknown, HumanActor);

        TaskResult.NotFound notFound = Assert.IsType<TaskResult.NotFound>(result);
        Assert.Equal(unknown, notFound.Id);
    }

    /// <summary>Reopen mirrors Close_DiskVersionChangedSinceRead_Conflict: a stale cached version loses the Move's version check and reports a Conflict rather than overwriting an outside edit.</summary>
    [Fact]
    public void Reopen_DiskVersionChangedSinceRead_Conflict()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskEvents events = new();
        TaskService service = CreateTaskService(dir, store, personas, events);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        TaskItem closed = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor)).Task;

        string onDisk = File.ReadAllText(closed.Path);
        string outsideEdit = TaskFileFormat.AppendEntry(onDisk, new ChangeLogEntry(DateTimeOffset.UtcNow, "Someone", "edited outside Huddle"));
        File.WriteAllText(closed.Path, outsideEdit);

        int raiseCount = 0;
        events.TaskChanged += _ => raiseCount++;

        TaskResult result = service.Reopen(closed.Id, HumanActor);

        Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal(0, raiseCount);
    }

    /// <summary>An Update that only changes the Team keeps a Closed Task closed - it moves within "_closed", it doesn't reopen it (Settled corrections-B2 D6 item 7: "An Update on a closed Task keeps Closed when it moves the file").</summary>
    [Fact]
    public void Update_OnClosedTask_StaysClosed()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        _ = personas.Add(new PersonaIdentity("Rae", "Rae", "rae", ["Marketing"]), "You are Rae.");
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        TaskItem closed = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor)).Task;

        TaskResult result = service.Update(closed.Id, new TaskPatch { Team = "Marketing" }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.True(saved.Task.Location.Closed);
        Assert.Equal("Marketing", saved.Task.Location.Team);
        string expectedPath = Path.Combine(store.RootDirectory, TestTaskStore.RelativePath("Marketing", null, closed: true, $"{task.Id}.md"));
        Assert.Equal(expectedPath, saved.Task.Path);
    }

    /// <summary>ClosedAt is set to the "closed" entry's time after Close, and cleared after Reopen (Spec §7.4: "the At of the last closed entry that isn't followed by a reopened entry, and only when Location.Closed is true").</summary>
    [Fact]
    public void ClosedAt_SetAfterClose_NullAfterReopen()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));
        Assert.Null(task.ClosedAt);

        TaskItem closed = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor)).Task;
        Assert.NotNull(closed.ClosedAt);

        TaskItem reopened = Assert.IsType<TaskResult.Saved>(service.Reopen(closed.Id, HumanActor)).Task;
        Assert.Null(reopened.ClosedAt);
    }

    /// <summary>A second Close after a Reopen derives ClosedAt from the newest "closed" entry, not the first one (Spec §7.4).</summary>
    [Fact]
    public void ClosedAt_AfterCloseReopenClose_UsesLatestClosedEntry()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskItem firstClosed = Assert.IsType<TaskResult.Saved>(service.Close(task.Id, HumanActor)).Task;
        TaskItem reopened = Assert.IsType<TaskResult.Saved>(service.Reopen(firstClosed.Id, HumanActor)).Task;
        TaskItem secondClosed = Assert.IsType<TaskResult.Saved>(service.Close(reopened.Id, HumanActor)).Task;

        Assert.NotNull(secondClosed.ClosedAt);
        ChangeLogEntry lastClosedEntry = secondClosed.ChangeLog.Last(entry => entry.Summary == "closed");
        Assert.Equal(lastClosedEntry.At, secondClosed.ClosedAt);
    }

    /// <summary>Spec §6.3: a Project named <c>memory</c> in any case is the Team's shared Memory, so Create refuses it with exactly one problem and writes nothing.</summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("Memory")]
    [InlineData("MEMORY")]
    public void Create_ProjectMemory_RefusedWithMemoryReservedProblem(string project)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", project), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["\"memory\" is reserved for the Team's shared Memory."], refused.Problems);
        Assert.False(Directory.Exists(Path.Combine(TestTaskStore.Root(dir), "Platform", project)));
        Assert.Empty(store.All);
    }

    /// <summary>A Team that is not known AND a Project named <c>memory</c> give two problems, one each, the memory text exactly once.</summary>
    [Fact]
    public void Create_UnknownTeamAndProjectMemory_OneProblemEach()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Ghost", "memory"), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(
            ["Unknown team 'Ghost'. Known teams: Platform.", "\"memory\" is reserved for the Team's shared Memory."],
            refused.Problems);
    }

    /// <summary>Guard (passes today): names that merely start with or resemble <c>memory</c> are legal Projects and Create still saves them.</summary>
    [Theory]
    [InlineData("memory-notes")]
    [InlineData("memories")]
    public void Create_ProjectNearMemory_Accepted(string project)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", project), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(project, saved.Task.Location.Project);
    }

    /// <summary>Guard (passes today, corrections-D3 item 11): a <c>.x</c> Project is NOT newly refused by TaskService.</summary>
    [Fact]
    public void Create_ProjectDotX_StillAccepted()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", ".x"), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(".x", saved.Task.Location.Project);
    }

    /// <summary>Guard (passes today): an underscore-prefixed Project keeps its own refusal text, unchanged by the memory rule.</summary>
    [Fact]
    public void Create_ProjectUnderscore_RefusedWithUnderscoreText()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", "_x"), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["The Project name '_x' cannot be a folder name on this computer (it starts with '_')."], refused.Problems);
    }

    /// <summary>Spec §6.3 on the Update entry point: moving a Task into Project <c>memory</c> (any case) is refused with exactly one problem (the Team is known), and the Task stays where it was.</summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("Memory")]
    public void Update_ProjectMemory_RefusedWithMemoryReservedProblem(string project)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", "Auth v2"));
        byte[] before = File.ReadAllBytes(task.Path);

        TaskResult result = service.Update(task.Id, new TaskPatch { Project = Optional<string?>.Set(project) }, null, HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["\"memory\" is reserved for the Team's shared Memory."], refused.Problems);
        Assert.Equal(before, File.ReadAllBytes(task.Path));
        Assert.False(Directory.Exists(Path.Combine(TestTaskStore.Root(dir), "Platform", project)));
    }

    /// <summary>Guard (passes today): Update into Project <c>memory-notes</c> still moves the Task.</summary>
    [Fact]
    public void Update_ProjectMemoryNotes_Accepted()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);
        TaskItem task = SeedTask(service, new TaskDraft("T", "Platform", null));

        TaskResult result = service.Update(task.Id, new TaskPatch { Project = Optional<string?>.Set("memory-notes") }, null, HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("memory-notes", saved.Task.Location.Project);
    }

    /// <summary>Creates a Task through <see cref="TaskService.Create"/> for a test to update, asserting it saved.</summary>
    private static TaskItem SeedTask(TaskService service, TaskDraft draft) =>
        Assert.IsType<TaskResult.Saved>(service.Create(draft, HumanActor)).Task;

    /// <summary>Constructs a real <see cref="PersonaStore"/> with Nova (alias "nova") and Kai (alias "kai") in Team Platform, over the same <see cref="TempDataDir"/> the Task store under test also reads from.</summary>
    private static PersonaStore CreatePersonaStore(TempDataDir dir)
    {
        PersonaStore personas = new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        _ = personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        return personas;
    }

    /// <summary>Constructs the <see cref="TaskStore"/> under test with default options and a real <see cref="TimeProvider"/>.</summary>
    private static TaskStore CreateTaskStore(TempDataDir dir, PersonaStore personas) =>
        new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

    /// <summary>Constructs the <see cref="TaskService"/> under test, with a fresh <see cref="TaskEvents"/> hub unless <paramref name="events"/> is supplied.</summary>
    private static TaskService CreateTaskService(TempDataDir dir, TaskStore store, PersonaStore personas, TaskEvents? events = null) =>
        new(store, new TaskIdAllocator(dir.Options()), events ?? new TaskEvents(), personas, dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
}
