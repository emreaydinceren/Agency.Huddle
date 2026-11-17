# Turn Queue

Prove that a Message an Agent owes is answered once whatever happens to Huddle between its post and the reply: a close, a kill, an Agent still connecting, a Restart; that no reply is ever posted twice; that a waiting `ask_human` card survives a restart; and that the Human can see and call off what waits. Spec: [Huddle.TurnQueue-Specifications.md](../../Huddle.TurnQueue-Specifications.md) (§14 lists the cases as TQ-M1 to TQ-M18; this page carries those built so far). Decision: [ADR-0091](../../adr/0091-a-rooms-waiting-turns-are-kept-in-team-db.md).

**11 tests** · 0 free · 11 paid · about 2.8 hours. Written 2026-11-17 for issues #617 (slices 0 and 1) and #618 (slice 2, TURNQUEUE-06 and -07). The slice 0 and 1 cases were **run once, 2026-11-17, build `a3142a11`**, by an agent acting as the Human on Sonnet: 9 Pass (see the [Run log](run-log.md)); they cost about 0.5 USD. The Follow-up cases join this page with slice 3.

> **Paid, Sonnet minimum.** Every case sends real Turns. Every Agent is Sonnet or better, never Haiku, and the Chief of Staff is set to Sonnet / low before its first Turn. Each case is a few Turns, well under a dollar. Record each result in the [Run log](run-log.md).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names.

| Test | Spec id | What it answers |
| --- | --- | --- |
| TURNQUEUE-01 | TQ-M1 | Does a Turn cut off by a normal close run again once, led by the cut-off note, with no reply twice |
| TURNQUEUE-02 | TQ-M2 | After a kill mid-Turn, does the re-run check its earlier work instead of doing it twice |
| TURNQUEUE-03 | TQ-M3 | Does a kill right after a reply never produce a second reply |
| TURNQUEUE-04 | TQ-M4 | Does a waiting `ask_human` card, and its Needs You row, survive a restart, and does the answer resume the asker |
| TURNQUEUE-05 | TQ-M5 | Does a stopped Turn stay stopped across a restart while another Agent's waiting Mention is answered |
| TURNQUEUE-06 | TQ-M6 | Do quick Human Messages in a one-to-one get one reply that answers them all |
| TURNQUEUE-07 | TQ-M7 | Do quick Mentions in a group Room still get one reply each |
| TURNQUEUE-12 | TQ-M12 | Is a Mention posted while an Agent is still starting answered once it is up, with no restart line |
| TURNQUEUE-13 | TQ-M13 | Does a Restart stop the running Turn and keep the Messages waiting behind it |
| TURNQUEUE-14 | TQ-M14 | Does the age limit and **Don't answer** keep old or refused Messages from coming back |
| TURNQUEUE-18 | TQ-M18 | Is a waiting Mention held while its Room is paused and answered once the Human speaks |

## Setup

