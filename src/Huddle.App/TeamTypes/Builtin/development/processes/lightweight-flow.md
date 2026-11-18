---
title: 'Lightweight flow'
template: 'lightweight-flow@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'accept-before-merge', 'blockers-done-before-start']
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

1. **Ready**: the Task has a title, an acceptance line and an assignee. Done when the manager assigns it. A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it before then.
2. **In progress**: the developer writes the change and its tests. Done when the build and the tests pass locally.
3. **Review**: a reviewer reads the diff, runs the Team's check command on it and records the outcome on the Task as an Evidence line with that command, with the next step, not only in chat. Done when no open finding remains.
4. **Verify**: QA starts only after the review has no open finding. QA checks the change against the acceptance line and posts an Evidence line. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill) and the Evidence line says what it did. Done when that line exists. If the change gets a new commit after the review, QA runs the verify again on that commit.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` picks the Task, writes its acceptance line and moves it to Done.
- `developer` builds the change, writes the tests and asks for review when the checks pass.
- `reviewer` reads the diff and records findings on the Task.
- `qa-engineer` verifies the acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the developer asks the manager for a second read, and the manager reviews the diff. The Definition of Done's Review item still needs an Evidence line from a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`.
- No `qa-engineer`: the manager verifies against the acceptance line and posts the Evidence line. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Review, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: evidence from challenge
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
