---
status: accepted
date: 2026-11-18
---

# A review is a Task owned by the reviewer, not a state

Until now a builder moved a Task to Review and kept it. Huddle then woke someone onto it, and the reviewer recorded an
Evidence line or a document history entry on a Task that belonged to someone else. The reviewer owned nothing, nothing
recorded that a review was asked for, and "a Task has one owner" did not hold. Adding review Tasks beside the Review state
would have left every Agent and Human choosing between two models. Issue #669 removes the state.

## Decision

**There is no Review state.** The states are Backlog, To Do, In Progress, Needs You, Done, Cancelled, Duplicate and Rejected.

**A review, a spec review and a QA check are each a Task owned by whoever does it.** The work Task's owner creates it as a
child of the work Task, tagged `review` (or `verify` for QA), assigned to the reviewer, then adds it to the work Task's
`blocked_by`. The manager may create it instead. The work Task stays In Progress, and the `blockers-done-before-start` gate
refuses Needs You and Done while the review is open.

**A review Task is Done only when the reviewer approves.** When changes are needed, the findings go on the review Task as a
comment and it stays open. It is never reopened, because it never closed, so `task tagged review` cannot count a failed
review as passed.

**The Proof reads the Tasks.** `Review: task tagged review` and `QA evidence: task tagged verify` use the existing
`TaskTagged` rule (met when a child with the tag is Done). A child reaching Done re-evaluates its parent, which moves from
In Progress to Needs You when it is Human-gated and its Proof is complete.

**The wake follows the assignment.** Creating the review Task assigns it, so the ordinary assignee wake reaches the
reviewer. The challenger wake rule is removed.

**Old data reads as In Progress.** A saved `status: Review` loads as In Progress, with a Timeline line saying why.

**Existing Teams are offered the rewrite, never given it.** Their `AGENTS.md` is never overwritten. A start-up step matches
the exact Review-stage text Huddle shipped and stores a retro proposal, which the Human accepts or rejects as any other.

The full design, with the decisions D-1 to D-21, is in [Huddle.ReviewTasks-Specifications.md](../Huddle.ReviewTasks-Specifications.md).

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep the Review state and add review Tasks | Two models for one act; Agents would do both or neither |
| Name the tags per Team type | The wake sentence and the Proof read them, so they are constants |
| Reopen a review Task when changes are needed | `task tagged review` would count a failed review as passed |
| A new proposal card for the `AGENTS.md` rewrite | The retro card already shows a diff and applies only on the Human's Accept |
| Rewrite an installed `AGENTS.md` automatically | It is the Human's file (ADR-0070 and the provisioner's rule) |

## Consequences

Every process, persona and prompt that said "move it to Review" now says "create a review Task". `ChallengerWakeRule` and
the Board's Review column are gone. A Team that has not accepted the rewrite keeps reading the old stage text in its
`AGENTS.md`; the tools tell its Agents what to do instead when they name Review. Amends ADR-0039, ADR-0061, ADR-0062 and
ADR-0085.
