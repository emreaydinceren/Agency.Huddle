namespace Agency.Huddle.App.Teammates;

/// <summary>
/// A roster of new Teammates one Agent has asked the Human to create, held in memory by
/// <see cref="ProposalStore"/> (Spec §6.9). At most one is pending per Room. Public because
/// <c>ProposalCard.razor</c> renders it directly, the same reason <see cref="Candidate"/> is
/// public.
/// </summary>
/// <param name="Id">This Proposal's own id, a Guid in <c>"N"</c> format.</param>
/// <param name="RoomId">The Room this Proposal is pending in - the store's key.</param>
/// <param name="ProposerAgentId">
/// The stable user id of the Agent that proposed this - the identity <see cref="ProposalStore.TryPut"/>
/// compares against to tell "the same proposer again" from "a different Agent", since a Name can
/// be renamed out from under it.
/// </param>
/// <param name="ProposerName">The proposer's Name at the moment of proposing, for display and for the Mention the outcome Message sends.</param>
/// <param name="Candidates">Between one and four proposed Teammates.</param>
/// <param name="ProposedAt">When this Proposal was made, from the injected <see cref="TimeProvider"/> - never <see cref="DateTimeOffset.Now"/>.</param>
public sealed record Proposal(
    string Id,
    string RoomId,
    string ProposerAgentId,
    string ProposerName,
    IReadOnlyList<Candidate> Candidates,
    DateTimeOffset ProposedAt);

/// <summary>The outcome of <see cref="ProposalStore.TryPut"/> against Spec §8.4's replacement table.</summary>
internal enum ProposalPutResult
{
    /// <summary>No Proposal was pending in the Room; the new one is now stored.</summary>
    Stored,

    /// <summary>The same Agent's earlier Proposal in this Room was replaced by the new one.</summary>
    Replaced,

    /// <summary>Another Agent's Proposal is already pending in this Room; the new one was refused.</summary>
    Refused,
}

/// <summary>
/// The result of <see cref="ProposalStore.TryPut"/>: what happened, and - only meaningful for
/// <see cref="ProposalPutResult.Refused"/> - the Proposal already pending, so the caller can name
/// its proposer.
/// </summary>
/// <param name="Result">Which of Spec §8.4's three outcomes occurred.</param>
/// <param name="Existing">The Proposal already pending in the Room when <see cref="Result"/> is <see cref="ProposalPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record ProposalPut(ProposalPutResult Result, Proposal? Existing);

/// <summary>What <see cref="ProposalService.ApproveAsync"/> or <c>DeclineAsync</c> actually did (Spec §6.10's outcome template table).</summary>
public enum ProposalOutcomeKind
{
    /// <summary>Every Candidate became a Teammate.</summary>
    Created,

    /// <summary>Some Candidates became Teammates; at least one did not.</summary>
    PartlyCreated,

    /// <summary>No Candidate became a Teammate, but at least one was attempted.</summary>
    NoneCreated,

    /// <summary>Nothing was attempted: creating every Candidate would have exceeded <see cref="Acp.AcpOptions.MaxTeammates"/>.</summary>
    OverLimit,

    /// <summary>The Human declined the Proposal; nothing was created.</summary>
    Declined,

    /// <summary>Another caller already took this Proposal - a second click, from any tab, finds nothing.</summary>
    Gone,
}

/// <summary>
/// What approving or declining a Proposal did (Spec §6.10). Public because <c>ProposalCard.razor</c>
/// reads it directly to show the outcome, the same reason <see cref="Proposal"/> and
/// <see cref="Candidate"/> are public.
/// </summary>
/// <param name="Kind">Which of Spec §6.10's outcomes occurred.</param>
/// <param name="Created">The Names of every Candidate that became a Teammate, in Proposal order.</param>
/// <param name="Failed">Every Candidate that did not become a Teammate, with why - empty unless <paramref name="Kind"/> is <see cref="ProposalOutcomeKind.PartlyCreated"/> or <see cref="ProposalOutcomeKind.NoneCreated"/>.</param>
/// <param name="PostedText">
/// The Message text that was posted - or, for <see cref="ProposalOutcomeKind.Gone"/>, an empty
/// string, since nothing was posted. Still populated even when the post itself failed after
/// Teammates were already created (Spec §6.10's Constraints), so a caller can show what would have
/// been said.
/// </param>
public sealed record ProposalOutcome(
    ProposalOutcomeKind Kind,
    IReadOnlyList<string> Created,
    IReadOnlyList<CandidateFailure> Failed,
    string PostedText);

/// <summary>One Candidate that did not become a Teammate, and why.</summary>
/// <param name="Name">The failed Candidate's proposed Name.</param>
/// <param name="Reason">
/// Why it failed - a <see cref="CandidateChecker"/> problem string, or the message of the
/// <see cref="Services.ChatException"/> <see cref="Acp.PersonaStore.Add"/> raised.
/// </param>
public sealed record CandidateFailure(string Name, string Reason);
