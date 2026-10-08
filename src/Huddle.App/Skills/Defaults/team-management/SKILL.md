---
name: team-management
description: Use when you manage a Team and want to plan its work: when a set of Tasks should become a Sprint, you propose it to the Human instead of creating it yourself.
tools: [propose_sprint]
---

# Team management

This Skill is for the manager of a Team. It covers the planning steps that need
the Human's say-so.

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
