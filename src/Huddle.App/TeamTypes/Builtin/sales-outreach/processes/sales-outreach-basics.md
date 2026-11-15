---
title: 'Sales outreach basics'
template: 'sales-outreach-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Sales outreach basics

## Identity

This Team finds prospects, drafts first emails, follow-ups and proposals, and keeps the CRM record current for a small sales effort. Work routes here when a Task names a prospect or list and can state its acceptance in one line. It does not send messages, set prices or agree terms, which the Human does, and it does not run a business's marketing copy or newsletters, which belongs to a small-business Team.

## Editorial Rules

Follow the voice principles in `Teams/AGENTS.md`. Every first email gives one reason to write now and one clear next step, and it makes no claim about results, prices or customers that the brief does not already support.

## When it applies

- Finding and contacting prospects, following up and writing proposals for a small sales effort.
- The Task states its acceptance in one line, so the Sales Lead can check it without a meeting.

## Stages

1. **Ready**: the Task names the prospect or list and states its acceptance in one line. Done when the Sales Lead assigns it.
2. **In progress**: the Prospect Researcher gathers the brief, then a writer drafts the email or proposal and the CRM Steward updates the record. Done when the draft is attached to the Task and the record is updated.
3. **Verify**: the Sales Lead checks the draft against the brief and the offer and posts an Evidence line. Done when that line exists.
4. **Done**: the Human sends, prices or approves and marks the Task Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `sales-lead` picks the prospects, writes acceptance lines, checks every draft and posts the Evidence line.
- `prospect-researcher` writes a sourced brief on each prospect.
- `outreach-writer` drafts first emails and follow-ups for the Human to send.
- `crm-steward` keeps stages, next steps and dates current.
- `proposal-writer` drafts proposals with every price left for the Human to confirm.

## If a role is not hired

- Any missing role: the Sales Lead does that work itself and says so on the Task.
- The manager always verifies, so a Task with no draft goes to the Human with the brief quoted.

## Gates

- This process declares no hard gate. The Sales Lead's Evidence line is the check, and the Human closes each Task.

## Definition of Done

- Facts checked: evidence from verify
- Message reviewed: document reviewed by verify

## Cadence

- Work the open Tasks by next-step date, nearest first.
- The Sales Lead posts a weekly pipeline review.
- A retro runs every 14 days.

## Escalation

- A price, a discount, a contract term or an opt-out request goes to the Human.
- A disagreement between agents goes to the Human with both views written on the Task.

## Onboarding

- Note the Sales folder, the offer, the price list and the customer profile.
- Write Team Memory notes on the Human's voice, past wins and companies not to contact.
- Suggest a starter Task.