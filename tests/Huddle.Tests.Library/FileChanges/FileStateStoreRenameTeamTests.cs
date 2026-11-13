using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// <see cref="FileStateStore.RenameTeam"/> (Team Rename Spec 4, Ledgers): in every Agent's state file the <c>team:&lt;label&gt;</c> folder
/// keys, the Writers keys and the Subscribed paths under the Team's folder follow the new name; unrelated keys and Room ids stay.
/// </summary>
public sealed class FileStateStoreRenameTeamTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static FileState Seed() => new(
        ["Teams/Growth/research", "Teams/Growth2/other", "Shared/Growth"],
        new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
        {
            ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["team:Growth"] = Snapshot("plan.md"),
                ["team:Ops"] = Snapshot("ops.md"),
                ["Nova"] = Snapshot("own.md"),
            }),
        },
        new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase)
        {
            ["team:Growth"] = new Dictionary<string, FileWriter>(FolderSnapshot.PathComparer) { ["plan.md"] = new FileWriter("room-1", new FileEntry(1, Stamp)) },
            ["team:Ops"] = new Dictionary<string, FileWriter>(FolderSnapshot.PathComparer) { ["ops.md"] = new FileWriter("room-1", new FileEntry(1, Stamp)) },
        });

    private static FolderSnapshot Snapshot(string file) =>
        new(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer) { [file] = new FileEntry(1, Stamp) });

    private static FileStateStore SeededStore(TempDataDir dataDir)
    {
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        store.Save("Nova", Seed());
        store.Save("Coach", Seed());
        return store;
    }

    /// <summary>The <c>team:</c> folder key of every Room baseline follows the new label, with its files, and the Room id is unchanged.</summary>
    [Fact]
    public void RenameTeam_RewritesTeamFolderKeysInEveryAgentsFile()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = SeededStore(dataDir);

        store.RenameTeam("Growth", "Revenue", "Teams/Growth", "Teams/Revenue");

        foreach (string agent in new[] { "Nova", "Coach" })
        {
            FileState? state = store.Load(agent);
            Assert.NotNull(state);
            IReadOnlyDictionary<string, FolderSnapshot> folders = state.Rooms["room-1"].Folders;
            Assert.True(folders.ContainsKey("team:Revenue"));
            Assert.False(folders.ContainsKey("team:Growth"));
            Assert.True(folders["team:Revenue"].Files.ContainsKey("plan.md"));
        }
    }

    /// <summary>The Writers key follows the new label and keeps its entries.</summary>
    [Fact]
    public void RenameTeam_RewritesWritersKey()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = SeededStore(dataDir);

        store.RenameTeam("Growth", "Revenue", "Teams/Growth", "Teams/Revenue");

        FileState? state = store.Load("Nova");
        Assert.NotNull(state);
        Assert.Equal("room-1", state.Writers["team:Revenue"]["plan.md"].RoomId);
        Assert.False(state.Writers.ContainsKey("team:Growth"));
    }

    /// <summary>Only a Subscribed path under the old folder moves; a sibling that merely starts with the same letters and an unrelated path stay.</summary>
    [Fact]
    public void RenameTeam_RewritesSubscribedPathsUnderThePrefixOnly()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = SeededStore(dataDir);

        store.RenameTeam("Growth", "Revenue", "Teams/Growth", "Teams/Revenue");

        FileState? state = store.Load("Nova");
        Assert.NotNull(state);
        Assert.Equal(["Teams/Revenue/research", "Teams/Growth2/other", "Shared/Growth"], state.Subscribed);
    }

    /// <summary>Another Team's key, an Agent's own-folder key and the Room id are untouched.</summary>
    [Fact]
    public void RenameTeam_LeavesUnrelatedKeysAlone()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = SeededStore(dataDir);

        store.RenameTeam("Growth", "Revenue", "Teams/Growth", "Teams/Revenue");

        FileState? state = store.Load("Nova");
        Assert.NotNull(state);
        Assert.Equal(["room-1"], state.Rooms.Keys);
        Assert.True(state.Rooms["room-1"].Folders.ContainsKey("team:Ops"));
        Assert.True(state.Rooms["room-1"].Folders.ContainsKey("Nova"));
        Assert.True(state.Writers.ContainsKey("team:Ops"));
    }
}
