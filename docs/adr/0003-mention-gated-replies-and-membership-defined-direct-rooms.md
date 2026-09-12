---
status: accepted
date: 2026-09-09
---

> **Partially superseded by [ADR-0004](0004-direct-rooms-reply-without-mention.md):**
> mention-gating below still governs Group Rooms, but a Bot in a Direct Room
> now answers every Message regardless of Mention.

# Every Bot member receives every Message with a mentioned flag; Direct Rooms are defined by membership

In a Group Room every Bot member is sent every Message (so it has conversational
context) together with a `mentioned` flag and the resolved Mentions, and a Bot
is never sent its own Message. The server labels; it never decides who replies.
The convention that only a Mentioned Bot replies is what prevents Bot-to-Bot
reply storms, and excluding the sender removes the echo-loop hazard for naive
Agent Hosts. Separately, a Direct Room is "the Room whose Members are exactly
the Human and one Bot" rather than a Room with a `kind` column, because a stored
kind would have to be kept in sync on every membership change and would drift.

## Considered options

- Deliver only to Mentioned Bots: rejected; Bots lose context.
- No mention concept, Bots decide freely: rejected; highest storm risk.
- Room `kind` column (`direct` | `group`): rejected; drift risk on `/invite`.

## Consequences

- After `/invite`, a former Direct Room is a Group Room; the next registration
  of that Bot creates a fresh Direct Room named after it.
- Room names are display-only; nothing in the system keys on them.
- Offline Bots receive nothing (no queue in V1).

## Amendment, 2026-09-10

The mention convention is necessary but not sufficient. It stops a bot answering
its own message, which is what excluding the sender from fan-out enforces. It
does not stop two bots answering each other. A bot that quotes the text it
received reproduces the mentions in that text, so two echo-style bots in one room
amplify without bound. Sample bots therefore strip `@` from quoted text. The
structural fix, deferred, is to tell bots whether a human or a bot spoke, so an
agent host can simply decline to answer other bots.
