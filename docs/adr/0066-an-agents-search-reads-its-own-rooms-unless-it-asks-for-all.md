---
status: accepted
date: 2026-10-28
---

# An Agent's search reads its own Rooms unless it asks for all

Global Search ([ADR-0051](0051-search-keeps-no-index-of-its-own.md)) is exposed to Agents as the `search`
App Tool. It reuses the five providers, so an Agent can find Teams, Agents, Library entries, Tasks and
Rooms by exact phrase. This record settles what an Agent may read when it searches Rooms. The design is
[Huddle.GlobalSearch-Specifications.md](../Huddle.GlobalSearch-Specifications.md) §18.

## The problem

The Human sees every Room, so the providers search every Room and open every Transcript. An Agent that
could do the same could read a Room it was never invited to and carry what it found into another Room
with `post_message`. Rooms are the one category that holds free-form conversation; the others hold names,
titles and files that an Agent can already reach.

## The decision

**The tool takes a `scope` argument for Rooms. The default, `mine`, covers the Rooms the caller is a Member
of and every Room scoped to one of the caller's Teams. `all` covers every Room.**

- `mine` is computed before any Transcript is read and passed to the Rooms provider as
  `SearchQuery.RoomIds`, so a Room outside the scope is never opened. A `null` list means every Room, which
  is what the Human's box and the `@` picker send.
- `all` is allowed to every Persona, because an Agent can have a real reason to read another Room's history,
  such as a decision made elsewhere. Every such call is logged with the caller and the phrase.
- The tool's description tells the Agent that Rooms default to its own, and when to widen.
- The Library, Tasks, Teams and Agents providers are not scoped: an Agent's own file tools already read
  the disk, and `list_tasks` and `get_task` already read every Task.

## Consequences

- The boundary is soft. The Persona Work Dir is not a jail, and `scope: "all"` is one argument away. The
  scope keeps the convenient path from leaking, and the log records every crossing; it does not stop a
  determined Agent.
- Widening the default later is safe. Narrowing it after Agents rely on it is not, which is why it starts
  narrow.
- Searching `mine` is cheaper than searching every Room, because fewer Transcripts are read.
- If Rooms ever need a stricter rule, such as an approval for `all`, the argument already marks the call
  to gate.

## Amendment, 2026-11-14

By owner decision on 2026-11-14 (issue #534), **the Chief of Staff is the one Agent with no read
limits.** The owner's words: "Chief of staff should have access to anything."

- **`search`.** For the built-in Chief of Staff (the Persona marked `_builtin: chief-of-staff`), every
  Room is in scope by default, with no `scope: "all"`. Its every-Room searches are logged at
  Information, because they are now routine. It may still narrow itself with `scope: "mine"`. Every
  other Agent keeps the rule above: its own Rooms and its Teams' Rooms by default, and `scope: "all"`
  logged at Warning with the caller and the phrase.
- **`read_records`.** The Chief of Staff passes the manager or Retro, `retrospective` Skill and
  Room-to-Team checks for any Team, and is offered the tool through its `team-rollup` Skill. The tool
  is unchanged for managers and the Retro agent.
- **Rooms are searched like grep.** The same decision makes `search` read Rooms Message by Message
  for every Agent: every matching Message rather than the newest per Room, with `regex`, `context`,
  `count` and the `room`, `team`, `sender`, `since` and `until` filters, and a `room` with no query
  returns that Room's latest Messages. The scope above is still applied before any Transcript is
  read. The Human's Global Search keeps one row per Room. The arguments are in
  [Huddle.GlobalSearch-Specifications.md](../Huddle.GlobalSearch-Specifications.md) §18.
