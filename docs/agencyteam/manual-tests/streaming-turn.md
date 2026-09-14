# Turn streaming, Drafts, Stop and failure surfacing

Prove, in a real browser, everything the Human sees while a Turn is happening and everything the Human is told when one fails: the live Draft row (in-memory only, keyed by Message id), the tool-activity line inside it, the per-Draft Stop button, the role="alert" member-health strip, and how all three survive - or deliberately do not survive - a reload, a Room switch, a dropped connection and an app restart. Nothing in the automated suite renders a browser (the page tests only ever see prerendered HTML), so the DraftChanged event path, the blinking caret, auto-scroll, the disabled-while-in-flight Stop button, two Drafts at once and every real-agent failure mode are unproven except by a person at http://localhost:5100. The tests are ordered so that every free test runs before any test that spends money, and the failure modes hunted hardest are the silent ones: a Draft that never renders, a Draft that freezes forever, an alert strip that is permanently on, and an Agent that goes deaf with a green dot.

**30 tests** · 22 free, 8 paid 💰 · about 4.4 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named below from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB` / `O-WIRE`, the four resets,
`P-NEW-PERSONA`, `P-ECHO-BOT` and the standing conventions. This area adds:

1. Take the [§0.4](../manual-tests.md#04-rollback) rollback copy before touching anything. Several tests here edit config, Personas and the database.
2. Free lane is `P-LAUNCH-FREE`. Tests STREAMINGTURN-21 onward additionally need the Adapter: run `pwsh tools/acp/install.ps1`, then confirm `tools\acp\node_modules` exists and `node --version` prints a version. Without both, every ACP test here is INCONCLUSIVE, not failed.
3. Open a Chromium browser at `http://localhost:5100`. Confirm it redirects to `/rooms/<some id>` and the sidebar shows, top to bottom: **New chat**, the Room list, **Teammates**, **Settings**. Write down the room id — tests call it ROOMID. Keep DevTools (F12) on the **Elements** tab; every "search the DOM" step below means Ctrl+F inside Elements, which searches the live DOM and not the page text.
4. Open `T-C` for pipe clients and `T-D` for file and HTTP checks.
5. SAVE THE SLOW-DRAFT CLIENT. It is the workhorse of this area: it holds a Draft open for as long as you like, costs nothing and needs no real agent. Create `$env:TEMP\drip.ps1` with exactly this content:

``powershell
param([string]$Name='drip',[string]$Pipe='team',[string]$RoomId='',[int]$DelaySeconds=3,[string[]]$Chunks=@('This ','reply ','arrives ','**slowly**. '))
$ErrorActionPreference='Stop'
$c=[System.IO.Pipes.NamedPipeClientStream]::new('.',$Pipe,'InOut','Asynchronous'); $c.Connect(5000)
$enc=[System.Text.UTF8Encoding]::new($false)
$r=[System.IO.StreamReader]::new($c,$enc); $w=[System.IO.StreamWriter]::new($c,$enc); $w.AutoFlush=$true
function S($o){ $w.WriteLine(($o|ConvertTo-Json -Compress)) }
S @{type='hello';version=3;name=$Name;description='slow draft bot'}
$welcome=$r.ReadLine()|ConvertFrom-Json
if($RoomId -eq ''){ $roomId=$welcome.rooms[0].id } else { $roomId=$RoomId }
$mid=[guid]::NewGuid().ToString('N')
Write-Host "AGENT=$Name ROOM=$roomId MESSAGEID=$mid"
$welcome.rooms | ForEach-Object { Write-Host "  room '$($_.name)' = $($_.id)" }
$full=''
foreach($t in $Chunks){ S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text=$t;isFinal=$false}; $full+=$t; Start-Sleep -Seconds $DelaySeconds }
Read-Host 'ENTER = post the Message and end the Draft'
S @{type='postMessage';version=3;roomId=$roomId;messageId=$mid;text=$full}
S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text='';isFinal=$true}
Read-Host 'ENTER = disconnect'
``

6. Understand three hard constraints of that client before using it: `version` MUST be 3 (the server compares for strict equality and answers `badMessage` otherwise, and nothing renders); `messageId` must match `[A-Za-z0-9_-]{1,64}`, which `[guid]::ToString('N')` satisfies; and the script only ever reads the welcome line, so it never drains envelopes the server sends it — keep each run short, and use the listening variant given inside STREAMINGTURN-17 when you need to see what the server sends.

## Tests

### STREAMINGTURN-01 — In the stock configuration no Room shows a member-health alert strip

**Free** · about 5 min

*Proves the role="alert" strip requires a reported reason rather than merely an unhealthy state, so it is not permanently on in every Room.*

**Before you start**

- The app is running from `T-A` with `$env:Team__Acp__Enabled="false"`.
- At least one Room exists in the sidebar.

**Steps**

1. Confirm in the `T-A` console that no line containing `failed to start` has been printed since startup.
2. In the browser, open http://localhost:5100 and let it redirect. Note the room id from the address bar as ROOMID.
3. In devtools Elements, press Ctrl+F and search for `member-health-alert`. Record the match count.
4. Click each room link in the sidebar in turn and repeat the same search in each Room.
5. In `T-D` run: `(Invoke-WebRequest "http://localhost:5100/rooms/ROOMID" -UseBasicParsing).Content | Select-String 'member-health-alert'` (substituting the real id). Record whether anything is printed.
6. Click **Teammates** in the sidebar and record the status label and dot colour shown for each teammate.
7. Go back to a Room and, in `T-C`, run `pwsh tools/echo-bot.ps1 -Name echo`, wait for it to print `Sent hello`, then press Ctrl+C in that window to disconnect it. Reload the Room in the browser and search for `member-health-alert` again.

**Pass if — all of these**

- The DOM search finds 0 matches for `member-health-alert` in every Room, before and after the echo agent connects and disconnects.
- The `Invoke-WebRequest` command prints nothing at all.
- /teammates may show teammates as **Offline** with a grey dot - that alone is correct and is not a failure of this test.

**Fail if — any of these**

- A `member-health-alert` strip appears in any Room -> an unhealthy state with no reported Reason is now being rendered, which puts a permanent role="alert" in every Room; an alert that is always on is the exact failure this strip exists to prevent.
- The strip appears only after the echo agent disconnects -> mere pipe absence is being treated as a reportable reason; disconnection without a reported failure must stay silent in the Room.
- `Invoke-WebRequest` prints a matching line while devtools shows none -> the strip is in the prerendered HTML and is being removed by the interactive render, so a reloading Human sees a flash of a false alert.

**Inconclusive if**

If the sidebar shows `No rooms yet. Start an agent to create one.`, no Room exists: run `pwsh tools/echo-bot.ps1 -Name echo` in `T-C`, wait for the sidebar to gain an `echo` room, then start again. If `Invoke-WebRequest` fails to connect, the app is not running on port 5100 - fix that first and do not record a result.

> [!NOTE]
> This is the cheapest and highest-value negative test in the failure-surfacing half. Run it first and re-run it after any test that leaves a Persona broken, to be sure the strip cleared.

### STREAMINGTURN-02 — A Draft appears as a distinct live row while a Turn is being written

**Free** · about 8 min

*Proves the whole live streaming path works in a browser: the DraftChanged event, the growing text, the caret, and that a Draft is visibly not a Message.*

**Before you start**

- App running, `Team__Acp__Enabled=false`.
- `$env:TEMP\drip.ps1` saved as described in setup.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip`.
2. Read the line it prints - `AGENT=drip ROOM=<roomid> MESSAGEID=<id>` - and write down both values as DRIPROOM and DRIPMSG.
3. In the browser, without reloading the page, watch the sidebar: a room named `drip` appears within a second or two. Click it.
4. Watch the message area for about 12 seconds while the four chunks arrive, one every 3 seconds.
5. While the text is still growing, right-click the growing row and choose **Inspect**, then read its classes in the Elements tab.
6. Still in Elements, check inside that row for a `<span class="message-time">` element.
7. Look at the end of the growing text for a small vertical bar that blinks roughly once a second.
8. In `T-D` run `Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" | Measure-Object -Line` while the Draft is still on screen. Record the line count.
9. Return to `T-C` and press ENTER at the `ENTER = post the Message and end the Draft` prompt, then re-run the line count command in `T-D`.

**Pass if — all of these**

- A new row appears below the last Message showing `drip` as the sender and the text growing chunk by chunk: `This `, then `This reply `, then `This reply arrives `, then `This reply arrives **slowly**. `.
- The row's class attribute is `message-row streaming` and the text element inside it is `message-body streaming`.
- There is NO `message-time` element inside the streaming row.
- A blinking vertical bar (`span.draft-caret`) sits at the end of the growing text.
- A **Stop** button is present in the same meta line as the sender name.
- The transcript line count is unchanged the whole time the Draft is on screen, and increases by exactly one after you press ENTER.

**Fail if — any of these**

- Nothing appears until the whole reply lands -> the delta path is dead; check `T-A` for `Persona 'X' failed to write a message delta for a turn in room Y`, which is logged once per Turn and then suppressed, so a dead pipe produces a Turn with no streaming and no further warning.
- One chunk appears and the row then freezes until the final Message -> the render-coalescing flag is never being cleared after a render, so only the first update of each Turn ever reaches the screen.
- No caret, or a caret that is present but does not blink -> the stylesheet did not load; check devtools Network for a 404 on the CSS bundle, which fails silently and takes the rest of the page styling with it.
- A timestamp appears inside the streaming row -> the Draft is being rendered through the Message path, which means it will be treated as a Message elsewhere too.
- The transcript file gains a line while the Draft is still streaming -> a Draft is being persisted, which contradicts the definition of a Draft and will leave half-replies in scrollback.

**Inconclusive if**

If the script throws on `Connect` the app is not running or the pipe name is not `team` - restart the app and retry. If the script prints an `error`/`badMessage` envelope, the protocol version is wrong: check the script says `version=3` everywhere. If the sidebar never gains a `drip` room, fix that before judging anything here.

> [!NOTE]
> Keep this drip session pattern for STREAMINGTURN-03 and 04 - they repeat the same run with different observations.

### STREAMINGTURN-03 — Draft text renders as plain text and becomes Markdown only when the Message lands

**Free** · about 5 min

*Proves partial model output is never pushed through the Markdown renderer, so a half-written fence cannot render as something that then flips.*

