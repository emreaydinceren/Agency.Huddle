namespace Agency.Huddle.App.Tasks;

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
