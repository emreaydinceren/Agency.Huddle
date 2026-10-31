---
status: proposed
date: 2026-10-30
---

# Memory is written through the memory tool, and Team Memory by its manager

An Agent's Memory and Team Memory gain a `memory` App Tool:

- six commands: `list`, `read`, `write`, `append`, `replace` and `delete`;
- topic files with an optional `description` line, which the built index prefers;
- read-before-write version tokens.

Team Memory becomes writable only by the Team's manager and the Chief of Staff.

This record amends [ADR-0032](0032-a-team-and-each-project-share-a-memory-folder.md), and the
"Not a tool" and "one fact per file" choices in
[FileChanges §6.15](../Huddle.FileChanges-Specifications.md#615-memory-what-an-agent-deliberately-remembers).
It narrows [ADR-0029](0029-between-the-human-and-an-agent-the-last-write-wins.md) for files
written through the tool. The design is
[Huddle.InstructionsAndMemory-Specifications.md](../Huddle.InstructionsAndMemory-Specifications.md) §4 to §7.

## The problem

Today, Memory is a convention the prompt asks for, and nothing enforces it:

- An Agent can write anywhere.
- It can overwrite a file that another copy of itself has just changed.
- It can leave a first line that says nothing about when to read the file.
- Every member writes Team Memory, so it drifts into one Agent's notes.
- An Adapter without file tools has no Memory at all. The memory blocks are only shown when the
  Adapter reads files.

The Human's *Huddle Agent Memory Protocol* asks for:

- confinement;
- read-before-write;
- a description on every file;
- a built index;
- size caps;
- a manager who alone maintains Team Memory.

## The decision

- **One `memory` tool with a `command`.** It is always offered and built per Persona.
  - Its `folder` is `me`, `<Team>` or `<Team>/<Project>`.
  - Folders are resolved against the catalog for members, the manager and the Chief of Staff,
    never joined from the raw argument.
  - A file is one bare `.md` name, checked by `LibraryNames.Validate`, with Windows device names
    refused.
- **Read before write.**
  - A `write` to an existing file, and a `delete`, need the token from the latest `read`. A
    mismatch is refused, with the current text and token.
  - `append`, and `replace` of an exact unique string, need no token.
  - A per-folder `SemaphoreSlim` in the app serialises every tool write. A content hash catches
    writers outside the gate.
- **A topic per file, with an optional `description` in frontmatter.**
  - The index uses the description, else the first line after any frontmatter, so existing files
    keep working.
  - The tool writes the frontmatter itself and enforces the mechanical rules: one line, 20 to 200
    characters, not the file name restated.
- **The index stays built. There is no `MEMORY.md` and no `name` key.** This departs from the
  Human's document, and the Human accepted it.
- **Only a Team's manager and the Chief of Staff write Team Memory.**
  - The tool enforces it.
  - So does `WorkDirPermissionHandler`, for `Edit`, `Delete` or `Move` file-tool calls into a
    Team's `memory/` folders. Reads are allowed.
  - Every member reads Team Memory, under one Team Memory heading with a manager paragraph and a
    member paragraph.
- **An Agent deletes only when the Human asks.** This is a rule in the tool description and the
  Prompt, not a code check.
- **The memory blocks are shown whenever the tool is offered.** The File Changes sentence is
  added only when File Changes run.
- **The Agent's own Memory stays writable with `Write`.** The Prompt teaches the tool.

## Rejected

| Alternative | Why not |
| --- | --- |
| Keep "not a tool" (FC §6.15) | A prompt cannot confine, version or validate, and Agents without file tools stay without Memory |
| A `MEMORY.md` index kept by the Agent | It drifts from the files, and every save rewrites one shared file. A built index is the Human's own guardrail 5 |
| Six separate tools | They lengthen the tool list in every system prompt |
| Refuse `Write` into the Agent's own `memory/` | It breaks existing habits and the Human's own editing, for no gain the index needs |
| Manager-only by prompt alone | The Human asked for one place that enforces it |
| A per-folder `System.Threading.Lock` | The critical section awaits the atomic write |

## Consequences

- A member's Team decision now goes through the manager. That is slower, and it is the point.
- A Team with no manager has Team Memory that only the Human and the Chief of Staff write.
- Both guards on file tools are soft: they see path arguments only, as the `~/.claude` guard does.
- `systemPrompt.teamMemory` becomes a frame with `{{teamMemoryRoles}}`. A Human override of the
  old frame is rendered the old way, with a Warning.
- Every prompt golden changes once in the build, because the tool list and the memory blocks
  change.
- In [language.md](../engineering/language.md), **Memory** and **Team Memory** change from "one
  fact per file, the fact on the first line" to "one topic per file, its description as the index
  line".
