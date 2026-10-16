# Creating Rooms, inviting Agents, Room naming

Prove that every path by which a Room is born or changes membership behaves as specified, and that the four live surfaces (sidebar, Room header h1, members line, invite candidate list) repaint over SignalR without a page reload. Covers: the automatic Direct Room an Agent gets on registration, the sidebar "New chat" panel, the "Invite teammate" control on the Room header, the `/invite @name` composer command, and the two Agent-facing tools `mcp__team__create_room` and `mcp__team__invite_agent`. The suite exists because a page GET only returns the Blazor prerender, so automated tests literally cannot see these panels, and because the load-bearing rule "an Agent belongs to at most one two-Member Room" only becomes visible as a second sidebar entry after a real re-registration. Tests INVITEROOMS-08 and INVITEROOMS-16 target this repo's two documented SILENT failures and are the highest-value tests in the set.

**38 tests** · 34 free, 4 paid 💰 · about 4.2 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Lane is `P-LAUNCH-UAT`. Wait until Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff. The built-in Chief of Staff is a Teammate like any other, not part of the cast: under this lane it runs on the mock adapter, and registering gives it a Direct Room, so it has its own sidebar entry (`AgentConnection.cs:109`). Its Room is not empty for long: with ACP on, an empty Direct Room takes a Greeting Turn, free on the mock adapter. Its data folder is `C:\seeds\huddle\uat\data`; that is `<data>` for INVITEROOMS-02 to 25 and 32 to 39. INVITEROOMS-26 to 31 need the real adapter and say explicitly when to leave and when to come back.
2. Open `http://localhost:5100` in Chrome or Edge with DevTools (F12) available — several tests need the Elements pane to read a CSS class such as `invite-panel` or `agent-dot`.
3. Learn the three `O-LOG` lines you will read all day, logged at Information by `ChatService`: `Created direct room '<id>' for agent '<name>'.` / `Created room '<id>' (<name>) with <n> members.` / `Invited agent '<name>' (<id>) into room '<roomId>'.` The ABSENCE of one of these is the oracle in several tests, so do not filter the console. Ada's and Bo's Rooms are seeded in advance, so launching the app logs no `Created direct room` line for them.
4. Ada, Bo and the Chief of Staff belong to no Team, so the **Team** select in the invite panel (a MudBlazor `MudSelect`, not an HTML `<select>`; its popover opens on click) offers only `All teams`.
5. Return to the starting point by re-running the seeder line of `P-LAUNCH-UAT` (it wipes the folder and rebuilds Ada, Bo and their two empty Rooms), then relaunch the app. Several tests tell you to do exactly this. A plain restart that keeps the data is the app command of `P-LAUNCH-UAT` WITHOUT the seeder line.
6. Each extra Agent is its own `P-ECHO-BOT` terminal. This area uses: `T-C` = `mybot`, `T-D` = short-lived scratch bots, and one further terminal per long-running bot, called by its bot's name: the `Emily Lee` terminal, the `gamma` terminal, the `delta` terminal.
7. RUN THE TESTS IN ID ORDER. Several deliberately set up the next one — INVITEROOMS-12 converts mybot's Direct Room, which is exactly what INVITEROOMS-21 needs to observe. Each test still states its own precondition and how to recover if the state is wrong.

## Tests

### INVITEROOMS-02 — Routing: / redirects to the first Room, and an unknown room id shows the empty state instead of crashing

**Free** · about 3 min

*Proves a bad or absent room id degrades to a readable empty state rather than an unhandled exception in the Blazor circuit.*

**Before you start**

- `P-LAUNCH-UAT` has been run, Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff.

**Steps**

1. Navigate to `http://localhost:5100/rooms/deadbeef`.
2. Read the main column.
3. Read the sidebar.
4. Look at the bottom of the browser window for the yellow/red bar reading `An unhandled error has occurred.`
5. Navigate to `http://localhost:5100/` again and read the address bar.

**Pass if — all of these**

- The main column reads exactly `No rooms yet. Start an agent to create one.`
- The sidebar still lists **Ada**, **Bo** and **Chief of Staff** — it is unaffected by the bad id.
- No `An unhandled error has occurred.` bar appears anywhere on the page.
- Returning to `/` redirects to `/rooms/<id>` of the first Room again.

**Fail if — any of these**

- The `An unhandled error has occurred.` bar appears -> a bad room id is throwing instead of returning `null` from `GetRoomAsync`; a stray link or a stale bookmark would kill the user's circuit.
- The sidebar empties as well -> the bad id is corrupting the shared Room list, not just this page's view.
- Navigating to `/` no longer redirects -> the redirect regressed; re-run the seeder line of `P-LAUNCH-UAT` and relaunch to confirm it is not a state problem.

**Inconclusive if**

If the page is blank white rather than showing the empty-state sentence, the circuit may have failed to connect rather than the route having failed. Press F5 once. If it is still blank, check `T-A` for an exception and report the test as inconclusive pending that stack trace.

> [!NOTE]
> The copy `No rooms yet. Start an agent to create one.` is MISLEADING for a bad id when Rooms plainly exist in the sidebar. That is current, documented behaviour — note it in your report, do NOT file it as a defect.

### INVITEROOMS-03 — The New chat panel toggles, lists every Agent with a status dot, and keeps Start chat disabled until something is ticked

**Free** · about 5 min

*Proves the panel's open/close, its population from the Agent user list, and the guard that stops an empty selection reaching the server. The panel is now `MudCollapse` (Stage 5 of the MudBlazor migration) rather than an always-in-the-DOM panel toggled by the `hidden` attribute, and its checkboxes are `MudCheckBox`, but the status dots are unchanged — `StatusDot.razor` still renders a plain `<span class="agent-dot ...">` with a `title` attribute.*

**Before you start**

- `P-LAUNCH-UAT` has been run, Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff. Nothing else has registered.

**Steps**

1. Click **New chat** in the sidebar's Rooms group, just under the Room list.
2. In the DevTools Console, type `document.querySelector('.new-chat .mud-collapse-container').getBoundingClientRect().height > 0` and press Enter — confirms the panel is genuinely open, not merely present in the DOM.
3. Count the checkbox rows in the panel that opens and read each row's text.
4. For each row, hover the small round dot immediately left of the name and read its tooltip.
5. In the Console, type `[...document.querySelectorAll('.agent-dot')].map(d => d.title)` and press Enter. Record the array.
6. Find the **Start chat** button at the bottom of the panel and try to click it without ticking anything.
7. In DevTools Elements, select the **Start chat** button and confirm it carries the `disabled` attribute.
8. Tick the checkbox next to `Ada`.
9. Look at the **Start chat** button again.
10. Untick `Ada`.
11. Click **New chat** again and repeat the Console command from step 2.

**Pass if — all of these**

- Step 2 printed `true` while the panel is open, and `false` after step 11 collapses it.
- The panel lists exactly three rows: `Ada`, `Bo` and the built-in Chief of Staff. `Ada` comes before `Bo`; the Chief of Staff's position is not asserted.
- Each row has a coloured dot whose tooltip is one of `online`, `offline`, `degraded` or `starting`. With Ada and Bo connected, both of their tooltips read `online`.
- Step 5's array contains only those four words.
- `You` does NOT appear anywhere in the list.
- Before anything is ticked, **Start chat** is visibly greyed out, does nothing when clicked, and carries the `disabled` attribute in the DOM.
- After ticking `Ada`, **Start chat** loses the `disabled` attribute and becomes clickable.
- Optional DB check: `sqlite3 <data>\team.db "SELECT name FROM users WHERE kind='agent' ORDER BY rowid;"` returns the same three names as the panel, in the panel's order.

**Fail if — any of these**

- **Start chat** is clickable with nothing ticked -> the `Disabled="@(this.selectedAgentIds.Count == 0)"` binding regressed and the server will be asked to create a Room with zero Agents.
- `You` appears as a tickable row -> the list is no longer filtering on `UserKind.Agent`; creating a chat with the Human would produce a nonsense Room name.
- An Agent is missing, or `Bo` is listed before `Ada` -> the panel is not reading `users ORDER BY rowid`, so the list will not be stable between visits.
- Every dot is grey while Ada and Bo are clearly connected (the Teammates page shows them Online) -> the presence lookup is broken and the tester can no longer tell a live Agent from a dead one anywhere in the app. Offline is RED (`--mud-palette-error`, `app.css:71-73`) and online is a green; a grey dot means neither state resolved.
- Step 2 prints `true` on a fresh page load, before ever clicking **New chat** -> `MudCollapse`'s `Expanded` parameter is defaulting open instead of closed.

**Inconclusive if**

If the dots are not green and you are NOT sure Ada and Bo are connected, open **Teammates** and read their status, and run `O-ADAPTERS` to confirm the `mock-acp` processes are running. If you cannot establish whether the agents are live, the dot colours are inconclusive — judge only the list contents and the disabled button, and say so. The panel's content is only rendered while it is open (`NewChat.razor:16`), and `MudCollapse` animates its height, so do not judge open/closed from View Source or a `curl.exe` fetch — use the Console height check above.

> [!NOTE]
> The status dots in this panel repaint only on RoomsChanged, not on presence changes. Do NOT test 'disconnect an agent with the panel open and watch the dot go red' — that is current behaviour, not a defect; note it in your report if you see it.

### INVITEROOMS-04 — A pipe client connecting creates its Room and it appears in the sidebar with no page refresh

**Free** · about 5 min

*Proves the SignalR RoomsChanged repaint reaches an already-rendered sidebar — the single most-used live behaviour in the product, and one no automated test can render.*

**Before you start**

- The app is running and the browser is open on any Room. Do NOT reload the page at any point during this test.

**Steps**

1. Position the browser so the sidebar is visible, and leave it alone — do not click in it.
2. Open a NEW PowerShell terminal at `<repo>`. Call it **`T-C`**.
3. In `T-C` run exactly: `pwsh tools/echo-bot.ps1 -Name mybot`
4. Read `T-C`'s output.
5. Watch the browser sidebar for up to 5 seconds WITHOUT touching the keyboard or mouse.
6. Read `T-A`.
7. Click the new sidebar link.
8. Read the `<h1>` and the grey line beneath it.
9. Read the main column between the header and the composer.

**Pass if — all of these**

- `T-C` prints `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...` then `Sent hello. Listening for messages (Ctrl+C to exit)...` and then a JSON envelope line.
- A new sidebar link reading exactly `mybot` appears within about a second, with NO page reload and no interaction of any kind.
- `T-A` prints `Created direct room '<id>' for agent 'mybot'.`
- Clicking the link navigates to a `/rooms/<id>` whose `<h1>` reads `mybot` and whose grey line reads `You, mybot`.
- The Transcript area is empty — no messages.
- Optional DB check: `sqlite3 <data>\team.db "SELECT name, kind FROM users WHERE name='mybot';"` returns `mybot|agent`.

**Fail if — any of these**

- The link only appears after you press F5 -> `RoomsChanged` is not reaching `RoomList`; either `PublishRoomsChanged` is not being called from `EnsureRoomForAsync`, or a component leaked its subscription. This is a silent failure in production — the user simply never sees new teammates arrive.
- `T-C` prints a `"type":"error"` envelope instead of a welcome -> the handshake rejected a legal Name; check the code in that envelope against `NameRules` before blaming the UI.
- TWO `mybot` links appear -> the Direct-Room lookup ran twice or the Room was minted twice.
- `T-C` hangs after `Sent hello` with no envelope at all and no sidebar change -> the server accepted the connection but never answered; look in `T-A` for an exception in the pipe accept loop.

**Inconclusive if**

If `pwsh` is not recognised, PowerShell 7 is not installed — this whole area's free tests need it. Install it, or run the script with `powershell.exe -File tools\echo-bot.ps1 -Name mybot` and note the substitution in your report. If `T-C` reports `Connect timed out`, the app is not running or another process holds the pipe — restart from the area setup rather than judging this test.

> [!NOTE]
> Leave `T-C` running. INVITEROOMS-05, 11, 12, 13 and 21 all use `mybot`.

### INVITEROOMS-05 — Reconnecting under the same Name re-attaches to the existing Room — it never mints a second one

**Free** · about 4 min

*Proves the exact-two-Member Direct Room lookup, so a bot that restarts every day does not accumulate one Room per restart.*

**Before you start**

- INVITEROOMS-04 passed and `T-C` is still running `mybot`.
- Nobody has been invited into mybot's Room, so it still has exactly two Members.

**Steps**

1. In the browser, click the sidebar link `mybot` and write down the full `/rooms/<id>` from the address bar.
2. Count the sidebar links and write the count down.
3. Note the current last line in `T-A`.
4. In `T-C` press Ctrl+C to stop the bot.
5. In `T-C` run exactly the same command again: `pwsh tools/echo-bot.ps1 -Name mybot`
6. Wait 3 seconds, then read the browser sidebar.
7. Read every line `T-A` has printed since the note you took.
8. Click the `mybot` link and compare the address bar to what you wrote down.

**Pass if — all of these**

- The sidebar shows exactly ONE link reading `mybot`, and the total sidebar link count is unchanged.
- `T-A` printed NO new `Created direct room` line during the reconnect.
- Clicking `mybot` lands on the identical `/rooms/<id>` you recorded before the restart.
- Optional DB check: `sqlite3 <data>\team.db "SELECT count(*) FROM rooms;"` returns the same number before and after the reconnect.

**Fail if — any of these**

- A second `mybot` link appears -> `FindRoomWithExactMembersAsync` regressed and every reconnect now mints a Room; over a week of restarts the sidebar becomes unusable. This is the failure this test exists for.
- `T-A` prints `Created direct room` on the reconnect while the sidebar still shows one link -> a Room was created and orphaned; the DB count check will catch it even though the UI looks fine. Treat it as the same defect.
- The `mybot` link now points at a DIFFERENT room id -> the old Room was abandoned and its Transcript is now unreachable from the UI.

**Inconclusive if**

If `T-C` fails to reconnect (`Connect timed out`), the server may not have released the previous connection yet. Wait 5 seconds and run the command once more. If it still fails, the test is inconclusive on a connection problem, not a Room problem — say so rather than recording a fail.

### INVITEROOMS-06 — A Name differing only in case re-attaches to the existing Agent instead of creating a second one

**Free** · about 4 min

*Proves the `users.name UNIQUE COLLATE NOCASE` constraint and the upsert behind it, so `MYBOT` cannot become a second identity beside `mybot`.*

**Before you start**

