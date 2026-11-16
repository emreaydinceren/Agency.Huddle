using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// The ledger a spend cap counts against (ADR-0088): the USD each scope has had spent in it, added to at the moment a Persona spends. One row of
/// <c>team.db</c> per scope key (<c>global</c> or <c>team:&lt;teamId&gt;</c>), amounts as invariant-culture decimal text. A scope that a
/// Persona leaves keeps what it was already charged, and a Persona that joins is charged only from then on.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="NeedsYouCapStore"/>. Adds are serialised here, because spend events can arrive on any thread.</remarks>
internal sealed class NeedsYouSpendStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS needs_you_spend (
            scope TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
            usd   TEXT NOT NULL
        );
        """;

    private const string Upsert = """
        INSERT INTO needs_you_spend(scope, usd) VALUES($scope, $usd)
        ON CONFLICT(scope) DO UPDATE SET usd = excluded.usd;
        """;

    private readonly Lock gate = new();
    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public NeedsYouSpendStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>What a scope has been charged in USD.</summary>
    /// <param name="scope">The scope key, compared ignoring case.</param>
    /// <returns>The amount, or zero when the scope has no row.</returns>
    public decimal Get(string scope)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Read(connection, null, scope);
    }

    /// <summary>Charges <paramref name="delta"/> USD to each scope, in one transaction.</summary>
    /// <param name="scopes">The scope keys.</param>
    /// <param name="delta">The amount to add; the caller passes a positive figure.</param>
    public void Add(IReadOnlyList<string> scopes, decimal delta)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            return;
        }

        lock (this.gate)
        {
            using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (string scope in scopes)
            {
                decimal total = Read(connection, transaction, scope) + delta;
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = Upsert;
                _ = command.Parameters.AddWithValue("$scope", scope);
                _ = command.Parameters.AddWithValue("$usd", total.ToString(CultureInfo.InvariantCulture));
                _ = command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private static decimal Read(SqliteConnection connection, SqliteTransaction? transaction, string scope)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT usd FROM needs_you_spend WHERE scope = $scope;";
        _ = command.Parameters.AddWithValue("$scope", scope);
        return command.ExecuteScalar() is string text ? decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture) : 0m;
    }
}
