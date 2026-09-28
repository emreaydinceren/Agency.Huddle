namespace Agency.Huddle.App.Library;

/// <summary>The kind of file in the Library.</summary>
public enum LibraryFileKind
{
    /// <summary>Markdown text file.</summary>
    Markdown,

    /// <summary>Plain text file.</summary>
    Text,

    /// <summary>Image file.</summary>
    Image,

    /// <summary>SVG vector file.</summary>
    Svg,

    /// <summary>Other file type.</summary>
    Other,
}
