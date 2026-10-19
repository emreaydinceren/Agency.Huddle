namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>One thing the move could not carry over.</summary>
/// <param name="Subject">The file or item.</param>
/// <param name="Message">One sentence for the Human.</param>
internal sealed record MoveProblem(string Subject, string Message);
