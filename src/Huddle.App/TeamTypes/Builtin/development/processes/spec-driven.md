---
title: 'Spec-driven'
template: 'spec-driven@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'spec-approved-before-build', 'accept-before-merge', 'blockers-done-before-start']
retro: { every: 10, unit: 'done-tasks' }
checkCommand: 'dotnet test'
---
# Spec-driven

## Identity

This Team delivers features and changes that touch shared contracts, the wire protocol or more than one module, as the spec-driven process of the `development` Team type. Work starts from a spec Task that the Human approves before anyone breaks it down or builds. Small single-module changes go to the lightweight-flow process, and fixes to shipped code go to maintenance.

## When it applies

- Features and changes that touch shared contracts, the wire protocol or more than one module.
- Work where the team must agree on behaviour before anyone writes code.

## Stages

1. **Spec**: a spec Task holds the behaviour, the scope and the open questions. Done when the spec is complete enough to review and is attached to the Task.
2. **Approved**: the human approves the spec Task. Done when the approval is recorded on it.
3. **Break down**: the tech lead splits the spec into build Tasks, each with an acceptance line. Done when every build Task has one.
4. **Build**: the developer writes the change and its tests against the spec. Done when the build and the tests pass locally. A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it before then.
5. **Ask for a review and a QA check**: the Task's owner (the developer) must create a review Task and a QA Task, each a child of the build Task, assigned to the reviewer or to QA, added to the build Task's `blocked_by`, and tagged `review` or `verify`. The reviewer reads the diff against the spec and approves by moving the review Task to Done, or leaves it open with the findings as a comment; a review Task is never reopened. QA posts an Evidence line on the QA Task and moves it to Done. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill). Done when both Tasks are Done.
6. **Done**: the manager moves the build Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

A draft that is reviewed is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The spec is reviewed the same way as the build: its owner creates a review Task for it, and the reviewer checks that file, logs the review on it with `log_change` and the kind `reviewed` as its history line, and moves the review Task to Done. A draft written only in the Task's description cannot be reviewed.

## Who does what

- `development-manager` drafts or assigns the spec, asks for approval and moves Tasks to Done.
- `tech-lead` writes the breakdown and answers design questions on the spec.
- `developer` builds the change against the approved spec and creates the review and QA Tasks.
- `reviewer` reads the diff against the spec and comments the findings on the review Task.
- `qa-engineer` verifies each acceptance line on the QA Task and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the manager owns the review Task and reviews the diff against the spec, or assigns the review Task to a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`. The Definition of Done's review item still needs that review Task to be Done.
- No `qa-engineer`: the manager owns the QA Task, verifies each acceptance line and posts the Evidence line on it. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line, or a child Task tagged `verify` is Done.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `spec-approved-before-build`: Huddle refuses to move a build Task to In Progress until its parent spec Task is marked approved by the human.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: task tagged review
- QA evidence: task tagged verify
- Tests pass: command `dotnet test` exits 0

## Cadence

- Approve specs before the team starts breaking them down, and keep one spec in build at a time.
- A retro runs every 10 done Tasks.

## Escalation

- A spec disagreement or a priority clash goes to the Chief of Staff.
- If the Chief of Staff cannot settle it, it goes to the Human for the approval decision.

## Onboarding

- Read the workspace and note its layout and its specs.
- Run the build and the tests, and record the results.
- Write Team Memory notes on the conventions you found.
- Suggest a starter Task, such as a spec for the smallest open question.
