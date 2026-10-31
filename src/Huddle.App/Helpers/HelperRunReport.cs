namespace Agency.Huddle.App.Helpers;

/// <summary>What the dispatcher records about one finished judgement, whatever the Helper's types.</summary>
/// <param name="Kind">How the judgement ended.</param>
/// <param name="Summary">For <see cref="HelperRunKind.Applied"/>, what the card shows as the last result.</param>
/// <param name="Error">For <see cref="HelperRunKind.Failed"/>, the words for the card's last error.</param>
internal sealed record HelperRunReport(HelperRunKind Kind, string? Summary = null, string? Error = null)
{
    /// <summary>The budget refused before any process started.</summary>
    public static HelperRunReport OverBudget { get; } = new(HelperRunKind.OverBudget);
}
