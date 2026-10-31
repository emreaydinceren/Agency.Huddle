# Long-running Rooms: the experiment

**6 tests** · 2 free · 4 paid 💰 · about 5.5 hours. Written 2026-10-30; **not yet run by a person**.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the procedures this page names. Read [Planning](planning.md) to see where this area sits.

## Purpose

Decide, on a real long-running Room, whether the Teammate keeps what matters across a long Transcript, what a fresh start or `/compact` costs in repeats and recall, and whether the memory flush earns its Turn. The answer is the decision in §9.1 step 6 of the spec, and it settles whether phase 2b is built and whether phase 4 is built. The design is [the Long-running Rooms spec §9.1](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment) and the task is 4.4 of [the project plan](../../Huddle.LongRunningRooms-ProjectPlan.md).

## Setup

1. **Scratch install.** Run a scratch install of the app with its own data directory, launched as [Common procedures](common.md) describes for a normal install. The data directory holds a copy of the Chief of Staff's Room, so the Turns below never change the everyday Room.
2. **Phase 0 V0-1 is not yet run.** V0-1, which keeps Claude Code's own memory out of play, is deferred to user acceptance. Until it passes, note in the Notes column of each result table whether Claude Code's own memory was visible to the Teammate during the test.
3. **The `memory` App Tool is live.** Confirm it before LRR-02: ask the Teammate to save one fact in a Turn, and check that a topic file is written under its Memory. This is how the Room will really run.
4. **Default Catch-up.** Unless a step says otherwise, the Catch-up is at its default, `Acp:TranscriptCatchUpTokens` of 100000 estimated tokens, with no override in Settings › Sessions.
5. **Start afresh, step by step.** Open the Room and click the Teammate's avatar in the Room header. In the popover, click **Start afresh**. The inline confirm asks "Start afresh? {Name} keeps every Message but forgets its working notes for this Room." Click **Start afresh** to confirm, or **Cancel** to stop. The Room then shows the note "{Name} started afresh, as you asked. It reread the last {count} Messages (about {tokens} tokens)." Write down the count and the tokens. Click **See what it reread** and check whether it went past 20 Messages (§9.1 step 3). It can only go past 20 when the Message cap is raised, as LRR-03 does, so record the answer in the Notes column. If the note says "There were no earlier Messages to reread." or "It could not reread the Room." instead, record that text and mark the test Inconclusive.

## Conventions used in the result tables

- **Recall score**: correct answers out of 10. A correct answer scores 1, a wrong or missing one 0.
- **Source of each answer**: `memory` when the Teammate read a memory file, or the fact is in the memory index; `reread` when the answer came from the Catch-up reread of the Transcript; `neither` when it came from neither.
- **First-Turn cost**: the cost shown on the Teammate card after the first Turn that follows a *Start afresh*.
- **Repeats**: a suggestion counts as a repeat when the Teammate has already made the same suggestion in this Room's Transcript.

## Tests

### LRR-01 — Ten recall facts are picked from the Transcript

**Free** · about 20 min

*Fixes the ten questions and their correct answers before any scoring, so the score cannot be chosen after the answers are known.*

**Before you start**

- The Room has more than 20 Messages.

**Steps**

1. Open the Transcript and count back 20 Messages from the newest.
2. Pick 10 facts or decisions from Messages older than those 20.
3. For each, write the question and the correct answer, quoting the answer from its Message.
4. Record the Message id of each in the table below.

**Pass if — all of these**

- The table has 10 rows, each with a question, a quoted answer and a Message id.
- No Message id is among the 20 newest Messages.

| # | Question | Correct answer (quoted) | Message id |
| --- | --- | --- | --- |
| 1 |  |  |  |
| 2 |  |  |  |
| 3 |  |  |  |
| 4 |  |  |  |
| 5 |  |  |  |
| 6 |  |  |  |
| 7 |  |  |  |
| 8 |  |  |  |
| 9 |  |  |  |
| 10 |  |  |  |

