using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores the <c>raw_baselines</c> table of <c>team.db</c> (Spec 7.6): the size and modified time of each raw upload
/// right after it was written, for <c>brain-check</c>. Synchronous like <see cref="CadenceStateStore"/>.
/// </summary>
internal sealed class RawBaselineStore
{
    private const string DdlCreateRawBaselines = """
        CREATE TABLE IF NOT EXISTS raw_baselines (
            path          TEXT PRIMARY KEY,
            size_bytes    INTEGER NOT NULL,
            modified_utc  TEXT    NOT NULL,
            recorded_utc  TEXT    NOT NULL
        );
        """;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    /// <param name="timeProvider">The clock for the recorded time; the system clock when <see langword="null"/>.</param>
    public RawBaselineStore(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateRawBaselines);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Stores the baseline of a file, replacing the row of the same path.</summary>
    /// <param name="relativePath">The path relative to the data folder; <c>\</c> is stored as <c>/</c>.</param>
    /// <param name="sizeBytes">The file size.</param>
    /// <param name="modifiedUtc">The file's last-write time.</param>
    public void Record(string relativePath, long sizeBytes, DateTimeOffset modifiedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO raw_baselines(path, size_bytes, modified_utc, recorded_utc)
            VALUES($path, $size, $modified, $recorded)
            ON CONFLICT(path) DO UPDATE SET
                size_bytes = excluded.size_bytes,
                modified_utc = excluded.modified_utc,
                recorded_utc = excluded.recorded_utc;
            """;
        _ = command.Parameters.AddWithValue("$path", Normalize(relativePath));
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$size", sizeBytes);
#pragma warning restore HAA0601
        _ = command.Parameters.AddWithValue("$modified", FormatTime(modifiedUtc));
        _ = command.Parameters.AddWithValue("$recorded", FormatTime(this.timeProvider.GetUtcNow()));
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Reads the baseline of a file.</summary>
    /// <param name="relativePath">The path relative to the data folder; <c>\</c> is read as <c>/</c>.</param>
    /// <param name="baseline">The stored baseline when found.</param>
    /// <returns><see langword="true"/> when a row exists for the path.</returns>
    public bool TryGet(string relativePath, [NotNullWhen(true)] out RawBaseline? baseline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT size_bytes, modified_utc, recorded_utc FROM raw_baselines WHERE path = $path;";
        string path = Normalize(relativePath);
        _ = command.Parameters.AddWithValue("$path", path);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            baseline = null;
            return false;
        }

        baseline = new RawBaseline(path, reader.GetInt64(0), ParseTime(reader.GetString(1)), ParseTime(reader.GetString(2)));
        return true;
    }

    /// <summary>
    /// Moves every row whose path starts with <paramref name="oldPrefix"/> + <c>/</c> to the same path under <paramref name="newPrefix"/>,
    /// replacing a row already there. The prefix is compared as text (<c>substr</c>), so <c>_</c> and <c>%</c> in a Team name are not wildcards.
    /// </summary>
    /// <param name="oldPrefix">The old folder prefix, without a trailing separator.</param>
    /// <param name="newPrefix">The new folder prefix.</param>
    public void RenamePrefix(string oldPrefix, string newPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPrefix);

        string from = Normalize(oldPrefix).TrimEnd('/') + "/";
        string to = Normalize(newPrefix).TrimEnd('/') + "/";
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return;
        }

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO raw_baselines(path, size_bytes, modified_utc, recorded_utc)
            SELECT $to || substr(path, $len + 1), size_bytes, modified_utc, recorded_utc
            FROM raw_baselines WHERE substr(path, 1, $len) = $from;
            DELETE FROM raw_baselines WHERE substr(path, 1, $len) = $from;
            """;
        _ = command.Parameters.AddWithValue("$from", from);
        _ = command.Parameters.AddWithValue("$to", to);
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$len", from.Length);
#pragma warning restore HAA0601
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FormatTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
