---
status: proposed
date: 2026-11-19
---

# One Task In Progress per Project is a Team setting a Project can override

ADR-0102 limits each person to one Task In Progress. It says nothing about a Project: two people can each start a Task
in the same Project at once, and a Team that wants its Projects worked one Task at a time, as a pipeline, has no way to
say so. Other Teams want the opposite, with several Tasks in one Project moving in parallel.

## Decision

**A Team has a setting, off by default, that allows only one Task In Progress per Project. Each Project can override it
with `on`, `off` or `inherit`. A Task with no Project follows the Team setting and counts as one group of its own.**

| | Decision |
| --- | --- |
| Team setting | `oneInProgressPerProject` in `_team.md`: `on` or `off`. Absent is `off`, so no existing Team changes |
| Project setting | The same key in the Project's `_project.md`: `on`, `off` or absent for `inherit` |
| Effective rule | The Project's setting when it is `on` or `off`, otherwise the Team's. A Task with no Project uses the Team's |
| Counts | Status In Progress only, outside `_closed/`, in the same Team and the same Project. Tasks with no Project are one group per Team. Needs You does not count, as in ADR-0102 |
| Refuses | A move into In Progress, a move of an In Progress Task into another Project, or a create as In Progress that would make two in one group |
| Text | Names the Task already In Progress, with its id and title, and says whether the Project or the Team set the rule |
| Override | The Human can override one move, as with every refusal. The setting itself is changed on the Process tab of the Team page and of the Project page, not as a gate switch |
| Separate from | The `gates:` list and the per-person gate. The two rules combine: either can refuse |

## Consequences

- Both settings off is today's behaviour. Team `on` gives every Project its own single lane without touching each
  Project, and one Project can opt out with `off`. Team `off` with one Project `on` limits only that Project.
- `next_task` and the Task order panel follow the effective rule: with it on, only the first startable Task of a
  group counts toward "can start now" and carries the Next tag, so neither promises a Task the rule then refuses.
- `_project.md` gains its first setting beyond the avatar. Its parser already keeps unknown frontmatter lines, so an older
  build leaves the new key alone when it rewrites the file.
- The Process `wip` number is stored but enforced nowhere. This ADR does not use it; a numeric limit per Project is a
  later question, and `on`, `off` and `inherit` would stay valid if it is added.
