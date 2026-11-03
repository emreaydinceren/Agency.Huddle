using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>The real <c>RoomUploadFolders</c> against a temp <c>DataDir</c> (ChatUploads spec 6.9).</summary>
public sealed class RoomUploadFoldersTests
{
    private static string Seed(string folder, string name = "a.txt")
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), "x");
        return folder;
    }

    /// <summary>Both the root and the Team folder go through the Library recycle path, and nothing fails.</summary>
    [Fact]
    public async Task RecycleAsync_TeamRoom_SendsBothFoldersToTheBin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);
        string rootFolder = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        string teamFolder = Seed(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id));

        IReadOnlyList<string> failures = await harness.Folders.RecycleAsync(room, ct);

        Assert.Empty(failures);
        Assert.Equal([rootFolder, teamFolder], harness.RecycleBin.Sent);
    }

    /// <summary>A folder the bin refuses is reported as a failure, not thrown.</summary>
    [Fact]
    public async Task RecycleAsync_BinRefuses_ReturnsTheFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        _ = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        harness.RecycleBin.FailureError = "No bin.";

        IReadOnlyList<string> failures = await harness.Folders.RecycleAsync(room, ct);

        Assert.Single(failures);
    }

    /// <summary>The count adds the files of both folders, nested ones included.</summary>
    [Fact]
    public async Task CountFiles_TeamRoom_CountsBothFoldersRecursively()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);
        _ = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        string teamFolder = Seed(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id));
        _ = Seed(Path.Combine(teamFolder, "deeper"), "b.txt");

        Assert.Equal(3, harness.Folders.CountFiles(room));
    }

    /// <summary>A Team name that is a traversal segment fails closed: only the root folder is used.</summary>
    [Fact]
    public async Task For_TeamNameWithDotDot_FailsClosed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room created = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        Room room = created with { Team = ".." };

        IReadOnlyList<LibraryPath> folders = harness.Folders.For(room);

        LibraryPath only = Assert.Single(folders);
        Assert.Equal("raw", only.Root.Id);
    }
}
