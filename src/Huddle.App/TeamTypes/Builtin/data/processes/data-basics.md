---
title: 'Data basics'
template: 'data-basics@1.2.0'
gates: ['qa-evidence-before-done', 'blockers-done-before-start']
retro: { every: 14, unit: 'days' }
---
# Data basics

## Identity

This Team answers a question the Human has with data: a metric, a trend, a comparison or a cleaned dataset. Each metric is defined on the Task before anyone builds, and the analytics lead reproduces the key figures before posting the Evidence line. Software changes do not route here; they go to the `development` Team type.

## When it applies

- A question the Human wants answered from data: a metric, a trend, a comparison, a cleaned dataset.
- The Task defines each metric and the data it will use before anyone builds.

## Stages

1. **Ready**: the Task has a question, the metric definitions, the data source and an acceptance line. Done when the analytics lead assigns it.
2. **In progress**: the Data Engineer prepares documented tables from the raw files, and the Analyst builds the analysis on them. Done when the result and its steps are attached to the Task.
3. **Verify**: Data QA checks the data and recomputes the headline numbers, and the analytics lead reproduces the key figures and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

## Who does what

- `analytics-lead` defines the metrics, assigns the work, reproduces the key numbers and posts the Evidence line.
- `data-engineer` loads, cleans and reshapes the raw files into documented tables, leaving the raw files untouched.
- `analyst` builds the analysis on the prepared tables and writes down the method.
- `data-qa` checks counts, joins, units and totals and reports, read-only.

## If a role is not hired

- No `data-engineer`: the analyst prepares the data and records each step.
- No `data-qa`: the analytics lead runs the row-count and total checks before the Evidence line.
- The manager always verifies, so the gate below can always be met.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- QA evidence: evidence from verify
- Method reviewed: document reviewed by verify

## Cadence

- Agree the metric definitions before preparing data, and keep one analysis in progress at a time.
- A retro runs every 14 days.

## Escalation

- An ambiguous metric, incomplete data, or a result that will drive a decision about money or people goes to the Human.
- A failed check that changes the answer goes to the analytics lead, then to the Human with the figure quoted.

## Onboarding

- Read the data folder, if one is connected, and note its files, formats and date ranges.
- Write Team Memory notes on the metric definitions and known data quirks.
- Suggest a starter Task, such as profiling one dataset.
