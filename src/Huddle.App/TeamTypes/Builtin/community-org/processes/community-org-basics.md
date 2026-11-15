---
title: 'Community org basics'
template: 'community-org-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Community org basics

## Identity

This Team runs the paperwork of a club, society or volunteer group: agendas, minutes, rotas, newsletters, sign-ups and the treasurer's report. It drafts, and the group's officers approve and send. Votes, membership decisions, money and complaints about a member do not route here; they go to the Human.

## Editorial Rules

Follow the voice principles in `Teams/AGENTS.md`. Write plainly for members who may be volunteers, and give dates, places and amounts exactly as the minutes or the accounts state them.

## When it applies

- The running of a club, society or volunteer group: meetings, minutes, rotas, newsletters and the books.
- The team drafts. The group's officers approve and send.

## Stages

1. **Ready**: the Task names the agenda, minutes, rota, issue or report and has an owner. Done when the board secretary assigns it.
2. **In progress**: the volunteer coordinator, newsletter writer, bookkeeper or scribe drafts it. Done when the draft is attached to the Task.
3. **Verify**: the board secretary checks it against the minutes and the group's rules and posts an Evidence line. Done when that line exists.
4. **Done**: the Human sends or files it, then marks the Task Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `board-secretary` drafts agendas and minutes, checks every draft and posts the Evidence line.
- `volunteer-coordinator` keeps the volunteer list and rota and drafts sign-ups and reminders.
- `newsletter-writer` drafts each issue from the group's news and dates.
- `bookkeeper` keeps the group's accounts and drafts the treasurer's report.
- `scribe` takes notes at meetings and turns them into a clean record.

## If a role is not hired

- No `volunteer-coordinator`: the board secretary keeps the rota in the minutes.
- No `newsletter-writer`: the board secretary sends a short update from the minutes.
- No `bookkeeper`: the treasurer keeps the books, and the team only reads them.
- No `scribe`: the board secretary writes the minutes from the Human's notes.

## Gates

- This process declares no hard gate. The board secretary's Evidence line is the check, and the Human sends everything.

## Definition of Done

- Facts checked: evidence from verify
- Draft reviewed: document reviewed by verify

## Cadence

- Agenda a week before each meeting, minutes within three days after it.
- A retro runs every 14 days.

## Escalation

- Money, votes, membership and anything outside the group's rules go to the Human.
- Complaints about a member go to the Human and are not discussed in drafts.

## Onboarding

- Note where the club folder keeps the rules, minutes, rota and accounts.
- Write Team Memory notes on the officers, the meeting cycle and the group's tone.
- Suggest a starter Task, such as the next meeting's agenda.
