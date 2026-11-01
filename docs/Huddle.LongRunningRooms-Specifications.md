# Huddle.LongRunningRooms — Requirements and Design Specification

**Date:** 2026-10-30 (first draft 2026-10-11 as *Huddle.PersistentMemory*; rewritten after review
2026-10-16; renamed and extended with OpenClaw's prior art 2026-10-30) · **Status:** Closed 2026-10-31. Phases 1 and 2a built and the experiment run once (§9.3); phase 2b, the single-session mode and phase 4 not pursued; phase 0's checks and `SessionRetentionDays` not run or built ·
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
- **Save before forgetting.** Before a live session is retired, it gets one silent Turn to save what
  matters through the `memory` tool: a **memory flush**, the technique OpenClaw runs before every
  compaction (§6.6).
- **Measure before automating.** Phases 0 and 1 and the Human's *Start afresh* (phase 2a) ship
  first: they fix a certain failure, the lost session. The automatic triggers, the flush and the
  records (phase 2b) wait for the experiment, because the rut they treat has not yet been observed
  in a Huddle Room.
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
| **Fresh start** | A clean notebook page, with the recent pages clipped to the front | When a session has gone stale, Huddle retires it and starts a new one that rereads the Room. You can ask for one in a Room at any time; automatic fresh starts are off until you turn them on for a Room. Then they happen when the notepad gets too big, when the AI has just squeezed it, or when a big notepad has sat unused for an hour (reopening it would cost as much as starting afresh) | Asking for one: step 2. Automatic: only after step 3, if it earns its place |
| **Save before forgetting** | Writing down what matters before you clear your desk | Just before a Teammate's notepad is retired, it gets a quiet moment to write anything worth keeping into its notebook (the `memory` tool). Nothing is posted in the Room; the Room's note says how many notebook pages it saved | Tried by hand in step 3; built afterwards only if it beats "save as you go" |
| **Squeeze** (`@Nova /compact`) | Asking someone to boil their notes down to one page | Already built. You can tell a Teammate to compress its notepad, even saying what to keep. Huddle never does this on its own, because you cannot read or check the squeezed version | Exists today |
| **The experiment** | Testing a new routine for a month before buying equipment | Before anything bigger is built, you ask a long-running Teammate ten questions about old decisions, and count repeated suggestions, before and after each step. The scores decide whether more is needed | Step 3 |
| **Handoff note** (only if the experiment says so) | A colleague's handover note before leave | Decisions and preferences are already saved in step 2. If the experiment shows the Teammate still loses track of its open work, it also writes a short note of what it was doing and what is still open. The new session reads it. You can read and edit it | Step 4, conditional |
| **Saving lasting facts** (the `memory` tool) | The Teammate's own notebook, and the team noticeboard | Being built separately ([Instructions and Memory](Huddle.InstructionsAndMemory-Specifications.md)). The Teammate writes down decisions, your preferences and corrections as it goes, and every new session starts by reading the notebook's index. So what is written there survives every fresh start | Arriving separately, soon |
| **House rules** (Instructions) | The rules pinned on the office wall | Also in that separate feature: rules you write once, which every session starts with. A fresh start is also how a Teammate picks up rules you changed for everyone | Arriving separately, soon |
| **Look it up** | Searching your email for the exact words | A later, separate feature. Not part of this plan | Not planned here |

### Why Claude's own squeeze is not enough

Claude Code already compacts a session by itself when it gets nearly full. That covers only part
of the problem:

- **It cannot help when the notepad is thrown away.** Compaction works inside a live session. After
  30 days unused, a Restart or a Persona edit, there is nothing left to compact, and the Teammate
  starts again from the last 20 Messages. Steps 0 and 1 are for exactly this.
- **It comes too late.** On current models it happens at about 967,000 tokens, while quality starts
  to slip at tens of thousands. Every answer in between carries the whole, growing notepad, which
  costs money and drifts.
- **You cannot check it.** Claude decides what to keep, nobody sees the summary, and it does not
  point back to the Messages it came from.
- **It does not break a rut.** A squeezed session is the same session on the same path. A fresh
  start, rereading the Room's own words, is what gets a Teammate off it.
- **It is Claude-only.** A Teammate on a local model has no compaction at all.

If the experiment (step 3) shows that Claude's squeeze plus your own occasional `@Nova /compact`
is good enough, automatic fresh starts are never built and nothing bigger is. Steps 0 and 1, and
asking for a fresh start yourself, are worth doing either way: they fix the lost notepad, which
compaction can never touch.

### What it costs, and what can go wrong

- **Money.** Rereading more of a Room makes a fresh session's first answer cost more, and saving
  before a fresh start costs one extra, quiet answer. A fresh start is rare (at most a few a day)
  and is shown in the Room.
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

On 2026-10-30 it was compared with OpenClaw, an open-source agent runtime that keeps one session
going for good and compacts it. Huddle cannot do the same, because Claude Code, not Huddle, owns
the session and deletes it after 30 days unused. But four of OpenClaw's habits were worth taking,
and they are now in step 2: saving to memory before it forgets, starting afresh after a long idle
spell, counting each session's fresh starts, and telling you what to do when it cannot help itself. The page was also renamed from *Persistent Memory* to *Long-Running Rooms*, because "memory"
now means the Teammate's notebook. [Appendix A](#appendix-a-what-changed-from-the-first-draft) lists
every change and why.

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
| **Memory flush** | One silent Turn, just before a live session is retired, in which the Agent saves anything durable through the `memory` App Tool. Its text is never posted (§6.6). The term is OpenClaw's | handoff (that is phase 4's note), save turn, checkpoint |
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
   clock alone: the idle trigger also needs the session to be large (§6.2), and a daily reset is
   deliberately not offered (§7.2).
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
| LR-R1 | This feature MUST NOT edit, reorder or delete a Transcript line. | all |
| LR-R2 | With every setting at its default, the system prompt and every Turn prompt MUST be byte-identical to today's, and the existing goldens MUST stay unchanged. | all |
| LR-R3 | This feature MUST NOT make a model call other than an Agent's own Turns, the memory flush of LR-R26 and, in phase 4, the handoff Turn. | all |

### 3.2 Retention and isolation (phase 0)

