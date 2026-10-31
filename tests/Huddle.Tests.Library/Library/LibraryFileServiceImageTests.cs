using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.ReadImageAsync"/> (design §6.3): an image's bytes, MIME
/// type and size when the file really is one and is within the caps, and a refusal that is data,
/// never an exception, when it is not, so the caller can fall back to the path line.
/// </summary>
public sealed class LibraryFileServiceImageTests
{
    private const int GenerousBytes = 3 * 1024 * 1024;

    private const int GenerousEdge = 8000;

    /// <summary>A PNG within every cap returns its own bytes, <c>image/png</c> and its header size.</summary>
    [Fact]
    public async Task ReadImage_Png_ReturnsBytesMimeAndSize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        byte[] png = TestImages.Png(640, 480, paddingBytes: 100);
        File.WriteAllBytes(Path.Combine(vault, "shot.png"), png);
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "shot.png"), GenerousBytes, GenerousEdge, ct);

        LibraryImageRead read = Assert.IsType<LibraryImageRead>(result);
        Assert.Equal("image/png", read.MimeType);
        Assert.Equal(png, read.Bytes);
        Assert.Equal((640, 480), (read.Width, read.Height));
    }

    /// <summary>JPEG, GIF and WebP are read the same way and report their own MIME type.</summary>
    /// <param name="fileName">The file name to write.</param>
    /// <param name="mime">The MIME type expected.</param>
    [Theory]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    public async Task ReadImage_OtherFormats_ReportTheirMimeType(string fileName, string mime)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        byte[] bytes = mime switch
        {
            "image/jpeg" => TestImages.Jpeg(30, 20),
            "image/gif" => TestImages.Gif(30, 20),
            _ => TestImages.WebpLossy(30, 20),
        };
        File.WriteAllBytes(Path.Combine(vault, fileName), bytes);
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, fileName), GenerousBytes, GenerousEdge, ct);

        LibraryImageRead read = Assert.IsType<LibraryImageRead>(result);
        Assert.Equal(mime, read.MimeType);
        Assert.Equal((30, 20), (read.Width, read.Height));
    }

    /// <summary>A text file named <c>.png</c> is not an image: the magic bytes decide, never the extension.</summary>
    [Fact]
    public async Task ReadImage_TextNamedPng_IsNotAnImage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "shot.png"), "<html>not an image</html>");
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "shot.png"), GenerousBytes, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.NotAnImage, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>An SVG is never an image block, however it is named.</summary>
    [Fact]
    public async Task ReadImage_Svg_IsNotAnImage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "mock.svg"), "<svg width=\"10\" height=\"10\"></svg>");
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "mock.svg"), GenerousBytes, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.NotAnImage, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>A real image whose header cannot be parsed is refused as not an image, not sent blind.</summary>
    [Fact]
    public async Task ReadImage_PngSignatureWithTruncatedHeader_IsNotAnImage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "cut.png"), TestImages.Png(10, 10)[..12]);
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "cut.png"), GenerousBytes, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.NotAnImage, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>A file over the byte cap is refused as too large, whatever its size in pixels.</summary>
    [Fact]
    public async Task ReadImage_OverMaxBytes_IsTooLarge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "big.png"), TestImages.Png(10, 10, paddingBytes: 5000));
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "big.png"), 1000, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.TooLarge, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>A file of exactly the byte cap is still read: the cap is a maximum, not an exclusive bound.</summary>
    [Fact]
    public async Task ReadImage_ExactlyMaxBytes_IsRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        byte[] png = TestImages.Png(10, 10, paddingBytes: 200);
        File.WriteAllBytes(Path.Combine(vault, "edge.png"), png);
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "edge.png"), png.Length, GenerousEdge, ct);

        Assert.IsType<LibraryImageRead>(result);
    }

    /// <summary>An image whose longest side is over the edge cap is refused, even at 200 KiB.</summary>
    [Fact]
    public async Task ReadImage_EdgeOverCap_IsTooManyPixels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "wide.png"), TestImages.Png(9000, 9000, paddingBytes: 200 * 1024));
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "wide.png"), GenerousBytes, 8000, ct);

        Assert.Equal(LibraryImageRefusal.TooManyPixels, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>The edge cap applies to the height as well as the width.</summary>
    [Fact]
    public async Task ReadImage_TallImageOverCap_IsTooManyPixels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "tall.png"), TestImages.Png(10, 8001));
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "tall.png"), GenerousBytes, 8000, ct);

        Assert.Equal(LibraryImageRefusal.TooManyPixels, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>An image exactly at the edge cap is read.</summary>
    [Fact]
    public async Task ReadImage_EdgeExactlyAtCap_IsRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "max.png"), TestImages.Png(8000, 100));
        LibraryFileService service = fixture.CreateService();

        LibraryImageResult result = await service.ReadImageAsync(fixture.Resolve(vault, "max.png"), GenerousBytes, 8000, ct);

        Assert.IsType<LibraryImageRead>(result);
    }

    /// <summary>A path that no longer resolves inside any Library Root is unreadable, not an exception: a caller's stale path is never trusted.</summary>
    [Fact]
    public async Task ReadImage_PathOutsideRoot_IsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.png"), TestImages.Png(10, 10));
        LibraryFileService service = fixture.CreateService();
        LibraryPath real = fixture.Resolve(vault, "a.png");
        LibraryPath escaping = real with { RelativePath = "../../outside.png" };

        LibraryImageResult result = await service.ReadImageAsync(escaping, GenerousBytes, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.Unreadable, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>A file deleted after it was resolved is unreadable.</summary>
    [Fact]
    public async Task ReadImage_DeletedFile_IsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string file = Path.Combine(vault, "gone.png");
        File.WriteAllBytes(file, TestImages.Png(10, 10));
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "gone.png");
        File.Delete(file);

        LibraryImageResult result = await service.ReadImageAsync(path, GenerousBytes, GenerousEdge, ct);

        Assert.Equal(LibraryImageRefusal.Unreadable, Assert.IsType<LibraryImageRefused>(result).Reason);
    }

    /// <summary>A file held open by an editor with a write lock is still read: the Library reads with shared access.</summary>
    [Fact]
    public async Task ReadImage_FileOpenForWriteElsewhere_IsRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string file = Path.Combine(vault, "held.png");
        File.WriteAllBytes(file, TestImages.Png(10, 10));
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "held.png");
        using FileStream editor = new(file, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);

        LibraryImageResult result = await service.ReadImageAsync(path, GenerousBytes, GenerousEdge, ct);

        Assert.IsType<LibraryImageRead>(result);
    }

    /// <summary>A cancelled token ends the read with <see cref="OperationCanceledException"/>, which is a Stop and not a refusal.</summary>
    [Fact]
    public async Task ReadImage_Cancelled_Throws()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.png"), TestImages.Png(10, 10));
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "a.png");
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReadImageAsync(path, GenerousBytes, GenerousEdge, cancelled.Token));
    }
}
