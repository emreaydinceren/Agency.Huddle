# Reply Gate, Mentions and Room Budget

Prove the three decisions that govern whether an Agent speaks at all, in a real browser against a real server: the Reply Gate (a Room of two Members answers everything; three or more is Mention-gated), Mention resolution (longest handle first, Names beating equal-length Aliases, multi-word Names, word boundaries), and the per-Room Budget that halts a Room and asks the Human with Continue / Leave paused. Almost every failure in this area is SILENCE rather than an error, so every test below names a non-UI oracle (a wire envelope printed by a pipe client, a line in the transcript file, a line in the server log) that separates "the parser decided against it" from "nobody was listening".

**36 tests** · 30 free, 6 paid 💰 · about 5.9 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. RESET here always means `P-RESET-ROOMS` — it clears every Room, Member list and Transcript while leaving the Persona library alone. Budget counters are in memory; a restart clears them.
2. **PROFILE A — free, built-in demo agents.** `P-LAUNCH-FREE`, plus `$env:Team__DemoAgent__Enabled = 'true'` and `$env:Team__AgentMessageBudget = '40'` (or whatever budget the test names) set before `dotnet run`. `echo` and `alpha` connect by themselves and each gets a two-Member Room named after it.
3. **PROFILE B — free, scripted pipe clients you control.** Needed wherever a test names a wire envelope as its oracle. Profile A but with `$env:Team__DemoAgent__Enabled = 'false'` so the built-in names cannot clash with yours, then one `P-ECHO-BOT` terminal per Agent the test names. `O-WIRE` is the primary oracle for this whole area.
4. **PROFILE C — paid, real Personas.** `P-LAUNCH-PAID` with `$env:Team__AgentMessageBudget` set to the small number the test names. Every Persona must already be Haiku / low.
5. Learn the `O-LOG` lines you will read all day, verbatim: `Room '{RoomId}' refused a message from '{AgentName}': its budget of {N} agent messages since a human last spoke is spent.` (warning) / `Room '{RoomId}' was extended to {N} agent messages.` / `Persona '{PersonaName}' declined a turn in room {RoomId}: the room has spent its budget of {N} agent messages.` (warning) / `Persona '{PersonaName}' has spent its token budget of {N} and is taking no more turns until a human speaks to it.` / `Invited agent '{AgentName}' ({AgentId}) into room '{RoomId}'.` / `Created direct room '{RoomId}' for agent '{AgentName}'.`
6. In `O-DB`, `SELECT COUNT(*) FROM room_members WHERE room_id='…'` is what makes a Room two-Member or group — that count and nothing else. The Room's grey member line in the browser is an acceptable substitute.
7. READ THIS BEFORE FILING ANYTHING. Both demo agents now implement the Reply Gate: `DemoAgentHost` calls `ReplyGate.Decide(mentioned, memberCount, agentMessagesSinceHuman, budget, following: false)` directly — always passing `following: false`, because a demo agent is a raw pipe client with no `mcp__team__follow_room` tool to call and can never be following a Room — and `tools/echo-bot.ps1` mirrors that same decision (budget check first, then member-count-or-mention; following never enters into it for the same reason) from the fields already on the `messagePosted` envelope. So a two-Member Room now answers an un-mentioned Message under Profile A or B — that is the correct behaviour per ADR-0004, not silence to expect. The free tests below can therefore observe the Reply Gate's own decisions directly — answer-without-mention in a two-Member Room, Mention-gating once a third Member joins, and decline-before-Turn once the Budget is spent — without needing a real Persona. Following (roadmap item 8) is invisible to both demo agents by construction and is exercised only by real Personas in `app-tools.md`'s APPTOOLS-23 to -25. (Catch-up buffering is still server state a demo agent client does not surface, so that piece remains a paid-tests concern.)

## Tests

### REPLYGATEBUDGET-01 — A demo two-Member Room answers with or without a Mention

**Free** · about 6 min

*Proves the stock free installation works end to end and establishes the baseline every later free test builds on, including that the server labels a Mention correctly.*

**Before you start**

- No app running.
- Nothing running on port 5100.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A leaving the budget at `40`.
3. Open `http://localhost:5100` in a browser.
4. In the sidebar, click the Room named `echo`.
5. Read the grey member line directly under the Room heading.
6. Click into the composer, type `hello` and press Enter.
7. Wait 5 seconds and watch for a reply.
8. Type `hello @echo` and press Enter.
9. Wait 5 seconds.
10. In `T-A`, note the Room id from the browser URL's last segment, and run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl` in a second PowerShell window.

**Pass if — all of these**

- The heading reads `echo` and the member line reads exactly `You, echo`.
- After step 6 your Message `hello` appears in the transcript with the sender name `You`, and within a few seconds a streaming row appears with sender `echo`, settling into an ordinary Message whose body reads `echo: hello`, with `echo:` in bold and no `@` in it. The `echo` Room has exactly two Members, so per ADR-0004 it answers without needing a Mention.
- A grey line reading exactly `1 of 40 agent replies since you last spoke.` appears between the transcript and the composer.
- After step 8 a second streaming row appears with sender `echo` and a **Stop** button beside the name, then settles into an ordinary Message whose body reads `echo: hello echo`, with `echo:` in bold and no `@` anywhere in it. The demo agent strips only the `@` character from what it quotes, so the mention word survives even though the at-sign does not.
- The grey line now reads exactly `2 of 40 agent replies since you last spoke.`
- The `.jsonl` file holds exactly four lines: `hello` from `human`, the first reply from the agent, `hello @echo` from `human`, and the second reply from the agent.

**Fail if — any of these**

- `echo` does not reply to the bare `hello` at step 6 and the agent is confirmed connected (see INCONCLUSIVE) -> the `echo` Room has only two Members, so ADR-0004 requires a reply without a Mention; silence here means the demo agent is still gating on `mentioned` alone instead of calling `ReplyGate.Decide`.
- `echo` does not reply to `hello @echo` and the agent is confirmed connected (see INCONCLUSIVE) -> the Mention did not resolve, i.e. mention parsing is broken at its simplest case.
- Either reply lands but no matching `N of 40 agent replies since you last spoke.` line appears -> the Room view is not reading the Budget carried on the posted-Message event, so the pause block tested later will also never appear and a capped Room will look identical to a crashed one.
- Either reply body still contains an `@` -> the demo agent's loop guard is gone and two agents quoting each other can re-trigger each other indefinitely.

**Inconclusive if**

If there is no Room named `echo` in the sidebar at all, the demo agents never connected: search `T-A` for `Demo agent echo connected.`; if it is missing, stop, re-run RESET, confirm `$env:Team__DemoAgent__Enabled` is `'true'`, and start again. If `T-A` mentions Personas starting or a `node` process, `Team__Acp__Enabled` was not set to `false` — press Ctrl+C at once, money is being spent, and re-run the test from step 1.

> [!NOTE]
> A reply to the bare `hello` at step 6 is correct and documented: the `echo` Room has exactly two Members, and per ADR-0004 a Direct Room answers every Message without a Mention. The real defect to watch for in this test is silence, in either half.

### REPLYGATEBUDGET-02 — Three or more Members makes the same Room Mention-gated

**Free** · about 7 min

*Proves membership count alone flips a Room from answer-everything to Mention-gated, with no stored Room kind, and that one Mention wakes one Agent while two wake two.*

**Before you start**

- REPLYGATEBUDGET-01 has just passed and the app is still running.
- You are on the Room named `echo`.

**Steps**

1. Click **Add teammate** at the right of the Room header.
2. In the panel that opens, click `alpha`.
3. Read the green strip at the top of the panel.
4. Click **Add teammate** again to close the panel.
5. Read the sidebar entry, the Room heading and the grey member line, WITHOUT reloading the page.
6. Type `good morning everyone` and press Enter.
7. Wait 15 seconds without touching anything.
8. Type `good morning @echo` and press Enter, then wait 5 seconds.
9. Type `@echo @alpha go` and press Enter, then wait 5 seconds.

**Pass if — all of these**

- The green strip reads exactly `Invited alpha. Room is now "echo, alpha".`
- With no page refresh, the sidebar entry and the Room heading both change to `echo, alpha` and the member line becomes `You, echo, alpha`.
- Step 6 produces no reply at all from either Agent, AND a quiet blue note appears above the
  composer reading `No teammate was @-mentioned - name one to ask for a reply.` That note is the
  point of the silence being legible at all; before #40 step 6 looked identical to a broken app.
- The note disappears the moment step 8's Message posts, because that one names somebody.
- Step 8 produces exactly one reply, from `echo`.
- Step 9 produces exactly two replies, one starting `echo:` and one starting `alpha:`.
- `SELECT COUNT(*) FROM room_members WHERE room_id='<RoomId>'` returns 3 (or, without SQLite, the member line names three people).

**Fail if — any of these**

- Both Agents reply to `good morning everyone` -> the member-count gate has collapsed or `mentioned` is being set true for every Member; this is the reply storm the design exists to prevent and it will burn a billed Turn per Agent per Message once real Personas are used.
- Only `echo` replies at step 9 -> the Mention scan stops after consuming the first handle, so a Message naming two people reaches only one of them, silently.
- The Room is renamed only after a manual page refresh -> the rooms-changed event is not reaching the sidebar and the header, so membership changes made anywhere else in the app will not show either.
- `alpha` stays silent at step 9 while `echo` answers, and `alpha`'s dot is green -> the second Mention did not resolve.

**Inconclusive if**

If `alpha` does not appear in the **Add teammate** list, it is either already a Member or never connected — check the sidebar for a Room named `alpha` and `T-A` for `Demo agent alpha connected.`, then restart from RESET. If the dot beside `alpha` in the panel is red rather than green, `alpha` is offline and its silence proves nothing about the gate: fix the agent first. If the strip is red instead of green, read its text and treat the test as inconclusive.

> [!NOTE]
> A Room has no stored kind anywhere: the member count is the whole of this rule. That is why this test changes nothing but membership.

### REPLYGATEBUDGET-03 — /invite from the composer, and its two error strips, never touch the Transcript

**Free** · about 6 min

*Proves the composer's slash command flips a Room's membership through the same path the header button uses, reports unknown agents and unknown commands distinctly, and writes nothing to the Transcript.*

**Before you start**

- The app is running under PROFILE A.
- A two-Member Room named `alpha` exists (it does after RESET plus a start).

**Steps**

