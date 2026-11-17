namespace Agency.Huddle.App.Helpers;

/// <summary>What the dispatcher records about one finished judgement, whatever the Helper's types.</summary>
/// <param name="Kind">How the judgement ended.</param>
/// <param name="Summary">For <see cref="HelperRunKind.Applied"/>, what the card shows as the last result.</param>
/// <param name="Error">For <see cref="HelperRunKind.Failed"/>, the words for the card's last error.</param>
/// <param name="CallRan">
/// Whether the adapter process was started for this judgement: <see langword="true"/> for an answer, an unreadable answer, a
/// timeout or a failed process; <see langword="false"/> when the budget refused, when a cached Verdict was applied, when the
/// Adapter was not configured or installed or did not offer the Model, and when the judge made the report itself from an
/// exception. A caller that counts failures of one input counts a <see cref="HelperRunKind.Failed"/> report only when this is true.
/// </param>
internal sealed record HelperRunReport(HelperRunKind Kind, string? Summary = null, string? Error = null, bool CallRan = true)
{
    /// <summary>The budget refused before any process started.</summary>
    public static HelperRunReport OverBudget { get; } = new(HelperRunKind.OverBudget, CallRan: false);
}
