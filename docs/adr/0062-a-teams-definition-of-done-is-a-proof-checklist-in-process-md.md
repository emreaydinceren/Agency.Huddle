---
status: accepted
date: 2026-10-19
---

# A Team's Definition of Done is a Proof checklist in Process.md

A Human-gated Task ([ADR-0061](0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md))
reaches the Human only when its **Proof** is complete: every item of its Team's **Definition of
Done** is met. A Team writes its Definition of Done as a short list in its `Process.md`. Each item
is met by Evidence lines, a passing check, a finished sub-Task or a reviewed document, never by
someone saying so.
The full design is [Huddle.HumanGate-Specifications.md](../Huddle.HumanGate-Specifications.md).

## The problem

The Human should spend their time deciding, not checking. "Proof before people" needs a list of
what proof means for this Team: QA evidence for a software team, a Skeptic review and cited
sources for a research team. ADR-0039 fixed the gate ids at four and said a template cannot add a
fifth, so a research team has no way to say "every claim cites a source".

## The decision

**A `## Definition of Done` section in the body of `Process.md`, one item per bullet.**

```markdown
## Definition of Done

- QA evidence: evidence from verify
- Citation check: task tagged citation-check
- Tests pass: command `dotnet test Huddle.slnx --` exits 0
- Skeptic review: document reviewed by verify
```

Each bullet is `- <Label>: <rule>`, and a rule is one of four:

| Rule | Met when | Open item shows |
| --- | --- | --- |
| `evidence from <step>` | An Evidence line on the Task, with a command, by a member whose role covers `<step>` (the same test as `qa-evidence-before-done`) | The first Team member whose role covers `<step>` |
| `command `<cmd>` exits 0` | The latest Evidence line on the Task for `<cmd>` has exit code 0 | The Task's assignee |
| `task tagged <tag>` | A child Task (its `parent` is this Task) tagged `<tag>` exists and is Done | The child's assignee, and its checked boxes against all boxes, for example "19 of 23 in #RSH-209"; "no Task yet" when there is none |
| `document reviewed by <step>` | A Markdown document attached to the Task (a path line in its description) has, in its document history ([ADR-0060](0060-a-documents-history-is-what-its-editors-report.md)), a `Reviewed` entry by a member whose role covers `<step>`, later than its latest `Created` or `Edited` entry, with no `Objected` entry after it | "no document attached", "not reviewed yet", "edited after Ruth's review" or "Ruth objected: \<summary>"; the owner is the first member covering `<step>` |

A document edited after its review no longer meets the item, the same way an edit voids the
Human's acceptance. Document history is what its editors report, and an edit made outside Huddle
is not recorded (ADR-0060), so this rule trusts the history as the others trust Evidence lines.

**The ADR-0039 gates that guard a move to Done join the checklist on their own.** When
`qa-evidence-before-done` is enabled, the checklist gains "QA evidence" unless an item already
says `evidence from verify`. `failing-test-before-fix` joins for bugs. The other two gates guard
other moves and stay as they are.

**The section is a body section, not frontmatter.** The `Process.md` frontmatter reader is flat
`key: value`, the Human edits this list in the Library like any prose, and a body heading
survives a retro's exact-text edit. `ProcessDocument` parses it into a list of items.

**Proof is enforced only on Human-gated Tasks.** On those, an incomplete checklist refuses the
move into Needs You and locks Accept, and nothing overrides it. On every other Task the checklist
is shown in the Task panel for information and refuses nothing, which keeps ADR-0039's plan
decision D3 ("Done acceptance is a process rule, not a hard gate") for ordinary work.

**A line that cannot be read counts as open.** The Task panel shows the line as written with
"Huddle can't read this rule". A Team with no `Process.md`, a Task outside any Team, or a section
with no items has a checklist of zero items, which is complete: the Human still decides.

**Proof is the checklist; Evidence is a line.** "Proof 3/4" counts items. An item is met by
Evidence, a check, a sub-Task or a reviewed document. The glossary in `language.md` says so.

## Rejected

| Alternative | Why not |
| --- | --- |
| Only the four ADR-0039 gates | A research or legal Team could not ask for a Skeptic review or cited sources |
| A YAML list in the frontmatter | The reader is flat `key: value`, and a nested list would be the only one in the file |
| A per-Task checklist | Every Task would need setting up, and the Team's standard would drift Task by Task. A Task that needs more creates a tagged sub-Task |
| Items met by a member ticking a box | A tick is a claim. Every rule points at a record someone can open: an Evidence line, a check run, a Done Task, a document's history |
| Free-text items read by an Agent | The gate would rest on a model's reading. The four rules are decided by code |
| A document rule met when any document is attached | Attaching a file proves nothing was checked. The rule asks for a review after the last edit |

## Consequences

- **A Team can add Proof but not lower the Human's bar.** Removing every item still leaves the
  Human's acceptance.
- **ADR-0039's "a template cannot add a fifth gate" still holds.** A Definition of Done item is
  not a gate id. It refuses nothing on ordinary Tasks.
- **Team-type process templates gain a Definition of Done section.** A Team copied from an older
  template has none until its Human or a Retro adds one.
- **A Retro may propose a Definition of Done change** as an exact-text edit to `Process.md`, the
  same way it changes the rest of the file.
