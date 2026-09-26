using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="TextFileCodec"/>: byte-exact decode/encode round trips, BOM and line-ending
/// detection, and refusal of unsupported or malformed byte content (Spec §6.4, ADR-0028,
/// corrections-B3 5.1.t items 34-36).
/// </summary>
public sealed class TextFileCodecTests
{
    private static byte[] Utf8Bytes(string text, bool bom)
    {
        byte[] body = new UTF8Encoding(false).GetBytes(text);
        return bom ? [0xEF, 0xBB, 0xBF, .. body] : body;
    }

    private static byte[] Utf16LeBytes(string text, bool bom)
    {
        byte[] body = Encoding.Unicode.GetBytes(text);
        return bom ? [0xFF, 0xFE, .. body] : body;
    }

    private static byte[] Utf16BeBytes(string text, bool bom)
    {
        byte[] body = Encoding.BigEndianUnicode.GetBytes(text);
        return bom ? [0xFE, 0xFF, .. body] : body;
    }

    /// <summary>Every row round-trips: decoded text uses <c>\n</c> only, and re-encoding reproduces the original bytes exactly.</summary>
    public static TheoryData<string, byte[], string> RoundTripCases()
    {
        TheoryData<string, byte[], string> data = new()
        {
            { "Utf8_NoBom_CrLf", Utf8Bytes("line1\r\nline2", bom: false), "line1\nline2" },
            { "Utf8_NoBom_Lf", Utf8Bytes("line1\nline2", bom: false), "line1\nline2" },
            { "Utf8_Bom_CrLf", Utf8Bytes("line1\r\nline2", bom: true), "line1\nline2" },
            { "Utf16Le_Bom_CrLf", Utf16LeBytes("line1\r\nline2", bom: true), "line1\nline2" },
            { "Utf16Be_Bom_Lf", Utf16BeBytes("line1\nline2", bom: true), "line1\nline2" },
            { "NoTrailingNewline", Utf8Bytes("onlyline", bom: false), "onlyline" },
            { "TrailingCrLf", Utf8Bytes("line1\r\n", bom: false), "line1\n" },
            { "Empty", Utf8Bytes(string.Empty, bom: false), string.Empty },
            { "Frontmatter_KeysBAOrder", Utf8Bytes("---\r\nb: 1\r\na: 2\r\n---\r\ncontent", bom: false), "---\nb: 1\na: 2\n---\ncontent" },
            { "BomOnly", Utf8Bytes(string.Empty, bom: true), string.Empty },
        };
        return data;
    }

