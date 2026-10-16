# Huddle.PersistentMemory — Requirements and Design Specification

**Date:** 2026-10-11 · **Status:** Proposed, not built · **Decision record:** none yet (§13 lists the
ADR candidates) · **Builds on:** [the Room Sessions spec](Huddle.RoomSessions-Specifications.md)
(cited as *RS §n*), [the File Changes spec](Huddle.FileChanges-Specifications.md) and its Memory
(§6.15 there), [ADR-0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md),
[ADR-0024](adr/0024-an-agent-holds-one-session-per-room.md) and
[ADR-0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md), bounded by
[ADR-0051](adr/0051-search-keeps-no-index-of-its-own.md) · **Vocabulary:**
[language.md](engineering/language.md) (Room, Member, Room Session, Transcript, Catch-up, Memory,
Team Memory, Spend, Budget), plus the proposed terms in §1.4 · **Roadmap:**
[item 26](engineering/roadmap.md)

This page specifies **Room Memory**: what lets a Room run for months, the way two people keep
talking for months, without an Agent losing the thread when its session is lost and without the
session itself decaying as it fills. It is for the engineers and agents who will build it, and for
the Human deciding whether to turn it on for a Room. Read §1 for why, §4 for the requirements, §5 to
§9 for the design, §11 for the order it ships in, and §14 for the guidance a Human sees.

The one decision to hold on to while reading: **Room Memory is a per-Room setting, and it is Off
for every Room until the Human turns it on for a reason they can say in one sentence.** What every
Room gets for free is the Baseline: the full Transcript, a catch-up depth the Human can set, and,
from phase 3, a `recall` tool that costs nothing until an Agent uses it.