**Before you start**

- The drip client can be started as in STREAMINGTURN-02.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip`. Note the ROOM and MESSAGEID it prints.
2. Open the `drip` room in the browser and wait until all four chunks have arrived (about 12 seconds).
3. Read the streaming row carefully and write down exactly what characters you see around the word `slowly`.
4. In `T-C` press ENTER at the `ENTER = post the Message and end the Draft` prompt.
5. Read the same text again on the settled row.
6. In `T-D` run: `Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Tail 1` and read the `text` field.

**Pass if — all of these**

- While streaming, the row shows the literal characters `**slowly**.` with the asterisks visible.
- The instant the Message is posted, the same text re-renders with `slowly` in bold and no asterisks visible.
- The persisted line contains the raw `**slowly**. ` text with the asterisks - only the rendering differed.

**Fail if — any of these**

- Bold text (or a rendered code block) appears DURING streaming -> the Draft is being run through the Markdown renderer, which means a partial triple-backtick fence will render as a paragraph and then flip to a code block, reading as broken output.
- The literal `**slowly**.` is still on screen after the Message has landed -> the Draft was never completed and you are still looking at the Draft row, i.e. the Message arrived under a different id than the deltas.
- The persisted text has been converted to HTML or has the asterisks stripped -> rendering has leaked into the store; the Transcript must hold raw text.

**Inconclusive if**

If no Draft row ever appears, STREAMINGTURN-02 has already failed - record this test as not run and fix that first.

> [!NOTE]
> Multi-line draft text keeps its newlines by design (white-space: pre-wrap). To confirm that too, run the script with a chunk containing a newline, e.g. -Chunks @("line one`n","line two").

### STREAMINGTURN-04 — The Draft is replaced by exactly one Message and never shown twice

**Free** · about 5 min

*Proves the Draft is completed by the Message that shares its id, so the same reply cannot persist on screen twice.*

**Before you start**

- The drip client can be started as in STREAMINGTURN-02.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip`. Write down the ROOM and MESSAGEID it prints.
2. Open the `drip` room and wait for all four chunks.
3. Press ENTER in `T-C` at the post prompt.
4. Count how many rows on the page contain the text `This reply arrives`.
5. In devtools Elements, search for `streaming` and record the match count.
6. Confirm the remaining row has a sender name and an HH:mm timestamp beside it.
7. In `T-D` run: `Select-String -Path "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Pattern '<DRIPMSG>'` using the printed MESSAGEID.

**Pass if — all of these**

- Exactly one row on the page contains `This reply arrives`.
- The DOM search finds zero elements with the `streaming` class after the post.
- The surviving row shows sender + HH:mm timestamp + rendered text.
- `Select-String` returns exactly one matching line, and its `id` field equals the MESSAGEID the deltas carried.

**Fail if — any of these**

- The text stays on screen twice, once as a streaming row and once as a settled Message -> the Draft was never completed for that Message id, which happens when an agent posts under a different id than it streamed under; every such Turn leaves a permanent duplicate.
- The Draft vanishes and no Message ever appears -> the post was refused; check `T-A` for a `had a message refused` warning and record the code it names.
- More than one line matches the MESSAGEID in the transcript -> the same Turn was persisted twice.

**Inconclusive if**

A single frame in which both rows are briefly visible is a render race, not a defect - only a duplicate that persists after the page settles counts. If you cannot tell, take a screenshot two seconds after the post and judge from that.

> [!NOTE]
> The DRIPMSG value printed by the script is the only link between the Draft and its Message; without it this test cannot be judged, so write it down before starting.

### STREAMINGTURN-05 — The Stop button exists beside a live Draft and nowhere else, styled as an ordinary secondary action

**Free** · about 6 min

*Proves Stop is offered exactly when there is a Turn to stop, and that it is not dressed as a fault.*

**Before you start**

- At least one Room with some existing Messages.

**Steps**

1. With no Draft in flight, open any Room and in devtools Elements search for `draft-stop`. Record the match count.
2. In `T-C` start `pwsh $env:TEMP\drip.ps1 -Name drip` and open the `drip` room.
3. While the Draft is streaming, search the DOM for `draft-stop` again and record the count.
4. Click the streaming row's **Stop** button once with devtools open, and confirm no red banner or error text appears anywhere on the page.
5. In devtools, select the Stop button and read its **Computed** styles: record `color`, `background-color` and `border-color`.
6. Scroll the transcript and confirm no settled Message row carries a Stop button.
7. Press ENTER twice in `T-C` to post and then disconnect.

**Pass if — all of these**

- Zero `draft-stop` elements exist when no Draft is in flight.
- Exactly one `draft-stop` button exists per live Draft, inside that Draft's meta line beside the sender name.
- No settled Message row has a Stop button.
- The button's computed colours are muted/neutral - the same secondary greys the rest of the page uses - not a red or danger colour.

**Fail if — any of these**

- A Stop button appears on a settled Message row or with no Draft present -> Stop is being offered for a Turn that cannot be stopped, so clicking it can only ever do nothing.
- The button renders in red or with the same styling as the composer error box or the budget prompt -> a stopped Turn is being presented as a fault, contradicting the rule that stopping is one of three normal ways a Turn ends.
- Clicking Stop raises a visible error -> see STREAMINGTURN-16; a Stop with nothing to stop must be silent.

**Inconclusive if**

If your browser theme makes red hard to judge, compare the computed `background-color` with the **Leave paused** button in a Room showing the budget prompt (see STREAMINGTURN-20); they should be in the same family. Record hex values rather than an impression.

> [!NOTE]
> The drip client ignores stopTurn, so the reply still posts after you click Stop. That is expected and is proven separately in STREAMINGTURN-17.

### STREAMINGTURN-06 — A page reload mid-Turn shows the whole Draft so far, not an orphaned suffix

**Free** · about 8 min

*Proves the page reads the server-side Draft store while rendering rather than relying on an event a fresh page never witnessed - the classic silent orphaned-suffix bug.*

**Before you start**

- drip.ps1 available; a 5-second chunk delay gives a comfortable window.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 5`. Note the ROOM it prints.
2. Open the `drip` room and wait until at least three chunks have arrived (`This reply arrives `).
3. Press F5 to reload the page.
4. Read the very first painted content of the streaming row, before any further chunk arrives.
5. Wait for the next chunk and confirm the row keeps growing after the reload.
6. In `T-D`, while the Draft is still open, run: `(Invoke-WebRequest "http://localhost:5100/rooms/<DRIPROOM>" -UseBasicParsing).Content | Select-String 'draft-stop','This reply arrives'`.
7. Reload again and click **Stop** within the first fraction of a second, then wait two seconds and click it again.
8. Press ENTER twice in `T-C` to finish the run.

**Pass if — all of these**

- The first painted row after the reload already contains ALL the text that had arrived before the reload.
- The row carries on growing live after the reload without a second reload.
- The `Invoke-WebRequest` output contains both `draft-stop` and the draft text - the Draft is in the server-rendered HTML.
- The very first click on Stop immediately after a reload may do nothing; a click a moment later behaves normally.

**Fail if — any of these**

- The reloaded page shows an empty streaming row that only fills with text arriving AFTER the reload -> the orphaned-suffix bug: the page is not reading the Draft store on load, so a Human who reloads loses everything said before it, with no error anywhere.
- The Draft is missing entirely after the reload and reappears only when the next chunk lands -> the same defect, worse: for slow Turns the Room looks idle while an Agent is working.
- `Invoke-WebRequest` does not contain the draft text although the browser shows it -> the prerender path is broken and only the interactive path renders Drafts.
- Stop is still dead several seconds after the reload, or a yellow `An unhandled error has occurred.` bar sits at the bottom of the page -> the Blazor circuit never connected and every interactive control on the page is inert.

**Inconclusive if**

If the chunks arrive too fast to reload in time, re-run with `-DelaySeconds 10`. If devtools Network shows the `/_blazor` websocket failing to open, the circuit problem is environmental (proxy, extension) - retry in a clean browser profile before filing.

> [!NOTE]
> The first-click-inert behaviour immediately after a reload is expected (prerendered HTML carries no event handlers) and must not be filed. Only a button still dead after the circuit connects is a defect.

### STREAMINGTURN-07 — Two browser tabs on the same Room show and update the same Draft

**Free** · about 6 min

*Proves the Draft store is a server-side singleton broadcast to every viewer, not per-circuit state.*

**Before you start**

- drip.ps1 available.

**Steps**

1. Open the `drip` room in tab 1.
2. Open the same `/rooms/<DRIPROOM>` URL in a second tab, side by side if possible.
3. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 4`.
4. Watch both tabs for the full 16 seconds of chunks.
5. Click **Stop** in tab 1 (the drip client ignores it - this only confirms neither tab errors).
6. Press ENTER in `T-C` to post the Message.
7. Compare both tabs after the post.
8. Check `T-A` for any line reading `A DraftChanged handler threw and was skipped.`

**Pass if — all of these**

- Both tabs show the same growing Draft text, updating within about a second of each other.
- When the Message posts, both tabs replace the Draft with the settled Message.
- `T-A` prints no `A DraftChanged handler threw and was skipped.` line.

**Fail if — any of these**

- Only the tab that was open when the Turn started shows the Draft -> Draft state is being held per circuit, so a second viewer or a reopened tab is blind to Turns in progress.
- One tab freezes while the other keeps updating -> that tab's DraftChanged subscription died; `T-A`'s `A DraftChanged handler threw and was skipped.` line is the only trace such a tab leaves, and the tab itself shows nothing wrong.
- A tab never clears the Draft after the post -> completion is not reaching every subscriber and that tab now shows a permanent ghost reply.

**Inconclusive if**

If the second tab shows an `Attempting to reconnect` overlay, its circuit dropped for unrelated reasons - reload it and retry before judging.

> [!NOTE]
> Judge the two tabs side by side on one screen; comparing by switching tabs makes a one-second lag look like a freeze.

### STREAMINGTURN-08 — A Draft in one Room never leaks into another and survives navigating away and back

**Free** · about 7 min

*Proves the Room filter on Draft updates works in both directions: no cross-Room bleed, and no lost Draft on return.*

**Before you start**

