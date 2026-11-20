---
title: 'Maintenance'
template: 'maintenance@1.1.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'failing-test-before-fix', 'accept-before-merge', 'blockers-done-before-start', 'one-in-progress-per-assignee']
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
4. **Ask for a review and a QA check**: the Task's owner (the developer) must create a review Task and a QA Task, each a child of the bug Task, assigned to the reviewer or to QA, added to the bug Task's `blocked_by`, and tagged `review` or `verify`. The reviewer reads the diff and approves by moving the review Task to Done, or leaves it open with the findings as a comment, Mentioning the owner. When the reviewer leaves findings, the owner fixes the work and comments on that same review Task, Mentioning the reviewer, that it is ready, and the reviewer reviews again; never create a second review Task for the same work. A review Task is never reopened. QA checks the symptom is gone, posts an Evidence line on the QA Task and moves it to Done. When QA leaves findings as a comment, Mentioning the owner, the owner fixes the work and comments on that same QA Task, Mentioning QA, that it is ready, and QA checks again; never create a second QA Task for the same work. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill). Done when both Tasks are Done.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` triages bugs, assigns them and moves Tasks to Done.
- `developer` writes the failing test, makes the fix and creates the review and QA Tasks.
- `reviewer` reads the diff on the review Task and checks that the fix is minimal.
- `qa-engineer` confirms the symptom is gone on the QA Task and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `reviewer`: the manager owns the review Task and reviews the fix before it is merged, or assigns the review Task to a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`. The Definition of Done's review item still needs that review Task to be Done.
- No `qa-engineer`: the manager owns the QA Task, confirms the symptom is gone and posts the Evidence line on it. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line, or a child Task tagged `verify` is Done.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `failing-test-before-fix`: Huddle refuses to move a bug Task on to Needs You or Done until an Evidence line shows a failing test written before the fix.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.
- `one-in-progress-per-assignee`: Huddle refuses to move a Task to In Progress, or to give an In Progress Task to someone, while its assignee already has another Task In Progress, and names that Task.

## Definition of Done

- Review: task tagged review
- QA evidence: task tagged verify
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
