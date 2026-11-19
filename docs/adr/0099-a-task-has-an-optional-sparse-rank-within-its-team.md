---
status: accepted
date: 2026-11-19
---

# A Task has an optional, sparse Rank within its Team

The Tasks specification ruled out reordering cards by hand: "A rank field would make every reorder a file write, and
therefore a wake-up." Priority and due date order a backlog only coarsely, so a manager could not say "do this one
next", and an Agent choosing its next Task had nothing to go by beyond them.

## Decision

**A Task carries an optional `rank:` in its front matter: a whole number of 1 or more, where 1 is handled first. Rank
orders one Team's backlog. Ranks are sparse: gaps and ties are allowed and Huddle never renumbers, so a change of rank
writes exactly one Task file.**

| | Decision |
| --- | --- |
| Storage | `rank:` in the Task file, after `priority:`, written only when set. Its own `TaskField.Rank`. An invalid value (`0`, negative, not a whole number) rejects the file, as an invalid `priority:` does |
| Order | Ranked Tasks first, ascending; unranked after them. Ties fall back to the existing order: priority descending, due date ascending, id ascending. This is the default sort of every View and of `list_tasks` |
| Scope | A rank means something only within its Team. Across Teams, ranks compare as plain numbers and interleave |
| Who sets it | Anyone, as with priority: the Human in the Task window or by dragging a card within a Board column, an Agent with `create_task` or `update_task` (`rank: 0` clears it) |
| Dragging | A drop within one column sets the dragged card's rank only, to a value between its new neighbours; with no integer gap it ties with a neighbour, the one under which the fallback order keeps it in place. A drop into another column changes status only. Reordering is off when the View has explicit sort keys |
| Wakes | Not changed by this decision. A rank change is a field change and wakes as any other does ([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)). Making rank silent is left to the field-aware wake work |

## Consequences

- A manager can order its Team's backlog, and the Playbooks and tool descriptions tell Agents to take their open
  Tasks in rank order.
- Until rank changes are silent, each drag wakes the dragged Task's assignee. The owner accepted that cost for this
  version; sparse ranks keep it to one Task per drag rather than every renumbered one.
- Sparse ranks can collect ties after many drops into the same gap. Ties are harmless (the fallback order decides),
  and the Human can type a rank in the Task window to separate them.
