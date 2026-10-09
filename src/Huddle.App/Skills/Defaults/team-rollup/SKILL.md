---
name: team-rollup
description: Use when a manager posts a digest, a Request is declined, or the Human asks how the Teams are doing, and you roll the Teams up for the Human.
tools: [list_teams]
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
