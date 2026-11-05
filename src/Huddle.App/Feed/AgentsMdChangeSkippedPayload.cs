namespace Agency.Huddle.App.Feed;

/// <summary>An AGENTS.md change that the Helper judged not worth telling Agents about, so no Agent was told (spec 6.8).</summary>
/// <param name="HelperId">The Helper's id.</param>
/// <param name="Name">The Helper's display name.</param>
/// <param name="Path">The relative path of the AGENTS.md file that changed.</param>
/// <param name="Reason">Why the Helper did not tell Agents.</param>
internal sealed record AgentsMdChangeSkippedPayload(string HelperId, string Name, string Path, string Reason) : FeedPayload;
