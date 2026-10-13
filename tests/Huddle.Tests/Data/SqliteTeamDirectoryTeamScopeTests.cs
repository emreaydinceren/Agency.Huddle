using Microsoft.Data.Sqlite;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Tests for the Scope a Room carries (ADR-0052, Team chats spec §6.1 and §6.3): the
/// <c>room_teams</c> sibling table, <see cref="Room.Team"/>, the Scope parameter on the two exact-member
/// lookups, <see cref="ITeamDirectory.GetRoomsForTeamAsync"/> and <see cref="ITeamDirectory.FindTeamRoomAsync"/>.
/// </summary>
public sealed class SqliteTeamDirectoryTeamScopeTests
{
    /// <summary>A Room created with a Team reads back that Team through every read; a root Room reads back null.</summary>
    [Fact]
    public async Task CreateRoom_WithTeam_RoundTripsTeamOnEveryRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);

        Room scoped = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        Room root = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);

        Assert.Equal("Marketing", scoped.Team);
        Assert.Null(root.Team);
        Room? byId = await directory.GetRoomAsync(scoped.Id, ct);
        Assert.Equal("Marketing", byId?.Team);
        IReadOnlyList<Room> all = await directory.GetRoomsAsync(ct);
        Assert.Equal("Marketing", all.Single(r => r.Id == scoped.Id).Team);
        Assert.Null(all.Single(r => r.Id == root.Id).Team);
        IReadOnlyList<Room> forUser = await directory.GetRoomsForUserAsync(nova.Id, ct);
        Assert.Equal("Marketing", forUser.Single(r => r.Id == scoped.Id).Team);
    }

    /// <summary>A root lookup never returns a scoped Room, even when it is the only exact match (T7).</summary>
    [Fact]
    public async Task RootFind_NeverReturnsScopedRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);

        Room? pair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct);
        Room? set = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], ct);
        Room? explicitRootPair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, null, ct);
        Room? explicitRootSet = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], null, ct);

        Assert.Null(pair);
        Assert.Null(set);
        Assert.Null(explicitRootPair);
        Assert.Null(explicitRootSet);
    }

    /// <summary>A Team lookup finds only that Team's Room: not the root one, not another Team's.</summary>
    [Fact]
    public async Task TeamFind_ReturnsOnlyThatTeamsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room root = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        Room sales = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Sales", ct: ct);
        Room marketing = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);

        Room? pair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "Marketing", ct);
        Room? set = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "Marketing", ct);
        Room? rootPair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct);

        Assert.Equal(marketing.Id, pair?.Id);
        Assert.Equal(marketing.Id, set?.Id);
        Assert.Equal(root.Id, rootPair?.Id);
        Assert.NotEqual(sales.Id, pair?.Id);
    }

    /// <summary>A Team lookup with no Room in that Team yields null, not the root Room.</summary>
    [Fact]
    public async Task TeamFind_WithOnlyRootRoom_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);

        Room? pair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "Marketing", ct);
        Room? set = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "Marketing", ct);

        Assert.Null(pair);
        Assert.Null(set);
    }

    /// <summary>The Team match ignores case on both lookups.</summary>
    [Fact]
    public async Task TeamFind_IgnoresCase()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room marketing = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);

        Room? pair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "mARKETING", ct);
        Room? set = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "marketing", ct);

        Assert.Equal(marketing.Id, pair?.Id);
        Assert.Equal(marketing.Id, set?.Id);
    }

    /// <summary>A Team lookup still skips an Archived Room.</summary>
    [Fact]
    public async Task TeamFind_SkipsArchivedRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room marketing = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        await directory.SetRoomArchivedAsync(marketing.Id, true, ct);

        Room? pair = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "Marketing", ct);
        Room? set = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "Marketing", ct);

        Assert.Null(pair);
        Assert.Null(set);
    }

    /// <summary>
    /// <see cref="ITeamDirectory.GetRoomsForTeamAsync"/> lists a Team's Rooms, Archived ones included,
    /// matched ignoring case, and never a root Room or another Team's.
    /// </summary>
    [Fact]
    public async Task GetRoomsForTeam_ListsOnlyThatTeamsRooms_ArchivedIncluded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("root", [KnownIds.Human, nova.Id], ct);
        await directory.CreateRoomAsync("sales", [KnownIds.Human, nova.Id], team: "Sales", ct: ct);
        Room first = await directory.CreateRoomAsync("first", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        await Task.Delay(5, ct);
        Room second = await directory.CreateRoomAsync("second", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        await directory.SetRoomArchivedAsync(second.Id, true, ct);

        IReadOnlyList<Room> rooms = await directory.GetRoomsForTeamAsync("marketing", ct);

        Assert.Equal([first.Id, second.Id], rooms.Select(r => r.Id));
        Assert.False(rooms[0].Archived);
        Assert.True(rooms[1].Archived);
    }

    /// <summary>A Team with no Rooms lists nothing.</summary>
    [Fact]
    public async Task GetRoomsForTeam_UnknownTeam_ReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, _) = await Seed(dir, ct);

        IReadOnlyList<Room> rooms = await directory.GetRoomsForTeamAsync("Marketing", ct);

        Assert.Empty(rooms);
    }

    /// <summary>
    /// <see cref="ITeamDirectory.FindTeamRoomAsync"/> finds the Room marked as the Team Room by its flag,
    /// archived or not, ignoring case, and not a Room that merely shares the Team's name.
    /// </summary>
    [Fact]
    public async Task FindTeamRoom_FindsFlaggedRoom_ArchivedOrNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        Room teamRoom = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);

        Room? live = await directory.FindTeamRoomAsync("marketing", ct);
        await directory.SetRoomArchivedAsync(teamRoom.Id, true, ct);
        Room? archived = await directory.FindTeamRoomAsync("Marketing", ct);

        Assert.Equal(teamRoom.Id, live?.Id);
        Assert.Equal(teamRoom.Id, archived?.Id);
        Assert.True(archived?.Archived);
    }

    /// <summary>With no flagged Room, or for another Team, <see cref="ITeamDirectory.FindTeamRoomAsync"/> yields null.</summary>
    [Fact]
    public async Task FindTeamRoom_NoFlaggedRoom_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        await directory.CreateRoomAsync("Sales", [KnownIds.Human, nova.Id], "Sales", true, ct);

        Room? unflagged = await directory.FindTeamRoomAsync("Marketing", ct);
        Room? unknown = await directory.FindTeamRoomAsync("Support", ct);

        Assert.Null(unflagged);
        Assert.Null(unknown);
    }

    /// <summary>At most one Team Room exists per Team: a second flagged Room for the same Team is refused.</summary>
    [Fact]
    public async Task CreateRoom_SecondTeamRoomForSameTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);

        await Assert.ThrowsAsync<SqliteException>(
            () => directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "MARKETING", true, ct));

        IReadOnlyList<Room> rooms = await directory.GetRoomsForTeamAsync("Marketing", ct);
        Assert.Single(rooms);
    }

    /// <summary>A refused scoped create leaves no half-written Room behind: the Room and its row go in one transaction.</summary>
    [Fact]
    public async Task CreateRoom_SecondTeamRoom_LeavesNoRoomBehind()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);
        int before = (await directory.GetRoomsAsync(ct)).Count;

        await Assert.ThrowsAsync<SqliteException>(
            () => directory.CreateRoomAsync("Again", [KnownIds.Human, nova.Id], "Marketing", true, ct));

        Assert.Equal(before, (await directory.GetRoomsAsync(ct)).Count);
    }

    /// <summary>Deleting a scoped Room removes its <c>room_teams</c> row too, so the delete does not trip the foreign key.</summary>
    [Fact]
    public async Task DeleteRoom_ScopedRoom_RemovesItAndItsScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);
        Room chat = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);

        await directory.DeleteRoomAsync(teamRoom.Id, ct);
        await directory.DeleteRoomAsync(chat.Id, ct);

        Assert.Null(await directory.GetRoomAsync(teamRoom.Id, ct));
        Assert.Null(await directory.FindTeamRoomAsync("Marketing", ct));
        Assert.Empty(await directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    /// <summary>A Room keeps its Scope across an app restart: a second directory over the same file reads it back (T16).</summary>
    [Fact]
    public async Task Scope_SurvivesRestart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);

        SqliteTeamDirectory restarted = new(dir.Options());
        await restarted.InitializeAsync("You", ct);

        Room? found = await restarted.FindTeamRoomAsync("Marketing", ct);
        Assert.Equal(teamRoom.Id, found?.Id);
        Assert.Equal("Marketing", found?.Team);
    }

    /// <summary>
    /// A database from before Team chats has no <c>room_teams</c> table; <see cref="SqliteTeamDirectory.InitializeAsync"/>
    /// creates it, the old Rooms read as root, and a scoped Room can then be created.
    /// </summary>
    [Fact]
    public async Task PreExistingDatabase_WithoutRoomTeamsTable_GainsItOnInitialize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string dbPath = Path.Combine(dir.Path, "team.db");

        await using (SqliteConnection connection = new($"Data Source={dbPath}"))
        {
            await connection.OpenAsync(ct);
            await using SqliteCommand legacy = connection.CreateCommand();
            legacy.CommandText = """
                CREATE TABLE users (id TEXT PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE UNIQUE, kind TEXT NOT NULL CHECK (kind IN ('human', 'agent')), description TEXT NULL);
                CREATE TABLE rooms (id TEXT PRIMARY KEY, name TEXT NOT NULL, created TEXT NOT NULL);
                CREATE TABLE room_members (room_id TEXT NOT NULL REFERENCES rooms(id), user_id TEXT NOT NULL REFERENCES users(id), PRIMARY KEY (room_id, user_id));
                CREATE TABLE archived_rooms (room_id TEXT PRIMARY KEY REFERENCES rooms(id), archived TEXT NOT NULL);
                INSERT INTO rooms(id, name, created) VALUES('legacy-room', 'Old', '2026-01-01T00:00:00.0000000+00:00');
                """;
            await legacy.ExecuteNonQueryAsync(ct);
        }

        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);

        Room? legacyRoom = await directory.GetRoomAsync("legacy-room", ct);
        Assert.NotNull(legacyRoom);
        Assert.Null(legacyRoom.Team);
        Room scoped = await directory.CreateRoomAsync("Marketing", [KnownIds.Human], "Marketing", true, ct);
        Assert.Equal(scoped.Id, (await directory.FindTeamRoomAsync("Marketing", ct))?.Id);
    }

    /// <summary>A Team-scoped exact-member find never returns the Team Room, even when it holds exactly the asked members; a scoped non-team Room is found.</summary>
    [Fact]
    public async Task TeamFind_NeverReturnsTheTeamRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);

        Room? pairBefore = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "Marketing", ct);
        Room? setBefore = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "Marketing", ct);
        Room chat = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        Room? pairAfter = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, "Marketing", ct);
        Room? setAfter = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], "Marketing", ct);

        Assert.Null(pairBefore);
        Assert.Null(setBefore);
        Assert.Equal(chat.Id, pairAfter?.Id);
        Assert.Equal(chat.Id, setAfter?.Id);
    }

    /// <summary>Every read fills <see cref="Room.IsTeamRoom"/> from the Team Room flag: true for the Team Room, false for a scoped chat and a root Room.</summary>
    [Fact]
    public async Task IsTeamRoom_IsFilledByEveryRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (SqliteTeamDirectory directory, User nova) = await Seed(dir, ct);
        Room teamRoom = await directory.CreateRoomAsync("Marketing", [KnownIds.Human, nova.Id], "Marketing", true, ct);
        Room chat = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], team: "Marketing", ct: ct);
        Room root = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);

        Assert.True(teamRoom.IsTeamRoom);
        Assert.False(chat.IsTeamRoom);
        Assert.True((await directory.GetRoomAsync(teamRoom.Id, ct))?.IsTeamRoom);
        Assert.False((await directory.GetRoomAsync(chat.Id, ct))?.IsTeamRoom);
        IReadOnlyList<Room> all = await directory.GetRoomsAsync(ct);
        Assert.True(all.Single(r => r.Id == teamRoom.Id).IsTeamRoom);
        Assert.False(all.Single(r => r.Id == root.Id).IsTeamRoom);
        IReadOnlyList<Room> forUser = await directory.GetRoomsForUserAsync(nova.Id, ct);
        Assert.True(forUser.Single(r => r.Id == teamRoom.Id).IsTeamRoom);
        Assert.False(forUser.Single(r => r.Id == chat.Id).IsTeamRoom);
        IReadOnlyList<Room> forTeam = await directory.GetRoomsForTeamAsync("Marketing", ct);
        Assert.True(forTeam.Single(r => r.Id == teamRoom.Id).IsTeamRoom);
        Assert.False(forTeam.Single(r => r.Id == chat.Id).IsTeamRoom);
        Assert.True((await directory.FindTeamRoomAsync("Marketing", ct))?.IsTeamRoom);
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
