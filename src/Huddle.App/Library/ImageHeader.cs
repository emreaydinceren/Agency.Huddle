using System.Buffers.Binary;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Reads an image's pixel size from its first bytes, with no image library (design §8.4). Only a
/// header is parsed, never pixel data, so a hostile file costs at most <see cref="JpegScanLimit"/>
/// bytes of scanning. A header that does not parse is a refusal, never a guess: the caller falls
/// back to the path line. SVG is never an image here.
/// </summary>
internal static class ImageHeader
{
    /// <summary>The most of a JPEG scanned for its frame header. A file that places it later is refused.</summary>
    internal const int JpegScanLimit = 65536;

    /// <summary>Reads the pixel size of a PNG, GIF, WebP or JPEG from <paramref name="data"/>.</summary>
    /// <param name="data">The file's bytes, or at least the first <see cref="JpegScanLimit"/> of them.</param>
    /// <param name="width">The width in pixels, or 0 when this returns <see langword="false"/>.</param>
    /// <param name="height">The height in pixels, or 0 when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the header held a positive size that fits an <see cref="int"/>.</returns>
    internal static bool TryReadSize(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;

        long parsedWidth;
        long parsedHeight;
        bool found = LibraryFileKinds.ImageContentType(data) switch
        {
            "image/png" => TryReadPng(data, out parsedWidth, out parsedHeight),
            "image/gif" => TryReadGif(data, out parsedWidth, out parsedHeight),
            "image/webp" => TryReadWebp(data, out parsedWidth, out parsedHeight),
            "image/jpeg" => TryReadJpeg(data, out parsedWidth, out parsedHeight),
            _ => Refuse(out parsedWidth, out parsedHeight),
        };

        if (!found || parsedWidth is < 1 or > int.MaxValue || parsedHeight is < 1 or > int.MaxValue)
        {
            return false;
        }

        width = (int)parsedWidth;
        height = (int)parsedHeight;
        return true;
    }

    private static bool Refuse(out long width, out long height)
    {
        width = 0;
        height = 0;
        return false;
    }

    /// <summary>PNG: the <c>IHDR</c> chunk follows the 8-byte signature; width then height, big-endian, at bytes 16 to 23.</summary>
    private static bool TryReadPng(ReadOnlySpan<byte> data, out long width, out long height)
    {
        if (data.Length < 24 || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return Refuse(out width, out height);
        }

        width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        return true;
    }

    /// <summary>GIF: the logical screen width then height, little-endian 16-bit, at bytes 6 to 9.</summary>
    private static bool TryReadGif(ReadOnlySpan<byte> data, out long width, out long height)
    {
        if (data.Length < 10)
        {
            return Refuse(out width, out height);
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8, 2));
        return true;
    }

    /// <summary>WebP: lossy (<c>VP8 </c>), lossless (<c>VP8L</c>) and extended (<c>VP8X</c>) each keep the size somewhere different.</summary>
    private static bool TryReadWebp(ReadOnlySpan<byte> data, out long width, out long height)
    {
        if (data.Length < 16)
        {
            return Refuse(out width, out height);
        }

        ReadOnlySpan<byte> chunk = data.Slice(12, 4);
        if (chunk.SequenceEqual("VP8X"u8) && data.Length >= 30)
        {
            width = (data[24] | (data[25] << 8) | (data[26] << 16)) + 1L;
            height = (data[27] | (data[28] << 8) | (data[29] << 16)) + 1L;
            return true;
        }

        if (chunk.SequenceEqual("VP8 "u8) && data.Length >= 30 && data[23] == 0x9D && data[24] == 0x01 && data[25] == 0x2A)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(28, 2)) & 0x3FFF;
            return true;
        }

        if (chunk.SequenceEqual("VP8L"u8) && data.Length >= 25 && data[20] == 0x2F)
        {
            uint packed = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(21, 4));
            width = (packed & 0x3FFF) + 1L;
            height = ((packed >> 14) & 0x3FFF) + 1L;
            return true;
        }

        return Refuse(out width, out height);
    }

    /// <summary>
    /// JPEG: walk the marker segments from <c>SOI</c>, skipping each by its length, until the first
    /// start-of-frame marker, which holds height then width as big-endian 16-bit. <c>DHT</c>,
    /// <c>JPG</c> and <c>DAC</c> share the <c>C0</c> to <c>CF</c> range but are not frames.
    /// </summary>
    private static bool TryReadJpeg(ReadOnlySpan<byte> data, out long width, out long height)
    {
        ReadOnlySpan<byte> scan = data.Length > JpegScanLimit ? data[..JpegScanLimit] : data;
        int position = 2;
        while (position + 4 <= scan.Length)
        {
            if (scan[position] != 0xFF)
            {
                break;
            }

            byte marker = scan[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }

            if (marker == 0xD9 || marker == 0xDA)
            {
                break;
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(scan.Slice(position + 2, 2));
            if (length < 2)
            {
                break;
            }

            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                if (position + 9 > scan.Length)
                {
                    break;
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(scan.Slice(position + 5, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(scan.Slice(position + 7, 2));
                return true;
            }

            position += 2 + length;
        }

        return Refuse(out width, out height);
    }
}