- At least two Rooms in the sidebar (the `drip` room plus any other, e.g. `echo`). If only one exists, run `pwsh tools/echo-bot.ps1 -Name echo` once in `T-D` to create a second, then Ctrl+C it.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 5`.
2. Open the `drip` room and confirm the Draft is streaming.
3. Click the OTHER room in the sidebar.
4. Watch that Room for 10 seconds while at least two more chunks arrive in the drip room.
5. In devtools Elements, search for `streaming` in this other Room and record the match count.
6. Click back to the `drip` room in the sidebar.
7. Read the streaming row immediately on arrival, and confirm it keeps growing.
8. Press ENTER twice in `T-C` to finish.

**Pass if — all of these**

- The other Room shows no streaming row at any point (zero `streaming` matches) and does not visibly flicker or repaint as drip's chunks arrive.
- Returning to the drip room shows the Draft still present, with ALL the text accumulated so far - including the chunks that arrived while you were away.
- The Draft continues to grow after you return.

**Fail if — any of these**

- The other Room shows drip's Draft -> the Room filter on Draft updates is broken and every Room will paint every other Room's in-flight replies.
- Returning to the drip room shows no Draft, or a Draft that restarts from empty -> the Room load is not re-reading the Draft store, so any navigation during a Turn silently loses what was said.
- The other Room visibly repaints on every one of drip's chunks -> the Room filter is not applied before re-render; harmless to the eye, but it means the filter has been lost.

**Inconclusive if**

If the sidebar has only one room, the test cannot run - create a second Room first (New chat, or connect echo-bot) and record this as not run until then.

> [!NOTE]
> Judge 'flicker' with devtools' paint-flashing enabled if the naked eye is ambiguous; otherwise report only the two decisive observations (leak, and survival on return).

### STREAMINGTURN-09 — Draft rows always render below every Message, whatever the true chronology

**Free** · about 6 min

*Records the designed layout so a tester does not file it, and checks that the real chronology in the Transcript is nevertheless correct.*

**Before you start**

- drip.ps1 available.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 6`. Note the ROOM and MESSAGEID.
2. Open the `drip` room and wait for the first chunk.
3. Click into the composer, type `posted while drip is still writing` and press Enter.
4. Observe where your Message lands relative to the streaming row.
5. Press ENTER in `T-C` to post drip's Message.
6. Observe where drip's settled Message lands relative to yours.
7. In `T-D` run: `Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Tail 2` and read the two lines in order.

**Pass if — all of these**

- Your finished Message appears ABOVE drip's still-streaming row, even though it was sent later.
- When drip's Message posts, it settles BELOW your Message.
- The last two transcript lines are, in order: your `posted while drip is still writing`, then drip's reply - matching the order they were actually persisted.

**Fail if — any of these**

- The last two transcript lines are in the opposite order to the order you saw them persist -> the Transcript, which is the authoritative chronology, is being written out of order and scrollback will be wrong after any reload.
- Your Message does not appear at all until drip's Draft finishes -> Messages are being queued behind live Drafts.

**Inconclusive if**

If the composer shows a red error line instead of sending, read it: a spent Room Budget (`Agents have sent N replies since you last spoke, and are paused.`) means you are in the STREAMINGTURN-20 situation - clear it with **Continue** and retry.

> [!NOTE]
> Draft rows rendering below Messages is deliberate layout, NOT an out-of-order defect. Do not file it.

### STREAMINGTURN-10 — The message list auto-scrolls as a Draft grows, not only when a Message arrives

**Free** · about 8 min

*Proves the scroll trigger folds in Draft text length, so a long streaming reply does not grow off the bottom of the screen.*

**Before you start**

- A Room with enough Messages to make the list scroll.

**Steps**

1. Open the `drip` room. If the message list does not scroll, type `filler` and press Enter about 25 times until it does.
2. Resize the browser window smaller if needed so the list is clearly scrollable.
3. Scroll to the bottom of the list.
4. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 2 -Chunks @('Lorem ipsum dolor sit amet, ','consectetur adipiscing elit, ','sed do eiusmod tempor incididunt ','ut labore et dolore magna aliqua. ','Ut enim ad minim veniam, ','quis nostrud exercitation ullamco. ')`.
5. Without touching the scrollbar, watch whether the newest draft text stays visible as it grows over the six chunks.
6. Now scroll UP about half a page while chunks are still arriving and observe what happens on the next chunk.
7. Press ENTER twice in `T-C` to finish.

**Pass if — all of these**

- The list stays pinned to the bottom and the newest draft text remains visible through all six chunks without you touching the scrollbar.
- Scrolling up while chunks arrive pulls you back to the bottom on the next chunk (by design).

**Fail if — any of these**

- The view stays put while the Draft grows out of sight below the fold -> the scroll signature has stopped folding in the Draft text length and is counting Messages only, so every long streaming reply is invisible until it lands.
- The list jumps to the top or scrolls erratically -> the scroll target element is wrong.

**Inconclusive if**

If the list never becomes scrollable (a very tall window), shrink the browser window and repeat. If you cannot tell by eye, record a short screen capture and judge frame by frame.

> [!NOTE]
> Two by-design quirks that are NOT bugs: being yanked back to the bottom while reading scrollback, and a tool-activity change alone not scrolling (only Message count and Draft text length feed the signature).

### STREAMINGTURN-11 — A Draft dies with its Agent's connection - no frozen half-reply is left behind

**Free** · about 5 min

*Proves a client that vanishes without sending a terminator cannot leave a half-written reply blinking on screen forever - the exact failure streaming exists to remove.*

**Before you start**

- drip.ps1 available.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 5`.
2. Open the `drip` room and wait for at least two chunks to arrive.
3. With the Draft still visibly streaming, press Ctrl+C in `T-C` to kill the client without letting it post.
4. Watch the browser without reloading for 10 seconds.
5. In devtools Elements, search for `streaming` and record the match count.
6. Check the `T-A` console for a line reading `Agent connection <id> ended.` It is NOT a pass condition and is often absent: `AgentConnection` logs it only from its `catch (IOException)`, so a client that dies in a way which ends the read loop cleanly leaves no line at all. Judge on the browser and the transcript.
7. In `T-D` run: `Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Tail 1` and confirm the half-reply text is not there.
8. Reload the page and confirm the Room shows only real Messages.

**Pass if — all of these**

- The streaming row disappears from the open page within a second or two of the Ctrl+C, with no reload.
- Zero `streaming` elements remain and no error banner appears anywhere.
- The room still exists in the sidebar.
- The transcript's last line is not the partial draft text.

**Fail if — any of these**

- The half-written reply stays on screen with the caret still blinking -> the Draft was not cleared when its Agent's connection ended; it will now hang there for the life of the process with nothing logged, the silent failure this behaviour exists to prevent.
- The partial text appears as a persisted Message -> a Draft that never completed is being written to the Transcript.
- The whole Room or its sidebar entry disappears -> a disconnect is being treated as a Room deletion.

**Inconclusive if**

If the row disappears but you never saw it streaming first, the chunks had already finished - re-run with `-DelaySeconds 10` and kill earlier.

> [!NOTE]
> The cross-check (killing one client must not clear another's Draft in the same Room) is covered by STREAMINGTURN-14.

### STREAMINGTURN-12 — Tool activity shows as one muted italic line inside the Draft and is never persisted

**Free** · about 10 min

*Proves the tool-activity line replaces rather than accumulates, renders nothing when the Agent gives no title, and leaves no trace on the Transcript.*

**Before you start**

- App running free.

**Steps**

1. Create `$env:TEMP\drip-tool.ps1` with exactly this content:

param([string]$Name='drip',[string]$Pipe='team',[string]$RoomId='')
$ErrorActionPreference='Stop'
$c=[System.IO.Pipes.NamedPipeClientStream]::new('.',$Pipe,'InOut','Asynchronous'); $c.Connect(5000)
$enc=[System.Text.UTF8Encoding]::new($false)
$r=[System.IO.StreamReader]::new($c,$enc); $w=[System.IO.StreamWriter]::new($c,$enc); $w.AutoFlush=$true
function S($o){ $w.WriteLine(($o|ConvertTo-Json -Compress)) }
S @{type='hello';version=3;name=$Name;description='tool draft bot'}
$welcome=$r.ReadLine()|ConvertFrom-Json
if($RoomId -eq ''){ $roomId=$welcome.rooms[0].id } else { $roomId=$RoomId }
$mid=[guid]::NewGuid().ToString('N')
Write-Host "ROOM=$roomId MESSAGEID=$mid"
S @{type='toolActivity';version=3;roomId=$roomId;messageId=$mid;toolCallId='tc1';title='Reading Persona.cs';status='inProgress'}
Read-Host 'ENTER = send some text'
S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text='Looking into it. ';isFinal=$false}
Read-Host 'ENTER = second tool call'
S @{type='toolActivity';version=3;roomId=$roomId;messageId=$mid;toolCallId='tc2';title='Writing notes.md';status='completed'}
Read-Host 'ENTER = tool call with no title'
S @{type='toolActivity';version=3;roomId=$roomId;messageId=$mid;toolCallId='tc3';status='inProgress'}
Read-Host 'ENTER = post and finish'
S @{type='postMessage';version=3;roomId=$roomId;messageId=$mid;text='Looking into it. '}
S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text='';isFinal=$true}
Read-Host 'ENTER = disconnect'
2. In `T-C` run: `pwsh $env:TEMP\drip-tool.ps1 -Name drip`. Note the ROOM it prints.
3. Open the `drip` room and look at the message area BEFORE pressing any key in `T-C`.
4. Record what the new row contains: sender name, any text, a caret, a Stop button, and the small line below.
5. Press ENTER in `T-C` (sends the text) and read the row again.
6. Press ENTER again (second tool call) and read the small line again.
7. Press ENTER again (tool call with no title); check whether a small line is present at all and, in devtools Elements, search for `tool-activity` and record the count.
8. Press ENTER again to post the Message, and check the settled Message row for any tool line.
9. In `T-D` run: `Select-String -Path "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Pattern 'Reading Persona.cs','Writing notes.md','toolCallId'`.
10. Press ENTER once more in `T-C` to disconnect.

**Pass if — all of these**

