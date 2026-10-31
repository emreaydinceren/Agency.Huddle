namespace Agency.Huddle.App.Feed;

/// <summary>One Feed item as its sender describes it.</summary>
/// <param name="SourceType">What kind of item it is.</param>
/// <param name="TargetKey">The Task id, Room id or Library absolute path it is about.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Notify">Whether it is a Notification for the Human.</param>
/// <param name="Payload">What it says; must be the record <see cref="FeedPayloads.TypeOf"/> names for the source type.</param>
internal sealed record FeedMessage(
    FeedSourceType SourceType,
    string TargetKey,
    FeedActor Actor,
    bool Notify,
    FeedPayload Payload);
