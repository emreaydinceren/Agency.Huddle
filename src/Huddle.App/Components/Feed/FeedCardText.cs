using System.Globalization;
using Agency.Huddle.App.Feed;
using MudBlazor;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>The words, pills and badges of a Feed card that are decided without rendering anything (Feed spec §6.13).</summary>
internal static class FeedCardText
{
    /// <summary>The status pill of a card, or <see langword="null"/> for an Activity item.</summary>
    /// <param name="item">The item.</param>
    /// <param name="now">The current time.</param>
    /// <param name="zone">The Human's time zone, for the clock time of a resolved item.</param>
    /// <param name="due">The due date of the Task a <c>task-assigned-to-you</c> item is about, when it could be read.</param>
    /// <returns>The pill.</returns>
    public static FeedPill? PillOf(FeedItem item, DateTimeOffset now, TimeZoneInfo zone, DateOnly? due)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        if (!item.Notify)
        {
            return null;
        }

        if (item.ResolvedAt is { } resolvedAt)
        {
            return new(FeedPillKind.Done, "Done · " + TimeZoneInfo.ConvertTime(resolvedAt, zone).ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        if (item.SourceType == FeedSourceType.TaskAssignedToYou)
        {
            string text = due is { } day ? "Assigned to you · due " + day.ToString("ddd d MMM", CultureInfo.InvariantCulture) : "Assigned to you";
            return new(FeedPillKind.Assigned, text);
        }

        return new(FeedPillKind.Waiting, "Waiting for you · " + FeedTime.CardAge(now, item.At), item.SourceType == FeedSourceType.RoomBudget);
    }

    /// <summary>The two-letter monogram of a Team: the initials of its first two words, or its first two letters.</summary>
    /// <param name="team">The Team Name.</param>
    /// <returns>The monogram in capitals.</returns>
    public static string Monogram(string team)
    {
        ArgumentNullException.ThrowIfNull(team);

        string[] words = team.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        string letters = words.Length >= 2 ? new string([words[0][0], words[1][0]]) : words[0][..Math.Min(2, words[0].Length)];
        return letters.ToUpperInvariant();
    }

    /// <summary>The badge a kind of item wears.</summary>
    /// <param name="type">The item's source type.</param>
    /// <returns>The badge.</returns>
    public static FeedBadge BadgeOf(FeedSourceType? type) => type switch
    {
        FeedSourceType.TaskNeedsYou => new("check", Icons.Material.Filled.Check),
        FeedSourceType.DocumentChanged => new("document", Icons.Material.Filled.Description),
        FeedSourceType.TaskAssignedToYou => new("person", Icons.Material.Filled.Person),
        FeedSourceType.RoomBudget or FeedSourceType.HelperPaused => new("gauge", Icons.Material.Filled.Speed),
        FeedSourceType.HelperFailing => new("alert", Icons.Material.Outlined.ErrorOutline),
        FeedSourceType.TaskGateOn or FeedSourceType.GateDecisionDue => new("lock", Icons.Material.Filled.Lock),
        FeedSourceType.RoomMention => new("mention", Icons.Material.Filled.AlternateEmail),
        FeedSourceType.RoomQuestion => new("question", Icons.Material.Filled.QuestionMark),
        FeedSourceType.RoomForm => new("form", Icons.Material.Filled.ListAlt),
        FeedSourceType.TaskCreated or FeedSourceType.RoomCreated => new("plus", Icons.Material.Filled.Add),
        FeedSourceType.RoomJoined => new("joined", Icons.Material.Filled.GroupAdd),
        FeedSourceType.RoomArchived => new("archived", Icons.Material.Filled.Archive),
        FeedSourceType.RequestAwaitsYou => new("request", Icons.Material.Filled.Inbox),
        _ => new("other", Icons.Material.Filled.Circle),
    };

    /// <summary>The context word of the meta line: where the item belongs.</summary>
    /// <param name="kind">What the item is about.</param>
    /// <returns>"Tasks", "Rooms", "Library" or "Helpers"; empty when the item is unreadable.</returns>
    public static string ContextWord(FeedTargetKind kind) => kind switch
    {
        FeedTargetKind.Task => "Tasks",
        FeedTargetKind.Room => "Rooms",
        FeedTargetKind.Document => "Library",
        FeedTargetKind.Helper => "Helpers",
        _ => string.Empty,
    };
}
