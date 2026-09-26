using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for LibraryFileKinds detection logic.</summary>
public sealed class LibraryFileKindsTests
{
    /// <summary>Markdown files with .md extension are detected as Markdown.</summary>
    [Fact]
    public void Detect_MarkdownFile_ReturnsMarkdown()
    {
        ReadOnlySpan<byte> head = "# Header"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.md", head);
        Assert.Equal(LibraryFileKind.Markdown, kind);
    }

    /// <summary>Markdown files with .markdown extension are detected as Markdown.</summary>
    [Fact]
    public void Detect_MarkdownFileExtension_ReturnsMarkdown()
    {
        ReadOnlySpan<byte> head = "# Header"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.markdown", head);
        Assert.Equal(LibraryFileKind.Markdown, kind);
    }

    /// <summary>JSON files are detected as Text.</summary>
    [Fact]
    public void Detect_JsonFile_ReturnsText()
    {
        ReadOnlySpan<byte> head = "{\"key\": \"value\"}"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.json", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>C# files are detected as Text.</summary>
    [Fact]
    public void Detect_CsFile_ReturnsText()
    {
        ReadOnlySpan<byte> head = "public class Foo { }"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.cs", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>Log files are detected as Text.</summary>
    [Fact]
    public void Detect_LogFile_ReturnsText()
    {
        ReadOnlySpan<byte> head = "[INFO] Application started"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.log", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>Files with no extension containing UTF-8 text are detected as Text.</summary>
    [Fact]
    public void Detect_NoExtensionUtf8Text_ReturnsText()
    {
        ReadOnlySpan<byte> head = "This is UTF-8 text content"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>Files with no extension containing NUL byte are detected as Other.</summary>
    [Fact]
    public void Detect_NoExtensionWithNul_ReturnsOther()
    {
        byte[] head = new byte[] { 0x00, 0x01, 0x02 };
        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>PNG files with PNG magic bytes are detected as Image.</summary>
    [Fact]
    public void Detect_PngFileWithMagic_ReturnsImage()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        LibraryFileKind kind = LibraryFileKinds.Detect("a.png", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>PNG files with text bytes (no magic) are detected as Other.</summary>
    [Fact]
    public void Detect_PngFileWithText_ReturnsOther()
    {
        ReadOnlySpan<byte> head = "just text content"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.png", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>PNG files with incomplete magic (first 4 bytes only) are detected as Other.</summary>
    [Fact]
    public void Detect_PngFileWithIncompleteMagic_ReturnsOther()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00 };
        LibraryFileKind kind = LibraryFileKinds.Detect("a.png", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>JPEG files with JPEG magic bytes are detected as Image.</summary>
    [Fact]
    public void Detect_JpegFileWithMagic_ReturnsImage()
    {
        byte[] head = new byte[] { 0xFF, 0xD8, 0xFF };
        LibraryFileKind kind = LibraryFileKinds.Detect("a.jpg", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>GIF files with GIF8 magic are detected as Image.</summary>
    [Fact]
    public void Detect_GifFileWithMagic_ReturnsImage()
    {
        ReadOnlySpan<byte> head = "GIF89a"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.gif", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>WebP files with RIFF....WEBP magic are detected as Image.</summary>
    [Fact]
    public void Detect_WebpFileWithMagic_ReturnsImage()
    {
        byte[] head = new byte[12];
        head[0] = (byte)'R';
        head[1] = (byte)'I';
        head[2] = (byte)'F';
        head[3] = (byte)'F';
        head[8] = (byte)'W';
        head[9] = (byte)'E';
        head[10] = (byte)'B';
        head[11] = (byte)'P';
        LibraryFileKind kind = LibraryFileKinds.Detect("a.webp", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>SVG files are detected as Svg.</summary>
    [Fact]
    public void Detect_SvgFile_ReturnsSvg()
    {
        ReadOnlySpan<byte> head = "<svg></svg>"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("a.svg", head);
        Assert.Equal(LibraryFileKind.Svg, kind);
    }

    /// <summary>PPTX files (zip magic) are detected as Other.</summary>
    [Fact]
    public void Detect_PptxFile_ReturnsOther()
    {
        byte[] head = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        LibraryFileKind kind = LibraryFileKinds.Detect("deck.pptx", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>Case-insensitive extension matching: A.PNG uppercase is detected as Image.</summary>
    [Fact]
    public void Detect_UppercasePngExtension_ReturnsImage()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        LibraryFileKind kind = LibraryFileKinds.Detect("A.PNG", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>Case-insensitive extension matching: README.MD uppercase is detected as Markdown.</summary>
    [Fact]
    public void Detect_UppercaseMarkdownExtension_ReturnsMarkdown()
    {
        ReadOnlySpan<byte> head = "# Title"u8;
        LibraryFileKind kind = LibraryFileKinds.Detect("README.MD", head);
        Assert.Equal(LibraryFileKind.Markdown, kind);
    }

    /// <summary>TypeScript files with binary/NUL bytes are detected as Other.</summary>
    [Fact]
    public void Detect_TsFileWithBinary_ReturnsOther()
    {
        byte[] head = new byte[] { 0x00, 0xFF, 0xFE };
        LibraryFileKind kind = LibraryFileKinds.Detect("a.ts", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>Markdown files containing NUL bytes are detected as Other.</summary>
    [Fact]
    public void Detect_MarkdownFileWithNul_ReturnsOther()
    {
        byte[] head = new byte[] { 0x23, 0x20, 0x00, 0x54 }; // "# " + NUL + "T"
        LibraryFileKind kind = LibraryFileKinds.Detect("a.md", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>Empty file with no extension is detected as Text.</summary>
    [Fact]
    public void Detect_EmptyNoExtension_ReturnsText()
    {
        ReadOnlySpan<byte> head = ReadOnlySpan<byte>.Empty;
        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>File with no extension containing UTF-16 BOM is detected as Text.</summary>
    [Fact]
    public void Detect_NoExtensionWithUtf16Bom_ReturnsText()
    {
        byte[] head = new byte[] { 0xFF, 0xFE, 0x48, 0x00 }; // UTF-16 LE BOM + 'H'
        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>Valid 3-byte UTF-8 character split at byte 8192 boundary is detected as Text.</summary>
    [Fact]
    public void Detect_Utf8CharSplitAt8192_ReturnsText()
    {
        byte[] head = new byte[8192];
        // Fill with valid UTF-8 content
        for (int i = 0; i < 8190; i++)
        {
            head[i] = (byte)'a';
        }
        // Place a 3-byte UTF-8 character (é = 0xC3 0xA9) such that it's split
        head[8190] = 0xC3; // First byte of é
        head[8191] = 0xA9; // Second byte of é

        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Text, kind);
    }

    /// <summary>File with no extension containing PNG signature is detected as Other.</summary>
    [Fact]
    public void Detect_NoExtensionWithPngSignature_ReturnsOther()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        LibraryFileKind kind = LibraryFileKinds.Detect("noext", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }

    /// <summary>JPEG file with PNG bytes returns Image with PNG content type.</summary>
    [Fact]
    public void Detect_JpegFileWithPngBytes_ReturnsImage()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        LibraryFileKind kind = LibraryFileKinds.Detect("a.jpg", head);
        Assert.Equal(LibraryFileKind.Image, kind);
    }

    /// <summary>ImageContentType returns image/png for PNG magic bytes.</summary>
    [Fact]
    public void ImageContentType_PngMagic_ReturnsPng()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Equal("image/png", contentType);
    }

    /// <summary>ImageContentType returns null for incomplete PNG magic (first 4 bytes only).</summary>
    [Fact]
    public void ImageContentType_PngIncompleteMagic_ReturnsNull()
    {
        byte[] head = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00 };
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Null(contentType);
    }

    /// <summary>ImageContentType returns image/jpeg for JPEG magic bytes.</summary>
    [Fact]
    public void ImageContentType_JpegMagic_ReturnsJpeg()
    {
        byte[] head = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Equal("image/jpeg", contentType);
    }

    /// <summary>ImageContentType returns image/gif for GIF87a magic.</summary>
    [Fact]
    public void ImageContentType_Gif87aMagic_ReturnsGif()
    {
        ReadOnlySpan<byte> head = "GIF87a"u8;
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Equal("image/gif", contentType);
    }

    /// <summary>ImageContentType returns image/gif for GIF89a magic.</summary>
    [Fact]
    public void ImageContentType_Gif89aMagic_ReturnsGif()
    {
        ReadOnlySpan<byte> head = "GIF89a"u8;
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Equal("image/gif", contentType);
    }

    /// <summary>ImageContentType returns image/webp for WebP magic.</summary>
    [Fact]
    public void ImageContentType_WebpMagic_ReturnsWebp()
    {
        byte[] head = new byte[12];
        head[0] = (byte)'R';
        head[1] = (byte)'I';
        head[2] = (byte)'F';
        head[3] = (byte)'F';
        head[8] = (byte)'W';
        head[9] = (byte)'E';
        head[10] = (byte)'B';
        head[11] = (byte)'P';
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Equal("image/webp", contentType);
    }

    /// <summary>ImageContentType returns null for non-image magic bytes.</summary>
    [Fact]
    public void ImageContentType_NonImageMagic_ReturnsNull()
    {
        ReadOnlySpan<byte> head = "some text content"u8;
        string? contentType = LibraryFileKinds.ImageContentType(head);
        Assert.Null(contentType);
    }

    /// <summary>10-byte RIFF without WEBP is not recognized as an image.</summary>
    [Fact]
    public void Detect_RiffWithoutWebp_ReturnsOther()
    {
        byte[] head = new byte[10];
        head[0] = (byte)'R';
        head[1] = (byte)'I';
        head[2] = (byte)'F';
        head[3] = (byte)'F';
        LibraryFileKind kind = LibraryFileKinds.Detect("a.data", head);
        Assert.Equal(LibraryFileKind.Other, kind);
    }
}
