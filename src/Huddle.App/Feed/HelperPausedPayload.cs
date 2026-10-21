namespace Agency.Huddle.App.Feed;

/// <summary>A Helper stopped judging because its daily cap was reached.</summary>
/// <param name="HelperId">The Helper's id, such as <c>NeedsHuman</c>.</param>
/// <param name="Name">The Helper's display name when it paused.</param>
/// <param name="DailyCap">The calls a day before it pauses.</param>
/// <param name="Granted">The calls the Human had already granted today.</param>
internal sealed record HelperPausedPayload(string HelperId, string Name, int DailyCap, int Granted) : FeedPayload;
