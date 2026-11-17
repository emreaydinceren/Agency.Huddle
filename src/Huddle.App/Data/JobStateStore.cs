using System.Globalization;
using Agency.Huddle.App.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores each Job's next and last run in <c>team.db</c>, so a restart neither repeats a run nor forgets one (ADR-0042's rule
/// for cadences, applied to Jobs). Deliberately synchronous like <see cref="CadenceStateStore"/>.
/// </summary>
internal sealed class JobStateStore
{
    private const string DdlCreateJobState = """
        CREATE TABLE IF NOT EXISTS job_state (
            job_id       TEXT PRIMARY KEY,
            when_key     TEXT NOT NULL,
            next_run_utc TEXT,
            last_run_utc TEXT,
            last_outcome TEXT
        );
        """;

    /// <summary>The sibling table for a Job's retry count and whether it ever ran, so the older table's shape is untouched (a <c>CREATE TABLE IF NOT EXISTS</c> never adds a column).</summary>
    private const string DdlCreateJobStateTries = """
        CREATE TABLE IF NOT EXISTS job_state_tries (
            job_id       TEXT PRIMARY KEY,
            failed_tries INTEGER NOT NULL,
            ever_ran     INTEGER NOT NULL
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public JobStateStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateJobState + "\n" + DdlCreateJobStateTries);
    }

    /// <summary>Reads how many runs of a Job's current due time have failed and whether it ever ran.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <returns>The pair; a Job with no row reads as 0 and <see langword="false"/>.</returns>
    public (int FailedTries, bool EverRan) GetTries(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT failed_tries, ever_ran FROM job_state_tries WHERE job_id = $id;";
        _ = command.Parameters.AddWithValue("$id", jobId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1) != 0) : (0, false);
    }

    /// <summary>Stores a Job's retry count and whether it ever ran.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <param name="failedTries">How many runs of the current due time have failed.</param>
    /// <param name="everRan">Whether it ever ran.</param>
    public void SetTries(string jobId, int failedTries, bool everRan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO job_state_tries(job_id, failed_tries, ever_ran)
            VALUES($id, $failed, $ran)
            ON CONFLICT(job_id) DO UPDATE SET
                failed_tries = excluded.failed_tries,
                ever_ran = excluded.ever_ran;
            """;
        _ = command.Parameters.AddWithValue("$id", jobId);
        _ = command.Parameters.AddWithValue("$failed", failedTries.ToString(CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$ran", everRan ? "1" : "0");
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Reads a Job's state.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <returns>The state, or <see langword="null"/> when the Job was never seen.</returns>
    public JobState? Get(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT when_key, next_run_utc, last_run_utc, last_outcome FROM job_state WHERE job_id = $id;";
        _ = command.Parameters.AddWithValue("$id", jobId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new JobState(
            jobId,
            reader.GetString(0),
            reader.IsDBNull(1) ? null : ParseTime(reader.GetString(1)),
            reader.IsDBNull(2) ? null : ParseTime(reader.GetString(2)),
            reader.IsDBNull(3) ? null : ParseOutcome(reader.GetString(3)));
    }

    /// <summary>Stores a Job's state, replacing what was there.</summary>
    /// <param name="state">The state.</param>
    public void Set(JobState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO job_state(job_id, when_key, next_run_utc, last_run_utc, last_outcome)
            VALUES($id, $when, $next, $last, $outcome)
            ON CONFLICT(job_id) DO UPDATE SET
                when_key = excluded.when_key,
                next_run_utc = excluded.next_run_utc,
                last_run_utc = excluded.last_run_utc,
                last_outcome = excluded.last_outcome;
            """;
        _ = command.Parameters.AddWithValue("$id", state.JobId);
        _ = command.Parameters.AddWithValue("$when", state.WhenKey);
        _ = command.Parameters.AddWithValue("$next", FormatTime(state.NextRunUtc));
        _ = command.Parameters.AddWithValue("$last", FormatTime(state.LastRunUtc));
        _ = command.Parameters.AddWithValue("$outcome", state.LastOutcome is { } outcome ? ToWire(outcome) : DBNull.Value);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets a deleted Job's state.</summary>
    /// <param name="jobId">The Job's id.</param>
    public void Remove(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM job_state WHERE job_id = $id; DELETE FROM job_state_tries WHERE job_id = $id;";
        _ = command.Parameters.AddWithValue("$id", jobId);
        _ = command.ExecuteNonQuery();
    }

    private static object FormatTime(DateTimeOffset? time) =>
        time is { } value ? value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value;

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>The stored name of an outcome, never its number, so reordering the enum cannot rewrite history.</summary>
    private static string ToWire(JobOutcome outcome) => outcome switch
    {
        JobOutcome.Delivered => "delivered",
        JobOutcome.BudgetSpent => "budget-spent",
        JobOutcome.NoAgent => "no-agent",
        JobOutcome.DeliveredLate => "delivered-late",
        JobOutcome.Held => "held",
        JobOutcome.NoRoom => "no-room",
        _ => "failed",
    };

    private static JobOutcome? ParseOutcome(string text) => text switch
    {
        "delivered" => JobOutcome.Delivered,
        "budget-spent" => JobOutcome.BudgetSpent,
        "no-agent" => JobOutcome.NoAgent,
        "failed" => JobOutcome.Failed,
        "delivered-late" => JobOutcome.DeliveredLate,
        "held" => JobOutcome.Held,
        "no-room" => JobOutcome.NoRoom,
        _ => null,
    };
}
