using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.Seeder;
using Agency.Huddle.Seeder.Scenarios;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>A scenario seeded into a fresh temp folder, deleted again on dispose.</summary>
internal sealed class SeededFolder : IDisposable
{
    /// <summary>The run date every seeded offset is measured from; fixed so tests are reproducible.</summary>
    internal static readonly DateOnly Today = new(2026, 9, 29);

    private SeededFolder(string root, SeedSummary summary)
    {
        this.Root = root;
        this.Summary = summary;
    }

    /// <summary>The seed root.</summary>
    internal string Root { get; }

    /// <summary>The data folder Huddle would be pointed at.</summary>
    internal string DataDir => this.Summary.DataDir;

    /// <summary>What the seeder reported.</summary>
    internal SeedSummary Summary { get; }

    /// <summary>The options every store in a test is built over.</summary>
    /// <returns>Options whose <c>DataDir</c> is the seeded data folder and whose Human is the scenario's.</returns>
    internal IOptions<TeamOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.DataDir, HumanName = this.Summary.Plan.HumanName });

    /// <summary>Seeds the default scenario into a new temp folder.</summary>
    /// <param name="ct">Cancels the seeding.</param>
    /// <returns>The seeded folder.</returns>
    internal static async Task<SeededFolder> CreateAsync(CancellationToken ct)
    {
        string root = Path.Combine(Path.GetTempPath(), "seed-tests", Guid.NewGuid().ToString("N"), "root");
        IScenario scenario = ScenarioCatalog.Find(ScenarioCatalog.DefaultName)
            ?? throw new InvalidOperationException("The default scenario is not registered.");
        SeedSummary summary = await SeedRunner.RunAsync(new SeedRequest(scenario, root, Today), [], ct);
        return new SeededFolder(root, summary);
    }

    /// <summary>Closes the database's pooled connections, then deletes the folder.</summary>
    public void Dispose()
    {
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(this.DataDir, "team.db")}"));

        try
        {
            string? parent = Path.GetDirectoryName(this.Root);
            if (parent is not null && Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
        catch (IOException)
        {
            // A watcher or pooled handle still held the folder; the OS temp cleaner removes what is left.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: leaking a temp folder must not fail a test that already passed.
        }
    }
}
