namespace Agency.Huddle.App.Helpers;

/// <summary>What a Helper did with a Verdict; the dispatcher records it on the card.</summary>
internal abstract record HelperApplied
{
    /// <summary>The Helper changed something.</summary>
    /// <param name="Summary">What it did, such as <c>Turned the gate on for RSH-0208</c>.</param>
    public sealed record Changed(string Summary) : HelperApplied;

    /// <summary>The Verdict asked for nothing.</summary>
    public sealed record NoChange : HelperApplied;

    /// <summary>The Verdict was stale or no longer applies, and was dropped.</summary>
    /// <param name="Why">Why, for the log.</param>
    public sealed record Dropped(string Why) : HelperApplied;

    /// <summary>The domain service refused the change; counted as a failure.</summary>
    /// <param name="Message">The refusal.</param>
    public sealed record Refused(string Message) : HelperApplied;
}
