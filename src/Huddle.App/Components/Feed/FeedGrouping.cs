using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>Joins neighbouring items that read as one thing (Feed spec §6.12, D-23). Only drawing groups; storage never does.</summary>
internal static class FeedGrouping
{
    /// <summary>The most two neighbouring items of one group may be apart.</summary>
    internal static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Groups <paramref name="items"/> (newest first). Two neighbours join when they have the same actor, the same source type and
    /// the same Team, are at most ten minutes apart, and fall on the same local day. An item of a type this build cannot read never joins.
    /// </summary>
    /// <param name="items">The items to draw, newest first.</param>
    /// <param name="zone">The Human's time zone, for the day rule.</param>
    /// <returns>The groups, in the same order.</returns>
    public static IReadOnlyList<FeedGroup> Group(IReadOnlyList<FeedItem> items, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(zone);

        List<FeedGroup> groups = [];
        List<FeedItem> current = [];
        foreach (FeedItem item in items)
        {
            if (current.Count > 0 && !Joins(current[^1], item, zone))
            {
                groups.Add(new FeedGroup([.. current]));
                current.Clear();
            }

            current.Add(item);
        }

        if (current.Count > 0)
        {
            groups.Add(new FeedGroup([.. current]));
        }

        return groups;
    }

    private static bool Joins(FeedItem previous, FeedItem next, TimeZoneInfo zone) =>
        previous.SourceType is not null
        && previous.SourceType == next.SourceType
        && previous.Notify == next.Notify
        && previous.Actor == next.Actor
        && string.Equals(FeedSummary.TeamOf(previous.Payload), FeedSummary.TeamOf(next.Payload), StringComparison.OrdinalIgnoreCase)
        && (previous.At - next.At).Duration() <= MaxGap
        && FeedTime.DayOf(previous.At, zone) == FeedTime.DayOf(next.At, zone);
}
