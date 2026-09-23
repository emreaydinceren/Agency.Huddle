using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="FileStateStore"/>: load, save, atomicity, corruption tolerance, the
/// lock-guarded <see cref="FileStateStore.Update"/> (finding P-18), <c>Rename</c> and
/// <c>Remove</c>, per FC §6.6.
/// </summary>
public sealed class FileStateStoreTests
{
    /// <summary>A missing file loads as <see langword="null"/>, not an exception.</summary>
    [Fact]
    public void Load_Missing_ReturnsNull()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState? loaded = store.Load("Nova");

        Assert.Null(loaded);
    }

    /// <summary>A saved state with two Rooms round-trips through <c>Load</c> unchanged.</summary>
    [Fact]
    public void SaveThenLoad_TwoRooms_RoundTrips()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState state = BuildState(
            subscribed: ["Shared/research"],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["room-alex"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nova"] = new FolderSnapshot(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
                    {
                        [@"memory\launch-date.md"] = new FileEntry(4410, new DateTimeOffset(2026, 9, 22, 14, 2, 11, TimeSpan.Zero)),
                    }),
                }),
                ["room-kelly"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nova"] = FolderSnapshot.Empty,
                }),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nova"] = new Dictionary<string, FileWriter>(FolderSnapshot.PathComparer)
                {
                    ["notes.md"] = new FileWriter("room-alex", new FileEntry(120, new DateTimeOffset(2026, 9, 22, 14, 5, 0, TimeSpan.Zero))),
                },
            });

        store.Save("Nova", state);
        FileState? loaded = store.Load("Nova");

        Assert.NotNull(loaded);
        Assert.Equal(state.Subscribed, loaded.Subscribed);
        Assert.Equal(2, loaded.Rooms.Count);
        Assert.Equal(4410, loaded.Rooms["room-alex"].Folders["Nova"].Files[@"memory\launch-date.md"].Size);
        Assert.True(loaded.Rooms.ContainsKey("room-kelly"));
        Assert.Equal("room-alex", loaded.Writers["Nova"]["notes.md"].RoomId);
    }

    /// <summary>After a round trip, a snapshot's keys use <see cref="FolderSnapshot.PathComparer"/>, not the default comparer STJ builds.</summary>
    [Fact]
    public void Load_AfterRoundTrip_SnapshotKeysUsePathComparer()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only case-insensitivity check.");
            return;
        }

        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState state = BuildState(
            subscribed: [],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nova"] = new FolderSnapshot(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
                    {
                        [@"MEMORY\A.MD"] = new FileEntry(1, DateTimeOffset.UtcNow),
                    }),
                }),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));

        store.Save("Nova", state);
        FileState? loaded = store.Load("Nova");

        Assert.NotNull(loaded);
        Assert.True(loaded.Rooms["room-1"].Folders["Nova"].Files.ContainsKey(@"memory\a.md"));
    }

    /// <summary>A file that cannot be parsed logs a warning and loads as <see langword="null"/>, rather than throwing.</summary>
    [Fact]
    public void Load_Corrupt_ReturnsNullAndLogsWarning()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        string folder = Path.Combine(dataDir.Path, "file-state");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Nova.json"), "{not json");

        FileState? loaded = store.Load("Nova");

        Assert.Null(loaded);
    }

    /// <summary>A save never leaves the temporary <c>.tmp</c> file behind.</summary>
    [Fact]
    public void Save_LeavesNoTmpFile()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        store.Save("Nova", FileState.Empty);

        string folder = Path.Combine(dataDir.Path, "file-state");
        Assert.False(File.Exists(Path.Combine(folder, "Nova.json.tmp")));
    }

    /// <summary>A save writes under <c>{DataDir}/file-state/</c>, keyed by the Agent's Name.</summary>
    [Fact]
    public void Save_WritesUnderFileStateFolderKeyedByName()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        store.Save("Nova", FileState.Empty);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "file-state", "Nova.json")));
    }

    /// <summary>The saved JSON is indented and leaves an em-dash unescaped, so a Human can read it.</summary>
    [Fact]
    public void Save_JsonIsHumanReadable()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState state = BuildState(
            subscribed: [],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["01J8ALEX—room"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));

        store.Save("Nova", state);

        string json = File.ReadAllText(Path.Combine(dataDir.Path, "file-state", "Nova.json"));
        Assert.Contains('—', json);
        Assert.DoesNotContain("\\u2014", json, StringComparison.Ordinal);
        Assert.Contains('\n', json);
    }

    /// <summary>An <c>Update</c> applies the change under the lock and saves the result, returning it.</summary>
    [Fact]
    public void Update_AppliesChangeUnderLock_AndSaves()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState updated = store.Update("Nova", _ => BuildState(
            subscribed: ["Nova"],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal),
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)));

        Assert.Equal(["Nova"], updated.Subscribed);
        FileState? loaded = store.Load("Nova");
        Assert.NotNull(loaded);
        Assert.Equal(["Nova"], loaded.Subscribed);
    }

    /// <summary>An <c>Update</c> against a missing file passes <see langword="null"/> to the change function.</summary>
    [Fact]
    public void Update_OnMissing_PassesNull()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        FileState? seen = FileState.Empty;
        bool sawNull = false;

        store.Update("Nova", current =>
        {
            sawNull = current is null;
            seen = current;
            return FileState.Empty;
        });

        Assert.True(sawNull);
        Assert.Null(seen);
    }

    /// <summary>Two concurrent <c>Update</c> calls, each replacing a different Room's baseline, both survive.</summary>
    [Fact]
    public async Task Update_ConcurrentUpdatesToTwoRooms_BothSurvive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        store.Save("Nova", FileState.Empty);

        const int iterations = 50;

        Task first = Task.Run(
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    store.Update("Nova", current => WithRoom(current, "room-a"));
                }
            },
            cancellationToken);

        Task second = Task.Run(
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    store.Update("Nova", current => WithRoom(current, "room-b"));
                }
            },
            cancellationToken);

        await Task.WhenAll(first, second);

        FileState? loaded = store.Load("Nova");
        Assert.NotNull(loaded);
        Assert.True(loaded.Rooms.ContainsKey("room-a"));
        Assert.True(loaded.Rooms.ContainsKey("room-b"));
    }

    /// <summary><c>Rename</c> moves the Agent's own file to the new Name.</summary>
    [Fact]
    public void Rename_MovesTheFile()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        store.Save("Nova", FileState.Empty);

        store.Rename("Nova", "Nora");

        string folder = Path.Combine(dataDir.Path, "file-state");
        Assert.False(File.Exists(Path.Combine(folder, "Nova.json")));
        Assert.True(File.Exists(Path.Combine(folder, "Nora.json")));
    }

    /// <summary><c>Rename</c> rewrites another Agent's <c>subscribed</c> entries and Room folder keys equal to the old Name, case-insensitively.</summary>
    [Fact]
    public void Rename_RewritesOtherFilesSubscribedAndFolderKeysAndWriterKeys_CaseInsensitively()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState coach = BuildState(
            subscribed: ["nova"],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nova"] = new FolderSnapshot(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer)
                    {
                        ["a.md"] = new FileEntry(1, DateTimeOffset.UtcNow),
                    }),
                }),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nova"] = new Dictionary<string, FileWriter>(FolderSnapshot.PathComparer)
                {
                    ["a.md"] = new FileWriter("room-1", new FileEntry(1, DateTimeOffset.UtcNow)),
                },
            });
        store.Save("Coach", coach);
        store.Save("Nova", FileState.Empty);

        store.Rename("Nova", "Nora");

        FileState? reloaded = store.Load("Coach");
        Assert.NotNull(reloaded);
        Assert.Equal(["Nora"], reloaded.Subscribed);
        Assert.True(reloaded.Rooms["room-1"].Folders.ContainsKey("Nora"));
        Assert.False(reloaded.Rooms["room-1"].Folders.ContainsKey("Nova"));
        Assert.Equal(1, reloaded.Rooms["room-1"].Folders["Nora"].Files["a.md"].Size);
        Assert.True(reloaded.Writers.ContainsKey("Nora"));
        Assert.False(reloaded.Writers.ContainsKey("Nova"));
    }

    /// <summary>A Room id equal to the renamed Name (an unlikely coincidence) is never rewritten by <c>Rename</c>.</summary>
    [Fact]
    public void Rename_NeverRewritesRoomIds()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState coach = BuildState(
            subscribed: [],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["Nova"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));
        store.Save("Coach", coach);
        store.Save("Nova", FileState.Empty);

        store.Rename("Nova", "Nora");

        FileState? reloaded = store.Load("Coach");
        Assert.NotNull(reloaded);
        Assert.True(reloaded.Rooms.ContainsKey("Nova"));
    }

    /// <summary><c>Remove</c> deletes the Agent's own file and strips its Name from every other file's <c>subscribed</c> and Room folders.</summary>
    [Fact]
    public void Remove_DeletesFileAndStripsNameFromOthers()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);

        FileState coach = BuildState(
            subscribed: ["nova"],
            rooms: new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
            {
                ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Nova"] = FolderSnapshot.Empty,
                }),
            },
            writers: new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));
        store.Save("Coach", coach);
        store.Save("Nova", FileState.Empty);

        store.Remove("Nova");

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "file-state", "Nova.json")));
        FileState? reloaded = store.Load("Coach");
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.Subscribed);
        Assert.False(reloaded.Rooms["room-1"].Folders.ContainsKey("Nova"));
    }

    /// <summary>Builds a <see cref="FileState"/> with the given fields, for test arrangement.</summary>
    private static FileState BuildState(
        IReadOnlyList<string> subscribed,
        IReadOnlyDictionary<string, RoomBaseline> rooms,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FileWriter>> writers)
    {
        return new FileState(subscribed, rooms, writers);
    }

    /// <summary>Returns <paramref name="current"/> (or an empty state) with <paramref name="roomId"/>'s baseline set to an empty <see cref="RoomBaseline"/>.</summary>
    private static FileState WithRoom(FileState? current, string roomId)
    {
        FileState state = current ?? FileState.Empty;
        Dictionary<string, RoomBaseline> rooms = new(state.Rooms, StringComparer.Ordinal)
        {
            [roomId] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)),
        };

        return new FileState(state.Subscribed, rooms, state.Writers);
    }
}
