---
status: accepted
date: 2026-10-19
---

# A Human-gated Task waits in Needs You for the Human's acceptance

> Amended by [ADR-0087](0087-a-needs-you-level-per-scope-lets-a-teams-local-work-run-without-an-accept.md) (2026-11-15):
> the Human may also lower what waits in advance, by scope (the Needs You level), and only the Human may. A Task with an
> Action or `human_gate: true` is gated as below under every level.

Agents do the work and the Human makes the calls. A Task that names an **Action** (something
that leaves the Team or cannot easily be undone: sending, paying, signing, publishing, merging,
deleting), or that the Human marked **Requires your acceptance**, is **Human-gated**. It cannot
reach Done until its **Proof** is complete and the Human has **accepted** it. The full design is
[Huddle.HumanGate-Specifications.md](../Huddle.HumanGate-Specifications.md).

## The problem

AI Agents are fast and tireless, but they are not accountable. "The AI sent it" is not an
answer a customer, an auditor or a lawyer accepts. A wrong email, payment, signature or merge is
out before anyone looks, and a polished draft can hide a wrong number or a missing source.

[ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)
already lets a Team refuse a move to Done until Evidence exists. It does not make the Human the
one who decides. An Agent whose Evidence is in can mark the Task Done, and the Human can wave
through work nobody checked with "Move anyway".

## The decision

**A Task is Human-gated when it has `human_gate: true` or a non-empty `action:` in its
frontmatter.** An Action turns the gate on and locks it on. Anyone may turn the gate on or set an
Action. Only the Human may turn the gate off, and only on a Task with no Action. Only the Human
may clear an Action. A Team's process cannot turn the gate off: it can add Proof, never remove
the Human's decision.

**Needs You is a ninth Task status, between Review and Done, used only by Human-gated Tasks.**
Any member may move a Human-gated Task into Needs You, and `TaskService` refuses the move until
the Task's Proof is complete ([ADR-0062](0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md)).
A Task in Needs You, not yet accepted, is listed on the Needs You card and page. A Task that is
not Human-gated cannot enter Needs You.

**The Human decides with one of three actions, and only the Human can take them.**

| Action | Available when | Effect |
| --- | --- | --- |
| **Accept** | Needs You, Proof complete | Logs `accepted <hash>`. A Task with no Action moves to Done in the same write. A Task with an Action stays in Needs You, marked accepted, and its assignee is woken to carry the Action out |
| **Request changes** | Review or Needs You, Proof complete or not | A note is required. Logs `changes requested: <note>`, moves the Task to In Progress and wakes the assignee with the note |
| **Decline** | Review or Needs You, from an overflow menu | A reason is required. Logs `declined: <reason>`, sets Rejected, closes the Task and wakes the assignee with the reason |

**Only an acceptance opens Done.** A move of a Human-gated Task into Done is refused unless the
Task is in Needs You, its Proof is still complete, and the latest `accepted` entry since it last
entered Needs You carries the hash of the Task's current title, description and Action. After an
acceptance, the assignee (or the Human) moves the Task to Done once the Action is carried out.
An edit to the title, description or Action voids the acceptance, so the Human never accepts one
text and finds another carried out.

**No override on a Human-gated Task.** While its Proof is incomplete, `TaskService` ignores
`humanOverridesGates` for every refusal on that Task, the ADR-0039 gates included. The Task panel
shows "Done locked · Proof 3/4" and an "Ask Ruth for the citation check" button that posts a
Mention, never "Move anyway". Ordinary Tasks keep ADR-0039's logged override.

**An outside edit cannot dodge the gate.** An edit on disk that clears `human_gate` or `action`,
or moves a Human-gated Task into Needs You or Done past a refusal, is written back the way
ADR-0039 writes back a refused status, and logged as `outside edit refused by Human gate: <message>`.

## Rejected

| Alternative | Why not |
| --- | --- |
| A fifth gate id in `Process.md` | A Team could then switch the Human's decision off. The decision must hold whatever the process says |
| Gate every Task at Done | Internal notes, drafts and research would wait on the Human for nothing, and the Human would learn to click Accept without reading |
| Accept moves every Task to Done | Done would be recorded before an outward Action happened. Done means the thing happened |
| Huddle carries out the Action on Accept | Each kind of Action (mail, payment, merge, post) would need its own integration. The Agent already has the tools |
| Show Needs You as a variant of Review, keeping eight states | The board, Views and the Change log could not tell work under review from work waiting on the Human |
| Keep "Move anyway" for the Human on gated Tasks | It is the button that waves through work nobody checked. The way forward is to ask for the missing Proof |
| Record the decision in the Agent's Memory | Memory is what an Agent deliberately keeps for itself. The decline reason reaches the Agent in its wake Message and the Retro through the Change log. The Agent decides what to remember |

## Consequences

- **A named person approved every Human-gated Task that reached Done,** with a Change log line
  saying who and when, against which text and Proof.
- **The Task file gains two keys and the Task status list gains one value.** Every reader of
  `TaskState` (the board columns, Views, filters, the Agent tools) learns Needs You. A saved View
  whose state filter includes Review shows Needs You too, worked out when the View is read.
- **Needs You lists Tasks as well as Rooms.** Task entries are read from the Task files, so
  unlike ADR-0050's Room entries they survive a restart, and they have no ✕: only a decision or a
  status change takes a Task off the list. This amends
  [ADR-0050](0050-needs-you-is-derived-from-room-events-and-held-in-memory.md).
- **The gate guards the Task, not the outside world.** Huddle refuses the status change. It does
  not stop an Agent's tool from sending a mail or pushing a branch before acceptance. The Agent
  is told the rule, and a refused Done says why. Tying `git merge` and `git push` to an accepted
  Task is an open decision in the specification.
- **This amends ADR-0039** for Human-gated Tasks only: the Human's override does not apply to them.
