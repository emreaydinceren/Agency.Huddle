# AGENTS.md changes

Prove, against the real Claude Adapter, that a running Agent follows an edit to an `AGENTS.md` at its next Turn in each Room: that a new Team rule and a new Huddle-wide rule both arrive in two Rooms, that a typo fix is skipped and the Feed lists it, that the notice's wording is followed rather than argued with, that `@Nova /compact` makes the next Turn hear the notices again, that with the Helper off the comparison arrives, that a real restart and resume still tells what was missed, and that saving in a loop makes few Helper calls. It also settles two questions the design does not depend on: whether a cold resume honours the changed prompt, and what a Turn that waits on the Helper does to the Agent's other Room. The automated suite proves the parts with a fake Helper process and a scripted clock; this page proves what only a real model, a real restart and a browser show. **Written 2026-11-04; run 2026-11-04 to 05 by an agent (Haiku 5.5 at low, the real Claude Adapter, a separate data folder, branch `feat/agentsmd-changes`): all pass but one open clause in test 11. A person should still eyeball the card in test 01, the Feed item in test 04, and the Stop control in test 11, including a Stop just after the prompt was sent.**

**11 tests** · 1 free, 10 paid 💰 · about 3.1 hours. Paid steps are marked **Paid 💰** in each test: an Agent's Turn on Haiku costs a fraction of a cent, and the Helper's call is one more.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below. The design is [the AGENTS.md Changes spec](../../Huddle.AgentsMdChanges-Specifications.md) and [ADR-0076](../../adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md). Its Appendix A lists what this page must cover. [ZERO-20](second-brain-zero.md) checks only a new session after **Start afresh**; this page covers running sessions.

| Test | What it settles | Cost |
| --- | --- | --- |
| AGENTSMDCHANGES-01 | Setup: the card | Free |
| AGENTSMDCHANGES-02 | A Team rule reaches a running session in two Rooms | Paid 💰 |
| AGENTSMDCHANGES-03 | A Huddle-wide rule reaches it too | Paid 💰 |
| AGENTSMDCHANGES-04 | A typo is skipped and the Feed lists it | Paid 💰 |
| AGENTSMDCHANGES-05 | The notice's wording is followed | Paid 💰 |
| AGENTSMDCHANGES-06 | `/compact` makes the Agent hear the notices again | Paid 💰 |
| AGENTSMDCHANGES-07 | Helper off: the comparison arrives | Paid 💰 |
| AGENTSMDCHANGES-08 | A real restart, then resume | Paid 💰 |
| AGENTSMDCHANGES-09 | The cold-resume question | Paid 💰 |
| AGENTSMDCHANGES-10 | A save loop and the card's call count | Paid 💰 |
| AGENTSMDCHANGES-11 | A waiting Turn holds the Agent's other Room | Paid 💰 |

## Setup

