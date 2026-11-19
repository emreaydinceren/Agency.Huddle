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

1. RESET under PROFILE B and PROFILE C always means `P-RESET-ROOMS` — it clears every Room, Member list and Transcript while leaving the Persona library alone. Under `P-LAUNCH-UAT` RESET means `P-STOP` and then re-running its seeder line, which wipes and rebuilds the data folder back to Ada, Bo and their two empty Rooms. Budget counters are in memory; a restart clears them.
2. **`P-LAUNCH-UAT` — free, the mock Agents Ada and Bo.** Run [`P-LAUNCH-UAT`](common.md#p-launch-uat) as written; Ada and Bo go **Online** and each has a two-Member Room named after itself. The Budget defaults to `40`. Where a test names another Budget, add `--Team:AgentMessageBudget=<N>` as one more argument on the `dotnet run --project src/Huddle.App` line of that procedure (a backtick-continued line like the others), or set `$env:Team__AgentMessageBudget = '<N>'` in `T-A` before that line; the seeder line is unchanged. Both only take effect at startup, so changing the value means `P-STOP` and a relaunch. `<DataDir>` in a path below is `<data>` as [Common procedures](common.md) defines it: `C:\seeds\huddle\uat\data` under this lane and `<repo>\src\Huddle.App\App_Data` under the others, so `O-TRANSCRIPT` is `<data>\rooms\<RoomId>.jsonl` and `O-DB` is `<data>\team.db`.
3. **PROFILE B — free, scripted pipe clients you control.** Needed wherever a test names a wire envelope as its oracle. `P-LAUNCH-FREE` plus `$env:Team__AgentMessageBudget = '40'` (or whatever budget the test names) set before `dotnet run`, then one `P-ECHO-BOT` terminal per Agent the test names. `O-WIRE` is the primary oracle for this whole area.
4. **PROFILE C — paid, real Personas.** `P-LAUNCH-PAID` with `$env:Team__AgentMessageBudget` set to the small number the test names. Every Persona must already be Haiku / low.
5. Learn the `O-LOG` lines you will read all day, verbatim: `Room '{RoomId}' refused a message from '{AgentName}': its budget of {N} agent messages since a human last spoke is spent.` (warning) / `Room '{RoomId}' was extended to {N} agent messages.` / `Persona '{PersonaName}' declined a turn in room {RoomId}: the room has spent its budget of {N} agent messages.` (warning) / `Persona '{PersonaName}' has spent its token budget of {N} and is taking no more turns until a human speaks to it.` / `Invited agent '{AgentName}' ({AgentId}) into room '{RoomId}'.` / `Created direct room '{RoomId}' for agent '{AgentName}'.`
6. In `O-DB`, `SELECT COUNT(*) FROM room_members WHERE room_id='…'` is what makes a Room two-Member or group — that count and nothing else. The Room's grey member line in the browser is an acceptable substitute.
7. READ THIS BEFORE FILING ANYTHING. Both free kinds of Agent implement the Reply Gate. The mock Agents of `P-LAUNCH-UAT` are real Personas on the free mock adapter, so they go through the real Reply Gate and the real Budget checks. `tools/echo-bot.ps1` is a raw pipe client that mirrors the same decision (budget check first, then member-count-or-mention) from the fields already on the `messagePosted` envelope; following never enters into it, because a pipe client has no `mcp__team__follow_room` tool to call and can never be following a Room. So a two-Member Room answers an un-mentioned Message under `P-LAUNCH-UAT` or PROFILE B — that is the correct behaviour per ADR-0004, not silence to expect. The free tests below can therefore observe the Reply Gate's own decisions directly — answer-without-mention in a two-Member Room, Mention-gating once a third Member joins, and decline-before-Turn once the Budget is spent — without needing a real model. Following (roadmap item 8) is invisible to both by construction and is exercised only by real Personas in `app-tools.md`'s APPTOOLS-23 to -25. (Catch-up buffering is still not something a free test observes reliably, so that piece remains a paid-tests concern.)
8. THE MOCK RE-QUOTES MENTIONS. A mock Agent's reply is the whole prompt echoed back, so a reply to a Message that names `@Ada @Bo` itself names both of them. In a Room of three or more Members that Mentions the other Agent, so Ada and Bo can keep answering each other until the Budget is spent, and in a Room with no cap they never stop. Every `P-LAUNCH-UAT` test below therefore either names ONE Agent per Message or says what to expect from a Message that names two; none of them sends a two-Mention Message to an uncapped Room. The echo also carries Catch-up and Transcript lines (an Agent's first Turn in a Room includes up to 20 earlier Messages), so a reply can name an Agent that your own Message did not: an earlier Message in the Room that names `@Bo` can wake Bo again. A test that needs Bo to stay silent therefore runs in a Room whose Transcript never named Bo. A reply that arrives only because the mock re-quoted a Mention is the echo, not a Reply Gate defect. The pipe bot `tools/echo-bot.ps1` strips `@` from what it quotes and never has this effect.

## Tests

### REPLYGATEBUDGET-02 — Three or more Members makes the same Room Mention-gated

**Free** · about 7 min

*Proves membership count alone flips a Room from answer-everything to Mention-gated, with no stored Room kind, and that one Mention wakes one Agent while two wake two.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada is Online.
- You are on the Room named `Ada`.

**Steps**

1. Click **Invite agent** at the right of the Room header.
2. In the panel that opens, click `Bo`.
3. Read the blue strip at the top of the panel.
4. Click **Invite agent** again to close the panel.
5. Read the sidebar entry, the Room heading and the grey member line, WITHOUT reloading the page.
6. Type `good morning everyone` and press Enter.
7. Wait 15 seconds without touching anything.
8. Type `good morning @Ada` and press Enter, then wait 10 seconds.
9. Type `@Ada @Bo go` and press Enter, then wait 15 seconds.

**Pass if — all of these**

- The blue strip (an Info alert, `InviteAgent.razor:23`) reads exactly `Invited Bo. Room is now "Ada, Bo".`
- With no page refresh, the sidebar entry and the Room heading both change to `Ada, Bo` and the member line becomes `You, Ada, Bo`.
- Step 6 produces no reply at all from either Agent, AND a quiet blue note appears above the
  composer reading `No agent was @-mentioned — name one to ask for a reply.` That note is the
  point of the silence being legible at all; before #40 step 6 looked identical to a broken app.
- The note disappears the moment step 8's Message posts, because that one names somebody.
- Step 8 produces exactly one reply, from `Ada`, and it contains the text you sent.
- Step 9 produces a reply from `Ada` and a reply from `Bo`, each containing the text you sent. Any further replies after those two are Ada and Bo answering each other, because each mock reply re-quotes the Mentions it was sent (Setup item 8); they stop by themselves at the Budget and are not a defect.
- `SELECT COUNT(*) FROM room_members WHERE room_id='<RoomId>'` returns 3 (or, without SQLite, the member line names three people).

**Fail if — any of these**

- Both Agents reply to `good morning everyone` -> the member-count gate has collapsed or `mentioned` is being set true for every Member; this is the reply storm the design exists to prevent and it will burn a billed Turn per Agent per Message once real Personas are used.
- Only `Ada` replies to the Message at step 9 (and `Bo` stays silent) -> the Mention scan stops after consuming the first handle, so a Message naming two people reaches only one of them, silently.
- The Room is renamed only after a manual page refresh -> the rooms-changed event is not reaching the sidebar and the header, so membership changes made anywhere else in the app will not show either.
- `Bo` stays silent at step 9 while `Ada` answers, and `Bo`'s dot is green -> the second Mention did not resolve.

**Inconclusive if**

If `Bo` does not appear in the **Invite agent** list, it is either already a Member or not Online — check the sidebar for a Room named `Bo` and `Bo`'s avatar in that Room reading Online, then restart from RESET. If the badge beside `Bo` in the panel is a hollow grey ring rather than a green check circle, `Bo` is offline and its silence proves nothing about the gate: fix the agent first. If the strip is a red error alert instead of the blue note, read its text and treat the test as inconclusive.

> [!NOTE]
> A Room has no stored kind anywhere: the member count is the whole of this rule. That is why this test changes nothing but membership.

### REPLYGATEBUDGET-03 — /invite from the composer, and its two error strips, never touch the Transcript

**Free** · about 6 min

*Proves the composer's slash command flips a Room's membership through the same path the header button uses, reports unknown agents and unknown commands distinctly, and writes nothing to the Transcript.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Bo is Online.
- A two-Member Room named `Bo` exists (it does straight after the seeder).

**Steps**

1. Click the Room named `Bo` in the sidebar and note the Room id from the URL's last segment.
2. Type `/invite @Ada` and press Enter.
3. Read the strip directly above the composer box.
4. Read the sidebar entry and the Room heading, without reloading.
5. Type `/invite @nobody` and press Enter, then read the strip.
6. Type `/shout hello` and press Enter, then read the strip.
7. Scroll the transcript and count the Messages in it.
8. In a second PowerShell window run `Get-Content <data>\rooms\<RoomId>.jsonl`.
9. In `T-A`, look for the invite log line.

**Pass if — all of these**

- Step 3 shows a GREEN strip reading exactly `Invited Ada. Room is now "Bo, Ada".`
- The sidebar and heading rename to `Bo, Ada` with no page refresh.
- Step 5 shows a RED strip reading exactly `Unknown agent @nobody`.
- Step 6 shows a RED strip reading exactly `Unknown command`.
- The transcript shows no Message at all for any of the three commands.
- The `.jsonl` file gained no line from any of the three commands (it may still be absent entirely if nothing was ever posted in this Room).
- `T-A` shows `Invited agent 'Ada' (<AgentId>) into room '<RoomId>'.` exactly once (the agent id is not quoted, `ChatService.cs:674`).

**Fail if — any of these**

- A `/invite` or an error strip appears as a Message in the transcript or as a line in the `.jsonl` -> a command is being persisted as conversation, which would then be re-delivered to Agents and would count against the Room's Budget.
- `/shout hello` reports `Unknown agent @shout hello` rather than `Unknown command` -> the command regex is matching anything beginning with a slash, so a typo silently becomes an invite attempt.
- The strip is green but the sidebar name does not change until a refresh -> the rooms-changed notification is not reaching the components.
- `/invite @nobody` succeeds or is silent -> an invite is not being validated against the directory.

**Inconclusive if**

If `Ada` is already a Member of the `Bo` Room from an earlier test, step 2 will not produce the expected strip — run RESET (re-run `P-LAUNCH-UAT`) and begin again. If no strip appears at all after Enter, check that the text actually sent (the composer box empties on send); if the box still holds your text, the keyboard handler is not attached and the whole test is inconclusive.

> [!NOTE]
> The `/invite` Name is captured as 'everything after /invite' rather than by shape, precisely so a Name containing spaces survives. That half of the rule is exercised in REPLYGATEBUDGET-19.

### REPLYGATEBUDGET-04 — A Mention is case-insensitive and ends at trailing punctuation

**Free** · about 6 min

*Proves six spellings of the same Mention all resolve, which is where a case-sensitive comparison or a greedy name boundary would otherwise fail in complete silence.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada and Bo are Online.
- You are in a Room with three or more Members that contains `Ada` — the `Ada, Bo` Room from REPLYGATEBUDGET-02 is the right shape, but only if its Transcript has never named `@Bo`. After -02 step 9 it has, and the mock's Catch-up echo could then wake Bo (Setup item 8): RESET, then repeat -02 steps 1-5 only (add Bo, send nothing naming Bo).