| Id | Requirement | Phase |
| --- | --- | --- |
| LR-R4 | Before phase 2a ships, the isolation of a Claude-profile session from the Human's `~/.claude` settings and auto memory MUST be verified live (Task 14.3.m), and the result recorded in [known-limits.md](engineering/known-limits.md). **Status:** not run. Phases 0 to 2a were built without it; the check (V0-1) is deferred to the Human's acceptance (2026-10-30), so the requirement stands. | 0 |
| LR-R5 | A configured Adapter entry that runs Claude (its `Command`, `AdapterPath` or `Args` contains `claude-agent-acp`) and whose `IsolateUserSettings` is false SHOULD log one Warning at start-up naming the entry. `AdapterCatalog` logs it, through an optional `ILogger<AdapterCatalog>`. `UsesToolNamePrefix` is not the test: it only means tool names carry `mcp__team__`, and it defaults to true for every configured entry, `mock-acp` and `agency-acp` included. | 0 |
| LR-R6 | If V0-2 (§4.1) shows session retention can be set for an isolated session, Huddle MUST set it to `Team:Acp:SessionRetentionDays` (default 365) for every Claude-profile session. If it cannot, the finding MUST be recorded and nothing else in this spec changes. **Status:** not built. V0-2 was not run (deferred to the Human's acceptance, 2026-10-30), so `Team:Acp:SessionRetentionDays` does not exist yet; the requirement stands. | 0 |

### 3.3 Catch-up budget (phase 1)

| Id | Requirement | Phase |
| --- | --- | --- |
| LR-R7 | A first Turn's Catch-up MUST be bounded by `Team:Acp:TranscriptCatchUpTokens` (estimated tokens) as well as by `TranscriptCatchUpMessages` (a Message cap). Whichever is reached first ends it. | 1 |
| LR-R8 | Tokens MUST be estimated at three characters per token, rounding up, and the estimate MUST be called an estimate wherever it is shown. One Message costs `ceil((SenderName.Length + 2 + Text.Length) / 3)` tokens, and the `EstimatedTokens` of a note is the sum over the Messages kept, so the trim and the note use the same measure. | 1 |
| LR-R9 | Both values MUST be settable on the Settings page and MUST apply from the next first Turn without restarting the app, a Persona or a session. A Settings value overrides the configuration default. | 1 |
| LR-R10 | Changing either value MUST NOT forget any Room Session and MUST NOT be part of the `Persona` record, so `PersonaSupervisor.NeedsRestart` never fires on it. | 1 |
| LR-R11 | When the budget cuts the Catch-up, the receiving Agent's **own** Messages MUST be dropped before anyone else's, oldest first, except its newest `CatchUpKeepOwnMessages` (default 5). Only then are other Messages dropped, oldest first. If the rest still does not fit, the second pass drops the oldest remaining Messages, **including** the protected newest own ones, until it does; no minimum is kept, because the budget is a hard bound. "Own" means `SenderId` equals the Agent's id when the Room Session has one, else `SenderName` equals the Persona's name (ignoring case). The kept Messages stay in Transcript order. | 1 |
| LR-R12 | The existing `turn.transcriptOmitted` line MUST count every dropped Message. When some dropped Messages were the Agent's own, a proposed `turn.transcriptOmittedOwn` line MUST say how many. | 1 |
| LR-R13 | The Message cap MUST accept 1 to 2000 and the token budget 1000 to 150000. A value outside the range MUST be refused with a message, never clamped silently. (A value of 0 is not offered: `HandleReadTranscriptAsync` refuses `Max < 1`.) | 1 |
| LR-R14 | The defaults MUST stay `20` Messages and a token budget large enough not to bind at 20 Messages, so LR-R2 holds until the Human changes them. | 1 |

### 3.4 Fresh starts (phases 2a and 2b)

Phase 2 is split by the evidence it needs. **2a** is the Human's *Start afresh* and the Room note:
small, certain, and needed by the experiment (§9.1). **2b** is everything automatic (the per-Room
setting, the triggers, the memory flush, the records and the fallback note) and is built only on
a "go" from the experiment, with thresholds taken from its measurements. The 2b requirements are
written now so the design is whole, not as a commitment to build them.

| Id | Requirement | Phase |
| --- | --- | --- |
| LR-R15 | The Human MUST be able to ask for a fresh start of one Teammate's session in one Room, from the Room, without affecting that Teammate's other Rooms. | 2a |
| LR-R16 | Automatic fresh starts MUST be a per-Room setting, **Off** for every Room, existing and new, the Chief of Staff's included. Only the Human can turn it on; no App Tool, Persona field, Team type or Agent can. | 2b |
| LR-R17 | With automatic fresh starts on, a Room Session MUST be marked **degraded** at the end of a Turn when any trigger of §6.2 fires. | 2b |
| LR-R18 | A fresh start MUST happen only between Turns, immediately before the next item starts, and never while a Question or Form waits in that Room. | 2a |
| LR-R19 | A fresh start MUST close the old session, forget its `RoomSessionStore` entry, and open a new session through the existing lazy-open path, so its first Turn carries Catch-up at the configured budget. | 2a |
| LR-R20 | A fresh start MUST NOT count as a Turn failure, touch the failure streak or post a Transcript Message. | 2a |
| LR-R21 | Every fresh start MUST be shown in the Room as a Room-level note (ADR-0012's slot), naming the Teammate, the reason and what the new session reread, for example *Nova started afresh after its conversation was compacted. It reread the last 38 Messages (about 31,000 tokens).* | 2a |
| LR-R22 | The text a fresh session's first Turn carried MUST be viewable from that note, as sent. The note persists beside the reread file, as `last-catch-up/{Name}/{roomId}.json` next to the `.md`, so both survive an app restart. Dismissing a note deletes its `.json` only. | 2a |
| LR-R23 | Automatic fresh starts MUST be limited to `FreshStarts:MaxPerDay` (default 3) per Room and Agent per local day, counted in the records of §6.7, so a fresh start, which replaces the Room Session, does not reset the count. A trigger past the limit is logged and skipped, never queued. | 2b |
| LR-R24 | A Turn that is a Human-run Adapter command (`@Nova /compact`) MUST NOT trigger a fresh start: the Human chose compaction. | 2b |
| LR-R25 | With automatic fresh starts Off, no trigger MUST be evaluated for that Room, so its behaviour is exactly today's. | 2b |
| LR-R26 | Before a fresh start of a **live** session (the Size, Observed compaction and Turn-count triggers and, from phase 2b, *Start afresh*; phase 2a's *Start afresh* has no flush), the session MUST get one memory flush (§6.6) when the `memory` App Tool is offered to the Agent and `FreshStarts:FlushToMemory` is true. At most one flush per fresh start. Its text MUST never be posted, and it MUST NOT count as a Turn failure. | 2b |
| LR-R27 | A flush that fails, is refused or passes `FreshStarts:FlushTimeoutSeconds` MUST NOT block the fresh start: it is logged, and the Room note says the Teammate could not save first. | 2b |
| LR-R28 | With automatic fresh starts on, a Room Session MUST be started afresh, not resumed and not flushed, when its next item arrives after `FreshStarts:IdleMinutes` without a Turn **and** its last reported context use was at least `FreshStarts:IdleAboveTokens` above its baseline (LR-R31). | 2b |
| LR-R29 | Each Room Session's fresh-start count, last reason, the context use at that moment and its time MUST be recorded (§6.7), and the Room note MUST show the count. | 2b |
| LR-R30 | When a degraded Room Session cannot be started afresh (the daily limit is reached, or a Question or Form keeps it waiting past one item), the Room MUST say so once per degraded mark and suggest `@Name /compact` or *Start afresh*. | 2b |
| LR-R31 | The Size and Idle-and-large triggers MUST measure a session's **growth** above its baseline, the context use reported at the end of its first Turn, not its absolute context use. A session with no recorded baseline MUST use a baseline of zero, so it behaves as an absolute threshold. A fresh start MUST therefore never mark the new session degraded for the size of its own Catch-up. | 2b |

### 3.5 Handoff note (phase 4, conditional)

LR-R32 and onward are written only if the experiment (§9.1) shows phases 0 to 2 fall short. §9.2
sketches the candidate.

---

## 4. Session retention and isolation (phase 0)

Phase 0 is two live checks and, if the first passes, one setting. It costs no model calls beyond
the checks.

**Built status (2026-10-31).** Only LR-R5's Warning was built. V0-1 and V0-2 were not run: they are
deferred to the Human's acceptance (2026-10-30). Because V0-2 has not shown that retention can be
set, `Team:Acp:SessionRetentionDays` (LR-R6) does not exist. Both requirements stand.

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
2. Trim the returned Messages to the **token budget** in the runner, by LR-R11's order.
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

A new **Settings › Sessions** tab: *Messages a fresh session rereads* (cap) and *Size of what a fresh
session rereads* (budget, shown as "about N tokens"), each with its default and a Reset. Copy
follows [language.md](engineering/language.md): "you" for the Human, never "user", "chat" or
"conversation". Check [agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) before building.

---

## 6. Fresh starts (phase 2)

*Start afresh* (§6.1) and its Room note (LR-R21, LR-R22) are phase 2a. The triggers (§6.2), the
Room setting (§6.5), the flush (§6.6) and the records and fallback note (§6.7) are phase 2b, built only on a "go" from the experiment
(§9). In phase 2a, §6.3 runs without steps 2 and 3.

### 6.1 Two ways in

| Way | Who | Available |
| --- | --- | --- |
| **Start afresh** in the Teammate's profile popover (opened from their avatar in the Room header) | The Human | Every Room, always (LR-R15); hidden for the Human and for an Adapter profile with `SessionPerRoom == false` (LR-E5) |
| Automatic, on a trigger | Huddle | Only in a Room with automatic fresh starts on (LR-R16) |

*Start afresh* is the Room-scoped sibling of the card's Restart: it forgets one Room Session, not
all of them. It needs a proposed additive Envelope from server to runner, `StartAfresh(string
RoomId)`, so `ProtocolVersion.Current` stays as it is (traps.md: adding to the wire is not
changing it). The connection already identifies the Agent, as for `StopTurn` and `ToolActivity`; a
name on the wire would be a second, spoofable source. The server accepts `FreshStartNote` before the
runner sends one, because an unknown Envelope would otherwise be answered with `BadMessage` and mark
the Agent Degraded. A Room with no live session yet creates no session object: its stored entry is
forgotten and the Room is remembered in the pool as "note pending", so its next session still sends
the note.

### 6.2 Triggers

Only with automatic fresh starts on. The first three are evaluated at the end of each ordinary Turn
(never a Command Turn, LR-R24); the idle trigger is evaluated when the next item arrives:

| Trigger | Fires when | Default |
| --- | --- | --- |
| **Observed compaction** | `Used` falls by more than `FreshStarts:CompactionDropPercent` between two `UsageUpdated` within one ordinary Turn. The Adapter has just replaced the context with its own summary; Huddle replaces it with the Room's own words instead | 50 % |
| **Size** | `lastUsed` minus the session's baseline reaches `FreshStarts:AboveTokens` (LR-R31) | 200,000 |
| **Turn count**, fallback | The session has completed `FreshStarts:AfterTurns` Turns **and** has reported no usage at all | 80 |
| **Idle and large** | The next item arrives more than `FreshStarts:IdleMinutes` after the session's last Turn, **and** its last reported context use was at least `FreshStarts:IdleAboveTokens` above its baseline (LR-R28, LR-R31) | 60 minutes, 50,000 tokens |

- The size trigger is absolute, not a percentage of `Size`. `Size` is the model's window (1M on
  current Claude models), not the point where Claude Code compacts (about 967K, or wherever the
  `autoCompactWindow` setting puts it), so a percentage fires far too late or never.
- The first `UsageUpdated` of a resumed session is a baseline (D22 correction 9) and never counts
  as a fall.
- The Turn-count fallback exists for an Adapter such as `agency-acp` on a local model, which reports
  no usage and has no compaction to observe.
- With the Size trigger well below Claude Code's compaction point, it normally fires first, so the
  flush (§6.6) runs before any detail is lost. The Observed compaction trigger then catches the
  cases where compaction comes earlier, for example when the Human's `autoCompactWindow` is lower.
- **Why idle and large.** The prompt cache lasts about an hour on a subscription and five minutes on
  an API key. Resuming a large session after that reloads its whole context cold, which costs about
  as much as a fresh start and keeps the stale context. Both conditions are required, so a small
  session is always resumed, however long it was idle (principle 2). The idle trigger needs the last
  context use to survive an app restart, and `RoomSessionEntry` already keeps it: its trailing
  optional `ContextUsed`, written at Turn end beside `LastTurnUtc` for the context meter, is read
  by the idle trigger as is. No new field is needed for it.
  There is no flush: the session is not live, and resuming it only to flush would pay the cold
  reload this trigger avoids.
- A degraded mark set by one trigger is cleared by the fresh start; triggers are not re-evaluated
  until the new session's first Turn has finished.
- **Why growth, not size (LR-R31).** A fresh session starts with its system prompt, the
  Instructions and Memory indexes and a Catch-up of up to the budget (100,000 tokens by default,
  150,000 at most). Measured absolutely, that start alone can be close to the Size trigger and is
  already past Idle and large. The next trigger would then rotate a session that has learnt
  nothing new, use up `MaxPerDay` and pay a cold refill each time. Growth is the right measure for both triggers. For Size, what
  degrades the session is what accumulated after its start. For Idle and large, a fresh start
  costs roughly the baseline anyway, so rotating saves only the growth above it. The baseline is
  the `Used` reported at the end of the first Turn, kept on `RoomSessionEntry` as a trailing
  optional `BaselineTokens` beside `ContextUsed`. A session opened before this change has
  none and keeps the absolute behaviour. The Catch-up budget and the trigger thresholds therefore
  stay independent settings, and no combination of them can make fresh starts loop.

### 6.3 What happens

At the next item admitted to a degraded Room Session (or at once, for *Start afresh*, when the
session is idle):

1. If a Question or Form waits in the Room, wait; the mark stays (LR-R18). If it is still waiting
   when the next item arrives, the Room says so (LR-R30).
2. If today's automatic count has reached `MaxPerDay`, clear the mark, log, run the item on
   the old session (LR-R23), and post the fallback note (LR-R30). *Start afresh* is never limited.
3. Unless the trigger was Idle and large, run the memory flush on the old session (§6.6, LR-R26).
4. Close the session, call `RoomSessionStore.Forget(name, roomId)`, and open fresh through the
   existing lazy-open path, so `firstTurnPending` is set (LR-R19). Record the fresh start (§6.7).
5. The item's Turn carries Catch-up at the configured budget (§5). The new session's system prompt
   is composed afresh, so it also carries the current Instructions and Memory indexes, including
   Huddle-wide Instructions edits that a running session never sees (Instructions and Memory §3.4).
6. Send the Room note (LR-R21) through a proposed additive Envelope, `FreshStartNote(string RoomId,
   string Reason, int MessageCount, int EstimatedTokens, bool ReadFailed)`; phase 2b adds
   `FlushOutcome`, `FilesSaved` and `FreshStartCount` as trailing optional members. The server keeps
   the latest note per Room and Agent (`FreshStartNotes`) and persists it through `LastCatchUpStore`
   (LR-R22); that store, not the session, owns the folder. Write the whole prompt text the fresh
   session's first ordinary Turn sent to
   `{DataDir}/room-sessions/last-catch-up/{Name}/{roomId}.md` for LR-R22, and the note to the `.json`
   beside it. The runner computes the text and writes the file before the Turn is published, and sends
   the note after the Turn ends, never inside the watchdog window. `LastCatchUpStore.Has` tells the
   UI whether a reread file exists. The notes cache is cleared whenever the store renames or removes a
   folder, or a Room is deleted.

The old session is not deleted from the Adapter in v1. ACP's `session/delete` is stable and
`claude-agent-acp` advertises it, but `IAgentSession` has no delete today and `Huddle.Acp` belongs
to the ACP effort (Q-4).

### 6.4 Cost

A fresh session's first Turn is a cold start: no prompt cache, the system prompt plus the Catch-up.
That refill is real spend and is counted against the token Budget like any first Turn. `MaxPerDay`
bounds it. Rotating rarely, on degradation only, keeps the cost near what one compaction costs.
The memory flush adds one more Turn on the old session; that session is warm, so the flush is
mostly cache reads plus the memory writes. The idle trigger skips the flush for exactly this reason.

### 6.5 The Room setting

Stored in a proposed `{DataDir}/room-fresh-starts.json`, keyed by Room id: `{ "automatic": true,
"changedUtc": "…" }`. Absent means Off. A file, not a column, because a new column on `rooms` is a
known trap (rules.md) and the runner already reads `{DataDir}` stores such as `RoomSessionStore`.
Shown in the Room menu as *Start afresh automatically when a Teammate's conversation gets too long*,
with a link to §13's guidance.

### 6.6 The memory flush

Adapted from OpenClaw, which runs a silent turn before every auto-compaction, once per compaction
cycle, so the agent can write durable state to its memory files before the context is summarised.

| Aspect | Huddle's rule |
| --- | --- |
| When | Step 3 of §6.3: on the old, live session, after the trigger and before it is closed. Once per fresh start (LR-R26) |
| Skipped | When the `memory` App Tool is not offered to the Agent, when `FreshStarts:FlushToMemory` is false, for the Idle and large trigger, and for a shared session (no Room Session to retire) |
| Shape | A proposed `WorkItemKind.Flush` whose prompt is the proposed Prompt `turn.memoryFlush`, in `Live` timing. Its default: *This conversation is about to be closed and a fresh one started that rereads the Room. Before that happens, save anything durable you have not saved yet (decisions, the Human's preferences, corrections, facts about the people and work) with the {{memoryTool}} tool, following your memory rules. Do not reply to the Room.* The tool name is substituted in code (rules.md) |
| Silent | Its text is discarded, never posted (OpenClaw's `NO_REPLY`, made structural rather than a magic reply). Its tool calls show in Turn detail like any Turn's |
| Visible | The memory writes are ordinary `memory` tool writes: attributed to the Agent, listed as File Changes in its other Rooms (Instructions and Memory §6.1), and readable in the Library. The Room note counts them: *Nova saved 2 memory files, then started afresh…* |
| Bounded | `FreshStarts:FlushTimeoutSeconds` (120). A Stop by the Human cancels the flush and the pending item, as any Stop does; the degraded mark stays. A failed, refused or timed-out flush never blocks the fresh start (LR-R27) |
| After an observed compaction | The flush still runs, but on the Adapter's compacted context, so it can save only what survived. The Size trigger is set low enough to fire first (§6.2) |
| Security | The flush writes only through the `memory` tool, so ADR-0071's confinement, read-before-write and manager-only Team Memory apply. It adds no new write path, so no new route for planted text in the Room to reach a shared scope |

### 6.7 Records and the fallback note

Adapted from OpenClaw's per-session `compactionCount` and its "retry, `/compact` or `/new`" advice
when overflow recovery fails.

- **Records (LR-R29).** `room-fresh-starts.json` keeps, per Room and Agent Name: `count`,
  `lastReason`, `lastContextTokens` (the context use that triggered it) and `lastUtc`. They feed
  the Room note (*This is Nova's 3rd fresh start in this Room*) and the tuning of Q-5. A Persona
  rename moves its key (LR-E11).
- **Fallback note (LR-R30).** When a degraded session cannot start afresh, the Room says once:
  *Nova's conversation is getting long, but it cannot start afresh right now (daily limit
  reached). You can run @Nova /compact or choose Start afresh.* The same Room-level slot as the
  fresh-start note (ADR-0012).

---

## 7. Compaction's place

### 7.1 Compaction and fresh starts

**Why automatic compaction alone does not meet the goal.** Claude Code compacts on its own near
967K tokens on 1M-window models (`autoCompactWindow` moves it). That leaves four gaps this spec
exists to fill:

| Gap | Why compaction cannot fill it | Filled by |
| --- | --- | --- |
| Session loss (retention, Restart, Persona edit) | Compaction needs a live session; a lost one has nothing to compact | Phase 0 (retention), phase 1 (budgeted Catch-up) |
| Degradation well before 967K | It fires far past the point where quality slips, and every Turn until then carries the full context | Phase 2's size trigger |
| An unreviewed, uncited summary | The Adapter writes it, nobody sees it, and it does not cite Messages | Phase 2: a fresh start rereads the Transcript, and the Room note shows what was reread |
| The rut | The compacted session continues the same trajectory | A fresh start resets it (Laban et al., Appendix B) |
| Adapters without compaction | `agency-acp` and local models have none | Phase 2's Turn-count trigger |

If the experiment (§9.1) shows automatic compaction plus the Human's `/compact` is enough,
automatic fresh starts stay off and phase 4 is not built. Phases 0 and 1 stand regardless, because
session loss is outside compaction's reach.

| Option | Decision | Why |
| --- | --- | --- |
| The Human runs `@Nova /compact keep the decisions and open questions` | **Keep, and document it** in the Settings › Sessions tab and §13 | Already built, costs nothing new, and the Human can steer what is kept. But the summary is the Adapter's, invisible, Claude-only and not in the Transcript: a tool, not the strategy |
| Huddle compacts automatically | **No** | It would reverse ADR-0035 ("only the Human") for a worse result than a fresh start: an unreviewable summary with no link back to the Messages, on one Adapter only |
| An observed compaction triggers a fresh start | **Yes**, the safety-net trigger (§6.2): Size normally fires first, and this catches a compaction that comes earlier | The Adapter has already thrown detail away; replacing its summary with the Room's own words costs one cold Turn and makes the trade visible |
| A fresh start instead of compaction | **Yes**, by default where it is on | Reviewable, adapter-agnostic, and it moves the model off the path it was stuck on, which compaction does not |

A Human who runs `/compact` keeps the compacted session: LR-R24 makes sure Huddle does not undo the
choice by starting afresh straight after.

**Tool-result pruning is out of reach.** Trimming or clearing old tool results inside a live
context, as OpenClaw's session pruning or Anthropic's context editing does, happens inside the
Adapter's session, which Huddle does not own. Whatever the Adapter does there is its own business.
A fresh start clears tool-result bloat as a side effect, because the new session rereads Messages
only.

### 7.2 Prior art: OpenClaw

OpenClaw (docs.openclaw.ai, read 2026-10-30) is an agent runtime that owns its sessions: it stores
each transcript in its own SQLite database, keeps one session per key **forever by default**, and
bounds it by compacting older history into a persisted summary while keeping about 20,000 recent
tokens. Huddle cannot follow that model: Claude Code owns the session, Huddle cannot keep, read or
edit its history, and Claude Code deletes it after 30 days unused. So this spec relies on retention
(phase 0) plus a reread of Huddle's own Transcript (phases 1 and 2) instead.

| OpenClaw technique | Taken? | Where, or why not |
| --- | --- | --- |
| Memory flush: a silent turn before auto-compaction that writes durable state to memory, once per cycle, on by default | **Yes** | §6.6, LR-R26 and LR-R27; before a fresh start rather than before a compaction Huddle cannot intercept |
| Idle reset (`session.reset.mode: "idle"`) | **Yes, with a size condition** | §6.2, LR-R28. Idle alone would discard healthy sessions (principle 2) |
| Daily reset (`mode: "daily"`, default 04:00) | **No** | A clock alone retires healthy sessions and their tool context; the experiment can revisit it |
| `compactionCount` and token counters per session | **Yes** | §6.7, LR-R29 |
| "Retry, `/compact` or `/new`" when recovery fails | **Yes** | §6.7, LR-R30 |
| `/new` and `/reset` | **Already** | *Start afresh* (§6.1); `/compact` with instructions already exists |
| `keepRecentTokens` 20,000 after compaction | **Reference only** | A product default, not a measurement. It sits beside the experiment's 40,000 (§9.1) |
| Pruning thresholds: server-side from max(50,000, 30 % of the window); client-side nothing below about 30 % use, hard clear at about 50 % | **Reference only** | They govern tool results, not the whole context, but they sit far below 200,000 on a 1M window, so 200,000 is treated as a ceiling to tune down from (Q-5) |
| Session pruning of tool results | **Out of reach** | §7.1 |
| Compaction quality guard, tool-pair-safe cuts | **Not needed** | Huddle writes no summary; a reread is the Room's own words |
| "Dreaming": background consolidation into `MEMORY.md` | **No** | Removed after review (Appendix A): benchmarks and injection risk |
| `memory_search`, `sessions_search` | **Elsewhere** | The `search` App Tool covers the Library; a Transcript `recall` stays a later feature (ADR-0051) |
| Forks, resident history, store maintenance | **Not applicable** | OpenClaw's own session store and threading; Huddle has no equivalent |

### 7.3 Toward OpenClaw's single session: a proposed mode

The goal behind this spec is a Chief of Staff that works like OpenClaw's never-ending single
session. The table in §7.2 takes OpenClaw's habits, but not its central idea, and the difference
should be stated plainly:

| | OpenClaw | This spec as written |
| --- | --- | --- |
| What lasts forever | **One session**, squeezed as it grows | **One Room**; the Claude session under it is replaced from time to time |
| When the context gets long | Compacts and keeps going | A fresh start, rebriefed from the Transcript |
| After a compaction | Carries on | An observed compaction *triggers* a fresh start (§6.2) |
| When durable facts are saved | At a soft token threshold, before compaction (`softThresholdTokens`) | Only just before a fresh start (§6.6) |

**Why the spec took the other route.** OpenClaw owns its sessions: it stores, reads and edits the
history itself. Claude Code owns Huddle's: Huddle cannot read or edit that history, and Claude Code
deletes it after 30 days unused. So the spec makes Huddle's own Transcript the thing that lasts, and
treats the Claude session as disposable. From the Human's seat the Room still *looks* like one
never-ending conversation, but the Agent under it is quietly rebuilt now and then.

**What OpenClaw's "forever" actually is.** Not one context that never ends, but *compaction, plus
facts saved before compaction, plus the recent part kept verbatim*. Huddle can get close to that for
a Room such as the Chief of Staff's with three changes, proposed here as a **single-session mode**
per Room. It is not yet a requirement (Q-13):

1. **Let the Adapter's own auto-compaction run, and carry on.** In single-session mode an observed
   compaction does not trigger a fresh start; it is only recorded (§6.7). The Size trigger stays,
   as a ceiling well above the Adapter's compaction point, or is turned off.
2. **Flush at a threshold, not only before a reset.** Watch `usage_update` and run the memory flush
   (§6.6) once each time the session crosses a set size, as OpenClaw does with
   `softThresholdTokens`, so durable facts reach Memory *before* the Adapter's compaction drops
   detail. Once per crossing, never twice for the same compaction cycle.
3. **Keep retention at 365 days** (phase 0, LR-R6), so a quiet month does not delete the one
   session.

The fresh start stays as the **escape hatch** for a session that has gone stale or into a rut,
whether manual (*Start afresh*) or via the Degraded mark. It stops being the routine.

**Cost of this mode.** It leans on Claude Code's compaction, whose summary Huddle cannot see or
steer beyond `/compact` instructions; the rut that §7.1 describes after repeated compaction is the
risk it accepts. The experiment's rut score (§9.1), after `/compact` against after a fresh start,
is what decides whether this mode is safe to recommend.

---

## 8. Behaviour and edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| LR-E1 | Resume answers "not found" (RS E-1) | Unchanged, except the fresh session's Catch-up uses the phase 1 budget |
| LR-E2 | Resume throws (RS E-2) | Unchanged |
| LR-E3 | `ReadTranscript` refused or timed out (RS E-3) | Unchanged: the Turn goes ahead without the block. After a fresh start, the Room note says *It could not reread the Room* |
| LR-E4 | A successful resume | Unchanged: the gap after the stored `LastMessageId`, now also bounded by the budget. No fresh start is triggered by a resume |
| LR-E5 | `SessionPerRoom: false` (shared session, `RoomSession.RoomId` null) | No Catch-up today and none here; no trigger is evaluated. *Start afresh* is not offered for it |
| LR-E6 | A Human's `/compact` drops `Used` | Not a trigger (LR-R24) |
| LR-E7 | Restart, or a Persona edit that forgets every Room Session | Unchanged (`ForgetAll`) |
| LR-E8 | The Human clicks Stop during a fresh session's first Turn | A Stop like any other; the new session stays |
| LR-E9 | Several Teammates in one Room | Each Room Session is judged and started afresh on its own |
| LR-E10 | The Room is archived or deleted | Archiving changes nothing (rules.md: archiving never reaches delivery). Deleting a Room drops its `room-fresh-starts.json` key and its `last-catch-up` files: `ChatService` removes them after the delete succeeds, and the cached notes are cleared |
| LR-E11 | A Persona is renamed | `PersonaRenameCascade` moves `last-catch-up/{Name}/`, above the "no Agent row" early return (rules.md) |
| LR-E12 | `session-settings.json` corrupt | Ignored with a Warning; configuration defaults apply; Settings shows `session-settings.json could not be read, so the defaults apply: {error}` (`{error}` is the parser's message) in an alert. The same holds when the watcher rereads a file that was corrupted by hand: `Current` falls back to the defaults and `LoadError` is set. Out-of-range keys show the same alert |
| LR-E13 | A Room's Transcript is larger than the budget by far | Only the newest part within the budget is reread; the `Omitted` lines say how much was left out |
| LR-E14 | Two triggers fire in one Turn | One fresh start; the note names the first in §6.2's order |
| LR-E15 | The flush times out, fails or is refused | The fresh start goes ahead; the note says *Nova could not save first* (LR-R27) |
| LR-E16 | The Human clicks Stop during a flush | The flush and the pending item are cancelled as any Stop; the degraded mark stays, so the next item tries again |
| LR-E17 | The Agent's Adapter does not offer the `memory` tool | No flush; the fresh start runs as before (§6.6) |
| LR-E18 | The flush writes nothing | Normal: there was nothing new to save. The note omits the count |
| LR-E19 | The app restarts while a large Room Session is idle | `ContextUsed` and `LastTurnUtc` on the stored entry let the idle trigger fire on the next item, opening fresh instead of resuming (LR-R28) |
| LR-E20 | A fresh session's Catch-up is close to, or above, a trigger threshold | No trigger fires for it: the threshold counts growth above the baseline (LR-R31). A later fresh start happens only once the session has grown by the threshold since it opened |
| LR-E21 | A stored entry has no `ContextUsed`, because its Adapter reported no usage | The idle trigger does not fire for it; it resumes as today |

---

## 9. Phasing and the experiment

Each phase ships on its own and leaves the app coherent if the next never comes. They ship in the
order of the table: phase 2b comes **after** the experiment, because its triggers are built for a
rut the literature describes but no Room in this install has yet shown (Q-6), and the experiment is
what would show it.

| Phase | Ships | Acceptance |
| --- | --- | --- |
| **0. Retention and isolation** | Live checks V0-1 and V0-2; `SessionRetentionDays` if V0-2 passes; LR-R5's Warning | LR-R4 to LR-R6. Findings recorded in known-limits.md |
| **1. Catch-up by budget** | `ISessionSettings`, the token trim, `turn.transcriptOmittedOwn`, Settings › Sessions | LR-R7 to LR-R14. Goldens unchanged at the defaults; a changed value reaches the next first Turn, fresh or resumed, without a restart |
| **2a. Start afresh** | *Start afresh* in the Teammate's profile popover, the Room note, `last-catch-up` | LR-R15 and LR-R18 to LR-R22. Every Room behaves exactly as phase 1 until the Human presses *Start afresh*; it forgets one Room Session, with no flush |
| **3. The experiment** | A manual test page and a recorded result (§9.1); no code | A result recorded, with the go or no-go for phase 2b, the single-session question (Q-13) and phase 4 |
| **2b. Automatic fresh starts** | Only on a "go" from phase 3: the per-Room setting, the four triggers, the memory flush, the records and the fallback note, `BaselineTokens` on `RoomSessionEntry` | LR-R16, LR-R17 and LR-R23 to LR-R31, with thresholds set from phase 3's numbers. A Room with the setting Off behaves exactly as phase 2a, except that *Start afresh* flushes |
| **4. Handoff note** | Only on a "go" from phase 3; specified then | To be written |

### 9.1 The experiment

Run on a real long-running Room (the Chief of Staff's is the intended one), with phase 0 V0-1
passed so Claude Code's own memory is not in play, and with the `memory` App Tool live, because
that is how the Room will really run. Record everything in
`engineering/manual-tests/long-running-rooms.md` (written with phase 1, listed in [docs/Index.md](Index.md)
in the same change).

1. **Recall score.** From the Transcript, pick 10 facts or decisions older than the last 20
   Messages; note their Message ids.
2. *Start afresh* the Teammate in this Room (phase 2a) at the default Catch-up; not the card's
   Restart, which forgets its sessions in every other Room too. Ask the 10 questions; score 1 for each correct
   answer, and note for each whether the answer came from a memory file (the Teammate read one, or
   it is in the index) or from the reread. Note the first Turn's cost from the Teammate card.
3. Set the budget to 40,000 tokens **and** the Message cap to 400, so the budget is the bound that
   is reached (LR-R7: the Catch-up ends at whichever comes first, and at a cap of 20 neither the
   default nor 40,000 tokens binds). *Start afresh*; ask again; score and cost. Check in the Room
   note that the reread went past 20 Messages, or the step has measured the default twice.
4. **Rut score.** In a live session, ask "suggest three things we haven't tried yet" five Turns
   apart, three times; count suggestions that repeat. Then run `@Teammate /compact keep the
   decisions and open questions` and repeat the count. Then *Start afresh* and repeat it.
5. **The flush, A/B, by hand.** The flush is not built yet (phase 2b), so play it: twice, a few
   days apart so new facts have accrued, *Start afresh* with no warning, ask five questions about
   decisions from the last week, and score; then the same, on fresh questions, after first asking
   the Teammate "save anything worth keeping to memory before I start you afresh". Note the files
   it saved and that Turn's cost. This settles whether the flush earns its Turn, or whether the
   `memory` tool's own Prompt ("save as you go") already does the job (it replaces the first
   draft's Q-9).
6. **Decision.**
   - Recall at 40,000 tokens 7 of 10 or better **and** fewer repeats after a fresh start than after
     compaction: phases 0 to 2a meet the goal; build phase 2b with thresholds from these numbers
     and the cost of each fresh start, so automatic fresh starts can be turned on where wanted, and
     do not build phase 4.
   - Fewer repeats after `/compact` than, or as few as, after a fresh start: the rut does not need a
     fresh start in this Room; take the single-session mode (§7.3, Q-13) instead of phase 2b's
     trigger set, not both.
   - Recall below 7 of 10, and the missed answers are durable facts the Teammate should have saved:
     the fix is the Memory Prompt (`systemPrompt.memory`), not this spec. Tune it and repeat.
   - Recall below 7 of 10, and the missed answers are conversation (what was being worked on, open
     questions, the reasoning behind a choice): build phase 4, then repeat the experiment with it.
   - No difference in repeats: record it; the rut claim is unproven for this Room (Q-6).
   - The flush scores no better than no flush: leave it out of phase 2b (or ship `FlushToMemory`
     false), tune the `memory` Prompt if facts were missed, and record why.

### 9.2 Phase 4 candidate: the handoff note

Sketch only, for the case the experiment calls for it. With the `memory` App Tool live, decisions
and preferences should already be in Memory, so the note carries only what does **not** belong
there: what the Teammate was doing, the open questions, and the reasoning behind recent choices.

Phase 2's memory flush already asks the old session to save durable facts. Phase 4 extends that
same Turn rather than adding another: its Prompt (proposed `turn.handoffRequest`, replacing
`turn.memoryFlush` when phase 4 is on) also asks for a short note of the open work, each line
citing Message ids. The note is stored at
`{DataDir}/room-sessions/handoff/{Name}/{roomId}.md`, editable by the Human, and placed above the
Catch-up in the new session's first Turn.

Why this rather than the first draft's background Digest: the old session knows its own tool calls
and reasoning, which no reread can recover; it needs no separate model host (the first draft's Q-8),
no daily caps and no background spend; and it is written once per fresh start, not continuously.
Its risks must be specified before it is built: a note is a model summary and can be wrong
(benchmarks favour raw text, Appendix B), and planted text in the Room can reach it, so it must be
marked as the Teammate's own note, visible, and never promoted anywhere.

### 9.3 First run (provisional)

Run 2026-10-31 by an agent on a **seeded** Room, not the Chief of Staff's real one: a fresh install
whose Chief of Staff (on `opus[1m]`) held a real 127-Message conversation about a small company's
product launch, about 66,400 tokens, with 15 facts planted early and an answer key. The code was
the build before phases 1 and 2a, so *Start afresh* was a hand edit of `room-sessions/<Name>.json`
and the 40,000-token reread was an 82-Message cap that kept the Teammate's own Messages (LR-R11
would have dropped them first). Spend about $35. Full tables and the answer key were kept outside
the repository; the procedure is [long-running-rooms.md](engineering/manual-tests/long-running-rooms.md).

| Measure | Result |
| --- | --- |
| Isolation (V0-1, auto memory only) | Held: the `memory` tool saved the test fact; Claude Code wrote no memory of its own |
| Recall, default reread (two runs) | 8/10, 10/10 |
| Recall, about 40,000 tokens (two runs) | 9/10, 9/10 |
| What was missed | Only conversation facts (an email's status, a vendor choice), never a durable fact |
| Repeated suggestions out of 9: live / after `/compact` / after a fresh start | 1 / 2 / **7** |
| Flush A/B: no request / ask to save first (two runs) | 5/5 vs 5/5; 4/5 vs 5/5 (the miss was the one decision not saved). The save Turn cost $0.83 and $1.49 |

**What it shows.** Recall after a fresh start is good: Memory plus a short reread kept the
decisions. But the fresh start made the rut **worse**: the Teammate re-pitched its earlier
suggestions as new, because the list of what it had already suggested lived only in older Room
history, while `/compact` kept it. Of §9.1 step 6's rows, only "as few repeats after `/compact`"
holds, which points to the single-session mode (§7.3, Q-13) rather than phase 2b's triggers. The
flush helped once in two runs.

**A finding the decision table does not cover.** In one recall run the Teammate, failing to find
two facts that were in its Memory, answered "never discussed" and **wrote that false negative into
a memory file**. A miss after a fresh start can corrupt Memory, and every later session inherits
it. That is a Memory Prompt question (`systemPrompt.memory`: do not record the absence of a fact)
whichever way the decision goes.

**Why it is provisional.** One seeded Room and one repeats round; the resumed rounds were not fully
isolated (Claude Code's session file sat outside the copied data folder, since fixed on the test
page); and phase 1's own-Messages-first trim was not in play.

**Closed, 2026-10-31.** The Human closed the effort on this result: recall after a fresh start held,
and the issues it found were fixed in the Memory Prompt (never save that a fact is missing; keep a
record of proposals and the Human's answers) and the tool-call log (a `memory` call names its file).
Phase 2b, the single-session mode (Q-13) and phase 4 are not pursued, and the LONGROOMS-04 rerun
on the real Room was not made. Reopen from here if fresh starts or the rut become a problem again.

---

## 10. Testing

Test-first, per [agents/Testing.md](../agents/Testing.md): every test has a `///` summary,
user-facing text is asserted exactly, and a test that passes on arrival gets a mutation. Fixtures:
`TempDataDir`, `ManualTimeProvider`, `FakePromptSource`, `FakeAgentHostFactory`, `FakeAgentSession`.

| Test class (proposed) | Pins |
| --- | --- |
| `SessionSettingsStoreTests` | Absent file means configuration defaults; a save writes overrides only; out-of-range refused with its text; a hand edit is picked up; a corrupt file is ignored with a Warning |
| `CatchUpBudgetTests` | The first Turn asks for the Message cap and trims to the budget; own Messages dropped first except the newest five; Transcript order kept; both omitted lines exact; applies to fresh and resumed first Turns; no `ForgetAll` |
| `CatchUpDefaultsTests` | **LR-R2's proof:** at the defaults, `turnPromptTranscript.txt` and `turnPromptTranscriptResumed.txt` are unchanged |
| `FreshStartTriggerTests` | Each trigger fires at its threshold and not below; a resumed session's baseline update is not a fall; a Command Turn never triggers; Off evaluates nothing; `MaxPerDay` per local day; idle fires only when both idle time and size are met, survives an app restart through `ContextUsed`, and never fires for an entry without a reading; Size and idle count growth above `BaselineTokens`, so a fresh session whose Catch-up exceeds a threshold is not marked, and an entry without a baseline uses zero |
| `MemoryFlushTests` | The flush runs once before a live fresh start and never for Idle and large; its text is never posted; skipped without the `memory` tool or with `FlushToMemory` false; a timeout, failure or refusal never blocks the fresh start; Stop cancels it and keeps the mark; the Prompt carries no tool-name prefix; files saved are counted in the note |
| `FreshStartRecordTests` | Count, reason, context use and time recorded per Room and Name; moved on rename; the fallback note posted once per degraded mark, with its exact text |
| `FreshStartTests` | The old entry is forgotten and the next Turn carries Catch-up; never mid-Turn; waits while a Question waits; not a failure; no Transcript Message; the note Envelope's fields; `last-catch-up` written as sent |
| `StartAfreshTests` | Forgets one Room's session only; other Rooms of the same Teammate keep theirs |
| `ProtocolJsonTests` (extended) | Literal JSON of `StartAfresh` and `FreshStartNote`; `ProtocolVersion.Current` unchanged |
| `PersonaRenameCascadeTests` (extended) | The `last-catch-up` move sits above the early return |
| bUnit: Settings › Sessions, profile popover, Room note | Exact copy; Reset; the setting's label; the note's wording for each reason |

---

## 11. Configuration

| Key | Default | Note |
| --- | --- | --- |
| `Team:Acp:TranscriptCatchUpMessages` | `20` | Exists. Now the Message cap; Settings may override |
| `Team:Acp:TranscriptCatchUpTokens` | `100000` | Proposed. Does not bind at 20 Messages (LR-R14); the experiment uses 40,000 |
| `Team:Acp:CatchUpKeepOwnMessages` | `5` | Proposed (LR-R11) |
| `Team:Acp:SessionRetentionDays` | `365` | Proposed; only if V0-2 passes |
| `Team:Acp:FreshStarts:CompactionDropPercent` | `50` | Proposed |
| `Team:Acp:FreshStarts:AboveTokens` | `200000` | Proposed. Growth above the session's baseline (LR-R31). Zero disables |
| `Team:Acp:FreshStarts:AfterTurns` | `80` | Proposed. Only for a session that reports no usage. Zero disables |
| `Team:Acp:FreshStarts:MaxPerDay` | `3` | Proposed. Automatic fresh starts per Room and Agent per local day (LR-R23) |
| `Team:Acp:FreshStarts:IdleMinutes` | `60` | Proposed. Matches the subscription prompt cache's lifetime. Zero disables the idle trigger |
| `Team:Acp:FreshStarts:IdleAboveTokens` | `50000` | Proposed. Growth above the baseline (LR-R31); a session that grew less is always resumed |
| `Team:Acp:FreshStarts:FlushToMemory` | `true` | Proposed (LR-R26); the experiment's A/B may change the default |
| `Team:Acp:FreshStarts:FlushTimeoutSeconds` | `120` | Proposed (LR-R27) |

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
| Q-5 | Are 200,000 tokens, a 50 % fall, and 60 minutes with 50,000 tokens the right thresholds? | No | Starting values. Treat 200,000 as a ceiling: OpenClaw's pruning acts from max(50,000, 30 % of the window) and its compaction keeps 20,000 recent tokens (§7.2). Tune down from the experiment and the records of §6.7 |
| Q-6 | Does a fresh start measurably reduce the rut? | No | The best evidence is about tasks derailing, not repetition; the experiment records it. First run (§9.3, provisional): a fresh start gave **more** repeats than `/compact`, 7 to 2. Closed 2026-10-31 on that result: no automatic fresh starts (§9.3) |
| Q-7 | Should observing compaction use ACP `compaction_update` instead of the `Used` fall? | No | Later; it needs a mapping in `SessionUpdateMapper` (ACP effort) and a client capability Huddle does not advertise yet |
| Q-8 | Should the Room setting be on by default once the experiment passes? | No | No; keep it the Human's choice (§13) |
| Q-9 | Does the memory flush earn its Turn? | No | Decided to build it, after OpenClaw (§6.6); the experiment's A/B (§9.1 step 5) decides whether it stays on by default |
| Q-10 | Does this spec depend on PR #287 merging first? | Phase 2b's flush | No for phases 0 and 1, or for fresh starts themselves. The memory flush needs the `memory` tool, and the experiment (§9.1) should run after it, so it measures the Room as it will really run |
| Q-11 | Should a daily reset be offered after all, as OpenClaw does? | No | No (§7.2); revisit only if the experiment shows the idle and size triggers miss stale sessions |
| Q-12 | Should the flush also run after an observed compaction, when the Adapter's summary has already dropped detail? | No | Yes, as specified: it is cheap and can still save what survived. The records of §6.7 will show how often compaction fires before the Size trigger |
| Q-13 | Should a Room offer a single-session mode (§7.3): Adapter compaction allowed, a threshold-driven memory flush, fresh starts only as an escape hatch? | No | Lean yes for the Chief of Staff, the stated goal. Decide after the experiment's rut score (§9.1), as an alternative to phase 2b's trigger set, not in addition to it; if adopted it becomes requirements in phase 2b. First run (§9.3, provisional) points this way. Closed 2026-10-31: not pursued, and neither is phase 2b (§9.3) |

### 12.2 ADR candidates

1. **A degraded Room Session is replaced by a fresh one briefed from the Transcript, never compacted
   by Huddle.** Extends ADR-0024; keeps ADR-0035.
2. **Catch-up is bounded by a token budget, and an Agent's own Messages are dropped first.**
3. **Memory layers are built only after a recorded experiment shows the cheap levers fall short.**
4. **A live session saves to Memory in one silent Turn before it is retired** (the memory flush).
   Builds on ADR-0071's `memory` tool; prior art OpenClaw.

---

## 13. Choosing automatic fresh starts

This section is written for the Human and moves into the [User Guide](Huddle.UserGuide.md) when
phase 2b ships; until then only *Start afresh* exists.

Every Room keeps everything that was said, and you can always start a Teammate afresh in a Room
from the Teammate's profile popover. Automatic fresh starts let Huddle do it for you when a Teammate's conversation
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

**What a fresh start does:** first the Teammate gets a quiet moment to write anything worth keeping
into its notebook (nothing is posted in the Room; *Start afresh* gains this moment with automatic
fresh starts, and until then you can ask the Teammate to save to memory first). Then it starts a new session and rereads the
Room, up to the size set in Settings, skipping most of its own older suggestions. The Room shows a
note each time, saying why, how many notebook pages were saved and how many fresh starts there have
been, and you can open it to see exactly what was reread. If a big conversation has sat unused for
an hour, it starts afresh without the quiet moment, because reopening the old one would cost about
as much.

If a Teammate needs a fresh start but cannot have one (it has had three today, for example), the
Room tells you, and suggests `@Teammate /compact` or *Start afresh*.

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
| 59 requirements | 31 (25 after review, then 6 from OpenClaw's prior art) | Scope cut to what the goal needs |

The first draft is in git history (commit `139d3696`) for anyone who builds phase 4 or a later
memory feature.

**Added 2026-10-30, after comparing with OpenClaw (§7.2):**

| Change | Why |
| --- | --- |
| Renamed from *Huddle.PersistentMemory* to *Huddle.LongRunningRooms*; requirement ids from `PM-` to `LR-` | Since ADR-0071 "Memory" means the `memory` tool; OpenClaw also keeps "context" and "memory" apart |
| Memory flush before a live fresh start (LR-R26, LR-R27, §6.6) | OpenClaw's best technique: save durable state before forgetting. It replaces Q-9's "lean no" with a build plus an A/B in the experiment |
| Idle-and-large trigger (LR-R28) | OpenClaw's idle reset, made safe for healthy sessions by a size condition; it also avoids a cold reload of a large session after the prompt cache has expired |
| Per-session records (LR-R29, §6.7) | OpenClaw's `compactionCount`; feeds Q-5's tuning |
| Fallback note (LR-R30, §6.7) | OpenClaw's "retry, `/compact` or `/new`" advice |
| Settings tab renamed from *Memory* to *Sessions* | Avoids the clash with the `memory` tool |
| Daily reset considered and not taken (Q-11) | A clock alone retires healthy sessions |
| Single-session mode proposed (§7.3, Q-13) | The goal is a Chief of Staff that works like OpenClaw's never-ending session; states plainly where the spec differs and how to close the gap |

**Changed while building phases 0 to 2a (2026-10-31):**

| Id | The spec said | What was built | Why |
| --- | --- | --- | --- |
| C1 | *Start afresh* in the Room menu, per Teammate (§6.1) | A button in the Teammate's profile popover, opened from the avatar in the Room header | There is no per-Teammate Room menu; the avatars already open a popover |
| C2 | `StartAfresh(RoomId, AgentName)`; `FreshStartNote(RoomId, AgentName, …)` | `StartAfresh(RoomId)`; `FreshStartNote(RoomId, Reason, MessageCount, EstimatedTokens, ReadFailed)`; phase 2b adds the flush members as trailing optionals | The connection identifies the Agent; a name on the wire is a second, spoofable source |
| C3 | Write "the first Turn's text" to `last-catch-up` (§6.3) | `LastCatchUpStore` owns the folder; the `.md` holds the whole prompt of the first ordinary Turn, the `.json` the note | One owner per folder |
| C4 | A Room-level note (LR-R21) | Latest note per Room and Agent in `FreshStartNotes`, persisted through `LastCatchUpStore` | In memory alone, a restart orphaned the reread file (see C12) |
| C5 | Three characters per token (LR-R8) | One Message costs `ceil((SenderName.Length + 2 + Text.Length) / 3)`; `EstimatedTokens` sums the kept Messages | The trim and the note need one measure |
| C6 | Other Messages dropped oldest first (LR-R11) | The second pass also drops the protected newest own Messages; no minimum | The budget is a hard bound |
| C7 | "The receiving Agent's own Messages" | `SenderId == agentId` when known, else `SenderName` equals the Persona name, ignoring case | `agentId` is optional |
| C8 | LR-R5's Warning, no location | Logged by `AdapterCatalog` through an optional logger | It is where entries are built |
| C9 | *Start afresh* not offered for a shared session (LR-E5) | The popover hides the button for `SessionPerRoom == false` and for the Human | The same rule, where the button lives |
| C10 | `turn.transcriptOmittedOwn` wording unspecified | Wording settled in the plan's texts | Model-facing text needs one wording |
| C11 | LR-R5 warns when `UsesToolNamePrefix` is true and `IsolateUserSettings` false | Warns when the entry runs `claude-agent-acp` and `IsolateUserSettings` is false | `UsesToolNamePrefix` is true for every configured entry, so the Warning fired for Adapters with no Claude settings to leak |
| C12 | LR-R22: the reread text viewable from the note, with the note held in memory | The note persists as `last-catch-up/{Name}/{roomId}.json` beside the `.md`; dismissing deletes the `.json` only | A restart cleared the note and left the `.md` unreachable |
| R-2.3 | A corrupt `session-settings.json` is ignored with a Warning (LR-E12) | The watcher's reread of a corrupt hand edit also falls back to the defaults and sets `LoadError` | The panel must show the error whenever the file cannot be read |
| R-4.2 | Settings shows "the error" (LR-E12) | Exactly `session-settings.json could not be read, so the defaults apply: {error}` in an alert; the store's error text is the parser's message only | The panel's sentence already says the rest |
| R-3.5 | Catch-up reads the settings (§5) | One snapshot per first Turn sets both the cap and the budget, and the settings reach every session through the pool | Without the pool's argument the feature is silently off |
| R-D5a | The runner sends `FreshStartNote` (§6.3) | The server accepts it first | An unknown Envelope is a `BadMessage` and marks the Agent Degraded |
| R-D5b | A fresh start applies to a live session (LR-R19) | A Room with no live session still forgets its entry and is remembered as "note pending" in the pool | The next session must still send its note |
| R-D5c | Deleting or renaming touches `last-catch-up` (LR-E10, LR-E11) | `ChatService` removes a deleted Room's files; the notes cache is cleared on rename, removal and delete; `LastCatchUpStore.Has` reports a reread file | A stale cache showed notes for files that were gone |
| P0 | V0-1 and V0-2 run in phase 0; `SessionRetentionDays` if V0-2 passes (LR-R4, LR-R6) | Not run and not built; both deferred to the Human's acceptance (2026-10-30) | A paid live session was not available; the requirements stand |

**Changed 2026-10-30, after the review of PR 289:**

| Change | Why |
| --- | --- |
| Phase 2 split: 2a (*Start afresh* and the Room note) ships before the experiment; 2b (setting, triggers, flush, records, fallback note) only on its "go", with thresholds from its numbers | "Measure, then build" was applied to phase 4 but not to phase 2, whose triggers treat a rut no Huddle Room has shown, at defaults that would rarely fire |
| The experiment uses *Start afresh*, raises the Message cap with the budget, and plays the flush by hand | The card's Restart forgets every Room; a 40,000-token budget under a 20-Message cap measured the default twice; the flush is not built before the experiment |
| Single-session mode and phase 2b are alternatives, chosen by the rut score (Q-13) | The spec held both philosophies; picking one halves the trigger logic |
| `LastUsedTokens` dropped for the existing `RoomSessionEntry.ContextUsed` | The field already exists and is persisted for the context meter |
| `MaxPerDay` counted per Room and Agent, not per Room Session (LR-R23) | A fresh start replaces the Room Session, so a per-session count never reached the limit |
| LR-R28 measures growth above the baseline; Observed compaction called the safety-net trigger | Both contradicted §6.2 |
| LR-R1 and LR-R3 say MUST NOT; the second LR-E20 is LR-E21; the requirement count is 31 | Wording and numbering |

---

## Appendix B: sources

Checked during the 2026-10-16 reviews; the OpenClaw rows were read on 2026-10-30, and their
defaults may change between versions. Third-party measurements are weighted below primary
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
| OpenClaw sessions: one session per key by default, daily and idle resets, `/new` and `/reset` | docs.openclaw.ai/concepts/session (read 2026-10-30) |
| OpenClaw compaction: `keepRecentTokens` 20,000, reserve, overflow recovery, quality guard | docs.openclaw.ai/concepts/compaction; /reference/session-management-compaction/compaction |
| OpenClaw memory flush: silent turn before auto-compaction, once per cycle, `softThresholdTokens` 4,000 | docs.openclaw.ai/concepts/memory; /reference/session-management-compaction |
| OpenClaw session pruning thresholds and cache-TTL behaviour | docs.openclaw.ai/concepts/session-pruning |
| OpenClaw session store: `compactionCount`, token counters, append-only transcript | /reference/session-management-compaction/store and /schema |
