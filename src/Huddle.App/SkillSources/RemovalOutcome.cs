namespace Agency.Huddle.App.SkillSources;

/// <summary>How a removal ended, in words the snackbar shows.</summary>
/// <param name="Ok">Whether every step ran.</param>
/// <param name="Message">The snackbar text.</param>
internal sealed record RemovalOutcome(bool Ok, string Message);
