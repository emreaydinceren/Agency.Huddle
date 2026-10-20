namespace Agency.Huddle.App.Feed;

/// <summary>A Task needs the Human.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="ProofMet">How many proof items are met.</param>
/// <param name="ProofTotal">How many proof items there are.</param>
internal sealed record TaskNeedsYouPayload(string Id, string Title, string Team, int ProofMet, int ProofTotal) : FeedPayload;
