---
title: 'Spec-driven'
template: 'spec-driven@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'spec-approved-before-build']
retro: { every: 10, unit: 'done-tasks' }
checkCommand: 'dotnet test'
---
# Spec-driven

## When it applies

- Features and changes that touch shared contracts, the wire protocol or more than one module.
- Work where the team must agree on behaviour before anyone writes code.

## Stages

1. **Spec**: a spec Task holds the behaviour, the scope and the open questions. Done when the spec is complete enough to review.
2. **Approved**: the human approves the spec Task. Done when the approval is recorded on it.
3. **Break down**: the tech lead splits the spec into build Tasks, each with an acceptance line. Done when every build Task has one.
4. **Build**: the developer writes the change and its tests against the spec. Done when the build and the tests pass locally.
5. **Review and verify**: a reviewer reads the diff and QA posts an Evidence line. Done when both are recorded.
6. **Done**: the manager moves the build Task to Done. Done when every gate in Gates is met.

## Who does what

- `development-manager` drafts or assigns the spec, asks for approval and moves Tasks to Done.
- `tech-lead` writes the breakdown and answers design questions on the spec.
- `developer` builds the change against the approved spec and asks for review.
- `reviewer` reads the diff against the spec and records findings.
- `qa-engineer` verifies each acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager writes the spec and does the breakdown itself.
- No `reviewer`: the manager reviews the diff against the spec.
- No `qa-engineer`: the manager verifies each acceptance line and posts the Evidence line. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.
- `spec-approved-before-build`: Huddle refuses to move a build Task to In Progress until its parent spec Task is marked approved by the human.

## Definition of Done

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
- Propose a starter Task, such as a spec for the smallest open question.
