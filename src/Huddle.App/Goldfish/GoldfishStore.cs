using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Goldfish;

/// <summary>
/// The goldfish ledger in <c>team.db</c>: one row per possession, with at most one open row per Agent (a partial unique
/// index), so a restart finds what was left open and a return is claimed once. Deliberately synchronous like
/// <see cref="TurnQueueStore"/>: every call is one small statement, or one transaction when it reads before it writes.
/// Enums are stored as lower-case names and the spend as invariant-culture decimal text.
/// </summary>
#pragma warning disable HAA0601 // AddWithValue takes object, so boxing an int parameter cannot be avoided; each call is one small statement.
internal sealed class GoldfishStore
{
    /// <summary>The table and its index, created here and by <see cref="SqliteTeamDirectory"/>, which deletes a Room's rows.</summary>
    internal const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS goldfish_possessions (
            id                TEXT NOT NULL PRIMARY KEY,
            agent_id          TEXT NOT NULL,
            room_id           TEXT NOT NULL,
            task_id           TEXT,
            brief             TEXT NOT NULL,
            state             TEXT NOT NULL,
            exit              TEXT,
            exit_by           TEXT,
            started_at        TEXT NOT NULL,
            ended_at          TEXT,
            turns             INTEGER NOT NULL,
            spend             TEXT NOT NULL,
            currency          TEXT,
            result_message_id TEXT,
            CHECK ((state IN ('returning', 'ended')) = (exit IS NOT NULL))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS goldfish_one_open ON goldfish_possessions(agent_id) WHERE state <> 'ended';
        """;

    private const int SqliteConstraint = 19;

    private const int SqliteConstraintCheck = 275;

    private const string Columns = "id, agent_id, room_id, task_id, brief, state, exit, exit_by, started_at, ended_at, turns, spend, currency, result_message_id";

    private const string SelectOne = "SELECT " + Columns + " FROM goldfish_possessions WHERE id = $id;";

    private const string SelectOpen = "SELECT " + Columns + " FROM goldfish_possessions WHERE state <> 'ended' ORDER BY started_at, id;";

    private const string Insert = """
        INSERT INTO goldfish_possessions(id, agent_id, room_id, task_id, brief, state, exit, exit_by, started_at, ended_at, turns, spend, currency, result_message_id)
        VALUES($id, $agent, $room, $task, $brief, $state, $exit, $by, $started, $ended, $turns, $spend, $currency, $result);
        """;

    /// <summary>
    /// Claims a row for its return. An open row is claimed once: a Returning row is claimed again only by the start-up pass,
    /// which ends it as <c>restarted</c>, and an Ended row never.
    /// </summary>
    private const string BeginReturnSql = """
        UPDATE goldfish_possessions SET state = 'returning', exit = $exit, exit_by = $by
        WHERE id = $id AND state <> 'ended' AND (state <> 'returning' OR $exit = 'restarted');
        """;

    private static readonly Dictionary<string, PossessionState> States = Enum.GetValues<PossessionState>()
        .ToDictionary(static state => Name(state), StringComparer.Ordinal);

    private static readonly Dictionary<string, GoldfishExit> Exits = Enum.GetValues<GoldfishExit>()
        .ToDictionary(static exit => Name(exit), StringComparer.Ordinal);

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public GoldfishStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>Writes a possession row.</summary>
    /// <param name="possession">The row.</param>
    /// <returns><see langword="false"/> when the Agent already has an open possession, and nothing was written.</returns>
    public bool TryInsert(Possession possession)
    {
        ArgumentNullException.ThrowIfNull(possession);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Insert;
        _ = command.Parameters.AddWithValue("$id", possession.Id);
        _ = command.Parameters.AddWithValue("$agent", possession.AgentId);
        _ = command.Parameters.AddWithValue("$room", possession.RoomId);
        _ = command.Parameters.AddWithValue("$task", (object?)possession.TaskId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$brief", possession.Brief);
        _ = command.Parameters.AddWithValue("$state", Name(possession.State));
        _ = command.Parameters.AddWithValue("$exit", possession.Exit is { } exit ? Name(exit) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$by", (object?)possession.ExitBy ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$started", Text(possession.StartedAt));
        _ = command.Parameters.AddWithValue("$ended", possession.EndedAt is { } ended ? Text(ended) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$turns", possession.Turns);
        _ = command.Parameters.AddWithValue("$spend", Text(possession.Spend));
        _ = command.Parameters.AddWithValue("$currency", (object?)possession.Currency ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$result", (object?)possession.ResultMessageId ?? DBNull.Value);
        try
        {
            _ = command.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraint && ex.SqliteExtendedErrorCode != SqliteConstraintCheck)
        {
            return false;
        }
    }

    /// <summary>Reads one possession.</summary>
    /// <param name="id">The possession id.</param>
    /// <returns>The row, or <see langword="null"/> when there is none.</returns>
    public Possession? Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return this.Read(SelectOne, ("$id", id)).FirstOrDefault();
    }

    /// <summary>Reads every possession that has not ended.</summary>
    /// <returns>The rows, oldest first.</returns>
    public IReadOnlyList<Possession> Open() =>
        this.Read(SelectOpen);

    /// <summary>Moves a row to a state only while it is in one of the allowed source states, so a move cannot land on a row the return has claimed.</summary>
    /// <param name="id">The possession id.</param>
    /// <param name="to">The new state.</param>
    /// <param name="from">The states the row may be in for the move to apply.</param>
    /// <returns><see langword="true"/> when the row moved; <see langword="false"/> when it was in another state or unknown.</returns>
    public bool SetState(string id, PossessionState to, params PossessionState[] from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(from);
        if (from.Length == 0)
        {
            return false;
        }

        string sources = string.Join(", ", from.Select(source => $"'{Name(source)}'"));
        return this.Run($"UPDATE goldfish_possessions SET state = $state WHERE id = $id AND state IN ({sources});", ("$id", id), ("$state", Name(to))) == 1;
    }

    /// <summary>Remembers the posted result Message, so a restart does not post it twice.</summary>
    /// <param name="id">The possession id.</param>
    /// <param name="messageId">The result Message.</param>
    public void SetResultMessage(string id, string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        this.Run("UPDATE goldfish_possessions SET result_message_id = $message WHERE id = $id;", ("$id", id), ("$message", messageId));
    }

    /// <summary>Claims a row for its return, writing its exit and who chose it.</summary>
    /// <param name="id">The possession id.</param>
    /// <param name="exit">How it ends.</param>
    /// <param name="by">Who chose the exit, or <see langword="null"/> when nobody did (a failure or a restart).</param>
    /// <returns><see langword="true"/> when this call claimed the row; <see langword="false"/> when it is already returning, ended or unknown.</returns>
    public bool BeginReturn(string id, GoldfishExit exit, string? by)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return this.Run(BeginReturnSql, ("$id", id), ("$exit", Name(exit)), ("$by", by)) == 1;
    }

    /// <summary>Closes a row at the given time, keeping its exit.</summary>
    /// <param name="id">The possession id.</param>
    /// <param name="at">When it ended.</param>
    public void End(string id, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        this.Run("UPDATE goldfish_possessions SET state = 'ended', ended_at = $at WHERE id = $id AND exit IS NOT NULL;", ("$id", id), ("$at", Text(at)));
    }

    /// <summary>Counts one more Turn on a row and adds its spend.</summary>
    /// <param name="id">The possession id.</param>
    /// <param name="spend">What the Turn cost, or <see langword="null"/> when unknown.</param>
    /// <param name="currency">The currency of <paramref name="spend"/>; kept when <see langword="null"/>.</param>
    public void AddTurn(string id, decimal? spend, string? currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        decimal total = ReadSpend(connection, transaction, id) + (spend ?? 0m);
        Execute(
            connection,
            transaction,
            "UPDATE goldfish_possessions SET turns = turns + 1, spend = $spend, currency = COALESCE($currency, currency) WHERE id = $id;",
            ("$id", id),
            ("$spend", Text(total)),
            ("$currency", currency));
        transaction.Commit();
    }

    /// <summary>Deletes ended rows that ended before a time; an open row is never deleted.</summary>
    /// <param name="cutoff">Rows that ended before this are deleted.</param>
    /// <returns>How many rows were deleted.</returns>
    public int PruneBefore(DateTimeOffset cutoff) =>
        this.Run("DELETE FROM goldfish_possessions WHERE state = 'ended' AND ended_at < $cutoff;", ("$cutoff", Text(cutoff)));

    /// <summary>Deletes every row of a Room, inside the Room delete's transaction.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The transaction the delete runs in.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the delete.</param>
    internal static async Task DeleteRoomRowsAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, CancellationToken ct)
    {
        SqliteCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM goldfish_possessions WHERE room_id = $id;";
            _ = command.Parameters.AddWithValue("$id", roomId);
            _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static string Name<T>(T value)
        where T : struct, Enum =>
        (Enum.GetName(value) ?? string.Empty).ToLowerInvariant();

    private static string Text(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Text(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static T Lookup<T>(Dictionary<string, T> map, string text, string what)
        where T : struct, Enum =>
        map.TryGetValue(text, out T value) ? value : throw new InvalidOperationException($"Unknown goldfish {what} '{text}' in team.db.");

    private static decimal ReadSpend(SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT spend FROM goldfish_possessions WHERE id = $id;";
        _ = command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is string text ? decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture) : 0m;
    }

    private static int Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
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

    private static Possession ToPossession(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetString(4),
        Lookup(States, reader.GetString(5), "state"),
        reader.IsDBNull(6) ? null : Lookup(Exits, reader.GetString(6), "exit"),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        ParseTime(reader.GetString(8)),
        reader.IsDBNull(9) ? null : ParseTime(reader.GetString(9)),
        reader.GetInt32(10),
        decimal.Parse(reader.GetString(11), NumberStyles.Number, CultureInfo.InvariantCulture),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13));

    private int Run(string sql, params (string Name, object? Value)[] parameters)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Execute(connection, null, sql, parameters);
    }

    private List<Possession> Read(string sql, params (string Name, object? Value)[] parameters)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        List<Possession> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ToPossession(reader));
        }

        return rows;
    }
}
#pragma warning restore HAA0601
