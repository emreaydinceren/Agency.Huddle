# Long-running Rooms: the experiment

Run the experiment in [the Long-running Rooms spec §9.1](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment) on the Chief of Staff's real Room, and record its result here. It answers one question before anything automatic is built: after a fresh start, does the Chief of Staff still know what the Room decided, from its Memory and a reread of the latest Messages, and is it less stuck in a rut than after `/compact`? The result decides phase 2b, the single-session mode (spec §7.3, Q-13) and phase 4 (spec §9 phase table).

**5 tests** · 0 free · 5 paid 💰 · about 3.3 hours of hands-on time, spread over at least a week. Written 2026-10-31 for phases 1 and 2a as built (*Start afresh*, Settings › Sessions, the Room note). **First run 2026-10-31 on a seeded Room, not the real one:** its provisional result is [spec §9.3](../../Huddle.LongRunningRooms-Specifications.md#93-first-run-provisional); the run on the Chief of Staff's real Room is still to do.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

These tests deliberately break two habits of the other pages, because they measure the real Room rather than a feature:

- **They run on the Chief of Staff's own Model and Effort, not Haiku at `low`** ([§0.3](../manual-tests.md#03-the-model-and-effort-convention)). A cheaper model would measure a different Teammate. Expect each first Turn to cost dollars rather than cents (about $0.50 to $2.50 on Opus in the first run).
- **They run on a copy of your everyday data folder, never on it.** Asking the questions writes their answers into the Room, so a second round in the same Room would reread them. Each round starts from a fresh copy of the same snapshot, so every round sees the same Room, the same Memory and the same live session, and your real Room is never touched.

### Claude Code's session files

The data folder is not the whole snapshot. Claude Code keeps each session's transcript outside it, at `%USERPROFILE%\.claude\projects\<folder>\<sessionId>.jsonl`, where `<folder>` is the Work Dir `<live>\Agents\<Name>\work` with every character that is not a letter or digit turned into `-`. A round copy's `room-sessions\<Name>.json` holds the same session id as `<live>`, so a round that **resumes** the live session (LONGROOMS-01, -04, -05 B) finds that file and appends its Turns to it. Without the save and restore below, each resumed round inherits the earlier rounds' Turns, and your everyday Chief of Staff inherits them all.

So: keep your everyday Huddle **stopped** from P-SNAPSHOT until you have run [P-RESTORE](#p-restore) after the last round of that snapshot.

### P-SNAPSHOT

1. Stop your everyday Huddle, so nothing is half-written. Leave it stopped until P-RESTORE.
2. Copy its data folder (call it `<live>`) and the Chief of Staff's Claude Code session folder to a dated snapshot that you never launch:

   ```powershell
   $snap   = "C:\seeds\huddle\lrr\snap-$(Get-Date -Format yyyy-MM-dd)"
   $claude = "$env:USERPROFILE\.claude\projects\" + ("<live>\Agents\<Name>\work" -replace '[^A-Za-z0-9]', '-')
   robocopy <live> $snap /E /NFL /NDL /NJH /NJS
   robocopy $claude "$snap-claude" /E /NFL /NDL /NJH /NJS
   ```

### P-ROUND

Run this at the start of every round. It gives the round its own copy of the snapshot, and puts the session files back as they were at the snapshot.

```powershell
$run = "C:\seeds\huddle\lrr\run"
if (Test-Path $run) { Remove-Item -Recurse -Force $run }
robocopy $snap $run /E /NFL /NDL /NJH /NJS
robocopy "$snap-claude" $claude /MIR /NFL /NDL /NJH /NJS
```

`<data>` on this page is `$run`.

### P-RESTORE

After the last round of a snapshot, run `robocopy "$snap-claude" $claude /MIR /NFL /NDL /NJH /NJS` once more. Only then start your everyday Huddle again: its Chief of Staff resumes exactly where it was at P-SNAPSHOT.

### P-LAUNCH-LRR 💰

Like [`P-LAUNCH-PAID`](common.md#p-launch-paid-) but on `$run`, with the Chief of Staff's own Model:

```powershell
Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue
Remove-Item Env:TEAM_E2E -ErrorAction SilentlyContinue
$env:Team__Acp__Enabled     = 'true'
$env:Team__Acp__TokenBudget = '2000000'   # a ceiling, so a long reread is not cut off mid-round
dotnet run --project src/Huddle.App --urls http://localhost:5100 `
  "--Team:DataDir=$run" `
  "--Team:Acp:TurnIdleTimeoutSeconds=1200"
```

`TurnIdleTimeoutSeconds` matters for LONGROOMS-04. A `/compact` of a long session streams nothing until it ends: in the first run it took 203 seconds on a 257,000-token session, and at the default 180 seconds Huddle cancelled it.

Post nothing in any other Room while a round runs, so the Spend on the Chief of Staff's card is this Room's alone.

### P-FRESH-START

*Start afresh* (spec §6.1): the Chief of Staff forgets its session in **this Room only**. Don't use the card's **Restart**, which forgets its sessions in every Room.

1. In the Room header, click the Chief of Staff's avatar. In the popover, press **Start afresh**, then confirm **Start afresh** in `Start afresh? <Name> keeps every Message but forgets its working notes for this Room.`
2. Read the Spend on the card now (call it S0).
3. Post the round's Message. The first Turn of the new session rereads the latest Messages (the Catch-up). When it ends, the Room shows `<Name> started afresh, as you asked. It reread the last <n> Messages (about <t> tokens).` Copy `<n>` and `<t>`.

It works in a running app and needs no relaunch, so a fresh start inside a round happens **in place, on the same `$run`**: never P-ROUND between a round's live part and its fresh start.

### Settings › Sessions

The reread is bounded by **Messages a fresh session rereads** (default 20) and **Size of what a fresh session rereads** (default 100,000 tokens, an estimate), whichever binds first. When the size binds, the Chief of Staff's **own** Messages are dropped first, oldest first, keeping its newest 5 (LR-R11). Set them before *Start afresh*; they reach the next fresh session without a restart.

### Oracles

| Oracle | What it tells you |
| --- | --- |
| The Room note's `<n>` and `<t>`, and **See what it reread** beside it | Exactly what the fresh session reread: whether a fact was in the reread is a search of that text |
| **Spend** on the Chief of Staff's card after the first answer, minus S0 | The first Turn's cost |
| `contextUsed` in `<data>\room-sessions\<Name>.json`, this Room's entry, after the first answer | How many tokens the new session holds |
| `<data>\Agents\<Name>\work\memory\` and the `systemPrompt.memory` index | Whether a fact was in Memory. [`O-TOOLCALLS`](common.md#o-toolcalls) shows that the `memory` tool was called but not which file: it logs that tool's arguments as empty, so search the memory files for the fact instead |

## Tests

### LONGROOMS-01 — Claude Code's own memory stays off for the Chief of Staff

**Paid 💰** · about 10 min

*The part of check V0-1 (spec §4.2) that this experiment depends on. Claude Code keeps an auto memory of its own per Work Dir that survives a fresh start. If it is on, the recall scores below measure Claude Code's memory rather than Huddle's. Huddle sends `autoMemoryEnabled: false` (`DotAcpAgentHostFactory.cs:493`); this proves it holds. Whether repository `CLAUDE.md` files reach a Persona is [INSTRMEM-07](instructions-memory.md#instrmem-07--spike-s-1-whether-claude-code-loads-the-repository-claude-files-into-a-persona), not this test.*

**Before you start**

- P-SNAPSHOT, P-ROUND, P-LAUNCH-LRR. The Chief of Staff is **Online**.
- List every folder in `%USERPROFILE%\.claude\projects\` whose name ends in `-Agents-<Name>-work` (spaces in `<Name>` become `-`). A resumed session writes into the **snapshot's** folder, `$claude`, not one for `$run`; a fresh one writes into a folder for `$run`. Note which of them hold `memory\`.

**Steps**

1. In the Chief of Staff's Room, post: `Remember for good: the experiment's code word is "heron".`
2. Wait for the answer. Search `<data>\Agents\<Name>\work\memory\` for `heron`.
3. List the same Claude Code folders again.

**Pass if — all of these**

- `heron` is in a file under `<data>\Agents\<Name>\work\memory\`.
- None of the folders gained a `memory\` folder or a file in one.

**Fail if — any of these**

- A Claude Code `memory\` folder gained a file: auto memory is on. **Stop:** the experiment cannot run until it is off. Record it in [known-limits.md](../known-limits.md).

### LONGROOMS-02 — Recall after a fresh start at the default Catch-up

**Paid 💰** · about 40 min

*§9.1 steps 1 and 2: the score to beat. The Chief of Staff rereads the default 20 Messages and has its Memory. Run it twice: in the first run, identical rounds scored 8/10 and 10/10, so one run cannot place a score against the 7/10 line.*

**Before you start**

- LONGROOMS-01 passed.
- From the snapshot's Transcript, pick **10 facts or decisions older than the latest 20 Messages**. Write each one, its Message id and its expected answer in [the recall table](#recall). Mix durable facts (a preference, a standing decision) with conversation (what was being worked on, an open question, why a choice was made). That split decides which way the result points.
- P-ROUND, P-LAUNCH-LRR. Settings › Sessions at its defaults.

**Steps**

1. P-FRESH-START, posting the 10 questions as **one** numbered Message, worded so that no question gives away an answer.
2. When the answer arrives, read the Room note, the Spend and this Room's `contextUsed`.
3. Score each answer: 1 if correct, 0 if not. For each one, note where it came from: **M** (Memory), **R** (the reread: search **See what it reread**), or **?** (neither, so a guess).
4. Check the memory files for anything the round wrote. A miss can be written back as a false note ("never discussed"): record it.
5. Stop the app. Run steps 1 to 5 again on a new P-ROUND.

**Pass if — all of these**

- The recall table's *default* columns hold both runs, with each first Turn's cost, `contextUsed` and the note's `<n>`.

### LONGROOMS-03 — Recall after a fresh start with a 40000-token Catch-up

**Paid 💰** · about 30 min

*§9.1 step 3: the same questions with a longer reread, bounded by size rather than count.*

**Before you start**

- LONGROOMS-02 is done.
- P-ROUND (a fresh copy, so earlier answers are not in the Room), P-LAUNCH-LRR. In Settings › Sessions set **Messages a fresh session rereads** to `400` and **Size of what a fresh session rereads** to `40000`.

**Steps**

1. P-FRESH-START, posting the same numbered Message as in LONGROOMS-02.
2. Read the Room note, the Spend and `contextUsed`.
3. Score and mark the source as in LONGROOMS-02, and check the memory files the same way.
4. Stop the app. Run steps 1 to 4 again on a new P-ROUND, with the same settings.

**Pass if — all of these**

- The *40,000* columns of the recall table hold both runs.
- The note's `<n>` is above 20. If it is not, the size never bound: this Room's latest 20 Messages already exceed 40,000 tokens, so record that and the default and 40,000 columns measure the same thing.

### LONGROOMS-04 — Repeats in a live session, after compact and after a fresh start

**Paid 💰** · about 60 min

*§9.1 step 4: the rut score. Does a fresh start, unlike `/compact`, stop the Chief of Staff from repeating itself?*

**Before you start**

- P-ROUND, P-LAUNCH-LRR, without a fresh start, so the Chief of Staff resumes its live session. Settings › Sessions at its defaults.

**Steps**

1. **Live:** post `Suggest three things we haven't tried yet.` Then talk about other things for five Turns, and ask again. Do this three times in all. Count the suggestions that repeat an earlier one, in this round or from before the snapshot.
2. **After compact:** post `@<Name> /compact keep the decisions and open questions`. Press Escape to close the Mention picker before Enter, or Enter picks a name instead of sending. Wait for it to end (a few minutes), then run step 1's three asks again and count.
3. **After a fresh start:** in place, on the same running app, P-FRESH-START, posting step 1's first ask. Run the other two asks and count.
4. Stop the app.

**Pass if — all of these**

- [The repeats table](#repeats) has a count for all three rounds, with the suggestions you counted as repeats.

### LONGROOMS-05 — A fresh start with and without a save request first

**Paid 💰** · about 60 min, run twice a few days apart

*§9.1 step 5: the memory flush (spec §6.6) played by hand. Phase 2b would spend a Turn asking the Chief of Staff to save what matters before a fresh start; this shows whether that Turn earns its cost, or whether the `memory` tool's own "save as you go" already does the job.*

**Before you start**

- Your everyday Huddle has been used in the Chief of Staff's Room for a few days since the last snapshot, so new decisions exist. Take a new P-SNAPSHOT.
- Pick **5 decisions from the last week**, all older than the latest 20 Messages, and write them in [the flush table](#flush).

**Steps**

1. **Without the request (A):** P-ROUND, P-LAUNCH-LRR, P-FRESH-START posting the 5 questions as one Message. Score them. Stop the app.
2. **With the request (B):** P-ROUND, P-LAUNCH-LRR, and in the live session post `Save anything worth keeping to memory before I start you afresh.` Note the memory files it changed and that Turn's Spend. Then, in place, P-FRESH-START posting the same 5 questions, and score them. Stop the app.
3. P-RESTORE. Repeat steps 1 and 2 a few days later with a new snapshot and 5 new decisions.

Because both arms start from the same snapshot, A and B answer the **same** questions about the same Room: a fairer comparison than the spec's fresh questions per round.

**Pass if — all of these**

- Both runs of [the flush table](#flush) are filled in, including what the save Turn wrote and cost.

## Results

Fill these in as you run. Keep the snapshots until the decision is recorded.

### Recall

LONGROOMS-02 and LONGROOMS-03. Snapshot: ______ · Room id: ______

| # | Fact or decision | Message id | Kind (durable / conversation) | Default: score, run 1 / 2 | Default: source | 40,000: score, run 1 / 2 | 40,000: source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 |  |  |  |  |  |  |  |
| 2 |  |  |  |  |  |  |  |
| 3 |  |  |  |  |  |  |  |
| 4 |  |  |  |  |  |  |  |
| 5 |  |  |  |  |  |  |  |
| 6 |  |  |  |  |  |  |  |
| 7 |  |  |  |  |  |  |  |
| 8 |  |  |  |  |  |  |  |
| 9 |  |  |  |  |  |  |  |
| 10 |  |  |  |  |  |  |  |
| **Total** |  |  |  | **/10 · /10** |  | **/10 · /10** |  |
| **First Turn** | Spend | | | $ · $ |  | $ · $ |  |
| | `contextUsed` | | |  |  |  |  |
| | Note: Messages, tokens | | |  |  |  |  |
| | False notes written to Memory | | |  |  |  |  |

### Repeats

LONGROOMS-04.

| Round | Repeats out of 9 suggestions | The repeats |
| --- | --- | --- |
| Live session |  |  |
| After `/compact` |  |  |
| After a fresh start |  |  |

### Flush

LONGROOMS-05.

| Run | Snapshot | A: score without the request | B: score with it | B: files saved | B: save Turn's Spend |
| --- | --- | --- | --- | --- | --- |
| 1 |  | /5 | /5 |  | $ |
| 2 |  | /5 | /5 |  | $ |

### Decision

Apply [§9.1 step 6](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment) to the tables above. Tick every row that holds. Then record the result in spec §9.3 and update roadmap row 26.

| Holds? | Finding | Then |
| --- | --- | --- |
|  | Recall at 40,000 is 7/10 or better, **and** fewer repeats after a fresh start than after `/compact` | Build phase 2b, with thresholds from these numbers; no phase 4 |
|  | As few repeats after `/compact` as after a fresh start, or fewer | Take the single-session mode (§7.3, Q-13) instead of phase 2b |
|  | Recall below 7/10, and the misses are durable facts | Tune the Memory Prompt (`systemPrompt.memory`) and run again |
|  | Recall below 7/10, and the misses are conversation | Build phase 4 (the handoff note), then run again |
|  | No difference in repeats | Record that the rut is unproven for this Room (Q-6) |
|  | B scores no better than A | Leave the flush out of phase 2b, or ship `FlushToMemory` false |

**Decided:** ______ on ______.

---

Back to [the manual test script](../manual-tests.md).
