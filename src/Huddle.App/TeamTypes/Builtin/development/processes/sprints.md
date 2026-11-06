---
title: 'Sprints'
template: 'sprints@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'accept-before-merge']
retro: { every: 10, unit: 'done-tasks' }
digest: { every: 1, unit: 'days' }
checkCommand: 'dotnet test'
---
# Sprints

## Identity

This Team delivers software in fixed sprints, as the sprints process of the `development` Team type: the manager sets the goal and the scope at the start, and the scope does not change for the stretch. It suits a team that wants a daily digest and a shared view of what is in flight. Work with no fixed goal or stretch goes to the lightweight-flow process, and a change that needs an agreed spec goes to spec-driven.

## When it applies

- Work that the team plans around a goal for a fixed stretch, with the scope set at the start.
- Teams that want a daily rhythm and a shared view of what is in flight.

## Stages

1. **Plan**: the manager sets the sprint goal and chooses the Tasks. Done when each Task has an acceptance line and an assignee.
2. **In progress**: the developer works the Task and its tests. Done when the build and the tests pass locally.
3. **Review**: a reviewer reads the diff. Done when no open finding remains.
4. **Verify**: QA checks the acceptance line and posts an Evidence line. Done when that line exists.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge names that merge as its Action, such as `Merge feat/x into main`, so the Human's Accept leads to the merge.

## Who does what

- `development-manager` sets the sprint goal, chooses the Tasks, sends the daily digest and moves Tasks to Done.
- `developer` builds each Task, writes its tests and asks for review.
- `reviewer` reads the diff and records findings on the Task.
- `qa-engineer` verifies each acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager breaks each goal into Tasks itself.
- No `reviewer`: the manager reviews the diff before it is merged.
- No `qa-engineer`: the manager verifies the acceptance line and posts the Evidence line. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.
- `accept-before-merge`: Huddle refuses a merge into main, whoever runs it, until a Task of the Team has the Human's Accept. Put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept.

## Definition of Done

- QA evidence: evidence from verify
- Tests pass: command `dotnet test` exits 0

## Cadence

- Keep the sprint goal fixed for the stretch. Add a Task only by swapping out another one.
- The digest runs every day, and a retro runs every 10 done Tasks, because a sprint has no dates.

## Escalation

- A goal that cannot be met in the sprint, or a priority clash, goes to the Chief of Staff.
- If the Chief of Staff cannot settle it, it goes to the Human.

## Onboarding

- Read the workspace and the current sprint goal.
- Run the build and the tests, and record the results.
- Write Team Memory notes on the conventions you found.
- Suggest a starter Task that fits the sprint goal.
