using System.Globalization;

namespace Agency.Huddle.App.Library;

/// <summary>Utilities for formatting file sizes as human-readable text.</summary>
internal static class LibrarySize
{
    /// <summary>Format a byte count as a human-readable size string: "X B", "X KB", or "X.X MB".</summary>
    /// <param name="bytes">The byte count.</param>
    /// <returns>The formatted size string in invariant culture.</returns>
    public static string Format(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        if (bytes < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        }

        if (bytes < 1048576)
        {
            long kilobytes = (bytes + 1023) / 1024;
            return string.Create(CultureInfo.InvariantCulture, $"{kilobytes} KB");
        }

        double megabytes = bytes / 1048576.0;
        return string.Create(CultureInfo.InvariantCulture, $"{megabytes:0.#} MB");
    }
}
