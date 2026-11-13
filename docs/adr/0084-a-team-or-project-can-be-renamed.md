---
status: accepted
date: 2026-11-12
---

# A Team or a Project can be renamed, and the rename is journalled

> **Records decisions D-1 to D-10 of the
> [Team Rename specification](../Huddle.TeamRename-Specifications.md).** It amends
> [ADR-0055](0055-a-reference-is-text-with-a-stable-key.md) OP-1 ("Huddle offers no Team rename").

A Team's Name is its only identity, and ADR-0055 accepted that: Huddle offered no rename, so only a folder
renamed by hand broke a Team reference. The Human now wants to rename a Team or a Project from the app, and
a hand rename in Explorer still reads as one Team vanishing and another appearing. This record settles how
an in-app rename works.

## The decision

**A Team or a Project is renamed by one action that rewrites every stored copy of the name, writes a journal
row first and finishes from it after a crash. The old name keeps working for reads and links.**

- **D-1. The Name stays the identity.** There is no Team id. Every copy of the name in the spec's §4
  inventory is rewritten, or resolved through the journal.
- **D-2. Only the in-app rename is a rename.** A folder renamed by hand keeps today's behaviour: the old
  Team's Rooms are archived.
- **D-3. Storage first, labels last and in one batch.** The folder moves, then the store rows, then the
  rewrites, then every member's `teams:` line through `PersonaStore.UpdateMany`, with the reconciler
  suspended and the provisioner and `AgentsMdWatcher` told to skip the name. The step order ends: mark the
  row done, lift the Task pauses, release the reconciler, post the Team Room line.
- **D-4. It rolls forward from a journal row.** A failure before the folder move finishes puts the folder
  back and discards the row. From the store rows on, a failed step is retried and the row stays `pending`;
  `TeamRenameRecoveryService` finishes it at the next start, reading the step from disk. A rename that cannot
  finish posts one Feed Notification (`TeamRenameStuck`).
- **D-5. Agents are told by a Turn notice, once per session.** `TeamRenamedTurnHook` is first in the hook
  order and sends the notice to every session that is not fresh, plus a fresh one whose Catch-up predates the
  rename. A fresh session's baseline is taken at its first Turn, from the oldest Message in its Catch-up.
- **D-6. An old name is forwarded for reads and refused for writes,** with a hint naming the new name.
- **D-7. A journalled name is reserved** against another Team, so an old link never reaches the wrong Team.
  A Team may take back its own former name. A Project's old name stays reserved for its Team.
- **D-8. `watches:` paths are resolved through the journal,** not rewritten, because rewriting a non-member's
  Persona text restarts it.
- **D-9. The Team Room gets one Huddle line that wakes no one.** `ReplyGate` takes a `fromHuddle` input, so a
  Huddle post with no Mention only adds to Catch-up.
- **D-10. The confirmation lists every consequence,** in the shared `ConfirmDialog` extended with a list of
  points. This is a deliberate exception to the one-sentence body rule.

## What the build settled

| Question | Settled as |
| --- | --- |
| Which lock | A `TeamOperationGate` singleton, shared by `ArchiveService` and `TeamRenameService`, in place of the spec's service-level `SemaphoreSlim` |
| How the folder moves | `ArchiveMove.Rename`, a retrying rename that refuses an existing destination; a case-only rename goes through `Teams/.huddle-renaming-<New>`. The journal row is the journal |
| One transaction | None: each store keeps its own connection, so each rename is idempotent and recovery rolls forward |
| A Task file the rename rewrites | It gets a Change log entry, "Team renamed from Old to New", or the next start reports an outside edit. The renamed Team's own Tasks are moved, not rewritten |
| A second rename while one is Pending | Refused: the hook remembers the highest id a session has heard |
| V-2: do Write calls reach the permission handler | Edit and Write do. `RenamedFolderGuard` refuses a write under a journalled folder silently, because the permission reply has no reason text; the notice carries the hint. `MultiEdit`, `NotebookEdit` and `Bash` are not covered |
| V-3: does a Huddle post wake anyone | It did, in a Room of two. Fixed by `fromHuddle` in `ReplyGate` |
| `search` redirect | None: `search` has no Team scope |
| Where the Human renames a Team | A **Rename** button beside **Archive** on the Details tab, not a pencil beside the Name, because the tab does not show the Name as a field. Also the More (⋮) menus of the sidebar rows |
| Prompt text | Four Prompts (`turn.teamRenamed`, `turn.projectRenamed`, `room.teamRenamed`, `room.projectRenamed`); the Settings Prompts page lists the last two under "Team Room lines" |

## Consequences

- **ADR-0055 OP-1 no longer holds.** A Team reference to an old name resolves through the journal. A folder
  renamed by hand still makes it unavailable.
- **Every member restarts and loses its working context.** The Room Sessions are deleted with the restart;
  the confirmation says so.
- **Some gaps are accepted.** The localStorage keys `teamsNav:expanded`, `roomList:closedFolders` and
  `library:team:*` are not mapped and fall back to defaults. An Agent's `MultiEdit`, `NotebookEdit` and `Bash`
  writes to the old folder are not caught. Both are in [known-limits.md](../engineering/known-limits.md).
- **Transcripts, Feed rows and an Agent's own notes keep the old name,** by design.
- **V-1 is open.** Whether a real model reads the notice before acting on Catch-up needs a live, paid run
  (TEAMRENAME-14).

## Rejected

| Alternative | Why not |
| --- | --- |
| A Team id | Every store in the inventory would need it, and the schema cannot gain a column |
| Detecting a hand rename | A moved folder and a deleted-plus-new one look the same to the watchers |
| Labels first, or one at a time | The old folder and a second Team Room come back |
| Rolling every step back | Undoing a move can fail too, and a half-rolled-back rename shows as two Teams |
| A rule for who is involved with the Team | Costly to compute per Turn, and it misses Agents that only read the Team's files |
| Forwarding writes to the new Team | A write meant for one Team must never land in another |
| Rewriting `watches:` | It changes a non-member's Persona text, which restarts that Agent |
| Posting to every Room, or Mentioning members | Every Agent would take a paid Turn just to be told |
| One sentence in the confirmation | The restart and the broken links are not obvious from "rename" |
