using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Collects, dedupes, caps, labels and (when the Agent has no file tools) inlines the Library
/// documents mentioned across a Turn's message texts (Spec §6.14). Every candidate path goes
/// through <see cref="LibraryPathResolver.TryResolveAbsolute"/>; nothing is built by hand, and a
/// path outside every Library Root or pointing at a missing file adds nothing. There is no
/// <c>Library.Enabled</c> gate here (corrections-B6 item 8): the caller passes a
/// <see langword="null"/> collector when the Library is disabled.
/// </summary>
/// <param name="resolver">Resolves every candidate path against the current Library Roots.</param>
/// <param name="files">Reads a document's kind, size and decoded text.</param>
/// <param name="options">The bound <see cref="TeamOptions"/>, for the Library's caps.</param>
internal sealed class LibraryDocumentCollector(LibraryPathResolver resolver, LibraryFileService files, IOptions<TeamOptions> options)
{
    /// <summary>The most known-extension boundaries tried per plain-text candidate start (corrections-B6 item 2).</summary>
    private const int MaxCandidateEnds = 8;

    /// <summary>The Teammates-root Work Dir folder name, matching <see cref="LibraryPathResolver"/>'s own
    /// (currently unconfigurable) convention for classifying a folder as a Work Dir.</summary>
    private const string WorkDirFolderName = "work";

    /// <summary>Shares one Markdig pipeline with <see cref="MarkdownRenderer"/> so code-span detection never drifts.</summary>
    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();

    private readonly LibraryPathResolver resolver = resolver;
    private readonly LibraryFileService files = files;
    private readonly IOptions<TeamOptions> options = options;

    /// <summary>
    /// Scans every message text for Library document paths, in first-seen order across all of
    /// them, dedupes by resolved full path, caps at <see cref="LibraryOptions.MaxReferencedDocuments"/>
    /// and builds a <see cref="LibraryDocumentItem"/> for each kept document.
    /// </summary>
    /// <param name="messageTexts">The Turn's message texts, in order.</param>
    /// <param name="readsFiles">Whether the Agent's Adapter can read files itself: when true, no text is inlined.</param>
    /// <param name="ct">Cancels the read of each kept document.</param>
    internal async Task<LibraryDocumentsReport> CollectAsync(IReadOnlyList<string> messageTexts, bool readsFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messageTexts);

        List<LibraryPath> ordered = [];
        HashSet<string> seen = new(FolderSnapshot.PathComparer);

        foreach (string text in messageTexts)
        {
            this.CollectFromText(text, ordered, seen);
        }

        int cap = this.options.Value.Library.MaxReferencedDocuments;
        int notListed = Math.Max(0, ordered.Count - cap);
        IReadOnlyList<LibraryPath> kept = ordered.Count > cap ? ordered.GetRange(0, cap) : ordered;

        List<LibraryDocumentItem> items = [];
        foreach (LibraryPath path in kept)
        {
            LibraryDocumentItem? item = await this.TryBuildItemAsync(path, readsFiles, ct);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return new LibraryDocumentsReport(items, notListed);
    }

    /// <summary>Finds code-span candidates (whole-string, spaces allowed) then plain-text candidates, in that order.</summary>
    private void CollectFromText(string text, List<LibraryPath> ordered, HashSet<string> seen)
    {
        MarkdownDocument document = Markdig.Markdown.Parse(text, LibraryDocumentCollector.Pipeline);
        foreach (CodeInline codeInline in document.Descendants<CodeInline>())
        {
            if (LibraryPathPatterns.TryMatchWhole(codeInline.Content, out string? wholePath)
                && this.TryResolveExistingFile(wholePath, out LibraryPath? resolved))
            {
                LibraryDocumentCollector.AddIfNew(resolved, ordered, seen);
            }
        }

        foreach ((int index, int _, string _) in LibraryPathPatterns.Find(text))
        {
            LibraryPath? resolved = this.ResolvePlainTextCandidate(text, index);
            if (resolved is not null)
            {
                LibraryDocumentCollector.AddIfNew(resolved, ordered, seen);
            }
        }
    }

