using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores spend per Room and per currency in <c>team.db</c>, so a Project's spend can be read as the sum over the Rooms that
/// belong to it. A Turn is recorded against the Room it ran in and nothing else: which Project a Room belongs to is looked up
/// when the spend is read, through <c>room_projects</c> and <c>room_teams</c>, so a Room moved into a Project brings its spend
/// with it. Amounts are kept as invariant-culture decimal text, as in <see cref="PersonaSpendStore"/>.
/// </summary>
/// <remarks>
/// Deliberately synchronous, in the style of <see cref="PersonaSpendStore"/>: an add is a read-modify-write of one row, so every
/// write runs in a transaction under a process-wide lock. A Room that has no Project is recorded here and counts toward no Project.
/// </remarks>
internal sealed class RoomSpendStore
{
    private static readonly Lock WriteGate = new();

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the tables the Project join reads, in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public RoomSpendStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(
            options.Value,
            SqliteTeamDirectory.DdlCreateRoomSpend
            + SqliteTeamDirectory.DdlCreateProjectSpendRetained
            + SqliteTeamDirectory.DdlCreateRoomTeams
            + SqliteTeamDirectory.DdlCreateRoomProjects);
    }

    /// <summary>Adds <paramref name="amount"/> into the Room's row for the currency, creating it if needed. A zero or negative amount is ignored.</summary>
    /// <param name="roomId">The Room the Turn ran in.</param>
    /// <param name="currency">The currency code.</param>
    /// <param name="amount">The amount to add.</param>
    public void Add(string roomId, string currency, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (amount <= 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction();
            decimal total = amount;
            using (SqliteCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = "SELECT amount FROM room_spend WHERE room_id = $room AND currency = $currency;";
                _ = select.Parameters.AddWithValue("$room", roomId);
                _ = select.Parameters.AddWithValue("$currency", currency);
                if (select.ExecuteScalar() is string existing)
                {
                    total += ParseAmount(existing);
                }
            }

            using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO room_spend(room_id, currency, amount) VALUES($room, $currency, $amount)
                    ON CONFLICT(room_id, currency) DO UPDATE SET amount = excluded.amount;
                    """;
                _ = upsert.Parameters.AddWithValue("$room", roomId);
                _ = upsert.Parameters.AddWithValue("$currency", currency);
                _ = upsert.Parameters.AddWithValue("$amount", total.ToString(CultureInfo.InvariantCulture));
                _ = upsert.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Returns what the Room has spent, per currency.</summary>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>One entry per currency, ordered by currency code; empty when nothing is stored.</returns>
    public IReadOnlyList<StoredSpend> ForRoom(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        return this.Query("SELECT currency, amount FROM room_spend WHERE room_id = $a;", roomId, null);
    }

    /// <summary>Returns the Team and Project a Room belongs to now.</summary>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>The Team and Project, or <see langword="null"/> when the Room has no Project or is unknown.</returns>
    public (string Team, string Project)? ProjectOf(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.team, p.project
            FROM room_teams t
            JOIN room_projects p ON p.room_id = t.room_id
            WHERE t.room_id = $a;
            """;
        _ = command.Parameters.AddWithValue("$a", roomId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    /// <summary>
    /// Returns what the Rooms of one Project have spent, per currency, read now from the Rooms' current Team and Project, plus what
    /// Rooms since deleted spent there.
    /// </summary>
    /// <param name="team">The Team the Project belongs to, compared case-insensitively.</param>
    /// <param name="project">The Project's name, compared case-insensitively.</param>
    /// <returns>One entry per currency, ordered by currency code; empty when nothing is stored.</returns>
    public IReadOnlyList<StoredSpend> ForProject(string team, string project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        const string Sql = """
            SELECT s.currency, s.amount
            FROM room_spend s
            JOIN room_projects p ON p.room_id = s.room_id
            JOIN room_teams t ON t.room_id = s.room_id
            WHERE t.team = $a AND p.project = $b
            UNION ALL
            SELECT currency, amount FROM project_spend_retained WHERE team = $a AND project = $b;
            """;
        return this.Query(Sql, team, project);
    }

    private static decimal ParseAmount(string text) => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);

    private List<StoredSpend> Query(string sql, string first, string? second)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.Parameters.AddWithValue("$a", first);
        if (second is not null)
        {
            _ = command.Parameters.AddWithValue("$b", second);
        }

        SortedDictionary<string, decimal> totals = new(StringComparer.Ordinal);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string currency = reader.GetString(0);
            totals[currency] = totals.GetValueOrDefault(currency) + ParseAmount(reader.GetString(1));
        }

        return [.. totals.Select(static pair => new StoredSpend(pair.Key, pair.Value))];
    }
}
