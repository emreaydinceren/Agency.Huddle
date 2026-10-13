namespace Agency.Huddle.App.Library;

/// <summary>
/// The kind of a file attached to a chat Message, decided from its first bytes only (spec 6.3). The
/// extension never changes the kind. <see cref="Svg"/> and <see cref="Executable"/> are refused.
/// </summary>
internal enum UploadKind
{
    /// <summary>A PNG, JPEG, GIF or WebP image, by the <see cref="LibraryFileKinds"/> signatures.</summary>
    Image,

    /// <summary>A PDF document (<c>%PDF-</c>).</summary>
    Pdf,

    /// <summary>A zip container (<c>PK\x03\x04</c>), which includes docx, xlsx and pptx.</summary>
    Archive,

    /// <summary>Text: a byte-order mark, or valid UTF-8 with no NUL in the head.</summary>
    Text,

    /// <summary>Text that holds an <c>&lt;svg</c> element. Refused, because an SVG can run script.</summary>
    Svg,

    /// <summary>A program: a DOS/Windows <c>MZ</c> image, an ELF file or a Mach-O binary. Refused.</summary>
    Executable,

    /// <summary>Anything else, a binary file with no known signature.</summary>
    Other,
}