1. Click the Room named `alpha` in the sidebar and note the Room id from the URL's last segment.
2. Type `/invite @echo` and press Enter.
3. Read the strip directly above the composer box.
4. Read the sidebar entry and the Room heading, without reloading.
5. Type `/invite @nobody` and press Enter, then read the strip.
6. Type `/shout hello` and press Enter, then read the strip.
7. Scroll the transcript and count the Messages in it.
8. In a second PowerShell window run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl`.
9. In `T-A`, look for the invite log line.

**Pass if — all of these**

- Step 3 shows a GREEN strip reading exactly `Invited echo. Room is now "alpha, echo".`
- The sidebar and heading rename to `alpha, echo` with no page refresh.
- Step 5 shows a RED strip reading exactly `Unknown agent @nobody`.
- Step 6 shows a RED strip reading exactly `Unknown command`.
- The transcript shows no Message at all for any of the three commands.
- The `.jsonl` file gained no line from any of the three commands (it may still be absent entirely if nothing was ever posted in this Room).
- `T-A` shows `Invited agent 'echo' ('…') into room '…'.` exactly once.

**Fail if — any of these**

- A `/invite` or an error strip appears as a Message in the transcript or as a line in the `.jsonl` -> a command is being persisted as conversation, which would then be re-delivered to Agents and would count against the Room's Budget.
- `/shout hello` reports `Unknown agent @shout hello` rather than `Unknown command` -> the command regex is matching anything beginning with a slash, so a typo silently becomes an invite attempt.
- The strip is green but the sidebar name does not change until a refresh -> the rooms-changed notification is not reaching the components.
- `/invite @nobody` succeeds or is silent -> an invite is not being validated against the directory.

**Inconclusive if**

If `echo` is already a Member of the `alpha` Room from an earlier test, step 2 will not produce the expected strip — run RESET, restart under Profile A and begin again. If no strip appears at all after Enter, check that the text actually sent (the composer box empties on send); if the box still holds your text, the keyboard handler is not attached and the whole test is inconclusive.

> [!NOTE]
> The `/invite` Name is captured as 'everything after /invite' rather than by shape, precisely so a Name containing spaces survives. That half of the rule is exercised in REPLYGATEBUDGET-19.

### REPLYGATEBUDGET-04 — A Mention is case-insensitive and ends at trailing punctuation

**Free** · about 6 min

*Proves six spellings of the same Mention all resolve, which is where a case-sensitive comparison or a greedy name boundary would otherwise fail in complete silence.*

**Before you start**

- The app is running under PROFILE A.
- You are in a Room with three or more Members that contains `echo` — the `echo, alpha` Room from REPLYGATEBUDGET-02 is exactly right.

**Steps**

1. Confirm the member line names three people, one of them `echo`.
2. Type `@ECHO hi` and press Enter, then wait 4 seconds and count the replies.
3. Type `@Echo hi` and press Enter, then wait 4 seconds and count the replies.
4. Type `@echo,` and press Enter, then wait 4 seconds and count the replies.
5. Type `@echo.` and press Enter, then wait 4 seconds and count the replies.
6. Type `@echo:` and press Enter, then wait 4 seconds and count the replies.
7. Type `@echo?` and press Enter, then wait 4 seconds and count the replies.

**Pass if — all of these**

- Each of the six Messages produces exactly one reply, and every reply's sender is `echo`.
- `alpha` never replies to any of the six.
- No grey budget line ever shows a figure above `1` (each of your Messages resets the counter first).

**Fail if — any of these**

- `@ECHO hi` or `@Echo hi` produces no reply while `@echo hi` does -> Mention matching is case-sensitive, so a Human who capitalises a Name gets silence with no error anywhere.
- `@echo,` or `@echo.` produces no reply -> the punctuation is being swallowed into the Name, so the handle no longer matches any Member and the Mention resolves to nobody, silently.
- Any of the six produces two replies -> a single handle is being matched more than once, which with a real Persona means paying twice for one Mention.

**Inconclusive if**

If `echo` is offline (red dot in the **Add teammate** panel, or the Teammate tile reads Offline), every line of this test is inconclusive — no Mention can be observed through an Agent that receives nothing. If the Room has only two Members, a reply proves nothing about Mention resolution because a two-Member Room would be answered anyway by a client that implemented the gate: add a second Agent first.

> [!NOTE]
> A space is deliberately NOT a boundary character, which is what makes multi-word Names possible; that is covered in REPLYGATEBUDGET-19 and -21.

### REPLYGATEBUDGET-05 — Repeating a Mention wakes an Agent once, and the Human can be Mentioned harmlessly

**Free** · about 4 min

*Proves duplicate handles collapse to one delivery (three billed Turns for one Mention is the failure) and that naming the Human resolves to somebody who is not an Agent without erroring.*

**Before you start**

- The app is running under PROFILE A.
- You are in a Room of three or more Members containing `echo`.

**Steps**

1. Type `@echo @echo @echo hi` and press Enter.
2. Wait 8 seconds and count the replies.
3. Type `@You are needed` and press Enter.
4. Wait 10 seconds and watch the whole page.
5. In a second PowerShell window run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl | Select-Object -Last 4`.

**Pass if — all of these**

- Step 1 produces exactly ONE reply, from `echo`.
- Step 3's Message posts normally and appears in the transcript with sender `You`.
- Nothing at all follows step 3 by way of a reply, a red strip, an error banner, or an entry in
  `T-A` at Warning level. What DOES appear is the quiet blue note above the composer reading
  `No teammate was @-mentioned - name one to ask for a reply.` Read its wording closely: it says
  *no teammate*, not *nobody*, precisely because `@You` IS a Mention - of the Human, who is not a
  Teammate and is never delivered to. A note here reading "nobody was mentioned" would be false,
  and this test is the case that proves it.
- The tail of the `.jsonl` shows one agent line after `@echo @echo @echo hi`, and the `@You are needed` line with no agent line after it.

**Fail if — any of these**

- Three replies arrive at step 2 -> duplicate Mentions are not collapsing; with a real Persona this is three billed Turns for one Mention, and a Message naming an Agent five times would cost five.
- Step 3 shows a red strip or an error -> Mentioning the Human is being treated as an error rather than as a Mention that simply reaches no Agent, which will make `@You` unusable in ordinary conversation. The blue `role="status"` note is NOT this: it is an Info consequence, not a failure.
- The note at step 3 says "nobody" rather than "no teammate" -> the copy has drifted into a claim that is false in this exact case.
- An Agent replies to `@You are needed` -> the Human is being delivered to, or the Mention resolved to the wrong Member.

**Inconclusive if**

If the Human's display name is not `You` (it is configurable), step 3 names nobody and tells you nothing: read the first name in the Room's grey member line and use that instead. If `echo` is offline, step 1 is inconclusive.

> [!NOTE]
> The Human is a genuine Mention target; delivery simply skips non-Agent Members.

### REPLYGATEBUDGET-06 — The Budget note counts agent replies and a Human Message resets it to nothing

**Free** · about 6 min

*Proves the counter shown under the transcript matches the number of agent Messages since the Human last spoke, updates live, and disappears the moment the Human speaks.*

**Before you start**

- The app is running under PROFILE A with `Team__AgentMessageBudget` at `40`.
- You are in the `echo, alpha` Room (three Members).

**Steps**

1. Type `starting fresh` and press Enter, then look at the gap between the transcript and the composer.
2. Type `@echo hi` and press Enter, then wait 5 seconds and read the grey line above the composer.
3. Type `@echo @alpha go` and press Enter, then watch the grey line while both replies arrive.
4. Wait until both replies have landed and read the grey line again.
5. Type `.` and press Enter and read the same area again.
6. Count the agent-authored lines after the last `"senderId":"human"` line in `E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl`.

**Pass if — all of these**

- After step 1 there is NO grey budget line (no agent has spoken since you did).
- After step 2 the grey line reads exactly `1 of 40 agent replies since you last spoke.`
- During step 3 the line disappears when your Message posts, then reappears as `1 of 40 …` and then `2 of 40 …` as the two replies land — with no page refresh at any point.
- After step 5 the grey line is gone again.
- The file count in step 6 matches the last figure the line showed before step 5.

**Fail if — any of these**

- The grey line never appears -> the Room view is not reading the Budget off the posted-Message event, and the pause block will be invisible too: a capped Room would then look exactly like a crashed Agent.
- The figures only update after a manual refresh -> the same event is not repainting the view live, so a Human watching a running conversation sees a stale allowance.
- The line survives your Message at step 5, or shows a non-zero figure right after it -> a Human Message is not resetting the counter, so a Room will eventually pause even though the Human keeps speaking.
- The figure disagrees with the file count in step 6 -> the counter is being incremented somewhere other than the accepted-Message path, so it describes no real prefix of the conversation.

**Inconclusive if**

If one of the two Agents is offline at step 3 only one reply arrives and the line will read `1 of 40`, which is correct for what happened — check both dots are green in **Add teammate** before judging. If `Team__AgentMessageBudget` was left at a different value from a previous test, substitute that number for 40 throughout rather than failing the test.

> [!NOTE]
> The line is deliberately suppressed when the Room is uncapped; that case is REPLYGATEBUDGET-16.

### REPLYGATEBUDGET-07 — A spent Budget halts the Room and asks the Human, visibly

**Free** · about 6 min

*Proves the single most important negative in this area: a cap that stops an Agent is announced in the Room view rather than being silent, and it never marks anyone unhealthy and never posts a Message of its own.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A but set `$env:Team__AgentMessageBudget = '1'` before `dotnet run`.
3. Open `http://localhost:5100` and click the Room named `echo` in the sidebar.
4. Type `@echo hi` and press Enter.
5. Wait 5 seconds, then read everything between the transcript and the composer.
6. Read the sender name of every Message in the transcript.
7. Click **Teammates** in the sidebar and read the state under the `echo` tile, then use the browser Back button.
8. Read `T-A`.

**Pass if — all of these**

- `echo`'s reply lands, and then a `budget-prompt` block appears between the transcript and the composer, separated from the transcript by a plain rule, whose `budget-prompt-text` paragraph is in the danger colour and reads exactly `Agents have sent 1 replies since you last spoke, and are paused.`
- That block carries exactly two buttons, labelled **Continue** and **Leave paused**, and above
  them a second, grey `budget-prompt-warning` paragraph reading
  `Continue will deliver the last message again, and it is a teammate's own - a teammate is
  never delivered its own message, so this will wake nobody. Say something instead.` It is there because the
  Room's last Message is `echo`'s own reply, and an Agent is never delivered its own Message.
- No context-only note appears. This is a two-Member Room, so a Message is answered without a
  Mention and there is nothing to explain — the note is for Rooms of three or more.
- No other strip appears above the composer — in particular no alert listing `echo is Degraded` or `echo is Offline`.
- Every Message in the transcript is from `You` or from `echo`; there is no Message from a `system` or similar sender announcing the pause.
- `T-A` shows NO `refused a message from` warning yet (nothing has tried to post a second time).
- The Teammates page still shows `echo` as Online (or, with the demo agent, at least not Degraded).

**Fail if — any of these**

- The reply lands and nothing else appears -> the pause is invisible, which is the exact failure this design exists to prevent: a Room that silently stops taking replies is indistinguishable from an Agent that crashed.
- A Message announcing the pause appears in the transcript -> the notice is being posted as conversation; posted as the Human it would reset the very Budget it reports, and posted as a third kind of sender it breaks the two-kind directory.
- `echo` is shown Degraded, or a red member-health alert lists it -> a spent Room Budget is marking an Agent that is working exactly as designed, which trains the Human to ignore health warnings.
- The block appears but with only one button, or with different wording -> record the exact text seen; the wording is what tells the Human this is a question and not a crash.
- A context-only note appears in this two-Member Room -> the note is firing where the Reply Gate
  answers everything, which makes it an always-on strip nobody will read.

**Inconclusive if**

If no reply arrives at all, you have a dead agent rather than a pause — go back to REPLYGATEBUDGET-01 and get a reply before judging this test. If the block reads `1 of 1` in grey rather than red with buttons, you are looking at the dismissed variant, which means **Leave paused** was clicked in this view earlier: reload the page and repeat from step 4.

> [!NOTE]
> The count in the sentence is not pluralised (`1 replies`). That is current, known, cosmetic behaviour and must not be filed as a defect.

### REPLYGATEBUDGET-08 — The write path refuses an over-Budget post: two Agents are woken, only one Message lands

**Free** · about 7 min

*Proves the Budget is enforced atomically on the write path, so two Agents that were both legitimately woken cannot between them take one more Message than the Room granted.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A but set `$env:Team__AgentMessageBudget = '1'` before `dotnet run`.
3. Open `http://localhost:5100`, click the Room named `echo`, and note the Room id from the URL.
4. Click **Add teammate**, click `alpha`, and confirm the header renames to `echo, alpha`.
5. Click **Add teammate** again to close the panel.
6. Type `@echo @alpha go` and press Enter.
7. Wait 10 seconds and count the agent Messages that appear.
8. Read `T-A`.
9. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl`.

**Pass if — all of these**

- Exactly ONE agent Message appears in the Room (from either `echo` or `alpha` — which one wins is a race and either is correct).
- The red pause block appears with **Continue** and **Leave paused**.
- `T-A` shows exactly ONE `Room '…' refused a message from '…': its budget of 1 agent messages since a human last spoke is spent.` warning.
- The `.jsonl` file holds exactly one agent line after your `@echo @alpha go` line.

**Fail if — any of these**

- TWO agent Messages appear and the `.jsonl` holds two agent lines -> the Budget check is not atomic with the write, so two concurrent posts each read the same spare capacity and both take it; with real Personas this is how a cap of N becomes a cap of N-plus-however-many-agents.
- No refusal warning appears in `T-A` and only one Message landed -> the second Agent may simply not have posted at all (see INCONCLUSIVE); without the warning you cannot claim the refusal happened.
- Zero agent Messages appear -> both were refused, meaning the counter was already non-zero before your Message reset it, which would mean a Human Message is not resetting the Budget.

**Inconclusive if**

If only one of the two Agents is online, only one post is ever attempted and the test proves nothing about the race: check both dots are green in **Add teammate** first. If the timing is such that the second Agent's post arrives after you have already typed something else, the Budget will have reset — do not type anything between step 6 and step 9.

> [!NOTE]
> This layer refuses AFTER the Turn that produced the Message has already happened. With a real Persona the model has thought and the text is thrown away. That is the documented price of putting the authority on the write path, not a defect; the layer that avoids the spend is covered in REPLYGATEBUDGET-33.

### REPLYGATEBUDGET-09 — Leave paused hides the question but keeps the pause legible

**Free** · about 4 min

*Proves dismissing the prompt replaces it with a muted statement of the same fact rather than clearing the Room's state or hiding the pause entirely.*

**Before you start**

- REPLYGATEBUDGET-08 has just finished and the pause block is on screen in the `echo, alpha` Room.
- The run's budget is `1`.

**Steps**

1. Click **Leave paused**.
2. Read whatever now sits between the transcript and the composer.
3. Read `T-A` and count the `was extended to` lines.
4. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl` and count the lines.