### LRR-02 — The default Catch-up after a fresh start is scored

**Paid** 💰 · about 45 min

*Proves the recall score at the default Catch-up, which is the baseline for LRR-03.*

**Before you start**

- LRR-01 is done and the `memory` App Tool is live (Setup, steps 1 to 3).
- This test needs *Start afresh*, done as Setup, step 5 describes.

**Steps**

1. *Start afresh* the Teammate in this Room, at the default Catch-up. Do not use the card's Restart, which forgets its sessions in every other Room too.
2. Ask the ten questions from LRR-01, one per Turn, and note the cost of the first Turn on the Teammate card.
3. Score each answer 1 or 0 against the quoted answer.
4. For each answer, note its source: `memory`, `reread` or `neither`.

**Pass if — all of these**

- All ten answers are scored and each has a source.
- The first-Turn cost is noted from the Teammate card.

| # | Score (1 or 0) | Source (`memory`, `reread`, `neither`) | Notes |
| --- | --- | --- | --- |
| 1 |  |  |  |
| 2 |  |  |  |
| 3 |  |  |  |
| 4 |  |  |  |
| 5 |  |  |  |
| 6 |  |  |  |
| 7 |  |  |  |
| 8 |  |  |  |
| 9 |  |  |  |
| 10 |  |  |  |

Recall score: ___ of 10. First-Turn cost: ___ (from the card). Repeats: not counted here.

### LRR-03 — The 40,000-token Catch-up is scored

**Paid** 💰 · about 45 min

*Proves the budget is the bound that is reached: the Catch-up ends at whichever comes first, 40,000 tokens or 400 Messages (LR-R7). At a cap of 20 Messages neither bound binds, so the Message cap is raised to 400 here.*

**Before you start**

- LRR-02 is done.
- This test needs *Start afresh*, done as Setup, step 5 describes.

**Steps**

1. In Settings › Sessions, set the Catch-up budget to 40,000 tokens and the Message cap to 400.
2. *Start afresh* the Teammate in this Room. Do not use the card's Restart.
3. Ask the same ten questions from LRR-01, one per Turn, and note the cost of the first Turn on the Teammate card.
4. Score each answer and note its source.
5. In the Room note, check whether the reread went past 20 Messages. If it did not, the step measured the default a second time: mark the test Inconclusive and say so in the run log.
6. Set the Catch-up back to the default before LRR-04.

**Pass if — all of these**

- All ten answers are scored and each has a source.
- The Room note says whether the reread went past 20 Messages.

| # | Score (1 or 0) | Source (`memory`, `reread`, `neither`) | Notes |
| --- | --- | --- | --- |
| 1 |  |  |  |
| 2 |  |  |  |
| 3 |  |  |  |
| 4 |  |  |  |
| 5 |  |  |  |
| 6 |  |  |  |
| 7 |  |  |  |
| 8 |  |  |  |
| 9 |  |  |  |
| 10 |  |  |  |

Recall score: ___ of 10. First-Turn cost: ___. Reread went past 20 Messages: yes / no.

### LRR-04 — The rut is counted after three Turns, /compact and a fresh start

**Paid** 💰 · about 90 min

*Proves whether repeats fall after a fresh start, after `/compact`, or not at all. Its result is the rut claim for this Room (Q-6).*

**Before you start**

- A live session in this Room.
- The `/compact` and the live-session counts need no *Start afresh*. The fresh-start count needs it, done as Setup, step 5 describes.

**Steps**

1. In the live session, ask "suggest three things we haven't tried yet". Then do five Turns of other work. Ask again. Do this three times, and count the repeats in each ask.
2. Run `@Teammate /compact keep the decisions and open questions`. Repeat step 1's three asks, with five Turns between them, and count the repeats.
3. *Start afresh* the Teammate (Setup, step 5). Repeat step 1's three asks, with five Turns between them, and count the repeats.

