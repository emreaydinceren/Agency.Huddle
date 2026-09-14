# The named pipe: external agents and the wire

Prove that the named pipe `\\.\pipe\team` — the one door every Agent enters through — behaves as documented end to end: a client connects and says hello, the server mints an Agent and a Room that appear in the browser with no refresh, every posted Message fans out to the right Members with the right per-recipient labels, and every malformed, unauthorised or over-budget request is refused in the one way the client can act on. Almost nothing on this wire has dedicated UI, so most failures here are silent in the browser and visible only as a single JSON line in the client's own console or a single line in the server log. These tests deliberately put those two consoles beside the browser and treat them as the oracle. Every test in this area is FREE: the demo agents and `tools/echo-bot.ps1` are ordinary pipe clients speaking exactly the envelopes a real Persona speaks, so no test here needs a model turn.

**36 tests** · 36 free, none paid · about 3.9 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Lane is `P-LAUNCH-FREE`. ACP is the only thing in this whole area that could spend money, and nothing here needs a real model turn.
2. `T-A` runs the app. Extra terminals are opened by individual tests as `T-C`, `T-D` and so on, each at `E:\Repos\Huddle`.
3. Arrange the browser at `http://localhost:5100` and `T-A` so both are visible at once. Confirm the sidebar shows, top to bottom: **New chat**, a list of Rooms, **Teammates**, **Settings**. If it does not, STOP and report the app as not starting; every test below is INCONCLUSIVE.
4. No test in this area configures a Persona and ACP stays off throughout, so nothing here spends money.
5. DO NOT run `P-ECHO-BOT` under the name `echo` or `alpha` anywhere except PIPEEXTERNAL-33, which exists to test exactly that. Connecting an external client under a demo agent's name silently kills that demo agent for the life of the app process, with no log line anywhere, and every later demo-agent test then fails for the wrong reason.
6. **RAW-CLIENT** (used by PIPEEXTERNAL-28 through -32): a plain PowerShell pipe client, distinct from `P-ECHO-BOT`. In a new terminal, line by line:

``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``

Send one envelope with `$w.WriteLine('<json>')`, read one line back with `$r.ReadLine()`, close with `$c.Dispose()`. Single-quote the JSON so PowerShell leaves the double quotes alone.

7. `P-RESET-ALL` is the only reset in this area. RESET BETWEEN TESTS: unless a test says otherwise, end it by pressing Ctrl+C in every bot terminal it opened; if it changed an environment variable in `T-A`, clear it with `Remove-Item Env:\<NAME>` and restart the app.

## Tests

### PIPEEXTERNAL-01 — The two demo agents are already there, and they reply to a mention

**Free** · about 4 min

*Proves the zero-cost stand-in works: the app dials its own pipe on startup, two Agents exist and two Rooms appear before any external process is started, and a mention produces a reply.*

**Before you start**

- The app is running per `P-LAUNCH-FREE`.
- `Team:DemoAgent:Enabled` is at its default `true` and `Team:DemoAgent:Names` is unset (no `Team__DemoAgent__*` environment variable is set in `T-A` — check with `Get-ChildItem Env:Team__DemoAgent__*`, which should print nothing).

**Steps**

1. Look at the sidebar Room list in the browser. Confirm it contains an entry reading exactly `echo` and an entry reading exactly `alpha`. Other Rooms left over from earlier work may also be present; that is fine.
2. Scroll `T-A`'s console back to the app's startup output and find the lines `Demo agent echo connected.` and `Demo agent alpha connected.`
3. Click the sidebar entry `echo`.
4. Confirm the page heading (`<h1>`) reads `echo` and the line directly beneath it reads `You, echo`.
5. Click into the message box at the bottom (its placeholder reads `Message… (/invite @agent)`), type `hi @echo` and press Enter.
6. Wait 3 seconds and read the last row in the transcript.
7. Click the sidebar entry `alpha`, type `hi @alpha` and press Enter, then wait 3 seconds.

**Pass if — all of these**

- Both `echo` and `alpha` appear in the sidebar Room list.
- `T-A` shows one `Demo agent echo connected.` line and one `Demo agent alpha connected.` line — exactly one each, not two.
- In the `echo` Room, a new row appears whose sender name is `echo` and whose body reads `echo: hi echo` with `echo:` rendered in BOLD (not as literal asterisks).
- A sender name and an `HH:mm` timestamp appear above both the human row and the agent row.
- In the `alpha` Room, the same happens with `alpha: hi alpha`.

**Fail if — any of these**

- No `echo`/`alpha` Rooms at startup and `T-A` shows `Demo agent echo failed to connect to pipe team after 30 attempts.` -> the pipe server is not accepting, so nothing in this area can work; report and stop.
- The sidebar shows `echo` (or `alpha`) twice and `T-A` shows two `Demo agent echo connected.` lines -> `DemoAgentOptions.Names` was given a pre-populated default and the configuration binder appended to it instead of replacing it, doubling the built-in agents.
- The Rooms appear but nothing ever replies to `hi @echo` -> the demo client connected but the mention label or the delivery fan-out is broken; check whether the survey's displacement trap applies (has anyone run `echo-bot.ps1 -Name echo` against this process? see PIPEEXTERNAL-33).
- The reply appears but renders as the literal text `**echo:** hi echo` -> the markdown pipeline is not being applied to Message bodies.
- The reply text still contains an `@` (`hi @echo`) -> the demo client stopped stripping `@` from quoted text, which is the amplification bug; expect PIPEEXTERNAL-23 to fail too.

**Inconclusive if**

If the sidebar has no `echo`/`alpha` but `T-A` shows no demo-agent lines at all, the demo agents are switched off rather than broken: run `Get-ChildItem Env:Team__DemoAgent__*` and check `src/Huddle.App/appsettings.json` for `Team:DemoAgent:Enabled`. If either is `false`, the result is INCONCLUSIVE — clear the variable, restart the app and re-run. If a reply does not arrive within 3 seconds but `T-A` is still printing startup output, wait until startup is quiet and send the mention again before judging.

> [!NOTE]
> Typing a bare `hi` with no `@echo` and getting no reply is CORRECT here and is not a fail: the demo clients and `echo-bot.ps1` reply only when mentioned, even in a two-Member Room. The relaxed two-Member rule lives in the real Persona runner, not in these clients.

### PIPEEXTERNAL-02 — A demo agent streams a Draft, then replaces it with the finished Message

**Free** · about 6 min

*Proves the Draft path works end to end over the pipe — deltas produce a live streaming row with a Stop button, and the terminator plus the real Message replace it exactly once.*

**Before you start**

- PIPEEXTERNAL-01 passed.
- The `echo` Room is open.

**Steps**

