using Agency.Huddle.App.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores one row per Helper per local day in <c>team.db</c>, in its own table (the <see cref="PersonaModelStore"/>
/// pattern: synchronous, <c>CREATE TABLE IF NOT EXISTS</c>). It only reads and adds; <see cref="HelperBudget"/> decides.
/// </summary>
/// <remarks>
/// The SQLite parameter values are boxed (<c>HAA0601</c>). Each call is one local SQLite round trip that dwarfs a
/// small allocation, and the Helper path runs a few times a day, so the pragmas below stay narrow rather than
/// contorting the code.
/// </remarks>
internal sealed class HelperUsageStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS helper_usage (
            helper_id TEXT NOT NULL COLLATE NOCASE,
            day       TEXT NOT NULL,
            calls     INTEGER NOT NULL DEFAULT 0,
            granted   INTEGER NOT NULL DEFAULT 0,
            skipped   INTEGER NOT NULL DEFAULT 0,
            cost      REAL,
            currency  TEXT,
            PRIMARY KEY (helper_id, day)
        );
        """;

    private const string AddCallSql = """
        INSERT INTO helper_usage(helper_id, day, calls) VALUES($id, $day, $amount)
        ON CONFLICT(helper_id, day) DO UPDATE SET calls = calls + excluded.calls;
        """;

    private const string AddSkippedSql = """
        INSERT INTO helper_usage(helper_id, day, skipped) VALUES($id, $day, $amount)
        ON CONFLICT(helper_id, day) DO UPDATE SET skipped = skipped + excluded.skipped;
        """;

    private const string AddGrantSql = """
        INSERT INTO helper_usage(helper_id, day, granted) VALUES($id, $day, $amount)
        ON CONFLICT(helper_id, day) DO UPDATE SET granted = granted + excluded.granted;
        """;

    private const string AddCostSql = """
        INSERT INTO helper_usage(helper_id, day, cost, currency) VALUES($id, $day, $cost, $currency)
        ON CONFLICT(helper_id, day) DO UPDATE SET cost = COALESCE(cost, 0) + excluded.cost, currency = excluded.currency;
        """;

    private readonly string connectionString;

    /// <summary>Opens <c>team.db</c> under the data directory and creates the table.</summary>
    /// <param name="options">The bound options, read for <see cref="TeamOptions.DataDir"/>.</param>
    public HelperUsageStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>Reads one day's usage.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day, <c>yyyy-MM-dd</c>.</param>
    public HelperUsage Get(HelperId id, string day)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT calls, granted, skipped, cost, currency FROM helper_usage WHERE helper_id = $id AND day = $day;";
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$day", day);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return HelperUsage.None;
        }

        decimal? cost = reader.IsDBNull(3) ? null : (decimal)reader.GetDouble(3);
        return new HelperUsage(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), cost, reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    /// <summary>Adds one call to the day.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day.</param>
    public void AddCall(HelperId id, string day) => this.Add(AddCallSql, id, day, 1);

    /// <summary>Adds one skipped input to the day.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day.</param>
    public void AddSkipped(HelperId id, string day) => this.Add(AddSkippedSql, id, day, 1);

    /// <summary>Adds calls granted by Continue to the day.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day.</param>
    /// <param name="calls">How many calls to add.</param>
    public void AddGrant(HelperId id, string day, int calls) => this.Add(AddGrantSql, id, day, calls);

    /// <summary>Adds a reported cost to the day.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day.</param>
    /// <param name="amount">The cost.</param>
    /// <param name="currency">Its currency.</param>
    public void AddCost(HelperId id, string day, decimal amount, string currency)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = AddCostSql;
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$day", day);
#pragma warning disable HAA0601 // A cold path: one boxed double per reported cost, beside a SQLite round trip.
        command.Parameters.AddWithValue("$cost", (double)amount);
#pragma warning restore HAA0601
        command.Parameters.AddWithValue("$currency", currency);
        command.ExecuteNonQuery();
    }

    /// <summary>Runs one of the counter statements with its amount.</summary>
    /// <param name="sql">A constant statement taking <c>$id</c>, <c>$day</c> and <c>$amount</c>.</param>
    /// <param name="id">The Helper.</param>
    /// <param name="day">The local day.</param>
    /// <param name="amount">How much to add.</param>
    private void Add(string sql, HelperId id, string day, int amount)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$day", day);
#pragma warning disable HAA0601 // A cold path: one boxed int per call, beside a SQLite round trip.
        command.Parameters.AddWithValue("$amount", amount);
#pragma warning restore HAA0601
        command.ExecuteNonQuery();
    }
}
