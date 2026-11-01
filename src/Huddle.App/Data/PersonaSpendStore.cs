using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores spend per Persona, per Task and per currency in <c>team.db</c> so it survives a restart.
/// Amounts are kept as invariant-culture decimal text, never as SQLite's binary floating point, so
/// <c>0.1 + 0.2</c> is exactly <c>0.3</c>. Rows with no Task use the empty string as their Task id.
/// </summary>
/// <remarks>
/// Deliberately synchronous, in the style of <see cref="PersonaModelStore"/>. An add is a
/// read-modify-write of one row, so every write runs in a transaction under a process-wide lock
/// (store instances over the same file share it, as the connection string is the only state).
/// </remarks>
public sealed class PersonaSpendStore
{
    private const string DdlCreatePersonaSpend = """
        CREATE TABLE IF NOT EXISTS persona_spend (
            persona_name TEXT NOT NULL COLLATE NOCASE,
            task_id      TEXT NOT NULL DEFAULT '',
            currency     TEXT NOT NULL,
            amount       TEXT NOT NULL,
            PRIMARY KEY (persona_name, task_id, currency)
        );
        """;

    private static readonly Lock WriteGate = new();

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public PersonaSpendStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreatePersonaSpend);
    }

    /// <summary>
    /// Adds <paramref name="amount"/> into the row for the Persona, Task and currency, creating it if
    /// needed. An <paramref name="amount"/> that is zero or negative is ignored.
    /// </summary>
    /// <param name="personaName">The Persona that spent.</param>
    /// <param name="taskId">The Task the spend belongs to, or <c>null</c> when it belongs to none.</param>
    /// <param name="currency">The currency code.</param>
    /// <param name="amount">The amount to add.</param>
    public void Add(string personaName, string? taskId, string currency, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (amount <= 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction();
            AddRow(connection, transaction, personaName, taskId ?? string.Empty, currency, amount);
            transaction.Commit();
        }
    }

    /// <summary>Returns the Persona's total per currency across all Tasks.</summary>
    /// <param name="personaName">The Persona name, compared case-insensitively.</param>
    /// <returns>One entry per currency; empty when nothing is stored.</returns>
    public IReadOnlyList<StoredSpend> ForPersona(string personaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        const string Sql = "SELECT currency, amount FROM persona_spend WHERE persona_name = $key;";
        return this.Query(Sql, personaName);
    }

    /// <summary>Returns the combined total per currency of the given Personas.</summary>
    /// <param name="personaNames">The Persona names; duplicates (ignoring ASCII case, as SQLite NOCASE does) count once.</param>
    /// <returns>One entry per currency; empty when nothing is stored.</returns>
    public IReadOnlyList<StoredSpend> ForPersonas(IEnumerable<string> personaNames)
    {
        ArgumentNullException.ThrowIfNull(personaNames);

        Dictionary<string, decimal> totals = new(StringComparer.Ordinal);
        foreach (string name in personaNames.Where(static n => !string.IsNullOrWhiteSpace(n)).Distinct(SqliteNoCaseComparer.Instance))
        {
            foreach (StoredSpend spend in this.ForPersona(name))
            {
                totals[spend.Currency] = totals.GetValueOrDefault(spend.Currency) + spend.Amount;
            }
        }

        return [.. totals.Select(static pair => new StoredSpend(pair.Key, pair.Value))];
    }

    /// <summary>
    /// Moves every row of <paramref name="oldName"/> to <paramref name="newName"/>, adding into any
    /// row the new name already has for the same Task and currency.
    /// </summary>
    /// <param name="oldName">The current Persona name.</param>
    /// <param name="newName">The new Persona name.</param>
    public void Rename(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        lock (WriteGate)
        {
            using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction();

            List<(string TaskId, string Currency, decimal Amount)> moved = [];
            using (SqliteCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = "SELECT task_id, currency, amount FROM persona_spend WHERE persona_name = $name;";
                select.Parameters.AddWithValue("$name", oldName);
                using SqliteDataReader reader = select.ExecuteReader();
                while (reader.Read())
                {
                    moved.Add((reader.GetString(0), reader.GetString(1), ParseAmount(reader.GetString(2))));
                }
            }

            DeleteRows(connection, transaction, oldName);
            foreach ((string taskId, string currency, decimal amount) in moved)
            {
                AddRow(connection, transaction, newName, taskId, currency, amount);
            }

            transaction.Commit();
        }
    }

    /// <summary>Deletes every row of the Persona.</summary>
    /// <param name="personaName">The Persona name, compared case-insensitively.</param>
    public void Remove(string personaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        lock (WriteGate)
        {
            using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction();
            DeleteRows(connection, transaction, personaName);
            transaction.Commit();
        }
    }

    private static decimal ParseAmount(string text) => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static void DeleteRows(SqliteConnection connection, SqliteTransaction transaction, string personaName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM persona_spend WHERE persona_name = $name;";
        command.Parameters.AddWithValue("$name", personaName);
        command.ExecuteNonQuery();
    }

    private static void AddRow(SqliteConnection connection, SqliteTransaction transaction, string personaName, string taskId, string currency, decimal amount)
    {
        decimal total = amount;
        using (SqliteCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT amount FROM persona_spend WHERE persona_name = $name AND task_id = $task AND currency = $currency;";
            select.Parameters.AddWithValue("$name", personaName);
            select.Parameters.AddWithValue("$task", taskId);
            select.Parameters.AddWithValue("$currency", currency);
            if (select.ExecuteScalar() is string existing)
            {
                total += ParseAmount(existing);
            }
        }

        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO persona_spend(persona_name, task_id, currency, amount) VALUES($name, $task, $currency, $amount)
            ON CONFLICT(persona_name, task_id, currency) DO UPDATE SET amount = excluded.amount;
            """;
        upsert.Parameters.AddWithValue("$name", personaName);
        upsert.Parameters.AddWithValue("$task", taskId);
        upsert.Parameters.AddWithValue("$currency", currency);
        upsert.Parameters.AddWithValue("$amount", total.ToString(CultureInfo.InvariantCulture));
        upsert.ExecuteNonQuery();
    }

    private List<StoredSpend> Query(string sql, string key)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$key", key);

        Dictionary<string, decimal> totals = new(StringComparer.Ordinal);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string currency = reader.GetString(0);
            totals[currency] = totals.GetValueOrDefault(currency) + ParseAmount(reader.GetString(1));
        }

        return [.. totals.Select(static pair => new StoredSpend(pair.Key, pair.Value))];
    }

    /// <summary>Compares names the way SQLite <c>NOCASE</c> does: only the ASCII letters A-Z fold, every other character is compared ordinally.</summary>
    private sealed class SqliteNoCaseComparer : IEqualityComparer<string>
    {
        /// <summary>The shared instance.</summary>
        public static readonly SqliteNoCaseComparer Instance = new();

        /// <inheritdoc/>
        public bool Equals(string? x, string? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return string.Equals(Fold(x), Fold(y), StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public int GetHashCode(string obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return Fold(obj).GetHashCode(StringComparison.Ordinal);
        }

        /// <summary>Lower-cases the ASCII letters of <paramref name="text"/> and leaves all other characters alone.</summary>
        private static string Fold(string text)
        {
            return string.Create(text.Length, text, static (span, source) =>
            {
                for (int i = 0; i < source.Length; i++)
                {
                    char c = source[i];
                    span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
                }
            });
        }
    }
}
