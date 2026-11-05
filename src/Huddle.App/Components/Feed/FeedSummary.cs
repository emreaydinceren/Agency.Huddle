using System.Globalization;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>
/// The interface copy of a Feed item: the words of its first line and of a Notification's status line (Feed spec §7). Pure,
/// so the wording is pinned by tests without rendering anything.
/// </summary>
internal static class FeedSummary
{
    /// <summary>The longest a Task title runs inside its chip before it is cut with an ellipsis.</summary>
    internal const int TaskTitleLimit = 32;

    /// <summary>What an item's first line says, after the actor's Name.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The line; an item this build cannot read says only that something happened.</returns>
    public static FeedSummaryLine Of(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Payload switch
        {
            TaskCreatedPayload p => new([Text("created "), TaskChip(p.Id, p.Title)], InTeam(p.Team)),
            TaskGatePayload p => TaskGate(p),
            TaskNeedsYouPayload p => new([Text("stopped to wait for your approval on "), TaskChip(p.Id, p.Title)], InTeam(p.Team)),
            TaskAssignedPayload p => new([Text("assigned you "), TaskChip(p.Id, p.Title)], null),
            TaskWakesPausedPayload { RoomId: not null } p => new(
                [Text("held a hand-off on "), TaskChip(p.Id, p.Title), Text(": its Room is paused")],
                $"The hand-off to {p.Waiting} waits until you press Continue in the Room"),
            TaskWakesPausedPayload p => new(
                [Text("paused wake-ups on "), TaskChip(p.Id, p.Title), Text(": Agents changed it too many times")],
                $"The hand-off to {p.Waiting} waits until you allow more"),
            RequestAwaitsPayload p => new([Text("finished the Request "), TaskChip(p.Id, p.Title), Text(" from "), Strong(p.FromTeam)], "only you can confirm it"),
            GateDecisionPayload p => new([Text("was stopped by a gate moving "), TaskChip(p.Id, p.Title), Text(" to " + p.ToStatus)], p.Message),
            RoomWaitPayload p => RoomWait(item.SourceType, p),
            RoomCreatedPayload p => new([Text("opened "), RoomChip(p.RoomId, RoomLabel(p))], Agents(p)),
            RoomJoinedPayload p => new([Strong(p.Member), Text(" joined "), RoomChip(p.RoomId, p.RoomName)], null, OmitActor: true),
            RoomArchivedPayload p => new([Text("archived "), RoomChip(p.RoomId, p.RoomName)], null),
            DocumentChangedPayload p => new([Text(DocumentVerb(p.Kind) + " "), DocumentChip(p)], p.Summary.Length > 0 ? p.Summary : null),
            HelperPausedPayload p => new(
                [Text("paused "), HelperChip(p.HelperId, p.Name), Text(string.Create(CultureInfo.InvariantCulture, $": its daily cap of {p.DailyCap + p.Granted} calls is reached"))],
                "Agents' Tasks are not judged until you continue or the day ends"),
            HelperFailingPayload p => new(
                [HelperChip(p.HelperId, p.Name), Text(string.Create(CultureInfo.InvariantCulture, $" failed {p.Failures} times in a row"))],
                p.LastError.Length > 0 ? p.LastError : null),
            TeamMemoryNotFiledPayload p => new(
                [HelperChip(p.HelperId, p.Name), Text(" didn't file "), Strong(p.Proposer), Text("'s proposal for "), Strong(p.Team), Text(": " + p.Reason)],
                p.Fact,
                OmitActor: true),
            PairingRequestedPayload p => new([DeviceChip(p.DeviceName), Text(" wants to use Huddle")], null, OmitActor: true),
            AdminRequestedPayload p => new([DeviceChip(p.DeviceName), Text(" asks for admin access")], null, OmitActor: true),
            DevicePairedPayload p => new([DeviceChip(p.DeviceName), Text(" was paired")], null, OmitActor: true),
            DeviceRevokedPayload p => new([DeviceChip(p.DeviceName), Text(DeviceEnded(p.Action))], null, OmitActor: true),
            SecondBrainOverCapPayload p => new([Text(p.Message)], null, OmitActor: true),
            TeamArchivedPayload p => new([Text(p.Message)], null, OmitActor: true),
            _ => new([Text("Something happened")], null, OmitActor: true),
        };
    }

    /// <summary>The status line under a Notification, or <see langword="null"/> for an Activity item.</summary>
    /// <param name="item">The item.</param>
    /// <param name="zone">The Human's time zone, for the clock times.</param>
    /// <returns>The line, or <see langword="null"/> when the item is not a Notification.</returns>
    public static FeedStatusLine? StatusOf(FeedItem item, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zone);

        if (!item.Notify)
        {
            return null;
        }

        string opening = item.SourceType switch
        {
            FeedSourceType.RoomBudget or FeedSourceType.HelperPaused or FeedSourceType.TaskWakesPaused => "Paused ",
            FeedSourceType.HelperFailing => "Since ",
            _ => "Asked ",
        } + Clock(item.At, zone);
        if (item.ResolvedAt is { } resolvedAt)
        {
            return new(true, opening + " · done " + Clock(resolvedAt, zone));
        }

