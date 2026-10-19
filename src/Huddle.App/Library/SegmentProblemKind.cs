namespace Agency.Huddle.App.Library;

/// <summary>
/// The kind of problem found in a path segment by <see cref="LibrarySegments.Check(string)"/>.
/// </summary>
internal enum SegmentProblemKind
{
    /// <summary>The segment is empty.</summary>
    Empty,

    /// <summary>The segment is <c>.</c> or <c>..</c>.</summary>
    DotSegment,

    /// <summary>The segment contains a character that is invalid in a file or folder name.</summary>
    InvalidCharacter,

    /// <summary>The segment ends with a dot or a space.</summary>
    TrailingDotOrSpace,

    /// <summary>The segment's stem is a name reserved by Windows (e.g. <c>CON</c>, <c>NUL</c>).</summary>
    ReservedDeviceName,

    /// <summary>The segment is shaped like a Windows 8.3 short-name alias (e.g. <c>ROOM-S~1</c>).</summary>
    ShortNameAlias,
}
