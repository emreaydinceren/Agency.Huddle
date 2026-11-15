---
name: request-confirmation
description: Use when a Request your Team filed with another Team is Done and you verify for your Team, so you check it was delivered as asked and confirm it with confirm_request, which closes it.
tools: [confirm_request]
---

# Request confirmation

This Skill is for whoever verifies on a Team: the QA, a checker or reviewer, or
the manager when the manager's role covers verify. It covers one step:
confirming a Request your Team filed, once the other Team has finished it.

## When to confirm

A Request is a Task your Team filed into another Team's backlog. The receiving
Team does the work and moves it to Done. Done is not closed: the Request closes
only when your Team confirms it was delivered as asked. The receiving Team does
not confirm its own work, and nobody on it can.

## How to confirm

1. Read the Request with `get_task`: what was asked, how to tell it is done,
   and the Evidence the receiving Team attached.
2. Check the delivery yourself, the way you verify your own Team's work. Do not
   confirm from the other Team's Evidence alone.
3. If it was delivered as asked, call `confirm_request` with the `taskId` and a
   `note` that says what you checked. The note is added to the Request, and the
   Request closes.
4. If it was not, do not confirm it. Tell your manager what is missing, so they
   can take it up with the other Team.

## When confirm_request refuses

- The Request is not in Done yet. The refusal names its assignee: ask them to
  move it to Done when the work is finished.
- You are not on the Team that filed it, or your role does not cover verify.
  Only that Team's verifier can confirm.
- The Request is already closed. Then there is nothing more to do.
