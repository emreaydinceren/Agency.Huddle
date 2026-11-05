---
title: 'Lightweight flow'
template: 'lightweight-flow@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge']
retro: { every: 10, unit: 'done-tasks' }
checkCommand: 'dotnet test'
---
# Lightweight flow

## Identity

This Team builds and ships software in a repository, and this process takes a small change or fix that one developer can carry from start to Done without a written spec. Work that needs agreed behaviour across modules or a shared contract goes to the spec-driven process, and bugs in code that already ships go to maintenance. Documentation-only changes belong to the `docs-team` Team type.

## When it applies

- Small changes and fixes that one developer can take from start to Done without a written spec.
- The Task states its acceptance in one line, so QA can check it without a meeting.

## Stages

1. **Ready**: the Task has a title, an acceptance line and an assignee. Done when the manager assigns it.
2. **In progress**: the developer writes the change and its tests. Done when the build and the tests pass locally.
3. **Review**: a reviewer reads the diff. Done when no open finding remains.
4. **Verify**: QA checks the change against the acceptance line and posts an Evidence line. Done when that line exists.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met.

## Who does what

- `development-manager` picks the Task, writes its acceptance line and moves it to Done.
- `developer` builds the change, writes the tests and asks for review when the checks pass.
- `reviewer` reads the diff and records findings on the Task.
- `qa-engineer` verifies the acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the developer asks the manager for a second read, and the manager reviews the diff.
- No `qa-engineer`: the manager verifies against the acceptance line and posts the Evidence line. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.

## Definition of Done

- QA evidence: evidence from verify
- Tests pass: command `dotnet test` exits 0

## Cadence

- Work the Team's open Tasks in the order the manager sets, and keep the number in progress small.
- A retro runs every 10 done Tasks.

## Escalation

- A decline or a priority clash goes to the Chief of Staff.
- If the Chief of Staff cannot settle it, it goes to the Human.

## Onboarding

- Read the workspace and note its layout.
- Run the build and the tests, and record the results.
- Write Team Memory notes on the conventions you found.
- Suggest a starter Task.
