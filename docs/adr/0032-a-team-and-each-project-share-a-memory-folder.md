---
status: accepted
date: 2026-09-28
---

# A Team and each Project share a memory folder

A Team folder and each of its Project folders may hold a **`memory/`** folder:
`Teams/<Team>/memory/` for facts about the whole Team, and `Teams/<Team>/<Project>/memory/` for
facts about one Project. It uses the same convention as a Teammate's own Memory: one Markdown file
per fact, with the fact on the first line. Every member of the Team gets an index of it in its
system prompt at session start, and later edits through File Changes. `memory` becomes a reserved
name at the Project level. The full design is
[Huddle.TeamPages-Specifications.md](../Huddle.TeamPages-Specifications.md) (§6.3, §6.4).

## The problem

A Project scopes documents, notes and memory for one effort by the Team. Documents and notes
already have a home: the Project folder, which every member watches (ADR-0030). Memory does not.
Memory is per Teammate (`Teammates/<Name>/work/memory/`,
[ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md)). So a decision Nova reaches
with the Human is in Nova's starting context and nobody else's. Ada learns it only if she happens
to be in a Room where it is mentioned.

## The decision

- **Two levels:** Team-wide `Teams/<Team>/memory/` and per-Project
  `Teams/<Team>/<Project>/memory/`. There is no per-Teammate-per-Project variant.
- **The same format as personal Memory,** parsed by the same `MemoryIndex`. The Agent writes it
  with its own file tools, and the Human can edit it in the Library.
- **Every member is told at session start.** A *Team Memory* block follows the personal Memory
  block, grouped by Team and Project. It is capped by `Team:Teams:MaxMemoryEntries` (50) across
  all of a Persona's Teams. It is omitted for an Adapter that can't read files.
- **Changes after that arrive as File Changes.** The Team folder is already an implicit Watched
  Folder for its members, and `memory/` is not `_`-prefixed, so it is not pruned.
- **`memory` is reserved as a Project name,** ignoring case, in one function
  (`TeamNames.IsReservedProjectName`) that Tasks, the Library and Team pages all call. It is not
  reserved as a Team name.

## Rejected

| Alternative | Why not |
| --- | --- |
| `_memory/`, reserved by its prefix | `_` folders under Teams are hidden in the Library and pruned from File Changes. It would need two exceptions to the rule `_tasks/` relies on. The Human also wanted the name Teammates already use |
| No prompt listing: Project notes are memory enough | A note is found only if the Agent goes looking. The index is what makes Memory reach a new session without a lookup |
| A per-Teammate, per-Project folder inside each Work Dir | One fact would be stored once per member and drift. The Human asked for memory the Team shares |
| Reuse `FileChanges:MaxMemoryEntries` for the cap | A busy Team would crowd a Teammate's own Memory out of its prompt |

## Consequences

- **A Team folder can no longer have a Project called `memory`.** An existing
  `Teams/<Team>/memory/_tasks/` stops being read as Tasks. `TaskStore` logs a Warning naming the
  files at start-up, and Huddle never moves them itself.
- **A Team label reaches the system prompt.** A Team is still never a permission, but it now
  changes what a member is told at session start. That is one more reason a membership change
  restarts the Teammate.
- **Memory has two owners now.** [language.md](../engineering/language.md) keeps **Memory** for the
  Agent's own, and adds **Team Memory** for this.
