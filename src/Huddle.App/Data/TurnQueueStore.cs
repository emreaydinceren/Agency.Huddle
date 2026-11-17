using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// The Turn Queue's ledger in <c>team.db</c>: one row per Message an Agent owes a Turn for, so a restart neither forgets a
/// Message nor answers it twice (spec §5.2). Deliberately synchronous like <see cref="JobStateStore"/>: every call is one
/// small statement, or one transaction when it touches several rows. A <c>done</c> row is never reopened by a write here.
/// </summary>
#pragma warning disable HAA0601 // AddWithValue takes object, so boxing a long or int parameter cannot be avoided; each call is one small statement.
internal sealed class TurnQueueStore
{
    /// <summary>The tables and the index, created here and by <see cref="SqliteTeamDirectory"/>, which deletes a Room's rows.</summary>
    internal const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS turn_queue (
            seq              INTEGER PRIMARY KEY AUTOINCREMENT,
            agent_id         TEXT NOT NULL,
            message_id       TEXT NOT NULL,
            room_id          TEXT NOT NULL,
            posted_utc       TEXT NOT NULL,
            status           TEXT NOT NULL,
            outcome          TEXT,
            reply_message_id TEXT,
            connection_id    TEXT,
            attempts         INTEGER NOT NULL DEFAULT 0,
            created_utc      TEXT NOT NULL,
            updated_utc      TEXT NOT NULL,
            UNIQUE (agent_id, message_id)
        );
        CREATE INDEX IF NOT EXISTS turn_queue_open ON turn_queue(agent_id, status, seq);
        CREATE TABLE IF NOT EXISTS turn_queue_agents (agent_id TEXT PRIMARY KEY);
        """;

    private const string SelectRows = """
        SELECT seq, agent_id, message_id, room_id, posted_utc, status, outcome, reply_message_id, connection_id,
               attempts, created_utc, updated_utc
        FROM turn_queue
        """;

    private const string CloseOpenSql = """
        UPDATE turn_queue SET status = 'done', outcome = $outcome, updated_utc = $now
        WHERE agent_id = $agent AND room_id = $room AND message_id = $message AND status IN ('waiting', 'running');
        """;

    private const string CloseUnrepliedSql = """
        UPDATE turn_queue SET status = 'done', outcome = $outcome, updated_utc = $now
        WHERE agent_id = $agent AND room_id = $room AND message_id = $message AND NOT (status = 'done' AND outcome = 'replied');
        """;

    private const string ForgetSql = "DELETE FROM turn_queue WHERE message_id = $message;";

    private const string DeleteForRoomSql = "DELETE FROM turn_queue WHERE room_id = $room;";

    private const string OpenOfAgentSql = SelectRows + " WHERE agent_id = $agent AND status IN ('waiting', 'running') ORDER BY seq;";

    private const string OpenInRoomSql = SelectRows + " WHERE room_id = $room AND status IN ('waiting', 'running') ORDER BY seq;";

    private const string OpenBeforeSql = SelectRows + " WHERE status IN ('waiting', 'running') AND updated_utc < $cutoff ORDER BY seq;";

    private readonly string connectionString;

    /// <summary>Opens the database and creates the tables when they are missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public TurnQueueStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>Writes a <c>waiting</c> row for a Message an Agent owes a Turn for, never changing a row that is already there.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="messageId">The Message.</param>
    /// <param name="roomId">The Room the Message was posted to.</param>
    /// <param name="postedUtc">The Message's own timestamp.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when a row was inserted.</returns>
    public bool Record(string agentId, string messageId, string roomId, DateTimeOffset postedUtc, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return InsertIfMissing(connection, null, agentId, messageId, roomId, postedUtc, TurnQueueStatus.Waiting, 0, now) == 1;
    }

    /// <summary>Deletes every row for a Message, for a Message that was deleted.</summary>
    /// <param name="messageId">The Message.</param>
    /// <returns>How many rows were deleted.</returns>
    public int Forget(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(connection, null, ForgetSql, ("$message", messageId));
    }

    /// <summary>
    /// Marks Messages as sent to a client: a missing row is inserted <c>running</c>, a <c>waiting</c> or <c>running</c> row
    /// becomes <c>running</c> with one more attempt, and a <c>done</c> row is left alone.
    /// </summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="messageIds">The Messages in the Turn.</param>
    /// <param name="replyMessageId">The id the runner minted for the reply.</param>
    /// <param name="connectionId">The connection the prompt went over.</param>
    /// <param name="postedUtcForNew">The timestamp for a row that has to be inserted.</param>
    /// <param name="now">The current time.</param>
    public void Started(
        string agentId, string roomId, IReadOnlyList<string> messageIds, string replyMessageId, string connectionId,
        DateTimeOffset postedUtcForNew, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(replyMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (string messageId in messageIds)
        {
            int moved = Execute(
                connection,
                transaction,
                """
                UPDATE turn_queue
                SET status = 'running', outcome = NULL, reply_message_id = $reply, connection_id = $connection,
                    attempts = attempts + 1, updated_utc = $now
                WHERE agent_id = $agent AND room_id = $room AND message_id = $message AND status IN ('waiting', 'running');
                """,
                ("$reply", replyMessageId),
                ("$connection", connectionId),
                ("$now", FormatTime(now)),
                ("$agent", agentId),
                ("$room", roomId),
                ("$message", messageId));
            if (moved == 0)
            {
                _ = InsertIfMissing(connection, transaction, agentId, messageId, roomId, postedUtcForNew, TurnQueueStatus.Running, 1, now);
                _ = Execute(
                    connection,
                    transaction,
                    """
                    UPDATE turn_queue SET reply_message_id = $reply, connection_id = $connection
                    WHERE agent_id = $agent AND room_id = $room AND message_id = $message AND status = 'running' AND reply_message_id IS NULL;
                    """,
                    ("$reply", replyMessageId),
                    ("$connection", connectionId),
                    ("$agent", agentId),
                    ("$room", roomId),
                    ("$message", messageId));
            }
        }

        transaction.Commit();
    }

    /// <summary>Closes the open rows of one Agent in one Room for these Messages with an outcome; <c>done</c> rows are untouched.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="messageIds">The Messages.</param>
    /// <param name="outcome">How the Turn ended.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many rows were closed.</returns>
    public int Finish(string agentId, string roomId, IReadOnlyList<string> messageIds, TurnQueueOutcome outcome, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(messageIds);

        return this.ClosePerMessage(agentId, roomId, messageIds, outcome, now, CloseOpenSql);
    }

    /// <summary>
    /// Closes these Messages' rows of one Agent in one Room as <c>replied</c>, whatever their status, except a row that is
    /// already <c>replied</c>, whose time stays as it was.
    /// </summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="messageIds">The Messages the reply names.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many rows were changed.</returns>
    public int MarkReplied(string agentId, string roomId, IReadOnlyList<string> messageIds, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(messageIds);

        return this.ClosePerMessage(agentId, roomId, messageIds, TurnQueueOutcome.Replied, now, CloseUnrepliedSql);
    }

    /// <summary>Closes the <c>running</c> rows of one connection, for a connection that dropped or a Turn that ended.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="outcome">How they ended.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many rows were closed.</returns>
    public int CloseRunning(string connectionId, TurnQueueOutcome outcome, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(
            connection,
            null,
            "UPDATE turn_queue SET status = 'done', outcome = $outcome, updated_utc = $now WHERE connection_id = $connection AND status = 'running';",
            ("$outcome", ToWire(outcome)),
            ("$now", FormatTime(now)),
            ("$connection", connectionId));
    }

    /// <summary>The highest <c>seq</c> of one Agent's rows in one Room, which a Stop closes up to.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <returns>The highest <c>seq</c>, or 0 when there is none.</returns>
    public long MaxSeq(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(seq), 0) FROM turn_queue WHERE agent_id = $agent AND room_id = $room;";
        _ = command.Parameters.AddWithValue("$agent", agentId);
        _ = command.Parameters.AddWithValue("$room", roomId);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>The Rooms in which a connection has <c>running</c> rows, so a close can tell each one.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <returns>The distinct Room ids.</returns>
    public IReadOnlyList<string> RunningRoomsOf(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT room_id FROM turn_queue WHERE connection_id = $connection AND status = 'running';";
        _ = command.Parameters.AddWithValue("$connection", connectionId);
        List<string> rooms = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rooms.Add(reader.GetString(0));
        }

        return rooms;
    }

    /// <summary>The Room of one row.</summary>
    /// <param name="seq">The row.</param>
    /// <returns>The Room id, or <see langword="null"/> when there is no such row.</returns>
    public string? RoomOf(long seq)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT room_id FROM turn_queue WHERE seq = $seq;";
        _ = command.Parameters.AddWithValue("$seq", seq);
        return command.ExecuteScalar() as string;
    }

    /// <summary>Closes the open rows of one Agent in one Room up to a <c>seq</c>; later rows stay open.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="maxSeq">The highest <c>seq</c> to close.</param>
    /// <param name="outcome">How they ended.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many rows were closed.</returns>
    public int CloseUpTo(string agentId, string roomId, long maxSeq, TurnQueueOutcome outcome, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(
            connection,
            null,
            """
            UPDATE turn_queue SET status = 'done', outcome = $outcome, updated_utc = $now
            WHERE agent_id = $agent AND room_id = $room AND seq <= $max AND status IN ('waiting', 'running');
            """,
            ("$outcome", ToWire(outcome)),
            ("$now", FormatTime(now)),
            ("$agent", agentId),
            ("$room", roomId),
            ("$max", maxSeq));
    }

    /// <summary>Closes every open row of one Agent, for an Agent that went away.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="outcome">How they ended.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many rows were closed.</returns>
    public int CloseForAgent(string agentId, TurnQueueOutcome outcome, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(
            connection,
            null,
            "UPDATE turn_queue SET status = 'done', outcome = $outcome, updated_utc = $now WHERE agent_id = $agent AND status IN ('waiting', 'running');",
            ("$outcome", ToWire(outcome)),
            ("$now", FormatTime(now)),
            ("$agent", agentId));
    }

    /// <summary>Deletes every row of a Room.</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>How many rows were deleted.</returns>
    public int DeleteForRoom(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(connection, null, DeleteForRoomSql, ("$room", roomId));
    }

    /// <summary>The open (<c>waiting</c> or <c>running</c>) rows of one Agent, oldest first.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <returns>The rows, ordered by <c>seq</c>.</returns>
    public IReadOnlyList<TurnQueueRow> Open(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        return this.Query(OpenOfAgentSql, ("$agent", agentId));
    }

    /// <summary>The open rows in one Room, oldest first.</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>The rows, ordered by <c>seq</c>.</returns>
    public IReadOnlyList<TurnQueueRow> OpenIn(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        return this.Query(OpenInRoomSql, ("$room", roomId));
    }

    /// <summary>The open rows last changed before a time.</summary>
    /// <param name="cutoff">Rows updated at or after this are left out.</param>
    /// <returns>The rows, ordered by <c>seq</c>.</returns>
    public IReadOnlyList<TurnQueueRow> OpenUpdatedBefore(DateTimeOffset cutoff) =>
        this.Query(OpenBeforeSql, ("$cutoff", FormatTime(cutoff)));

    /// <summary>Sets one row's status and outcome, for the start-up pass; a row set back to <c>waiting</c> loses its reply and connection ids.</summary>
    /// <param name="seq">The row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="outcome">The outcome, for <see cref="TurnQueueStatus.Done"/>.</param>
    /// <param name="now">The current time.</param>
    public void Set(long seq, TurnQueueStatus status, TurnQueueOutcome? outcome, DateTimeOffset now)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        _ = Execute(
            connection,
            null,
            """
            UPDATE turn_queue
            SET status = $status, outcome = $outcome, updated_utc = $now,
                reply_message_id = CASE WHEN $status = 'waiting' THEN NULL ELSE reply_message_id END,
                connection_id = CASE WHEN $status = 'waiting' THEN NULL ELSE connection_id END
            WHERE seq = $seq;
            """,
            ("$status", ToWire(status)),
            ("$outcome", outcome is { } value ? ToWire(value) : null),
            ("$now", FormatTime(now)),
            ("$seq", seq));
    }

    /// <summary>Deletes one row.</summary>
    /// <param name="seq">The row.</param>
    public void Delete(long seq)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        _ = Execute(connection, null, "DELETE FROM turn_queue WHERE seq = $seq;", ("$seq", seq));
    }

    /// <summary>Deletes the <c>done</c> rows last changed before a time.</summary>
    /// <param name="cutoff">Rows updated at or after this are kept.</param>
    /// <returns>How many rows were deleted.</returns>
    public int PruneDoneBefore(DateTimeOffset cutoff)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(
            connection, null, "DELETE FROM turn_queue WHERE status = 'done' AND updated_utc < $cutoff;", ("$cutoff", FormatTime(cutoff)));
    }

    /// <summary>Records that an Agent's last connection asked for the queue.</summary>
    /// <param name="agentId">The Agent.</param>
    public void Subscribe(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        _ = Execute(connection, null, "INSERT OR IGNORE INTO turn_queue_agents(agent_id) VALUES($agent);", ("$agent", agentId));
    }

    /// <summary>Records that an Agent no longer asks for the queue.</summary>
    /// <param name="agentId">The Agent.</param>
    public void Unsubscribe(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        _ = Execute(connection, null, "DELETE FROM turn_queue_agents WHERE agent_id = $agent;", ("$agent", agentId));
    }

    /// <summary>The Agents whose last connection asked for the queue.</summary>
    /// <returns>Their ids, compared ordinally.</returns>
    public IReadOnlySet<string> Subscribers()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT agent_id FROM turn_queue_agents;";
        HashSet<string> ids = new(StringComparer.Ordinal);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            _ = ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static int InsertIfMissing(
        SqliteConnection connection, SqliteTransaction? transaction, string agentId, string messageId, string roomId,
        DateTimeOffset postedUtc, TurnQueueStatus status, int attempts, DateTimeOffset now) =>
        Execute(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO turn_queue(agent_id, message_id, room_id, posted_utc, status, attempts, created_utc, updated_utc)
            VALUES($agent, $message, $room, $posted, $status, $attempts, $now, $now);
            """,
            ("$agent", agentId),
            ("$message", messageId),
            ("$room", roomId),
            ("$posted", FormatTime(postedUtc)),
            ("$status", ToWire(status)),
            ("$attempts", attempts),
            ("$now", FormatTime(now)));

    private int ClosePerMessage(
        string agentId, string roomId, IReadOnlyList<string> messageIds, TurnQueueOutcome outcome, DateTimeOffset now, string sql)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        int closed = 0;
        foreach (string messageId in messageIds)
        {
            closed += Execute(
                connection,
                transaction,
                sql,
                ("$outcome", ToWire(outcome)),
                ("$now", FormatTime(now)),
                ("$agent", agentId),
                ("$room", roomId),
                ("$message", messageId));
        }

        transaction.Commit();
        return closed;
    }

    private List<TurnQueueRow> Query(string sql, (string Name, object? Value) parameter)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);

        List<TurnQueueRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (ReadRow(reader) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>Reads one row, or <see langword="null"/> when its status or outcome is a value this build does not know.</summary>
    private static TurnQueueRow? ReadRow(SqliteDataReader reader)
    {
        TurnQueueStatus? status = ParseStatus(reader.GetString(5));
        TurnQueueOutcome? outcome = reader.IsDBNull(6) ? null : ParseOutcome(reader.GetString(6));
        if (status is null || (!reader.IsDBNull(6) && outcome is null))
        {
            return null;
        }

        return new TurnQueueRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ParseTime(reader.GetString(4)),
            status.Value,
            outcome,
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetInt32(9),
            ParseTime(reader.GetString(10)),
            ParseTime(reader.GetString(11)));
    }

    private static int Execute(
        SqliteConnection connection, SqliteTransaction? transaction, string sql, params ReadOnlySpan<(string Name, object? Value)> parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command.ExecuteNonQuery();
    }

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>The stored name of a status, never its number, so reordering the enum cannot rewrite history.</summary>
    private static string ToWire(TurnQueueStatus status) => status switch
    {
        TurnQueueStatus.Waiting => "waiting",
        TurnQueueStatus.Running => "running",
        _ => "done",
    };

    private static string ToWire(TurnQueueOutcome outcome) => outcome switch
    {
        TurnQueueOutcome.Replied => "replied",
        TurnQueueOutcome.Silent => "silent",
        TurnQueueOutcome.Failed => "failed",
        TurnQueueOutcome.Stopped => "stopped",
        TurnQueueOutcome.Declined => "declined",
        TurnQueueOutcome.Dropped => "dropped",
        TurnQueueOutcome.Expired => "expired",
        TurnQueueOutcome.Abandoned => "abandoned",
        _ => "gone",
    };

    private static TurnQueueStatus? ParseStatus(string text) => text switch
    {
        "waiting" => TurnQueueStatus.Waiting,
        "running" => TurnQueueStatus.Running,
        "done" => TurnQueueStatus.Done,
        _ => null,
    };

    private static TurnQueueOutcome? ParseOutcome(string text) => text switch
    {
        "replied" => TurnQueueOutcome.Replied,
        "silent" => TurnQueueOutcome.Silent,
        "failed" => TurnQueueOutcome.Failed,
        "stopped" => TurnQueueOutcome.Stopped,
        "declined" => TurnQueueOutcome.Declined,
        "dropped" => TurnQueueOutcome.Dropped,
        "expired" => TurnQueueOutcome.Expired,
        "abandoned" => TurnQueueOutcome.Abandoned,
        "gone" => TurnQueueOutcome.Gone,
        _ => null,
    };

    /// <summary>Deletes a Room's rows inside the transaction that deletes the Room itself.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The Room delete's transaction.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the statement.</param>
    internal static async Task DeleteRoomRowsAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, CancellationToken ct)
    {
        SqliteCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM turn_queue WHERE room_id = $id;";
            _ = command.Parameters.AddWithValue("$id", roomId);
            _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
#pragma warning restore HAA0601
