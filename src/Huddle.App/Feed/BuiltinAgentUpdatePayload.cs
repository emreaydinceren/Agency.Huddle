namespace Agency.Huddle.App.Feed;

/// <summary>A new shipped text exists for a built-in Agent whose file the Human edited, so it was left alone.</summary>
/// <param name="Agent">The Agent's current Name.</param>
/// <param name="Message">The item text: an update exists and Reset to default would replace the edits.</param>
internal sealed record BuiltinAgentUpdatePayload(string Agent, string Message) : FeedPayload;
