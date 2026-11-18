---
title: 'Maintenance'
template: 'maintenance@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'failing-test-before-fix']
retro: { every: 10, unit: 'done-tasks' }
wip: 2
checkCommand: 'dotnet test'
---
# Maintenance

## Identity

This Team fixes bugs and upgrades dependencies in code that already ships, as the maintenance process of the `development` Team type. Work starts from a bug Task that states the symptom, the affected version and the expected behaviour, and a regression test must fail before any fix lands. A new feature or a change that needs a design goes to the spec-driven process, and documentation fixes go to the `docs-team` Team type.

## When it applies

- Bug fixes, dependency upgrades and upkeep of code that already ships.
- Work where a regression must be pinned by a test before the fix lands.

## Stages

1. **Triage**: the bug Task states the symptom, the affected version and the expected behaviour. Done when the manager assigns it.
2. **Reproduce**: the developer writes a test that fails on the current code. Done when the failing test is recorded as Evidence.
3. **Fix**: the developer changes the code until the test passes. Done when the build and the tests pass locally.
4. **Review and verify**: a reviewer reads the diff and QA checks the symptom is gone. Done when both are recorded.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge names that merge as its Action, such as `Merge feat/x into main`, so the Human's Accept leads to the merge.

## Who does what

- `development-manager` triages bugs, assigns them and moves Tasks to Done.
- `developer` writes the failing test, makes the fix and asks for review.
- `reviewer` reads the diff and checks that the fix is minimal.
- `qa-engineer` confirms the symptom is gone and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `reviewer`: the manager reviews the fix before it is merged.
- No `qa-engineer`: the manager confirms the symptom is gone and posts the Evidence line. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.
- `failing-test-before-fix`: Huddle refuses to move a bug Task to Review until an Evidence line shows a failing test written before the fix.

## Definition of Done

- QA evidence: evidence from verify
- Tests pass: command `dotnet test` exits 0

## Cadence

- Keep at most two bug Tasks in progress at a time (`wip: 2`), so fixes do not pile up.
- A retro runs every 10 done Tasks.

## Escalation

- A fix that needs a design change, or a priority clash, goes to the Chief of Staff.
- If the Chief of Staff cannot settle it, it goes to the Human.

## Onboarding

- Read the workspace and the open bug list.
- Run the build and the tests, and record the results.
- Write Team Memory notes on the areas that break most often.
- Suggest a starter Task, such as the oldest open bug with a clear symptom.
