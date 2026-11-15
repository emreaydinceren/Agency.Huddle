---
title: 'Household basics'
template: 'household-basics@1.1.0'
gates: []
digest: { every: 7, unit: 'days' }
retro: { every: 28, unit: 'days' }
---
# Household basics

## Identity

This Team runs the everyday life of one family's home: school mail, meals and groceries, home upkeep, appointments, bills and renewals, and family events. Work that arrives here is a one-off job or a chore that comes round again, each kept as one Task with its acceptance in one line. A single personal errand for one adult belongs to the life-admin Team type, and a renovation or repair belongs to the home-project Team type.

## When it applies

- The everyday running of one family's home: school mail, meals and groceries, home upkeep, appointments, bills and renewals, and family events.
- A one-off job or a chore that comes round again, such as a school form, the boiler service or the weekly shop.
- The Task states its acceptance in one line, so the household manager can check it without a meeting.

## Stages

1. **Ready**: the Task has a title, an acceptance line and one owner. Done when the household manager assigns it.
2. **In progress**: the owner gathers the facts from the source (a school email, the pantry list, a quote, a bill, the upkeep calendar) and drafts the to-dos, menu, list, form or reply. Done when the draft is attached to the Task.
3. **Verify**: the household manager compares the draft with its source and posts an Evidence line. Done when that line exists.
4. **Done**: a parent orders, pays, books, signs or sends what is needed, and the Human marks the Task Done after reading the Evidence line.

A recurring job is one Task per occurrence. Huddle has no repeating Tasks, so the owner opens the next Task a week before it is due, from the dates in Team Memory, and writes the date after that when the Task is Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

Each job has one owner from noticing that it needs doing to getting it done, never a helper who waits to be asked. The household manager keeps the "who owns what" note and gives a new kind of job an owner before it becomes a Task.

- `household-manager` picks the Task, writes its acceptance line, checks each draft against its source, posts the Evidence line and leads the weekly check-in.
- `school-liaison` reads the school emails, turns them into to-dos, events and the weekly digest, and drafts replies for a parent to send.
- `meal-planner` plans the week's meals around the allergy notes, keeps the pantry list and drafts the grocery list.
- `home-keeper` keeps the upkeep calendar, the contractors and the warranties, and opens a Task each time a recurring job comes round.
- `health-admin` keeps the family's appointments, vaccinations, prescriptions and health forms in order.
- `schedule-keeper` keeps the family calendar and the renewals list (passports, insurance, car, memberships), each with its date and a reminder Task a month ahead.
- `finance-tracker` tracks bills, subscriptions and spending against the household budget.
- `comparison-shopper` compares quotes and prices for a big purchase or a renewal and recommends a short list.
- `retro-agent` runs the retro: it reads what slipped since the last one and proposes at most three small changes to this process.

## If a role is not hired

- Any missing drafting role: the household manager does that work itself, more slowly, and says so on the Task.
- No `retro-agent`: the household manager runs the retro itself.
- The manager always verifies, so a Task that has no draft goes to the Human with its source quoted.

## Gates

- This process declares no hard gate. The household manager's Evidence line is the check before a Task is ready, and the Human closes each Task.
- Every Task that spends money or needs a signature carries an `action:` line naming the amount and who is paid, or what is signed. That Task waits in Needs You until a parent accepts it, however small the amount.

## Definition of Done

- Plan checked: evidence from verify
- Plan reviewed: document reviewed by verify

## Cadence

- Work the open Tasks nearest their date first, and keep the number in progress small.
- Huddle wakes the household manager every 7 days, counted from the day this process is adopted, for the weekly check-in: what is done, what is due in the next two weeks, what slipped, and who owns each item. Adopt it on the day the family meets, such as a Sunday.
- A retro runs every 28 days.

## Escalation

- Money, a signature, a school commitment, a health question or a change to a standing arrangement goes to the Human. A health question also goes to a clinician.
- A disagreement between agents goes to the Human with both views written on the Task.

## Onboarding

- Note which mailboxes and calendars the family uses.
- Write Team Memory notes on the allergies and diet of each person, the emergency contacts, the bin day, the weekly check-in day, the school's usual senders, the parents' preferences and the digest day.
- Write the "who owns what" note: each area of the home and its one owner.
- Notes record where a document is kept, never its number: "passports in the blue folder", never a passport, policy, account or card number.
- Suggest a starter Task.
