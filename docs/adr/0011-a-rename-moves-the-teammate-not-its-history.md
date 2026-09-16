---
status: accepted
date: 2026-09-15
---

# A rename moves the Teammate, not its history

[Roadmap](../agencyteam/roadmap.md) item 1 has had one unanswered question since it was
written: *what does renaming a Teammate mean to the chat surface?* Item 10 answered the easy
half in 2026-09-12 — a rename is an edit to one frontmatter field, and `PersonaStore.Update`
moves the Model and Effort rows by hand. This answers the hard half.

Until now the answer was **nothing happens**, and that was not a decision so much as the
absence of one. The consequence was visible and was recorded in three places as a known limit:
the old Agent, its Rooms and its Transcripts stayed behind under the old Name.

## What was actually wrong

The ghost was not caused by any rule about cascading. It was caused by an **id**.

`AgentConnection` registers an Agent with `UpsertAgentUserAsync(hello.Name, …)`, which is
keyed on the Name. Rename a Persona and the supervisor sees the old Name disappear and a new
Name arrive — an unrelated stop and start, not a rename — so the restarted runner says `hello`
under the new Name, the upsert takes its INSERT branch, and a **brand-new user id** is minted.

Everything downstream references the Agent by id: `room_members`, `GetRoomsForUserAsync`,
`ChatMessage.SenderId`, `AgentGateway.connections`, `FindRoomWithExactMembersAsync`. A new id
therefore orphans all of it in one step, and `ChatService.EnsureRoomForAsync` finds no Room for
the new id and helpfully creates a second one. One rename produced two users, two direct Rooms,
two sidebar entries and an unreachable Transcript.

## The decision

**A rename renames the existing `users` row in place and keeps its id.**

That is the whole mechanism. Because every association is by id, Rooms, memberships and
Transcripts follow with no work and no migration:

| Thing | Keyed by | Follows a rename? |
| --- | --- | --- |
| `room_members` | user id | yes, untouched |
| `GetRoomsForUserAsync` | user id | yes, untouched |
| `ChatMessage.SenderId` | user id | yes, untouched |
| `FindRoomWithExactMembersAsync` | user id | yes — so **no second Room is created** |
| `persona_models`, `persona_efforts` | Persona Name | already moved by hand, since item 10 |

`ITeamDirectory.RenameUserAsync(userId, newName)` is the one new storage primitive. It needs no
schema change, which matters: the schema is created with `CREATE TABLE IF NOT EXISTS`, so a
column added now would silently never reach an existing `team.db`.

## Three things deliberately not done

**History is not rewritten.** `ChatMessage` stores a denormalised `SenderName` alongside
`SenderId`, so old lines keep the Name that was true when they were posted. Rewriting them would
require a rewrite path on `FileChatStore`, which is append-only by construction
(`FileMode.Append`), and would edit a historical record in place — a half-failed rewrite leaves
a corrupt Transcript. Leaving it alone costs nothing and is the more honest artefact: the
Transcript says what was said, under the name it was said under, and `SenderId` still proves it
was one Teammate throughout. **A rename is not a retcon.**

**The Human can never be renamed this way.** `RenameUserAsync` reproduces
`UpsertAgentUserAsync`'s `WHERE users.kind = 'agent'` guard. Without it, a Persona renamed to
the Human's Name would take over the Human's row — the same hole `nameReserved` exists to close
on the registration path.

**A Room the Human renamed keeps its name.** Rooms that still carry their auto-derived name are
re-derived so they track the new Name; Rooms renamed by hand are left alone. This reuses
`RoomNaming.Derive` and the same comparison `ChatService.InviteAsync` already makes, rather
than inventing a second rule for when a Room may be renamed out from under somebody.

## How a rename is detected, and why by path

`PersonaStore` raises a new `PersonaRenamed(oldName, newName)`. It is detected by diffing the
previous `PersonaIndex` against the new one **keyed by `PersonaEntry.Path`**: `Update` rewrites
the same physical file, so the path is the only thing that survives a rename, and a path whose
Name changed *is* a rename.