1. In the browser, press F12 to open developer tools and select the Elements/Inspector tab. Leave it open and scrolled to the message list (the `div` with class `message-list`).
2. Start a screen recording, or be ready to watch closely — the whole stream lasts about 120 ms.
3. Click into the message box, type `hi @echo` and press Enter.
4. Watch the message list for a row that appears and then disappears.
5. If you missed it, repeat steps 3-4 with `hi @echo streaming check` and watch the Elements panel for a `div` whose class is `message-row streaming` appearing and then being removed.
6. After the stream finishes, read the final transcript row.
7. Open `src\Huddle.App\App_Data\rooms\` and open the `.jsonl` file whose name matches the room id in the browser address bar (`/rooms/{id}`). Read the last two lines.

**Pass if — all of these**

- A transient row appears carrying the sender name `echo`, a **Stop** button beside the name, and text that grows in about three visible chunks, with a blinking caret at the end of the text.
- That transient row is styled `message-row streaming` in the Elements panel and its body text is PLAIN — the `**echo:**` prefix shows as literal asterisks while streaming.
- The transient row disappears and is replaced by exactly one finished Message row rendered in bold (`echo: hi echo`). There is never both a streaming row and a finished row for the same reply at once.
- The `.jsonl` file's last two lines are the human Message then the `echo` Message. No line in that file corresponds to the partial Draft text.

**Fail if — any of these**

- No streaming row ever appears, only the finished Message -> either the deltas are being refused on the wire or `DraftChanged` is not reaching the Room view; check the bot-facing behaviour with PIPEEXTERNAL-31/32 before blaming the UI.
- The streaming row never disappears and sits half-written on screen forever -> the terminator or the Draft-complete path is broken, which is the exact failure streaming exists to remove. Confirm by opening a second browser tab on the same Room: if the frozen row is there too, the server-side Draft store was never cleared and only an app restart will clear it.
- Both the streaming row and the finished Message remain, so the reply shows twice -> the `PostMessage` is not completing the Draft that carried the same message id.
- The Draft text renders as formatted Markdown mid-stream (the `**echo:**` prefix already bold before the reply finishes) -> the Draft is being pushed through the markdown renderer, which makes a half-written code fence flip to a code block and read as broken.
- A Draft line appears in the `.jsonl` transcript -> a Draft is being persisted, which it never should be.

**Inconclusive if**

If the stream is simply too fast to see and you have no recording tool, the visual half of this test is INCONCLUSIVE — do not guess. Fall back to the Elements panel: a `message-row streaming` node appearing in the DOM at all is enough to pass the first two conditions, and the transcript check still stands. If you cannot observe either, record the test as INCONCLUSIVE and say which observation you could not make.

> [!NOTE]
> `tools/echo-bot.ps1` cannot show this at all — it sends only `postMessage` and never a `messageDelta`. The absence of a streaming row when using the PowerShell bot is NOT a streaming bug. Only the demo agents and real Personas stream.

### PIPEEXTERNAL-03 — New chat builds a group Room named after its Agents, and that Room is mention-gated

**Free** · about 5 min

*Proves the directory keeps every Agent selectable and that a multi-Agent Room is named from its Agents and delivers to all of them while only the mentioned one replies.*

**Before you start**

- PIPEEXTERNAL-01 passed, so `echo` and `alpha` exist and are online.

**Steps**

1. Click **New chat** in the sidebar.
2. Confirm the panel lists `echo` and `alpha`, each as a checkbox with a small coloured dot and a name.
3. Tick the checkbox next to `echo`.
4. Tick the checkbox next to `alpha`.
5. Click **Start chat**.
6. Read the page heading and the line beneath it.
7. Read `T-A`'s newest log line.
8. Type `hi @echo` in the message box and press Enter, then wait 5 seconds.
9. Type `hi` (with no `@`) and press Enter, then wait 10 seconds.

**Pass if — all of these**

- Before any checkbox is ticked, the **Start chat** button is disabled; it becomes enabled once at least one box is ticked.
- The browser navigates to a new Room whose heading reads exactly `echo, alpha` and whose members line reads exactly `You, echo, alpha`.
- `T-A` logs `Created room '<id>' (echo, alpha) with 3 members.`
- After `hi @echo`, exactly ONE new Message appears, from `echo`, reading `echo: hi echo` in bold. `alpha` posts nothing.
- After the bare `hi`, NOTHING replies within 10 seconds.

**Fail if — any of these**

- The Room heading is something other than the comma-joined Agent names -> the Room name is not being rebuilt from its Members.
- Both `echo` and `alpha` reply to `hi @echo` -> the per-recipient `mentioned` label is wrong, or the clients are ignoring it; cross-check with PIPEEXTERNAL-22.
- Ticking a single agent and clicking Start chat creates a duplicate two-member Room instead of opening that agent's existing Room -> the single-agent path is no longer routed through the reuse check, and every New chat will mint a Room.
- The **Start chat** button is clickable with nothing ticked -> the guard on an empty selection is gone.

**Inconclusive if**

If a Room named `echo, alpha` already exists from an earlier run, a NEW second Room with the same name is created — that is expected for a multi-agent New chat and is not a fail. If neither `echo` nor `alpha` appears in the panel, this test is INCONCLUSIVE: PIPEEXTERNAL-01's precondition has not held, so fix that first.

> [!NOTE]
> The bare `hi` producing no reply is correct here twice over: a three-Member Room is mention-gated by design, AND these demo clients only ever reply when mentioned.

### PIPEEXTERNAL-04 — A Room appears in the sidebar the moment an external agent says hello, with no refresh

**Free** · about 4 min

*Proves the whole contract of this area in one observation — an Agent exists because something connected and said hello — and that the sidebar learns about it live.*

**Before you start**

- The app is running and the browser is open at http://localhost:5100.
- The sidebar has no entry reading `mybot`. If it does, use `mybot2` (or the next free digit) everywhere below and keep that spelling for the rest of the test.

**Steps**

1. Note the current contents of the sidebar Room list.
2. Open `T-C` at `E:\Repos\Huddle` and run `pwsh tools/echo-bot.ps1 -Name mybot`.
3. Do NOT touch the browser. Watch the sidebar for 5 seconds.
4. Read `T-A`'s newest log line.
5. Click the new sidebar entry `mybot`.
6. Read the page heading and the line beneath it.
7. Open `src\Huddle.App\App_Data\` and confirm `team.db` exists (its timestamp should be seconds old).

**Pass if — all of these**

- A new entry reading exactly `mybot` appears at the BOTTOM of the sidebar Room list without the page being refreshed or clicked.
- `T-C` prints `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...`, then `Sent hello. Listening for messages (Ctrl+C to exit)...`, then one long JSON line beginning `{"type":"welcome",`.
- `T-A` logs `Created direct room '<id>' for agent 'mybot'.`
- Clicking `mybot` opens a Room whose heading reads `mybot` and whose members line reads `You, mybot`.
- Exactly one `mybot` entry is in the sidebar, not two.

**Fail if — any of these**

- The Room only appears after pressing F5 -> the sidebar's live subscription to room changes is broken, or the room-created notification is not being published; this is the class of silent failure that makes every Agent look dead until a human reloads.
- No Room appears at all and `T-C`'s first line is `{"type":"error",...}` rather than `welcome` -> read the `code` field and jump to the matching test: `invalidName` (PIPEEXTERNAL-14), `nameReserved` (PIPEEXTERNAL-15), `expectedHello` (PIPEEXTERNAL-28/29).
- Two `mybot` entries appear at once -> the reconnect/upsert path is minting an Agent per connection; cross-check PIPEEXTERNAL-09.
- A Room appears under a different name than the one passed to `-Name` -> the hello name is not what the Room is created from.

**Inconclusive if**

If `T-C` throws `Exception calling "Connect" with "1" argument(s): "The operation has timed out."`, no connection was made at all and the result is INCONCLUSIVE for this behaviour — confirm the app in `T-A` is still running, confirm no `Team__PipeName` variable is set (`Get-ChildItem Env:Team__PipeName`), then re-run. See PIPEEXTERNAL-17 for the pipe-name case and PIPEEXTERNAL-36 for the two-instances case.

> [!NOTE]
> Leave `T-C`'s bot running — PIPEEXTERNAL-05, 06 and 07 continue from this state.

### PIPEEXTERNAL-05 — The welcome envelope: shape, fields and version 3

**Free** · about 5 min

*Pins the one envelope the whole handshake produces, including the strict protocol version, so a silent rename or version drift in the wire contract is caught here rather than by a client that stops connecting.*

**Before you start**

- PIPEEXTERNAL-04 passed and `mybot` is still connected in `T-C`.

**Steps**

1. In `T-C`, find the single JSON line printed directly after `Sent hello. Listening for messages (Ctrl+C to exit)...`. Select and copy it.
2. Confirm the line starts with the characters `{"type":"welcome",`.
3. Confirm the line ends with the characters `,"version":3}`.
4. Find the `agentId` value and count its characters.
5. Find the `name` value.
6. Find the `rooms` array and read its single entry: its `id`, its `name`, and its `members` array.
7. Read each entry of `members` — each is an object with `id`, `name` and `kind`.

**Pass if — all of these**

- The welcome line starts `{"type":"welcome",` and ends `,"version":3}`.
- `agentId` is exactly 32 lowercase hexadecimal characters.
- `name` is exactly `mybot` — the Name passed to `-Name`.
- `rooms` holds exactly one entry, whose `name` is `mybot` and whose `id` matches the room id in the browser address bar when the `mybot` Room is open.
- That room entry's `members` array holds exactly two objects: `{"id":"human","name":"You","kind":"human"}` and one whose `id` equals the `agentId` above, whose `name` is `mybot` and whose `kind` is `agent`.

**Fail if — any of these**

- The line ends in a `version` other than 3 -> the protocol version was bumped without updating `tools/echo-bot.ps1` in the same commit; the version check is strict equality, so every client not updated in lockstep is now cut off.
- A property is spelled differently from the list above (for example `agent_id`, or `roomName` instead of a room object's `name`) -> a C# member was renamed in the contracts; there are no explicit wire-name attributes anywhere, so a rename silently changes the protocol with no compiler or analyser signal.
- `rooms` is `[]`, or a room entry has no `members`, or `members` omits the `human` entry -> a client's reply gate reads the member count from exactly this shape, so an empty or short list makes a group Room behave like a two-Member one.
- The `type` discriminator is missing from the line -> the polymorphic discriminator was lost; every client will fail to parse, at runtime only.

**Inconclusive if**

If `T-C`'s scrollback has been lost, do not reconstruct the line from memory: Ctrl+C the bot, re-run `pwsh tools/echo-bot.ps1 -Name mybot`, and read the fresh welcome (reconnecting is safe and is itself tested by PIPEEXTERNAL-09).

> [!NOTE]
> `docs/AgencyTeam.md` shows a sample welcome ending `"version":2`. That doc sample is stale; the app is right at 3. Do not file the doc's number as the expected value.

### PIPEEXTERNAL-06 — The echo round trip: a mention produces a bold reply in the Room

**Free** · about 4 min

*Proves the full loop over the wire — human Message in, labelled envelope out to the external client, the client's post back in, rendered Message on screen and appended to the Transcript.*

**Before you start**

- `mybot` is connected in `T-C` and its Room is open in the browser.

**Steps**

1. Position the browser and `T-C` so both are visible.
2. Click into the message box (placeholder `Message… (/invite @agent)`), type `hi @mybot` and press Enter.
3. Immediately read the new line `T-C` prints.
4. Read the new rows in the browser transcript.
5. Copy the room id from the browser address bar (`/rooms/{id}`).
6. Open `src\Huddle.App\App_Data\rooms\{that id}.jsonl` in a text editor and read the last two lines.

**Pass if — all of these**

- `T-C` prints exactly one new line beginning `{"type":"messagePosted",` and containing `"mentioned":true`.
- The browser gains a human row reading `hi @mybot` and then one agent row whose sender name is `mybot` and whose body reads `mybot: hi mybot` with `mybot:` in BOLD.
- The quoted text in the reply has NO `@` in it.
- A sender name and an `HH:mm` timestamp appear above both rows.
- The `.jsonl` file's last two lines are, in order, the human Message then the agent Message; the agent line's `senderName` is `mybot`.

**Fail if — any of these**

- Nothing replies and `T-C` printed no `messagePosted` line -> the fan-out never reached the connection; the Agent is registered but not receiving.
- `T-C` printed the line but with `"mentioned":false` -> mention resolution failed for this Name; cross-check PIPEEXTERNAL-16 if the Name contains a space.
- The reply renders as the literal text `**mybot:** hi mybot` -> the markdown pipeline is not being applied to Message bodies.
- The reply keeps the `@` -> the client is no longer stripping it, and any second agent in the Room will now be re-triggered by the quote; expect PIPEEXTERNAL-23 to storm.
- The reply appears twice in the transcript -> the Message is being delivered or persisted twice.

**Inconclusive if**

If the bot console shows the envelope but the browser shows nothing new, refresh the page once. If the Message is there after the refresh, the wire is fine and the defect is in the Room view's live update — record that distinction rather than a flat fail. If the browser shows a red error strip above the message box, read it verbatim and report it; the result is INCONCLUSIVE for the wire.

> [!NOTE]
> Wire JSON HTML-escapes some characters: a Message containing an em-dash or a quote shows up escaped in the bot console. That is correct for wire JSON and is not a fail.

### PIPEEXTERNAL-07 — An Agent never receives its own Message, and nothing loops

**Free** · about 3 min

*Confirms the structural rule that stops naive clients echo-looping — the one guard between a two-agent Room and a runaway.*

**Before you start**

- PIPEEXTERNAL-06 has just been run, so `mybot` has just replied.

**Steps**

1. Look at `T-C`'s console immediately after the reply from PIPEEXTERNAL-06.
2. Count how many lines beginning `{"type":"messagePosted"` `T-C` has printed since you pressed Enter.
3. Watch the browser transcript and `T-C` for a full 30 seconds without typing anything.
4. Re-open the Room's `.jsonl` file in `src\Huddle.App\App_Data\rooms\` and count its lines.
5. Scan `T-A` for any line containing `refused a message`.

**Pass if — all of these**

- `T-C` printed exactly ONE `messagePosted` line for the exchange — the human's Message. It printed nothing for its own reply.
- Over 30 seconds of watching, no new Message appears in the browser and no new line appears in `T-C`.
- The `.jsonl` file's line count stops growing and is exactly two lines longer than before the exchange.
- `T-A` logs no `refused a message` line.

**Fail if — any of these**

- `T-C` prints its own reply back as a `messagePosted` -> the sender is no longer being skipped in the fan-out; this is the structural echo loop, and in the browser the Room will fill without end until the budget prompt stops it (or forever, if the budget is configured at zero or less).
- The Room keeps gaining Messages with nobody typing -> same defect; the give-away in `T-A` is a `Room '<id>' refused a message from '<name>': its budget of 40 agent messages since a human last spoke is spent.` warning appearing with no human input.
- The transcript file grows past two extra lines with nothing typed -> the loop reached persistence, not just the screen.

**Inconclusive if**

If you cannot tell which `messagePosted` line belongs to which Message, send a uniquely worded mention such as `hi @mybot loopcheck77` and count again — every delivered line quotes the Message text, so the ownership is then unambiguous.

> [!NOTE]
> This is a confirm-the-absence test. Thirty seconds of silence is the evidence; do not shorten it.

### PIPEEXTERNAL-08 — hello property order does not matter, so connecting is not intermittently refused

**Free** · about 4 min

*Catches a failure that presents as flakiness rather than as a bug: the shipped client serialises hello from an unordered hashtable, so the `type` discriminator is not always first on the line.*

**Before you start**

- The app is running.

**Steps**

1. Pick a Name that is not yet in the sidebar, for example `ordercheck`.
2. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name ordercheck` and note whether the first line printed after `Sent hello...` is a `welcome` or an `error`.
3. Press Ctrl+C.
4. Repeat steps 2-3 four more times with the SAME name, for five runs in total.
5. For each run, note where `"type":"hello"` would have sat — you cannot see the outgoing line, so judge only on what came back.

**Pass if — all of these**

- All five runs print a line beginning `{"type":"welcome",`.
- No run prints `{"type":"error","code":"expectedHello"`.
- Exactly one `ordercheck` entry exists in the sidebar after all five runs.

**Fail if — any of these**

- Some runs get `welcome` and others get `{"type":"error","code":"expectedHello",...}` from the unmodified shipped script -> out-of-order metadata handling has been turned off in the JSON options, and clients whose serialiser does not put `type` first will now fail at random. This reads as flakiness, not as a clean bug, which is why it is worth naming.
- Every run gets `expectedHello` -> either the shipped script's protocol version no longer matches the server (see PIPEEXTERNAL-28) or hello parsing is broken outright.

**Inconclusive if**

If any run times out on connect rather than returning a line, that run says nothing about property order — discard it and run a sixth. If five clean runs cannot be obtained because of connect failures, the result is INCONCLUSIVE; investigate the connection first (PIPEEXTERNAL-17, PIPEEXTERNAL-36).

> [!NOTE]
> Five runs is the minimum that makes an intermittent failure likely to show. If you have time, ten is better.

### PIPEEXTERNAL-09 — Reconnecting under the same name keeps the Agent id and creates no second Room

**Free** · about 5 min

*Proves the identity rule: an Agent is its Name, and a reconnect re-attaches rather than minting a new Agent with an empty history.*

**Before you start**

- `mybot` exists and its Room holds at least one Message (run PIPEEXTERNAL-06 first).
- `mybot`'s Room has NOT been turned into a three-member Room. If you have already invited anyone into it, use a fresh Name for this test instead.

**Steps**

1. In `T-C`, find the `agentId` value in the current welcome line and write it down.
2. Count the `mybot` entries in the sidebar and note the number of rows in the open `mybot` Room.
3. Press Ctrl+C in `T-C`.
4. Run `pwsh tools/echo-bot.ps1 -Name mybot` again in `T-C`.
5. Read the `agentId` in the NEW welcome line and compare it to the one you wrote down.
6. Look at the sidebar without refreshing the page.
7. Click the `mybot` Room and read the transcript.
8. Open `src\Huddle.App\App_Data\rooms\` and count how many `.jsonl` files contain this conversation.

**Pass if — all of these**

- The new welcome line's `agentId` is character-for-character identical to the one from the first connection.
- The sidebar still shows exactly ONE entry reading `mybot`.
- The `mybot` Room still shows every earlier Message, in the same order.
- Only one `.jsonl` file holds this conversation.
- `T-A` logs NO second `Created direct room '<id>' for agent 'mybot'.` line for this reconnect.

**Fail if — any of these**

- The second connection carries a different `agentId` -> the user row is being re-inserted instead of upserted. In the browser this shows as a SECOND `mybot` Room with an empty transcript while the first becomes permanently dead — a Room that looks fine and can never be answered again.
- A second `mybot` entry appears in the sidebar -> same defect, or the Room-reuse lookup no longer matches the existing two-member Room.
- No Room at all after the second connect -> registration succeeded but the Room-ensure step did not run.
- The reconnected Room's transcript is empty -> the Transcript is keyed on something that changed across the reconnect.

**Inconclusive if**

If a second `mybot` Room DOES appear and you have previously invited anyone into `mybot`'s Room during this session, that is the documented invite consequence and NOT this defect — see PIPEEXTERNAL-20. Verify by checking whether either `mybot` entry is named `mybot, <something>`. If so, this test is INCONCLUSIVE here; re-run it with a brand-new Name.

### PIPEEXTERNAL-10 — Reconnecting with different letter-case is the same Agent, and the stored Name does not change

**Free** · about 4 min

*Proves Names are matched case-insensitively and that the app keeps its original spelling, so a client can never label itself differently from what the sidebar shows.*

**Before you start**

- An Agent is already registered as `mybot` (PIPEEXTERNAL-04).
- Its `agentId` from PIPEEXTERNAL-09 is written down.

**Steps**

1. Press Ctrl+C in `T-C` to stop `mybot`.
2. Run `pwsh tools/echo-bot.ps1 -Name MYBOT` in `T-C` — note the deliberate upper case.
3. Read the `agentId` in the welcome line and compare it to the one you wrote down.
4. Read the `name` value in the same welcome line.
5. Look at the sidebar without refreshing.
6. Click **New chat** and read the agent names listed.

**Pass if — all of these**

- The welcome line's `agentId` is identical to `mybot`'s existing id.
- The welcome line reads `"name":"mybot"` — the ORIGINAL stored spelling, not the `MYBOT` you typed.
- No new sidebar entry appears; there is still exactly one entry reading `mybot`, spelled in lower case.
- The New chat panel lists `mybot` once, spelled in lower case, and does not list `MYBOT`.

**Fail if — any of these**

- A different `agentId` comes back and a second sidebar Room reading `MYBOT` appears -> the case-insensitive uniqueness on the Name column was lost, and the Team Directory now holds two Agents a reader cannot tell apart.
- The welcome echoes back `"name":"MYBOT"` while the sidebar says `mybot` -> the client will label itself with a spelling the app does not use, so every prompt and every mention it writes disagrees with the UI.
- The connection is refused outright -> case handling changed from tolerant to strict, which breaks every existing client that does not match the stored casing exactly.

**Inconclusive if**

If `mybot` was never registered (no such sidebar entry), this test has no baseline and is INCONCLUSIVE — run PIPEEXTERNAL-04 first. Leave `MYBOT` connected or press Ctrl+C; either is fine, since it is the same Agent.

> [!NOTE]
> Two Members whose Names differ only in case are a known, unguarded limit elsewhere in the product. This test checks only that the pipe does not CREATE such a pair.

### PIPEEXTERNAL-11 — Disconnecting an external agent changes nothing visible in the Room — the silent case

**Free** · about 5 min

*Confirms the documented silence: a plain pipe client going away leaves the Room looking exactly as it did, with no alert strip. The bug to catch here is the opposite — a permanent alert that nobody will read.*

**Before you start**

- `mybot` is connected in `T-C` and its Room is open in the browser.

**Steps**

1. Take a screenshot of the whole Room page, or write down: the sidebar entries, the page heading, the members line, and everything between the transcript and the message box.
2. Press Ctrl+C in `T-C`.
3. Do not touch the browser. Watch it for 10 seconds.
4. Compare the page with your screenshot or notes.
5. Press F12, open the Elements/Inspector tab, press Ctrl+F in the inspector and search the page HTML for `role="alert"`.
6. Read `T-A`'s newest log lines.
7. Click into the message box, type `hi @mybot` and press Enter.
8. Wait 15 seconds and watch both the Room and any error strip above the message box.

**Pass if — all of these**

- The sidebar still lists `mybot`.
- The page heading still reads `mybot` and the members line still reads `You, mybot`. Neither changed.
- NO banner, alert strip or badge appears anywhere on the Room page. The inspector search finds no `role="alert"` element in the rendered page.
- `T-A` logs `Agent connection <id> ended.` at Warning level (usually with an IOException beneath it).
- After Enter, your Message posts normally and appears in the transcript, and nothing ever answers. No error is shown to you.
- The Message is present in the Room's `.jsonl` file with no agent line after it.

**Fail if — any of these**

- A permanent `role="alert"` strip appears naming `mybot` as Offline -> an always-on alert is exactly the failure that strip exists to prevent; a plain pipe client has no reported failure reason, so it must produce no banner at all.
- The Room vanishes from the sidebar when the client disconnects -> presence is being confused with existence; Rooms and Agents are persistent and presence is in-memory only.
- The Message fails to post, or an error strip appears above the message box -> posting is being gated on a recipient being online, which it must not be.
- `T-A` logs nothing at all on disconnect -> the connection teardown path is not running, and the Agent may still be registered as online; cross-check with PIPEEXTERNAL-12.

**Inconclusive if**

If a banner DOES appear and the name it lists is a Persona (an entry that also appears on the **Teammates** page), this test is INCONCLUSIVE for this area — that banner belongs to Persona health, not to the pipe. Confirm `mybot` is absent from /teammates before judging.

> [!NOTE]
> A pipe-only Agent NEVER appears on the **Teammates** page — that page renders Persona files, and `mybot`, `echo` and `alpha` have none. "My bot is missing from Teammates" is not a bug.

### PIPEEXTERNAL-12 — Where a disconnect IS visible: the agent-dot in New chat and Add teammate

**Free** · about 6 min

*Proves the one surface that does show liveness reads it live, and pins the known staleness so a tester does not file it as a presence bug.*

**Before you start**

- The app is running.
- `mybot` exists in the directory.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name mybot` and wait for its welcome line.
2. In the browser, click **New chat** in the sidebar.
3. Find the row for `mybot`. Hover the small coloured dot to its left and read the tooltip; if no tooltip shows, right-click the dot, choose Inspect, and read the element's `class` and `title` attributes.
4. Note the dot's colour and its tooltip text.
5. Click **New chat** again to close the panel.
6. Press Ctrl+C in `T-C`.
7. Click **New chat** again to reopen the panel and read `mybot`'s dot and tooltip again.
8. Open any Room, click **Add teammate**, leave the Team filter on `All teams`, and read the dot beside `mybot` in that panel.
9. Now test the known staleness: restart the bot in `T-C`, open the **New chat** panel and LEAVE IT OPEN, then press Ctrl+C in `T-C` and watch the open panel for 10 seconds.

**Pass if — all of these**

- While the bot is connected, `mybot`'s dot in the New chat panel is green and its `title` / tooltip reads `online` (the element's class is `agent-dot online`).
- After Ctrl+C and REOPENING the panel, `mybot`'s dot is grey and its `title` reads `offline` (class `agent-dot offline`).
- The **Add teammate** panel shows the same dot and the same tooltip for `mybot`.
- While the bot is connected, `echo` and `alpha` also show green `online` dots.
- The dot's colour and its tooltip text agree with each other in every case.

**Fail if — any of these**

- The dot stays green after the panel is closed and reopened -> liveness is not being read live; the panel is serving a cached or persisted presence value, which will show dead agents as available forever.
- Every dot is grey while bots are demonstrably connected (their consoles are still running) -> the presence registry is not being consulted, or connections are not being registered at all.
- The panel lists no agents at all when agents have connected -> the directory read is broken; nothing can then be invited or chatted with.
- The tooltip says `online` on a grey dot, or `offline` on a green one -> colour and label are computed from different sources.

**Inconclusive if**

In step 9, a dot that does NOT repaint while the panel sits open is EXPECTED and is not a fail — these two panels do not subscribe to presence changes, and the dot is correct every time the panel is opened or the page re-renders. Only judge the dot after closing and reopening the panel. Do not use the **Teammates** page as an oracle here: pipe-only agents never appear on it at all.

### PIPEEXTERNAL-13 — An offline Agent receives nothing, and nothing is replayed on reconnect

**Free** · about 5 min

*Confirms the documented no-queue decision: the welcome carries Rooms and Members, never Transcript, so a reconnecting bot correctly stays silent about what it missed.*

**Before you start**

- `mybot` exists, and `T-C` is available.

**Steps**

1. Make sure `mybot` is NOT running — press Ctrl+C in `T-C` if it is.
2. In the browser, open the `mybot` Room.
3. Type `hi @mybot while you were away` and press Enter.
4. Confirm the Message appears in the transcript and wait 10 seconds.
5. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name mybot`.
6. Watch `T-C` for 20 seconds after its welcome line.
7. Search the welcome line for the words `while you were away`.
8. Scroll the browser transcript to the bottom.

**Pass if — all of these**

- The Message posts normally while the Agent is offline and sits in the transcript unanswered.
- On reconnect, `T-C` prints the welcome line and then NOTHING for 20 seconds.
- The welcome line does NOT contain the Message text — it carries only `agentId`, `name` and `rooms` with their `members`.
- The browser transcript still shows your Message, still unanswered, and gains nothing on reconnect.
- The Room's `.jsonl` file holds the human line with no agent line after it.

**Fail if — any of these**

- The Message disappears from the transcript when nobody is there to receive it -> delivery and persistence have been coupled; Messages must be persisted regardless of who is online.
- The welcome envelope carries Transcript content -> the handshake payload has grown beyond Rooms and Members, which is a protocol change no client was told about.
- `T-A` logs an exception when the Message is posted to a Room whose Agent is disconnected -> delivery is not skipping absent members safely.
- A queued Message is delivered on reconnect and the bot answers it -> a queue was introduced; that may be desirable, but it contradicts the recorded decision and every client's assumptions, so report it as a behaviour change rather than silently accepting it.

**Inconclusive if**

If the bot DOES reply immediately after reconnecting, check first whether you typed anything in the Room after the reconnect — a fresh human Message is delivered normally and is not a replay. Re-run the test without typing after step 5 before judging.

> [!NOTE]
> This is a confirm-the-absence test. The twenty seconds of silence is the evidence.

### PIPEEXTERNAL-14 — An invalid Name is refused with invalidName, the connection closes, and no Room appears

**Free** · about 6 min

*Proves the Name guard — which is also the path-traversal guard — rejects every shape it must, and does so with a code the client can act on.*

**Before you start**

- The app is running.
- The browser is open on any page.

**Steps**

1. Note the current sidebar Room list.
2. In `T-C` run `pwsh tools/echo-bot.ps1 -Name 'bad name!'` and read the two lines it prints before it exits.
3. Look at the browser sidebar without refreshing.
4. Run `pwsh tools/echo-bot.ps1 -Name 'my  bot'` — note the TWO spaces between `my` and `bot`.
5. Run `pwsh tools/echo-bot.ps1 -Name ' mybot'` — note the leading space.
6. Run `pwsh tools/echo-bot.ps1 -Name 'mybot '` — note the trailing space.
7. Run `pwsh tools/echo-bot.ps1 -Name '-bot'` — note the leading hyphen.
8. Run `pwsh tools/echo-bot.ps1 -Name 'bot.1'` — note the dot.
9. Refresh the browser with F5 and compare the sidebar to your note from step 1.

**Pass if — all of these**

- Step 2 prints `{"type":"error","code":"invalidName","message":"'bad name!' is not a valid agent name.","version":3}` and then `Server closed the connection.`, and the script exits.
- Each of steps 4-8 prints an `invalidName` error naming the exact Name you passed, then `Server closed the connection.`
- The sidebar gains NOTHING at any point, before or after the F5 refresh.
- `team.db` gains no row for any of these Names — confirmed by clicking **New chat** and seeing none of them listed.

**Fail if — any of these**

- Any of these Names is accepted and a Room appears -> the Name guard is weaker than documented. A Name with a leading, trailing or doubled space produces a sidebar entry a reader cannot distinguish from another Room; a Name with a dot could reach a file extension, and this guard is the path-traversal guard for Persona files.
- The refusal arrives with code `badMessage` rather than `invalidName` -> a client cannot tell a bad Name from a bad envelope and will retry forever.
- The connection is refused but stays open -> a rejected client holds a pipe instance it has no right to.
- A Name ending in a newline (not testable from this script, but worth noting if you see one in `team.db`) is accepted -> the guard is using anchors that also match before a trailing newline.

**Inconclusive if**

If PowerShell itself rejects the argument before connecting (a parameter-binding error rather than a JSON line), the test says nothing about the server — re-quote the Name exactly as written above and re-run. If the console closes too fast to read, re-run with `pwsh -NoExit tools/echo-bot.ps1 -Name 'bad name!'`.

### PIPEEXTERNAL-15 — The Human's Name is reserved on the pipe

**Free** · about 3 min

*Proves no Agent can impersonate the Human, in any casing, which would otherwise make every Message in every Room ambiguous about who sent it.*

**Before you start**

- `Team:HumanName` is at its default `You` — confirm with `Get-ChildItem Env:Team__HumanName`, which should print nothing.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name You` and read the lines it prints.
2. Look at the browser sidebar without refreshing.
3. Run `pwsh tools/echo-bot.ps1 -Name you` — note the lower case.
4. Read the lines it prints.
5. Click **New chat** in the sidebar and read the list of agents.

**Pass if — all of these**

- Step 1 prints `{"type":"error","code":"nameReserved","message":"'You' is reserved.","version":3}` and then `Server closed the connection.`
- Step 3 prints a `nameReserved` error too (the message quotes `'you'`, the spelling you passed).
- No Room appears in the sidebar for either attempt.
- The New chat panel does not list `You` or `you` as an agent.

**Fail if — any of these**

- Either attempt is accepted -> a second `You` now exists as an Agent; every Message in every Room becomes ambiguous about whether the Human or a bot wrote it, and the New chat panel offers the Human as a chat partner.
- The lower-case attempt is accepted while the exact-case one is refused -> the reservation is case-sensitive but the Name table is not, so a bot can squat a spelling the Human cannot distinguish.
- The refusal arrives as `invalidName` instead of `nameReserved` -> a client cannot tell "pick a different Name" from "fix your Name's shape".

**Inconclusive if**

If `Team__HumanName` is set to something other than `You` in this terminal, the reserved Name is that value instead and this test is INCONCLUSIVE as written — clear the variable with `Remove-Item Env:Team__HumanName`, restart the app, and re-run.

### PIPEEXTERNAL-16 — A Name with a single interior space is accepted and works end to end

**Free** · about 6 min

*Proves a display Name like `Emily Lee` is legitimate and that mention resolution uses the Room's Members rather than a pattern, so it neither truncates at the space nor matches a prefix.*

**Before you start**

- The app is running.
- No sidebar entry reads `my bot`.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name 'my bot'`.
2. Read the welcome line and check the `name` value.
3. Look at the sidebar without refreshing.
4. Click the `my bot` entry and read the heading and the members line.
5. Type `hi @my bot` in the message box and press Enter.
6. Read the line `T-C` prints and the rows the browser gains.
7. Type `hi @my` (just `@my`, nothing after it) and press Enter.
8. Read `T-C`'s new line and watch the browser for 10 seconds.

**Pass if — all of these**

- The connection is accepted and the welcome line reads `"name":"my bot"`.
- A sidebar entry reading exactly `my bot` appears with no refresh.
- The Room heading reads `my bot` and the members line reads `You, my bot`.
- After `hi @my bot`, `T-C`'s delivered line contains `"mentioned":true` and a `mentions` array holding one member whose `name` is `my bot`, and the bot replies `my bot: hi my bot` in bold.
- After `hi @my`, `T-C`'s delivered line contains `"mentioned":false` and its `mentions` array is empty, and NOTHING replies within 10 seconds.

**Fail if — any of these**

- The Name is rejected with `invalidName` -> single interior spaces were removed from the allowed Name shape, and every display-name Teammate (`Emily Lee`) is now unusable.
- `hi @my bot` arrives with `"mentioned":false` -> the mention is being truncated at the space, so a two-word Name can never be addressed; the fix is that Name resolution must go through the Room's Member list, not a pattern.
- `hi @my` arrives with `"mentioned":true` -> a prefix is matching a longer Name, so `@my` would wake `my bot` and `@Emily` would wake `Emily Lee`; a writer can no longer address one Member without waking another.
- The Room heading and the welcome `name` disagree about the spacing -> the stored and rendered forms of the Name have diverged.

**Inconclusive if**

If the composer strips or collapses the space before sending (check the human row that appears in the transcript — it must read `hi @my bot` verbatim), the mention never had a chance and the test is INCONCLUSIVE for the server; report the composer instead.

### PIPEEXTERNAL-17 — Dialling the wrong pipe name fails outside the browser, and the pipe name is configuration

**Free** · about 7 min

*Proves the pipe name is a real configuration knob and that a client on the wrong name fails loudly at the client and invisibly at the app — which is the diagnosis a tester needs when nothing appears in the sidebar.*

**Before you start**

- The app is running on the default pipe name `team`.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name mybot -Pipe wrongname`.
2. Wait up to 10 seconds and read what PowerShell prints.
3. Look at the browser sidebar and at `T-A`.
4. Press Ctrl+C in `T-A` to stop the app.
5. In `T-A` run `$env:Team__PipeName = 'huddle'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
6. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name pipecheck -Pipe team` and read what it prints.
7. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name pipecheck -Pipe huddle` and read what it prints.
8. Look at the browser sidebar (refresh with F5 if needed).
9. Press Ctrl+C in `T-C` and in `T-A`, then run `Remove-Item Env:Team__PipeName` and restart the app with `P-LAUNCH-FREE`.

**Pass if — all of these**

- Step 1 fails after about five seconds with a PowerShell error containing `Exception calling "Connect"` and `The operation has timed out.`
- Nothing appears in the browser sidebar and `T-A` logs nothing at all for that attempt.
- After the app is restarted on pipe name `huddle`, `-Pipe team` fails the same way with a timeout.
- `-Pipe huddle` succeeds, prints a `welcome` line, and a `pipecheck` Room appears in the sidebar.
- After the final restart on the default, the app is serving `team` again (verify by running `pwsh tools/echo-bot.ps1 -Name mybot` successfully).

**Fail if — any of these**

- The client connects to a pipe the app is not serving -> something else is listening on that name; check for a stray app instance with `Get-Process dotnet` before filing anything (see PIPEEXTERNAL-36).
- The client hangs forever instead of timing out after five seconds -> the client-side connect timeout is gone, and a misconfigured agent will sit silent with no diagnosis.
- Changing `Team__PipeName` has no effect and `-Pipe team` still works -> the pipe name is not being read from configuration and is effectively hardcoded.

**Inconclusive if**

If step 7 also fails to connect, the app may not have finished starting — wait for `T-A` to print its listening URL, then retry once before judging. If the demo agents log `Demo agent echo failed to connect to pipe huddle after 30 attempts.` during the renamed run, that is a separate startup-race observation; note it but judge this test on the external client only.

> [!NOTE]
> This wire is local-machine only by design. There is no port, no HTTP endpoint and no remote-agent story; do not look for one.

### PIPEEXTERNAL-18 — Members is filled on every messagePosted, and grows when the Room does

**Free** · about 7 min

*Proves the server labels every delivery with the Room's full membership, recomputed per delivery — the field a client's reply gate reads to decide whether the Room is mention-gated.*

**Before you start**

- `mybot` is connected in `T-C` and its Room is open.
- The demo agent `echo` is online.

**Steps**

1. In the `mybot` Room, type `hi @mybot` and press Enter.
2. In `T-C`, find the `"members":[` section of the delivered line and count its entries.
3. Click **Add teammate** at the top right of the Room.
4. Leave the `Team` dropdown on `All teams`.
5. Click `echo` in the list.
6. Read the info line the panel shows, then read the page heading and the members line.
7. Read `T-A`'s newest log line.
8. Type `hi @mybot` again and press Enter.
9. In `T-C`, count the `members` entries in the NEW delivered line.

**Pass if — all of these**

- The first delivered line's `members` array holds exactly two objects: `{"id":"human","name":"You","kind":"human"}` and one with `"name":"mybot","kind":"agent"`.
- The Add teammate panel's info line reads `Invited echo. Room is now "mybot, echo".`
- The page heading becomes `mybot, echo`, the members line becomes `You, mybot, echo`, and the sidebar entry renames itself — all without a refresh.
- `T-A` logs `Invited agent 'echo' (<id>) into room '<roomId>'.`
- The SECOND delivered line's `members` array holds exactly THREE objects, including the Human and both agents.
- Every `messagePosted` line carries the full list, not only the first one.

**Fail if — any of these**

- `members` is `[]` or absent -> a client's reply gate reads the member count from this field alone, so an empty list makes a three-member Room behave like a two-member one and every Agent answers everything.
- `members` lists only the Agents and omits the Human -> the count is off by one, which flips exactly the two-versus-three-member boundary the gate turns on.
- `members` is stale after the invite and still shows two entries -> membership is being cached in the gateway instead of resolved per delivery; new Members will be invisible to every client until it reconnects.
- The heading, the members line or the sidebar entry does not update until F5 -> the room-changed notification is not reaching the Room view.

**Inconclusive if**

If `echo` does not appear in the Add teammate list, check the `Team` dropdown is on `All teams` — a pipe-only agent has no Team and is filtered out by any narrower selection, and the panel then reads `No agents in this team.` That is correct filtering, not a missing agent. If the panel reads `Every agent is already in this room.`, `echo` is already a Member and this test needs a fresh two-member Room.

> [!NOTE]
> Leave this three-member Room in place: PIPEEXTERNAL-20 and PIPEEXTERNAL-21 build on it.

### PIPEEXTERNAL-19 — An external agent joins a Room live, without reconnecting

**Free** · about 8 min

*Proves membership is resolved per delivery, so an Agent that has been connected the whole time starts receiving a Room's Messages the moment it is invited — through either door into the invite.*

**Before you start**

- `mybot` is connected in `T-C` and has NOT been restarted since it connected.
- `echo` and `alpha` are online.

**Steps**

1. Click **New chat**, tick `echo` and `alpha`, and click **Start chat**. You are now in a Room named `echo, alpha` that `mybot` is not a Member of.
2. Copy the room id from the address bar (`/rooms/{id}`).
3. Click **Add teammate**, leave the Team filter on `All teams`, and click `mybot`.
4. Read the panel's info line, the page heading, the members line and the sidebar.
5. Read `T-A`'s newest log line.
6. Type `hi @mybot` in this Room and press Enter.
7. Read the line `T-C` prints — in particular its `roomId` and `roomName` — and watch the browser.
8. Now test the other door: click **New chat**, tick only `alpha`, click **Start chat** to open `alpha`'s own Room (or click the `alpha` entry in the sidebar).
9. In that Room, type `/invite @mybot` in the message box and press Enter.
10. Read the info line above the message box and the page heading.

**Pass if — all of these**

- The Add teammate info line reads `Invited mybot. Room is now "echo, alpha, mybot".`
- The heading becomes `echo, alpha, mybot`, the members line becomes `You, echo, alpha, mybot`, and the sidebar entry renames itself — all live, with no refresh and with no restart of the bot.
- `T-A` logs `Invited agent 'mybot' (<id>) into room '<roomId>'.`
- `T-C` — the same process that has been running since before this Room existed — prints a `messagePosted` line whose `roomId` is the id you copied and whose `roomName` is `echo, alpha, mybot`, and the bot's reply appears in that Room.
- The `/invite @mybot` command produces the same result: an info line reading `Invited mybot. Room is now "alpha, mybot".` and the same live rename.

**Fail if — any of these**

- The bot receives nothing until it is restarted -> membership is being resolved once at connect instead of per delivery, so every invite silently requires a client restart that nobody will know to perform.
- The bot receives the envelope but its reply comes back refused as `notMember` -> the membership check on the inbound path is caching a stale verdict.
- The Room name is not rebuilt from its Agents after the invite -> a Room's name and its membership have diverged.
- `/invite @mybot` behaves differently from the **Add teammate** button -> the two doors are no longer reaching the same code path, so one of them will drift.

**Inconclusive if**

If `/invite @mybot` returns a red error strip reading `Unknown agent @mybot`, check the spelling against the sidebar exactly — Name resolution is case-insensitive but not fuzzy. If the strip persists with the exact spelling, that IS a fail. If `mybot` is already a Member, the panel reads `Every agent is already in this room.` and the test needs a Room it is not in.

### PIPEEXTERNAL-20 — The welcome lists every Room the Agent is in, and a reconnect after an invite mints a second Room of the same name

**Free** · about 6 min

*Pins two behaviours that only show together: the handshake payload covers all of an Agent's Rooms, and a Room that has gained a third Member no longer satisfies the two-member reuse rule, so the Agent's private Room is recreated.*

**Before you start**

- PIPEEXTERNAL-18 has been run, so `mybot`'s original Room is now the three-member `mybot, echo`.
- `mybot` is connected in `T-C`.

**Steps**

1. Count the sidebar entries whose name starts with `mybot`.
2. Press Ctrl+C in `T-C`.
3. Run `pwsh tools/echo-bot.ps1 -Name mybot` again in `T-C`.
4. Read the new welcome line and count the entries in its `rooms` array.
5. For each `rooms` entry, read its `name` and the length of its `members` array.
6. Look at the sidebar without refreshing.
7. Read `T-A`'s newest log lines.
8. Click the plain `mybot` entry and read its transcript.

**Pass if — all of these**

- The new welcome line's `rooms` array holds at least TWO entries.
- One entry is named `mybot, echo` and its `members` array has three objects; another is named `mybot` and its `members` array has two.
- The sidebar now shows BOTH `mybot, echo` and a plain `mybot`, and the new one appeared with no refresh.
- `T-A` logs a second `Created direct room '<id>' for agent 'mybot'.`
- The new plain `mybot` Room's transcript is empty.

**Fail if — any of these**

- The welcome lists only one Room -> a client cannot learn about Rooms it was invited into while away, so it can never post to them.
- A room entry's `members` array is short or omits the Human -> the same count problem as PIPEEXTERNAL-18, now on the handshake path.
- A room entry's `name` disagrees with the sidebar entry for the same id -> the Room name is being computed differently on two paths.
- NO second Room is created and the Agent's private conversation has silently become the group one -> the two-member reuse rule is now matching a three-member Room, so an Agent loses its direct channel the moment anyone is invited.
- A duplicate two-member Room appears on EVERY reconnect, even when nobody was ever invited -> the exact-members lookup is broken and every reconnect mints a Room; cross-check PIPEEXTERNAL-09, which must then also fail.

**Inconclusive if**

If the sidebar shows only one `mybot`-prefixed entry, check whether PIPEEXTERNAL-18's invite actually landed (does any entry read `mybot, echo`?). If it did not, this test has no setup and is INCONCLUSIVE — run PIPEEXTERNAL-18 first.

> [!NOTE]
> Two sidebar entries beginning `mybot` is the EXPECTED outcome here, recorded as a consequence of defining a direct Room by its exact membership. Do not file it as a duplicate-room bug.

### PIPEEXTERNAL-21 — Drafts are never delivered to another Agent

**Free** · about 5 min

*Confirms that streaming text is for the human's eyes only — an Agent that could see half-written text would react to something that was never said, which is a strictly worse version of the echo loop.*

**Before you start**

- A Room contains both `mybot` (the external bot, connected in `T-C`) and the demo agent `echo` — the `mybot, echo` Room from PIPEEXTERNAL-18 is exactly this.

**Steps**

1. Open the `mybot, echo` Room in the browser.
2. Clear `T-C`'s console (`Clear-Host`) so new lines are easy to spot, then reconnect if clearing killed the process — if you reconnect, use the Room that now reads `mybot, echo`, not the new plain `mybot` Room.
3. Type `hi @echo` in the message box and press Enter.
4. Watch the browser: `echo` streams a Draft and then posts its reply.
5. Read every line `T-C` printed during and after that exchange.
6. Count the lines by `type`.

**Pass if — all of these**

- `T-C` printed exactly TWO lines: one `{"type":"messagePosted"` for your Message, and one `{"type":"messagePosted"` for `echo`'s finished reply.
- `T-C` printed NO line containing `"type":"messageDelta"`.
- `T-C` printed NO line containing `"type":"toolActivity"`.
- The browser did show the streaming row for `echo`, so deltas were definitely flowing on the server side.

**Fail if — any of these**

- `messageDelta` lines reach `mybot` -> Drafts are being fanned out to Agents. The visible symptom in the browser is agents replying to half-sentences, and in a Room with two reactive clients it amplifies faster than the echo loop does.
- `toolActivity` lines reach `mybot` -> same defect on the tool-activity path.
- `mybot` receives `echo`'s reply twice -> the finished Message is being delivered on both the Message path and the Draft path.

**Inconclusive if**

If the browser showed NO streaming row for `echo`, no deltas were produced at all and this test proves nothing — it is INCONCLUSIVE. Fix or confirm PIPEEXTERNAL-02 first, then re-run. If `T-C` was reconnected mid-test, make sure you are typing in the three-member `mybot, echo` Room and not in the fresh two-member `mybot` Room.

### PIPEEXTERNAL-22 — The mentioned flag is per recipient, and unmentioned Members still receive the envelope

**Free** · about 8 min

*Proves the server labels rather than decides: every Member gets the Message so nobody loses context, and only the addressed one is told it was addressed.*

**Before you start**

- Three terminals are free (`T-A` runs the app; `T-C` and 3 run bots).
- No sidebar entries read `bot1` or `bot2`.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name bot1`.
2. In `T-D`, run `pwsh tools/echo-bot.ps1 -Name bot2`.
3. In the browser, click the sidebar entry `bot1`.
4. Click **Add teammate**, leave the Team filter on `All teams`, and click `bot2`.
5. Confirm the heading reads `bot1, bot2` and the members line reads `You, bot1, bot2`.
6. Type `hello @bot1` and press Enter.
7. Read the new line in `T-C` and the new line in `T-D`.
8. Count the new Messages in the browser transcript and wait 15 seconds.

**Pass if — all of these**

- BOTH `T-C` and `T-D` print a `{"type":"messagePosted"` line for your Message.
- `T-C`'s line contains `"mentioned":true`.
- `T-D`'s line contains `"mentioned":false`.
- Both lines carry the SAME `mentions` array, holding exactly one member object whose `name` is `bot1`.
- Exactly ONE new agent Message appears in the browser, from `bot1`, and nothing further arrives in 15 seconds.

**Fail if — any of these**

- `bot2` receives nothing -> delivery has been narrowed to mentioned Members only. An unmentioned Agent then loses the conversation's context entirely and will answer later questions blind; this narrowing was considered and rejected.
- Both lines carry `"mentioned":true` -> the flag is being computed once for the Room rather than per recipient, so every Agent in every Room answers everything.
- The `mentions` array is empty or omits `bot1` in one of the two lines -> the two recipients are being labelled from different data.
- Two agent replies appear -> `bot2` acted on a false `mentioned` flag; check its line before blaming the client.

**Inconclusive if**

If either bot's console shows no line at all, confirm both are still running and both are Members (the members line must read `You, bot1, bot2`). If only one console is connected, this test cannot distinguish the two labels and is INCONCLUSIVE.

> [!NOTE]
> Keep both bots running — PIPEEXTERNAL-23 continues from this exact state.

### PIPEEXTERNAL-23 — Loop safety: two bots in one Room reply once each and go quiet

**Free** · about 5 min

*Proves the amplification guard — quoting clients strip the `@` — so two reactive Agents in one Room cannot answer each other forever.*

**Before you start**

- PIPEEXTERNAL-22 has been run: `bot1` and `bot2` are both connected and both Members of the `bot1, bot2` Room.

**Steps**

1. Make sure the `bot1, bot2` Room is open and note how many rows the transcript has.
2. Type `hi @bot1 @bot2` and press Enter.
3. Watch the browser transcript for 15 seconds without typing anything.
4. Read the two agent replies carefully, character by character, looking for any `@`.
5. Watch `T-A` for any line containing `refused a message`.
6. Count the transcript rows again.

**Pass if — all of these**

- Exactly two agent Messages appear: `bot1: hi bot1 bot2` and `bot2: hi bot1 bot2`, both with the sender prefix in bold.
- NEITHER reply contains an `@` anywhere.
- After the two replies, the Room is completely quiet for 15 seconds — no further Messages.
- The transcript gained exactly three rows in total (one human, two agent).
- `T-A` logs no `refused a message` warning.

**Fail if — any of these**

- The replies keep the `@` and the two bots amplify without bound -> the Room fills as fast as the pipe allows (a recorded failure produced 4299 messages in two seconds). In this build the per-Room budget stops it, so the visible symptom is a Room that races to the pause prompt entirely on its own.
- `T-A` logs `Room '<id>' refused a message from '<name>': its budget of 40 agent messages since a human last spoke is spent.` without you having typed anything after step 2 -> that warning IS the tell; a storm happened and the budget caught it.
- Only one bot replies -> one of the two `mentioned` flags is wrong; re-check PIPEEXTERNAL-22.
- Messages keep arriving after 15 seconds at any rate -> the loop is slow but real; do not wait it out, stop both bots and report.

**Inconclusive if**

If the Room reaches the pause prompt during this test, stop both bots immediately, note the transcript size, and record a FAIL with the storm evidence rather than an inconclusive. If neither bot replies at all, the setup did not hold (both must be Members and both mentioned) and the result is INCONCLUSIVE.

> [!NOTE]
> Press Ctrl+C in `T-C` and 3 when done unless the next test says otherwise.

### PIPEEXTERNAL-24 — A second connection under the same name displaces the first, which is told by having its pipe closed

**Free** · about 6 min

*Proves the one-connection-per-Agent rule and, critically, that the displaced client is TOLD rather than left hanging — the mechanism behind the displacement trap in PIPEEXTERNAL-33.*

**Before you start**

- `T-C` and 3 are free.
- A Name that is not a demo agent name — use `dup1`.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name dup1` and note its `agentId` from the welcome line.
2. In the browser, confirm a `dup1` Room appeared, open it, type `hi @dup1` and press Enter, and confirm one reply arrives.
3. Leave `T-C` running. Do NOT press Ctrl+C.
4. In `T-D`, run `pwsh tools/echo-bot.ps1 -Name dup1`.
5. Read `T-D`'s welcome line and compare its `agentId` to the one from step 1.
6. Read what `T-C` prints and whether its script exits.
7. Look at the browser: sidebar, heading, members line, transcript.
8. Type `hi @dup1` and press Enter.
9. Read which terminal prints the delivered line, and count the new Messages in the browser.
10. Open the Room's `.jsonl` file and count the lines added by step 8.

**Pass if — all of these**

- `T-D` receives a normal `welcome` carrying the SAME `agentId` as `T-C`'s.
- `T-C` prints `Server closed the connection.` and its script exits.
- The browser shows no change at all — same sidebar, same heading, same members line, same transcript.
- After the second mention, only `T-D` prints a delivered line, and exactly ONE new agent Message appears in the browser.
- The `.jsonl` file gains exactly two lines for step 8 (one human, one agent), never three.

**Fail if — any of these**

- Both connections stay live and the mention produces TWO identical replies in the transcript -> the displaced connection is not being closed; every reconnect then leaves a zombie that doubles every reply.
- The new connection is refused instead of the old one being closed -> a client that crashed without a clean disconnect can never get back in until the app restarts.
- `T-C` hangs silently rather than printing `Server closed the connection.` -> the displaced client is not told, so it will sit forever believing it is connected. This is the silent half of the displacement trap.
- The browser changes (the Room disappears and reappears, or the transcript reloads empty) -> displacement is being treated as a Room-level event, which it is not.

**Inconclusive if**

If `T-C` exits without printing anything, scroll its console back one line — the exit message can be the last thing before the prompt. If you genuinely cannot see it, note that specifically; a silent exit and a reported one are different findings.

### PIPEEXTERNAL-25 — Three clients connect at once and the accept loop does not jam

**Free** · about 7 min

*Catches the specific regression where the next pipe server instance is not created before I/O starts on the accepted one — the failure mode is a client that cannot connect at all while another is connecting.*

**Before you start**

- Three free terminals (`T-C`, 3 and 4).
- No sidebar entries read `b1`, `b2` or `b3`.

**Steps**

1. Open `T-C`, 3 and 4 at `E:\Repos\Huddle` and type — but do not run — `pwsh tools/echo-bot.ps1 -Name b1`, `-Name b2` and `-Name b3` respectively.
2. Press Enter in all three terminals as close to simultaneously as you can manage.
3. Read the first line each terminal prints after `Sent hello...`.
4. Look at the browser sidebar without refreshing.
5. Compare the three `agentId` values.
6. Read `T-A`'s log.
7. Press Ctrl+C in all three, then repeat steps 1-6 twice more with names `c1`/`c2`/`c3` and `d1`/`d2`/`d3`.

**Pass if — all of these**

- All three terminals print a `{"type":"welcome"` line in every one of the three rounds.
- Three new Room entries appear in the sidebar with no refresh, in each round.
- The three `agentId` values in a round are all different from one another.
- `T-A` logs three `Created direct room '<id>' for agent '<name>'.` lines per round.

**Fail if — any of these**

- Any client fails with a PowerShell exception mentioning `All pipe instances are busy` -> the next server instance is not being created before I/O begins on the accepted one; under any real concurrency, clients will randomly fail to connect.
- Two clients receive the same `agentId` -> distinct Names are collapsing onto one Agent.
- Only two of the three Rooms appear -> one connection was accepted but never completed its handshake, which will look to its operator like a hung bot.
- One round passes and another fails -> the same race, just intermittent; a single failing round is a FAIL, not a flake.

**Inconclusive if**

Pressing Enter by hand is not truly simultaneous. If all three rounds pass, that is a pass. If a round fails with a connect TIMEOUT rather than "all pipe instances are busy", check the app is still running before judging — a crashed app produces timeouts and makes the round INCONCLUSIVE.

### PIPEEXTERNAL-26 — The Budget labels ride on every messagePosted, and a refused post comes back as budgetExhausted

**Free** · about 12 min

*Proves the Room's spend guard is both advertised on the wire (so a client can apply it) and enforced (so a client that ignores it is refused with a terminal, non-retryable code), and that the human is asked rather than merely told.*

**Before you start**

- `T-C` and 3 are free.
- You are willing to restart the app with a changed environment variable and restart it again afterwards.

**Steps**

1. Press Ctrl+C in `T-A` to stop the app.
2. In `T-A` run `$env:Team__AgentMessageBudget = '1'`.
3. In `T-A` run `$env:Team__Acp__Enabled = 'false'` again (a fresh shell state can lose it), then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name bud1`. In `T-D`, run `pwsh tools/echo-bot.ps1 -Name bud2`.
5. In the browser, open the `bud1` Room, click **Add teammate**, leave the filter on `All teams`, and click `bud2`. Confirm the members line reads `You, bud1, bud2`.
6. Type `hi @bud1 @bud2` and press Enter.
7. Read the delivered line in BOTH `T-C` and `T-D` — specifically the two values at the very end of the line.
8. Read whichever terminal printed an `error` line and copy it in full.
9. Read `T-A`'s newest warning line.
10. Look at the browser between the transcript and the message box.
11. Open the Room's `.jsonl` file and check whether the refused reply text is in it.

**Pass if — all of these**

- The line delivered to both bots for YOUR Message ends `...,"agentMessagesSinceHuman":0,"budget":1}` — used 0, granted 1.
- Exactly ONE of the two bots' replies lands and appears in the browser.
- The OTHER bot's console prints `{"type":"error","code":"budgetExhausted","message":"This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.",...}`.
- The line delivered to the second bot for the FIRST bot's reply ends `...,"agentMessagesSinceHuman":1,"budget":1}` — used 1, granted 1.
- `T-A` logs a Warning: `Room '<id>' refused a message from '<name>': its budget of 1 agent messages since a human last spoke is spent.`
- The browser shows, BETWEEN the transcript and the message box, a block reading `Agents have sent 1 replies since you last spoke, and are paused.` with two buttons: **Continue** and **Leave paused**.
- The refused reply text is ABSENT from the `.jsonl` transcript.

**Fail if — any of these**

- `agentMessagesSinceHuman` or `budget` is missing from the delivered line -> a client cannot apply the budget rule itself and will keep posting until the server refuses it, spending a turn each time.
- The second post is accepted anyway -> the guard is not enforced, only advertised; there is then nothing between a loop and the user's bill.
- The refusal arrives as a generic `badMessage` -> to a client that does not know the specific code, a refused post reads as a Message that simply vanished, with no reason and no instruction not to retry.
- No prompt appears at all, or it appears ABOVE the transcript -> the human is not being asked before more spend, or is being asked where the exchange that caused it cannot be read.
- The refused text appears in the transcript -> the refusal happened after persistence, so the count no longer describes the Transcript.

**Inconclusive if**

Which of the two bots wins the race is not deterministic — either one landing is a pass. If BOTH replies land, re-run once before judging: a genuine failure reproduces. If the pause block does not appear but the error line did, refresh the page once; if it appears after the refresh, the wire is right and the defect is in the live update — record that distinction. Do NOT clear `Team__AgentMessageBudget` yet; PIPEEXTERNAL-27 needs this exact state.

> [!NOTE]
> This test spends no money: the budget exists to cap real spend, but with ACP off the only thing being counted is free echo traffic.

### PIPEEXTERNAL-27 — Continue re-delivers the same Message to the Agents without showing it twice

**Free** · about 8 min

*Proves the grant actually wakes the Room — raising a number nobody reads would be a no-op — while the re-delivery stays invisible in the transcript.*

**Before you start**

- PIPEEXTERNAL-26 has just been run and the Room is showing the pause prompt.
- Both `bud1` and `bud2` are still connected in `T-C` and 3.

**Steps**

1. Write down the exact number of rows currently in the transcript, and the text of the last row.
2. In whichever terminal belongs to the bot that was REFUSED, note the last delivered `messagePosted` line's `message` object and its `id`.
3. Click **Continue** in the pause block.
4. Watch the button text while the click is in flight.
5. Read the new line the refused bot's terminal prints.
6. Compare that line's `message.id` and `message.text` to the last agent reply already in the transcript.
7. Read the two values at the end of that line.
8. Count the transcript rows again and compare to step 1.
9. Read `T-A`'s newest log line.
10. Read what replaced the pause block between the transcript and the message box.
11. Try to click **Continue** twice in quick succession on a fresh pause (type `hi @bud1 @bud2` again to re-pause first).

**Pass if — all of these**

- The **Continue** button reads `Continuing…` while the grant is in flight and is disabled during that time.
- The previously refused bot's terminal prints a `messagePosted` line again for a Message it has already seen — same `message.id`, same `message.text`.
- That line now ends `...,"budget":2}` — the granted figure went up.
- The browser transcript row count is UNCHANGED: no Message is rendered a second time.
- `T-A` logs `Room '<id>' was extended to 2 agent messages.`
- The pause block is replaced by the plain note `1 of 2 agent replies since you last spoke.`
- Double-clicking **Continue** on a fresh pause produces only ONE `was extended to` line in `T-A`.

**Fail if — any of these**

- A Message is rendered a second time in the transcript -> a component is subscribed to the re-delivery event that only the wire is allowed to see; every Continue will then visibly duplicate history.
- Nothing reaches any bot after Continue -> the grant changed a number nobody reads and the Room stays silent; the human clicks, nothing happens, and there is no error.
- `T-A` logs two `was extended to` lines from one double-click -> the button can be clicked twice while the first grant is in flight, so one click can grant twice the spend.
- The pause block stays on screen with the same wording after a successful grant -> the Room view is not re-reading the budget after the grant.

**Inconclusive if**

With `tools/echo-bot.ps1`, NO new reply follows the Continue, and that is correct: the re-delivered Message is the first bot's reply, which mentions nobody (the `@` was stripped), so a mention-gated client rightly stays silent. The evidence for this test is the re-delivered line plus the unchanged transcript — do not record a fail for the missing reply. If the previously refused bot's terminal prints nothing at all after Continue, THAT is a fail. Afterwards, reset: Ctrl+C in `T-A`, 2 and 3; run `Remove-Item Env:Team__AgentMessageBudget`; restart the app with `P-LAUNCH-FREE`.

### PIPEEXTERNAL-28 — A wrong protocol version in hello is refused, and the browser never learns

**Free** · about 6 min

*Proves the version check is strict equality and that a version mismatch is completely invisible in the UI — the reason bumping the version obliges you to update every client in the same commit.*

**Before you start**

- The app is running.
- The browser is open.

**Steps**

1. Note the current sidebar Room list.
2. Open a new terminal and run the `RAW-CLIENT` lines from setup:
``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``
3. Send a version-2 hello: `$w.WriteLine('{"type":"hello","version":2,"name":"vtest"}')`
4. Read one line back: `$r.ReadLine()`
5. Read another line: `$r.ReadLine()`
6. Look at the browser sidebar without refreshing, then refresh with F5 and look again.
7. Look at `T-A`'s log.
8. Close the raw client: `$c.Dispose()`
9. In a terminal, run `Select-String -Path src/Huddle.Contracts/ProtocolVersion.cs -Pattern 'Current'` and `Select-String -Path tools/echo-bot.ps1 -Pattern 'version'`.

**Pass if — all of these**

- Step 4 returns `{"type":"error","code":"expectedHello","message":"Unsupported protocol version '2'.","version":3}`.
- Step 5 returns nothing (an empty result / `$null`) because the server closed the connection.
- The browser sidebar gains NOTHING, before or after the refresh — no Room, no error, no visible trace.
- `ProtocolVersion.cs` shows `Current = 3` and `tools/echo-bot.ps1` sends `version = 3` — the two agree.

**Fail if — any of these**

- The version-2 hello is ACCEPTED and a `vtest` Room appears -> strict equality is gone, and the contract that bumping the version means updating every client in lockstep is silently broken; mixed-version clients will then produce shape errors far from here.
- `ProtocolVersion.cs` and `tools/echo-bot.ps1` disagree on the number -> the version was bumped without updating the shipped client in the same commit. Confirm by running the unmodified `pwsh tools/echo-bot.ps1 -Name vcheck`: if it now gets `expectedHello`, that is the fail.
- Anything about this appears in the browser -> not a fail in itself, but note it: the UI is documented as learning nothing about a refused handshake.

**Inconclusive if**

If step 4 blocks and never returns, the server did not answer at all — press Ctrl+C in that terminal and re-run the whole raw-client block; if it blocks again, the result is INCONCLUSIVE for the version check and the finding is that a bad hello leaves the connection hanging (which is itself a defect, reported under PIPEEXTERNAL-29's timeout condition).

> [!NOTE]
> Use the raw client rather than editing `tools/echo-bot.ps1` — that file is shared with the other effort's documentation and must not be modified.

### PIPEEXTERNAL-29 — A first message that is not hello, and a silent client, are both refused as expectedHello

**Free** · about 8 min

*Proves an unauthenticated client cannot post, and that a client which says nothing is timed out rather than holding a pipe instance forever.*

**Before you start**

- The app is running.

**Steps**

1. Open a new terminal and run the raw-client connect block:
``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``
2. Send a post instead of a hello: `$w.WriteLine('{"type":"postMessage","version":3,"roomId":"x","text":"hi"}')`
3. Read one line: `$r.ReadLine()`
4. Read another line: `$r.ReadLine()`
5. Close it: `$c.Dispose()`
6. Now the silent case. Run the connect block again, but send NOTHING.
7. Immediately run `$r.ReadLine()` and note how long it takes to return and what it returns.
8. Read one more line: `$r.ReadLine()`
9. Close it: `$c.Dispose()`
10. Look at the browser sidebar (refresh with F5) and at `T-A`.

**Pass if — all of these**

- Step 3 returns `{"type":"error","code":"expectedHello","message":"Expected 'hello' as the first message.","version":3}`.
- Step 4 returns nothing — the connection was closed.
- Step 7 returns `{"type":"error","code":"expectedHello","message":"Timed out waiting for 'hello'.","version":3}` after roughly five seconds.
- Step 8 returns nothing — the connection was closed.
- The browser gains nothing from either attempt, before or after the refresh.
- No Message was posted anywhere — no Room named `x` exists and no transcript grew.

**Fail if — any of these**

- The `postMessage` is processed -> an unauthenticated client can post into Rooms, which is the whole point of the handshake.
- Step 7 never returns, or returns only when you kill the app -> the hello timeout is gone, so a stuck or hostile client holds a pipe instance indefinitely and eventually exhausts them.
- Either case leaves the connection OPEN afterwards (step 4 or step 8 returns another line rather than nothing) -> a client that failed the handshake is being allowed to continue.
- The refusal arrives with a code other than `expectedHello` -> a client cannot tell "say hello first" from any other failure.

**Inconclusive if**

If `$r.ReadLine()` in step 7 returns instantly with nothing, the connection was closed without an error being sent. That is not the documented behaviour — record it as a distinct finding rather than a pass, and note whether `T-A` logged anything.

### PIPEEXTERNAL-30 — After the handshake, a bad line or a bad request is reported and the connection STAYS OPEN

**Free** · about 12 min

*Proves one malformed envelope cannot kill a long-running Agent, and that each refusal carries the code and the related message id a client needs to know which of its in-flight posts failed.*

**Before you start**

- The app is running.
- The browser is open.

**Steps**

1. Open a new terminal and run the raw-client connect block:
``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``
2. Complete the handshake: `$w.WriteLine('{"type":"hello","version":3,"name":"raw1","description":"raw test client"}')`
3. Read the welcome and capture the room id: `$wel = $r.ReadLine() | ConvertFrom-Json; $room = $wel.rooms[0].id; $room`
4. Confirm a `raw1` Room appeared in the browser sidebar and open it.
5. Case (a), bad JSON: `$w.WriteLine('not json at all')` then `$r.ReadLine()`
6. Case (b), a second hello: `$w.WriteLine('{"type":"hello","version":3,"name":"raw1"}')` then `$r.ReadLine()`
7. Case (c), unknown room: `$w.WriteLine('{"type":"postMessage","version":3,"roomId":"nosuchroom","messageId":"m9","text":"hi"}')` then `$r.ReadLine()`
8. Case (d), empty text: `$w.WriteLine('{"type":"postMessage","version":3,"roomId":"' + $room + '","messageId":"m10","text":""}')` then `$r.ReadLine()`
9. Case (e), bad message id: `$w.WriteLine('{"type":"postMessage","version":3,"roomId":"' + $room + '","messageId":"m.1","text":"hi"}')` then `$r.ReadLine()`
10. Now prove the connection survived all five: `$w.WriteLine('{"type":"postMessage","version":3,"roomId":"' + $room + '","messageId":"m11","text":"still alive"}')`
11. Look at the browser `raw1` Room.
12. Open `src\Huddle.App\App_Data\rooms\<that room id>.jsonl` and read every line.
13. Close the client: `$c.Dispose()`

**Pass if — all of these**

- (a) returns a line with `"code":"badMessage"` whose `message` is a JSON parsing complaint (the exact wording comes from the JSON library — do not pin it).
- (b) returns `{"type":"error","code":"badMessage","message":"already registered",...}`.
- (c) returns `"code":"unknownRoom"` with `"message":"Unknown room 'nosuchroom'."` AND `"relatedMessageId":"m9"`.
- (d) returns `"code":"badMessage"` with `"message":"empty message"` and `"relatedMessageId":"m10"`.
- (e) returns `"code":"badMessage"` with `"message":"Invalid message id."` and `"relatedMessageId":"m.1"`.
- After all five refusals, step 10's valid post SUCCEEDS: `still alive` appears in the `raw1` Room in the browser with no refresh.
- The `.jsonl` file holds exactly ONE line — the `still alive` Message. None of the five refused requests reached it.

**Fail if — any of these**

- The connection drops after any single bad line (a later `$r.ReadLine()` returns nothing) -> one malformed envelope kills a long-running agent, which for a real Persona means a lost session and a lost conversation.
- Any error omits `relatedMessageId` -> the client cannot tell which of its in-flight posts failed, so it must either retry everything or drop everything.
- A refused Message reaches the transcript -> validation is happening after persistence.
- A refusal arrives with a generic code where a specific one is documented -> a client that does not know the specific code degrades to a generic failure, and a refused post then reads as a Message that simply vanished.

**Inconclusive if**

If `$wel.rooms[0].id` is empty, the handshake did not complete and every case below is INCONCLUSIVE — re-run from step 1. If a `$r.ReadLine()` blocks, the previous request may have been accepted rather than refused: check the browser transcript before assuming a hang, then press Ctrl+C and restart the raw client.

> [!NOTE]
> PowerShell string concatenation is used to splice the real room id into the JSON; keep the single quotes exactly as written so the double quotes survive.

### PIPEEXTERNAL-31 — An Agent cannot write a Draft into a Room it is not a Member of, even by reusing a message id

**Free** · about 10 min

*Proves the membership check on the streaming path is keyed on the Room as well as the message id, so a cached verdict from one Room can never be handed to another.*

**Before you start**

- The app is running with the demo agents online, so an `echo` Room exists that a new client is not a Member of.

**Steps**

1. In the browser, click the sidebar entry `echo` and copy the room id from the address bar (`/rooms/{id}`). Call this ECHOROOM.
2. Keep the `echo` Room open in the browser and visible.
3. Open a new terminal and run the raw-client connect block:
``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``
4. Handshake as a client that is NOT in the echo Room: `$w.WriteLine('{"type":"hello","version":3,"name":"raw2"}')` then `$wel = $r.ReadLine() | ConvertFrom-Json; $own = $wel.rooms[0].id; $own`
5. Set the target: `$echo = '<paste ECHOROOM here>'`
6. Attempt an intrusion: `$w.WriteLine('{"type":"messageDelta","version":3,"roomId":"' + $echo + '","messageId":"m1","text":"intruder","isFinal":false}')` then `$r.ReadLine()`
7. Watch the browser's `echo` Room for 10 seconds.
8. Now write a LEGITIMATE Draft into raw2's own Room under the SAME message id: `$w.WriteLine('{"type":"messageDelta","version":3,"roomId":"' + $own + '","messageId":"m1","text":"mine","isFinal":false}')`
9. Open the `raw2` Room in the browser and confirm a streaming row appears.
10. Immediately retry the intrusion with the same id: `$w.WriteLine('{"type":"messageDelta","version":3,"roomId":"' + $echo + '","messageId":"m1","text":"intruder again","isFinal":false}')` then `$r.ReadLine()`
11. Switch back to the `echo` Room in the browser and look again.
12. Close the client: `$c.Dispose()`

**Pass if — all of these**

- Step 6 returns a line with `"code":"notMember"` and `"relatedMessageId":"m1"`.
- The `echo` Room in the browser shows NOTHING new — no streaming row, no text, no `intruder`.
- Step 8 is accepted (no error line comes back) and a streaming row reading `mine` appears in the `raw2` Room.
- Step 10 is STILL refused with `"code":"notMember"` — reusing the message id that was just accepted elsewhere does not buy access.
- The `echo` Room never shows `intruder` or `intruder again` at any point.

**Fail if — any of these**

- Text from a non-member appears as a streaming row in someone else's Room -> any client that can dial the pipe can put words on screen in any Room, attributed to itself.
- Step 10 SUCCEEDS after step 8 succeeded -> the membership verdict is being cached on the message id alone, so a client can launder access into any Room by first writing legitimately into its own.
- Step 6 returns `unknownRoom` rather than `notMember` for a Room that demonstrably exists -> the room lookup is failing before the membership check and the client gets the wrong diagnosis.
- Step 8 is refused -> a Member cannot write a Draft into its own Room, which breaks all streaming.

**Inconclusive if**

If step 8 produces no streaming row in the browser, Drafts are not rendering at all and the second half of this test cannot be judged — check PIPEEXTERNAL-02 first; the refusal half (steps 6 and 10) still stands on its own. If `$wel.rooms[0].id` is empty, the handshake failed and the whole test is INCONCLUSIVE.

### PIPEEXTERNAL-32 — An Agent disconnecting mid-Draft clears its Draft rather than freezing it on screen

**Free** · about 7 min

*Proves the cleanup on disconnect — a client that dies without sending a terminator must not leave a half-written row on screen forever, which is the exact failure streaming exists to remove.*

**Before you start**

- The app is running.

**Steps**

1. Open a new terminal and run the raw-client connect block:
``powershell
$c = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'team', 'InOut')
$c.Connect(5000)
$w = [System.IO.StreamWriter]::new($c); $w.AutoFlush = $true
$r = [System.IO.StreamReader]::new($c)
``
2. Handshake: `$w.WriteLine('{"type":"hello","version":3,"name":"rawdraft"}')` then `$wel = $r.ReadLine() | ConvertFrom-Json; $own = $wel.rooms[0].id; $own`
3. In the browser, click the new `rawdraft` sidebar entry and keep the Room visible.
4. Start a Draft and do NOT terminate it: `$w.WriteLine('{"type":"messageDelta","version":3,"roomId":"' + $own + '","messageId":"d1","text":"half a sentence","isFinal":false}')`
5. Look at the browser Room and describe what appears.
6. Open a SECOND browser tab on the same Room URL and confirm the same row is there.
7. Now kill the client without a terminator: `$c.Dispose()`
8. Watch BOTH browser tabs for 5 seconds.
9. Open a THIRD browser tab on the same Room URL.
10. Read `T-A`'s newest log line.

**Pass if — all of these**

- After step 4, a streaming row appears in the Room: sender name `rawdraft`, a **Stop** button, the plain text `half a sentence`, and a blinking caret.
- The same row is present in the second tab, confirming the Draft is server-side and not per-browser.
- Within about a second of `$c.Dispose()`, the row DISAPPEARS from both tabs with no refresh, leaving the Room with no rows at all.
- A freshly opened third tab also shows no streaming row.
- `T-A` logs the connection ending (`Agent connection <id> ended.`).

**Fail if — any of these**

- The half-written row stays on screen forever -> this is the exact failure streaming exists to remove; a dead Agent leaves the Room permanently mid-sentence.
- The row disappears from the tab you were watching but a freshly opened tab still shows it -> only the client state was cleared, not the server-side Draft store, so every new viewer sees the ghost and only an app restart will clear it.
- The row is present in the first tab but absent from the second even BEFORE the disconnect -> the Draft is per-circuit rather than server-side, so a tab opened mid-stream shows an orphaned suffix or nothing.
- The Draft text is written into the Room's `.jsonl` file -> a Draft is being persisted, which it never should be.

**Inconclusive if**

If step 4 produces no streaming row at all, either the delta was refused (check with an `$r.ReadLine()` — an error line means a membership or id problem) or Drafts do not render; either way this test is INCONCLUSIVE and PIPEEXTERNAL-02 and PIPEEXTERNAL-31 should be resolved first.

> [!NOTE]
> `$c.Dispose()` is the closest safe equivalent to a client crashing without sending its terminator. Closing the whole terminal window works too.

### PIPEEXTERNAL-33 — THE DISPLACEMENT TRAP: an external bot named echo kills that demo agent for the life of the process

**Free** · about 8 min

*Confirms the area's most dangerous silent failure — there is no log line for it at all, and a tester who once ran the bot under a demo agent's name will wrongly conclude the demo agents are broken for the rest of the session.*

**Before you start**

- The app has been freshly restarted and PIPEEXTERNAL-01 passes (both demo agents reply).
- You accept that this test poisons the app process and REQUIRES a restart afterwards. Run it last among the free tests.

**Steps**

1. Confirm the baseline: open the `echo` Room, type `hi @echo` and press Enter, and confirm `echo: hi echo` comes back.
2. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name echo`.
3. Read its welcome line and note the `agentId`.
4. Compare that `agentId` to the `echo` member id in any earlier `messagePosted` line from another bot, or simply note that the same `echo` Room is reused rather than a new one appearing.
5. Press Ctrl+C in `T-C` to kill the external bot.
6. In the browser, open the `echo` Room, type `hi @echo` and press Enter.
7. Wait 30 seconds and watch the Room, the area between the transcript and the message box, and `T-A`.
8. Click **New chat** and read `echo`'s dot and tooltip.
9. Try once more: type `hi @echo` and press Enter, wait 15 seconds.
10. Scan `T-A`'s entire log since step 2 for ANY line mentioning `echo` or `Demo agent`.
11. Press Ctrl+C in `T-A` and restart the app with `P-LAUNCH-FREE`.
12. Once restarted, open the `echo` Room and type `hi @echo` again.

**Pass if — all of these**

- Step 3's welcome carries the SAME `agentId` the demo agent had — the external bot took over the identity rather than creating a second one.
- No new `echo` Room appears; the existing one is reused.
- After the external bot is killed, `hi @echo` produces NO reply, ever, for the rest of the process's life.
- The browser shows no error, no banner and no badge for this — the Room looks entirely normal.
- The New chat panel shows `echo`'s dot grey and titled `offline`.
- `T-A` logs NOTHING about the demo agent's death — no `Demo agent echo stopped unexpectedly.` line, no warning, nothing. THE ABSENCE OF A LOG LINE IS THE FINDING.
- After the app restart, `T-A` logs `Demo agent echo connected.` again and `hi @echo` is answered normally.

**Fail if — any of these**

- The demo agent reconnects by itself and answers again without an app restart -> behaviour has changed (arguably for the better); report it as a change, since the documented behaviour and the test-fixture rule that avoids these names both assume it does not.
- `T-A` DOES log the demo agent's death -> also a change, and a welcome one; report it so the trap documentation can be updated.
- The external bot under the name `echo` is REFUSED rather than displacing the demo agent -> displacement semantics changed; cross-check PIPEEXTERNAL-24, which must then also fail.
- After the app restart, `echo` still does not answer -> something persisted that should not have; presence is in-memory only and a restart must fully recover.

**Inconclusive if**

If step 6 produces a reply, check whether `T-C` is genuinely dead (its prompt has returned) — an external bot still running under the name `echo` will answer, and that is not the trap. Re-run from step 5 before judging. Do NOT skip step 11: leaving the app in this state makes every later demo-agent test fail for the wrong reason.

> [!NOTE]
> This is a confirm-the-silence test. The evidence is the empty log, not a visible symptom. The same collision is why the automated test fixtures switch the demo agents off: their names collide with the names the pipe tests register.

### PIPEEXTERNAL-34 — Stopping the app closes every Agent connection cleanly, and the browser's reconnect dialog is readable

**Free** · about 8 min

*Proves shutdown tells every client rather than leaving it hanging, and checks the reconnect dialog for a documented styling failure that is invisible except over a real HTTP round trip.*

**Before you start**

- The app is running and at least two external bots are connected (use `T-C` and 3 with names `shut1` and `shut2`).
- The browser is open on a Room.

**Steps**

1. In `T-C` run `pwsh tools/echo-bot.ps1 -Name shut1`; in `T-D` run `pwsh tools/echo-bot.ps1 -Name shut2`. Confirm both got a welcome and both Rooms appeared.
2. Note the full sidebar Room list and open one Room with Messages in it; note the transcript.
3. Press Ctrl+C in `T-A`.
4. Immediately read `T-C` and 3.
5. Watch the browser for 60 seconds and read the dialog that appears, in order.
6. Take a screenshot of the dialog at each stage.
7. Count how many paragraphs of text the dialog shows at once.
8. Restart the app with `P-LAUNCH-FREE`.
9. Click **Retry** in the browser dialog, or refresh the page.
10. Compare the sidebar and the transcript with your note from step 2.
11. Open the Room that had a budget note or pause prompt before the restart, if any, and look for it.

**Pass if — all of these**

- Both `T-C` and `T-D` print `Server closed the connection.` and their scripts exit — neither hangs.
- The browser shows a dialog reading `Rejoining the server...`, then `Rejoin failed... trying again in N seconds.`, then eventually `Failed to rejoin.` followed by `Please retry or reload the page.` with a **Retry** button.
- At each stage the dialog shows exactly ONE state paragraph, not several stacked on top of each other.
- After the restart and a Retry or refresh, every Room is still in the sidebar and every transcript is intact.
- Any budget note or pause prompt that was showing before the restart is GONE — the counters reset.

**Fail if — any of these**

- The bots hang instead of being told, with no `Server closed the connection.` line -> shutdown is not closing the registered connections, so every agent process has to be killed by hand.
- The reconnect dialog shows several of its state paragraphs at once (for example `Rejoining the server...` AND `Failed to rejoin.` together) -> the dialog's scoped stylesheet is not loading. This exact symptom persisted for a month after a project rename and is invisible except over a real HTTP round trip — check the browser's Network tab for a 404 on a `ReconnectModal` CSS file.
- A Room or a transcript is missing after the restart -> persistence is broken; Rooms and Transcripts must survive a restart.
- A budget pause survives the restart -> the counter is being persisted when it is documented as in-memory; not necessarily wrong, but a behaviour change worth reporting.

**Inconclusive if**

If the browser tab was already disconnected or backgrounded before step 3, the dialog sequence may not render — bring the tab to the foreground, restart from step 1. If the dialog never appears at all, check that the page was actually loaded from this app (the address bar shows http://localhost:5100) before judging.

> [!NOTE]
> A restart un-pausing a Room and losing every Draft is a recorded decision, not a defect.

### PIPEEXTERNAL-35 — An empty install shows its two empty-state strings, and the main pane does not auto-open the first new Room

**Free** · about 10 min

*Proves the app is usable and honest with nothing in it, and pins the one reasonable-looking-but-static behaviour a tester is likely to misfile.*

**Before you start**

- You are willing to DELETE the data directory. Every Room, Agent and Transcript on this machine is lost. If any of it matters, copy `src\Huddle.App\App_Data` somewhere first.

**Steps**

1. Press Ctrl+C in `T-A` and in every bot terminal. Wait for all prompts to return.
2. Delete the whole folder `E:\Repos\Huddle\src\Huddle.App\App_Data`.
3. In `T-A` run `$env:Team__DemoAgent__Enabled = 'false'` and `$env:Team__Acp__Enabled = 'false'`.
4. In `T-A` run `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
5. Open a fresh browser tab at http://localhost:5100.
6. Read the sidebar's Room list area and read the main pane.
7. Click **New chat** and read the panel.
8. Close the panel. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name mybot`.
9. WITHOUT touching the browser, read the sidebar and then read the main pane.
10. Click the `mybot` sidebar entry and read the heading.
11. Confirm `src\Huddle.App\App_Data` was recreated and contains `team.db`.
12. Type `hi @mybot`, press Enter, and confirm `src\Huddle.App\App_Data\rooms\` now exists with one `.jsonl` file.
13. When done, press Ctrl+C everywhere, run `Remove-Item Env:Team__DemoAgent__Enabled`, and restart the app with `P-LAUNCH-FREE`.

**Pass if — all of these**

- The sidebar's Room list reads exactly `No rooms yet. Start an agent to create one.`
- The main pane reads exactly the SAME sentence: `No rooms yet. Start an agent to create one.`
- The New chat panel reads exactly `No agents have connected yet. Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.`
- After the bot connects, the sidebar gains `mybot` with NO refresh.
- The MAIN PANE still reads `No rooms yet. Start an agent to create one.` until you click the sidebar entry.
- Clicking the entry opens the Room with heading `mybot`.
- `App_Data` is recreated with `team.db`, and `App_Data\rooms\` appears only after the first Message.

**Fail if — any of these**

- The empty-state sentences differ from the ones above -> user-facing copy drifted; report both the expected and the actual text.
- The New chat panel lists ghost agents after `App_Data` was deleted -> it is reading a stale database; find where `Team:DataDir` actually resolved (`Get-ChildItem -Recurse -Filter team.db E:\Repos\Huddle`) before filing, because the likely cause is that you deleted the wrong folder.
- The main pane throws or shows a Blazor error instead of the empty state -> the Room view does not handle having no Room.
- `App_Data` is not recreated at startup -> the data initialiser is not running, and nothing will persist.
- Demo agent Rooms appear despite `Team__DemoAgent__Enabled=false` -> the switch is not being read; every "empty install" test is then impossible.

**Inconclusive if**

The main pane NOT picking up the first new Room is EXPECTED, not a fail — the auto-navigate only runs when the page loads with no Room selected, and it is worth flagging as awkward but is not a crash. If you cannot delete `App_Data` because a file is locked, the app is still running: stop it, wait for the prompt, and delete again. If you skip the deletion, this test is INCONCLUSIVE — do not judge empty states against a populated install.

> [!NOTE]
> Deleting `App_Data` is also the documented fix for anything that looks like a database schema problem, because the table creation is conditional and never migrates.

### PIPEEXTERNAL-36 — Two app instances on one pipe name split clients unpredictably

**Free** · about 10 min

*Not a defect test but a diagnosis test: it reproduces, on purpose, the situation a tester creates by leaving a stale app running in another terminal, so agents landing in the wrong browser are recognised rather than filed as a delivery bug.*

**Before you start**

- The app is running normally in `T-A` on port 5100 with the default pipe name.
- A free terminal (`T-E`) and a free port (5101).

**Steps**

1. In `T-E` run `$env:Team__DataDir = 'App_Data2'`, `$env:Team__Acp__Enabled = 'false'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5101`. Do NOT set `Team__PipeName` — both instances must serve the same pipe name.
2. Open a second browser window at http://localhost:5101 and place it beside the first (http://localhost:5100).
3. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name splitter`.
4. Look at BOTH browser windows and note which one gained a `splitter` Room.
5. Press Ctrl+C in `T-C` and re-run `pwsh tools/echo-bot.ps1 -Name splitter1`, then `-Name splitter2`, then `-Name splitter3`, noting which window each lands in.
6. In whichever window does NOT hold `splitter`, look for any error or clue that the Agent went elsewhere.
7. In the window that DOES hold it, type `hi @splitter` and confirm a reply.
8. Run `Get-Process dotnet` and count the processes.
9. Look for both `src\Huddle.App\App_Data\team.db` and `src\Huddle.App\App_Data2\team.db`.
10. Press Ctrl+C in `T-E`, run `Remove-Item Env:Team__DataDir` there, and close that browser window.

**Pass if — all of these**

- The `splitter` Room appears in exactly ONE of the two browser windows, never both.
- Across the four names, the landings are not all in the same window — repeating the experiment sends a client to the other instance.
- The window that did not get the client shows NOTHING about it: no error, no warning, no clue.
- `Get-Process dotnet` shows more than one host process.
- Both `App_Data\team.db` and `App_Data2\team.db` exist, and only one of them has the Agent.

**Fail if — any of these**

- A client appears in BOTH windows -> that would be new behaviour and worth reporting, since a single instance per pipe name is the recorded assumption.
- The second instance refuses to start with a pipe error -> also worth reporting; the documented consequence is unpredictable splitting, not a hard failure.
- Neither window gets the client -> something else is serving the pipe; stop both instances, re-run `Get-Process dotnet`, and look for a stray host before filing anything.

**Inconclusive if**

If all four names land in the same window every time, the split is real but the race happened to be one-sided — that is not a fail, and the test still demonstrates the hazard. Record it as observed. If the second instance fails to start for an unrelated reason (port already in use), the test is INCONCLUSIVE — free the port and re-run.

> [!NOTE]
> THE POINT OF THIS TEST IS DIAGNOSTIC. A tester who leaves a stale `dotnet run` in another terminal will see agents arriving in the wrong browser and messages going unanswered, and will file it as a delivery bug. Always check the process list before filing anything in this area. Running one app instance per pipe name is the recorded consequence of choosing a named pipe, not a defect.

---

Back to [the manual test script](../manual-tests.md).
