---
title: 'Events basics'
template: 'events-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Events basics

## Identity

This Team plans one event at a time, from a small birthday to a small conference, with its date, guest count and budget. Work that arrives here is a venue, vendor or product comparison, a vendor quote or reply, the timeline and run of show, or the weekly status. Work that is not one event, such as a recurring household errand, belongs to the household Team type, and the Human confirms every deposit and contract.

## When it applies

- Planning one event, from a birthday to a small conference, with a date, a guest count and a budget.
- The Task states its acceptance in one line, so the Event Lead can check it without a meeting.

## Stages

1. **Ready**: the Task has a title, an acceptance line and an owner. Done when the Event Lead assigns it.
2. **In progress**: an agent drafts the comparison, the vendor reply or the timeline. Done when the draft is attached to the Task.
3. **Verify**: the Event Lead checks the draft against the plan, the budget and the source emails and posts an Evidence line. Done when that line exists.
4. **Done**: the Human confirms, books or sends what is needed and marks the Task Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `event-lead` agrees the plan, writes acceptance lines, checks every draft and posts the Evidence line.
- `vendor-coordinator` reads vendor emails, tracks quotes and drafts replies for the Human to send.
- `schedule-keeper` keeps the timeline, the run-of-show and the weekly status.
- `comparison-shopper` compares venues, products and services and recommends a short list.

## If a role is not hired

- Any missing role: the Event Lead does that work itself and says so on the Task.
- The manager always verifies, so a Task with no draft goes to the Human with its source quoted.

## Gates

- This process declares no hard gate. The Event Lead's Evidence line is the check, and the Human closes each Task.

## Definition of Done

- Bookings confirmed: evidence from verify
- Run sheet reviewed: document reviewed by verify

## Cadence

- Work the open Tasks nearest deadline first.
- The Schedule Keeper posts a weekly status, daily in the last week before the event.
- A retro runs every 14 days and once after the event.

## Escalation

- A deposit, a contract, a date change or a cost over budget goes to the Human.
- A disagreement between agents goes to the Human with both views written on the Task.

## Onboarding

- Note the event date, guest count, budget and the mailbox vendors write to.
- Write Team Memory notes on the Human's must-haves, dietary needs and vendors they already trust.
- Suggest a starter Task.