---
status: accepted
date: 2026-10-08
---

# Process gates live in Process.md and are enforced in TaskService and the permission handler

A Team's **process** says how its work is done, and its **gates** are the few rules that
refuse a change until a condition is met. A Team lists its gates in its `Process.md`. The app
enforces them at the two places where the change happens: `TaskService` for a Task's status,
and `WorkDirPermissionHandler` for a `git merge` or `git push` from the Team's workspace. The
full design is in the [Teams specification](../engineering/teams-spec.md), under *Gates* and *Runtime behavior*.

## The problem

A process written as prose is a suggestion. An Agent that is told "do not mark a Task Done
without QA evidence" can forget it, and a Human who is in a hurry can skip it without a trace.
The Human wants a few rules that cannot be skipped by accident, that say which rule refused,
and that the Human can still override on purpose, with the override on record.

Two places already see every change. Every Task change goes through `TaskService`, which is
the only way to change a Task. Every shell command an Agent asks to run goes through
`WorkDirPermissionHandler`, which answers each permission request.

## The decision

**Four gates, named in the Team's `Process.md`.**

```markdown
---
title: 'Lightweight flow'
template: 'lightweight-flow@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge']
checkCommand: 'dotnet test Huddle.slnx --'
retro: { every: 10, unit: 'done-tasks' }
---
```

| Gate | Refuses | Met when |
| --- | --- | --- |
| `qa-evidence-before-done` | A move to Done | An Evidence line on the Task comes from a member whose role covers `verify` |
| `spec-approved-before-build` | A move to In Progress | The parent spec Task is approved. A Task with no parent passes |
| `failing-test-before-fix` | A bug moving to Review | Its Evidence shows a non-zero exit code, then a later zero exit code |
| `checks-before-merge` | `git merge` or `git push` from the Team's workspace | The latest Evidence line for the Team's `checkCommand` has exit code 0 |

`checks-before-merge` does nothing when `checkCommand` is absent. An unknown gate id is
ignored, and the file is still read.

**The Task gates are checked in `TaskService`.** `ProcessGateService.CheckTaskChange` runs on
the proposed status change, for the Team of both the current and the candidate location. The
first refusal is returned to the caller as `TaskResult.GateRefused`, and the message names the
gate. Every Task tool and the caller gets the refusal back, so a tool result or the Task panel shows the same gate and message.

**The merge gate is checked in `WorkDirPermissionHandler`.** It asks `IMergeGate`, which
`ProcessMergeGate` implements, whether the Persona's Team allows the command. The handler reads
only `git [options] merge` and `git [options] push`. The remarks say what it does not catch: a
shell or git alias, a command run by an IDE or by `gh`, and a command in an Adapter Work Mode
that never asks for permission. The UI says the gate works only in work modes that ask.

**The Human may override a Task gate, on purpose and on record.** `TaskService` lets a Human
change pass a refusing gate only when the caller says so, and the only such caller is a
confirmed "Move anyway" in the Task panel. An Agent never passes it. The override is written to
the Change log as `gate overridden by Human: <gate id>`, on a move and on a create. The merge
gate has no override in the app: the handler answers only an Agent's permission request, so
a merge the Human runs in a terminal is never refused by it.

## Rejected

| Alternative | Why not |
| --- | --- |
| Gates as instructions in the Agent's prompt | An Agent can ignore a sentence, and nothing records that it did. The code path cannot be skipped by a prompt |
| Gates in the Task panel only | Agents change Tasks through the same `TaskService` as the panel, so a panel-only gate lets an Agent around it |
| A gate for every process rule | Done acceptance is a process rule, not a hard gate (plan decision D3). Only four rules are hard, so the Human can still see why a Task is slow |
| A merge gate that reads the shell output of the check | The app does not run the check. It reads the Evidence line an Agent recorded with the exit code, so the record is what the gate trusts |

## Consequences

- **A gate refusal says which gate refused,** and the Change log says which gate was overridden.
  Nothing else in the app has to be told about a gate.
- **`checks-before-merge` is only as strong as the permission prompt.** In a work mode that
  does not ask, or through an alias, the merge is not refused. The UI says so, and the known
  gaps are listed in the handler's remarks.
- **Evidence is the proof for three of the four gates.** A gate is met by an Evidence line an
  Agent or a member wrote, so a false line passes. The Evidence is in the Task file, where the
  Human can read it.
- **Gates are read from `Process.md` on each check.** A Team that edits its gates sees the
  change at the next Task change, with no restart.
- **A Team's process can be copied from a type's process template.** The four gate ids are
  fixed in code. A template can choose which of them to enable, and cannot add a fifth.
