using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// <see cref="FileStateStore.RenamePrefix"/> (Team Rename tr11 item 5): in every Agent's state file the Subscribed paths equal to
/// or under the old prefix follow the new one; the <c>team:</c> keys, Room ids and unrelated paths stay.
/// </summary>
public sealed class FileStateStoreRenamePrefixTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static FileState Seed() => new(
        ["Teams/Growth/Launch", "Teams/Growth/Launch/docs", "teams/growth/launch/more", "Teams/Growth/Launch2/other", "Teams/Ops/Launch"],
        new Dictionary<string, RoomBaseline>(StringComparer.Ordinal)
        {
            ["room-1"] = new RoomBaseline(new Dictionary<string, FolderSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["team:Growth"] = new FolderSnapshot(new Dictionary<string, FileEntry>(FolderSnapshot.PathComparer) { ["Launch/plan.md"] = new FileEntry(1, Stamp) }),
            }),
        },
        new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>A Subscribed path equal to or under the prefix (ignoring case) is swapped; a sibling whose name starts the same and another Team's path stay.</summary>
    [Fact]
    public void RenamePrefix_SwapsOnlyPathsUnderThePrefix_InEveryAgentsFile()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        store.Save("Nova", Seed());
        store.Save("Coach", Seed());

        store.RenamePrefix("Teams/Growth/Launch", "Teams/Growth/Debut");

        foreach (string agent in new[] { "Nova", "Coach" })
        {
            FileState? state = store.Load(agent);
            Assert.NotNull(state);
            Assert.Equal(
                ["Teams/Growth/Debut", "Teams/Growth/Debut/docs", "Teams/Growth/Debut/more", "Teams/Growth/Launch2/other", "Teams/Ops/Launch"],
                state.Subscribed);
        }
    }

    /// <summary>The <c>team:</c> folder key and the Room id are not touched by a path-only prefix rename.</summary>
    [Fact]
    public void RenamePrefix_LeavesTeamKeysAndRoomIdsAlone()
    {
        using TempDataDir dataDir = new();
        FileStateStore store = new(dataDir.Options(), NullLogger<FileStateStore>.Instance);
        store.Save("Nova", Seed());

        store.RenamePrefix("Teams/Growth/Launch", "Teams/Growth/Debut");

        FileState? state = store.Load("Nova");
        Assert.NotNull(state);
        Assert.True(state.Rooms["room-1"].Folders.ContainsKey("team:Growth"));
        Assert.True(state.Rooms["room-1"].Folders["team:Growth"].Files.ContainsKey("Launch/plan.md"));
    }
}
