namespace Agency.Huddle.App.Helpers;

/// <summary>What one Helper call came to.</summary>
/// <typeparam name="TVerdict">The Helper's Verdict type.</typeparam>
internal abstract record HelperOutcome<TVerdict>
    where TVerdict : class
{
    /// <summary>The text held a JSON object that parsed and passed <c>Validate</c>.</summary>
    /// <param name="Verdict">The Verdict.</param>
    public sealed record Ok(TVerdict Verdict) : HelperOutcome<TVerdict>;

    /// <summary>The answer was not the JSON the prompt asks for.</summary>
    /// <param name="Text">The answer, cut to 2,000 characters for the log.</param>
    /// <param name="Problem">What was wrong with it.</param>
    public sealed record Unparseable(string Text, string Problem) : HelperOutcome<TVerdict>;

    /// <summary>The call could not run or did not finish.</summary>
    /// <param name="Reason">Why, in words for the card.</param>
    public sealed record Failed(string Reason) : HelperOutcome<TVerdict>;

    /// <summary>The budget refused before any process started.</summary>
    public sealed record OverBudget : HelperOutcome<TVerdict>;
}
