---
status: accepted
date: 2026-09-21
---

# A Room can be archived or deleted

Until now a Room was permanent. That was not an oversight — it was a stated position,
recorded in [Known limits](../engineering/known-limits.md) and repeated as an instruction to
testers in `manual-tests/invite-rooms.md`:

> A Room can never be deleted, left, or renamed by hand — there is no such control anywhere in
> the UI, and a Room name is always derived. Do not look for one and do not file its absence.

The reasoning behind it was sound and is restated in [ADR-0011](0011-a-rename-moves-the-teammate-not-its-history.md):
a Room and its Transcript are **chat facts that outlive the Teammate which created them**. That
is why removing a Persona does not cascade, and it is still true.

But "a Persona's removal must not destroy a Room" and "a Human may never put a Room away" are
two different claims, and only the first follows from the argument. The second was inherited
from it. Its cost is ordinary and compounding: every Name ever connected under leaves a Room in
the sidebar forever, which is why `manual-tests/common.md` had to tell testers to *"append a
digit"* to Names they had already used. The sidebar is the app's primary navigation and it only
ever grows.

The position had already been half-reversed once. Renaming a Room by hand shipped with issue
#43, which made the sentence above false in its middle clause while nobody updated it. This ADR
reverses the rest deliberately, rather than by attrition.

## The decision

**Two operations, and they are not variations of each other.**

| | Archive | Delete |
| --- | --- | --- |
| Reversible | yes | no |
| Transcript on disk | kept | removed |
| Room still live | **yes** | n/a |
| Confirmation | none | names the Room |
| Reached from | row context menu | row context menu, and the Archived Chats dialog |

Archive is the one to reach for. Delete exists because a Room created by a typo'd Name, or a
test run, is not a chat fact anybody wants to keep — and archiving it only moves the clutter.

## Archive is a sibling table, never a column

This is the least obvious decision here and the one most likely to be "tidied" later.

The obvious shape is `ALTER TABLE rooms ADD COLUMN archived`. It is wrong in this codebase, and
silently so. All schema DDL in `SqliteTeamDirectory` is `CREATE TABLE IF NOT EXISTS`, which
means **an existing `team.db` never gains a new column and never says so**. [Traps](../engineering/traps.md)
records this, and [Known limits](../engineering/known-limits.md) records the one previous time
the decision came up: Room auto-naming detects a Human-chosen name by *comparing* against
`RoomNaming.Derive` rather than storing a `name_is_custom` flag, for exactly this reason.

So archived-ness lives in its own table, where **the presence of a row means archived**:

```sql
CREATE TABLE IF NOT EXISTS archived_rooms (
    room_id  TEXT PRIMARY KEY REFERENCES rooms(id),
    archived TEXT NOT NULL
);
```

`CREATE TABLE IF NOT EXISTS archived_rooms` creates cleanly on a database that predates it,
which is the whole property we need. `PersonaModelStore` and `PersonaEffortStore` are the two
existing precedents, and `PersonaModelStore` states the rule in its own header comment.

Absence-of-row as the default encoding also matches `PersonaModelStore.Set(name, null)`, which
deletes the row rather than storing a blank.

`Room` gains `Archived` as a **non-positional** `init` member rather than a fourth positional
parameter, so every existing `new Room(...)` keeps compiling. The four room-reading queries
`LEFT JOIN` the new table and select `a.room_id IS NOT NULL`; `ReadRoom` reads it positionally,
which means the SELECT lists and that reader must change in lockstep or it fails at runtime
rather than at compile time.

`SqliteTeamDirectoryTests.PreExistingDatabase_WithoutArchivedRoomsTable_StillSupportsArchiving`
builds a database containing only the original three tables, by hand, and proves the claim.
Without that test this design's central argument is unverified.

## Archive is a display filter and nothing else

An archived Room stays live. Agents still post into it, the Transcript still grows, the Budget
is untouched, and opening it from the Archived Chats dialog behaves exactly like any other
Room. Archiving decides one thing: whether the Room appears in the sidebar.

Freezing the Room instead — refusing delivery into it — was considered and rejected. It reads
tidier, but it would put a new condition inside the delivery path, next to `ReplyGate` and
`AgentGateway.DeliverAsync`, where the rules about who receives what are already the most
load-bearing logic in the app. A sidebar preference does not belong there. The filter lives in
`RoomList` and `Chat`, which is where a display decision belongs, and no storage or delivery
code needs to know the feature exists.

