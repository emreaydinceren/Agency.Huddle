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
3. **Ask for a review**: to ask for a review, the Task's owner (the developer) must create a review Task: tagged `review`, a child of the work Task, assigned to the reviewer, and adds it to the work Task's `blocked_by`. The reviewer reads the diff, runs the Team's check command on it and records the outcome on the review Task as an Evidence line with that command, with the next step, not only in chat. The reviewer approves by moving the review Task to Done, or leaves it open with the findings as a comment; when the reviewer leaves findings, the owner fixes the work and comments on that same review Task that it is ready, and the reviewer reviews again; never create a second review Task for the same work. A review Task is never reopened. Done when the review Task is Done.
4. **Ask for a QA check**: once the review Task is Done, the owner creates a QA Task the same way: tagged `verify`, a child of the work Task, assigned to QA, added to the work Task's `blocked_by`. QA checks the change against the acceptance line, posts an Evidence line on the QA Task and moves it to Done. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill) and the Evidence line says what it did. When QA leaves findings, the owner fixes the work and comments on that same QA Task that it is ready, and QA checks again; never create a second QA Task for the same work. Done when the QA Task is Done. If the change gets a new commit after the review, the owner creates a new QA Task for that commit.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` picks the Task, writes its acceptance line and moves it to Done.
- `developer` builds the change, writes the tests and creates the review Task when the checks pass.
- `reviewer` reads the diff and comments the findings on the review Task.
- `qa-engineer` verifies the acceptance line on the QA Task and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the manager owns the review Task and reviews the diff, or assigns the review Task to a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`. The Definition of Done's review item still needs that review Task to be Done.
- No `qa-engineer`: the manager owns the QA Task, verifies against the acceptance line and posts the Evidence line on it. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line, or a child Task tagged `verify` is Done.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: task tagged review
- QA evidence: task tagged verify
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
