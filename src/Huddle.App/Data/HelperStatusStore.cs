using System.Globalization;
using Agency.Huddle.App.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>One row per Helper in <c>team.db</c>: the last result and the last error (Helpers Spec §6.5).</summary>
internal sealed class HelperStatusStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS helper_status (
            helper_id            TEXT PRIMARY KEY COLLATE NOCASE,
            last_run_at          TEXT,
            last_result          TEXT,
            last_error           TEXT,
            last_error_at        TEXT,
            consecutive_failures INTEGER NOT NULL DEFAULT 0
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens <c>team.db</c> under the data directory and creates the table.</summary>
    /// <param name="options">The bound options, read for <see cref="TeamOptions.DataDir"/>.</param>
    public HelperStatusStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>The Helper's status, or <see cref="HelperStatus.None"/> when it never ran.</summary>
    /// <param name="id">The Helper.</param>
    public HelperStatus Get(HelperId id)
    {
        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT last_run_at, last_result, last_error, last_error_at, consecutive_failures FROM helper_status WHERE helper_id = $id;";
        command.Parameters.AddWithValue("$id", id.Value);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return HelperStatus.None;
        }

        return new HelperStatus(ReadTime(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadTime(reader, 3), reader.GetInt32(4));
    }

    /// <summary>A good run: sets the result and the run time, resets the failure count, and keeps the earlier error.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="at">When it ran.</param>
    /// <param name="result">What it did.</param>
    public void RecordSuccess(HelperId id, DateTimeOffset at, string result)
    {
        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO helper_status(helper_id, last_run_at, last_result, consecutive_failures) VALUES($id, $at, $result, 0)
            ON CONFLICT(helper_id) DO UPDATE SET last_run_at = excluded.last_run_at, last_result = excluded.last_result, consecutive_failures = 0;
            """;
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$at", Format(at));
        command.Parameters.AddWithValue("$result", result);
        command.ExecuteNonQuery();
    }

    /// <summary>A failed run: sets the error and its time, adds one to the failure count, and keeps the earlier result with its run time, so the two never pair a new time with an old result.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="at">When it failed.</param>
    /// <param name="error">What went wrong, in words for the card.</param>
    public void RecordFailure(HelperId id, DateTimeOffset at, string error)
    {
        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO helper_status(helper_id, last_error, last_error_at, consecutive_failures) VALUES($id, $error, $at, 1)
            ON CONFLICT(helper_id) DO UPDATE SET last_error = excluded.last_error,
                last_error_at = excluded.last_error_at, consecutive_failures = consecutive_failures + 1;
            """;
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$at", Format(at));
        command.Parameters.AddWithValue("$error", error);
        command.ExecuteNonQuery();
    }

    /// <summary>The round-trip text of an instant.</summary>
    private static string Format(DateTimeOffset at) => at.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>A nullable text column.</summary>
    private static string? ReadText(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    /// <summary>A nullable instant column.</summary>
    private static DateTimeOffset? ReadTime(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>Opens a connection to <c>team.db</c>.</summary>
    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(this.connectionString);
        connection.Open();
        return connection;
    }
}
