---
title: 'Docs team basics'
template: 'docs-team-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Docs team basics

## When it applies

- Writing, fixing or restructuring documentation that lives in the repository.
- Each Task covers one page or one closely related set, with its acceptance in one line.

## Stages

1. **Ready**: the Task names the page and the reader's task and states its acceptance in one line. Done when the Docs Lead assigns it.
2. **In progress**: the Technical Writer drafts or updates the page. Done when the page is on a branch.
3. **Verify**: the Docs Tester follows every step, the Copy Editor reads it, and the Docs Lead checks both results and posts an Evidence line. Done when that line exists.
4. **Done**: the Human merges, or approves the merge, and marks the Task Done.

## Who does what

- `docs-lead` picks pages, writes acceptance lines, checks the test and copy results and posts the Evidence line.
- `technical-writer` writes and updates the pages from the code and notes.
- `docs-tester` follows each page from a clean start and reports every failing step.
- `copy-editor` reads for clarity, consistency and house style.
- `developer` answers questions about the code and fixes product bugs the tester finds, as separate Tasks.

## If a role is not hired

- No `docs-tester`: the Docs Lead runs the steps itself before posting the Evidence line.
- No `copy-editor` or `developer`: the Docs Lead reads for style, and code questions go to the Human.

## Gates

- This process declares no hard gate. The Docs Lead's Evidence line, with the tester's result, is the check, and the Human closes each Task.

## Definition of Done

- Technical review: document reviewed by verify
- Examples tried: evidence from verify

## Cadence

- Work the open Tasks in the order the Docs Lead sets, most-read pages first.
- A retro runs every 14 days.

## Escalation

- A page that documents behaviour the code does not have goes to the Human with both quoted.
- A disagreement between agents goes to the Human with both views written on the Task.

## Onboarding

- Note the docs folder, the docs index and how the docs are built.
- Write Team Memory notes on the style guide, the audience and the pages readers ask about most.
- Propose a starter Task to the Docs Lead.