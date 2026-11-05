namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>Everything the Helper sees for one edit.</summary>
/// <param name="RelativePath">The file, relative to the Teams root, with forward slashes.</param>
/// <param name="LevelLabel">Who it applies to: everyone, or the Team or Project's name.</param>
/// <param name="FromHash">The head the edit starts from.</param>
/// <param name="ToHash">The text being judged.</param>
/// <param name="OldText">The head's body, for checking a removed rule's quote.</param>
/// <param name="NewText">The current body.</param>
/// <param name="Comparison">The changed lines, with context.</param>
internal sealed record AgentsMdChangeInput(string RelativePath, string LevelLabel, string FromHash, string ToHash, string OldText, string NewText, AgentsMdComparison Comparison);
