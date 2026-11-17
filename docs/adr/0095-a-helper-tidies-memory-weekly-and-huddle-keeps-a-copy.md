---
status: accepted
date: 2026-11-16
---

# A Helper tidies Memory weekly and Huddle keeps a copy

An Agent's own Memory grows for months. Two files end up on one topic, a fact is replaced by a later file and the old
one stays, and a file written in a hurry has no description that works. The Agent that wrote them does not tidy them,
and the Human does not read them (issue #621).

This record amends [ADR-0071](0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md) in one
place. ADR-0071 says an Agent deletes a memory file only when the Human asks. That rule stays, word for word, for the
Agent. This record adds a second rule for Huddle itself: **Huddle removes a memory file only on a Memory Tidy Verdict,
after copying it to the backup.** The design is in [Huddle.MemoryTidy-Specifications.md](../Huddle.MemoryTidy-Specifications.md);
this record is its slice 4. It builds on [ADR-0064](0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md)
(a Helper judges and the app applies) and on [ADR-0094](0094-an-agent-keeps-a-record-of-what-it-told-the-human.md).

## Decision

**Once a week a Helper reads each Agent's own Memory folder and answers with steps. Huddle checks every step, copies
the file to the Memory backups root first, applies what passes, and writes a list of every change.**

| | Decision |
| --- | --- |
| Who judges | A fourth Helper, `MemoryTidyHelper`. It sees one folder and answers in JSON (spec D-7). It is not an Agent's Turn and posts no Message |
| What it may ask for | Four steps, read once into closed types: merge, rewrite, describe, drop. There is no create and no move (spec D-8). A bad shape is thrown away whole; a step that breaks a rule is refused alone and the others still apply (D-13) |
| What it sees | Memory files and nothing else: no Room, no Transcript, no Task (policy 1). A file marked `tidy: keep`, `reported.md` and `MEMORY.md` are never shown (D-15) |
| Who acts | Huddle, through the memory write rules. A step that breaks a check is refused, never repaired (policy 2) |
| The checks | A step must name only files it was shown, may not grow a file, may not add a date, number, link or address that the files did not hold, and a merge may not lose one. A rewrite that removes more than half a file counts as a drop, and at most `Team:Memory:Tidy:MaxDropsPerFolder` (3) drops are allowed for one folder (D-14, D-27) |
| Descriptions | A new description is written only where the kept file has none that works. There is no rule on a description's words (D-14) |
| The copy | No file is changed or removed before its bytes are in a dated folder under `MemoryBackups/`, a read-only Library root kept 90 days and left out of every search (D-12). A copy that cannot be written stops that step |
| No lost update | Each step carries the version of every file the Helper was shown. A file that changed since is left alone until the next pass (D-16) |
| The record | Every step is written to the pass's journal before it is made. The list (`changes.md`) and one Feed Notification are made from the journal, even after a stop or a crash (D-25, D-17) |
| When | A hosted timer, Sunday 03:00 local by default, over the due-time function the Job scheduler shares (D-9, D-24). **Tidy now** on the Helper's card starts a pass at once |
| Cost | One call per eligible folder per week, on `sonnet` at low Effort (D-21). A folder with fewer than 10 topic files, one that did not change, and one that failed twice unchanged cost nothing |
| Switches | The Helper's card, `tidy: keep` per file, and `MaxDropsPerFolder` at zero (D-15, D-22). There is no per-Agent switch |
| What the Agent hears | Its next Turn lists each file the pass wrote or removed, as File Changes. One sentence in the Memory Prompt says the folder is tidied and a copy is kept (D-18) |

## Why this amends ADR-0071

ADR-0071 put every deletion behind the Human's word because the `memory` tool is used by an Agent that can be wrong or
steered by text it read. The reason holds for the Agent, so the rule stays for the Agent.

The pass is not the Agent. It is a Helper's Verdict, applied by Huddle, under a rule the Human set by leaving Memory
Tidy on. Four things make it a narrower power than the Agent's delete:

- the files it may touch are listed in the input, and only those;
- a copy exists before the change, so the Human can undo it;
- the list names every removal and what each rewrite took out;
- the Human can turn it off, pause it, or protect a file.

So the new sentence is an addition: *Huddle removes a memory file only on a Memory Tidy Verdict, after copying it to
the backup.* The Agent is told this in its Memory Prompt, so a file that has gone does not read as a fault. The Team
Memory Helper still has no delete.

## Why this is not the background summariser Long-Running Rooms rejected

[Long-Running Rooms](../Huddle.LongRunningRooms-Specifications.md) lists "Dreaming: background consolidation into
`MEMORY.md`" as **No**, for benchmarks and injection risk. Memory Tidy shares the idea of consolidating in the
background and differs on the points that reasoning rested on:

| | The rejected summariser | Memory Tidy |
| --- | --- | --- |
| What goes in | Whatever the model had seen: conversation, tool results | Memory files only, of one Agent, one folder at a time |
| What comes out | A new summary written into `MEMORY.md`, which every session reads | No new file is possible. Text can only be merged, shortened or dropped, and the checked tokens cannot grow |
| What guards the text | The model's own judgement | Huddle's checks on every step, and a copy first |
| Where a planted line can go | Into the index every session reads | Only within one folder (the text cannot cross folders or grow); a description is written only where none works |
| How loss is seen | Not at all | The list, and the copy |

The Long-Running Rooms row gets a dated note pointing here. It stays **No** for a summariser of conversation.

## What the checks do and do not stop

They stop a Helper from adding a number, a date, a link or an address, from growing a file, from losing one of those
tokens in a merge, from naming a file it was not shown, and from changing a file that moved. They keep an injected
"add this link" line out of Memory.

They do not stop the following. Each is in [known-limits.md](../engineering/known-limits.md).

- **Meaning changes without a token.** A number written as a word, a month name, a person's name, and a "not" or "never"
  that reverses a sentence are not checked. The list and the copy are how the Human catches these.
- **Text moves inside a folder.** A merge can carry text from one file into another of the same folder.
- **A description can come from the body.** Where a file has no working description, text from its body can become one,
  within 200 characters, one line and the token checks.
- **A file can be halved every week.** A rewrite to less than half counts against the drop cap, so each pass may do it
  to up to three files, and nothing stops it happening to the same file again.
- **A restored file can be dropped again** unless the Human adds `tidy: keep`.
- **The Prompt sentence is fixed at session start**, so it can be stale after the Human turns the Helper on or off.

## Rejected

| Alternative | Why not |
| --- | --- |
| The Agent tidies in its own Turn | A Turn has a Room, a Budget and context that can carry planted text (D-7) |
| A weekly Job running a Skill | A Job is an Agent's Turn, and a Job more than two minutes late is skipped, so a sleeping computer never tidies |
| A free-form "new set of files" as the answer | It could add files, and Huddle could not check each change against its source (D-8) |
| Trusting the prompt alone to add nothing | A prompt is not a check (D-14) |
| A rule on the words of a new description | It refused fair descriptions ("Read for…") and did not stop a planted line, whose words are in the file (D-14) |
| The backup inside `memory/` | File Changes would show the Agent the dropped text, and `memory/archive/` means something else (D-12) |
| All or nothing for a Verdict | One poor description would waste a whole paid call (D-13) |
| A Restore button now | A page, a version check and wireframes before anyone has needed it (D-23) |
| Tidying Team and Project Memory | The Team Memory Helper judges each write there (D-20) |

## Consequences

- **A fourth Helper** is registered. It defaults to on, `claude`, `sonnet`, `low`, a daily cap of 20, a grant of 10 and
  a 180-second timeout. It uses the same card as the others, with one extra section.
- **`HelperJudge`** runs one judgement now for the queue and for the pass, so there is one `Team:HelperConcurrency`.
  `IHelperAgent.CachesVerdicts` lets a Helper opt out of the Verdict cache, and `HelperRunReport.CallRan` says whether a
  process started. See [Helpers](../Huddle.Helpers-Specifications.md).
- **New keys:** `Team:Memory:Tidy:MinFiles`, `MaxInputChars`, `MaxActions`, `MaxAnswerChars`, `MaxDropsPerFolder`, and
  `Team:Helpers:MemoryTidy:*`. See [the Engineering Guide](../Huddle.EngineeringGuide.md).
- **A new Feed type**, `MemoryTidied` (`memory-tidied`), a Notification in the Helpers Category that opens the list.
- **New Prompts:** `systemPrompt.memoryTidy`, `helper.memoryTidy.system` and `helper.memoryTidy.folder`; and
  `systemPrompt.memory` gains the optional `{{memoryTidy}}`.
- **The Helper's card** gains an optional section, container `G9a1` in [wireframes.md](../engineering/wireframes.md).
- In [language.md](../engineering/language.md), **Memory Tidy**, **Memory Tidy Helper** and **Memory backup** are added.
- **Team Memory is not tidied.** The Team Memory Helper judges each write there.

## Huddle-wide Memory (slice 5, 2026-11-17)

The pass judges Huddle-wide Memory (`Teams/memory`) after every Agent's own folder, with its descriptions locked: the
Helper is not asked to describe, and a rewrite or merge whose kept file's description is missing or broken is refused,
while one that passes keeps the description it had. The folder is shared by every Agent, so a description is what each
of them reads to choose a file, and a model-written one would change all their Memory at once. Copies go to
`<pass>/Huddle/`. Because every running Agent is told by [ADR-0077](0077-a-running-agent-hears-of-a-huddle-wide-fact-through-a-turn-hook.md)'s
hook that a file was removed ("Removed, no longer true"), a file a merge removed there is recorded for 30 days in
`memory_tidy_merges` (`MemoryTidyMergeStore`), and the hook says `- Now in <into>: <name>` instead; a dropped file is
still announced as removed. The cost is one more call a week at most.
