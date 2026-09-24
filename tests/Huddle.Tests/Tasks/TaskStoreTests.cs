using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskStore"/>'s scan, rejected files, Teams and orphan tracking (Spec §8.1-§8.2).</summary>
public sealed class TaskStoreTests
{
    /// <summary>Every valid Task file found under the scan root is indexed and reachable by its id.</summary>
    [Fact]
    public void Constructor_ValidFiles_AllIndexedById()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        WriteTask(root, Path.Combine("Platform", "Auth", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("Platform", "Auth", false)));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        Assert.Equal(2, store.All.Count);
        _ = TaskId.TryParse("PLAT-0001", out TaskId first);
        _ = TaskId.TryParse("PLAT-0002", out TaskId second);
        Assert.NotNull(store.Get(first));
        Assert.NotNull(store.Get(second));
    }

    /// <summary>A file directly at the Tasks root, not inside any Team folder, is rejected.</summary>
    [Fact]
    public void Constructor_FileAtRoot_Rejected()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = WriteRawFile(root, "x.md", TaskFileFormat.Compose(TestTasks.Make()));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        RejectedTaskFile rejected = Assert.Single(store.RejectedFiles);
        Assert.Equal(path, rejected.Path);
        Assert.Contains("not inside a Team folder", rejected.Reason, StringComparison.Ordinal);
    }

    /// <summary>Two files that parse to the same id are both rejected, each naming the other's path.</summary>
    [Fact]
    public void Constructor_DuplicateIds_BothRejectedNamingEachOther()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string firstPath = WriteTask(root, Path.Combine("Platform", "one.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        string secondPath = WriteTask(root, Path.Combine("Platform", "Auth", "two.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", "Auth", false)));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Equal(2, store.RejectedFiles.Count);
        RejectedTaskFile firstRejection = Assert.Single(store.RejectedFiles, file => string.Equals(file.Path, firstPath, StringComparison.Ordinal));
        RejectedTaskFile secondRejection = Assert.Single(store.RejectedFiles, file => string.Equals(file.Path, secondPath, StringComparison.Ordinal));
        Assert.Contains("duplicate id PLAT-0001", firstRejection.Reason, StringComparison.Ordinal);
        Assert.Contains(secondPath, firstRejection.Reason, StringComparison.Ordinal);
        Assert.Contains("duplicate id PLAT-0001", secondRejection.Reason, StringComparison.Ordinal);
        Assert.Contains(firstPath, secondRejection.Reason, StringComparison.Ordinal);
    }

    /// <summary>A file under a reserved underscore folder is ignored: it is neither indexed nor rejected.</summary>
    [Fact]
    public void Constructor_UnderscoreFolder_Ignored()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        WriteTask(root, Path.Combine("Platform", "_drafts", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>A file that parses to a valid location but fails to parse as a Task is rejected with its parse error.</summary>
    [Fact]
    public void Constructor_InvalidFile_RejectedWithParseError()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        string path = WriteRawFile(root, Path.Combine("Platform", "bad.md"), "---\n---\n");
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

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
        string root = Path.Combine(dir.Path, "Tasks");
        WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        WriteTask(root, Path.Combine("Platform", "Auth", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("Platform", "Auth", false)));
        Directory.CreateDirectory(Path.Combine(root, "Platform", "_closed"));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        TeamFolder platform = Assert.Single(store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal));
        Assert.Single(platform.Projects, project => string.Equals(project, "Auth", StringComparison.Ordinal));
        Assert.DoesNotContain(TaskLayout.ClosedFolder, platform.Projects);
    }

    /// <summary>A Team folder with no Persona whose Teams field names it is an orphan.</summary>
    [Fact]
    public void Teams_FolderWithNoMatchingLabel_IsOrphan()
    {
        using TempDataDir dir = new();
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        Directory.CreateDirectory(Path.Combine(root, "Legal"));
        using PersonaStore personas = CreatePersonaStore(dir);
        personas.Add(Identity("Nova", ["Platform"]), "You work on Platform.");

        using TaskStore store = CreateTaskStore(dir, personas);

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
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        using PersonaStore personas = CreatePersonaStore(dir);
        personas.Add(Identity("Nova"), "You work on Platform.");
        using TaskStore store = CreateTaskStore(dir, personas);
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
                options.Value.Tasks.Dir = options.Value.Acp.TeamsDir;
                break;
            case "tasksInsideTeams":
                options.Value.Tasks.Dir = Path.Combine(options.Value.Acp.TeamsDir, "Sub");
                break;
            case "teamsInsideTasks":
                options.Value.Acp.TeamsDir = Path.Combine(options.Value.Tasks.Dir, "Sub");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "unknown scenario");
        }

        using PersonaStore personas = CreatePersonaStore(dir);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new TaskStore(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance));

        Assert.Contains("Tasks", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Teams", exception.Message, StringComparison.Ordinal);
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
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform"));
        if (Directory.Exists(Path.Combine(root, "platform")))
        {
            Assert.Skip("case-insensitive file system");
            return;
        }

        Directory.CreateDirectory(Path.Combine(root, "platform"));
        WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        string secondPath = WriteTask(root, Path.Combine("platform", "PLAT-0002.md"), TestTasks.Make(id: "PLAT-0002", location: new("platform", null, false)));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        TeamFolder folded = Assert.Single(store.Teams);
        Assert.Equal("Platform", folded.Name);
        _ = TaskId.TryParse("PLAT-0001", out TaskId keptId);
        Assert.NotNull(store.Get(keptId));
        RejectedTaskFile rejected = Assert.Single(store.RejectedFiles);
        Assert.Equal(secondPath, rejected.Path);
        Assert.Contains("Platform", rejected.Reason, StringComparison.Ordinal);
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
        string root = Path.Combine(dir.Path, "Tasks");
        Directory.CreateDirectory(Path.Combine(root, "Platform", "Auth"));
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

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
        string root = Path.Combine(dir.Path, "Tasks");
        string path = WriteTask(root, Path.Combine("Platform", "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)));
        using PersonaStore personas = CreatePersonaStore(dir);

        using FileStream lockHandle = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Exception? exception = Record.Exception(() =>
        {
            using TaskStore store = CreateTaskStore(dir, personas);
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
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
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
        string root = Path.Combine(dir.Path, "Tasks");
        const string garbage = "this is not a valid task file at all";
        WriteRawFile(root, Path.Combine("_archive", "x.md"), garbage);
        WriteRawFile(root, Path.Combine("T", "_drafts", "x.md"), garbage);
        WriteRawFile(root, Path.Combine("T", "P", "_notes", "x.md"), garbage);
        using PersonaStore personas = CreatePersonaStore(dir);

        using TaskStore store = CreateTaskStore(dir, personas);

        Assert.Empty(store.All);
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>Writes a valid Task file's composed text under <paramref name="root"/>, and backdates it to its last Change log entry's time (or a fixed past time with none) so a later reconciliation pass never rewrites a fixture.</summary>
    private static string WriteTask(string root, string relativePath, TaskItem task)
    {
        string path = Path.Combine(root, relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, TaskFileFormat.Compose(task));
        DateTimeOffset lastWrite = task.ChangeLog.Count > 0 ? task.ChangeLog[^1].At : new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
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

    /// <summary>Constructs a real <see cref="PersonaStore"/> over the same <see cref="TempDataDir"/> a <see cref="TaskStore"/> under test also reads from.</summary>
    private static PersonaStore CreatePersonaStore(TempDataDir dir) =>
        new(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);

    /// <summary>Constructs the <see cref="TaskStore"/> under test with default options and a real <see cref="TimeProvider"/>.</summary>
    private static TaskStore CreateTaskStore(TempDataDir dir, PersonaStore personas) =>
        new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

    /// <summary>A minimal <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias defaulting to <paramref name="name"/>.</summary>
    private static PersonaIdentity Identity(string name, IReadOnlyList<string>? teams = null) =>
        new(name, name, name, teams ?? []);

    /// <summary>Hand-written Persona frontmatter naming a single Team, for <see cref="PersonaStore.Update"/> calls that need to add a Teams field.</summary>
    private static string PersonaTextWithTeams(string name, string body, string team) =>
        $"---\nName: {name}\nTitle: {name}\nAlias: {name}\nteams: [{team}]\n---\n{body}";
}
