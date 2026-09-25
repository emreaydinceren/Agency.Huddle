namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library</c>, per Spec §7.</summary>
public sealed class LibraryOptions
{
    /// <summary>Whether the Library pane, page, sidebar link and chat links show. <see langword="false"/> hides all Library UI.</summary>
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
}
