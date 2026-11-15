---
title: 'Support desk basics'
template: 'support-desk-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Support desk basics

## Identity

This Team handles customer tickets one at a time: it drafts replies for the Human to send, writes help articles from real tickets and reports on problems that keep coming back. Work routes here when a Task is a ticket, an article or a recurring problem that needs an acceptance line. It does not send replies, promise refunds or answer legal, safety or data matters, which go to the Human at once, and it does not write marketing copy, which belongs to a small-business Team.

## Editorial Rules

Follow the voice principles in `Teams/AGENTS.md`. Answer the customer's question in the first sentence of each reply, and make no promise about refunds, dates or fixes that the Human has not approved.

## When it applies

- Customer questions and problems that arrive one ticket at a time, and the help articles that answer them.
- Each Task is a ticket, an article or a recurring problem, with its acceptance in one line.

## Stages

1. **Ready**: the Task quotes the customer's question, has a priority and an owner. Done when the support lead assigns it.
2. **In progress**: customer support drafts the reply, the knowledge base writer drafts the article, or the escalation analyst writes the report. Done when the draft is attached to the Task.
3. **Verify**: the tone reviewer reports on the draft, and the support lead checks it against the ticket and the articles and posts an Evidence line. Done when that line exists.
4. **Done**: the Human sends the reply or publishes the article, then marks the Task Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `support-lead` sorts the queue, assigns Tasks, checks each draft and posts the Evidence line.
- `customer-support` drafts replies to tickets for the Human to send.
- `knowledge-base-writer` writes and updates help articles from real tickets.
- `escalation-analyst` writes repro steps and root-cause reports for hard or repeated problems.
- `tone-reviewer` reads each draft for tone and promises, without rewriting it.

## If a role is not hired

- No `customer-support`: the support lead drafts replies itself.
- No `knowledge-base-writer`: the support lead keeps a list of articles to write for the Human.
- No `escalation-analyst`: the support lead writes the bug report from the tickets.
- No `tone-reviewer`: the support lead reads each reply for tone before the Evidence line.

## Gates

- This process declares no hard gate. The support lead's Evidence line is the check, and the Human sends every reply.

## Definition of Done

- Answer verified: evidence from verify
- Reply reviewed: document reviewed by verify

## Cadence

- Urgent tickets first, then oldest first. Keep each drafter to a few tickets at a time.
- A retro runs every 14 days.

## Escalation

- Refunds, legal threats, safety and data concerns go to the Human at once.
- A problem seen three times goes to the escalation analyst.

## Onboarding

- Note where the support folder keeps tickets, articles and saved replies.
- Write Team Memory notes on the product, the tone of voice and what the team may promise.
- Suggest a starter Task, such as the five most asked questions.
