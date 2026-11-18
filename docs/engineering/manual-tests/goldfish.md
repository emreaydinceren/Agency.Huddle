# Goldfish mode

Prove, in a real browser against real Claude models, that an Agent can hand one Room to a clean copy of itself and get an answer back: the clean session starts on a stock install, sees only the brief and the files it names, is told apart on screen by its stamp and chip, is reached only by Mentions of the Agent in that Room, and always returns the body, whether it finishes, is called back, is stopped, waits for an answer or is cut off by a restart. The automated side is in `GoldfishEndToEndTests` and the Goldfish unit tests; this page checks what only a person and a real model show.

**16 tests** · 0 free · 16 paid · about 4.8 hours. GOLDFISH-01 to 16 are the spec's GF-M1 to GF-M16 ([the spec](../../Huddle.Goldfish-Specifications.md) §14.2), written for issue #627 and first run on 2026-11-17 and 2026-11-18 in four lanes; what those runs found is folded in below.

> **Paid, Sonnet minimum, Claude only.** Every case sends real Turns. Use Sonnet or better for each Agent, never Haiku, at Low effort, and read the models back in `persona_models` before the first case. Goldfish mode is offered only to an Agent on a Claude Adapter that isolates user settings (ADR-0097); the stock profile qualifies. A whole page costs about 1.5 USD. Ask the owner first, and record each result in the [Run log](run-log.md).

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. The design is [the Goldfish spec](../../Huddle.Goldfish-Specifications.md) and [ADR-0097](../../adr/0097-goldfish-mode-a-possession-of-one-agent-in-one-room.md).

| Test | Spec id | Cost | What it answers |
| --- | --- | --- | --- |
| GOLDFISH-01 | GF-M1 | Paid 💰 | Does a call run end to end on a stock install, with the start row, chip, stamp and return |
| GOLDFISH-02 | GF-M2 | Paid 💰 | Is the clean session's prompt free of Room Messages, Memory and `AGENTS.md` |
| GOLDFISH-03 | GF-M3 | Paid 💰 | Does only a Mention of Ruth start the goldfish's next Turn |
| GOLDFISH-04 | GF-M4 | Paid 💰 | Are five questions in a row ordinary Messages, and does the result list them |
| GOLDFISH-05 | GF-M5 | Paid 💰 | Does an unrelated Mention of Ruth in the Room go to the goldfish |
| GOLDFISH-06 | GF-M6 | Paid 💰 | Does **Call Ruth back** end the possession with the partial result |
| GOLDFISH-07 | GF-M7 | Paid 💰 | Does "@Ruth comeback later" end nothing |
| GOLDFISH-08 | GF-M8 | Paid 💰 | Is the warm Ruth still answering in another Room |
| GOLDFISH-09 | GF-M9 | Paid 💰 | Do plain Messages stay context for the warm Ruth after the return |
| GOLDFISH-10 | GF-M10 | Paid 💰 | Does a kill mid-possession end it as Restarted, never resumed |
| GOLDFISH-11 | GF-M11 | Paid 💰 | Does a goldfish that asked and got nothing wait |
| GOLDFISH-12 | GF-M12 | Paid 💰 | Does a goldfish's `reviewed` History line read "Goldfish review by Ruth" |
| GOLDFISH-13 | GF-M13 | Paid 💰 | Does a `spec-driven` Team's spec review Task lead Ruth to call `goldfish` |
| GOLDFISH-14 | GF-M14 | Paid 💰 | Does the Read deny stop `Read` and `cat` of Ruth's Memory |
| GOLDFISH-15 | GF-M15 | Paid 💰 | Can the Human answer a goldfish's Question from its card |
| GOLDFISH-16 | GF-M16 | Paid 💰 | Is `goldfish` refused while `MaxConcurrentTurns` is above 1 |

## Setup

