using Agency.Huddle.App.Data;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;
using Fixture = Agency.Huddle.Tests.Teams.ArchiveTeamTests.Fixture;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// The edges the review of Tasks 111 and 113 found in the archive move: how a failed rename is classified, the step journal that
/// lets <see cref="ArchiveMove.Recover"/> finish or undo a move from any crash point, a merge into an existing archive that never
/// touches the live archive until one swap, and the refusals for exceptions other than a held-open file.
/// </summary>
public sealed class ArchiveRecoveryEdgeTests
{
    private const string Home = "Home";
    private const string Archiving = ".huddle-archiving-";
    private const string Unarchiving = ".huddle-unarchiving-";
    private const string Replaced = ".huddle-replaced-";
    private const string Journal = ".huddle-move-Home.json";

    /// <summary>Two drive letters are another volume even though the exception carries no device HResult (<c>Directory.Move</c> throws 0x80131620 before touching the disk).</summary>
    [Fact]
    public void Classify_DifferentDriveRoots_IsCrossVolume()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Drive letters exist only on Windows.");

        ArchiveMoveResult? result = PhysicalArchiveFileSystem.Classify(new IOException("Source and destination path must have identical roots.") { HResult = unchecked((int)0x80131620) }, @"E:\Teams\Home", @"D:\Archive\Home");

