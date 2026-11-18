using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>A proposed Task status change, with everything a gate needs to judge it (PR-7).</summary>
/// <param name="Team">The Team the Task belongs to.</param>
/// <param name="TaskId">The Task being changed.</param>
/// <param name="FromStatus">The wire name of the current status.</param>
/// <param name="ToStatus">The wire name of the proposed status.</param>
/// <param name="IsBug">Whether the Task is a bug.</param>
/// <param name="ParentSpecApproved">Whether the parent spec Task is approved; <see langword="true"/> when the Task has no parent spec.</param>
/// <param name="Evidence">The Task's Evidence lines.</param>
/// <param name="AuthorRoleCovers">Each Evidence author's role coverage, comma-joined.</param>
/// <param name="Actor">Who makes the change; the Human bypasses nothing here, the override lives in the caller.</param>
internal sealed record GateContext(
    string Team,
    TaskId TaskId,
    string FromStatus,
    string ToStatus,
    bool IsBug,
    bool ParentSpecApproved,
    IReadOnlyList<EvidenceLine> Evidence,
    IReadOnlyDictionary<string, string> AuthorRoleCovers,
    string? Actor)
{
    /// <summary>The Tasks in the stored <c>blocked_by</c> that are not finished (Done, Cancelled, Duplicate or Rejected); empty when none.</summary>
    public IReadOnlyList<OpenBlocker> OpenBlockers { get; init; } = [];

    /// <summary>Whether a child of the Task tagged <c>verify</c> is Done (#669, D-16): a QA check is a Task, and it meets <c>qa-evidence-before-done</c>.</summary>
    public bool HasDoneVerifyChild { get; init; }

    /// <summary>Whether the Task is itself a review or verify Task (#669, D-22): it is the check, so <c>qa-evidence-before-done</c> does not apply to it.</summary>
    public bool IsCheckTask { get; init; }
}
