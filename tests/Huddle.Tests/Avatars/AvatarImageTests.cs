using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <see cref="AvatarImage.SniffExtension(ReadOnlySpan{byte})"/>'s magic-byte identification: real
/// PNG, JPEG and WebP headers are accepted; SVG, HTML, a malformed WebP and truncated or empty buffers
/// are all rejected, and none of them ever throw.
/// </summary>
public sealed class AvatarImageTests
{
    /// <summary>A real PNG signature is identified as <c>".png"</c>.</summary>
    [Fact]
    public void SniffExtension_PngHeader_ReturnsPngExtension()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

        var extension = AvatarImage.SniffExtension(png);

        Assert.Equal(".png", extension);
    }

    /// <summary>A real JPEG signature is identified as <c>".jpg"</c>.</summary>
    [Fact]
    public void SniffExtension_JpegHeader_ReturnsJpgExtension()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        var extension = AvatarImage.SniffExtension(jpeg);

        Assert.Equal(".jpg", extension);
    }

    /// <summary>A real WebP signature - <c>RIFF</c> at offset 0 and <c>WEBP</c> at offset 8 - is identified as <c>".webp"</c>.</summary>
    [Fact]
    public void SniffExtension_WebpHeader_ReturnsWebpExtension()
    {
        byte[] webp = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        var extension = AvatarImage.SniffExtension(webp);

        Assert.Equal(".webp", extension);
    }

    /// <summary>An SVG opening with a bare <c>&lt;svg</c> tag is rejected, since SVG is never an accepted format.</summary>
    [Fact]
    public void SniffExtension_SvgBareTag_ReturnsNull()
    {
        byte[] svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">"u8.ToArray();

        var extension = AvatarImage.SniffExtension(svg);

        Assert.Null(extension);
    }

    /// <summary>An SVG opening with an XML prolog before its <c>&lt;svg</c> tag is also rejected.</summary>
    [Fact]
    public void SniffExtension_SvgWithXmlProlog_ReturnsNull()
    {
        byte[] svg = "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\">"u8.ToArray();

        var extension = AvatarImage.SniffExtension(svg);

        Assert.Null(extension);
    }

    /// <summary>An HTML file is rejected.</summary>
    [Fact]
    public void SniffExtension_Html_ReturnsNull()
    {
        byte[] html = "<!DOCTYPE html><html><body>hi</body></html>"u8.ToArray();

        var extension = AvatarImage.SniffExtension(html);

        Assert.Null(extension);
    }

    /// <summary>A zero-byte buffer is rejected.</summary>
    [Fact]
    public void SniffExtension_ZeroBytes_ReturnsNull()
    {
        var extension = AvatarImage.SniffExtension(ReadOnlySpan<byte>.Empty);

        Assert.Null(extension);
    }

    /// <summary>A PNG header truncated to seven of its eight signature bytes is rejected.</summary>
    [Fact]
    public void SniffExtension_TruncatedPngHeader_ReturnsNull()
    {
        byte[] truncated = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A];

        var extension = AvatarImage.SniffExtension(truncated);

        Assert.Null(extension);
    }

    /// <summary><c>RIFF</c> without a matching <c>WEBP</c> at offset 8 is rejected.</summary>
    [Fact]
    public void SniffExtension_RiffWithoutWebp_ReturnsNull()
    {
        byte[] notWebp = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x41, 0x56, 0x49, 0x20];

        var extension = AvatarImage.SniffExtension(notWebp);

        Assert.Null(extension);
    }

    /// <summary>A buffer too short for any signature does not throw, and is rejected.</summary>
    [Fact]
    public void SniffExtension_ThreeByteBuffer_DoesNotThrowAndReturnsNull()
    {
        byte[] tooShort = [0xFF, 0xD8, 0x00];

        var extension = AvatarImage.SniffExtension(tooShort);

        Assert.Null(extension);
    }
}
