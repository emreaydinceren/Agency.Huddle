namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Builds the smallest byte sequences each supported image format needs for its HEADER to be read:
/// the magic bytes <c>LibraryFileKinds.ImageContentType</c> checks plus the field that holds the pixel
/// size. They are not decodable pictures, which is the point: nothing under test decodes one, and a
/// test can set any width and height without carrying a real image.
/// </summary>
internal static class TestImages
{
    /// <summary>A PNG signature, an <c>IHDR</c> chunk with the given size, and a few trailing bytes.</summary>
    /// <param name="width">The pixel width written to <c>IHDR</c>.</param>
    /// <param name="height">The pixel height written to <c>IHDR</c>.</param>
    /// <param name="paddingBytes">Extra bytes appended, so a test can make the file any length.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Png(int width, int height, int paddingBytes = 0)
    {
        List<byte> bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        bytes.AddRange([0x00, 0x00, 0x00, 0x0D]);
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndian32(width));
        bytes.AddRange(BigEndian32(height));
        bytes.AddRange([0x08, 0x06, 0x00, 0x00, 0x00]);
        bytes.AddRange(new byte[4]);
        bytes.AddRange(new byte[paddingBytes]);
        return [.. bytes];
    }

    /// <summary>A JPEG with a JFIF segment, then a start-of-frame marker of the given kind.</summary>
    /// <param name="width">The pixel width written to the frame header.</param>
    /// <param name="height">The pixel height written to the frame header.</param>
    /// <param name="frameMarker">The marker byte after <c>FF</c>: <c>C0</c> baseline, <c>C2</c> progressive.</param>
    /// <param name="paddingSegments">Count of 40,000-byte comment segments placed BEFORE the frame header.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Jpeg(int width, int height, byte frameMarker = 0xC0, int paddingSegments = 0)
    {
        List<byte> bytes = [0xFF, 0xD8];
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        bytes.AddRange("JFIF\0"u8.ToArray());
        bytes.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        for (int segment = 0; segment < paddingSegments; segment++)
        {
            bytes.AddRange([0xFF, 0xFE, 0x9C, 0x42]);
            bytes.AddRange(new byte[0x9C42 - 2]);
        }

        bytes.AddRange([0xFF, frameMarker, 0x00, 0x11, 0x08]);
        bytes.AddRange(BigEndian16(height));
        bytes.AddRange(BigEndian16(width));
        bytes.AddRange([0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    /// <summary>A GIF89a with the given logical screen size.</summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Gif(int width, int height)
    {
        List<byte> bytes = [.. "GIF89a"u8.ToArray()];
        bytes.AddRange(LittleEndian16(width));
        bytes.AddRange(LittleEndian16(height));
        bytes.AddRange([0x00, 0x00, 0x00, 0x3B]);
        return [.. bytes];
    }

    /// <summary>A lossy WebP (<c>VP8 </c>) with the given size in its key-frame header.</summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The bytes.</returns>
    public static byte[] WebpLossy(int width, int height)
    {
        List<byte> bytes = RiffWebp("VP8 ", 10);
        bytes.AddRange([0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A]);
        bytes.AddRange(LittleEndian16(width));
        bytes.AddRange(LittleEndian16(height));
        return [.. bytes];
    }

    /// <summary>A lossless WebP (<c>VP8L</c>) with the given size packed into 14-bit fields.</summary>
    /// <param name="width">The pixel width.</param>
    /// <param name="height">The pixel height.</param>
    /// <returns>The bytes.</returns>
    public static byte[] WebpLossless(int width, int height)
    {
        List<byte> bytes = RiffWebp("VP8L", 5);
        bytes.Add(0x2F);
        uint packed = (uint)(width - 1) | ((uint)(height - 1) << 14);
        bytes.AddRange(BitConverter.GetBytes(packed));
        return [.. bytes];
    }

    /// <summary>An extended WebP (<c>VP8X</c>) whose canvas size is stored as 24-bit values minus one.</summary>
    /// <param name="width">The canvas pixel width.</param>
    /// <param name="height">The canvas pixel height.</param>
    /// <returns>The bytes.</returns>
    public static byte[] WebpExtended(int width, int height)
    {
        List<byte> bytes = RiffWebp("VP8X", 10);
        bytes.AddRange([0x00, 0x00, 0x00, 0x00]);
        bytes.AddRange(LittleEndian24(width - 1));
        bytes.AddRange(LittleEndian24(height - 1));
        return [.. bytes];
    }

    private static List<byte> RiffWebp(string chunk, int chunkSize)
    {
        List<byte> bytes = [.. "RIFF"u8.ToArray()];
        bytes.AddRange(BitConverter.GetBytes(chunkSize + 12));
        bytes.AddRange("WEBP"u8.ToArray());
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(chunk));
        bytes.AddRange(BitConverter.GetBytes(chunkSize));
        return bytes;
    }

    private static byte[] BigEndian32(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] BigEndian16(int value) => [(byte)(value >> 8), (byte)value];

    private static byte[] LittleEndian16(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] LittleEndian24(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];
}
