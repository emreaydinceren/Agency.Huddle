---
title: 'Sprints'
template: 'sprints@1.1.0'
gates: ['qa-evidence-before-done', 'checks-before-merge', 'accept-before-merge', 'blockers-done-before-start', 'one-in-progress-per-assignee']
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
3. **Ask for a review**: to ask for a review, the Task's owner (the developer) must create a review Task: tagged `review`, a child of the work Task, assigned to the reviewer, and adds it to the work Task's `blocked_by`. The reviewer reads the diff, runs the Team's check command on it and records the outcome on the review Task as an Evidence line with that command. The reviewer approves by moving the review Task to Done, or leaves it open with the findings as a comment; when the reviewer leaves findings, the owner fixes the work and comments on that same review Task that it is ready, and the reviewer reviews again; never create a second review Task for the same work. A review Task is never reopened. Done when the review Task is Done.
4. **Ask for a QA check**: once the review Task is Done, the owner creates a QA Task the same way: tagged `verify`, a child of the work Task, assigned to QA, added to the work Task's `blocked_by`. QA checks the acceptance line, posts an Evidence line on the QA Task and moves it to Done. When the acceptance names a web page or a UI, QA drives it in a browser (the browser-check Skill) and the Evidence line says what it did. When QA leaves findings, the owner fixes the work and comments on that same QA Task that it is ready, and QA checks again; never create a second QA Task for the same work. Done when the QA Task is Done. If the change gets a new commit after the review, the owner creates a new QA Task for that commit.
5. **Done**: the manager moves the Task to Done. Done when every gate in Gates is met. A Task whose work ends in a merge follows the `accept-before-merge` gate below.

## Who does what

- `development-manager` sets the sprint goal, chooses the Tasks, sends the daily digest and moves Tasks to Done.
- `developer` builds each Task, writes its tests and creates its review Task.
- `reviewer` reads the diff and comments the findings on the review Task.
- `qa-engineer` verifies each acceptance line on the QA Task and posts the Evidence line.
- `release-engineer` merges and pushes once the checks gate passes.

## If a role is not hired

- No `tech-lead`: the manager breaks each goal into Tasks itself.
- No `reviewer`: the manager owns the review Task and reviews the diff before it is merged, or assigns the review Task to a member whose role covers challenge, such as a `tech-lead` or a `security-engineer`. The Definition of Done's review item still needs that review Task to be Done.
- No `qa-engineer`: the manager owns the QA Task, verifies the acceptance line and posts the Evidence line on it. When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.
- No `release-engineer`: the developer merges after the checks pass.

## Gates

- `qa-evidence-before-done`: Huddle refuses to move a Task to Done until a member whose role covers verify posts an Evidence line, or a child Task tagged `verify` is Done.
- `checks-before-merge`: a merge or push needs an Evidence line that shows exit code 0 for the Team's check command, `dotnet test`, first, whatever runs it.
- `accept-before-merge`: a merge into main needs the Human's Accept, as the Team's or project's Needs You level says. Under Ask me (the default) put the merge in the Task's Action, for example `Merge feat/x into main`; Huddle moves the Task to Needs You when its Proof is complete, and the merge follows the Accept. Under Run inside the Team do not set a merge Action: when the Proof is complete, Huddle releases the Task and wakes its assignee, who merges its branch into the local main and then moves the Task to Done. The branch must be named after the Task id, for example `feat/DEV-0012-login`; a merge of any other branch needs the Human's Accept. Every push needs the Human's Accept at every level, whatever runs it (a script or a git alias too): put the push in the Task's Action, for example `Push feat/DEV-0012-login to origin`.
- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.
- `one-in-progress-per-assignee`: Huddle refuses to move a Task to In Progress, or to give an In Progress Task to someone, while its assignee already has another Task In Progress, and names that Task.

## Definition of Done

- Review: task tagged review
- QA evidence: task tagged verify
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
