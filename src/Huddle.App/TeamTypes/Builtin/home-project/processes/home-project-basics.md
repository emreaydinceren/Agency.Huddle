---
title: 'Home project basics'
template: 'home-project-basics@1.2.0'
gates: ['blockers-done-before-start']
retro: { every: 14, unit: 'days' }
---
# Home project basics

## Identity

This Team gathers the costs, rules, quotes and dates for one renovation or repair, from the scope to the last contractor visit. Work that arrives here is a written scope, cost ranges, permit rules with their authority, contractor messages or the project calendar. The Human and qualified professionals agree every price, contract and date, and routine household chores belong to the household Team type.

## When it applies

- One renovation or repair, from the scope to the last contractor visit.
- The team gathers costs, rules and quotes. The Human and qualified professionals decide and agree.

## Stages

1. **Ready**: the Task names what to find or draft and has an owner. Done when the renovation planner assigns it.
2. **In progress**: the cost estimator, permit researcher, contractor liaison or schedule keeper gathers ranges, rules, quotes or dates with sources. Done when they are attached to the Task.
3. **Verify**: the renovation planner checks the work against the scope and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

## Who does what

- `renovation-planner` writes the scope and steps, checks each result and posts the Evidence line.
- `cost-estimator` gives cost ranges with assumptions and compares quotes on the same scope.
- `permit-researcher` finds the rules and permits that may apply, citing the authority, and prepares forms.
- `contractor-liaison` drafts messages to contractors and tracks their replies.
- `schedule-keeper` keeps the project calendar and flags clashes.

## If a role is not hired

- No `cost-estimator`: the renovation planner records the quotes as they arrive, without ranges.
- No `permit-researcher`: the Human asks the local authority, and the planner records the answer.
- No `contractor-liaison`: the renovation planner drafts the messages.
- No `schedule-keeper`: the renovation planner keeps the dates in the scope.

## Gates

- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.
- This process declares no other hard gate. The renovation planner's Evidence line is the check, and the Human agrees every price and date.

## Definition of Done

- Quotes and facts checked: evidence from verify
- Plan reviewed: document reviewed by verify

## Cadence

- Scope first, then permits and estimates, then quotes, then the schedule.
- A retro runs every 14 days.

## Escalation

- Prices, contracts, deposits, safety and structural questions go to the Human.
- A scope change goes to the Human with what it adds in cost and time.

## Onboarding

- Note where the project folder keeps the scope, photos, quotes and permits.
- Write Team Memory notes on the property, the budget limit and the must-not-change list.
- Suggest a starter Task, such as a one-page scope.
