---
status: proposed
date: 2026-11-19
---

# A Task change wakes only for what its readers act on

A change to a **Task** wakes its assignee and its Team's manager only when it changes something they would act on:
the assignee, the title, the description, the blockers, the Action, the Team or Project it is filed in, or a status
move that starts, stops, finishes or reopens work. A change that only orders, labels or schedules the work (rank,
priority, tags, dates, sprint, parent), switches the Human gate, files away a Task nobody started or relabels a finished
one is saved and logged without waking anyone. A comment wakes only the Agents it Mentions. This amends
[ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md), whose rule was "any change wakes the assignee", and the Wakes row of
[ADR-0099](0099-a-task-has-an-optional-sparse-rank-within-its-team.md), which left rank waking until this decision. The full design
is [Huddle.SmartWakes-Specifications.md](../Huddle.SmartWakes-Specifications.md).

## The problem

A wake starts a Turn, and a Turn costs tokens. Agent-made wakes also spend the Task's wake budget. Waking on any change
was the right default when a Task had few fields, but three cases now make it costly:

- **Rank** ([ADR-0099](0099-a-task-has-an-optional-sparse-rank-within-its-team.md), merged) is set by dragging a card
  within a column. Every reorder wakes the dragged Task's assignee and manager, and a manager ordering its backlog
  spends the wake budget of each Task it moves. ADR-0099 accepted that cost only until field-aware wakes, and
  [ADR-0101](0101-taskorder-derives-which-task-is-next-and-writes-nothing.md) lets an Agent ask `next_task` for its order
  instead of being told by a wake.
- **Approving a Sprint** sets the sprint on every Task in it: one click, two wakes per Task.
- **Comments** are the most frequent change, and each one wakes the manager, even a member's routine end-of-pass note.

## The decision

**Fields that wake:** assignee (the new one only), title, description, action, blocked by, location when moved inside
Huddle, and any front matter key Huddle does not parse. **Fields that do not:** rank, priority, tags, start and due dates,
sprint, parent, origin, duplicate of, the Human gate, and a location change made outside Huddle (a folder rename in
Explorer). A change that touches both kinds wakes, and its Message lists everything.

**The Human gate is silent** because code enforces it at the Task's next move: Huddle stops a gated Task in Needs You, and
the gates refuse Done and the merge without an Accept, saying why. No Agent has to do anything when it is switched.

**A status move wakes when work starts, stops, finishes or comes back.** The statuses fall into four phases: Planning
(Backlog, To Do), Active (In Progress, Needs You), Done, and Won't do (Cancelled, Duplicate, Rejected). Moving out of
Active, into Done from Active, or out of a finished status wakes the assignee and the manager. Starting a Task, or
moving it between Backlog and To Do, wakes only the assignee. Filing away a Task nobody started (Backlog → Cancelled,
Backlog → Done) and relabelling a finished one (Cancelled → Duplicate) wake no one. Unblocking is unchanged: a blocker
that finishes still wakes the Tasks it blocked. A Request's requesting manager is woken when it is done, refused or reopened.

**Changes with no field** say what they are: creating, Evidence, a Human decision and a release wake; closing or
reopening wakes only when the Task is In Progress or Needs You; Huddle's own Sprint step and its move to Needs You do not.

**A comment wakes only the Agents it Mentions**, the assignee and the manager included, with the usual guards: not the
author, not the Human, a known Persona, and the wake budget. The Task window's comment box and the task tools'
descriptions say so, and the built-in review loop Mentions the reviewer, or the owner, in its comments.

**One list for every actor.** The Human editing a silent field wakes no one either. The Human comments with a Mention to
get an Agent's attention.

**A silent change is dropped on receipt,** before the Human's change resets the wake budget or drops held wakes, so a
reorder cannot cancel a paused hand-off.

**The interface still says who will be woken,** from the same `Preview`, which now compares the Task before and after.
A save of only silent fields reads **Save**, not **Save & Notify Nova**.

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep waking on any change, and keep Rank out of the Task file | The owner put Rank in the Task file, so Agents read it with the Task; the Sprint and comment costs remain either way |
| A different list for the Human than for Agents | Two lists to learn and to test; a Mention says "look at this" more clearly than a priority change |
| A configurable list of silent fields | A setting nobody tunes, and every UI label would depend on it |
| Every status change wakes, as before | Most moves out of the Backlog into a finished status, and between finished statuses, ask nothing of anyone |
| Waking the assignee when the Human gate is switched on | Code stops the Task at its next move and says why; a wake would only repeat that |
| Comments wake the assignee as before, with Mentions adding others | Most comments are notes for the record; the author knows who must act, and the review loop already names them |
| Batching silent changes into the next wake's Message | The Agent reads the current rank, priority and dates with `get_task`; a list of reorders is noise |
| Telling the previous assignee on reassignment | Unchanged from ADR-0026 by the owner's decision; they find out on their next `get_task` |

## Consequences

- **A rank drag, a Sprint approval or a tag edit wakes no one.** The Timeline still logs each one.
- **ADR-0026's Explorer consequence is gone:** renaming a Team folder outside Huddle no longer wakes every Task's assignee.
- **The review loop depends on Mentions.** The built-in processes and hiring-pool personas are reworded; an installed
  Team keeps its old wording until it is recreated, so its "ready" comments wake no one.
- **A comment with no Mention wakes no one, not even the assignee.** The `add_comment` reply says who will be woken, or
  that no one will, so an Agent learns the rule from the tool as it uses it.
- **`TaskChange` carries its kind,** so a rule never has to guess from an empty field list what kind of change it was.
- **Adding a `TaskField` fails the build until it is classified** as waking or silent.

## As built

Built 2026-11-19 on `feat/smart-wakes`; the full design as built is the spec. The build differs from the decision text in these places:

- **A Human decision is checked first.** A decline closes the Task (status Rejected) and must still wake, so a decision wakes
  before the closed-flag rule is read.
- **Closing or reopening a To Do or Backlog Task is silent.** The decision text says a close or reopen wakes only when the
  Task is In Progress or Needs You; the build applies that to every Task, a To Do one included, and the owner may flip it
  (spec C-6). The `close_task` and `reopen_task` replies for such a Task say the assignee is not woken.
- **`@all` in a Task comment wakes no one** (spec C-7).
- **The requesting manager's reopen reading:** it wakes on a move into Done, Cancelled or Rejected and on a move out of a
  finished status into a non-finished one; a move from one finished status into Duplicate is silent.
- **`add_comment` has a fourth reply,** "No one is woken: wake-ups are off.", when wake-ups are off.
- **A kept Human change still runs both `ResetForHuman` and `ForgetHeld`;** only a dropped change runs neither.
- **The unblock check runs before the silent drop,** so a blocker that moves Backlog → Cancelled still wakes what it blocked.
