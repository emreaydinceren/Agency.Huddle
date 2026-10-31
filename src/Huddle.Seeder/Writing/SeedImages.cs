using System.Buffers.Binary;
using System.IO.Compression;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>Makes tiny, deterministic PNG files in code, so the seed needs no binary assets in the repository.</summary>
internal static class SeedImages
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Builds a square RGB PNG: a diagonal gradient from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <param name="size">The width and height in pixels.</param>
    /// <param name="from">The top-left colour as red, green, blue.</param>
    /// <param name="to">The bottom-right colour as red, green, blue.</param>
    /// <returns>The PNG file bytes.</returns>
    internal static byte[] Gradient(int size, (byte R, byte G, byte B) from, (byte R, byte G, byte B) to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        using MemoryStream raw = new();
        using (ZLibStream zlib = new(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            int steps = Math.Max(1, (2 * size) - 2);
            for (int y = 0; y < size; y++)
            {
                zlib.WriteByte(0);
                for (int x = 0; x < size; x++)
                {
                    int step = x + y;
                    zlib.WriteByte(Mix(from.R, to.R, step, steps));
                    zlib.WriteByte(Mix(from.G, to.G, step, steps));
                    zlib.WriteByte(Mix(from.B, to.B, step, steps));
                }
            }
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)size);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB

        using MemoryStream png = new();
        png.Write(Signature);
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, raw.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static byte Mix(byte from, byte to, int step, int steps) =>
        (byte)(from + (((to - from) * step) / steps));

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        uint crc = uint.MaxValue;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(length, crc ^ uint.MaxValue);
        stream.Write(length);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
