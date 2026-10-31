# Long-running Rooms: the experiment

Run the experiment in [the Long-running Rooms spec §9.1](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment) on the Chief of Staff's real Room, and record its result here. It answers one question before anything automatic is built: after a fresh start, does the Chief of Staff still know what the Room decided, from its Memory and a reread of the latest Messages, and is it less stuck in a rut than after `/compact`? The result decides phase 2b, the single-session mode (spec §7.3, Q-13) and phase 4 (spec §9 phase table).

**5 tests** · 0 free · 5 paid 💰 · about 3.3 hours of hands-on time, spread over at least a week. Written 2026-10-31; **not yet run**. Phases 1 and 2a are not built, so this page uses two stand-ins, set out in [Setup](#setup): a hand edit for *Start afresh*, and a Message cap for the token budget.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

These tests deliberately break two habits of the other pages, because they measure the real Room rather than a feature:

- **They run on the Chief of Staff's own Model and Effort, not Haiku at `low`** ([§0.3](../manual-tests.md#03-the-model-and-effort-convention)). A cheaper model would measure a different Teammate. Expect each first Turn to cost dollars rather than cents.
- **They run on a copy of your everyday data folder, never on it.** Asking the questions writes their answers into the Room, so a second round in the same Room would reread them. Each round starts from a fresh copy of the same snapshot, so every round sees the same Room and the same Memory, and your real Room is never touched.

### P-SNAPSHOT

1. Stop your everyday Huddle, so nothing is half-written.
2. Copy its data folder (call it `<live>`) to a dated snapshot that you never launch:

   ```powershell
   $snap = "C:\seeds\huddle\lrr\snap-$(Get-Date -Format yyyy-MM-dd)"
   robocopy <live> $snap /E /NFL /NDL /NJH /NJS
   ```

3. Restart your everyday Huddle if you want it back. The snapshot is fixed from now on.

### P-ROUND

Run this at the start of every round. It gives the round its own copy of the snapshot.

```powershell
$run = "C:\seeds\huddle\lrr\run"
if (Test-Path $run) { Remove-Item -Recurse -Force $run }
robocopy $snap $run /E /NFL /NDL /NJH /NJS
```

`<data>` on this page is `$run`.

### P-FRESH-START

The stand-in for *Start afresh* (spec §6.1, phase 2a): the Chief of Staff forgets its session in **this Room only**. Don't use the card's **Restart**, which forgets its sessions in every Room.

1. With the app stopped, open `<data>\room-sessions\<Name>.json`, where `<Name>` is the Chief of Staff's Name (`Chief of Staff` unless you renamed it).
2. Under `rooms`, delete the one entry whose key is this Room's id, and save. Leave every other entry alone. The Room id is the 32 hex characters after `/rooms/` in the address bar.
3. Launch with [P-LAUNCH-LRR](#p-launch-lrr-).

The next Message in the Room opens a new session, and its first Turn rereads the latest Messages (the Catch-up), exactly as *Start afresh* will. There is no Room note yet, so use the oracles below to see what was reread.

### P-LAUNCH-LRR 💰

Like [`P-LAUNCH-PAID`](common.md#p-launch-paid-) but on `$run`, with the Chief of Staff's own Model, and with an optional Catch-up cap (`$cap`, default 20):

```powershell
Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue
Remove-Item Env:TEAM_E2E -ErrorAction SilentlyContinue
$env:Team__Acp__Enabled     = 'true'
$env:Team__Acp__TokenBudget = '2000000'   # a ceiling, so a long reread is not cut off mid-round
dotnet run --project src/Huddle.App --urls http://localhost:5100 `
  "--Team:DataDir=$run" `
  "--Team:Acp:TranscriptCatchUpMessages=$(if ($cap) { $cap } else { 20 })"
```

Post nothing in any other Room while a round runs, so the Spend on the Chief of Staff's card is this Room's alone.

### Oracles for what a fresh start reread

| Oracle | What it tells you |
| --- | --- |
| **Spend** on the Chief of Staff's card, read right after its first answer | The first Turn's cost. Spend starts at zero at every launch ([TURNDETAIL-02](turn-detail.md#turndetail-02--spend-is-the-adapters-running-total-across-two-turns)), and P-FRESH-START always relaunches |
| `contextUsed` in `<data>\room-sessions\<Name>.json`, this Room's entry, after the first answer | How many tokens the new session holds. A bigger reread shows up here |
| [`O-TOOLCALLS`](common.md#o-toolcalls) for `<Name>` | Whether an answer came from Memory: a `memory` view or a read of a file under `<data>\Agents\<Name>\work\memory\` |
| The `systemPrompt.memory` index | A fact listed in the index at session start counts as "from Memory" even without a read |

### The 40,000-token cap

Phase 1's token budget does not exist yet. Stand in for it with the Message cap that 40,000 tokens buys in this Room, counted the way phase 1 will count (spec §5, plan correction C5: `ceil((SenderName.Length + 2 + Text.Length) / 3)` per Message, newest first):

```powershell
$lines = Get-Content "$snap\rooms\<RoomId>.jsonl"
$sum = 0; $cap = 0
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    $m = $lines[$i] | ConvertFrom-Json
    $sum += [math]::Ceiling(($m.senderName.Length + 2 + $m.text.Length) / 3)
    if ($sum -gt 40000) { break }
    $cap++
}
"Cap for 40,000 tokens: $cap Messages"
```

If `$cap` comes out at 20 or below, this Room's latest Messages are already over 40,000 tokens and LONGROOMS-03 measures nothing new: record that and skip it.

## Tests

### LONGROOMS-01 — Claude Code's own memory stays off for the Chief of Staff

**Paid 💰** · about 10 min

*The part of check V0-1 (spec §4.2) that this experiment depends on. Claude Code keeps an auto memory of its own per Work Dir that survives a fresh start. If it is on, the recall scores below measure Claude Code's memory rather than Huddle's. Huddle sends `autoMemoryEnabled: false` (`DotAcpAgentHostFactory.cs:493`); this proves it holds. Whether repository `CLAUDE.md` files reach a Persona is [INSTRMEM-07](instructions-memory.md#instrmem-07--spike-s-1-whether-claude-code-loads-the-repository-claude-files-into-a-persona), not this test.*

**Before you start**

- P-SNAPSHOT, P-ROUND, P-LAUNCH-LRR. The Chief of Staff is **Online**.
- List `%USERPROFILE%\.claude\projects\`. Claude Code names each folder after a working folder, with every character that is not a letter or digit turned into `-`. Note whether a folder for `<data>\Agents\<Name>\work` exists, and whether it holds `memory\`.

**Steps**

1. In the Chief of Staff's Room, post: `Remember for good: the experiment's code word is "heron".`
2. Wait for the answer. Read `O-TOOLCALLS` for `<Name>`.
3. List the Claude Code project folder for the Work Dir again.

**Pass if — all of these**

- The fact was saved through the `memory` tool or into `<data>\Agents\<Name>\work\memory\`.
- No `memory\` folder appeared in, or gained a file in, Claude Code's project folder for the Work Dir.

**Fail if — any of these**

- Claude Code's own `memory\` folder gained a file: auto memory is on. **Stop:** the experiment cannot run until it is off. Record it in [known-limits.md](../known-limits.md).

### LONGROOMS-02 — Recall after a fresh start at the default Catch-up

**Paid 💰** · about 40 min

*§9.1 steps 1 and 2: the score to beat. The Chief of Staff rereads the default 20 Messages and has its Memory.*

**Before you start**

- LONGROOMS-01 passed.
- From the snapshot's Transcript, pick **10 facts or decisions older than the latest 20 Messages**. Write each one, its Message id and its expected answer in [the recall table](#recall). Mix durable facts (a preference, a standing decision) with conversation (what was being worked on, an open question, why a choice was made). That split decides which way the result points.
- P-ROUND, then P-FRESH-START with no `$cap`.

**Steps**

1. In the Room, post the 10 questions as **one** numbered Message, worded so that no question gives away an answer.
2. When the answer arrives, read the Spend on the card and this Room's `contextUsed`.
3. Read `O-TOOLCALLS`.
4. Score each answer: 1 if correct, 0 if not. For each one, note where it came from: **M** (Memory, read or in the index), **R** (the reread), or **?** (neither, so a guess).
5. Stop the app.

**Pass if — all of these**

- Every row of the recall table's *default* columns is filled in, plus the first Turn's cost and `contextUsed`.

### LONGROOMS-03 — Recall after a fresh start with a 40000-token Catch-up

**Paid 💰** · about 30 min

*§9.1 step 3: the same questions with a longer reread. Phase 1 will bound it by tokens; here the Message cap stands in for that.*

**Before you start**

- LONGROOMS-02 is done. Work out `$cap` with [the 40,000-token cap](#the-40000-token-cap).
- P-ROUND (a fresh copy, so round 1's answers are not in the Room), then P-FRESH-START with `$cap` set.

**Steps**

1. Post the same numbered Message as in LONGROOMS-02.
2. Read the Spend and `contextUsed`, then `O-TOOLCALLS`.
3. Score and mark the source as in LONGROOMS-02.
4. Stop the app.

**Pass if — all of these**

- The *40,000* columns of the recall table are filled in.
- `contextUsed` is clearly above LONGROOMS-02's. If it is not, the larger cap never reached the session: the step has measured the default twice, so find out why before you score it.

### LONGROOMS-04 — Repeats in a live session, after compact and after a fresh start

**Paid 💰** · about 60 min

*§9.1 step 4: the rut score. Does a fresh start, unlike `/compact`, stop the Chief of Staff from repeating itself?*

**Before you start**

- P-ROUND, then P-LAUNCH-LRR **without** P-FRESH-START, so the Chief of Staff resumes its live session.

**Steps**

1. **Live:** post `Suggest three things we haven't tried yet.` Then talk about other things for five Turns, and ask again. Do this three times in all. Count the suggestions that repeat an earlier one, in this round or from before the snapshot.
2. **After compact:** post `@<Name> /compact keep the decisions and open questions`, then run step 1's three asks again and count.
3. **After a fresh start:** stop the app, P-FRESH-START (no `$cap`), then run step 1's three asks again and count.
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

1. **Without the request (A):** P-ROUND, P-FRESH-START (no `$cap`). Post the 5 questions as one Message. Score them. Stop the app.
2. **With the request (B):** P-ROUND, then P-LAUNCH-LRR without a fresh start, and post `Save anything worth keeping to memory before I start you afresh.` Note the files it wrote (`O-TOOLCALLS`) and that Turn's Spend. Stop the app, P-FRESH-START, post the same 5 questions, and score them. Stop the app.
3. Repeat steps 1 and 2 a few days later with a new snapshot and 5 new decisions.

Because both rounds start from the same snapshot, A and B answer the **same** questions about the same Room: a fairer comparison than the spec's fresh questions per round.

**Pass if — all of these**

- Both runs of [the flush table](#flush) are filled in, including what the save Turn wrote and cost.

## Results

Fill these in as you run. Keep the snapshots until the decision is recorded.

### Recall

LONGROOMS-02 and LONGROOMS-03. Snapshot: ______ · Room id: ______ · `$cap` for 40,000 tokens: ______

| # | Fact or decision | Message id | Kind (durable / conversation) | Default: score | Default: source | 40,000: score | 40,000: source |
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
| **Total** |  |  |  | **/10** |  | **/10** |  |
| **First Turn** | Spend | | | $ |  | $ |  |
| | `contextUsed` | | |  |  |  |  |

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

Apply [§9.1 step 6](../../Huddle.LongRunningRooms-Specifications.md#91-the-experiment) to the tables above. Tick every row that holds. Then record the result in the spec's Appendix A and update roadmap row 26.

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
