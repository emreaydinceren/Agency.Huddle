namespace Agency.Huddle.App.Feed;

/// <summary>Dismiss one Notification.</summary>
/// <param name="Id">The Notification's id.</param>
/// <param name="At">When it was dismissed.</param>
internal sealed record DismissCommand(long Id, DateTimeOffset At) : FeedCommand(At);
