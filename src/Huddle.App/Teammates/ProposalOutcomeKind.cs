namespace Agency.Huddle.App.Teammates;

/// <summary>What <see cref="ProposalService.ApproveAsync"/> or <c>DeclineAsync</c> actually did (Spec §6.10's outcome template table).</summary>
public enum ProposalOutcomeKind
{
    /// <summary>Every Candidate became a Teammate.</summary>
    Created,

    /// <summary>Some Candidates became Teammates; at least one did not.</summary>
    PartlyCreated,

    /// <summary>No Candidate became a Teammate, but at least one was attempted.</summary>
    NoneCreated,

    /// <summary>Nothing was attempted: creating every Candidate would have exceeded a hire limit (<see cref="HireLimits"/>).</summary>
    OverLimit,

    /// <summary>The Human declined the Proposal; nothing was created.</summary>
    Declined,

    /// <summary>Another caller already took this Proposal - a second click, from any tab, finds nothing.</summary>
    Gone,
}
