namespace Agency.Huddle.App.Teams;

/// <summary>The four hard gates a process document can declare (PR-7, Spec §Process).</summary>
internal enum GateId
{
    /// <summary>A Task cannot move to Done until a verifying member has added an Evidence line.</summary>
    QaEvidenceBeforeDone,

    /// <summary>A release merge or push waits for an Evidence line with exit code 0 for the check command.</summary>
    ChecksBeforeMerge,

    /// <summary>A Task cannot move to In Progress until its parent spec Task is approved.</summary>
    SpecApprovedBeforeBuild,

    /// <summary>A bug Task cannot move to Review until an Evidence line shows a failing test before the fix.</summary>
    FailingTestBeforeFix,

    /// <summary>A Human-gated Task cannot skip the Human's acceptance; never written in _team.md.</summary>
    HumanGate,

    /// <summary>A Task cannot go to Needs You while its Proof has an open item; never written in _team.md.</summary>
    ProofIncomplete,
}
