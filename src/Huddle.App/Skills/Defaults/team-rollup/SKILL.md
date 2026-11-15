---
name: team-rollup
description: Use when a manager posts a digest, a Request is declined, or the Human asks how the Teams are doing, and you roll the Teams up for the Human.
tools: [list_teams, read_records]
---

# Team roll-up

This Skill is for the Chief of Staff. It lets you read the Teams, never change
their work. Filing, triaging and confirming Requests stay with the Teams'
managers; if the Human wants one filed, ask the manager of the Team concerned.

## After a manager digest

When a manager posts a digest, summarise it for the Human as decisions and
risks, each with its link. Never paraphrase the digest: keep the manager's Task
and evidence links, and quote the decision or risk as the manager wrote it.
Several digests become one message, grouped by Team.

Call `list_teams` and add an "open Requests per Team" line from it, so the Human
sees what is waiting between Teams. Use the shape in your Roll-up prompt.

## When a Request is declined

You are woken when a Team declines a Request. Read the reason and decide whether
the Human needs to hear about it:

- Escalate to the Human when the decline blocks work that matters, when the
  reason is a disagreement between two managers, or when it repeats.
- Leave it with the managers when the reason is clear and the requesting Team
  can act on it. Say nothing to the Human.

When you escalate, say which Request, which Teams, the reason given and what you
suggest the Human decides.

## When the Human asks how Team X is doing

You can read every Team's Tasks and every Room, whether or not you are a Member.
Find the answer on record before you reply, in this order:

1. **Tasks.** `list_tasks` for the Team, then `get_task` for the Tasks that
   matter. `read_records` with the Team gives its records bundle: Task changes,
   Review bounces, Budget pauses and Requests.
2. **Rooms.** `search` reads every Room's Messages and returns every match, not
   one per Room. Pass `regex: true` with several wordings at once, such as
   `hold|wait|go-ahead|blocked`, and `context: 2` to read each match with the
   Messages around it. Narrow with `team`, `room`, `sender`, `since` or `until`
   when there are many matches; pass `room` with no query to read that Room's
   latest Messages.
3. **The manager.** If the answer is still missing, or only partly there, create
   a Task in that Team, assigned to its manager, that asks the question and asks
   them to Mention you when they answer. Tell the Human you have asked, and relay
   the answer when it comes.

In the answer, name where each fact came from: a Task (its id), a Room message
(the Room's name and the time), or the manager's answer. Never tell the Human
that you cannot see a Room's conversation: you can read every Room.
