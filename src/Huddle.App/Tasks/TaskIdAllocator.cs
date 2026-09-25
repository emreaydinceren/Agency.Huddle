using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Assigns Task ids: a per-Team prefix, derived once and stored, and a per-Team number that is
/// never reused (Spec §8.6). Stores both in the same <c>team.db</c> file as
/// <see cref="Data.PersonaModelStore"/>, in its own table, following that store's pattern.
/// </summary>
/// <remarks>
/// Deliberately synchronous, like <see cref="Data.PersonaModelStore"/>: the only callers are
/// <c>TaskStore</c>'s synchronous file-scan and write paths.
/// </remarks>
internal sealed class TaskIdAllocator
{
    private const string DdlCreateTaskPrefixes = """
        CREATE TABLE IF NOT EXISTS task_prefixes (
            team   TEXT PRIMARY KEY COLLATE NOCASE,
            prefix TEXT NOT NULL UNIQUE COLLATE NOCASE,
            next   INTEGER NOT NULL
        );
        """;

    private readonly Lock gate = new();
    private readonly string connectionString;

    public TaskIdAllocator(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string dbPath = Path.Combine(options.Value.DataDir, "team.db");
        this.connectionString = $"Data Source={dbPath}";

        string? directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using SqliteConnection connection = OpenConnection(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = DdlCreateTaskPrefixes;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>
    /// Derives a prefix candidate for a Team name: upper-cased ASCII letters and digits, leading
    /// digits skipped, the first 4 characters kept (or "TASK" if none are left), with a numeric
    /// suffix (2, 3, ...) appended until the result isn't in <paramref name="taken"/>, compared
    /// ignoring case.
    /// </summary>
    internal static string DerivePrefix(string team, IReadOnlySet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(taken);

        string letters = ExtractLetters(team);
        string basePrefix = letters.Length == 0 ? "TASK" : letters[..Math.Min(4, letters.Length)];

        if (!taken.Contains(basePrefix, StringComparer.OrdinalIgnoreCase))
        {
            return basePrefix;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = string.Create(CultureInfo.InvariantCulture, $"{basePrefix}{suffix}");
            if (!taken.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
    }

    /// <summary>Returns the stored prefix for a Team, deriving and storing one on first use.</summary>
    internal string PrefixFor(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        string key = team.ToUpperInvariant();

        lock (this.gate)
        {
            using SqliteConnection connection = OpenConnection(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

            string prefix = PrefixForCore(connection, transaction, key);
            transaction.Commit();
            return prefix;
        }
    }

    /// <summary>
    /// Allocates the next Task number for a Team: <c>max(next, highestNumberSeen + 1)</c>, then
    /// stores that value plus 1. A file copied in by hand with a higher number can't be reused,
    /// and a number is never reused after a Task file is deleted.
    /// </summary>
    internal TaskId Next(string team, int highestNumberSeen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        string key = team.ToUpperInvariant();

        lock (this.gate)
        {
            using SqliteConnection connection = OpenConnection(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

            string prefix = PrefixForCore(connection, transaction, key);

            using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE task_prefixes SET next = max(next, $h + 1) + 1
                WHERE team = $t
                RETURNING next - 1;
                """;
            update.Parameters.AddWithValue("$h", highestNumberSeen);
            update.Parameters.AddWithValue("$t", key);

            object? result = update.ExecuteScalar();
            int number = Convert.ToInt32(result, CultureInfo.InvariantCulture);

            transaction.Commit();
            return new TaskId(prefix, number);
        }
    }

    private static string PrefixForCore(SqliteConnection connection, SqliteTransaction transaction, string key)
    {
        using (SqliteCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT prefix FROM task_prefixes WHERE team = $t;";
            select.Parameters.AddWithValue("$t", key);

            if (select.ExecuteScalar() is string existing)
            {
                return existing;
            }
        }

        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand selectAll = connection.CreateCommand())
        {
            selectAll.Transaction = transaction;
            selectAll.CommandText = "SELECT prefix FROM task_prefixes;";
            using SqliteDataReader reader = selectAll.ExecuteReader();
            while (reader.Read())
            {
                _ = taken.Add(reader.GetString(0));
            }
        }

        string prefix = DerivePrefix(key, taken);

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO task_prefixes(team, prefix, next) VALUES ($t, $p, 1);";
            insert.Parameters.AddWithValue("$t", key);
            insert.Parameters.AddWithValue("$p", prefix);
            _ = insert.ExecuteNonQuery();
        }

        return prefix;
    }

    private static string ExtractLetters(string team)
    {
        List<char> chars = [];
        foreach (char c in team)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Add(char.ToUpperInvariant(c));
            }
        }

        int start = 0;
        while (start < chars.Count && char.IsAsciiDigit(chars[start]))
        {
            start++;
        }

        return new string(chars.ToArray(), start, chars.Count - start);
    }

    private static SqliteConnection OpenConnection(string connectionString)
    {
        SqliteConnection connection = new(connectionString);
        connection.Open();
        return connection;
    }
}