**Pass if — all of these**

- The two-button block is replaced by a single muted line reading exactly `Paused — 1 of 1 agent replies since you last spoke.`
- `T-A` contains NO `was extended to` line — dismissing granted nothing.
- The `.jsonl` line count is unchanged from REPLYGATEBUDGET-08's step 9.

**Fail if — any of these**

- The whole area goes blank after the click -> the pause has become invisible, so a Human arriving later cannot tell a paused Room from a dead one; that is the same silent-cap failure one step removed.
- A `was extended to` line appears in `T-A` -> **Leave paused** is granting Budget, which is the opposite of what it says and lets an unattended Room keep spending.
- A new line appears in the `.jsonl` -> dismissing is posting something.

**Inconclusive if**

Because no demo agent ever posts spontaneously, you cannot make a fresh post get refused while paused without first speaking (which un-pauses the Room). The proof that the Room is still refusing is therefore the refusal warning already in `T-A` from REPLYGATEBUDGET-08, not a new one. If that warning is not there, this test is inconclusive — re-run REPLYGATEBUDGET-08 first.

> [!NOTE]
> The pause itself is unchanged by the dismissal; only the question is hidden, and only for this view.

### REPLYGATEBUDGET-10 — A dismissal is per-view: navigation, reload and the next Message all bring the question back

**Free** · about 6 min

*Proves a dismissal cannot become sticky, because a sticky one would mean a later pause is never announced.*

**Before you start**

- REPLYGATEBUDGET-09 has just passed: the muted `Paused — 1 of 1 …` line is on screen.

**Steps**

1. Click the Room named `alpha` in the sidebar.
2. Click the Room named `echo, alpha` in the sidebar to come back.
3. Read the area between the transcript and the composer.
4. Click **Leave paused** again.
5. Press F5 to reload the page.
6. Read the same area again.
7. Type `carry on` and press Enter, and confirm the pause area goes empty.
8. Type `@echo hi` and press Enter, then wait 5 seconds.

**Pass if — all of these**

- After step 2 the red block with **Continue** and **Leave paused** is back.
- After the F5 at step 5 the red block is back again.
- After step 7 the pause area is empty (your Message reset the Room).
- After step 8 the red block appears again, rather than staying suppressed from the earlier dismissal.

**Fail if — any of these**

- The muted `Paused —` line survives navigating away and back, or survives an F5 -> the dismissal is being stored beyond the view, so the next pause in this Room will never be announced to this Human.
- After step 8 nothing appears where the red block should be -> a dismissal from the previous pause is suppressing a NEW pause: the Room is capped and silent, the exact failure the prompt exists to prevent.

**Inconclusive if**

If clicking `alpha` in the sidebar shows a Room you do not recognise, you are in the wrong Room — match the Room id in the URL against the one you noted earlier. If step 8 produces no reply at all, `echo` has gone offline and the test cannot reach a second pause.

> [!NOTE]
> Two separate mechanisms are being checked here: reloading the view, and a new Message arriving. Both must clear the dismissal.

### REPLYGATEBUDGET-11 — The pause survives a reload and shows in a brand-new tab

**Free** · about 5 min

*Proves the Room view reads the Budget when it loads and not only from the live event, so a reconnected browser or a second tab cannot show an un-paused Room that is in fact paused.*

**Before you start**

- A Room is paused and the red block is showing (reach that state by re-running REPLYGATEBUDGET-07 if needed).
- Do NOT click **Leave paused** during this test.

**Steps**

1. Note the full URL of the paused Room, including the Room id.
2. Press F5 and wait for the page to finish loading.
3. Read the area between the transcript and the composer.
4. Open a NEW browser tab and paste the URL from step 1 into it.
5. Read the same area in the new tab.
6. Read `T-A`.

**Pass if — all of these**

- After the reload the red block is present with the same figures and both buttons.
- In the brand-new tab, which never witnessed the Message that caused the pause, the red block is also present with the same figures.
- `T-A` gained NO new `was extended to` line — reloading only re-read the state, it changed nothing.

**Fail if — any of these**

- The block disappears after F5 or is absent in the new tab -> the view only knows about the pause from the live event and never reads the current Budget on load; any browser reconnect, refresh, or second device then shows a Room that looks free to use while every agent Message into it is being refused.
- The figures differ between the two tabs -> two views disagree about one Room's state.
- A `was extended to` line appears -> merely loading the page is granting Budget, which would let a runaway Room be resumed by a refresh.

**Inconclusive if**

If the whole page fails to load after F5, the server has stopped — check `T-A` and restart, then re-reach the pause. If the new tab redirects to a different Room, you pasted the wrong URL.

> [!NOTE]
> This is the half of the behaviour that is easiest to miss, because the happy path (watching the Message arrive) works even when the load path does not.

### REPLYGATEBUDGET-12 — Continue grants exactly one more Budget, once per click, and duplicates nothing on screen

**Free** · about 6 min

*Proves the grant is bounded and single, and that re-delivering the last Message to the Agents does not show that Message twice to the Human or write it to the Transcript again.*

**Before you start**

- A Room is paused with the red block showing, under a run whose `Team__AgentMessageBudget` is `1`.
- You know the Room id and the current line count of its `.jsonl`.

**Steps**

1. Count the Messages currently visible in the transcript and write the number down.
2. Run `(Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl).Count` and write the number down.
3. Click **Continue** and watch the two buttons closely as you do.
4. Wait 10 seconds.
5. Read the area between the transcript and the composer.
6. Count the Messages visible in the transcript again and compare with step 1.
7. Run the same `.Count` command again and compare with step 2.
8. Read `T-A` and count the `was extended to` lines produced by this one click.
9. Try to click **Continue** a second time.

**Pass if — all of these**

- During step 3 the first button's label changes to `Continuing…` and both buttons are disabled while the grant is in flight.
- The red block then disappears and is replaced by a grey line reading exactly `1 of 2 agent replies since you last spoke.`
- The visible Message count is unchanged from step 1 — nothing is rendered twice and the view does not jump to a repeated row.
- The `.jsonl` line count is unchanged from step 2 — a re-delivery is not a post.
- `T-A` shows exactly ONE `Room '…' was extended to 2 agent messages.` line for the click.
- At step 9 there is no **Continue** button left to click.

**Fail if — any of these**

- Two `was extended to` lines appear from one click -> the busy flag is not holding, so a double-click grants two Budgets and an unattended Room can spend twice what the Human allowed.
- The granted figure jumps by more than one Budget (for example to `1 of 41` under a budget of 1) -> Continue is lifting the cap rather than granting one more allowance, so the runaway guard is gone for the rest of the run.
- The last Message appears a second time in the transcript -> a view component is subscribed to the re-delivery event, which only the Agent-delivery layer may ever subscribe to; every Continue will then visibly duplicate history.
- A new line appears in the `.jsonl` -> the re-delivery is being persisted as a new Message, which will itself count against the Budget.

**Inconclusive if**

If nothing at all changes after the click — no label change, no log line — the click did not reach the server; check `T-A` for an error and retry once. Whether any Agent actually wakes up and replies here is NOT part of this test: with demo agents it usually will not, for the reason set out in REPLYGATEBUDGET-26. Judge this test only on the button behaviour, the note, the log line and the two counts.

> [!NOTE]
> Under a budget of 1 the note after a single grant must read `1 of 2`. If your run used a different budget N, expect `N of 2N`.

### REPLYGATEBUDGET-13 — Any Human Message resets the Budget, is never refused, and expires an earlier grant

**Free** · about 6 min

*Proves the Human can always speak in their own Room, that speaking clears the pause, and that a Budget previously granted with Continue does not persist past the next Human Message.*

**Before you start**

- REPLYGATEBUDGET-12 has just passed: the Room shows `1 of 2 agent replies since you last spoke.` under a run whose configured budget is `1`.

**Steps**

1. Type a single full stop `.` and press Enter.
2. Read the transcript and the area between it and the composer.
3. Type `@echo hi` and press Enter and wait 5 seconds.
4. Read the area above the composer. Under a configured budget of `1` that one reply spends the
   whole allowance, so what you see is the pause block again, which does not print the Granted
   figure. Click **Leave paused** to read it: that only hides the question (REPLYGATEBUDGET-09) and
   grants nothing.
5. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl | Select-Object -Last 3`.
6. Read `T-A` for any warning produced by your own two Messages.

**Pass if — all of these**

- The `.` posts normally, appears in the transcript with sender `You`, and is never refused.
- Immediately after step 1 the pause block and the grey budget line are both gone.
- After `echo` replies at step 3, the line read at step 4 reads `Paused — 1 of 1 agent replies since you last spoke.` — the allowance is back to the configured default, NOT `1 of 2`.
- The tail of the `.jsonl` shows your `.` line with `"senderId":"human"`.
- `T-A` shows no refusal warning naming the Human.

**Fail if — any of these**

- The Human's Message is refused, or shows an error strip -> the cap is being applied to Human Messages, which locks the Human out of their own Room with no way back in.
- The pause block survives the Human's Message -> the reset is not clearing the pause, so the Room stays visibly paused while actually accepting replies (or worse, stays paused in fact).
- The grey line reads `1 of 2` after step 3 -> a Budget the Human granted for one pause is persisting past the conversation it was granted for, so each Continue permanently raises the Room's ceiling.

**Inconclusive if**

If no reply arrives at step 3, you cannot read the Granted figure at all: confirm `echo` is online and repeat. If the configured budget for this run is not `1`, substitute it: the pass condition is that Granted returns to the CONFIGURED value, whatever that is.

> [!NOTE]
> This is the escape hatch for every pause: anything the Human says, even a full stop, resumes the Room.

### REPLYGATEBUDGET-14 — The Budget is per Room: a paused Room does not starve the one beside it

**Free** · about 6 min

*Proves one looping conversation cannot pause an unrelated one, and that switching Rooms in the sidebar does not carry the previous Room's figures across.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A and `$env:Team__AgentMessageBudget = '1'`.
3. Open `http://localhost:5100` and click the Room named `echo`.
4. Type `@echo hi` and press Enter, then wait until the red pause block appears.
5. Click the Room named `alpha` in the sidebar.
6. Read the area between the transcript and the composer in this Room.
7. Type `@alpha hi` and press Enter and wait 5 seconds.
8. Click back to the Room named `echo` in the sidebar.
9. Read the area between the transcript and the composer again.

**Pass if — all of these**

- In the `alpha` Room at step 6 there is no pause block and no budget line at all.
- `alpha` replies immediately at step 7, and only afterwards does that Room show its own `1 of 1 …` state.
- Back in the `echo` Room at step 9, the red pause block is still there with its own figures.

**Fail if — any of these**

- The `alpha` Room shows a pause block or a budget line before anyone has spoken in it -> the counter is global, so two unrelated conversations starve each other and the Human cannot tell which one was looping.
- `@alpha hi` gets no reply while `alpha` is online -> the other Room's pause is suppressing this one.
- The figures shown in `alpha` match the ones from `echo` for a moment after the switch -> the view is rendering one Room's state under another Room's name, which makes every Budget reading untrustworthy during navigation.

**Inconclusive if**

