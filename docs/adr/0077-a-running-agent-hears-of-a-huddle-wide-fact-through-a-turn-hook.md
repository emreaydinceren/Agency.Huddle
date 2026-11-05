---
status: accepted
date: 2026-11-05
---

# A running Agent hears of a Huddle-wide fact through a Turn Hook

A new, changed or deleted file directly in `Teams/memory` reaches each running Agent at its next Turn in each Room. The
block lists the file name and, when the file has one, its one-line `description`. It says the fact holds for every Team and
every Room, and tells the Agent to read the file if it bears on its work. A deleted file is reported as no longer true.

This record extends [ADR-0076](0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md) to Huddle-wide
Memory. It exists because of #361 and #344: in ZERO-18 a running Agent learned a Huddle-wide fact only at its next session.

## Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| M-1 | One small `HuddleMemoryTurnHook` on the pipeline, between AGENTS.md and File Changes | Widening `FileChangeTracker.ResolveFolders`: the Teams root also holds every Team's folder, so watching it would report other Teams' files, and the tracker would still need a description, a deleted rule and a skip for sessions that opened later |
| M-2 | No Helper: name plus description | A Helper summary: a Memory file already has a one-line description, and only the Chief of Staff writes the folder |
| M-3 | State is a hash map per file name, `started` and `heard`, in the session's `HookState`, as AGENTS.md does | File Changes' per-Room store: it is absent when File Changes is off. The map needs no table and survives a restart with the session |
| M-4 | A fresh session snapshots the folder, so a session that opened after the change hears nothing | Comparing times: clocks and edits made while closed |
| M-5 | At most 20 files per block, then a "more" line; the files left out are told at the next Turn, not lost | Dropping them, as File Changes does |
| M-6 | Before always records the folder; After marks the files the Agent itself touched as heard | Telling the Chief of Staff of its own write at its next Turn |

Only `*.md` files directly in `Teams/memory` count. A subfolder, the index file, a file at the Teams root, another folder at
the Teams root and every Team's own `memory` are never reported. A link (reparse point) is skipped, as
`TeamMemoryIndex.IsSkipped` skips it. A folder that cannot be listed says nothing, and a file that cannot be read keeps its
last heard hash, so neither is reported as gone. A failure of the hook never fails the Turn (ADR-0076, pipeline).

A compacted session hears again what changed since it started, as for AGENTS.md.

The description is the file's own words, which a Human or a file tool may write without the Memory Tool's checks. The block
therefore shows it as one line (line breaks, tabs and runs of spaces become one space, a double quote becomes a single one),
cut to `MemoryFileRules.MaxDescriptionLength`, and fenced as `The file says: "..."`, as the AGENTS.md notice fences a quote. A
linked Teams root skips the whole folder, as `MemoryPaths` does. A name ending `.MD` counts on every file system.

Known small cost: the own-write signal (`Touched`) comes from `PersonaSupervisor`'s `StartPlan.MemoryPaths`, which is
`tracker?.MemoryPaths`, so with File Changes off, or on an Adapter with no read tool, the Chief of Staff hears its own Memory
Tool write once at its next Turn. Decoupling it needs the supervisor to take `MemoryPaths` itself and changes a pinned plan
test (`PersonaSupervisorFileChangesTests`: no paths while File Changes is off), so it is left for a decision of its own.
