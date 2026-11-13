namespace Agency.Huddle.App.Feed;

/// <summary>A Team rename that did not finish (Team Rename Spec §5.4).</summary>
/// <param name="Team">The Team's new name.</param>
/// <param name="Message">The item text, saying what failed and that Huddle tries again at the next start.</param>
internal sealed record TeamRenameStuckPayload(string Team, string Message) : FeedPayload;
