---
title: 'Open source maintainers basics'
template: 'oss-maintainers-basics@1.0.0'
gates: []
retro: { every: 14, unit: 'days' }
---
# Open source maintainers basics

## Identity

This Team runs one open-source repository. It triages issues, builds and reviews changes and prepares releases for the Human to approve. Work routes here when an issue, a contributor request or a release needs a Task with an acceptance line in that repository. It does not take security reports, licence questions or code of conduct matters, which the Human handles privately, and it does not plan product direction, which belongs to a product Team.

## When it applies

- Running an open-source repository: triaging issues, building and reviewing changes, releasing and talking to contributors.
- Each Task is one issue, change or release, with its acceptance in one line.

## Stages

1. **Ready**: the Issue Triager has labelled the issue and confirmed the repro, and the Maintainer has accepted it with an acceptance line. Done when it is assigned.
2. **In progress**: the Maintainer or the Developer builds the change and its tests. Done when the build and tests pass locally.
3. **Verify**: the Reviewer reads the diff against the issue and the tests, and the Maintainer posts an Evidence line with the check command and its result. Done when both are recorded.
4. **Done**: the Human merges, or approves the Release Engineer's merge, and marks the Task Done.

A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.

## Who does what

- `maintainer` accepts issues, builds or assigns the changes, plans releases and posts the Evidence line.
- `developer` builds the changes the Maintainer assigns, with their tests.
- `issue-triager` labels issues, finds duplicates and drafts requests for missing details.
- `reviewer` reads each change against its issue and approves or bounces it.
- `release-engineer` merges and tags a release after the checks pass.
- `community-manager` drafts release notes, announcements and replies for a maintainer to post.

## If a role is not hired

- No `developer`: the Maintainer builds every change.
- No `issue-triager` or `community-manager`: the Maintainer does that work itself.
- No `release-engineer`: the Human merges and tags.
- The Reviewer is required, because no one reviews their own change.

## Gates

- This process declares no hard gate, because the check command differs per project. The Reviewer's approval and the Maintainer's Evidence line are the check, and the Human closes each Task.

## Definition of Done

- Change verified: evidence from verify
- Change reviewed: document reviewed by verify

## Cadence

- Triage new issues at least weekly. Work accepted Tasks by label priority.
- A retro runs every 14 days.

## Escalation

- A security report, a licence question or a code of conduct report goes to the Human privately.
- A disagreement between the builder and the Reviewer goes to the Human with both views written on the Task.

## Onboarding

- Note the repository, its check command, its labels and its release steps.
- Write Team Memory notes on the contribution guide, the code of conduct and the project's voice.
- Suggest a starter Task.