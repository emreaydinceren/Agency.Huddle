using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for <see cref="UploadKinds"/>: the kind of an attached file, from its first bytes only (spec 6.3).</summary>
public sealed class UploadKindsTests
{
    /// <summary>Each binary signature in spec 6.3 classifies as its kind.</summary>
    [Theory]
    [InlineData("89504E470D0A1A0A", "Image")]
    [InlineData("FFD8FFE000104A4649", "Image")]
    [InlineData("474946383761", "Image")]
    [InlineData("474946383961", "Image")]
    [InlineData("52494646000000005745425050", "Image")]
    [InlineData("255044462D312E37", "Pdf")]
    [InlineData("504B0304140000", "Archive")]
    [InlineData("4D5A90000300", "Executable")]
    [InlineData("7F454C4602010100", "Executable")]
    [InlineData("FEEDFACE0000", "Executable")]
    [InlineData("FEEDFACF0000", "Executable")]
    [InlineData("CEFAEDFE0000", "Executable")]
    [InlineData("CFFAEDFE0000", "Executable")]
    [InlineData("CAFEBABE0000", "Executable")]
    public void Classify_BinarySignature_ReturnsItsKind(string hex, string expectedKind)
    {
        UploadKind kind = UploadKinds.Classify(Convert.FromHexString(hex));

        Assert.Equal(Enum.Parse<UploadKind>(expectedKind), kind);
    }

    /// <summary>Text heads classify as Text (BOM or valid UTF-8 with no NUL) or as Svg when they contain an svg element.</summary>
    [Theory]
    [InlineData("hello, world\n", "Text")]
    [InlineData("﻿plain text", "Text")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "Svg")]
    [InlineData("﻿<svg></svg>", "Svg")]
    [InlineData("<?xml version=\"1.0\"?>\n<svg></svg>", "Svg")]
    [InlineData("<!-- drawn in a tool -->\n<svg></svg>", "Svg")]
    [InlineData("﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!-- note -->\n<svg></svg>", "Svg")]
    public void Classify_TextHead_ReturnsTextOrSvg(string text, string expectedKind)
    {
        UploadKind kind = UploadKinds.Classify(Encoding.UTF8.GetBytes(text));

        Assert.Equal(Enum.Parse<UploadKind>(expectedKind), kind);
    }

    /// <summary>An empty head is Text: nothing in it is a signature, and an empty file is valid UTF-8.</summary>
    [Fact]
    public void Classify_EmptyHead_ReturnsText()
    {
        UploadKind kind = UploadKinds.Classify(ReadOnlySpan<byte>.Empty);

        Assert.Equal(UploadKind.Text, kind);
    }

    /// <summary>Bytes that are neither a signature nor text (a NUL, or invalid UTF-8) classify as Other.</summary>
    [Theory]
    [InlineData("00010203")]
    [InlineData("41004200")]
    [InlineData("C328")]
    public void Classify_NoSignatureAndNotText_ReturnsOther(string hex)
    {
        UploadKind kind = UploadKinds.Classify(Convert.FromHexString(hex));

        Assert.Equal(UploadKind.Other, kind);
    }

    /// <summary>A text head is classified from its bytes alone: the same text is Text whatever the file was named.</summary>
    [Fact]
    public void Classify_TextBytes_IsTextBecauseOfBytesOnly()
    {
        byte[] pngNamedText = Encoding.UTF8.GetBytes("this is not really a picture");

        UploadKind kind = UploadKinds.Classify(pngNamedText);

        Assert.Equal(UploadKind.Text, kind);
    }

    /// <summary>A UTF-8 sequence cut by the end of the sampled head is still valid text.</summary>
    [Fact]
    public void Classify_MultiByteSequenceCutAtHeadEnd_IsStillText()
    {
        byte[] head = [.. Encoding.UTF8.GetBytes("abc"), 0xE2, 0x82];

        UploadKind kind = UploadKinds.Classify(head);

        Assert.Equal(UploadKind.Text, kind);
    }

    /// <summary>SVG and executables are refused; every other kind is accepted.</summary>
    [Theory]
    [InlineData("Svg", true)]
    [InlineData("Executable", true)]
    [InlineData("Image", false)]
    [InlineData("Pdf", false)]
    [InlineData("Archive", false)]
    [InlineData("Text", false)]
    [InlineData("Other", false)]
    public void IsRefused_Kind_OnlySvgAndExecutables(string kindName, bool expected)
    {
        bool refused = UploadKinds.IsRefused(Enum.Parse<UploadKind>(kindName));

        Assert.Equal(expected, refused);
    }

    /// <summary>The refused kinds carry the spec 6.3 refusal text; accepted kinds carry none.</summary>
    [Theory]
    [InlineData("Svg", "SVG files can't be attached.")]
    [InlineData("Executable", "Programs can't be attached.")]
    [InlineData("Image", null)]
    [InlineData("Text", null)]
    public void RefusalText_Kind_ReturnsSpecText(string kindName, string? expected)
    {
        string? text = UploadKinds.RefusalText(Enum.Parse<UploadKind>(kindName));

        Assert.Equal(expected, text);
    }
}
