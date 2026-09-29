namespace Agency.Huddle.App.Teams;

/// <summary>The outcome, plus the store's message when the save was rejected.</summary>
public sealed record MembershipResult(MembershipOutcome Outcome, string? Problem = null);
