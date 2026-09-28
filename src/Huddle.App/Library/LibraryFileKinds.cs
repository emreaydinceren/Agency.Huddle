using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Unicode;

namespace Agency.Huddle.App.Library;

/// <summary>Detects file kinds and image content types for Library files.</summary>
internal static class LibraryFileKinds
{
    private static readonly FrozenSet<string> TextExtensions = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".txt", ".json", ".yaml", ".yml", ".cs", ".js", ".ts", ".css", ".xml", ".ps1", ".csv", ".log"
    );

    /// <summary>The Spec §6.11 Markdown extensions, shared by <see cref="HasKnownExtension"/> and <see cref="Detect"/>.</summary>
    private static readonly FrozenSet<string> MarkdownExtensions = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".md", ".markdown"
    );

    /// <summary>The Spec §6.11 SVG extension, shared by <see cref="HasKnownExtension"/> and <see cref="Detect"/>.</summary>
    private const string SvgExtension = ".svg";

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Whether <paramref name="fileName"/> has a Markdown extension (<c>.md</c> or <c>.markdown</c>).
    /// Shares <see cref="MarkdownExtensions"/> with <see cref="Detect"/> and <see cref="HasKnownExtension"/>
    /// so the set lives once; used by <see cref="WikiLinkIndex"/> to decide which files it parses for links.
    /// </summary>
    /// <param name="fileName">The candidate file name.</param>
    internal static bool IsMarkdown(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return MarkdownExtensions.Contains(Path.GetExtension(fileName));
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> carries one of the Spec §6.11 text/code extensions (Task 12.2.i-a):
    /// shared with <see cref="Detect"/> so <see cref="LibraryFileService.ReadAsync"/> can still offer the
    /// settled unsupported-encoding reason (Spec §10 E-5) for a file whose extension names it as text even
    /// though its content sniffs as <see cref="LibraryFileKind.Other"/>.
    /// </summary>
    /// <param name="fileName">The candidate file name.</param>
    internal static bool IsTextExtension(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return TextExtensions.Contains(Path.GetExtension(fileName));
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> already carries a Spec §6.11 known extension (Markdown, text/code
    /// or image), so <see cref="LibraryFileService.CreateFileAsync"/> keeps it as-is rather than appending
    /// <c>.md</c> (corrections-B4 item 23). Shares the same extension lists as <see cref="Detect"/> so the two
    /// can never drift.
    /// </summary>
    /// <param name="fileName">The candidate file name.</param>
    /// <returns><see langword="true"/> when the extension is one Spec §6.11 already names.</returns>
    internal static bool HasKnownExtension(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        string extension = Path.GetExtension(fileName);
        if (extension.Length == 0)
        {
            return false;
        }

        return MarkdownExtensions.Contains(extension)
            || extension.Equals(SvgExtension, StringComparison.OrdinalIgnoreCase)
            || IsImageExtension(extension)
            || TextExtensions.Contains(extension);
    }

    /// <summary>Detects the kind of file based on its name and content head.</summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="head">The first 8 KB of the file content.</param>
    /// <returns>The detected kind of file.</returns>
    internal static LibraryFileKind Detect(string fileName, ReadOnlySpan<byte> head)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        string extension = Path.GetExtension(fileName);

        // Check for SVG first
        if (extension.Equals(SvgExtension, StringComparison.OrdinalIgnoreCase))
        {
            return LibraryFileKind.Svg;
        }

        // Check for image extensions
        if (IsImageExtension(extension))
        {
            // Verify magic bytes
            if (IsImageMagic(head))
            {
                return LibraryFileKind.Image;
            }
            else
            {
                return LibraryFileKind.Other;
            }
        }

        // Check for Markdown extensions
        bool isMarkdown = MarkdownExtensions.Contains(extension);

        // Sniff content to determine if it's valid text
        LibraryFileKind sniffedKind = SniffFileKind(head);

        // If content is valid text and we have a Markdown extension, return Markdown
        if (isMarkdown && sniffedKind != LibraryFileKind.Other)
        {
            return LibraryFileKind.Markdown;
        }

        // Otherwise return the sniffed kind
        return sniffedKind;
    }

    /// <summary>Gets the image content type for the given file head.</summary>
    /// <param name="head">The first 8 KB of the file content.</param>
    /// <returns>The content type (e.g., "image/png") or null if not an image.</returns>
    internal static string? ImageContentType(ReadOnlySpan<byte> head)
    {
        // PNG signature
        if (head.StartsWith(PngSignature))
        {
            return "image/png";
        }

        // JPEG signature
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // GIF87a or GIF89a
        if (head.Length >= 6)
        {
            string gif = Encoding.ASCII.GetString(head.Slice(0, 6));
            if (gif.Equals("GIF87a", StringComparison.Ordinal) || gif.Equals("GIF89a", StringComparison.Ordinal))
            {
                return "image/gif";
            }
        }

        // WebP signature (RIFF....WEBP)
        if (head.Length >= 12)
        {
            if (head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F' &&
                head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P')
            {
                return "image/webp";
            }
        }

        return null;
    }

    /// <summary>Checks if the extension is an image extension.</summary>
    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Checks if the content head contains image magic bytes.</summary>
    private static bool IsImageMagic(ReadOnlySpan<byte> head) => ImageContentType(head) is not null;

    /// <summary>Sniffs the file kind based on content.</summary>
    private static LibraryFileKind SniffFileKind(ReadOnlySpan<byte> head)
    {
        // Check for BOM first (before NUL check, as UTF-16 can contain legitimate NULs)
        if (head.Length >= 2)
        {
            // UTF-16 LE BOM
            if (head[0] == 0xFF && head[1] == 0xFE && !(head.Length >= 4 && head[2] == 0x00 && head[3] == 0x00))
            {
                return LibraryFileKind.Text;
            }

            // UTF-16 BE BOM
            if (head[0] == 0xFE && head[1] == 0xFF)
            {
                return LibraryFileKind.Text;
            }
        }

        // UTF-8 BOM
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            return LibraryFileKind.Text;
        }

        // Check for NUL byte (binary file)
        if (head.Contains((byte)0x00))
        {
            return LibraryFileKind.Other;
        }

        // Check for known binary file signatures
        if (head.Length >= 2)
        {
            // ZIP/PPTX/DOCX (PK..)
            if (head[0] == 0x50 && head[1] == 0x4B)
            {
                return LibraryFileKind.Other;
            }

            // PDF (%PDF)
            if (head.Length >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46)
            {
                return LibraryFileKind.Other;
            }

            // EXE/DLL (MZ)
            if (head[0] == 0x4D && head[1] == 0x5A)
            {
                return LibraryFileKind.Other;
            }
        }

        // ELF executable (\x7fELF)
        if (head.Length >= 4 && head[0] == 0x7F && head[1] == 0x45 && head[2] == 0x4C && head[3] == 0x46)
        {
            return LibraryFileKind.Other;
        }

        // Check if content is valid UTF-8
        Span<char> buffer = new char[head.Length];
        OperationStatus status = Utf8.ToUtf16(head, buffer, out _, out _, replaceInvalidSequences: false, isFinalBlock: head.Length < 8192);

        // If it's valid UTF-8 or needs more data (at boundary), it's text
        if (status == OperationStatus.Done || status == OperationStatus.NeedMoreData)
        {
            return LibraryFileKind.Text;
        }

        // Invalid UTF-8 means binary
        return LibraryFileKind.Other;
    }
}
