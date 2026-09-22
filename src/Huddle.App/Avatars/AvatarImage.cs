namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Identifies an uploaded avatar image's real format from its bytes, and nothing else. This
/// application serves uploaded files same-origin with no authentication of any kind, by design, so
/// trusting a client-supplied name or content type would let an attacker's file lie its way past
/// validation; magic bytes cannot be forged without also forging a file that decodes as the claimed
/// format.
/// </summary>
internal static class AvatarImage
{
    /// <summary>
    /// The largest avatar upload this application accepts, in bytes. This is not an arbitrary limit -
    /// it is Blazor's own default <c>maxAllowedSize</c> for <c>IBrowserFile.OpenReadStream</c>, so an
    /// upload already inside that ceiling never needs a second, stricter one invented here.
    /// </summary>
    internal const int MaxBytes = 512_000;

    /// <summary>PNG's eight-byte signature at offset 0.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>JPEG's three-byte signature at offset 0.</summary>
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>The <c>RIFF</c> four bytes a WebP file opens with, at offset 0.</summary>
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];

    /// <summary>The <c>WEBP</c> four bytes a WebP file carries at offset 8, after its 4-byte chunk size.</summary>
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];

    /// <summary>
    /// Identifies <paramref name="head"/>'s image format by magic bytes alone - never by a filename
    /// extension and never by a client-supplied content type, both of which are trivially forged.
    /// </summary>
    /// <remarks>
    /// SVG is deliberately never accepted, however it is spelled: it is a document format that can
    /// carry a <c>&lt;script&gt;</c> element, and this application has no authentication gate in front
    /// of the files it serves - the same class of reasoning <c>docs/agencyteam/rules.md</c> gives for
    /// never turning on <c>UseAdvancedExtensions()</c> in the markdown pipeline. Decoding, dimension
    /// checks and resizing are out of scope for the same reason: any of them needs an image decoder,
    /// which means a new NuGet dependency and a decompression-bomb surface this method does not take on.
    /// </remarks>
    /// <param name="head">The uploaded file's leading bytes.</param>
    /// <returns><c>".png"</c>, <c>".jpg"</c> or <c>".webp"</c> when <paramref name="head"/> matches a known signature; otherwise <see langword="null"/>.</returns>
    internal static string? SniffExtension(ReadOnlySpan<byte> head)
    {
        if (head.Length >= PngSignature.Length && head[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return ".png";
        }

        if (head.Length >= JpegSignature.Length && head[..JpegSignature.Length].SequenceEqual(JpegSignature))
        {
            return ".jpg";
        }

        if (head.Length >= RiffSignature.Length + 4 + WebpSignature.Length
            && head[..RiffSignature.Length].SequenceEqual(RiffSignature)
            && head.Slice(RiffSignature.Length + 4, WebpSignature.Length).SequenceEqual(WebpSignature))
        {
            return ".webp";
        }

        return null;
    }
}
