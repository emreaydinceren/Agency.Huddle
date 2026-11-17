using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores what Memory Tidy remembers between passes in <c>team.db</c>: a row per memory folder and a row per file with the day it was written
/// (Memory Tidy spec §6.5). Both tables are made whole here, every column, because a table made with <c>CREATE TABLE IF NOT EXISTS</c> never gains a column
/// on an install that already has it. Deliberately synchronous like <see cref="JobStateStore"/>.
/// </summary>
internal sealed class MemoryTidyStateStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS memory_tidy_state (
            folder_key         TEXT PRIMARY KEY,
            fingerprint        TEXT,
            tidied_utc         TEXT,
            outcome            TEXT,
            failures           INTEGER NOT NULL DEFAULT 0,
            failed_fingerprint TEXT,
            "offset"           INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS memory_tidy_files (
            folder_key  TEXT NOT NULL,
            file        TEXT NOT NULL,
            version     TEXT NOT NULL,
            written_day TEXT NOT NULL,
            PRIMARY KEY (folder_key, file)
        );
        """;

    private const string StateColumns = "folder_key, fingerprint, tidied_utc, outcome, failures, failed_fingerprint, \"offset\"";
    private const string DayFormat = "yyyy-MM-dd";

    private readonly string connectionString;

    /// <summary>Opens the database and creates both tables when they are missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public MemoryTidyStateStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>Reads a folder's state.</summary>
    /// <param name="folderKey">The folder key.</param>
    /// <returns>The state, or <see langword="null"/> when the folder was never tidied.</returns>
    public MemoryTidyState? GetState(string folderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {StateColumns} FROM memory_tidy_state WHERE folder_key = $key;";
        _ = command.Parameters.AddWithValue("$key", folderKey);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadState(reader) : null;
    }

    /// <summary>Lists every folder's state.</summary>
    /// <returns>The states, in folder key order.</returns>
    public IReadOnlyList<MemoryTidyState> ListStates()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {StateColumns} FROM memory_tidy_state ORDER BY folder_key;";

        using SqliteDataReader reader = command.ExecuteReader();
        List<MemoryTidyState> states = [];
        while (reader.Read())
        {
            states.Add(ReadState(reader));
        }

        return states;
    }

    /// <summary>Stores a folder's state, replacing what was there.</summary>
    /// <param name="state">The state.</param>
    public void PutState(MemoryTidyState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO memory_tidy_state(folder_key, fingerprint, tidied_utc, outcome, failures, failed_fingerprint, "offset")
            VALUES($key, $fingerprint, $tidied, $outcome, $failures, $failedFingerprint, $offset)
            ON CONFLICT(folder_key) DO UPDATE SET
                fingerprint = excluded.fingerprint,
                tidied_utc = excluded.tidied_utc,
                outcome = excluded.outcome,
                failures = excluded.failures,
                failed_fingerprint = excluded.failed_fingerprint,
                "offset" = excluded."offset";
            """;
        _ = command.Parameters.AddWithValue("$key", state.FolderKey);
        _ = command.Parameters.AddWithValue("$fingerprint", (object?)state.Fingerprint ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$tidied", state.TidiedUtc is { } tidied ? FormatTime(tidied) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$outcome", (object?)state.Outcome ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$failedFingerprint", (object?)state.FailedFingerprint ?? DBNull.Value);
#pragma warning disable HAA0601 // A cold path: two boxed ints per folder per pass, beside a SQLite round trip.
        _ = command.Parameters.AddWithValue("$failures", state.Failures);
        _ = command.Parameters.AddWithValue("$offset", state.Offset);
#pragma warning restore HAA0601
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Lists the file rows of one folder.</summary>
    /// <param name="folderKey">The folder key.</param>
    /// <returns>The rows, in file name order.</returns>
    public IReadOnlyList<MemoryTidyFileRow> GetFiles(string folderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT file, version, written_day FROM memory_tidy_files WHERE folder_key = $key ORDER BY file;";
        _ = command.Parameters.AddWithValue("$key", folderKey);

        using SqliteDataReader reader = command.ExecuteReader();
        List<MemoryTidyFileRow> rows = [];
        while (reader.Read())
        {
            rows.Add(new MemoryTidyFileRow(folderKey, reader.GetString(0), reader.GetString(1), ParseDay(reader.GetString(2))));
        }

        return rows;
    }

    /// <summary>Stores a file's row, replacing what was there.</summary>
    /// <param name="row">The row.</param>
    public void PutFile(MemoryTidyFileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO memory_tidy_files(folder_key, file, version, written_day)
            VALUES($key, $file, $version, $day)
            ON CONFLICT(folder_key, file) DO UPDATE SET
                version = excluded.version,
                written_day = excluded.written_day;
            """;
        _ = command.Parameters.AddWithValue("$key", row.FolderKey);
        _ = command.Parameters.AddWithValue("$file", row.File);
        _ = command.Parameters.AddWithValue("$version", row.Version);
        _ = command.Parameters.AddWithValue("$day", row.WrittenDay.ToString(DayFormat, CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets one file's row.</summary>
    /// <param name="folderKey">The folder key.</param>
    /// <param name="file">The file's name.</param>
    public void RemoveFile(string folderKey, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM memory_tidy_files WHERE folder_key = $key AND file = $file;";
        _ = command.Parameters.AddWithValue("$key", folderKey);
        _ = command.Parameters.AddWithValue("$file", file);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets a folder: its state row and every file row.</summary>
    /// <param name="folderKey">The folder key.</param>
    public void DeleteFolder(string folderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        Execute(connection, transaction, "DELETE FROM memory_tidy_state WHERE folder_key = $key;", folderKey, null);
        Execute(connection, transaction, "DELETE FROM memory_tidy_files WHERE folder_key = $key;", folderKey, null);
        transaction.Commit();
    }

    /// <summary>
    /// Moves a folder's state row and file rows to a new key, as when an Agent is renamed. Rows already under the new key are replaced.
    /// </summary>
    /// <param name="oldKey">The folder key before.</param>
    /// <param name="newKey">The folder key after.</param>
    public void RenameFolder(string oldKey, string newKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(newKey);
        if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
        {
            return;
        }

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        Execute(connection, transaction, "DELETE FROM memory_tidy_state WHERE folder_key = $new;", oldKey, newKey);
        Execute(connection, transaction, "DELETE FROM memory_tidy_files WHERE folder_key = $new;", oldKey, newKey);
        Execute(connection, transaction, "UPDATE memory_tidy_state SET folder_key = $new WHERE folder_key = $key;", oldKey, newKey);
        Execute(connection, transaction, "UPDATE memory_tidy_files SET folder_key = $new WHERE folder_key = $key;", oldKey, newKey);
        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, string key, string? newKey)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.Parameters.AddWithValue("$key", key);
        if (newKey is not null)
        {
            _ = command.Parameters.AddWithValue("$new", newKey);
        }

        _ = command.ExecuteNonQuery();
    }

    private static MemoryTidyState ReadState(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.IsDBNull(2) ? null : ParseTime(reader.GetString(2)),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetInt32(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetInt32(6));

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static DateOnly ParseDay(string text) => DateOnly.ParseExact(text, DayFormat, CultureInfo.InvariantCulture);
}
