namespace Agency.Huddle.App.DocHistory;

/// <summary>Bound from <c>Team:DocHistory</c> (DocHistory spec §6.9).</summary>
public sealed class DocHistoryOptions
{
    /// <summary>Whether <c>log_change</c> is offered, Human saves are recorded, and the byline, History and panel are shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Entries kept per document; older ones are dropped on the next append. Zero or less keeps every entry.</summary>
    public int MaxEntries { get; set; } = 500;

    /// <summary>The longest summary <c>log_change</c> accepts, in characters.</summary>
    public int MaxSummaryLength { get; set; } = 200;

    /// <summary>How close consecutive Human saves must be to share one row in the panel and the History.</summary>
    public TimeSpan HumanSaveGroupWindow { get; set; } = TimeSpan.FromMinutes(5);
}
