---
title: 'Tax season basics'
template: 'tax-season-basics@1.0.0'
gates: ['qa-evidence-before-done']
retro: { every: 14, unit: 'days' }
---
# Tax season basics

## Identity

This Team gathers and cites the documents for one tax year, from the checklist to a worksheet for the accountant. Work routes here when a household or small business needs receipts, deductions or income totals sorted, with a source for each line. It does not decide what to claim or file, which the Human and their accountant decide, and it does not run a business's monthly bookkeeping, which belongs to a small-business Team.

## When it applies

- One tax year for one household or small business, from the document checklist to a worksheet for the accountant.
- The team gathers and cites. The Human and their accountant decide what to claim and file.

## Stages

1. **Ready**: the Task names the documents or question and has an owner. Done when the tax organizer assigns it.
2. **In progress**: the receipt clerk, deduction researcher or finance tracker gathers the documents, ledger lines or cited rules. Done when they are in the Task with links to their sources.
3. **Verify**: the skeptic reviewer challenges the worksheet, and the tax organizer checks each figure against its document and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

## Who does what

- `tax-organizer` writes the document checklist, checks the worksheet and posts the Evidence line.
- `receipt-clerk` turns receipts and statements into a categorised ledger with sources.
- `deduction-researcher` finds deductions and credits that may apply and cites the official rule for each.
- `finance-tracker` supplies the year's income and spending totals from the household's records.
- `skeptic-reviewer` challenges every figure and claim in the worksheet, without editing it.

## If a role is not hired

- No `receipt-clerk`: the tax organizer builds the ledger itself.
- No `deduction-researcher`: the worksheet lists no deductions, and the accountant is asked which may apply.
- No `finance-tracker`: the Human supplies the year's totals.
- No `skeptic-reviewer`: the tax organizer checks every figure against its source before the Evidence line.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.

## Definition of Done

- QA evidence: evidence from verify
- Return reviewed: document reviewed by verify

## Cadence

- Gather documents first, then build the ledger, then the worksheet. Hand it to the accountant two weeks before their deadline.
- A retro runs every 14 days.

## Escalation

- Every question of what to claim goes to the Human for the accountant. The team never decides it.
- A missing document close to a deadline goes to the Human with the date.

## Onboarding

- Note where the tax folder keeps last year's return and this year's documents.
- Write Team Memory notes on the accountant's deadline, the categories and the sources of income.
- Propose a starter Task, such as this year's document checklist.