        Assert.Equal(ArchiveMoveResult.CrossVolume, result);
    }

    /// <summary>Windows ERROR_NOT_SAME_DEVICE and Linux EXDEV (errno 18) are another volume even on identical roots (a mount point).</summary>
    [Theory]
    [InlineData(unchecked((int)0x80070011))]
    [InlineData(18)]
    public void Classify_DeviceError_IsCrossVolume(int hresult)
    {
        string root = Path.GetTempPath();

        ArchiveMoveResult? result = PhysicalArchiveFileSystem.Classify(new IOException("x") { HResult = hresult }, Path.Combine(root, "a"), Path.Combine(root, "b"));

        Assert.Equal(ArchiveMoveResult.CrossVolume, result);
    }

    /// <summary>Only sharing and lock violations and access denied are "files open"; an already-exists or not-found error is neither retried nor reported as open files.</summary>
    [Theory]
    [InlineData(unchecked((int)0x80070020), true)]
    [InlineData(unchecked((int)0x80070021), true)]
    [InlineData(unchecked((int)0x800700B7), false)]
    [InlineData(unchecked((int)0x80070002), false)]
    public void Classify_IoError_IsInUseOnlyForSharingAndLockViolations(int hresult, bool inUse)
    {
        string root = Path.GetTempPath();

        ArchiveMoveResult? result = PhysicalArchiveFileSystem.Classify(new IOException("x") { HResult = hresult }, Path.Combine(root, "a"), Path.Combine(root, "b"));

        Assert.Equal(inUse ? ArchiveMoveResult.InUse : null, result);
    }

    /// <summary>Access denied while renaming is how Windows reports a folder held open, so it retries like a sharing violation.</summary>
    [Fact]
    public void Classify_AccessDenied_IsInUse()
    {
        Assert.Equal(ArchiveMoveResult.InUse, PhysicalArchiveFileSystem.Classify(new UnauthorizedAccessException(), "a", "b"));
    }

    /// <summary>A crash between placing the archive copy and renaming the source (archive direction) converges: Recover retires the source and clears the journal.</summary>
    [Fact]
    public void Recover_ArchiveCrashBetweenPlaceAndSourceRename_Converges()
    {
        using Scratch s = new(withArchive: false);
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "Move:Home" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));
        Assert.True(Directory.Exists(s.Source));
        Assert.True(Directory.Exists(s.Destination));
        Assert.True(File.Exists(Path.Combine(s.Archive, Journal)));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.False(Directory.Exists(s.Source));
        Assert.Equal(before, Fixture.Snapshot(s.Destination));
        Assert.Empty(Dots(s.Archive));
        Assert.Empty(Dots(s.Teams));
    }

    /// <summary>The same crash point in the unarchive direction converges the other way: the archive folder is retired and the Team stays live.</summary>
    [Fact]
    public void Recover_UnarchiveCrashBetweenPlaceAndSourceRename_Converges()
    {
        using Scratch s = new(withArchive: false);
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        string archived = s.Destination;
        Directory.Move(s.Source, archived);
        string live = s.Source;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "Move:Home" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(archived, live, Unarchiving, fs));
        Assert.True(Directory.Exists(archived));
        Assert.True(Directory.Exists(live));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.False(Directory.Exists(archived));
        Assert.Equal(before, Fixture.Snapshot(live));
        Assert.Empty(Dots(s.Archive));
        Assert.Empty(Dots(s.Teams));
    }

    /// <summary>A crash after the copy leaves a journal; Recover deletes the staging folder and the journal, and the source stays whole.</summary>
    [Fact]
    public void Recover_CrashWhileStaging_DeletesStagingAndJournal()
    {
        using Scratch s = new(withArchive: false);
        Dictionary<string, string> before = Fixture.Snapshot(s.Source);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = $"CopyTree:{Archiving}Home" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));
        Assert.Equal([$"{Archiving}Home", Journal], Dots(s.Archive));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.Empty(Dots(s.Archive));
        Assert.Equal(before, Fixture.Snapshot(s.Source));
    }

    /// <summary>A finished move leaves no journal behind.</summary>
    [Fact]
    public void Move_Completed_LeavesNoJournal()
    {
        using Scratch s = new(withArchive: false);

        _ = ArchiveMove.Move(s.Source, s.Destination, Archiving, new FakeArchiveFileSystem(new PhysicalArchiveFileSystem()) { CrossVolume = true });

        Assert.Empty(Dots(s.Archive));
    }

    /// <summary>A crash before the swap of a merge leaves the archive exactly as it was and the live Team whole; Recover only removes the staging folder.</summary>
    [Fact]
    public void Move_MergeCrashBeforeSwap_LeavesArchiveAsItWasAndLiveTeamIntact()
    {
        using Scratch s = new(withArchive: true);
        Dictionary<string, string> live = Fixture.Snapshot(s.Source);
        Dictionary<string, string> archive = Fixture.Snapshot(s.Destination);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrashBefore = Replaced.Insert(0, "Move:") + Home };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));

        Assert.Equal(archive, Fixture.Snapshot(s.Destination));
        Assert.Equal(live, Fixture.Snapshot(s.Source));
        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());
        Assert.Equal(archive, Fixture.Snapshot(s.Destination));
        Assert.Equal(live, Fixture.Snapshot(s.Source));
        Assert.Empty(Dots(s.Archive));
    }

    /// <summary>A crash in the middle of the swap (old archive set aside, new one not yet in place) converges to the merged archive and the live Team is retired.</summary>
    [Fact]
    public void Recover_MergeCrashMidSwap_ConvergesToMergedArchive()
    {
        using Scratch s = new(withArchive: true);
        Dictionary<string, string> expected = Fixture.Snapshot(s.Source);
        foreach ((string key, string value) in Fixture.Snapshot(s.Destination))
        {
            expected[key] = value;
        }

        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrashAfter = $"Move:{Replaced}Home" };
        _ = Assert.Throws<SimulatedCrashException>(() => ArchiveMove.Move(s.Source, s.Destination, Archiving, fs));
        Assert.False(Directory.Exists(s.Destination));

        ArchiveMove.Recover(s.Archive, s.Teams, new PhysicalArchiveFileSystem());

        Assert.Equal(expected, Fixture.Snapshot(s.Destination));
        Assert.False(Directory.Exists(s.Source));
        Assert.Empty(Dots(s.Archive));
        Assert.Empty(Dots(s.Teams));
    }

    /// <summary>A merge never renames entries one by one into the live archive: the only renames are the staging folder, the set-aside and the source retirement.</summary>
    [Fact]
    public void Move_Merge_RenamesNoEntryIntoTheLiveArchive()
    {
        using Scratch s = new(withArchive: true);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());

        ArchiveMoveResult result = ArchiveMove.Move(s.Source, s.Destination, Archiving, fs);

        Assert.Equal(ArchiveMoveResult.Moved, result);
        Assert.Empty(fs.Log.Where(static l => l.StartsWith("Move:", StringComparison.Ordinal)).Except([$"Move:{Replaced}Home", "Move:Home", "Move:.huddle-deleting-Home"], StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(s.Destination, "Alpha", "plan.md")));
        Assert.True(File.Exists(Path.Combine(s.Destination, "a.txt")));
    }

    /// <summary>A source that cannot be retired after a merge puts the archive back exactly as it was (the unplace swaps the old archive back).</summary>
    [Fact]
    public void Move_MergeSourceCannotBeRetired_RestoresArchiveAsItWas()
    {
        using Scratch s = new(withArchive: true);
        Dictionary<string, string> live = Fixture.Snapshot(s.Source);
        Dictionary<string, string> archive = Fixture.Snapshot(s.Destination);
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { InUseFor = ".huddle-deleting-Home" };

        ArchiveMoveResult result = ArchiveMove.Move(s.Source, s.Destination, Archiving, fs);

        Assert.Equal(ArchiveMoveResult.InUse, result);
        Assert.Equal(archive, Fixture.Snapshot(s.Destination));
        Assert.Equal(live, Fixture.Snapshot(s.Source));
        Assert.Empty(Dots(s.Archive));
        Assert.Empty(Dots(s.Teams));
    }

    /// <summary>Access denied during the copy (not an IOException) is handled like one: the archive refuses, the source stays whole and <c>_team.md</c> goes back as it was.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_AccessDeniedDuringCopy_RefusesAndRestoresTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CopyThrows = new UnauthorizedAccessException("denied") };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Dictionary<string, string> before = Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal("Home could not be moved: denied", outcome.Refusal);
        Assert.Equal(before, Fixture.Snapshot(f.TeamFolder));
        Assert.Equal("Ada", ReadLive(f).Manager);
        Assert.Empty(ReadLive(f).ArchivedMembers);
        Assert.Empty(f.Membership.Calls);
        Assert.False(Directory.Exists(f.ArchivedFolder));
    }

    /// <summary>Access denied during the unarchive copy refuses, adds no label and leaves the Team archived.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_AccessDeniedDuringCopy_RefusesAndLeavesTeamArchived()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        fs.CrossVolume = true;
        fs.CopyThrows = new UnauthorizedAccessException("denied");

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal("Home could not be moved: denied", outcome.Refusal);
        Assert.DoesNotContain(f.Membership.Calls, static c => c.Method == "Add");
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.False(Directory.Exists(f.TeamFolder));
    }

    /// <summary>A rename that fails for a reason other than a held-open file is not retried and not blamed on open files.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_RenameFailsForOtherReason_FailsFastWithGenericRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { MoveThrows = new IOException("Cannot create a file when that file already exists.") };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal("Home could not be moved: Cannot create a file when that file already exists.", outcome.Refusal);
        Assert.Equal(1, fs.Log.Count(static l => l == "Move:Home"));
        Assert.Equal("Ada", ReadLive(f).Manager);
        Assert.Empty(Dots(f.ArchiveRoot));
    }

    /// <summary>A label-only Team whose archive crashed while staging gets the <c>_team.md</c> that archiving created removed again by recovery.</summary>
    [Fact]
    public async Task RecoverAsync_LabelOnlyTeamCrashWhileStaging_DeletesTheCreatedTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = $"CopyTree:{Archiving}Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: true, ct, files: fs);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)));

        await f.Service.RecoverAsync(ct);

        Assert.False(File.Exists(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)));
        Assert.True(File.Exists(Path.Combine(f.TeamFolder, "notes.md")));
        Assert.Empty(Dots(f.ArchiveRoot));
    }

    /// <summary>The archive folder is found by listing, ignoring case: a live <c>hOME</c> beside an archived <c>Home</c> is still "in both roots" on a case-sensitive file system.</summary>
    [Fact]
    public async Task RecoverAsync_TeamInBothRootsWithDifferentCase_LeavesBothAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CaseSensitiveNames = true };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        string other = Path.Combine(f.TeamsRoot, "hOME");
        new PhysicalArchiveFileSystem().CopyTree(f.ArchivedFolder, other);
        int calls = f.Membership.Calls.Count;

        await f.Service.RecoverAsync(ct);

        Assert.Equal(calls, f.Membership.Calls.Count);
        Assert.NotEmpty(ReadRoster(other));
    }

    /// <summary>Recovery reads the archived <c>_team.md</c> through the file-system seam, not <c>File.ReadAllText</c>.</summary>
    [Fact]
    public async Task RecoverAsync_RemovesLabelsAgain_ReadsTeamFileThroughTheSeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "DeleteTree:half" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));

        await f.Service.RecoverAsync(ct);

        Assert.Contains("ReadText:_team.md", fs.Log);
        Assert.Equal(2, f.Membership.Calls.Count(static c => c.Method == "RemoveForArchive"));
    }

    /// <summary>A crash after the archive copy was placed (labels still on, Rooms still open) converges fully: recovery retires the source, archives the recorded Room and removes both labels.</summary>
    [Fact]
    public async Task RecoverAsync_ArchiveCrashAfterPlace_ArchivesRoomsAndRemovesLabels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem()) { CrossVolume = true, CrashAfter = "Move:Home" };
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Room room = await f.CreateRoomAsync(Home, ct);
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.ArchiveTeamAsync(Home, ct));
        Assert.False((await f.Directory.GetRoomsAsync(ct)).Single(r => r.Id == room.Id).Archived);

        await f.Service.RecoverAsync(ct);

        Assert.False(Directory.Exists(f.TeamFolder));
        Assert.True((await f.Directory.GetRoomsAsync(ct)).Single(r => r.Id == room.Id).Archived);
        Assert.Equal(2, f.Membership.Calls.Count(static c => c.Method == "RemoveForArchive"));
        Assert.Equal([room.Id], f.ReadArchivedTeamFile().ArchivedRooms);
    }

    /// <summary>A crash after the unarchive copy was placed converges fully: the Room is restored, the labels are added and the manager is back.</summary>
    [Fact]
    public async Task RecoverAsync_UnarchiveCrashAfterPlace_RestoresRoomsLabelsAndManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        using Fixture f = await Fixture.CreateAsync(labelOnly: false, ct, files: fs);
        Room room = await f.CreateRoomAsync(Home, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        fs.CrossVolume = true;
        fs.CrashAfter = "Move:Home";
        _ = await Assert.ThrowsAsync<SimulatedCrashException>(() => f.Service.UnarchiveTeamAsync(Home, ct));

        await f.Service.RecoverAsync(ct);

        Assert.False(Directory.Exists(f.ArchivedFolder));
        Assert.False((await f.Directory.GetRoomsAsync(ct)).Single(r => r.Id == room.Id).Archived);
        Assert.Equal(["Ada", "Nova"], [.. f.Membership.Calls.Where(static c => c.Method == "Add").Select(static c => c.Name).Order(StringComparer.Ordinal)]);
        Assert.Equal("Ada", ReadLive(f).Manager);
    }

    /// <summary>The journal opened before the roster write lets recovery delete a <c>_team.md</c> that archiving created, even when the crash came before the move began; abandoning removes it.</summary>
    [Fact]
    public void Begin_ThenRecover_DeletesCreatedTeamFile_AndAbandonRemovesJournal()
    {
        using Scratch s = new(withArchive: false);
        File.WriteAllText(Path.Combine(s.Source, TeamPaths.TeamFileName), "created");
        FakeArchiveFileSystem fs = new(new PhysicalArchiveFileSystem());
        ArchiveMove.Begin(s.Source, s.Destination, Archiving, fs, teamFileCreated: true);
        Assert.True(File.Exists(Path.Combine(s.Archive, Journal)));

        ArchiveMove.Recover(s.Archive, s.Teams, fs);

        Assert.False(File.Exists(Path.Combine(s.Source, TeamPaths.TeamFileName)));
        Assert.True(File.Exists(Path.Combine(s.Source, "a.txt")));
        Assert.Empty(Dots(s.Archive));
        ArchiveMove.Begin(s.Source, s.Destination, Archiving, fs, teamFileCreated: false);
        ArchiveMove.Abandon(s.Source, s.Destination, Archiving, fs);
        Assert.Empty(Dots(s.Archive));
    }

    private static IReadOnlyList<string> ReadRoster(string folder)
    {
        Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(folder, TeamPaths.TeamFileName)), out TeamFile? file));
        return file.ArchivedMembers;
    }

    private static TeamFile ReadLive(Fixture f)
    {
        Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)), out TeamFile? file));
        return file;
    }

    private static string[] Dots(string root) =>
        [.. Directory.EnumerateFileSystemEntries(root).Select(static p => Path.GetFileName(p)).Where(static n => n.StartsWith(".huddle-", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    /// <summary>A temp <c>Teams/Home</c> of three files and, optionally, an <c>Archive/Home</c> holding the archived Project <c>Alpha/plan.md</c>.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("archive-edge-").FullName;

        public Scratch(bool withArchive)
        {
            _ = Directory.CreateDirectory(this.Archive);
            _ = Directory.CreateDirectory(Path.Combine(this.Source, "sub"));
            File.WriteAllText(Path.Combine(this.Source, "a.txt"), "alpha");
            File.WriteAllText(Path.Combine(this.Source, "b.txt"), "bravo");
            File.WriteAllBytes(Path.Combine(this.Source, "sub", "d.bin"), [0, 255, 10, 13]);
            if (withArchive)
            {
                _ = Directory.CreateDirectory(Path.Combine(this.Destination, "Alpha"));
                File.WriteAllText(Path.Combine(this.Destination, "Alpha", "plan.md"), "plan");
            }
        }

        public string Teams => Path.Combine(this.root, "Teams");

        public string Archive => Path.Combine(this.root, "Archive");

        public string Source => Path.Combine(this.Teams, Home);

        public string Destination => Path.Combine(this.Archive, Home);

        public void Dispose() => Directory.Delete(this.root, recursive: true);
    }
}
