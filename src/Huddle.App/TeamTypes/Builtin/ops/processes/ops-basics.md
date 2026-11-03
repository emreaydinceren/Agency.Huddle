---
title: 'Ops basics'
template: 'ops-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Ops basics

## Identity

This Team keeps a live system running. It diagnoses incidents, runs the agreed mitigations, keeps the timeline and drafts the blameless review. Work routes here when something is broken or alerting and needs an owner, a timeline and an Evidence line before it closes. It does not build new features or plan the roadmap, which belong to a development or product Team, and it does not change a live system without a reversible step the Human runs.

## When it applies

- Incidents, alerts and the follow-up work after them: diagnosis, mitigation, updates and review.
- Each incident has one Task that holds its timeline, its decisions and its evidence.

## Stages

1. **Ready**: the incident Task has a symptom, a severity and an owner. Done when the incident commander assigns it.
2. **In progress**: the SRE diagnoses the cause and writes exact, reversible steps for the Human to run, the Scribe keeps the timeline, and the Comms Lead drafts updates for the Human to send. Done when the mitigation has been run and its result is on the Task.
3. **Verify**: the incident commander confirms from the evidence that the symptom is gone, the timeline is complete and the follow-ups are written down, and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

## Who does what

- `incident-commander` sets severity, assigns the work, decides the next step and posts the Evidence line.
- `sre` diagnoses from logs and metrics, writes the steps to run and updates the runbook afterwards.
- `scribe` keeps the timeline and drafts the blameless review.
- `comms-lead` drafts status updates and customer notes for the Human to send.

## If a role is not hired

- No `sre`: the incident commander proposes the steps, and the Human runs them.
- No `scribe`: the incident commander keeps the timeline on the Task.
- No `comms-lead`: the incident commander drafts the updates.

## Gates

- This process declares no hard gate. The incident commander's Evidence line is the check before an incident is closed, and the Human closes each Task.

## Definition of Done

- Fix verified: evidence from verify
- Write-up reviewed: document reviewed by verify

## Cadence

- Mitigate first, find the root cause second.
- Post an update at the interval the severity sets, even when nothing has changed.
- Draft the review within two days of an incident.
- A retro runs every 14 days.

## Escalation

- Every change to a live system, every rollback and every message to customers goes to the Human.
- A risky step goes to the incident commander before it is proposed to the Human.

## Onboarding

- Read the runbooks folder and note which systems each runbook covers.
- Write Team Memory notes on the severity levels, the update intervals and who to reach.
- Propose a starter Task, such as reviewing one runbook for gaps.
