namespace Agency.Huddle.App.Tasks;

/// <summary>What a Human's answer to a Sprint proposal did.</summary>
internal enum SprintProposalOutcomeKind
{
    /// <summary>The Sprint exists and the proposed Tasks that could join it did.</summary>
    Approved,

    /// <summary>The proposal was dropped and its reason posted; no Sprint was created.</summary>
    Declined,

    /// <summary>No such proposal is pending in that room: someone else answered it first, or it was never there.</summary>
    Gone,

    /// <summary>The Sprint could not be created; the proposal is still pending.</summary>
    Failed,
}
