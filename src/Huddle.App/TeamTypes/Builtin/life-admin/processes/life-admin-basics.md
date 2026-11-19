---
title: 'Life admin basics'
template: 'life-admin-basics@1.2.0'
gates: ['blockers-done-before-start']
retro: { every: 14, unit: 'days' }
---
# Life admin basics

## Identity

This Team takes one person's personal admin from start to Done without a plan: a trip, a bill, an appointment, a form or a full inbox. Work that arrives here is one Task with an acceptance line the Chief of Household can check. A family's shared schedule, meals and chores belong to the household Team type, and money, bookings and health questions go to the Human.

## When it applies

- Personal admin one person can take from start to Done without a plan: a trip, a bill, an appointment, a form, a full inbox.
- The Task states its acceptance in one line, so the Chief of Household can check it without a meeting.

## Stages

1. **Ready**: the Task has a title, an acceptance line and an owner. Done when the Chief of Household assigns it.
2. **In progress**: an agent gathers the facts and drafts the itinerary, budget note, form or reply. Done when the draft is attached to the Task.
3. **Verify**: the Chief of Household compares the draft with its source and posts an Evidence line. Done when that line exists.
4. **Done**: the Human books, pays or sends what is needed and marks the Task Done.

## Who does what

- `chief-of-household` picks the Task, writes its acceptance line, checks the draft and posts the Evidence line.
- `travel-planner` researches trips and drafts itineraries with their booking terms.
- `finance-tracker` tracks bills, subscriptions and spending against the budget.
- `health-admin` keeps appointments, claims and health forms in order and drafts the replies.
- `inbox-triager` sorts the inbox and drafts the short replies.

## If a role is not hired

- Any missing role: the Chief of Household does that work itself, more slowly, and says so on the Task.
- The manager always verifies, so a Task with no draft goes to the Human with its source quoted.

## Gates

- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.
- This process declares no other hard gate. The Chief of Household's Evidence line is the check, and the Human closes each Task.

## Definition of Done

- Details checked: evidence from verify
- Form reviewed: document reviewed by verify

## Cadence

- Work the open Tasks nearest deadline first, and keep the number in progress small.
- The Chief of Household posts a weekly list of what is due.
- A retro runs every 14 days.

## Escalation

- Money, a booking, a health question or anything that commits the Human goes to the Human. A health question also goes to a clinician.

## Onboarding

- Note which mailbox, calendar and budget sheet the Human uses.
- Write Team Memory notes on travel preferences, regular bills, clinics and the Human's reply style.
- Suggest a starter Task.