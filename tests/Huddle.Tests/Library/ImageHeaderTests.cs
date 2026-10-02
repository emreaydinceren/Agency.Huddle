using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="ImageHeader.TryReadSize"/> (design §8.4): the pixel size of an image read
/// from its first bytes alone, with no image library. A failure to parse is a refusal, never a guess.
/// </summary>
public sealed class ImageHeaderTests
{
    /// <summary>Every supported format yields exactly the width and height written to its header.</summary>
    /// <param name="format">The format under test, for the failure message.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="height">The expected height.</param>
    [Theory]
    [InlineData("png", 640, 480)]
    [InlineData("png-wide", 9000, 1)]
    [InlineData("jpeg", 1024, 768)]
    [InlineData("jpeg-progressive", 800, 600)]
    [InlineData("gif", 320, 200)]
    [InlineData("webp-lossy", 1280, 720)]
    [InlineData("webp-lossless", 300, 150)]
    [InlineData("webp-extended", 4096, 2160)]
    public void TryReadSize_SupportedFormat_ReturnsHeaderSize(string format, int width, int height)
    {
        byte[] bytes = format switch
        {
            "png" or "png-wide" => TestImages.Png(width, height),
            "jpeg" => TestImages.Jpeg(width, height),
            "jpeg-progressive" => TestImages.Jpeg(width, height, frameMarker: 0xC2),
            "gif" => TestImages.Gif(width, height),
            "webp-lossy" => TestImages.WebpLossy(width, height),
            "webp-lossless" => TestImages.WebpLossless(width, height),
            "webp-extended" => TestImages.WebpExtended(width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown test format."),
        };

        bool ok = ImageHeader.TryReadSize(bytes, out int actualWidth, out int actualHeight);

        Assert.True(ok);
        Assert.Equal((width, height), (actualWidth, actualHeight));
    }

    /// <summary>A PNG cut off inside <c>IHDR</c> does not parse.</summary>
    [Fact]
    public void TryReadSize_TruncatedPng_ReturnsFalse()
    {
        byte[] bytes = TestImages.Png(10, 10)[..20];

        bool ok = ImageHeader.TryReadSize(bytes, out int width, out int height);

        Assert.False(ok);
        Assert.Equal((0, 0), (width, height));
    }

    /// <summary>A JPEG cut off before any frame header does not parse.</summary>
    [Fact]
    public void TryReadSize_JpegWithoutFrameHeader_ReturnsFalse()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

        Assert.False(ImageHeader.TryReadSize(bytes, out _, out _));
    }

    /// <summary>A JPEG whose frame header lies past the first 64 KiB is refused: the safe direction.</summary>
    [Fact]
    public void TryReadSize_JpegFrameHeaderPast64KiB_ReturnsFalse()
    {
        byte[] bytes = TestImages.Jpeg(100, 100, paddingSegments: 2);

        Assert.True(bytes.Length > 65536);
        Assert.False(ImageHeader.TryReadSize(bytes, out _, out _));
    }

    /// <summary>A JPEG whose frame header lies within the first 64 KiB still parses after a large comment segment.</summary>
    [Fact]
    public void TryReadSize_JpegFrameHeaderAfterOneLargeSegment_Parses()
    {
        byte[] bytes = TestImages.Jpeg(100, 50, paddingSegments: 1);

        bool ok = ImageHeader.TryReadSize(bytes, out int width, out int height);

        Assert.True(ok);
        Assert.Equal((100, 50), (width, height));
    }

    /// <summary>An empty span has no size.</summary>
    [Fact]
    public void TryReadSize_Empty_ReturnsFalse()
    {
        Assert.False(ImageHeader.TryReadSize([], out _, out _));
    }

    /// <summary>Text is not an image, whatever its extension.</summary>
    [Fact]
    public void TryReadSize_Text_ReturnsFalse()
    {
        Assert.False(ImageHeader.TryReadSize("<html>not an image</html>"u8, out _, out _));
    }

    /// <summary>An SVG has no header size here: it is never an image.</summary>
    [Fact]
    public void TryReadSize_Svg_ReturnsFalse()
    {
        Assert.False(ImageHeader.TryReadSize("<svg width=\"10\" height=\"10\"></svg>"u8, out _, out _));
    }

    /// <summary>A header claiming zero pixels is not a usable image.</summary>
    [Fact]
    public void TryReadSize_PngWithZeroWidth_ReturnsFalse()
    {
        Assert.False(ImageHeader.TryReadSize(TestImages.Png(0, 10), out _, out _));
    }

    /// <summary>A PNG whose declared edge does not fit an <see cref="int"/> is refused rather than wrapped negative.</summary>
    [Fact]
    public void TryReadSize_PngWithHugeEdge_ReturnsFalse()
    {
        byte[] bytes = TestImages.Png(1, 1);
        bytes[16] = 0xFF;
        bytes[17] = 0xFF;
        bytes[18] = 0xFF;
        bytes[19] = 0xFF;

        Assert.False(ImageHeader.TryReadSize(bytes, out _, out _));
    }
}
