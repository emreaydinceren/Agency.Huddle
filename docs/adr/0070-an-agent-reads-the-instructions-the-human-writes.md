---
status: proposed
date: 2026-10-30
---

# An Agent reads the Instructions the Human writes

**Instructions** are standing rules the Human writes once. Every Agent in scope starts each
session with them in its system prompt. They are Huddle's version of a repository's `CLAUDE.md`
or `AGENTS.md`. The design is
[Huddle.InstructionsAndMemory-Specifications.md](../Huddle.InstructionsAndMemory-Specifications.md) §3.

## The problem

The Human has no place to say "how we work here" to more than one Agent:

- The Persona file is a job description, and editing it restarts the Agent.
- `Process.md` reaches only a Persona with a `_role`. Only the manager gets it in full, and only
  for the first Team in `Teams:`.

The Human's *Huddle Agent Memory Protocol* asked for its rules to be pasted into every Persona.
That is the duplication this record removes.

## The decision

- **Three levels, each a plain file in a Library Root:**
  - `Teams/Instructions.md` for every Agent;
  - `Teams/<Team>/Instructions.md` for every member of that Team;
  - `Agents/<Name>/work/Instructions.md` for one Agent.
- **Huddle injects them.** One Instructions part goes right after the Persona text, from
  general to specific; the more specific level wins. With no files, the part is absent.
- **The name is `Instructions.md`.** It is never `AGENTS.md` or `CLAUDE.md`, which Adapters
  load on their own and which Adapters-Vendors §6.3 plans to generate.
- **They are read at session start, like `Process.md`.** Team and Agent edits reach running
  sessions as File Changes where File Changes run. A Huddle-wide edit applies from the next
  session. No restart is forced.
- **Agents never write them.** `WorkDirPermissionHandler` refuses an `Edit`, `Delete` or `Move`
  of an `Instructions.md` at the Teams root, in a Team folder or in the Agent's own Work Dir.
  Reads are allowed.

## Rejected

| Alternative | Why not |
| --- | --- |
| Write a `CLAUDE.md` or `AGENTS.md` into each Work Dir for the Adapter to load | It differs per Adapter and is invisible to the prompt goldens. It is loaded twice when a parent folder has one, and it collides with the generated `AGENTS.md` |
| A Settings Prompt for the Huddle-wide level | It is not a plain file the Human can open in the Library or Obsidian, and it would be the first Prompt with an empty default |
| `{DataDir}/Instructions.md` or `Agents/Instructions.md` | `DataDir` is not a Library Root, and `PersonaStore` rejects loose files at the Agents root |
| `Agents/<Name>/Instructions.md` beside the Persona file | `PersonaStore` rejects a second top-level `.md` in a Persona folder. Allowing it would be a wider change |
| Restart every Agent in scope on an edit | It breaks the prompt cache and interrupts Turns, while a File Change already carries the news |

## Consequences

- **The Agent level weakens [ADR-0031](0031-agents-and-teams-are-sibling-folders.md).** That ADR
  keeps the definition out of the cwd so an ordinary `Write` cannot change it. `work/Instructions.md`
  is in the cwd, protected only by the soft guard. This is accepted.
- **The guard is soft.** It reads path arguments only, not shell redirects, like the existing
  `~/.claude` guard. Unlike that guard, it filters by tool kind, so reads pass.
- **Instructions can contradict the Persona.** The Prompt orders the levels of Instructions; it
  does not rank them against the Persona, which the Human also owns.
- **One question is still open.** Whether Claude Code also loads the repository's `CLAUDE.md`
  files into Personas in development is unverified (spec OP-5, spike S-1).
