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

/// <summary>A proposed Task that did not join the Sprint.</summary>
/// <param name="Id">The Task's id, as the manager wrote it.</param>
/// <param name="Reason">Why it was skipped.</param>
internal sealed record SkippedTask(string Id, string Reason);

/// <summary>The result of approving or declining a Sprint proposal.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="SprintExisted"><see langword="true"/> when the approved name was already a Sprint in <c>sprints.md</c>.</param>
/// <param name="Applied">The ids of the Tasks that now carry the Sprint.</param>
/// <param name="Skipped">The Tasks that were left out, each with its reason.</param>
/// <param name="PostedText">The text posted to the manager; for <see cref="SprintProposalOutcomeKind.Failed"/> the problem found, and empty for <see cref="SprintProposalOutcomeKind.Gone"/>.</param>
internal sealed record SprintProposalOutcome(
    SprintProposalOutcomeKind Kind,
    bool SprintExisted,
    IReadOnlyList<string> Applied,
    IReadOnlyList<SkippedTask> Skipped,
    string PostedText);
