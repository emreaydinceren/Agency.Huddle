using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Everything <see cref="ProofEvaluator"/> needs about a Task, gathered once so the evaluator stays pure.</summary>
/// <param name="Definition">The Team's Definition of Done items.</param>
/// <param name="Gates">The Team's enabled ADR-0039 gates.</param>
/// <param name="IsBug">Whether the Task is a bug.</param>
/// <param name="Evidence">The Task's Evidence lines.</param>
/// <param name="Coverage">Role coverage by name, comma-joined, for Evidence authors, Team members and document editors.</param>
/// <param name="Roster">The Team's members in roster order.</param>
/// <param name="Children">The Task's child Tasks.</param>
/// <param name="Documents">The attached Markdown documents with their history.</param>
internal sealed record ProofContext(
    IReadOnlyList<DoneItem> Definition,
    IReadOnlyList<GateId> Gates,
    bool IsBug,
    IReadOnlyList<EvidenceLine> Evidence,
    IReadOnlyDictionary<string, string> Coverage,
    IReadOnlyList<string> Roster,
    IReadOnlyList<TaskItem> Children,
    IReadOnlyList<ProofDocument> Documents);
