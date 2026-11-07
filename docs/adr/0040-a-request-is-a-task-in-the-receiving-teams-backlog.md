---
status: accepted
date: 2026-10-08
---

# A Request is a Task in the receiving Team's backlog

When one Team needs something from another, such as a bug, a feature, feedback or a question,
that need is a **Request**. A Request is an ordinary Task file in the receiving Team's
`_tasks/` folder, with six extra frontmatter keys. Its lifecycle is a small state machine in
code, and only the managers and the QA of the right Team can move it. The Human's view is in
the [User Guide](../Huddle.UserGuide.md) (*Requests between Teams*); the original Teams specification has
been retired.

## The problem

Teams talk to each other across a boundary. A Web Team that finds an API bug needs the API
Team to triage it, work it, prove it is fixed, and let the Web Team confirm that the fix is
what was asked. Without a shared record, that conversation happens in chat, where nobody can
see whether it was accepted, declined or closed, and an Agent can close a Request by saying so.

A Request also needs the things a Task already has: a board where the receiving Team sees it,
a status, Evidence, `blocked_by`, and a rule that a change wakes the assignee
([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)).

## The decision

**A Request is a Task with a `request` key.** The file lives in the receiving Team's `_tasks/`.
The frontmatter carries:

| Key | Value |
| --- | --- |
| `request` | `bug`, `feature`, `feedback` or `question`. Its presence makes the Task a Request |
| `from_team` | The Team that filed it |
| `requester` | The Persona that filed it |
| `triage` | empty, `needs-info`, `accepted` or `declined` |
| `decline_reason` | Why it was declined; empty otherwise |
| `confirmed_by` | The requesting Team's QA who confirmed it; required to close it |

**The lifecycle is a pure state machine.** `RequestLifecycle.Apply` takes the current status and
triage and an event, and returns whether the event is allowed, the next state and who to wake.
It touches no store. The Task's status is one of Backlog, To Do, In Progress, Review, Done or
Rejected.

| From | Event | To | Woken |
| --- | --- | --- | --- |
| (none) | Filed | Backlog, triage empty | Receiving manager |
| Backlog, triage empty | Needs info | Backlog, needs-info | Requesting manager |
| Backlog, needs-info | Info added | Backlog, triage empty | Receiving manager |
| Backlog, triage empty | Accept | To Do, accepted | Requesting manager |
| Backlog, triage empty | Decline, with a reason | Rejected, declined | Requesting manager and Chief of Staff |
| To Do, In Progress or Review | Work | Same status | Requesting manager |
| Review | Evidence attached | Done | Requesting manager |
| Done | Confirm, by the requesting Team's QA | Closed through `TaskService.Close`, `confirmed_by` set | Receiving manager |

Any other event is refused with a message that names the status, the triage and the event.

**Only the right people move a Request, and the tools enforce it.** The four tools are
`file_request`, `triage_request`, `confirm_request` and `list_teams`. The built-in
`team-management` Skill lists them, and a Persona gets a tool only through a Skill that lists
it, so a specialist does not hold them.

- `file_request` works only for a Persona who manages a Team, and it writes the Task in the
  receiving Team with the six keys set.
- `triage_request` works only for the receiving Team's manager, by name.
- `confirm_request` works only for a member of `from_team` whose role covers `verify`, and only
  when the Request is Done. It then closes the Task through `TaskService.Close`.

- `file_request` takes a `fromTeam` argument. A Persona who manages one Team needs no
  argument; one who manages several is told to name the Team filing, and a name it does not
  manage is refused.
- `list_teams` works only for a Team manager or the built-in Chief of Staff, which manages no
  Team and lists Teams for its roll-up.

**The lifecycle is enforced in `TaskService`, not only in the Request tools.** `close_task`,
`update_task` and every other path to a Task go through `TaskService`. It refuses an Agent a
move out of Backlog that is not a triage, with a message that names `triage_request`, and it
refuses an Agent closing a Request that is neither declined nor confirmed (`confirmed_by`
empty), with a message that names `confirm_request`. A Human's change can pass the Backlog
refusal only through the same confirmed override as a gate
([ADR-0039](0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)).

**A Request event wakes each recipient once.** `RequestNotifier` wakes the Persona each target
resolves to, and skips a name it has already woken for the event, so a manager who is both
recipients is woken once. `TaskTriggerService` remembers the Request wake it sent and skips the
ordinary status-change wake for the same Task and recipient in the same change batch.

**`read_records` is scoped to the caller's Team.** A Task id must belong to the named Team. A
Room is shown only when a member of the Team posted in it and every sender is a member of the
Team or the Human, so a Room shared with another Team's Persona is refused.

## Rejected

| Alternative | Why not |
| --- | --- |
| A separate Request store and board | The receiving Team already has a board, Evidence, `blocked_by` and wake-ups for Tasks. A second store would repeat all of them |
| Requests in chat between managers | Nothing records whether a Request was accepted, declined or closed, and no one can see its state |
| Any Agent may triage | Triage decides whether another Team's work is accepted. Only the receiving manager can make that call |
| The requesting Team closes the Request | Closing is the proof that the work was delivered as asked, and only the requester can say so |

## Consequences

- **A Request is visible in the receiving Team's Tasks.** It sits in the Backlog until triaged,
  and the Human can filter for it like any Task.
- **Specialists cannot file or triage.** A Persona without the `team-management` Skill has no
  Request tools, so the rule does not depend on a prompt.
- **A Human can move a Request past triage** through the confirmed override, and the Change log
  records it.
- **An outside edit meets the same two rules** (added 2026-11-07). An Agent's `Bash` and `Write`
  reach the Task file on disk, so a file that takes a Request out of Backlog without triage has
  its head written back, and a file moved into `_closed/` before the Request is confirmed or
  declined is moved back. Each is logged as `outside edit refused: <message>`, with the message the
  tools give. As with a process gate (ADR-0039), this applies to the Human's own file edits too,
  because a file edit cannot carry the confirmed override.
- **Each change to a Request wakes the assignee and the managers named in the table,** through
  the existing Task wake-up rule. No new wake mechanism exists for Requests.
- **The receiving Team's Task folder holds the record.** Deleting that file deletes the Request,
  as it would any Task. A closed Request stays in the closed Tasks of the receiving Team.
