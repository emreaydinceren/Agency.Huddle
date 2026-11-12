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

    // A sibling table, not a column on `rooms`: every DDL statement in this class is
    // `CREATE TABLE IF NOT EXISTS`, which silently does nothing to a table that already exists, so an
    // existing team.db would never gain an `archived` column. A new table creates cleanly on an
    // existing database instead. See PersonaModelStore for the same pattern and its fuller rationale.
    // The presence of a room_id row means "archived"; its absence means "not archived".
    private const string DdlCreateArchivedRooms = """
        CREATE TABLE IF NOT EXISTS archived_rooms (
            room_id  TEXT PRIMARY KEY REFERENCES rooms(id),
            archived TEXT NOT NULL
        );
        """;

    // Same reasoning as archived_rooms above: a sibling table so an existing team.db gains Scope by a
    // plain CREATE TABLE IF NOT EXISTS. No row means a root Room (ADR-0052). team_room = 1 marks a
    // Team's Team Room, and the unique partial index allows at most one per Team.
    private const string DdlCreateRoomTeams = """
        CREATE TABLE IF NOT EXISTS room_teams (
            room_id   TEXT PRIMARY KEY REFERENCES rooms(id),
            team      TEXT NOT NULL COLLATE NOCASE,
            team_room INTEGER NOT NULL DEFAULT 0
        );
        """;

    private const string DdlCreateRoomTeamsIndex =
        "CREATE INDEX IF NOT EXISTS ix_room_teams_team ON room_teams(team);";

    private const string DdlCreateRoomTeamsTeamRoomIndex =
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_room_teams_team_room ON room_teams(team) WHERE team_room = 1;";

    // Same reasoning as room_teams above: a sibling table so an existing team.db gains Project by a plain
    // CREATE TABLE IF NOT EXISTS. No row means a Room with no Project; a row is only ever written for a Team Room.
    private const string DdlCreateRoomProjects = """
        CREATE TABLE IF NOT EXISTS room_projects (
            room_id TEXT PRIMARY KEY REFERENCES rooms(id),
            project TEXT NOT NULL COLLATE NOCASE
        );
        """;

    /// <summary>
    /// Marks a Job Room: a row means the Room belongs to that Job. A sibling table for the same reason as the two above. Created
    /// here as well as by <see cref="JobRoomStore"/>, because the one-to-one lookups below exclude its Rooms and so read it.
    /// </summary>
    internal const string DdlCreateRoomJobs = """
        CREATE TABLE IF NOT EXISTS room_jobs (
            room_id TEXT PRIMARY KEY REFERENCES rooms(id),
            job_id  TEXT NOT NULL UNIQUE
        );
        """;

    /// <summary>
    /// The Room projection and its two LEFT JOINs, ending in a newline so a <c>WHERE</c>, extra
    /// <c>JOIN</c> or <c>ORDER BY</c> can be appended directly. <see cref="ReadRoom"/> reads these
    /// eight columns by ordinal, so the column order here and there must change together.
    /// </summary>
    private const string RoomSelect = """
        SELECT r.id, r.name, r.created, a.room_id IS NOT NULL, t.team, COALESCE(t.team_room, 0), a.archived, p.project
        FROM rooms r
        LEFT JOIN archived_rooms a ON a.room_id = r.id
        LEFT JOIN room_teams t ON t.room_id = r.id
        LEFT JOIN room_projects p ON p.room_id = r.id

        """;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    /// <param name="options">Supplies the data directory the database lives in.</param>
    /// <param name="timeProvider">
    /// The clock stamped onto a new Room and an archive. Optional and last so every earlier caller
    /// compiles unchanged; <see langword="null"/> uses <see cref="TimeProvider.System"/>.
    /// </param>
    public SqliteTeamDirectory(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dbPath = Path.Combine(options.Value.DataDir, "team.db");
        this.connectionString = $"Data Source={dbPath}";
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InitializeAsync(string humanName, CancellationToken ct = default)
    {
        var dbPath = new SqliteConnectionStringBuilder(this.connectionString).DataSource;
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateUsers, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRooms, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomMembers, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomMembersIndex, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateArchivedRooms, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomTeams, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomTeamsIndex, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomTeamsTeamRoomIndex, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomProjects, ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, DdlCreateRoomJobs, ct).ConfigureAwait(false);

        // A Name already held by an Agent row is skipped rather than allowed to throw on the unique
        // index: the Human keeps the Name they had, and startup goes on. On a fresh database there
        // are no other rows, so the Human's row is always created.
        var seed = connection.CreateCommand();
        await using var seedScope = seed.ConfigureAwait(false);
        seed.CommandText = """
            INSERT INTO users(id, name, kind)
            SELECT 'human', $name, 'human'
            WHERE NOT EXISTS (SELECT 1 FROM users WHERE name = $name COLLATE NOCASE AND id <> 'human')
            ON CONFLICT(id) DO UPDATE SET name = excluded.name;
            """;
        seed.Parameters.AddWithValue("$name", humanName);
        await seed.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<User> GetHumanAsync(CancellationToken ct = default)
    {
        var human = await this.GetUserAsync(KnownIds.Human, ct).ConfigureAwait(false);
        return human ?? throw new InvalidOperationException("The human user has not been seeded. Call InitializeAsync first.");
    }

    /// <inheritdoc />
    public async Task<string?> ReadHumanNameAsync(CancellationToken ct = default)
    {
        var dbPath = new SqliteConnectionStringBuilder(this.connectionString).DataSource;
        if (!File.Exists(dbPath))
        {
            return null;
        }

        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var exists = connection.CreateCommand();
        await using var existsScope = exists.ConfigureAwait(false);
        exists.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'users';";
        if (await exists.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
        {
            return null;
        }

        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = "SELECT name FROM users WHERE id = 'human';";
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }

    /// <inheritdoc />
    public bool RenameHuman(string newName)
    {
        if (!NameRules.IsValidAgentName(newName))
        {
            return false;
        }

        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET name = $name
            WHERE id = 'human'
              AND kind = 'human'
              AND NOT EXISTS (
                  SELECT 1 FROM users WHERE name = $name COLLATE NOCASE AND id <> 'human'
              );
            """;
        command.Parameters.AddWithValue("$name", newName);
        return command.ExecuteNonQuery() == 1;
    }

    public Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default) =>
        this.QuerySingleAsync(
            """
            INSERT INTO users(id, name, kind, description) VALUES($id, $name, 'agent', $desc)
            ON CONFLICT(name) DO UPDATE SET description = excluded.description WHERE users.kind = 'agent'
            RETURNING id, name, kind, description;
            """,
            command =>
            {
                command.Parameters.AddWithValue("$id", Guid.CreateVersion7().ToString("N"));
                command.Parameters.AddWithValue("$name", name);
                command.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
            },
            ReadUser,
            ct);

    /// <summary>
    /// Renames the <c>users</c> row with id <paramref name="userId"/> to <paramref name="newName"/>,
    /// keeping its id unchanged.
    /// </summary>
    /// <param name="userId">The id of the row to rename.</param>
    /// <param name="newName">
    /// The new Name. Must satisfy <see cref="NameRules.IsValidAgentName(string?)"/>, and must not
    /// already belong to a different Agent.
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

    public Task<User?> GetUserAsync(string id, CancellationToken ct = default) =>
        this.QuerySingleAsync(
            "SELECT id, name, kind, description FROM users WHERE id = $id;",
            command => command.Parameters.AddWithValue("$id", id),
            ReadUser,
            ct);

    public Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default) =>
        this.QuerySingleAsync(
            "SELECT id, name, kind, description FROM users WHERE name = $name COLLATE NOCASE;",
            command => command.Parameters.AddWithValue("$name", name),
            ReadUser,
            ct);

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

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default) =>
        await this.QueryAsync(
            "SELECT id, name, kind, description FROM users ORDER BY rowid;",
            null,
            ReadUser,
            ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default) =>
        await this.QueryAsync(
            RoomSelect + "ORDER BY r.created, r.id;",
            null,
            ReadRoom,
            ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default) =>
        await this.QueryAsync(
            RoomSelect + """
            JOIN room_members m ON m.room_id = r.id
            WHERE m.user_id = $u
            ORDER BY r.created, r.id;
            """,
            command => command.Parameters.AddWithValue("$u", userId),
            ReadRoom,
            ct).ConfigureAwait(false);

    public Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default) =>
        this.QuerySingleAsync(
            RoomSelect + "WHERE r.id = $id;",
            command => command.Parameters.AddWithValue("$id", roomId),
            ReadRoom,
            ct);

    public async Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default) =>
        await this.QueryAsync(
            """
            SELECT u.id, u.name, u.kind, u.description
            FROM room_members m
            JOIN users u ON u.id = m.user_id
            WHERE m.room_id = $r
            ORDER BY m.rowid;
            """,
            command => command.Parameters.AddWithValue("$r", roomId),
            ReadUser,
            ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<User>>> GetMembersByRoomAsync(CancellationToken ct = default)
    {
        // ReadUser reads columns 0-3 positionally, so the Room id goes last.
        List<(string RoomId, User Member)> rows = await this.QueryAsync(
            """
            SELECT u.id, u.name, u.kind, u.description, m.room_id
            FROM room_members m
            JOIN users u ON u.id = m.user_id
            ORDER BY m.rowid;
            """,
            null,
            static reader => (reader.GetString(4), ReadUser(reader)),
            ct).ConfigureAwait(false);

        Dictionary<string, IReadOnlyList<User>> byRoom = new(StringComparer.Ordinal);
        foreach (IGrouping<string, (string RoomId, User Member)> room in rows.GroupBy(static row => row.RoomId, StringComparer.Ordinal))
        {
            byRoom[room.Key] = [.. room.Select(static row => row.Member)];
        }

        return byRoom;
    }

    public Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default) =>
        this.CreateRoomAsync(name, memberIds, null, false, null, ct);

    /// <inheritdoc />
    public async Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, string? team, bool teamRoom = false, string? project = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        if (project is not null && team is null)
        {
            throw new ArgumentException("A Project is only valid for a Room in a Team.", nameof(project));
        }

        var id = Guid.CreateVersion7().ToString("N");
        var created = this.timeProvider.GetUtcNow();

        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);

        var insertRoom = connection.CreateCommand();
        await using (insertRoom.ConfigureAwait(false))
        {
            insertRoom.Transaction = transaction;
            insertRoom.CommandText = "INSERT INTO rooms(id, name, created) VALUES($id, $name, $created);";
            insertRoom.Parameters.AddWithValue("$id", id);
            insertRoom.Parameters.AddWithValue("$name", name);
            insertRoom.Parameters.AddWithValue("$created", created.ToString("o", CultureInfo.InvariantCulture));
            await insertRoom.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        foreach (var memberId in memberIds)
        {
            var insertMember = connection.CreateCommand();
            await using var insertMemberScope = insertMember.ConfigureAwait(false);
            insertMember.Transaction = transaction;
            insertMember.CommandText = "INSERT INTO room_members(room_id, user_id) VALUES($r, $u);";
            insertMember.Parameters.AddWithValue("$r", id);
            insertMember.Parameters.AddWithValue("$u", memberId);
            await insertMember.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (team is not null)
        {
            var insertTeam = connection.CreateCommand();
            await using var insertTeamScope = insertTeam.ConfigureAwait(false);
            insertTeam.Transaction = transaction;
            insertTeam.CommandText = "INSERT INTO room_teams(room_id, team, team_room) VALUES($r, $team, $teamRoom);";
            insertTeam.Parameters.AddWithValue("$r", id);
            insertTeam.Parameters.AddWithValue("$team", team);
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
            insertTeam.Parameters.AddWithValue("$teamRoom", teamRoom ? 1 : 0);
#pragma warning restore HAA0601
            await insertTeam.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (project is not null)
        {
            await InsertRoomProjectAsync(connection, transaction, id, project, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return new Room(id, name, created) { Team = team, Project = project, IsTeamRoom = team is not null && teamRoom };
    }

    private static async Task InsertRoomProjectAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, string project, CancellationToken ct)
    {
        var insertProject = connection.CreateCommand();
        await using var insertProjectScope = insertProject.ConfigureAwait(false);
        insertProject.Transaction = transaction;
        insertProject.CommandText = "INSERT INTO room_projects(room_id, project) VALUES($r, $project);";
        insertProject.Parameters.AddWithValue("$r", roomId);
        insertProject.Parameters.AddWithValue("$project", project);
        await insertProject.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Room>> GetRoomsForTeamAsync(string team, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        return await this.QueryAsync(
            RoomSelect + """
            WHERE t.team = $team
            ORDER BY r.created, r.id;
            """,
            command => command.Parameters.AddWithValue("$team", team),
            ReadRoom,
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Room?> FindTeamRoomAsync(string team, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        return this.QuerySingleAsync(
            RoomSelect + """
            WHERE t.team = $team AND t.team_room = 1
            LIMIT 1;
            """,
            command => command.Parameters.AddWithValue("$team", team),
            ReadRoom,
            ct);
    }

    public async Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default)
    {
        var rowsAffected = await this.ExecuteAsync(
            "INSERT OR IGNORE INTO room_members(room_id, user_id) VALUES($r, $u);",
            command =>
            {
                command.Parameters.AddWithValue("$r", roomId);
                command.Parameters.AddWithValue("$u", userId);
            },
            ct).ConfigureAwait(false);
        return rowsAffected == 1;
    }

    public async Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default) =>
        _ = await this.ExecuteAsync(
            "UPDATE rooms SET name = $name WHERE id = $id;",
            command =>
            {
                command.Parameters.AddWithValue("$name", name);
                command.Parameters.AddWithValue("$id", roomId);
            },
            ct).ConfigureAwait(false);

    public Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default) =>
        this.FindRoomWithExactMembersAsync(humanId, agentId, null, null, ct);

    /// <inheritdoc />
    public Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, string? team, string? project = null, CancellationToken ct = default) =>
        this.QuerySingleAsync(
            RoomSelect + """
            WHERE EXISTS (SELECT 1 FROM room_members WHERE room_id = r.id AND user_id = $h)
              AND EXISTS (SELECT 1 FROM room_members WHERE room_id = r.id AND user_id = $ag)
              AND (SELECT COUNT(*) FROM room_members WHERE room_id = r.id) = 2
              AND NOT EXISTS (SELECT 1 FROM archived_rooms WHERE room_id = r.id)
              AND NOT EXISTS (SELECT 1 FROM room_jobs WHERE room_id = r.id)
              AND (($team IS NULL AND t.room_id IS NULL) OR (t.team = $team AND COALESCE(t.team_room, 0) = 0 AND (($project IS NULL AND p.room_id IS NULL) OR p.project = $project)))
            ORDER BY r.created, r.id
            LIMIT 1;
            """,
            command =>
            {
                command.Parameters.AddWithValue("$h", humanId);
                command.Parameters.AddWithValue("$ag", agentId);
                command.Parameters.AddWithValue("$team", (object?)team ?? DBNull.Value);
                command.Parameters.AddWithValue("$project", (object?)project ?? DBNull.Value);
            },
            ReadRoom,
            ct);

    /// <inheritdoc />
    public Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default) =>
        this.FindRoomWithExactMemberSetAsync(memberIds, null, null, ct);

    /// <inheritdoc />
    public async Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, string? team, string? project = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memberIds);

        var distinctIds = memberIds.Distinct(StringComparer.Ordinal).ToList();
        if (distinctIds.Count == 0)
        {
            return null;
        }

        var placeholders = new List<string>(distinctIds.Count);
        for (var i = 0; i < distinctIds.Count; i++)
        {
            placeholders.Add(string.Create(CultureInfo.InvariantCulture, $"$p{i}"));
        }

        var inClause = string.Join(", ", placeholders);
        var sql = string.Create(CultureInfo.InvariantCulture, $"""
            {RoomSelect}WHERE (SELECT COUNT(*) FROM room_members m WHERE m.room_id = r.id) = $count
              AND (SELECT COUNT(*) FROM room_members m WHERE m.room_id = r.id AND m.user_id IN ({inClause})) = $count
              AND NOT EXISTS (SELECT 1 FROM archived_rooms WHERE room_id = r.id)
              AND NOT EXISTS (SELECT 1 FROM room_jobs WHERE room_id = r.id)
              AND (($team IS NULL AND t.room_id IS NULL) OR (t.team = $team AND COALESCE(t.team_room, 0) = 0 AND (($project IS NULL AND p.room_id IS NULL) OR p.project = $project)))
            ORDER BY r.created, r.id
            LIMIT 1;
            """);

        return await this.QuerySingleAsync(
            sql,
            command =>
            {
                for (var i = 0; i < distinctIds.Count; i++)
                {
                    command.Parameters.AddWithValue(placeholders[i], distinctIds[i]);
                }

#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
                command.Parameters.AddWithValue("$count", distinctIds.Count);
#pragma warning restore HAA0601
                command.Parameters.AddWithValue("$team", (object?)team ?? DBNull.Value);
                command.Parameters.AddWithValue("$project", (object?)project ?? DBNull.Value);
            },
            ReadRoom,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Archives or unarchives a Room by adding or removing its row in <c>archived_rooms</c>. Archiving
    /// is a display filter only - see <see cref="Room.Archived"/> - so this never touches
    /// <c>rooms</c> or <c>room_members</c>.
    /// </summary>
    /// <param name="roomId">The Room to archive or unarchive.</param>
    /// <param name="archived">
    /// <see langword="true"/> to archive the Room, recording the current instant; <see langword="false"/>
    /// to unarchive it.
    /// </param>
    /// <param name="ct">Cancels the write.</param>
    public async Task SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default)
    {
        var sql = archived
            ? "INSERT OR REPLACE INTO archived_rooms(room_id, archived) VALUES($id, $at);"
            : "DELETE FROM archived_rooms WHERE room_id = $id;";
        _ = await this.ExecuteAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("$id", roomId);
                if (archived)
                {
                    command.Parameters.AddWithValue("$at", this.timeProvider.GetUtcNow().ToString("o", CultureInfo.InvariantCulture));
                }
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Permanently deletes a Room: its <c>room_members</c> rows, its <c>archived_rooms</c> row (if any)
    /// and finally the <c>rooms</c> row itself, in one transaction. Deletes in that order because
    /// <c>room_members.room_id</c> and <c>archived_rooms.room_id</c> both reference <c>rooms(id)</c>
    /// with no <c>ON DELETE CASCADE</c>, and this connection runs with <c>PRAGMA foreign_keys=ON</c>,
    /// so deleting the Room first would throw.
    /// </summary>
    /// <param name="roomId">The Room to delete.</param>
    /// <param name="ct">Cancels the transaction.</param>
    /// <remarks>
    /// Deleting an unknown id is a silent no-op, matching <see cref="RenameRoomAsync"/>. This method
    /// does not delete the Room's transcript file - that lives outside SQLite in <see cref="IChatStore"/>
    /// and is the caller's responsibility, so an orphan transcript is never left behind only when both
    /// are called.
    /// </remarks>
    public async Task DeleteRoomAsync(string roomId, CancellationToken ct = default)
    {
        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);

        var deleteMembers = connection.CreateCommand();
        await using (deleteMembers.ConfigureAwait(false))
        {
            deleteMembers.Transaction = transaction;
            deleteMembers.CommandText = "DELETE FROM room_members WHERE room_id = $id;";
            deleteMembers.Parameters.AddWithValue("$id", roomId);
            await deleteMembers.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await DeleteRoomProjectAsync(connection, transaction, roomId, ct).ConfigureAwait(false);

        var deleteScope = connection.CreateCommand();
        await using (deleteScope.ConfigureAwait(false))
        {
            deleteScope.Transaction = transaction;
            deleteScope.CommandText = "DELETE FROM room_teams WHERE room_id = $id;";
            deleteScope.Parameters.AddWithValue("$id", roomId);
            await deleteScope.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var deleteJob = connection.CreateCommand();
        await using (deleteJob.ConfigureAwait(false))
        {
            deleteJob.Transaction = transaction;
            deleteJob.CommandText = "DELETE FROM room_jobs WHERE room_id = $id;";
            deleteJob.Parameters.AddWithValue("$id", roomId);
            await deleteJob.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var deleteArchived = connection.CreateCommand();
        await using (deleteArchived.ConfigureAwait(false))
        {
            deleteArchived.Transaction = transaction;
            deleteArchived.CommandText = "DELETE FROM archived_rooms WHERE room_id = $id;";
            deleteArchived.Parameters.AddWithValue("$id", roomId);
            await deleteArchived.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var deleteRoom = connection.CreateCommand();
        await using (deleteRoom.ConfigureAwait(false))
        {
            deleteRoom.Transaction = transaction;
            deleteRoom.CommandText = "DELETE FROM rooms WHERE id = $id;";
            deleteRoom.Parameters.AddWithValue("$id", roomId);
            await deleteRoom.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private static async Task DeleteRoomProjectAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, CancellationToken ct)
    {
        var deleteProject = connection.CreateCommand();
        await using var deleteProjectScope = deleteProject.ConfigureAwait(false);
        deleteProject.Transaction = transaction;
        deleteProject.CommandText = "DELETE FROM room_projects WHERE room_id = $id;";
        deleteProject.Parameters.AddWithValue("$id", roomId);
        await deleteProject.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
        var archived = reader.GetBoolean(3);

        var team = reader.IsDBNull(4) ? null : reader.GetString(4);

        var isTeamRoom = reader.GetBoolean(5);

        // The instant is the archived_rooms.archived column, which SetRoomArchivedAsync has always
        // stamped, so no schema change is needed. A value that is not a timestamp reads as unknown.
        DateTimeOffset? archivedAt = !reader.IsDBNull(6)
            && DateTimeOffset.TryParse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed)
            ? parsed
            : null;

        var project = reader.IsDBNull(7) ? null : reader.GetString(7);

        return new Room(id, name, created) { Archived = archived, ArchivedAt = archivedAt, Team = team, Project = project, IsTeamRoom = isTeamRoom };
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Runs a query on a fresh connection and reads every row with <paramref name="read"/>.</summary>
    /// <param name="sql">The statement.</param>
    /// <param name="bind">Adds the statement's parameters, or <see langword="null"/> for none.</param>
    /// <param name="read">Maps the reader's current row to a value.</param>
    /// <param name="ct">Cancels the query.</param>
    private async Task<List<T>> QueryAsync<T>(string sql, Action<SqliteCommand>? bind, Func<SqliteDataReader, T> read, CancellationToken ct)
    {
        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = sql;
        bind?.Invoke(command);

        var rows = new List<T>();
        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    /// <summary>Runs a query on a fresh connection and reads only its first row.</summary>
    /// <returns>The first row mapped by <paramref name="read"/>, or <see langword="null"/> if there is none.</returns>
    private async Task<T?> QuerySingleAsync<T>(string sql, Action<SqliteCommand>? bind, Func<SqliteDataReader, T> read, CancellationToken ct)
        where T : class
    {
        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = sql;
        bind?.Invoke(command);

        var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? read(reader) : null;
    }

    /// <summary>Runs a non-query statement on a fresh connection.</summary>
    /// <returns>The number of rows the statement affected.</returns>
    private async Task<int> ExecuteAsync(string sql, Action<SqliteCommand>? bind, CancellationToken ct)
    {
        var connection = await this.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var connectionScope = connection.ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = sql;
        bind?.Invoke(command);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys=ON;", ct).ConfigureAwait(false);
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