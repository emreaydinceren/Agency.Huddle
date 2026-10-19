namespace Agency.Huddle.App.Library;

/// <summary>Why <see cref="LibraryFileService.ReadImageAsync"/> refused a file.</summary>
public enum LibraryImageRefusal
{
    /// <summary>The magic bytes are not a supported raster image, or the header could not be parsed. SVG lands here.</summary>
    NotAnImage,

    /// <summary>The file is over the byte cap.</summary>
    TooLarge,

    /// <summary>The longest side is over the edge cap.</summary>
    TooManyPixels,

    /// <summary>The path no longer resolves inside a Library Root, or the file could not be opened or read.</summary>
    Unreadable,
}