**Steps**

1. Confirm the member line names three people, one of them `Ada`.
2. Type `@ADA hi` and press Enter, then wait 8 seconds and count the replies.
3. Type `@Ada hi` and press Enter, then wait 8 seconds and count the replies.
4. Type `@Ada,` and press Enter, then wait 8 seconds and count the replies.
5. Type `@Ada.` and press Enter, then wait 8 seconds and count the replies.
6. Type `@Ada:` and press Enter, then wait 8 seconds and count the replies.
7. Type `@Ada?` and press Enter, then wait 8 seconds and count the replies.

**Pass if — all of these**

- Each of the six Messages produces exactly one reply, every reply's sender is `Ada`, and each reply contains the text you sent.
- `Bo` never replies to any of the six.
- No grey budget line ever shows a figure above `1` (each of your Messages resets the counter first).

**Fail if — any of these**

- `@ADA hi` produces no reply while `@Ada hi` does -> Mention matching is case-sensitive, so a Human who capitalises a Name gets silence with no error anywhere.
- `@Ada,` or `@Ada.` produces no reply -> the punctuation is being swallowed into the Name, so the handle no longer matches any Member and the Mention resolves to nobody, silently.
- Any of the six produces two replies -> a single handle is being matched more than once, which with a real Persona means paying twice for one Mention.

**Inconclusive if**

