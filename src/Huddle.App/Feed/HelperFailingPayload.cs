namespace Agency.Huddle.App.Feed;

/// <summary>A Helper failed several times in a row.</summary>
/// <param name="HelperId">The Helper's id, such as <c>NeedsHuman</c>.</param>
/// <param name="Name">The Helper's display name when it started failing.</param>
/// <param name="Failures">How many times it failed in a row.</param>
/// <param name="LastError">The last failure, in words for the card.</param>
internal sealed record HelperFailingPayload(string HelperId, string Name, int Failures, string LastError) : FeedPayload;