- INVITEROOMS-04 passed; an Agent named `mybot` has registered. `T-C` may be running or stopped — either is fine.

**Steps**

1. Count the sidebar links and write down every Room name you can see.
2. Open a NEW PowerShell terminal at `<repo>`. Call it **`T-D`**.
3. In `T-D` run exactly: `pwsh tools/echo-bot.ps1 -Name MYBOT` (upper case).
4. Read `T-D`'s output.
5. Read `T-C`'s output if it was running.
6. Wait 3 seconds, then read the browser sidebar.
7. Press Ctrl+C in `T-D` to stop the upper-case bot.
8. In `T-C`, re-run `pwsh tools/echo-bot.ps1 -Name mybot` so `mybot` is connected again for later tests.

**Pass if — all of these**

- `T-D` connects successfully and prints `Sent hello. Listening for messages (Ctrl+C to exit)...` — it is NOT rejected.
- No new sidebar link appears. The sidebar still shows the same Room names you wrote down, and the `mybot` entry is still spelled in lower case.
- `T-A` prints no new `Created direct room` line.
- If `T-C` was running, it prints `Server closed the connection.` — the server closed the stale connection when the same identity re-registered.
- Optional DB check: `sqlite3 <data>\team.db "SELECT name FROM users WHERE name LIKE 'mybot';"` returns exactly ONE row, still spelled `mybot`.

**Fail if — any of these**

- A second sidebar link reading `MYBOT` appears -> the `COLLATE NOCASE UNIQUE` constraint on `users.name` or the upsert regressed. Two Members whose Names differ only in case would then coexist, and Mention resolution between them becomes ambiguous everywhere in the app.
- The stored name flips to upper case (`MYBOT` in the sidebar and in `team.db`) -> the upsert is writing the Name as well as the description; every existing Mention of `mybot` in past Transcripts now reads against a renamed identity.
- `T-D` is rejected with a `"type":"error"` envelope -> the server is treating a case variant as an invalid Name rather than as the same identity; that is a regression, since case-insensitive re-attachment is the documented behaviour.

**Inconclusive if**

If `T-D` prints `Connect timed out`, the app is not reachable — fix that and retry rather than judging. If `T-C` was already stopped before you started, you cannot observe the `Server closed the connection.` half; judge only the sidebar and DB halves and note the omission.

> [!NOTE]
> Two Members whose Names differ only in case cannot both exist — this is a documented known limit, and re-attachment is the designed outcome, not a bug.

### INVITEROOMS-07 — An invalid Agent Name is refused at the handshake and no Room appears anywhere

**Free** · about 5 min

*Proves NameRules is enforced at the wire boundary, so a Name that could reach a file path or produce an unreadable Room name never enters the Team Directory.*

**Before you start**

- The app is running and the browser sidebar is visible.

**Steps**

1. Write down every sidebar link name you can currently see.
2. In `T-D` run exactly: `pwsh tools/echo-bot.ps1 -Name "bad name!"`
3. Read `T-D`'s output in full, including the JSON envelope.
4. Wait 3 seconds and read the browser sidebar.
5. In `T-D` run exactly: `pwsh tools/echo-bot.ps1 -Name "Emily  Lee"` (TWO spaces between the words).
6. Read `T-D`'s output.
7. In `T-D` run exactly: `pwsh tools/echo-bot.ps1 -Name "bot.exe"`
8. Read `T-D`'s output.
9. Wait 3 seconds and read the browser sidebar one more time.

**Pass if — all of these**

- For each of the three runs, `T-D` prints a JSON line containing `"type":"error"` (the wire discriminator a `ProtocolError` serialises to) and `"code":"invalidName"`, with a message of the form `'<the name you typed>' is not a valid agent name.`
- After each run the connection ends — the script does not sit waiting for messages.
- The browser sidebar is IDENTICAL to the list you wrote down. No new link appeared for any of the three names, and none appeared with a mangled or truncated spelling.
- `T-A` printed no `Created direct room` line for any of the three.
- Optional DB check: `sqlite3 <data>\team.db "SELECT name FROM users WHERE kind='agent';"` contains none of `bad name!`, `Emily  Lee` or `bot.exe`.

**Fail if — any of these**

- A Room appears for any of the three -> the handshake is no longer validating against NameRules; the `.` case is the dangerous one, because a Name is also the filename of a Persona (`{name}.md`) and this check is the path-traversal guard.
- The doubled-space name is ACCEPTED -> two Teammates could then exist that no reader can tell apart on screen (`Emily Lee` and `Emily  Lee` render identically).
- The script hangs with no envelope and the sidebar also gains nothing -> the server is silently dropping the connection instead of answering; the client has no way to learn why, which is a worse failure than the rejection itself.
- A Room appears whose name contains a path fragment or a file extension -> the guard is bypassed entirely; stop testing and report immediately.

**Inconclusive if**

If PowerShell's own quoting mangles the argument (you see the script report a different Name than you typed), retype the command by hand rather than pasting, and confirm the `Connecting to pipe ... as agent '<name>'` line echoes the exact string you intended. A mismatch there makes the test inconclusive, not failed.

> [!NOTE]
> Windows reserved device names (CON, NUL, COM1) DO pass NameRules and are accepted as Agent Names. That is a documented known limit — do not add a case for them and do not file it.

### INVITEROOMS-08 — New chat with exactly ONE Agent opens that Agent's existing Room and creates nothing

**Free** · about 7 min

*Proves the single-Agent short-circuit in CreateRoomForAsync. This is the one branch in the whole area that REUSES instead of creating, which makes it the most likely silent regression here — and the symptom is an empty-looking Room, which a user reads as lost history. The New chat panel this test drives is `MudCollapse` with `MudCheckBox` rows (Stage 5 of the MudBlazor migration) — see INVITEROOMS-03 — but nothing about that change touches the reuse-versus-create logic this test is actually about.*

**Before you start**

- `P-LAUNCH-UAT` has been run, Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff. Ada's Room is still a two-Member Room — nobody has been invited into it. If the sidebar's `Ada` entry has been renamed to anything containing a comma, this precondition is broken: re-run the seeder line of `P-LAUNCH-UAT` and relaunch first.

**Steps**

1. Click the sidebar link `Ada`.
2. Write down the full `/rooms/<id>` from the address bar. Call it ROOM-ADA.
3. Click into the composer textarea at the bottom (its placeholder reads `Message… (/invite @agent)`).
4. Type exactly: `hi @Ada`
5. Press Enter.
6. Wait up to 15 seconds and confirm two messages are now in the Transcript: yours, and a reply from Ada that contains `hi @Ada` (the mock adapter echoes the whole prompt back, so the reply is long and plain text).
7. Write down the exact text of both messages.
8. Note the current last line in `T-A`.
9. Count the sidebar links and write the count down.
10. Click **New chat** in the sidebar.
11. Tick the checkbox next to `Ada`, and ONLY that one. Confirm `Bo` is unticked.
12. Click **Start chat**.
13. Read the address bar.
14. Read the Transcript.
15. Count the sidebar links again.
16. Read every line `T-A` printed since your note.

**Pass if — all of these**

- The address bar reads exactly ROOM-ADA — the same room id you recorded before clicking Start chat.
- Both messages are still in the Transcript, with the same text.
- The sidebar link count is UNCHANGED, and there is still exactly one link reading `Ada`.
- `T-A` printed NO `Created room` line and NO `Created direct room` line.
- The New chat panel closed itself and its checkbox is no longer ticked when you reopen it.
- Optional DB check: `sqlite3 <data>\team.db "SELECT count(*) FROM rooms;"` returns the same number as before step 10.

**Fail if — any of these**

- A SECOND sidebar link reading `Ada` appears and the Transcript looks empty -> the `if (agents.Count == 1) return await EnsureRoomForAsync(...)` short-circuit regressed and every 'message this teammate' click now mints a fresh Room. To the user this reads as lost conversation history, with no error anywhere. This is THE regression this test exists to catch.
- The address bar changes to a different room id even though the sidebar count stayed the same -> a Room was created and the old one orphaned; the DB count check will confirm it.
- `T-A` prints `Created room '<id>' (Ada) with 2 members.` -> the same defect, visible in the log before you even look at the sidebar.
- `Start chat` does nothing at all -> the selection is not reaching the server; check the checkbox actually shows as ticked before blaming the button.

**Inconclusive if**

If Ada did not reply to `hi @Ada` within 15 seconds, check on the Teammates page that Ada is still Online. Without a reply you can still judge this test on the room id and the sidebar count alone, but say in your report that the history half was not observed; your own message is still in the Transcript, so a missing reply does not by itself empty it.

> [!NOTE]
> Do not invite anyone into Ada's Room for the rest of the free tests — INVITEROOMS-24 and the restart test read best with it intact.

### INVITEROOMS-09 — New chat with two Agents creates a Room named after them and navigates to it, live

**Free** · about 5 min

*Proves multi-Agent Room creation, the derived Room name (Agents only, in tick order), the Human's membership, and the sidebar repainting without a reload.*

**Before you start**

- `P-LAUNCH-UAT` has been run, Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff.

**Steps**

1. Note the current last line in `T-A`.
2. Count the sidebar links.
3. Click **New chat**.
4. Tick `Ada` FIRST, then tick `Bo`, without unticking either. The order matters.
5. Click **Start chat**.
6. Read the address bar.
7. Read the `<h1>` and the grey line beneath it.
8. Read the sidebar without pressing F5.
9. In DevTools Elements, select the new sidebar link and read its class list.
10. Click **New chat** again and read the checkbox states.
11. Read every line `T-A` printed since your note.

**Pass if — all of these**

- The address bar is a NEW `/rooms/<id>`, different from every id you have recorded so far.
- The `<h1>` reads exactly `Ada, Bo` — in the order you ticked, and with NO `You` in it.
- The grey line beneath reads exactly `You, Ada, Bo` — `You` first.
- A new sidebar link reading `Ada, Bo` is present WITHOUT a page reload.
- That sidebar link carries the `active` class (it is the highlighted one).
- The New chat panel closed itself, and reopening it shows both checkboxes cleared.
- `T-A` printed `Created room '<id>' (Ada, Bo) with 3 members.`
- Optional DB check: `sqlite3 <data>\team.db "SELECT u.name FROM room_members m JOIN users u ON u.id=m.user_id WHERE m.room_id='<the new id>' ORDER BY m.rowid;"` returns `You` (or the human row) first, then `Ada`, then `Bo`.

**Fail if — any of these**

- The `<h1>` includes `You` -> the Human is being folded into the derived name; every subsequent rename will compound it (`You, Ada, Bo, You`-style drift) and the name stops identifying the Agents.
- The name reads `Bo, Ada` when you ticked `Ada` first -> the name follows the stored list order rather than the tick order; the user's intent is being silently reordered.
- The new link only appears after F5 -> `RoomsChanged` is not reaching `RoomList` after a create; same class of failure as INVITEROOMS-04 but on a different call path.
- The panel stays open, or the checkboxes stay ticked -> a second click would create a duplicate Room the user did not ask for.
- `T-A` reports `with 2 members` -> the Human was not seeded; that Room is unpostable and violates ADR-0005's 'the Human is a Member of every Room'.

**Inconclusive if**

If the `active` class is not on the link but everything else passed, the highlight may be a routing/NavLink match detail rather than this feature — record the highlight sub-check as inconclusive and pass the rest. If the panel renders no checkboxes at all, go back and re-run INVITEROOMS-03 before judging this one.

### INVITEROOMS-10 — The same two-Agent selection twice creates a SECOND Room with the identical name

**Free** · about 4 min

*Confirms the documented absence of de-duplication for group Rooms, and — more importantly — that the second click actually does something rather than silently no-opping or bouncing you into the first Room.*

**Before you start**

- INVITEROOMS-09 passed and one Room named `Ada, Bo` exists. Write its room id down before you start.

**Steps**

1. Write down the `/rooms/<id>` of the existing `Ada, Bo` Room. Call it ROOM-EA1.
2. Note the current last line in `T-A`.
3. Click **New chat**.
4. Tick `Ada`, then `Bo` — the identical pair, in the identical order.
5. Click **Start chat**.
6. Read the address bar.
7. Read the sidebar.
8. Read every line `T-A` printed since your note.

**Pass if — all of these**

- The address bar shows a room id that is NOT ROOM-EA1.
- The sidebar now shows TWO links both reading `Ada, Bo`, pointing at two different room ids.
- `T-A` printed exactly one new `Created room '<id>' (Ada, Bo) with 3 members.` line, naming the new id.
- Both Rooms open and both are empty and independent.

**Fail if — any of these**

- Clicking **Start chat** does nothing visible — no navigation, no new link -> the create path is throwing silently; check `T-A` for an exception. A user clicking a button that does nothing has no way to know whether it worked.
- The browser navigates to ROOM-EA1 while `T-A` also logs `Created room` -> a Room was created and then abandoned; the user sees the old Transcript and believes it is the new Room.
- Only ONE `Ada, Bo` link is visible afterwards but the address bar shows a new id -> the sidebar did not repaint; press F5 once to distinguish a repaint failure from a create failure and say which you saw.

**Inconclusive if**

If you cannot tell the two `Ada, Bo` links apart in the sidebar, hover each and read the target `/rooms/<id>` in the browser status bar, or inspect both `href` values in DevTools. Do not guess.

> [!NOTE]
> Two Rooms with identical names is CORRECT behaviour today — only the single-Agent case reuses. Do NOT file the duplicate name as a defect. This test exists to catch the opposite failure: the second click doing nothing.

### INVITEROOMS-11 — Invite teammate on the Room header offers only Agents that are not already Members, and starts closed

**Free** · about 5 min