Run [`P-BUILD`](common.md#p-build), then start a paid lane as [`P-LAUNCH-PAID`](common.md#p-launch-paid-) describes, on a fresh data folder and a free port, with **the stock Claude profile**: do not set `Team__Acp__AdapterPath` and add no Adapter Profile. The first runs needed that workaround and failed without it; the fix is part of what GOLDFISH-01 proves.

1. Create **Ruth** (body: *"You review work and report findings."*) and **Quinn** (body: one sentence) with [`P-NEW-PERSONA`](common.md#p-new-persona), both on Sonnet / Low, and set the Chief of Staff to Sonnet / Low before any paid Message. Make a group Room with you, Ruth and Quinn. Ruth's direct Room exists already.
2. Put a short spec at `<data>\Agents\Ruth\work\docs\spec.md`: six numbered points for an invented command-line tool, about 20 lines. "Ruth's Work Dir" below is `<data>\Agents\Ruth\work\`.
3. **Say `goldfish` in the request, and name the tool if Ruth answers plainly.** Ruth decides whether to call the tool. "@Ruth, goldfish docs/spec.md: can it be built as written?" worked on the stock profile; wording that named `goldfish_done` made Ruth call it herself with no possession. If Ruth answers without calling `goldfish`, ask again with "start a goldfish session (the goldfish tool) to ...". No possession row means the model misread the request, not a defect.
4. The Human's words reach the goldfish only through Ruth's brief, and Ruth adds what she thinks it needs (for example "ask Quinn to begin the answer with @Ruth"). Where a case needs the goldfish to wait or to ask no one back, dictate the brief's words in the request.
5. Read the possession table with [`O-DB`](common.md#o-db): `goldfish_possessions` (`state`, `exit`, `exit_by`, `turns`, `spend`, `ended_at`). A possession's state is `working`, `waiting`, `returning` or `ended`. Read the Room with [`O-TRANSCRIPT`](common.md#o-transcript) and Ruth's tool calls with [`O-TOOLCALLS`](common.md#o-toolcalls).
6. "The chip" is the Room header's `C2c`: the fish icon and "Ruth in goldfish mode · n min", or "Ruth (goldfish) is waiting · n min", with **Call Ruth back**. "The stamp" is the author line "Ruth · goldfish" with the fish. "The start row" is "Ruth went into goldfish mode" with the brief; "the return row" is "Ruth is back. ..." with the Turn count and spend.
7. GOLDFISH-02 reads the wire, which nothing but the trace shows. This page is a stated exception to the "never set `Team__Acp__TraceWire`" convention in [Common procedures](common.md#standing-conventions): set `Team__Acp__TraceWire=true` and `Logging__Console__LogLevel__Agency.Huddle.Acp.Wire=Trace` in the lane's own launch script only, in an isolated lane, never a shared one. The trace holds the tool server's bearer token and lands in `O-LOG`: redirect it to a file, scrub every token before you keep or paste any of it (replace each with `SCRUBBED`), and delete the copy afterwards. Close the terminal after the run.
8. Every run ends with `O-ADAPTERS` showing no `claude-agent-acp` left and the port free.

## Tests

### GOLDFISH-01 — A call runs end to end on a stock install 💰

**Paid** 💰 · about 30 min

*Proves the whole shape on the stock Claude profile with no workaround: the Turn ends at once, the start row carries the brief, the chip, ring and fish badge appear, the goldfish's posts are stamped, the return row and the result follow, and every live cue goes while the stamp and rows stay. Spec GF-M1.*

**Before you start**

- The stock lane of Setup, Ruth and Quinn in the group Room, the spec in place.

**Steps**

1. In the group Room, send *"@Ruth, goldfish docs/spec.md: can it be built as written?"*
2. Watch the Room until the return row and Ruth's answer appear. Take a screenshot of the chip and of Ruth's avatar.
3. Read `O-TRANSCRIPT`, then the possession row, then `O-TOOLCALLS` for Ruth.
4. Reload the page and look again.
5. Send a plain *"@Ruth, reply with just pong"*.

**Pass if — all of these**

- Ruth's tool calls hold `mcp__team__goldfish`, and the start row with the brief posted within seconds. No "needs a Claude Adapter" refusal appears in `O-LOG`.
- Ruth's calling Turn ended at once with one line.
- The chip, Ruth's ring and the fish badge appeared during the possession.
- The goldfish's Messages show the stamp "Ruth · goldfish" and the fish, **including its first Message when Ruth's own one-line Message sits just before it** (the header must not be grouped away).
- The return row reads "Ruth is back. Goldfish finished: ..." with a Turn count and spend, the result Message from Huddle holds the brief and the result, and the possession is `ended`, `exit finished`, `ended_at` set.
- After the return, the chip, ring, badge and any Draft row are gone, also after the reload; the stamped Messages and rows stay.
- Ruth's reply to *"reply with just pong"* has no stamp.
- No "Ruth is Degraded" banner appears at any point.

**Fail if — any of these**

- The possession ends `failed` with `turns 0`, or the Room says "Didn't finish: the Adapter stopped" -> the clean session did not open on the stock profile.
- Ruth answered without calling `goldfish` -> see Setup step 3, retry once with the tool named.
- The goldfish's first Message has no header or stamp.
- An empty "Ruth" Draft row with a Stop control stays after the return, or a Degraded banner appears.
- The possession stays `returning` after the Messages posted.
- The return row reads "0 Turns" for a possession that ran one.

### GOLDFISH-02 — The clean session sees only the brief and the files it names 💰

**Paid** 💰 · about 20 min

*Proves the prompt of the clean session holds no Room history, Memory or `AGENTS.md`, and that the deny list is sent. Spec GF-M2. Needs the wire trace (Setup step 7).*

**Before you start**

- The wire trace on, in an isolated lane, with the output redirected to a file.

**Steps**

1. Run the steps of GOLDFISH-01 once.
2. In the trace file, find the `session/new` of the clean session (the one after the goldfish tool call) and the first `session/prompt` after it. Scrub the bearer tokens from any copy you keep.

**Pass if — all of these**

- The clean `session/new` holds Ruth's persona text, the orientation, the tool list and the closing goldfish paragraph ("You are in goldfish mode. The brief in your first Turn is your only context ...").
- It holds no Memory section, no Huddle-wide `AGENTS.md` block, no Room history, no Message of the Room and no library list.
- The first `session/prompt` is only the brief, "Brief for the work in [Room: ...]: ...".
- `settingSources` is `["project","local"]` and `permissions.deny` names `Read(//.../Agents/Ruth/work/memory/**)`, the Team memory and `rooms/**`. The warm sessions' `session/new` carry no `deny`.
- `goldfish` and `goldfish_done` are in its tool list.

**Fail if — any of these**

- A Room Message, Memory text or `AGENTS.md` text is in the clean prompt.
- `permissions.deny` is missing.

Not a failure: the tool list is the warm one plus the two goldfish tools by design, so `memory` and `search` are named; they refuse inside a possession (spec §8.3).

### GOLDFISH-03 — Only a Mention of Ruth starts the goldfish's next Turn 💰

**Paid** 💰 · about 20 min

*Proves a question is an ordinary stamped Message, the goldfish waits, and only a Message that Mentions Ruth reaches it. Spec GF-M3.*

**Before you start**

- The lane of Setup. Dictate the brief in the request: "ask @Quinn which language fits the spec, then end your Turn and wait; report with `goldfish_done` once Quinn's answer Mentions you".

**Steps**

1. Start the possession as in GOLDFISH-01 with that brief. Wait until the chip reads "Ruth (goldfish) is waiting".
2. Note the `turns` of the possession.
3. As Quinn's answer, have Quinn post a Message with no Mention. Then post, as yourself, *"a note: the build number is 42"* with no Mention.
4. Wait 30 seconds. Check `turns`.
5. Now post *"@Ruth Go"*, or have Quinn answer with "@Ruth Go".

**Pass if — all of these**

- The goldfish's question is a Message stamped "Ruth · goldfish", and the possession is `waiting`.
- After step 3, `turns` is unchanged and the state is `waiting`.
- After step 5 the goldfish took a Turn (`turns` rose), finished with `goldfish_done`, and the return row posted.

**Fail if — any of these**

- A Message with no Mention started a goldfish Turn.
- The Mentioning answer did not reach the goldfish, or reached the warm Ruth.

### GOLDFISH-04 — Five questions in a row are ordinary Messages 💰

**Paid** 💰 · about 25 min

*Proves a goldfish can ask repeatedly and the result lists every question with its answer. Spec GF-M4.*

**Steps**

1. Send *"@Ruth, use goldfish mode: read docs/spec.md, then ask @Quinn five short questions one after another, each about one numbered point of the spec. Tell Quinn to begin each answer with "@Ruth". After the fifth answer, report the five questions with their answers."*
2. Let Quinn answer each question as it comes.
3. When the possession ends, read `O-TRANSCRIPT` and the possession row.

**Pass if — all of these**

- All five questions are ordinary stamped Messages in the Room, one per Message, and each answer is a Mention of Ruth.
- The possession is `ended`, `exit finished`, `turns` 5.
- The result lists the five questions with their answers.

**Fail if — any of these**

- A question is missing from the Room or from the result.
- A question or answer is posted as anything other than a Message.

Not a failure: the Room's "n agent replies since you last spoke" pause can be reached by a five-question possession plus Ruth's own Messages (budget 12). The next Human Message lifts it. That is the Room Budget, not goldfish.

### GOLDFISH-05 — An unrelated Mention of Ruth goes to the goldfish 💰

**Paid** 💰 · about 15 min

*Proves the trade-off of spec §6.4: while a Room is possessed, any Mention of Ruth in it goes to the goldfish, and the chip says who would answer. Spec GF-M5.*

**Before you start**

- A waiting goldfish as in GOLDFISH-03 step 1.

**Steps**

1. In that Room, send *"@Ruth what is 2 plus 3?"*
2. Read the Room and `turns`.

**Pass if — all of these**

- The goldfish answered, stamped "Ruth · goldfish", and the chip kept reading "Ruth (goldfish) is waiting".
- `turns` rose and the warm Ruth posted nothing for it.

**Fail if — any of these**

- The warm Ruth answered in the possessed Room.

### GOLDFISH-06 — Call Ruth back ends the possession with the partial result 💰

**Paid** 💰 · about 15 min

*Proves the button ends a waiting possession, the result carries what the goldfish had posted, and the warm Ruth sees it. Spec GF-M6.*

**Before you start**

- A waiting goldfish (GOLDFISH-03's brief). Have it post one reply first: send *"@Ruth post the words 'goldfish here' in the Room"* and wait for it.

**Steps**

1. Press **Call Ruth back** on the chip.
2. Read the Room, the possession row and Ruth's next reply.
3. Reload.

**Pass if — all of these**

- The return row reads "Ruth is back. Called back by You after n minute(s). Partial result above." and the possession is `ended`, `exit calledback`, `exit_by You`.
- The Huddle result Message reads "Partial result: ..." and names what the goldfish posted ("goldfish here"), **not** "No result was posted".
- The warm Ruth's reply uses that result.
- No Draft row, no Degraded banner and no Stop control, also after the reload.

**Fail if — any of these**

- The result says "No result was posted" although the goldfish posted a Message.
- An empty Draft row of Ruth stays.

### GOLDFISH-07 — "@Ruth comeback later" ends nothing 💰

**Paid** 💰 · about 10 min

*Proves the comeback word ends a possession only when the Message is the comeback itself, not when it is words around it. Spec GF-M7.*

**Before you start**

- A waiting goldfish as in GOLDFISH-03 step 1.

**Steps**

1. Post *"Ruth, @Ruth comeback later"*.
2. Read the Room and the possession row.

**Pass if — all of these**

- The goldfish took a Turn (it may answer "I'll keep waiting"); the possession is still `waiting` and the chip is still there.

**Fail if — any of these**

- The possession ended.

### GOLDFISH-08 — The warm Ruth answers in another Room 💰

**Paid** 💰 · about 10 min

*Proves the possession belongs to one (Agent, Room): Ruth's other Rooms are untouched. Spec GF-M8.*

**Before you start**

- A waiting goldfish in the group Room.

**Steps**

1. In your direct Room with Ruth, send *"What is 4 plus 4? Answer in one line."*
2. Check the possession's `turns`.

**Pass if — all of these**

- Ruth answered in the direct Room from her own session, unstamped, and the possession is still `waiting` with `turns` unchanged.

**Fail if — any of these**

- The goldfish got the Message, or the answer is stamped.

### GOLDFISH-09 — Plain Messages stay context for the warm Ruth after the return 💰

**Paid** 💰 · about 15 min

*Proves what the goldfish was not Mentioned about is still read by the warm Ruth once the body returns. Spec GF-M9.*

**Before you start**

- A waiting goldfish.

**Steps**

1. In the possessed Room, send *"Unrelated note: the build number is 42."* (no Mention), then *"@Ruth what is 2 plus 3?"*.
2. When the goldfish has answered, press **Call Ruth back**, then ask the warm Ruth *"@Ruth what did you miss?"*.

**Pass if — all of these**

- The goldfish answered only the Mentioning Message.
- After the return, the warm Ruth lists both Messages and the goldfish's answer as context.

**Fail if — any of these**

- The goldfish answered the plain note, or the warm Ruth does not know it.

### GOLDFISH-10 — A kill mid-possession ends as Restarted 💰

**Paid** 💰 · about 20 min

*Proves a possession is never resumed after a restart, that its body comes back, and that a Mention still owed is answered once, by the warm Ruth. Spec GF-M10.*

**Before you start**

- A waiting goldfish, in a lane you may kill. Find the process by the lane's port, and kill only that one.

**Steps**

1. Post *"@Ruth what is 17 plus 25? Answer in one line."* and two seconds later kill the Huddle.App process on the lane's port (the Adapters die with it).
2. Start the lane again with the same data folder and launch script.
3. Read the Room, the possession row and the number of answers.

**Pass if — all of these**

- The possession is `ended`, `exit restarted`, and was not resumed.
- The Room shows the Huddle result with "Interrupted by a restart" and the return row.
- The owed Mention was answered once, by the warm Ruth, unstamped.

**Fail if — any of these**

- The goldfish resumed, the Mention was answered twice, or no return posted.

### GOLDFISH-11 — A goldfish that asked and got nothing waits 💰

**Paid** 💰 · about 15 min

*Proves the goldfish ends its Turn and waits when nobody Mentions it, and nothing starts it until a Mention of Ruth or **Call Ruth back**. Spec GF-M11.*

**Before you start**

- Dictate the brief's words: "Plain question, do NOT ask Quinn to mention Ruth. Then end your Turn and wait; do not call goldfish_done until a message that mentions you arrives." Without them Ruth tends to add "begin the answer with @Ruth" herself and the goldfish finishes at once.

**Steps**

1. Ask the goldfish to ask Quinn which test framework fits, with that brief.
2. Let Quinn answer without a Mention. Watch the chip for 70 seconds.

**Pass if — all of these**

- The chip reads "Ruth (goldfish) is waiting · n min" and the minutes count up.
- No new goldfish Turn starts in those 70 seconds.

**Fail if — any of these**

- The goldfish took a Turn with no Mention, or the chip vanished.

### GOLDFISH-12 — A goldfish's review reads "Goldfish review by Ruth" 💰

**Paid** 💰 · about 15 min

*Proves a `log_change` of kind `reviewed` made by the goldfish is marked in the document's History. Spec GF-M12.*

**Before you start**

- A waiting goldfish that has read `docs/spec.md` without changing it.

**Steps**

1. Send *"@Ruth, before you finish: you reviewed docs/spec.md without changing it, so record that in the document's History with log_change (kind reviewed, one-line summary), then call goldfish_done."*
2. Open `Agents/Ruth/work/docs/spec.md` in the Library and read its byline. Read the History file with [`O-FILE`](common.md#o-file): `<data>\doc-history\agents\Ruth\work\docs\spec.md.jsonl`.

**Pass if — all of these**

- The History line has `"kind":"reviewed"`, `"actor":"Ruth"` and `"goldfish":true`.
- The Library byline reads "Goldfish review by Ruth".

**Fail if — any of these**

- The line is not marked as a goldfish's, or the byline reads "Reviewed by Ruth".

### GOLDFISH-13 — A spec review Task leads Ruth to call `goldfish` 💰

**Paid** 💰 · about 30 min

*Proves #669 and goldfish meet: in a Team made from the `spec-driven` template, a review Task tagged `review`, a child of the spec Task, leads Ruth to review in goldfish mode, because the built-in process says "Spec reviews are done in goldfish mode". Spec GF-M13.*

**Before you start**

- A Team created from the `development` Team type with the `spec-driven` process (check the Team's process text still holds the sentence "Spec reviews are done in goldfish mode"), Ruth hired into it as the reviewer, and a spec file attached to a spec Task by its path in backticks, as the process describes.

**Steps**

1. As the owner of the spec Task, create a review Task for it: a child of the spec Task, tagged `review`, assigned to Ruth and added to the spec Task's `blocked_by`. Use the Task form or ask the developer Agent to do it.
2. Wait for Ruth's wake. Read `O-TOOLCALLS` for Ruth, the possession row and the review Task's comments.
3. Let the goldfish finish. Read the review Task.

**Pass if — all of these**

- Ruth's tool calls hold `mcp__team__goldfish`, and the brief is also a comment on the review Task.
- The goldfish's findings come back as the result, and Ruth moves the review Task to Done (approval) or leaves it open with the findings as a comment.
- If the goldfish logged a review on the spec, its History reads "Goldfish review by Ruth".

**Fail if — any of these**

- Ruth reviewed in her own session without calling `goldfish`, with the sentence present in the process text -> record the wording she saw.
- Ruth was not woken for the review Task.

### GOLDFISH-14 — The Read deny stops `Read` and `cat` of Memory 💰

**Paid** 💰 · about 15 min

*Proves the deny list stops the file route to Ruth's Memory: `Read` is refused and a plain `cat` is refused too. A script that opens the file (`python -c`) is the open route the spec accepts (E-9). Spec GF-M14.*

**Before you start**

- `<data>\Agents\Ruth\work\memory\codeword.md` holding "the secret codeword is KINGFISHER-42".

**Steps**

1. Send *"@Ruth, use goldfish mode for a permissions check: 1) use the Read tool on memory/codeword.md; 2) use Bash to run: cat memory/codeword.md; report each result and do not try another way."*
2. Read `O-TOOLCALLS` for Ruth and the Room.

**Pass if — all of these**

- The goldfish's `Read` has status `failed` with "File is in a directory that is denied by your permission settings."
- Its `cat` has status `failed` with "Permission to use Bash with command cat memory/codeword.md has been denied."
- The word KINGFISHER-42 never appears in the Room.

**Fail if — any of these**

- Either call succeeded or the codeword was posted.

Do not make the `python -c` route a pass condition: the spec accepts it as a known limit (E-9).

### GOLDFISH-15 — A goldfish's Question can be answered from its card 💰

**Paid** 💰 · about 20 min

*Proves the Human can answer a goldfish's `ask_human` from the card, and that the answer reaches the goldfish, not the warm Ruth. Spec GF-M15.*

**Steps**

1. Send *"@Ruth, use goldfish mode to review docs/spec.md. Put in the brief: 'Ask the Human which environment to test (use ask_human, with options Windows, Linux and macOS), then report the answer with your findings.'"*
2. When the card appears, read its title and the Rooms sidebar. While the goldfish's Turn is open, note whether the options are disabled. When the possession reads `waiting`, tap an option.
3. Read the Room and the return row.

**Pass if — all of these**

- The card reads "Ruth · goldfish is asking" and Needs You lists the Room.
- The options are disabled while the goldfish's Turn is open ("is still writing...") and **enabled once it waits**.
- Tapping an option posts the answer as a Message that Mentions Ruth, the goldfish continues, the warm Ruth gets no Turn for it, and the result lists the question and the answer.

**Fail if — any of these**

- The options stay disabled after the goldfish waits (a stale Draft of Ruth is being counted).
- The warm Ruth answered it.

### GOLDFISH-16 — `goldfish` is refused while `MaxConcurrentTurns` is above 1 💰

**Paid** 💰 · about 10 min

*Proves goldfish mode refuses when more than one Turn may run at once, with the one-Turn-at-a-time sentence, and starts nothing. Spec GF-M16.*

**Before you start**

- Relaunch the lane with `Team__Acp__MaxConcurrentTurns=2` in the launch script.

**Steps**

1. Send *"@Ruth, goldfish docs/spec.md: can it be built as written?"*
2. Read `O-TOOLCALLS` for Ruth, the Room and the possession table.

**Pass if — all of these**

- Ruth called `mcp__team__goldfish` and the result reads "Goldfish mode needs one Turn at a time, and this Huddle runs up to 2 at once. ...".
- Ruth told you she cannot send a goldfish copy right now.
- No start row, no chip, no ring, and no new row in `goldfish_possessions`.

**Fail if — any of these**

- A possession started, or Ruth was left possessed.
