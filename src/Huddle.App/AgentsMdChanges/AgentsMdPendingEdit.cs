namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>An edit saved but not yet settled.</summary>
/// <param name="File">The file.</param>
/// <param name="Hash">The hash of its latest text.</param>
/// <param name="ChangedAt">When the text last changed.</param>
internal sealed record AgentsMdPendingEdit(AgentsMdFile File, string Hash, DateTimeOffset ChangedAt);
