using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Tests for <see cref="SqliteTeamDirectory.RenameTeamScopeAsync"/> and
/// <see cref="SqliteTeamDirectory.RenameProjectScopeAsync"/> (Team rename spec §4, §5.3 step 5 and §5.5).
/// </summary>
public sealed class SqliteTeamDirectoryRenameScopeTests
{
    /// <summary>Every Room of the old Team, archived ones included, is listed under the new name and none under the old.</summary>
    [Fact]
    public async Task RenameTeamScope_MovesEveryRoomIncludingArchived()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);
        Room plain = await directory.CreateRoomAsync("Chat", [KnownIds.Human, nova.Id], team: "Growth", ct: ct);
        Room archived = await directory.CreateRoomAsync("Old", [KnownIds.Human, nova.Id], team: "Growth", ct: ct);
        await directory.SetRoomArchivedAsync(archived.Id, true, ct);

        int moved = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Assert.Equal(3, moved);
        IReadOnlyList<Room> underNew = await directory.GetRoomsForTeamAsync("Reach", ct);
        Assert.Equal(
            new[] { teamRoom.Id, plain.Id, archived.Id }.Order(StringComparer.Ordinal),
            underNew.Select(r => r.Id).Order(StringComparer.Ordinal));
        Assert.Empty(await directory.GetRoomsForTeamAsync("Growth", ct));
        Room? archivedAfter = await directory.GetRoomAsync(archived.Id, ct);
        Assert.True(archivedAfter?.Archived);
        Assert.Equal("Reach", archivedAfter?.Team);
    }

    /// <summary>The Team Room is found under the new name and not the old, and an auto-named one takes the new name.</summary>
    [Fact]
    public async Task RenameTeamScope_AutoNamedTeamRoom_FoundUnderNewNameAndRenamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);

        _ = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Room? found = await directory.FindTeamRoomAsync("Reach", ct);
        Assert.Equal(teamRoom.Id, found?.Id);
        Assert.Equal("Reach", found?.Name);
        Assert.Null(await directory.FindTeamRoomAsync("Growth", ct));
    }

    /// <summary>A Team Room the Human renamed by hand keeps its name (ADR-0011).</summary>
    [Fact]
    public async Task RenameTeamScope_HandRenamedTeamRoom_KeepsItsName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);
        await directory.RenameRoomAsync(teamRoom.Id, "War room", ct);

        _ = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Room? found = await directory.FindTeamRoomAsync("Reach", ct);
        Assert.Equal(teamRoom.Id, found?.Id);
        Assert.Equal("War room", found?.Name);
    }

    /// <summary>The name match for the Team Room is ordinal: a Room named in a different case counts as renamed by hand.</summary>
    [Fact]
    public async Task RenameTeamScope_TeamRoomNameDiffersByCase_KeepsItsName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);

        _ = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Assert.Equal("growth", (await directory.GetRoomAsync(teamRoom.Id, ct))?.Name);
    }

    /// <summary>Another Team's Rooms and a root Room are untouched.</summary>
    [Fact]
    public async Task RenameTeamScope_LeavesOtherTeamsAndRootRoomsAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        _ = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);
        Room sales = await directory.CreateRoomAsync("Sales", [KnownIds.Human, nova.Id], team: "Sales", teamRoom: true, ct: ct);
        Room root = await directory.CreateRoomAsync("Root", [KnownIds.Human, nova.Id], ct);

        _ = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Room? salesAfter = await directory.GetRoomAsync(sales.Id, ct);
        Assert.Equal("Sales", salesAfter?.Team);
        Assert.Equal("Sales", salesAfter?.Name);
        Assert.Equal(sales.Id, (await directory.FindTeamRoomAsync("Sales", ct))?.Id);
        Assert.Null((await directory.GetRoomAsync(root.Id, ct))?.Team);
    }

    /// <summary>A second call finds nothing to move, returns 0 and changes nothing.</summary>
    [Fact]
    public async Task RenameTeamScope_SecondCall_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);
        int first = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        int second = await directory.RenameTeamScopeAsync("Growth", "Reach", ct);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Room? after = await directory.GetRoomAsync(teamRoom.Id, ct);
        Assert.Equal("Reach", after?.Team);
        Assert.Equal("Reach", after?.Name);
    }

    /// <summary>A rename that only changes case moves the rows and renames the auto-named Team Room.</summary>
    [Fact]
    public async Task RenameTeamScope_CaseOnly_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Growth", [KnownIds.Human, nova.Id], team: "Growth", teamRoom: true, ct: ct);

        int moved = await directory.RenameTeamScopeAsync("Growth", "growth", ct);

        Assert.Equal(1, moved);
        Room? after = await directory.GetRoomAsync(teamRoom.Id, ct);
        Assert.Equal("growth", after?.Team);
        Assert.Equal("growth", after?.Name);
    }

    /// <summary>A Project rename touches only that Team's Rooms; the same Project name in another Team stays.</summary>
    [Fact]
    public async Task RenameProjectScope_IsScopedToItsTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room mine = await directory.CreateRoomAsync("A", [KnownIds.Human, nova.Id], team: "Growth", project: "Launch", ct: ct);
        Room other = await directory.CreateRoomAsync("B", [KnownIds.Human, nova.Id], team: "Sales", project: "Launch", ct: ct);
        Room different = await directory.CreateRoomAsync("C", [KnownIds.Human, nova.Id], team: "Growth", project: "Other", ct: ct);

        int moved = await directory.RenameProjectScopeAsync("Growth", "Launch", "Debut", ct);

        Assert.Equal(1, moved);
        Assert.Equal("Debut", (await directory.GetRoomAsync(mine.Id, ct))?.Project);
        Assert.Equal("Launch", (await directory.GetRoomAsync(other.Id, ct))?.Project);
        Assert.Equal("Other", (await directory.GetRoomAsync(different.Id, ct))?.Project);
    }

    /// <summary>A second Project rename call returns 0 and changes nothing.</summary>
    [Fact]
    public async Task RenameProjectScope_SecondCall_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room room = await directory.CreateRoomAsync("A", [KnownIds.Human, nova.Id], team: "Growth", project: "Launch", ct: ct);
        _ = await directory.RenameProjectScopeAsync("Growth", "Launch", "Debut", ct);

        int second = await directory.RenameProjectScopeAsync("Growth", "Launch", "Debut", ct);

        Assert.Equal(0, second);
        Assert.Equal("Debut", (await directory.GetRoomAsync(room.Id, ct))?.Project);
    }

    private static async Task<(SqliteTeamDirectory Directory, User Nova)> Seed(TempDataDir dir, CancellationToken ct)
    {
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        return (directory, nova);
    }
}