- Before any text arrives, a Draft row already exists with the sender name, a caret, a **Stop** button and the single line `Reading Persona.cs` beneath it.
- The tool line shows only the title - no status word (`inProgress`, `completed`) and no JSON.
- The line is small, muted in colour and italic (`div.tool-activity`).
- After the second tool call the line reads `Writing notes.md` and `Reading Persona.cs` is GONE - exactly one `tool-activity` element exists at any time.
- After the tool call with no title, zero `tool-activity` elements exist.
- The settled Message row has no tool line, and `Select-String` finds no match for any of the three patterns in the transcript.

**Fail if — any of these**

- Tool lines accumulate into a list -> only the current activity is meant to be kept; a long Turn will grow an unbounded list inside the Draft.
- A status word or raw JSON is rendered -> the line is showing protocol detail rather than the Agent's own title.
- A `tool-activity` line appears for the no-title envelope -> a null title is being rendered as an empty or placeholder line rather than nothing.
- A tool line survives onto the settled Message, or any pattern is found in the .jsonl -> tool activity is reaching the Transcript, so scrollback would start reporting what an Agent did rather than what it said.
- No Draft row appears until text arrives -> a tool call before any text cannot open a Draft, so the Human sees an idle Room while the Agent works.

**Inconclusive if**

If the row does not appear at step 3, check `T-C` for an `error` envelope - a `badMessage` there means the status string was rejected (valid values: `pending`, `inProgress`, `completed`, `failed`). Fix the script rather than filing.

> [!NOTE]
> The last title staying visible after that call completed, until the whole Draft goes, is documented behaviour and not a defect.

### STREAMINGTURN-13 — A Draft written for a Room the sender is not a Member of is refused and nothing renders

**Free** · about 8 min

*Proves the pipe connection is the validation boundary for any client, and that a per-(Room, Message id) decision cannot be handed from one Room to another.*

**Before you start**

- Two Rooms exist: the `drip` room and one other (e.g. `echo`) that drip is NOT a member of.

**Steps**

1. In the browser, click the OTHER room (e.g. `echo`) and copy its id from the address bar. Call it OTHERROOM.
2. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -RoomId <OTHERROOM>`.
3. Watch the OTHER room in the browser for 15 seconds while the script sends its chunks.
4. In devtools Elements, search that Room for `streaming` and record the count.
5. Search the drip room for `streaming` too, to be sure the Draft did not land somewhere else.
6. Press ENTER twice in `T-C` to end the run.
7. Now test an invalid id: copy `$env:TEMP\drip.ps1` to `$env:TEMP\drip-badid.ps1` and change its `$mid=[guid]::NewGuid().ToString('N')` line to `$mid='not a valid id!'`.
8. Run `pwsh $env:TEMP\drip-badid.ps1 -Name drip` against the drip room, watch it for 10 seconds and search for `streaming`.

**Pass if — all of these**

- No streaming row appears in the OTHER room at any point (zero `streaming` matches).
- No streaming row appears in any other Room either.
- With the invalid message id, no Draft renders anywhere.

**Fail if — any of these**

- A Draft appears in a Room the sender is not a Member of -> membership is not enforced on the delta path, and any pipe client can write into any Room's live view.
- A Draft appears for the invalid message id -> the id format check has been lost, so ids that cannot correlate to a Message will open Drafts that can never be completed.
- A file appears under `App_Data\rooms` named after the invalid id -> path safety has been lost; treat as urgent.

**Inconclusive if**

The drip client does not print the server's error replies, so a silent absence of a Draft has two possible causes: a correct refusal, or the line never arriving at all. To resolve it, re-run with the listening client from STREAMINGTURN-17 (`-RoomId <OTHERROOM>`), which prints every envelope the server sends; you should see `"code":"notMember"` and, for the bad id, `"code":"badMessage"`. Record the test as inconclusive if you never saw the error envelope.

> [!NOTE]
> When a REAL Persona hits this path the Room ALSO gets a strip reading `<Name> is Degraded: A post was refused — <message>`, and `T-A` logs `Persona 'X' had a message refused: notMember - ...`. A raw pipe client gets no strip because it is not a Persona.

### STREAMINGTURN-14 — Two Agents stream into one Room at once and neither Draft is dropped

**Free** · about 12 min

*Proves Drafts are keyed by Message id, not by Room, so concurrent Turns cannot overwrite or merge each other - and that killing one client clears only its own Draft.*

**Before you start**

- drip.ps1 available.
- Two agent names must exist before the Group Room can be created.

**Steps**

1. In `T-C` run `pwsh $env:TEMP\drip.ps1 -Name drip` and in `T-D` run `pwsh $env:TEMP\drip.ps1 -Name drop`. Let both register; the sidebar gains `drip` and `drop` rooms.
2. Press ENTER twice in each window to let them finish and disconnect. Both agent names now exist.
3. In the browser sidebar, click **New chat**.
4. Tick the checkbox beside `drip` and the checkbox beside `drop`.
5. Click **Start chat**. The browser navigates to a new Room named `drip, drop`. Copy its id from the address bar as GROUPROOM.
6. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -RoomId <GROUPROOM> -DelaySeconds 6`.
7. Within two seconds, in `T-D` run: `pwsh $env:TEMP\drip.ps1 -Name drop -RoomId <GROUPROOM> -DelaySeconds 6`.
8. Watch the Group Room for about 25 seconds.
9. In devtools Elements, search for `message-row streaming` and record the match count. Read the sender name on each streaming row.
10. Read the text in each streaming row and confirm neither contains the other's words interleaved.
11. Press Ctrl+C in `T-C` only (kill drip without posting). Watch the browser for 10 seconds.
12. Press ENTER twice in `T-D` to post drop's Message and disconnect.
13. In a fourth window run: `Get-Content "src\Huddle.App\App_Data\rooms\<GROUPROOM>.jsonl" | Measure-Object -Line` and compare with the count before the run.

**Pass if — all of these**

- Two separate streaming rows are visible at the same time, one labelled `drip` and one labelled `drop`.
- Each has its own growing text, its own caret and its own **Stop** button.
- Neither row's text contains chunks from the other Agent.
- Killing drip removes ONLY drip's streaming row; drop's row stays and keeps growing.
- The transcript gains exactly one line - drop's - because drip was killed before posting.

**Fail if — any of these**

- Only one streaming row ever appears -> Drafts are being keyed by Room, so one concurrent Turn is silently dropped and that Agent appears to do nothing.
- One row's text mixes both Agents' words -> the two Turns are writing into the same Draft, so the reply the Human reads is not what either Agent said.
- One Draft replaces the other as they alternate updates -> the same keying defect, visible as flicker.
- Killing drip also clears drop's Draft -> Draft clearing is keyed by Room or connection rather than by Agent, so one agent's disconnect wipes another's live reply.

**Inconclusive if**

The store returns a Room's Drafts in no particular order, so the two rows may swap vertical position between renders - that is documented and NOT a defect. If the **New chat** panel shows `No agents have connected yet.`, steps 1-2 did not register both names; repeat them and reopen the panel.

> [!NOTE]
> Keep the GROUPROOM id - STREAMINGTURN-15 reuses it.

### STREAMINGTURN-15 — Stop disables itself while the request is in flight, per Agent rather than per Room

**Free** · about 8 min

*Proves the busy flag is keyed by Agent id, so stopping one Agent cannot lock out the Stop button of another Agent streaming in the same Room.*

**Before you start**

- The Group Room `drip, drop` from STREAMINGTURN-14 exists; its id is GROUPROOM.

**Steps**

1. Open devtools, go to the **Network** tab and set throttling to **Slow 3G** - this widens the in-flight window enough to see.
2. Use the LISTENING client from STREAMINGTURN-17 for both Agents, not `drip.ps1`. In `T-C` run: `pwsh $env:TEMP\drip-listen.ps1 -Name drip -RoomId <GROUPROOM>`.
3. In `T-D` run: `pwsh $env:TEMP\drip-listen.ps1 -Name drop -RoomId <GROUPROOM>`.

   > WHY: `drip.ps1` reads only the welcome line and never drains what the server sends it (see
   > setup note 6). The `stopTurn` envelope is a WRITE to that client, so against a non-draining
   > client it can block, `StopDraftAsync` never returns, its `finally` never clears the busy flag,
   > and the button stays disabled for the rest of the run. That looks exactly like this test's
   > "stays disabled indefinitely" Fail-if and is NOT the app's doing. Verified 2026-09-14: with
   > `drip.ps1` the button never re-enabled; with `drip-listen.ps1` it disabled and re-enabled
   > cleanly on every click.
4. Open the `drip, drop` Room and confirm both streaming rows are present with a Stop button each.
5. Click **Stop** on drip's row once, and immediately watch both buttons.
6. Immediately double-click drip's Stop button and note whether anything visibly changes.
7. While drip's button is disabled, hover over drop's Stop button and confirm it still shows a pointer cursor and can be clicked.
8. Wait five seconds and re-check drip's button.
9. Set Network throttling back to **No throttling** and press ENTER twice in both `T-C` and `T-D` to finish.

**Pass if — all of these**

- Clicking drip's **Stop** visibly dims it (reduced opacity, no pointer cursor) while the request is in flight, and it re-enables afterwards.
- drop's **Stop** button stays fully enabled and clickable throughout.
- Double-clicking the disabled button produces no additional effect and no error.

**Fail if — any of these**

- Neither button ever disables -> the busy flag is not wired, so a Human can fire repeated Stop requests for the same Agent with no feedback that the first is still running.
- Both buttons disable together although they belong to different Agents -> the flag is keyed per Room, so stopping one Agent freezes the control for every other Agent in the Room.
- drip's button stays disabled indefinitely -> the Stop request never returned and the control is now permanently dead for that Agent.

**Inconclusive if**

On localhost without throttling the disabled window can be a single frame and genuinely unobservable by eye. If you see nothing with Slow 3G either, record a high-frame-rate screen capture and judge from it, or mark the test inconclusive - do not report 'never disables' from a naked-eye observation on an unthrottled connection.

> [!NOTE]
> Both buttons disabling together IS correct when both Drafts belong to the SAME Agent - the flag is a set of Agent ids. That case cannot arise with drip and drop.

### STREAMINGTURN-16 — Clicking Stop on an Agent with no live connection is a silent no-op

**Free** · about 5 min

*Proves the deliberate silence, so that 'I clicked Stop and nothing happened' is only ever a defect when the Agent is genuinely still connected.*

**Before you start**

- drip.ps1 available.

**Steps**

1. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 10`.
2. Open the `drip` room and wait for one chunk so the Draft row and its Stop button are present.
3. Open the browser devtools **Console** tab and clear it.
4. Press Ctrl+C in `T-C` to kill the client.
5. Within the second or two before the row clears, click **Stop** on that row. If the row clears first, repeat from step 1 and click faster.
6. Read the browser console for errors.
7. Scroll to the very bottom of the page and look for any error banner, red text, or the yellow `An unhandled error has occurred.` bar.
8. Check the `T-A` console for any new warning or error lines.

**Pass if — all of these**

- Nothing happens: no error banner on the page, no red text, no exception in the browser console.
- The button either re-enables or disappears with the row.
- `T-A` logs nothing beyond the ordinary `Agent connection <id> ended.`

**Fail if — any of these**

- An error message or the yellow unhandled-error bar appears -> a Stop for a connection that is already gone is throwing rather than returning early, so every late click breaks the page for the Human.
- A browser-console exception appears -> the same defect, invisible to the Human but it will kill the circuit's next interaction.

**Inconclusive if**

If the Draft row clears too fast to click, re-run with a longer `-DelaySeconds` and kill the client between chunks. If you never managed to click while the row was present, record as not run.

> [!NOTE]
> The yellow unhandled-error bar can sit below the fold and be invisible when the stylesheet fails to load - always scroll to the very bottom before concluding there is no banner.

### STREAMINGTURN-17 — A Stop click reaches the wire even when the client ignores it

**Free** · about 8 min

*Separates 'the server never sent the stop' (a real defect) from 'the client ignored the stop' (expected for demo and raw clients), using the client's own console as the oracle.*

**Before you start**

- App running free.

**Steps**

1. Create `$env:TEMP\drip-listen.ps1` with exactly this content:

param([string]$Name='drip',[string]$Pipe='team',[string]$RoomId='')
$ErrorActionPreference='Stop'
$c=[System.IO.Pipes.NamedPipeClientStream]::new('.',$Pipe,'InOut','Asynchronous'); $c.Connect(5000)
$enc=[System.Text.UTF8Encoding]::new($false)
$r=[System.IO.StreamReader]::new($c,$enc); $w=[System.IO.StreamWriter]::new($c,$enc); $w.AutoFlush=$true
function S($o){ $w.WriteLine(($o|ConvertTo-Json -Compress)) }
S @{type='hello';version=3;name=$Name;description='listening draft bot'}
$welcome=$r.ReadLine()|ConvertFrom-Json
if($RoomId -eq ''){ $roomId=$welcome.rooms[0].id } else { $roomId=$RoomId }
$mid=[guid]::NewGuid().ToString('N')
Write-Host "ROOM=$roomId MESSAGEID=$mid"
foreach($t in 'This ','reply ','arrives ','slowly. '){ S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text=$t;isFinal=$false} }
Write-Host 'Draft is open. Click Stop in the browser. Every envelope the server sends prints below. Ctrl+C to quit.'
while($true){ $line=$r.ReadLine(); if($null -eq $line){ break }; Write-Host $line }
2. In `T-C` run: `pwsh $env:TEMP\drip-listen.ps1 -Name drip`. Note the ROOM it prints.
3. Open the `drip` room in the browser; the full draft text is already on screen with a Stop button.
4. Click **Stop** once.
5. Read the next line printed in `T-C`.
6. Watch the browser for 10 seconds.
7. Press Ctrl+C in `T-C`.

**Pass if — all of these**

- Immediately after the click, `T-C` prints a line containing `"type":"stopTurn"` whose `roomId` equals the Room you clicked in.
- The Draft stays on screen afterwards, because this client ignores stopTurn - expected, not a failure.

**Fail if — any of these**

- No `stopTurn` line is printed when you click Stop -> the click is not reaching the wire at all, which means Stop is dead for real Personas too and will read as a broken button with no error anywhere.
- The printed `roomId` is not the Room you clicked in -> the envelope is being addressed from the wrong Room, so the log line that later names where the Human asked will be wrong.
- Several `stopTurn` lines print for one click -> the button is firing more than once per click.

**Inconclusive if**

If `T-C` prints nothing after the click AND the Draft row was not visible at that moment, you clicked nothing - re-run and confirm the Stop button is present first. If pwsh exits immediately with a pipe error, the app is not running.

> [!NOTE]
> The built-in demo agents (`echo`, `alpha`) and `tools/echo-bot.ps1` also ignore stopTurn by design and will still post their reply. Honouring stopTurn is a real-Persona behaviour, proven in STREAMINGTURN-24.

### STREAMINGTURN-18 — A Draft stops growing at 256 KB but the posted Message still arrives intact

**Free** · about 10 min

*Proves the in-memory Draft cap neither truncates what is already shown nor discards the Draft, and does not bound the real Message.*

**Before you start**

- App running free.

**Steps**

1. Create `$env:TEMP\drip-big.ps1` with exactly this content:

param([string]$Name='drip',[string]$Pipe='team',[string]$RoomId='',[int]$ChunkCount=300)
$ErrorActionPreference='Stop'
$c=[System.IO.Pipes.NamedPipeClientStream]::new('.',$Pipe,'InOut','Asynchronous'); $c.Connect(5000)
$enc=[System.Text.UTF8Encoding]::new($false)
$r=[System.IO.StreamReader]::new($c,$enc); $w=[System.IO.StreamWriter]::new($c,$enc); $w.AutoFlush=$true
function S($o){ $w.WriteLine(($o|ConvertTo-Json -Compress)) }
S @{type='hello';version=3;name=$Name;description='big draft bot'}
$welcome=$r.ReadLine()|ConvertFrom-Json
if($RoomId -eq ''){ $roomId=$welcome.rooms[0].id } else { $roomId=$RoomId }
$mid=[guid]::NewGuid().ToString('N')
Write-Host "ROOM=$roomId MESSAGEID=$mid"
$chunk=('x'*1023)+"`n"
$full=''
for($i=1;$i -le $ChunkCount;$i++){ S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text=$chunk;isFinal=$false}; $full+=$chunk; Start-Sleep -Milliseconds 30 }
Write-Host "sent $($full.Length) characters"
Read-Host 'ENTER = post the full Message'
S @{type='postMessage';version=3;roomId=$roomId;messageId=$mid;text=$full}
S @{type='messageDelta';version=3;roomId=$roomId;messageId=$mid;text='';isFinal=$true}
Read-Host 'ENTER = disconnect'
2. In `T-C` run: `pwsh $env:TEMP\drip-big.ps1 -Name drip`. Note the ROOM and MESSAGEID.
3. Open the `drip` room and watch the streaming row grow for the ~15 seconds the loop takes.
4. Confirm the browser stays responsive (you can still scroll and click the sidebar).
5. When `T-C` prints `sent 307200 characters`, confirm the streaming row is still on screen and has not been emptied or shortened.
6. Press ENTER in `T-C` to post the full Message.
7. In `T-D` run: `$line = Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" -Tail 1; ($line | ConvertFrom-Json).text.Length` and record the number.
8. Press ENTER in `T-C` to disconnect.

**Pass if — all of these**

- The Draft row keeps growing up to roughly 256 KB of text and then stops lengthening, while remaining visible.
- Text already shown is NOT removed or shortened when the cap is hit.
- The browser and the app stay responsive throughout.
- After the post, the persisted `text` length is 307200 - the full text, not the 262144-character cap.

**Fail if — any of these**

- The Draft row disappears at the cap -> hitting the cap discards the Draft, so a long reply vanishes mid-Turn with nothing said.
- The already-shown text is truncated back when the cap is hit -> the visible reply rewrites itself backwards, which reads as corruption.
- The persisted text length equals 262144 -> the Draft cap is bounding the real Message, so replies longer than 256 KB are silently truncated in the Transcript.
- The browser or the app hangs -> unbounded growth or a rendering stall under load.

**Inconclusive if**

If `T-C` errors with `line too long`, one envelope exceeded the 1 MB wire cap - reduce the chunk size in the script rather than filing. If the .jsonl line cannot be parsed by ConvertFrom-Json, measure with `(Get-Content ... -Tail 1).Length` instead and allow for JSON overhead.

> [!NOTE]
> The cap is on the Draft only; the Message is bounded separately at 1 MB per wire line.

### STREAMINGTURN-19 — Restarting the application loses every Draft in flight, and nothing resurrects as a Message

**Free** · about 7 min

*Confirms the documented in-memory limit and, more importantly, that a lost half-reply never reappears in the Transcript.*

**Before you start**

- drip.ps1 available.

**Steps**

1. In `T-D` run: `Get-Content "src\Huddle.App\App_Data\rooms\<DRIPROOM>.jsonl" | Measure-Object -Line` and write down the count as BEFORE.
2. In `T-C` run: `pwsh $env:TEMP\drip.ps1 -Name drip -DelaySeconds 10`.
3. Open the `drip` room and wait for two chunks to appear.
4. With the Draft still on screen, press Ctrl+C in `T-A` to stop the application.
5. Press Ctrl+C in `T-C` as well to kill the client.
6. In `T-A` run `dotnet run --project src/Huddle.App` again and wait for it to serve.
7. Reload http://localhost:5100 and open the `drip` room.
8. Search the DOM for `streaming` and record the count.
9. In `T-D` re-run the line count command and compare with BEFORE.

**Pass if — all of these**

- After the restart the Room shows only Messages that were actually posted; zero `streaming` elements.
- Nothing on the page reports that a Draft was lost - the loss is silent by design.
- The transcript line count is identical to BEFORE.

**Fail if — any of these**

- The half-written reply reappears as a persisted Message after the restart -> a Draft has been written to the Transcript, which is the one real defect this test can find.
- The transcript line count grew -> the same defect.
- The Room fails to load or the sidebar is empty after the restart -> a restart is damaging stored state; outside this area but report it.

**Inconclusive if**

If the app fails to restart (port in use), wait for the old process to exit fully and retry; do not judge the Room's contents until the app is serving normally.

> [!NOTE]
> Losing the Draft itself is a documented limit and must NOT be filed.

### STREAMINGTURN-20 — A spent Room Budget shows the Continue prompt and never a health strip

**Free** · about 10 min

*Proves an Agent that is working exactly as designed is never marked broken: the budget refusal owns its own surface.*

**Before you start**

- The demo agents are enabled (default), so `echo` is available in the New chat panel or as a Room.

