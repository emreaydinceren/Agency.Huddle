namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library</c>, per Spec §7.</summary>
public sealed class LibraryOptions
{
    /// <summary>Whether the Library shows in the UI and agent context. <see langword="false"/> hides all Library UI and passes no <see cref="LibraryDocumentCollector"/> to a Turn.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pinned roots, as <c>[{ "Name": "Huddle docs", "Path": "E:\\Repos\\Huddle\\docs" }]</c>.
    /// Nullable, with no initialiser: <c>ConfigurationBinder</c> appends bound array elements to an
    /// already-populated list/array property instead of replacing it (the "Collection options need
    /// no initialiser" rule), so a configured value must be read through this property directly.
    /// </summary>
    public IReadOnlyList<PinnedRootOption>? Roots { get; set; }

    /// <summary>Above this, a text file opens read-only.</summary>
    public int MaxEditableBytes { get; set; } = 2097152;

    /// <summary>Above this, a root's wikilink index is not built and backlinks say so.</summary>
    public int MaxIndexedFiles { get; set; } = 5000;

    /// <summary>The most Library documents listed in one Turn's prompt; the rest are counted.</summary>
    public int MaxReferencedDocuments { get; set; } = 10;

    /// <summary>Per document, the most text inlined for an Adapter without file tools.</summary>
    public int MaxInlineBytes { get; set; } = 16384;

    /// <summary>
    /// The most bytes of one image sent to an Adapter as a Prompt block. 3 MiB is 4 MiB once
    /// base64-encoded, under the 5 MB Anthropic documents as its per-image limit. Zero or less sends none.
    /// </summary>
    public int MaxImageBytes { get; set; } = 3145728;

    /// <summary>The most image Prompt blocks in one Turn's prompt; the rest stay path lines. Zero or less sends none.</summary>
    public int MaxImagesPerTurn { get; set; } = 4;

    /// <summary>The most raw image bytes in one Turn's prompt, about a third more on the wire. Zero or less sends none.</summary>
    public int MaxImageBytesPerTurn { get; set; } = 8388608;

    /// <summary>The longest side, in pixels, of an image sent as a Prompt block. Anthropic documents 8,000 as its limit.</summary>
    public int MaxImageEdgePixels { get; set; } = 8000;
}
