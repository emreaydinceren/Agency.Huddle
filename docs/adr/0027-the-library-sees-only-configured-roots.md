---
status: accepted
date: 2026-09-24
---

# The Library sees only configured Library Roots

The **Library** is Huddle's built-in explorer, viewer and editor for files. It shows a fixed list
of **Library Roots** and nothing else: no drive letters, no parent folder, no browsing above a
root. Every path it reads, writes, renames, moves or recycles is first resolved by one
`LibraryPathResolver`, which refuses anything outside the roots. The full design is
[Huddle.Library-Specifications.md](../Huddle.Library-Specifications.md) (§6.1, §9).

## The problem

The Human wants to open what the Agents produced, fix a Memory file and edit a spec without
leaving the chat. The files involved are spread across Huddle's own `DataDir` (Work Dirs,
[Team folders](0030-a-team-folder-is-its-library-and-holds-its-tasks.md)) and, sometimes, a
folder outside it, such as a repo's `docs/` kept as an Obsidian vault.

Huddle serves its pages same-origin with no authentication, and the Open action already runs a
process on the server, ungated ([rules.md](../agencyteam/rules.md), *The Open action launches a
process…*). A file browser that can reach the whole disk would turn every future bug in it into a
read or write anywhere on the machine.

## The decision

**The Library has three kinds of root, and only these:**

| Root | Where | Configured by |
| --- | --- | --- |
| **Teams** | `{DataDir}/Teams/`, one folder per Team: notes and Projects ([ADR-0030](0030-a-team-folder-is-its-library-and-holds-its-tasks.md)) | Built in |
| **Teammates** | `{DataDir}/Teammates/`, one folder per Teammate: its definition, and its `work/` folder with artifacts and `memory/` ([ADR-0031](0031-teammates-and-teams-are-sibling-folders.md)) | Built in |
| **Pinned roots** | Any folder the Human adds, such as `E:\Repos\Huddle\docs` | `Team:Library:Roots` in configuration, plus the Settings → Library panel, persisted to `{DataDir}/library-roots.json` on first save |

**One resolver guards every operation.** `LibraryPathResolver` takes a root id and a path
relative to that root. It never takes an absolute path from the browser. It:

1. Joins and canonicalises with `Path.GetFullPath`.
2. Resolves every reparse point (junction or symbolic link) on the way down, and refuses the
   path if the resolved target leaves the root.
3. Refuses any path inside `DataDir`'s reserved folders (`WatchedFolderResolver.ReservedFolders`:
   `rooms`, `logs`, `file-state`, `avatars`, `room-sessions`), even when a pinned root contains
   `DataDir`.
4. Compares paths case-insensitively, as Windows does.

The result is a `LibraryPath` value (root id, relative path, full path). Services accept only
that type, so there is no code path that touches the disk with an unresolved string.

## Rejected

| Alternative | Why not |
| --- | --- |
| The whole file system, like an OS file dialog | One path-handling bug becomes a read or write anywhere on the machine, from a same-origin page with no authentication |
| `DataDir` only | Excludes the main pinned-root use case: editing specs in a repo that is also an Obsidian vault |
| One configured root | A Team folder, a Teammate's Work Dir and a repo's docs are three different places. One root would have to be a common ancestor, which is usually a drive |
| Per-Room scope (only the Room's Teammates' folders) | The Library changes under the Human when they switch Rooms. Specs and Team notes belong to no Room |
| Absolute paths from the browser, checked with `StartsWith` | `StartsWith` passes `C:\Data\Library-evil` for root `C:\Data\Library`, and ignores junctions |

## Consequences

- **Adding a pinned root is a trust decision.** Anything in it becomes readable and writable from
  the Huddle page. The Settings panel says so next to the Add button.
- **Pinned roots are not Watched Folders.** An Agent learns of a change in one only when its
  Persona lists the folder in `watches`, and `watches` accepts only folders inside `DataDir`
  ([ADR-0023](0023-an-agent-learns-of-file-changes-on-its-next-turn.md)). Pinned roots outside `DataDir` stay invisible to File Changes.
- **The reserved-folder list becomes shared.** `LibraryPathResolver` reads
  `WatchedFolderResolver.ReservedFolders` rather than copying it, so a folder reserved later is
  protected in both places.