If `alpha` never connected there is no `alpha` Room to switch to — check `T-A` for `Demo agent alpha connected.` and restart from RESET. If both Rooms happen to have the same name after earlier invites, use the Room ids in the URL to tell them apart.

> [!NOTE]
> Sidebar switching is deliberately fast here; a stale-state bug shows as a flash of the wrong figures rather than a permanent wrong reading, so watch the moment of the switch.

### REPLYGATEBUDGET-15 — A restart un-pauses every Room over a Transcript that already spent its Budget

**Free** · about 5 min

*Documents deliberate behaviour a tester will otherwise file as a bug, and pins the real defect that would be its opposite: a Room stuck paused forever with no way to clear it.*

**Before you start**

- A Room is paused with the red block showing.
- You know the Room id.

**Steps**

1. Note the exact text of the pause block and the number of Messages in the transcript.
2. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl` and count the agent lines after the last `"senderId":"human"` line.
3. In `T-A` press Ctrl+C to stop the app.
4. Start the app again with exactly the same PROFILE A command and the same budget value. Do NOT run RESET.
5. Open `http://localhost:5100` and navigate to the same Room (its id is unchanged).
6. Read the transcript and the area between it and the composer.

**Pass if — all of these**

- The transcript is fully intact — the same Messages, in the same order, as at step 1.
- There is NO pause block and NO budget line: the Room reads as though no agent has spoken since the Human last did.
- The `.jsonl` still holds the agent lines counted at step 2, so the file and the view deliberately disagree.

**Fail if — any of these**

- The Room is still paused after the restart and no Human Message will clear it -> the counter has been persisted without a way to reset it, which locks a Room permanently.
- The transcript has lost Messages -> the Transcript file is not being read back on load, which is a data-loss defect unrelated to the Budget.
- A pause block appears with figures that do not match anything in the transcript -> stale state is being restored incorrectly.

**Inconclusive if**

If the Room id changes after the restart you ran RESET by mistake, or an Agent re-registered into a new Room — compare against the id noted at step 1 and repeat without RESET.

> [!NOTE]
> DO NOT FILE the missing pause as a defect. The Budget counter is in memory and per Room by design, the same trade made for the Catch-up buffer. This test exists so a tester recognises it on sight.

### REPLYGATEBUDGET-16 — A Budget of zero removes the cap entirely

**Free** · about 6 min

*Proves the documented escape hatch works in both directions: no pause ever appears, and no agent Message is ever refused.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A but set `$env:Team__AgentMessageBudget = '0'` before `dotnet run`.
3. Open `http://localhost:5100`, click the Room named `echo`, click **Add teammate**, click `alpha`, then close the panel.
4. Type `@echo @alpha go` and press Enter, then wait 5 seconds.
5. Repeat step 4 five more times, waiting 5 seconds each time.
6. Read the area between the transcript and the composer after every round.
7. Read `T-A` for the whole run.
8. Stop the app, and restart it with `$env:Team__AgentMessageBudget = '-1'`, then repeat steps 3 to 7.

**Pass if — all of these**

- Every round produces two agent replies.
- No grey budget line and no red pause block ever appears, at any point, in either run.
- `T-A` contains no `refused a message from` line and no `was extended to` line, in either run.
- The negative value behaves identically to zero.

**Fail if — any of these**

- A pause block appears -> the uncapped guard has been lost and the documented way back to un-guarded behaviour no longer works.
- No agent Message is ever accepted and `T-A` fills with refusals -> zero is being read as 'no replies allowed', which silently mutes every Agent in every Room while looking like a configuration that merely turns the cap off.
- A grey `0 of 0 …` line appears -> the note is rendering for an uncapped Room, which is noise the design deliberately suppresses.

**Inconclusive if**

If replies stop arriving mid-run, check the Agents are still connected in **Add teammate** before concluding anything: a disconnected demo agent produces the same silence as a refusal but leaves no warning in `T-A`.

> [!NOTE]
> This setting exists only as configuration — there is no control for it on /settings or in any Room, and it is bound at startup, so a change needs a restart. Never leave it at 0 for a paid run: with real Personas it restores unbounded spend.

### REPLYGATEBUDGET-17 — The model-facing wording of this area is readable and editable at /settings/hooks

**Free** · about 8 min

*Proves the text that tells a model when to answer and what a refusal means is visible to the Human, that an override file is written only on a save, and that the fields which need a restart to take effect are badged as such.*

**Before you start**

- The app is running under PROFILE A.
- No `hooks.json` exists yet: confirm with `Test-Path E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json` — it should print `False` on a fresh install.

**Steps**

1. Run `Test-Path E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json` and note the result.
2. Click **Settings** in the sidebar.
3. Confirm the **Hooks** tab is the one already selected, then find the field labelled **Chat rules**.
4. Read its default text.
5. Find the fields labelled **Help: Rooms**, **Help: mentions**, **Help: replying** and **Help: budget** and read the default text of **Help: budget**.
6. Look for a small badge beside the **Chat rules** label and hover it.
7. Look for the same badge beside **Help: budget**.
8. Append the word ` TESTEDIT` to the end of the **Chat rules** text.
9. Note the badge that appears beside the field once it is edited, then click **Save**.
10. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json`.
11. Remove ` TESTEDIT` again and click **Save**.

**Pass if — all of these**

- Step 1 prints `False` — no override file exists until a save.
- **Chat rules** default text begins `A Room with two members is a private conversation with the human: answer every message. A Room with three or more members is a group: answer only when you are @-mentioned.`
- **Help: budget** default text contains the sentence `A refusal that says the budget is spent is final: do not retry it, and do not work around it by posting to another Room.`
- **Chat rules** carries a badge reading `Next session`, whose tooltip says it applies to teammates started after the change.
- **Help: budget** carries no `Next session` badge.
- After the edit and before saving, the field shows an `Unsaved` badge; the **Save** button is enabled only while there is something to save.
- After saving, `hooks.json` exists and contains ONLY the one changed key — not the full set of defaults.

**Fail if — any of these**

- `hooks.json` exists before you have ever saved -> absent no longer means 'nothing overridden', so a future default change would never reach an installed system.
- `hooks.json` contains every hook after saving one -> the same problem: every default is frozen at whatever shipped the day of the first save.
- **Chat rules** has no `Next session` badge -> the Human is given no signal that their edit will not reach a running Teammate until it restarts, so an edit that silently never takes effect looks like a broken hook.
- A `Next session` badge appears on the `Help:` fields -> the badge is decoration rather than a statement about timing, which makes it worthless on the fields that need it.

**Inconclusive if**

If the Hooks tab shows no fields at all, the hook catalog failed to load — check `T-A` for an error and stop. If `hooks.json` already existed at step 1 from a previous test, the 'only on first save' half of this test is inconclusive: delete the file, restart the app and begin again.

> [!NOTE]
> Always undo the edit (step 11). Leaving `TESTEDIT` in the Chat rules hook changes what every real Persona is told in every later paid test.

### REPLYGATEBUDGET-18 — The server labels a delivery correctly even when the client chooses to stay silent

**Free** · about 8 min

*Establishes the wire-envelope oracle that every remaining free test depends on, and separates 'the label was wrong' from 'the client decided not to act' once and for all.*

**Before you start**

- No app running.
- PowerShell 7 available as `pwsh`.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B: `$env:Team__Acp__Enabled = 'false'`, `$env:Team__DemoAgent__Enabled = 'false'`, `$env:Team__AgentMessageBudget = '40'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open a second PowerShell terminal — call it `T-B` — and run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
4. Confirm `T-B` prints `Sent hello. Listening for messages…`.
5. Open `http://localhost:5100`; a Room named `echo` appears in the sidebar with no refresh. Click it.
6. Type `hello` and press Enter.
7. Read the newest JSON line printed in `T-B`.
8. Type `hello @echo` and press Enter.
9. Read the newest JSON line printed in `T-B` and the Room view.

**Pass if — all of these**

- A Room named `echo` appears in the sidebar within a few seconds of starting the bot, without a page refresh.
- The envelope from step 7 has `"type":"messagePosted"`, `"mentioned":false`, a `"members"` array of exactly two entries, `"agentMessagesSinceHuman":0` and `"budget":40`.
- Nothing appears in the Room after step 6 — the bot received a correct label and chose not to act.
- The envelope from step 9 has `"mentioned":true` and a `"mentions"` array of exactly one entry naming `echo`.
- A reply from `echo` appears in the Room after step 9.

**Fail if — any of these**

- The step-7 envelope carries `"mentioned":true` -> the server is labelling an un-named recipient as Mentioned; every Agent in every group Room would be woken by every Message.
- The `"members"` array is shorter or longer than the Room's actual membership -> the Reply Gate's Room rule is being fed the wrong count, so a group Room can behave as a private one or vice versa.
- `"budget"` does not match the configured value -> clients are being told the wrong allowance, and an Agent that implements the gate will stop too early or too late.
- No envelope at all is printed at step 7 -> the bot is not a Member or not connected; that is an infrastructure failure, not a labelling one.

**Inconclusive if**

If `T-B` prints a line containing `"code":"invalidName"`, the Name was rejected — check for a leading or trailing space in what you typed. If the bot exits with a connection error, the app is not running or its pipe name differs: confirm the app started before the bot. If a Room named `echo` was already there from the built-in demo agent, `Team__DemoAgent__Enabled` was not set to `'false'`: stop everything, RESET and start again, or you will have two Agents fighting over one Name.

> [!NOTE]
> Keep `T-B` open; the next several tests reuse this exact oracle. The silence at step 6 is correct client behaviour, not a gate defect.

### REPLYGATEBUDGET-19 — The longest handle wins: @Emily Lee reaches Emily Lee, never Emily

**Free** · about 12 min

*Catches the headline silent failure of this area — a Mention of a two-word Name being read as a Mention of the one-word Name plus a stray word — and proves a multi-word Name survives an /invite.*

**Before you start**

- No app running.
- PowerShell 7 available.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Emily`.
4. In a third terminal — TERMINAL C — run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name "Emily Lee"` (the quotes matter).
5. Open `http://localhost:5100`. Confirm the sidebar shows two Rooms, `Emily` and `Emily Lee`.
6. Click the Room named `Emily`.
7. Type `/invite @Emily Lee` and press Enter.
8. Read the strip above the composer, and the Room heading.
9. Type `@Emily Lee any news?` and press Enter, then wait 5 seconds.
10. Read the newest line in `T-B` and the newest line in TERMINAL C, and read the Room.
11. Type `@Emily any news?` and press Enter, then wait 5 seconds, and read both terminals again.
12. Type `@emily lee any news?` and press Enter, then wait 5 seconds, and read both terminals again.

**Pass if — all of these**

- Step 8 shows a GREEN strip reading exactly `Invited Emily Lee. Room is now "Emily, Emily Lee".` and the heading renames to `Emily, Emily Lee`.
- At step 10 exactly one reply appears, from `Emily Lee`; TERMINAL C's envelope has `"mentioned":true` and `T-B`'s copy of the SAME envelope has `"mentioned":false`.
- At step 11 exactly one reply appears, from `Emily`; the two terminals' labels are the other way round.
- At step 12 exactly one reply appears, from `Emily Lee` — the match is case-insensitive across the space.

**Fail if — any of these**

- `Emily` replies to `@Emily Lee any news?` -> the parser is reading the shorter Name and treating `Lee` as ordinary text; every Mention of a two-word Name in a Room that also holds the one-word Name reaches the wrong person, silently and with no error.
- BOTH reply to `@Emily Lee any news?` -> the scan did not advance past the handle it consumed, so the words inside a multi-word Name are being re-read as further Mentions; with real Personas that is a billed Turn for someone who was never addressed.
- Step 7 produces a red strip reading `Unknown agent @Emily` -> the invite command truncated the Name at the first space, so no Teammate whose Name contains a space can ever be invited from the composer.
- Nobody replies to `@emily lee any news?` while `@Emily Lee any news?` works -> the multi-word match is case-sensitive.

**Inconclusive if**

If either terminal prints `"code":"invalidName"`, the Name was rejected (a doubled or trailing space) — retype it exactly. If only one Room appears in the sidebar, one bot never connected; fix that before judging anything. If both replies would look alike in the Room, trust the terminals: exactly one envelope must carry `"mentioned":true` and its `"mentions"` array names the winner.

