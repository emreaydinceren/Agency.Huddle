using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// Pins SB-R03 (Second Brain spec 6.1): a Room's chat upload lands in the Library's <c>raw/</c> tree, under
/// the root folder for a root Room and under the Team's <c>raw/</c> for a Team Room. A guard test: the
/// behaviour already exists, so it arrives green and must fail if the Team segment regresses to <c>Uploads</c>.
/// </summary>
public sealed class RawUploadPlacementTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    /// <summary>A root Room's upload is saved under <c>{DataDir}/Raw/&lt;roomId&gt;/</c>.</summary>
    [Fact]
    public async Task SaveAsync_RootRoom_SavesUnderRawRoomFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "photo.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        Assert.Equal(Path.Combine(harness.DataDir, "Raw", room.Id, "photo.png"), result.Value.File.FullPath);
    }

    /// <summary>A Home Team Room's upload is saved under <c>{DataDir}/Teams/Home/raw/&lt;roomId&gt;/</c>.</summary>
    [Fact]
    public async Task SaveAsync_HomeTeamRoom_SavesUnderTeamsHomeRawRoomFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Home", ct: ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "notes.txt", new MemoryStream("hello"u8.ToArray()), ct);

        Assert.NotNull(result.Value);
        Assert.Equal(Path.Combine(harness.DataDir, "Teams", "Home", "raw", room.Id, "notes.txt"), result.Value.File.FullPath);
    }

    /// <summary>
    /// A Team Room whose Team folder lives under <c>Archive/</c> (Spec 6.7) refuses the upload with T15 and
    /// writes nothing: no file under <c>Teams/</c>, no raw folder, no baseline row.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ArchivedTeamRoom_RefusesWithT15AndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Home", ct: ct);

        // Archiving moved Teams/Home to Archive/Home, so only the Archive copy exists.
        Directory.CreateDirectory(Path.Combine(harness.DataDir, "Archive", "home"));
        File.WriteAllText(Path.Combine(harness.DataDir, "Archive", "home", "_team.md"), "---\n---\n");

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "notes.txt", new MemoryStream("hello"u8.ToArray()), ct);

        Assert.Null(result.Value);
        Assert.Equal("This Team is archived, so files can't be added here.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(harness.DataDir, "Teams", "Home", "raw", room.Id)));
        Assert.False(Directory.Exists(Path.Combine(harness.DataDir, "Raw", room.Id)));
        Assert.False(File.Exists(Path.Combine(harness.DataDir, "Archive", "Home", "raw", room.Id, "notes.txt")));
    }

    /// <summary>A Team that has a live folder is unaffected, even if an Archive folder of the same name also exists (a Project archived earlier).</summary>
    [Fact]
    public async Task SaveAsync_LiveTeamWithArchiveSibling_StillSaves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Home", ct: ct);
        Directory.CreateDirectory(Path.Combine(harness.DataDir, "Teams", "Home"));
        Directory.CreateDirectory(Path.Combine(harness.DataDir, "Archive", "Home", "Apollo"));

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "notes.txt", new MemoryStream("hello"u8.ToArray()), ct);

        Assert.NotNull(result.Value);
    }

    /// <summary>For a root Room, <c>RoomUploadFolders.For</c> returns exactly the root <c>raw/</c> folder for that Room.</summary>
    [Fact]
    public async Task For_RootRoom_ReturnsOnlyRawRoomFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        IReadOnlyList<LibraryPath> folders = harness.Folders.For(room);

        LibraryPath only = Assert.Single(folders);
        Assert.Equal(Path.Combine(harness.DataDir, "Raw", room.Id), only.FullPath);
    }

    /// <summary>For a Home Team Room, <c>For</c> returns the root <c>raw/</c> folder first and the Team's <c>raw/</c> folder last.</summary>
    [Fact]
    public async Task For_HomeTeamRoom_ReturnsRootRawFolderThenTeamRawFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Home", ct: ct);

        IReadOnlyList<LibraryPath> folders = harness.Folders.For(room);

        Assert.Equal(
            [Path.Combine(harness.DataDir, "Raw", room.Id), Path.Combine(harness.DataDir, "Teams", "Home", "raw", room.Id)],
            folders.Select(folder => folder.FullPath).ToList());
    }
}
