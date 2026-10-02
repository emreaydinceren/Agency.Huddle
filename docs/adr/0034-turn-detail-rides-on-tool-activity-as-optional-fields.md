---
status: accepted
date: 2026-09-30
---

# Turn detail rides on `ToolActivity` as optional fields, not as a new Envelope

**Turn detail** — a Draft's recent tool calls, each with its path, line and an **Edit preview** —
reaches the Room view by adding three optional members to the existing `ToolActivity` Envelope:
`Path`, `Line` and `Edit` (an `EditChange`). `ProtocolVersion.Current` stays 3. The full design is
[Huddle.TurnDetail-Specifications.md](../Huddle.TurnDetail-Specifications.md) (§6.2).

## The problem

The Adapter already sends what an Edit preview needs: a `diff` content block and `locations` on
every edit's tool call. `SessionUpdateMapper` drops both, and `ToolActivity` has nowhere to put
them: it carries a Room id, a Message id, a tool call id, a title and a status, and nothing else
([ADR-0008](0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md)).

The obvious shape for new data on the pipe, in this codebase, is a new Envelope, and ADR-0008 is
the precedent: `ToolActivity` and `StopTurn` were two new types, and they moved the version from 2
to 3. A reader of the code will reasonably ask why `EditChange` is not its own Envelope. This
records why it is not.

## The decision

- **`ToolActivity` gains three optional trailing members:** `Path` (a string), `Line` (an integer)
  and `Edit` (an `EditChange` holding the old text, the new text, whether either was clipped, and
  how many further changes the call carried).
- **Null members are omitted from the line.** `ProtocolJson` writes with
  `DefaultIgnoreCondition = WhenWritingNull`, so an activity that uses none of them is
  byte-identical to today's, and the literal pinned by `ProtocolJson_RoundTripsToolActivity` stays
  valid without an edit. That test becomes the regression proof for this decision.
- **`ProtocolVersion.Current` stays 3.** `ProtocolVersion_StillThree` and
  `ProtocolVersion_IsStill3` are not touched, and `tools/echo-bot.ps1`, which hardcodes the
  version, needs no change.
- **The server sanitises the new members and never rejects on them.** It clips or drops what is
  too long or invalid and carries on, so a large preview can never cost a status change. The
  limits are constants in one class in `Huddle.Contracts`, shared by the runner and the pipe.
- **This covers data that belongs to a tool call, and no more.** A Plan or Thinking is not a tool
  call and needs its own carrier. That is a later decision, made when an Adapter sends either.

## Why an optional member is safe where a new type was not

A closed `[JsonPolymorphic]` throws on a `"type"` it has not registered, which is exactly why
ADR-0008 had to bump the version: an old reader cannot skip a line it cannot name. A new *member*
on a type the reader already knows is a different thing. `System.Text.Json` ignores it, and
`MessagePosted_WithUnknownProperties_StillDeserialises` already proves that for these options.
[Traps](../engineering/traps.md) draws the same line: a new property is additive, a new type is not.

The direction helps too. `ToolActivity` travels from the runner to the server, and both run in
the same process, so the only client that sends the new members is one that was built with them.
A pipe client that predates them never sends `ToolActivity` with them, and a server that
predates them ignores them.

## Rejected

| Alternative | Why not |
| --- | --- |
| A new `EditPreview` Envelope | A new type throws in the closed polymorphic, so it forces version 4: `echo-bot.ps1` (which hardcodes 3), every pinned-JSON test, and every client in one commit. The data describes a tool call that already has a row, so a second Envelope would need joining back to it by `ToolCallId` |
| A new Envelope with no bump, following `ReadTranscript` and `TranscriptTail` | That argument holds for a reply the server sends only when asked, so an old client never receives one. `ToolActivity` is pushed from the runner, and an old server would answer the unknown type with `BadMessage`. [The decision record](../engineering/decisions.md) (D-17) applies the same rule: a new Envelope needs version 4, where an additive `RoomInfo.IsEmpty` needed no bump |
| The runner writing `Drafts` directly | It bypasses the pipe that every `PersonaRunner` test stands on through `FakePersonaServer`, and it makes the Room view depend on something the pipe does not carry |
| Making the protocol tolerate unknown types | ADR-0008 already declined it: it weakens a strict-equality check that catches real mistakes loudly, and it is a decision of its own rather than a rider on this one |

## Consequences

- `ToolActivity` is no longer "the title and the status and nothing else". ADR-0008's reason for
  shipping one type rather than two — that neither was rendered beyond its title — no longer
  holds, and its note that per-kind detail would need a bump is superseded for any detail that can
  be an optional member.
- **The precedent:** display data for a tool call grows `ToolActivity` by optional members, and a
  new Envelope type still costs a bump. Whoever adds the next member should add a test that an old
  literal still serialises unchanged.
- The wire carries file contents, clipped to 4,096 characters a side. They reach only the Room
  view: `RoomEvents.DraftChanged` is documented never to be delivered to an Agent.
- Every member is a display hint. Nothing decides a Turn, a Reply Gate outcome or a Budget from
  them.
- If `System.Text.Json` does not bind the optional constructor parameters of this closed
  polymorphic record as expected, the round-trip tests in the first wire task fail before any
  other code depends on it, and this ADR is revisited rather than worked around.