        return new(false, opening + item.Payload switch
        {
            TaskNeedsYouPayload p => string.Create(CultureInfo.InvariantCulture, $" · proof {p.ProofMet}/{p.ProofTotal} · waiting for you"),
            GateDecisionPayload => " · only you can move it anyway",
            RoomWaitPayload when item.SourceType == FeedSourceType.RoomBudget => " · waiting for you to continue or wrap up",
            TaskGatePayload => " · you can turn it off on the Task",
            HelperPausedPayload => " · Continue is on the Helper's card",
            TaskWakesPausedPayload { RoomId: not null } => " · Continue is on the Room's Budget card",
            TaskWakesPausedPayload => " · Allow more is on the Task; after a restart, Wake is on this card",
            HelperFailingPayload => " · Tasks are not being judged",
            _ => " · waiting for you",
        });
    }

    /// <summary>The Team an item is about, for grouping and filtering; <see langword="null"/> for a root Room, a document or an unreadable item.</summary>
    /// <param name="payload">The item's payload.</param>
    /// <returns>The Team Name, or <see langword="null"/>.</returns>
    public static string? TeamOf(FeedPayload? payload) => payload switch
    {
        TaskCreatedPayload p => p.Team,
        TaskGatePayload p => p.Team,
        TaskNeedsYouPayload p => p.Team,
        TaskWakesPausedPayload p => p.Team,
        TaskAssignedPayload p => p.Team,
        RequestAwaitsPayload p => p.Team,
        GateDecisionPayload p => p.Team,
        RoomWaitPayload p => p.Team,
        RoomCreatedPayload p => p.Team,
        RoomJoinedPayload p => p.Team,
        RoomArchivedPayload p => p.Team,
        TeamMemoryNotFiledPayload p => p.Team,
        _ => null,
    };

    /// <summary>The plural noun a group of items of one type is counted in: <c>Tasks</c> in "and 2 more Tasks".</summary>
    /// <param name="type">The group's source type.</param>
    /// <returns>The noun.</returns>
    public static string NounOf(FeedSourceType? type) => type switch
    {
        FeedSourceType.TaskCreated or FeedSourceType.TaskGateOn or FeedSourceType.TaskNeedsYou
            or FeedSourceType.TaskAssignedToYou or FeedSourceType.RequestAwaitsYou or FeedSourceType.GateDecisionDue or FeedSourceType.TaskWakesPaused => "Tasks",
        FeedSourceType.DocumentChanged => "documents",
        FeedSourceType.HelperPaused or FeedSourceType.HelperFailing => "Helpers",
        FeedSourceType.TeamMemoryNotFiled => "proposals",
        FeedSourceType.PairingRequested or FeedSourceType.AdminRequested or FeedSourceType.DevicePaired or FeedSourceType.DeviceRevoked => "devices",
        FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.RoomJob
            or FeedSourceType.RoomCreated or FeedSourceType.RoomJoined or FeedSourceType.RoomArchived => "Rooms",
        _ => "items",
    };

    private static string DeviceEnded(string action) => action switch
    {
        "removed" => " was removed",
        "rotated" => " got a new token",
        _ => " was revoked",
    };

    private static FeedSegment DeviceChip(string name) => new(FeedSegmentKind.Device, name);

    private static FeedSummaryLine TaskGate(TaskGatePayload p)
    {
        string? context = p.Reason is { Length: > 0 } reason ? reason : InTeam(p.Team);
        return p.Action is { Length: > 0 } action
            ? new([Text("set the Action \"" + action + "\" on "), TaskChip(p.Id, p.Title)], context)
            : new([Text("turned on Requires your acceptance for "), TaskChip(p.Id, p.Title)], context);
    }

    private static FeedSummaryLine RoomWait(FeedSourceType? type, RoomWaitPayload p)
    {
        FeedSegment room = RoomChip(p.RoomId, p.RoomName ?? "a Room that was deleted");
        return type switch
        {
            FeedSourceType.RoomMention => new([Text("mentioned you in "), room], null),
            FeedSourceType.RoomQuestion => new([Text("asked you a Question in "), room], null),
            FeedSourceType.RoomForm => new([Text("is waiting for you to fill in a Form in "), room], null),
            FeedSourceType.RoomJob => new([Text("ran a scheduled Job for you in "), room], null),
            _ => new([room, Text(" reached its Budget")], null, OmitActor: false),
        };
    }

    private static string DocumentVerb(string kind) => kind switch
    {
        "created" => "created",
        "reviewed" => "reviewed",
        "objected" => "objected to",
        _ => "edited",
    };

    private static string RoomLabel(RoomCreatedPayload p) =>
        p.Members.Count == 2 ? p.Members[0] + " ↔ " + p.Members[1] : p.RoomName;

    private static string? Agents(RoomCreatedPayload p)
    {
        int count = Math.Max(p.Members.Count - 1, 1);
        return string.Create(CultureInfo.InvariantCulture, $"{count} Agent{(count == 1 ? string.Empty : "s")}");
    }

    private static string? InTeam(string team) => team.Length > 0 ? "in " + team : null;

    private static string Clock(DateTimeOffset at, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static FeedSegment Text(string text) => new(FeedSegmentKind.Text, text);

    private static FeedSegment Strong(string text) => new(FeedSegmentKind.Strong, text);

    private static FeedSegment RoomChip(string roomId, string label) => new(FeedSegmentKind.Room, label, roomId);

    private static FeedSegment HelperChip(string helperId, string name) => new(FeedSegmentKind.Helper, name, helperId);

    private static FeedSegment DocumentChip(DocumentChangedPayload p) => new(FeedSegmentKind.Document, p.RelativePath, p.Path);

    private static FeedSegment TaskChip(string id, string title) => new(
        FeedSegmentKind.Task,
        title.Length <= TaskTitleLimit ? title : title[..(TaskTitleLimit - 1)] + "…",
        id,
        title);
}
