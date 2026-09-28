using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Proves the ADR-0030 hand-off once Tasks writes under <c>Teams/&lt;Team&gt;/[&lt;Project&gt;/]_tasks/</c>
/// (Spec §6.3, plan :951-954, corrections-B4 item 44): a Task created through the Tasks stack lands
/// where the Library expects it and stays out of the Library's listing, a Library-created Project
/// folder is picked up by <see cref="TaskStore.Teams"/>, and a plain note beside Tasks is never
/// mistaken for a rejected Task file.
/// </summary>
public sealed class TasksLibraryHandOffTests
{
    /// <summary>A Task written through <see cref="TaskStore.Create"/> lands under <c>Teams/Platform/_tasks/</c>
    /// on disk, and <see cref="LibraryFileService.ListAsync"/> of the Team folder hides that <c>_tasks</c>
    /// folder while still listing a sibling <c>notes.md</c> (the allowed neighbour).</summary>
    [Fact]
    public async Task TaskCreated_IsUnderTasksFolder_AndHiddenFromLibrary()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0001", location: new TaskLocation("Platform", null, false)));
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        string noteFullPath = Path.Combine(fixture.DataDir, "Teams", "Platform", "notes.md");
        await File.WriteAllTextAsync(noteFullPath, "hello", ct);
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamFolder = fixture.ResolveTeams("Platform");

        string expectedTaskPath = Path.Combine(fixture.DataDir, "Teams", "Platform", "_tasks", "PLAT-0001.md");
        Assert.True(File.Exists(expectedTaskPath));

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(teamFolder, ct);