Run [`P-BUILD`](common.md#p-build), then launch with [`P-LAUNCH-PAID`](common.md#p-launch-paid-). Use the **real** Claude Adapter, Haiku at `low` as [§0.3](../manual-tests.md#03-the-model-and-effort-convention) says. This area adds:

1. Make a Team `Research` with `Ada` as its manager and `Leo` and `Miriam` as members, with [`P-NEW-PERSONA`](common.md#p-new-persona). Huddle has no Team-manager control, so give Ada the title `Research Manager`. Give `Leo` two Rooms, **Room A** and **Room B**, each with a first Turn already done, so that both have an open session. A Direct Room reopens, so Room C in test 05 needs a second member (Ada).
   Create `<data>\Teams\Research\AGENTS.md` with one line and let it settle **before** the tests: an edit of a file that did not exist settles free with no Helper call, by design, so test 02 would count 0.
   Keep each test's rule separate, or remove the old ones. Rules that conflict confuse the Agent ("Endings conflict... Which should I use?").
   The Helper judges a file at most once in 5 minutes. A 90-second wait only holds when the file's last judgement was over 5 minutes ago, so add that wait to each paid test that edits the same file again.
2. `<data>` is the data folder of the lane. The files are `<data>\Teams\AGENTS.md` (Huddle-wide) and `<data>\Teams\Research\AGENTS.md` (the Team). Edit them in any text editor and save. Do not restart Leo between steps unless a step says so.
3. The watcher checks the files every 10 seconds, and a Turn compares at its start. Wait 15 seconds after a save before reading a Feed result, unless a step says otherwise.
4. The Helper's card is in **Settings › Helpers**, named **AGENTS.md Changes**. Its Daily cap is 20 calls.
5. To see what an Agent knows, ask it. The Agent's own words are the oracle here: [`O-TRANSCRIPT`](common.md#o-transcript). Write down the Helper card's `calls today` before each test that spends a Helper call.

## Tests

### AGENTSMDCHANGES-01 — The Helpers tab shows the AGENTS.md Changes card

**Free** · about 8 min

*Proves the card, its defaults and that it sits beside the other Helpers.*

**Before you start**

- Setup is done. No Helper has run today.

**Steps**

1. Open **Settings › Helpers**.
2. Read the **AGENTS.md Changes** card from top to bottom.
3. Press **Edit the prompt**.

**Pass if — all of these**

- The tab shows three cards: Needs Human, Team Memory and AGENTS.md Changes.
- The AGENTS.md Changes card shows the chip `On`, the **On** switch, **Model** Haiku, **Effort** low, **Daily cap** `20` and `0 of 20 calls today`.
- At step 3 Prompts opens with the `Helpers` group and the AGENTS.md Changes system prompt.

### AGENTSMDCHANGES-02 — A running session follows a new Team rule in two Rooms

**Paid 💰** · about 20 min · one Helper call and four Turns

*Proves O-1, O-4: the next Turn in each Room hears the change, and the Helper runs once.*

**Before you start**

- Setup is done. Leo's two sessions are open and idle. The card reads `0 of 20 calls today`.

**Steps**

1. Add this line to `<data>\Teams\Research\AGENTS.md` and save: `Always end your reply with the words "Noted, Research."`.
2. Wait 90 seconds. Do not message Leo yet.
3. In Room A, send `@Leo what is 2 + 2?`. Read the reply.
4. In Room B, send `@Leo name a colour.` Read the reply.
5. In Room A, send `@Leo name an animal.` Read the reply.
6. Read the card.

**Pass if — all of these**

- The reply at step 3 ends with `Noted, Research.`.
- The reply at step 4 ends with `Noted, Research.`: the second Room heard it too.
- The reply at step 5 still ends with it, and Leo says nothing about the file changing.
- The card reads `1 of 20 calls today`: one call for two Rooms.

**Fail if — any of these**

- A Room never follows the rule -> the notice did not reach it.
- The card counts 2 or more -> the Helper ran per Room.

### AGENTSMDCHANGES-03 — A running session follows a new Huddle-wide rule

**Paid 💰** · about 15 min · one Helper call and two Turns

*Proves a Huddle-wide edit reaches a running session. ADR-0070 said it applied only from the next session.*

**Before you start**

- AGENTSMDCHANGES-02 passed. Leo's sessions are still the same ones.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. Add this line to `<data>\Teams\AGENTS.md` and save: `Start every reply with the word "Understood."`.
2. Wait 90 seconds.
3. In Room A, send `@Leo what is 3 + 3?`.
4. In Room B, send `@Leo name a fruit.`

**Pass if — all of these**

- Both replies start with `Understood.`.
- Both replies still end with `Noted, Research.`: the Team rule was not dropped.

### AGENTSMDCHANGES-04 — A typo fix is skipped and the Feed lists it

**Paid 💰** · about 15 min · one Helper call and one Turn

*Proves O-5, O-9: a small edit the Helper calls not meaningful is skipped, and the Feed says so.*

**Before you start**

- AGENTSMDCHANGES-02 passed. Note the Feed's count of Helpers items.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. In `<data>\Teams\Research\AGENTS.md`, change one word's spelling only, such as "recieve" to "receive" in a sentence you added. Save.
2. Wait 90 seconds.
3. Open the **Feed**, Category **Helpers**. Read the newest item.
4. In Room A, send `@Leo since your last reply, were you told of any change to your rules? Answer yes or no, then say what.` The old wording mixed in earlier rules.

**Pass if — all of these**

- The Feed holds an item that reads `AGENTS.md Changes didn't tell Agents about` followed by the file's path and a reason.
- Leo answers that nothing changed, or that it was not told of a change.

**Fail if — any of these**

- No Feed item appears and Leo was not told -> a silent skip.
- Leo describes the typo as a new rule -> the skip was overruled or never judged. Note the card's count.

### AGENTSMDCHANGES-05 — The notice's wording is followed

**Paid 💰** · about 20 min · one Helper call and three Turns

*Settles the wording question. Claude Code's hook documentation recommends factual phrasing over commands, which can trigger prompt-injection defences. This test finds out whether an Agent pushes back on "applies from now on".*

**Before you start**

- Setup is done. Use a new Room C with Leo, so that the session is fresh and has its own history.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. In Room C, send `@Leo name a country.` and read the reply.
2. Add this line to `<data>\Teams\Research\AGENTS.md` and save: `Always end your reply with the words "Wording check."`.
3. Wait 90 seconds.
4. In Room C, send `@Leo name a river.` Read the whole reply, including anything before the answer.
5. Repeat steps 2 to 4 twice more, with `"Wording check two."` and `"Wording check three."`.

**Pass if — all of these**

- Each of the three replies ends with the new words, and none of them says the rule is a suspicious instruction, an injection, or something it will not follow.

**Fail if — any of these**

- Leo says it will not follow, calls the note untrusted, or asks the Human to confirm -> the Agent pushes back. Open **Settings › Prompts**, edit `turn.agentsMdHeader` to `This Team's rules now say:`, save, and repeat the test. Write both results in the run log and tell the owner.

### AGENTSMDCHANGES-06 — After /compact the next Turn hears the notices again

**Paid 💰** · about 20 min · one `/compact` and two Turns

*Proves a completed `/compact` makes the Agent hear every notice since its session began.*

**Before you start**

- AGENTSMDCHANGES-02 and AGENTSMDCHANGES-03 passed, so Leo's Room A has heard two changes.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. In Room A, send `@Leo /compact` and wait for the compaction to finish.
2. Send `@Leo name a planet.` Read the reply.
3. Send `@Leo name a moon.` Read the reply.
4. Ask `@Leo which rules about how you start and end a reply are you following now?`.

**Pass if — all of these**

- The replies at steps 2 and 3 start with `Understood.` and end with `Noted, Research.`.
- At step 4 Leo names both rules.
- Leo says nothing about the file changing at step 3: the notices were told once, at step 2, not again.

### AGENTSMDCHANGES-07 — With the Helper off the comparison arrives

**Paid 💰** · about 15 min · two Turns

*Proves O-6: with the Helper off, the Agent still hears, with the changed lines instead of a summary.*

**Before you start**

- AGENTSMDCHANGES-02 passed.

**Steps**

1. In **Settings › Helpers**, switch the AGENTS.md Changes card **On** off and press **Save**. Note `calls today`.
2. Add this line to `<data>\Teams\Research\AGENTS.md` and save: `Never use the word "very".`.
3. In Room A, send `@Leo describe the weather in one sentence.` straight away. Read the reply.
4. Send `@Leo what exact line did you just read in the Research AGENTS.md about the word "very"?`.
5. Switch the card **On** again and press **Save**.

**Pass if — all of these**

- The reply at step 3 arrives at once, with no long wait, and does not use the word "very".
- At step 4 Leo quotes the line.
- The card's `calls today` did not move.

### AGENTSMDCHANGES-08 — A real restart then resume still tells what was missed

**Paid 💰** · about 20 min · a restart and three Turns

*The fixture cannot do this end to end: it closes an idle session in the same host, not a real restart. This test restarts the app.*

**Before you start**

- Leo's Room A session is open, and its last Turn is done.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. Stop the app with [`P-STOP`](common.md#p-stop). While it is stopped, add this line to `<data>\Teams\Research\AGENTS.md` and save: `Add the word "Restart-ok" to the end of every reply.`.
2. Launch again with the same launch command. Do not message anyone yet.
3. Wait 90 seconds.
4. In Room A, send `@Leo name a tree.`. Read the reply.
5. In Room B, send `@Leo name a flower.`. Read the reply.
6. In Room A, send `@Leo name a bird.`.

**Pass if — all of these**

- The replies at steps 4 and 5 contain `Restart-ok`: an edit made while the app was closed was found at start-up and told once to each Room.
- The reply at step 6 contains it too, and Leo does not mention the file changing.
- The Helper card's `calls today` rose by one, not two.

### AGENTSMDCHANGES-09 — Does a cold resume honour the changed prompt

**Paid 💰** · about 20 min · a restart and two Turns

*Answers the open question in the issue: when the Adapter resumes an old session after a restart, does it also honour the system prompt Huddle sends now? The design does not depend on the answer, because the notice carries the change either way.*

**Before you start**

- Close every Room's session by restarting the app once, with no message sent after the restart.
- Turn the Helper off, as in AGENTSMDCHANGES-07 step 1, so that the answer comes from the prompt and not from a summary.

**Steps**

1. Stop the app. While it is stopped, add this line to `<data>\Teams\Research\AGENTS.md` and save: `Always call the Research Team "the lab".`.
2. Launch again and send nothing.
3. In Room A, send `@Leo what do you call the Research Team?`. Read the reply.
4. Write down whether the reply says "the lab". The UI has no Turn detail control, so check the ledger in `<data>\agents-md-ledger` for the notice and the tool log for a Read in that Turn.
5. Switch the Helper on again.

**Pass if — all of these**

- The reply says "the lab". This test records the result in the run log either way.

**Notes for the run log**

- If the Agent says "the lab" and the Turn detail shows the notice, the answer is the notice, not the system prompt. Record which.
- If the Agent says "the lab" and the Turn detail shows no notice, the Adapter honoured the changed prompt on resume. Record that, and tell the owner.
- If the Agent does not say "the lab", record the reply and the notice you saw.

### AGENTSMDCHANGES-10 — A save loop and the card's call count

**Paid 💰** · about 15 min · one Helper call

*Proves O-10: saving in a loop does not spend a call per save.*

**Before you start**

- The Helper is on. Write down the card's `calls today`.
- This test edits the same file again: the file's last judgement must be over 5 minutes old, or a 90-second wait does not hold.

**Steps**

1. In `<data>\Teams\Research\AGENTS.md`, change a sentence and save. Repeat every 5 seconds for 3 minutes: about 36 saves, each with a different word. Do not message any Agent.
2. During the loop, open **Settings › Helpers** every minute and read `calls today`. Do not leave the page open at a stale count: reload it.
3. Stop saving. Wait 90 seconds. Read `calls today` again.
4. Then send `@Leo what changed in your rules?` in Room A.

**Pass if — all of these**

- During the loop, `calls today` does not move.
- After 90 quiet seconds, it rose by exactly one.
- Leo's answer at step 4 describes the net change, not 36 edits.

**Fail if — any of these**

- The count rose during the loop -> the debounce failed.
- It rose by more than one after the loop -> a call per save leaked through.

### AGENTSMDCHANGES-11 — A waiting Turn holds the Agent's other Room

**Paid 💰** · about 15 min · one Helper call and two Turns

*Proves O-12: a Turn that waits on the Helper holds that Agent's other Room, and a Stop ends the wait.*

**Before you start**

- The Helper is on. Leo is in Room A and Room B, both idle. The Team rule file has not been edited for more than 5 minutes, so spacing allows a call.

**Steps**

1. Add a new rule line to `<data>\Teams\Research\AGENTS.md` and save. Do not wait. Use a larger edit (several lines) for a longer wait.
2. In Room A, send `@Leo name a metal.` and, within 2 seconds, in Room B send `@Leo name a gas.`.
3. Watch both Rooms. Write down how long Room A shows no activity before Leo starts, and when Room B starts.
4. Repeat step 1 with a larger edit. In Room A, send `@Leo name a stone.` and, while the Stop control is shown during the wait, press **Stop** in Room A.
5. Repeat step 1 once more. Send a prompt that takes a few seconds to answer and press **Stop** just after it was sent, then send `@Leo name a bridge.`. **Open clause, not settled:** the run could not land a Stop mid-reply.

**Pass if — all of these**

- In step 3, Room A starts within 30 seconds, and Room B does not start until Room A's Turn is under way.
- At step 4, the Stop ends the wait at once, and Leo's Turn does not start, or starts with the comparison.
- Neither Room is left stuck.
- Open: after a Stop just after the prompt (step 5), the next Turn follows the new rule, or the notice is told again.

**Notes for the run log**

- Record the wait you saw. The measured Helper times are 8 to 13 seconds for a normal edit and 27.5 seconds once at the largest file size.

---

Back to [the manual test script](../manual-tests.md).