> [!NOTE]
> This is the behaviour that forced membership, rather than a pattern, to decide where a Name ends: `@Emily Lee` and `@Emily` followed by the word `Lee` are the same characters.

### REPLYGATEBUDGET-20 — A Mention falls back to the shorter Name when the longer one is not in this Room

**Free** · about 9 min

*Proves longest-first does not mean all-or-nothing: a handle that looks like the start of a longer Name still resolves to the Member who is actually present.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Emily`.
4. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
5. Open `http://localhost:5100` and click the Room named `Emily`.
6. Type `/invite @echo` and press Enter; confirm the heading becomes `Emily, echo` so the Room is Mention-gated.
7. Confirm the member line names three people and that `Emily Lee` is NOT one of them.
8. Type `@Emily Lee any news?` and press Enter, then wait 5 seconds.
9. Read `T-B`'s newest line and the Room.

**Pass if — all of these**

- Exactly one reply appears, from `Emily`.
- `T-B`'s envelope for that Message carries `"mentioned":true`.
- TERMINAL C's copy of the same envelope carries `"mentioned":false` and `echo` stays silent.
- The trailing word `Lee` is simply part of the text and causes nothing.

**Fail if — any of these**

- Nobody replies -> the parser refused to match because a longer handle looked like it should have won, so a Mention that names a real Member of this Room resolves to nobody with no error anywhere: the Human sees only silence.
- `echo` replies -> the Mention resolved to the wrong Member.
- An error strip appears -> an unresolvable-looking Mention is being treated as an error rather than as text.

**Inconclusive if**

If an `Emily Lee` bot from REPLYGATEBUDGET-19 is still running, it may have been invited into this Room by accident — check the member line at step 7 and, if it names `Emily Lee`, RESET and start over with only the two bots this test asks for. Its merely being connected (but not a Member) is fine and does not invalidate the test.

> [!NOTE]
> Aliases and Names only ever resolve against the Members of THIS Room, which is exactly why the longer reading can lose here.

### REPLYGATEBUDGET-21 — Two Mentions in one Message do not run together, and a multi-word Name stops at punctuation

**Free** · about 10 min

*Proves a Message naming two people reaches both, that an ordinary word after a multi-word Name is not swallowed into it, and that punctuation ends a Name containing spaces.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name "Chief of Staff"`.
4. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
5. Open `http://localhost:5100`, click the Room named `Chief of Staff`, type `/invite @echo` and press Enter.
6. Confirm the heading is now `Chief of Staff, echo` and the member line names three people.
7. Type `@Chief of Staff and @echo` and press Enter, then wait 6 seconds.
8. Read both terminals and the Room.
9. Type `thanks @Chief of Staff!` and press Enter, then wait 5 seconds, and read both terminals.
10. Type `@Chief of Staff.` and press Enter, then wait 5 seconds, and read both terminals.

**Pass if — all of these**

- Step 7 produces exactly TWO replies, one from `Chief of Staff` and one from `echo`; the single envelope printed in both terminals carries a `"mentions"` array of two entries, and each terminal's own copy shows `"mentioned":true`.
- Step 9 produces exactly ONE reply, from `Chief of Staff`.
- Step 10 produces exactly ONE reply, from `Chief of Staff`.
- `echo` never replies at steps 9 or 10.

**Fail if — any of these**

- Only `Chief of Staff` replies at step 7 -> the scan overshot past the second handle, so a Message addressed to two people reaches one, silently.
- Nobody replies at step 7 -> the word `and` was eaten into the multi-word Name, so neither handle matched.
- Step 9 or 10 produces no reply -> trailing punctuation is being absorbed into a Name that contains spaces, so `@Chief of Staff.` at the end of a sentence never reaches anyone.

**Inconclusive if**

If `T-B` prints `"code":"invalidName"`, the quoted Name was mistyped — it must be `"Chief of Staff"` with single spaces. If only one terminal is connected, this test cannot distinguish 'the second Mention did not resolve' from 'the second Agent is not there'.

> [!NOTE]
> With a real Persona this behaviour additionally depends on the model writing another Member's Name out in full, which no automated test can prove; that half is only ever observable in a paid run.

### REPLYGATEBUDGET-22 — A Mention ends at a word boundary, and an email address is not a Mention

**Free** · about 12 min

*Prevents the expensive false positive — waking a Teammate every time someone pastes an email address or writes a longer word starting with their Name — without over-blocking the legitimate case.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name ech`.
4. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name example`.
5. In a fourth terminal — TERMINAL D — run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
6. Open `http://localhost:5100`, click the Room named `ech`, type `/invite @example` and press Enter, then type `/invite @echo` and press Enter.
7. Confirm the heading reads `ech, example, echo` and the member line names four people.
8. Type `@echo` and press Enter, wait 5 seconds, then read all three terminals and the Room.
9. Type `mail me@example.com` and press Enter, wait 5 seconds, then read all three terminals and the Room.
10. Type `see-@echo` and press Enter, wait 5 seconds, then read all three terminals and the Room.
11. Type `you@@echo` and press Enter, wait 5 seconds, then read all three terminals and the Room.

**Pass if — all of these**

- Step 8: exactly one reply, from `echo`. `T-B` (`ech`) shows `"mentioned":false` for that envelope.
- Step 9: no reply at all. TERMINAL C (`example`) shows `"mentioned":false`.
- Step 10: exactly one reply, from `echo`. TERMINAL D shows `"mentioned":true` — a hyphen before the `@` is deliberately not a blocker.
- Step 11: no reply at all. TERMINAL D shows `"mentioned":false`.

**Fail if — any of these**

- `ech` replies to `@echo` -> a Mention is not ending at a word boundary, so any Teammate whose Name is a prefix of another is woken by Mentions meant for the longer one; with a real Persona that is a billed Turn for nothing.
- `example` replies to `mail me@example.com` -> every pasted email address wakes a Teammate. This is the most expensive false positive in the product.
- `see-@echo` gets no reply -> `-` was wrongly added to the blocked set, so a legitimate Mention after a hyphen silently reaches nobody.
- `you@@echo` produces a reply -> the doubled `@` is not being treated as joining two things, so text like an escaped handle wakes an Agent.

**Inconclusive if**

If any of the three bots failed to connect (no Room appears for it, or its terminal shows an error), the corresponding step proves nothing — an Agent that receives no envelope is silent for the wrong reason. Check that all four names appear on the member line at step 7 before judging any step.

> [!NOTE]
> All four steps must be read from the terminals, not from the Room: 'no reply' in the Room is the same picture for a correct non-match and for a dead bot.

### REPLYGATEBUDGET-23 — An Agent never receives its own Message, so no self-echo loop can start

**Free** · about 8 min

*Proves the loop guard that makes every other test in this area safe to run: a quoting Agent cannot be re-triggered by its own reply.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
4. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name alpha`.
5. Open `http://localhost:5100`, click the Room named `echo`, type `/invite @alpha` and press Enter, and note the Room id from the URL.
6. Type `@echo hi` and press Enter.
7. Wait 30 seconds without touching anything.
8. Read `T-B` carefully: find the envelope for your own Message, and look for any envelope carrying `echo`'s own reply text.
9. Read TERMINAL C.
10. Run `(Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl).Count`, wait 20 seconds, and run it again.

**Pass if — all of these**

- Exactly one reply appears in the Room, from `echo`, and no further Messages arrive during the 30-second wait.
- `T-B` contains NO `messagePosted` envelope carrying `echo`'s own reply text — the sender is never delivered its own Message.
- TERMINAL C DOES contain that envelope, with `"mentioned":false`.
- The two line counts in step 10 are identical.
- `echo`'s reply text contains no `@` character.

**Fail if — any of these**

- The line count keeps rising and Messages keep arriving -> an Agent is being delivered its own Message, or the reply still carries a Mention; this is the unbounded self-echo that once produced thousands of messages in seconds. STOP THE APP IMMEDIATELY with Ctrl+C in `T-A` and in every bot terminal.
- `T-B` shows an envelope containing its own reply -> the sender-skip on delivery is gone; only the demo client's `@`-stripping is preventing a loop, and a client that quoted faithfully would loop forever.
- TERMINAL C shows no envelope for `echo`'s reply -> other Members are not being delivered an Agent's Message at all, which breaks group conversation entirely.

**Inconclusive if**

If neither bot replies, nothing is being exercised — get a reply working (REPLYGATEBUDGET-18) first. If the Room has only two Members, `alpha` never joined; check the member line.

> [!NOTE]
> If this test fails in the runaway direction, treat it as urgent and stop the run before doing anything else, including reading logs.

### REPLYGATEBUDGET-24 — The refusal an over-Budget Agent reads is worded as terminal, on the pipe door

**Free** · about 8 min

*Proves the exact sentence sent back to a refused client reads as final rather than as something worth retrying — wording is the only thing stopping a model from spending the very Turn the refusal exists to save.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B but set `$env:Team__AgentMessageBudget = '1'` before `dotnet run`.
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
4. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name alpha`.
5. Open `http://localhost:5100`, click the Room named `echo`, type `/invite @alpha` and press Enter.
6. Type `@echo @alpha go` and press Enter.
7. Wait 10 seconds.
8. Read both terminals and find the line whose `"type"` is `error` — that is the wire discriminator a `ProtocolError` serialises to (`Messages.cs`: `[JsonDerivedType(typeof(ProtocolError), "error")]`), so grepping for `protocolError` finds nothing.
9. Copy that line's `"message"` value out in full and compare it character for character with the sentence in the pass conditions.
10. Read `T-A`.

**Pass if — all of these**

- Exactly one of the two bot terminals prints a `protocolError` envelope; the other prints none.
- That envelope carries `"code":"budgetExhausted"`.
- Its `"message"` reads exactly: `This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.`
- `T-A` shows exactly one `refused a message from` warning naming the same Agent.
- Exactly one agent Message is visible in the Room.

**Fail if — any of these**

- The message says anything like 'try again later', 'temporarily', or names a wait -> a model will read it as transient and retry, spending the Turn the refusal exists to save, and may route around it into another Room which has a full Budget of its own.
- No `"code"` field, or a different code -> a client cannot distinguish a Budget refusal from an unknown Room or a malformed Message, so it cannot stop for the right reason.
- Both terminals print a `protocolError` -> neither post was accepted, meaning the Room was already at its cap before your Message reset it.
- Neither prints one, yet only one Message landed -> the second Agent never attempted a post, so nothing was actually refused (see INCONCLUSIVE).

**Inconclusive if**

If only one bot is connected, only one post is attempted and no refusal can occur: confirm both Rooms appeared in the sidebar and both terminals printed `Sent hello`. If both replies landed, the budget env var did not take effect — check the value printed nowhere but confirmable by the pause block appearing; restart with `$env:Team__AgentMessageBudget = '1'` set BEFORE `dotnet run`.

> [!NOTE]
> The same sentence must also come back from the Agent-facing post tool; that second door is covered in the paid REPLYGATEBUDGET-35, because only a real model can be observed obeying it.

### REPLYGATEBUDGET-25 — Continue re-delivers the last Message to the other Agents, once, with the raised Budget

**Free** · about 9 min

*Proves granting Budget actually wakes the Room rather than only raising a number nothing reads, that the re-delivery carries the new allowance, and that the sender is still skipped.*

**Before you start**

- REPLYGATEBUDGET-24 has just been run: the app is on PROFILE B with budget `1`, bots `echo` and `alpha` are connected, and the Room `echo, alpha` shows the red pause block.

**Steps**

1. In the Room, note the exact text of the last Message and which Agent sent it.
2. Note the Room id from the URL and run `(Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl).Count`.
3. Count the Messages visible in the transcript.
4. Click **Continue**.
5. Wait 10 seconds.
6. Read the terminal belonging to the Agent that did NOT send the last Message.
7. Read the terminal belonging to the Agent that DID send it.
8. Read `T-A`.
9. Run the `.Count` command again and count the visible Messages again.

**Pass if — all of these**

- The non-sender's terminal prints a SECOND `messagePosted` envelope whose `"message"` object carries the SAME `"id"` as the first copy.
- That second envelope carries `"budget":2` — the raised allowance, not the configured default.
- The sender's terminal prints no envelope for its own Message at any point.
- `T-A` shows exactly one `Room '…' was extended to 2 agent messages.` line.
- Neither the `.jsonl` line count nor the visible Message count changed.

