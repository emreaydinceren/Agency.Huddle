using System.Globalization;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>Formats a byte count for people: B below 1 KB, whole KB below 1 MB, and MB with one decimal above that.</summary>
internal static class ByteSize
{
    /// <summary>The size in the largest unit it reaches, in invariant culture, as the Composer already showed it.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>For example "512 B", "2 KB" or "5.0 MB".</returns>
    internal static string Format(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{(long)Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero)} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.0} MB"),
    };
}
