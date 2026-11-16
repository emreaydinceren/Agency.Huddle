namespace Agency.Huddle.App.Feed;

/// <summary>The Human switched an Agent's browser (the Playwright MCP server, issue #551) on or off.</summary>
/// <param name="Name">The Agent's name.</param>
/// <param name="On"><see langword="true"/> when switched on, <see langword="false"/> when switched off.</param>
internal sealed record AgentBrowserPayload(string Name, bool On) : FeedPayload;
