namespace Agency.Huddle.App.Teams;

/// <summary>The closed set of rules a Definition of Done item can state (ADR-0062).</summary>
internal abstract record DoneRule
{
    /// <summary>Met by an Evidence line with a command from a member whose role covers the step.</summary>
    /// <param name="Step">The process step.</param>
    internal sealed record EvidenceFrom(string Step) : DoneRule;

    /// <summary>Met when the latest Evidence line for the command has exit code 0.</summary>
    /// <param name="Command">The command, with its inner spaces untouched.</param>
    internal sealed record CommandExits(string Command) : DoneRule;

    /// <summary>Met when a child Task carrying the tag is Done.</summary>
    /// <param name="Tag">The tag.</param>
    internal sealed record TaskTagged(string Tag) : DoneRule;

    /// <summary>Met when an attached document has a later review by a member covering the step.</summary>
    /// <param name="Step">The process step.</param>
    internal sealed record DocumentReviewedBy(string Step) : DoneRule;

    /// <summary>A line Huddle cannot read; it is never met.</summary>
    /// <param name="Text">The bullet as written, after its dash.</param>
    internal sealed record Unreadable(string Text) : DoneRule;
}
