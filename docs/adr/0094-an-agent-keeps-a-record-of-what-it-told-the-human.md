---
status: accepted
date: 2026-11-16
---

# An Agent keeps a record of what it told the Human

An Agent that reports on a schedule, or in several Rooms, says the same thing again: a Job that checks a status every
morning repeats an unchanged item in full, and the Chief of Staff's digest repeats what a manager told the Human
directly. No Agent can know what it has already said in another Room, because its context is per Room (issue #620).

This record amends [ADR-0071](0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md) only
in that the `memory` tool now writes the lines of one file, `reported.md`, in an Agent's own Memory. Who may write which
folder, and the rule that an Agent deletes a file only when the Human asks, are unchanged. The design is in
[Huddle.MemoryTidy-Specifications.md](../Huddle.MemoryTidy-Specifications.md); this record is its slice 1.

## Decision

**Every Agent keeps `reported.md` in its own Memory: one line for each thing it told the Human. The `memory` tool writes
the date, starts the file and drops old lines. The Chief of Staff can read a manager's file, and nothing else of that
Agent.**

| | Decision |
| --- | --- |
| Where the rule lives | One paragraph of `systemPrompt.memory`, so it holds in every Room on every Turn (spec D-1). Not a Skill, and not the Chief of Staff alone |
| The name | A known name, special only in an Agent's own Memory, never refused by the tool (spec D-2). In a Team's, a Project's or Huddle-wide Memory a `reported.md` is an ordinary topic file |
| Who writes the lines | The tool, on `append` to the caller's own `reported.md` (spec D-3). It starts the file with a fixed description, always sets the date (a date the Agent writes is replaced), refuses several lines, drops lines older than `Team:Memory:Reported:KeepDays` (14; zero or less keeps all) and, at `Team:Memory:MaxFileBytes`, drops the oldest dated lines to make room |
| The line | `- yyyy-MM-dd · <Room> · <what>`. A line Huddle cannot read is kept, because the Human may write in the file. Age is counted in the Human's local calendar days (spec D-4). The rule is one class, `ReportedFile`, so the later weekly pass uses the same code |
| What counts as a report | Something the Agent brings to the Human. A direct answer to a question just asked is not one. An item already reported and unchanged is said in one line as "no change since" the date, never left out, and adds no line (spec D-6) |
| Who reads it | The Agent, before it reports. The Chief of Staff, through `folder: agent:<Name>`, for a Team manager's file only, by read or list. The value never builds a `MemoryFolder`, so no write can reach another Agent (spec D-5). A manager reading its members' files was not asked for |
| What the Chief of Staff does with it | Reads the manager's `reported.md` before summarising a digest, and leaves out what the manager already told the Human. The text is in `Builtin/chief-of-staff.md` and the `team-rollup` Skill |

## Why

- **The Agent is the one that knows what it said.** Huddle writing the file from Messages was rejected: Huddle cannot
  tell a report from an answer, and it would be writing into the Agent's Memory behind its back.
- **The tool, not the Agent, holds the mechanics.** No Agent is given the date, `append` refused a missing file, and a
  line in free form cannot be aged. With the tool setting the date and dropping old lines, the Agent's part is one
  sentence of what it said.
- **A short life keeps it cheap.** Fourteen days is enough for a weekly Job and a digest, and a file the size cap can
  grow into is made room in rather than refused.
- **One reader outside the owner, with one door.** The Chief of Staff already sees every Team. A single-file read of a
  manager's file gives it the one thing the digest needs, and cannot grow into access to a manager's other Memory.
- **No new place to keep it.** It is a topic file in the Memory the Agent already has, listed in its index, and the Human
  can read and edit it in the Library.

## Rejected

| Alternative | Why not |
| --- | --- |
| A paragraph in the Chief of Staff's Persona only | The gap is every Agent's, not only the Chief of Staff's |
| A Skill | The rule must hold on every Turn, not when a Skill is read |
| Reserving the name, as `MEMORY.md` is reserved | In code that means refusing every command on it, and the Agent could not keep the file |
| A code guard on `delete` | Huddle cannot tell from a call whether the Human asked |
| Keeping the date the Agent wrote | A wrong or future date is never dropped, and the age rule would not hold |
| A table or JSON | Harder for a model to append to and for the Human to read |
| Recording every statement | Two tool calls on nearly every Turn of a one-to-one Room |
| Going silent on an unchanged item | A status Job would look broken |
| A folder value for another Agent | It is writable by default; a single-file read is not |
| Copying the manager's lines into the digest wake-up | Huddle would be writing into a Message |

## Consequences

- **The Memory Prompt grows by one paragraph** and takes an optional `{{reportedDays}}`. Every golden that shows the
  Memory block changes once.
- **The `memory` tool's `append` has a special case** for the caller's own `reported.md`, and its reply can end with
  `; dropped N lines older than 14 days` or `; dropped N of the oldest lines to make room`.
- **`Team:Memory:Reported:KeepDays`** is a new key, `14`.
- **Three new Prompts** carry the refusals: `systemPrompt.memoryAgentFolderRefusal`, `systemPrompt.memoryReportedNone` and
  `systemPrompt.memoryReportedNoManager`; and two carry the file: `systemPrompt.memoryReportedDescription` and
  `systemPrompt.memoryReportedOneLine`.
- **The record is the Agent's word.** A report it forgot to note can repeat; two Rooms of one Agent can report the same
  thing at once; the Room in a line is whatever the Agent wrote. These are in
  [known-limits.md](../engineering/known-limits.md).
- In [language.md](../engineering/language.md), **Reported file** is added.
- The weekly pass that drops old lines, and the Helper that tidies the rest of an Agent's Memory, are later slices of the
  same spec and get their own record.
