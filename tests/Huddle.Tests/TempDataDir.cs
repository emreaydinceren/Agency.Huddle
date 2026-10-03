using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;

namespace Agency.Huddle.Tests;

/// <summary>
/// Creates an isolated, disposable data directory under the OS temp folder for
/// tests that exercise <see cref="TeamOptions"/>-driven storage (file chat store,
/// SQLite directory, pipe host, web host).
/// </summary>
public sealed class TempDataDir : IDisposable
{
    public TempDataDir()
    {
        this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "team-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public IOptions<TeamOptions> Options()
    {
        return Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.Path });
    }

    /// <summary>
    /// Closes the pooled SQLite connections of this directory's databases, then deletes the directory.
    /// <c>Microsoft.Data.Sqlite</c> keeps one open connection per database file for the life of the
    /// process, so without this every test's <c>team.db</c> stays open (locking the file, so the delete
    /// below fails silently and the directory leaks) and all of them are closed one by one in a
    /// <c>ProcessExit</c> handler after the last test - seconds of exit work that the xunit runner
    /// reports as leftover foreground threads. Only this directory's pools are cleared, never all pools:
    /// <c>SqliteConnection.ClearAllPools</c> would close connections other, parallel tests are using.
    /// </summary>
    public void Dispose()
    {
        ClearPools(this.Path);

        try
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory; a leftover is not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of a temp directory; a leftover is not a test failure.
        }
    }

    /// <summary>Clears the connection pool of every SQLite database file under <paramref name="directory"/>, using the connection string the stores build (<c>Data Source={path}</c>) so the pool key matches.</summary>
    private static void ClearPools(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string database in Directory.EnumerateFiles(directory, "*.db", SearchOption.AllDirectories))
        {
            using SqliteConnection connection = new($"Data Source={database}");
            SqliteConnection.ClearPool(connection);
        }
    }
}