> [!IMPORTANT]
> Binding before any code in this spec is written: [rules.md](engineering/rules.md) before editing
> `src/Huddle.App`, [traps.md](engineering/traps.md) before touching `Huddle.Contracts` or the wire,
> and [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides them. Where this spec proposes a type, file or key that does not exist yet, it says
> *proposed*.

---

## Contents

1. [Purpose, background and scope](#1-purpose-background-and-scope)
2. [Use cases](#2-use-cases)
3. [Principles](#3-principles)
4. [Requirements](#4-requirements)
5. [Architecture](#5-architecture)
6. [Data model and on-disk layout](#6-data-model-and-on-disk-layout)
7. [Hooking into the Room Session](#7-hooking-into-the-room-session)
8. [The consolidator](#8-the-consolidator)
9. [App Tools, configuration, UI and Spend](#9-app-tools-configuration-ui-and-spend)
10. [Behaviour and edge cases](#10-behaviour-and-edge-cases)
11. [Phasing](#11-phasing)
12. [Testing](#12-testing)
13. [Open questions and ADR candidates](#13-open-questions-and-adr-candidates)
14. [Choosing whether to turn it on](#14-choosing-whether-to-turn-it-on)

---

## 1. Purpose, background and scope

### 1.1 The two decays

A Room's Transcript is never lost. What an Agent *sees* of it decays in two ways.

**Session loss.** A Room Session is resumed by its stored id where the Adapter advertises resume
(RS §6.6). When resume answers "not found" (RS §9 E-1), when the Human presses Restart, or when an
edit to the Persona's text, Model, Effort, Work Mode or Adapter forgets every Room Session
(`RoomSessionStore.ForgetAll`, RS §6.13), the next Turn opens a fresh session. That session's first
Turn carries Transcript Catch-up: at most `Team:Acp:TranscriptCatchUpMessages` (default `20`)
Messages, read through the `ReadTranscript` and `TranscriptTail` Envelopes and rendered as one block
by `RoomSession.AppendTranscriptBlock` (`src/Huddle.App/Acp/Sessions/RoomSession.cs`). It holds the
Messages only: no tool calls, no reasoning, and nothing older than those twenty. A Room three
months old comes back as its last twenty lines.

**Decay inside a live session.** A session that is never lost decays too. Its context window
fills, the Adapter compacts it into a summary nobody reviewed, and later Turns grow vaguer and
repeat themselves. Call that last symptom the **rut**: the same framings, the same suggestions, the
same phrasing, Turn after Turn.

### 1.2 The goal

A Room that can run indefinitely without either decay: an Agent in it can always say what was
decided, what is still open and where to find the exact words, and its working context stays
small and fresh enough that it does not fall into a rut.

### 1.3 What already exists, and what this adds

Most of the layers this needs already exist. This spec adds only the missing Room layer and the
machinery that keeps it current.

| Layer | What it holds | Status |
| --- | --- | --- |
| 1. Raw log | The Transcript, `{DataDir}/rooms/{roomId}.jsonl` (`FileChatStore`) | **Exists.** The source of truth; this spec never edits it |
| 2. Rolling Digests | Day, week and era summaries of one Room, each line citing Message ids | **New** (phase 2) |
| 3. Durable facts | Memory (`Teammates/<Name>/work/memory/`), Team Memory (`Teams/<Team>/memory/`), Project Memory (`Teams/<Team>/<Project>/memory/`) | **Exists** (ADR-0023, ADR-0032). This spec adds a way to *promote* into them, not a fourth store |
| 4. Recall | A `recall` App Tool that re-reads Transcripts and memory notes per call, grep style, with no index (ADR-0051) | **New** (phase 3) |
| 5. The Brief | A budgeted block on a session's first Turn: Digest, Loose ends, recent Messages and recalled snippets | **New** (phase 2, snippets phase 3); replaces the flat Transcript block for a Room with Room Memory on |
| 6. Rotation | The app deliberately opens a fresh session, seeded with the Brief, before compaction drifts | **New** (phase 4) |
| 7. Consolidation | Rewrites Digests and Loose ends, merges duplicates and retires stale items | **New** (phase 2) |

### 1.4 Proposed vocabulary

These terms are new. They are proposed here and go into
[language.md](engineering/language.md) with the ADR that accepts this spec, not before.

| Term | Meaning | Avoid |
| --- | --- | --- |
| **Room Memory** | The per-Room setting, and what it keeps: the Room's Digest and Loose ends, kept current by Consolidation. On or Off per Room; Off by default | room context, chat memory, conversation memory, long-term memory |
| **Baseline** | What every Room has with Room Memory Off: the Transcript, Catch-up at the configured depth, and `recall` | default memory, basic mode |
| **Digest** | One Markdown file summarising one period of one Room (a day, a week or an era), each line citing the Messages it came from. A Room's Digest, singular, is all of them together | summary on its own, recap, minutes |
| **Loose end** | One unresolved item in a Room, listed in its `loose-ends.md`, closed by Consolidation when the Transcript resolves it | open thread (a *thread* is avoided in language.md), todo, Task (a Task is a file in a Team) |
| **Consolidation** | One run that reads new Messages and the current Digest and rewrites them. Real model Spend | summarisation job, compaction (that is the Adapter's own, inside a session) |
| **Steward** | The Agent Member whose Adapter runs a Room's Consolidation and whose Spend it is counted under | owner, keeper (the Engineering Guide already uses *Keepers* for a kind of Teammate) |
| **Brief** | The block a Room Session's first Turn carries in a Room with Room Memory on, in place of the Transcript block | working set (fine in prose about the design), context dump |
| **Rotation** | Closing a healthy Room Session on purpose and opening a fresh one seeded with the Brief | reset, restart (that is the Teammate card's button), refresh |
| **Promotion** | Writing a fact learned in a Room into Memory, Team Memory or Project Memory | export, sync |
| **Memory Proposal** | A card in the Room asking the Human to approve one Promotion into a shared scope | request, draft |

### 1.5 Non-goals

- **No fourth fact store.** Facts about the Human, a Teammate's way of working, a Team or a Project
  already have owners. A Room keeps only what is about the Room itself: its Digest and Loose ends.
- **No index and no embeddings in v1.** Recall is a grep-style phrase match over files read fresh
  per call, as Global Search is (ADR-0051). Semantic search is a later option (§13, Q-3).
- **No reliance on an Adapter's own memory.** Claude Code's auto-memory is not used, and its
  isolation from the Human's settings is unverified ([known-limits.md](engineering/known-limits.md)).
- **No change to the Transcript.** Nothing here edits, compacts or deletes a Transcript line.
- **No change to Room kinds.** There is one kind of Room; Room Memory is a setting on any Room.
- **No automatic turning on.** Not for the Chief of Staff, not for a long Room, not by an Agent.
- **No cross-Room Brief.** A Brief holds one Room's Digest. Continuity between Rooms stays with
  Memory, Team Memory and File Changes (ADR-0024).
- **No tool calls or reasoning in the Digest in v1.** Consolidation reads Messages only (Q-7).

---

## 2. Use cases

| # | Who | What happens | What this spec gives them |
| --- | --- | --- | --- |
| U1 | The Human and Jarvis, the Chief of Staff, daily for months | Jarvis's session is lost after an Adapter upgrade | The fresh session's first Turn carries the Digest and Loose ends, so "where were we on the offsite?" has an answer from week 3, not only the last 20 Messages |
| U2 | The Human and Nova, in a Room planning a house move | The Room passes 300 Messages over five weeks | The app suggests turning Room Memory on, with an estimated cost; the Human turns it on with the reason "we decide things here over weeks" |
| U3 | A five-member Room for one Project | Ada and Kai each need the decisions made while they were away | One Consolidation per Room, not one per Agent; both read the same Digest |
| U4 | The Human, in a quick one-off Room | Asks Nova to rename some files | Nothing changes: Room Memory stays Off, no Consolidation runs, and the prompt is byte-identical to today's |
| U5 | Nova, two months into a Room | Needs the exact wording of a decision the Digest summarises | Calls `recall` with the cited Message id and gets the original text |
| U6 | Jarvis | Learns in a Room that the Human prefers morning meetings | Promotes it to Jarvis's own Memory without asking; it reaches Jarvis's other Rooms as a File Change |
| U7 | Ada, in a Project Room | Learns a decision every Project member needs | Proposes a Promotion into Project Memory; the Human approves it on a Memory Proposal card |
| U8 | The Human | Wonders why Nova keeps citing a plan that was dropped | Opens "What the Agents remember", reads the Digest line, and deletes it |
| U9 | Jarvis, 400 Turns into a live session | Answers are getting repetitive | Rotation opens a fresh session seeded with the Brief, before the Adapter's compaction drifts further |

---

## 3. Principles

1. **The Transcript is the truth; everything else is a lossy, cited copy.** Every Digest line
   names the Messages it came from, so a reader can always go back to the words.
2. **Off until chosen.** Memory costs money, can be wrong, and keeps what nobody decided to keep.
   Turning it on is a decision with a stated reason, never a default.
3. **Visible and editable.** Every byte Room Memory keeps is a plain Markdown file the Human can
   read, edit and delete, in the app or in an editor. Nothing is hidden in a database.
4. **One cost per Room.** One Digest per Room, shared by all its Agents.
5. **Reuse the owners.** Facts go where they already live. Shared scopes need the Human's approval.
6. **Fail toward the Baseline.** Any failure in Room Memory leaves the Room exactly as it is today:
   Turns run, Catch-up works, and the Human is told.
7. **Adapter-agnostic.** Everything reaches the model as prompt text or an App Tool, so a local model
   on `agency-acp` gets the same Room Memory as `claude-agent-acp`.

---

## 4. Requirements

Each requirement has a stable id. **MUST** is a condition of acceptance; **SHOULD** is expected
unless a recorded reason says otherwise. The phase column says when it ships (§11). Phase 2 ships in
two steps: a plain `2` means 2a, and `2b` marks the cost and suggestion work that may follow it.

### 4.1 Baseline

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R1 | Nothing in this feature MUST edit, reorder or delete a Transcript line. | all |
| PM-R2 | With Room Memory Off for a Room, the system prompt and every Turn prompt in that Room MUST be byte-identical to what the same inputs produce before this feature, and the prompt goldens (`turnPrompt*.txt`, `systemPrompt.*.txt`) MUST stay unchanged. The App Tool roster is the one exception: phase 3 adds `recall` and `remember` to every Agent's tools while `Recall:Enabled` is true, which changes `toolDescriptions.txt` and `getHelp.txt` once (Q-10). | all |
| PM-R3 | With Room Memory Off, no model call MUST be made on that Room's behalf by anything other than the Agents' own Turns. | all |
| PM-R4 | The Transcript Catch-up depth MUST be settable on the Settings page, and MUST apply from the next Room Session first Turn without restarting the app, a Persona or a session. `Team:Acp:TranscriptCatchUpMessages` stays the default when no Settings value is stored. | 1 |
| PM-R5 | Changing the catch-up depth MUST NOT forget any Room Session (`RoomSessionStore` is untouched) and MUST NOT be part of the `Persona` record, so `PersonaSupervisor.NeedsRestart` never fires on it. | 1 |
| PM-R6 | The catch-up depth MUST accept 0 to 500. 0 means no Transcript block, as an empty read does today. A value outside the range MUST be refused with a message, not clamped silently. | 1 |
| PM-R7 | `recall` MUST be available to every Agent whatever its Rooms' Room Memory setting, and MUST spend no model tokens beyond the tool call itself. | 3 |

### 4.2 Digest

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R8 | A Room with Room Memory on MUST have exactly one Digest, shared by every Agent Member. One Consolidation run serves the whole Room. | 2 |
| PM-R9 | The Digest MUST be hierarchical: day files, rolled into ISO-week files, rolled into era files (one per calendar quarter). | 2 |
| PM-R10 | Every Digest line that states something from the Room MUST cite at least one source Message id in the `[m:<id>]` form. A roll-up line MUST keep at least one original Message id, not only a citation of the child Digest. | 2 |
| PM-R11 | Digests and Loose ends MUST be stored as Markdown files with YAML front matter under `{DataDir}/room-memory/{roomId}/` (§6). | 2 |
| PM-R12 | Consolidation MUST rewrite, not append: it merges duplicate lines, retires lines the Transcript has since contradicted, and closes Loose ends the Transcript has resolved. | 2 |
| PM-R13 | A Digest or Loose ends file the Human edited MUST never be overwritten by Consolidation. It remains input to later runs and roll-ups. | 2 |
| PM-R14 | A citation of a Message id that is not in the Room's Transcript MUST be dropped with its line, and logged as a Warning. | 2 |
| PM-R15 | Digest text in a Brief MUST be labelled as a summary that may be wrong, and MUST say how to fetch the original wording (`recall`, from phase 3; "ask the Human" before it). | 2 |
| PM-R16 | Consolidation MUST NOT read or write any file outside the Room's `room-memory` folder, except the Promotions of §4.3. | 2 |

### 4.3 Facts and Promotion

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R17 | There MUST be no Room-level fact store. A durable fact is promoted to Memory, Team Memory or Project Memory, in their existing format: one Markdown file per fact, the fact on the first line, read by `MemoryIndex`. | 3 |
| PM-R18 | A Promotion into an Agent's own Memory (scope `self`) MAY be written without asking. It MUST carry a provenance line naming the Room, the source Message ids and whether an Agent or Consolidation wrote it. | 3 |
| PM-R19 | A Promotion into Team Memory or Project Memory made through Room Memory (the `remember` tool, or a Consolidation candidate) MUST go through a Memory Proposal card. Nothing MUST be written before the Human approves. | 3 |
| PM-R20 | The Memory Proposal card MUST say who will see the fact once approved: the scope and the number of Teammates it reaches. | 3 |
| PM-R21 | The outcome of a Memory Proposal MUST be posted as a Message from the Human that Mentions the proposer, as a Teammate Proposal's outcome is (Skills spec §6.10). | 3 |
| PM-R22 | A Promotion MUST NOT duplicate an existing fact in the target scope. Consolidation receives the target's current index and SHOULD propose an edit of the existing file instead. | 3 |

### 4.4 Recall

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R23 | Recall MUST keep no index of its own ([ADR-0051](adr/0051-search-keeps-no-index-of-its-own.md)). Each call re-reads the caller's Room Transcripts and the memory notes it may see, grep style, as `RoomSearchProvider` does, bounded by `Recall:MaxRoomsScanned` and `Recall:MaxResults`. A call cut short by a bound MUST say so in its result. | 3 |
| PM-R24 | `recall` MUST return only Messages from Rooms the calling Agent is a Member of, and only notes from scopes it already sees: its own Memory, the Team Memory and Project Memory of its Teams, and the Digests of its Rooms. | 3 |
| PM-R25 | Each Transcript result MUST carry the Room's name and id, the Message id, its timestamp, its sender and an excerpt. | 3 |
| PM-R26 | `recall` MUST accept Message ids and return those Messages' full text, so a Digest citation can be expanded to the original words. | 3 |
| PM-R27 | A Message the Human marked *Don't remember* SHOULD be left out of the index and out of Consolidation input. | 3 |
| PM-R28 | Embeddings MUST NOT be part of v1. | 3 |

### 4.5 The Brief

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R29 | In a Room with Room Memory on, a Room Session's first Turn after a **fresh** or **rotated** open MUST carry the Brief in place of the Transcript block. After a **successful resume** the session still holds its own context, so its first Turn MUST carry today's gap block (the Messages after the stored `LastMessageId`) plus a Digest update block when the Digest changed since that session's stored `DigestVersion`, never the whole Brief. A stored entry with no `DigestVersion` (written before this feature) gets the Digest section of the Brief once. | 2 |
| PM-R30 | The Brief MUST hold, in order: the Digest (era, then week, then day files, oldest first), the Loose ends, recalled snippets (phase 3), then the recent Messages, then the triggering Message. | 2 |
| PM-R31 | The Brief MUST end strictly before the Message that started the Turn, as Transcript Catch-up does (RS D-7). | 2 |
| PM-R32 | The Brief MUST fit `Team:RoomMemory:BriefMaxTokens`, estimated at four characters per token. It MUST be trimmed finest detail first, so the long view survives longest: snippets, then day files (oldest first), then week files (oldest first), then recent Messages down to `BriefMinMessages`, and only then era files (oldest first). The newest era file and the triggering Message are never trimmed. | 2 |
| PM-R33 | On a later Turn of the same session, the prompt MUST carry a Digest update block only when the Digest changed since that session last saw it, listing the changed lines, never the whole Digest again. | 2 |
| PM-R34 | The Brief MUST be deterministic for the same files, Transcript and settings, so it can be pinned by a golden. | 2 |

### 4.6 Rotation

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R35 | Rotation MUST apply only to a Room with Room Memory on and `Rotation:Enabled` true. | 4 |
| PM-R36 | Rotation MUST happen only between Turns, immediately before the next item starts: never during a Turn, and never while a Question or Form waits in that Room. | 4 |
| PM-R37 | Rotation MUST trigger when the session's Turns since open reach `RotateAfterTurns`, or its last reported context use (`UsageUpdated.Used / Size`) reaches `RotateAtContextPercent`. Either at zero disables that trigger. | 4 |
| PM-R38 | Before Rotation, the Digest MUST cover the Room up to at least `BriefMinMessages` Messages before the latest one. If it does not, a Consolidation runs first; if that fails, Rotation is deferred, never done blind. | 4 |
| PM-R39 | A rotated session's first Turn MUST carry the Brief. The old Room Session entry MUST be forgotten. Rotation MUST NOT count as a Turn failure or touch the failure streak. | 4 |
| PM-R40 | In a Room with Room Memory on, a resume that answers "not found" (RS §9 E-1) MUST open fresh with the Brief and MUST NOT be reported as a failure, as today. | 2 |

### 4.7 Settings and defaults

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R41 | Room Memory MUST be a per-Room setting, never a kind of Room. | 2 |
| PM-R42 | Room Memory MUST default to Off for every Room, existing and new, the Room with the Chief of Staff included. No code path, Persona field, Team type or Agent MUST turn it on. | 2 |
| PM-R43 | Turning Room Memory on MUST be done by the Human, in a dialog that requires a one-sentence reason (prefilled from a suggestion when there is one, always editable). The reason MUST be stored and shown. | 2 |
| PM-R44 | The app MUST suggest turning Room Memory on, with an estimated cost, when a two-member Room passes `SuggestAfterMessages` Messages or `SuggestAfterDays` days. For a Room of three or more Members it MUST only offer it (Room menu, and a suggestion past `GroupSuggestAfterMessages`). A dismissed suggestion MUST NOT return for `SuggestSnoozeDays`. | 2b |
| PM-R45 | No App Tool and no Agent MUST be able to turn Room Memory on or off. | 2 |
| PM-R46 | Turning Room Memory off MUST be one action, MUST say what happens (Consolidation stops; existing files are kept until you delete them), and MUST stop Consolidation for that Room at once. A run in progress is cancelled and writes nothing. | 2 |
| PM-R47 | An Archived Room MUST NOT consolidate; unarchiving resumes it. Deleting a Room MUST delete its `room-memory` folder. | 2 |
| PM-R48 | The Room header MUST show `Memory: On` or `Memory: Off`, with the stored one-line reason when On. | 2 |
| PM-R49 | The Human MUST be able to delete a Room's Room Memory (Digests and Loose ends) in one action, with a confirmation. Transcripts and already-promoted facts are not touched. | 2 |

### 4.8 Safeguards

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R50 | The Room MUST offer a "What the Agents remember" view listing every Digest file and the Loose ends, each readable, editable and deletable. | 2 |
| PM-R51 | Every Room Memory file MUST be plain Markdown, hand-editable in an editor, and an edit MUST reach the next Brief without a restart. | 2 |
| PM-R52 | The view MUST show the last Brief each Agent received in the Room, as sent, so "what did the Agent actually see?" has an answer. | 2 |
| PM-R53 | Room Memory MUST work on any Adapter, including one with `ReadsFiles` false, and MUST NOT depend on Claude Code's auto-memory. | 2 |
| PM-R54 | An unreadable Room Memory file MUST be reported in the view with its path and reason, never silently skipped (rules.md: a malformed file is reported, never swallowed). | 2 |

### 4.9 Cost

| Id | Requirement | Phase |
| --- | --- | --- |
| PM-R55 | Consolidation Spend MUST be counted in `PersonaSpend` under the Room's Steward, so it appears on the Steward's Teammate card (2), and MUST also be shown per Room in the "What the Agents remember" view (2b). | 2, 2b |
| PM-R56 | Each Room MUST have a daily Consolidation cap: at most `MaxRunsPerRoomPerDay` runs (2), and, where the Adapter reports cost, at most `MaxSpendPerRoomPerDay` (2b). Each run MUST read at most `MaxInputTokensPerRun` estimated tokens (2). | 2, 2b |
| PM-R57 | When a cap is reached, Consolidation for that Room MUST pause until the next local day (§6.3) or until the Human allows one more day's runs. The Room MUST degrade to the Baseline plus its existing Digest, marked as stale in the Brief, and the Room MUST say so once. Turns MUST never wait on Consolidation. | 2 |
| PM-R58 | The turn-on dialog MUST show an estimate: runs per day from the Room's Message rate over the last 14 days, input tokens per run, and money only when the Steward's Adapter has reported cost before. | 2b |
| PM-R59 | Consolidation posts no Messages, so it MUST NOT count against the Room's Budget, and it MUST NOT count against the Steward's token Budget (`Team:Acp:TokenBudget`), which exists to catch a looping Agent. Its own cap is PM-R56. | 2 |

---

## 5. Architecture

```text
                             ┌──────────────────────────── Huddle.App (server side) ───────────────────────────┐
 Human ── Settings ─────────►│ SessionSettingsStore (P1)        RoomMemoryStore (P2)                            │
       ── Room header ──────►│        │                          {DataDir}/room-memory/{roomId}/...              │
                             │        │                                 ▲          ▲                             │
 Transcript (rooms/*.jsonl)─►│ RoomEvents.MessagePosted ─► RoomMemoryConsolidator ─┘          │                  │
                             │                                (hosted service, P2) ─► ConsolidatorHost (Adapter) │
                             │                                       │ Spend ─► PersonaSpend (Steward)           │
                             │ recall (P3): re-reads Transcripts and notes per call, no index ◄┘                 │
                             │        ▲                                                                          │
                             │ AgentConnection.HandleReadTranscriptAsync ── TranscriptTail + Brief ──┐           │
                             │ App Tools: recall, remember (P3) ──► MemoryProposalStore ─► card       │           │
                             └────────────────────────────────────────────────────────────────────────┼───────────┘
                                                                                                      ▼ pipe
                                                    PersonaRunner ► RoomSessionPool ► RoomSession (first Turn: Brief;
                                                                                    rotation check at Turn end, P4)
```

- **The server builds the Brief; the runner renders it.** A runner stays an ordinary pipe client
  that learns about Room history only from Envelopes (ADR-0024). The Brief rides on the existing
  `ReadTranscript` and `TranscriptTail` pair as optional fields (§7.2).
- **Consolidation is a server-side hosted service.** It is created once at build time, so it may be
  a `BackgroundService`; only `PersonaRunner` must not be one (rules.md).
- **Recall is an App Tool**, whose body runs inside `Huddle.App` and may read the Transcript
  through `IChatStore`, like `ReadRecordsTool` does.

---

## 6. Data model and on-disk layout

### 6.1 Layout

```text
{DataDir}/
  session-settings.json                 Phase 1. Overrides only; absent is normal
  room-memory/
    {roomId}/
      room-memory.json                  The setting and the bookkeeping. Absent means Off, never suggested
      loose-ends.md
      digest/
        days/2026-10-11.md
        weeks/2026-W41.md
        eras/2026-Q3.md
        rolled/                         Children folded into a roll-up; out of the Brief, still recalled
      last-brief/{Name}.md              The last Brief each Agent got here, as sent (PM-R52)
```

- `room-memory` joins `WatchedFolderResolver.ReservedFolders` beside `rooms`, `file-state` and
  `room-sessions`, so it is never a Watched Folder and a Consolidation never floods File Changes.
  The Brief and the Digest update block (PM-R33) are how Agents learn of a change.
- **Files, not `team.db`.** The runner has no database access, the state is per Room and small, and
  the Human must be able to edit it (PM-R51). This also sidesteps the `rooms` column trap
  (rules.md: persisted Room state goes in a sibling table, never a new column).

### 6.2 `room-memory.json`

```json
{
  "enabled": true,
  "reason": "We plan the house move here over weeks.",
  "changedUtc": "2026-10-11T09:12:00Z",
  "steward": "Nova",
  "consolidatedThrough": "01J9C4…",
  "lastRunUtc": "2026-10-11T21:40:03Z",
  "runsToday": 2,
  "runsDay": "2026-10-11",
  "extraRunsGranted": 0,
  "consecutiveFailures": 0,
  "paused": null,
  "suggestionDismissedUtc": null,
  "excludedMessageIds": [],
  "written": { "digest/days/2026-10-11.md": "sha256:9f2c…", "loose-ends.md": "sha256:41ab…" }
}
```

| Field | Meaning |
| --- | --- |
| `enabled` | The setting. Absent file or `false` is Off |
| `reason` | The Human's one sentence, shown in the header (PM-R43, PM-R48) |
| `steward` | The Steward's **Name**; moved by `PersonaRenameCascade` |
| `consolidatedThrough` | The last Message id any Consolidation has read |
| `written` | The hash of each file as Consolidation last wrote it. A file whose hash differs was edited by the Human and is never overwritten (PM-R13) |
| `paused` | `null`, `"cap"`, `"failures"`, `"noSteward"` or `"archived"`, shown in the view |
| `excludedMessageIds` | Messages the Human marked *Don't remember* (PM-R27) |

Written atomically (`.tmp`, then `File.Move(…, overwrite: true)`) under one `Lock`, as
`RoomSessionStore` does. A corrupt file is treated as Off, logged, and shown in the view (§10).

### 6.3 A Digest file

```markdown
---
room: 01J8MOVE00000000000000000
level: day
period: 2026-10-11
from: 01J9A0…
through: 01J9C4…
messages: 86
written: 2026-10-11T21:40:03Z
by: consolidation
---
# Saturday 11 October 2026

- The Human chose the Thursday removal slot over Saturday, to avoid the weekend surcharge. [m:01J9B2…]
- Nova is to confirm whether the van can take the piano; not yet answered. [m:01J9B7…]
- Dropped: the plan to store the sofa at Ada's. [m:01J9A9…] [m:01J9C1…]
```

- `level` is `day`, `week` or `era`; `period` is `yyyy-MM-dd`, `yyyy-Www` or `yyyy-Qn`.
- **One clock for every day boundary.** A day's file, its roll-up and the daily cap
  (`runsDay`, PM-R56, PM-R57) all use the installation's local time zone, read from the injected
  `TimeProvider.LocalTimeZone`, never UTC for one and local for another. Every timestamp stored
  (`written`, `lastRunUtc`) stays UTC; only the *period* a Message falls in is local.
- `by` is `consolidation` or `human`. The view sets `human` on save; the hash check (§6.2) catches an
  editor save that leaves it unchanged.
- **Roll-up.** Day files older than `DaysBeforeWeekRollup` (14) are folded into their ISO week; week
  files older than `WeeksBeforeEraRollup` (12) into their quarter. The children move to `rolled/`.
- **Drift bound.** A roll-up run is given each child line *and* the original Messages its citations
  name, up to the run's input cap, and every kept line keeps at least one `[m:…]` citation
  (PM-R10). A summary of a summary can lose detail, but every claim stays one `recall` away from
  the words it came from.

### 6.4 `loose-ends.md`

```markdown
---
room: 01J8MOVE00000000000000000
written: 2026-10-11T21:40:03Z
by: consolidation
---
# Loose ends

- Can the van take the piano? Nova to confirm with the removal firm. (since 11 Oct) [m:01J9B7…]

## Pinned

- The Human's own lines. Consolidation reads this section and never edits it.
```

A Loose end closed by Consolidation leaves this file and appears in that day's Digest as
`Closed: …`, with the citation of the Message that closed it.

### 6.5 A promoted fact

The existing Memory format, unchanged, with a provenance line after the fact:

```markdown
The Human prefers meetings before 11:00.

Source: Room 'Jarvis' (id 01J7…), Messages 01J9B2…, 01J9B5…; promoted by Consolidation on 2026-10-11.
```

`MemoryIndex` reads only the first line, so the provenance line changes nothing in the index.

### 6.6 `session-settings.json` (phase 1)

```json
{ "transcriptCatchUpMessages": 60 }
```

Overrides only, like `prompts.json` and `library-roots.json`: absent is normal, the app does not
create it, and it appears on the first save from Settings. Hand-editable and watched.

### 6.7 Recall keeps no index (phase 3)

There is no recall database. [ADR-0051](adr/0051-search-keeps-no-index-of-its-own.md) rejects any
structure that holds names or text for the purpose of search, and `recall` follows it: each call
reads the Transcripts of the caller's Rooms through `IChatStore.ReadAllAsync` and the memory
notes it may see (PM-R24) from disk, matches the phrase ignoring case, and keeps nothing between
calls. So a Message posted a second ago is found, a deleted Room is simply not read, and there is
nothing to build, invalidate or rebuild.

The read is bounded the way Global Search bounds its own: at most `Recall:MaxRoomsScanned` Rooms
per call (the caller's Rooms by most recent activity first, the `roomId` argument's Room only when
given) and `Recall:MaxResults` hits. A call cut short says so.

Room Memory's Digest is not a search structure in ADR-0051's sense. It exists to brief a fresh
session (§7), not to answer searches, and `recall` reads its files as plain notes, like any other
Markdown on disk.

---

## 7. Hooking into the Room Session

### 7.1 Phase 1: the depth comes from a store

`RoomSession` reads `this.options.TranscriptCatchUpMessages` at the Transcript read. Phase 1 adds a
proposed `ISessionSettings` (one implementation, `SessionSettingsStore` over
`session-settings.json`) and reads `TranscriptCatchUpMessages` from it at that point, per first
Turn. The `AcpOptions` value is its default. Nothing else changes: the header, `Omitted` line and
goldens stay as they are.

### 7.2 Phase 2: the Brief rides on `ReadTranscript`

Two additive, optional fields, so `ProtocolVersion.Current` stays 3 (traps.md: adding to the wire is
not changing it):

```csharp
/// <summary>Asks for a Room's Messages, and the Room's Brief when it has Room Memory on. Client to server.</summary>
/// <param name="WantBrief">True asks the server to add <see cref="TranscriptTail.Brief"/> when the Room has Room Memory on.</param>
public sealed record ReadTranscript(
    string RequestId, string RoomId, string? AfterMessageId, string BeforeMessageId, int Max,
    bool WantBrief = false) : ProtocolMessage;

/// <summary>The answer to one <see cref="ReadTranscript"/>.</summary>
/// <param name="Brief">The Room's Brief sections, or <see langword="null"/> when Room Memory is Off or none was asked for.</param>
public sealed record TranscriptTail(
    string RequestId, string RoomId, IReadOnlyList<ChatMessage> Messages, int Omitted,
    RoomBrief? Brief = null) : ProtocolMessage;

/// <summary>What a Brief holds before the runner renders it with Prompts (proposed).</summary>
public sealed record RoomBrief(
    IReadOnlyList<BriefDigest> Digests, string? LooseEnds, IReadOnlyList<ChatMessage> Snippets,
    bool Stale, string DigestVersion);
```

The two existing records keep their parameters in today's order (RS §6.5) and gain only the trailing
optional field; `RoomBrief` and `BriefDigest` are new. `ProtocolJsonTests` pins the literal JSON with and without it.

`RoomSessionEntry` gains a trailing optional `string? DigestVersion = null`, the same way it gained
`WorkMode`, so every existing construction and every file already written load unchanged. It
records the Digest this session last saw, which is what lets a resumed session get only the
changes (PM-R29) instead of the whole Brief again.

`AgentConnection.HandleReadTranscriptAsync` fills `Brief` only when `WantBrief` is true **and** the
Room's `room-memory.json` says On. With Room Memory on, `Max` is `BriefMaxMessages` rather than the
catch-up depth. The server trims to `BriefMaxTokens` (PM-R32); the runner renders.

### 7.3 Phase 2: what `RoomSession` changes

| Step | Today | With Room Memory on |
| --- | --- | --- |
| First Turn after a fresh or rotated open | `ReadTranscriptAsync(…, TranscriptCatchUpMessages)`; `AppendTranscriptBlock` | Same call with `WantBrief: true`; a proposed `AppendBriefBlock` renders the Brief when one came back, else today's block |
| First Turn after a successful resume | The gap after `resumedAfterMessageId`; `turn.transcriptResumedHeader` | Unchanged gap block, plus a Digest update block against the entry's stored `DigestVersion` (PM-R29). No full Brief |
| Later Turn | Catch-up buffer and own posts | Same, plus a Digest update block when `DigestVersion` moved (PM-R33) |
| Turn end | `RoomSessionStore.Put` | Same, with the `DigestVersion` this session has now seen stored on the entry, plus writing `last-brief/{Name}.md` when this Turn carried a Brief |
| Resume "not found" (E-1) | Fresh; latest `TranscriptCatchUpMessages` | Fresh; Brief |

Block order in a first-Turn prompt: File Changes, Library documents, the Brief (or the Transcript
block), then the Message, matching RS §8.2. The Brief header Prompt, proposed
`turn.briefHeader`, reads:

```text
[Room: House move (id: 01J8…)] This is a new session for this Room, which has Room Memory on.
Below is a summary of the Room so far, written by a model; it can be wrong. Each line cites the
Messages it came from as [m:id]; call recall with an id to read the original words.
```

New Prompts, all `Live` timing: `turn.briefHeader`, `turn.briefDigestHeader`,
`turn.briefLooseEndsHeader`, `turn.briefSnippetsHeader`, `turn.briefRecentHeader`,
`turn.briefStale`, `turn.digestUpdateHeader`. None contains `mcp__team__`; a tool is named through
`{{toolName}}` (rules.md).

### 7.4 Phase 4: Rotation

At Turn end, after `RoomSessionStore.Put`, `RoomSession` checks PM-R37's two triggers. When one
fires and PM-R36 holds, it marks `rotatePending`. The next item admitted to that Room Session:

1. Asks `RoomMemoryConsolidator.EnsureCoveredAsync(roomId, latestMessageId)` (PM-R38). A failure
   or a cap defers Rotation and clears the mark; the Turn runs on the old session.
2. Closes the session, calls `RoomSessionStore.Forget(name, roomId)`, and opens fresh through the
   existing lazy-open path, so `firstTurnPending` is set and the Turn carries the Brief.
3. Logs one Information line and records `lastRotationUtc` for the view. Nothing is posted.

Rotation is never a Stop, a timeout or a failure (rules.md: a Turn ends four ways), and Restart's
`ForgetAll` is unchanged.

---

## 8. The consolidator

### 8.1 Triggers

`RoomMemoryConsolidator` (proposed) subscribes to `RoomEvents.MessagePosted` and keeps a count per
Room with Room Memory on. A Room becomes due when:

| Trigger | Default |
| --- | --- |
| New Messages since `consolidatedThrough` reach `ConsolidateAfterMessages`, and the Room has been quiet for `ConsolidateQuietMinutes` | 40 Messages, 10 minutes |
| The local day ends and the day has any unconsolidated Message | on |
| A roll-up is due (§6.3) | checked daily |
| `EnsureCoveredAsync` from Rotation | on demand |
| Room Memory was just turned on (backfill of the last `BackfillDays`) | 14 days |

At most `MaxConcurrentRuns` (1) run installation-wide, oldest-due Room first. A Room that is
Archived, Off, paused or over its cap is never due.

### 8.2 Inputs and outputs

**Inputs**, all bounded by `MaxInputTokensPerRun`: the current day's Digest, every week and era
file still in the Brief set (not `rolled/`), so a new Message can retire or contradict an older
line (PM-R12), `loose-ends.md` (including `## Pinned`), the new Messages oldest first minus `excludedMessageIds`, the Members'
Names, and, from phase 3, the indexes of the promotion targets (each Agent Member's Memory and its
Teams' Team and Project Memory). More new Messages than fit are processed in several runs, oldest
first, each advancing `consolidatedThrough`.

**Output**, a Markdown document with exactly these headings, parsed strictly:

```markdown
## Day digest
- … [m:…]
## Loose ends
- … [m:…]
## Closed
- … [m:…]
## Facts
- self:Nova | The Human prefers meetings before 11:00. [m:…]
- project:Platform/Porto | Releases go out on Thursdays. [m:…]
```

The consolidator validates every citation against the Transcript (PM-R14), writes each file
atomically, records its hash, and only then advances `consolidatedThrough`. A `self:` fact is
written to that Agent's `memory/` folder; a `team:` or `project:` fact becomes a Memory Proposal
in the Room (§9.4). Phase 2 ignores `## Facts`.

### 8.3 Where it runs, and on what

Consolidation needs a model but no tools. It runs on a **consolidator host**: an Adapter process
started on demand, with a fixed system Prompt (proposed `consolidation.system`), no App Tools, a
scratch Work Dir `{DataDir}/room-memory/_work/`, a permission handler that refuses every tool call,
and the Model and Effort from `Consolidator:Model` and `Consolidator:Effort` (a cheaper Model). It
closes after `Consolidator:IdleMinutes`. It uses the Steward's Adapter Profile unless
`Consolidator:AdapterId` names another.

`IAgentHostFactory.StartAsync` takes a `Persona` and an Agent id today, so this needs a proposed
second entry point for a host with no Persona and no tools (Q-8, blocking). The host counts towards
`Team:Acp:MaxLiveProcesses`.

### 8.4 Who writes memory: Agent or consolidator

| Writer | Strengths | Weaknesses |
| --- | --- | --- |
| **The Agent**, during its Turn, with its file tools or `remember` | It knows what matters; no extra run; the fact is written while the Human is there to correct it | Inconsistent: it forgets to, or writes too much; nothing summarises the Room as a whole; one Agent's view in a group Room |
| **The consolidator**, in the background, on a cheaper Model | Consistent and complete over every Message; one cost per Room; works for an Adapter with no file tools | Real Spend; a cheaper Model misreads nuance; writes when nobody is watching |

**Decision: both, split by kind.** Agents write facts, as they do today. The consolidator writes
Digests and Loose ends, and proposes facts the Agents missed. Shared-scope facts from either go
through the Human (PM-R19).

### 8.5 Failure modes

| Failure | Behaviour |
| --- | --- |
| Adapter cannot start, or the run times out (`ConsolidationTimeoutSeconds`, 120) | Nothing written; `consecutiveFailures` + 1; retried at the next trigger |
| Output does not parse, or has no `## Day digest` | Nothing written; counted as a failure; the raw output is logged at Debug only |
| `consecutiveFailures` reaches `MaxConsecutiveFailures` (3) | `paused: "failures"`; one note in the Room; Turns go on with the existing Digest, marked stale |
| A cap is reached | `paused: "cap"` (PM-R57) |
| A file to rewrite was edited by the Human | Left as it is; the run's version of that file is dropped and logged |
| Room Memory turned off or the Room deleted mid-run | The run is cancelled and writes nothing (PM-R46) |
| The Steward is removed | `paused: "noSteward"` until the Human picks another (a Room of two loses its only Agent anyway) |

---

## 9. App Tools, configuration, UI and Spend

### 9.1 `recall` (phase 3)

The tool is named `recall`; the model sees it with its Adapter's prefix (`mcp__team__recall` on
`claude-agent-acp`, bare on `agency-acp`), and that prefix is built in code, never typed into a Prompt (rules.md). Its description is the Prompt
`tool.recall.description`, and `get_help` lists it.

| Argument | Type | |
| --- | --- | --- |
| `query` | string | A phrase matched ignoring case, as Global Search matches. Required unless `messageIds` is given |
| `messageIds` | string[] | Return these Messages in full (PM-R26). At most 20 |
| `roomId` | string | Limit to one Room; it must be one the caller is a Member of |
| `scope` | string | `messages`, `notes` or `all` (default) |
| `limit` | int | Default `Recall:MaxResults` (8), at most 25 |

Result, one block per hit, newest first:

```text
[Room 'House move' (id 01J8…)] 2026-10-04 18:02 Human (message 01J9A9…):
…we could keep the sofa at Ada's until the new place is ready…

[Team Memory: Teams/Platform/memory/release-day.md] Releases go out on Thursdays.
```

Refusals, exact text pinned by tests: `Give a 'query' or 'messageIds'.`, `You are not a member of
room '{name}' (id {id}).`, `No results for '{query}'.`, and, when a bound cut the read short,
a closing line `Results may be incomplete.`, the text Global Search already uses.

### 9.2 `remember` (phase 3)

| Argument | Type | |
| --- | --- | --- |
| `fact` | string | One fact, one line; at most 300 characters |
| `scope` | string | `self`, `team` or `project` |
| `team` | string | Required for `team` and `project`; must be one of the caller's Teams |
| `project` | string | Required for `project` |
| `sourceMessageIds` | string[] | Optional citations, written into the provenance line |

`self` writes one file in the caller's `memory/` folder and answers with its path. `team` and
`project` create a Memory Proposal and answer: `Proposed to {scope}. The Human has been asked to
approve; you will be told the outcome in this Room.` An Agent can still write Team Memory with its
own file tools, as ADR-0032 allows; `remember` is the route that works on every Adapter and goes
through the Human.

### 9.3 Configuration

All under `Team:RoomMemory` (proposed `RoomMemoryOptions`), except phase 1's existing key.

| Key | Default | Note |
| --- | --- | --- |
| `Team:Acp:TranscriptCatchUpMessages` | `20` | Exists. Phase 1 makes it the default under a Settings value |
| `Offered` | `true` | `false` hides the setting and stops every Consolidation; files are kept |
| `SuggestAfterMessages` | `200` | Two-member Rooms (PM-R44) |
| `SuggestAfterDays` | `14` | Two-member Rooms, with at least `SuggestMinMessages` (50) |
| `GroupSuggestAfterMessages` | `500` | Rooms of three or more |
| `SuggestSnoozeDays` | `30` | |
| `BriefMaxTokens` | `6000` | Estimated at four characters per token |
| `BriefMaxMessages` | `20` | Recent Messages in a Brief before trimming |
| `BriefMinMessages` | `5` | The trim floor |
| `ConsolidateAfterMessages` | `40` | |
| `ConsolidateQuietMinutes` | `10` | |
| `BackfillDays` | `14` | Read when Room Memory is turned on; older history is reached with `recall` |
| `DaysBeforeWeekRollup` | `14` | |
| `WeeksBeforeEraRollup` | `12` | |
| `MaxRunsPerRoomPerDay` | `6` | |
| `MaxSpendPerRoomPerDay` | `0.50` | In the Adapter's reported currency; zero or less disables the money cap, never the run cap |
| `MaxInputTokensPerRun` | `30000` | |
| `MaxConcurrentRuns` | `1` | Installation-wide |
| `MaxConsecutiveFailures` | `3` | |
| `ConsolidationTimeoutSeconds` | `120` | |
| `Consolidator:AdapterId` | `null` | `null` uses the Steward's Adapter Profile |
| `Consolidator:Model` | `null` | `null` is the Adapter's default; set a cheaper Model here |
| `Consolidator:Effort` | `null` | |
| `Consolidator:IdleMinutes` | `10` | |
| `Recall:Enabled` | `true` | `false` removes `recall` and `remember` from every roster |
| `Recall:MaxResults` | `8` | |
| `Recall:MaxRoomsScanned` | `50` | Rooms read per call, most recently active first (§6.7) |
| `Rotation:Enabled` | `false` | Per installation; applies only to Rooms with Room Memory on |
| `Rotation:RotateAfterTurns` | `60` | Zero disables |
| `Rotation:RotateAtContextPercent` | `70` | Zero disables; inert on an Adapter that reports no usage |

The Engineering Guide's configuration table gains these rows in the same change that builds each
phase.

### 9.4 UI

Copy follows [language.md](engineering/language.md): "you" for the Human, never "user", "chat" or
"conversation". Check [agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) for an existing
component before building any of these.

| Surface | Phase | What it shows and does |
| --- | --- | --- |
| **Settings › Memory** (new tab) | 1, 2 | Phase 1: *Messages a new session sees* (0–500) with the stored and default values and a Reset. Phase 2: every Room with Room Memory on, its reason, Steward, last run and Spend this week; the Room Memory keys stay in configuration |
| **Room header indicator** | 2 | `Memory: Off`, or `Memory: On · We plan the house move here over weeks.` Opens the panel |
| **Turn-on dialog** | 2 | The reason (required, one sentence), the Steward picker for a Room of three or more (default: the first Agent Member), the estimate (PM-R58), the three questions of §14, and *Turn on* |
| **Turn-off** | 2 | One action in the panel; its confirmation reads *Consolidation stops now. The summaries already written are kept until you delete them.* |
| **Suggestion strip** | 2 | Non-modal, in the Room: *This Room is 3 weeks old with 240 Messages. Room Memory would keep a running summary so Nova keeps the thread after a lost session: about 3 runs a day.* **Turn on…** and **Not now** |
| **"What the Agents remember" panel** | 2 | Status and reason; pause reason; each Digest file and the Loose ends, with View, Edit (the Library's Markdown editor, byte-exact per ADR-0028) and Delete; *Last Brief* per Agent (PM-R52); Spend in this Room; *Delete Room Memory* |
| **Memory Proposal card** | 3 | The fact, the target scope, *Approving shows this to the 5 Teammates in Team Platform*, the source Messages, **Approve** and **Decline**. At most one per Room, in memory like `ProposalStore`, dropped on archive and delete, and lost on an app restart (PM-E21) |
| **Message action *Don't remember*** | 3 | Adds the id to `excludedMessageIds`; `recall` and Consolidation skip it from then on |

The Human-authored outcome Messages of a Memory Proposal are interface copy held as constants in a
proposed `MemoryProposalService`, as `ProposalService`'s are, not Prompts.

### 9.5 Spend

- The consolidator host reports cost as any session does. Each report goes to
  `PersonaSpend.Add(stewardName, sessionId, runningTotal, currency)`, so it is on the Steward's
  Teammate card (PM-R55).
- Per-Room figures go into a proposed sibling table `room_memory_spend(room_id, day, currency,
  amount, runs)` in `team.db`, created with `CREATE TABLE IF NOT EXISTS` beside `persona_spend`, so
  no existing table gains a column (rules.md).
- An Adapter that reports no cost, such as a local Model, still counts runs and estimated input
  tokens; the money cap is inert there and the run cap holds.
- Nothing on the Turn path reads Spend (`PersonaSpend` remarks); the cap is enforced in the
  consolidator only.

---

## 10. Behaviour and edge cases

### 10.1 State table

| Room state | Consolidation | First Turn carries | `recall` |
| --- | --- | --- | --- |
| Off, never suggested | None | Transcript block at the Settings depth | Yes (phase 3) |
| Off, files kept | None | Transcript block | Yes; old Digests stay recallable |
| On | Per §8.1 | Brief | Yes |
| On, paused (cap, failures, no Steward) | None | Brief, Digest marked stale | Yes |
| On, Archived | None until unarchived | Brief | Yes |
| Deleted | None; folder deleted | No Room | Nothing left to read |

### 10.2 Edge cases

| # | Case | Behaviour |
| --- | --- | --- |
| PM-E1 | Resume answers "not found" (RS E-1) | Fresh session. Room Memory on: the Brief. Off: the latest Messages at the Settings depth |
| PM-E2 | Resume throws (RS E-2) | Unchanged: a Turn failure, the entry kept, a second failure forgets it. The next fresh open carries the Brief when Room Memory is on |
| PM-E3 | `ReadTranscript` refused or timed out (RS E-3) | The Turn goes ahead without the block, as today. The first-Turn opportunity is spent, so the next Turn does not get the Brief either; the Digest update block still applies |
| PM-E4 | Consolidation fails | §8.5. The Room keeps working on the existing Digest; never a Turn failure, never a health badge |
| PM-E5 | A Digest file has unparseable front matter | Left out of the Brief with a Warning, listed as unreadable in the panel with its reason, never overwritten (it counts as Human-edited) |
| PM-E6 | `room-memory.json` corrupt | Treated as Off with a Warning; the panel shows the error and offers to turn on again, which rewrites the file |
| PM-E7 | The Human renames a Room | Nothing stored holds the name; the next Brief uses the new label |
| PM-E8 | The Human archives a Room | Consolidation pauses; delivery is untouched (rules.md: archiving never reaches delivery). Unarchiving resumes and catches up |
| PM-E9 | The Human deletes a Room | `ChatService.DeleteRoomAsync` deletes `room-memory/{roomId}/` after the Transcript, and drops a waiting Memory Proposal. A running Consolidation is cancelled |
| PM-E10 | A Persona is renamed | `PersonaRenameCascade` updates `steward` and moves `last-brief/{Name}.md`, **above** the "no Agent row" early return (rules.md). Digest text keeps the old Name: it is history |
| PM-E11 | A Persona is removed | Steward cleared, `paused: "noSteward"`; its `last-brief` file deleted |
| PM-E12 | Restart, or an edit that forgets every Room Session | Unchanged (`ForgetAll`). Each Room's next Turn opens fresh, with the Brief where Room Memory is on |
| PM-E13 | The Steward's Adapter, Model or Effort changes | The consolidator host restarts on its next run with the new profile, unless `Consolidator:AdapterId` pins one. The Digest is untouched |
| PM-E14 | `SessionPerRoom: false` on the Persona's profile | A shared session has no Room (`RoomSession.RoomId` is `null`), and the Transcript read is skipped for it today (`RoomSession.cs`, the `this.RoomId is not null` guard). So it gets no Brief and no Digest update block, and Rotation is off for it. Such a Persona still has `recall`, and its Rooms still consolidate for their other Agents. The turn-on dialog says so when a Member runs on a shared session |
| PM-E15 | Many Rooms with Room Memory on | One run at a time, oldest-due first; a Room waiting more than a day is shown as *behind* in the panel |
| PM-E16 | Turning on a very large Room | Backfill reads only `BackfillDays`, within the daily cap; older history is reached with `recall`. The estimate says so |
| PM-E17 | A Message is edited or deleted in the Transcript by hand | A citation that no longer resolves is dropped at the next run (PM-R14) |
| PM-E18 | Two Agents promote the same fact | The second sees the first's Proposal pending and is refused with `A Memory Proposal from {name} is already waiting in this Room.` |
| PM-E19 | An Agent is invited to a Room with Room Memory on | Its first Turn there carries the Brief, so it learns the Room's history in summary (RS E-11) |
| PM-E20 | The Human edits a Digest while a run is writing it | Last write wins (ADR-0029); the hash check means the next run treats the Human's save as an edit and leaves it |
| PM-E21 | The app restarts with a Memory Proposal waiting | The Proposal is lost, as a Teammate Proposal is today: it lives in memory only. Nothing was written, so nothing is half-done. A `self` Promotion is unaffected (it never waits). A Consolidation candidate is proposed again on a later run if the Transcript still supports it; an Agent's `remember` is not, and the Agent is not told. Accepted for v1; a persisted store is a follow-up if lost Proposals are noticed |

---

## 11. Phasing

Each phase ships on its own and leaves the app coherent if the next never comes.

| Phase | Ships | Acceptance criteria |
| --- | --- | --- |
| **1. Catch-up depth in Settings** (roadmap item 26's first step) | `session-settings.json`, `ISessionSettings`, Settings › Memory with one field | PM-R1, PM-R2, PM-R4 to PM-R6 pass. A changed value reaches the next first Turn, fresh or resumed, with no restart. The existing goldens are unchanged at the default. The Engineering Guide row for the key says it can be overridden in Settings |
| **2a. Digest and Brief, turned on by hand** | `RoomMemoryStore`, `RoomMemoryConsolidator`, the consolidator host (needs Q-8), the Brief on `TranscriptTail`, `DigestVersion` on `RoomSessionEntry`, the indicator, the panel, the turn-on dialog with its reason but no estimate, Steward Spend and the run cap | Every phase-2 requirement except those marked 2b passes. A Room with Room Memory off is byte-identical to phase 1. A forced Consolidation failure leaves the Room answering. A resumed session gets the gap and a Digest update, not the whole Brief. Manual test: a Room turned on, a session forgotten with Restart, and the next answer refers to a decision older than 20 Messages |
| **2b. Cost and suggestion** | The suggestion strip and its snooze, the estimate in the turn-on dialog, the money cap, the per-Room `room_memory_spend` table and its figure in the panel | PM-R44, PM-R58, and the 2b halves of PM-R55 and PM-R56 pass. Until 2b ships, Room Memory is reachable only from the Room menu, and the run cap alone bounds cost |
| **3. Promotion, `recall` and `remember`** | Both App Tools, Memory Proposal card, `self` Promotion, recalled snippets in the Brief, *Don't remember* | PM-R7, PM-R17 to PM-R28 pass. `recall` never returns a Room the caller is not in. A shared-scope fact is written only on Approve. The `toolDescriptions.txt` and `getHelp.txt` goldens change once, for the two tools only |
| **4. Rotation** | The Turn-end check, `EnsureCoveredAsync`, the view's last-rotation line | PM-R35 to PM-R39 pass. A rotated session's first prompt equals the Brief golden. No Rotation while a Question waits. Off by default |

Roll-ups (week and era) may follow 2a's day files as a second delivery inside 2a, provided PM-R9
is met before 2a is called done. 2a is useful without 2b: the Human turns Room Memory on knowingly,
and the run cap bounds what it can cost.

---

## 12. Testing

Test-first, per [agents/Testing.md](../agents/Testing.md): every test has a `///` summary, user-facing
text is asserted exactly, collections whole, and a test that passes on arrival gets a mutation.
Fixtures: `TempDataDir`, `ManualTimeProvider` or `FiringTimeProvider`, `FakePromptSource`,
`FakeAgentHostFactory` and `FakeAgentSession`.

### 12.1 What must be pinned

| Test class (proposed) | Pins |
| --- | --- |
| `SessionSettingsStoreTests` | Absent file means the config default; a save writes overrides only; out-of-range refused with its text; hand edit picked up |
| `TranscriptCatchUpDepthTests` | The first Turn reads `Max` from the store; a change applies to the next fresh **and** resumed first Turn; no `ForgetAll`, no restart |
| `RoomMemoryOffTests` | **The default-off proof:** for a Room with no `room-memory.json`, the system prompt and every Turn prompt equal today's goldens (`turnPromptTranscript.txt`, `turnPromptTranscriptResumed.txt`, `systemPrompt.roomSessions.txt`); `WantBrief` answers no Brief; the fake host records zero consolidator sessions |
| `BriefBuilderTests` + goldens `turnPromptBrief.txt`, `turnPromptBriefStale.txt`, `turnPromptDigestUpdate.txt` | Section order; trim order (day files go before week files, week files before era files, the newest era never) and the floor; ends before the trigger; deterministic; a resumed session gets the gap and a Digest update block, never the whole Brief; a shared session (`RoomId` null) gets neither; a Message at 23:59 local lands in that local day's file |
| `RoomMemoryStoreTests` | Atomic write; corrupt file is Off with a Warning; hash detects a Human edit; rename and remove |
| `RoomMemoryConsolidatorTests` | Each trigger; one run per Room for several Agents; valid output writes and advances `consolidatedThrough` **after** the writes; malformed output writes nothing; unknown citations dropped; a Human-edited file kept; caps; Off mid-run writes nothing |
| `ConsolidationFailureTests` | **A failing consolidator leaves the Room working:** three failures pause it, the next Turn still runs and carries the stale-marked Brief, and no health state changes |
| `RotationTests` | **Rotation seeding:** after the trigger, the next prompt is the Brief golden and the store entry is gone; never mid-Turn; never with a Question waiting; deferred when coverage fails; not a failure |
| `ProtocolJsonTests` (extended) | Both Envelopes' literal JSON with and without the new optional field; `ProtocolVersion.Current` still 3 |
| `RecallToolTests` | No file is written by a call (ADR-0051); membership scoping; `messageIds` returns full text; exact refusal texts; a Message posted a moment ago is found; `Results may be incomplete.` past `MaxRoomsScanned`; a deleted Room is never read |
| `RememberToolTests`, `MemoryProposalServiceTests` | `self` writes one file with provenance; shared scope writes nothing before Approve; outcome Message text and Mention; one Proposal per Room |
| `PersonaRenameCascadeTests` (extended) | The Room Memory call sits above the early return; a test fails when it moves |
| bUnit: indicator, panel, turn-on dialog, suggestion strip, card | Exact copy; the reason is required; Turn-off confirmation text; the card's audience line |

### 12.2 Manual tests

A page `engineering/manual-tests/room-memory.md` is written with phase 2, listed in
[docs/Index.md](Index.md) in the same change. Its outline:

1. A stock install: no Room shows `Memory: On`; the Chief of Staff's Room is Off.
2. Set the depth to 60 in Settings; Restart a Teammate; its next answer refers to a Message about
   50 back.
3. Turn Room Memory on in a long two-member Room; check the estimate, then the first Digest file.
4. Restart the Teammate; ask about a decision from the first week; the answer cites it.
5. Edit a Digest line in an editor; the next Brief carries the edit, and the next run keeps it.
6. Force the cap to 1; post until a second run is due; the Room says Consolidation paused.
7. Turn it off; no further runs; the files remain; delete them from the panel.
8. Phase 3: `recall` a Message by id from the Digest; propose a Team fact and decline it.
9. Phase 4: with Rotation on and `RotateAfterTurns` at 3, the fourth Turn opens a fresh session and
   still answers in context.

---

## 13. Open questions and ADR candidates

### 13.1 Open questions

| # | Question | Blocking | Leaning |
| --- | --- | --- | --- |
| Q-1 | Catch-up depth: one global value, or per Persona too (a Chief of Staff wants more)? | Phase 1 | Global first; a per-Persona override later, stored outside the `Persona` record so it never restarts a session |
| Q-2 | Message count or token budget for catch-up depth and the Brief? | No | Count for the Settings field, tokens for the Brief, as specified |
| Q-3 | Embeddings for recall? | No | Not in v1. Revisit when lexical recall measurably misses; it needs an embedding model every Adapter can reach |
| Q-4 | Digest retention and decay: keep `rolled/` children forever? Let eras expire? | No | Keep forever in v1; files are small and recallable |
| Q-5 | Multi-human Rooms | No | There is one Human per installation today; if that changes, who may turn Room Memory on and approve Proposals must be decided first |
| Q-6 | Privacy of a two-member Room: should its Digest be recallable by the same Agent from another Room, and should Promotion from it into a shared scope carry an extra warning? | Phase 3 | Recallable only by its Members (PM-R24); the card names the source Room |
| Q-7 | Should Consolidation read tool calls from `logs/tool-calls/<Name>/` as well as Messages? | No | Not in v1; the log is per Persona and clipped at 200 characters |
| Q-8 | How is a consolidator host started? `IAgentHostFactory.StartAsync` needs a `Persona` and an Agent id | **Phase 2** | A second, tool-less entry point on the factory, or a synthetic Persona with no App Tools. Needs the ACP effort's view where it touches `Huddle.Acp` |
| Q-9 | Does Consolidation Spend belong under the Steward's Persona, or under a separate line on the Teammates page? | **Phase 2** | The Steward, as specified, plus the per-Room table; confirm with the Human |
| Q-10 | Does adding `recall` and `remember` to every Agent's tool roster conflict with PM-R2's "nothing extra when Off"? | **Phase 3** | No conflict as PM-R2 is now worded: `recall` is part of the free Baseline (no background cost, no stored state), so it changes the tool list and `get_help` once and no prompt. `Recall:Enabled` is the switch. Confirm with the Human |
| Q-11 | Is re-reading every member Room's Transcript per `recall` call fast enough for a months-old Room? | No | Yes at Huddle's sizes (ADR-0051's own reasoning); GS-M3's timing check covers the same read. If it fails, an index comes by a new ADR that supersedes ADR-0051, never grown in place |
| Q-12 | Can an Agent's direct file-tool writes to Team Memory be brought under the same approval? | No | Not enforceable while the Work Dir is not a jail (rules.md); left as ADR-0032 has it |
| Q-13 | Does Rotation measurably reduce the rut? | No | Unknown; the manual test records it before Rotation leaves its default of off |

### 13.2 ADR candidates

This spec would produce these decisions. None is written yet.

1. **Room Memory is a per-Room setting, Off by default, turned on only by the Human with a reason.**
2. **A Room keeps a cited Digest and Loose ends, and no facts; facts are promoted to their existing
   owners, and a shared scope needs the Human's approval.** Amends ADR-0032's account of who
   writes Team Memory, and language.md's **Memory** ("written with its own tools") for Consolidation's
   `self` Promotions.
3. **Room Memory lives in files under `{DataDir}/room-memory/`, not in `team.db`.**
4. **The Brief rides on `ReadTranscript` and `TranscriptTail` as optional fields; the protocol
   version stays 3.** Extends ADR-0024's Transcript Catch-up.
5. **Consolidation runs on a tool-less consolidator host and its Spend is counted under a Steward.**
6. **Recall keeps no index; it re-reads per call.** Not a new decision so much as ADR-0051 applied to
   an App Tool; an ADR is needed only if recall's timing ever forces an index.
7. **The app may rotate a healthy Room Session on purpose.** Amends ADR-0024's "resumed when needed
   again".

---

## 14. Choosing whether to turn it on

This section is written for the Human and is meant to move into the
[User Guide](Huddle.UserGuide.md) when phase 2 ships.

Every Room already keeps everything that was said. Room Memory adds a running summary that a
Teammate reads whenever it starts afresh in that Room. It is off until you turn it on, and you can
turn it off with one click.

### Answer three questions first

1. **Will you come back to this Room next month?**
2. **Would a newcomer need the history to be useful here?**
3. **Are you comfortable with a summary of this Room being stored and shown to the Teammates in
   it?**

Three yeses: turn it on, and write the reason down when asked. Any no: leave it off.

### Good reasons to turn it on

- **An ongoing relationship with one Teammate**, such as your Chief of Staff or a coach you talk to
  most days.
- **Decisions that must stay consistent** over a long-running Project Room.
- **Re-explaining is costly:** you keep repeating the background.
- **The Room will live for weeks** or grow past a few hundred Messages.
- **You notice a rut:** the same suggestions, the same framings, Turn after Turn.

### Good reasons to leave it off

- **A summary can be wrong, and it persists.** A Teammate may treat a lossy line as fact.
- **It can deepen a rut** by anchoring every fresh start to the old framing.
- **It costs money** for every Room it is on, including Rooms you no longer visit.
- **Extra text distracts** a Teammate from a short task.
- **It keeps things you never decided to keep,** such as a remark made in passing.
- **Promoted facts reach every member** of a Team or Project once you approve them.
- **It is harder to tell what a Teammate actually saw.** The panel shows the last Brief, but it is
  one more thing to check.
- **A clean slate is a feature.** A quick Room should forget.

### What turning it off does

Consolidation stops at once. The summaries already written stay, and you can read or delete them in
**What the Agents remember**. Your Transcript is never touched, and facts already promoted stay
where they were promoted to.
