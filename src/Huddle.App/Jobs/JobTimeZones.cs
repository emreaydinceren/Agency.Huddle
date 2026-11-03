using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Jobs;

/// <summary>Resolves a Job's time zone id. IANA and Windows ids both resolve on every OS .NET runs on.</summary>
internal static class JobTimeZones
{
    /// <summary>Finds a time zone by id.</summary>
    /// <param name="id">An IANA or Windows id.</param>
    /// <param name="zone">The zone, when found.</param>
    /// <returns><see langword="true"/> when the id names a zone.</returns>
    public static bool TryFind(string id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        ArgumentNullException.ThrowIfNull(id);
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone);
    }

    /// <summary>
    /// A zone's IANA name when it has one, so a new Job written on Windows names <c>America/Los_Angeles</c> rather than
    /// <c>Pacific Standard Time</c> and reads the same on any machine; otherwise its own id.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The id to store.</returns>
    public static string IdOf(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (zone.HasIanaId)
        {
            return zone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out string? iana) ? iana : zone.Id;
    }

    /// <summary>The answer to a time zone this machine does not know, the same for the tool and the form.</summary>
    /// <param name="id">The id as given.</param>
    /// <returns>The message.</returns>
    public static string Unknown(string id) => $"'{id}' is not a time zone this machine knows; use an IANA name such as Europe/Berlin.";

    /// <summary>The zone a Job's times are in: its own, or <paramref name="fallback"/> when it names none or one this machine lacks.</summary>
    /// <param name="id">The Job's zone id, or <see langword="null"/>.</param>
    /// <param name="fallback">The zone to use instead, normally the clock's local zone.</param>
    /// <returns>The zone.</returns>
    public static TimeZoneInfo Resolve(string? id, TimeZoneInfo fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return id is not null && TryFind(id, out TimeZoneInfo? zone) ? zone : fallback;
    }
}