**Fail if — any of these**

- No terminal shows a second envelope -> Continue only raised a number; nothing will ever read it, so a paused Room stays stopped and the button does nothing the Human can see.
- The second envelope carries the OLD budget figure -> a client that implements the gate will decline it again immediately, so the extension is inert even though the re-delivery went out.
- The Message appears a second time in the transcript, or the `.jsonl` gains a line -> the re-delivery is being treated as a new post; it would then count against the Budget and be shown twice to the Human.
- The sender's terminal also receives the copy -> the skip-the-sender rule is gone, which reopens the self-echo loop at every Continue.

**Inconclusive if**

Whether the woken Agent actually replies depends on the re-parsed Mentions in that Message: the demo bots strip `@` from what they quote, so the re-delivered Message usually names nobody and the woken bot correctly stays quiet. Judge this test on the envelope, the log line and the two counts — never on whether a new reply appears.

> [!NOTE]
> The Mentions are re-parsed from the Message text at Continue time rather than remembered, which is why the same Agent that was woken originally is the one woken again.

### REPLYGATEBUDGET-26 — Continue in a two-Member Room grants Budget and wakes nobody — and that is correct

**Free** · about 7 min

*Inoculates the tester against the most common false bug report in this area ('Continue does nothing') by showing the grant in the log while nothing visible happens.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B and `$env:Team__AgentMessageBudget = '1'`.
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
4. Open `http://localhost:5100` and click the Room named `echo`. Confirm the member line reads `You, echo` — two Members only.
5. Type `@echo hi` and press Enter and wait for the reply and the red pause block.
6. Note the Message count in the transcript.
7. Click **Continue** and wait 15 seconds.
8. Read the Room, `T-B` and `T-A`.

**Pass if — all of these**

- BEFORE clicking at step 7, the red pause block carries an extra grey line reading `Continue will
  deliver the last message again, and it is a teammate's own - a teammate is never delivered its
  own message, so this will wake nobody. Say something instead.` The Human is told the button
  cannot work BEFORE pressing it, which is the whole of product observation 5.
- `T-A` shows exactly one `Room '…' was extended to 2 agent messages.` line — the grant definitely happened.
- AFTER the click, a quiet blue note reads `Budget granted, but there was nobody to wake. Say
  something to start the room again.`
- The red block is replaced by a grey line reading `1 of 2 agent replies since you last spoke.`
- `T-B` prints NO new envelope: the last Message in the Room was `echo`'s own, and an Agent is never delivered its own Message.
- No new Message appears in the Room and the transcript count is unchanged.

**Fail if — any of these**

- No `was extended to` line appears in `T-A` -> Continue genuinely did nothing: the grant itself is broken.
- `T-B` prints a copy of `echo`'s own reply -> the sender-skip is gone.
- A new Message appears in the Room without any Agent having been woken -> something other than an Agent is posting.

**Inconclusive if**

If the grey line does not update to `1 of 2` but the log line is present, the view is not re-reading the Budget after the grant — note it and re-check with REPLYGATEBUDGET-11's reload path before filing, since a reload will show the true figure.

> [!NOTE]
> DO NOT FILE 'Continue does nothing' from this test. It did something — there was simply nobody
> left to wake. As of #40 the Room says so itself, before the click and after it, so the log line
> is no longer the only oracle — but it is still the authoritative one, and a missing
> `was extended to` line is a real defect even if the notes render correctly.

### REPLYGATEBUDGET-27 — An Alias resolves a Mention to the Persona that owns it

**Free** · about 12 min

*Proves the short handle reaches the same Member the full Name does, and that it stops at the same boundaries — a silently unresolved Alias looks exactly like an offline Agent.*

**Before you start**

- No app running.
- No Persona files that would clash: `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\` should be empty or contain no `Jarvis.md`.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (`Team__Acp__Enabled` MUST be `false` — no model is needed for this test and none must start).
3. Open `http://localhost:5100`, click **Teammates** in the sidebar, and click **New teammate**.
4. Fill in Name `Jarvis`, Title `Butler`, Alias `jar`, leave Teams blank, and type `You are Jarvis.` into the Persona body.
5. Leave Model and Effort at `Use the agent's default` and click **Add teammate**.
6. Confirm a tile named `Jarvis` now appears on the Teammates page and that the **Files that didn't load** section either is absent or does not list `Jarvis.md`.
7. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Jarvis`.
8. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo`.
9. Click the Room named `Jarvis` in the sidebar, type `/invite @echo` and press Enter so the Room has three Members.
10. Type `@jar do the thing` and press Enter, wait 5 seconds, then read `T-B` and the Room.
11. Type `@Jarvis do the thing` and press Enter, wait 5 seconds, then read `T-B` and the Room.
12. Type `x@jar` and press Enter, wait 5 seconds, then read `T-B`.
13. Type `@jarring` and press Enter, wait 5 seconds, then read `T-B`.
14. Click the Room named `echo` in the sidebar, type `/invite @jar` and press Enter, and read the strip.

**Pass if — all of these**

- Step 10 produces exactly one reply, from `Jarvis`; `T-B`'s envelope carries `"mentioned":true` and its `"mentions"` array names `Jarvis`, NOT `jar`.
- Step 11 behaves identically — the Alias and the Name are interchangeable.
- Steps 12 and 13 each produce `"mentioned":false` in `T-B` and no reply.
- Step 14 shows a green strip reading `Invited jar. Room is now "echo, Jarvis".` — an Alias is accepted anywhere a Name is.

**Fail if — any of these**

- `@jar` reaches nobody while `@Jarvis` works and the Persona file loaded cleanly -> Aliases are not being folded into Mention resolution at all, so every shorter handle silently reaches nobody.
- `@jarring` or `x@jar` wakes `Jarvis` -> an Alias is not respecting word boundaries, which is the same expensive false positive as REPLYGATEBUDGET-22 with a shorter, more collision-prone handle.
- The `"mentions"` array names `jar` rather than `Jarvis` -> the wire is carrying something that is not a Member, which no client can resolve.
- Step 14 reports `Unknown agent @jar` -> the invite path does not resolve Aliases, so the two ways of naming a Teammate disagree.

**Inconclusive if**

If `Jarvis.md` appears under **Files that didn't load**, read the reason there and fix it — the Alias cannot resolve from a file that did not load, and the test proves nothing. If the pipe client's Name and the Persona Name do not match exactly (`Jarvis`), the Alias has no Member to point at and its silence is CORRECT, not a defect: Aliases are library-wide but resolve only against the Members of the Room. If a real model process starts, `Team__Acp__Enabled` was not `false` — Ctrl+C immediately.

> [!NOTE]
> Delete `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Jarvis.md` when finished, or later tests will see an extra Persona.

### REPLYGATEBUDGET-28 — A longer Alias beats a shorter Name, and an equal-length Name beats an Alias

**Free** · about 15 min

*Pins two tie-breaks that nothing in the compiler protects and whose failure is a silent mis-routing to the wrong Teammate.*

**Before you start**

