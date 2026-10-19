---
title: 'Home lab basics'
template: 'home-lab-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Home lab basics

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

- This process declares no hard gate. The IT admin's Evidence line is the check, and the Human applies every change.

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
- Propose a starter Task, such as an inventory of what runs where.
