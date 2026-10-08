---
name: retrospective
description: Use when you run or join a Team retrospective and need the Team's records as evidence.
tools: [read_records, propose_retro_changes]
---

# Retrospective

This Skill gives you `read_records`, the compact evidence bundle a retrospective
starts from, and `propose_retro_changes`, which puts a change in front of the Human.
Run a retro in six stages, in this order.

## Gather

Call `read_records` for the Team since the last retro. Read the whole bundle before
you form a view: Task change logs, Review bounces, Budget pauses, wake-up limits hit,
escalations, spend, Requests and the tool-call summaries. Every issue you report must
cite a Task id, a log line or a retro file.

## Ask

Open a room with the members involved in each issue; the Human is added as always.
Ask about one specific moment at a time: what they saw, what they expected and what
they did. Do not lead them. Stop once you can state the cause in one sentence.

## Report

Write the report to `_retros/<date>.md`: the top three issues, each with its finding,
its evidence link and the member's account where they gave one.

## Propose

Propose at most three changes with `propose_retro_changes`. Each is a diff to
`Process.md`, a stance or a skill assignment. Removing a rule counts as a change. The
Human accepts or rejects each one, and Huddle applies an accepted change; never say a
change is already applied.

## Register

Add each finding to `Retro register.md` with status Open, an action, an owner and the
evidence or the next check.

## Verify

At the next retro, check each Done row against new evidence. Mark a row Verified only
when later evidence shows the change worked, and cite that evidence. Mark it Partial
when it helped less than planned, and Changed when the Team replaced it.
