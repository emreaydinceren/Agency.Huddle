---
name: team-management
description: Use when you manage a Team and need to see the other Teams, file or triage a Request between Teams, propose a Sprint to the Human, or answer a Task that asks you a question.
tools: [list_teams, file_request, triage_request, propose_sprint]
---

# Team management

How to manage, from triage to running a mission, is in your Manager Playbook; this Skill covers the tools.

This Skill is for the manager of a Team. It covers working with other Teams,
answering the questions they send you, and the planning steps that need the
Human's say-so.

## Answering a question Task

Someone, often the Chief of Staff, may give you a Task that only asks a question
or asks you to decide something: there is no work in it to build or check. When
you have the answer:

1. Record it on the Task with `add_evidence`. Put the whole answer in `note`, so
   it is on record without the conversation.
2. Do what else the Task asks, such as Mentioning the one who asked.
3. Close the Task with `close_task`. Closing is not marking it Done, so it needs
   no QA Evidence and does not break the rule that you never mark work Done
   yourself.

If `close_task` says the Task is already closed, the one who asked closed it
first, and there is nothing more to do.

## Planning the Backlog

Creating Backlog Tasks never needs the Human's approval, at any level. Do not
ask for it: create the Tasks and carry on.

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
- Confirming a finished Request is not part of this Skill. Whoever verifies on
  the Team that filed it holds the `request-confirmation` Skill and confirms it
  once it is Done; the receiving Team does not confirm its own work.

## Proposing a Sprint

Use `propose_sprint` when several of your Team's Tasks belong together as one
stretch of work. Give it a `name` and the `taskIds` to include.

- Only the manager of the Team can propose, and every Task must belong to your
  Team and not be Done.
- The name must not match an earlier Sprint. You may reuse the name of the
  current Sprint to add Tasks to it.
- When every Task's project or Team runs inside the Team, Huddle approves the
  proposal at once: the Sprint is created, the Tasks move into it, and the
  tool's answer says so. Do not wait for the Human.
- Otherwise proposing creates nothing yet. The Human sees the proposal and
  approves or declines it; only an approval creates the Sprint and moves the
  Tasks into it.
- After you propose, carry on with other work. Do not propose the same Sprint
  twice while one is waiting.