**Steps**

1. Press Ctrl+C in `T-A`. In the same window run: `$env:Team__AgentMessageBudget="2"; $env:Team__Acp__Enabled="false"; dotnet run --project src/Huddle.App`.
2. Reload the browser and open the `echo` room. If it does not exist, click **New chat**, tick `echo`, click **Start chat**.
3. Click into the composer, type `@echo hello one` and press Enter. Wait for the reply, and read the grey line: `1 of 2 agent replies since you last spoke.`
4. Put a SECOND Agent in the Room - type `/invite @alpha` and press Enter.
5. Send ONE Message mentioning both: `hello @echo and @alpha`. Wait for both replies.

   > A Human Message RESETS this counter, so sending `@echo hello two` and `@echo hello three`
   > can never fill a budget of 2: each Message you type zeroes it and draws exactly one reply,
   > leaving the line at `1 of 2` forever. Two replies must arrive between two Human Messages,
   > which needs two Agents. Same rule as `ROOMMESSAGING-30` and `STARTUPCONFIG-33`.
6. Read the area between the transcript and the composer.
7. In devtools Elements, search for `member-health-alert` and record the count; then search for `budget-prompt`.
8. Click **Teammates** and read the status label and dot for `echo`.
9. The refusal needs its own configuration, because at a budget of 2 with two Agents both replies are accepted. Stop the app, relaunch with `$env:Team__AgentMessageBudget="1"`, and send one `hello again @echo and @alpha`: the first Agent to answer spends the budget and the second is refused. Check `T-A` for a Warning line reading `Room '<id>' refused a message from '<agent>': its budget of 1 agent messages since a human last spoke is spent.` - the number is the configured budget.
10. Return to the Room and click **Continue**, then send `@echo hello four` and confirm a reply arrives.
11. Press Ctrl+C in `T-A` and restart without the variable: `Remove-Item Env:Team__AgentMessageBudget; dotnet run --project src/Huddle.App`.

**Pass if — all of these**

- Once the budget is spent, the Room shows `Agents have sent 2 replies since you last spoke, and are paused.` with a **Continue** button and a **Leave paused** button.
- Zero `member-health-alert` elements exist in that Room.
- /teammates does not show `echo` as **Degraded** because of the pause.
- `T-A` logs the refusal at Warning with no health report alongside it.
- Clicking **Continue** lets the next reply through.

**Fail if — any of these**

- A `member-health-alert` strip appears alongside the Continue prompt -> a budgetExhausted refusal is being reported as a health failure, marking an Agent that is behaving exactly as designed as broken.
- `echo` reads **Degraded** on /teammates after the pause -> the same defect on the other surface.
- No Continue prompt appears although the console logged the refusal -> the pause is invisible, and the Human sees only a Room that stopped answering.

**Inconclusive if**

The demo agents only reply when Mentioned, so you MUST type `@echo` each time - a plain `hello` produces nothing and is not evidence. If no reply ever arrives, check `T-A` for `Demo agent echo connected.`; if it is missing, the demo agents are off and this test cannot run.

> [!NOTE]
> `Team__AgentMessageBudget` maps to the `Team:AgentMessageBudget` setting and only takes effect on restart.

### STREAMINGTURN-21 — A failed Persona start surfaces as a role="alert" strip in every Room it is a Member of

**Free** · about 15 min

*Closes the original defect this whole feature exists for: an Agent that has crashed and an Agent that is thinking used to look identical - both showed nothing.*

**Before you start**

- `pwsh tools/acp/install.ps1` has been run and `tools\acp\node_modules` exists.
- `node --version` prints a version.

**Steps**

1. Press Ctrl+C in `T-A`, then run: `$env:Team__Acp__Enabled="true"; Remove-Item Env:Team__Acp__Command -ErrorAction SilentlyContinue; dotnet run --project src/Huddle.App`.
2. In the browser click **Teammates** in the sidebar, then click **New teammate**.
3. In **Name** type `Nova`. In **Title** type `Test persona`. In **Alias** type `nova`. Leave **Teams** blank.
4. In the **Persona body** textarea type `You are Nova. Answer briefly.`
5. In the **Model** dropdown choose the option whose name contains `Haiku`.
6. In the **Effort** dropdown choose `low`.
7. Click **Add teammate**. Wait until Nova's tile shows **Online** with a green dot (it may pass through **Starting** first).
8. Open Nova's Room from the sidebar (or click her tile and then **Message**) and confirm the Room shows NO alert strip while she is healthy. Note the Room id as NOVAROOM.
9. Press Ctrl+C in `T-A`. In the same window run: `$env:Team__Acp__Command="definitely-not-node"; dotnet run --project src/Huddle.App`.
10. Wait for startup, then reload the browser and open Nova's Room.
11. Read the area between the transcript and the composer, word for word.
12. In devtools Elements, confirm the strip element has `class="member-health-alert"` and `role="alert"`, and that it sits BETWEEN the message list and the composer.
13. Check `T-A` for `Persona 'Nova' failed to start.` at Warning.
14. Click **Teammates**, then Nova's tile; read her status label, dot colour and the reason line on the card.
15. Confirm the card offers a **Restart** button and the hint `Like saving an edit, this restarts the teammate, which clears what it remembers.`

**Pass if — all of these**

- The Room shows a bordered strip reading exactly `Nova is Offline: Failed to start 'definitely-not-node'.`
- The strip's element carries `member-health-alert` and `role="alert"`, positioned between the transcript and the composer.
- `T-A` logs `Persona 'Nova' failed to start.` at Warning with the exception.
- /teammates shows Nova as **Offline** with a red dot, the same reason line on the card, a **Restart** button and the quoted hint.

**Fail if — any of these**

- The failure appears only in the `T-A` console and the Room shows nothing -> the original defect is back: a crashed Agent and a thinking Agent look identical to the Human.
- The strip appears but its reason is blank -> a state is being reported without a reason, which also means the strip will start appearing for merely-unhealthy Agents (see STREAMINGTURN-01).
- The strip renders below the composer or above the transcript -> it is no longer the thing you read before typing.
- Nova reads **Degraded** rather than **Offline** -> a start failure is being reported as a mid-session degradation, which hides the fact that no session exists at all.

**Inconclusive if**

If Nova never reaches **Online** in step 7 even with a working `node`, this test cannot distinguish a deliberate break from a pre-existing one: read the reason on her card and fix that first. If the Model dropdown says `This agent advertises no models, so it will use its own default.`, the adapter is not installed - run `pwsh tools/acp/install.ps1` and retry.

> [!NOTE]
> FREE: no prompt is ever sent, so no tokens are spent - starting a session costs nothing. A second variant of the same behaviour: rename `tools\acp\node_modules` aside and restart instead; the reason should then read `Nova is Offline: No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.` Rename it back afterwards.

### STREAMINGTURN-22 — Restart from the Teammate card clears the failure and the Room's strip goes with it

**Free** · about 8 min

*Proves the one manual recovery path works for a Persona that never started, and that the Room clears without a reload.*

**Before you start**

- STREAMINGTURN-21 has left Nova Offline with the `Failed to start 'definitely-not-node'.` reason, and `T-A` is still running with that broken command.

**Steps**

1. Open a second browser tab on Nova's Room (`/rooms/<NOVAROOM>`), confirm the alert strip is visible there, and leave that tab open and untouched.
2. In the first tab click **Teammates**, then click Nova's tile.
3. Confirm the card shows **Offline**, the reason line, and a **Restart** button.
4. Click **Restart** and immediately read the button's label.
5. Watch the card's status line until it settles, and read the reason line.
6. Now fix the cause: press Ctrl+C in `T-A` and run `Remove-Item Env:Team__Acp__Command; dotnet run --project src/Huddle.App`.
7. Reload the first tab, open Nova's card again, and click **Restart** if she is not already coming up.
8. Watch the card's status line until it settles.
9. Switch to the second Room tab WITHOUT reloading it and read the strip area.
10. In `T-D` run: `Test-Path "src\Huddle.App\App_Data\work\Nova"`.

**Pass if — all of these**

- A **Restart** button is offered for a Persona that never started, not only for one that started and then failed.
- While the restart is in flight the button reads `Restarting…` and is disabled.
- With the command still broken, the restart fails again and the card still shows **Offline** with the reason - correct behaviour, not a failure of this test.
- Once the command is fixed, the card shows **Starting** and then **Online** with a green dot and NO reason line.
- The Room tab left open clears its alert strip without a manual reload.
- `Test-Path` prints True.

**Fail if — any of these**

- No **Restart** button is offered for a never-started Persona -> the one case the button exists for is unreachable, and the Human has no recovery short of restarting the app.
- **Restart** is offered on a healthy **Online** teammate -> the control invites a needless session restart, which clears what the teammate remembers.
- The Room's strip only clears after a manual page reload -> health changes are not reaching open Rooms, so a fixed Agent still reads as broken.
- The card jumps straight from Offline to Online with no **Starting** state -> a normally-launching Persona will flash red on every start, training the Human to ignore red dots.

**Inconclusive if**

If clicking Restart shows an error line at the top of the card, record it verbatim - with a broken command a failure is correct. Confirm `Team__Acp__Command` is unset in `T-A`'s environment before judging the final Online result.

> [!NOTE]
> FREE: a restart creates a session but takes no Turn, so no tokens are spent.

### STREAMINGTURN-23 — A stored Model the adapter does not advertise degrades the Persona but does not stop it

**💰 Spends money** · about 12 min

*Proves a stale stored Model is a warning, never a failure - it must not brick a Persona, and it must not be silent either.*

**Before you start**

- Nova exists with Model set to a Haiku option (from STREAMINGTURN-21).
- `sqlite3` is available on PATH, or a SQLite editor is installed.

**Steps**

1. Press Ctrl+C in `T-A` to stop the app - the database is open while it runs.
2. In `T-D` run: `sqlite3 src/Huddle.App/App_Data/team.db "select * from persona_models;"` and write down the stored model id for Nova.
3. Run: `sqlite3 src/Huddle.App/App_Data/team.db "update persona_models set model='claude-not-a-real-model' where persona_name='Nova';"`.
4. Run the select again and confirm the stored id is now `claude-not-a-real-model`.
5. In `T-A` run: `$env:Team__Acp__Enabled="true"; dotnet run --project src/Huddle.App`.
6. Reload the browser and open Nova's Room.
7. Read the strip between the transcript and the composer, word for word.
8. Click **Teammates**, click Nova's tile, and read the **Model** section and the status line.
9. Optional (costs one very short Haiku turn): go back to Nova's Room, type `@Nova say ok` and press Enter, then wait up to 30 seconds for a Draft and a reply.
10. Stop the app and restore the stored model: `sqlite3 src/Huddle.App/App_Data/team.db "update persona_models set model='<the id you wrote down>' where persona_name='Nova';"`, then restart the app.

