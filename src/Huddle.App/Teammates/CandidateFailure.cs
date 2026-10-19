namespace Agency.Huddle.App.Teammates;

/// <summary>One Candidate that did not become a Teammate, and why.</summary>
/// <param name="Name">The failed Candidate's proposed Name.</param>
/// <param name="Reason">
/// Why it failed - a <see cref="CandidateChecker"/> problem string, or the message of the
/// <see cref="Services.ChatException"/> <see cref="Acp.PersonaStore.Add"/> raised.
/// </param>
public sealed record CandidateFailure(string Name, string Reason);