The accepted cost is real and worth stating: **a Room you archived can accrue messages you will
not see until you unarchive it.**

## Delete removes both halves, because a Room has two

[ADR-0002](0002-jsonl-file-per-room.md) put Message history in one JSON Lines file per Room
rather than in SQLite. The consequence for this feature is that a Room is stored in two places
— `rooms` and `room_members` in `team.db`, and `{DataDir}/rooms/{roomId}.jsonl` on disk — and a
delete that forgets the second leaves an orphan transcript file behind.

So `IChatStore` gains `DeleteAsync`, its first destructive operation. The SQLite side deletes
inside one transaction, in the order `room_members` → `archived_rooms` → `rooms`:
`PRAGMA foreign_keys=ON` is set per connection and `room_members.room_id` references `rooms(id)`
with no `ON DELETE CASCADE`, so the reverse order throws.

**A soft delete was rejected.** It would have added a second hidden state beside archived, with
no UI to reach it, no purge story, and two different meanings of "gone". Archive already *is*
the reversible option; making delete reversible too would leave the pair with no clear
distinction and nothing that actually reclaims disk.

## Starting a chat with a teammate whose Room is archived

`FindRoomWithExactMembersAsync` enforces the invariant from
[ADR-0003](0003-mention-gated-replies-and-membership-defined-direct-rooms.md): an Agent belongs
to at most one two-Member Room. An archived Room is now **excluded** from that lookup, so
starting a chat with a teammate whose 1:1 Room you archived creates a fresh one rather than
resurrecting the old.

The alternative — find it and unarchive it — preserves the invariant exactly and was the
recommendation. It was not chosen. The repo owner's call was that archiving a conversation
should mean it stays put, and that a new conversation is a new conversation.

**The cost, accepted knowingly:** archive your Room with Bob, start a new chat with Bob, then
unarchive the first from the dialog, and two non-archived Rooms now have exactly the members
`{Human, Bob}`. `RoomNaming.Derive` names both of them "Bob", so the sidebar shows two
identical entries. Nothing breaks, but the invariant no longer holds. `FindRoomWithExactMembersAsync`
gained an `ORDER BY r.created, r.id` before its `LIMIT 1` — it had none — so which one wins is
at least deterministic (the oldest) rather than whatever SQLite happens to return first.

## Three things deliberately not done

**`Drafts` and `RoomFollows` are not cleared on delete.** Both are in-memory, both die on
restart, and both are only ever read *by room id* — an id that is now unreachable, so their
entries for it are inert. Reaching them would mean injecting `RoomFollows`, an `internal` type
in the `Acp` namespace, into `ChatService`: a new cross-layer dependency for no observable
gain, against the hub's standing instruction not to add abstraction no current feature asks
for. `ChatService` does drop its own `budgets` and `postLocks` entries, because those are its
own fields and cost one line each. The omission is commented in the code, because otherwise it
reads as an oversight rather than a decision.

**Delete has no trash and no undo.** The confirmation names the Room; that is the whole safety
mechanism. It is an inline row swap rather than a nested dialog, following
`TeammateCard.razor`'s written rationale — MudBlazor dialogs are not designed to stack, and a
yes/no choice does not need a second backdrop and a second focus trap.

**Archiving is not offered to Agents.** No App Tool archives or deletes a Room, and none
should. A Room's visibility in the Human's sidebar is the Human's filing system, the same
argument that keeps Persona Team sub-folders a no-op to the code.

## Consequences

- Two documents became false and are corrected in this change:
  `manual-tests/invite-rooms.md`'s *"A Room can never be deleted, left, or renamed by hand"*
  (already half-false since issue #43) and `manual-tests/common.md`'s *"Nothing in the UI
  deletes a Room or an Agent"*. The latter's practical advice — append a digit to a Name you
  have already used — is now optional rather than necessary.
- P-RESET-ROOMS, the documented reset procedure of stopping the app and deleting `App_Data`,
  is no longer the only way to get rid of a Room.
- `IChatStore` is no longer append-only in its surface. It is still append-only in its *write*
  path — `FileMode.Append`, as ADR-0011 relies on for not rewriting history — but the file can
  now be removed as a whole. Deleting a file is not rewriting a record, and the distinction is
  the one that matters: a Transcript is still never edited in place.
- The sidebar has a context menu for the first time, and `MudMenu` enters the codebase.
