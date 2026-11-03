namespace Agency.Huddle.App.Feed;

/// <summary>A Team was archived (Spec §6.7 step 8).</summary>
/// <param name="Team">The Team's name.</param>
/// <param name="Message">The item text, <c>Team &lt;team&gt; archived</c>.</param>
internal sealed record TeamArchivedPayload(string Team, string Message) : FeedPayload;
