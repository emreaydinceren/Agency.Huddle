namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>What <see cref="AgentsMdLedger.Observe"/> found.</summary>
/// <param name="RelativePath">The file observed.</param>
/// <param name="Hash">The hash of its text; empty when absent or unreadable.</param>
/// <param name="FirstSight">True when this call recorded the file's first sight.</param>
/// <param name="Pending">True when the file now has an unsettled edit.</param>
internal sealed record AgentsMdObservation(string RelativePath, string Hash, bool FirstSight, bool Pending);
