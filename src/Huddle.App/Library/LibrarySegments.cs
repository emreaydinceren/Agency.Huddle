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

/// <summary>A single problem found in a path segment by <see cref="LibrarySegments.Check(string)"/>.</summary>
/// <param name="Kind">The kind of problem found.</param>
/// <param name="Character">The offending character, when <paramref name="Kind"/> is <see cref="SegmentProblemKind.InvalidCharacter"/>.</param>
/// <param name="DeviceName">The reserved device name, when <paramref name="Kind"/> is <see cref="SegmentProblemKind.ReservedDeviceName"/>.</param>
internal sealed record SegmentProblem(SegmentProblemKind Kind, char Character = '\0', string? DeviceName = null);

/// <summary>
/// The single per-segment rule set shared by <see cref="LibraryPathResolver"/>'s step 2 and
/// <c>LibraryNames.Validate</c> (corrections-B3 Addendum A1), so the two can never drift. Every rule
/// lives once in <see cref="Check(string)"/>; <see cref="Refusal(string)"/> and <c>LibraryNames.Validate</c>
/// each map the result to their own settled user-facing text. Behaviour is identical on every OS except
/// the 8.3-alias row, which is Windows-only.
/// </summary>
internal static class LibrarySegments
{
    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// Checks one path segment against every step-2 rule (corrections-B3 4.2.t item 16, and item 23
    /// for the Windows-only 8.3-alias check). Returns <see langword="null"/> when the segment is
    /// acceptable, or the problem found otherwise.
    /// </summary>
    /// <param name="segment">The single path segment to check (never containing a separator).</param>
    /// <returns><see langword="null"/> when acceptable, the problem found otherwise.</returns>
    internal static SegmentProblem? Check(string segment)
    {
        if (segment.Length == 0)
        {
            return new SegmentProblem(SegmentProblemKind.Empty);
        }

        if (segment is "." or "..")
        {
            return new SegmentProblem(SegmentProblemKind.DotSegment);
        }

        if (segment[^1] is '.' or ' ')
        {
            return new SegmentProblem(SegmentProblemKind.TrailingDotOrSpace);
        }

        foreach (char c in segment)
        {
            if (c is ':' or '<' or '>' or '"' or '|' or '?' or '*' || char.IsControl(c))
            {
                return new SegmentProblem(SegmentProblemKind.InvalidCharacter, Character: c);
            }
        }

        string stem = segment;
        int dotIndex = stem.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex >= 0)
        {
            stem = stem[..dotIndex];
        }

        foreach (string deviceName in DeviceNames)
        {
            if (string.Equals(stem, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return new SegmentProblem(SegmentProblemKind.ReservedDeviceName, DeviceName: deviceName);
            }
        }

        if (OperatingSystem.IsWindows() && IsEightDotThreeAlias(segment))
        {
            return new SegmentProblem(SegmentProblemKind.ShortNameAlias);
        }

        return null;
    }

    /// <summary>
    /// Checks one path segment against every step-2 rule via <see cref="Check(string)"/>, collapsing
    /// any problem to the boundary's single refusal text.
    /// </summary>
    /// <param name="segment">The single path segment to check (never containing a separator).</param>
    /// <returns><see langword="null"/> when acceptable, a refusal reason otherwise.</returns>
    internal static string? Refusal(string segment) =>
        Check(segment) is null ? null : "That path isn't inside the Library.";

    /// <summary>
    /// True when <paramref name="segment"/> is shaped like an 8.3 alias (<c>NAME~1</c>,
    /// corrections-B3 item 23): 1-6 characters other than <c>~</c>, then <c>~</c>, then one or more
    /// digits, with nothing after.
    /// </summary>
    /// <param name="segment">The segment to check.</param>
    private static bool IsEightDotThreeAlias(string segment)
    {
        int tilde = segment.IndexOf('~', StringComparison.Ordinal);
        if (tilde is < 1 or > 6)
        {
            return false;
        }

        for (int i = 0; i < tilde; i++)
        {
            if (segment[i] == '~')
            {
                return false;
            }
        }

        if (tilde == segment.Length - 1)
        {
            return false;
        }

        for (int i = tilde + 1; i < segment.Length; i++)
        {
            if (!char.IsAsciiDigit(segment[i]))
            {
                return false;
            }
        }

        return true;
    }
}
