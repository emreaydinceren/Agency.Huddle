using System.Globalization;
using Agency.Huddle.App.Scheduling;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores Memory Tidy's two timetables (<c>weekly</c> and <c>resume</c>) in <c>team.db</c>, one <see cref="DueState"/> a row, so a restart
/// neither repeats a pass nor forgets one. Deliberately synchronous like <see cref="JobStateStore"/>.
/// </summary>
internal sealed class MemoryTidyScheduleStore
{
    /// <summary>The name of the regular timetable's row.</summary>
    internal const string Weekly = "weekly";

    /// <summary>The name of the row that goes on after a pass ended at the daily cap.</summary>
    internal const string Resume = "resume";

    private const string DdlCreateSchedule = """
        CREATE TABLE IF NOT EXISTS memory_tidy_schedule (
            name          TEXT PRIMARY KEY,
            when_key      TEXT,
            next_run_utc  TEXT,
            failed_tries  INTEGER NOT NULL,
            ever_ran      INTEGER NOT NULL,
            last_run_utc  TEXT
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public MemoryTidyScheduleStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateSchedule);
    }

    /// <summary>Reads one timetable's row.</summary>
    /// <param name="name">The row's name.</param>
    /// <returns>The row, or <see langword="null"/> when there is none.</returns>
    public MemoryTidyScheduleRow? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT when_key, next_run_utc, failed_tries, ever_ran, last_run_utc FROM memory_tidy_schedule WHERE name = $name;";
        _ = command.Parameters.AddWithValue("$name", name);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        DueState state = new(
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? null : ParseTime(reader.GetString(1)),
            reader.GetInt32(2),
            reader.GetInt32(3) != 0);
        return new MemoryTidyScheduleRow(name, state, reader.IsDBNull(4) ? null : ParseTime(reader.GetString(4)));
    }

    /// <summary>Stores a row, replacing what was there.</summary>
    /// <param name="row">The row.</param>
    public void Put(MemoryTidyScheduleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO memory_tidy_schedule(name, when_key, next_run_utc, failed_tries, ever_ran, last_run_utc)
            VALUES($name, $when, $next, $tries, $ever, $last)
            ON CONFLICT(name) DO UPDATE SET
                when_key = excluded.when_key,
                next_run_utc = excluded.next_run_utc,
                failed_tries = excluded.failed_tries,
                ever_ran = excluded.ever_ran,
                last_run_utc = excluded.last_run_utc;
            """;
        _ = command.Parameters.AddWithValue("$name", row.Name);
        _ = command.Parameters.AddWithValue("$when", row.State.WhenKey);
        _ = command.Parameters.AddWithValue("$next", FormatTime(row.State.NextRunUtc));
#pragma warning disable HAA0601 // A cold path: two boxed ints a pass, beside a SQLite round trip.
        _ = command.Parameters.AddWithValue("$tries", row.State.FailedTries);
        _ = command.Parameters.AddWithValue("$ever", row.State.EverRan ? 1 : 0);
#pragma warning restore HAA0601
        _ = command.Parameters.AddWithValue("$last", FormatTime(row.LastRunUtc));
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets a row.</summary>
    /// <param name="name">The row's name.</param>
    public void Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM memory_tidy_schedule WHERE name = $name;";
        _ = command.Parameters.AddWithValue("$name", name);
        _ = command.ExecuteNonQuery();
    }

    private static object FormatTime(DateTimeOffset? time) =>
        time is { } value ? value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value;

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
