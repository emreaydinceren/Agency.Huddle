---
status: accepted
date: 2026-11-19
---

# A person has at most one Task In Progress

`next_task` (ADR-0101) tells an Agent to finish its started Task before it takes another, but nothing enforced it.
An Agent could move several Tasks to In Progress and work none of them to the end, and the Board then said little
about what anyone was actually doing.

## Decision

**A new gate, `one-in-progress-per-assignee`, refuses any change that would give one assignee two Tasks In Progress.
It counts In Progress Tasks in every Team, applies to every Agent and to the Human, and is on in all 29 built-in
processes.**

| | Decision |
| --- | --- |
| Counts | Status In Progress only, outside `_closed/`, across Teams. Needs You waits on the Human and does not count |
| Refuses | A move into In Progress, a reassignment of an In Progress Task, or a create as In Progress that would make two for one assignee. Unassigned Tasks are never limited |
| Text | Names the Task already In Progress, with its id and title |
| Override | The Human can override one move, or switch the gate off per Team, as with every gate |

## Consequences

- An Agent finishes, hands to Needs You, or moves back to To Do before it starts something else. `next_task` and the
  gate now say the same thing.
- A review Task is a Task like any other, so a reviewer with its own work In Progress has to park one to start the
  review. This is intended: one thing at a time.
- The Human is limited too. Their own override is one click away when they mean to work two things at once.
