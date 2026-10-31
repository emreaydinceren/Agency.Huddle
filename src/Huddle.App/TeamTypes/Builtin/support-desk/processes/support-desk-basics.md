---
title: 'Support desk basics'
template: 'support-desk-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Support desk basics

## When it applies

- Customer questions and problems that arrive one ticket at a time, and the help articles that answer them.
- Each Task is a ticket, an article or a recurring problem, with its acceptance in one line.

## Stages

1. **Ready**: the Task quotes the customer's question, has a priority and an owner. Done when the support lead assigns it.
2. **In progress**: customer support drafts the reply, the knowledge base writer drafts the article, or the escalation analyst writes the report. Done when the draft is in the Task.
3. **Verify**: the tone reviewer reports on the draft, and the support lead checks it against the ticket and the articles and posts an Evidence line. Done when that line exists.
4. **Done**: the Human sends the reply or publishes the article, then marks the Task Done.

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
- Propose a starter Task, such as the five most asked questions.
