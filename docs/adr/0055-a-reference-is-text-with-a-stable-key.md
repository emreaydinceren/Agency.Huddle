---
status: accepted
date: 2026-10-12
---

# A reference is text with a stable key

> **Records decisions D-11, D-13, D-14 and D-16 of the
> [References specification](../Huddle.AtTagging-Specifications.md)** (D-8, D-17, D-29 and D-30 are
> cited where they follow from it). The product owner's brief was *"type `@`, pick a Task, a Room, a
> Team or a file, and have it just work"*. The rules it leans on are
> [ADR-0003](0003-mention-gated-replies-and-membership-defined-direct-rooms.md) (only a Mention wakes
> an Agent), [ADR-0011](0011-a-rename-moves-the-agent-not-its-history.md) (a rename keeps the id)
> and [ADR-0035](0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) (a command is
> a Message with a Mention). Search's no-index rule, [ADR-0051](0051-search-keeps-no-index-of-its-own.md),
> is why the picker reads the things themselves.

## The defect

The Composer lets a Human point at a thing: a Task, a Room, a Team, a Library file. A pointer has to
survive four journeys. The Human **pastes** it, **undoes** it and edits around it. An **Agent** reads it
inside a Prompt and may write one back. An **old client** or an old Transcript line meets it without
knowing the feature exists. And the thing it points at **changes**: a Task is retitled, a Room is
renamed, a file is moved.

The obvious designs each break on one of these. A **rich token** (a chip object in the editor, a
hidden marker such as `<ref:task:T-12>` in the text) is lost on paste into another field, leaves
residue after undo, reads as noise to an Agent, and means nothing to an old client. A **snapshot kept
as the source of truth** (the Message stores "this Message references Task T-12" and the text is
decoration) goes stale the moment the Message is edited, can disagree with the text the Human
sees, and needs bookkeeping on every deletion. **Expanding history**, rewriting old Messages when a
title changes, falsifies a Transcript. **Inlining descriptions or Transcripts** into the Agent's Prompt
floods every Turn with text that was never asked for.

## The decision

**A reference is text with a stable key. The snapshot is metadata of what the sender saw. Agents get a
compact, fresh header.**

- **The text is the reference (D-11).** What the picker writes is ordinary Message text: a Task id, a
  Room link by id, a Team Name, a Library file's absolute path in a code span (D-8, the form chat
  uploads already send, [ADR-0053](0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md)).
  One scanner reads that text at send time and again at Turn time, so a **typed** reference counts as
  much as a **picked** one, undo and deletion need no bookkeeping, and the snapshot and the header
  cannot disagree about what the Message says.
- **The key is what makes a rename harmless.** A Task is named by its id, a Room by its id, and the
  label next to it is only what the sender saw. Retitling a Task or renaming a Room changes nothing in
  any Message that points at it, and the rendered link opens the right thing.
- **The snapshot only reports (D-13, D-14).** `ChatMessage` gains an optional `References` property,
  written only when non-empty, holding what the sender saw for Tasks, Rooms, Teams and Library files
  (title, state, label). The Room view compares it with today's value and draws a chip only for a
  target that is gone or changed (D-15). It is never the source of a Mention, and it never holds an
  Agent: a Mention already has its own rule and its own record in `SenderId` (ADR-0003,
  ADR-0011), and the Reply Gate is untouched.
- **Agents get a fresh, compact header (D-16, D-17).** For the **trigger Message only**, `RoomSession`
  writes a *Referenced items* block with one line per Task, Room and Team, read **now**, not from the
  snapshot, with values JSON-quoted inside one dynamic fence under a line that says they are data.
  Library files, picked or uploaded, are left to the existing Library documents block, which already
  lists them with caps; a second listing would list an attachment twice. No description and no
  Transcript is inlined: the header names the thing and where to ask (`get_task`, `list_agents`).

### Rejected

| Rejected | Because |
| --- | --- |
| A rich token or hidden marker in the text | Lost on paste and undo, noise to an Agent, meaningless to an old client; it needs a parser the plain text does not |
| The snapshot as the source of Mentions or of "who is referenced" | It can disagree with the visible text and needs bookkeeping on every edit; Mentions stay derived from text (D-13) |
| Expanding or rewriting history when a target changes | A Transcript is a record; the chip reports a change instead of falsifying the line |
| Inlining descriptions or Transcripts in the header | Floods every Turn; the Agent has tools to ask for what it needs |

## The wire rule

The change is **additive and the protocol version stays**. `References` is an optional `init`
property on `ChatMessage`, written with `WhenWritingNull` and absent when empty. An old line reads
(a missing property is the default), an old client ignores the property, and no constructor call site
changes. This is the precedent of ADR-0004 and ADR-0006 and the rule in traps.md, *Adding to the wire*:
bumping `ProtocolVersion.Current` over an addition would break every client for nothing.

## One mechanism, two doors

Because a reference is only text, there is no second code path. **Copy link (D-30)** puts the same
reference text on the clipboard as plain text, so pasting it into the Composer is a reference the
scanner already reads. **Picker rows are always the things themselves (D-29)**: the Rooms provider
under the reference purpose never reads a Transcript, and the Library provider never opens a file, so
a row never carries Message text or file content and the search box keeps its behaviour unchanged.

## Honest limits

- **Teams have no id (OP-1).** A Team is keyed by its Name, as a Room's Scope is. Huddle offers no Team
  rename, so only a by-hand folder rename breaks a Team reference; the snapshot then shows it as
  unavailable.
- **A Library file has no id (D-8).** Its reference is its absolute path, so a file that is renamed or
  moved shows as unavailable. Library link rewriting covers notes, never a Transcript, and no form of
  link is rename-safe in chat. The snapshot reports the loss instead of hiding it, and the limit is
  written into `known-limits.md`.
- **The snapshot is a report, not a guarantee.** It records what the sender saw at send time. It does
  not follow later changes, and the header, not the snapshot, tells an Agent the current state.

## Consequences

- Paste, undo, typing a reference by hand and an Agent writing one all work with no extra code; a
  stray character can only make a reference not match, never corrupt a Message.
- Renaming a Task or a Room breaks nothing. A moved Library file or a hand-renamed Team folder
  degrades to *unavailable* in the chip, on purpose.
- Old Transcripts, old clients and the unchanged Reply Gate keep working with no migration.
- Each Agent Turn pays a few lines of header for the trigger Message and nothing for the history.
- A later move to ids for Teams or Library files is a new decision. It would add keys to the text the
  scanner reads, not a different mechanism.
