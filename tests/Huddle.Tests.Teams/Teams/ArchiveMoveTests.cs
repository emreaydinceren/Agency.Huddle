using Agency.Huddle.App.Data;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Fixture = Agency.Huddle.Tests.Teams.ArchiveTeamTests.Fixture;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// The crash-safe move behind archive and unarchive (Spec 8.4 as replaced by corrections-12 items 4, 7, 8, 9, 15, 16).
/// <see cref="ArchiveMove.Move"/> tries a plain rename first; when the file system answers
/// <see cref="ArchiveMoveResult.CrossVolume"/> (or the destination already holds a Team's archived Projects) it copies into a
/// staging folder, verifies, renames the staging folder into place, renames the source to a <c>.huddle-deleting-</c> folder and deletes
/// that. <see cref="ArchiveService.RecoverAsync"/> (run once at start-up by <see cref="ArchiveRecoveryService"/>) finishes or undoes
/// whatever a crash left. The rows drive <see cref="FakeArchiveFileSystem"/> over the real disk.
/// </summary>
public sealed class ArchiveMoveTests
{
    private const string Home = "Home";
    private const string Archiving = ".huddle-archiving-";
    private const string Unarchiving = ".huddle-unarchiving-";
    private const string Deleting = ".huddle-deleting-";
    private const string OpenFiles = "Home has files open in another program. Close them and try again.";

    /// <summary>Cross-volume: copy into <c>Archive/.huddle-archiving-Home</c>, verify, rename in, rename the source to <c>Teams/.huddle-deleting-Home</c>, delete it; in that order, nothing left over.</summary>
    [Fact]
    public void Move_CrossVolume_StagesVerifiesRenamesAndDeletesViaDeletingFolder()
    {
        using Scratch s = new();
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true };

        ArchiveMoveResult result = ArchiveMove.Move(s.Source, s.Destination, Archiving, fs);

