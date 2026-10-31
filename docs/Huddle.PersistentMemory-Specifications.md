# Huddle.PersistentMemory — Requirements and Design Specification

**Date:** 2026-10-16 (rewrite; first draft 2026-10-11) · **Status:** Proposed, not built ·
**Decision record:** none yet (§12 lists the ADR candidates) · **Builds on:**
[the Room Sessions spec](Huddle.RoomSessions-Specifications.md) (cited as *RS §n*),
[the Commands spec](Huddle.Commands-Specifications.md),
[the Instructions and Memory spec](Huddle.InstructionsAndMemory-Specifications.md) (being built in
PR #287; [ADR-0070](adr/0070-an-agent-reads-the-instructions-the-human-writes.md),
[ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)),
[ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md),
[ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) and
[ADR-0051](adr/0051-search-keeps-no-index-of-its-own.md) · **Vocabulary:**
[language.md](engineering/language.md), plus the proposed terms in §1.4 · **Roadmap:**
[item 26](engineering/roadmap.md)

This page specifies how a Room can go on for months, the way two people keep talking for months,
without a Teammate losing the thread when its session is lost and without the session going stale
as it fills. It is for the engineers and agents who will build it, and for the Human deciding how a
Room should behave. If you do not build software, read only the
[Executive summary](#executive-summary). Otherwise read §1 for why, §3 for the requirements, §4 to
§7 for the design and §9 for the order it ships in.

The decisions to hold on to while reading:

- **Measure before building memory.** The cheap levers come first: keep sessions longer, let a fresh
  session reread far more of the Room, and start a fresh session on purpose when the old one has
  degraded. A summary layer is built only if a recorded experiment (§9.1) shows those are not enough.
- **A fresh start, not a squeeze.** When a session degrades, Huddle replaces it with a fresh session
  briefed from the Room's own record, which the Human can read. It never compacts on its own;
  `@Nova /compact` stays the Human's tool (ADR-0035).
- **Automatic fresh starts are Off for every Room until the Human turns them on.**
- **Facts live in Memory; the conversation lives in the Transcript.** Durable facts (a decision, a
  preference, a correction) belong to the `memory` App Tool of the
  [Instructions and Memory spec](Huddle.InstructionsAndMemory-Specifications.md), which every
  session reads at its start. This spec adds no fact store; it makes sure a fresh session also
  rereads enough of the conversation itself.

> [!IMPORTANT]
> Binding before any code in this spec is written: [rules.md](engineering/rules.md) before editing
> `src/Huddle.App`, [traps.md](engineering/traps.md) before touching `Huddle.Contracts` or the wire,
> and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides them. Where this spec proposes a type, file or key that does not exist yet, it says
> *proposed*.

---

## Executive summary

*For readers who do not build software. Nothing here needs the rest of the page.*

### The problem in one paragraph

You can talk to an AI Teammate in a Room for months, and every word is kept. But the Teammate does
not remember the way you do. Its working memory is a **session**: a notepad holding the
conversation so far. The notepad is sometimes thrown away (after an update, a Restart, or about 30
days without use), and then the Teammate rereads only the last 20 Messages. Even when it survives,
it fills up; the AI squeezes it into a summary nobody checked, and the Teammate starts to repeat
itself and drift. The goal is a Room that can go on as long as a friendship does, without the
Teammate forgetting what you decided or falling into a rut.

### The strategies, in plain words

| Strategy | Everyday comparison | What it does for you | When |
| --- | --- | --- | --- |
| **The record** | A stenographer's full record | Every Message is kept forever, word for word. Nothing here ever changes it | Exists today |
| **Keep the notepad longer** | Not shredding last month's notes | Claude Code throws a session away after about 30 days unused. Huddle would keep it for a year, so a quiet Room picks up where it left off | Step 0, after a check that it works |
| **Reread more** | How many pages of the minutes a colleague rereads before a meeting | When a Teammate does start afresh, it rereads as much of the Room as fits a size you choose, not just 20 Messages. Its own old suggestions are skipped first, so it does not repeat itself | Step 1 |
| **Fresh start** | A clean notebook page, with the recent pages clipped to the front | When a session has gone stale, Huddle retires it and starts a new one that rereads the Room. You can ask for one in a Room at any time; automatic fresh starts are off until you turn them on for a Room | Step 2 |
| **Squeeze** (`@Nova /compact`) | Asking someone to boil their notes down to one page | Already built. You can tell a Teammate to compress its notepad, even saying what to keep. Huddle never does this on its own, because you cannot read or check the squeezed version | Exists today |
| **The experiment** | Testing a new routine for a month before buying equipment | Before anything bigger is built, you ask a long-running Teammate ten questions about old decisions, and count repeated suggestions, before and after each step. The scores decide whether more is needed | Step 3 |
| **Handoff note** (only if the experiment says so) | A colleague's handover note before leave | Just before a fresh start, the Teammate writes a short note: decisions, your preferences, open questions, what it was doing. The new session reads it. You can read and edit it | Step 4, conditional |
| **Saving lasting facts** (the `memory` tool) | The Teammate's own notebook, and the team noticeboard | Being built separately ([Instructions and Memory](Huddle.InstructionsAndMemory-Specifications.md)). The Teammate writes down decisions, your preferences and corrections as it goes, and every new session starts by reading the notebook's index. So what is written there survives every fresh start | Arriving separately, soon |
| **House rules** (Instructions) | The rules pinned on the office wall | Also in that separate feature: rules you write once, which every session starts with. A fresh start is also how a Teammate picks up rules you changed for everyone | Arriving separately, soon |
| **Look it up** | Searching your email for the exact words | A later, separate feature. Not part of this plan | Not planned here |

### What it costs, and what can go wrong

- **Money.** Rereading more of a Room makes a fresh session's first answer cost more. A fresh start
  is rare (at most a few a day) and is shown in the Room.
- **Losing "how".** A fresh session rereads what was *said*, not the tools the Teammate used or its
  reasoning. That is why Huddle keeps a healthy session for as long as it can and starts fresh only
  when the old one has degraded.
- **Nothing new is stored about you** by steps 0 to 3. What a Teammate chooses to remember goes in
  its notebook (the `memory` tool), which you can read and edit like any file. The handoff note in
  step 4 would be the first new thing this plan keeps, and it would be visible and editable.

### What changed after review

The first draft (2026-10-11) designed a large memory system: background summaries by day, week and
quarter, automatic saving of facts, a search tool and fresh starts last. Two adversarial reviews
and a second opinion on 2026-10-16 found that published benchmarks favour rereading the original
words over summaries at these sizes, that automatic fact-saving opens a door to planted
instructions, and that the fresh start was the best-supported idea but was scheduled last. This
version puts the cheap, proven steps first and makes any memory layer earn its place.
[Appendix A](#appendix-a-what-changed-from-the-first-draft) lists every change and why.

---

## Contents

1. [Purpose, background and scope](#1-purpose-background-and-scope)
2. [Principles](#2-principles)
3. [Requirements](#3-requirements)
4. [Session retention and isolation (phase 0)](#4-session-retention-and-isolation-phase-0)
5. [Catch-up by budget (phase 1)](#5-catch-up-by-budget-phase-1)
6. [Fresh starts (phase 2)](#6-fresh-starts-phase-2)
7. [Compaction's place](#7-compactions-place)
8. [Behaviour and edge cases](#8-behaviour-and-edge-cases)
9. [Phasing and the experiment](#9-phasing-and-the-experiment)
10. [Testing](#10-testing)
11. [Configuration](#11-configuration)
12. [Open questions and ADR candidates](#12-open-questions-and-adr-candidates)
13. [Choosing automatic fresh starts](#13-choosing-automatic-fresh-starts)
14. [Appendix A: what changed from the first draft](#appendix-a-what-changed-from-the-first-draft)
15. [Appendix B: sources](#appendix-b-sources)

---

## 1. Purpose, background and scope

### 1.1 The two decays

**Session loss.** A Room Session is resumed by its stored id where the Adapter advertises resume
(RS §6.6; `session/resume` is stable in ACP since 0.12.2). The next Turn opens a fresh session
instead when resume answers "not found" (RS §9 E-1), when the Human presses Restart, or when an
edit to the Persona forgets every Room Session (`RoomSessionStore.ForgetAll`, RS §6.13). "Not
found" is predictable, not rare: Claude Code deletes a session's transcript after
`cleanupPeriodDays` (default 30), so **a Room unused for a month always loses its session.** The
fresh session's first Turn carries Transcript Catch-up: at most
`Team:Acp:TranscriptCatchUpMessages` (default `20`) Messages, rendered by
`RoomSession.AppendTranscriptBlock`. Messages only: no tool calls, no reasoning.

**Decay inside a live session.** A session that is never lost fills. The Adapter compacts it into
its own summary, which nobody reviews, and later Turns grow vaguer and repeat themselves: the
**rut**. Multi-turn performance drops sharply, and models do not recover from an early wrong turn
(Laban et al., Appendix B). On current Claude models with a 1M-token window, Claude Code compacts
automatically only near 967K tokens, so a session can carry a very large, degraded context for a
long time first.

### 1.2 The goal

A Room that can run indefinitely without either decay, at a cost the Human can see, and without
storing anything about the Human that they did not choose to keep.

### 1.3 What exists today

| Lever | Status |
| --- | --- |
| The Transcript, `{DataDir}/rooms/{roomId}.jsonl` | Exists. The source of truth; this spec never edits it |
| Resume by stored id | Exists (RS §6.6) |
| Transcript Catch-up, 20 Messages | Exists (RS §6.5) |
| `@Nova /compact [instructions]` | Exists ([Commands spec](Huddle.Commands-Specifications.md)): the Human compacts one Teammate's session, optionally saying what to keep. The Teammate posts *"Compacted my conversation: 50,624 → 2,964 tokens in 9 s."* |
| Restart on the Teammate card | Exists: forgets **every** Room Session of that Teammate |
| Isolation from the Human's Claude settings | Built, **unverified**: the built-in Claude profile sends `settingSources: ["project", "local"]` and `autoMemoryEnabled: false` (`AdapterProfile.IsolateUserSettings`, true on the synthesised profile, false by default on a configured entry). Not yet checked live (Task 14.3.m) |
| Usage reporting | Exists: `UsageUpdated(Size, Used)` from ACP `usage_update`, read in `RoomSession`, which keeps `lastUsed`. `compaction_update` is **not** mapped |
| Memory and Instructions | **Being built** (PR #287, the [Instructions and Memory spec](Huddle.InstructionsAndMemory-Specifications.md)). Instructions at three levels and the Memory and Team Memory indexes go into the system prompt at **every session start**. The `memory` App Tool writes topic files, and its Prompt tells the Agent to save as it goes, to "assume your context can reset at any moment", and to re-read after a restart |
| Search | Exists: the `search` App Tool finds Library text. Nothing searches a Room's Transcript for an Agent |

### 1.4 Proposed vocabulary

Proposed here; added to [language.md](engineering/language.md) with the ADR that accepts this
spec, not before.

| Term | Meaning | Avoid |
| --- | --- | --- |
| **Fresh start** | Huddle closing one Room Session on purpose and opening a new one for that Room, whose first Turn carries Catch-up. Automatic or asked for by the Human | rotation (fine in prose about the design), reset, Restart (that is the card's button, which forgets every Room) |
| **Catch-up budget** | The most of the Room's Transcript, in estimated tokens, a session's first Turn rereads | context size, window |
| **Degraded** | A Room Session that has met a fresh-start trigger (§6.2) | full, stale, broken |
| **Handoff note** | *Phase 4, conditional.* A Markdown note the old session writes just before a Fresh start, read by the new one | summary on its own, digest, memory |

### 1.5 Non-goals

- **No background summariser, no fact store, no promotion, no index.** The first draft's Digest,
  Consolidation, `remember` and Memory Proposals are out (Appendix A). Durable facts belong to the
  `memory` App Tool (ADR-0071), and this spec changes nothing in it. A Transcript `recall` tool is a
  separate, later feature; if built it follows ADR-0051.
- **No automatic compaction.** Huddle never sends `/compact` on its own (§7).
- **No change to the Transcript, and no change to Room kinds.**
- **No reliance on any Adapter's own memory.** Isolation must be verified, not assumed (§4.2).

---

## 2. Principles

1. **The Transcript is the truth.** A fresh session rereads the Room's own words, not a model's
   summary of them.
2. **Prefer resume while healthy; start fresh when degraded.** A resumed session keeps tool calls
   and reasoning that a reread cannot restore. Huddle starts fresh only on a trigger, never on a
   clock alone.
3. **Off until chosen** for anything automatic. A Human-asked fresh start is always available.
4. **Visible.** Every fresh start is announced in the Room, and the Human can read exactly what the
   new session was given.
5. **Fail toward today.** Any failure in this feature leaves the Room working as it does now.
6. **Adapter-agnostic.** Everything reaches the model as prompt text. An Adapter that reports no
   usage still gets the Turn-count trigger; one with no `/compact` loses nothing.
7. **Measure, then build.** Each step must show a measured gain (§9.1) before the next is built.
8. **Facts in Memory, conversation in the Transcript.** A fresh session is briefed by two things,
   in this order: the system prompt (Persona, Instructions and the Memory and Team Memory indexes,
   all read at session start), then the Catch-up reread of the Room. What must survive *as a fact*
   is the `memory` tool's job; what must survive *as conversation* is this spec's.

---

## 3. Requirements

Each requirement has a stable id. **MUST** is a condition of acceptance; **SHOULD** is expected
unless a recorded reason says otherwise.

### 3.1 Baseline (every phase)

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R1 | Nothing in this feature MUST edit, reorder or delete a Transcript line. | all |
| PM-R2 | With every setting at its default, the system prompt and every Turn prompt MUST be byte-identical to today's, and the existing goldens MUST stay unchanged. | all |
| PM-R3 | Nothing in this feature MUST make a model call other than an Agent's own Turns (and, in phase 4, the handoff Turn). | all |

### 3.2 Retention and isolation (phase 0)

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R4 | Before phase 2 ships, the isolation of a Claude-profile session from the Human's `~/.claude` settings and auto memory MUST be verified live (Task 14.3.m), and the result recorded in [known-limits.md](engineering/known-limits.md). | 0 |
| PM-R5 | A configured Adapter entry whose `UsesToolNamePrefix` is true and whose `IsolateUserSettings` is false SHOULD log one Warning at start-up naming the entry. | 0 |
| PM-R6 | If V0-2 (§4.1) shows session retention can be set for an isolated session, Huddle MUST set it to `Team:Acp:SessionRetentionDays` (default 365) for every Claude-profile session. If it cannot, the finding MUST be recorded and nothing else in this spec changes. | 0 |

### 3.3 Catch-up budget (phase 1)

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R7 | A first Turn's Catch-up MUST be bounded by `Team:Acp:TranscriptCatchUpTokens` (estimated tokens) as well as by `TranscriptCatchUpMessages` (a Message cap). Whichever is reached first ends it. | 1 |
| PM-R8 | Tokens MUST be estimated at three characters per token, rounding up, and the estimate MUST be called an estimate wherever it is shown. | 1 |
| PM-R9 | Both values MUST be settable on the Settings page and MUST apply from the next first Turn without restarting the app, a Persona or a session. A Settings value overrides the configuration default. | 1 |
| PM-R10 | Changing either value MUST NOT forget any Room Session and MUST NOT be part of the `Persona` record, so `PersonaSupervisor.NeedsRestart` never fires on it. | 1 |
| PM-R11 | When the budget cuts the Catch-up, the receiving Agent's **own** Messages MUST be dropped before anyone else's, oldest first, except its newest `CatchUpKeepOwnMessages` (default 5). Only then are other Messages dropped, oldest first. The kept Messages stay in Transcript order. | 1 |
| PM-R12 | The existing `turn.transcriptOmitted` line MUST count every dropped Message. When some dropped Messages were the Agent's own, a proposed `turn.transcriptOmittedOwn` line MUST say how many. | 1 |
| PM-R13 | The Message cap MUST accept 1 to 2000 and the token budget 1000 to 150000. A value outside the range MUST be refused with a message, never clamped silently. (A value of 0 is not offered: `HandleReadTranscriptAsync` refuses `Max < 1`.) | 1 |
| PM-R14 | The defaults MUST stay `20` Messages and a token budget large enough not to bind at 20 Messages, so PM-R2 holds until the Human changes them. | 1 |

### 3.4 Fresh starts (phase 2)

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R15 | The Human MUST be able to ask for a fresh start of one Teammate's session in one Room, from the Room, without affecting that Teammate's other Rooms. | 2 |
| PM-R16 | Automatic fresh starts MUST be a per-Room setting, **Off** for every Room, existing and new, the Chief of Staff's included. Only the Human can turn it on; no App Tool, Persona field, Team type or Agent can. | 2 |
| PM-R17 | With automatic fresh starts on, a Room Session MUST be marked **degraded** at the end of a Turn when any trigger of §6.2 fires. | 2 |
| PM-R18 | A fresh start MUST happen only between Turns, immediately before the next item starts, and never while a Question or Form waits in that Room. | 2 |
| PM-R19 | A fresh start MUST close the old session, forget its `RoomSessionStore` entry, and open a new session through the existing lazy-open path, so its first Turn carries Catch-up at the configured budget. | 2 |
| PM-R20 | A fresh start MUST NOT count as a Turn failure, touch the failure streak or post a Transcript Message. | 2 |
| PM-R21 | Every fresh start MUST be shown in the Room as a Room-level note (ADR-0012's slot), naming the Teammate, the reason and what the new session reread, for example *Nova started afresh after its conversation was compacted. It reread the last 38 Messages (about 31,000 tokens).* | 2 |
| PM-R22 | The text a fresh session's first Turn carried MUST be viewable from that note, as sent. | 2 |
| PM-R23 | Automatic fresh starts MUST be limited to `FreshStarts:MaxPerDay` (default 3) per Room Session per local day. A trigger past the limit is logged and skipped, never queued. | 2 |
| PM-R24 | A Turn that is a Human-run Adapter command (`@Nova /compact`) MUST NOT trigger a fresh start: the Human chose compaction. | 2 |
| PM-R25 | With automatic fresh starts Off, no trigger MUST be evaluated for that Room, so its behaviour is exactly today's. | 2 |

### 3.5 Handoff note (phase 4, conditional)

PM-R26 and onward are written only if the experiment (§9.1) shows phases 0 to 2 fall short. §9.2
sketches the candidate.

---

## 4. Session retention and isolation (phase 0)

Phase 0 is two live checks and, if the first passes, one setting. It costs no model calls beyond
the checks.

### 4.1 V0-2: can retention be set?

Claude Code reads `cleanupPeriodDays` from its settings. An isolated session (§1.3) loads only
`project` and `local` settings, so the Human's `~/.claude/settings.json` value does not apply to
it. Two routes, both **unverified**:

| Route | Concern |
| --- | --- |
| A project setting in the Persona's Work Dir, `.claude/settings.json` with `cleanupPeriodDays` | The Work Dir is the Agent's own folder and is not a jail; the Agent can edit the file. Whether the adapter's transcript cleanup reads project settings at all is unknown |
| The `settings` `_meta` option | It **replaces** the adapter's computed settings rather than merging (AdapterProfile remarks, correction item 22), so it would drop `CLAUDE_MODEL_CONFIG` and `autoMemoryEnabled: false` unless both are re-sent |

The check: set the value by one route, leave a test session idle past a shortened period on a
scratch install, and confirm resume still succeeds. Record the outcome in
[known-limits.md](engineering/known-limits.md). If neither route works, phase 0 ends with the
finding; nothing later depends on it.

### 4.2 V0-1: is isolation real?

Task 14.3.m, already planned: confirm that a session started with the isolation `_meta` does not
load the Human's `CLAUDE.md`, hooks or auto memory, and cannot write auto memory. It matters here
because auto memory is stored per Work Dir, survives every fresh start and compaction, and would
make the experiment in §9.1 measure Claude Code's memory rather than Huddle's. Once the `memory`
App Tool ships, Claude Code's auto memory would also be a second, unmanaged notebook beside it.

Run V0-1 in the same paid session as the Instructions and Memory spec's **spike S-1** (its OP-5):
whether Claude Code loads ancestor `CLAUDE.md` files into a Persona. Both turn on what
`settingSources: ["project", "local"]` actually loads for a Work Dir, and both are recorded in
[known-limits.md](engineering/known-limits.md).

---

## 5. Catch-up by budget (phase 1)

### 5.1 Where it changes

`RoomSession` reads `this.options.TranscriptCatchUpMessages` at the first-Turn Transcript read.
Phase 1 adds a proposed `ISessionSettings` (one implementation, `SessionSettingsStore` over
`{DataDir}/session-settings.json`), read at that point per first Turn:

1. Ask the server for up to the **Message cap**, as today (`ReadTranscript.Max`). No wire change.
2. Trim the returned Messages to the **token budget** in the runner, by PM-R11's order.
3. Render with today's header and `Omitted` line, plus `turn.transcriptOmittedOwn` when it applies.

`TranscriptTail` is already capped at half of `JsonLineStream.MaxLineBytes` (512 KB), roughly
170,000 tokens at three characters per token, so the largest allowed budget fits one Envelope.

### 5.2 `session-settings.json`

```json
{ "transcriptCatchUpMessages": 400, "transcriptCatchUpTokens": 40000 }
```

Overrides only, like `prompts.json` and `library-roots.json`: absent is normal, the app does not
create it, it appears on the first save from Settings, and it is hand-editable and watched.

### 5.3 Why the Agent's own Messages go first

A Teammate in a rut repeats framings and suggestions it already made. Rereading thirty of its own
proposals re-seeds exactly that (self-conditioning, Appendix B). The Human's Messages carry the
decisions, preferences and questions the Teammate most needs. Keeping the newest few of its own
keeps the thread of what it last said.

### 5.4 Settings

A new **Settings › Memory** tab: *Messages a fresh session rereads* (cap) and *Size of what a fresh
session rereads* (budget, shown as "about N tokens"), each with its default and a Reset. Copy
follows [language.md](engineering/language.md): "you" for the Human, never "user", "chat" or
"conversation". Check [agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) before building.

---

## 6. Fresh starts (phase 2)

### 6.1 Two ways in

| Way | Who | Available |
| --- | --- | --- |
| **Start afresh** in the Room menu, per Teammate | The Human | Every Room, always (PM-R15) |
| Automatic, on a trigger | Huddle | Only in a Room with automatic fresh starts on (PM-R16) |

*Start afresh* is the Room-scoped sibling of the card's Restart: it forgets one Room Session, not
all of them. It needs a proposed additive Envelope from server to runner, `StartAfresh(RoomId,
AgentName)`, so `ProtocolVersion.Current` stays as it is (traps.md: adding to the wire is not
changing it).

### 6.2 Triggers

Evaluated at the end of each ordinary Turn (never a Command Turn, PM-R24), only with automatic
fresh starts on:

| Trigger | Fires when | Default |
| --- | --- | --- |
| **Observed compaction** | `Used` falls by more than `FreshStarts:CompactionDropPercent` between two `UsageUpdated` within one ordinary Turn. The Adapter has just replaced the context with its own summary; Huddle replaces it with the Room's own words instead | 50 % |
| **Size** | `lastUsed` reaches `FreshStarts:AboveTokens` | 200,000 |
| **Turn count**, fallback | The session has completed `FreshStarts:AfterTurns` Turns **and** has reported no usage at all | 80 |

- The size trigger is absolute, not a percentage of `Size`. `Size` is the model's window (1M on
  current Claude models), not the point where Claude Code compacts (about 967K, or wherever the
  `autoCompactWindow` setting puts it), so a percentage fires far too late or never.
- The first `UsageUpdated` of a resumed session is a baseline (D22 correction 9) and never counts
  as a fall.
- The Turn-count fallback exists for an Adapter such as `agency-acp` on a local model, which reports
  no usage and has no compaction to observe.
- A degraded mark set by one trigger is cleared by the fresh start; triggers are not re-evaluated
  until the new session's first Turn has finished.

### 6.3 What happens

At the next item admitted to a degraded Room Session (or at once, for *Start afresh*, when the
session is idle):

1. If a Question or Form waits in the Room, wait; the mark stays (PM-R18).
2. If today's automatic count has reached `MaxPerDay`, clear the mark, log, and run the item on
   the old session (PM-R23). *Start afresh* is never limited.
3. Close the session, call `RoomSessionStore.Forget(name, roomId)`, and open fresh through the
   existing lazy-open path, so `firstTurnPending` is set (PM-R19).
4. The item's Turn carries Catch-up at the configured budget (§5). The new session's system prompt
   is composed afresh, so it also carries the current Instructions and Memory indexes, including
   Huddle-wide Instructions edits that a running session never sees (Instructions and Memory §3.4).
5. Send the Room note (PM-R21) through a proposed additive Envelope, `FreshStartNote(RoomId,
   AgentName, Reason, MessageCount, EstimatedTokens)`, and write the first Turn's text to
   `{DataDir}/room-sessions/last-catch-up/{Name}/{roomId}.md` for PM-R22.

The old session is not deleted from the Adapter in v1. ACP's `session/delete` is stable and
`claude-agent-acp` advertises it, but `IAgentSession` has no delete today and `Huddle.Acp` belongs
to the ACP effort (Q-4).

### 6.4 Cost

A fresh session's first Turn is a cold start: no prompt cache, the system prompt plus the Catch-up.
That refill is real spend and is counted against the token Budget like any first Turn. `MaxPerDay`
bounds it. Rotating rarely, on degradation only, keeps the cost near what one compaction costs.

### 6.5 The Room setting

Stored in a proposed `{DataDir}/room-fresh-starts.json`, keyed by Room id: `{ "automatic": true,
"changedUtc": "…" }`. Absent means Off. A file, not a column, because a new column on `rooms` is a
known trap (rules.md) and the runner already reads `{DataDir}` stores such as `RoomSessionStore`.
Shown in the Room menu as *Start afresh automatically when a Teammate's conversation gets too long*,
with a link to §13's guidance.

---

## 7. Compaction's place

| Option | Decision | Why |
| --- | --- | --- |
| The Human runs `@Nova /compact keep the decisions and open questions` | **Keep, and document it** in the Settings › Memory tab and §13 | Already built, costs nothing new, and the Human can steer what is kept. But the summary is the Adapter's, invisible, Claude-only and not in the Transcript: a tool, not the strategy |
| Huddle compacts automatically | **No** | It would reverse ADR-0035 ("only the Human") for a worse result than a fresh start: an unreviewable summary with no link back to the Messages, on one Adapter only |
| An observed compaction triggers a fresh start | **Yes**, the main trigger (§6.2) | The Adapter has already thrown detail away; replacing its summary with the Room's own words costs one cold Turn and makes the trade visible |
| A fresh start instead of compaction | **Yes**, by default where it is on | Reviewable, adapter-agnostic, and it moves the model off the path it was stuck on, which compaction does not |

A Human who runs `/compact` keeps the compacted session: PM-R24 makes sure Huddle does not undo the
choice by starting afresh straight after.

---

## 8. Behaviour and edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| PM-E1 | Resume answers "not found" (RS E-1) | Unchanged, except the fresh session's Catch-up uses the phase 1 budget |
| PM-E2 | Resume throws (RS E-2) | Unchanged |
| PM-E3 | `ReadTranscript` refused or timed out (RS E-3) | Unchanged: the Turn goes ahead without the block. After a fresh start, the Room note says *It could not reread the Room* |
| PM-E4 | A successful resume | Unchanged: the gap after the stored `LastMessageId`, now also bounded by the budget. No fresh start is triggered by a resume |
| PM-E5 | `SessionPerRoom: false` (shared session, `RoomSession.RoomId` null) | No Catch-up today and none here; no trigger is evaluated. *Start afresh* is not offered for it |
| PM-E6 | A Human's `/compact` drops `Used` | Not a trigger (PM-R24) |
| PM-E7 | Restart, or a Persona edit that forgets every Room Session | Unchanged (`ForgetAll`) |
| PM-E8 | The Human clicks Stop during a fresh session's first Turn | A Stop like any other; the new session stays |
| PM-E9 | Several Teammates in one Room | Each Room Session is judged and started afresh on its own |
| PM-E10 | The Room is archived or deleted | Archiving changes nothing (rules.md: archiving never reaches delivery). Deleting a Room drops its `room-fresh-starts.json` key and its `last-catch-up` files |
| PM-E11 | A Persona is renamed | `PersonaRenameCascade` moves `last-catch-up/{Name}/`, above the "no Agent row" early return (rules.md) |
| PM-E12 | `session-settings.json` corrupt | Ignored with a Warning; configuration defaults apply; Settings shows the error |
| PM-E13 | A Room's Transcript is larger than the budget by far | Only the newest part within the budget is reread; the `Omitted` lines say how much was left out |
| PM-E14 | Two triggers fire in one Turn | One fresh start; the note names the first in §6.2's order |

---

## 9. Phasing and the experiment

Each phase ships on its own and leaves the app coherent if the next never comes.

| Phase | Ships | Acceptance |
| --- | --- | --- |
| **0. Retention and isolation** | Live checks V0-1 and V0-2; `SessionRetentionDays` if V0-2 passes; PM-R5's Warning | PM-R4 to PM-R6. Findings recorded in known-limits.md |
| **1. Catch-up by budget** | `ISessionSettings`, the token trim, `turn.transcriptOmittedOwn`, Settings › Memory | PM-R7 to PM-R14. Goldens unchanged at the defaults; a changed value reaches the next first Turn, fresh or resumed, without a restart |
| **2. Fresh starts** | *Start afresh*, the per-Room setting, the three triggers, the Room note, `last-catch-up` | PM-R15 to PM-R25. A Room with the setting Off behaves exactly as phase 1 |
| **3. The experiment** | A manual test page and a recorded result (§9.1); no code | A result recorded, with the go or no-go for phase 4 |
| **4. Handoff note** | Only on a "go" from phase 3; specified then | To be written |

### 9.1 The experiment

Run on a real long-running Room (the Chief of Staff's is the intended one), with phase 0 V0-1
passed so Claude Code's own memory is not in play, and with the `memory` App Tool live, because
that is how the Room will really run. Record everything in
`engineering/manual-tests/room-memory.md` (written with phase 1, listed in [docs/Index.md](Index.md)
in the same change).

1. **Recall score.** From the Transcript, pick 10 facts or decisions older than the last 20
   Messages; note their Message ids.
2. Restart the Teammate at the default Catch-up; ask the 10 questions; score 1 for each correct
   answer, and note for each whether the answer came from a memory file (the Teammate read one, or
   it is in the index) or from the reread. Note the first Turn's cost from the Teammate card.
3. Set the budget to 40,000 tokens; Restart; ask again; score and cost.
4. **Rut score.** In a live session, ask "suggest three things we haven't tried yet" five Turns
   apart, three times; count suggestions that repeat. Then run `@Teammate /compact keep the
   decisions and open questions` and repeat the count. Then *Start afresh* and repeat it.
5. **Decision.**
   - Recall at 40,000 tokens 7 of 10 or better **and** fewer repeats after a fresh start than after
     compaction: phases 0 to 2 meet the goal; turn automatic fresh starts on where wanted, and do not
     build phase 4.
   - Recall below 7 of 10, and the missed answers are durable facts the Teammate should have saved:
     the fix is the Memory Prompt (`systemPrompt.memory`), not this spec. Tune it and repeat.
   - Recall below 7 of 10, and the missed answers are conversation (what was being worked on, open
     questions, the reasoning behind a choice): build phase 4, then repeat the experiment with it.
   - No difference in repeats: record it; the rut claim is unproven for this Room (Q-6).

### 9.2 Phase 4 candidate: the handoff note

Sketch only, for the case the experiment calls for it. With the `memory` App Tool live, decisions
and preferences should already be in Memory, so the note carries only what does **not** belong
there: what the Teammate was doing, the open questions, and the reasoning behind recent choices.

The cheapest form reuses the memory tool's own habit: just before a fresh start, the old session
gets one extra Turn with a fixed Prompt (proposed `turn.handoffRequest`) asking it to save anything
durable to Memory and to write a short note of the open work, each line citing Message ids. The note is stored at
`{DataDir}/room-sessions/handoff/{Name}/{roomId}.md`, editable by the Human, and placed above the
Catch-up in the new session's first Turn.

Why this rather than the first draft's background Digest: the old session knows its own tool calls
and reasoning, which no reread can recover; it needs no separate model host (the first draft's Q-8),
no daily caps and no background spend; and it is written once per fresh start, not continuously.
Its risks must be specified before it is built: a note is a model summary and can be wrong
(benchmarks favour raw text, Appendix B), and planted text in the Room can reach it, so it must be
marked as the Teammate's own note, visible, and never promoted anywhere.

---

## 10. Testing

Test-first, per [agents/Testing.md](../agents/Testing.md): every test has a `///` summary,
user-facing text is asserted exactly, and a test that passes on arrival gets a mutation. Fixtures:
`TempDataDir`, `ManualTimeProvider`, `FakePromptSource`, `FakeAgentHostFactory`, `FakeAgentSession`.

| Test class (proposed) | Pins |
| --- | --- |
| `SessionSettingsStoreTests` | Absent file means configuration defaults; a save writes overrides only; out-of-range refused with its text; a hand edit is picked up; a corrupt file is ignored with a Warning |
| `CatchUpBudgetTests` | The first Turn asks for the Message cap and trims to the budget; own Messages dropped first except the newest five; Transcript order kept; both omitted lines exact; applies to fresh and resumed first Turns; no `ForgetAll` |
| `CatchUpDefaultsTests` | **PM-R2's proof:** at the defaults, `turnPromptTranscript.txt` and `turnPromptTranscriptResumed.txt` are unchanged |
| `FreshStartTriggerTests` | Each trigger fires at its threshold and not below; a resumed session's baseline update is not a fall; a Command Turn never triggers; Off evaluates nothing; `MaxPerDay` per local day |
| `FreshStartTests` | The old entry is forgotten and the next Turn carries Catch-up; never mid-Turn; waits while a Question waits; not a failure; no Transcript Message; the note Envelope's fields; `last-catch-up` written as sent |
| `StartAfreshTests` | Forgets one Room's session only; other Rooms of the same Teammate keep theirs |
| `ProtocolJsonTests` (extended) | Literal JSON of `StartAfresh` and `FreshStartNote`; `ProtocolVersion.Current` unchanged |
| `PersonaRenameCascadeTests` (extended) | The `last-catch-up` move sits above the early return |
| bUnit: Settings › Memory, Room menu, Room note | Exact copy; Reset; the setting's label; the note's wording for each reason |

---

## 11. Configuration

| Key | Default | Note |
| --- | --- | --- |
| `Team:Acp:TranscriptCatchUpMessages` | `20` | Exists. Now the Message cap; Settings may override |
| `Team:Acp:TranscriptCatchUpTokens` | `100000` | Proposed. Does not bind at 20 Messages (PM-R14); the experiment uses 40,000 |
| `Team:Acp:CatchUpKeepOwnMessages` | `5` | Proposed (PM-R11) |
| `Team:Acp:SessionRetentionDays` | `365` | Proposed; only if V0-2 passes |
| `Team:Acp:FreshStarts:CompactionDropPercent` | `50` | Proposed |
| `Team:Acp:FreshStarts:AboveTokens` | `200000` | Proposed. Zero disables |
| `Team:Acp:FreshStarts:AfterTurns` | `80` | Proposed. Only for a session that reports no usage. Zero disables |
| `Team:Acp:FreshStarts:MaxPerDay` | `3` | Proposed. Automatic fresh starts per Room Session per local day |

The Engineering Guide's configuration table gains each row in the change that builds it.

---

## 12. Open questions and ADR candidates

### 12.1 Open questions

| # | Question | Blocking | Leaning |
| --- | --- | --- | --- |
| Q-1 | Catch-up budget: one global value, or per Persona too? | No | Global first; a later per-Persona value stays out of the `Persona` record |
| Q-2 | Can `cleanupPeriodDays` be set for an isolated session, and by which route? | Phase 0 | Unknown; V0-2 decides |
| Q-3 | Does isolation hold live (Task 14.3.m)? | **Phase 2** | Unknown; until it does, the experiment cannot separate Huddle's effect from Claude Code's memory |
| Q-4 | Should a fresh start delete the old session with ACP `session/delete`? | No | Yes eventually; needs `IAgentSession` support from the ACP effort |
| Q-5 | Are 200,000 tokens and a 50 % fall the right thresholds? | No | Starting values; tune from the experiment and the notes' history |
| Q-6 | Does a fresh start measurably reduce the rut? | No | The best evidence is about tasks derailing, not repetition; the experiment records it |
| Q-7 | Should observing compaction use ACP `compaction_update` instead of the `Used` fall? | No | Later; it needs a mapping in `SessionUpdateMapper` (ACP effort) and a client capability Huddle does not advertise yet |
| Q-8 | Should the Room setting be on by default once the experiment passes? | No | No; keep it the Human's choice (§13) |
| Q-9 | Should every fresh start, not only phase 4's, first ask the old session to save anything durable to Memory? | No | Not in phase 2: it costs a Turn per fresh start, and an observed compaction has already lost detail. Revisit with the experiment's memory-versus-reread split |
| Q-10 | Does this spec depend on PR #287 merging first? | No | No for phases 0 to 2. The experiment (§9.1) should run after it, so it measures the Room as it will really run |

### 12.2 ADR candidates

1. **A degraded Room Session is replaced by a fresh one briefed from the Transcript, never compacted
   by Huddle.** Extends ADR-0024; keeps ADR-0035.
2. **Catch-up is bounded by a token budget, and an Agent's own Messages are dropped first.**
3. **Memory layers are built only after a recorded experiment shows the cheap levers fall short.**

---

## 13. Choosing automatic fresh starts

This section is written for the Human and moves into the [User Guide](Huddle.UserGuide.md) when
phase 2 ships.

Every Room keeps everything that was said, and you can always start a Teammate afresh in a Room
from the Room menu. Automatic fresh starts let Huddle do it for you when a Teammate's conversation
has grown too long or has just been squeezed by the AI. It is off until you turn it on.

**Turn it on when** you talk to one Teammate in this Room most days for weeks, and you have noticed
it repeating itself or losing track of what you decided.

**Leave it off when**:

- **The Room is a short task.** A fresh start throws away what the Teammate did with its tools, and
  a short task never needs it.
- **The Teammate is in the middle of long, careful work** in the Room, such as a refactor across many
  files. Its current session knows how it got there; a reread does not.
- **You would rather steer it yourself.** `@Teammate /compact keep the decisions and open questions`
  squeezes its notes while keeping what you name, and *Start afresh* gives a clean page when you
  choose.

**What a fresh start does:** the Teammate starts a new session and rereads the Room, up to the size
set in Settings, skipping most of its own older suggestions. The Room shows a note each time, and
you can open it to see exactly what was reread. Nothing about you is stored.

---

## Appendix A: what changed from the first draft

| First draft (2026-10-11) | This version | Why |
| --- | --- | --- |
| Hierarchical Digest (day, week, quarter) written by a background consolidator | Removed; a handoff note is the conditional phase 4 | Benchmarks favour raw text over summaries at tens of thousands of tokens (LoCoMo, LongMemEval, the Mem0 paper); summary-of-summary loses detail; the consolidator needed a host that does not exist (Q-8) and continuous spend |
| Brief: Digest first, trimmed oldest era last | Catch-up: raw Transcript to a token budget, own Messages dropped first | Raw text is the stronger baseline; replaying an Agent's own suggestions re-seeds the rut |
| Catch-up by Message count only | By Message cap and token budget | A count can be 2,000 or 400,000 tokens |
| 4 characters per token | 3 characters per token, called an estimate | Claude 4.7 and later produce about 30 % more tokens for the same text |
| `self` facts promoted without approval; Memory Proposals for shared scopes; `remember` | Removed | A persistent prompt-injection channel (MINJA, AgentPoison, OWASP agentic T1). Durable facts now have their own design: the `memory` App Tool, with read-before-write and Team Memory written only by the manager (ADR-0071, PR #287) |
| `recall` over 50 Rooms per call | Removed from this spec | Holds each Room's append lock and breaks ADR-0051's reasoning for an Agent in a loop; a later, separate feature |
| Digest update blocks for live and resumed sessions, `DigestVersion` | Removed | A live or resumed session already saw what a summary would tell it; the plumbing had no wire path |
| Hash-based edit lock on Digest files | Removed | Froze a file after one edit and lost content |
| Rotation last (phase 4), at 70 % of the window | Fresh starts in phase 2, on observed compaction or an absolute size | Best-supported idea (Laban et al.; Anthropic's guidance to prefer a new context); `Size` is the 1M window, so 70 % fired at about 700,000 tokens or never |
| Compaction mentioned only as the problem | §7 decides its role | `/compact` already exists; compaction is observable through `Used` |
| Auto memory "not used" | Phase 0 verifies isolation | Auto memory loads unless switched off; isolation is built but unverified |
| No retention lever | Phase 0 checks `cleanupPeriodDays` | The commonest cause of lost sessions may be one setting |
| 59 requirements | 25 | Scope cut to what the goal needs |

The first draft is in git history (commit `139d3696`) for anyone who builds phase 4 or a later
memory feature.

---

## Appendix B: sources

Checked during the 2026-10-16 reviews. Third-party measurements are weighted below primary
documentation.

| Claim | Source |
| --- | --- |
| `session/resume` stable; `session/load` replays history | ACP changelog 0.12.2; agentclientprotocol.com/protocol/session-setup |
| `claude-agent-acp` advertises resume and maps a missing session to "not found" | The `claude-agent-acp` source, `src/acp-agent.ts` |
| Transcripts deleted after `cleanupPeriodDays`, default 30 | code.claude.com/docs/en/sessions |
| Auto-compaction near 967K on 1M models; `autoCompactWindow` | code.claude.com/docs/en/model-config |
| Auto memory loads unless switched off | code.claude.com/docs/en/agent-sdk/claude-code-features |
| `usage_update` `used` and `size`, stable | ACP changelog 0.13.6; rfds/session-usage |
| About 30 % more tokens on Claude 4.7 and later | platform.claude.com, token counting |
| Multi-turn degradation; consolidate and start afresh | Laban et al., arXiv 2505.06120 |
| Self-conditioning on own errors | arXiv 2509.09677 |
| Prefer a new context window over compaction | Anthropic, Claude Code best practices |
| Full context beats memory systems on LoCoMo (about 26K tokens) | Mem0 paper, arXiv 2504.19413 |
| Summaries lose information | LongMemEval, arXiv 2410.10813; LoCoMo, arXiv 2402.17753 |
| Context degrades as it grows | Chroma, "Context Rot"; NoLiMa, arXiv 2502.05167; Lost in the Middle, arXiv 2307.03172 |
| Memory injection attacks | MINJA, arXiv 2503.03704; AgentPoison, arXiv 2407.12784; OWASP Agentic AI threats (T1) |
| Users want to review and edit AI memory | arXiv 2508.07664; arXiv 2410.14931 |
