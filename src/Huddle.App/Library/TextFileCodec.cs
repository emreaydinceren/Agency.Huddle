using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>Detects and restores a text file's encoding, byte-order mark and line ending (Spec §6.4).</summary>
internal static class TextFileCodec
{
    private static readonly UTF8Encoding Utf8Strict = new(false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16LeStrict = new(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16BeStrict = new(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);

    /// <summary>Decodes <paramref name="bytes"/>, recording the encoding, BOM and dominant line ending.
    /// Normalises <c>\r\n</c> to <c>\n</c> on decode; a lone <c>\r</c> is left untouched.</summary>
    /// <param name="bytes">The raw file content.</param>
    /// <param name="fallback">The line ending recorded when the content has no newlines.</param>
    /// <param name="text">The decoded text, with <c>\n</c>-only newlines, or <see langword="null"/> when decoding is refused.</param>
    /// <param name="format">The detected format, or <see langword="null"/> when decoding is refused.</param>
    /// <returns><see langword="true"/> when the bytes decoded successfully.</returns>
    public static bool TryDecode(byte[] bytes, LineEnding fallback, [NotNullWhen(true)] out string? text, [NotNullWhen(true)] out TextFileFormat? format)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (!TryDecodeBody(bytes, out string? encodingName, out bool hasBom, out string? decoded))
        {
            text = null;
            format = null;
            return false;
        }

        if (decoded.Contains('\0', StringComparison.Ordinal))
        {
            text = null;
            format = null;
            return false;
        }

        string normalised = NormaliseLineEndings(decoded, out LineEnding lineEnding, out bool mixed, fallback);

        text = normalised;
        format = new TextFileFormat(encodingName, hasBom, lineEnding, mixed);
        return true;
    }

    /// <summary>Re-encodes <paramref name="editorText"/> with the recorded format: replaces <c>\n</c> with the
    /// recorded line ending and prepends the preamble when <see cref="TextFileFormat.HasBom"/>.</summary>
    /// <param name="editorText">The text as edited, with <c>\n</c>-only newlines.</param>
    /// <param name="format">The format recorded by <see cref="TryDecode"/>.</param>
    /// <returns>The re-encoded bytes.</returns>
    public static byte[] Encode(string editorText, TextFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(editorText);
        ArgumentNullException.ThrowIfNull(format);

        string fileText = format.LineEnding == LineEnding.CrLf
            ? editorText.Replace("\n", "\r\n", StringComparison.Ordinal)
            : editorText;

        byte[] body = format.EncodingName switch
        {
            "utf-8" => new UTF8Encoding(false).GetBytes(fileText),
            "utf-16" => Encoding.Unicode.GetBytes(fileText),
            "utf-16BE" => Encoding.BigEndianUnicode.GetBytes(fileText),
            _ => throw new NotSupportedException($"Unsupported text encoding '{format.EncodingName}'."),
        };

        if (!format.HasBom)
        {
            return body;
        }

        byte[] preamble = format.EncodingName switch
        {
            "utf-8" => [0xEF, 0xBB, 0xBF],
            "utf-16" => [0xFF, 0xFE],
            "utf-16BE" => [0xFE, 0xFF],
            _ => throw new NotSupportedException($"Unsupported text encoding '{format.EncodingName}'."),
        };

        byte[] result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static bool TryDecodeBody(byte[] bytes, [NotNullWhen(true)] out string? encodingName, out bool hasBom, [NotNullWhen(true)] out string? decoded)
    {
        if (StartsWith(bytes, [0xFF, 0xFE, 0x00, 0x00]) || StartsWith(bytes, [0x00, 0x00, 0xFE, 0xFF]))
        {
            // UTF-32 is not a supported Library encoding (corrections-B3 5.1.i item 37).
            encodingName = null;
            hasBom = false;
            decoded = null;
            return false;
        }

        if (StartsWith(bytes, [0xEF, 0xBB, 0xBF]))
        {
            return TryDecodeWith(Utf8Strict, bytes, 3, "utf-8", hasBom: true, out encodingName, out hasBom, out decoded);
        }

        if (StartsWith(bytes, [0xFF, 0xFE]))
        {
            return TryDecodeWith(Utf16LeStrict, bytes, 2, "utf-16", hasBom: true, out encodingName, out hasBom, out decoded);
        }

        if (StartsWith(bytes, [0xFE, 0xFF]))
        {
            return TryDecodeWith(Utf16BeStrict, bytes, 2, "utf-16BE", hasBom: true, out encodingName, out hasBom, out decoded);
        }

        return TryDecodeWith(Utf8Strict, bytes, 0, "utf-8", hasBom: false, out encodingName, out hasBom, out decoded);
    }

    private static bool TryDecodeWith(Encoding encoding, byte[] bytes, int offset, string name, bool hasBom, [NotNullWhen(true)] out string? encodingName, out bool hasBomResult, [NotNullWhen(true)] out string? decoded)
    {
        int length = bytes.Length - offset;
        if (encoding is UnicodeEncoding && (length % 2) != 0)
        {
            encodingName = null;
            hasBomResult = false;
            decoded = null;
            return false;
        }

        try
        {
            decoded = encoding.GetString(bytes, offset, length);
            encodingName = name;
            hasBomResult = hasBom;
            return true;
        }
        catch (DecoderFallbackException)
        {
            encodingName = null;
            hasBomResult = false;
            decoded = null;
            return false;
        }
        catch (ArgumentException)
        {
            encodingName = null;
            hasBomResult = false;
            decoded = null;
            return false;
        }
    }

    private static bool StartsWith(byte[] bytes, ReadOnlySpan<byte> prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        return bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private static string NormaliseLineEndings(string decoded, out LineEnding lineEnding, out bool mixed, LineEnding fallback)
    {
        int crLfCount = 0;
        int loneLfCount = 0;
        int loneCrCount = 0;

        StringBuilder builder = new(decoded.Length);
        int i = 0;
        while (i < decoded.Length)
        {
            char current = decoded[i];
            if (current == '\r' && i + 1 < decoded.Length && decoded[i + 1] == '\n')
            {
                crLfCount++;
                builder.Append('\n');
                i += 2;
            }
            else if (current == '\r')
            {
                loneCrCount++;
                builder.Append('\r');
                i++;
            }
            else if (current == '\n')
            {
                loneLfCount++;
                builder.Append('\n');
                i++;
            }
            else
            {
                builder.Append(current);
                i++;
            }
        }

        if (crLfCount == 0 && loneLfCount == 0 && loneCrCount == 0)
        {
            lineEnding = fallback;
        }
        else
        {
            lineEnding = crLfCount >= loneLfCount ? LineEnding.CrLf : LineEnding.Lf;
        }

        int distinctKinds = (crLfCount > 0 ? 1 : 0) + (loneLfCount > 0 ? 1 : 0) + (loneCrCount > 0 ? 1 : 0);
        mixed = distinctKinds > 1;

        return builder.ToString();
    }
}
