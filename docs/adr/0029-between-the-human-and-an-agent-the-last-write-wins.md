---
status: accepted
date: 2026-09-24
---

# Between the Human and an Agent, the last write wins

When the Human saves a file in the Library, the save overwrites whatever is on disk, including a
change an Agent made after the Human opened it. The Library does not lock files, does not tell
Agents a file is being edited and does not check for a conflict before saving. The full design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.8).

## The problem

Agents are not sandboxed. `Bash` and `Write` run against the real disk
([rules.md](../engineering/rules.md), *The Work Dir is not a jail*), so an Agent can change a file
while the Human has it open. Any policy has to accept that Huddle can see those writes but cannot
stop them.

## The decision

**The last write wins.** A save writes the editor's text, whatever is on disk.

**Cheap freshness, not conflict handling.** When a file is opened, and whenever the pane regains
focus while a file is open with **no unsaved edits**, the Library re-reads the file and shows the
current text. A file with unsaved edits is never reloaded under the Human.

**Agents learn of the Human's saves through File Changes.** A save in a Watched Folder is listed
on the Agent's next Turn, as any outside edit is
([ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md)). A Team folder is a
Watched Folder for every Persona in that Team
([ADR-0030](0030-a-team-folder-is-its-library-and-holds-its-tasks.md)), so the main case,
editing a Team spec, needs no new mechanism.

## Rejected

| Alternative | Why not |
| --- | --- |
| Detect and prompt (compare modified time or hash on save; offer Reload, Overwrite, Diff) | Right eventually, and the first thing to add if lost edits happen in practice. Not in v1: the Human chose simplicity |
| Lock the file while it is open in the editor | Not enforceable: an Agent's `Bash` ignores any lock Huddle could hold, and a stale lock would block the Agents |
| Tell the Agent, in its prompt, which files are open | Spends tokens on every Turn and still depends on the model complying |
| A `FileSystemWatcher` pushing live reloads into the editor | Adds debounce and overflow handling ([ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md), *Why a snapshot and not a watcher*) for a refresh that focus already gives |

## Consequences

- **An Agent's write can be lost** if the Human saves a stale buffer. The window is the time the
  Human spends editing with unsaved changes.
- **A Human's save can be overwritten** by an Agent that read the file before the save. The Agent
  learns of the save on its next Turn, but may already have acted on the old text.
- **The upgrade path is additive.** Detect-and-prompt needs only the file's modified time and
  length at load, which the Library already reads to decide whether to reload.
