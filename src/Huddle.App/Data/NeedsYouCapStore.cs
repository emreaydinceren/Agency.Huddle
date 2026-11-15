using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Keeps, per scope that sets Run inside the Team with a spend cap, how far its spend has come (ADR-0087). One row of <c>team.db</c> per
/// scope key (<c>global</c>, <c>team:Team</c> or <c>project:Team/Project</c>). Amounts are invariant-culture decimal text.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="NeedsYouLevelStore"/>. The caller serialises its read-modify-write.</remarks>
internal sealed class NeedsYouCapStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS needs_you_cap (
            scope     TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
            cap       TEXT NOT NULL,
            baseline  TEXT NOT NULL,
            allowance TEXT NOT NULL,
            asked     INTEGER NOT NULL,
            declined  INTEGER NOT NULL
        );
        """;

    private const string SelectOne = "SELECT scope, cap, baseline, allowance, asked, declined FROM needs_you_cap WHERE scope = $scope;";

    private const string SelectAsked = "SELECT scope, cap, baseline, allowance, asked, declined FROM needs_you_cap WHERE asked = 1 ORDER BY scope;";

    private const string Upsert = """
        INSERT INTO needs_you_cap(scope, cap, baseline, allowance, asked, declined) VALUES($scope, $cap, $baseline, $allowance, $asked, $declined)
        ON CONFLICT(scope) DO UPDATE SET cap = excluded.cap, baseline = excluded.baseline, allowance = excluded.allowance,
            asked = excluded.asked, declined = excluded.declined;
        """;

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public NeedsYouCapStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>Reads one scope's row.</summary>
    /// <param name="scope">The scope key, compared ignoring case.</param>
    /// <returns>The row, or <see langword="null"/> when counting has not started for the scope.</returns>
    public CapRow? Get(string scope) => this.Read(SelectOne, scope).FirstOrDefault();

    /// <summary>Reads every row with a question open.</summary>
    /// <returns>The rows, in key order.</returns>
    public IReadOnlyList<CapRow> Asked() => this.Read(SelectAsked, null);

    /// <summary>Writes a row, replacing the scope's earlier one.</summary>
    /// <param name="row">The row.</param>
    public void Save(CapRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Upsert;
        _ = command.Parameters.AddWithValue("$scope", row.Scope);
        _ = command.Parameters.AddWithValue("$cap", Text(row.Cap));
        _ = command.Parameters.AddWithValue("$baseline", Text(row.Baseline));
        _ = command.Parameters.AddWithValue("$allowance", Text(row.Allowance));
        _ = command.Parameters.AddWithValue("$asked", row.Asked ? "1" : "0");
        _ = command.Parameters.AddWithValue("$declined", row.Declined ? "1" : "0");
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Deletes a scope's row; nothing happens when it has none.</summary>
    /// <param name="scope">The scope key, compared ignoring case.</param>
    public void Delete(string scope)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM needs_you_cap WHERE scope = $scope;";
        _ = command.Parameters.AddWithValue("$scope", scope);
        _ = command.ExecuteNonQuery();
    }

    private static string Text(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);

    private static decimal Parse(string text) => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);

    private List<CapRow> Read(string sql, string? scope)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (scope is not null)
        {
            _ = command.Parameters.AddWithValue("$scope", scope);
        }

        List<CapRow> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new CapRow(
                reader.GetString(0),
                Parse(reader.GetString(1)),
                Parse(reader.GetString(2)),
                Parse(reader.GetString(3)),
                reader.GetInt32(4) != 0,
                reader.GetInt32(5) != 0));
        }

        return rows;
    }
}
