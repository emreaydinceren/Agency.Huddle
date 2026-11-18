---
status: accepted
date: 2026-11-17
---

# Goldfish mode: a possession of one Agent in one Room

An Agent that reviews its own work reads it with the memory of writing it. It knows what it meant, what was agreed in
chat and what it already ruled out, so it forgives what a fresh reader would catch (issue #627). The Human wants a second
look from the same Agent, with the same Persona, Adapter and Model, but with an empty mind.

The design is in [Huddle.Goldfish-Specifications.md](../Huddle.Goldfish-Specifications.md). It builds on
[ADR-0024](0024-an-agent-holds-one-session-per-room.md) (one session per Room), [ADR-0065](0065-huddle-posts-as-itself.md)
(Huddle posts as itself) and [ADR-0091](0091-a-rooms-waiting-turns-are-kept-in-team-db.md) (the Turn Queue).

## Decision

**An Agent hands one Room to a clean session of itself. The brief is the only thing that crosses, it is posted word for
word, and a possession is an (Agent, Room) pair that Huddle tracks from the server.**

| | Decision |
| --- | --- |
| The brief | `goldfish(instructions)` takes one text, at most `Team:Goldfish:MaxBriefChars` (1000) characters. Huddle posts it word for word in the Room and again at the top of the result, so anyone can judge whether the goldfish was led (D-4). Nothing else crosses: no request Message, no history, no Memory, no Catch-up |
| What a possession is | The Agent's own identity with an empty mind. The warm Agent is set aside **in that one Room**; its other Rooms go on. The goldfish keeps its Skills and tools except the readers (D-6, O-8) |
| Telling the goldfish's calls apart | By (Agent, Room), not by session. An App Tool cannot tell which session called it, but it can tell which Room. From the runner's `Opened` report until the return step, every call from the possessed pair is the goldfish's. The same rule decides the stamp "Ruth · goldfish" on a Message, a Task write, a History entry and an `ask_human` card (D-14) |
| Mentions | While a possession is open, a Mention of the Agent in its Room is routed to the goldfish, and only that Message. A goldfish asks the way anyone asks, by posting; an answer is an ordinary Mention. There is no question tool (D-7, D-19) |
| Leaving | The goldfish leaves by `goldfish_done(result, findings?, outcome?)`. A Turn that ends without it means *waiting*: the possession stays until the next Mention (D-18). Huddle detects nothing |
| Calling back | "@Ruth comeback", exact match, from anyone but the goldfish, or the chip's **Call back** button. Huddle handles it before delivery (D-9) |
| Every exit returns the body | Finished, stuck, called back, failed and restarted all end through one `GoldfishSupervisor.EndAsync`, which posts the result and a return row that Mentions the Agent (D-8) |
| Never resumed | The clean session is never stored. A restart, an Adapter death or a Persona edit ends the possession as Restarted or Failed, and the Turn Queue brings the warm Agent's return Turn (D-6, D-16) |
| No limits | There is no time, Turn, spend or question limit and no "hit a limit" exit. The only guardrail is the call back. The Agent's own Room and token budgets apply as to any Turn (D-17) |
| One at a time | At most one open possession per Agent, enforced by a partial unique index on `goldfish_possessions`. No nesting: a possessed (Agent, Room) refuses `goldfish` |
| One Turn at a time | `goldfish` refuses while `Team:Acp:MaxConcurrentTurns` is above 1, because the (Agent, Room) rule is exact only when an Agent runs one Turn at a time (D-20) |
| Which Adapters | Claude Adapters with isolation only, where tool lists and read-deny rules are known to work. Any other Adapter refuses with a plain sentence (O-3) |
| What it may not read | `memory`, `propose_team_memory`, `search` and `read_records` refuse inside a possession (O-4, O-5). On disk, the session starts with `Read(...)` deny rules for the Agent's Memory, every Team, Project and Huddle-wide `memory/` folder and the Rooms' data |
| Where it lives | Server state in `goldfish_possessions` in `team.db`; a clean `RoomSession` in the Agent's own Adapter process; three additive envelopes. See [code-map.md](../engineering/code-map.md) |

## Why the possession is (Agent, Room) and not a second tool server

The first design gave each goldfish its own tool server, with its own list and a mark on every tool, so a call carried the
goldfish's identity. It would have been a second copy of every tool and of the wiring that offers them.

`TurnActivity` already tells a tool its Room, and the warm Agent cannot act in a possessed Room while the goldfish holds
it. So the pair names the caller exactly, and the existing tool server, tool list and stamp code serve both. The price is
the `MaxConcurrentTurns` refusal: with two Turns running at once, a tool call might not name one Room. Refusing to start is
simpler than solving that, and the default is already 1.

## Why no limits

The first proposal had 30 minutes, 5 Turns, a 2 USD cap, a daily cap and three questions of ten minutes each. Each limit
needed an exit, a notice and a test, and each guessed at what a good review costs. The owner chose the call back instead:
the Human sees the chip, and one Mention ends it. The cost is that a goldfish left waiting holds its Room until someone
speaks (known-limits.md).

## Rejected

| Alternative | Why not |
| --- | --- |
| A review tool or a review Task kind | The brief says what the work is; a new kind of Task would need its own rules (D-2, D-12) |
| Passing the request Message too | The brief is the only thing that crosses, so anyone can judge whether the goldfish was led (O-1) |
| A dedicated question tool, counter and timeouts | Questions are ordinary Messages; what the goldfish learns stays on the record (D-7) |
| Huddle detecting a question or finishing a Turn for it | A detected question is a guess. The goldfish knows whether it is waiting (D-18) |
| Resuming a clean session after a restart | Nothing is stored, so there is nothing to trust. The Agent comes back with a result and the Turn Queue (D-6) |
| A throwaway process, as a Helper runs | A second sign-in and a cold start, with a different process limit from the Agent's own (D-15) |
| Renaming the Room or tab, or a banner | A chip beside the Room name carries the live state, so nothing stale survives a crash (D-10) |

## Consequences

- **Three wire envelopes** are added, `goldfishBegin`, `goldfishEnd` and `goldfishReport`, and `ChatMessage.Goldfish`. An
  older client does not know a new envelope type and **rejects** it, so the server and the runner ship together; see
  [traps.md](../engineering/traps.md), "Adding to the wire". `ProtocolVersion.Current` stays 3.
- **Two App Tools**, `goldfish` and `goldfish_done`. `goldfish` is offered only when `Team:Goldfish:Enabled` is true and the
  Adapter qualifies.
- **New keys:** `Team:Goldfish:Enabled`, `MaxBriefChars` and `KeepDays`. See [the Engineering Guide](../Huddle.EngineeringGuide.md).
- **Known limits.** A `Bash` script can still read a file the deny list names; a waiting goldfish holds its Room; its
  `post_message` into another Room is not stamped; a folder created after the session opens is not denied. See
  [known-limits.md](../engineering/known-limits.md).
- In [language.md](../engineering/language.md), **Goldfish mode**, **brief** and **possession** are added.
