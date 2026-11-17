using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Remembers, in <c>team.db</c>, each Huddle-wide Memory file a Memory Tidy merge removed and the file it went into (Memory Tidy spec §6.14), so a running
/// Agent is told the fact moved rather than that it is no longer true. A row older than <see cref="KeepDays"/> days is ignored on read and deleted on
/// write. Deliberately synchronous like <see cref="MemoryTidyStateStore"/>.
/// </summary>
internal sealed class MemoryTidyMergeStore
{
    /// <summary>How many days a record of a merge is kept and believed.</summary>
    internal const int KeepDays = 30;

    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS memory_tidy_merges (
            file       TEXT NOT NULL,
            into_file  TEXT NOT NULL,
            merged_utc TEXT NOT NULL
        );
        """;

    private readonly string connectionString;
    private readonly TimeProvider clock;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    /// <param name="timeProvider">The clock; the system clock when <see langword="null"/>.</param>
    public MemoryTidyMergeStore(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
        this.clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Records that a merge removed <paramref name="file"/> and put its facts into <paramref name="intoFile"/>; rows past their days are deleted.</summary>
    /// <param name="file">The removed file's name.</param>
    /// <param name="intoFile">The file the merge kept.</param>
    public void Record(string file, string intoFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(intoFile);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand prune = connection.CreateCommand();
        prune.Transaction = transaction;
        prune.CommandText = "DELETE FROM memory_tidy_merges WHERE merged_utc < $cutoff OR file = $file;";
        _ = prune.Parameters.AddWithValue("$cutoff", Format(this.clock.GetUtcNow().AddDays(-KeepDays)));
        _ = prune.Parameters.AddWithValue("$file", file);
        _ = prune.ExecuteNonQuery();

        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO memory_tidy_merges(file, into_file, merged_utc) VALUES($file, $into, $merged);";
        _ = insert.Parameters.AddWithValue("$file", file);
        _ = insert.Parameters.AddWithValue("$into", intoFile);
        _ = insert.Parameters.AddWithValue("$merged", Format(this.clock.GetUtcNow()));
        _ = insert.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>Finds where a removed file's facts went.</summary>
    /// <param name="file">The removed file's name, matched ignoring case.</param>
    /// <returns>The file the merge kept, or <see langword="null"/> when no merge in the last <see cref="KeepDays"/> days removed it.</returns>
    public string? FindInto(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT file, into_file FROM memory_tidy_merges WHERE merged_utc >= $cutoff ORDER BY merged_utc DESC;";
        _ = command.Parameters.AddWithValue("$cutoff", Format(this.clock.GetUtcNow().AddDays(-KeepDays)));

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(0), file, StringComparison.OrdinalIgnoreCase))
            {
                return reader.GetString(1);
            }
        }

        return null;
    }

    /// <summary>The ISO-8601 round-trip form in UTC, which sorts as text in time order.</summary>
    private static string Format(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
