using Microsoft.Data.Sqlite;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Pins the <c>Microsoft.Data.Sqlite</c> version against dotnet/efcore#39008. Up to 10.0.12 the connection pool could lend
/// one native handle to two connections opened at once, so a store's statement still running on the handle made
/// <c>SqliteTeamDirectory.InitializeAsync</c>'s <c>PRAGMA journal_mode=WAL</c> fail with "cannot change into wal mode from
/// within a transaction" during host start-up. Every store in <c>team.db</c> opens its own pooled connection, so the
/// guarantee this test checks is one the whole data layer rests on.
/// </summary>
public sealed class SqliteConnectionPoolTests
{
    private const int Connections = 16;

    /// <summary>On 10.0.12 a shared handle showed after 31 to 315 rounds, and 400 rounds missed it once in six; 800 take about 4 s on a fixed pool.</summary>
    private const int Rounds = 800;

    /// <summary>Connections opened together from an empty pool each get a native handle of their own.</summary>
    [Fact]
    public void Open_ConcurrentlyFromAnEmptyPool_NeverSharesAHandle()
    {
        using TempDataDir dir = new();

        for (int round = 0; round < Rounds; round++)
        {
            // A new file per round is a new, empty pool, the state in which the pool reclaims a connection it thinks leaked.
            string path = Path.Combine(dir.Path, $"pool{round}.db");
            SqliteConnection[] connections = OpenTogether($"Data Source={path}");
            try
            {
                // The pool hands each owner its connection's one handle object, so a shared handle is a shared reference.
                int distinct = connections.Select(static connection => (object?)connection.Handle).Distinct(ReferenceEqualityComparer.Instance).Count();
                Assert.True(distinct == Connections, $"Round {round}: {Connections} connections shared {distinct} native handles.");
            }
            finally
            {
                foreach (SqliteConnection connection in connections)
                {
                    connection.Dispose();
                }

                SqliteConnection.ClearPool(connections[0]);
            }
        }
    }

    /// <summary>Opens <see cref="Connections"/> connections on real threads released together by one event.</summary>
    /// <param name="connectionString">The connection string every connection uses.</param>
    private static SqliteConnection[] OpenTogether(string connectionString)
    {
        SqliteConnection[] connections = [.. Enumerable.Range(0, Connections).Select(_ => new SqliteConnection(connectionString))];
        using ManualResetEventSlim go = new();
        Thread[] threads = [.. connections.Select(connection => new Thread(() =>
        {
            go.Wait();
            connection.Open();
        }))];
        foreach (Thread thread in threads)
        {
            thread.Start();
        }

        go.Set();
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        return connections;
    }
}
