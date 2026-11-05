namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>One settled edit in a file's chain.</summary>
/// <param name="FromHash">The head before the edit; empty for a first sight.</param>
/// <param name="ToHash">The head after the edit.</param>
/// <param name="SettledUtc">When the edit settled.</param>
/// <param name="Notice">What a Room is told.</param>
internal sealed record AgentsMdRevision(string FromHash, string ToHash, DateTimeOffset SettledUtc, AgentsMdNotice Notice);
