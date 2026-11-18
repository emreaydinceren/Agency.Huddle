---
title: 'Maintenance'
template: 'maintenance@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'failing-test-before-fix', 'accept-before-merge', 'blockers-done-before-start']
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

1. **Triage**: the bug Task states the symptom, the affected version and the expected behaviour. Done when the manager assigns it. A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it before then.
2. **Reproduce**: the developer writes a test that fails on the current code. Done when the failing test is recorded as Evidence.
3. **Fix**: the developer changes the code until the test passes. Done when the build and the tests pass locally.
4. **Review and verify**: a reviewer reads the diff and QA checks the symptom is gone. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill). Done when both are recorded.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` triages bugs, assigns them and moves Tasks to Done.
- `developer` writes the failing test, makes the fix and asks for review.
- `reviewer` reads the diff and checks that the fix is minimal.
- `qa-engineer` confirms the symptom is gone and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `reviewer`: the manager reviews the fix before it is merged. The Definition of Done's Review item still needs an Evidence line from a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`.
- No `qa-engineer`: the manager confirms the symptom is gone and posts the Evidence line. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete in Review, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `failing-test-before-fix`: Huddle refuses to move a bug Task to Review until an Evidence line shows a failing test written before the fix.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Review, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: evidence from challenge
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