- No app running.
- `App_Data\Teams\` contains no files from earlier tests.

**Steps**

1. Run `P-RESET-ROOMS` and delete any leftover files in `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\`.
2. Start the app with PROFILE B (`Team__Acp__Enabled` MUST be `false`).
3. CASE A. On **Teammates**, click **New teammate** and create Name `Other`, Title `Someone else`, Alias `Emily Lee`, body `You are Other.`, then click **Add teammate**.
4. Confirm `Other.md` is not listed under **Files that didn't load**.
5. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Emily`; in TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Other`.
6. Click the Room named `Emily`, type `/invite @Other` and press Enter, and confirm three Members.
7. Type `@Emily Lee any news?` and press Enter, wait 5 seconds, then read both terminals and the Room.
8. CASE B. On **Teammates**, click **New teammate** and create Name `Jarvis`, Title `Butler`, Alias `Jar`, body `You are Jarvis.`, then click **Add teammate**.
9. In a fourth terminal — TERMINAL D — run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Jar`; in a fifth — TERMINAL E — run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name Jarvis`.
10. Click the Room named `Jar`, type `/invite @Jarvis` and press Enter, and confirm three Members.
11. Type `@Jar are you there?` and press Enter, wait 5 seconds, then read TERMINAL D and TERMINAL E and the Room.

**Pass if — all of these**

- CASE A: exactly one reply, from `Other`. TERMINAL C shows `"mentioned":true`; `T-B` (`Emily`) shows `"mentioned":false`. The longer Alias beat the shorter Name.
- CASE B: exactly one reply, from `Jar`. TERMINAL D shows `"mentioned":true`; TERMINAL E (`Jarvis`) shows `"mentioned":false`. The equal-length Name beat the Alias.

**Fail if — any of these**

- CASE A routes to `Emily` -> Names and Aliases are being matched in two separate passes rather than sorted together, so a short Name that happens to sit at a word boundary steals a longer Alias's Mention. Silent: the wrong Teammate simply answers.
- CASE B routes to `Jarvis` -> the tie between an equal-length Name and Alias is being won by the Alias; a Teammate can then be shadowed out of their own Name by somebody else's shorthand. Also silent.
- BOTH reply in either case -> the scan did not advance past the handle it consumed.

**Inconclusive if**

If either Persona file is listed under **Files that didn't load**, read the reason: an Alias that duplicates another Persona's Name is rejected by design, so Case B cannot be built from two Persona files — it needs a pipe client named `Jar` (as written) alongside a Persona `Jarvis`. If the bot Names do not exactly match the Persona Names, the Aliases resolve to nobody and both cases are inconclusive. If a Room already contains an unexpected fourth Member from an earlier invite, RESET and rebuild.

> [!NOTE]
> Delete both Persona files from `App_Data\Teams\` when finished. These two orderings are decided by a stable sort over one combined candidate list; there is no compiler or analyser signal if that ordering is ever swapped, which is why they are tested by hand.

### REPLYGATEBUDGET-29 — Reconnecting re-attaches to the same Room, but after an invite a fresh two-Member Room appears

**Free** · about 10 min

*Proves an Agent does not multiply Rooms on every reconnect, and that once its Room has become a group it gets a new private Room rather than silently inheriting the group.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (demo agents OFF, budget `40`).
3. In `T-B` run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name echo` and wait for a Room named `echo` to appear in the sidebar.
4. Count the Rooms in the sidebar and write the number down.
5. In `T-B` press Ctrl+C, then run the same command again and wait 10 seconds.
6. Count the Rooms in the sidebar again.
7. In TERMINAL C run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name alpha`.
8. Click the Room named `echo`, type `/invite @alpha` and press Enter, and confirm the heading becomes `echo, alpha`.
9. In `T-B` press Ctrl+C, then run the `echo` bot command again and wait 10 seconds.
10. Read the sidebar and `T-A`.

**Pass if — all of these**

- At step 6 the Room count is UNCHANGED from step 4 — a plain reconnect re-attaches rather than creating a second Room.
- At step 10 a NEW Room named `echo` appears alongside the Room named `echo, alpha`.
- `T-A` shows `Created direct room '…' for agent 'echo'.` exactly once for that new Room.
- The new Room's member line reads `You, echo` — two Members.

**Fail if — any of these**

- A new Room appears on the plain reconnect at step 5 -> every disconnect multiplies Rooms, and a long-running Agent fills the sidebar with duplicates.
- No new Room appears at step 9 and the Agent re-attaches to the three-Member `echo, alpha` Room -> the exact-members lookup matched a Room that is no longer private, so the Agent's private conversation is silently merged into a group and everything it says there is Mention-gated.
- `T-A` shows more than one `Created direct room` line for one reconnect -> Rooms are being created more than once per registration.

**Inconclusive if**

If the bot fails to reconnect (the terminal shows a connection error), nothing is being tested — confirm the app is still running in `T-A`. If the sidebar does not update without a refresh, note that separately (it is the rooms-changed event, covered in REPLYGATEBUDGET-02) and reload before counting.

> [!NOTE]
> Membership alone defines what a private Room is, which is why the second half of this test has to create a new Room rather than reuse the old one.

### REPLYGATEBUDGET-30 — A Mention that reaches nobody is completely silent — and how to tell that apart from a bug

**Free** · about 8 min

*Teaches the disambiguation every other test in this area depends on, and pins that the silence itself is the design rather than a defect to file.*

**Before you start**

- The app is running under PROFILE B with bots `echo` and `alpha` connected and both in one Room of three or more Members.

**Steps**

1. Type `@nobodyhere hello` and press Enter, then wait 10 seconds and watch the whole page.
2. Read both bot terminals for that Message.
3. Click **Add teammate** and note the colour of the dot beside each Agent, then close the panel.
4. In `T-B` press Ctrl+C to take `echo` offline, and wait 5 seconds.
5. Click **Add teammate** again and note the colour of the dot beside `echo`, then close the panel.
6. Type `@echo are you there?` and press Enter, then wait 10 seconds.
7. Read `T-B` (now stopped) and TERMINAL C.
8. Restart the `echo` bot in `T-B` and repeat step 6.

**Pass if — all of these**

- Step 1's Message posts normally and produces no composer error, no highlight, and no indication
  that the `@` in particular matched nobody. It DOES produce the quiet blue note above the
  composer reading `No teammate was @-mentioned - name one to ask for a reply.` — which is
  correct and is not about the unresolvable handle: the same note appears for a plain
  `hello` in this Room. The product still never reports an unresolvable Mention as a failure.
- At step 6, with `echo` offline and `alpha` online, there is NO note at all. A Teammate WAS
  named, so "no teammate was @-mentioned" would be false; the Room stays silent rather than say
  something untrue. Explaining that `echo` is offline is deliberately not built - see
  `docs/agencyteam/known-limits.md`.
- Both terminals show that envelope with `"mentioned":false`.
- At step 5 the dot beside `echo` is red — `agent-dot offline`, which `theme.css` paints with `--status-offline` (computed `rgb(224, 90, 90)` on the dark theme). There is no grey dot in the design.
- At step 6, with `echo` offline, TERMINAL C still receives the envelope (with `"mentioned":false`) while `T-B` receives nothing at all — the Agent is a Member but not connected, so no Envelope is queued for it.
- After restarting at step 8, the same Message produces a reply.

**Fail if — any of these**

- An error or warning appears for `@nobodyhere hello`, or any note that singles out the
  unresolvable handle -> the product is reporting an unresolvable Mention as a failure, which
  will fire on ordinary prose containing an `@`. The generic context-only note is not this.
- A note appears at step 6, while `echo` is offline and named -> the Room is claiming no Teammate
  was mentioned when one was.
- `@echo are you there?` produces a reply while `echo` is stopped -> something other than the live connection is answering.
- After restarting `echo`, the Messages it missed while offline are delivered to it -> there is no queue in this version by design; a queue appearing would change the whole delivery model.

**Inconclusive if**

This test is itself the disambiguation procedure, so it cannot be inconclusive in the usual sense — but note the rule it teaches: NO envelope in a terminal means the Agent is not a Member or not connected; an envelope with `"mentioned":false` means it IS a Member and the parser decided against it. Never conclude anything about mention resolution without checking which of the two you are looking at.

> [!NOTE]
> DO NOT FILE the silence itself. It is the design: offline Agents receive nothing and there is no queue. But this silence is also the shape every genuine mention-resolution bug takes, which is why every test in this area names a terminal or a file as its oracle.

### REPLYGATEBUDGET-31 — PAID: a real Persona answers a two-Member Room with no Mention at all

**💰 Spends money** · about 15 min

*The only way to observe the Reply Gate's answer-everything branch, because no demo client implements it. Proves a private conversation does not require the Human to @-name their Teammate in every sentence. The New teammate and Edit cards used here are real `MudDialog`s and their Model/Effort dropdowns are `MudSelect` (Stages 3-4 of the MudBlazor migration); neither changes what this test is checking.*

**Before you start**

- No app running.
- You are willing to spend ONE real, billed model Turn.
- `App_Data\Teams\` is empty or contains only the Persona this test creates.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE A settings but `$env:Team__Acp__Enabled = 'false'` still in place, only so you can create the Persona without starting a model.
3. Open `http://localhost:5100`, click **Teammates**, click **New teammate**, and create Name `Ada`, Title `Analyst`, Alias `ada1`, body `You are Ada, a concise analyst. Answer in one short sentence.` Click **Add teammate**.
4. Stop the app with Ctrl+C.
5. Start the app with PROFILE C: `$env:Team__Acp__Enabled = 'true'`, `$env:Team__DemoAgent__Enabled = 'false'`, `$env:Team__AgentMessageBudget = '4'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
6. Open `http://localhost:5100`, click **Teammates**, and wait until the `Ada` tile reads **Online**.
7. Click the `Ada` tile, click **Edit**, set **Model** to the entry whose name contains `Haiku`, set **Effort** to `low`, and click **Save**. Wait until the tile reads **Online** again.
8. Close the card, click the Room named `Ada` in the sidebar, and confirm the member line reads exactly `You, Ada`.
9. Type `what is 2 plus 2` — with no `@` anywhere — and press Enter.
10. Watch the Room for 60 seconds.
11. Read `T-A`.

**Pass if — all of these**

- A streaming row appears under your Message with sender `Ada` and a **Stop** button beside the name, then settles into an ordinary Message.
- The reply is a real answer to the question, and you never typed an `@`.
- A grey line reading `1 of 4 agent replies since you last spoke.` appears.
- `T-A` contains NO `declined a turn in room` warning for this Message.

**Fail if — any of these**

- No reply arrives and the tile still reads Online and no warning appears in `T-A` -> the member-count branch of the Reply Gate is broken: the Message was treated as not-for-me and quietly buffered instead of answered. This is invisible from the UI, which is exactly why it needs a manual test.
- `T-A` shows `Persona 'Ada' declined a turn in room …: the room has spent its budget …` -> a stale in-memory Budget is blocking the very first Message; restart and retry once before filing.
- A reply arrives but no grey budget line does -> the Room view is not reading the Budget (see REPLYGATEBUDGET-06).

**Inconclusive if**

If the `Ada` tile reads **Offline** or **Degraded**, read the reason line on its card and fix that first — an offline Persona is silent for a reason that has nothing to do with the gate. If the tile never leaves **Starting**, the agent process is failing to launch; check `T-A`. If the Model dropdown offers no Haiku entry, stop and ask before selecting anything else: this test must not run on a larger model.

> [!NOTE]
> COST: one short billed Turn at Haiku/low — a few seconds of model time. Leave the app running if you intend to do REPLYGATEBUDGET-32 next; it reuses this exact setup.

### REPLYGATEBUDGET-32 — PAID: an un-mentioned Message is not answered but rides along as context on the next Mention

**💰 Spends money** · about 15 min

*Proves Catch-up: nothing reaches the model until the Persona is Mentioned, and when it finally is, the Messages it stayed silent for are in its prompt.*

**Before you start**

- REPLYGATEBUDGET-31 has passed and the app is still running on PROFILE C with `Ada` Online at Haiku/low.
- You are willing to spend ONE more billed Turn.

**Steps**

1. In a spare terminal run `pwsh E:\Repos\Huddle\tools\echo-bot.ps1 -Name filler` so there is a second Agent to make a Room Mention-gated.
2. In the browser, click the Room named `Ada`, type `/invite @filler` and press Enter, and confirm the member line names three people.
3. Type `the deadline moved to Friday` and press Enter. Wait 20 seconds.
4. Type `the budget was cut 10 percent` and press Enter. Wait 20 seconds.
5. Confirm `Ada` has said nothing at all and `T-A` shows no Turn for her.
6. Type `@Ada what day did the deadline move to, and by what percent was the budget cut?` and
   press Enter. Ask for the facts back, not for an account of what she "was told": the Catch-up
   Messages arrive as a bracketed context block rather than as turns addressed to her, and a small
   model reliably answers "I wasn't told any facts" to the second phrasing even when the block is
   demonstrably in its prompt.
7. Wait up to 90 seconds and read the reply.
8. Read `T-A`.

**Pass if — all of these**

- Neither of the two un-mentioned Messages produces a reply, a streaming row, or any model activity in `T-A`.
- Each of them DOES produce the quiet blue note above the composer reading
  `No teammate was @-mentioned - name one to ask for a reply.` It is replaced by nothing once step 6's
  Mention lands. This is the cheapest confirmation that the Catch-up path was taken deliberately
  rather than the delivery having failed.
- The reply to step 6 names BOTH earlier facts — the Friday deadline and the 10 percent cut.
- The grey budget line afterwards reads `1 of 4 agent replies since you last spoke.` — only one Turn was taken for three Messages.

**Fail if — any of these**

- `Ada` replies to either un-mentioned Message -> the absolute rule that nothing reaches the model until it is Mentioned has broken; in a busy group Room every Teammate would take a billed Turn on every Message.
- The reply knows nothing of the earlier Messages -> the Catch-up buffer is being dropped rather than carried, so a Teammate joining a conversation mid-way answers blind.
- Two or three Turns appear in `T-A` for the three Messages -> the un-mentioned Messages each bought a Turn.

**Inconclusive if**

If the app or the Persona restarted between step 4 and step 6, the buffer is empty by design and the reply SHOULD know nothing — check `T-A` for Persona start lines before judging, and repeat the test without a restart. The same applies if `Ada` was Offline when you posted the un-mentioned Messages: the buffer is filled only from deliveries she actually received. Neither is a defect.

> [!NOTE]
> COST: one short billed Turn. The two un-mentioned Messages cost nothing, which is itself part of what this test proves. The buffer holds at most 20 Messages per Room, oldest discarded first, and is emptied silently by any restart.

### REPLYGATEBUDGET-33 — PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn

**💰 Spends money** · about 25 min

*The containment test for the whole area, plus the one observation that separates the cheap layer from the expensive one: at the cap the next Agent must not start a Draft at all, because a Draft means a Turn was already paid for.*

**Before you start**

