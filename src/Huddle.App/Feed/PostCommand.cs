namespace Agency.Huddle.App.Feed;

/// <summary>Insert one Feed item.</summary>
/// <param name="Message">The item.</param>
/// <param name="At">When it was posted.</param>
internal sealed record PostCommand(FeedMessage Message, DateTimeOffset At) : FeedCommand(At);
