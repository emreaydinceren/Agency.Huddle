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
5. **Review and verify**: a reviewer reads the diff and QA posts an Evidence line. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill). Done when both are recorded.
6. **Done**: the manager moves the build Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge names that merge as its Action, such as `Merge feat/x into main`, so the Human's Accept leads to the merge.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `development-manager` drafts or assigns the spec, asks for approval and moves Tasks to Done.
- `tech-lead` writes the breakdown and answers design questions on the spec.
- `developer` builds the change against the approved spec and asks for review.
- `reviewer` reads the diff against the spec and records findings.
- `qa-engineer` verifies each acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the manager reviews the diff against the spec. The Definition of Done's Review item still needs an Evidence line from a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`.
- No `qa-engineer`: the manager verifies each acceptance line and posts the Evidence line. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.
- `accept-before-merge`: Huddle refuses a merge into main, whoever runs it, until a Task of the Team has the Human's Accept. Put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept.
- `spec-approved-before-build`: Huddle refuses to move a build Task to In Progress until its parent spec Task is marked approved by the human.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Review, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: evidence from challenge
- QA evidence: evidence from verify
- Tests pass: command `dotnet test` exits 0
- Spec reviewed: document reviewed by verify

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
