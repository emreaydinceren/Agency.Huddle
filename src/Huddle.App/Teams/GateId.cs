namespace Agency.Huddle.App.Teams;

/// <summary>The seven hard gates a process document can declare in <c>_team.md</c> (PR-7, Spec §Process), and the two refusal ids that are never declared.</summary>
internal enum GateId
{
    /// <summary>A Task cannot move to Done until a verifying member has added an Evidence line.</summary>
    QaEvidenceBeforeDone,

    /// <summary>A release merge or push waits for an Evidence line with exit code 0 for the check command.</summary>
    ChecksBeforeMerge,

    /// <summary>A Task cannot move to In Progress until its parent spec Task is approved.</summary>
    SpecApprovedBeforeBuild,

    /// <summary>A bug Task cannot move to Needs You or Done until an Evidence line shows a failing test before the fix.</summary>
    FailingTestBeforeFix,

    /// <summary>A merge into main waits for an accepted Task of the Team, whoever runs it and however the Task is worded (#401).</summary>
    AcceptBeforeMerge,

    /// <summary>A Task cannot move to In Progress, Needs You or Done while a Task it is blocked by is still open (#538).</summary>
    BlockersDoneBeforeStart,

    /// <summary>A change cannot leave one assignee with two Tasks In Progress, in any Team; unassigned Tasks are never limited (ADR-0102).</summary>
    OneInProgressPerAssignee,

    /// <summary>A Human-gated Task cannot skip the Human's acceptance; never written in _team.md.</summary>
    HumanGate,

    /// <summary>A Task cannot go to Needs You while its Proof has an open item; never written in _team.md.</summary>
    ProofIncomplete,
}
