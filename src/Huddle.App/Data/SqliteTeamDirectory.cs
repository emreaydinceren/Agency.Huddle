using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public sealed class SqliteTeamDirectory : ITeamDirectory
{
    private const string DdlCreateUsers = """
        CREATE TABLE IF NOT EXISTS users (
            id          TEXT PRIMARY KEY,
            name        TEXT NOT NULL COLLATE NOCASE UNIQUE,
            kind        TEXT NOT NULL CHECK (kind IN ('human', 'agent')),
            description TEXT NULL
        );
        """;

    private const string DdlCreateRooms = """
        CREATE TABLE IF NOT EXISTS rooms (
            id      TEXT PRIMARY KEY,
            name    TEXT NOT NULL,
            created TEXT NOT NULL
        );
        """;

    private const string DdlCreateRoomMembers = """
        CREATE TABLE IF NOT EXISTS room_members (
            room_id TEXT NOT NULL REFERENCES rooms(id),
            user_id TEXT NOT NULL REFERENCES users(id),
            PRIMARY KEY (room_id, user_id)
        );
        """;

    private const string DdlCreateRoomMembersIndex =
        "CREATE INDEX IF NOT EXISTS ix_room_members_user ON room_members(user_id);";

    private readonly string connectionString;

    public SqliteTeamDirectory(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dbPath = Path.Combine(options.Value.DataDir, "team.db");
        this.connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync(string humanName, CancellationToken ct = default)
    {
        var dbPath = new SqliteConnectionStringBuilder(this.connectionString).DataSource;
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await this.OpenConnectionAsync(ct);

        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", ct);
        await ExecuteNonQueryAsync(connection, DdlCreateUsers, ct);
        await ExecuteNonQueryAsync(connection, DdlCreateRooms, ct);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomMembers, ct);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomMembersIndex, ct);

        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            INSERT INTO users(id, name, kind) VALUES('human', $name, 'human')
            ON CONFLICT(id) DO UPDATE SET name = excluded.name;
            """;
        seed.Parameters.AddWithValue("$name", humanName);
        await seed.ExecuteNonQueryAsync(ct);
    }

    public async Task<User> GetHumanAsync(CancellationToken ct = default)
    {
        var human = await this.GetUserAsync(KnownIds.Human, ct);
        return human ?? throw new InvalidOperationException("The human user has not been seeded. Call InitializeAsync first.");
    }

    public async Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users(id, name, kind, description) VALUES($id, $name, 'agent', $desc)
            ON CONFLICT(name) DO UPDATE SET description = excluded.description WHERE users.kind = 'agent'
            RETURNING id, name, kind, description;
            """;
        command.Parameters.AddWithValue("$id", Guid.CreateVersion7().ToString("N"));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return ReadUser(reader);
    }

    /// <summary>
    /// Renames the <c>users</c> row with id <paramref name="userId"/> to <paramref name="newName"/>,
    /// keeping its id unchanged.
    /// </summary>
    /// <param name="userId">The id of the row to rename.</param>
    /// <param name="newName">
    /// The new Name. Must satisfy <see cref="NameRules.IsValidAgentName(string?)"/>, and must not
    /// already belong to a different Teammate.
    /// </param>
    /// <returns><see langword="true"/> if the row was renamed; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Deliberately synchronous, unlike every other member of this class: its only caller is
    /// <see cref="Agency.Huddle.App.Acp.PersonaRenameCascade"/>, reacting to
    /// <see cref="Agency.Huddle.App.Acp.PersonaStore.PersonaRenamed"/> — a plain
    /// <c>Action&lt;T&gt;</c> event that cannot be awaited. This is the one Team Directory write that
    /// MUST complete before <see cref="Agency.Huddle.App.Acp.PersonaStore.PersonasChanged"/> fires and
    /// <c>PersonaSupervisor</c> starts a runner under the new Name; an async handler could not
    /// guarantee that ordering, and without it the restarted runner's <c>hello</c> mints a brand-new
    /// user id instead of renaming this one (see ADR-0011). <see cref="PersonaModelStore"/> and
    /// <see cref="PersonaEffortStore"/> are the existing precedent for synchronous access to this same
    /// database.
    /// <para>
    /// Returns <see langword="false"/> rather than throwing because the caller is a
    /// file-watcher-driven Persona rename: an exception raised from inside that callback would
    /// either be swallowed silently or kill the debounce timer that invoked it, with nothing
    /// surfaced to a person. The <c>WHERE kind = 'agent'</c> clause reproduces the same guard
    /// <see cref="UpsertAgentUserAsync"/> uses, so the Human's row is never a rename target — a
    /// Persona renamed to the Human's Name must not be able to take over the Human's row and its
    /// Rooms. The uniqueness check excludes the row being renamed by id, not just by name, so a
    /// case-only rename (<c>coo</c> to <c>Coo</c>) is not mistaken for a collision with itself under
    /// the table's <c>COLLATE NOCASE</c> comparison.
    /// </para>
    /// </remarks>
    public bool RenameUser(string userId, string newName)
    {
        if (!NameRules.IsValidAgentName(newName))
        {
            return false;
        }

        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET name = $name
            WHERE id = $id
              AND kind = 'agent'
              AND NOT EXISTS (
                  SELECT 1 FROM users WHERE name = $name COLLATE NOCASE AND id <> $id
              );
            """;
        command.Parameters.AddWithValue("$name", newName);
        command.Parameters.AddWithValue("$id", userId);

        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 1;
    }

    public async Task<User?> GetUserAsync(string id, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, kind, description FROM users WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadUser(reader) : null;
    }

    public async Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, kind, description FROM users WHERE name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadUser(reader) : null;
    }

    /// <summary>
    /// Finds the <c>users</c> row with the given Name, case-insensitively — the synchronous twin of
    /// <see cref="FindUserByNameAsync"/>.
    /// </summary>
    /// <param name="name">The Name to look up.</param>
    /// <returns>The matching <see cref="User"/>, or <see langword="null"/> if none exists.</returns>
    /// <remarks>
    /// Synchronous for the same reason <see cref="RenameUser"/> is: <see cref="Agency.Huddle.App.Acp.PersonaRenameCascade"/>
    /// must find the Agent to rename from inside a plain <c>Action&lt;T&gt;</c> event handler, which
    /// cannot await, rather than blocking on the async version. <see cref="PersonaModelStore"/> and
    /// <see cref="PersonaEffortStore"/> are the existing precedent for synchronous access to this same
    /// database.
    /// </remarks>
    public User? FindUserByName(string name)
    {
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, kind, description FROM users WHERE name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, kind, description FROM users ORDER BY rowid;";

        var users = new List<User>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created FROM rooms ORDER BY created, id;";

        var rooms = new List<Room>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rooms.Add(ReadRoom(reader));
        }

        return rooms;
    }

    public async Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, r.name, r.created FROM rooms r
            JOIN room_members m ON m.room_id = r.id
            WHERE m.user_id = $u
            ORDER BY r.created, r.id;
            """;
        command.Parameters.AddWithValue("$u", userId);

        var rooms = new List<Room>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rooms.Add(ReadRoom(reader));
        }

        return rooms;
    }

    public async Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created FROM rooms WHERE id = $id;";
        command.Parameters.AddWithValue("$id", roomId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRoom(reader) : null;
    }

    public async Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id, u.name, u.kind, u.description
            FROM room_members m
            JOIN users u ON u.id = m.user_id
            WHERE m.room_id = $r
            ORDER BY m.rowid;
            """;
        command.Parameters.AddWithValue("$r", roomId);

        var members = new List<User>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            members.Add(ReadUser(reader));
        }

        return members;
    }

    public async Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memberIds);

        var id = Guid.CreateVersion7().ToString("N");
        var created = DateTimeOffset.UtcNow;

        await using var connection = await this.OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using (var insertRoom = connection.CreateCommand())
        {
            insertRoom.Transaction = transaction;
            insertRoom.CommandText = "INSERT INTO rooms(id, name, created) VALUES($id, $name, $created);";
            insertRoom.Parameters.AddWithValue("$id", id);
            insertRoom.Parameters.AddWithValue("$name", name);
            insertRoom.Parameters.AddWithValue("$created", created.ToString("o", CultureInfo.InvariantCulture));
            await insertRoom.ExecuteNonQueryAsync(ct);
        }

        foreach (var memberId in memberIds)
        {
            await using var insertMember = connection.CreateCommand();
            insertMember.Transaction = transaction;
            insertMember.CommandText = "INSERT INTO room_members(room_id, user_id) VALUES($r, $u);";
            insertMember.Parameters.AddWithValue("$r", id);
            insertMember.Parameters.AddWithValue("$u", memberId);
            await insertMember.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return new Room(id, name, created);
    }

    public async Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO room_members(room_id, user_id) VALUES($r, $u);";
        command.Parameters.AddWithValue("$r", roomId);
        command.Parameters.AddWithValue("$u", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected == 1;
    }

    public async Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE rooms SET name = $name WHERE id = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", roomId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default)
    {
        await using var connection = await this.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, r.name, r.created FROM rooms r
            WHERE EXISTS (SELECT 1 FROM room_members WHERE room_id = r.id AND user_id = $h)
              AND EXISTS (SELECT 1 FROM room_members WHERE room_id = r.id AND user_id = $a)
              AND (SELECT COUNT(*) FROM room_members WHERE room_id = r.id) = 2
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$h", humanId);
        command.Parameters.AddWithValue("$a", agentId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRoom(reader) : null;
    }

    private static User ReadUser(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        var name = reader.GetString(1);
        var kind = reader.GetString(2) switch
        {
            "human" => UserKind.Human,
            "agent" => UserKind.Agent,
            var other => throw new InvalidOperationException($"Unknown user kind '{other}'."),
        };
        var description = reader.IsDBNull(3) ? null : reader.GetString(3);

        return new User(id, name, kind, description);
    }

    private static Room ReadRoom(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        var name = reader.GetString(1);
        var created = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);

        return new Room(id, name, created);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys=ON;", ct);
        return connection;
    }

    /// <summary>
    /// The synchronous twin of <see cref="OpenConnectionAsync"/>, used only by <see cref="RenameUser"/>
    /// and <see cref="FindUserByName"/> — see their remarks for why those two members are synchronous
    /// at all.
    /// </summary>
    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(this.connectionString);
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
        }

        return connection;
    }
}