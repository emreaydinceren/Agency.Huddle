using Agency.Huddle.App.Data;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Unarchiving a Team (Spec 6.7, corrections-12 items 6, 12 and 15): the folder moves back whole, <c>Add</c> runs for each
/// recorded member that still exists, the Rooms the archive recorded are restored, and only then are the roster and the Room
/// list cleared from <c>_team.md</c>. The fixture is <see cref="ArchiveTeamTests.Fixture"/>: each row archives Home first.
/// </summary>
public sealed class UnarchiveTeamTests
{
    private const string Home = "Home";
    private const string Refused = "A Team named Home exists now. Rename one of them first.";

    /// <summary>The folder comes back: <c>Archive/Home</c> is gone and <c>Teams/Home</c> holds every file and folder with the same bytes, except <c>_team.md</c>.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_SameVolume_MovesFolderBackWithIdenticalBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        Dictionary<string, string> before = ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Null(outcome.Refusal);
        Assert.Empty(outcome.Skipped);
        Assert.False(Directory.Exists(f.ArchivedFolder));
        // Seeded: notes.md, memory/lesson.md, raw/Tasks/HOME-0001/spec.bin and the _tasks files; the snapshot ignores _team.md.
        Assert.Contains("raw/Tasks/HOME-0001/spec.bin", before.Keys);
        Assert.Equal(before, ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder));
    }

    /// <summary>The restored <c>_team.md</c> carries neither <c>archivedMembers</c> nor <c>archivedRooms</c> and keeps the Team type.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_Done_RosterAndRoomKeysRemovedFromTeamFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.CreateRoomAsync(Home, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        Assert.Contains("archivedRooms", File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)), StringComparison.Ordinal);
        ForgetHome(f);

        _ = await f.Service.UnarchiveTeamAsync(Home, ct);

        string text = File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName));
        Assert.DoesNotContain("archivedMembers", text, StringComparison.Ordinal);
        Assert.DoesNotContain("archivedRooms", text, StringComparison.Ordinal);
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        Assert.Equal(new VersionedRef("web", "1.2.0"), file.TeamType);
    }

    /// <summary>
    /// <c>Add</c> runs for each recorded member in roster order (Ada, Nova), with the folder's exact spelling even when the request
    /// is in another case, and each call happens while the folder is back in <c>Teams</c> and <c>archivedMembers</c> is still in its file.
    /// </summary>
    [Fact]
    public async Task UnarchiveTeamAsync_Members_AddedBeforeTheRosterIsCleared()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        int archiveCalls = f.Membership.Calls.Count;
        List<(bool FolderBack, bool RosterStillThere)> seen = [];
        f.Membership.OnCall = _ => seen.Add((
            Directory.Exists(f.TeamFolder),
            File.Exists(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName))
                && File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)).Contains("archivedMembers", StringComparison.Ordinal)));

        _ = await f.Service.UnarchiveTeamAsync("hOME", ct);

        // Archived roster ['Ada', 'Nova'] (ArchiveTeamTests.ArchiveTeamAsync_Roster_WrittenIntoArchivedTeamFile) -> two Adds, in that order, on "Home" not "hOME".
        Assert.Equal(
            [new MembershipCall("Add", Home, "Ada"), new MembershipCall("Add", Home, "Nova")],
            f.Membership.Calls.Skip(archiveCalls));
        Assert.Equal(2, seen.Count);
        Assert.All(seen, s => Assert.True(s.FolderBack && s.RosterStillThere));
    }

    /// <summary>A recorded member whose Persona was deleted meanwhile gets no <c>Add</c> and is named in <see cref="ArchiveOutcome.Skipped"/>; the unarchive still completes.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_DeletedPersona_SkippedAndNamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        f.Personas.Remove("Nova");
        int archiveCalls = f.Membership.Calls.Count;

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Equal(["Nova"], outcome.Skipped);
        Assert.Equal([new MembershipCall("Add", Home, "Ada")], f.Membership.Calls.Skip(archiveCalls));
        Assert.DoesNotContain("archivedMembers", File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName)), StringComparison.Ordinal);
    }

    /// <summary>
    /// Archiving records the manager in <c>archivedManager</c> (Home's manager is Ada in the fixture catalog), so unarchive puts Ada
    /// back as <c>manager</c> and removes the key.
    /// </summary>
    [Fact]
    public async Task UnarchiveTeamAsync_RecordedManager_ManagerRestored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);

        _ = await f.Service.UnarchiveTeamAsync(Home, ct);

        string text = File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName));
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        Assert.Equal("Ada", file.Manager);
        Assert.DoesNotContain("archivedManager", text, StringComparison.Ordinal);
    }

    /// <summary>A former manager whose Persona was deleted meanwhile is not restored: <c>manager</c> stays unset and Ada is named in <see cref="ArchiveOutcome.Skipped"/>.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_DeletedFormerManager_ManagerUnsetAndNamedInSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        f.Personas.Remove("Ada");

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Equal(["Ada"], outcome.Skipped);
        string text = File.ReadAllText(Path.Combine(f.TeamFolder, TeamPaths.TeamFileName));
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        Assert.Null(file.Manager);
        Assert.DoesNotContain("archivedManager", text, StringComparison.Ordinal);
    }

    /// <summary>Exactly the Rooms the archive recorded come back; a Room archived by someone else, after the Team went, stays archived, and another Team's Room is untouched.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_Rooms_OnlyRecordedOnesRestored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        Room recorded = await f.CreateRoomAsync(Home, ct);
        Room otherTeam = await f.CreateRoomAsync("Other", ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        // A Team owns one Room, so the unrelated archived Room has no Team; it is not in archivedRooms.
        Room later = await f.Directory.CreateRoomAsync("loose", [KnownIds.Human], null, false, ct: ct);
        await f.Directory.SetRoomArchivedAsync(later.Id, true, ct);
        ForgetHome(f);

        _ = await f.Service.UnarchiveTeamAsync(Home, ct);

        IReadOnlyList<Room> rooms = await f.Directory.GetRoomsAsync(ct);
        Assert.False(rooms.Single(r => r.Id == recorded.Id).Archived);
        Assert.True(rooms.Single(r => r.Id == later.Id).Archived);
        Assert.False(rooms.Single(r => r.Id == otherTeam.Id).Archived);
    }

    /// <summary>T17: a live <c>Teams/Home</c> refuses the unarchive; the live folder, the archive and the labels are exactly as they were.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_LiveFolderExists_RefusedWithT17AndNothingChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        Directory.CreateDirectory(f.TeamFolder);
        File.WriteAllText(Path.Combine(f.TeamFolder, "mine.md"), "a new Team's file");
        Dictionary<string, string> live = ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder);
        Dictionary<string, string> archived = ArchiveTeamTests.Fixture.Snapshot(f.ArchivedFolder);
        string archivedFile = File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName));
        int calls = f.Membership.Calls.Count;

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal(Refused, outcome.Refusal);
        Assert.Equal(live, ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder));
        Assert.Equal(archived, ArchiveTeamTests.Fixture.Snapshot(f.ArchivedFolder));
        Assert.Equal(archivedFile, File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.Equal(calls, f.Membership.Calls.Count);
    }

    /// <summary>Spec 6.7 (ADR-0042): the cadence marks from before the archive are forgotten on unarchive, so the next tick baselines at now instead of catching up.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_Success_ForgetsTheCadenceState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        DateTimeOffset old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        f.Cadences.Set(new CadenceState(Home, old, old, 3));
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        Assert.NotNull(f.Cadences.Get(Home)); // Archiving leaves the marks alone; only the unarchive resets them.

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.Null(f.Cadences.Get(Home));
    }

    /// <summary>T17 names the Team as the folder is spelled on disk, not as the request typed it.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_RequestInOtherCase_T17UsesTheOnDiskSpelling()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        Directory.CreateDirectory(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync("hOME", ct);

        Assert.False(outcome.Done);
        Assert.Equal(Refused, outcome.Refusal); // "Home" is the Archive folder's spelling.
    }

    /// <summary>T17 also covers a live folder spelled in another case: the check lists <c>Teams</c> and compares ignoring case.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_LiveFolderInOtherCase_RefusedWithT17()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        ForgetHome(f);
        Directory.CreateDirectory(Path.Combine(f.TeamsRoot, "HOME"));

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal(Refused, outcome.Refusal);
        Assert.True(File.Exists(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
    }

    /// <summary>T17 also covers a catalog that still knows a label-only Home (no folder): nothing moves and no label is added.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_CatalogKnowsLabelOnlyHome_RefusedWithT17AndNothingChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary(Home, [], ["Nova"], HasFolder: false)];
        Dictionary<string, string> archived = ArchiveTeamTests.Fixture.Snapshot(f.ArchivedFolder);
        string archivedFile = File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName));
        int calls = f.Membership.Calls.Count;

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.False(outcome.Done);
        Assert.Equal(Refused, outcome.Refusal);
        Assert.False(Directory.Exists(f.TeamFolder));
        Assert.Equal(archived, ArchiveTeamTests.Fixture.Snapshot(f.ArchivedFolder));
        Assert.Equal(archivedFile, File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName)));
        Assert.Equal(calls, f.Membership.Calls.Count);
    }

    /// <summary>A Team that was never archived is refused (with some reason) and nothing is created in <c>Teams</c>.</summary>
    [Fact]
    public async Task UnarchiveTeamAsync_NotArchived_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync("Nowhere", ct);

        Assert.False(outcome.Done);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Refusal));
        Assert.False(Directory.Exists(Path.Combine(f.TeamsRoot, "Nowhere")));
        Assert.Empty(f.Membership.Calls);
    }

    /// <summary>After an archive the catalog no longer lists the Team (its labels are gone); the fake is told the same.</summary>
    private static void ForgetHome(ArchiveTeamTests.Fixture f) =>
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
}
