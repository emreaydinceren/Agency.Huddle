namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>What a settled <c>AGENTS.md</c> revision tells a Room (AgentsMdChanges Spec §7.1). A closed hierarchy: only the cases below exist.</summary>
internal abstract record AgentsMdNotice
{
    private protected AgentsMdNotice()
    {
    }

    /// <summary>A Helper's summary: each change carries a verified quote.</summary>
    /// <param name="Changes">The changed rules.</param>
    internal sealed record Summary(IReadOnlyList<AgentsMdRuleChange> Changes) : AgentsMdNotice;

    /// <summary>A plain comparison: <c>-</c> and <c>+</c> lines.</summary>
    /// <param name="Lines">The lines shown.</param>
    /// <param name="NotShown">How many lines were left out.</param>
    internal sealed record Comparison(IReadOnlyList<string> Lines, int NotShown) : AgentsMdNotice;

    /// <summary>An edit not told to a Room: its hash moves past silently.</summary>
    /// <param name="Reason">Why it was skipped.</param>
    internal sealed record Skipped(string Reason) : AgentsMdNotice;

    /// <summary>An edit whose body was over <c>MaxLedgerChars</c>: nothing is known of what changed.</summary>
    internal sealed record Unknown : AgentsMdNotice;
}
