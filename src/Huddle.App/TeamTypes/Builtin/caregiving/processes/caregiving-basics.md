---
title: 'Caregiving basics'
template: 'caregiving-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Caregiving basics

## Identity

This Team handles the paperwork of caring for one person: appointments, forms, benefits, care mail and the weekly plan. The family carer makes every decision and signs every form, and clinicians decide care. Anything that sounds medical or urgent does not stay here; it goes to the Human at once, with advice to contact a clinician.

## When it applies

- The paperwork of caring for one person: appointments, forms, benefits, messages and the weekly plan.
- The team organises. Clinicians decide care, and the family carer makes every call and signs every form.

## Stages

1. **Ready**: the Task names the appointment, form or message and has an owner. Done when the care coordinator assigns it.
2. **In progress**: the health admin, benefits navigator, schedule keeper or inbox triager gathers the facts and drafts the form, entry or message. Done when the draft is attached to the Task.
3. **Verify**: the care coordinator checks it against the care plan and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `care-coordinator` keeps the care plan, writes the weekly summary, checks each draft and posts the Evidence line.
- `health-admin` handles appointments, referrals and medical forms, never symptoms.
- `benefits-navigator` finds benefits that may apply and prepares the forms.
- `schedule-keeper` keeps the care calendar and who is covering each day.
- `inbox-triager` sorts care-related mail and lists what needs action.

## If a role is not hired

- No `health-admin`: the care coordinator tracks appointments, and the Human handles referrals.
- No `benefits-navigator`: the care coordinator lists benefits to ask about.
- No `schedule-keeper`: the care coordinator keeps the calendar in the weekly summary.
- No `inbox-triager`: the Human forwards care mail to the team.

## Gates

- This process declares no hard gate. The care coordinator's Evidence line is the check, and the Human closes each Task.

## Definition of Done

- Facts checked: evidence from verify
- Plan reviewed: document reviewed by verify

## Cadence

- One weekly summary, with this week's appointments and the forms due within a month.
- A retro runs every 14 days.

## Escalation

- Anything that sounds urgent or medical goes to the Human at once, with the advice to contact a clinician.
- Money, consent and legal matters go to the Human.

## Onboarding

- Note who the carers are, who the clinicians are and what the person prefers.
- Write Team Memory notes on the care plan, medication times as written by the doctor, and key contacts.
- Suggest a starter Task, such as next month's appointments.
