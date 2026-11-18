---
title: 'Product basics'
template: 'product-basics@1.1.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Product basics

## Identity

This Team decides what to build next and writes each problem down well enough for engineers to start. Work routes here when a Task needs a problem, a user, a success measure and an out-of-scope list before anyone builds. It does not write or ship the code, which belongs to a development Team, and it does not run outreach to buyers, which belongs to a sales-outreach Team.

## When it applies

- Deciding what to build next and writing it down well enough for engineers to start.
- Each Task names one problem, who has it and how you will know it is solved.

## Stages

1. **Ready**: the Task states the problem, the user, the success measure and what is out of scope. Done when the product manager assigns it.
2. **In progress**: the UX Researcher gathers the evidence, and the Designer writes the flow and screens beside the spec in the repository. Done when the spec, design and research are attached to the Task.
3. **Verify**: the Tech Lead checks the spec for feasibility, and the product manager reads it against the problem and the research and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line, and decides whether it ships.

## Who does what

- `product-manager` defines the problem, orders the work, checks the spec and posts the Evidence line.
- `ux-researcher` plans research for the Human to run and turns the notes into findings.
- `designer` writes the user flow, the screens and their states as text and simple wireframes.
- `tech-lead` checks the spec for feasibility and raises open technical questions.

## If a role is not hired

- No `ux-researcher`: the product manager states which assumptions are untested on the Task.
- No `designer`: the product manager writes the flow in the spec as numbered steps.
- No `tech-lead`: the product manager lists the technical questions for the Human's engineers.

## Gates

- This process declares no hard gate. The product manager's Evidence line is the check before a spec is ready, and the Human closes each Task.

## Definition of Done

- Assumptions checked: evidence from verify
- Spec reviewed: document reviewed by verify

## Cadence

- Define one problem at a time, and research before you design.
- A retro runs every 14 days.

## Escalation

- A date, a price, a customer promise or a change of priority goes to the Human.
- A disagreement between the Designer and the Tech Lead goes to the product manager, then to the Human with both views on the Task.

## Onboarding

- Read the repository and note its specs, designs and open issues.
- Write Team Memory notes on the product's users, goals and constraints.
- Suggest a starter Task, such as defining the smallest open problem.
