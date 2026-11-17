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

    /// <summary>The badge a kind of item wears.</summary>
    /// <param name="type">The item's source type.</param>
    /// <returns>The badge.</returns>
    public static FeedBadge BadgeOf(FeedSourceType? type) => type switch
    {
        FeedSourceType.TaskNeedsYou => new("check", Icons.Material.Filled.Check, "Needs you"),
        FeedSourceType.DocumentChanged or FeedSourceType.TeamMemoryNotFiled or FeedSourceType.AgentsMdChangeSkipped or FeedSourceType.MemoryTidied => new("document", Icons.Material.Filled.Description, "Document"),
        FeedSourceType.TaskAssignedToYou => new("person", Icons.Material.Filled.Person, "Assigned to you"),
        FeedSourceType.RoomBudget or FeedSourceType.HelperPaused or FeedSourceType.TaskWakesPaused or FeedSourceType.RoomMentionHeld => new("gauge", Icons.Material.Filled.Speed, "Limit reached"),
        FeedSourceType.HelperFailing => new("alert", Icons.Material.Outlined.ErrorOutline, "Failing"),
        FeedSourceType.TaskGateOn or FeedSourceType.GateDecisionDue => new("lock", Icons.Material.Filled.Lock, "Human Gate"),
        FeedSourceType.RoomMention => new("mention", Icons.Material.Filled.AlternateEmail, "Mention"),
        FeedSourceType.RoomQuestion => new("question", Icons.Material.Filled.QuestionMark, "Question"),
        FeedSourceType.RoomForm => new("form", Icons.Material.Filled.ListAlt, "Form"),
        FeedSourceType.RoomJob or FeedSourceType.JobGaveUp => new("schedule", Icons.Material.Filled.Schedule, "Scheduled job"),
        FeedSourceType.TaskCreated or FeedSourceType.RoomCreated => new("plus", Icons.Material.Filled.Add, "Created"),
        FeedSourceType.RoomJoined => new("joined", Icons.Material.Filled.GroupAdd, "Joined"),
        FeedSourceType.RoomArchived => new("archived", Icons.Material.Filled.Archive, "Archived"),
        FeedSourceType.RequestAwaitsYou => new("request", Icons.Material.Filled.Inbox, "Request"),
        FeedSourceType.PairingRequested or FeedSourceType.AdminRequested or FeedSourceType.DevicePaired or FeedSourceType.DeviceRevoked => new("device", Icons.Material.Outlined.Devices, "Device"),
        _ => new("other", Icons.Material.Filled.Circle, "Activity"),
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
        FeedTargetKind.Device => "Devices",
        _ => string.Empty,
    };
}
