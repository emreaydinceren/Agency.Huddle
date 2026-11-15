---
status: accepted
date: 2026-11-14
---

# A Task is a description plus an immutable timeline

In the 2026-11-14 run for #537, the manager of the Calc Team called `update_task` with a new `description` on CALC-0001.
The Task's body was its description, and the `## Evidence` section lived inside the body, so the write replaced both.
Ana's and Tomas's Evidence lines were gone while the Change log still said `evidence added by Ana`, and Proof dropped from
complete to 1/3. `TaskFileFormat` had said since Tasks were built that "`TaskItem.Description` is the whole body for now".
Issue #586 records the run and the owner's decisions.

## Decision

A Task has a **description** and a **timeline**, like an issue in a tracker.

1. **The description is the only editable text.** `update_task` with `description`, the Task view's Save and
   `TaskService.Update` change the description and nothing else.
2. **Evidence is a comment tagged `evidence`.** Proof, the process gates and `checks-before-merge` read only those
   entries. `add_evidence` keeps its name and arguments (`note`, or `command` with `exitCode`). A new tool,
   `add_comment`, adds an untagged comment. Both are offered to every Agent, as `add_evidence` already was: no Skill
   grants either.
3. **Entries are immutable.** No tool, control or `TaskService` method edits or deletes a comment, an Evidence entry
   or an event, the Human's included. Move anyway stays the Human's override for a gated Task.
4. **The Change log joins the timeline as events.** Only `TaskService` writes events. Callers write comments and
   Evidence.

### The file

The body is the description, then a `## Timeline` section of one line per entry:

```text
- 2026-11-14T04:03:57Z | Joe | event | created
- 2026-11-14T04:04:02Z | Ana | evidence | dotnet test Calc.slnx | exit 0 | .huddle/logs/ana/tool-calls.md#L12
- 2026-11-14T04:05:00Z | Tomas | comment | Looks good.\nOne nit: rename Parse.
```

The grammar, the escaping and every reader and writer are in the
[Tasks spec §7.7](../Huddle.Tasks-Specifications.md#77-the-timeline). A description write recomposes the frontmatter
and description and keeps every byte from `## Timeline` onward, so it cannot reach an entry.

### Files written before the timeline

They stay valid with no migration step. A body with no `## Timeline` is read the old way: `## Change log` lines are
events, the `## Evidence` lines are Evidence entries read by the same rule Proof used, and the description is the body
without its Evidence sections by the rule the acceptance fingerprint used. So Proof and a recorded acceptance read the
same before and after. The first write by Huddle rewrites the body in the new form, copying the Change log lines
verbatim.

### Settled where the brief left it open

The owner asked for the smallest choice that keeps old files readable. These were chosen that way:

| Detail | Choice |
| --- | --- |
| Separate events for Evidence and comments | None. The entry is the record; the in-memory `TaskChange` still carries `evidence added by {actor}` or `comment added by {actor}`, so wakes and feeds read as before. `Updated` and startup reconciliation read the last timeline entry |
| `TaskItem` shape | `ChangeLog` keeps its type and holds the events, so `Acceptance`, `ClosedAt` and the feed rules are unchanged. `Comments` holds comments and Evidence; `Timeline` merges the two |
| A description with a reserved heading | `## Timeline`, `## Change log` and `## Evidence` headings outside a fence are refused, so no description can look like Evidence |
| `get_task` | Comments and Evidence always follow the description; `include_change_log: true` adds the events |
| A comment's size | 1 to 4000 characters after trimming |

## Rejected

| Alternative | Why not |
| --- | --- |
| Keeping `## Evidence` in the description and writing it back on every description edit (the issue's first proposal) | Every writer of the body has to remember it, including the form and outside-edit paths; the next new writer repeats #586 |
| A one-off migration step that rewrites every Task file at start-up | The owner ruled it out; a lazy rewrite on first write needs no marker and touches only files that change |
| Keeping old files in the old layout and appending new entries under `## Change log` | Two layouts forever, and a heading named for the Change log holding comments |
| Editing or deleting a comment, for the Human only | The owner ruled it out for now: Evidence is what Proof stands on, and an edit would change Proof after the fact |

## Consequences

- A description edit, by any route, can no longer drop Evidence or change Proof (#586).
- The Task view shows the description, then the timeline: comments, Evidence with its command and exit code, and
  events, in time order, with a box for the Human's comment.
- An old Task file is rewritten the first time Huddle changes it. Its Proof, its acceptance and its history read the
  same.
- An editor can still change a timeline line on disk; Huddle reads the file as written
  ([known limits](../engineering/known-limits.md)).

Supersedes the note in `TaskFileFormat.TryParse` that the description is the whole body.
[ADR-0061](0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md) and
[ADR-0062](0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md) read Evidence through this model.
