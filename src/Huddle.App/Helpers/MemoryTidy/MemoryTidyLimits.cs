namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>The caps the Verdict must keep.</summary>
/// <param name="MaxActions">The most actions the Verdict may hold.</param>
/// <param name="MaxDrops">The most files one Verdict may drop or mostly empty: a cap for each folder, not for the whole pass.</param>
/// <param name="MaxAnswerChars">The most characters of <c>text</c> the Verdict may hold in all.</param>
/// <param name="DescriptionsLocked">Whether descriptions may not change: Huddle-wide Memory in the first build.</param>
internal sealed record MemoryTidyLimits(int MaxActions, int MaxDrops, int MaxAnswerChars, bool DescriptionsLocked);