Run [`P-BUILD`](common.md#p-build), then start a paid lane as [`P-LAUNCH-PAID`](common.md#p-launch-paid-) describes, with these settings (the 2026-11-17 run used them):

| Setting | Value |
| --- | --- |
| URL | `http://localhost:5617` |
| `Team:DataDir` | A fresh folder, for example `C:\HuddleLane617\data` |
| `Team:PipeName` | A name no other lane uses, for example `huddle-lane617` |
| `Team:Acp:Enabled` | `true` |
| `Team:Acp:AdapterPath` | The `dist/index.js` of the `claude-agent-acp` under `tools/acp`, when the build runs from a checkout or a publish folder |

Remove `ANTHROPIC_*`, `CLAUDE_CODE_*` and `CLAUDECODE` from the environment before the launch, and confirm `claude auth status` says `loggedIn: true`. The cast is **Nova** and **Cleo**, two scratch Agents on Sonnet / low, and **Coach** for TURNQUEUE-04. "Close Huddle" means stopping the process normally (Ctrl+C, or `Stop-Process` without `-Force`); "kill" means `Stop-Process -Force`, as Task Manager's End task does.

**Making an Agent Offline.** TURNQUEUE-14 and -18 need an Agent that is not connected while Huddle runs. An unknown `adapter:` id falls back to the default Adapter, so configure a second Adapter Profile that cannot start and point the Agent at it. Set these before the launch (the first entry repeats the stock Claude profile, which a configured list replaces):

```powershell
$env:Team__Acp__Adapters__0__Id = 'claude'; $env:Team__Acp__Adapters__0__Command = 'node'
$env:Team__Acp__Adapters__0__AdapterPath = '<repo>\tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js'
$env:Team__Acp__Adapters__0__IsolateUserSettings = 'true'; $env:Team__Acp__Adapters__0__SessionPerRoom = 'true'
$env:Team__Acp__Adapters__0__PromptBlocks = 'true'; $env:Team__Acp__Adapters__0__Commands__0 = 'compact'
$env:Team__Acp__Adapters__1__Id = 'broken'; $env:Team__Acp__Adapters__1__Command = 'C:\no-such-adapter.exe'
```

Then `adapter: 'broken'` in the Agent's frontmatter makes it Offline, and removing the line brings it back. "Close Huddle" can be done from a script by sending the process Ctrl+C (`AttachConsole` + `GenerateConsoleCtrlEvent`); a close while a tool runs waits for that tool to return.

**Reading the evidence.** The rows are in `team.db`, table `turn_queue` (`status`, `outcome`, `attempts`, `reply_message_id`); read them with [`O-DB`](common.md#o-db). The prompt each Turn was sent is in the Agent's tool-call and session logs, or in [`O-LOG`](common.md#o-log) with `Team__Acp__TraceWire` set for that case only (the trace holds a bearer token: delete the file afterwards). Count replies in the Room's Transcript file, `<DataDir>\rooms\<roomId>.jsonl`.

## Tests

### TURNQUEUE-01 — A Turn cut off by a normal close runs again once 💰

**Paid** · about 15 min

*Proves U2: a Turn running when Huddle closes runs again on the next start, led by the cut-off note, and no reply appears twice.*

**Before you start**

- Nova Online, in its one-to-one Room. About 0.1 USD.

**Steps**

1. Ask Nova for something long: "Run `ping -n 90 127.0.0.1` in the foreground and then summarise the output." (Claude Code's shell refuses a long foreground `Start-Sleep`, so use `ping`.)
2. While the tool call runs, send a second Message: "Also, what is 2 + 2?"
3. Close Huddle normally. Start it again and open Nova's Room.

**Pass if — all of these**

- The Room shows a Huddle line beginning "Picked up after a restart." naming Nova.
- Nova's first Turn after the start is led by the cut-off note ("Huddle stopped while you were answering the Message below, and this is a second try ...").
- Each of the two Messages gets exactly one reply; no reply appears twice.
- `turn_queue` shows both rows `done` / `replied`, the first with `attempts` 2.

### TURNQUEUE-02 — After a kill, the re-run checks its earlier work 💰

**Paid** · about 15 min

*Proves the cut-off note works on a model: a Turn killed after it wrote a file does not blindly write it again.*

**Before you start**

- Nova Online, in its one-to-one Room, with a Work Dir it can write. About 0.1 USD.

**Steps**

1. Send: "Write `notes/a.md` with one line saying hello, then run `ping -n 60 127.0.0.1` in the foreground, then explain what you did at length."
2. Once `notes/a.md` exists and the ping is running, kill Huddle.
3. Start Huddle and watch Nova's Room.

**Pass if — all of these**

- Nova runs again once and says it found `notes/a.md` already there, or checks it before writing; it does not report writing it a second time.
- Exactly one reply to the Message.

### TURNQUEUE-03 — A kill right after a reply never brings a second reply 💰

**Paid** · about 20 min

*Proves U3: a reply already in the Transcript means nothing runs again.*

**Before you start**

- Nova Online, in its one-to-one Room. About 0.1 USD for five tries.

**Steps**

1. Send: "Reply with the single word ok."
2. The moment the reply appears, kill Huddle.
3. Start Huddle and wait a minute.
4. Repeat steps 1 to 3 at least four more times.

**Pass if — all of these**

- No try produces a second reply to the same Message.
- No try produces a restart line that says something is waiting for Nova.

### TURNQUEUE-04 — A waiting Question survives a restart 💰

**Paid** · about 15 min

*Proves slice 0 (U5): an `ask_human` card and its Needs You row are back after a restart, and the answer resumes the asker.*

**Before you start**

- Coach Online (body: "You are a fitness coach."), in its one-to-one Room. About 0.1 USD.

**Steps**

1. Send: "Before you plan my week, ask me two questions with options using ask_human."
2. When the card is on screen, close Huddle. Start it again.
3. Open Needs You and the Room. Tap an answer on the card.

**Pass if — all of these**

- The card is back in the Room and its Room is listed in Needs You.
- `waiting_questions` held one row while the card waited and holds none after the answer.
- Coach replies using the answer.

### TURNQUEUE-05 — A stopped Turn stays stopped across a restart 💰

**Paid** · about 15 min

*Proves U10 for Stop: what the Human stopped never comes back, while another Agent's waiting Mention is answered.*

**Before you start**

- Nova and Cleo Online, in a group Room with you. About 0.1 USD.

**Steps**

1. Send: "@Nova run `ping -n 60 127.0.0.1` and summarise it. @Cleo say hello."
2. While Nova's Draft streams, press **Stop** on Nova's Draft.
3. Close Huddle before Cleo has replied if you can; otherwise kill it while Cleo's Turn runs. Start Huddle.

**Pass if — all of these**

- Cleo answers once after the start (or had already answered once).
- Nova's stopped Turn does not come back; its row is `done` / `stopped`.

### TURNQUEUE-06 — Quick Messages in a one-to-one get one reply 💰

**Paid** · about 10 min

*Proves U6 and slice 2: the Human's Messages queued while Nova is busy are answered together, in one Turn.*

**Before you start**

- Nova Online, in its one-to-one Room. `Team:Acp:FoldOneToOne` at its default (`true`). About 0.05 USD.

**Steps**

1. Send: "Run `ping -n 30 127.0.0.1` in the foreground and summarise it."
2. While it runs, send three short Messages: "can you also name a fruit", "and a colour", "and a planet".

**Pass if — all of these**

- After the ping summary, Nova posts exactly one reply that answers all three.
- `turn_queue` shows the three rows `done` / `replied`.

### TURNQUEUE-07 — Quick Mentions in a group Room get one reply each 💰

**Paid** · about 10 min

*Proves U7: a group Room never folds; each Mention is its own Turn.*

**Before you start**

- Nova and Cleo in a group Room with you. About 0.05 USD.

**Steps**

1. Send: "@Nova run `ping -n 30 127.0.0.1` in the foreground and summarise it."
2. While it runs, send "@Nova name a fruit", "@Nova name a colour" and "@Nova name a planet" as three Messages.

**Pass if — all of these**

- Nova answers each of the three Mentions in its own reply, three replies after the ping summary.

### TURNQUEUE-12 — A Mention while an Agent is starting is answered, with no line 💰

**Paid** · about 10 min

*Proves U4 and V-1: a Message posted while a runner is still connecting is answered after its Welcome, in order, with no restart line.*

**Before you start**

- Nova and Cleo in a group Room with you. Huddle stopped. About 0.05 USD.

**Steps**

1. Start Huddle and open the group Room at once.
2. Before Nova's tile reads Online, send "@Nova what is the capital of France?"

The window after an app start is short, often shorter than the page's own start. A Persona edit opens the same window (Nova reads `Starting` for a few seconds while it restarts), and the 2026-11-17 run used that: edit Nova's text, and send the Mention while its tile reads `Starting`.

**Pass if — all of these**

- Nova answers once, after it comes Online.
- No restart line is posted.

### TURNQUEUE-13 — A Restart stops the running Turn and keeps what waits 💰

**Paid** · about 15 min

*Proves U11 and D-29: a Persona edit stops that connection's running Turn and the restarted runner answers both waiting Messages.*

**Before you start**

- Nova Online, in its one-to-one Room. About 0.15 USD.

**Steps**

1. Send: "Run `ping -n 90 127.0.0.1` in the foreground and summarise it."
2. While it runs, send "What is 3 + 3?" and "Name a colour."
3. Edit Nova's Persona text (add one sentence) and save, which restarts it.

**Pass if — all of these**

- The running Turn stops; its row is `done` / `stopped`.
- The restarted Nova answers both waiting Messages, once each.
- No restart line is posted (Huddle did not restart).

### TURNQUEUE-14 — The age limit and Don't answer 💰

**Paid** · about 20 min

*Proves U10 for Don't answer and the age rule: an old Message is not picked up and says why, and what the Human refused never comes back.*

**Before you start**

- Nova Online, in its one-to-one Room. Launch with `Team__TurnQueue__ResumeWithin=00:02:00`. About 0.05 USD.

**Steps**

1. Make Nova Offline (Setup, "Making an Agent Offline"). Send Nova one Message. Close Huddle, remove the `adapter:` line, wait three minutes and start Huddle.
2. Make Nova Offline again the same way. Send Nova two Messages, and press **Don't answer** on the waiting notice.
3. Remove the `adapter:` line again (Nova restarts and comes Online), then restart Huddle.

**Pass if — all of these**

- After step 1 the Room reads "Not picked up after a restart: 1 Message for Nova that waited more than 2 minutes." and Nova does not answer it.
- In step 2 the notice reads "Waiting for Nova: 2 Messages" with **Don't answer**; after the press it is gone.
- After step 3 nothing comes back.

### TURNQUEUE-18 — A waiting Mention is held while its Room is paused 💰

**Paid** · about 20 min

*Proves N-2 and D-37: a re-sent Mention that meets a spent Budget is held, shown, and answered once the Human speaks, without pressing Continue.*

**Before you start**

- Nova, Cleo and Kai in a group Room with you. Launch with `Team__AgentMessageBudget=3`. About 0.15 USD.
- The Budget lives in memory and starts at zero when Huddle starts, so a Huddle restart alone never meets a spent Budget. The held case is an Agent that comes back while its Room is paused, which is what the steps build.

**Steps**

1. Make Nova Offline (Setup, "Making an Agent Offline").
2. Send: "@Cleo ask @Nova to name a fruit, then talk with @Kai about fruit until you are stopped." Wait until the Room pauses (the Budget prompt shows).
3. Remove the `adapter:` line from Nova's file. Nova restarts and comes Online.
4. Read the waiting notice. Then type "carry on" in the Room instead of pressing Continue.

**Pass if — all of these**

- While paused, the notice reads "Waiting for Nova: 1 Message, until this Room's Budget is continued" with **Don't answer**.
- After you type, Nova answers the waiting Mention once.