**Pass if — all of these**

- The Room strip reads exactly `Nova is Degraded: The Model 'claude-not-a-real-model' is not in the Adapter's catalog; running on its default.`
- Nova is reported as running - she is NOT Offline - and the card's **Model** section shows the raw stored id.
- If step 9 was performed: she still replies when Mentioned.

**Fail if — any of these**

- Nova fails to start entirely -> a stale stored Model can brick a Persona, turning a warning into an outage.
- No strip and no reason anywhere -> the Persona is silently running on a different model than the card claims; this is the silent half of the defect.
- The reason names a category the code cannot know (a quota, a permissions problem) -> interface copy has drifted from what is actually known.

**Inconclusive if**

If `sqlite3` is not on PATH, do NOT improvise - record the test as inconclusive and note that it needs a SQLite client. If `persona_models` has no row for Nova, her Model was never stored: open her card, click **Edit**, choose the Haiku option, click **Save**, and start again.

> [!NOTE]
> FREE except for the optional step 9, which costs one very short Haiku turn at low effort. An EMPTY model catalog means 'unknown', never 'no models available', and must produce NO Degraded report at all - seeing this strip when the adapter is simply not installed is itself a defect. Reclassified as a paid test: its final verification step runs one short Haiku Turn. A test cannot be half-free, or filtering this script by cost stops being trustworthy.

### STREAMINGTURN-24 — Stop actually ends a real Turn: the Draft goes, no Message is posted, nothing is written

**💰 Spends money** · about 10 min

*Proves Stop does what it says against a real agent - the only place the cancellation path can be exercised end to end.*

**Before you start**

- `Team__Acp__Enabled=true` and Nova is **Online** with Model = Haiku and Effort = low.
- `tools\acp\node_modules` installed and the adapter authenticated.

**Steps**

1. Confirm on /teammates that Nova reads **Online** with a green dot and no reason line.
2. Open Nova's Room and note its id as NOVAROOM.
3. In `T-D` run: `Get-Content "src\Huddle.App\App_Data\rooms\<NOVAROOM>.jsonl" | Measure-Object -Line` and write down the count as BEFORE.
4. In the composer type exactly `@Nova count from 1 to 200, one number per line` and press Enter.
5. Watch for the Draft row to appear and start streaming numbers.
6. As soon as you can read a dozen or so numbers, click **Stop** on that Draft's row.
7. Watch the Room for 30 seconds without reloading.
8. Read the `T-A` console for new lines.
9. In `T-D` re-run the line count: it should be BEFORE plus one (your own prompt) and no more.
10. Click into the composer, type `@Nova say ok` and press Enter to confirm the Room is still usable.

**Pass if — all of these**

- The Draft row disappears within a second or two of the click.
- No Message is posted for the stopped Turn, and none appears during the following 30 seconds.
- The partial text is nowhere on the page after the Draft clears.
- The transcript gained only your own prompt line - no agent line for the stopped Turn.
- `T-A` logs, at Information, exactly `Persona 'Nova' turn in room <NOVAROOM> was stopped.`
- The follow-up `@Nova say ok` is answered normally.

**Fail if — any of these**

- The partial text is posted as a Message -> a cancelled Turn is being treated as postable, so stopping half a reply saves half a reply.
- The Draft freezes on screen instead of clearing -> the stream terminator is not written on the cancellation path, leaving the Human with a permanently blinking half-reply.
- The Agent keeps generating and a full reply lands seconds later -> the Stop never reached the Turn; the button is decorative.
- `T-A` logs `Persona 'Nova' failed to process a turn in room ...` instead of the `was stopped.` line -> the cancellation is being classified as a failure (see STREAMINGTURN-25).

**Inconclusive if**

If no Draft ever appears, the Turn failed before streaming: read `T-A` for a warning and treat that as STREAMINGTURN-29's territory, not a Stop defect. If Nova is not **Online**, fix that first and record this as not run.

> [!NOTE]
> COSTS MONEY: one partial Haiku turn at low effort - a few hundred output tokens if you stop within a second or two - plus one very short confirmation turn. Total well under a cent.

### STREAMINGTURN-25 — A stopped Turn is not a failure: no alert strip, no Degraded badge, no broken streak

**💰 Spends money** · about 12 min

*The single most important negative in this area: everywhere else in the codebase a cancellation means shutdown, so a regression shows up as 'stopping a Turn marks the Agent broken'.*

**Before you start**

- Nova **Online**, Model = Haiku, Effort = low.
- STREAMINGTURN-24 has passed.

**Steps**

1. Open Nova's Room.
2. Type `@Nova count from 1 to 200, one number per line` and press Enter; click **Stop** after a second or two.
3. Immediately look at the area between the transcript and the composer.
4. In devtools Elements, search for `member-health-alert` and record the count.
5. Click **Teammates** and read Nova's tile: label and dot colour; open her card and check for a reason line.
6. Confirm the card offers NO **Restart** button.
7. Return to the Room and repeat the prompt-and-Stop twice more, so three Turns in a row have been stopped.
8. After the third Stop, re-check the Room strip, the /teammates tile and the card.
9. Read `T-A` and count the `was stopped.` lines and any `failed to process a turn` warnings.
10. Type `@Nova say ok` and press Enter; confirm a normal reply.

**Pass if — all of these**

- After every Stop the Room shows NO `member-health-alert` strip, no red text and no budget prompt.
- Nova's tile and card read **Online** with a green dot and no reason line throughout, including after three consecutive Stops.
- No **Restart** button is offered, because nothing is wrong.
- `T-A` shows exactly three `Persona 'Nova' turn in room <id> was stopped.` lines at Information and ZERO `failed to process a turn` warnings.
- The follow-up message is answered normally.

**Fail if — any of these**

- An alert strip or a Degraded/Offline badge appears after a Stop -> the Human's own action is being reported as a fault, and the strip that should mean 'something broke' now fires on normal use.
- After the third Stop the reason line mentions consecutive failures -> stopped Turns are counting toward the failure escalation, so a Human who stops three Turns is told their Agent is unreliable.
- `T-A` logs a `failed to process a turn` warning for a stopped Turn -> the cancellation is taking the failure path even if the UI happens to look right today.

**Inconclusive if**

If a strip appears whose reason is clearly about something else (a spent token Budget, a refused post), that is a different behaviour - record the exact wording and judge it against STREAMINGTURN-28 or 29 rather than failing this test.

> [!NOTE]
> COSTS MONEY: three partial Haiku turns at low effort plus one short confirmation turn. Stop each one quickly; total spend is a fraction of a cent.

### STREAMINGTURN-26 — Stop means this Agent now: everything queued behind the live Turn is discarded too

**💰 Spends money** · about 10 min

*Proves one Stop does not leave a backlog grinding through afterwards, which would read as a broken button and spend money the Human asked not to spend.*

**Before you start**

- Nova **Online**, Model = Haiku, Effort = low.

**Steps**

1. Open Nova's Room and note its id as NOVAROOM.
2. In `T-D` run the transcript line count and record it as BEFORE.
3. Type `@Nova count from 1 to 300, one number per line` and press Enter.
4. Without waiting, type `@Nova what is 2 plus 2` and press Enter.
5. Type `@Nova name three colours` and press Enter.
6. Type `@Nova name three fruits` and press Enter.
7. Wait until the first Draft is visibly streaming, then click **Stop** on it once.
8. Watch the Room for a full 60 seconds without typing anything else.
9. Count how many Draft rows appear during that minute.
10. Re-run the transcript line count in `T-D` and compare with BEFORE plus your four prompt lines.
11. Read `T-A` for `was stopped.` and for any further turn activity for Nova.

**Pass if — all of these**

- The live Draft ends and NO further Draft appears for the three queued prompts during the 60 seconds.
- The Room goes quiet rather than working through the backlog.
- The transcript gains only your four Human Messages - no agent replies.
- `T-A` shows one `Persona 'Nova' turn in room <id> was stopped.` and no further turn activity for Nova.

**Fail if — any of these**

- One queued Turn after another runs anyway after the single Stop -> the Stop only ended the Turn in flight, so the Human clicks Stop and watches the Agent carry on; it reads as a broken button and spends money on work that was cancelled.
- Several Drafts appear in sequence after the single click -> the same defect, visibly.
- The transcript gains agent replies -> the discarded prompts were answered and persisted.

**Inconclusive if**

If the composer shows the budget pause prompt, the Room Budget intervened and the queue was never built - click **Continue**, confirm `Team__AgentMessageBudget` is at its default, and retry. If only one prompt ever produced a Draft, you may have typed too slowly and the Turns ran one at a time; retry, typing all four within a couple of seconds.

> [!NOTE]
> COSTS MONEY: one partial Haiku turn only. The queued prompts are discarded WITHOUT being sent to the model, so they cost nothing - and that is exactly what this test proves.

### STREAMINGTURN-27 — Stopping an Agent stops it in every Room, not just the one you clicked in

**💰 Spends money** · about 12 min

*Confirms the documented cross-Room reach of Stop so a tester does not file it, while catching the opposite real defect - a live Turn surviving because the stop was matched against a Room.*

**Before you start**

- Nova **Online**, Model = Haiku, Effort = low.
- Nova is a Member of two Rooms.

**Steps**

1. Click **New chat** in the sidebar, tick `Nova` and one other agent (e.g. `echo`), and click **Start chat**. Call this ROOM B; Nova's own direct Room is ROOM A.
2. Open ROOM B, type `@Nova count from 1 to 300, one number per line` and press Enter. Confirm a Draft starts streaming there.
3. Switch to ROOM A and type `@Nova list ten countries` and press Enter.
4. Switch back to ROOM B and click **Stop** on Nova's Draft.
5. Watch ROOM B for 20 seconds.
6. Switch to ROOM A and watch for 40 seconds.
7. Read `T-A` and note which room id the `was stopped.` line names.
8. In `T-D` check both rooms' .jsonl files for new agent lines.

