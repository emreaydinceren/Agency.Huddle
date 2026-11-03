using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Uploads;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// Tests for <see cref="ChatUploadService"/> (ChatUploads spec 6.4 and section 5): which folder an upload
/// lands in, by the Room's Scope, and the refusals. Real directory, resolver and byte writer over a temp
/// <c>DataDir</c>.
/// </summary>
public sealed class ChatUploadServiceTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    /// <summary>An upload to a root Room is saved under <c>uploads:/&lt;roomId&gt;</c>, with its kind and size.</summary>
    [Fact]
    public async Task SaveAsync_RootRoom_SavesUnderUploadsRoomFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "photo.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        string expected = Path.Combine(harness.DataDir, "Raw", room.Id, "photo.png");
        Assert.Equal(expected, result.Value.File.FullPath);
        Assert.Equal("raw", result.Value.File.Root.Id);
        Assert.Equal(UploadKind.Image, result.Value.Kind);
        Assert.Equal(PngHead.Length, result.Value.Size);
        Assert.True(File.Exists(expected));
    }

    /// <summary>An upload to a Team's Room is saved under <c>teams:/&lt;Team&gt;/raw/&lt;roomId&gt;</c>.</summary>
    [Fact]
    public async Task SaveAsync_TeamRoom_SavesUnderTeamRawFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "notes.txt", new MemoryStream("hello"u8.ToArray()), ct);

        Assert.NotNull(result.Value);
        Assert.Equal(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id, "notes.txt"), result.Value.File.FullPath);
        Assert.Equal("teams", result.Value.File.Root.Id);
        Assert.False(Directory.Exists(Path.Combine(harness.DataDir, "Raw", room.Id)));
    }

    /// <summary>A Team with no folder yet gets one: the writer creates the whole chain.</summary>
    [Fact]
    public async Task SaveAsync_TeamWithNoFolder_CreatesTeamFolderChain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Brand New", ct: ct);
        Assert.False(Directory.Exists(Path.Combine(harness.DataDir, "Teams", "Brand New")));

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "a.txt", new MemoryStream("x"u8.ToArray()), ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(harness.DataDir, "Teams", "Brand New", "raw", room.Id, "a.txt")));
    }

    /// <summary>A Room id the directory does not know is refused and nothing is written.</summary>
    [Fact]
    public async Task SaveAsync_UnknownRoom_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync("no-such-room", "a.txt", new MemoryStream("x"u8.ToArray()), ct);

        Assert.Null(result.Value);
        Assert.Equal("Couldn't save a.txt.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(harness.DataDir, "Raw")));
    }

    /// <summary>The browser's name is cleaned first: a directory part is stripped and a refused character becomes a dash.</summary>
    [Fact]
    public async Task SaveAsync_DirtyName_IsCleaned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "C:\\temp\\a?b.txt", new MemoryStream("x"u8.ToArray()), ct);

        Assert.Equal("a-b.txt", Path.GetFileName(result.Value?.File.FullPath));
    }

    /// <summary>A pasted image's generic name becomes the timestamped name from the injected clock.</summary>
    [Fact]
    public async Task SaveAsync_GenericPasteName_UsesClockTimestamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 12, 9, 8, 7, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct, clock: clock);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "image.png", new MemoryStream(PngHead), ct);

        Assert.Equal("pasted-image-20261012-090807.png", Path.GetFileName(result.Value?.File.FullPath));
    }

    /// <summary>A file past <see cref="LibraryOptions.MaxUploadBytes"/> is refused with the file name in the message.</summary>
    [Fact]
    public async Task SaveAsync_OverMaxUploadBytes_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct, maxUploadBytes: 1024 * 1024);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "big.txt", new MemoryStream(new byte[(1024 * 1024) + 1]), ct);

        Assert.Null(result.Value);
        Assert.Equal("big.txt is larger than 1 MB.", result.Error);
    }

    /// <summary><see cref="IRoomUploadFolders"/> gives a root Room one folder: <c>uploads:/&lt;roomId&gt;</c>.</summary>
    [Fact]
    public async Task RoomUploadFolders_RootRoom_ReturnsUploadsFolderOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        IReadOnlyList<LibraryPath> folders = harness.Folders.For(room);

        LibraryPath only = Assert.Single(folders);
        Assert.Equal(Path.Combine(harness.DataDir, "Raw", room.Id), only.FullPath);
    }

    /// <summary><see cref="IRoomUploadFolders"/> gives a Team Room two folders, the root one first.</summary>
    [Fact]
    public async Task RoomUploadFolders_TeamRoom_ReturnsRootThenTeamFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);

        IReadOnlyList<LibraryPath> folders = harness.Folders.For(room);

        Assert.Equal(2, folders.Count);
        Assert.Equal(Path.Combine(harness.DataDir, "Raw", room.Id), folders[0].FullPath);
        Assert.Equal(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id), folders[1].FullPath);
    }
}
