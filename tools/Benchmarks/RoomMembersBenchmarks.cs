using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tools.Benchmarks;

/// <summary>
/// perf-audit R2: how a sidebar list gets its rows' Members, against a real SQLite directory holding 16 Rooms (the
/// software-co seed's count) of three Members each. <see cref="Old"/> is one <c>GetRoomMembersAsync</c> per row, as each
/// <c>RoomNavRow</c> did on every parameter set; <see cref="New"/> is the list's one <c>GetMembersByRoomAsync</c>.
/// </summary>
[MemoryDiagnoser]
public class RoomMembersBenchmarks
{
    private const int RoomCount = 16;

    private readonly List<string> roomIds = [];
    private string dataDir = string.Empty;
    private SqliteTeamDirectory? directory;

    private SqliteTeamDirectory Store => this.directory ?? throw new InvalidOperationException("Setup has not run.");

    /// <summary>Creates the database in a temp folder with 16 Rooms, each holding the Human and two Agents.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.dataDir = Directory.CreateTempSubdirectory("huddle-bench-").FullName;
        this.directory = new SqliteTeamDirectory(Options.Create(new TeamOptions { DataDir = this.dataDir }));
        await this.Store.InitializeAsync("You").ConfigureAwait(false);
        for (int index = 0; index < RoomCount; index++)
        {
            string first = await this.AgentIdAsync($"agent{index}a").ConfigureAwait(false);
            string second = await this.AgentIdAsync($"agent{index}b").ConfigureAwait(false);
            Room room = await this.Store.CreateRoomAsync($"room{index}", [KnownIds.Human, first, second]).ConfigureAwait(false);
            this.roomIds.Add(room.Id);
        }
    }

    /// <summary>Deletes the temp folder.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(this.dataDir, recursive: true);
    }

    /// <summary>The code before the fix: one query per row.</summary>
    [Benchmark(Baseline = true)]
    public async Task<int> Old()
    {
        int members = 0;
        foreach (string roomId in this.roomIds)
        {
            members += (await this.Store.GetRoomMembersAsync(roomId).ConfigureAwait(false)).Count;
        }

        return members;
    }

    /// <summary>The shipped code: one query for the whole list.</summary>
    [Benchmark]
    public async Task<int> New() =>
        (await this.Store.GetMembersByRoomAsync().ConfigureAwait(false)).Values.Sum(static members => members.Count);

    /// <summary>Registers an Agent and returns its id.</summary>
    private async Task<string> AgentIdAsync(string name) =>
        (await this.Store.UpsertAgentUserAsync(name, null).ConfigureAwait(false))?.Id
            ?? throw new InvalidOperationException($"Agent '{name}' was not created.");
}