        Assert.Equal(ArchiveMoveResult.Moved, result);
        // Seeded by Scratch: a.txt, b.txt, c.txt, sub/, sub/d.bin (four files).
        Assert.Equal(4, before.Values.Count(static v => v != "<dir>"));
        Assert.Equal(before, Fixture.Snapshot(s.Destination));
        Assert.False(Directory.Exists(s.Source));
        Assert.Empty(Dots(s.Teams));
        Assert.Empty(Dots(s.Archive));
        string[] order = [$"CopyTree:{Archiving}Home", $"Measure:{Archiving}Home", "Move:Home", $"Move:{Deleting}Home", $"DeleteTree:{Deleting}Home"];
        int[] at = [.. order.Select((step, i) => i == 2 ? fs.Log.ToList().LastIndexOf(step) : fs.Log.ToList().IndexOf(step))];
        Assert.DoesNotContain(-1, at);
        Assert.Equal(at.Order(), at);
    }

    /// <summary>A copy that fails on the third file removes the staging folder, rethrows and leaves the source whole.</summary>
    [Fact]
    public void Move_CopyFailsOnThirdFile_RemovesStagingAndKeepsSource()
    {
        using Scratch s = new();
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, FailCopyAtFile = 3 };

        _ = Assert.Throws<IOException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));

        Assert.Equal(before, Fixture.Snapshot(s.Source));
        Assert.False(Directory.Exists(s.Destination));
        Assert.Empty(Dots(s.Archive));
        Assert.Empty(Dots(s.Teams));
    }

    /// <summary>A copy one byte short fails the byte-count verify: nothing is renamed in and the source is kept.</summary>
    [Fact]
    public void Move_CopyOneByteShort_VerifyFailsAndSourceKept()
    {
        using Scratch s = new();
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CorruptCopy = true };

        _ = Assert.Throws<IOException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));

        Assert.Equal(before, Fixture.Snapshot(s.Source));
        Assert.False(Directory.Exists(s.Destination));
        Assert.Empty(Dots(s.Archive));
    }

    /// <summary>A crash after the copy leaves the source intact and no <c>Archive/Home</c>; <c>Recover</c> removes the staging folder.</summary>
    [Fact]
    public void Recover_CrashAfterCopy_RemovesStagingAndKeepsSource()
    {
        using Scratch s = new();
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = $"CopyTree:{Archiving}Home" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));
        Assert.Equal([$"{Archiving}Home"], Dots(s.Archive));
        Assert.False(Directory.Exists(s.Destination));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.Empty(Dots(s.Archive));
        Assert.Equal(before, Fixture.Snapshot(s.Source));
    }

    /// <summary>A crash half-way through deleting <c>Teams/.huddle-deleting-Home</c> leaves <c>Archive/Home</c> complete; <c>Recover</c> finishes the delete.</summary>
    [Fact]
    public void Recover_CrashMidDelete_FinishesTheDeleteAndKeepsDestination()
    {
        using Scratch s = new();
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "DeleteTree:half" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));
        Assert.Equal(before, Fixture.Snapshot(s.Destination));
        Assert.Equal([$"{Deleting}Home"], Dots(s.Teams));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.Empty(Dots(s.Teams));
        Assert.Equal(before, Fixture.Snapshot(s.Destination));
    }

    /// <summary>Recover deletes the three kinds of Huddle dot folder in either root and nothing else: a user's <c>.git</c>, a Team named like a dot folder's neighbour and the archived Team stay.</summary>
    [Fact]
    public void Recover_DotFolders_RemovesOnlyTheHuddleKinds()
    {
        using Scratch s = new();
        string[] doomed = [$"{s.Archive}/{Archiving}A", $"{s.Teams}/{Unarchiving}B", $"{s.Teams}/{Deleting}C", $"{s.Archive}/{Deleting}D"];
        string[] kept = [$"{s.Archive}/.obsidian", $"{s.Teams}/.git", $"{s.Archive}/Real", $"{s.Teams}/Huddle-archiving"];
        foreach (string folder in doomed.Concat(kept))
        {
            _ = Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "x.md"), "x");
        }

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.All(doomed, static d => Assert.False(Directory.Exists(d)));
        Assert.All(kept, static k => Assert.True(File.Exists(Path.Combine(k, "x.md"))));
    }

    /// <summary>Cross-volume archive of a real Team: staged, verified, renamed in; <c>Teams/Home</c> gone, identical bytes, no dot folder in either root, labels removed after.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_CrossVolume_MovesThroughStagingAndLeavesNoDotFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Contains($"CopyTree:{Archiving}Home", fs.Log);
        Assert.Contains($"DeleteTree:{Deleting}Home", fs.Log);
        Assert.False(Directory.Exists(f.TeamFolder));
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.Empty(Dots(f.TeamsRoot));
        Assert.Equal(2, f.Membership.Calls.Count(static c => c.Method == "RemoveForArchive"));
    }

    /// <summary>A crash after the cross-volume copy: Teams/Home is whole, no <c>Archive/Home</c>, labels untouched; start-up recovery deletes the staging folder and puts the manager and roster back.</summary>
    [Fact]
    public async Task RecoverAsync_CrashAfterCopy_UndoesRosterAndRemovesStaging()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = $"CopyTree:{Archiving}Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        Assert.Equal([$"{Archiving}Home"], Dots(f.ArchiveRoot));
        Assert.False(Directory.Exists(f.ArchivedFolder));
        Assert.Empty(f.Membership.Calls);

        await f.Service.RecoverAsync(ct);

        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        TeamFile live = ReadLive(f);
        // The fixture's _team.md names Ada manager; the roster write cleared it and recovery restores it from archivedManager.
        Assert.Equal("Ada", live.Manager);
        Assert.Empty(live.ArchivedMembers);
        Assert.Null(live.ArchivedManager);
    }

    /// <summary>A crash while deleting the old folder leaves <c>Archive/Home</c> complete with labels still on; recovery finishes the delete and removes the labels again.</summary>
    [Fact]
    public async Task RecoverAsync_CrashMidDelete_FinishesDeleteAndRemovesLabels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "DeleteTree:half" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
        Assert.Equal([$"{Deleting}Home"], Dots(f.TeamsRoot));
        Assert.Empty(f.Membership.Calls);

        await f.Service.RecoverAsync(ct);

        Assert.Empty(Dots(f.TeamsRoot));
        Assert.Equal(before, Fixture.Snapshot(f.ArchivedFolder));
        // The archived _team.md lists Ada and Nova (see ArchiveTeamTests); their labels were never removed, so recovery removes them.
        Assert.Equal(
            [new MembershipCall("RemoveForArchive", Home, "Ada"), new MembershipCall("RemoveForArchive", Home, "Nova")],
            f.Membership.Calls.OrderBy(static c => c.Name, StringComparer.Ordinal));
    }

    /// <summary>Corrections item 16: a crash between the roster write and the move leaves the live <c>_team.md</c> without a manager and with <c>archivedMembers</c>; recovery restores the manager, re-adds the labels and clears the roster, and the Room stays open.</summary>
    [Fact]
    public async Task RecoverAsync_CrashBetweenRosterAndMove_RestoresManagerLabelsAndClearsRoster()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrashBefore = "Move:Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Room room = await f.CreateRoomAsync(Home, ct);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        TeamFile crashed = ReadLive(f);
        Assert.Null(crashed.Manager);
        Assert.Equal(["Ada", "Nova"], crashed.ArchivedMembers);

        await f.Service.RecoverAsync(ct);

        TeamFile live = ReadLive(f);
        Assert.Equal("Ada", live.Manager);
        Assert.Empty(live.ArchivedMembers);
        Assert.Empty(live.ArchivedRooms);
        Assert.Null(live.ArchivedManager);
        Assert.Equal(
            [new MembershipCall("Add", Home, "Ada"), new MembershipCall("Add", Home, "Nova")],
            f.Membership.Calls.OrderBy(static c => c.Name, StringComparer.Ordinal));
        Assert.False((await f.Directory.GetRoomsAsync(ct)).Single(r => r.Id == room.Id).Archived);
    }

    /// <summary>Recovering a Team that was never half-archived changes nothing and calls no membership method.</summary>
    [Fact]
    public async Task RecoverAsync_NothingInterrupted_ChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct);
        string before = File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName));

        await f.Service.RecoverAsync(ct);

        Assert.Equal(before, File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)));
        Assert.Empty(f.Membership.Calls);
    }

    /// <summary>A rename refused three times because a program has a file open succeeds on the fourth try.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_FileOpenThreeTimes_RetriesThenSucceeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { SharingViolations = 3 };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        // Three refusals plus the rename that worked.
        Assert.Equal(4, fs.Log.Count(static l => l == "Move:Home"));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>A file that stays open: archive retries, then refuses with the settled text, changes no label and puts <c>_team.md</c> back.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_FileStaysOpen_RefusesWithSettledTextAndChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { SharingViolations = int.MaxValue };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal(OpenFiles, outcome.Refusal);
        Assert.True(fs.Log.Count(static l => l == "Move:Home") >= 3);
        Assert.Empty(f.Membership.Calls);
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        Assert.False(Directory.Exists(f.ArchivedFolder));
        TeamFile live = ReadLive(f);
        Assert.Equal("Ada", live.Manager);
        Assert.Empty(live.ArchivedMembers);
        Assert.Empty(Dots(f.ArchiveRoot));
    }

    /// <summary>Unarchive with a file open refuses with the same text and adds no label; the Team stays archived.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_FileStaysOpen_RefusesWithSettledTextAndAddsNoLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        fs.SharingViolations = int.MaxValue;

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal(OpenFiles, outcome.Refusal);
        Assert.DoesNotContain(f.Membership.Calls, static c => c.Method == "Add");
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.False(Directory.Exists(f.TeamFolder));
    }

    /// <summary>While an unarchive moves the folder, the Team's Task writes are paused; afterwards they work again.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_WhileMoving_TaskWritesAreRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        bool refused = false;
        fs.OnMove = () =>
        {
            TaskItem candidate = TestTasks.Make(id: "HOME-0099", location: new(Home, null, false), path: "");
            refused = Assert.Throws<InvalidOperationException>(() => f.Tasks.Create(candidate, TaskFileFormat.Compose(candidate))) is not null;
        };

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.True(refused);
        TaskItem after = TestTasks.Make(id: "HOME-0098", location: new(Home, null, false), path: "");
        Assert.NotNull(f.Tasks.Create(after, TaskFileFormat.Compose(after)));
    }

    /// <summary>Cross-volume unarchive mirrors archive: stage under <c>Teams/.huddle-unarchiving-Home</c>, rename in, delete the archive folder through <c>Archive/.huddle-deleting-Home</c>.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_CrossVolume_StagesInTeamsAndLeavesNoDotFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        fs.CrossVolume = true;

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Contains($"CopyTree:{Unarchiving}Home", fs.Log);
        Assert.Contains($"DeleteTree:{Deleting}Home", fs.Log);
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        Assert.False(Directory.Exists(f.ArchivedFolder));
        Assert.Empty(Dots(f.TeamsRoot));
        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.Equal(["Ada", "Nova"], [.. f.Membership.Calls.Where(static c => c.Method == "Add").Select(static c => c.Name).Order(StringComparer.Ordinal)]);
    }

    /// <summary>A crash during the cross-volume unarchive copy leaves the archive whole; recovery removes <c>Teams/.huddle-unarchiving-Home</c>.</summary>
    [Fact]
    public async Task RecoverAsync_CrashAfterUnarchiveCopy_RemovesStagingAndKeepsArchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        Dictionary<string, string> archived = Fixture.Snapshot(f.ArchivedFolder);
        fs.CrossVolume = true;
        fs.CrashAfter = $"CopyTree:{Unarchiving}Home";
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.UnarchiveTeamAsync(Home, ct));
        Assert.Equal([$"{Unarchiving}Home"], Dots(f.TeamsRoot));

        await f.Service.RecoverAsync(ct);

        Assert.Empty(Dots(f.TeamsRoot));
        Assert.Equal(archived, Fixture.Snapshot(f.ArchivedFolder));
        Assert.False(Directory.Exists(f.TeamFolder));
    }

    /// <summary>An interrupted unarchive (folder back in Teams, labels not yet added) is finished by recovery: labels added, manager restored, roster cleared.</summary>
    [Fact]
    public async Task RecoverAsync_UnarchiveMovedButLabelsNotAdded_CompletesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        fs.CrashAfter = "Move:Home";
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.UnarchiveTeamAsync(Home, ct));
        Assert.DoesNotContain(f.Membership.Calls, static c => c.Method == "Add");

        await f.Service.RecoverAsync(ct);

        TeamFile live = ReadLive(f);
        Assert.Equal("Ada", live.Manager);
        Assert.Empty(live.ArchivedMembers);
        Assert.Equal(["Ada", "Nova"], [.. f.Membership.Calls.Where(static c => c.Method == "Add").Select(static c => c.Name).Order(StringComparer.Ordinal)]);
    }

    /// <summary>A Team folder in both Teams and Archive (each with <c>_team.md</c>) is ambiguous after a crash: recovery leaves both alone and calls no membership method.</summary>
    [Fact]
    public async Task RecoverAsync_TeamInBothRoots_LeavesBothAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: new FakeArchiveFileSystem(new PhysicalArchiveFileSystem()));
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        new PhysicalArchiveFileSystem().CopyTree(f.ArchivedFolder, f.TeamFolder);
        Dictionary<string, string> live = Fixture.Snapshot(f.TeamFolder);
        Dictionary<string, string> archived = Fixture.Snapshot(f.ArchivedFolder);
        int calls = f.Membership.Calls.Count;

        await f.Service.RecoverAsync(ct);

        Assert.Equal(live, Fixture.Snapshot(f.TeamFolder));
        Assert.Equal(archived, Fixture.Snapshot(f.ArchivedFolder));
        Assert.Equal(calls, f.Membership.Calls.Count);
    }

    /// <summary>Merging into an existing <c>Archive/Home/</c> that holds archived Projects goes through staging: a crash after the copy leaves no <c>_team.md</c> there, so the Team does not look archived.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_MergeCrashAfterCopy_ArchiveFolderStillHasNoTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrashAfter = $"CopyTree:{Archiving}Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        string project = Path.Combine(f.ArchivedFolder, "Alpha");
        _ = Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "plan.md"), "plan");

        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));

        Assert.False(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.True(File.Exists(Path.Combine(project, "plan.md")));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)));

        await f.Service.RecoverAsync(ct);

        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.True(File.Exists(Path.Combine(project, "plan.md")));
    }

    /// <summary>A finished merge holds the archived Project, the Team's files and <c>_team.md</c> in <c>Archive/Home</c>, with no dot folder and no live folder.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_MergeIntoExistingArchiveFolder_HoldsBothAndLeavesNoDotFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        string project = Path.Combine(f.ArchivedFolder, "Alpha");
        _ = Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "plan.md"), "plan");
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Contains($"CopyTree:{Archiving}Home", fs.Log);
        Assert.Equal("plan", File.ReadAllText(Path.Combine(project, "plan.md")));
        Dictionary<string, string> after = Fixture.Snapshot(f.ArchivedFolder);
        // The archive now holds the Team's own entries plus Alpha/ and Alpha/plan.md.
        Assert.Equal(before.Count + 2, after.Count);
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.False(Directory.Exists(f.TeamFolder));
        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.Empty(Dots(f.TeamsRoot));
    }

    /// <summary>The start-up hosted service runs the recovery: after a crash, <c>StartAsync</c> leaves no dot folder.</summary>
    [Fact]
    public async Task ArchiveRecoveryService_StartAsync_RunsRecovery()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = $"CopyTree:{Archiving}Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        Assert.NotEmpty(Dots(f.ArchiveRoot));
        ArchiveRecoveryService startup = new(f.Service);

        await startup.StartAsync(ct);

        Assert.Empty(Dots(f.ArchiveRoot));
        Assert.Equal("Ada", ReadLive(f).Manager);
    }

    private static string[] Dots(string root) =>
        [.. Directory.EnumerateFileSystemEntries(root).Select(static p => Path.GetFileName(p)).Where(static n => n.StartsWith(".huddle-", StringComparison.Ordinal) && !n.StartsWith(".huddle-move-", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    private static TeamFile ReadLive(Fixture f)
    {
        Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)), out TeamFile? file));
        return file;
    }

    /// <summary>A temp <c>Teams/Home</c> of four files and an empty <c>Archive</c>, deleted on dispose.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("archive-move-").FullName;

        public Scratch()
        {
            _ = Directory.CreateDirectory(this.Archive);
            _ = Directory.CreateDirectory(Path.Combine(this.Source, "sub"));
            File.WriteAllText(Path.Combine(this.Source, "a.txt"), "alpha");
            File.WriteAllText(Path.Combine(this.Source, "b.txt"), "bravo");
            File.WriteAllText(Path.Combine(this.Source, "c.txt"), "charlie");
            File.WriteAllBytes(Path.Combine(this.Source, "sub", "d.bin"), [0, 255, 10, 13]);
        }

        public string Teams => Path.Combine(this.root, "Teams");

        public string Archive => Path.Combine(this.root, "Archive");

        public string Source => Path.Combine(this.Teams, Home);

        public string Destination => Path.Combine(this.Archive, Home);

        public void Dispose() => Directory.Delete(this.root, recursive: true);
    }
}
