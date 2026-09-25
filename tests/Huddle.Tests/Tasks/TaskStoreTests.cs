using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskStore"/>'s scan, rejected files, Teams and orphan tracking (Spec
/// §8.1-§8.2), and its writing, moving and version-history behaviour (Spec §8.3, §9.3; Task 5.3).
/// </summary>
public sealed class TaskStoreTests
{
    /// <summary>Every valid Task file found under the scan root is indexed and reachable by its id.</summary>
    [Fact]
    public void Constructor_ValidFiles_AllIndexedById()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "Auth", "_tasks", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("Platform", "Auth", false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Equal(2, store.All.Count);
        _ = TaskId.TryParse("PLAT-0001", out TaskId first);
        _ = TaskId.TryParse("PLAT-0002", out TaskId second);
        Assert.NotNull(store.Get(first));
        Assert.NotNull(store.Get(second));
    }

    /// <summary>A file directly at the Tasks root, not inside any Team's <c>_tasks/</c> folder, is
    /// ignored: it is neither indexed nor rejected (Spec §6.3, Task G1.1.i).</summary>
    [Fact]
    public void Constructor_FileAtRoot_Ignored()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        _ = WriteRawFile(root, "x.md", TaskFileFormat.Compose(TestTasks.Make()));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>Two files that parse to the same id are both rejected, each naming the other's path.</summary>
    [Fact]
    public void Constructor_DuplicateIds_BothRejectedNamingEachOther()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string firstPath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "one.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        string secondPath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "Auth", "_tasks", "two.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", "Auth", false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Equal(2, store.RejectedFiles.Count);
        RejectedTaskFile firstRejection = Assert.Single(store.RejectedFiles, file => string.Equals(file.Path, firstPath, StringComparison.Ordinal));
        RejectedTaskFile secondRejection = Assert.Single(store.RejectedFiles, file => string.Equals(file.Path, secondPath, StringComparison.Ordinal));
        Assert.Equal($"duplicate id PLAT-0001, also in {secondPath}", firstRejection.Reason);
        Assert.Equal($"duplicate id PLAT-0001, also in {firstPath}", secondRejection.Reason);
    }

    /// <summary>A file under a reserved underscore folder is ignored: it is neither indexed nor rejected.</summary>
    [Fact]
    public void Constructor_UnderscoreFolder_Ignored()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_drafts", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>A file that parses to a valid location but fails to parse as a Task is rejected with its parse error.</summary>
    [Fact]
    public void Constructor_InvalidFile_RejectedWithParseError()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = WriteRawFile(root, Path.Combine("Platform", "_tasks", "bad.md"), "---\n---\n");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        RejectedTaskFile rejected = Assert.Single(store.RejectedFiles);
        Assert.Equal(path, rejected.Path);
        Assert.Equal("missing required key: id", rejected.Reason);
    }

    /// <summary>Teams lists every Team folder together with its Project sub-folders, excluding _closed.</summary>
    [Fact]
    public void Teams_ListsFoldersAndProjects_ExcludingClosed()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "Auth", "_tasks", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("Platform", "Auth", false)));
        Directory.CreateDirectory(Path.Combine(root, "Platform", "_closed"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        TeamFolder platform = Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal));
        Assert.Single(platform.Projects, project => string.Equals(project, "Auth", StringComparison.Ordinal));
        Assert.DoesNotContain(TaskLayout.ClosedFolder, platform.Projects);
    }

    /// <summary>A Team folder with no Persona whose Teams field names it is an orphan.</summary>
    [Fact]
    public void Teams_FolderWithNoMatchingLabel_IsOrphan()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        Directory.CreateDirectory(Path.Combine(root, "Legal"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        personas.Add(Identity("Nova", ["Platform"]), "You work on Platform.");

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        TeamFolder platform = Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal));
        TeamFolder legal = Assert.Single(store.Teams, team => string.Equals(team.Name, "Legal", StringComparison.Ordinal));
        Assert.False(platform.IsOrphan);
        Assert.True(legal.IsOrphan);
    }

    /// <summary>When a Persona is edited to name a Team, that Team's orphan flag clears once PersonasChanged fires.</summary>
    [Fact]
    public async Task Teams_PersonaGainsLabel_OrphanClears()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        personas.Add(Identity("Nova"), "You work on Platform.");
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        Assert.True(Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal)).IsOrphan);

        TaskCompletionSource indexChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.IndexChanged += () => indexChanged.TrySetResult();
        personas.Update("Nova", PersonaTextWithTeams("Nova", "You work on Platform.", "Platform"), model: null, effort: null);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => indexChanged.TrySetCanceled());
        await indexChanged.Task;

        Assert.False(Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal)).IsOrphan);
    }

    /// <summary>
    /// <see cref="PersonaStore.Update"/> raises <see cref="PersonaStore.PersonasChanged"/> twice for
    /// one save - once synchronously, once again about 500&#160;ms later once its own debounced watcher
    /// settles on the write it already knew about (<c>PersonaStore.RaiseRenames</c>'s remarks: "naturally
    /// idempotent"). When neither call actually flips a Team's orphan flag (here, Nova already claimed
    /// "Platform" before and after the edit), <see cref="TaskStore.IndexChanged"/> must not fire at all:
    /// before this fix, <c>TaskStore.OnPersonasChanged</c> republished unconditionally on every
    /// <see cref="PersonaStore.PersonasChanged"/>, so a UI component such as <c>TaskDetail</c>, subscribed
    /// to reloads, saw a needless reload and re-render about half a second after every Persona save,
    /// whether or not anything a Task-facing view cares about actually changed. Waits 900&#160;ms, a full
    /// debounce window and then some, before asserting the count.
    /// </summary>
    [Fact]
    public async Task Teams_PersonaEditedWithNoOrphanChange_IndexChangedNeverRaised()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        _ = personas.Add(Identity("Nova", ["Platform"]), "You work on Platform.");
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        Assert.False(Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal)).IsOrphan);

        int indexChangedCount = 0;
        store.IndexChanged += () => indexChangedCount++;
        personas.Update("Nova", PersonaTextWithTeams("Nova", "You still work on Platform.", "Platform"), model: null, effort: null);

        await Task.Delay(TimeSpan.FromMilliseconds(900), ct);

        Assert.Equal(0, indexChangedCount);
    }

    /// <summary>
    /// Configuring the Tasks folder to resolve equal to, inside, or as a parent of the Teams folder
    /// is rejected at startup either way (Settled corrections-B2 D5 item 12: "throw on equality and
    /// on either containing the other").
    /// </summary>
    /// <param name="scenario">Which of the three relationships to configure.</param>
    [Theory]
    [InlineData("equal")]
    [InlineData("tasksInsideTeams")]
    [InlineData("teamsInsideTasks")]
    public void Constructor_TasksDirInsideTeamsDir_Throws(string scenario)
    {
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        switch (scenario)
        {
            case "equal":
                options.Value.Tasks.Dir = options.Value.Acp.TeammatesDir;
                break;
            case "tasksInsideTeams":
                options.Value.Tasks.Dir = Path.Combine(options.Value.Acp.TeammatesDir, "Sub");
                break;
            case "teamsInsideTasks":
                options.Value.Acp.TeammatesDir = Path.Combine(options.Value.Tasks.Dir, "Sub");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "unknown scenario");
        }

        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        string expectedTasksRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Tasks.Dir));
        string expectedTeamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Acp.TeammatesDir));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new TaskStore(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance));

        Assert.Equal(
            $"Team:Tasks:Dir ('{expectedTasksRoot}') must not equal or nest with Team:Acp:TeammatesDir ('{expectedTeamsRoot}').",
            exception.Message);
    }

    /// <summary>
    /// Two Team folders differing only by case fold into one <see cref="TeamFolder"/> (the first by
    /// Ordinal wins - "Platform" sorts before "platform"); the losing folder's file is rejected,
    /// naming the folder it duplicates (Settled corrections-B2 D5 item 16). Skipped on a
    /// case-insensitive file system (Windows/macOS), where the second <c>Directory.CreateDirectory</c>
    /// call is a no-op on the same directory the first created.
    /// </summary>
    [Fact]
    public void Teams_CaseOnlyDuplicateTeamFolders_FoldedAndSecondRejected()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        if (Directory.Exists(Path.Combine(root, "platform")))
        {
            Assert.Skip("case-insensitive file system");
            return;
        }

        Directory.CreateDirectory(Path.Combine(root, "platform"));
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        string secondPath = TestTaskStore.WriteTask(root, Path.Combine("platform", "_tasks", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        TeamFolder folded = Assert.Single(store.Teams);
        Assert.Equal("Platform", folded.Name);
        _ = TaskId.TryParse("PLAT-0001", out TaskId keptId);
        Assert.NotNull(store.Get(keptId));
        RejectedTaskFile rejected = Assert.Single(store.RejectedFiles);
        Assert.Equal(secondPath, rejected.Path);
        Assert.Equal("Team folder 'platform' duplicates 'Platform' (case-insensitive); its files are rejected.", rejected.Reason);
    }

    /// <summary>
    /// A Team folder and a Project folder that hold no files yet still appear in <see cref="TaskStore.Teams"/>,
    /// because it enumerates with <see cref="Directory.GetDirectories(string)"/> rather than deriving
    /// folders from the scanned files - <c>Directory.GetFiles</c> alone could never see an empty
    /// folder (Settled corrections-B2 D5 item 14).
    /// </summary>
    [Fact]
    public void Teams_EmptyTeamAndProjectFolders_Listed()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform", "Auth"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        TeamFolder platform = Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal));
        Assert.Single(platform.Projects, project => string.Equals(project, "Auth", StringComparison.Ordinal));
        Assert.Empty(store.All);
    }

    /// <summary>
    /// A file locked by another handle during the scan is caught by the §8.1
    /// <c>try/catch (IOException)</c> and rejected rather than failing the whole scan or throwing out
    /// of the constructor. Windows-only: mandatory file locking via <see cref="FileShare.None"/> is
    /// not enforced on Linux, so there is nothing to lock against there.
    /// </summary>
    [Fact]
    public void Constructor_UnreadableFile_RejectedNotThrown()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux does not mandatory-lock files opened with FileShare.None");
            return;
        }

        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using FileStream lockHandle = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Exception? exception = Record.Exception(() =>
        {
            using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
            Assert.Empty(store.All);
            Assert.Single(store.RejectedFiles);
        });

        Assert.Null(exception);
    }

    /// <summary>Looking up an id no file carries returns null rather than throwing.</summary>
    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-9999", out TaskId unknownId);

        TaskItem? found = store.Get(unknownId);

        Assert.Null(found);
    }

    /// <summary>
    /// An unparsable file under a reserved underscore folder at any depth - at the Tasks root, under
    /// a Team, or under a Project - is never even read, so it appears in neither <see cref="TaskStore.All"/>
    /// nor <see cref="TaskStore.RejectedFiles"/>. Proves <see cref="TaskLayout.TryMap"/> runs before
    /// any <c>File.ReadAllText</c> call, per the Settled corrections-B2 note.
    /// </summary>
    [Fact]
    public void Constructor_UnderscoreFolderAtAnyDepth_NotRead()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        const string garbage = "this is not a valid task file at all";
        WriteRawFile(root, Path.Combine("_archive", "x.md"), garbage);
        WriteRawFile(root, Path.Combine("T", "_drafts", "x.md"), garbage);
        WriteRawFile(root, Path.Combine("T", "P", "_notes", "x.md"), garbage);
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>A successful <see cref="TaskStore.Write"/> leaves no ".tmp" file behind at the target path (Spec §8.3's atomic-write precedent).</summary>
    [Fact]
    public void Write_LeavesNoTmpFile()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string text = TaskFileFormat.Compose(original with { Title = "Updated title" });

        TaskItem? result = store.Write(original, original.Version, text);

        Assert.NotNull(result);
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>A successful Write republishes the index with the new content and raises <see cref="TaskStore.IndexChanged"/> exactly once.</summary>
    [Fact]
    public void Write_UpdatesIndexAndRaisesIndexChangedOnce()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string text = TaskFileFormat.Compose(original with { Title = "Updated title" });
        int raisedCount = 0;
        store.IndexChanged += () => raisedCount++;

        TaskItem? result = store.Write(original, original.Version, text);

        Assert.NotNull(result);
        Assert.Equal(1, raisedCount);
        TaskItem? indexed = store.Get(id);
        Assert.Equal("Updated title", indexed?.Title);
    }

    /// <summary>Moving a Task to its Team's "_closed" folder moves the file on disk and the index reflects the new location (Spec §8.3).</summary>
    [Fact]
    public void Move_ToClosed_FileMovesAndIndexUpdates()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        TaskLocation closedLocation = original.Location with { Closed = true };
        string text = TaskFileFormat.Compose(original with { Location = closedLocation });

        TaskItem? result = store.Move(original, original.Version, closedLocation, text);

        Assert.NotNull(result);
        Assert.False(File.Exists(sourcePath));
        string targetPath = TaskLayout.PathFor(root, closedLocation, id);
        Assert.True(File.Exists(targetPath));
        TaskItem? indexed = store.Get(id);
        Assert.True(indexed?.Location.Closed);
    }

    /// <summary>A Move refuses to overwrite an existing file at the target path, throwing an <see cref="IOException"/> that names it (Spec §8.3 step 4), and leaves the source untouched.</summary>
    [Fact]
    public void Move_TargetExists_Throws()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        WriteRawFile(root, Path.Combine("Platform", "_tasks", TaskLayout.ClosedFolder, "PLAT-0001.md"), "conflicting content");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        TaskLocation closedLocation = original.Location with { Closed = true };
        string text = TaskFileFormat.Compose(original with { Location = closedLocation });

        IOException exception = Assert.Throws<IOException>(() => store.Move(original, original.Version, closedLocation, text));

        string targetDir = Path.Combine(root, "Platform", "_tasks", TaskLayout.ClosedFolder);
        Assert.Equal($"A file named PLAT-0001.md already exists in {targetDir}.", exception.Message);
        Assert.True(File.Exists(sourcePath));
    }

    /// <summary>A Project folder that becomes empty after a Move is left in place; deleting folders is the Human's job (Spec §8.3).</summary>
    [Fact]
    public void Move_EmptiedProjectFolder_IsLeftInPlace()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "Auth", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", "Auth", false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        TaskLocation newLocation = new("Platform", null, false);
        string text = TaskFileFormat.Compose(original with { Location = newLocation });

        TaskItem? result = store.Move(original, original.Version, newLocation, text);

        Assert.NotNull(result);
        Assert.False(File.Exists(sourcePath));
        Assert.True(Directory.Exists(Path.Combine(root, "Platform", "Auth")));
    }

    /// <summary>
    /// A Team change that only differs in case from the Task's current, existing Team folder is not a
    /// move at all once the target is resolved against that folder's on-disk casing (ADR-0025: a Team
    /// folder is matched case-insensitively, "because Windows paths do" - a rule that must hold on
    /// every OS, not only the ones that fold directory case for free). The file stays at its original,
    /// canonical-case path; no sibling folder differing only by case is created alongside it. This is
    /// the regression test for the bug where <see cref="TaskStore.Move"/> built its target path from
    /// the requested casing directly: on a case-insensitive file system (Windows, macOS) that path
    /// coincided with the source and merely looked correct, while on a case-sensitive one (Linux) it
    /// silently created a second "platform" folder next to "Platform" and moved the file into it.
    /// </summary>
    [Fact]
    public void Move_CaseOnlyTeamChange_KeepsTheFile()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        TaskLocation lowerCaseLocation = new("platform", null, false);
        string text = TaskFileFormat.Compose(original with { Location = lowerCaseLocation, Title = "renamed casing" });

        TaskItem? result = store.Move(original, original.Version, lowerCaseLocation, text);

        Assert.NotNull(result);
        Assert.True(File.Exists(sourcePath));
        string[] filesUnderRoot = Directory.GetFiles(root, "*.md", SearchOption.AllDirectories);
        TaskItem? indexed = store.Get(id);
        Assert.Equal("renamed casing", indexed?.Title);
        Assert.Equal(sourcePath, indexed?.Path);
        Assert.Single(filesUnderRoot);
        Assert.False(File.Exists(sourcePath + ".tmp-move"));

        string[] teamDirs = Directory.GetDirectories(root);
        string teamDirName = Assert.Single(teamDirs, d => string.Equals(Path.GetFileName(d), "Platform", StringComparison.Ordinal));
        Assert.DoesNotContain(teamDirs, d => string.Equals(Path.GetFileName(d), "platform", StringComparison.Ordinal) && !string.Equals(d, teamDirName, StringComparison.Ordinal));
    }

    /// <summary>
    /// The same case-only-Team-change guarantee as <see cref="Move_CaseOnlyTeamChange_KeepsTheFile"/>,
    /// but for a Project folder nested under an already-canonical Team (ADR-0025 §8.2 applies the same
    /// case-insensitive matching to Project folders as to Team folders).
    /// </summary>
    [Fact]
    public void Move_CaseOnlyProjectChange_KeepsTheFile()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string sourcePath = TestTaskStore.WriteTask(root, Path.Combine("Platform", "Auth", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", "Auth", false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        TaskLocation lowerCaseLocation = new("Platform", "auth", false);
        string text = TaskFileFormat.Compose(original with { Location = lowerCaseLocation, Title = "renamed casing" });

        TaskItem? result = store.Move(original, original.Version, lowerCaseLocation, text);

        Assert.NotNull(result);
        Assert.True(File.Exists(sourcePath));
        TaskItem? indexed = store.Get(id);
        Assert.Equal(sourcePath, indexed?.Path);

        string[] projectDirs = Directory.GetDirectories(Path.Combine(root, "Platform"));
        string projectDirName = Assert.Single(projectDirs, d => string.Equals(Path.GetFileName(d), "Auth", StringComparison.Ordinal));
        Assert.DoesNotContain(projectDirs, d => string.Equals(Path.GetFileName(d), "auth", StringComparison.Ordinal) && !string.Equals(d, projectDirName, StringComparison.Ordinal));
    }

    /// <summary>
    /// <see cref="TaskStore.Create"/> resolves a differently-cased Team straight to the existing
    /// folder's on-disk casing itself (ADR-0025), the store's own guarantee independent of
    /// <c>TaskService.CreateCore</c>'s own canonicalisation before it calls in - this is the direct
    /// regression test for that layer, so the invariant holds even for a caller that skips it.
    /// </summary>
    [Fact]
    public void Create_ExistingFolderDifferentCase_ReusesExistingFolder()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem candidate = TestTasks.Make(id: "PLAT-0001", location: new("platform", null, false), path: "");
        string text = TaskFileFormat.Compose(candidate);

        TaskItem? written = store.Create(candidate, text);

        Assert.NotNull(written);
        Assert.Equal("Platform", written.Location.Team);
        Assert.Equal(Path.Combine(root, "Platform", "_tasks", "PLAT-0001.md"), written.Path);
        string[] teamDirs = Directory.GetDirectories(root);
        string teamDirName = Assert.Single(teamDirs, d => string.Equals(Path.GetFileName(d), "Platform", StringComparison.Ordinal));
        Assert.DoesNotContain(teamDirs, d => string.Equals(Path.GetFileName(d), "platform", StringComparison.Ordinal) && !string.Equals(d, teamDirName, StringComparison.Ordinal));
    }

    /// <summary>
    /// <see cref="TaskStore.GetVersion"/> finds a Task as any of its last 20 recorded versions, but
    /// not an older one that has since been evicted (Spec §9.3 step 1). The initial scan records one
    /// version, then 20 Writes push it out.
    /// </summary>
    [Fact]
    public void VersionHistory_KeepsLast20()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", title: "v0", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem current = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        List<string> versions = [current.Version];
        for (int i = 1; i <= 20; i++)
        {
            string title = string.Create(CultureInfo.InvariantCulture, $"v{i}");
            string text = TaskFileFormat.Compose(current with { Title = title });
            current = store.Write(current, current.Version, text) ?? throw new InvalidOperationException("write unexpectedly conflicted");
            versions.Add(current.Version);
        }

        TaskItem? evicted = store.GetVersion(id, versions[0]);
        TaskItem? kept = store.GetVersion(id, versions[1]);

        Assert.Null(evicted);
        Assert.NotNull(kept);
        Assert.Equal("v1", kept.Title);
    }

    /// <summary>
    /// A Task the constructor's initial scan loaded, and that has never been written since, already
    /// has its version in <see cref="TaskStore.GetVersion"/>'s history - the history is seeded at
    /// scan time, not only on the first Write (Settled corrections-B2 D5 item 8; the base-version
    /// merge path in Spec §9.3 step 1 depends on this for a Task nobody has edited yet).
    /// </summary>
    [Fact]
    public void GetVersion_AfterInitialScan_KnowsTheScannedVersion()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", title: "scanned", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem scanned = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");

        TaskItem? found = store.GetVersion(id, scanned.Version);

        Assert.NotNull(found);
        Assert.Equal("scanned", found.Title);
    }

    /// <summary>A Write whose expected version no longer matches the file on disk - something else wrote it first - returns null rather than overwriting the newer content (Settled corrections-B2 D5 item 6).</summary>
    [Fact]
    public void Write_DiskChangedSinceSeen_ReturnsConflict()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", title: "original", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem seen = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string outsideText = TaskFileFormat.Compose(seen with { Title = "changed outside" });
        File.WriteAllText(path, outsideText);
        string attemptedText = TaskFileFormat.Compose(seen with { Title = "attempted write" });

        TaskItem? result = store.Write(seen, seen.Version, attemptedText);

        Assert.Null(result);
        Assert.Equal(outsideText, File.ReadAllText(path));
    }

    /// <summary>A Write whose composed text does not parse throws before anything reaches disk, so a bad compose can never corrupt a Task file (Settled corrections-B2 D5 item 9).</summary>
    [Fact]
    public void Write_ComposedTextDoesNotParse_ThrowsInvalidOperationException()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem original = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string originalDiskText = File.ReadAllText(path);
        const string invalidText = "not a valid task file at all";

        Assert.Throws<InvalidOperationException>(() => store.Write(original, original.Version, invalidText));

        Assert.Equal(originalDiskText, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary><see cref="TaskStore.AppendEntry"/> returns null, and leaves the file untouched, when the disk no longer matches the expected version (Settled corrections-B2 D5 item 6).</summary>
    [Fact]
    public void AppendEntry_VersionMismatch_ReturnsNull()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        TaskItem seen = store.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        string outsideText = TaskFileFormat.Compose(seen with { Title = "changed outside" });
        File.WriteAllText(path, outsideText);
        ChangeLogEntry entry = TestTasks.Entry("2020-01-01T00:00:00Z", "Human", "attempted");

        TaskItem? result = store.AppendEntry(id, seen.Version, entry);

        Assert.Null(result);
        Assert.Equal(outsideText, File.ReadAllText(path));
    }

    /// <summary><see cref="TaskStore.ReadText"/> returns the Task file's current text straight from disk.</summary>
    [Fact]
    public void ReadText_ExistingTask_ReturnsCurrentDiskText()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        string path = TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", title: "read me", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);

        string? text = store.ReadText(id);

        Assert.Equal(File.ReadAllText(path), text);
    }

    /// <summary><see cref="TaskStore.ReadText"/> returns null, rather than throwing, for an id no file carries.</summary>
    [Fact]
    public void ReadText_UnknownId_ReturnsNull()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-9999", out TaskId unknownId);

        string? text = store.ReadText(unknownId);

        Assert.Null(text);
    }

    /// <summary>
    /// <see cref="TaskStore.HighestNumber"/> is the max Task number for a prefix over both every
    /// parsed id and every file name that parses as a <see cref="TaskId"/>, including a rejected
    /// file's (B1 corrections-B2 D6 item 6).
    /// </summary>
    [Fact]
    public void HighestNumber_ParsedIdsAndFileNames_ReturnsMax()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "a.md"), TestTasks.Make(id: "PLAT-0003", location: new("Platform", null, false)));
        WriteRawFile(root, Path.Combine("Platform", "_tasks", "PLAT-0007.md"), "not a valid task file");
        TestTaskStore.WriteTask(root, Path.Combine("Ops", "_tasks", "OPS-0099.md"), TestTasks.Make(id: "OPS-0099", location: new("Ops", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        int highest = store.HighestNumber("PLAT");

        Assert.Equal(7, highest);
    }

    /// <summary>
    /// A batch of three Writes through <see cref="TaskStore.WriteMany"/> updates every Task's index
    /// entry but raises <see cref="TaskStore.IndexChanged"/> exactly once, not three times (Settled
    /// corrections-B2 D5 item 7 - the Teammate-rename cascade in Task 6.6 needs this to avoid firing
    /// once per renamed file).
    /// </summary>
    [Fact]
    public void WriteMany_ThreeTasks_RaisesIndexChangedOnce()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("Platform", null, false)));
        TestTaskStore.WriteTask(root, Path.Combine("Platform", "_tasks", "PLAT-0003.md"), TestTasks.Make(id: "PLAT-0003", location: new("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        _ = TaskId.TryParse("PLAT-0001", out TaskId first);
        _ = TaskId.TryParse("PLAT-0002", out TaskId second);
        _ = TaskId.TryParse("PLAT-0003", out TaskId third);
        TaskItem firstTask = store.Get(first) ?? throw new InvalidOperationException("fixture task missing");
        TaskItem secondTask = store.Get(second) ?? throw new InvalidOperationException("fixture task missing");
        TaskItem thirdTask = store.Get(third) ?? throw new InvalidOperationException("fixture task missing");
        List<(TaskItem Task, string ExpectedVersion, string Text)> writes =
        [
            (firstTask, firstTask.Version, TaskFileFormat.Compose(firstTask with { Title = "renamed 1" })),
            (secondTask, secondTask.Version, TaskFileFormat.Compose(secondTask with { Title = "renamed 2" })),
            (thirdTask, thirdTask.Version, TaskFileFormat.Compose(thirdTask with { Title = "renamed 3" })),
        ];
        int raisedCount = 0;
        store.IndexChanged += () => raisedCount++;

        IReadOnlyList<TaskItem?> results = store.WriteMany(writes);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(1, raisedCount);
        Assert.Equal("renamed 1", store.Get(first)?.Title);
        Assert.Equal("renamed 2", store.Get(second)?.Title);
        Assert.Equal("renamed 3", store.Get(third)?.Title);
    }

    /// <summary>Writes arbitrary raw text - valid or deliberately unparsable - under <paramref name="root"/>, for fixtures that don't need a real Task.</summary>
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

    /// <summary>A minimal <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias defaulting to <paramref name="name"/>.</summary>
    private static PersonaIdentity Identity(string name, IReadOnlyList<string>? teams = null) =>
        new(name, name, name, teams ?? []);

    /// <summary>Hand-written Persona frontmatter naming a single Team, for <see cref="PersonaStore.Update"/> calls that need to add a Teams field.</summary>
    private static string PersonaTextWithTeams(string name, string body, string team) =>
        $"---\nName: {name}\nTitle: {name}\nAlias: {name}\nteams: [{team}]\n---\n{body}";
}
