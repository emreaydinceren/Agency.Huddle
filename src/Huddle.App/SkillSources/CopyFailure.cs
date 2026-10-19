namespace Agency.Huddle.App.SkillSources;

/// <summary>A Skill that was not copied.</summary>
/// <param name="Name">The Skill.</param>
/// <param name="Kind">Why.</param>
/// <param name="Message">A sentence for the Human (§6.6, "Exact messages").</param>
internal sealed record CopyFailure(string Name, CopyFailureKind Kind, string Message);
