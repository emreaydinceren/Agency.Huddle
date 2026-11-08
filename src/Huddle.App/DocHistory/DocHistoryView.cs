using System.Globalization;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.DocHistory;

/// <summary>Pure functions from a document's entries to what the byline, the panel and the History show (DocHistory spec §6.6).</summary>
internal static class DocHistoryView
{
    /// <summary>How many Names a byline part shows before "+N".</summary>
    internal const int MaxNames = 3;

    /// <summary>The byline, or <see langword="null"/> for an empty history.</summary>
    /// <param name="entries">The document's entries, oldest first.</param>
    /// <returns>The byline's parts.</returns>
    internal static DocByline? Byline(IReadOnlyList<DocHistoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return null;
        }

        string writer = entries.FirstOrDefault(static entry => entry.Kind == DocChangeKind.Created)?.Actor ?? entries[0].Actor;
        List<string> editedBy = ActorsOf(entries, DocChangeKind.Edited);
        editedBy.RemoveAll(name => string.Equals(name, writer, StringComparison.OrdinalIgnoreCase));
        return new DocByline(writer, editedBy, ActorsOf(entries, DocChangeKind.Reviewed), ActorsOf(entries, DocChangeKind.Objected), entries.Max(static entry => entry.At));
    }

    /// <summary>Joins Names for the byline: at most <see cref="MaxNames"/>, then "+N".</summary>
    /// <param name="names">The Names, in display order.</param>
    /// <returns>For example "Mira, Ruth, Quinn +2".</returns>
    internal static string JoinNames(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        string shown = string.Join(", ", names.Take(MaxNames));
        return names.Count > MaxNames
            ? string.Create(CultureInfo.InvariantCulture, $"{shown} +{names.Count - MaxNames}")
            : shown;
    }

    /// <summary>
    /// The newest entries first, at most <paramref name="max"/>. Consecutive Human saves with empty summaries
    /// within <paramref name="groupWindow"/> of the newer one collapse into one row showing the newest time.
    /// Nothing is collapsed in the file.
    /// </summary>
    /// <param name="entries">The document's entries, oldest first.</param>
    /// <param name="max">The most rows returned.</param>
    /// <param name="groupWindow">How close Human saves must be to share a row.</param>
    /// <returns>The rows, newest first.</returns>
    internal static IReadOnlyList<DocHistoryEntry> Recent(IReadOnlyList<DocHistoryEntry> entries, int max, TimeSpan groupWindow)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return [.. NewestFirstGrouped(entries, groupWindow).Take(Math.Max(max, 0))];
    }

    /// <summary>Every entry, newest first, grouped by calendar day in <paramref name="zone"/>; Human saves group as in <see cref="Recent"/>.</summary>
    /// <param name="entries">The document's entries, oldest first.</param>
    /// <param name="now">The current time, so the function stays pure.</param>
    /// <param name="zone">The viewer's time zone, which decides where a day starts.</param>
    /// <param name="groupWindow">How close Human saves must be to share a row.</param>
    /// <returns>The days, newest first.</returns>
    internal static IReadOnlyList<DocHistoryDay> ByDay(IReadOnlyList<DocHistoryEntry> entries, DateTimeOffset now, TimeZoneInfo zone, TimeSpan groupWindow)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(zone);

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        List<DocHistoryDay> days = [];
        DateOnly? current = null;
        List<DocHistoryEntry> bucket = [];
        foreach (DocHistoryEntry entry in NewestFirstGrouped(entries, groupWindow))
        {
            DateOnly day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.At, zone).DateTime);
            if (current is { } open && open != day)
            {
                days.Add(new DocHistoryDay(DayLabel(open, today), bucket));
                bucket = [];
            }

            current = day;
            bucket.Add(entry);
        }

        if (current is { } last)
        {
            days.Add(new DocHistoryDay(DayLabel(last, today), bucket));
        }

        return days;
    }

    /// <summary>A time the way the byline and the panel read it: "Today 11:02", "Yesterday 16:40" (the day words and 24-hour clock of <see cref="HumanDate.Moment"/>), or "11 Oct".</summary>
    /// <param name="at">The entry's time.</param>
    /// <param name="now">The current time, so the function stays pure.</param>
    /// <param name="zone">The viewer's time zone.</param>
    /// <returns>The short text.</returns>
    internal static string RelativeTime(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        DateTime local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        DateOnly day = DateOnly.FromDateTime(local);
        return day == today || day == today.AddDays(-1)
            ? HumanDate.Moment(at, today, zone)
            : local.ToString("d MMM", CultureInfo.InvariantCulture);
    }

    /// <summary>The label of an entry's kind in the History: Created, Edited, Reviewed, Objected, or Saved for a bare Human save.</summary>
    /// <param name="entry">An entry.</param>
    /// <returns>The label.</returns>
    internal static string KindLabel(DocHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return IsBareHumanSave(entry)
            ? "Saved"
            : entry.Kind switch
            {
                DocChangeKind.Created => "Created",
                DocChangeKind.Edited => "Edited",
                DocChangeKind.Reviewed => "Reviewed",
                _ => "Objected",
            };
    }

    /// <summary>"Today", "Yesterday", or the date as "11 October 2026".</summary>
    /// <param name="day">The day to label.</param>
    /// <param name="today">The viewer's today.</param>
    /// <returns>The heading text.</returns>
    internal static string DayLabel(DateOnly day, DateOnly today)
    {
        if (day == today)
        {
            return "Today";
        }

        if (day == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return day.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Whether an entry is a Human save with nothing to say, the kind that groups.</summary>
    /// <param name="entry">An entry.</param>
    /// <returns><see langword="true"/> for a Human <see cref="DocChangeKind.Edited"/> entry with an empty summary.</returns>
    internal static bool IsBareHumanSave(DocHistoryEntry entry) =>
        entry is { ActorKind: DocActorKind.Human, Kind: DocChangeKind.Edited, Summary.Length: 0 };

    /// <summary>Distinct actors of one kind, most recent first, compared case-insensitively as Names are.</summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <param name="kind">The kind to collect.</param>
    /// <returns>The Names.</returns>
    private static List<string> ActorsOf(IReadOnlyList<DocHistoryEntry> entries, DocChangeKind kind)
    {
        List<string> names = [];
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            DocHistoryEntry entry = entries[index];
            if (entry.Kind == kind && !names.Contains(entry.Actor, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(entry.Actor);
            }
        }

        return names;
    }

    /// <summary>The entries newest first, with each run of close bare Human saves by one Human reduced to its newest.</summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <param name="groupWindow">How close saves must be to share a row.</param>
    /// <returns>The rows, newest first.</returns>
    private static List<DocHistoryEntry> NewestFirstGrouped(IReadOnlyList<DocHistoryEntry> entries, TimeSpan groupWindow)
    {
        List<DocHistoryEntry> rows = [];
        DocHistoryEntry? previous = null;
        foreach (DocHistoryEntry entry in entries.OrderByDescending(static entry => entry.At))
        {
            bool joinsPrevious = previous is not null
                && IsBareHumanSave(entry)
                && IsBareHumanSave(previous)
                && string.Equals(entry.Actor, previous.Actor, StringComparison.OrdinalIgnoreCase)
                && previous.At - entry.At <= groupWindow;
            if (!joinsPrevious)
            {
                rows.Add(entry);
            }

            previous = entry;
        }

        return rows;
    }
}