        Assert.DoesNotContain(entries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "_tasks", StringComparison.Ordinal));
        // contains-ok: collection-membership predicate over LibraryEntry results, not a markup/string haystack.
        Assert.Contains(entries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "notes.md", StringComparison.Ordinal));
    }

    /// <summary>A Project folder created through <see cref="TeamFolderProvisioner.EnsureProject"/> is picked up
    /// by <see cref="TaskStore"/>'s own watcher (awaited via <see cref="TaskStore.IndexChanged"/>, never a
    /// sleep): <see cref="TaskStore.Teams"/> lists it under its Team.</summary>
    [Fact]
    public async Task LibraryProject_AppearsInTaskStoreTeams()
    {
        using TaskToolHarness harness = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        TeamFolderProvisioner provisioner = new(harness.Personas, fixture.RootStore, fixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance);

        // Creating the Team folder itself raises its own IndexChanged; wait it out first so it can
        // never be mistaken for the signal EnsureProject's own Project folder below is awaited for.
        TaskCompletionSource platformReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action onPlatformCreated = () => platformReady.TrySetResult();
        harness.Store.IndexChanged += onPlatformCreated;
        Directory.CreateDirectory(Path.Combine(harness.Options.Value.DataDir, "Teams", "Platform"));
        await WaitForIndexChangedAsync(platformReady);
        harness.Store.IndexChanged -= onPlatformCreated;

        LibraryPath teamFolder = fixture.ResolveTeams("Platform");
        Assert.DoesNotContain(harness.Store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal) && team.Projects.Contains("Auth v2"));

        TaskCompletionSource projectReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Store.IndexChanged += () => projectReady.TrySetResult();

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder, "Auth v2");
        Assert.NotNull(result.Value);

        await WaitForIndexChangedAsync(projectReady);

        // contains-ok: collection-membership predicate over TaskStore.Teams, not a markup/string haystack.
        Assert.Contains(harness.Store.Teams, team => string.Equals(team.Name, "Platform", StringComparison.Ordinal) && team.Projects.Contains("Auth v2"));
    }

    /// <summary>Awaits <paramref name="signal"/>, cancelling it after 10 seconds so a mutation that
    /// stops the watcher from ever raising <see cref="TaskStore.IndexChanged"/> fails the test instead
    /// of hanging it forever.</summary>
    /// <param name="signal">The completion source a <see cref="TaskStore.IndexChanged"/> handler resolves.</param>
    private static async Task WaitForIndexChangedAsync(TaskCompletionSource signal)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => signal.TrySetCanceled());
        await signal.Task;
    }

    /// <summary>A note already on disk at the Team root (<c>Teams/Platform/notes.md</c>) before the
    /// initial <see cref="TaskStore"/> scan is never a candidate the Tasks scan considers, so it never
    /// appears in <see cref="TaskStore.RejectedFiles"/> (corrections-B4 item 44, green on arrival after
    /// G1.3.i: only <c>_tasks</c>/<c>_tasks/_closed</c> are scanned).</summary>
    [Fact]
    public void LibraryNote_AtTeamRoot_IsNotARejectedTask()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform", "_tasks"));
        File.WriteAllText(Path.Combine(root, "Platform", "notes.md"), "hello");
        TestTaskStore.WriteTask(root, TestTaskStore.RelativePath("Platform", null, false, "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new TaskLocation("Platform", null, false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.DoesNotContain(store.RejectedFiles, rejected => rejected.Path.Contains("notes.md", StringComparison.Ordinal));
    }

    /// <summary>The same rule for a note already on disk inside a Project folder
    /// (<c>Teams/Platform/Auth v2/notes.md</c>) before the initial scan: it is never a candidate the
    /// Tasks scan considers, so it never appears in <see cref="TaskStore.RejectedFiles"/>.</summary>
    [Fact]
    public void LibraryNote_InProjectFolder_IsNotARejectedTask()
    {
        using TempDataDir dir = new();
        string root = TestTaskStore.Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Platform", "Auth v2", "_tasks"));
        File.WriteAllText(Path.Combine(root, "Platform", "Auth v2", "notes.md"), "hello");
        TestTaskStore.WriteTask(root, TestTaskStore.RelativePath("Platform", "Auth v2", false, "PLAT-0001.md"), TestTasks.Make(id: "PLAT-0001", location: new TaskLocation("Platform", "Auth v2", false)));
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);

        Assert.DoesNotContain(store.RejectedFiles, rejected => rejected.Path.Contains("notes.md", StringComparison.Ordinal));
    }

    /// <summary>Plan :951-954's added test: <see cref="LibraryFileService.ListAsync"/> hides an actual
    /// <c>_tasks</c> folder at both Team and Project depth, but shows a folder that merely starts the
    /// same way (<c>x_tasks</c>) - the underscore-prefix rule, not a substring match.</summary>
    [Fact]
    public async Task LibraryList_HidesTasksFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        string teamDir = Path.Combine(harness.Options.Value.DataDir, "Teams", "Platform");
        string projectDir = Path.Combine(teamDir, "Auth v2");
        Directory.CreateDirectory(Path.Combine(teamDir, "_tasks"));
        Directory.CreateDirectory(Path.Combine(teamDir, "x_tasks"));
        Directory.CreateDirectory(Path.Combine(projectDir, "_tasks"));
        Directory.CreateDirectory(Path.Combine(projectDir, "x_tasks"));
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        LibraryFileService service = fixture.CreateService();

        IReadOnlyList<LibraryEntry> teamEntries = await service.ListAsync(fixture.ResolveTeams("Platform"), ct);
        IReadOnlyList<LibraryEntry> projectEntries = await service.ListAsync(fixture.ResolveTeams("Platform/Auth v2"), ct);

        Assert.DoesNotContain(teamEntries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "_tasks", StringComparison.Ordinal));
        // contains-ok: collection-membership predicate over LibraryEntry results, not a markup/string haystack.
        Assert.Contains(teamEntries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "x_tasks", StringComparison.Ordinal));
        Assert.DoesNotContain(projectEntries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "_tasks", StringComparison.Ordinal));
        // contains-ok: collection-membership predicate over LibraryEntry results, not a markup/string haystack.
        Assert.Contains(projectEntries, entry => string.Equals(Path.GetFileName(entry.Path.FullPath), "x_tasks", StringComparison.Ordinal));
    }
}
