namespace Agency.Huddle.App.Teams;

/// <summary>The closed set of rules a Definition of Done item can state (ADR-0062).</summary>
internal abstract record DoneRule
{
    /// <summary>Tells whether a Definition of Done names a command: it has a <c>command ... exits 0</c> item (#550).</summary>
    /// <param name="definition">The Team's Definition of Done items; empty for a Team with no Process.</param>
    /// <returns><see langword="true"/> when a note without a command cannot stand for a step's Evidence.</returns>
    internal static bool NamesCommand(IEnumerable<DoneItem> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Any(static item => item.Rule is CommandExits);
    }

    /// <summary>
    /// Met by an Evidence line from a member whose role covers the step: one with a command always, and a note
    /// without one when the Definition of Done names no command (#550).
    /// </summary>
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
