using Microsoft.Data.Sqlite;
using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Data;

public sealed class SqliteTeamDirectoryTests
{
    [Fact]
    public async Task Initialize_IsIdempotent_AndSeedsHuman()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        await directory.InitializeAsync("Someone Else", ct);

        var human = await directory.GetHumanAsync(ct);
        Assert.Equal(KnownIds.Human, human.Id);
        Assert.Equal("Someone Else", human.Name);

        var users = await directory.GetUsersAsync(ct);
        Assert.Single(users);
    }

    [Fact]
    public async Task UpsertAgent_ReturnsAgentUser_WithGeneratedId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var agent = await directory.UpsertAgentUserAsync("echo", "an echo agent", ct);

        Assert.NotNull(agent);
        Assert.Equal(UserKind.Agent, agent.Kind);
        Assert.False(string.IsNullOrWhiteSpace(agent.Id));
        Assert.Equal("an echo agent", agent.Description);
    }

    [Fact]
    public async Task UpsertAgent_SameNameDifferentCase_ReturnsSameId_UpdatesDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var first = await directory.UpsertAgentUserAsync("Echo", "first description", ct);
        var second = await directory.UpsertAgentUserAsync("echo", "second description", ct);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("second description", second.Description);
    }

    [Fact]
    public async Task UpsertAgent_WithHumanName_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var result = await directory.UpsertAgentUserAsync("You", null, ct);

        Assert.Null(result);
        var human = await directory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
        Assert.Equal(UserKind.Human, human.Kind);
    }

    [Fact]
    public async Task FindUserByName_IsCaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);

        var found = await directory.FindUserByNameAsync("ECHO", ct);

        Assert.NotNull(found);
        Assert.Equal(agent.Id, found.Id);
    }

    /// <summary>Renaming an Agent keeps its id, and the new Name resolves where the old one did.</summary>
    [Fact]
    public async Task RenameUser_Agent_KeepsId_AndUpdatesNameLookup()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", "an echo agent", ct);
        Assert.NotNull(echo);

        var renamed = directory.RenameUser(echo.Id, "echoprime");

        Assert.True(renamed);
        var byNewName = await directory.FindUserByNameAsync("echoprime", ct);
        Assert.NotNull(byNewName);
        Assert.Equal(echo.Id, byNewName.Id);
        var byOldName = await directory.FindUserByNameAsync("echo", ct);
        Assert.Null(byOldName);
    }

    /// <summary>A rename that changes only casing succeeds and persists the new casing.</summary>
    [Fact]
    public async Task RenameUser_CaseOnlyChange_PersistsNewCasing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var coo = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(coo);

        var renamed = directory.RenameUser(coo.Id, "Coo");

        Assert.True(renamed);
        var stored = await directory.GetUserAsync(coo.Id, ct);
        Assert.NotNull(stored);
        Assert.Equal("Coo", stored.Name);
    }

    /// <summary>Renaming a Teammate to the Name it already has is a no-op that reports success.</summary>
    [Fact]
    public async Task RenameUser_SameName_IsNoOpAndReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);

        var renamed = directory.RenameUser(echo.Id, "echo");

        Assert.True(renamed);
        var stored = await directory.GetUserAsync(echo.Id, ct);
        Assert.NotNull(stored);
        Assert.Equal("echo", stored.Name);
    }

    /// <summary>There is no row for an unknown id, so the rename reports failure without throwing.</summary>
    [Fact]
    public async Task RenameUser_UnknownId_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var renamed = directory.RenameUser("no-such-id", "newname");

        Assert.False(renamed);
    }

    /// <summary>The Human's row is never a rename target, matching the guard on UpsertAgentUserAsync.</summary>
    [Fact]
    public async Task RenameUser_HumanId_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var renamed = directory.RenameUser(KnownIds.Human, "SomeoneElse");

        Assert.False(renamed);
        var human = await directory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
    }

    /// <summary>A new Name already held by a different Teammate is rejected rather than colliding.</summary>
    [Fact]
    public async Task RenameUser_NameTakenByDifferentUser_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);

        var renamed = directory.RenameUser(echo.Id, "alpha");

        Assert.False(renamed);
        var stillEcho = await directory.GetUserAsync(echo.Id, ct);
        Assert.NotNull(stillEcho);
        Assert.Equal("echo", stillEcho.Name);
    }

    /// <summary>A new Name that fails NameRules.IsValidAgentName is rejected without touching the row.</summary>
    [Fact]
    public async Task RenameUser_InvalidNewName_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);

        var renamed = directory.RenameUser(echo.Id, " bad name ");

        Assert.False(renamed);
        var stillEcho = await directory.GetUserAsync(echo.Id, ct);
        Assert.NotNull(stillEcho);
        Assert.Equal("echo", stillEcho.Name);
    }

    /// <summary>Renaming a Teammate leaves its Room memberships intact under the same id.</summary>
    [Fact]
    public async Task RenameUser_Agent_PreservesRoomMembershipAndRoomsForUser()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);

        var renamed = directory.RenameUser(echo.Id, "echoprime");

        Assert.True(renamed);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, member => member.Id == echo.Id && member.Name == "echoprime");
        var rooms = await directory.GetRoomsForUserAsync(echo.Id, ct);
        Assert.Single(rooms);
        Assert.Equal(room.Id, rooms[0].Id);
    }

    [Fact]
    public async Task CreateRoom_AddMember_GetMembers_InInsertionOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);

        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        await directory.AddMemberAsync(room.Id, alpha.Id, ct);

        var members = await directory.GetRoomMembersAsync(room.Id, ct);

        Assert.Equal(3, members.Count);
        Assert.Equal(KnownIds.Human, members[0].Id);
        Assert.Equal(echo.Id, members[1].Id);
        Assert.Equal(alpha.Id, members[2].Id);
    }

    [Fact]
    public async Task AddMember_Twice_ReturnsFalseSecondTime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        var first = await directory.AddMemberAsync(room.Id, echo.Id, ct);
        var second = await directory.AddMemberAsync(room.Id, echo.Id, ct);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task GetRoomsForUser_ReturnsOnlyMemberRooms()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);
        var echoRoom = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        await directory.CreateRoomAsync("alpha", [KnownIds.Human, alpha.Id], ct);

        var rooms = await directory.GetRoomsForUserAsync(echo.Id, ct);

        Assert.Single(rooms);
        Assert.Equal(echoRoom.Id, rooms[0].Id);
    }

    [Fact]
    public async Task RenameRoom_UpdatesName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        await directory.RenameRoomAsync(room.Id, "echo, alpha", ct);

        var updated = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("echo, alpha", updated.Name);
    }

    [Fact]
    public async Task FindDirectRoom_FoundOnlyWhenExactlyTwoMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);

        var missing = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.Null(missing);

        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var found = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.NotNull(found);
        Assert.Equal(room.Id, found.Id);

        await directory.AddMemberAsync(room.Id, alpha.Id, ct);
        var afterThirdMember = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.Null(afterThirdMember);
    }

    /// <summary>A newly created Room is not archived by default.</summary>
    [Fact]
    public async Task CreateRoom_IsNotArchived()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        Assert.False(room.Archived);
        var stored = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(stored);
        Assert.False(stored.Archived);
    }

    /// <summary>
    /// Archiving a Room is reported by both GetRoomsAsync and GetRoomAsync - the two doors the sidebar
    /// and the Room view each read through.
    /// </summary>
    [Fact]
    public async Task SetRoomArchived_True_ReportsArchivedEverywhere()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        await directory.SetRoomArchivedAsync(room.Id, true, ct);

        var single = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(single);
        Assert.True(single.Archived);
        var all = await directory.GetRoomsAsync(ct);
        var found = Assert.Single(all, r => r.Id == room.Id);
        Assert.True(found.Archived);
    }

    /// <summary>Unarchiving a Room reverses the archive, rather than merely toggling a fixed flag.</summary>
    [Fact]
    public async Task SetRoomArchived_FalseAfterTrue_ReportsUnarchived()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        await directory.SetRoomArchivedAsync(room.Id, true, ct);

        await directory.SetRoomArchivedAsync(room.Id, false, ct);

        var stored = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(stored);
        Assert.False(stored.Archived);
    }

    /// <summary>
    /// DeleteRoomAsync must remove the room_members rows too, or an orphan membership row survives a
    /// Room that no longer exists. Asserting only that GetRoomAsync returns null would still pass with
    /// those rows left behind, so this opens the database directly to prove they are really gone.
    /// </summary>
    [Fact]
    public async Task DeleteRoom_RemovesRoomAndItsMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);

        await directory.DeleteRoomAsync(room.Id, ct);

        var stored = await directory.GetRoomAsync(room.Id, ct);
        Assert.Null(stored);

        var dbPath = Path.Combine(dir.Path, "team.db");
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM room_members WHERE room_id = $id;";
        command.Parameters.AddWithValue("$id", room.Id);
        var count = (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
        Assert.Equal(0, count);
    }

    /// <summary>Deleting a Room that does not exist is a silent no-op, matching RenameRoomAsync.</summary>
    [Fact]
    public async Task DeleteRoom_UnknownId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        await directory.DeleteRoomAsync("no-such-room", ct);

        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Empty(rooms);
    }

    /// <summary>
    /// An archived 1:1 Room must not be reused: EnsureRoomForAsync's caller expects a fresh Room once
    /// the old one is archived away, not a resurrected archived one.
    /// </summary>
    [Fact]
    public async Task FindDirectRoom_ArchivedOnlyMatch_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        await directory.SetRoomArchivedAsync(room.Id, true, ct);

        var found = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);

        Assert.Null(found);
    }

    /// <summary>
    /// The whole justification for the sibling-table design: an existing team.db that predates
    /// archived_rooms - created with only the original three tables, exactly as a database from before
    /// this feature would look - must still open, initialize and support archiving without requiring
    /// anyone to delete it. `CREATE TABLE IF NOT EXISTS archived_rooms` creates cleanly here precisely
    /// because it is a new table rather than a column added to an existing one.
    /// </summary>
    [Fact]
    public async Task PreExistingDatabase_WithoutArchivedRoomsTable_StillSupportsArchiving()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var dbPath = Path.Combine(dir.Path, "team.db");
        var roomId = "legacy-room";

        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync(ct);
            await using (var users = connection.CreateCommand())
            {
                users.CommandText = """
                    CREATE TABLE IF NOT EXISTS users (
                        id          TEXT PRIMARY KEY,
                        name        TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        kind        TEXT NOT NULL CHECK (kind IN ('human', 'agent')),
                        description TEXT NULL
                    );
                    """;
                await users.ExecuteNonQueryAsync(ct);
            }

            await using (var rooms = connection.CreateCommand())
            {
                rooms.CommandText = """
                    CREATE TABLE IF NOT EXISTS rooms (
                        id      TEXT PRIMARY KEY,
                        name    TEXT NOT NULL,
                        created TEXT NOT NULL
                    );
                    """;
                await rooms.ExecuteNonQueryAsync(ct);
            }

            await using (var roomMembers = connection.CreateCommand())
            {
                roomMembers.CommandText = """
                    CREATE TABLE IF NOT EXISTS room_members (
                        room_id TEXT NOT NULL REFERENCES rooms(id),
                        user_id TEXT NOT NULL REFERENCES users(id),
                        PRIMARY KEY (room_id, user_id)
                    );
                    """;
                await roomMembers.ExecuteNonQueryAsync(ct);
            }

            await using (var seedHuman = connection.CreateCommand())
            {
                seedHuman.CommandText = "INSERT INTO users(id, name, kind) VALUES('human', 'You', 'human');";
                await seedHuman.ExecuteNonQueryAsync(ct);
            }

            await using (var insertRoom = connection.CreateCommand())
            {
                insertRoom.CommandText = "INSERT INTO rooms(id, name, created) VALUES($id, $name, $created);";
                insertRoom.Parameters.AddWithValue("$id", roomId);
                insertRoom.Parameters.AddWithValue("$name", "legacy");
                insertRoom.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                await insertRoom.ExecuteNonQueryAsync(ct);
            }
        }

        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        await directory.SetRoomArchivedAsync(roomId, true, ct);

        var stored = await directory.GetRoomAsync(roomId, ct);
        Assert.NotNull(stored);
        Assert.True(stored.Archived);
        var all = await directory.GetRoomsAsync(ct);
        var found = Assert.Single(all, r => r.Id == roomId);
        Assert.True(found.Archived);
    }
}