*Proves the invite candidate list is the set difference (all Agents minus this Room's Members), that the Human is never offered, and that the panel is collapsed on first render. The panel is now `MudCollapse` (Stage 5 of the MudBlazor migration) rather than an always-in-the-DOM panel toggled by the `hidden` attribute, and the Room header's **Invite teammate** button no longer carries an `invite-toggle` class — disambiguate it from the `/teammates` page's identically-labelled submit button by WHERE it is, not by a CSS class: this one lives inside the Room header's `.invite-teammate` wrapper.*

**Before you start**

- `T-C` is running `mybot` and the sidebar has a link reading exactly `mybot` (no comma in it). If it has a comma, mybot's Room has already been converted — re-run the seeder line of `P-LAUNCH-UAT`, relaunch and start again from INVITEROOMS-04.
- `P-LAUNCH-UAT` has been run, Ada and Bo are Online and the sidebar reads Ada, Bo, Chief of Staff.

**Steps**

1. Click the sidebar link `mybot`.
2. Before clicking anything else, look at the top right of the Room header and confirm the panel below **Invite teammate** is NOT showing.
3. In the DevTools Console, type `document.querySelector('.invite-teammate .mud-collapse-container').getBoundingClientRect().height > 0` and press Enter. It must print `false`.
4. Click the **Invite teammate** button at the top right of the Room header — the one inside `.invite-teammate`, not the `/teammates` page's **Add teammate** submit button. Repeat the Console command from step 3; it must now print `true`.
5. Read every entry in the list that opens.
6. Read the label above the list.

**Pass if — all of these**

- Before the click, the Console command in step 3 prints `false` and no candidate list is visible.
- After the click, it prints `true`, and the panel shows a select labelled **Team** (a `MudSelect`, so the options sit in a popover that opens when you click it) whose first item reads `All teams`.
- The list beneath contains one button per Agent that is NOT already in this Room — with the setup so far that is `Ada`, `Bo` and the built-in Chief of Staff.
- `mybot` itself is ABSENT from the list.
- `You` is absent from the list.
- Each entry has a status dot to the left of the name whose tooltip reads `online`, `offline`, `degraded` or `starting`.

**Fail if — any of these**

- `mybot` is offered in its own Room -> the member-exclusion filter regressed; clicking it would be a no-op dressed up as a success message, which is exactly the confusion `LoadCandidatesAsync` exists to prevent.
- `You` is offered -> the list is no longer filtering on `UserKind.Agent`; inviting the Human would duplicate a Member row that already exists in every Room.
- The panel is already open when the Room loads (step 3's FIRST check prints `true`) -> `MudCollapse`'s `Expanded` parameter is defaulting open instead of closed, and the invite UI now competes with the Transcript for attention on every Room open.
- An Agent you know has registered is missing from the list -> either the Team filter is stuck on a real Team (check the **Team** select reads `All teams`) or the candidate query is wrong.

**Inconclusive if**

If you clicked a button labelled 'Add teammate' and a Name/Title/Alias FORM opened instead of a candidate list, you are on the `/teammates` page, not in a Room. Navigate back to `/rooms/<id>` and click the one inside the Room header. Do not record a result from the wrong control.

### INVITEROOMS-12 — Clicking a candidate invites it, renames the Room, and repaints all four surfaces with no reload

**Free** · about 9 min

*Proves the whole invite-and-rename transaction, and that panel text, header h1, members line and sidebar link all follow live. Also proves an Invitation writes NO Message into the Transcript.*

**Before you start**

- INVITEROOMS-11 passed; you are in the `mybot` Room with the Invite teammate panel open, and `Bo` is offered.

**Steps**

1. Close the Invite teammate panel by clicking **Invite teammate** again.
2. Click into the composer and type exactly: `hi @mybot`
3. Press Enter. Wait 3 seconds and confirm the Transcript now holds your message and a reply that starts with a bold `mybot:` (the Transcript renders Markdown, so no asterisks show; only the `.jsonl` file holds the literal `**mybot:**`).
4. In File Explorer or a terminal, open `<data>\rooms\` and find the `<roomId>.jsonl` file matching this Room's id. Count its lines: `(Get-Content '<data>\rooms\<roomId>.jsonl' | Measure-Object -Line).Lines`. Write the number down.
5. Note the current last line in `T-A`.
6. Click **Invite teammate** in the Room header (inside `.invite-teammate` — see INVITEROOMS-11's note on why this is no longer identified by an `invite-toggle` class).
7. Click the entry `Bo` in the candidate list.
8. IMMEDIATELY read the blue Info alert that appears inside the panel (`InviteTeammate.razor:23`), and copy its text exactly.
9. Read the candidate list again.
10. Read the Room header `<h1>`.
11. Read the grey line beneath the `<h1>`.
12. Read the sidebar.
13. Confirm the browser has NOT reloaded (the address bar is unchanged and no page flash occurred).
14. Read every line `T-A` printed since your note.
15. Count the lines in the same `.jsonl` file again.

**Pass if — all of these**

- The panel line reads EXACTLY `Invited Bo. Room is now "mybot, Bo".` — including the straight double quotes around the Room name.
- `Bo` has disappeared from the candidate list.
- The Room header `<h1>` has changed from `mybot` to `mybot, Bo`.
- The grey members line has changed to `You, mybot, Bo`.
- The sidebar link has renamed in place from `mybot` to `mybot, Bo` — no new link was added.
- The address bar is unchanged and no page reload occurred.
- `T-A` printed `Invited agent 'Bo' (<id>) into room '<roomId>'.`
- The `.jsonl` line count is IDENTICAL to what you wrote down — an Invitation persists no Message.

**Fail if — any of these**

- Any one of the four surfaces (panel line, `<h1>`, members line, sidebar link) still shows the old value until you press F5 -> that surface is not subscribed to `RoomsChanged`, or its component leaked its handler. Say WHICH surface, because they are four separate subscriptions and the fix differs.
- The `<h1>` reads `You, mybot, Bo` -> the rename is including the Human; the Room name stops identifying the Agents, and every later rename compounds it.
- A NEW sidebar link appears beside the old one -> a Room was created rather than renamed; the old Transcript is now split from the membership the user thinks it has.
- The `.jsonl` line count grows by one -> an Invitation is persisting a Message, contradicting the pinned behaviour and polluting every Transcript with membership noise.
- A red error alert appears in the panel instead of the blue info alert -> read it; it is a `ChatException` message and names the actual failure.
- `Bo` is still offered in the list after the invite -> the candidate list did not reload; the next click would be a silent no-op.

**Inconclusive if**

If the Room has no `.jsonl` file at all, `mybot` never replied and no Message was ever persisted — redo steps 2-3 until the Transcript is non-empty, because the 'invite writes nothing' half cannot be judged against a file that does not exist. If you cannot match a room id to a `.jsonl` filename, read the id from the address bar; the filename is exactly that id.

> [!NOTE]
> This test deliberately converts mybot's Direct Room into a group Room. INVITEROOMS-21 depends on that having happened.

### INVITEROOMS-13 — Invite teammate says so when every Agent is already a Member, and hides the Team filter in that state

**Free** · about 5 min

*Proves the empty candidate state is explained rather than rendered as a blank panel, and that the Team filter does not hang over an empty list. This is the same `MudCollapse` panel INVITEROOMS-11 opens — its Team filter is a `MudSelect` (`InviteTeammate.razor:35`), so there is no `<select>` element to look for, in this state or any other.*

**Before you start**

- INVITEROOMS-12 passed; you are in the `mybot, Bo` Room with Invite teammate open (Ada and the Chief of Staff are still offered).
- No new Agent registers during this test — do not start any extra bots until it is finished.

**Steps**

1. With the Invite teammate panel open, click the FIRST entry in the candidate list.
2. Read the info line and confirm the candidate disappeared.
3. Repeat: click the next remaining entry, one at a time, until the candidate list is empty.
4. Read the whole panel.
5. Look for the select labelled **Team**: search the panel for the label text, or run `document.querySelector('.invite-teammate .mud-select')` in the Console and read whether it prints `null`.
6. Read the Room header `<h1>` and the members line.

**Pass if — all of these**

- After the last candidate is invited, the list is replaced by the sentence `Every agent is already in this room.`
- The **Team** select (the `MudSelect`) is NOT rendered at all in this state: its label is absent and the Console query in step 5 prints `null`.
- The `<h1>` now names every registered Agent, comma-separated, and the members line begins with `You`.
- `T-A` printed one `Invited agent ...` line per click, and no more.
- Optional DB check: for this room id, `SELECT count(*) FROM room_members WHERE room_id='<id>'` equals `SELECT count(*) FROM users`.

**Fail if — any of these**

- The panel goes blank with no sentence -> an empty list reads as a broken panel; the user cannot tell 'nothing to do' from 'failed to load'.
- The **Team** filter is still rendered over an empty list -> a control that can only ever produce the same empty result is offered, which tells the user the filter is at fault when it is not.
- A candidate you already invited reappears in the list -> the reload after an invite is reading stale membership.
- Two clicks produce only one `Invited agent` line in `T-A` -> one of the invites silently did nothing.

**Inconclusive if**

If a new Agent connects mid-test (a terminal you forgot about), the list will never empty. Check which terminals are running bots, stop the unexpected one, then restart this test. Do not record a fail caused by your own extra bot.

### INVITEROOMS-14 — The invite panel's open state and its message do not survive a Room switch

**Free** · about 4 min

*Proves the OnParametersSetAsync reset, so Room B never shows Room A's 'Invited …' line or Room A's candidate list.*

**Before you start**

- At least two Rooms exist and at least one Agent is not a Member of one of them. After INVITEROOMS-09 and 10 there are two `Ada, Bo` Rooms — use the second one (the one INVITEROOMS-10 created) as Room B.

**Steps**

1. Click the sidebar link whose name begins `mybot, Bo` (Room A — INVITEROOMS-13 may have appended more Members to its name).
2. Click **Invite teammate** in the Room header.
3. Confirm the panel shows a message, a candidate list or, if every Agent is already a Member, `Every agent is already in this room.`
4. Click a DIFFERENT sidebar link — the second `Ada, Bo` Room (Room B).
5. Without clicking anything else, look at the top right of Room B's header.
6. In the DevTools Console, type `document.querySelector('.invite-teammate .mud-collapse-container').getBoundingClientRect().height > 0` and press Enter. It must print `false`.
7. Click **Invite teammate** in Room B and read the panel contents.
8. Click back to Room A and look at its header area without clicking.

**Pass if — all of these**

- On arriving in Room B, the invite panel is CLOSED — nothing is visible beneath the Invite teammate button and the Console command in step 6 prints `false`.
- When you open Room B's panel, there is NO `Invited …` info line and no red error line carried over from Room A.
- Room B's candidate list contains only Agents that are not Members of Room B — an Agent that is a Member of B is absent even if it was offered in A.
- Returning to Room A shows its panel closed again, with no leftover message.

**Fail if — any of these**

- Room B's header shows Room A's `Invited …` message -> a stale-parameter bug of exactly the kind `OnParametersSetAsync`'s guard exists to prevent; the user is told an action happened in a Room where it did not.
- Room B's panel opens by itself on navigation -> the `isOpen` reset regressed.
- Room B offers an Agent that is already a Member of Room B -> Room A's candidate list was carried across; clicking it is a no-op dressed as a success.
- The panel in Room B is empty (no list, no sentence) -> candidates were cleared but not reloaded.

**Inconclusive if**

If every Agent happens to be a Member of both Rooms, both panels will show `Every agent is already in this room.` and you cannot tell a carried-over list from a correct one. Start one extra bot (`pwsh tools/echo-bot.ps1 -Name scratch` in `T-D`), invite it into Room B only, then re-run — or note the test as inconclusive for want of a distinguishing Agent.

> [!NOTE]
> This test's composer-side counterpart is `INVITEROOMS-32`, at the end of this file — the coloured line above the message box has its own separate reset guard, and its own separate bug.

### INVITEROOMS-15 — /invite @name in the composer does the same thing as the header control

**Free** · about 6 min

*Proves the composer command path reaches the same InviteAsync, reports through the composer's own info line, and posts nothing into the Transcript.*

**Before you start**

- Both `Ada, Bo` Rooms from INVITEROOMS-09/10 exist, and ROOM-EA1 (the one INVITEROOMS-10 told you to write down) does not yet contain `gamma`.

**Steps**

1. Open a NEW PowerShell terminal at `<repo>`. Call it the **`gamma` terminal**.
2. In the `gamma` terminal run exactly: `pwsh tools/echo-bot.ps1 -Name gamma`
3. Wait until a `gamma` link appears in the sidebar.
4. Click the `Ada, Bo` Room whose id you recorded as ROOM-EA1 in INVITEROOMS-10 (hover the two `Ada, Bo` links to tell them apart; the other one stays untouched for INVITEROOMS-16). Write its room id down.
5. Confirm the composer placeholder reads `Message… (/invite @agent)`.
6. Note the current last line in `T-A`.
7. If the Room already has a `.jsonl` file under `<data>\rooms\`, count its lines and write the number down. If it has none, write down 'no file'.
8. Click into the composer and type exactly: `/invite @gamma`
9. Press Enter.
10. Read the green line that appears directly ABOVE the composer (class `composer-info`, `app.css:732`).
11. Read the Transcript.
12. Read the header `<h1>`, the members line, and the sidebar.
13. Read every line `T-A` printed since your note.
14. Count the `.jsonl` lines again (or confirm the file still does not exist).

**Pass if — all of these**

- A green info line appears above the composer reading EXACTLY `Invited gamma. Room is now "Ada, Bo, gamma".`
- NOTHING was added to the Transcript — no message bubble for `/invite @gamma`.
- The header `<h1>` now reads `Ada, Bo, gamma`, the members line reads `You, Ada, Bo, gamma`, and the sidebar link renamed in place — all with no page reload.
- `T-A` printed `Invited agent 'gamma' (<id>) into room '<roomId>'.`
- The `.jsonl` line count is unchanged (or the file still does not exist).

**Fail if — any of these**

- `/invite @gamma` appears as an ordinary message bubble in the Transcript -> the leading-slash branch in `SubmitFromComposerAsync` is not being taken; every command a user types is now permanent Transcript noise.
- The rename happens but no info line appears (or the reverse) -> half the transaction is reporting; the user cannot tell whether the invite worked.
- The Room renames to include `You` -> the derived-name rule regressed on the composer path specifically, even though the header control is fine.
- The `.jsonl` file gains a line -> the command is being persisted as a Message; this is pinned by an automated test, so a regression here means that test was disabled or changed.

**Inconclusive if**

If no `gamma` link appeared in the sidebar within 5 seconds, the bot did not register — read the `gamma` terminal for a `"type":"error"` envelope and fix that before judging. If the composer swallows your Enter and inserts a newline instead, you pressed Shift+Enter; Enter alone sends.

> [!NOTE]
> Leave the `gamma` terminal running — INVITEROOMS-20 needs `gamma`.

### INVITEROOMS-16 — /invite accepts a multi-word Name, with and without the @, and never truncates at the space

**Free** · about 8 min

*Targets THE documented silent failure of this area: a Name captured by shape stops at the first space and then blames the wrong Name ('Unknown agent @Emily'). The Name must be captured as everything after /invite.*

**Before you start**

- Two Rooms in which `Emily Lee` is not yet a Member: ROOM-EA1, now named `Ada, Bo, gamma` after INVITEROOMS-15, and the second `Ada, Bo` Room from INVITEROOMS-10. Do not use `Ada` or `Bo`'s own Rooms: INVITEROOMS-33 and INVITEROOMS-38 need them untouched.

**Steps**

1. Open a NEW PowerShell terminal at `<repo>`. Call it the **`Emily Lee` terminal**.
2. In the `Emily Lee` terminal run exactly: `pwsh tools/echo-bot.ps1 -Name "Emily Lee"` (with the quotes, one space between the words).
3. Wait until a sidebar link reading exactly `Emily Lee` appears.
4. Click ROOM-EA1 (the `Ada, Bo, gamma` Room). Write its room id down.
5. Note the current last line in `T-A`.
6. Click into the composer and type exactly: `/invite @Emily Lee`
7. Press Enter.
8. Read the green line above the composer, and copy its exact text.
9. Read the header `<h1>` and the sidebar link for this Room.
10. Now click the second `Ada, Bo` Room from INVITEROOMS-10 (still named `Ada, Bo`; hover the two links to tell them apart).
11. Type exactly: `/invite Emily Lee` (NO @ this time).
12. Press Enter.
13. Read the green line above the composer.
14. Read that Room's header `<h1>`.
15. Read every line `T-A` printed since your note.

**Pass if — all of these**

- The first command produces a green info line reading `Invited Emily Lee. Room is now "Ada, Bo, gamma, Emily Lee".` — the full two-word Name, not truncated.
- The first Room's `<h1>` and sidebar link both end in `Emily Lee`, spelled in full.
- The second command (no `@`) also produces a green info line, `Invited Emily Lee. Room is now "Ada, Bo, Emily Lee".`, and that Room renames to include `Emily Lee` in full.
- `T-A` printed one `Invited agent 'Emily Lee' (<id>) into room '<roomId>'.` line for each of the two invites.
- Optional DB check: `sqlite3 <data>\team.db "SELECT name FROM rooms;"` shows the full string `Emily Lee` in both renamed Rooms, never `Emily` alone.

**Fail if — any of these**

- A RED line reads `Unknown agent @Emily` -> THE documented silent failure has returned: the Name was captured by shape and truncated at the first space, and the error then blames a Name the user never typed. Report this with the exact red text.
- The Room renames to `Ada, Bo, gamma, Emily` -> the same defect, but it has already reached the data; the truncated name is now persisted in `rooms.name`.
- The no-@ form fails while the @ form works -> the optional `@` in the pattern regressed; users who omit the @ get a false 'unknown agent'.
- A red `Unknown command` line appears -> the command did not match the invite pattern at all; the whole `/invite` branch is broken, not just the multi-word case.

**Inconclusive if**

If no `Emily Lee` link appears in the sidebar, check the `Emily Lee` terminal for a `"type":"error"` envelope with code `invalidName`. If the Name was rejected at the handshake, this test cannot run — that is an INVITEROOMS-07 concern, not this one, and you should say so rather than recording a fail here. Also confirm you typed exactly ONE space between the words; two spaces is a different, deliberately-invalid Name.

> [!NOTE]
> This and INVITEROOMS-08 are the two highest-value tests in the area. Run them even if you are short on time.

### INVITEROOMS-17 — /invite of an Agent already in the Room reports success and changes nothing

**Free** · about 4 min

*Proves the invite is idempotent at the data layer — no duplicate member row, no doubled name — even though the wording says 'Invited'.*

**Before you start**

- A Room in which the named Agent IS already a Member. Use the `Ada` Room and the Agent `Ada`.

**Steps**

1. Click the sidebar link `Ada` and write down its `<h1>` text and its members line text.
2. Note the current last line in `T-A`.
3. Click into the composer and type exactly: `/invite @Ada`
4. Press Enter.
5. Read the coloured line above the composer.
6. Read the `<h1>` and the members line again.
7. Read the sidebar.
8. Read every line `T-A` printed since your note.

**Pass if — all of these**

- A green info line reads exactly `Invited Ada. Room is now "Ada".`
- The `<h1>` is unchanged — still exactly `Ada`, NOT `Ada, Ada`.
- The members line is unchanged — still exactly `You, Ada`, with `Ada` appearing once.
- The sidebar link is unchanged.
- Optional DB check: `SELECT count(*) FROM room_members WHERE room_id='<id>'` is still 2, and `rooms.name` is still `Ada`.

**Fail if — any of these**

- The `<h1>` becomes `Ada, Ada` -> the member insert is no longer `INSERT OR IGNORE`, so a Member can be duplicated and the derived name doubles with it. Every later rename compounds the duplication.
- The members line shows `Ada` twice -> the same defect, visible on the other surface.
- A red error line appears instead -> a no-op invite is now an error; that is a behaviour change and should be reported, but it is NOT the dangerous direction.
- An unhandled error bar appears -> the duplicate-insert path is throwing.

**Inconclusive if**

If Ada's Room has been renamed by an earlier test (its `<h1>` contains a comma), pick any Room and any Agent already listed in its members line instead, and adjust the expected text accordingly. State which Room and Agent you used.

> [!NOTE]
> The wording 'Invited Ada' over a no-op is a KNOWN rough edge. Do NOT file it as a defect. The Agent-facing tool words the same case differently (`Ada is already a member of room '<id>'. Nothing to do.`) and that difference is intentional.

### INVITEROOMS-18 — /invite rejects an unknown Name, and rejects the Human

**Free** · about 4 min

*Proves an unresolvable handle produces a readable red error and no state change, and that the Human can never be added as an Agent Member.*

**Before you start**

- Use the second `Ada, Bo` Room from INVITEROOMS-10, not `Ada` or `Bo`'s own Room. `Team:HumanName` is `You` (the shipped default in `appsettings.json`).

**Steps**

1. Click the second `Ada, Bo` Room and write down its `<h1>`, its members line and the sidebar link text.
2. Note the current last line in `T-A`.
3. Click into the composer and type exactly: `/invite @nobody`
4. Press Enter.
5. Read the line above the composer and note its colour (red, class `composer-error`).
6. Read the `<h1>`, the members line and the sidebar again.
7. Type exactly: `/invite @You`
8. Press Enter.
9. Read the coloured line above the composer.
10. Read the `<h1>` and the members line again.
11. Read every line `T-A` printed since your note.
12. Check the bottom of the page for the `An unhandled error has occurred.` bar.

**Pass if — all of these**

- `/invite @nobody` produces a RED error line reading exactly `Unknown agent @nobody`.
- `/invite @You` produces a RED error line reading exactly `Unknown agent @You`.
- After both, the `<h1>`, the members line and the sidebar link are all unchanged.
- `T-A` printed NO `Invited agent` line for either attempt.
- No `An unhandled error has occurred.` bar appeared.

**Fail if — any of these**

- `You` is added as a Member -> the Human now has a duplicate membership row; the members line shows `You` twice and the derived Room name may pick up a Human. This breaks the one-Human invariant the whole model rests on.
- A GREEN info line appears for `@nobody` -> a failed invite is reported as a success; the user believes a teammate joined who did not.
- The `An unhandled error has occurred.` bar appears -> the unknown-name path is throwing instead of raising a `ChatException`, and the user's circuit dies on a typo.
- The Room renames even though the error line appeared -> the rename is happening before the resolve check.

**Inconclusive if**

If the line that appears is neither clearly red nor clearly green, read its CSS class in DevTools: `composer-error` is the failure class and `composer-info` is the success class. Judge on the class, not the colour, and say which you saw.

### INVITEROOMS-19 — Any other leading-slash text is refused as Unknown command, and the typed text is lost

**Free** · about 5 min

*Proves every leading-slash input goes through the command parser and that a non-matching one produces a readable error rather than a posted Message.*

**Before you start**

- Any Room.

**Steps**

1. Click any sidebar Room link. If it has a `.jsonl` file under `<data>\rooms\`, count its lines and write the number down.
2. Click into the composer and type exactly: `/help`
3. Press Enter.
4. Read the coloured line above the composer.
5. Read the Transcript.
6. Look at the composer textarea.
7. Type exactly: `/invite` (nothing after it).
8. Press Enter and read the coloured line.
9. Type exactly: `/invite ` (with ONE trailing space).
10. Press Enter and read the coloured line.
11. Count the `.jsonl` lines again.

**Pass if — all of these**

- `/help` produces a RED line reading exactly `Unknown command`, and nothing is added to the Transcript.
- Bare `/invite` produces the same RED `Unknown command` line.
- `/invite ` with a trailing space produces the same RED `Unknown command` line.
- After each Enter the composer textarea is EMPTY — your text is gone.
- The `.jsonl` line count is unchanged across all three attempts.

**Fail if — any of these**

- `/help` appears as a message bubble in the Transcript -> the leading-slash branch is not being taken; any future command a user types becomes permanent Transcript content.
- An `An unhandled error has occurred.` bar appears -> an unmatched command is throwing rather than raising a `ChatException`.
- Bare `/invite` produces a green info line -> the pattern now matches an empty Name and something was invited under a blank handle.

**Inconclusive if**

If the textarea does NOT clear, check whether you pressed Shift+Enter (which inserts a newline and does not send). Re-test with a plain Enter before recording anything.

> [!NOTE]
> BOTH of these are CURRENT behaviour and must NOT be filed as defects: (a) an ordinary message that legitimately begins with a slash (e.g. `/opt/bin is the path`) cannot be posted at all, because every leading-slash text goes through the parser; (b) the client-side Enter handler clears the textarea as soon as `SendAsync` returns (`app.js:54-55`), whether or not the server accepted the text, so a rejected command loses what you typed. Neither is listed in `known-limits.md`; note both in your report.

### INVITEROOMS-20 — A Room's name is always its Agent Members joined by ", " in membership order, with the Human excluded

**Free** · about 6 min

*Proves the rename is rebuilt from membership order (room_members rowid) so an invite APPENDS rather than reshuffles, and the Human is never in the name.*

**Before you start**

- `P-LAUNCH-UAT` is running, Ada and Bo are Online, and `gamma` is registered. The `gamma` terminal must still be running `gamma` (from INVITEROOMS-15).

**Steps**

1. Click **New chat** in the sidebar.
2. Tick `Bo` FIRST, then tick `Ada`. This order is deliberate and is the reverse of INVITEROOMS-09.
3. Click **Start chat**.
4. Read the `<h1>` and the members line, and write both down exactly.
5. Note the current last line in `T-A`.
6. Click into the composer and type exactly: `/invite @gamma`
7. Press Enter.
8. Read the `<h1>` and the members line again.
9. Read the sidebar link for this Room.
10. Read every line `T-A` printed since your note.

**Pass if — all of these**

- Immediately after Start chat, the `<h1>` reads exactly `Bo, Ada` — tick order, NOT alphabetical and NOT the New chat list order.
- The members line reads exactly `You, Bo, Ada`.
- After the `/invite @gamma`, the `<h1>` reads exactly `Bo, Ada, gamma` — `gamma` APPENDED, with `Bo` and `Ada` in their original order.
- The members line reads exactly `You, Bo, Ada, gamma` — `You` first and appearing exactly once.
- The sidebar link reads `Bo, Ada, gamma`.
- Optional DB check: `sqlite3 <data>\team.db "SELECT u.name FROM room_members m JOIN users u ON u.id=m.user_id WHERE m.room_id='<id>' AND u.kind='agent' ORDER BY m.rowid;"` returns `Bo`, `Ada`, `gamma` in that order, and joining them with ', ' equals `rooms.name` exactly.

**Fail if — any of these**

- The name becomes `Ada, Bo, gamma` or `Bo, gamma, Ada` after the invite -> the rename is not reading `ORDER BY m.rowid`; a Room the user has learned to recognise silently reshuffles its own name on every invite.
- `You` appears anywhere in the `<h1>` or in `rooms.name` -> the Human is leaking into the derived name.
- The name contains an id rather than a Name (a guid-looking fragment) -> the rename is being rebuilt from user ids instead of resolved Names.
- The `<h1>` and the sidebar link disagree -> two surfaces are deriving the name independently instead of both reading the stored `rooms.name`.

**Inconclusive if**

If `gamma` is not in the candidate set (the `/invite` returns `Unknown agent @gamma`), the `gamma` terminal has stopped. Restart it with `pwsh tools/echo-bot.ps1 -Name gamma`, wait for the sidebar entry, and retry. Do not record a fail for an absent bot.

> [!NOTE]
> A Room can be renamed by hand (issue #43), and since 2026-09-21 it can also be **archived or deleted** from the context menu on its sidebar row — see [ADR-0018](../../adr/0018-a-room-can-be-archived-or-deleted.md). A Room still cannot be *left*: membership changes only by Invitation, and there is no control anywhere that removes one Member from a Room. Archiving hides a Room from the sidebar and is reversible from **Archived Rooms**, bottom-left; deleting removes the Room and its Transcript for good. A Room name is still always derived until somebody renames it.

### INVITEROOMS-21 — An Agent belongs to at most one two-Member Room: after an invite, the next registration mints a fresh Direct Room

**Free** · about 7 min

*Proves ADR-0003's stated consequence, which only becomes visible after a real re-registration and which no automated test renders. Catches the two opposite regressions: no new Room at all, and the OLD Room being renamed back.*

**Before you start**

- INVITEROOMS-12 and 13 ran, so `mybot`'s original Direct Room has been converted into a group Room (its sidebar entry contains commas and at least three Members).
- `T-C` is running `mybot`.

**Steps**

1. Find the sidebar link whose name STARTS with `mybot,` — the converted group Room. Click it, write its room id down (call it OLD-ROOM), and confirm its Transcript still holds the `hi @mybot` exchange from INVITEROOMS-12.
2. Count the sidebar links and write the count down.
3. Note the current last line in `T-A`.
4. In `T-C` press Ctrl+C.
5. In `T-C` run exactly: `pwsh tools/echo-bot.ps1 -Name mybot`
6. Watch the browser sidebar for up to 5 seconds WITHOUT reloading the page.
7. Read every line `T-A` printed since your note.
8. Click the new sidebar link and read its room id, its `<h1>`, its members line, and its Transcript.
9. Click OLD-ROOM again and read its `<h1>` and its Transcript.

**Pass if — all of these**

- A SECOND sidebar link appears, reading exactly `mybot`, WITHOUT a page reload. The sidebar count went up by one.
- `T-A` printed a new `Created direct room '<newId>' for agent 'mybot'.` line, naming a room id you have not seen before.
- The new Room's `<h1>` reads `mybot`, its members line reads `You, mybot`, and its Transcript is EMPTY.
- OLD-ROOM is still present, still named `mybot, Bo, ...` (whatever INVITEROOMS-13 left it as), and still holds the original `hi @mybot` exchange.
- Optional DB check: two rooms include `mybot` as a member; only the NEW one has exactly 2 member rows.

**Fail if — any of these**

- No second Room appears and `T-A` logs nothing -> the Agent has been left with NO Direct Room at all; there is now no way to message it one-to-one. (`mybot` is a pipe client, so it has no Teammate card and no Message action; the sidebar is the only place its Room shows.)
- OLD-ROOM gets RENAMED back to `mybot` -> the exact-membership lookup is matching a 3+-member Room; the group Room's identity is being stolen and its name no longer describes its Members.
- Both Rooms exist but only one shows until you press F5 -> `RoomsChanged` is not reaching `RoomList` on this call path specifically.
- OLD-ROOM's Transcript is now empty or has moved into the new Room -> the Transcript is being re-keyed on rename, and history has been destroyed.

**Inconclusive if**

If the sidebar entry for `mybot` still has NO comma, INVITEROOMS-12 did not actually convert it and this test has nothing to observe. Go back, invite one Agent into the `mybot` Room, then return. If the reconnect fails with `Connect timed out`, wait 5 seconds and retry the command once before judging.

> [!NOTE]
> TWO sidebar entries reading `mybot`-and-something is the DESIGNED outcome, recorded in `docs/adr/0003-...md` under Consequences. Do NOT file the second entry as a duplicate-Room bug. The failure is the absence of it.

### INVITEROOMS-22 — Two browser tabs stay in step on a membership change, including an open candidate list

**Free** · about 7 min

*Proves the singleton RoomEvents hub fans out to every circuit, and that a second tab's OPEN invite panel reloads its candidates — the case where a leaked or unsubscribed component shows itself. The invite panel is a `MudCollapse` (`InviteTeammate.razor:15-16`) whose content is rendered while collapsed too, so "leave the panel OPEN" in step 4 means expanded (its `.mud-collapse-container` has a non-zero height), not merely rendered.*

**Before you start**

- `gamma` is running (the `gamma` terminal) and is not a Member of the test Room. Do NOT use an existing Room: ROOM-EA1 and the second `Ada, Bo` Room already contain `gamma`, `Ada`'s own Room is needed untouched by INVITEROOMS-33, and `Bo`'s own Room by INVITEROOMS-38. Create a fresh one in step 1: New chat ticking only `Ada` and `Bo` always creates another Room (INVITEROOMS-10).

**Steps**

1. In tab A (the current tab) click **New chat**, tick `Ada` and `Bo` and click **Start chat**. The new Room is named `Ada, Bo`. Then open a second browser tab on `http://localhost:5100`. Call the first tab A and the second tab B.
2. In tab A, stay on the new `Ada, Bo` Room. Write its room id down.
3. In tab B, navigate to the SAME `/rooms/<id>`.
4. In tab B, click **Invite teammate** in the Room header and leave the panel OPEN. Confirm `gamma` is listed as a candidate.
5. Arrange the two tabs (or two windows) so you can see tab B while acting in tab A. If you cannot, take a screenshot of tab B first and compare after.
6. Note the current last line in `T-A`.
7. In TAB A, click into the composer, type exactly `/invite @gamma`, and press Enter.
8. Switch to TAB B WITHOUT reloading it (do not press F5, do not re-navigate).
9. Read tab B's sidebar link for this Room.
10. Read tab B's header `<h1>` and its members line.
11. Read tab B's still-open candidate list.
12. Read every line `T-A` printed since your note, looking in particular for any line containing `RoomsChanged handler threw`.

**Pass if — all of these**

- Tab B's sidebar link renamed to include `gamma`, with no reload.
- Tab B's `<h1>` and members line both updated to include `gamma`, with no reload.
- `gamma` has VANISHED from tab B's still-open candidate list.
- `T-A` printed exactly ONE `Invited agent 'gamma' ...` line for the one invite.
- `T-A` printed NO line containing `A RoomsChanged handler threw and was skipped.`

**Fail if — any of these**

- Tab B is stale until F5 -> the fan-out is not reaching a second circuit; in practice one user's actions are invisible to another's open window.
- `T-A` shows repeated `A RoomsChanged handler threw and was skipped.` errors -> a component did not unsubscribe from the singleton `RoomEvents` hub in `Dispose`; `docs/engineering/rules.md` calls this out as 'a leaked component throws on disconnect and never dies'. The error count grows forever and every publish gets slower.
- Tab B throws an `An unhandled error has occurred.` bar after tab A navigates away -> the same leak, surfacing on the client.
- Tab B's `<h1>` updates but its candidate list still offers `gamma` -> `InviteTeammate` is subscribed for repaint but not reloading candidates; the next click in tab B is a silent no-op.

**Inconclusive if**

If tab B's SignalR circuit had already dropped (you will see a 'Attempting to reconnect' overlay or a frozen page), nothing it shows is evidence. Reload tab B, re-open the panel, and start the test again with a fresh `Ada, Bo` Room from New chat.

### INVITEROOMS-23 — Adding a third Member flips a Room from answer-everything to mention-gated

**Free** · about 8 min

*Proves the Reply Gate follows membership: in a group Room only the Mentioned Agent replies, and an unmentioned message draws no reply at all.*

**Before you start**

- A brand-new, untouched two-Member Direct Room. This test creates one on purpose so it does not depend on earlier state.

**Steps**

1. Open a NEW PowerShell terminal at `<repo>`. This is the `delta` terminal.
2. In the `delta` terminal run exactly: `pwsh tools/echo-bot.ps1 -Name delta`
3. Wait until a sidebar link reading `delta` appears, then click it. Write its room id down.
4. Confirm the members line reads `You, delta`.
5. Type exactly `hi @delta` and press Enter. Wait 3 seconds.
6. Read the Transcript.
7. Click **Invite teammate** in the Room header and click `Bo`.
8. Confirm the `<h1>` is now `delta, Bo` and the members line is `You, delta, Bo`.
9. Type exactly `hi` (no @ anywhere) and press Enter. Wait 10 seconds.
10. Read the Transcript.
11. Type exactly `hi @Bo` and press Enter. Wait 15 seconds for Bo's streamed reply to settle.
12. Read the Transcript.
13. Open `<data>\rooms\<roomId>.jsonl` and read the `senderName` of every line.

**Pass if — all of these**

- After `hi @delta` in the two-Member Room, exactly ONE reply appears, beginning with a bold `delta:` (the screen renders Markdown, so no asterisks show; only the `.jsonl` holds the literal `**delta:**`).
- After the plain `hi` in the three-Member Room, NO reply appears from either Agent within 10 seconds, and no Draft row from Bo appears either.
- After `hi @Bo`, exactly ONE reply appears, from Bo, and it contains the text `hi @Bo`. `delta` posts no reply to `hi @Bo`.
- In the `.jsonl`, every reply line's `senderName` is exactly the Agent that was Mentioned — there is never a line from an Agent that was not Mentioned in that message.
- The Transcript stops growing once each reply lands — it does not keep appending.

**Fail if — any of these**

- BOTH Agents reply to `hi @Bo` -> the per-Agent `mentioned` flag is not being carried in the fan-out; every Mention now wakes the whole Room and cost scales with membership.
- The Transcript keeps growing on its own after you stop typing -> a reply loop; an Agent is replying to its own or another Agent's Message. A failing version of this once recorded 4299 messages in two seconds. STOP the app immediately (Ctrl+C in `T-A`) and report it as the highest-severity finding in the area.
- A reply's `senderName` names an Agent that was not Mentioned -> the gate is passing the wrong flag to the wrong Agent.
- `hi @delta` in the two-Member Room draws NO reply -> the bot is not receiving the Mention at all; check the `mentioned` field in the envelope printed in the `delta` terminal before blaming the gate.

**Inconclusive if**

If neither `hi @delta` nor `hi @Bo` draws a reply, the Agents may be disconnected: check the `delta` terminal for `Server closed the connection.` and the Teammates page for Bo's status before recording anything. Bo's reply repeats the whole prompt, which can quote earlier Messages such as `hi @delta`; if `delta` answers only because its Name appears inside Bo's reply text, say so and record that part as inconclusive rather than as a Reply Gate defect.

> [!NOTE]
> IMPORTANT: `tools/echo-bot.ps1` and the mock Teammates (Ada and Bo) both implement ADR-0004 — the bot mirrors `ReplyGate.Decide` and the mock Teammates go through the real Reply Gate — so in a two-Member Room they reply to every Message, Mentioned or not, and only become Mention-gated once a third Member joins. That is exactly the boundary this test exercises: `hi @delta` works in the two-Member Room (step 5) because `delta` is Mentioned, and it would answer a plain `hi` there too, for the same reason `hi` draws nothing once `Bo` makes it a three-Member Room in step 9. This free test can therefore observe the 'answer everything when memberCount <= 2' rule directly, without needing a real Persona.

### INVITEROOMS-24 — Rooms, names and membership survive a restart; the Room Budget does not

**Free** · about 8 min

*Proves every rename and membership change was committed to team.db rather than held in memory, and confirms the in-memory Budget resetting is the expected outcome.*

**Before you start**

- Several Rooms exist, at least one created via New chat and at least one renamed by an invite. After INVITEROOMS-09 through 23 this is satisfied.

**Steps**

1. Write down, in order, EVERY sidebar link name you can see, top to bottom.
2. Click three different Rooms and for each write down the room id, the `<h1>`, the members line, and the first and last message in its Transcript (or 'empty').
3. If any Room shows a line reading `Paused — N of M agent replies since you last spoke.` or a box reading `Agents have sent N replies since you last spoke, and are paused.`, write down which Room and the numbers.
4. Press Ctrl+C in `T-A` and wait for the process to exit.
5. Press Ctrl+C in each of the bot terminals (`T-C`, `T-D`, and the `Emily Lee`, `gamma` and `delta` terminals) to stop the pipe bots.
6. In `T-A` run the app command of `P-LAUNCH-UAT` again, WITHOUT the seeder line — the seeder would wipe the folder you are trying to prove survives.
7. Wait for `Now listening on: http://localhost:5100` and for Ada and Bo to go Online, then reload the browser at `http://localhost:5100`.
8. Read the sidebar top to bottom and compare it to your list.
9. Open each of the three Rooms you recorded and compare the `<h1>`, the members line and the Transcript.
10. If you recorded a paused Room, open it and read the area just above the composer.

**Pass if — all of these**

- The sidebar lists exactly the same Room names, in exactly the same order, as before the restart. (The order is created-time, then id — it must be stable across runs.)
- Every renamed Room still carries its renamed name — no Room reverted to a single-Agent name.
- Each Room's members line is identical to what you recorded.
- Each Transcript is intact — same first and last message.
- Any Room that was paused on its Budget is no longer paused: the `Paused —` line and the `Agents have sent … and are paused.` box are both gone.
- The Rooms named `Ada` and `Bo` (if they are still two-Member Rooms) reappear as before — Ada and Bo re-register on startup and re-attach to their existing Rooms rather than minting new ones (`T-A` prints no `Created direct room` for them).

**Fail if — any of these**

- A Room is missing, or a renamed Room reverted to its old name -> the rename or the membership insert was never committed to `team.db`; every invite made in this session has been lost.
- The sidebar order changed between runs -> `GetRoomsAsync` is no longer `ORDER BY created, id`, so the user's Rooms shuffle on every restart.
- A Transcript is empty that was not empty -> the `.jsonl` write path is not durable.
- A duplicate `Ada` or `Bo` appears after restart whose Direct Room was still two-Member -> the re-attach lookup is failing on startup.

**Inconclusive if**

If Ada or Bo does not return to Online after the restart, its Rooms will still be listed but its dot will not be green — that does not affect this test's pass conditions, but note it and check `O-ADAPTERS`. If the app will not start because the port is in use, find and kill the orphaned process before judging.

> [!NOTE]
> The Budget NOT surviving is EXPECTED: the counter is in memory and per Room by design, so a restart un-pauses every Room and shows a fresh allowance over a Transcript that already spent one. Do NOT file it. Restart the pipe bots you need (`mybot`, `gamma`, `Emily Lee`, `delta`) before continuing to later tests.

### INVITEROOMS-25 — The Team filter narrows candidates, and an Agent with no Persona only ever shows under All teams

**Free** · about 9 min

*Proves the Team dropdown is populated from the Persona library, that filtering is pure display narrowing over already-loaded candidates, and that it never triggers a model probe or a node spawn. This dropdown (inside the Room header's Invite teammate panel) is a `MudSelect` labelled **Team** (`InviteTeammate.razor:35`), so its options are read by opening its popover, and it is only rendered while the Room has at least one candidate.*

**Before you start**

- `P-LAUNCH-UAT` is still running on `C:\seeds\huddle\uat\data` (no relaunch). The `coo` Teammate is created in step 3 on the free mock adapter, so it registers as an Agent and costs nothing.
- No Persona carries a Team — Ada, Bo and the Chief of Staff belong to no Team. That is the seeded state.
- Run `O-MOCK-ADAPTERS` in `T-B` and write the number down before step 2; it is the number a Team selection must not change.

**Steps**

1. Click the Room `Ada` (or any Room that still has a candidate), click **Invite teammate**, and open the **Team** select. Confirm its only option is `All teams`. This is the no-Persona baseline. Close the popover.
2. Run `O-MOCK-ADAPTERS` in `T-B` and write the count down.
3. Create the Teammate through `P-NEW-PERSONA`: **Name** = `coo`, **Title** = `Chief of Staff`, **Alias** = `coo`, **Teams** = `Business`, body = `You are the Chief of Staff.`; leave **Model** and **Effort** at whatever the mock adapter offers (it spends nothing). Wait until the `coo` tile reads Online and a sidebar link reading `coo` appears.
4. Run `O-MOCK-ADAPTERS` again and write the new count down (one more than step 2). This is the count to hold steady below.
5. Click the Room `Ada` and click **Invite teammate**.
6. Open the **Team** select and read every option.
7. Select `Business`.
8. Read the candidate list.
9. Select `All teams` again.
10. Read the candidate list.
11. Click the `coo` Room, click **Invite teammate**, open the **Team** select and select `Business`. Read the panel.
12. Run `O-MOCK-ADAPTERS` a last time.

**Pass if — all of these**

- Before `coo` existed, the **Team** select offered only `All teams`.
- After `coo` exists, the select offers `All teams` AND `Business`.
- In the `Ada` Room, selecting `Business` narrows the candidate list to `coo` alone: `Bo`, the Chief of Staff and the pipe clients `mybot`, `gamma` and `delta` (raw clients with no Persona, therefore no Teams) all disappear.
- Selecting `All teams` again brings the full candidate list back, unchanged.
- In the `coo` Room (where `coo` is already a Member, so no candidate is in `Business`), selecting `Business` shows the sentence `No agents in this team.` (`InviteTeammate.razor:47`) and the **Team** select stays on screen.
- The `O-MOCK-ADAPTERS` count in step 12 equals step 4 — selecting a Team spawned nothing.

**Fail if — any of these**

- Selecting `Business` in the `coo` Room blanks the whole panel with no sentence, or throws an `An unhandled error has occurred.` bar -> the filter is not degrading to the empty-state message and reads as a broken panel.
- `Bo` or any other Agent without that Team (a raw pipe client, say) is still listed under `Business` -> the Persona-to-Agent match by Name is broken, so the filter is meaningless and a user would invite the wrong teammate.
- The `O-MOCK-ADAPTERS` count rises when you change the select -> the filter is reaching the Persona start path; it must be pure display narrowing over already-loaded candidates, and a spawn here means selecting a dropdown can cost money.
- `Business` never appears in the dropdown after `coo` was created -> the Persona library did not pick the new Teammate up, or the **Teams** field was not saved.

**Inconclusive if**

If `coo` does not reach Online, or `Business` does not appear, check the `coo` card for its reason text and `Files that didn't load` on the Teammates page; if the Teammate never registered, the test cannot judge the filter — say so. Do not write the file by hand to work around it: that skips the stored Model and Effort rows (`P-NEW-PERSONA`).

> [!NOTE]
> An Agent with no Persona behind it has no Teams and therefore vanishes under any real Team filter, reachable only under `All teams`. That behaviour is by design, not a defect; it is not recorded in known-limits.md. Afterwards open the `coo` card on **Teammates**, click its Remove icon (`aria-label="Remove"`, `TeammateCard.razor:220`) and then **Confirm**, so INVITEROOMS-32 onward start without it. Its Direct Room may remain in the sidebar; leave it.

### INVITEROOMS-26 — /invite accepts a Persona's Alias wherever it accepts its Name

**Free** · about 12 min

*Proves the alias fallback in InviteAsync, which is only reached AFTER the direct Name lookup misses — a branch that is easy to regress without any other symptom. The New teammate card used to create `Jarvis` is a real `MudDialog` (Stage 4 of the MudBlazor migration), and its Model and Effort controls are `MudSelect` — "set" a select by opening it and clicking the option, as with any other MudSelect on this card.*

**Before you start**

- `node` must be on PATH — run `node --version` and confirm it answers. Without it no Persona can start and this test cannot run.
- This test requires `Team:Acp:Enabled=true` with the REAL adapter, which spawns one `node` adapter per Persona. Starting a Persona opens a session and never prompts a model, so NO tokens are spent — but it is heavier setup than every test above.
- INVITEROOMS-26 to 31 leave the UAT data folder: a Haiku Persona needs the real adapter rather than the mock one, so they run on the default data folder (`src\Huddle.App\App_Data`), and Ada and Bo are not present there. Pipe bots stand in as the other Members. Start `pwsh tools/echo-bot.ps1 -Name mybot` in `T-C` and `pwsh tools/echo-bot.ps1 -Name gamma` in the `gamma` terminal once the app is up, and wait for a sidebar link for each.
- COST: the default data folder also holds the built-in Chief of Staff, which has no stored Model, and with ACP on and an empty Direct Room it takes a Greeting Turn that is paid on the real adapter (`PersonaRunner.cs:481-495`). Before relaunching in step 2, make it cheap as `P-LAUNCH-PAID` describes (Edit on its card, Model Haiku, Effort `low`, Save), or accept one short unplanned Turn.

**Steps**

1. Press Ctrl+C in `T-A`, and in `T-C`, the `gamma` terminal and every other bot terminal still running.
2. In `T-A` run `$env:Team__Acp__Enabled = 'true'` then `dotnet run --project src/Huddle.App --urls http://localhost:5100` (no `--Team:DataDir`, so the default data folder is used). Then start `mybot` in `T-C` and `gamma` in the `gamma` terminal as the precondition says.
3. Reload the browser and click **Teammates** in the sidebar.
4. Click **New teammate**.
5. In the card, fill **Name** with exactly `Jarvis`.
6. Fill **Title** with exactly `Butler`.
7. Fill **Alias** with exactly `jar`.
8. Leave **Teams** empty.
9. In the persona body textarea type exactly: `You are Jarvis. Answer in one short sentence.`
10. Set the **Model** select to `Haiku`.
11. Set the **Effort** select to `low`.
12. Click the **Add teammate** submit button AT THE BOTTOM OF THIS CARD (this is the New teammate dialog's submit action, NOT the Room header's Invite teammate control — see INVITEROOMS-11's note on telling the two apart).
13. Wait until the `Jarvis` tile shows a status of Online or Starting, and until a sidebar link reading `Jarvis` appears. This may take 10-30 seconds.
14. Click a Room that does NOT contain `Jarvis` — use the Room named `mybot`.
15. Note the current last line in `T-A`.
16. Type exactly `/invite @jar` and press Enter.
17. Read the coloured line above the composer and copy its exact text.
18. Read the header `<h1>` and the sidebar link.
19. Click a DIFFERENT Room that does not contain `Jarvis` — use the Room named `gamma`.
20. Type exactly `/invite @JAR` (upper case) and press Enter.
21. Read the coloured line and the `<h1>`.
22. Read every line `T-A` printed since your note.

**Pass if — all of these**

- Both commands produce a GREEN info line (class `composer-info`), not a red one.
- The Room name in BOTH cases resolves to the owning Name: the `<h1>` and the sidebar link end in `Jarvis`, never `jar` and never `JAR`.
- The info line's first half repeats the handle you typed (`Invited jar. …` and `Invited JAR. …`) while its second half quotes the real Room name containing `Jarvis`. BOTH halves must be as described — repeating your typed handle is correct and is not a defect.
- `T-A` printed `Invited agent 'Jarvis' (<id>) into room '<roomId>'.` — naming `Jarvis`, not the alias — for each of the two invites.
- The two renamed Rooms read `mybot, Jarvis` and `gamma, Jarvis`.
- Optional DB check: `sqlite3 src\Huddle.App\App_Data\team.db "SELECT name FROM rooms;"` shows `Jarvis` in both renamed Rooms and never `jar`.

**Fail if — any of these**

- A RED line reads `Unknown agent @jar` -> the alias fallback regressed. It is only reached after the direct Name lookup misses, so this can break with no other visible symptom anywhere in the app.
- `/invite @jar` works but `/invite @JAR` fails -> the alias comparison lost its case-insensitivity.
- The Room renames to `jar` rather than `Jarvis` -> the alias is being persisted as an identity; the Room name no longer matches the Teammate the user sees on `/teammates`, and Mention resolution will disagree with it.
- No `Jarvis` sidebar link ever appears -> the Persona never registered as an Agent, and the alias cannot be resolved against the Team Directory at all. That is a start-up failure, not an alias failure.

**Inconclusive if**

If `node --version` fails, or the `Jarvis` tile stays Offline with a reason in the Room's member-health strip, the Persona never registered and this test cannot be judged — report it inconclusive with the reason text from the tile. A HAND-WRITTEN Persona file is NOT enough for this test: an Alias resolves against the Team Directory, which only has a row once the Persona has actually registered.

> [!NOTE]
> No model turn is taken for `Jarvis` here — creating and starting a Persona opens a session and never prompts — so the test itself spends no tokens despite needing Acp enabled; only the Chief of Staff's Greeting Turn (see the COST line above) can. Keep `Jarvis` running: INVITEROOMS-27 through 31 all use it.

### INVITEROOMS-27 — The Teammate card's Message action disappears once that Persona's Direct Room has been invited into

**Free** · about 10 min

*Proves the card resolves its Message link through the exact-two-Member lookup, so the link is ABSENT rather than pointing at a Room that is no longer a one-to-one conversation.*

**Before you start**

- INVITEROOMS-26 passed. `Team:Acp:Enabled=true`, and the Persona `Jarvis` is online with its own Direct Room named `Jarvis` in the sidebar.
- `Jarvis`'s Direct Room must still be a two-Member Room — its sidebar entry must read exactly `Jarvis` with no comma.

**Steps**

1. Click **Teammates** in the sidebar.
2. Click the `Jarvis` tile to open its card.
3. Read the row of actions near the bottom of the card and write down every action label you see.
4. Close the card (click the × at its top right).
5. Click the sidebar link reading exactly `Jarvis` and confirm its members line reads `You, Jarvis`.
6. Click **Invite teammate** in the Room header and click `mybot` (pipe bot `mybot` from `T-C` must be running).
7. Confirm the `<h1>` is now `Jarvis, mybot`.
8. Click **Teammates** in the sidebar again.
9. Click the `Jarvis` tile.
10. Read the row of actions again and compare.

**Pass if — all of these**

- Before the invite, the card's actions include **Message**, **Edit** and **Open**.
- After the invite, **Message** is GONE. **Edit** and **Open** are still present.
- The card does not error and shows no broken or dead link where Message used to be.

**Fail if — any of these**

- **Message** is still shown and clicking it 404s or lands on the empty state -> the card is holding a stale room id; the user clicks a link that appears to work and reaches nothing.
- **Message** is still shown and points at the now-THREE-member `Jarvis, mybot` Room -> the exact-two-Member lookup regressed; the card is presenting a group Room as a one-to-one conversation, which is exactly the confusion the disappearing link prevents.
- **Edit** or **Open** also disappeared -> the whole action row is being suppressed rather than just the one conditional action.
- The card fails to open at all after the invite -> the room lookup is throwing rather than returning null.

**Inconclusive if**

If `Jarvis`'s sidebar entry already contained a comma before you started, its Direct Room was already converted and the 'before' half cannot be observed. Restart `Jarvis` (click **Restart** on its card, which is offered in every state except Starting, `TeammateCard.razor:182-185`, or edit and save its file) to mint a fresh Direct Room, then start the test over. If the tile shows no actions at all, the card may not have finished loading — reopen it once before judging.

> [!NOTE]
> The disappearance is the DESIGNED consequence of membership-defined Direct Rooms and the source comment says the action is 'absent rather than broken'. A tester is quite likely to file this as a bug. Do NOT. Restarting the Persona brings the link back, pointing at a newly minted Direct Room — optionally confirm that as a bonus observation.

### INVITEROOMS-28 — An Agent creates a Room with mcp__team__create_room and it appears live in the sidebar

**💰 Spends money** · about 15 min

*Proves the Agent-facing create_room tool is reachable by its full name, creates a real Room named after its Agent Members, publishes RoomsChanged, and always seeds the Human.*

**Before you start**

- `Team:Acp:Enabled=true` and node on PATH.
- TWO Personas online, both Model = Haiku and Effort = low. `Jarvis` from INVITEROOMS-26, plus a second one you create the same way.
- COST: roughly two to four short Haiku Turns. Set a low Room Budget first (step 1) so a misbehaving model cannot run away.

**Steps**

1. Press Ctrl+C in `T-A`. Run `$env:Team__Acp__Enabled = 'true'` and `$env:Team__AgentMessageBudget = '6'`, then `dotnet run --project src/Huddle.App --urls http://localhost:5100`. The budget cap is what bounds the spend of this test.
2. Reload the browser and click **Teammates**.
3. Click **New teammate**. Fill **Name** = `Friday`, **Title** = `Analyst`, **Alias** = `fri`, **Teams** empty, body = `You are Friday. Answer in one short sentence.`, **Model** = `Haiku`, **Effort** = `low`. Click the card's **Add teammate** submit button.
4. Wait until both `Jarvis` and `Friday` tiles read Online and both appear in the sidebar.
5. Click **New chat**, tick `Jarvis`, `Friday` and `mybot`, and click **Start chat**. You need three or more Members so Mentions actually gate.
6. Note the current last line in `T-A`.
7. Count the sidebar links and write the count down.
8. In the composer type exactly: `@Jarvis start a separate room with Friday and give it the context`
9. Press Enter.
10. Watch the Transcript: you should see a streaming Draft from `Jarvis`, then a Message.
11. Watch the sidebar for up to 60 seconds WITHOUT reloading the page.
12. Read every line `T-A` printed since your note.
13. Click the new sidebar link and read its `<h1>`, its members line, and its Transcript.

**Pass if — all of these**

- A NEW sidebar link appears with no page reload, named after its Agent Members (e.g. `Jarvis, Friday`).
- `T-A` printed `Created room '<id>' (<name>) with <n> members.`
- The new Room opens, and its members line BEGINS with `You` — the Human is a Member of it.
- You can read the whole exchange in it as it happens: Drafts stream, then Messages land.
- `Jarvis` did not report that it could not find the tool — it took a Turn and the Room appeared.
- Optional DB check: `sqlite3 src\Huddle.App\App_Data\team.db "SELECT 1 FROM room_members WHERE room_id='<new id>' AND user_id='human';"` returns a row.

**Fail if — any of these**

- `Jarvis` replies that no such tool exists -> the system prompt is no longer spelling `mcp__team__create_room` in full; a bare tool name produces exactly this, and the Agent is then permanently unable to create Rooms.
- A Room is created whose members line omits `You` -> the Human is not being seeded by `CreateRoomForAsync`; that Room is unpostable by the user (the composer will report NotMember) and the ADR-0005 structural-auditability property is broken. This is the most serious possible failure here.
- `T-A` logs `Created room` but no sidebar link appears until F5 -> `RoomsChanged` is not published from the tool path, even though it is from the UI path.
- The Transcript keeps growing after `Jarvis` finishes -> an agent loop; the budget cap of 6 should stop it, but if it does not, press Ctrl+C in `T-A` immediately and report it as the highest-severity finding.
- `Jarvis` names an Agent that does not exist and the tool answers `Unknown agent(s): X. Agents that do exist: …` but `Jarvis` does NOT self-correct within the same Turn -> report the transcript; the error text is correct, the model's handling of it is the finding.

**Inconclusive if**

If `Jarvis` simply answers in prose without calling any tool, that is a model-behaviour outcome, not a product failure — retry ONCE with a more explicit prompt (`@Jarvis use your create_room tool to make a room with Friday`). If it still does not call the tool after two attempts, report INCONCLUSIVE and say how many Turns were spent. Do not spend more than four Turns on this test. If a budget pause box appears reading `Agents have sent N replies since you last spoke, and are paused.`, that is the cap working — read the Transcript, then click **Continue** only if you still need more Turns and are willing to spend them.

> [!NOTE]
> `seed` shipped with roadmap item 8: `create_room` now takes an optional `seed` argument, the opening Message posted into the new Room as part of creating it (see APPTOOLS-26). A Room created WITHOUT `seed` still legitimately arrives with no opening Message — that half of ADR-0005's original gap stands, because `seed` is optional and nothing forces a caller to pass it. But a `create_room` call that DID pass `seed` and produced a Room with no opening Message is no longer a documented gap — it is a `create_room` defect: the seed post silently failed. Tell the two apart by what the model actually passed: if step 8's call named no `seed` argument, an empty Room is correct; if it did, check Jarvis's exact reply text for `Created room '<name>' (id <id>), but could not post the seed message: <reason>`, which is the tool's own report of that failure. An Agent naming ONLY itself gets its own existing Direct Room id back and no new Room appears — also correct, and unaffected by `seed`.

### INVITEROOMS-29 — An Agent invites another with mcp__team__invite_agent, using the room id from its own [Room: …] label

**💰 Spends money** · about 15 min

*Proves the Agent can learn a room id only from the Room label prefix on the Messages it receives, and that the tool renames and republishes on success. Targets the area's documented silent failure: a wrong room id fails with no on-screen signal at all.*

**Before you start**

- INVITEROOMS-28 passed: `Team:Acp:Enabled=true`, `Team:AgentMessageBudget=6`, and `Jarvis` and `Friday` are both online (Haiku / low).
- A Room with three or more Members that `Friday` is NOT in. Create one via New chat ticking `Jarvis`, `mybot` and `gamma` (pipe bots from `T-C` and the `gamma` terminal) if you do not have one.
- COST: roughly two to four short Haiku Turns.

**Steps**

1. Click the Room with three or more Members that excludes `Friday`. Write down its room id, its `<h1>` and its members line.
2. Note the current last line in `T-A`.
3. In the composer type exactly: `@Jarvis bring Friday into this room`
4. Press Enter.
5. Watch the Transcript for a Draft from `Jarvis`, then a Message.
6. Watch the header `<h1>`, the members line and the sidebar for up to 60 seconds WITHOUT reloading.
7. Read every line `T-A` printed since your note, looking specifically for a line beginning `Invited agent`.
8. Once the rename has happened, type exactly `@Friday say hello` and press Enter.
9. Wait for a reply.

**Pass if — all of these**

- The Room's `<h1>` and sidebar link both rename to include `Friday`, with no page reload.
- The members line gains `Friday` and still begins with `You`.
- `T-A` printed `Invited agent 'Friday' (<id>) into room '<roomId>'.` naming the room id you wrote down.
- `@Friday say hello` draws a reply from `Friday` — it is genuinely a Member and receives Messages.
- `Jarvis` reported success back into the Transcript in words consistent with the tool's own result text (`Invited Friday into room '<name>' (id <id>). It now has N members.`).

**Fail if — any of these**

- NOTHING renames, NOTHING errors on screen, and `T-A` has NO `Invited agent` line -> THE documented silent failure: the model passed the WRONG room id. The tool result is text the model reads; it never surfaces to the user. The absence of the log line is the only signal, which is exactly why this test reads the log rather than the screen. Report the model's own reply text alongside it.
- `Jarvis` says it does not know the room id, or asks you for one -> the Room label prompt has lost its `{{roomId}}` placeholder. `docs/engineering/rules.md` states the Room label is the ONLY place an Agent can learn a room id; without it this fails silently and permanently. Check Settings -> Prompts for the Room-label prompt text and confirm it still contains `{{roomId}}`.
- The WRONG Room renames -> the tool acted on a room id belonging to a different Room; check which Room changed and report both ids.
- The Room renames but `@Friday say hello` draws no reply -> the member row was added without the Agent being wired into the fan-out.

**Inconclusive if**

If `Jarvis` answers in prose without calling the tool, retry ONCE with `@Jarvis use your invite_agent tool to add Friday to this room`. If it still does not, report INCONCLUSIVE with the Turn count. Do not exceed four Turns. If a budget pause appears, that is the cap working.

> [!NOTE]
> If `Friday` is ALREADY a Member, the tool answers `Friday is already a member of room '<id>'. Nothing to do.` and does NOT rename and does NOT publish RoomsChanged. That is DIFFERENT from the Human's `/invite` path (INVITEROOMS-17, which renames unconditionally) and is CORRECT — do not file the difference.

### INVITEROOMS-30 — An Agent can invite into a Room it is not a Member of

**💰 Spends money** · about 10 min

*Confirms the documented known limit holds in its benign form — the target Room renames and gains the Member while the caller stays outside — and that it does not crash or rename the wrong Room.*

**Before you start**

- `Team:Acp:Enabled=true`, `Team:AgentMessageBudget=6`, `Jarvis` online (Haiku / low).
- A second Room that `Jarvis` is NOT a Member of. Create one via New chat ticking `mybot` and `gamma` only, and write down its room id and its `<h1>`.
- COST: roughly one short Haiku Turn.

**Steps**

1. Create the target Room if you do not have one: click **New chat**, tick `mybot` and `gamma` only, click **Start chat**. Write down its `/rooms/<id>` and its `<h1>` — call it TARGET.
2. Navigate to a DIFFERENT Room that `Jarvis` IS a Member of.
3. Note the current last line in `T-A`.
4. In the composer type exactly, substituting the real id: `@Jarvis invite Friday into the room whose id is <TARGET id>`
5. Press Enter and wait for `Jarvis` to take its Turn.
6. Watch the sidebar for up to 60 seconds WITHOUT reloading.
7. Read every line `T-A` printed since your note.
8. Click TARGET and read its `<h1>` and its members line.
9. Read the members line of the Room you were typing in.

**Pass if — all of these**

- TARGET's sidebar link and `<h1>` rename to include `Friday`, live, with no reload.
- TARGET's members line gains `Friday` and still begins with `You`.
- `Jarvis` is NOT in TARGET's members line — the caller did not add itself.
- The Room you were typing in is unchanged.
- `T-A` printed `Invited agent 'Friday' (<id>) into room '<TARGET id>'.`
- Optional DB check: TARGET's `room_members` gained `Friday` and did NOT gain `Jarvis`.

**Fail if — any of these**

- An `An unhandled error has occurred.` bar appears, or `T-A` shows an unhandled exception -> the cross-Room invite path is throwing rather than being permitted.
- A DIFFERENT Room renames -> the tool acted on the wrong room id; report which Room changed.
- `Jarvis` is added to TARGET as a side effect -> the tool is adding the caller as well as the invitee; membership is no longer what the Human asked for.
- TARGET renames but the sidebar does not follow until F5 -> RoomsChanged is not published for a Room the current view is not showing.

**Inconclusive if**

If `Jarvis` refuses on the grounds that it is not in that Room, that is model judgement, not a product behaviour — report INCONCLUSIVE and say so; do not retry more than once. If it asks which room id you mean, you probably pasted a truncated id; re-read it from the address bar and retry once.

> [!NOTE]
> This is a DOCUMENTED known limit, recorded in `docs/engineering/known-limits.md` as 'An Agent can invite into any Room whose id it holds'. InviteAsync checks that the Room and the Agent exist, never who is asking. It is accepted while every Room contains the one Human. DO NOT FILE IT. The genuine failures are the crash and the wrong-Room cases above.

### INVITEROOMS-31 — Every Agent-created Room contains the Human, so none is hidden

**💰 Spends money** · about 6 min

*Proves ADR-0005's structural-auditability property on the Agent-driven path specifically: no Room the Agents make is one the Human cannot read and post into.*

**Before you start**

- INVITEROOMS-28 passed and at least one Agent-created Room exists in the sidebar.
- COST: rides along with INVITEROOMS-28 and 30 — no extra model Turns are needed unless you choose to post in the new Room.

**Steps**

1. Identify every Room in the sidebar that was created by an Agent tool during INVITEROOMS-28 or 30 (`T-A`'s `Created room '<id>' ...` lines list their ids).
2. Open each of them in turn.
3. For each, read the grey members line beneath the `<h1>`.
4. For each, confirm the Transcript is readable — you can see the Messages the Agents exchanged.
5. In one of them, type exactly `hello` and press Enter.
6. Read the area above the composer.

**Pass if — all of these**

- Every Agent-created Room's members line BEGINS with `You`.
- Every one of them opens and its whole Transcript is readable.
- Typing `hello` posts a normal message — no error line appears above the composer.
- Optional DB check, per Room: `sqlite3 src\Huddle.App\App_Data\team.db "SELECT 1 FROM room_members WHERE room_id='<id>' AND user_id='human';"` returns a row.

**Fail if — any of these**

- A Room's members line omits `You` -> `CreateRoomForAsync` is no longer seeding `KnownIds.Human`. The Room still appears in the sidebar (it is seeded from all Rooms, not from the Human's membership), so the symptom is a Room you can SEE but cannot POST into — the composer will report NotMember. This is the exact failure ADR-0005's 'the Human is a Member of every Room' exists to prevent.
- `T-A` logged `Created room` for an id that has NO sidebar link at all -> a Room exists in the data that the Human cannot reach through the UI; Agents would then be able to converse entirely out of sight. Report immediately.
- Typing `hello` produces a red error above the composer -> confirm the members line; a NotMember error with `You` missing from the line is the same defect seen from the other side.

**Inconclusive if**

If no Agent-created Room exists (INVITEROOMS-28 was inconclusive), this test has nothing to judge — mark it inconclusive and say it is blocked on INVITEROOMS-28. Do NOT substitute a New-chat Room; that path is already covered by INVITEROOMS-09 and proves something different.

> [!NOTE]
> After INVITEROOMS-31, leave the paid lane: Ctrl+C in `T-A`, `Remove-Item Env:\Team__AgentMessageBudget` and `Remove-Item Env:\Team__Acp__Enabled`, run `O-ADAPTERS` expecting `0` once the app has stopped, then relaunch the app command of `P-LAUNCH-UAT` WITHOUT the seeder line, so INVITEROOMS-32 onward find the Rooms from INVITEROOMS-09 to 13 in `C:\seeds\huddle\uat\data` and no later session spawns node adapters by accident. Restart the pipe bots you need (`mybot`, `scratch15` as the test says).

### INVITEROOMS-32 — The composer's own status line does not survive a Room switch

**Free** · about 5 min

*A FREE test appended here rather than beside `INVITEROOMS-14` — test ids are append-only and never renumbered once assigned, since the Tracker and filed issues cite them by number. It belongs conceptually right next to `INVITEROOMS-14` (see that test's own forward-pointer note), and proves the counterpart INVITEROOMS-14 does not cover: that test proves the invite PANEL's own state resets on a Room switch; this one proves the COMPOSER's status line — the coloured confirmation or error line directly above the message box — does too. Before the fix, `Composer.razor` cleared `errorText`/`infoText` only at the top of `SendAsync`, with no `OnParametersSetAsync` guard, so a confirmation or an error typed in one Room stayed on screen in every Room visited afterward.*

**Before you start**

- `P-LAUNCH-UAT` is running on the same data folder as INVITEROOMS-02 to 25, with Ada and Bo Online.
- At least two Rooms exist: the converted `mybot` Room from INVITEROOMS-12/13, whose name begins `mybot, Bo` (Room A) and one of the `Ada, Bo` Rooms from INVITEROOMS-09/10 (Room B).

**Steps**

1. In `T-D` run exactly: `pwsh tools/echo-bot.ps1 -Name scratch15`
2. Wait until a sidebar link reading `scratch15` appears.
3. Click the sidebar link whose name begins `mybot, Bo` (Room A — INVITEROOMS-13 may have appended more Members to its name).
4. Click into the composer and type exactly: `/invite @scratch15`
5. Press Enter.
6. Read the coloured line directly above the composer, and copy its exact text.
7. Click a DIFFERENT sidebar link — one of the `Ada, Bo` Rooms (Room B).
8. Without typing anything, look directly above Room B's composer.
9. In DevTools Elements confirm no element with class `composer-info` or `composer-error` is present above Room B's composer.
10. Click into Room B's composer and type exactly: `/invite @nobody`
11. Press Enter.
12. Read the coloured line above Room B's composer.
13. Click back to Room A and look directly above its composer, without typing anything.
14. In `T-D` press Ctrl+C to stop `scratch15`.

**Pass if — all of these**

- Step 6 shows a GREEN info line (class `composer-info`) reading exactly `Invited scratch15. Room is now "<Room A's current name>, scratch15".`, where Room A's current name begins `mybot, Bo`.
- On arriving in Room B (steps 7-9), NOTHING is shown above the composer — no leftover green line from Room A, and no `composer-info`/`composer-error` element anywhere in the DOM.
- Step 12 shows a RED line reading exactly `Unknown agent @nobody` — Room B's own error, not Room A's leftover confirmation.
- Returning to Room A (step 13) shows nothing above its composer — Room A's own confirmation is gone, cleared by the Room switch that carried it away from Room B, not merely by the next Send.

**Fail if — any of these**

- Room B's composer shows Room A's `Invited scratch15. Room is now "..."` line -> the same stale-parameter bug `OnParametersSetAsync`'s guard exists to prevent on `InviteTeammate.razor` (INVITEROOMS-14), now reproduced on `Composer.razor`; the user is told an action happened in a Room where it did not.
- Room B's red `Unknown agent @nobody` line follows you back to Room A -> the same defect in the other direction — an error that belongs to Room B reads as Room A's own failure.
- The status line only clears once Room B's composer is used, not the moment Room B is opened -> the guard is reacting to a Send rather than to `RoomId` changing; the fix belongs in `OnParametersSetAsync`, not `SendAsync`.

**Inconclusive if**

If `scratch15` fails to register within 5 seconds, read `T-D` for a line containing `"type":"error"` and fix that before judging — do not substitute an Agent already offered in Room A, since a failed invite (already-a-Member) produces the SAME wording either way and would not prove the state actually reset. If `pwsh` is not recognised, use `powershell.exe -File tools\echo-bot.ps1 -Name scratch15` and note the substitution in your report.

> [!NOTE]
> This test and INVITEROOMS-14 are two halves of one bug report: the same stale Room-switch failure, on two different components that both render around the composer.

---

### INVITEROOMS-33 — A renamed Room keeps its name across an Invitation; an un-renamed one still re-derives

**Free** · about 8 min

*The whole point of the rename. Rooms acquire their duplicate names through the invite path, so a rename that an Invitation overwrites would not fix anything — see [product observation 6](../product-observations.md), filed as #43. Both halves must be checked: the sticking AND the still-working auto-rename.*

**Before you start**

- `P-LAUNCH-UAT` is running, Ada and Bo are Online, and Ada's own Room is still the two-Member Room named `Ada` (never invited into, never renamed).
- You need a SECOND Room containing `Ada` but not `Bo`: start a pipe bot with `pwsh tools/echo-bot.ps1 -Name scratch33` in `T-D`, then use `New chat` ticking `Ada` and `scratch33`. That creates a Room named `Ada, scratch33`. Stop `scratch33` when the test is done.

**Steps**

1. Open the Room named `Ada`. Note the Edit icon beside the title (`aria-label="Rename room"`).
2. Click it. Note whether the title becomes an input seeded with the current name, and whether focus lands in it.
3. Type `Pricing` and press Enter. Note the header AND the sidebar entry, without refreshing.
4. In the composer type `/invite @Bo` and press Enter. Note the Room's name in the header and the sidebar.
5. Open the OTHER Room (`Ada, scratch33`, the one you have not renamed). Note its name.
6. In it, type `/invite @Bo` and press Enter. Note its name now.
7. Return to `Pricing`. Click the Edit icon, type `Renamed again`, and press **Escape**. Note the name.
8. Click the Edit icon, clear the field completely, and press Enter. Note what appears.
9. Reload the page. Note the name of both Rooms.

**Pass if — all of these**

- Step 3: both the header and the sidebar read `Pricing` immediately, with no refresh.
- Step 4: the Room is STILL `Pricing`. `Bo` is a Member (the members line under the title lists it).
- Step 6: the un-renamed Room DID re-derive — its name is now its Agent Members joined by `", "`, for example `Ada, scratch33, Bo`.
- Step 7: Escape left the name as `Pricing`.
- Step 8: a visible error appears and the name is unchanged.
- Step 9: both names survived the reload.

**Fail if — any of these**

- Step 4 renamed the Room -> the stickiness comparison is not working; a chosen name will not survive `mcp__team__invite_agent` either. This is the defect this test exists for.
- Step 6 did NOT rename -> auto-naming has been switched off wholesale rather than made conditional. Every Room will now keep whatever name it was created with. Defect.
- Step 3 updated the header but not the sidebar (or the reverse) -> the rename is not publishing `RoomsChanged`, or something is not subscribed to it. Defect.
- Step 8 saved a blank name -> the guard is missing and the sidebar now has an unclickable empty entry. Defect.

**Inconclusive if**

`Bo` is already a Member of the Room you picked, so the invite is a no-op and proves nothing. Pick a Room it is not in, or connect a fresh pipe client with `P-ECHO-BOT` under a new Name and invite that instead.

> [!NOTE]
> There is one edge with no observable symptom, and it is deliberate, not a defect: rename a Room to EXACTLY the name auto-naming would have produced (`Ada` for a Room whose only Agent is `Ada`) and it counts as still auto-named, so the next Invitation re-derives over it. Do not file that. See [Known limits](../known-limits.md).

### INVITEROOMS-34 — Archiving a Room from its row menu removes it from the sidebar and lists it under Archived chats

**Free** · about 5 min

*Archive is the whole point of ADR-0018 and its storage is a sibling table, so the one thing worth proving by hand is that the Room leaves the sidebar without a reload and is still findable. Catches an archive that writes nothing, and an archive that needs F5.*

**Before you start**

- At least two Rooms are in the sidebar. Note their names.

**Steps**

1. Hover the first Room's sidebar row. Note whether a `⋮` button appears at its right-hand end.
2. Press Tab until focus reaches that `⋮` button WITHOUT using the mouse, and confirm it is visible while focused.
3. Right-click the row.
4. Click **Archive**.
5. Watch the sidebar for up to 3 seconds WITHOUT reloading.
6. Click the **Archived chats** icon (`aria-label="Archived chats"`, `ArchivedChats.razor:14`), directly above the Settings button at the bottom-left.

**Pass if — all of these**

- The `⋮` button appears on hover and is reachable by Tab, and is visible when focused.
- Right-clicking opens a menu offering exactly **Archive** and **Delete**, and no browser context menu appears.
- The Room disappears from the sidebar WITHOUT a page reload.
- The Archived chats dialog lists that Room, with an **Unarchive** button beside it.
- The other Room is untouched and still visible.

**Fail if — any of these**

- The Room only disappears after F5 -> `RoomsChanged` is not reaching `RoomList`.
- The browser's own context menu appears -> `@oncontextmenu:preventDefault` is not applied.
- The `⋮` is unreachable by keyboard -> it is being hidden with `display:none` rather than `opacity`, which takes it out of the Tab order.
- The Room vanishes from the sidebar but the Archived chats dialog is empty -> it is being hidden without the `archived_rooms` row being written, and the Room is now unreachable.

**Inconclusive if**

If the app was started on a freshly seeded folder and only one Room is visible, archiving it leaves nothing to compare against; create a second Room first.

### INVITEROOMS-35 — Unarchiving returns the Room to the sidebar with its Transcript intact

**Free** · about 4 min

*Archive is meant to be reversible and non-destructive. This is the only check that the Transcript was never touched — archive writes to SQLite and must not go near the JSONL file.*

**Before you start**

- INVITEROOMS-34 ran, so one Room is archived. It had at least one Message in it before archiving.

**Steps**

1. Open **Archived chats**.
2. Click **Unarchive** beside the Room.
3. Watch the sidebar WITHOUT reloading.
4. Click the Room and read its Transcript.

**Pass if — all of these**

- The Room reappears in the sidebar immediately, in `created, id` order — not necessarily at the bottom.
- It leaves the Archived chats list, which either empties or shrinks by one.
- Its Transcript holds exactly what it held before archiving, in the same order.

**Fail if — any of these**

- The Transcript is empty or short -> archiving deleted or truncated the JSONL; archive must not touch `FileChatStore` at all.
- The Room reappears only after F5 -> the dialog is not observing `RoomsChanged`.
- The Room appears twice -> the sidebar is appending rather than re-reading.

### INVITEROOMS-36 — Deleting a Room needs a confirmation and removes its Transcript file from disk

**Free** · about 6 min

*Delete is irreversible and spans two stores — `team.db` and `{DataDir}/rooms/{id}.jsonl`. A delete that forgets the file leaves an orphan Transcript no UI can reach. This is the only test that looks at the disk.*

**Before you start**

- A Room with at least two Messages in it, whose room id you have written down (call it DOOMED). Do not use the `Bo` Room: INVITEROOMS-38 needs it.
- A file explorer or terminal open at `<data>\rooms\`.

**Steps**

1. Confirm `<data>\rooms\DOOMED.jsonl` exists and is non-empty.
2. Right-click the Room's sidebar row and click **Delete**.
3. Read the row WITHOUT clicking anything else.
4. Click **Cancel**.
5. Right-click the row, click **Delete** again, then click **Confirm**.
6. Look at `<data>\rooms\` again.

**Pass if — all of these**

- Step 3 shows the row swapped in place to `Delete "<name>"? [Confirm] [Cancel]`, naming the Room. No dialog opens, and no browser `confirm()` box appears.
- Cancel restores the row unchanged and the Room is still in the sidebar.
- After Confirm the Room leaves the sidebar without a reload.
- `DOOMED.jsonl` is GONE from `<data>\rooms\`.
- Optional DB check: `SELECT COUNT(*) FROM rooms WHERE id='DOOMED'` and `SELECT COUNT(*) FROM room_members WHERE room_id='DOOMED'` both return 0.

**Fail if — any of these**

- Delete removes the Room on the FIRST click with no confirmation step -> the confirmation is missing and there is no undo.
- A modal dialog or a browser `confirm()` appears -> the inline row swap was not used; see `TeammateCard.razor`'s comment on why dialogs must not stack.
- The Room is gone from the sidebar but `DOOMED.jsonl` is still on disk -> `IChatStore.DeleteAsync` was not called; an orphan Transcript is left behind.
- `rooms` is empty for that id but `room_members` still has rows -> the transaction deleted in the wrong order or partially committed.

**Inconclusive if**

If `Team:DataDir` has been pointed somewhere else, look there instead — do not conclude from the default path.

### INVITEROOMS-37 — Archiving or deleting the Room you are viewing moves you somewhere valid

**Free** · about 6 min

*The one interaction that can strand the Human on a Room that is no longer in the sidebar. The navigation deliberately lives in the RoomsChanged handler rather than in LoadRoomAsync, so it is worth proving it fires — and that it does NOT fire on an ordinary Room switch.*

**Before you start**

- At least three Rooms in the sidebar.

**Steps**

1. Open the first Room so it is the one on screen.
2. Right-click ITS row and Archive it.
3. Read the address bar and the `<h1>`.
4. Click a different Room, then click back. Confirm nothing ejects you while simply switching.
5. Archive every remaining Room one at a time. This includes the `Bo` Room and the Chief of Staff's Room; INVITEROOMS-38 starts by unarchiving `Bo`.
6. Read the address bar and the page.

**Pass if — all of these**

- After step 2 you land on another Room — its id is in the address bar and its `<h1>` names it. You are never left on an archived Room.
- Step 4 does NOT move you anywhere; switching Rooms normally is unaffected.
- After step 5 you are on `/` and the page shows the empty state, not a blank panel and not an error.
- Archived chats lists them all.

**Fail if — any of these**

- You stay on the archived Room, which is no longer in the sidebar -> the RoomsChanged handler is not checking `Archived`.
- Switching Rooms normally ejects you -> the check was put inside `LoadRoomAsync`, which runs on every RoomsChanged.
- With every Room archived, `/` redirects you INTO an archived Room -> `OnParametersSetAsync` is picking `rooms[0]` rather than the first non-archived one.
- The browser hangs or the address bar flickers between two Rooms -> a redirect loop.

### INVITEROOMS-38 — An archived two-Member Room is not reused: starting a chat with that Teammate mints a second one

**Free** · about 7 min

*This is the accepted cost of ADR-0018, chosen knowingly over reuse-and-unarchive. It is here so the duplicate is recognised as designed rather than filed as a bug — the same reason INVITEROOMS-21 exists.*

**Before you start**

- Bo's own two-Member Room (`Bo`), which nothing before INVITEROOMS-37 has touched; INVITEROOMS-37 step 5 archived it along with every other Room, so it now sits under **Archived chats**. Do NOT re-run the seeder line to get it back: that wipes the archived and deleted Rooms INVITEROOMS-39 needs. Instead open **Archived chats** and click **Unarchive** beside `Bo`, then open it. If you have not run INVITEROOMS-37 it is simply in the sidebar.
- The `Bo` Room holds at least one Message. If it is empty, send `hi` and wait for Bo's reply.

**Steps**

1. Archive the `Bo` Room. Write its room id down (call it OLD).
2. Open **Teammates**, click the `Bo` tile, and read its actions. Close the card.
3. Start a new chat with `Bo` (New chat, tick `Bo`, Start chat).
4. Read the new Room's id, `<h1>` and Transcript.
5. Open **Archived chats** and Unarchive OLD.
6. Read the sidebar.

**Pass if — all of these**

- Step 2: the card shows **Edit** and **Open** but NO **Message** action, because the Message link resolves through the exact-two-Member lookup, which excludes archived Rooms (`Teammates.razor:328`; `SqliteTeamDirectory.cs:393`).
- Step 3 creates a NEW Room with a new id. Its Transcript is EMPTY and its `<h1>` reads `Bo`.
- OLD is not resurrected by step 3 and stays archived until step 5.
- After step 5 the sidebar shows TWO entries both reading `Bo`, the older one holding the original Messages.

**Fail if — any of these**

- Step 2 shows **Message** -> the card still finds the archived Room, so its link would open a Room that is not in the sidebar.
- Step 3 unarchives and reopens OLD -> `FindRoomWithExactMembersAsync` is not excluding archived Rooms; this is the behaviour that was explicitly NOT chosen.
- Step 3 opens OLD while leaving it archived -> you are posting into a Room that is not in the sidebar and cannot be found again.
- No new Room appears at all -> the lookup matched the archived Room and returned it, and `Bo` now has no reachable Direct Room.

> [!NOTE]
> TWO sidebar entries reading `Bo` after step 5 is the DESIGNED outcome, recorded in [Known limits](../known-limits.md) and [ADR-0018](../../adr/0018-a-room-can-be-archived-or-deleted.md). Do NOT file it as a duplicate-Room bug. This is a *different* route to two same-named Rooms than INVITEROOMS-21's, which comes from re-registration.

### INVITEROOMS-39 — Archived and deleted Rooms survive a restart

**Free** · about 5 min

*Archived state is a row in a table that did not exist before 2026-09-21, and the whole sibling-table design rests on an existing `team.db` gaining it cleanly. An automated migration test covers that; this proves the round trip on a real database that has been through the app.*

**Before you start**

- One Room archived and one Room deleted, from the tests above. Note both names.

**Steps**

1. Stop the app (Ctrl+C in `T-A`). Do NOT re-run the seeder line or delete `C:\seeds\huddle\uat\data`.
2. Start it again with the app command of `P-LAUNCH-UAT` only, and open the browser.
3. Read the sidebar.
4. Open **Archived chats**.

**Pass if — all of these**

- The archived Room is still absent from the sidebar and still listed under Archived chats.
- The deleted Room is in neither place.
- Every other Room is present, in the same order, with its Transcript intact.
- `T-A` logs no schema, migration or SQLite error at startup.

**Fail if — any of these**

- The archived Room is back in the sidebar -> archived state was never persisted, or `archived_rooms` is being recreated empty.
- The deleted Room reappears -> the delete was not committed.
- Startup logs a SQLite error naming `archived_rooms` -> the DDL is not idempotent.

---

Back to [the manual test script](../manual-tests.md).