If `Ada` is offline (a hollow grey ring on Ada's avatar in her Room, or her card reads Offline — the **Invite agent** panel lists only non-Members, so Ada is not in it), every line of this test is inconclusive — no Mention can be observed through an Agent that receives nothing. If the Room has only two Members, a reply proves nothing about Mention resolution because a two-Member Room would be answered anyway: add a second Agent first. If `Bo` does reply, read its text: a reply that merely re-quotes an earlier Message naming `Bo` is the mock echoing a Mention (Setup item 8), not a gate failure — re-run from a fresh seeder.

> [!NOTE]
> A space is deliberately NOT a boundary character, which is what makes multi-word Names possible; that is covered in REPLYGATEBUDGET-19 and -21.

### REPLYGATEBUDGET-05 — Repeating a Mention wakes an Agent once, and the Human can be Mentioned harmlessly

**Free** · about 4 min

*Proves duplicate handles collapse to one delivery (three billed Turns for one Mention is the failure) and that naming the Human resolves to somebody who is not an Agent without erroring.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada and Bo are Online.
- You are in a Room of three or more Members containing `Ada` whose Transcript has never named `@Bo` — the `Ada, Bo` Room as in the precondition of REPLYGATEBUDGET-04 (after RESET and adding Bo only).

**Steps**

1. Type `@Ada @Ada @Ada hi` and press Enter.
2. Wait 8 seconds and count the replies.
3. Type `@You are needed` and press Enter.
4. Wait 10 seconds and watch the whole page.
5. In a second PowerShell window run `Get-Content <data>\rooms\<RoomId>.jsonl | Select-Object -Last 4`.

**Pass if — all of these**

- Step 1 produces exactly ONE reply, from `Ada`, containing the text you sent.
- Step 3's Message posts normally and appears in the transcript with sender `You`.
- Nothing at all follows step 3 by way of a reply, a red strip, an error banner, or an entry in
  `T-A` at Warning level. What DOES appear is the quiet blue note above the composer reading
  `No agent was @-mentioned — name one to ask for a reply.` Read its wording closely: it says
  *no agent*, not *nobody*, precisely because `@You` IS a Mention - of the Human, who is not an
  Agent and is never delivered to. A note here reading "nobody was mentioned" would be false,
  and this test is the case that proves it.
- The tail of the `.jsonl` shows one agent line after `@Ada @Ada @Ada hi`, and the `@You are needed` line with no agent line after it.

**Fail if — any of these**

- Three replies arrive at step 2 -> duplicate Mentions are not collapsing; with a real Persona this is three billed Turns for one Mention, and a Message naming an Agent five times would cost five.
- Step 3 shows a red strip or an error -> Mentioning the Human is being treated as an error rather than as a Mention that simply reaches no Agent, which will make `@You` unusable in ordinary conversation. The blue `role="status"` note is NOT this: it is an Info consequence, not a failure.
- The note at step 3 says "nobody" rather than "no agent" -> the copy has drifted into a claim that is false in this exact case.
- An Agent replies to `@You are needed` -> the Human is being delivered to, or the Mention resolved to the wrong Member.

**Inconclusive if**

If the Human's display name is not `You` (it is configurable), step 3 names nobody and tells you nothing: read the first name in the Room's grey member line and use that instead. If `Ada` is offline, step 1 is inconclusive.

> [!NOTE]
> The Human is a genuine Mention target; delivery simply skips non-Agent Members.

### REPLYGATEBUDGET-06 — The Budget note counts agent replies and a Human Message resets it to nothing

**Free** · about 6 min

*Proves the counter shown under the transcript matches the number of agent Messages since the Human last spoke, updates live, and disappears the moment the Human speaks.*

**Before you start**

- `P-LAUNCH-UAT` has been run with the default Budget of `40`.
- You are in the `Ada, Bo` Room (three Members).

**Steps**

1. Type `starting fresh` and press Enter, then look at the gap between the transcript and the composer.
2. Type `@Ada hi` and press Enter, then wait 8 seconds and read the grey line above the composer.
3. Type `@Ada @Bo go` and press Enter, then watch the grey line while the replies arrive.
4. Wait until the grey line has stopped changing for 10 seconds and read it again. The mock re-quotes the Mentions it was sent (Setup item 8), so Ada and Bo may keep answering each other for a while; if the Room reaches its Budget the line is replaced by the pause block, in which case read the figure in its sentence instead.
5. Type `.` and press Enter and read the same area again.
6. Count the agent-authored lines after the last `"senderId":"human"` line in `<data>\rooms\<RoomId>.jsonl` — do this BEFORE step 5 if you can, since the `.` starts a new count; otherwise count the agent lines between your `@Ada @Bo go` line and your `.` line.

**Pass if — all of these**

- After step 1 there is NO grey budget line (no agent has spoken since you did).
- After step 2 the grey line reads exactly `1 of 40 agent replies since you last spoke.`
- During step 3 the line disappears when your Message posts, then reappears as `1 of 40 …` and then `2 of 40 …` (and higher, if the two keep answering each other) as the replies land — with no page refresh at any point.
- After step 4 the figure is at least `2`, and both `Ada` and `Bo` have posted.
- After step 5 the grey line is gone again.
- The file count in step 6 matches the last figure the line (or the pause block) showed before step 5, whatever that figure is.

**Fail if — any of these**

- The grey line never appears -> the Room view is not reading the Budget off the posted-Message event, and the pause block will be invisible too: a capped Room would then look exactly like a crashed Agent.
- The figures only update after a manual refresh -> the same event is not repainting the view live, so a Human watching a running conversation sees a stale allowance.
- The line survives your Message at step 5, or shows a non-zero figure right after it -> a Human Message is not resetting the counter, so a Room will eventually pause even though the Human keeps speaking.
- The figure disagrees with the file count in step 6 -> the counter is being incremented somewhere other than the accepted-Message path, so it describes no real prefix of the conversation.

**Inconclusive if**

If one of the two Agents is offline at step 3 only one reply arrives and the line will read `1 of 40`, which is correct for what happened — check on their avatars in their Rooms that both `Ada` and `Bo` read Online before judging (the **Invite agent** panel lists only non-Members, so neither is in it). If the app was launched with a different `Team__AgentMessageBudget` from a previous test, substitute that number for 40 throughout rather than failing the test.

> [!NOTE]
> The line is deliberately suppressed when the Room is uncapped; that case is REPLYGATEBUDGET-16.

### REPLYGATEBUDGET-07 — A spent Budget halts the Room and asks the Human, visibly

**Free** · about 6 min

*Proves the single most important negative in this area: a cap that stops an Agent is announced in the Room view rather than being silent, and it never marks anyone unhealthy and never posts a Message of its own.*

**Before you start**

- No app running.

**Steps**

1. Run `P-LAUNCH-UAT` with a Budget of `1` (Setup item 2). Re-running its seeder line is the reset.
2. Wait for Ada to go **Online**.
3. Open `http://localhost:5100` and click the Room named `Ada` in the sidebar.
4. Type `@Ada hi` and press Enter.
5. Wait 8 seconds, then read everything between the transcript and the composer.
6. Read the sender name of every Message in the transcript.
7. Read the status on `Ada`'s avatar in her Room (`P-OPEN-AGENT` shows it on her card).
8. Read `T-A`.

**Pass if — all of these**

- `Ada`'s reply lands (it contains `hi`), and then a `budget-prompt` block appears between the transcript and the composer, separated from the transcript by a plain rule, whose `budget-prompt-text` paragraph is in the danger colour and reads exactly `Agents have sent 1 replies since you last spoke, and are paused.`
- That block carries exactly two buttons, labelled **Continue** and **Leave paused**, and above
  them a second, grey `budget-prompt-warning` paragraph reading
  `Continue will deliver the last message again, and it is an agent's own — an agent is
  never delivered its own message, so this will wake nobody. Say something instead.` (the em dash is the UI's, `Chat.razor:143`) It is there because the
  Room's last Message is `Ada`'s own reply, and an Agent is never delivered its own Message.
- No context-only note appears. This is a two-Member Room, so a Message is answered without a
  Mention and there is nothing to explain — the note is for Rooms of three or more.
- No other strip appears above the composer — in particular no alert listing `Ada is Degraded` or `Ada is Offline`.
- Every Message in the transcript is from `You` or from `Ada`; there is no Message from a `system` or similar sender announcing the pause.
- `T-A` shows NO `refused a message from` warning yet (nothing has tried to post a second time).
- `Ada`'s avatar still shows her as Online.

**Fail if — any of these**

- The reply lands and nothing else appears -> the pause is invisible, which is the exact failure this design exists to prevent: a Room that silently stops taking replies is indistinguishable from an Agent that crashed.
- A Message announcing the pause appears in the transcript -> the notice is being posted as conversation; posted as the Human it would reset the very Budget it reports, and posted as a third kind of sender it breaks the two-kind directory.
- `Ada` is shown Degraded, or a red member-health alert lists it -> a spent Room Budget is marking an Agent that is working exactly as designed, which trains the Human to ignore health warnings.
- The block appears but with only one button, or with different wording -> record the exact text seen; the wording is what tells the Human this is a question and not a crash.
- A context-only note appears in this two-Member Room -> the note is firing where the Reply Gate
  answers everything, which makes it an always-on strip nobody will read.

**Inconclusive if**

If no reply arrives at all, you have a dead agent rather than a pause — confirm `P-LAUNCH-UAT` has been run and Ada is Online and get a reply before judging this test. If the block reads `1 of 1` in grey rather than red with buttons, you are looking at the dismissed variant, which means **Leave paused** was clicked in this view earlier: reload the page and repeat from step 4.

> [!NOTE]
> The count in the sentence is not pluralised (`1 replies`). That is current, known, cosmetic behaviour and must not be filed as a defect.

### REPLYGATEBUDGET-08 — The write path refuses an over-Budget post: two Agents are woken, only one Message lands

**Free** · about 7 min

*Proves the Budget is enforced atomically on the write path, so two Agents that were both legitimately woken cannot between them take one more Message than the Room granted.*

**Before you start**

- No app running.

**Steps**

1. Run `P-LAUNCH-UAT` with a Budget of `1` (Setup item 2). Re-running its seeder line is the reset.
2. Wait for Ada and Bo to go **Online**.
3. Open `http://localhost:5100`, click the Room named `Ada`, and note the Room id from the URL.
4. Click **Invite agent**, click `Bo`, and confirm the header renames to `Ada, Bo`.
5. Click **Invite agent** again to close the panel.
6. Type `@Ada @Bo go` and press Enter.
7. Wait 10 seconds and count the agent Messages that appear.
8. Read `T-A`.
9. Run `Get-Content <data>\rooms\<RoomId>.jsonl`.

**Pass if — all of these**

- Exactly ONE agent Message appears in the Room (from either `Ada` or `Bo` — which one wins is a race and either is correct), and it contains the text you sent.
- The pause block appears with **Continue** and **Leave paused**.
- `T-A` shows exactly ONE `Room '…' refused a message from '…': its budget of 1 agent messages since a human last spoke is spent.` warning.
- The `.jsonl` file holds exactly one agent line after your `@Ada @Bo go` line.

**Fail if — any of these**

- TWO agent Messages appear and the `.jsonl` holds two agent lines -> the Budget check is not atomic with the write, so two concurrent posts each read the same spare capacity and both take it; with real Personas this is how a cap of N becomes a cap of N-plus-however-many-agents.
- No refusal warning appears in `T-A` and only one Message landed -> the second Agent may simply not have posted at all (see INCONCLUSIVE); without the warning you cannot claim the refusal happened.
- Zero agent Messages appear -> both were refused, meaning the counter was already non-zero before your Message reset it, which would mean a Human Message is not resetting the Budget.

**Inconclusive if**

If only one of the two Agents is online, only one post is ever attempted and the test proves nothing about the race: check on their avatars in their Rooms that both read Online first (the **Invite agent** panel lists only non-Members). If `T-A` shows a `declined a turn in room` warning for the second Agent and NO `refused a message from` line, the second Agent started its Turn after the first had already posted, so the cheap layer (REPLYGATEBUDGET-33) caught it and the write path was never reached: that is correct behaviour but not what this test is for — re-run from the seeder (a few attempts may be needed to hit the race). If the timing is such that the second Agent's post arrives after you have already typed something else, the Budget will have reset — do not type anything between step 6 and step 9.

> [!NOTE]
> This layer refuses AFTER the Turn that produced the Message has already happened. With a real Persona the model has thought and the text is thrown away. That is the documented price of putting the authority on the write path, not a defect; the layer that avoids the spend is covered in REPLYGATEBUDGET-33.

### REPLYGATEBUDGET-09 — Leave paused hides the question but keeps the pause legible

**Free** · about 4 min

*Proves dismissing the prompt replaces it with a muted statement of the same fact rather than clearing the Room's state or hiding the pause entirely.*

**Before you start**

- REPLYGATEBUDGET-08 has just finished and the pause block is on screen in the `Ada, Bo` Room.
- The run's budget is `1`.

**Steps**

1. Click **Leave paused**.
2. Read whatever now sits between the transcript and the composer.
3. Read `T-A` and count the `was extended to` lines.
4. Run `Get-Content <data>\rooms\<RoomId>.jsonl` and count the lines.

**Pass if — all of these**

- The two-button block is replaced by a single muted line reading exactly `Paused — 1 of 1 agent replies since you last spoke.`
- `T-A` contains NO `was extended to` line — dismissing granted nothing.
- The `.jsonl` line count is unchanged from REPLYGATEBUDGET-08's step 9.

**Fail if — any of these**

- The whole area goes blank after the click -> the pause has become invisible, so a Human arriving later cannot tell a paused Room from a dead one; that is the same silent-cap failure one step removed.
- A `was extended to` line appears in `T-A` -> **Leave paused** is granting Budget, which is the opposite of what it says and lets an unattended Room keep spending.
- A new line appears in the `.jsonl` -> dismissing is posting something.

**Inconclusive if**

Because Ada and Bo never post spontaneously, you cannot make a fresh post get refused while paused without first speaking (which un-pauses the Room). The proof that the Room is still refusing is therefore the refusal warning already in `T-A` from REPLYGATEBUDGET-08, not a new one. If that warning is not there, this test is inconclusive — re-run REPLYGATEBUDGET-08 first.

> [!NOTE]
> The pause itself is unchanged by the dismissal; only the question is hidden, and only for this view.

### REPLYGATEBUDGET-10 — A dismissal is per-view: navigation, reload and the next Message all bring the question back

**Free** · about 6 min

*Proves a dismissal cannot become sticky, because a sticky one would mean a later pause is never announced.*

**Before you start**

- REPLYGATEBUDGET-09 has just passed: the muted `Paused — 1 of 1 …` line is on screen.

**Steps**

1. Click the Room named `Bo` in the sidebar.
2. Click the Room named `Ada, Bo` in the sidebar to come back.
3. Read the area between the transcript and the composer.
4. Click **Leave paused** again.
5. Press F5 to reload the page.
6. Read the same area again.
7. Type `carry on` and press Enter, and confirm the pause area goes empty.
8. Type `@Ada hi` and press Enter, then wait 8 seconds.

**Pass if — all of these**

- After step 2 the pause block with **Continue** and **Leave paused** is back.
- After the F5 at step 5 the pause block is back again.
- After step 7 the pause area is empty (your Message reset the Room).
- After step 8 the pause block appears again, rather than staying suppressed from the earlier dismissal.

**Fail if — any of these**

- The muted `Paused —` line survives navigating away and back, or survives an F5 -> the dismissal is being stored beyond the view, so the next pause in this Room will never be announced to this Human.
- After step 8 nothing appears where the pause block should be -> a dismissal from the previous pause is suppressing a NEW pause: the Room is capped and silent, the exact failure the prompt exists to prevent.

**Inconclusive if**

If clicking `Bo` in the sidebar shows a Room you do not recognise, you are in the wrong Room — match the Room id in the URL against the one you noted earlier. If step 8 produces no reply at all, `Ada` has gone offline and the test cannot reach a second pause.

> [!NOTE]
> Two separate mechanisms are being checked here: reloading the view, and a new Message arriving. Both must clear the dismissal.

### REPLYGATEBUDGET-11 — The pause survives a reload and shows in a brand-new tab

**Free** · about 5 min

*Proves the Room view reads the Budget when it loads and not only from the live event, so a reconnected browser or a second tab cannot show an un-paused Room that is in fact paused.*

**Before you start**

- A Room is paused and the pause block is showing (reach that state by re-running REPLYGATEBUDGET-07 if needed).
- Do NOT click **Leave paused** during this test.

**Steps**

1. Note the full URL of the paused Room, including the Room id.
2. Press F5 and wait for the page to finish loading.
3. Read the area between the transcript and the composer.
4. Open a NEW browser tab and paste the URL from step 1 into it.
5. Read the same area in the new tab.
6. Read `T-A`.

**Pass if — all of these**

- After the reload the pause block is present with the same figures and both buttons.
- In the brand-new tab, which never witnessed the Message that caused the pause, the pause block is also present with the same figures.
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

- The two-Member `Ada` Room from REPLYGATEBUDGET-07 is paused with the pause block showing, under a run whose `Team__AgentMessageBudget` is `1`. Use that Room, not the `Ada, Bo` Room of REPLYGATEBUDGET-08: there the last Message may name another Agent, and Continue would then genuinely wake it and add a Message, which this test does not expect.
- You know the Room id and the current line count of its `.jsonl`.

**Steps**

1. Count the Messages currently visible in the transcript and write the number down.
2. Run `(Get-Content <data>\rooms\<RoomId>.jsonl).Count` and write the number down.
3. Click **Continue** once. (The `Continuing…` label and the disabled buttons last about one frame on localhost, because the grant is in memory — do not try to catch them, and do not file their absence.)
4. Wait 10 seconds.
5. Read the area between the transcript and the composer.
6. Count the Messages visible in the transcript again and compare with step 1.
7. Run the same `.Count` command again and compare with step 2.
8. Read `T-A` and count the `was extended to` lines produced by this one click.
9. Try to click **Continue** a second time.

**Pass if — all of these**

- The pause block then disappears and is replaced by a grey line reading exactly `1 of 2 agent replies since you last spoke.`
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

If nothing at all changes after the click — the pause block stays, no log line — the click did not reach the server; check `T-A` for an error and retry once. Whether any Agent actually wakes up and replies here is NOT part of this test: in the two-Member Room it will not, for the reason set out in REPLYGATEBUDGET-26. Judge this test only on the button behaviour, the note, the log line and the two counts.

> [!NOTE]
> Under a budget of 1 the note after a single grant must read `1 of 2`. If your run used a different budget N, expect `N of 2N`.

### REPLYGATEBUDGET-13 — Any Human Message resets the Budget, is never refused, and expires an earlier grant

**Free** · about 6 min

*Proves the Human can always speak in their own Room, that speaking clears the pause, and that a Budget previously granted with Continue does not persist past the next Human Message.*

**Before you start**

- `P-LAUNCH-UAT` has been run with a Budget of `1` (Setup item 2) and Bo has been added to the `Ada` Room, so you are in the three-Member `Ada, Bo` Room. This test does not use the two-Member Room: there a `.` is answered by Ada and the Room re-pauses within seconds, which hides the reset.
- The Room's Transcript has never named `@Bo` (see the precondition of REPLYGATEBUDGET-04).

**Steps**

1. Type `@Ada hi` and press Enter, wait 8 seconds, and confirm the pause block is showing.
2. Click **Continue** once and confirm the grey line reads `1 of 2 agent replies since you last spoke.` (the same grant as REPLYGATEBUDGET-12).
3. Type a single full stop `.` and press Enter.
4. Read the transcript and the area between it and the composer.
5. Type `@Ada hi` and press Enter and wait 8 seconds.
6. Read the area above the composer. Under a configured budget of `1` that one reply spends the
   whole allowance, so what you see is the pause block again, which does not print the Granted
   figure. Click **Leave paused** to read it: that only hides the question (REPLYGATEBUDGET-09) and
   grants nothing.
7. Run `Get-Content <data>\rooms\<RoomId>.jsonl | Select-Object -Last 3`.
8. Read `T-A` for any warning produced by your own two Messages.

**Pass if — all of these**

- The `.` posts normally, appears in the transcript with sender `You`, and is never refused.
- Immediately after step 3 the pause block and the grey budget line are both gone, and no Agent answers the `.` (three Members and no Mention): the quiet blue note `No agent was @-mentioned — name one to ask for a reply.` appears instead.
- After `Ada` replies at step 5, the line read at step 6 reads `Paused — 1 of 1 agent replies since you last spoke.` — the allowance is back to the configured default, NOT `1 of 2`.
- The tail of the `.jsonl` shows your `.` line with `"senderId":"human"`, then `@Ada hi`, then Ada's reply.
- `T-A` shows no refusal warning naming the Human.

**Fail if — any of these**

- The Human's Message is refused, or shows an error strip -> the cap is being applied to Human Messages, which locks the Human out of their own Room with no way back in.
- The pause block survives the Human's Message -> the reset is not clearing the pause, so the Room stays visibly paused while actually accepting replies (or worse, stays paused in fact).
- The grey line reads `1 of 2` after step 5 -> a Budget the Human granted for one pause is persisting past the conversation it was granted for, so each Continue permanently raises the Room's ceiling.

**Inconclusive if**

If no reply arrives at step 5, you cannot read the Granted figure at all: confirm `Ada` is online and repeat. If the configured budget for this run is not `1`, substitute it: the pass condition is that Granted returns to the CONFIGURED value, whatever that is.

> [!NOTE]
> This is the escape hatch for every pause: anything the Human says, even a full stop, resumes the Room.

### REPLYGATEBUDGET-14 — The Budget is per Room: a paused Room does not starve the one beside it

**Free** · about 6 min

*Proves one looping conversation cannot pause an unrelated one, and that switching Rooms in the sidebar does not carry the previous Room's figures across.*

**Before you start**

- No app running.

**Steps**

1. Run `P-LAUNCH-UAT` with a Budget of `1` (Setup item 2). Re-running its seeder line is the reset.
2. Wait for Ada and Bo to go **Online**.
3. Open `http://localhost:5100` and click the Room named `Ada`.
4. Type `@Ada hi` and press Enter, then wait until the pause block appears.
5. Click the Room named `Bo` in the sidebar.
6. Read the area between the transcript and the composer in this Room.
7. Type `@Bo hi` and press Enter and wait 8 seconds.
8. Click back to the Room named `Ada` in the sidebar.
9. Read the area between the transcript and the composer again.

**Pass if — all of these**

- In the `Bo` Room at step 6 there is no pause block and no budget line at all.
- `Bo` replies promptly at step 7 (the reply contains `hi`), and only afterwards does that Room show its own `1 of 1 …` state.
- Back in the `Ada` Room at step 9, the pause block is still there with its own figures.

**Fail if — any of these**

- The `Bo` Room shows a pause block or a budget line before anyone has spoken in it -> the counter is global, so two unrelated conversations starve each other and the Human cannot tell which one was looping.
- `@Bo hi` gets no reply while `Bo` is online -> the other Room's pause is suppressing this one.
- The figures shown in `Bo` match the ones from `Ada` for a moment after the switch -> the view is rendering one Room's state under another Room's name, which makes every Budget reading untrustworthy during navigation.

**Inconclusive if**

If `Bo` is not Online there is no working `Bo` Room to switch to — check `Bo`'s avatar status (`P-OPEN-AGENT`) and restart from RESET. If both Rooms happen to have the same name after earlier invites, use the Room ids in the URL to tell them apart.

> [!NOTE]
> Sidebar switching is deliberately fast here; a stale-state bug shows as a flash of the wrong figures rather than a permanent wrong reading, so watch the moment of the switch.

### REPLYGATEBUDGET-15 — A restart un-pauses every Room over a Transcript that already spent its Budget

**Free** · about 5 min

*Documents deliberate behaviour a tester will otherwise file as a bug, and pins the real defect that would be its opposite: a Room stuck paused forever with no way to clear it.*

**Before you start**

- A Room is paused with the pause block showing.
- You know the Room id.

**Steps**

1. Note the exact text of the pause block and the number of Messages in the transcript.
2. Run `Get-Content <data>\rooms\<RoomId>.jsonl` and count the agent lines after the last `"senderId":"human"` line.
3. In `T-A` press Ctrl+C to stop the app.
4. Start the app again with exactly the same `dotnet run` lines of `P-LAUNCH-UAT`, including the same Budget argument, but WITHOUT the seeder line — the seeder wipes the data folder, and that is RESET. Do NOT run RESET.
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

This test uses two pipe bots (PROFILE B), not the mock Agents: with the cap removed, the mock pair re-quotes Catch-up lines that name each other and answers itself forever (Setup item 8), whereas `tools/echo-bot.ps1` strips `@` from what it quotes and cannot loop.

1. Run `P-RESET-ROOMS`, then start the app under PROFILE B with the Budget set before `dotnet run`: `$env:Team__Acp__Enabled = 'false'`, `$env:Team__AgentMessageBudget = '0'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
2. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`; in a third terminal run `pwsh <repo>\tools\echo-bot.ps1 -Name Bo`. Confirm each prints `Sent hello. Listening for messages (Ctrl+C to exit)...`.
3. Open `http://localhost:5100`; the Rooms `Ada` and `Bo` appear. Click the Room named `Ada`, type `/invite @Bo` and press Enter, and confirm the heading becomes `Ada, Bo`.
4. One round is two Messages. Type `@Ada hi` and press Enter, wait 5 seconds, then type `@Bo hi` and press Enter and wait 5 seconds.
5. Repeat step 4 five more times.
6. Read the area between the transcript and the composer after every round.
7. Read `T-A` for the whole run.
8. Stop the app and both bots, run `P-RESET-ROOMS` again so Bo is no longer a Member of the `Ada` Room, restart under PROFILE B with `$env:Team__AgentMessageBudget = '-1'`, start both bots again, and repeat steps 3 to 7.

**Pass if — all of these**

- Every round produces two agent replies, one from `Ada` and one from `Bo`, each of the form `**Ada:** hi` / `**Bo:** hi` (the bot strips `@`), and nothing else arrives.
- No grey budget line and no pause block ever appears, at any point, in either run.
- `T-A` contains no `refused a message from` line and no `was extended to` line, in either run.
- The negative value behaves identically to zero.

**Fail if — any of these**

- A pause block appears -> the uncapped guard has been lost and the documented way back to un-guarded behaviour no longer works.
- No agent Message is ever accepted and `T-A` fills with refusals -> zero is being read as 'no replies allowed', which silently mutes every Agent in every Room while looking like a configuration that merely turns the cap off.
- A grey `0 of 0 …` line appears -> the note is rendering for an uncapped Room, which is noise the design deliberately suppresses.

**Inconclusive if**

If replies stop arriving mid-run, check that each bot terminal is still running (a disconnected bot prints `Server closed the connection.`) and still printing an envelope per Message before concluding anything: a disconnected bot produces the same silence as a refusal but leaves no warning in `T-A`.

> [!NOTE]
> This setting exists only as configuration — there is no control for it on /settings or in any Room, and it is bound at startup, so a change needs a restart. Never leave it at 0 for a paid run: with real Personas it restores unbounded spend.

### REPLYGATEBUDGET-17 — The model-facing wording of this area is readable and editable at /settings/prompts

**Free** · about 8 min

*Proves the text that tells a model when to answer and what a refusal means is visible to the Human, that an override file is written only on a save, and that the fields which need a restart to take effect are badged as such.*

**Before you start**

- The app is running under `P-LAUNCH-FREE` (no Agent is needed for this test).
- No `prompts.json` exists yet: confirm with `Test-Path <data>\prompts.json` — it should print `False` on a fresh install.

**Steps**

1. Run `Test-Path <data>\prompts.json` and note the result.
2. Click **Settings** in the sidebar.
3. Confirm the **Prompts** tab is the one already selected, then find the field labelled **Chat rules**.
4. Read its default text.
5. Find the fields labelled **Help: Rooms**, **Help: mentions**, **Help: replying** and **Help: budget** and read the default text of **Help: budget**.
6. Look for a small badge beside the **Chat rules** label and hover it.
7. Look for the same badge beside **Help: budget**.
8. Append the word ` TESTEDIT` to the end of the **Chat rules** text.
9. Note the badge that appears beside the field once it is edited, then click **Save**.
10. Run `Get-Content <data>\prompts.json`.
11. Remove ` TESTEDIT` again and click **Save**.

**Pass if — all of these**

- Step 1 prints `False` — no override file exists until a save.
- **Chat rules** default text (hard line breaks in the editor are not part of the match; read it as running prose, `PromptCatalog.cs:86-92`) begins `A Room with two members is a private conversation with the human: answer every message. A Room with three or more members is a group: answer only when you are @-mentioned.`
- **Help: budget** default text contains the sentence `A refusal that says the budget is spent is final: do not retry it, and do not work around it by posting to another Room.`
- **Chat rules** carries a badge reading `Next session`, whose tooltip says it applies to agents started after the change.
- **Help: budget** carries no `Next session` badge (its Timing is Live, `PromptCatalog.cs:633-650`).
- After the edit and before saving, the field shows an `Unsaved` badge; the **Save** button is enabled only while there is something to save.
- After saving, `prompts.json` exists and contains ONLY the one changed key — not the full set of defaults.

**Fail if — any of these**

- `prompts.json` exists before you have ever saved -> absent no longer means 'nothing overridden', so a future default change would never reach an installed system.
- `prompts.json` contains every prompt after saving one -> the same problem: every default is frozen at whatever shipped the day of the first save.
- **Chat rules** has no `Next session` badge -> the Human is given no signal that their edit will not reach a running Agent until it restarts, so an edit that silently never takes effect looks like a broken prompt.
- A `Next session` badge appears on the `Help:` fields -> the badge is decoration rather than a statement about timing, which makes it worthless on the fields that need it.

**Inconclusive if**

If the Prompts tab shows no fields at all, the prompt catalog failed to load — check `T-A` for an error and stop. If `prompts.json` already existed at step 1 from a previous test, the 'only on first save' half of this test is inconclusive: delete the file, restart the app and begin again.

> [!NOTE]
> Always undo the edit (step 11). Leaving `TESTEDIT` in the Chat rules prompt changes what every real Persona is told in every later paid test.

### REPLYGATEBUDGET-18 — The server labels a delivery correctly even when the client chooses to stay silent

**Free** · about 8 min

*Establishes the wire-envelope oracle that every remaining free test depends on, and separates 'the label was wrong' from 'the client decided not to act' once and for all.*

**Before you start**

- No app running.
- PowerShell 7 available as `pwsh`.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B: `$env:Team__Acp__Enabled = 'false'`, `$env:Team__AgentMessageBudget = '40'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open a second PowerShell terminal — call it `T-B` — and run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`. Open a third terminal, `T-C`, and run `pwsh <repo>\tools\echo-bot.ps1 -Name Bo`.
4. Confirm each terminal prints `Sent hello. Listening for messages (Ctrl+C to exit)...`.
5. Open `http://localhost:5100`; Rooms named `Ada` and `Bo` appear in the sidebar with no refresh. Click `Ada`.
6. Type `/invite @Bo` and press Enter, and confirm the heading becomes `Ada, Bo`. The bot replies in a Room of two Members or fewer even without a Mention (`tools/echo-bot.ps1:78`), so a second Member is what makes `Ada` Mention-gated.
7. Type `hello` and press Enter.
8. Read the newest JSON line printed in `T-B` and in `T-C`, and the Room view.
9. Type `hello @Ada` and press Enter.
10. Read the newest JSON line printed in `T-B` and in `T-C`, and the Room view.

**Pass if — all of these**

- A Room named `Ada` appears in the sidebar within a few seconds of starting the bot, without a page refresh.
- The envelope from step 8, in both `T-B` and `T-C`, has `"type":"messagePosted"`, `"mentioned":false`, a `"members"` array of exactly three entries, `"agentMessagesSinceHuman":0` and `"budget":40`.
- Nothing appears in the Room after step 7 — both bots received a correct label and chose not to act.
- The envelope from step 10 in `T-B` has `"mentioned":true` and a `"mentions"` array of exactly one entry naming `Ada`; the copy in `T-C` has `"mentioned":false` and the same `"mentions"` array.
- A reply from `Ada`, and none from `Bo`, appears in the Room after step 10.

**Fail if — any of these**

- The step-8 envelope carries `"mentioned":true`, or either bot replies to `hello` -> the server is labelling an un-named recipient as Mentioned; every Agent in every group Room would be woken by every Message.
- The `"members"` array is shorter or longer than the Room's actual membership -> the Reply Gate's Room rule is being fed the wrong count, so a group Room can behave as a private one or vice versa.
- `"budget"` does not match the configured value -> clients are being told the wrong allowance, and an Agent that implements the gate will stop too early or too late.
- No envelope at all is printed at step 8 -> the bot is not a Member or not connected; that is an infrastructure failure, not a labelling one.

**Inconclusive if**

If `T-B` prints a line containing `"code":"invalidName"`, the Name was rejected — check for a leading or trailing space in what you typed. If the bot exits with a connection error, the app is not running or its pipe name differs: confirm the app started before the bot. If a Room named `Ada` was already there before the bot started, it is left over from an earlier test: stop everything, RESET and start again.

> [!NOTE]
> Keep `T-B` open; the next several tests reuse this exact oracle. The silence at step 7 is correct client behaviour in a Room of three, not a gate defect; in a Room of two the same bot would have answered `hello` (`tools/echo-bot.ps1:78`).

### REPLYGATEBUDGET-19 — The longest handle wins: @Emily Lee reaches Emily Lee, never Emily

**Free** · about 12 min

*Catches the headline silent failure of this area — a Mention of a two-word Name being read as a Mention of the one-word Name plus a stray word — and proves a multi-word Name survives an /invite.*

**Before you start**

- No app running.
- PowerShell 7 available.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Emily`.
4. In a third terminal — TERMINAL C — run `pwsh <repo>\tools\echo-bot.ps1 -Name "Emily Lee"` (the quotes matter).
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
- Step 7 produces a red strip reading `Unknown agent @Emily` -> the invite command truncated the Name at the first space, so no Agent whose Name contains a space can ever be invited from the composer.
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
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Emily`.
4. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
5. Open `http://localhost:5100` and click the Room named `Emily`.
6. Type `/invite @Ada` and press Enter; confirm the heading becomes `Emily, Ada` so the Room is Mention-gated.
7. Confirm the member line names three people and that `Emily Lee` is NOT one of them.
8. Type `@Emily Lee any news?` and press Enter, then wait 5 seconds.
9. Read `T-B`'s newest line and the Room.

**Pass if — all of these**

- Exactly one reply appears, from `Emily`.
- `T-B`'s envelope for that Message carries `"mentioned":true`.
- TERMINAL C's copy of the same envelope carries `"mentioned":false` and `Ada` stays silent.
- The trailing word `Lee` is simply part of the text and causes nothing.

**Fail if — any of these**

- Nobody replies -> the parser refused to match because a longer handle looked like it should have won, so a Mention that names a real Member of this Room resolves to nobody with no error anywhere: the Human sees only silence.
- `Ada` replies -> the Mention resolved to the wrong Member.
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
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name "Chief of Staff"`.
4. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
5. Open `http://localhost:5100`, click the Room named `Chief of Staff`, type `/invite @Ada` and press Enter.
6. Confirm the heading is now `Chief of Staff, Ada` and the member line names three people.
7. Type `@Chief of Staff and @Ada` and press Enter, then wait 6 seconds.
8. Read both terminals and the Room.
9. Type `thanks @Chief of Staff!` and press Enter, then wait 5 seconds, and read both terminals.
10. Type `@Chief of Staff.` and press Enter, then wait 5 seconds, and read both terminals.

**Pass if — all of these**

- Step 7 produces exactly TWO replies, one from `Chief of Staff` and one from `Ada`; the single envelope printed in both terminals carries a `"mentions"` array of two entries, and each terminal's own copy shows `"mentioned":true`.
- Step 9 produces exactly ONE reply, from `Chief of Staff`.
- Step 10 produces exactly ONE reply, from `Chief of Staff`.
- `Ada` never replies at steps 9 or 10.

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

*Prevents the expensive false positive — waking an Agent every time someone pastes an email address or writes a longer word starting with their Name — without over-blocking the legitimate case.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ad`.
4. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name example`.
5. In a fourth terminal — TERMINAL D — run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
6. Open `http://localhost:5100`, click the Room named `Ad`, type `/invite @example` and press Enter, then type `/invite @Ada` and press Enter.
7. Confirm the heading reads `Ad, example, Ada` and the member line names four people.
8. Type `@Ada` and press Enter, wait 5 seconds, then read all three terminals and the Room.
9. Type `mail me@example.com` and press Enter, wait 5 seconds, then read all three terminals and the Room.
10. Type `see-@Ada` and press Enter, wait 5 seconds, then read all three terminals and the Room.
11. Type `you@@Ada` and press Enter, wait 5 seconds, then read all three terminals and the Room.

**Pass if — all of these**

- Step 8: exactly one reply, from `Ada`. `T-B` (`Ad`) shows `"mentioned":false` for that envelope.
- Step 9: no reply at all. TERMINAL C (`example`) shows `"mentioned":false`.
- Step 10: exactly one reply, from `Ada`. TERMINAL D shows `"mentioned":true` — a hyphen before the `@` is deliberately not a blocker.
- Step 11: no reply at all. TERMINAL D shows `"mentioned":false`.

**Fail if — any of these**

- `Ad` replies to `@Ada` -> a Mention is not ending at a word boundary, so any Agent whose Name is a prefix of another is woken by Mentions meant for the longer one; with a real Persona that is a billed Turn for nothing.
- `example` replies to `mail me@example.com` -> every pasted email address wakes an Agent. This is the most expensive false positive in the product.
- `see-@Ada` gets no reply -> `-` was wrongly added to the blocked set, so a legitimate Mention after a hyphen silently reaches nobody.
- `you@@Ada` produces a reply -> the doubled `@` is not being treated as joining two things, so text like an escaped handle wakes an Agent.

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
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
4. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Bo`.
5. Open `http://localhost:5100`, click the Room named `Ada`, type `/invite @Bo` and press Enter, and note the Room id from the URL.
6. Type `@Ada hi` and press Enter.
7. Wait 30 seconds without touching anything.
8. Read `T-B` carefully: find the envelope for your own Message, and look for any envelope carrying `Ada`'s own reply text.
9. Read TERMINAL C.
10. Run `(Get-Content <data>\rooms\<RoomId>.jsonl).Count`, wait 20 seconds, and run it again.

**Pass if — all of these**

- Exactly one reply appears in the Room, from `Ada`, and no further Messages arrive during the 30-second wait.
- `T-B` contains NO `messagePosted` envelope carrying `Ada`'s own reply text — the sender is never delivered its own Message.
- TERMINAL C DOES contain that envelope, with `"mentioned":false`.
- The two line counts in step 10 are identical.
- `Ada`'s reply text contains no `@` character.

**Fail if — any of these**

- The line count keeps rising and Messages keep arriving -> an Agent is being delivered its own Message, or the reply still carries a Mention; this is the unbounded self-echo that once produced thousands of messages in seconds. STOP THE APP IMMEDIATELY with Ctrl+C in `T-A` and in every bot terminal.
- `T-B` shows an envelope containing its own reply -> the sender-skip on delivery is gone; only the pipe bot's `@`-stripping is preventing a loop, and a client that quoted faithfully would loop forever.
- TERMINAL C shows no envelope for `Ada`'s reply -> other Members are not being delivered an Agent's Message at all, which breaks group conversation entirely.

**Inconclusive if**

If neither bot replies, nothing is being exercised — get a reply working (REPLYGATEBUDGET-18) first. If the Room has only two Members, `Bo` never joined; check the member line.

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
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
4. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Bo`.
5. Open `http://localhost:5100`, click the Room named `Ada`, type `/invite @Bo` and press Enter.
6. Type `@Ada @Bo go` and press Enter.
7. Wait 10 seconds.
8. Read both terminals and find the line whose `"type"` is `error` — that is the wire discriminator a `ProtocolError` serialises to (`Messages.cs`: `[JsonDerivedType(typeof(ProtocolError), "error")]`), so grepping for `protocolError` finds nothing.
9. Copy that line's `"message"` value out in full and compare it character for character with the sentence in the pass conditions.
10. Read `T-A`.

**Pass if — all of these**

- Exactly one of the two bot terminals prints an envelope with `"type":"error"`; the other prints none.
- That envelope carries `"code":"budgetExhausted"`.
- Its `"message"` reads exactly: `This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.`
- `T-A` shows exactly one `refused a message from` warning naming the same Agent.
- Exactly one agent Message is visible in the Room.

**Fail if — any of these**

- The message says anything like 'try again later', 'temporarily', or names a wait -> a model will read it as transient and retry, spending the Turn the refusal exists to save, and may route around it into another Room which has a full Budget of its own.
- No `"code"` field, or a different code -> a client cannot distinguish a Budget refusal from an unknown Room or a malformed Message, so it cannot stop for the right reason.
- Both terminals print a `"type":"error"` envelope -> neither post was accepted, meaning the Room was already at its cap before your Message reset it.
- Neither prints one, yet only one Message landed -> the second Agent never attempted a post, so nothing was actually refused (see INCONCLUSIVE).

**Inconclusive if**

If only one bot is connected, only one post is attempted and no refusal can occur: confirm both Rooms appeared in the sidebar and both terminals printed `Sent hello`. If both replies landed, the budget env var did not take effect — check the value printed nowhere but confirmable by the pause block appearing; restart with `$env:Team__AgentMessageBudget = '1'` set BEFORE `dotnet run`.

> [!NOTE]
> The same sentence must also come back from the Agent-facing post tool; that second door is covered in the paid REPLYGATEBUDGET-35, because only a real model can be observed obeying it.

### REPLYGATEBUDGET-25 — Continue re-delivers the last Message to the other Agents, once, with the raised Budget

**Free** · about 9 min

*Proves granting Budget actually wakes the Room rather than only raising a number nothing reads, that the re-delivery carries the new allowance, and that the sender is still skipped.*

**Before you start**

- REPLYGATEBUDGET-24 has just been run: the app is on PROFILE B with budget `1`, bots `Ada` and `Bo` are connected, and the Room `Ada, Bo` shows the pause block.

**Steps**

1. In the Room, note the exact text of the last Message and which Agent sent it.
2. Note the Room id from the URL and run `(Get-Content <data>\rooms\<RoomId>.jsonl).Count`.
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
- If the re-delivered Message names nobody (the usual case with the echo bots), a quiet blue note above the composer reads `Budget granted and the last message was delivered again, but it names no agent. Mention one by name to get an answer.` (`Chat.razor:181`). Its absence is not a failure when the Message did name an Agent; it is the observation, not the oracle.

**Fail if — any of these**

- No terminal shows a second envelope -> Continue only raised a number; nothing will ever read it, so a paused Room stays stopped and the button does nothing the Human can see.
- The second envelope carries the OLD budget figure -> a client that implements the gate will decline it again immediately, so the extension is inert even though the re-delivery went out.
- The Message appears a second time in the transcript, or the `.jsonl` gains a line -> the re-delivery is being treated as a new post; it would then count against the Budget and be shown twice to the Human.
- The sender's terminal also receives the copy -> the skip-the-sender rule is gone, which reopens the self-echo loop at every Continue.

**Inconclusive if**

Whether the woken Agent actually replies depends on the re-parsed Mentions in that Message: the echo bots strip `@` from what they quote, so the re-delivered Message usually names nobody and the woken bot correctly stays quiet. Judge this test on the envelope, the log line and the two counts — never on whether a new reply appears.

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
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
4. Open `http://localhost:5100` and click the Room named `Ada`. Confirm the member line reads `You, Ada` — two Members only.
5. Type `@Ada hi` and press Enter and wait for the reply and the pause block.
6. Note the Message count in the transcript.
7. Click **Continue** and wait 15 seconds.
8. Read the Room, `T-B` and `T-A`.

**Pass if — all of these**

- BEFORE clicking at step 7, the pause block carries an extra grey line reading `Continue will
  deliver the last message again, and it is an agent's own — an agent is never delivered its
  own message, so this will wake nobody. Say something instead.` The Human is told the button
  cannot work BEFORE pressing it, which is the whole of product observation 5.
- `T-A` shows exactly one `Room '…' was extended to 2 agent messages.` line — the grant definitely happened.
- AFTER the click, a quiet blue note reads `Budget granted, but there was nobody to wake. Say
  something to start the room again.`
- The pause block is replaced by a grey line reading `1 of 2 agent replies since you last spoke.`
- `T-B` prints NO new envelope: the last Message in the Room was `Ada`'s own, and an Agent is never delivered its own Message.
- No new Message appears in the Room and the transcript count is unchanged.

**Fail if — any of these**

- No `was extended to` line appears in `T-A` -> Continue genuinely did nothing: the grant itself is broken.
- `T-B` prints a copy of `Ada`'s own reply -> the sender-skip is gone.
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
- No Persona that would clash: `<data>\Agents\` has no `Jarvis` folder. A Persona lives at `<data>\Agents\<Name>\<Name>.md` (`AgentPaths.cs`); `<data>\Teams\` holds Team folders and Tasks, never Personas. The built-in `Chief of Staff` folder is always there and is not a clash.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (`Team__Acp__Enabled` MUST be `false` — no model is needed for this test and none must start).
3. Open `http://localhost:5100` and start `P-NEW-PERSONA` (the New agent card on a Team's Agents tab).
4. Fill in Name `Jarvis`, Title `Butler`, Alias `jar`, and type `You are Jarvis.` into the Persona text.
5. Leave Model and Effort at `Agent default`, select **Save draft**, then **Save changes**. For no Team, finish with `P-NEW-PERSONA`'s **Remove from team** step.
6. Confirm Ctrl K finds `Jarvis` as an Agent, that the **Files that didn't load** section at the top of the Agents tab either is absent or does not list `Jarvis.md`, and that `<data>\Agents\Jarvis\Jarvis.md` now exists.
7. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Jarvis`.
8. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
9. Click the Room named `Jarvis` in the sidebar, type `/invite @Ada` and press Enter so the Room has three Members.
10. Type `@jar do the thing` and press Enter, wait 5 seconds, then read `T-B` and the Room.
11. Type `@Jarvis do the thing` and press Enter, wait 5 seconds, then read `T-B` and the Room.
12. Type `x@jar` and press Enter, wait 5 seconds, then read `T-B`.
13. Type `@jarring` and press Enter, wait 5 seconds, then read `T-B`.
14. Click the Room named `Ada` in the sidebar, type `/invite @jar` and press Enter, and read the strip.

**Pass if — all of these**

- Step 10 produces exactly one reply, from `Jarvis`; `T-B`'s envelope carries `"mentioned":true` and its `"mentions"` array names `Jarvis`, NOT `jar`.
- Step 11 behaves identically — the Alias and the Name are interchangeable.
- Steps 12 and 13 each produce `"mentioned":false` in `T-B` and no reply.
- Step 14 shows a green strip reading `Invited jar. Room is now "Ada, Jarvis".` — an Alias is accepted anywhere a Name is.

**Fail if — any of these**

- `@jar` reaches nobody while `@Jarvis` works and the Persona file loaded cleanly -> Aliases are not being folded into Mention resolution at all, so every shorter handle silently reaches nobody.
- `@jarring` or `x@jar` wakes `Jarvis` -> an Alias is not respecting word boundaries, which is the same expensive false positive as REPLYGATEBUDGET-22 with a shorter, more collision-prone handle.
- The `"mentions"` array names `jar` rather than `Jarvis` -> the wire is carrying something that is not a Member, which no client can resolve.
- Step 14 reports `Unknown agent @jar` -> the invite path does not resolve Aliases, so the two ways of naming an Agent disagree.

**Inconclusive if**

If `Jarvis.md` appears under **Files that didn't load**, read the reason there and fix it — the Alias cannot resolve from a file that did not load, and the test proves nothing. If the pipe client's Name and the Persona Name do not match exactly (`Jarvis`), the Alias has no Member to point at and its silence is CORRECT, not a defect: Aliases are library-wide but resolve only against the Members of the Room. If a real model process starts, `Team__Acp__Enabled` was not `false` — Ctrl+C immediately.

> [!NOTE]
> Delete the folder `<data>\Agents\Jarvis\` when finished (or run `P-RESET-TEAMS`), or later tests will see an extra Persona. Do not delete anything under `<data>\Teams\`.

### REPLYGATEBUDGET-28 — A longer Alias beats a shorter Name, and an equal-length Name beats an Alias

**Free** · about 15 min

*Pins two tie-breaks that nothing in the compiler protects and whose failure is a silent mis-routing to the wrong Agent.*

**Before you start**

- No app running.
- `<data>\Agents\` has no `Other` or `Jarvis` folder left by an earlier test. The built-in `Chief of Staff` folder stays.

**Steps**

1. Run `P-RESET-ROOMS`, then delete any leftover `Other` and `Jarvis` folders under `<data>\Agents\`. Do not touch `<data>\Teams\` (Team folders and Tasks) or the `Chief of Staff` folder.
2. Start the app with PROFILE B (`Team__Acp__Enabled` MUST be `false`).
3. CASE A. Create an Agent with `P-NEW-PERSONA` (no Team, as in REPLYGATEBUDGET-27): Name `Other`, Title `Someone else`, Alias `Emily Lee`, Persona text `You are Other.`, then **Save draft** and **Save changes**.
4. Confirm `Other.md` is not listed under **Files that didn't load**.
5. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Emily`; in TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Other`.
6. Click the Room named `Emily`, type `/invite @Other` and press Enter, and confirm three Members.
7. Type `@Emily Lee any news?` and press Enter, wait 5 seconds, then read both terminals and the Room.
8. CASE B. Create an Agent with `P-NEW-PERSONA` (no Team): Name `Jarvis`, Title `Butler`, Alias `Jar`, Persona text `You are Jarvis.`, then **Save draft** and **Save changes**.
9. In a fourth terminal — TERMINAL D — run `pwsh <repo>\tools\echo-bot.ps1 -Name Jar`; in a fifth — TERMINAL E — run `pwsh <repo>\tools\echo-bot.ps1 -Name Jarvis`.
10. Click the Room named `Jar`, type `/invite @Jarvis` and press Enter, and confirm three Members.
11. Type `@Jar are you there?` and press Enter, wait 5 seconds, then read TERMINAL D and TERMINAL E and the Room.

**Pass if — all of these**

- CASE A: exactly one reply, from `Other`. TERMINAL C shows `"mentioned":true`; `T-B` (`Emily`) shows `"mentioned":false`. The longer Alias beat the shorter Name.
- CASE B: exactly one reply, from `Jar`. TERMINAL D shows `"mentioned":true`; TERMINAL E (`Jarvis`) shows `"mentioned":false`. The equal-length Name beat the Alias.

**Fail if — any of these**

- CASE A routes to `Emily` -> Names and Aliases are being matched in two separate passes rather than sorted together, so a short Name that happens to sit at a word boundary steals a longer Alias's Mention. Silent: the wrong Agent simply answers.
- CASE B routes to `Jarvis` -> the tie between an equal-length Name and Alias is being won by the Alias; an Agent can then be shadowed out of their own Name by somebody else's shorthand. Also silent.
- BOTH reply in either case -> the scan did not advance past the handle it consumed.

**Inconclusive if**

If either Persona file is listed under **Files that didn't load**, read the reason: an Alias that duplicates another Persona's Name is rejected by design, so Case B cannot be built from two Persona files — it needs a pipe client named `Jar` (as written) alongside a Persona `Jarvis`. If the bot Names do not exactly match the Persona Names, the Aliases resolve to nobody and both cases are inconclusive. If a Room already contains an unexpected fourth Member from an earlier invite, RESET and rebuild.

> [!NOTE]
> Delete the `Other` and `Jarvis` folders from `<data>\Agents\` when finished. These two orderings are decided by a stable sort over one combined candidate list; there is no compiler or analyser signal if that ordering is ever swapped, which is why they are tested by hand.

### REPLYGATEBUDGET-29 — Reconnecting re-attaches to the same Room, but after an invite a fresh two-Member Room appears

**Free** · about 10 min

*Proves an Agent does not multiply Rooms on every reconnect, and that once its Room has become a group it gets a new private Room rather than silently inheriting the group.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B (budget `40`).
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada` and wait for a Room named `Ada` to appear in the sidebar.
4. Count the Rooms in the sidebar and write the number down.
5. In `T-B` press Ctrl+C, then run the same command again and wait 10 seconds.
6. Count the Rooms in the sidebar again.
7. In TERMINAL C run `pwsh <repo>\tools\echo-bot.ps1 -Name Bo`.
8. Click the Room named `Ada`, type `/invite @Bo` and press Enter, and confirm the heading becomes `Ada, Bo`.
9. In `T-B` press Ctrl+C, then run the `Ada` bot command again and wait 10 seconds.
10. Read the sidebar and `T-A`.

**Pass if — all of these**

- At step 6 the Room count is UNCHANGED from step 4 — a plain reconnect re-attaches rather than creating a second Room.
- At step 10 a NEW Room named `Ada` appears alongside the Room named `Ada, Bo`.
- `T-A` shows `Created direct room '…' for agent 'Ada'.` exactly once for that new Room.
- The new Room's member line reads `You, Ada` — two Members.

**Fail if — any of these**

- A new Room appears on the plain reconnect at step 5 -> every disconnect multiplies Rooms, and a long-running Agent fills the sidebar with duplicates.
- No new Room appears at step 9 and the Agent re-attaches to the three-Member `Ada, Bo` Room -> the exact-members lookup matched a Room that is no longer private, so the Agent's private conversation is silently merged into a group and everything it says there is Mention-gated.
- `T-A` shows more than one `Created direct room` line for one reconnect -> Rooms are being created more than once per registration.

**Inconclusive if**

If the bot fails to reconnect (the terminal shows a connection error), nothing is being tested — confirm the app is still running in `T-A`. If the sidebar does not update without a refresh, note that separately (it is the rooms-changed event, covered in REPLYGATEBUDGET-02) and reload before counting.

> [!NOTE]
> Membership alone defines what a private Room is, which is why the second half of this test has to create a new Room rather than reuse the old one.

### REPLYGATEBUDGET-30 — A Mention that reaches nobody is completely silent — and how to tell that apart from a bug

**Free** · about 8 min

*Teaches the disambiguation every other test in this area depends on, and pins that the silence itself is the design rather than a defect to file.*

**Before you start**

- The app is running under PROFILE B with bots `Ada` and `Bo` connected and both in one Room of three or more Members.

**Steps**

1. Type `@nobodyhere hello` and press Enter, then wait 10 seconds and watch the whole page.
2. Read both bot terminals for that Message.
3. In the sidebar click **New room** and note the colour of the dot beside each Agent, then click **New room** again to close the panel. (This panel lists every Agent, Members or not, with its presence dot. The Room's **Invite agent** panel lists only Agents who are not yet Members, so Ada and Bo are not in it.)
4. In `T-B` press Ctrl+C to take `Ada` offline, and wait 5 seconds.
5. Click **New room** again and note the colour of the dot beside `Ada`, then close the panel.
6. Type `@Ada are you there?` and press Enter, then wait 10 seconds.
7. Read `T-B` (now stopped) and TERMINAL C.
8. Restart the `Ada` bot in `T-B` and repeat step 6.

**Pass if — all of these**

- Step 1's Message posts normally and produces no composer error, no highlight, and no indication
  that the `@` in particular matched nobody. It DOES produce the quiet blue note above the
  composer reading `No agent was @-mentioned — name one to ask for a reply.` — which is
  correct and is not about the unresolvable handle: the same note appears for a plain
  `hello` in this Room. The product still never reports an unresolvable Mention as a failure.
- At step 6, with `Ada` offline and `Bo` online, there is NO note at all. An Agent WAS
  named, so "no agent was @-mentioned" would be false; the Room stays silent rather than say
  something untrue. Explaining that `Ada` is offline is deliberately not built - see
  `docs/engineering/known-limits.md`.
- Both terminals show that envelope with `"mentioned":false`.
- At step 3 both dots are green (`agent-dot online`, the Success colour mixed 20% toward black, with a white check).
- At step 5 the badge beside `Ada` is a hollow grey ring — `agent-dot offline` (its hover title reads `offline`). It is not red; red is Degraded's dash.
- At step 6, with `Ada` offline, TERMINAL C still receives the envelope (with `"mentioned":false`) while `T-B` receives nothing at all — the Agent is a Member but not connected, so no Envelope is queued for it.
- After restarting at step 8, the same Message produces a reply.

**Fail if — any of these**

- An error or warning appears for `@nobodyhere hello`, or any note that singles out the
  unresolvable handle -> the product is reporting an unresolvable Mention as a failure, which
  will fire on ordinary prose containing an `@`. The generic context-only note is not this.
- A note appears at step 6, while `Ada` is offline and named -> the Room is claiming no Agent
  was mentioned when one was.
- `@Ada are you there?` produces a reply while `Ada` is stopped -> something other than the live connection is answering.
- After restarting `Ada`, the Messages it missed while offline are delivered to it -> there is no queue in this version by design; a queue appearing would change the whole delivery model.

**Inconclusive if**

This test is itself the disambiguation procedure, so it cannot be inconclusive in the usual sense — but note the rule it teaches: NO envelope in a terminal means the Agent is not a Member or not connected; an envelope with `"mentioned":false` means it IS a Member and the parser decided against it. Never conclude anything about mention resolution without checking which of the two you are looking at.

> [!NOTE]
> DO NOT FILE the silence itself. It is the design: offline Agents receive nothing and there is no queue. But this silence is also the shape every genuine mention-resolution bug takes, which is why every test in this area names a terminal or a file as its oracle.

### REPLYGATEBUDGET-31 — PAID: a real Persona answers a two-Member Room with no Mention at all

**💰 Spends money** · about 15 min

*The only way to observe the Reply Gate's answer-everything branch inside a real model's Turn, because the free lanes cannot show what a model does with it. Proves a private conversation does not require the Human to @-name their Agent in every sentence. The New agent and Edit cards used here are real `MudDialog`s and their Model/Effort dropdowns are `MudSelect` (Stages 3-4 of the MudBlazor migration); neither changes what this test is checking.*

**Before you start**

- No app running.
- You are willing to spend ONE real, billed model Turn for Ada, plus the Chief of Staff's Greeting Turn (Haiku / low once step 3 is done).
- `<data>\Agents\` holds no `Ada` folder (a Persona lives at `<data>\Agents\<Name>\<Name>.md`) and nothing but the built-in `Chief of Staff` besides.
- The built-in Chief of Staff is made cheap before the paid launch (step 3). It is seeded at every start with no stored Model, and with ACP on and an empty direct Room it takes a Greeting Turn at the account-default Model, which is billed (`PersonaRunner.cs:481-495`). `P-RESET-ROOMS` deletes `team.db`, which holds the stored Models, so this has to be redone after every reset. The same applies to REPLYGATEBUDGET-32 to -36.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with `P-LAUNCH-FREE` (`$env:Team__Acp__Enabled = 'false'`), only so you can create the Persona without starting a model.
3. Open `http://localhost:5100` and create an Agent with `P-NEW-PERSONA`: Name `Ada`, Title `Analyst`, Alias `ada1`, Persona text `You are Ada, a concise analyst. Answer in one short sentence.` Then open the **Chief of Staff** card (`P-OPEN-AGENT`), choose **Edit**, set **Model** to the Haiku entry and **Effort** to `low`, and **Save**.
4. Stop the app with Ctrl+C.
5. Start the app with PROFILE C: `$env:Team__Acp__Enabled = 'true'`, `$env:Team__AgentMessageBudget = '4'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
6. Open `http://localhost:5100`, open the `Ada` card (`P-OPEN-AGENT`), and wait until it reads **Online**.
7. On the `Ada` card click **Edit**, set **Model** to the entry whose name contains `Haiku`, set **Effort** to `low`, and click **Save**. Wait until the card reads **Online** again.
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

- No reply arrives and `Ada` still reads Online and no warning appears in `T-A` -> the member-count branch of the Reply Gate is broken: the Message was treated as not-for-me and quietly buffered instead of answered. This is invisible from the UI, which is exactly why it needs a manual test.
- `T-A` shows `Persona 'Ada' declined a turn in room …: the room has spent its budget …` -> a stale in-memory Budget is blocking the very first Message; restart and retry once before filing.
- A reply arrives but no grey budget line does -> the Room view is not reading the Budget (see REPLYGATEBUDGET-06).

**Inconclusive if**

If the `Ada` card reads **Offline** or **Degraded**, read the reason line on its card and fix that first — an offline Persona is silent for a reason that has nothing to do with the gate. If the status never leaves **Starting**, the agent process is failing to launch; check `T-A`. If the Model dropdown offers no Haiku entry, stop and ask before selecting anything else: this test must not run on a larger model.

> [!NOTE]
> COST: one short billed Turn at Haiku/low — a few seconds of model time — plus the Chief of Staff's Greeting Turn, which is also Haiku/low once step 3 has been done. Leave the app running if you intend to do REPLYGATEBUDGET-32 next; it reuses this exact setup.

### REPLYGATEBUDGET-32 — PAID: an un-mentioned Message is not answered but rides along as context on the next Mention

**💰 Spends money** · about 15 min

*Proves Catch-up: nothing reaches the model until the Persona is Mentioned, and when it finally is, the Messages it stayed silent for are in its prompt.*

**Before you start**

- REPLYGATEBUDGET-31 has passed and the app is still running on PROFILE C with `Ada` Online at Haiku/low.
- You are willing to spend ONE more billed Turn.

**Steps**

1. In a spare terminal run `pwsh <repo>\tools\echo-bot.ps1 -Name filler` so there is a second Agent to make a Room Mention-gated.
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
  `No agent was @-mentioned — name one to ask for a reply.` It is replaced by nothing once step 6's
  Mention lands. This is the cheapest confirmation that the Catch-up path was taken deliberately
  rather than the delivery having failed.
- The reply to step 6 names BOTH earlier facts — the Friday deadline and the 10 percent cut.
- The grey budget line afterwards reads `1 of 4 agent replies since you last spoke.` — only one Turn was taken for three Messages.

**Fail if — any of these**

- `Ada` replies to either un-mentioned Message -> the absolute rule that nothing reaches the model until it is Mentioned has broken; in a busy group Room every Agent would take a billed Turn on every Message.
- The reply knows nothing of the earlier Messages -> the Catch-up buffer is being dropped rather than carried, so an Agent joining a conversation mid-way answers blind.
- Two or three Turns appear in `T-A` for the three Messages -> the un-mentioned Messages each bought a Turn.

**Inconclusive if**

If the app or the Persona restarted, or her session in this Room was stopped, between step 4 and step 6, the Catch-up buffer is gone but a new Room session's first Turn carries the last 20 Transcript Messages (`Acp:TranscriptCatchUpMessages`, `AcpOptions.cs:173`; `RoomSession.cs:909`). The reply will then usually know both facts without Catch-up, so that run says nothing about the buffer — check `T-A` for Persona start lines before judging, and repeat the test without a restart. The same applies if `Ada` was Offline when you posted the un-mentioned Messages: the buffer is filled only from deliveries she actually received. Neither is a defect.

> [!NOTE]
> COST: one short billed Turn. The two un-mentioned Messages cost nothing, which is itself part of what this test proves. The buffer holds at most 20 Messages per Room, oldest discarded first, and is emptied silently by any restart (after which a new session's first Turn is seeded from the Transcript instead — see Inconclusive). One session exists per Persona per Room, so this test is about the `Ada` Room's session only.

### REPLYGATEBUDGET-33 — PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn

**💰 Spends money** · about 25 min

*The containment test for the whole area, plus the one observation that separates the cheap layer from the expensive one: at the cap the next Agent must not start a Draft at all, because a Draft means a Turn was already paid for.*

**Before you start**

- No app running.
- You are willing to spend roughly SIX short billed Turns, plus the Chief of Staff's Greeting Turn.
- The Chief of Staff is Haiku / low before the paid launch (step 2 below), because `P-RESET-ROOMS` cleared its stored Model and its Greeting would otherwise bill at the account default.
- You will stay at the keyboard for the whole run.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with `$env:Team__Acp__Enabled = 'false'` temporarily and create two Personas with `P-NEW-PERSONA`: Name `Ana`, Title `Planner`, Alias `ana1`, body `You are Ana. Keep the conversation going with @Ben. Always end your message by asking @Ben one short question. One or two sentences only.` and Name `Ben`, Title `Builder`, Alias `ben1`, body `You are Ben. Keep the conversation going with @Ana. Always end your message by asking @Ana one short question. One or two sentences only.` Then open the **Chief of Staff** card (`P-OPEN-AGENT`), choose **Edit**, set **Model** to the Haiku entry and **Effort** to `low`, and **Save**.
3. Stop the app with Ctrl+C.
4. Start the app with PROFILE C and `$env:Team__AgentMessageBudget = '6'` — set this BEFORE `dotnet run`.
5. Open each card (`P-OPEN-AGENT`), wait until both read **Online**, then set each one's **Model** to the Haiku entry and **Effort** to `low` via **Edit** and **Save**.
6. In the sidebar click **New room**, tick both `Ana` and `Ben`, and click **Open room**.
7. Confirm the member line names three people.
8. Type `@Ana start a short conversation with @Ben about picking a meeting time.` and press Enter.
9. Watch the Room continuously. Count every agent Message as it lands and keep your hand near Ctrl+C in `T-A`.
10. When the exchange stops, read the area between the transcript and the composer.
11. Read `T-A` in full.
12. Note the Room id and run `Get-Content <data>\rooms\<RoomId>.jsonl` and count the agent lines after your Message.

**Pass if — all of these**

- The exchange halts after exactly SIX agent Messages — never seven.
- The pause block appears reading `Agents have sent 6 replies since you last spoke, and are paused.` with **Continue** and **Leave paused**.
- The last Agent to be delivered a Message does NOT show a streaming row or a **Stop** button: no Draft is started at all after the cap is reached.
- `T-A` contains at least one `Persona '…' declined a turn in room …: the room has spent its budget of 6 agent messages.` warning, and NO `refused a message from` warning for that same Persona.
- The `.jsonl` holds exactly six agent lines after your Message.

**Fail if — any of these**

- The exchange continues past six -> the runaway guard is gone. STOP THE APP IMMEDIATELY with Ctrl+C; two agents quoting each other have been measured at thousands of messages in seconds, and every one of them is billed.
- It halts at seven rather than six -> the two layers are comparing the count differently; the Room takes one more Message than the Human granted every time.
- A streaming row appears and THEN the Message is refused (`T-A` shows `refused a message from` with no matching `declined a turn`) -> the cheap layer did not fire: you paid for a Turn whose output was thrown away, on every pause.
- A Mentioned Agent keeps taking Turns past the cap while an un-mentioned one stops -> the Budget check and the Mention check have been reordered, so a Mention now buys a Turn past the cap.

**Inconclusive if**

If the models simply stop talking to each other before six (they may not obey the persona instruction reliably), the halt was not caused by the cap and the test proves nothing: read `T-A` — without a `declined a turn` warning, re-run with a clearer instruction, or accept the run as inconclusive rather than as a pass. If either Agent is not Online, do not start: a one-sided conversation cannot reach the cap.

> [!NOTE]
> COST: about six short billed Turns at Haiku/low — the most expensive test in this area. Keep the budget at 6 or lower and never raise it for this test. Do not walk away while it runs.

### REPLYGATEBUDGET-34 — PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up

**💰 Spends money** · about 25 min

*Catches the subtlest trap in this area: if a Budget-declined Message were also buffered as context, clicking Continue would put it in the model's prompt twice — once as context and once as the live Message.*

**Before you start**

- Two Personas `Ana` and `Ben` exist and are Online at Haiku/low (as built in REPLYGATEBUDGET-33).
- You are willing to spend roughly FOUR short billed Turns, plus the Chief of Staff's Greeting Turn (it is billed at the account default unless it is set to Haiku / low, as step 2 does).

**Steps**

1. Run `P-RESET-ROOMS`, then start the app with `P-LAUNCH-FREE` (`Team__Acp__Enabled` `false`).
2. On each card (`P-OPEN-AGENT`), set the **Chief of Staff**, `Ana` and `Ben` each to the Haiku entry for **Model**
   and `low` for **Effort** via **Edit** and **Save**. `P-RESET-ROOMS` deletes `team.db`, which is where
   the stored Model and Effort live, so all three cards come back reading `Agent default` /
   `Model default` however they were left in REPLYGATEBUDGET-33, and the Chief of Staff's Greeting
   Turn would otherwise bill at the account default. Stop the app with Ctrl+C, then start it with
   PROFILE C and `$env:Team__AgentMessageBudget = '2'` set BEFORE `dotnet run`, and wait for both
   cards to read **Online**.
3. Click **New room**, tick `Ana` and `Ben`, and click **Open room**.
4. Type `@Ana start a short conversation with @Ben about picking a meeting time.` and press Enter.
5. Wait until the Room halts and the pause block appears.
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
- You are willing to spend roughly TWO to FOUR short billed Turns, plus the Chief of Staff's Greeting Turn.

**Steps**

1. Run `P-RESET-ROOMS`, then start the app with `P-LAUNCH-FREE`. On their cards (`P-OPEN-AGENT`) set the **Chief of Staff** and the Persona to Haiku / `low` via **Edit** and **Save** (`P-RESET-ROOMS` cleared the stored Models; the Greeting Turn would otherwise bill at the account default). Stop the app with Ctrl+C, then start it with PROFILE C and `$env:Team__AgentMessageBudget = '1'` set BEFORE `dotnet run`.
2. Wait for the Persona's card to read **Online** and confirm Haiku and low on its card.
3. Click the Room named after the Persona and note the Room id.
4. Type `@<Persona> reply once, then immediately post a second short message into this same room.` and press Enter.
5. Wait up to 120 seconds and watch the Room.
6. Read `T-A` in full.
7. Read the sidebar and note whether any new Room has appeared.
8. Run `Get-Content <data>\rooms\<RoomId>.jsonl` and count the agent lines after your Message.
9. If any other Room exists, check its `.jsonl` too.

**Pass if — all of these**

- Exactly ONE agent Message lands in the Room; the pause block appears.
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

*Documents a known gap so it is not filed as a bug, and tests the guard that does catch it: a spent per-Persona token Budget must show as Degraded with a reason on the Agent's card, not only in the log.*

**Before you start**

- TWO Personas exist and are Online — `Ana` and `Ben` from REPLYGATEBUDGET-33 are exactly right.
  One is not enough: `ChatService.CreateRoomForAsync` short-circuits a single-agent request to
  `EnsureRoomForAsync`, which returns that Agent's EXISTING direct Room, so a lone Persona asked to
  create a Room can only ever be handed the one it already has and no Budget is ever minted.
- You are willing to spend a handful of short billed Turns, plus the Chief of Staff's Greeting Turn.
- You will stay at the keyboard.

> [!IMPORTANT]
> The per-Persona token Budget can only be reached by an **Agent-to-Agent** exchange, never by
> typing at the Persona. `PersonaRunner`'s read loop does `Interlocked.Exchange(ref
> this.tokensConsumed, 0)` the moment it sees a Human Message, *before* queueing the work item,
> and `ProcessWorkItemAsync` checks the Budget at the start of that item — so a Turn you prompted
> is always measured against a freshly-zeroed counter. Same rule as `STREAMINGTURN-28`,
> `STARTUPCONFIG-33` and `ROOMMESSAGING-30`.

**Steps**

1. Run `P-RESET-ROOMS`, then start the app with `P-LAUNCH-FREE`. On their cards (`P-OPEN-AGENT`) set the **Chief of Staff**, `Ana` and `Ben` to Haiku / `low` via **Edit** and **Save** (`P-RESET-ROOMS` cleared the stored Models), and stop the app with Ctrl+C.
2. Start the app with PROFILE C plus `$env:Team__AgentMessageBudget = '6'` and
   `$env:Team__Acp__TokenBudget = '2000'`, both set BEFORE `dotnet run`. The Room Budget has to be
   comfortably larger than 2: the token Budget is only ever reached on an Agent-to-Agent Turn (see
   the box above), and a Room that hits its own cap first declines in the read loop, before
   `ProcessWorkItemAsync` ever runs the token check.
3. Wait for both Persona cards (`Ana` and `Ben`) to read **Online** and confirm Haiku and low on each card.
4. Click the Room named after the Persona and note the Room id.
5. Type `@<Persona> create a new room containing yourself and <the other Persona>, then post a short message in it.` and press Enter.
6. Wait up to 120 seconds, then read the sidebar.
7. Click the newly created Room and read the area between its transcript and its composer.
8. Go to the Room the Persona created, which has both Personas in it.
9. Type `@<Persona> start a short back-and-forth with <the other Persona>.` and let them bounce.
   Watch `T-A` for the token-budget line; stop as soon as it appears, or when the Room reaches its cap.
10. Open the Persona's card (`P-OPEN-AGENT`) and read the status and the line under it.
11. Type one more short Message to the Persona and observe whether it takes a Turn.
12. Send a final Message and then check whether the card returns to **Online**.

**Pass if — all of these**

- A new Room appears in the sidebar, and it has its own untouched Budget: no pause block, no budget line, until an agent Message lands there.
- `T-A` shows a `rooms` entry being created for it (and `team.db`'s `rooms` table gains a row).
- Once the token budget is spent, `T-A` prints `Persona '…' has spent its token budget of 2000 and is taking no more turns until a human speaks to it.`
- At that moment the Persona's status reads **Degraded**, and its card shows, in red, the reason line `The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.` (`PersonaRunner.cs:266-269`, `AgentProfileCard.razor:114-116`).
- After the Human speaks to it again, the Persona takes Turns again and the status returns to **Online**.

**Fail if — any of these**

- The token budget is spent but the status still reads **Online** and only the log says anything -> the guard is silent in the UI, so an Agent that has stopped working looks identical to one that is idle.
- The status goes Degraded and never recovers after a Human Message -> the token budget is not resetting, so an Agent is permanently disabled by one busy afternoon.
- No new Room is created at step 5, and the Persona was asked for a room with a SECOND agent in it -> unrelated to this area's cap; note it, but do not judge the budget behaviour from it. (If you asked for a room containing only the Persona itself, you have hit the short-circuit described above, not a defect.)

**Inconclusive if**

If the Persona never creates the Room (models do not always use the tool when asked), the first half is untested — retry once with a more direct instruction, then mark it inconclusive. If 2000 tokens are spent before the Persona does anything useful, lower the number of steps rather than raising the token budget. If you cannot reach the token budget within roughly six Messages, stop and mark the second half inconclusive rather than continuing to spend.

> [!NOTE]
> COST: a handful of short billed Turns; stop as soon as you have seen the Degraded state. DO NOT FILE the minted Room having a full Budget of its own — that is a documented gap in the per-Room cap, and the per-Persona token Budget is the guard that exists to catch it. What IS a defect is the token budget being enforced silently.

### REPLYGATEBUDGET-37 — Continue on a paused Room whose Transcript is empty says there is nothing to re-deliver

**Free** · about 8 min

*Pins the one Continue outcome no other test reaches: a grant needs a Message to deliver again, and when the Room holds none the Human is told so instead of seeing a button that does nothing.*

**Before you start**

- No app running.

**Steps**

1. Run `P-RESET-ROOMS`.
2. Start the app with PROFILE B and `$env:Team__AgentMessageBudget = '1'`.
3. In `T-B` run `pwsh <repo>\tools\echo-bot.ps1 -Name Ada`.
4. Open `http://localhost:5100`, click the Room named `Ada`, note the Room id from the URL, type `@Ada hi` and press Enter, and wait for the reply and the pause block.
5. Without reloading the page, delete the Transcript file `<data>\rooms\<RoomId>.jsonl` in a spare terminal (`Remove-Item <data>\rooms\<RoomId>.jsonl`). The open page keeps showing the pause block.
6. Click **Continue** and wait 5 seconds.
7. Read the Room and `T-A`.

**Pass if — all of these**

- A quiet blue note above the composer reads `There is nothing to re-deliver in this room yet.` (`Chat.razor:190-194`).
- `T-A` shows no `was extended to` line for this click: nothing was granted (`ChatService.cs:346-352` returns before the Budget is touched).
- `T-B` prints no new envelope.

**Fail if — any of these**

- A `was extended to` line appears -> Continue granted Budget over an empty Transcript, so a grant can be spent on nothing.
- No note appears and nothing else changes -> the Human is left pressing a button that does nothing, with no explanation.

**Inconclusive if**

If the page was reloaded after step 5, the empty Room no longer shows the pause block and there is nothing to click: repeat from step 1. If a `was extended to` line appears and the Room re-delivers its last Message, the Transcript file was not the one deleted: check the path and the Room id.

> [!NOTE]
> Deleting a Transcript file by hand is a test device, not a supported operation. Written from `ChatService.cs:346-352` and `FileChatStore.cs:51-61` (an absent file reads as an empty Room); not yet run in a browser.

---

Back to [the manual test script](../manual-tests.md).
