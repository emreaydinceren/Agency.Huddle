namespace Agency.Huddle.Contracts;

/// <summary>What an edit changes, as the Adapter described it. Display only: never applied, never diffed.</summary>
/// <param name="OldText">The text replaced, or <see langword="null"/> for a new file.</param>
/// <param name="NewText">The replacement text, or the whole new file, clipped to <see cref="ToolActivityLimits.MaxEditSideLength"/>.</param>
/// <param name="Truncated">Whether either side was clipped to fit.</param>
/// <param name="OmittedChanges">How many further changes the same call carried.</param>
public sealed record EditChange(string? OldText, string? NewText, bool Truncated = false, int OmittedChanges = 0);