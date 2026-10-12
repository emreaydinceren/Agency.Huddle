---
status: accepted
date: 2026-10-12
---

# Needs You is worked out from Room events and held in memory

**Needs You** is a card in the sidebar listing every Room that waits on the Human. A Room
joins it when an Agent Mentions the Human, asks a Question, opens a Form, or spends the
Room's Budget. It leaves when the Human posts there or dismisses it. The full design is
[Huddle.NeedsYou-Specifications.md](../Huddle.NeedsYou-Specifications.md).

## The problem

The Human runs several Rooms while Agents work in them unattended. Nothing today says which
of them is blocked on the Human. The Human finds out by opening each Room and reading it, and
a Room that waits unnoticed wastes the Agents' time as well as the Human's.

## The decision

**One singleton, `NeedsYouTracker`, watches events that already exist and holds the list in
memory, keyed by Room id.** It subscribes to `MessagePosted`, `MessageRedelivered`,
`QuestionsChanged`, `ElicitationsChanged` and `RoomsChanged` on `RoomEvents`. `ChatService`,
`QuestionStore` and `ElicitationStore` do not change to feed it.

**Only the Human's own action clears a Room**: posting in it, or pressing ✕. Opening the Room
does not, because the Human may look at a Room without having time to answer. ✕ hides the
entry until a new reason arrives, and never drops the Question card or Form behind it.

**At startup, the Mention reason is worked out again from the end of each Transcript**, by
the same pure rule the live path uses. The other three reasons live in stores that are
already in memory and already empty after a restart, so there is nothing to rebuild.

**The tracker, the list and the card are three pieces.** The list does not know which
sidebar holds it, so a planned right-hand sidebar can reuse it.

## Why not let each Room keep its own flag

There is no Room object to keep one. A `Room` is a record read fresh from SQLite each time it
is needed, and per-Room state in this app (Budgets, Questions, Forms, Drafts) already lives in
singletons keyed by Room id. An observer also keeps `ChatService` from gaining a dependency
for a sidebar feature.

## Why in memory, and not a table

The Transcript already records whether the Human has answered a Mention. A table would be a
second copy that can disagree with it. Losing the list on a restart costs nothing, because
the scan rebuilds it. Losing a dismissal only makes a Room come back, which is the safe
direction.

## Rejected

| Alternative | Why not |
| --- | --- |
| Each Room adds and removes its Name | No Room object exists to do it, and a Name goes stale on rename |
| Mention as the only reason | A Question, a Form and a spent Budget block the Human more firmly than a Mention, and all three are already tracked |
| Opening a Room clears it | The Human may glance at a Room without time to answer |
| Every Agent reply in a one-to-one Room counts | Every such Room would be listed all the time |
| Persisting the list in `team.db` | A second copy of what the Transcript says |
| Reading whole Transcripts at startup | Startup time would grow with history. The last 50 Messages are enough |
| One component holding the logic and the chrome | A right-hand sidebar is planned and must reuse the list without the left sidebar's card |

## Consequences

- `IChatStore` gains `ReadTailAsync`, with a default body so existing test doubles still
  compile.
- `MessageRedelivered`'s comment, which says `AgentGateway` must be its only subscriber,
  is narrowed: no Blazor component may subscribe, because it would draw the Message twice. The
  tracker may, because it draws nothing.
- An unanswered Mention more than 50 Messages back is not rebuilt after a restart. This goes
  into `known-limits.md`.
- A dismissal does not survive a restart. This goes into `known-limits.md` too.