**Pass if — all of these**

- Each condition has three counts.
- Each count follows the definition of a repeat in the Conventions above.

| Condition | Ask 1 repeats | Ask 2 repeats | Ask 3 repeats | Total repeats |
| --- | --- | --- | --- | --- |
| Live session, no compaction |  |  |  |  |
| After `/compact` |  |  |  |  |
| After *Start afresh* |  |  |  |  |

### LRR-05 — The flush is played by hand, with and without the save request

**Paid** 💰 · about 120 min active time, over two occasions a few days apart

*Proves whether the flush earns its Turn, or whether the `memory` tool's own Prompt ("save as you go") already does the job. It replaces the first draft's Q-9.*

**Before you start**

- Both occasions need *Start afresh*, done as Setup, step 5 describes.
- Let new facts accrue for a few days between the two occasions, so the second has something new to recall.
- Choose five questions about decisions from the last week for each occasion, and write their correct answers down before you start.

**Steps**

1. **Occasion A, no warning.** *Start afresh* the Teammate with no warning. Ask the five questions, score each answer and note its source.
2. **Occasion B, with the save request.** A few days after occasion A, first ask the Teammate "save anything worth keeping to memory before I start you afresh". Note the files it saved and that Turn's cost on the Teammate card.
3. Then *Start afresh* the Teammate. Ask five fresh questions about decisions from the last week, score each answer and note its source.
4. Record both scores in the table below.

**Pass if — all of these**

- Both occasions have five scored answers, each with a source.
- Occasion B has the files saved and the cost of its save Turn.

| Occasion | Recall (of 5) | Source of each answer | Files saved | Save Turn cost |
| --- | --- | --- | --- | --- |
| A, no warning |  |  | not applicable |  |
| B, with the save request |  |  |  |  |

### LRR-06 — The decision follows from the numbers

**Free** · about 10 min

*Proves that the decision is read from the recorded numbers, not from how the runs felt.*

**Before you start**

- LRR-02 to LRR-05 are recorded. Any test that is Blocked or Inconclusive says why.

**Steps**

1. Compare the recall at 40,000 tokens (LRR-03) with 7 of 10.
2. Compare the repeats after a fresh start (LRR-04) with the repeats after `/compact`.
3. Find the row of the decision table below that matches, and record it in the Notes column of the tracker and in the run log.
4. If no row matches, record that, with the reason, and do not choose a row by hand.

**Pass if — all of these**

- The Notes column names one row of the decision table, or says that none matched and why.

## Decision table

Read the rows against the numbers from LRR-02 to LRR-05. The rows are §9.1 step 6 of [the spec](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment).

| If | Then |
| --- | --- |
| Recall at 40,000 tokens is 7 of 10 or better, **and** repeats are fewer after a fresh start than after compaction | Phases 0 to 2a meet the goal. Build phase 2b with thresholds from these numbers and the cost of each fresh start, so automatic fresh starts can be turned on where wanted. Do not build phase 4. |
| Repeats after `/compact` are fewer than, or as few as, repeats after a fresh start | The rut does not need a fresh start in this Room. Take the single-session mode (§7.3, Q-13) instead of phase 2b's trigger set, not both. |
| Recall is below 7 of 10, and the missed answers are durable facts the Teammate should have saved | The fix is the Memory Prompt (`systemPrompt.memory`), not this spec. Tune it and repeat the experiment. |
| Recall is below 7 of 10, and the missed answers are conversation (what was being worked on, open questions, the reasoning behind a choice) | Build phase 4, then repeat the experiment with it. |
| There is no difference in repeats | Record it. The rut claim is unproven for this Room (Q-6). |
| The flush scores no better than no flush (LRR-05) | Leave it out of phase 2b, or ship `FlushToMemory` false. Tune the `memory` Prompt if facts were missed, and record why. |

---

Back to [the manual test script](../manual-tests.md).