    /// <summary>
    /// Resolves a plain-text candidate that started at <paramref name="start"/>: the longest
    /// known-extension boundary within the line - always including the natural, whitespace-bounded
    /// extent as the final fallback - that <see cref="LibraryPathResolver.TryResolveAbsolute"/> accepts
    /// and that exists (corrections-B6 item 2), capped at <see cref="MaxCandidateEnds"/> tries so a
    /// compound name with many extension-like dots is bounded rather than always winning.
    /// </summary>
    private LibraryPath? ResolvePlainTextCandidate(string text, int start)
    {
        int lineEnd = text.IndexOf('\n', start);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        List<int> ends = LibraryDocumentCollector.FindKnownExtensionBoundaries(text, start, lineEnd);
        for (int i = ends.Count - 1; i >= 0; i--)
        {
            string candidate = text[start..ends[i]];
            if (this.TryResolveExistingFile(candidate, out LibraryPath? extended))
            {
                return extended;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds, in ascending order and capped at <see cref="MaxCandidateEnds"/>, every position where the
    /// text since the previous <c>.</c> forms a known extension (<see cref="LibraryFileKinds.HasKnownExtension"/>),
    /// plus one final fallback boundary at the end of the line, so a compound extension (<c>.md.bak</c>)
    /// is still tried as the longest candidate even though its own last segment is not itself known.
    /// </summary>
    private static List<int> FindKnownExtensionBoundaries(string text, int start, int lineEnd)
    {
        List<int> ends = [];
        int i = start;
        while (i < lineEnd && ends.Count < LibraryDocumentCollector.MaxCandidateEnds)
        {
            int dot = text.IndexOf('.', i, lineEnd - i);
            if (dot < 0)
            {
                break;
            }

            int nextDot = text.IndexOf('.', dot + 1, lineEnd - dot - 1);
            int nextSpace = text.IndexOfAny([' ', '\t', '\r'], dot + 1, lineEnd - dot - 1);
            int tokenEnd = lineEnd;
            if (nextDot >= 0)
            {
                tokenEnd = Math.Min(tokenEnd, nextDot);
            }

            if (nextSpace >= 0)
            {
                tokenEnd = Math.Min(tokenEnd, nextSpace);
            }

            string token = text[dot..tokenEnd];
            if (LibraryFileKinds.HasKnownExtension("x" + token))
            {
                ends.Add(tokenEnd);
            }

            i = dot + 1;
        }

        if (ends.Count < LibraryDocumentCollector.MaxCandidateEnds && (ends.Count == 0 || ends[^1] != lineEnd))
        {
            ends.Add(lineEnd);
        }

        return ends;
    }

    /// <summary>Resolves <paramref name="candidateText"/> to an existing file inside a Library Root.</summary>
    private bool TryResolveExistingFile(string candidateText, [NotNullWhen(true)] out LibraryPath? path)
    {
        if (this.resolver.TryResolveAbsolute(candidateText, out path)
            && path.Role is LibraryNodeRole.File or LibraryNodeRole.TeammateDefinition
            && File.Exists(path.FullPath))
        {
            return true;
        }

        path = null;
        return false;
    }

    /// <summary>Adds <paramref name="path"/> at its first-seen position, deduping by resolved full path.</summary>
    private static void AddIfNew(LibraryPath path, List<LibraryPath> ordered, HashSet<string> seen)
    {
        if (seen.Add(path.FullPath))
        {
            ordered.Add(path);
        }
    }

    /// <summary>Reads and labels one kept document; a file-system failure skips the item entirely.</summary>
    private async Task<LibraryDocumentItem?> TryBuildItemAsync(LibraryPath path, bool readsFiles, CancellationToken ct)
    {
        try
        {
            LibraryDocumentContent content = await this.files.ReadAsync(path, ct);
            string size = LibrarySize.Format(content.Length);
            string location = await this.ResolveLocationAsync(path, ct);
            bool tooLarge = content.Length > this.options.Value.Library.MaxEditableBytes;
            bool isTextKind = content.Kind is LibraryFileKind.Markdown or LibraryFileKind.Text;

            string? text = null;
            bool truncated = false;
            if (!readsFiles && isTextKind && !tooLarge && content.Text is not null)
            {
                (text, truncated) = LibraryDocumentCollector.TruncateToMaxInlineBytes(content.Text, this.options.Value.Library.MaxInlineBytes);
            }

            return new LibraryDocumentItem(path.FullPath, location, size, text, truncated, content.Length, tooLarge);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Cuts <paramref name="text"/> to at most <paramref name="maxBytes"/> UTF-8 bytes, on the last
    /// whole rune (corrections-B6 item 6): never inside a surrogate pair.</summary>
    private static (string Text, bool Truncated) TruncateToMaxInlineBytes(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return (text, false);
        }

        int byteLength = 0;
        int charIndex = 0;
        while (charIndex < text.Length)
        {
            int runeLength = char.IsHighSurrogate(text[charIndex]) && charIndex + 1 < text.Length && char.IsLowSurrogate(text[charIndex + 1])
                ? 2
                : 1;
            int runeByteLength = Encoding.UTF8.GetByteCount(text, charIndex, runeLength);
            if (byteLength + runeByteLength > maxBytes)
            {
                break;
            }

            byteLength += runeByteLength;
            charIndex += runeLength;
        }

        return (text[..charIndex], true);
    }

    /// <summary>
    /// Builds a document's §6.14 location label from its <see cref="LibraryPath"/>: <c>Team {Team}</c>,
    /// <c>Team {Team}, Project {Project}</c>, <c>{Name}'s Work Dir</c>, <c>teammate {Name}</c> (corrections-B6
    /// item 4) or <c>pinned root "{Name}"</c>.
    /// </summary>
    private async Task<string> ResolveLocationAsync(LibraryPath path, CancellationToken ct)
    {
        if (path.Root.Kind == LibraryRootKind.Pinned)
        {
            return string.Create(CultureInfo.InvariantCulture, $"pinned root \"{path.Root.DisplayName}\"");
        }

        string[] segments = path.RelativePath.Split('/');

        if (path.Root.Kind == LibraryRootKind.Teams)
        {
            return segments.Length >= 3
                ? string.Create(CultureInfo.InvariantCulture, $"Team {segments[0]}, Project {segments[1]}")
                : string.Create(CultureInfo.InvariantCulture, $"Team {segments[0]}");
        }

        string teammateFolder = segments[0];
        string name = await this.ResolveTeammateNameAsync(path.Root, teammateFolder, ct) ?? teammateFolder;

        if (segments.Length >= 3 && FolderSnapshot.PathComparer.Equals(segments[1], LibraryDocumentCollector.WorkDirFolderName))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{name}'s Work Dir");
        }

        return string.Create(CultureInfo.InvariantCulture, $"teammate {name}");
    }

    /// <summary>Reads a Teammate's definition file and returns its frontmatter <c>Name</c>, or
    /// <see langword="null"/> when the definition can't be resolved, read or parsed.</summary>
    private async Task<string?> ResolveTeammateNameAsync(LibraryRoot teammatesRoot, string teammateFolder, CancellationToken ct)
    {
        if (!this.resolver.TryResolve(teammatesRoot.Id, $"{teammateFolder}/{teammateFolder}.md", out LibraryPath? definitionPath, out _))
        {
            return null;
        }

        try
        {
            LibraryDocumentContent content = await this.files.ReadAsync(definitionPath, ct);
            if (content.Text is null || !PersonaFrontmatter.TryReadIdentity(content.Text, out PersonaIdentity? identity, out _))
            {
                return null;
            }

            return identity.Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>The Library documents mentioned across a Turn, capped and deduped (Spec §6.14).</summary>
/// <param name="Items">The kept documents, in first-seen order.</param>
/// <param name="NotListed">How many further, distinct documents were mentioned beyond the cap.</param>
internal sealed record LibraryDocumentsReport(IReadOnlyList<LibraryDocumentItem> Items, int NotListed)
{
    /// <summary>Whether nothing was collected at all.</summary>
    internal bool IsEmpty => this.Items.Count == 0;
}

/// <summary>One Library document mentioned in a Turn (Spec §6.14).</summary>
/// <param name="FullPath">The resolved absolute path on disk.</param>
/// <param name="Location">The §6.14 location label.</param>
/// <param name="Size">The document's size, formatted with <see cref="LibrarySize.Format"/>.</param>
/// <param name="Text">The decoded, possibly-truncated text, or <see langword="null"/> when not inlined.</param>
/// <param name="Truncated">Whether <see cref="Text"/> was cut short of the document's full content.</param>
/// <param name="Length">The document's true size in bytes.</param>
/// <param name="TooLarge">Whether the document is over <see cref="LibraryOptions.MaxEditableBytes"/>.</param>
internal sealed record LibraryDocumentItem(string FullPath, string Location, string Size, string? Text, bool Truncated, long Length, bool TooLarge);