**Pass if — all of these**

- ROOM B's Draft ends immediately on the click.
- ROOM A's queued Turn does NOT run - no Draft appears there either.
- `T-A` prints one `Persona 'Nova' turn in room <id> was stopped.`, naming the Room you clicked in; that this is not necessarily the Room the Turn was running in is expected.
- Neither room's transcript gains an agent reply.

**Fail if — any of these**

- The Turn in ROOM B survives the Stop because the Room ids did not match -> the stop envelope's roomId is being used as a selector rather than as a record of where the Human asked, so Stop will silently fail whenever the Turn is running in another Room.
- An agent reply is persisted in either Room after the Stop -> cancelled work was completed and saved.

**Inconclusive if**

If ROOM A's prompt never queued because Nova answered it before you clicked Stop, the test did not set up its condition - retry with a longer first prompt. If `echo` is not offered in New chat, pick any other connected agent; the second agent only exists to make it a Group Room.

> [!NOTE]
> COSTS MONEY: one partial Haiku turn. Stopping Nova in every Room at once is a documented limit - do NOT file 'Stop killed my other conversation'.

### STREAMINGTURN-28 — A spent per-Persona token Budget reads as Degraded, and a Human Message clears it

**💰 Spends money** · about 10 min

*Proves the token Budget is no longer log-only: it reaches the browser, in interface copy, in the short window before the Human's next Message resets it.*

**Before you start**

- Nova exists with Model = Haiku, Effort = low.

**Steps**

1. Press Ctrl+C in `T-A`. In the same window run: `$env:Team__Acp__Enabled="true"; $env:Team__Acp__TokenBudget="1"; dotnet run --project src/Huddle.App`.
2. Wait for Nova to reach **Online** on /teammates.
3. Open Nova's Room, type `@Nova say ok` and press Enter. Wait for her reply to land.
4. Type `@Nova say ok again` and press Enter.
5. Watch the Room for 15 seconds and note whether any Draft appears.
6. Read the strip between the transcript and the composer, word for word, and screenshot it before typing anything else.
7. Check `T-A` for `Persona 'Nova' has spent its token budget of 1 and is taking no more turns until a human speaks to it.` at Warning.
8. Type `@Nova one more time` and press Enter, then watch for a Draft and a reply.
9. Press Ctrl+C in `T-A` and restart without the variable: `Remove-Item Env:Team__Acp__TokenBudget; dotnet run --project src/Huddle.App`.

**Pass if — all of these**

- The second prompt produces NO Draft at all.
- The Room shows the strip `Nova is Degraded: The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.`
- `T-A` logs the quoted Warning line.
- The wording avoids vocabulary this product does not use - no 'rate limit', 'quota' or 'cap'.
- The next Human Message resets the counter, so that message IS answered.

**Fail if — any of these**

- The Budget is spent with no signal in the browser -> the state only reaches the log, which is the defect this surface closed: the Room simply stops answering with no explanation.
- The strip uses 'rate limit', 'quota' or 'cap' -> interface copy has drifted from the product's language, which is binding for text the Human reads.
- Nova is reported **Offline** rather than **Degraded** -> a spending pause is being presented as a dead Agent.
- The following Human Message is also ignored -> the counter is not reset by a Human Message, and the Persona is mute until a restart.

**Inconclusive if**

The window in which the strip is visible is short by design - if you missed it because you typed again too quickly, repeat from step 4 and screenshot before typing. If the first prompt produces no reply at all, the Budget was already spent by an earlier run; restart the app to reset the counter.

> [!NOTE]
> COSTS MONEY: one short Haiku turn at low effort plus one more to prove the reset - a few dozen tokens.

### STREAMINGTURN-29 — A Turn that fails mid-flight reports Degraded in the Adapter's own words, and escalates on the third failure in a row

**💰 Spends money** · about 15 min

*Proves a provider-side failure reaches the Human as a reason rather than a silence, is never dressed up as a category the code cannot know, and escalates only on repetition.*

**Before you start**

- Nova **Online**, Model = Haiku, Effort = low.
- A way to break the provider deliberately: disconnect the machine's network, or invalidate the adapter's stored credentials.

**Steps**

1. Open Nova's Room and confirm no alert strip is present.
2. Break the provider: disconnect the network adapter, or revoke/expire the adapter's credentials, leaving the app running.
3. Type `@Nova say ok` and press Enter.
4. Watch: the Draft either never starts or cuts out. Read the strip that appears, word for word, and write it down.
5. In devtools Elements confirm the element carries `member-health-alert` and `role="alert"`.
6. Type `@Nova say ok` again and press Enter. Read the strip again.
7. Type `@Nova say ok` a third time. Read the strip again, carefully.
8. Check `T-A` for `Persona 'Nova' failed to process a turn in room <id>.` at Warning after each attempt.
9. Restore the provider (reconnect the network / re-authenticate).
10. Type `@Nova say ok` once more and watch the Room WITHOUT reloading.

**Pass if — all of these**

- After the first failure the strip reads `Nova is Degraded: A Turn failed — <the adapter's own wording>`.
- After the second failure the wording is still the `A Turn failed — ...` form.
- After the THIRD consecutive failure the line changes to `Nova is Degraded: 3 consecutive Turns have failed; this is unlikely to be transient — <adapter wording>`.
- Nova stays **Degraded** throughout and is never reported **Offline**.
- `T-A` logs one `failed to process a turn` Warning per attempt.
- After the provider is restored and a Turn succeeds, the strip clears with no page reload.

**Fail if — any of these**

- The strip claims to know WHICH failure it is - naming a quota, a network outage or expired credentials as a category -> all three arrive as one exception type carrying Adapter-authored wording that nothing here parses, so any such claim is invented.
- Nova is reported **Offline** -> the session and the pipe may both be healthy while the provider refuses; Offline is a claim that cannot be supported.
- The escalation fires on the FIRST failure -> a single transient blip is presented as a persistent fault.
- The escalation never fires after three in a row -> the streak counter is not incrementing, so a genuinely broken provider reads as three unrelated blips.
- A successful Turn afterwards leaves the strip in place -> the streak is not reset and the Agent stays marked broken after it recovered.
- Nothing appears in the browser at all -> the failure is log-only, the silent case this surface exists to close.

**Inconclusive if**

If pulling the network also kills the browser's connection to the app, do not judge the strip - use credential invalidation instead, or an adapter configuration that fails at prompt time. If you cannot break the provider cleanly, record this test as not run; do NOT simulate it by killing the adapter process, which is STREAMINGTURN-30 and a different code path.

> [!NOTE]
> COSTS MONEY: three prompt attempts. Each is refused before the model answers, so token spend is effectively nil, but it does require a live agent and real prompts. Do NOT file the exact adapter wording as a defect - it belongs to the adapter and will change.

### STREAMINGTURN-30 — Killing the adapter process mid-Turn clears the Draft and says so, instead of deafening the Agent forever

**💰 Spends money** · about 15 min

*The headline regression for this area, and it was live once: the Agent went permanently deaf in every Room with the pipe still open, the tile still green, the Draft frozen on screen and nothing logged anywhere.*

**Before you start**

- Nova **Online**, Model = Haiku, Effort = low.
- `O-ADAPTERS-LIST` in `T-D`, or Task Manager, available to find the Adapter's child process.

**Steps**

1. In `T-D` run `O-ADAPTERS-LIST` and note the process that started when Nova came online. (Or open Task Manager's **Details** tab and sort by **Name**.)
2. Open Nova's Room and note its id as NOVAROOM. In `T-D` record the transcript line count as BEFORE.
3. Type `@Nova count from 1 to 300, one number per line` and press Enter.
4. As soon as the Draft is visibly streaming, end that `node` process (Task Manager **End task**, or `Stop-Process -Id <id> -Force`).
5. Switch back to the browser immediately and watch the Room for 30 seconds WITHOUT reloading.
6. Record whether the Draft row cleared or froze.
7. Read the strip between the transcript and the composer, word for word.
8. Click **Teammates** and read Nova's tile label and dot colour.
9. Read `T-A` for warnings mentioning Nova.
10. Return to the Room, type `@Nova are you there` and press Enter, and watch for 30 seconds.
11. Open Nova's card on /teammates and click **Restart**; wait for **Online**.
12. Type `@Nova say ok` and confirm she answers.
13. In `T-D` re-check the transcript line count against BEFORE plus your own prompt lines.

**Pass if — all of these**

- The Draft row DISAPPEARS rather than freezing, within a few seconds of the kill.
- An alert strip appears in the Room. Any ONE of these reasons is correct: `Nova is Offline: The Adapter process disconnected.`, `Nova is Offline: Its event reader ended unexpectedly: <message>`, or `Nova is Degraded: A Turn failed — <message>`.
- Nova's tile is no longer green/Online.
- `T-A` logs at Warning `Persona 'Nova' agent event stream ended unexpectedly.` and/or `Persona 'Nova' failed to process a turn in room <id>.`
- The transcript gains no agent line for the killed Turn.
- Clicking **Restart** brings Nova back to **Online**, and she answers the next message.

**Fail if — any of these**

- Any combination of a frozen Draft, a still-green tile, and no strip -> THE headline regression: the Agent is deaf in every Room with the pipe still open and nothing said anywhere. File immediately.
- The Draft freezes on screen with the caret still blinking -> the stream terminator is not written on this failure path.
- Nova's tile stays **Online** -> pipe liveness alone is being used to judge health, which cannot tell a deaf Agent from a healthy one.
- Messages sent afterwards are swallowed forever and **Restart** does not recover her -> the only manual recovery path is broken and the app must be restarted to use that Persona.
- A partial reply is persisted -> a failed Turn produced a Message.

**Inconclusive if**

If you cannot identify which `node.exe` belongs to Nova, do not kill one at random: close other Node applications, restart the app so only Nova's adapter is running, and try again. If the Draft had already finished before you killed the process, retry with a longer prompt.

> [!NOTE]
> COSTS MONEY: one partial Haiku turn plus one short confirmation turn. WHICH of the three reasons lands is a race between the Turn's own catch and the event reader's finally - all three are correct and none is a defect. Only a frozen Draft or a still-green tile is.

---

Back to [the manual test script](../manual-tests.md).
