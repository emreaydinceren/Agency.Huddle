namespace Agency.Huddle.App.Feed;

/// <summary>One stored Feed item as the readers see it.</summary>
/// <param name="Id">The row id; grows with time and is never reused.</param>
/// <param name="At">When it was posted.</param>
/// <param name="SourceType">The source type, or <see langword="null"/> for a wire name this build does not know.</param>
/// <param name="TargetKey">The Task id, Room id or Library absolute path it is about.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Notify">Whether it is a Notification.</param>
/// <param name="SeenAt">When the bell marked it seen (Notifications only).</param>
/// <param name="DismissedAt">When it was dismissed (Notifications only).</param>
/// <param name="ResolvedAt">When its condition ended (Notifications only).</param>
/// <param name="Payload">What it says, or <see langword="null"/> when the row is unreadable ("Something happened").</param>
internal sealed record FeedItem(
    long Id,
    DateTimeOffset At,
    FeedSourceType? SourceType,
    string TargetKey,
    FeedActor Actor,
    bool Notify,
    DateTimeOffset? SeenAt,
    DateTimeOffset? DismissedAt,
    DateTimeOffset? ResolvedAt,
    FeedPayload? Payload);
