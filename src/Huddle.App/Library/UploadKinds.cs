using System.Buffers;
using System.Text.Unicode;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Classifies an attached file from its first bytes (spec 6.3) and says which kinds are refused.
/// Images reuse <see cref="LibraryFileKinds.ImageContentType"/>; no signature is copied here.
/// </summary>
internal static class UploadKinds
{
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    private static ReadOnlySpan<byte> ZipSignature => [0x50, 0x4B, 0x03, 0x04];

    private static ReadOnlySpan<byte> DosSignature => [0x4D, 0x5A];

    private static ReadOnlySpan<byte> ElfSignature => [0x7F, 0x45, 0x4C, 0x46];

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16LeBom => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16BeBom => [0xFE, 0xFF];

    private static ReadOnlySpan<byte> SvgMarker => "<svg"u8;

    /// <summary>The Mach-O magics: 32- and 64-bit, both byte orders, and the fat (universal) binary.</summary>
    private static readonly byte[][] MachOSignatures =
    [
        [0xFE, 0xED, 0xFA, 0xCE],
        [0xFE, 0xED, 0xFA, 0xCF],
        [0xCE, 0xFA, 0xED, 0xFE],
        [0xCF, 0xFA, 0xED, 0xFE],
        [0xCA, 0xFE, 0xBA, 0xBE],
    ];

    /// <summary>
    /// Classifies a file by its first bytes. Binary signatures are checked first, then text: an
    /// empty head is <see cref="UploadKind.Text"/>, and a head holding no signature and no valid text is
    /// <see cref="UploadKind.Other"/>.
    /// </summary>
    /// <param name="head">The first bytes of the file, at most 512 of them.</param>
    /// <returns>The kind the bytes name.</returns>
    internal static UploadKind Classify(ReadOnlySpan<byte> head)
    {
        if (LibraryFileKinds.ImageContentType(head) is not null)
        {
            return UploadKind.Image;
        }

        if (head.StartsWith(PdfSignature))
        {
            return UploadKind.Pdf;
        }

        if (head.StartsWith(ZipSignature))
        {
            return UploadKind.Archive;
        }

        if (IsExecutable(head))
        {
            return UploadKind.Executable;
        }

        if (!IsText(head))
        {
            return UploadKind.Other;
        }

        if (head.IndexOf(SvgMarker) >= 0)
        {
            return UploadKind.Svg;
        }

        return UploadKind.Text;
    }

    /// <summary>Whether a kind is refused: an SVG can run script in the browser, and a program can run on the Human's machine.</summary>
    /// <param name="kind">The kind to test.</param>
    /// <returns><see langword="true"/> for an SVG or a program, and <see langword="false"/> for every other kind.</returns>
    internal static bool IsRefused(UploadKind kind) => kind is UploadKind.Svg or UploadKind.Executable;

    /// <summary>The sentence shown when a file of a refused kind is attached (spec 6.3).</summary>
    /// <param name="kind">The kind the file was classified as.</param>
    /// <returns>The refusal text, or <see langword="null"/> when the kind is accepted.</returns>
    internal static string? RefusalText(UploadKind kind) => kind switch
    {
        UploadKind.Svg => "SVG files can't be attached.",
        UploadKind.Executable => "Programs can't be attached.",
        _ => null,
    };

    /// <summary>Whether the head starts with a DOS, ELF or Mach-O magic.</summary>
    /// <param name="head">The first bytes of the file.</param>
    /// <returns><see langword="true"/> when a program signature leads the head.</returns>
    private static bool IsExecutable(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(DosSignature) || head.StartsWith(ElfSignature))
        {
            return true;
        }

        foreach (byte[] signature in MachOSignatures)
        {
            if (head.StartsWith(signature))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the head is text: it opens with a byte-order mark, or it is valid UTF-8 with no NUL. A
    /// sequence cut off by the end of the head counts as valid, because the head is only a sample.
    /// </summary>
    /// <param name="head">The first bytes of the file.</param>
    /// <returns><see langword="true"/> when the head is text.</returns>
    private static bool IsText(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(Utf8Bom) || head.StartsWith(Utf16LeBom) || head.StartsWith(Utf16BeBom))
        {
            return true;
        }

        if (head.Contains((byte)0x00))
        {
            return false;
        }

        char[] buffer = new char[head.Length];
        OperationStatus status = Utf8.ToUtf16(head, buffer, out _, out _, replaceInvalidSequences: false, isFinalBlock: false);
        return status is OperationStatus.Done or OperationStatus.NeedMoreData;
    }
}
