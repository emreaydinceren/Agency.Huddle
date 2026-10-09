---
name: team-management
description: Use when you manage a Team and need to see the other Teams, file or triage a Request between Teams, confirm a finished Request, or propose a Sprint to the Human.
tools: [list_teams, file_request, triage_request, confirm_request, propose_sprint]
---

# Team management

This Skill is for the manager of a Team. It covers working with other Teams and
the planning steps that need the Human's say-so.

## Requests between Teams

Only managers file and triage Requests. If you are a specialist, ask your own
manager to do it.

- `list_teams` shows every Team with its type, manager, what it owns and how
  many Requests are open in its backlog. Read it before you file.
- `file_request` files a Request into another Team's backlog. Give the `toTeam`,
  the `kind`, a `title` and a `body` that fills every section the kind requires.
  Pass `relatedTaskId` when one of your own Tasks waits on the answer; it is
  blocked until the Request closes. You cannot file into your own Team.
- `triage_request` is for the receiving manager. Use `accept`, `decline` or
  `needs_info` on a Request in your backlog. Declining and asking for more
  information both need a `reason`.
- `confirm_request` is for the requesting Team's QA, after the Request is Done.
  The receiving Team does not confirm its own work.

## Proposing a Sprint

Use `propose_sprint` when several of your Team's Tasks belong together as one
stretch of work. Give it a `name` and the `taskIds` to include.

- Only the manager of the Team can propose, and every Task must belong to your
  Team and not be Done.
- The name must not match an earlier Sprint. You may reuse the name of the
  current Sprint to add Tasks to it.
- Proposing creates nothing. The Human sees the proposal and approves or
  declines it; only an approval creates the Sprint and moves the Tasks into it.
- After you propose, carry on with other work. Do not propose the same Sprint
  twice while one is waiting.
