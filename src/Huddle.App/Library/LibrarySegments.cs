namespace Agency.Huddle.App.Library;

/// <summary>
/// The single per-segment boundary check shared by <see cref="LibraryPathResolver"/>'s step 2 and
/// <c>LibraryNames.Validate</c> (corrections-B3 Addendum A1), so the two can never drift. Behaviour
/// is identical on every OS except the 8.3-alias row, which is Windows-only.
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
    /// acceptable, or a refusal reason otherwise.
    /// </summary>
    /// <param name="segment">The single path segment to check (never containing a separator).</param>
    /// <returns><see langword="null"/> when acceptable, a refusal reason otherwise.</returns>
    internal static string? Refusal(string segment)
    {
        if (segment.Length == 0 || segment is "." or "..")
        {
            return "That path isn't inside the Library.";
        }

        if (segment[^1] is '.' or ' ')
        {
            return "That path isn't inside the Library.";
        }

        foreach (char c in segment)
        {
            if (c is ':' or '<' or '>' or '"' or '|' or '?' or '*' || char.IsControl(c))
            {
                return "That path isn't inside the Library.";
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
                return "That path isn't inside the Library.";
            }
        }

        if (OperatingSystem.IsWindows() && IsEightDotThreeAlias(segment))
        {
            return "That path isn't inside the Library.";
        }

        return null;
    }

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