- No app running.
- You are willing to spend roughly SIX short billed Turns.
- You will stay at the keyboard for the whole run.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with `$env:Team__Acp__Enabled = 'false'` temporarily and create two Personas on **Teammates**: Name `Ana`, Title `Planner`, Alias `ana1`, body `You are Ana. Keep the conversation going with @Ben. Always end your message by asking @Ben one short question. One or two sentences only.` and Name `Ben`, Title `Builder`, Alias `ben1`, body `You are Ben. Keep the conversation going with @Ana. Always end your message by asking @Ana one short question. One or two sentences only.`
3. Stop the app with Ctrl+C.
4. Start the app with PROFILE C and `$env:Team__AgentMessageBudget = '6'` — set this BEFORE `dotnet run`.
5. On **Teammates**, wait until both tiles read **Online**, then set each one's **Model** to the Haiku entry and **Effort** to `low` via **Edit** and **Save**.
6. In the sidebar click **New chat**, tick both `Ana` and `Ben`, and click **Start chat**.
7. Confirm the member line names three people.
8. Type `@Ana start a short conversation with @Ben about picking a meeting time.` and press Enter.
9. Watch the Room continuously. Count every agent Message as it lands and keep your hand near Ctrl+C in `T-A`.
10. When the exchange stops, read the area between the transcript and the composer.
11. Read `T-A` in full.
12. Note the Room id and run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl` and count the agent lines after your Message.

**Pass if — all of these**

- The exchange halts after exactly SIX agent Messages — never seven.
- The red pause block appears reading `Agents have sent 6 replies since you last spoke, and are paused.` with **Continue** and **Leave paused**.
- The last Agent to be delivered a Message does NOT show a streaming row or a **Stop** button: no Draft is started at all after the cap is reached.
- `T-A` contains at least one `Persona '…' declined a turn in room …: the room has spent its budget of 6 agent messages.` warning, and NO `refused a message from` warning for that same Persona.
- The `.jsonl` holds exactly six agent lines after your Message.

**Fail if — any of these**

- The exchange continues past six -> the runaway guard is gone. STOP THE APP IMMEDIATELY with Ctrl+C; two agents quoting each other have been measured at thousands of messages in seconds, and every one of them is billed.
- It halts at seven rather than six -> the two layers are comparing the count differently; the Room takes one more Message than the Human granted every time.
- A streaming row appears and THEN the Message is refused (`T-A` shows `refused a message from` with no matching `declined a turn`) -> the cheap layer did not fire: you paid for a Turn whose output was thrown away, on every pause.
- A Mentioned Agent keeps taking Turns past the cap while an un-mentioned one stops -> the Budget check and the Mention check have been reordered, so a Mention now buys a Turn past the cap.

**Inconclusive if**

If the models simply stop talking to each other before six (they may not obey the persona instruction reliably), the halt was not caused by the cap and the test proves nothing: read `T-A` — without a `declined a turn` warning, re-run with a clearer instruction, or accept the run as inconclusive rather than as a pass. If either tile is not Online, do not start: a one-sided conversation cannot reach the cap.

> [!NOTE]
> COST: about six short billed Turns at Haiku/low — the most expensive test in this area. Keep the budget at 6 or lower and never raise it for this test. Do not walk away while it runs.

### REPLYGATEBUDGET-34 — PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up

**💰 Spends money** · about 25 min

*Catches the subtlest trap in this area: if a Budget-declined Message were also buffered as context, clicking Continue would put it in the model's prompt twice — once as context and once as the live Message.*

**Before you start**

- Two Personas `Ana` and `Ben` exist and are Online at Haiku/low (as built in REPLYGATEBUDGET-33).
- You are willing to spend roughly FOUR short billed Turns.

**Steps**

1. Run `P-RESET-ROOMS`, then start the app with PROFILE C and `$env:Team__AgentMessageBudget = '2'` set BEFORE `dotnet run`.
2. On **Teammates**, wait for both tiles to read **Online**, then set each one's **Model** to the
   Haiku entry and **Effort** to `low` again. `P-RESET-ROOMS` deletes `team.db`, which is where the
   stored Model and Effort live, so both cards come back reading `Agent default` / `Model default`
   however they were left in REPLYGATEBUDGET-33.
3. Click **New chat**, tick `Ana` and `Ben`, and click **Start chat**.
4. Type `@Ana start a short conversation with @Ben about picking a meeting time.` and press Enter.
5. Wait until the Room halts and the red pause block appears.
6. Read `T-A` and find the `declined a turn in room` warning; note which Persona it names.
7. Note the exact text of the last Message in the Room.
8. PATH ONE. Do NOT click Continue. Instead type `@<the declined Persona> summarise everything you have been told in this room so far, listing each message.` and press Enter.
9. Read the reply carefully and check whether it accounts for the Message it was paused on (the one you noted at step 7).
10. PATH TWO. Drive the Room back to its cap by typing `@Ana carry on with @Ben.` and waiting for
    the pause block to return. Before clicking Continue, check that the LAST Message actually
    `@`-mentions the Persona that declined: Continue re-delivers that Message, and in a Room of
    three the Reply Gate answers it only if it names them. These models often end a turn without
    writing the Mention their Persona body asks for, and a held Message that names nobody
    correctly produces Catch-up and no reply — which is not what this path is testing. If it
    names nobody, prompt again until one does.
11. Click **Continue** and wait up to 90 seconds.
12. Read the reply that arrives, if any, and count how many times it addresses the Message it was paused on.

**Pass if — all of these**

- PATH ONE: the reply does NOT account for the Message the Persona was paused on, even though that Message is still visible above the composer. That is the documented behaviour.
- PATH TWO: after Continue, the woken Persona answers the held Message exactly ONCE — it does not answer it twice, and does not repeat or restate it as if it had been given to it twice.
- `T-A` shows a `declined a turn` warning at each pause and one `was extended to` line per Continue click.

**Fail if — any of these**

- PATH TWO produces a reply that visibly handles the same Message twice — restating it as context and then answering it -> the declined Message is being buffered as Catch-up as well as held for re-delivery, so every Continue double-feeds the model. This produces no error at all; the only symptom is a strangely repetitive reply.
- No `declined a turn` warning appears at either pause and only `refused a message from` warnings do -> the cheap layer is not firing (see REPLYGATEBUDGET-33's fail conditions).
- Continue produces no reply at all and `T-A` shows no `was extended to` line -> the grant itself failed.

**Inconclusive if**

Model replies are not deterministic, and 'does the reply account for that Message' is a judgement call. If the reply is ambiguous, repeat PATH TWO once; if it is still ambiguous, record the reply text verbatim and mark the test inconclusive rather than guessing. If the exchange never reaches the cap, the models did not talk to each other — see REPLYGATEBUDGET-33's inconclusive note.

> [!NOTE]
> COST: roughly three to four short billed Turns. PATH ONE's outcome is a documented limitation and must NOT be filed as a defect: a Message declined for Budget is deliberately not buffered as context, so typing something new instead of clicking Continue does lose it from the model's view even though it stays in the Transcript.

### REPLYGATEBUDGET-35 — PAID: the Agent-facing post tool returns the same terminal refusal, and the model obeys it

**💰 Spends money** · about 20 min

*Proves the two doors into a post carry identical wording, and observes the only thing no automated test can check — whether a real model actually stops rather than retrying or routing around the cap.*

**Before you start**

- One Persona `Ada` (or `Ana`) exists and is Online at Haiku/low.
- REPLYGATEBUDGET-24 has been run, so you have the exact pipe-door sentence written down.
- You are willing to spend roughly TWO to FOUR short billed Turns.

**Steps**

1. Run `P-RESET-ROOMS`, then start the app with PROFILE C and `$env:Team__AgentMessageBudget = '1'` set BEFORE `dotnet run`.
2. Wait for the Persona tile to read **Online** and confirm Haiku and low on its card.
3. Click the Room named after the Persona and note the Room id.
4. Type `@<Persona> reply once, then immediately post a second short message into this same room.` and press Enter.
5. Wait up to 120 seconds and watch the Room.
6. Read `T-A` in full.
7. Read the sidebar and note whether any new Room has appeared.
8. Run `Get-Content E:\Repos\Huddle\src\Huddle.App\App_Data\rooms\<RoomId>.jsonl` and count the agent lines after your Message.
9. If any other Room exists, check its `.jsonl` too.

**Pass if — all of these**

- Exactly ONE agent Message lands in the Room; the red pause block appears.
- `T-A` shows exactly one `refused a message from` warning for the second attempt (or, if the Persona declined before trying, a `declined a turn` warning and no refusal at all — either is correct).
- If a refusal happened, it carries the same wording as the pipe door: the model is told the budget is spent and not to retry.
- The model does not keep retrying: `T-A` does NOT fill with repeated `refused a message from` warnings for the same Persona.
- No new Room appears in the sidebar and no other Room's `.jsonl` gains a line — the model did not route around the cap by posting elsewhere.

**Fail if — any of these**

- `T-A` shows the same Persona being refused repeatedly in quick succession -> the refusal reads as transient to the model, so it retries and spends exactly the Turns the refusal exists to save.
- A new Room appears and the second Message lands there instead -> the model routed around the cap; each new Room starts with a full allowance of its own, so this is how an unattended run escapes its budget.
- The tool result the model was given differs in wording from the pipe-door sentence recorded in REPLYGATEBUDGET-24 -> the two doors have drifted apart, and a client will behave differently depending on which one it hit.
- Two agent Messages land -> the cap did not hold at all.

**Inconclusive if**

A model may simply choose not to attempt a second post, in which case nothing is refused and the obedience half of this test is untested — read `T-A`: with neither a `refused` nor a `declined` line, mark it inconclusive and re-run once with a more explicit instruction. Never infer obedience from the absence of a second Message alone.

> [!NOTE]
> COST: two to four short billed Turns. Whether a real model obeys terminal wording is not provable by any automated test, which is the whole reason this test exists; record what the model actually did in your report, not just pass or fail.

### REPLYGATEBUDGET-36 — PAID: an Agent can mint a fresh Budget by creating a Room, and only the token Budget catches it

**💰 Spends money** · about 25 min

*Documents a known gap so it is not filed as a bug, and tests the guard that does catch it: a spent per-Persona token Budget must show as Degraded with a reason on the Teammate tile, not only in the log.*

**Before you start**

- TWO Personas exist and are Online — `Ana` and `Ben` from REPLYGATEBUDGET-33 are exactly right.
  One is not enough: `ChatService.CreateRoomForAsync` short-circuits a single-agent request to
  `EnsureRoomForAsync`, which returns that Agent's EXISTING direct Room, so a lone Persona asked to
  create a Room can only ever be handed the one it already has and no Budget is ever minted.
- You are willing to spend a handful of short billed Turns.
- You will stay at the keyboard.

> [!IMPORTANT]
> The per-Persona token Budget can only be reached by an **Agent-to-Agent** exchange, never by
> typing at the Persona. `PersonaRunner`'s read loop does `Interlocked.Exchange(ref
> this.tokensConsumed, 0)` the moment it sees a Human Message, *before* queueing the work item,
> and `ProcessWorkItemAsync` checks the Budget at the start of that item — so a Turn you prompted
> is always measured against a freshly-zeroed counter. Same rule as `STREAMINGTURN-28`,
> `STARTUPCONFIG-33` and `ROOMMESSAGING-30`.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE C plus `$env:Team__AgentMessageBudget = '6'` and
   `$env:Team__Acp__TokenBudget = '2000'`, both set BEFORE `dotnet run`. The Room Budget has to be
   comfortably larger than 2: the token Budget is only ever reached on an Agent-to-Agent Turn (see
   the box above), and a Room that hits its own cap first declines in the read loop, before
   `ProcessWorkItemAsync` ever runs the token check.
3. Wait for the Persona tile to read **Online** and confirm Haiku and low on its card.
4. Click the Room named after the Persona and note the Room id.
5. Type `@<Persona> create a new room containing yourself and <the other Persona>, then post a short message in it.` and press Enter.
6. Wait up to 120 seconds, then read the sidebar.
7. Click the newly created Room and read the area between its transcript and its composer.
8. Go to the Room the Persona created, which has both Personas in it.
9. Type `@<Persona> start a short back-and-forth with <the other Persona>.` and let them bounce.
   Watch `T-A` for the token-budget line; stop as soon as it appears, or when the Room reaches its cap.
10. Click **Teammates** and read the Persona's tile, then open its card and read the line under the status.
11. Type one more short Message to the Persona and observe whether it takes a Turn.
12. Send a final Message and then check whether the tile returns to **Online**.

**Pass if — all of these**

- A new Room appears in the sidebar, and it has its own untouched Budget: no pause block, no budget line, until an agent Message lands there.
- `T-A` shows a `rooms` entry being created for it (and `team.db`'s `rooms` table gains a row).
- Once the token budget is spent, `T-A` prints `Persona '…' has spent its token budget of 2000 and is taking no more turns until a human speaks to it.`
- At that moment the Persona's tile reads **Degraded**, and its card shows a reason line saying the per-Persona token Budget is spent and no more Turns will be taken until a Human speaks.
- After the Human speaks to it again, the Persona takes Turns again and the tile returns to **Online**.

**Fail if — any of these**

- The token budget is spent but the tile still reads **Online** and only the log says anything -> the guard is silent in the UI, so a Teammate that has stopped working looks identical to one that is idle.
- The tile goes Degraded and never recovers after a Human Message -> the token budget is not resetting, so a Teammate is permanently disabled by one busy afternoon.
- No new Room is created at step 5, and the Persona was asked for a room with a SECOND agent in it -> unrelated to this area's cap; note it, but do not judge the budget behaviour from it. (If you asked for a room containing only the Persona itself, you have hit the short-circuit described above, not a defect.)

**Inconclusive if**

If the Persona never creates the Room (models do not always use the tool when asked), the first half is untested — retry once with a more direct instruction, then mark it inconclusive. If 2000 tokens are spent before the Persona does anything useful, lower the number of steps rather than raising the token budget. If you cannot reach the token budget within roughly six Messages, stop and mark the second half inconclusive rather than continuing to spend.

> [!NOTE]
> COST: a handful of short billed Turns; stop as soon as you have seen the Degraded state. DO NOT FILE the minted Room having a full Budget of its own — that is a documented gap in the per-Room cap, and the per-Persona token Budget is the guard that exists to catch it. What IS a defect is the token budget being enforced silently.

---

Back to [the manual test script](../manual-tests.md).
