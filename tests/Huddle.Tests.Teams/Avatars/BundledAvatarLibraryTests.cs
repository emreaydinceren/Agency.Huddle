using System.Buffers.Binary;
using Agency.Huddle.App.Avatars;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins the Avatar Library that ships with the app (<c>src/Huddle.App/AvatarLibrary</c>), read the way the app reads it:
/// from <see cref="AvatarLibraryLocator.BundledDir"/> beside the test host, through the real <see cref="AvatarLibraryLoader"/>.
/// A build that forgets to copy the folder, a picture that is too big or the wrong size, and an index that names a file
/// that is not there all fail here instead of in front of a user.
/// </summary>
public sealed class BundledAvatarLibraryTests
{
    /// <summary>The number of pictures the shipped library holds.</summary>
    private const int ExpectedCount = 126;

    /// <summary>The side, in pixels, of every shipped picture, which the picker's 72 px thumbnails and the avatar circle are sized for.</summary>
    private const uint ExpectedSide = 256;

    private static AvatarLibraryLocator Locator() =>
        new(TestOptions.Create(), AvatarLibraryLocator.BundledDir);

    private static async Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken ct) =>
        await new AvatarLibraryLoader(Locator(), NullLogger<AvatarLibraryLoader>.Instance).LoadAsync(ct);

    /// <summary>With nothing configured, the folder the build copies beside the app is a library.</summary>
    [Fact]
    public void BundledFolder_IsAConfiguredLibrary()
    {
        Assert.True(Locator().IsConfigured);
    }

    /// <summary>The shipped index loads whole: every one of the 126 records is valid, and none is skipped as unsafe, missing, oversize or a duplicate.</summary>
    [Fact]
    public async Task BundledIndex_LoadsEveryRecordWithNoneSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        AvatarLibraryLoadResult result = await LoadAsync(ct);

        Assert.False(result.Unreadable);
        Assert.Equal(0, result.Skipped);
        Assert.NotNull(result.Catalog);
        Assert.Equal(ExpectedCount, result.Catalog.Count);
    }

    /// <summary>Every shipped picture is a PNG of 256 by 256 pixels under the upload cap, so a pick always passes the same validation an upload does.</summary>
    [Fact]
    public async Task BundledPictures_AreSmall256PixelPngs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AvatarLibraryLoadResult result = await LoadAsync(ct);
        Assert.NotNull(result.Catalog);

        List<string> problems = [];
        foreach (LibraryAvatar avatar in result.Catalog.All)
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(AvatarLibraryLocator.BundledDir, avatar.File), ct);
            if (bytes.Length > AvatarImage.MaxBytes)
            {
                problems.Add($"{avatar.File}: {bytes.Length} bytes is over the cap");
            }

            if (AvatarImage.SniffExtension(bytes) != ".png")
            {
                problems.Add($"{avatar.File}: not a PNG");
                continue;
            }

            // The IHDR chunk follows the 8-byte signature: 4 bytes of length, 4 of type, then width and height.
            uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width != ExpectedSide || height != ExpectedSide)
            {
                problems.Add($"{avatar.File}: {width} x {height}");
            }
        }

        Assert.Empty(problems);
    }

    /// <summary>
    /// No two shipped avatars are the same picture or carry the same tags: two alike would sit side by side in
    /// the picker with the same accessible name, so a screen reader could not tell them apart.
    /// </summary>
    [Fact]
    public async Task BundledAvatars_AreAllDifferent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AvatarLibraryLoadResult result = await LoadAsync(ct);
        Assert.NotNull(result.Catalog);

        Dictionary<string, string> byPicture = new(StringComparer.Ordinal);
        Dictionary<string, string> byName = new(StringComparer.Ordinal);
        List<string> problems = [];
        foreach (LibraryAvatar avatar in result.Catalog.All)
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(AvatarLibraryLocator.BundledDir, avatar.File), ct);
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            if (!byPicture.TryAdd(hash, avatar.File))
            {
                problems.Add($"{avatar.File} is the same picture as {byPicture[hash]}");
            }

            if (!byName.TryAdd(avatar.AccessibleName, avatar.File))
            {
                problems.Add($"{avatar.File} has the same name as {byName[avatar.AccessibleName]}: {avatar.AccessibleName}");
            }
        }

        Assert.Empty(problems);
    }

    /// <summary>The folder holds exactly the pictures the index names: no orphan file the picker can never show, and none missing.</summary>
    [Fact]
    public async Task BundledFolder_HoldsExactlyTheIndexedPictures()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AvatarLibraryLoadResult result = await LoadAsync(ct);
        Assert.NotNull(result.Catalog);

        string[] indexed = [.. result.Catalog.All.Select(static avatar => avatar.File).Order(StringComparer.Ordinal)];
        string[] onDisk = [.. Directory.EnumerateFiles(AvatarLibraryLocator.BundledDir, "*.png").Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

        Assert.Equal(indexed, onDisk);
    }
}
