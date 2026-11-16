---
title: 'Sprints'
template: 'sprints@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'accept-before-merge', 'blockers-done-before-start']
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

1. **Plan**: the manager sets the sprint goal and chooses the Tasks. Done when each Task has an acceptance line and an assignee. A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it before then.
2. **In progress**: the developer works the Task and its tests. Done when the build and the tests pass locally.
3. **Review**: a reviewer reads the diff, runs the Team's check command on it and records the outcome on the Task as an Evidence line with that command. Done when no open finding remains.
4. **Verify**: QA starts only after the review has no open finding. QA checks the acceptance line and posts an Evidence line. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill) and the Evidence line says what it did. Done when that line exists. If the change gets a new commit after the review, QA runs the verify again on that commit.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` sets the sprint goal, chooses the Tasks, sends the daily digest and moves Tasks to Done.
- `developer` builds each Task, writes its tests and asks for review.
- `reviewer` reads the diff and records findings on the Task.
- `qa-engineer` verifies each acceptance line and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager breaks each goal into Tasks itself.
- No `reviewer`: the manager reviews the diff before it is merged. The Definition of Done's Review item still needs an Evidence line from a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`.
- No `qa-engineer`: the manager verifies the acceptance line and posts the Evidence line. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line.
- `checks-before-merge`: Huddle refuses a merge or push until an Evidence line shows exit code 0 for the Team's check command, `dotnet test`.
- `accept-before-merge`: a merge into main waits for the Human, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete in Review, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch waits for the Human. Every push waits for the Human's Accept at every level: put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Review, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.

## Definition of Done

- Review: evidence from challenge
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