Keying on the path rather than special-casing `Update` buys the door nobody would have
remembered: a Persona file **hand-edited in an editor** bypasses `Update` entirely and reaches
only the debounced `FileSystemWatcher`, where no old Name exists anywhere else to compare
against. One mechanism covers both doors, and it is idempotent for free — a save raises the
event twice (once synchronously, once from the watcher ~500 ms later), and on the second pass
both indexes already carry the new Name, so nothing fires.

Comparison is **`Ordinal`**, so `coo` → `Coo` counts. Several stores downstream treat those two
as equal, but the displayed Name genuinely changed.

**The ordering is the rule.** `PersonaRenamed` is raised synchronously, to completion, *before*
`PersonasChanged`. `PersonaSupervisor` subscribes to `PersonasChanged` and reacts to a rename by
starting a runner under the new Name; that runner registers over the pipe. If the `users` row
has not already been renamed by then, the registration mints the new id and the ghost appears
regardless. Two lines reordered would silently restore the entire bug, so it is stated in the
code and here.

## Why one Team Directory write is synchronous

`ITeamDirectory` is otherwise async throughout, and `RenameUser` is not. That is deliberate and
it is the least obvious decision here, so it is worth stating why before somebody tidies it.

The ordering above only holds if the rename has *finished* when `PersonasChanged` fires. The
event is an `Action<T>`, so a handler cannot await; `.Result` and `.Wait()` are forbidden by
house style; and `PersonaSupervisor` dispatches its restarts fire-and-forget, so nothing
downstream is awaiting either. Fire-and-forget in the handler would leave the guarantee resting
on *"spawning a `node` process takes longer than a SQLite `UPDATE`"* — true in practice, and
exactly the kind of timing assumption this codebase keeps a Traps page about. When it lost, the
failure would be a silently re-created ghost and a warning nobody reads.

Making the whole write path async was the alternative, and it was rejected on cost: three
production call sites but around seventy in tests, for a guarantee that only **one** operation
needs. Everything else the cascade does — re-deriving Room names, moving the Work Dir — races
nothing, because a Room name landing a moment later is repainted by `RoomsChanged` anyway. So
one synchronous method buys the whole ordering guarantee, and the async surface elsewhere is
untouched.

`PersonaModelStore` and `PersonaEffortStore` are the existing precedent — both synchronous over
this same database, for the same shape of reason ("Synchronous, because its only caller is").

## What this does not retire

**`PersonaStore` still has no reference to `ITeamDirectory`.** [Rules](../agencyteam/rules.md)
records that property, and it survives: `PersonaStore` raises an event and knows nothing about
who listens. The cascade lives in a separate subscriber that holds both.

**Removing a Persona still does not cascade.** That remains deliberate and unchanged — an Agent
that goes away is an Agent that disconnected, and its Rooms and Transcripts are chat facts that
outlive the Persona which created them. A rename is a different claim: *the Teammate is still
here, under a different Name*, which is precisely why the no-cascade rule read as a bug for a
rename and reads as correct for a removal.

## Consequences

- The card's rename warning — *"its Agent, its Rooms and their Transcripts stay behind under the
  old name"* — became false. A test pinned that exact wording, which is how it was caught, and
  that test now asserts the old sentence is **absent** as well as that the new one is present: a
  doc can go stale quietly, but this one is load-bearing interface copy. It is no longer a
  `Warning` either. It reports the one thing a rename still does not do — rewrite what was
  already said — so it is an `Info` with `role="status"` rather than `role="alert"`: a
  consequence, not an interruption.
- The Work Dir moves with the rename. `App_Data/work/{Name}/` is handed to the agent process as
  its `cwd`, the Work Dir is not a jail, and the Adapter auto-loads `CLAUDE.md` and
  `.claude/settings.json` from it. Leaving it behind silently discarded whatever a Teammate had
  written for itself — an orphan no document had ever mentioned.
- A rename still restarts the session and still loses that Teammate's conversation memory. A
  system prompt is fixed at `session/new`; nothing here changes that.
- `mcp__team__list_agents` stops advertising a permanently-offline ghost to every model, which
  was the most expensive symptom: a model could invite one into a Room.
