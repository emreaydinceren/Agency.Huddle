using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// <see cref="MemoryTidyStateStore"/> (Memory Tidy spec §6.5): both tables are made whole with every column, each column round-trips,
/// and a folder's rows can be renamed or deleted.
/// </summary>
public sealed class MemoryTidyStateStoreTests : IDisposable
{
    private static readonly DateTimeOffset Tidied = new(2026, 11, 22, 3, 4, 5, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <summary>Closes the database and deletes the temp folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    /// <summary>A fresh database holds nothing and creates both tables with every column of the spec.</summary>
    [Fact]
    public void FreshDatabase_IsEmpty_AndBothTablesHaveEveryColumn()
    {
        MemoryTidyStateStore store = this.Store();

        Assert.Null(store.GetState("agent:Nova"));
        Assert.Empty(store.ListStates());
        Assert.Empty(store.GetFiles("agent:Nova"));
        Assert.Equal(
            ["failed_fingerprint", "failures", "fingerprint", "folder_key", "offset", "outcome", "tidied_utc"],
            this.Columns("memory_tidy_state"));
        Assert.Equal(["file", "folder_key", "version", "written_day"], this.Columns("memory_tidy_files"));
    }

    /// <summary>Every column of a state row survives a write and a read, and a second write replaces the row.</summary>
    [Fact]
    public void PutState_RoundTripsEveryColumn_AndReplaces()
    {
        MemoryTidyStateStore store = this.Store();
        MemoryTidyState full = new("agent:Nova", "fp-1", Tidied, "applied", 2, "fp-bad", 7);

        store.PutState(full);
        store.PutState(new MemoryTidyState("huddle"));

        Assert.Equal(full, store.GetState("agent:Nova"));
        Assert.Equal(new MemoryTidyState("huddle", null, null, null, 0, null, 0), store.GetState("huddle"));
        store.PutState(full with { Fingerprint = "fp-2", Failures = 0, Offset = 0, TidiedUtc = null });
        Assert.Equal(new MemoryTidyState("agent:Nova", "fp-2", null, "applied", 0, "fp-bad", 0), store.GetState("agent:Nova"));
        Assert.Equal(["agent:Nova", "huddle"], store.ListStates().Select(static state => state.FolderKey));
    }

    /// <summary>Every column of a file row survives a write and a read; a second write replaces the version and the day; rows are per folder.</summary>
    [Fact]
    public void PutFile_RoundTripsEveryColumn_PerFolder()
    {
        MemoryTidyStateStore store = this.Store();
        MemoryTidyFileRow row = new("agent:Nova", "pricing.md", "v1", new DateOnly(2026, 10, 3));

        store.PutFile(row);
        store.PutFile(new MemoryTidyFileRow("agent:Nova", "alpha.md", "v2", new DateOnly(2026, 11, 1)));
        store.PutFile(new MemoryTidyFileRow("huddle", "pricing.md", "v3", new DateOnly(2026, 1, 2)));
        store.PutFile(row with { Version = "v9", WrittenDay = new DateOnly(2026, 11, 20) });

        IReadOnlyList<MemoryTidyFileRow> nova = store.GetFiles("agent:Nova");
        Assert.Equal(["alpha.md", "pricing.md"], nova.Select(static file => file.File));
        Assert.Equal(new MemoryTidyFileRow("agent:Nova", "pricing.md", "v9", new DateOnly(2026, 11, 20)), nova[1]);
        Assert.Equal(new MemoryTidyFileRow("huddle", "pricing.md", "v3", new DateOnly(2026, 1, 2)), Assert.Single(store.GetFiles("huddle")));
    }

    /// <summary>A file row can be removed alone.</summary>
    [Fact]
    public void RemoveFile_ForgetsOnlyThatFile()
    {
        MemoryTidyStateStore store = this.Store();
        store.PutFile(new MemoryTidyFileRow("huddle", "a.md", "v1", new DateOnly(2026, 1, 1)));
        store.PutFile(new MemoryTidyFileRow("huddle", "b.md", "v1", new DateOnly(2026, 1, 1)));

        store.RemoveFile("huddle", "a.md");

        Assert.Equal("b.md", Assert.Single(store.GetFiles("huddle")).File);
    }

    /// <summary>Deleting a folder removes its state row and every file row and leaves other folders alone.</summary>
    [Fact]
    public void DeleteFolder_RemovesBothTablesRows_OfThatFolderOnly()
    {
        MemoryTidyStateStore store = this.Store();
        store.PutState(new MemoryTidyState("agent:Nova", "fp"));
        store.PutState(new MemoryTidyState("agent:Ben", "fp"));
        store.PutFile(new MemoryTidyFileRow("agent:Nova", "a.md", "v1", new DateOnly(2026, 1, 1)));
        store.PutFile(new MemoryTidyFileRow("agent:Ben", "a.md", "v1", new DateOnly(2026, 1, 1)));

        store.DeleteFolder("agent:Nova");

        Assert.Null(store.GetState("agent:Nova"));
        Assert.Empty(store.GetFiles("agent:Nova"));
        Assert.NotNull(store.GetState("agent:Ben"));
        Assert.Single(store.GetFiles("agent:Ben"));
    }

    /// <summary>Renaming a folder moves its state row and its file rows to the new key, whole, and leaves nothing under the old one.</summary>
    [Fact]
    public void RenameFolder_MovesBothTables_Whole()
    {
        MemoryTidyStateStore store = this.Store();
        MemoryTidyState state = new("agent:Nova", "fp-1", Tidied, "applied", 1, "fp-bad", 4);
        MemoryTidyFileRow file = new("agent:Nova", "a.md", "v1", new DateOnly(2026, 3, 4));
        store.PutState(state);
        store.PutFile(file);
        store.PutState(new MemoryTidyState("agent:Ben", "other"));

        store.RenameFolder("agent:Nova", "agent:Nova2");

        Assert.Null(store.GetState("agent:Nova"));
        Assert.Empty(store.GetFiles("agent:Nova"));
        Assert.Equal(state with { FolderKey = "agent:Nova2" }, store.GetState("agent:Nova2"));
        Assert.Equal(file with { FolderKey = "agent:Nova2" }, Assert.Single(store.GetFiles("agent:Nova2")));
        Assert.Equal("other", store.GetState("agent:Ben")?.Fingerprint);
    }

    /// <summary>Renaming onto a key that already has rows replaces them rather than failing on the key.</summary>
    [Fact]
    public void RenameFolder_OntoAnExistingKey_ReplacesIt()
    {
        MemoryTidyStateStore store = this.Store();
        store.PutState(new MemoryTidyState("agent:Nova", "new"));
        store.PutState(new MemoryTidyState("agent:Old", "stale"));
        store.PutFile(new MemoryTidyFileRow("agent:Nova", "a.md", "v-new", new DateOnly(2026, 1, 1)));
        store.PutFile(new MemoryTidyFileRow("agent:Old", "a.md", "v-stale", new DateOnly(2026, 1, 1)));
        store.PutFile(new MemoryTidyFileRow("agent:Old", "b.md", "v-stale", new DateOnly(2026, 1, 1)));

        store.RenameFolder("agent:Nova", "agent:Old");

        Assert.Equal("new", store.GetState("agent:Old")?.Fingerprint);
        Assert.Equal("v-new", Assert.Single(store.GetFiles("agent:Old")).Version);
    }

    /// <summary>A second store over the same data folder sees what the first wrote (the table is not recreated empty).</summary>
    [Fact]
    public void ASecondStore_SeesWhatTheFirstWrote()
    {
        this.Store().PutState(new MemoryTidyState("huddle", "fp", Tidied, "done", 0, null, 3));

        Assert.Equal(new MemoryTidyState("huddle", "fp", Tidied, "done", 0, null, 3), this.Store().GetState("huddle"));
    }

    private MemoryTidyStateStore Store() => new(this.dir.Options());

    private List<string> Columns(string table)
    {
        using SqliteConnection connection = SqliteTable.Connect($"Data Source={Path.Combine(this.dir.Path, "team.db")}");
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{table}') ORDER BY name;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> names = [];
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
