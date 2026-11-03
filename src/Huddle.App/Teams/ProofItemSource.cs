namespace Agency.Huddle.App.Teams;

/// <summary>Where a Proof item came from.</summary>
public enum ProofItemSource
{
    /// <summary>A bullet in the <c>## Definition of Done</c> section of the Team's <c>AGENTS.md</c>.</summary>
    Process,

    /// <summary>An ADR-0039 gate that joins the checklist on its own.</summary>
    Gate,
}