    /// <summary>Decoding then re-encoding with the recorded <see cref="TextFileFormat"/> reproduces the original bytes exactly.</summary>
    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void TryDecode_RoundTrip_ByteExact(string caseName, byte[] originalBytes, string expectedText)
    {
        bool ok = TextFileCodec.TryDecode(originalBytes, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.True(ok, caseName);
        Assert.NotNull(text);
        Assert.NotNull(format);
        Assert.Equal(expectedText, text);
        byte[] reEncoded = TextFileCodec.Encode(text, format);
        Assert.Equal(originalBytes, reEncoded);
    }

    /// <summary>Each round-trip case's detected encoding name, BOM flag and line ending.</summary>
    public static TheoryData<string, byte[], string, bool, LineEnding> FormatCases()
    {
        TheoryData<string, byte[], string, bool, LineEnding> data = new()
        {
            { "Utf8_NoBom_CrLf", Utf8Bytes("line1\r\nline2", bom: false), "utf-8", false, LineEnding.CrLf },
            { "Utf8_NoBom_Lf", Utf8Bytes("line1\nline2", bom: false), "utf-8", false, LineEnding.Lf },
            { "Utf8_Bom_CrLf", Utf8Bytes("line1\r\nline2", bom: true), "utf-8", true, LineEnding.CrLf },
            { "Utf16Le_Bom_CrLf", Utf16LeBytes("line1\r\nline2", bom: true), "utf-16", true, LineEnding.CrLf },
            { "Utf16Be_Bom_Lf", Utf16BeBytes("line1\nline2", bom: true), "utf-16BE", true, LineEnding.Lf },
        };
        return data;
    }

    /// <summary>The codec reports the encoding name, BOM presence and dominant line ending it detected.</summary>
    [Theory]
    [MemberData(nameof(FormatCases))]
    public void Decode_Format_Table(string caseName, byte[] bytes, string expectedEncodingName, bool expectedHasBom, LineEnding expectedLineEnding)
    {
        bool ok = TextFileCodec.TryDecode(bytes, LineEnding.CrLf, out _, out TextFileFormat? format);

        Assert.True(ok, caseName);
        Assert.NotNull(format);
        Assert.Equal(expectedEncodingName, format.EncodingName, StringComparer.Ordinal);
        Assert.Equal(expectedHasBom, format.HasBom);
        Assert.Equal(expectedLineEnding, format.LineEnding);
    }

    /// <summary>Three CRLF and one lone LF: CRLF is dominant and the format flags the file as mixed; re-encoding normalises every ending to CRLF.</summary>
    [Fact]
    public void Decode_Mixed_UsesDominantAndFlagsMixed()
    {
        byte[] original = Utf8Bytes("a\r\nb\r\nc\r\nd\nend", bom: false);

        bool ok = TextFileCodec.TryDecode(original, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.True(ok);
        Assert.NotNull(text);
        Assert.NotNull(format);
        Assert.Equal(LineEnding.CrLf, format.LineEnding);
        Assert.True(format.MixedLineEndings);
        byte[] reEncoded = TextFileCodec.Encode(text, format);
        byte[] expected = Utf8Bytes("a\r\nb\r\nc\r\nd\r\nend", bom: false);
        Assert.Equal(expected, reEncoded);
    }

    /// <summary>An equal count of CRLF and lone LF prefers CRLF regardless of the fallback passed in.</summary>
    [Fact]
    public void Decode_Tie_PrefersCrLf()
    {
        byte[] bytes = Utf8Bytes("a\r\nb\nc", bom: false);

        bool ok = TextFileCodec.TryDecode(bytes, LineEnding.Lf, out _, out TextFileFormat? format);

        Assert.True(ok);
        Assert.NotNull(format);
        Assert.Equal(LineEnding.CrLf, format.LineEnding);
    }

    /// <summary>A file with no newlines at all falls back to whichever <see cref="LineEnding"/> the caller supplies.</summary>
    [Theory]
    [InlineData(LineEnding.CrLf)]
    [InlineData(LineEnding.Lf)]
    public void Decode_NoNewlines_UsesFallback(LineEnding fallback)
    {
        byte[] bytes = Utf8Bytes("noNewlinesHere", bom: false);

        bool ok = TextFileCodec.TryDecode(bytes, fallback, out _, out TextFileFormat? format);

        Assert.True(ok);
        Assert.NotNull(format);
        Assert.Equal(fallback, format.LineEnding);
    }

    /// <summary>Malformed UTF-8 byte sequences are refused rather than lossily decoded.</summary>
    [Theory]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { 0x93 })]
    public void Decode_InvalidUtf8_ReturnsFalse(byte[] bytes)
    {
        bool ok = TextFileCodec.TryDecode(bytes, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.False(ok);
        Assert.Null(text);
        Assert.Null(format);
    }

    /// <summary>BOM kinds and malformed content the codec must refuse rather than guess at.</summary>
    public static TheoryData<string, byte[]> RefusedCases()
    {
        TheoryData<string, byte[]> data = new()
        {
            { "Utf32LeBom", [0xFF, 0xFE, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00] },
            { "Utf32BeBom", [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x41] },
            { "OddLengthUtf16", [0xFF, 0xFE, 0x41] },
            { "LoneSurrogateUtf16", [0xFF, 0xFE, 0x00, 0xD8] },
            { "Utf16WithoutBom", Encoding.Unicode.GetBytes("Hello") },
        };
        return data;
    }

    /// <summary>Each refused case decodes to false rather than throwing or producing lossy text.</summary>
    [Theory]
    [MemberData(nameof(RefusedCases))]
    public void Decode_Refused_Table(string caseName, byte[] bytes)
    {
        bool ok = TextFileCodec.TryDecode(bytes, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.False(ok, caseName);
        Assert.Null(text);
        Assert.Null(format);
    }

    /// <summary>A lone carriage return with no following line feed is preserved literally and flags the file as mixed.</summary>
    [Fact]
    public void Decode_LoneCarriageReturn_PreservedAndFlagsMixed()
    {
        byte[] original = Utf8Bytes("a\rb\r\nc\r\nd", bom: false);

        bool ok = TextFileCodec.TryDecode(original, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.True(ok);
        Assert.NotNull(text);
        Assert.NotNull(format);
        Assert.True(format.MixedLineEndings);
        byte[] reEncoded = TextFileCodec.Encode(text, format);
        Assert.Equal(original, reEncoded);
    }

    /// <summary>A carriage return immediately followed by a CRLF pair round-trips byte-for-byte.</summary>
    [Fact]
    public void Decode_CrCrLf_RoundTrip()
    {
        byte[] original = Utf8Bytes("a\r\r\nb", bom: false);

        bool ok = TextFileCodec.TryDecode(original, LineEnding.CrLf, out string? text, out TextFileFormat? format);

        Assert.True(ok);
        Assert.NotNull(text);
        Assert.NotNull(format);
        byte[] reEncoded = TextFileCodec.Encode(text, format);
        Assert.Equal(original, reEncoded);
    }

    /// <summary>Editing one word in the decoded text and re-encoding changes only that word's bytes.</summary>
    [Fact]
    public void Encode_EditedText_OnlyChangesEdits()
    {
        byte[] original = Utf8Bytes("The quick fox\r\njumps", bom: false);
        bool ok = TextFileCodec.TryDecode(original, LineEnding.CrLf, out string? text, out TextFileFormat? format);
        Assert.True(ok);
        Assert.NotNull(text);
        Assert.NotNull(format);

        string edited = text.Replace("quick", "slow", StringComparison.Ordinal);
        byte[] actual = TextFileCodec.Encode(edited, format);

        byte[] expected = Utf8Bytes("The slow fox\r\njumps", bom: false);
        Assert.Equal(expected, actual);
    }
}
