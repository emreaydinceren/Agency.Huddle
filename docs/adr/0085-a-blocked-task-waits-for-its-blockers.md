---
status: accepted
date: 2026-11-14
---

# A blocked Task waits for its blockers

In the 2026-11-14 run, a Developer moved DEVT-0002 from Backlog to Review while DEVT-0001, the Task it was blocked by,
was still in Needs You. `blocked_by` was only a note: the board showed the Task as blocked, the wake fired when the
blocker reached Done, and nothing refused a move. Issue #538 closes that.

## Decision

A new process gate, `blockers-done-before-start` (display name "Blockers done before start"), is part of the four
development processes. It is the sixth declarable gate. `ProcessGateService.CheckTaskChange` checks it first, before
the other Task gates ([ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)
has the mechanism).

While any Task in this Task's **stored** `blocked_by` exists and is not finished, a move into **In Progress**,
**Review**, **Needs You** or **Done** is refused. A move to Backlog, To Do or a won't-do status is allowed. The refusal
names the gate and every open blocker with its status:

> Gate blockers-done-before-start: this Task is blocked by DEVT-0001 (Needs You). A blocked Task waits until every
> blocker is Done, Cancelled, Duplicate or Rejected, and for the manager's go-ahead.

### The release rule

A blocker is finished when `TaskStates.IsTerminal` says so: Done, Cancelled, Duplicate or Rejected. There is no Closed
status. A blocker in **Needs You** (awaiting the Human's Accept) still blocks. A blocker that no longer exists blocks
nothing, the same rule as the board's Blocked filter (`TaskQuery.MatchesBlocked`).

The wake in `TaskTriggerService` (RQ-6) now fires when a blocker reaches any of the four finished statuses, not only
Done, and counts a blocker as finished by the same rule. So the gate, the Blocked filter and the wake agree on what
"blocked" means.

### The moves it refuses, and where

The gate runs on all three callers of `TaskService.CheckGates`:

- **Update:** any status change.
- **Create:** a Task created into In Progress, Review, Needs You or Done.
- **Outside edit** (a file changed on disk): the same list of statuses, with Needs You newly added. A refused outside
  edit is reverted and logged as `outside edit refused by gate: <message>`.

For a Task that is not gated by the Human, a move into Needs You is refused by the Human gate first, which runs before
the process gates; for a Human-gated Task with its Proof complete, this gate is what refuses it.

### Judged against the stored blockers

The gate reads the Task's `blocked_by` **as stored**, not as the patch would leave it. An `update_task` that clears
`blocked_by` and starts the Task in the same call is therefore refused.

In a Team that has the gate, only the Team's manager or the Human may remove a blocker. Any other Agent gets
`Only the Team's manager or the Human can remove a blocker from blocked_by.` In a Team without the gate, nothing changes.

### The Human can override

The Human can override the gate with the board or Task panel override ("Move anyway"). The Change log notes
`gate overridden by Human: blockers-done-before-start`. An Agent cannot override it.

### Installed Teams

The gate is new, so a Team created before it has none. `BlockersDoneBeforeStartMigration` (a hosted service) runs once at
the first start after the upgrade, recorded by the marker file `blockers-done-before-start.migrated` in the data folder.
It adds `blockers-done-before-start` to the `gates` of every Team whose Team type is `development`. Teams of other types
are not touched. The marker means a Human who turns the gate off later keeps it off. It shares its logic with
`AcceptBeforeMergeMigration` in `DevelopmentGateMigration`; see [ADR-0078](0078-a-merge-into-main-waits-for-the-humans-accept-by-team.md).

New Teams get the gate from the process template. The four templates (`lightweight-flow`, `maintenance`, `spec-driven`,
`sprints`) carry it in the `gates:` line, in a `## Gates` bullet, and in a sentence in the stage before work starts:
"A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it
before then." The Developer pool persona says a blocked Task waits too. A hired Agent keeps its own copy of its Persona,
and an installed Team's `AGENTS.md` is not rewritten, so for installed Teams the refusal text is what teaches the rule.

Other Team types do not get the gate now.

### How a Human turns it off

In the Team's **Process** tab, switch the gate off (the same control as any other gate). There is no control to switch a
gate on again; edit the `gates` line of the Team's `_team.md`.

### What it does not stop

- **Work before a status move.** A status gate stops the Task moving, not early work. In the 2026-11-14 run the Developer
  committed before any status move. The prose in the process and in `developer.md` is what asks Agents not to start.
- **An outside edit that removes a blocker.** Such an edit is not restricted: nobody is told who may remove a blocker
  there. It is still judged against the blockers before the edit, so it cannot start the Task in the same write.

## Decided by the owner, 2026-11-14

1. A blocker is released by `IsTerminal`; the wake is aligned to it.
2. A blocker in Needs You still blocks.
3. In Progress, Review, Needs You and Done are refused, on all three callers.
4. The gate is judged against the stored `blocked_by`; only the manager and the Human remove a blocker; the Human can
   override.
5. On in the four development templates plus the migration; other Team types not now.
6. The prompt side goes in the process (`AGENTS.md`) and `developer.md`.
7. The stale gate counts in the docs are fixed.

## Rejected

| Alternative | Why not |
| --- | --- |
| Releasing a blocker only at Done | A Cancelled, Duplicate or Rejected blocker would block for ever, and the Blocked filter already treats them as finished |
| Letting a blocker in Needs You release | The Human has not accepted its work yet; the dependant would build on something that may be sent back |
| Judging the patched `blocked_by` | One `update_task` call could clear the blocker and start the Task |
| Letting any Agent remove a blocker | It would be the easy way around the gate |

## Consequences

- A blocked Task cannot be started, reviewed or finished by an Agent until its blockers are finished. The Human can still
  move it on purpose, on record.
- The Blocked filter, the gate and the wake use one rule for "finished".
- A Team whose Human removed the gate from the Process tab is not overruled by the migration.
- Limits that remain are in [known limits](../engineering/known-limits.md): work before a status move, and a blocker
  removed by an outside edit.

See [ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md) for the
gate mechanism and [ADR-0078](0078-a-merge-into-main-waits-for-the-humans-accept-by-team.md) for the migration it shares.
