---
status: proposed
date: 2026-11-19
---

# TaskOrder derives which Task is next, and writes nothing

Rank (ADR-0099) lets the Human or the manager order a Team's backlog. Each Agent still works out its next Task from
prompt text and a `list_tasks` result. A blocked Task looks like any other there, so an Agent picks it and the
`blockers-done-before-start` gate refuses the start. A rank 1 Task can also wait behind a blocker ranked 40 that
nobody treats as urgent. In 25 of the 29 built-in processes `blocked_by` is only a note, because only the development
processes turn the gate on (ADR-0085).

## Decision

**One pure, deterministic component, `TaskOrder`, orders a set of Tasks from rank, priority and `blocked_by`, and names
the first one that can start. Each entry carries the reason for its place. It is computed when asked and never writes
a field. An open blocker is listed just ahead of the best-placed Task it blocks. Every built-in process turns the
blocker gate on.**

| | Decision |
| --- | --- |
| Order | The Views' order (`TaskQuery.DefaultOrder`: rank, then priority, due date and id), reused rather than copied. Rank is the order; priority is a label that breaks ties |
| Blocked | The gate's own test (`TaskBlockers.Open`, moved out of `TaskService`), so the gate and TaskOrder never disagree |
| Groups | Started (In Progress, Needs You), then Next (To Do, plus any Backlog Task that blocks one), then Backlog. Only a To Do Task with no open blocker is startable |
| Placement | A depth-first walk seeded in rank order: a blocker is listed just ahead of what it blocks, chains are followed, and a blocker of several Tasks takes the best place. Only the listing moves |
| Cycles | Reported on every Task in the cycle, which is not startable. The walk never loops and never chooses a Task to break the cycle |
| Writes | None. No rank is renumbered, no blocker is raised in rank or priority, no `blocked_by` is edited, and nobody is woken |
| Surfaces | A `next_task` tool for the caller's own Tasks, and a read-only Task order panel for a Team. The `blockers-done-before-start` refusal from `update_task` names the caller's next startable Task |
| Gate | `blockers-done-before-start` is in the `gates:` of every built-in process. Installed Teams are not migrated. The Human can still switch it off per Team, or override it for one move |
| Jump-ahead | Not refused. An Agent may start any startable Task; TaskOrder only says which comes first |

## Consequences

- An Agent asks one tool for its next Task instead of ordering a list itself. The Playbook and `list_tasks` texts point
  at `next_task`.
- The manager sees in the panel which blocker holds up a top-ranked Task, and fixes it by ranking the blocker or moving
  it to To Do. Huddle does neither on its own.
- A new household or second-brain Team now refuses a blocked start. Teams installed before this change keep their old
  gates.
- Ranks compare across Teams as plain numbers in an Agent's own list (ADR-0099). An Agent with Tasks in two Teams sees
  them interleaved, and it can filter by `team`.
- One In Progress Task per person is not decided by this ADR. If it is ever built, it is a gate, and TaskOrder does not
  change.

Amends ADR-0085: the gate is a default of every built-in process, not only the development ones.
