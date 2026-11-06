namespace Agency.Huddle.App.Feed;

/// <summary>The Human switched account connectors on or off for an Agent (docs/Huddle.AgentConnectors-Specifications.md section 11).</summary>
/// <param name="Name">The Agent's name.</param>
/// <param name="On"><see langword="true"/> when switched on, <see langword="false"/> when switched off.</param>
internal sealed record AgentConnectorsPayload(string Name, bool On) : FeedPayload;
