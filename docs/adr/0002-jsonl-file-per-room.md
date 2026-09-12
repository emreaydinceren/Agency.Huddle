---
status: accepted
date: 2026-09-09
---

# Message history lives in one JSON Lines file per Room, not in SQLite

Every Room's Transcript is an append-only `data/rooms/{roomId}.jsonl` file where
each line is the same `ChatMessage` JSON that travels over the pipe; the
Directory (users, rooms, members) lives in SQLite. We chose this deliberately so
transcripts are greppable, diffable, tail-able and trivially backed up, so
message-shape changes need no schema migration, and so the wire format and the
storage format are literally the same object. The cost is no ad-hoc querying and
whole-file reads on Room open, both acceptable at PoC scale and isolated behind
`IChatStore`.

## Consequences

- Single writer per Room is enforced in-process by a per-Room lock; one Team
  process per data directory.
- Readers tolerate a torn last line after a crash by skipping it.
- Search, pagination and tail reading are V2 concerns and belong behind
  `IChatStore`.
- Raw `Microsoft.Data.Sqlite` SQL (no EF Core) for the three Directory tables;
  an ORM would cost more than the ten statements it replaces.
