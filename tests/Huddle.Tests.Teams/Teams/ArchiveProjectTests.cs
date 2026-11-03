using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Tasks;
using Fixture = Agency.Huddle.Tests.Teams.ArchiveTeamTests.Fixture;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Archiving and unarchiving one Project (Spec 6.9, corrections-12 item 10): the folder moves between
/// <c>Teams/Home/Alpha</c> and <c>Archive/Home/Alpha</c> through the same staged, journalled move as a Team, with no
/// <c>_team.md</c>, labels or Rooms touched; a name clash on either side refuses; a crash converges at the next start.
/// </summary>
public sealed class ArchiveProjectTests
{
    private const string Alpha = "Alpha";

    /// <summary>Archiving a Project moves its folder, bytes intact, under Archive/Home without making Home look archived or touching labels.</summary>
    [Fact]
    public async Task ArchiveProject_MovesTheFolderAndTouchesNothingElse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string live = SeedProject(f);
        Dictionary<string, string> before = Fixture.Snapshot(live);

        ArchiveOutcome outcome = await f.Service.ArchiveProjectAsync("home", "alpha", ct);

        Assert.True(outcome.Done);
        Assert.False(Directory.Exists(live));
        Assert.Equal(before, Fixture.Snapshot(Path.Combine(f.ArchiveRoot, "Home", Alpha)));
        Assert.False(File.Exists(Path.Combine(f.ArchiveRoot, "Home", "_team.md")));
        Assert.Equal(["Alpha"], f.Service.ArchivedProjects("Home"));
        Assert.Empty(f.Service.ArchivedTeams());
        Assert.Empty(f.Membership.Calls);
    }

    /// <summary>Unarchiving moves the folder back, bytes intact, and leaves no empty Archive folder behind.</summary>
    [Fact]
    public async Task UnarchiveProject_MovesTheFolderBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string live = SeedProject(f);
        Dictionary<string, string> before = Fixture.Snapshot(live);
        _ = await f.Service.ArchiveProjectAsync("Home", Alpha, ct);

        ArchiveOutcome outcome = await f.Service.UnarchiveProjectAsync("Home", "ALPHA", ct);

        Assert.True(outcome.Done);
        Assert.Equal(before, Fixture.Snapshot(live));
        Assert.False(Directory.Exists(Path.Combine(f.ArchiveRoot, "Home")));
        Assert.Empty(f.Service.ArchivedProjects("Home"));
    }

    /// <summary>A Project whose name the archive already holds is refused with nothing moved.</summary>
    [Fact]
    public async Task ArchiveProject_NameAlreadyArchived_Refuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string live = SeedProject(f);
        _ = Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, "Home", Alpha));

        ArchiveOutcome outcome = await f.Service.ArchiveProjectAsync("Home", Alpha, ct);

        Assert.False(outcome.Done);
        Assert.Equal("A Project named Alpha is already archived in Home. Rename one of them first.", outcome.Refusal);
        Assert.True(File.Exists(Path.Combine(live, "notes.md")));
    }

    /// <summary>Unarchiving into a Team that already has a live Project of that name is refused with the archive untouched.</summary>
    [Fact]
    public async Task UnarchiveProject_NameExistsLive_Refuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string archived = Path.Combine(f.ArchiveRoot, "Home", Alpha);
        _ = Directory.CreateDirectory(archived);
        await File.WriteAllTextAsync(Path.Combine(archived, "old.md"), "old", ct);
        _ = SeedProject(f);

        ArchiveOutcome outcome = await f.Service.UnarchiveProjectAsync("Home", Alpha, ct);

        Assert.False(outcome.Done);
        Assert.Equal("A Project named Alpha exists in Home now. Rename one of them first.", outcome.Refusal);
        Assert.True(File.Exists(Path.Combine(archived, "old.md")));
    }

    /// <summary>A Project that does not exist is refused, and so is a reserved name such as the Team's own _tasks folder.</summary>
    [Theory]
    [InlineData("Missing")]
    [InlineData("_tasks")]
    public async Task ArchiveProject_NotAProject_Refuses(string project)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        _ = Directory.CreateDirectory(Path.Combine(f.TeamFolder, "_tasks"));

        ArchiveOutcome outcome = await f.Service.ArchiveProjectAsync("Home", project, ct);

        Assert.False(outcome.Done);
        Assert.Equal($"There is no Project named \"{project}\" in Home.", outcome.Refusal);
    }

    /// <summary>Archived Teams are the Archive folders holding a _team.md; a Project-only folder and a dot folder are not Teams, and reserved entries are not Projects.</summary>
    [Fact]
    public async Task ArchivedLists_ReadTheTeamFileAndSkipReservedNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string old = Path.Combine(f.ArchiveRoot, "Old");
        _ = Directory.CreateDirectory(Path.Combine(old, "Beta"));
        _ = Directory.CreateDirectory(Path.Combine(old, "_tasks"));
        await File.WriteAllTextAsync(Path.Combine(old, "_team.md"), "---\n---\n", ct);
        _ = Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, "Home", Alpha));
        _ = Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, ".huddle-archiving-X"));

        Assert.Equal(["Old"], f.Service.ArchivedTeams());
        Assert.Equal(["Beta"], f.Service.ArchivedProjects("old"));
        Assert.Equal(["Alpha"], f.Service.ArchivedProjects("Home"));
    }

    /// <summary>A crash after the archive copy is placed but before the source is retired converges at start-up: the Project is archived whole and no dot folder or journal remains.</summary>
    [Fact]
    public async Task ArchiveProject_CrashAfterPlacing_RecoversToArchived()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "Move:Alpha" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        string live = SeedProject(f);
        Dictionary<string, string> before = Fixture.Snapshot(live);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveProjectAsync("Home", Alpha, ct));
        string archivedTeam = Path.Combine(f.ArchiveRoot, "Home");
        Assert.True(File.Exists(Path.Combine(archivedTeam, ".huddle-move-Alpha.json")));

        await f.Service.RecoverAsync(ct);

        Assert.False(Directory.Exists(live));
        Assert.Equal(before, Fixture.Snapshot(Path.Combine(archivedTeam, Alpha)));
        Assert.Equal([Alpha], Directory.EnumerateFileSystemEntries(archivedTeam).Select(static e => Path.GetFileName(e)));
    }

    /// <summary>A crash before the first rename converges to the Project staying live, with the staging folder and journal gone.</summary>
    [Fact]
    public async Task ArchiveProject_CrashBeforePlacing_RecoversToLive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashBefore = "Move:Alpha" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        string live = SeedProject(f);
        Dictionary<string, string> before = Fixture.Snapshot(live);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveProjectAsync("Home", Alpha, ct));

        await f.Service.RecoverAsync(ct);

        Assert.Equal(before, Fixture.Snapshot(live));
        Assert.Empty(f.Service.ArchivedProjects("Home"));
        Assert.False(Directory.Exists(Path.Combine(f.ArchiveRoot, "Home"))); // Recovery removes the empty Archive/Home the move created.
    }

    /// <summary>A crash after the unarchive copy is placed converges at start-up: the Project is live whole, and no empty Archive/Home, dot folder or journal remains.</summary>
    [Fact]
    public async Task UnarchiveProject_CrashAfterPlacing_RecoversToLive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        string live = SeedProject(f);
        Dictionary<string, string> before = Fixture.Snapshot(live);
        _ = await f.Service.ArchiveProjectAsync("Home", Alpha, ct);
        fs.CrashAfter = "Move:Alpha";
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.UnarchiveProjectAsync("Home", Alpha, ct));
        fs.CrashAfter = null;

        await f.Service.RecoverAsync(ct);

        Assert.Equal(before, Fixture.Snapshot(live));
        Assert.False(Directory.Exists(Path.Combine(f.ArchiveRoot, "Home")));
        Assert.Empty(f.Service.ArchivedProjects("Home"));
    }

    /// <summary>Archiving and unarchiving a Project tell the Task store, so an archived-Task lookup remembered across the move is not served stale.</summary>
    [Fact]
    public async Task UnarchiveProject_ForgetsArchivedTaskLookups()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        _ = SeedProject(f);
        _ = TestTaskStore.WriteTask(f.TeamsRoot, TestTaskStore.RelativePath("Home", Alpha, false, "HOME-0007.md"), TestTasks.Make(id: "HOME-0007", location: new TaskLocation("Home", Alpha, false)));
        _ = TaskId.TryParse("HOME-0007", out TaskId id);
        _ = await f.Service.ArchiveProjectAsync("Home", Alpha, ct);
        Assert.NotNull(f.Tasks.FindArchived(id));

        _ = await f.Service.UnarchiveProjectAsync("Home", Alpha, ct);

        Assert.Null(f.Tasks.FindArchived(id));
    }

    /// <summary>Creates <c>Teams/Home/Alpha</c> with a nested file and returns its path.</summary>
    private static string SeedProject(Fixture f)
    {
        string live = Path.Combine(f.TeamFolder, Alpha);
        _ = Directory.CreateDirectory(Path.Combine(live, "sub"));
        File.WriteAllText(Path.Combine(live, "notes.md"), "alpha notes");
        File.WriteAllText(Path.Combine(live, "sub", "deep.md"), "deep");
        return live;
    }
}
