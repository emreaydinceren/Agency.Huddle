---
title: 'Home lab basics'
template: 'home-lab-basics@1.3.0'
gates: ['blockers-done-before-start', 'one-in-progress-per-assignee']
retro: { every: 14, unit: 'days' }
---
# Home lab basics

## Identity

This Team keeps a home lab as code in one repository: its configuration, scripts and runbook. Work that arrives here is a change to that repository, with the steps to check it and to undo it, or a review of a change for access, firewall or secrets. The Human applies every change to a live machine, and general support for a household's devices belongs to the household Team type.

## When it applies

- Changes to a home lab kept as code in one repository: configuration, scripts and the runbook.
- The team writes and reviews changes. The Human applies them to live machines.

## Stages

1. **Ready**: the Task names the change, how to check it and how to undo it, and has an owner. Done when the IT admin assigns it.
2. **In progress**: the automation engineer writes the change and its run, check and undo steps in the repository. Done when the change is on a branch.
3. **Verify**: the security engineer and the docs tester review, and the IT admin checks the change against the Task and posts an Evidence line. Done when that line exists.
4. **Done**: the Human applies the change, then marks the Task Done.

## Who does what

- `it-admin` keeps the inventory and runbook, checks each change and posts the Evidence line.
- `automation-engineer` writes scripts and configuration with run, check and undo steps.
- `security-engineer` reviews anything touching access, the firewall or secrets.
- `docs-tester` follows the runbook as written and reports every step that fails.

## If a role is not hired

- No `automation-engineer`: the IT admin writes the change.
- No `security-engineer`: the IT admin lists the access and secret changes for the Human to check.
- No `docs-tester`: the IT admin reads the runbook step by step before the Evidence line.

## Gates

- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.
- `one-in-progress-per-assignee`: Huddle refuses to move a Task to In Progress, or to give an In Progress Task to someone, while its assignee already has another Task In Progress, and names that Task.
- This process declares no other hard gate. The IT admin's Evidence line is the check, and the Human applies every change.

## Definition of Done

- Change verified: evidence from verify
- Backup and rollback noted: document reviewed by verify

## Cadence

- One change in progress at a time, and every change has an undo step.
- A retro runs every 14 days.

## Escalation

- Anything that exposes a service to the internet, touches secrets or could lock the Human out goes to the Human.
- A disagreement between the engineer and a reviewer goes to the IT admin, then to the Human.

## Onboarding

- Read the repository and note its layout, inventory and runbook.
- Write Team Memory notes on the machines, the network and where secrets live outside the repository.
- Suggest a starter Task, such as an inventory of what runs where.
