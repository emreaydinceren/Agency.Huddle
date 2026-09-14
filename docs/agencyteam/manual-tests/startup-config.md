# Startup, configuration and first-run state

Prove that Agency.Huddle launches on http://localhost:5100 with the state a first-time user is promised — two demo Rooms, an App_Data directory built from nothing, a styled two-column shell — and that every `Team:` configuration key and `Team__*` environment variable that a tester can reach actually changes what the browser shows. The area exists because nearly everything in it is a startup side effect (a directory created, a pipe bound, a Room seeded, a Persona process spawned) that no unit test observes end to end, and because this repo's most expensive failures here are SILENT: a single-underscore environment variable that binds to nothing, a demo agent that never dials the pipe, a stale `Logging:LogLevel` key that deletes the console evidence other tests rely on, and an `@Assets` key that 404s a stylesheet with no build warning, no startup error and no log line. Several tests below exist purely as controls for those silences.

**34 tests** · 32 free, 2 paid 💰 · about 3.9 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Before launching, confirm nothing else holds port 5100: `Get-NetTCPConnection -LocalPort 5100 -ErrorAction SilentlyContinue` must print nothing, and `Get-Process dotnet -ErrorAction SilentlyContinue` must show no leftover run of this app. A stale instance on 5100 is the commonest way to 'test' a change that was never deployed — the browser cheerfully shows the OLD process.
2. Know the launch profile this area keeps probing. `dotnet run --project src/Huddle.App` picks up the only profile (`http`), which sets `ASPNETCORE_ENVIRONMENT=Development`, `applicationUrl http://localhost:5100` and `launchBrowser true`. The `--urls` argument is therefore redundant but harmless.
3. Know the environment-variable form, because this area is mostly about it: the separator is a DOUBLE underscore, and the variable must be set in the same window that then runs `dotnet run`. Precedence is environment variable, then `appsettings.Development.json`, then `appsettings.json`.
4. AFTER EVERY ENVIRONMENT-VARIABLE TEST, clear what you set before moving on, or the next test inherits it and reports a false result.
5. For the ACP tests at the end only: `node --version` must print a version, `Test-Path tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js` must be True (if not, run `pwsh tools\acp\install.ps1`), and the Claude login requirement in [§0.1](../manual-tests.md#01-what-you-need) must be met. Tests that need this say so in their preconditions.

## Tests

### STARTUPCONFIG-01 — The documented launch command serves the styled app shell on http://localhost:5100

**Free** · about 5 min

*Proves the app starts in Development on the documented port and renders its two-column shell, and establishes which process you are actually looking at for every later test.*

**Before you start**

- `dotnet build Huddle.slnx` reported 0 warnings.
- Nothing is listening on port 5100 and no leftover `dotnet` process of this app is running.

**Steps**

1. In the pwsh window at `E:\Repos\Huddle`, run `Get-ChildItem Env:Team__*` and confirm it prints nothing. If it prints anything, clear each with `Remove-Item Env:<name>` before continuing.
2. Run `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Read the console output as it appears and find the lines beginning `Now listening on:` and `Hosting environment:`.
4. If a browser did not open by itself, open one and navigate to http://localhost:5100.
5. Look at the left-hand sidebar and read its controls top to bottom.
6. Look at the main column on the right.

**Pass if — all of these**

- The console prints `Now listening on: http://localhost:5100`.
- The console prints `Hosting environment: Development`.
- The page renders as TWO columns: a narrow sidebar on the left and a wide main column on the right.
- The sidebar contains, in this order: a button labelled exactly `New chat`, a list of rooms, a link labelled exactly `Teammates`, a link labelled exactly `Settings`.
- The main column shows a Room: a large heading with the Room name and a comma-separated members line directly under it.

**Fail if — any of these**

- The console throws an address-in-use / `Failed to bind to address` exception and the process exits -> something else already holds 5100; the browser is showing a DIFFERENT process and every later result would be about that one. Stop it and restart.
- The console prints `Hosting environment: Production` -> the launch profile was bypassed (you ran the built DLL, or passed `--no-launch-profile`); ACP will be off and the port will be 5000. Relaunch with the documented command.
- The page renders as unstyled black-on-white text with no sidebar layout -> a stylesheet did not load. This is a real, documented regression shape, not a rendering hiccup. Run STARTUPCONFIG-02 immediately and file from there.
- The sidebar link text reads anything other than `Teammates` and `Settings` -> navigation copy has drifted from the shipped labels.

**Inconclusive if**

If the browser shows the page but the console window you started shows no `Now listening on:` line at all, you are looking at a different (older) instance. Close every browser tab, stop every `dotnet` process (`Get-Process dotnet | Stop-Process`), and start again from step 2. Do not record a result until the console line and the page agree.

> [!NOTE]
> Leave this instance running — tests 02 through 09 all use it.

### STARTUPCONFIG-02 — Every stylesheet in the document head returns 200 — the silent @Assets regression

**Free** · about 5 min

*Catches this repo's most expensive documented silent failure: an unresolved `@Assets` key renders as an ordinary href that 404s, with no build warning, no startup error and no log line, so the only witness is an HTTP round-trip.*

**Before you start**

- The app from STARTUPCONFIG-01 is running and http://localhost:5100 is open.

**Steps**

1. Press F12 to open DevTools and select the **Network** tab.
2. Tick **Disable cache**, then press Ctrl+F5 to hard-reload the page.
3. Set the Network filter to **CSS**.
4. Read the Status column for every row.
5. Click each CSS row and read its full Request URL.
6. Close DevTools. Scroll the page all the way to the very bottom of the viewport and look for a strip reading `An unhandled error has occurred.` with a `Reload` link and a `🗙` character.

**Pass if — all of these**

- Exactly three application stylesheets are requested and all three return status 200: one whose name starts `theme`, one whose name starts `app`, and one whose name starts `Huddle.App.styles`.
- Each of those three request URLs is FINGERPRINTED — the filename carries a hash segment (for example `theme.ce2n94aiaf.css`), and is therefore not byte-identical to the key written in the markup (`theme.css`).
- No `An unhandled error has occurred.` strip is visible anywhere on the page, top or bottom.
- The layout is genuinely styled: two columns, a sidebar with a visible background, message rows each showing a sender name and a time on a meta line.

**Fail if — any of these**

- Any CSS request returns 404 -> an `@Assets["…"]` key in `Components/App.razor` no longer matches a real asset; it was emitted verbatim as a plain href instead of throwing. This is the documented month-long regression; file it with the exact 404 URL.
- A CSS request URL is NOT fingerprinted and exactly equals the key in the markup (e.g. `/theme.css`) -> the same unresolved-key failure, even if the server happens to answer 200 for it.
- The error strip is visible at the bottom of the page on a fresh load with no interaction -> the page is running without `app.css`, whose `position: fixed` is what would normally float that strip; a stylesheet is missing.
- Fewer than three stylesheets are requested -> a `<link>` was dropped from the head.

**Inconclusive if**

If DevTools shows CSS rows served `(from disk cache)` with no status code, you did not disable the cache. Re-tick **Disable cache** and hard-reload before judging. If your browser hides third-party/extension CSS rows, count only the three named above and ignore the rest.

> [!NOTE]
> This behaviour genuinely cannot be judged without a browser network panel — 'the page looks fine' is not evidence, because a partially-loaded shell still renders text.

### STARTUPCONFIG-03 — Two demo Rooms, echo and alpha, exist at startup with no user action

**Free** · about 5 min

*Proves the demo agents dialled the pipe and seeded the two Rooms a first-time user is promised, and that `/` redirects to the first Room.*

**Before you start**

- The app is running with no `Team__*` environment variables set.
- `Team:DemoAgent:Names` left at the shipped `["echo", "alpha"]`.

**Steps**

1. Navigate the browser to http://localhost:5100 (the bare root, no path).
2. Wait up to 10 seconds, then read the room list in the sidebar.
3. Read the browser's address bar.
4. Read the large heading at the top of the main column, and the line directly underneath it.
5. Switch to the console window and search the output for lines containing `Demo agent`.

**Pass if — all of these**

- The sidebar room list shows exactly two entries, reading `echo` then `alpha`, in that order.
- The address bar has changed from `http://localhost:5100/` to `http://localhost:5100/rooms/<some id>`.
- The main column's heading reads exactly `echo`.
- The line under the heading reads exactly `You, echo`.
- The console contains `Demo agent echo connected.` and `Demo agent alpha connected.`

**Fail if — any of these**

- The main column shows `No rooms yet. Start an agent to create one.` and never changes -> the demo agents failed to connect to the pipe. The page gives NO other signal; the console is the only witness. Look for `Demo agent {name} failed to connect to pipe team after 30 attempts.`
- Three or more Rooms appear on a clean App_Data, or `echo` appears twice -> the configuration binder APPENDED bound array elements to an already-populated Names collection instead of replacing it. This is the exact hazard `TeamOptions.DemoAgentOptions.Names` is written to avoid.
- The address bar stays at `/` with a Room clearly present in the sidebar -> the redirect in `Chat.razor`'s parameter handling broke.
- The members line reads anything other than `You, echo` (for example just `echo`) -> the Human was not seeded as a Room member.

**Inconclusive if**

The Rooms may take a second or two to appear — the demo agents dial the pipe as ordinary clients and retry up to 30 times at 200 ms apart. Only judge after 10 seconds. If `App_Data` already existed from an earlier session with different demo names, run `P-RESET-ALL` and relaunch before judging the Room count.

> [!NOTE]
> Optional disk oracle: `Get-ChildItem -Recurse src\Huddle.App\App_Data` — and, if `sqlite3` is available, `sqlite3 src\Huddle.App\App_Data\team.db "SELECT name,kind FROM users; SELECT name FROM rooms;"` should show one `human` row, two `agent` rows, and two Rooms.

### STARTUPCONFIG-04 — A demo agent answers only when @-mentioned, and answers in bold

**Free** · about 5 min

*Proves the whole free message round trip — composer to pipe to transcript — and pins the single result testers most often misfile as a bug.*

**Before you start**

- Demo agents connected (STARTUPCONFIG-03 passed).
- The `echo` Room is open.

**Steps**

1. Click `echo` in the sidebar room list.
2. Read the placeholder text in the message box at the bottom of the main column.
3. Click into the message box, type exactly `hi @echo` and press Enter.
4. Watch the message area for up to 10 seconds.
5. Now type exactly `hi` (no mention) and press Enter.
6. Watch the message area for a further 10 seconds.
7. Run `Get-ChildItem src\Huddle.App\App_Data\rooms` in a SECOND pwsh window (do not disturb the running app).

**Pass if — all of these**

- The message box placeholder reads exactly `Message… (/invite @agent)`.
- After step 3, a message appears immediately with sender `You` and body `hi @echo`.
- Within a few seconds a second message appears with sender `echo`, whose body renders `echo:` in BOLD followed by `hi echo` — rendered bold text, not literal `**` asterisks.
- The `echo` reply contains NO `@` character.
- After step 5, the `hi` message appears with sender `You` and NOTHING answers it, for the full 10 seconds.
- A `rooms` folder now exists under `App_Data` containing a `.jsonl` file.

**Fail if — any of these**

- Nothing answers `hi @echo` -> the mention was not parsed, or the demo agent's read loop is dead. Check the console for `Demo agent echo stopped unexpectedly.`
- The reply renders as literal `**echo:** hi echo` with visible asterisks -> the Markdown render path for a posted Message is broken.
- The reply still contains an `@` -> the demo agent's `@`-stripping loop-safety step was lost; a reply that carries a mention can re-trigger another agent.
- Something DOES answer the plain `hi` -> only a real Persona replies without a mention in a two-Member Room; a demo agent answering unmentioned means the mention gate was bypassed.

**Inconclusive if**

Silence after plain `hi` is CORRECT and must never be filed as a bug — `DemoAgentHost` answers only `MessagePosted { Mentioned: true }`. Note that `docs/why-agency-huddle.md` line 209 wrongly tells you to type plain `hi`; `docs/AgencyTeam.md` line 262 is the correct instruction. If you typed `hi @echo` and got no reply, first re-check the console for a `connected.` line before filing — an agent that never connected produces exactly this symptom.

> [!NOTE]
> Shift+Enter inserts a newline instead of sending; Enter alone sends.

### STARTUPCONFIG-05 — The demo agent streams a Draft before posting — the real streaming path, free

**Free** · about 5 min

*Proves the Draft rendering path (the same one a real, paid Persona uses) works end to end at zero token cost, including the Stop control and the hand-off to the finished Message.*

**Before you start**

- The `echo` Room is open and demo agents are connected.

**Steps**

1. Position the browser so the bottom of the message area is fully visible.
2. Type exactly `hi @echo please write this out slowly` and press Enter.
3. Watch the message area continuously, without clicking anything, for the next 5 seconds.
4. Note whether a row appears with a blinking caret before the final message settles.
5. Look for a button beside the streaming row's sender name.
6. After the exchange settles, count the message rows: there should be exactly the Human message and one agent message, with no leftover streaming row.

**Pass if — all of these**

- Before the final message settles, a row appears with sender `echo`, a blinking caret at the end of its text, and a button labelled exactly `Stop` next to the sender name.
- That row's text grows in visible steps (three chunks, roughly 40 ms apart) rather than appearing all at once.
- The streaming row is then REPLACED by a finished message whose `echo:` prefix renders in bold.
- No streaming row, caret or `Stop` button remains after the finished message arrives.
- No `.jsonl` line is written for the Draft itself — the transcript file gains exactly one line for the finished reply.

**Fail if — any of these**

- A streaming row with text and caret remains on screen after the real message arrives -> the stream terminator or the message-id correlation broke; a Draft is outliving its Turn.
- The streaming row renders formatted Markdown (bold `echo:`) WHILE streaming -> a Draft is deliberately plain text until it completes; rendering it through the Markdown path mid-stream is wrong.
- No `Stop` button appears beside the streaming row -> the Human has lost the only control that ends a Turn in progress.
- The reply appears in one jump with no intermediate growth -> the delta stream was collapsed or bypassed.

**Inconclusive if**

The whole stream takes well under a second on localhost — if you blink you will miss it. If you saw only the finished message, repeat with a longer prompt (the chunking splits the reply into three, so a longer echoed text streams longer) before judging. A Draft lives in memory only: if the app restarted mid-stream, the Draft is simply gone and that is a documented limit, not a result.

> [!NOTE]
> This is the free stand-in for the paid streaming path. Do not spend money to test streaming.

### STARTUPCONFIG-06 — Every navigation surface is reachable from a cold start

**Free** · about 6 min

*Proves the shell's four navigation controls all resolve, with the exact labels and empty-state copy a first-time user sees.*

**Before you start**

- The app is running with defaults and at least one Room exists.

**Steps**

1. Click `New chat` in the sidebar.
2. Read the panel that opens: note whether it lists agents with checkboxes and a coloured dot each, and whether a button is present at the bottom.
3. Without ticking anything, try to click the `Start chat` button.
4. Tick the checkbox beside `echo`.
5. Try the `Start chat` button again — note whether it is now clickable. Then click `New chat` again to close the panel WITHOUT starting a chat.
6. Click `Teammates` in the sidebar and read the address bar and the page heading.
7. Click `Settings` in the sidebar and read the address bar and the page heading.
8. Click a Room in the sidebar room list and confirm the main column shows that Room.

**Pass if — all of these**

- The `New chat` panel lists `echo` and `alpha`, each with a checkbox and a small status dot.
- With nothing ticked, the `Start chat` button is present but DISABLED (clicking does nothing).
- With `echo` ticked, `Start chat` becomes enabled.
- `Teammates` navigates to `/teammates` and the page heading reads exactly `Teammates`.
- `Settings` navigates to `/settings` and the page heading reads exactly `Settings`.
- Clicking a Room navigates to `/rooms/<id>` and the main column heading is that Room's name.

**Fail if — any of these**

- `Start chat` is clickable with nothing ticked -> the selection guard is gone and an empty Room can be created.
- `Teammates` or `Settings` leads to a blank page or an error -> the route was dropped from `Routes.razor` or the page throws on load.
- A sidebar link's text differs from `Teammates` / `Settings` -> shipped navigation copy has drifted.

**Inconclusive if**

If the `New chat` panel says `No agents have connected yet…` instead of listing agents, the demo agents did not connect — that is STARTUPCONFIG-03's result, not this test's. Fix that first and re-run; record this test as inconclusive meanwhile.

> [!NOTE]
> Do not actually click `Start chat` here — STARTUPCONFIG-07 owns that path and needs a known Room count first.

### STARTUPCONFIG-07 — Start chat on one known agent reuses its Room; two agents create one new named Room

**Free** · about 6 min

*Proves the Room-reuse rule that prevents a duplicate two-Member Room appearing every time the Human starts a chat with an agent that already has one.*

**Before you start**

- The app is running with defaults, and exactly two Rooms (`echo`, `alpha`) exist.

**Steps**

1. Count the entries in the sidebar room list and write the number down (expected: 2).
2. Click `New chat`, tick ONLY `echo`, and click `Start chat`.
3. Read the address bar and the main column heading, then count the sidebar room list again.
4. Click `New chat`, tick BOTH `echo` and `alpha`, and click `Start chat`.
5. Read the main column heading and count the sidebar room list again.
6. Click `New chat`, tick BOTH `echo` and `alpha` again, and click `Start chat` once more.
7. Count the sidebar room list a final time.

**Pass if — all of these**

- After step 2, the room list still shows 2 entries — no new Room was created.
- After step 2, the browser lands on the EXISTING `echo` Room, and its transcript still contains the messages from STARTUPCONFIG-04.
- After step 4, exactly one new Room exists (3 total) and its heading reads `echo, alpha`.
- After step 6, the count behaves consistently with step 4's rule — record exactly what it does.

**Fail if — any of these**

- Step 2 produces a SECOND Room also named `echo` (3 entries) -> single-agent selection stopped routing through the exact-membership lookup; every `Start chat` will now mint duplicates.
- Step 2 lands on an empty transcript rather than the existing `echo` history -> a new Room was created even though the sidebar count looks right; check the Room id in the address bar against the original.
- The two-agent Room's heading is not `echo, alpha` -> the multi-member Room naming rule changed.

**Inconclusive if**

If you have already created extra Rooms by hand in earlier exploration, the counts will not match. Run `P-RESET-ALL`, relaunch, and start this test from step 1. If step 6's behaviour is ambiguous, record it verbatim as an observation rather than a verdict — the reuse rule is defined for single-agent selection; multi-agent Room reuse is not specified here.

> [!NOTE]
> Optional oracle if `sqlite3` is available: `sqlite3 src\Huddle.App\App_Data\team.db "SELECT COUNT(*) FROM rooms;"` before and after each Start chat.

### STARTUPCONFIG-08 — /teammates lists Personas only — demo agents and pipe clients never appear there

**Free** · about 4 min

*Pins the category boundary that is most often misread as a bug: a Teammate tile comes from a Persona file, an Agent comes from anything that said hello on the pipe.*

**Before you start**

- Demo agents are connected and visibly online in the sidebar.
- `src\Huddle.App\App_Data\Teams` is empty (`Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams` prints nothing).

**Steps**

1. Confirm the sidebar shows the `echo` and `alpha` Rooms.
2. Click `Teammates` in the sidebar.
3. Read the page heading, the button beside it, the intro paragraph, and whatever appears below.
4. Scan the whole page for the words `echo` or `alpha`.

**Pass if — all of these**

- The heading reads exactly `Teammates` and a button beside it reads exactly `New teammate`.
- The intro paragraph begins `A Persona is a Markdown file describing how one teammate should behave`.
- The page shows exactly `No Personas yet. Choose New teammate to add one.`
- Neither `echo` nor `alpha` appears anywhere on the page.

**Fail if — any of these**

- `echo` or `alpha` appears as a Teammate tile -> pipe-connected Agents are leaking into the Persona list; the two concepts have been conflated.
- The page shows tiles despite `App_Data\Teams` being empty -> the Persona index is reading somewhere other than the configured Teams directory (check for a leftover `App_Data\Teams` legacy folder being read, which it must not be).

**Inconclusive if**

Expecting `echo` and `alpha` here is a category error, not a defect — do not file it. If `App_Data\Teams` is NOT empty because an earlier test left a file there, remove that file (or note it) before judging the empty-state copy.

> [!NOTE]
> `App_Data\Teams\` is a legacy folder from before the Teams rename and is never read by the app.

### STARTUPCONFIG-09 — Settings' Hooks tab on a first run says hooks.json is absent, and offers nothing to save or reset

**Free** · about 8 min

*Proves the app never creates its override file until the Human saves, that Save/Reset are correctly disabled with nothing edited, and that the tab route falls back rather than 404ing. Also establishes /settings as the quickest oracle for which DataDir is live.*

**Before you start**

- No `hooks.json` exists: `Test-Path src\Huddle.App\App_Data\hooks.json` returns False. If it returns True, run `P-RESET-ALL` and relaunch.

**Steps**

1. In `T-B` run `Test-Path src\Huddle.App\App_Data\hooks.json` and note the result.
2. In the browser click `Settings` in the sidebar.
3. Read the page heading and the two tab buttons in the tab rail.
4. Read the paragraph that names where overrides are stored, and note the absolute path it prints.
5. Try to click the `Save` button at the bottom of the form.
6. Try to click the `Reset all to defaults` button in the header.
7. Click the `Hooks` tab button and read the address bar.
8. Navigate the browser directly to http://localhost:5100/settings/nonsense and read what renders.
9. Go back to `/settings`, edit ANY one text field on the Hooks tab (add a single character), and look at the `Save` button again.
10. Click `Save`. Then in the second pwsh window run `Test-Path src\Huddle.App\App_Data\hooks.json` again.

**Pass if — all of these**

- Step 1 returns False — the file does not exist before any save.
- The heading reads exactly `Settings`; the tab rail holds exactly two buttons, `Hooks` and `Appearance`, with `Hooks` active by default.
- The overrides paragraph contains the sentence `The file does not exist until the first time you save here, so it being absent is expected, not a bug.`
- The absolute path printed in that paragraph ends `\src\Huddle.App\App_Data\hooks.json` — matching the live DataDir.
- With nothing edited, `Save` is disabled and `Reset all to defaults` is disabled.
- Clicking the `Hooks` tab changes the address bar to `/settings/hooks`.
- `/settings/nonsense` renders the Hooks tab rather than a 404 or an error page.
- After one edit, `Save` becomes enabled.
- After clicking `Save`, step 10 returns True.

**Fail if — any of these**

- Step 1 returns True on a clean App_Data -> the app created `hooks.json` on its own; it must never do that.
- `Save` is enabled with nothing edited, or `Reset all to defaults` is enabled with nothing modified -> the dirty/modified state tracking is broken and the page invites a write that changes nothing.
- The printed path does NOT point at the App_Data you believe is live -> either `Team__DataDir` is still set from an earlier test, or the working directory is not the project folder. Resolve that before trusting ANY other test's disk oracle.
- `/settings/nonsense` 404s or throws -> the unknown-tab fallback was removed.

**Inconclusive if**

Do not confuse `hooks.json` (the override file, under App_Data, absent until saved) with `hooks.default.json` (generated, sits beside the binary at `src\Huddle.App\bin\Debug\net10.0\hooks.default.json`, always present). Finding `hooks.default.json` is not a result. If you cannot tell which field you edited, undo by clicking that field's own reset control and re-reading the Save button state.

> [!NOTE]
> This test deliberately leaves a `hooks.json` behind. Delete it (`Remove-Item src\Huddle.App\App_Data\hooks.json`) if a later test needs a first-run state.

### STARTUPCONFIG-10 — Appearance tab on a first run reads System; picking Dark writes appearance.json and reloads the page

**Free** · about 8 min

*Proves the theme choice persists to disk, that the deliberate full page reload happens, and that the app never creates appearance.json unbidden.*

**Before you start**

- `Test-Path src\Huddle.App\App_Data\appearance.json` returns False. If True, delete the file and reload the page before starting.

**Steps**

1. In `T-B` run `Test-Path src\Huddle.App\App_Data\appearance.json` and note the result.
2. In the browser go to `/settings` and click the `Appearance` tab.
3. Read the intro paragraph.
4. Open the `Theme` dropdown and read every option it offers, and which one is currently selected.
5. Read the paragraph naming where overrides are stored.
6. Select `Dark` in the `Theme` dropdown.
7. Watch the browser: note whether the whole page reloads (the tab spinner turns, the page repaints from the server) rather than updating in place.
8. Read the page's colours after it comes back.
9. In the second pwsh window run `Test-Path src\Huddle.App\App_Data\appearance.json` and then `Get-Content src\Huddle.App\App_Data\appearance.json`.
10. Set the `Theme` dropdown back to `System`.

**Pass if — all of these**

- Step 1 returns False.
- The intro reads exactly `Pick a theme, or leave it on System to follow your device's own light or dark setting.`
- The `Theme` dropdown offers exactly three options — `System`, `Light`, `Dark` — with `System` selected.
- The overrides paragraph contains `The file does not exist until you save a theme choice here, so it being absent is expected, not a bug.`
- Choosing `Dark` causes a FULL page navigation, and the page comes back rendered dark.
- Step 9 returns True, and the file's content is overrides-only JSON containing a theme id such as `huddle-dark` — an id, never the label `Dark`.

**Fail if — any of these**

- Step 1 returns True on a clean App_Data -> the app created `appearance.json` on its own; it must never do that.
- The file stores the display label (`Dark`) rather than the id (`huddle-dark`) -> a display name was persisted where an id belongs; renaming a theme's label would then break the stored choice.
- The dropdown offers more or fewer than three options -> the theme catalog or the System option changed.
- Choosing `Dark` on a light-mode OS leaves the page light -> the theme stylesheet was not layered into the document head.

**Inconclusive if**

The full page reload is DELIBERATE (the document head belongs to the server and Blazor's render tree cannot reach it) — 'the theme flashes / reloads' is a documented limit, not a bug. Likewise, `Dark` staying dark on a light OS is correct: a Theme has no per-mode pair, and following the device means choosing `System`. The choice is per installation, so a second browser or a private window showing the same theme is also correct, not a session bug.

> [!NOTE]
> If the Appearance tab shows a section headed `Overrides that didn't load`, that is the allowlist refusing a hand-edited token value — record the listed reason, but it is a separate concern from this test.

### STARTUPCONFIG-11 — Rooms and Transcripts survive a restart; Drafts and the Budget do not

**Free** · about 8 min

*Proves durability of the things that must persist and confirms the two documented in-memory losses, so neither is misfiled.*

**Before you start**

- At least three messages exist across the `echo` and `alpha` Rooms (use STARTUPCONFIG-04's `hi @echo` form to generate them).

**Steps**

1. Open `echo` and count its messages; write down the count, the first sender name, and the first timestamp shown.
2. Open `alpha`, send `hi @alpha`, and count its messages the same way.
3. Count the entries in the sidebar room list.
4. Switch to the run window and press Ctrl+C to stop the app. Wait for the prompt to return.
5. Relaunch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
6. Reopen http://localhost:5100 and count the sidebar room list again.
7. Open `echo` and compare its message count, order, sender names and the first timestamp against what you wrote down.
8. Open `alpha` and do the same.

**Pass if — all of these**

- The sidebar room list holds the SAME number of Rooms after the restart as before — it does not grow.
- Each Room's messages are all present, in the same order, with the same sender names and the same displayed times.
- No streaming row, caret or `Stop` button is present after the restart.

**Fail if — any of these**

- Messages are missing after the restart -> transcript durability is broken. Before filing, confirm the DataDir did not change (open `/settings` and read the path in the Hooks tab's overrides paragraph).
- A DUPLICATE Room appears for `echo` or `alpha` after the restart -> reconnecting under the same name created a second two-Member Room instead of re-attaching to the existing one by exact membership. This is a real bug.
- The room count grows by more than the agents that reconnected -> the same duplication bug in another shape.

**Inconclusive if**

A Draft that was mid-stream when you pressed Ctrl+C is simply gone — that is a documented limit (Drafts are in memory, per process), not a result. Likewise, any Budget pause you had reached is cleared by the restart; that is STARTUPCONFIG-15's subject, not a failure here. If `Get-ChildItem src\Huddle.App\App_Data\rooms` shows no `.jsonl` files at all, you are looking at the wrong App_Data — re-read the path from `/settings`.

> [!NOTE]
> Optional oracle: count the lines of `src\Huddle.App\App_Data\rooms\<roomId>.jsonl` before and after with `(Get-Content <path> | Measure-Object -Line).Lines`.

### STARTUPCONFIG-12 — Deleting App_Data while running fails; stopping first makes the clean slate reliable

**Free** · about 8 min

*Establishes the reset procedure every later configuration test depends on, and proves a clean run rebuilds App_Data, an empty Teams directory, team.db and the two demo Rooms from nothing.*

**Before you start**

- The app is running and `src\Huddle.App\App_Data` exists.

**Steps**

1. With the app STILL RUNNING, in a second pwsh window run `Remove-Item -Recurse -Force src\Huddle.App\App_Data` and read the error.
2. Switch to the run window and press Ctrl+C. Wait for the prompt.
3. Run `Remove-Item -Recurse -Force src\Huddle.App\App_Data` again.
4. Run `Test-Path src\Huddle.App\App_Data` and confirm it returns False.
5. Relaunch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
6. Open http://localhost:5100 and read the sidebar room list.
7. In `T-B` run `Get-ChildItem -Recurse -Force src\Huddle.App\App_Data` and read the full listing.

**Pass if — all of these**

- Step 1 fails with a file-in-use error that NAMES `team.db`.
- After the app is stopped, step 3 succeeds and step 4 returns False.
- After relaunch, the sidebar again shows exactly `echo` and `alpha`.
- The App_Data listing contains: a `Teams` directory that is EMPTY, `team.db`, `team.db-wal` and `team.db-shm`.
- The listing does NOT contain `hooks.json`, `appearance.json`, or a `work` directory.
- A `rooms` directory appears only AFTER you send the first message — confirm by re-listing before typing anything.

**Fail if — any of these**

- `Teams\` is missing from the fresh App_Data -> the Persona store was never constructed; `/teammates` will be broken too.
- App_Data is created somewhere other than `src\Huddle.App\` -> the working directory was not the project folder. Testers report this as 'my rooms disappeared' when the data simply moved. Check where you launched from.
- Startup throws an exception naming `Directory.CreateDirectory` -> the path is not writable.
- `hooks.json` or `appearance.json` exists on a clean run -> the app created an override file it must never create (see STARTUPCONFIG-09 and -10).

**Inconclusive if**

On some runs the SQLite handle lingers briefly even after the app has exited — retry the delete once after a few seconds before filing anything. The failure in step 1 is a KNOWN, pre-existing, deliberately-unfixed bug (the team directory is not disposable and SQLite pooling holds the file); it is the expected result here, not a defect to report. Never delete only `team.db` and leave `rooms\` behind — that orphans transcripts against Room ids that no longer exist.

> [!NOTE]
> Every test below that says 'clean App_Data' means exactly steps 2-4 of this test.

### STARTUPCONFIG-13 — Team__AgentMessageBudget=1 pauses the Room after one agent reply and shows the Continue prompt

**Free** · about 8 min

*Proves the runaway-loop guard reaches the browser, in the right place on the page, with the right copy, and that a Human Message resets it.*

**Before you start**

- Demo agents enabled (default).
- No other `Team__*` variables set.

**Steps**

1. Stop the app (Ctrl+C).
2. In the run window set `$env:Team__AgentMessageBudget = '1'`.
3. Relaunch in that SAME window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open http://localhost:5100 and click `echo`.
5. Type exactly `hi @echo` and press Enter. Wait for the reply.
6. Read what appears between the message list and the message box at the bottom.
7. Note the two buttons in that block and their exact labels.
8. Click `Leave paused` and read what replaces the block.
9. Type exactly `anything` and press Enter.
10. Read the area between the message list and the message box again.
11. When finished, stop the app and run `Remove-Item Env:Team__AgentMessageBudget`.

**Pass if — all of these**

- After the reply lands, a block appears BETWEEN the message list and the message box reading exactly `Agents have sent 1 replies since you last spoke, and are paused.`
- That block holds exactly two buttons labelled `Continue` and `Leave paused`.
- Clicking `Leave paused` replaces the block with the single line `Paused — 1 of 1 agent replies since you last spoke.`
- After sending `anything`, both the paused line and the prompt are gone, and no budget note is shown at all.

**Fail if — any of these**

- The prompt renders ABOVE the message list rather than between it and the message box -> a placement regression; this block is deliberately the last thing read before deciding to spend money.
- The prompt or the paused line persists after the Human sends `anything` -> the Human Message no longer resets the counter, so the Room would stay paused forever.
- The counter does not reset (the note still shows a non-zero `Used` after a Human Message) -> same defect in a subtler shape.
- No prompt appears at all after the agent's reply -> either the environment variable did not bind (see the fail note below) or the guard is gone.

**Inconclusive if**

If no prompt appears, FIRST verify the variable took: `Get-ChildItem Env:Team__AgentMessageBudget` must print `1`, and it must have been set in the SAME window that ran `dotnet run`. A single-underscore spelling binds to nothing with no error whatsoever (see STARTUPCONFIG-17). Only after confirming the variable is set correctly and the agent actually replied should you file a defect.

> [!NOTE]
> Do not stop the app at step 10 — STARTUPCONFIG-14 continues from this same run.

### STARTUPCONFIG-14 — Continue grants Budget and produces no new reply — the trap that reads as a dead button

**Free** · about 5 min

*Proves the grant landed (the allowance figure rises) even though nothing speaks, so 'Continue does nothing' is not misfiled.*

**Before you start**

- `Team__AgentMessageBudget` is still `1` and the app from STARTUPCONFIG-13 is running.
- The `echo` Room is in the paused state with the agent's own reply as the most recent Message.

**Steps**

1. In the `echo` Room, type exactly `hi @echo` and press Enter, and wait for the reply so the Room is paused again with the AGENT's reply as the last Message.
2. Confirm the `Agents have sent 1 replies since you last spoke, and are paused.` block is showing with its two buttons.
3. Click `Continue`.
4. Watch the button's own label while the request is in flight.
5. Read what replaces the alert block.
6. Wait 15 seconds and watch the message list.
7. Switch to the console and search for a line containing `was extended to`.

**Pass if — all of these**

- While in flight, the button's label reads `Continuing…` and is not clickable.
- The alert block is replaced by the line `1 of 2 agent replies since you last spoke.` — the second number has RISEN from 1 to 2.
- No new message arrives in the 15 seconds after clicking.
- The console contains a line of the form `Room '<id>' was extended to 2 agent messages.`

**Fail if — any of these**

- The second number does NOT rise (still reads `1 of 1`) -> the grant did not land; this really is a dead button.
- The button stays clickable while the request is in flight -> the busy guard is gone and a double-click can grant twice, which spends real money on a paid Persona.
- The console shows no `was extended to` line and the number did not rise -> the extend path is broken.

**Inconclusive if**

'No new reply' is CORRECT and must not be filed. Continue re-delivers only the Room's most recent Message, and delivery always skips that Message's own sender — so when the last Message is the agent's own reply, there is nobody left to wake, and a demo agent only answers a mention anyway. The rising allowance figure is the proof the grant worked. To see Continue actually wake something you would need the last Message to be a Human Message that mentioned the agent, which only happens if the agent was paused mid-run.

> [!NOTE]
> Also documented: a Message declined for Budget is held for re-delivery, not kept as Catch-up. If you type something instead of clicking Continue, the agent's next prompt will not carry the Message it was paused on, even though that Message is still visible in the transcript.

### STARTUPCONFIG-15 — Team__AgentMessageBudget=0 removes the guard entirely and hides every budget surface; a restart un-pauses everything

**Free** · about 8 min

*Proves the documented escape hatch back to pre-guard behaviour, and confirms the deliberate in-memory-counter gap so it is not filed as data loss.*

**Before you start**

- Demo agents enabled.

**Steps**

1. With the app still running at `Team__AgentMessageBudget = '1'` and the `echo` Room paused, press Ctrl+C in the run window.
2. Relaunch in the same window (the variable is still `1`): `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Reopen the `echo` Room and look at the area between the message list and the message box, and at the transcript itself.
4. Stop the app. Run `$env:Team__AgentMessageBudget = '0'` and relaunch in the same window.
5. Open `echo` and send `hi @echo`. Wait for the reply. Repeat four more times, for five agent replies in total.
6. After each reply, look at the area between the message list and the message box.
7. Stop the app and run `Remove-Item Env:Team__AgentMessageBudget`.

**Pass if — all of these**

- After the restart at step 3, the transcript is fully intact but the pause alert and the budget note are BOTH gone, and the next agent reply is allowed.
- With the budget set to `0`, no budget note, no pause alert and no `Continue` button appears at any point across all five agent replies.

**Fail if — any of these**

- Any budget text at all appears with the value set to `0` -> zero-or-less is documented as the only way back to pre-guard behaviour; a counter showing here means the disable path is broken.
- The transcript is missing messages after the restart at step 3 -> that is a REAL bug (Messages are durable in `App_Data\rooms\*.jsonl`), unlike the lost pause.
- With `0` set, a reply is ever refused -> the cap is still enforcing despite being disabled.

**Inconclusive if**

A restart clearing the pause is DELIBERATE — the counter is in memory and per Room, the same trade the Catch-up buffer makes. Do not file 'the budget reset after a restart'. Conversely, do not file 'the budget counter is missing' when the value is `0`; that is the point of the setting. If you are unsure whether the variable took, confirm with `Get-ChildItem Env:Team__AgentMessageBudget`.

> [!NOTE]
> Two behaviours are combined here deliberately: they share one restart and would otherwise duplicate five minutes of setup.

### STARTUPCONFIG-16 — Team__DemoAgent__Enabled=false on a clean App_Data leaves the app with no Rooms and no Agents

**Free** · about 8 min

*The positive control for the whole environment-variable surface: a correctly spelled variable must produce an unmistakable, observable difference.*

**Before you start**

- The app is stopped and `src\Huddle.App\App_Data` has been deleted (see STARTUPCONFIG-12).

**Steps**

1. In the pwsh window run `$env:Team__DemoAgent__Enabled = 'false'`.
2. Run `Get-ChildItem Env:Team__DemoAgent__Enabled` and confirm it prints the name and value `false`.
3. Launch in that SAME window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open http://localhost:5100 and read the main column and the sidebar room list.
5. Click `New chat` and read the panel.
6. Search the whole console output for the text `Demo agent`.
7. When finished, stop the app and run `Remove-Item Env:Team__DemoAgent__Enabled`.

**Pass if — all of these**

- The main column reads exactly `No rooms yet. Start an agent to create one.`
- The sidebar room list shows the same sentence and no Room entries.
- The `New chat` panel reads exactly `No agents have connected yet. Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.`
- The `New chat` panel shows NO `Start chat` button.
- The console contains NO line containing `Demo agent` — neither a `connected.` nor a `failed to connect` line.

**Fail if — any of these**

- Rooms still appear -> the variable did not bind. The overwhelmingly likely cause is a SINGLE underscore or a different shell, not a product defect. Re-check step 2 before filing anything.
- Rooms are absent but the console DOES contain `Demo agent … failed to connect to pipe team after 30 attempts.` -> the agents were still enabled and simply could not reach the pipe; the flag did not take, and you would have recorded a false pass.
- A `Start chat` button renders with no agents listed -> the empty-state guard is gone.

**Inconclusive if**

A negative result (nothing appears) is only trustworthy when the console ALSO shows no `Demo agent` lines at all. If the console is silent for a different reason — a mis-set log filter — see STARTUPCONFIG-25 before judging. If you cannot get a clean App_Data because the delete fails, stop the app first (STARTUPCONFIG-12).

> [!NOTE]
> Run this test before any other environment-variable test. It is the proof that your shell, your spelling and your launch window are wired up correctly.

### STARTUPCONFIG-17 — A mis-spelled configuration key changes nothing and reports nothing

**Free** · about 6 min

*The highest-value negative test in this area: it demonstrates the silent-binding failure that can invalidate every other configuration result in this script.*

**Before you start**

- No `Team__*` variables set (`Get-ChildItem Env:Team__*` prints nothing).
- Clean or existing App_Data — either is fine.

**Steps**

1. Run `$env:Team_DemoAgent_Enabled = 'false'` — note the SINGLE underscores, which is the mistake under test.
2. Launch in that same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Read the whole console output from the first line to `Now listening on:`.
4. Open http://localhost:5100 and read the sidebar room list.
5. Stop the app. Run `Remove-Item Env:Team_DemoAgent_Enabled`.
6. Now run `$env:Team__DemoAgent__Enable = 'false'` — correct separators, misspelled leaf key — and relaunch.
7. Read the console and the sidebar again.
8. Stop the app and run `Remove-Item Env:Team__DemoAgent__Enable`.

**Pass if — all of these**

- With the single-underscore spelling, the app starts normally and the sidebar shows `echo` and `alpha` exactly as with no variable set.
- With the misspelled leaf key, the same: the app starts normally and both Rooms appear.
- In BOTH cases the console prints no warning, no error and no line mentioning the variable — the value simply bound to nothing.

**Fail if — any of these**

- The app refuses to start, or prints a warning naming the mis-spelled key -> record it as an OBSERVATION, not a defect: a loud failure here would be an improvement, but the documented current behaviour is silence. Note that exactly one key IS guarded loudly — see STARTUPCONFIG-22.
- The mis-spelled variable DOES disable the demo agents -> configuration is being bound by something other than the documented double-underscore scheme; every other test's spelling assumptions are then wrong.

**Inconclusive if**

This test has no failing outcome in the usual sense; its job is to calibrate the tester. The rule it teaches is binding on every other test here: NEVER conclude that a configuration value took effect because you set a variable. Always confirm positively — a Room that is or is not there, a budget note, a file on disk, a console line. STARTUPCONFIG-16 is the positive control to pair this with.

> [!NOTE]
> This is why every configuration test above and below names an observable positive effect rather than saying 'set the variable and check it worked'.

### STARTUPCONFIG-18 — Disabling the demo agents against an EXISTING App_Data keeps the Rooms but leaves them dead

**Free** · about 7 min

*Pins the silence that is most likely to be misfiled as 'the app stopped replying', and proves the Room view deliberately shows no health banner for an Agent that merely never started.*

**Before you start**

- An App_Data in which the demo agents have already connected at least once and at least one message exists (run STARTUPCONFIG-03 and -04 first if not).
- The app is stopped.

**Steps**

1. Run `$env:Team__DemoAgent__Enabled = 'false'` and relaunch in the same window.
2. Open http://localhost:5100 and read the sidebar room list.
3. Open the `echo` Room and read its transcript.
4. Look carefully at the area between the message list and the message box.
5. Click `New chat` and inspect the status dot beside `echo` and `alpha`; hover each dot and read its tooltip.
6. Close the panel, type exactly `hi @echo` and press Enter. Wait 30 seconds.
7. Search the console for `Demo agent`.
8. Stop the app and run `Remove-Item Env:Team__DemoAgent__Enabled`.

**Pass if — all of these**

- The sidebar still lists `echo` and `alpha`.
- The `echo` transcript still shows every earlier message.
- The `New chat` panel still lists both agents, each with a grey dot whose tooltip reads `offline`.
- The Human message posts and NOTHING answers it for the full 30 seconds.
- NO alert strip, banner or error appears anywhere in the Room.
- The console contains no `Demo agent` lines.

**Fail if — any of these**

- An always-on alert strip appears in the Room naming the offline agents -> a Member that merely never started has no recorded reason, and the Room view is deliberately quiet about it; a permanent alert is one nobody reads.
- The status dot beside `echo` shows green/online -> presence is being inferred from the database row rather than from a live pipe connection.
- The Rooms or transcript are gone -> that IS a real bug; check the DataDir via `/settings` first.

**Inconclusive if**

The silence here is CORRECT. Judging 'no reply' requires checking the dot in the `New chat` panel or the console — never the Room, which shows nothing by design. If you need to distinguish 'the agent is offline' from 'the mention did not parse', re-enable the demo agents and confirm the same message DOES get a reply (STARTUPCONFIG-04) before filing.

> [!NOTE]
> The agents remain listed because they are rows in team.db, not live connections.

### STARTUPCONFIG-19 — Team__DemoAgent__Names changes which demo Rooms exist, and must replace rather than append

**Free** · about 8 min

*Proves array-element configuration binding reaches the Rooms, and specifically watches for the documented binder hazard where bound elements are appended to an already-populated collection.*

**Before you start**

- The app is stopped and `src\Huddle.App\App_Data` has been deleted — an existing team.db keeps the old Rooms alongside the new ones and makes the result unreadable.

**Steps**

1. Run `$env:Team__DemoAgent__Names__0 = 'zeta'`.
2. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open http://localhost:5100, wait 10 seconds, and read the sidebar room list. Write down every entry.
4. Search the console for lines containing `connected.` and write down each agent name.
5. Stop the app. Delete App_Data again. Now ALSO run `$env:Team__DemoAgent__Names__2 = 'gamma'` and relaunch.
6. Read the sidebar room list again and write down every entry.
7. Stop the app and run `Remove-Item Env:Team__DemoAgent__Names__0; Remove-Item Env:Team__DemoAgent__Names__2`.

**Pass if — all of these**

- After step 3 the sidebar shows exactly two Rooms, `zeta` and `alpha` — index 0 was overridden and index 1 survived.
- `echo` does NOT appear.
- The console shows `Demo agent zeta connected.` and `Demo agent alpha connected.`
- After step 6 the sidebar shows exactly three Rooms: `zeta`, `alpha` and `gamma`.

**Fail if — any of these**

- `echo` reappears alongside `zeta`, or four Rooms exist where three were expected -> the configuration binder APPENDED the bound elements to an already-populated collection instead of replacing it. This is the exact hazard the options type is written to avoid; file it.
- A named Room simply never appears and the sidebar count is short -> the name was rejected at the pipe handshake. The only trace is a console line naming an invalid-name error; look for it before concluding the binding failed.
- No change at all from the default `echo`/`alpha` -> the index-form variable did not bind; re-check the double underscores and the index digits.

**Inconclusive if**

If `App_Data` was not deleted between steps, old Rooms persist alongside new ones and the count means nothing — redo from `P-RESET-ALL`. If a name you chose contains characters outside letters, digits, spaces, `-` and `_`, the handshake rejects it and no Room appears; that is correct behaviour, not a binding failure.

> [!NOTE]
> Windows reserved device names (`CON`, `NUL`, `COM1`) pass the name rules and then fail to become files — do not use them as demo names.

### STARTUPCONFIG-20 — Team__DataDir points the whole application at a different folder — a clean slate without deleting anything

**Free** · about 8 min

*Proves the softer reset path, and proves the Settings page is a reliable oracle for which DataDir is live.*

**Before you start**

- An App_Data with Rooms and at least one message already in it.
- The app is stopped.

**Steps**

1. Run `$env:Team__DataDir = 'App_Data_test'`.
2. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open http://localhost:5100 and read the main column immediately, then again after 10 seconds.
4. Open each Room and read its transcript.
5. Click `Settings` and read the absolute path printed in the Hooks tab's overrides paragraph.
6. In `T-B` run `Get-ChildItem -Recurse -Force src\Huddle.App\App_Data_test`.
7. Stop the app, run `Remove-Item Env:Team__DataDir`, and relaunch.
8. Open each Room and read its transcript again.

**Pass if — all of these**

- The two demo Rooms appear freshly recreated with EMPTY transcripts — none of the earlier messages are present.
- The path printed on the Settings page ends `\src\Huddle.App\App_Data_test\hooks.json`.
- `src\Huddle.App\App_Data_test\` exists and holds its own `team.db` and an empty `Teams\` folder.
- After removing the variable and relaunching, the ORIGINAL Rooms and their full history are back, untouched.

**Fail if — any of these**

- Old messages still appear with the variable set -> the variable did not bind (see STARTUPCONFIG-17); do not file this as a DataDir defect until the Settings path confirms which directory is live.
- The Settings path names a different directory from the one that actually gained files -> the page is reporting a path the app is not using, which would make it useless as an oracle everywhere else.
- The original data is missing after removing the variable -> a real data-loss bug; confirm the working directory has not changed before filing.

**Inconclusive if**

The value is resolved against the process working directory, so a relative value lands under `src\Huddle.App`, NOT the repo root. A tester looking for `E:\Repos\Huddle\App_Data_test` will find nothing and wrongly conclude the variable did nothing. Always read the absolute path from the Settings page rather than guessing where the folder went.

> [!NOTE]
> Clean up afterwards with `Remove-Item -Recurse -Force src\Huddle.App\App_Data_test` once the app is stopped.

### STARTUPCONFIG-21 — Team__HumanName renames the Human everywhere new, while old transcript lines keep the old name

**Free** · about 7 min

*Proves the rename reaches the members line and new message attribution, and pins the mixed attribution so it is not filed as a half-applied rename.*

**Before you start**

- An App_Data with at least one Human message already in a transcript.
- The app is stopped.

**Steps**

1. Open a Room in the browser first (before changing anything) and note that earlier Human messages show sender `You` and the members line reads `You, echo`.
2. Stop the app if it is running. Run `$env:Team__HumanName = 'Emre'`.
3. Relaunch in the same window and reopen the same Room.
4. Read the members line under the Room heading.
5. Read the sender name on the OLD messages already in the transcript.
6. Type exactly `hello again @echo` and press Enter.
7. Read the sender name on the message you just sent.
8. Stop the app and run `Remove-Item Env:Team__HumanName`.

**Pass if — all of these**

- The members line now reads exactly `Emre, echo`.
- Every message sent BEFORE the change still shows sender `You`.
- The message sent AFTER the change shows sender `Emre`.
- No error appears anywhere.

**Fail if — any of these**

- The members line still reads `You, echo` -> the rename did not reach the directory; check the variable spelling before filing.
- The NEW message still shows `You` -> the sender name is not being read from the renamed directory row at post time.
- Old messages were rewritten to `Emre` -> transcript lines are being mutated after the fact, which they must never be.

**Inconclusive if**

The mixed attribution is CORRECT and must not be filed as 'the rename only half applied'. The directory row is renamed in place, but each transcript line carries a sender name captured at post time and is never rewritten. If you cannot tell old messages from new, note the time on each message row.

> [!NOTE]
> Optional oracle if sqlite3 is available: `sqlite3 src\Huddle.App\App_Data\team.db "SELECT name FROM users WHERE id='human';"` returns the new name, while older lines in `App_Data\rooms\*.jsonl` still carry `"senderName":"You"`.

### STARTUPCONFIG-22 — The renamed Team__Acp__PersonaDir key refuses to start the app rather than scanning nothing

**Free** · about 5 min

*Proves the one configuration key that fails LOUDLY — written precisely because binding-to-nothing is this repo's signature failure shape.*

**Before you start**

- The app is stopped and nothing else holds port 5100.

**Steps**

1. Run `$env:Team__Acp__PersonaDir = 'personas'`.
2. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Read the console output in full.
4. Try to open http://localhost:5100 in the browser and note what it shows.
5. Run `Remove-Item Env:Team__Acp__PersonaDir`.
6. Relaunch and confirm the app starts normally again.

**Pass if — all of these**

- The app does NOT start. The console shows an `InvalidOperationException` whose message reads: `Configuration key 'Team:Acp:PersonaDir' was renamed to 'Team:Acp:TeamsDir'. Update the configuration source that sets it (environment variable, user secret, etc.) - there is no automatic fallback.`
- The stack trace names `AddTeamServices`.
- The browser at http://localhost:5100 shows a connection-refused page.
- After clearing the variable, the app starts normally.

**Fail if — any of these**

- The app starts normally with the variable set -> the loud guard has regressed into exactly the silent degradation it exists to prevent; the Persona directory would quietly scan nothing and `/teammates` would show `No Personas yet.` with no explanation anywhere.
- The message names the OLD key as the replacement, or omits the new key `Team:Acp:TeamsDir` -> the guard fires but does not tell the user how to fix it.

**Inconclusive if**

If the browser still shows a working app after the crash, you are looking at a DIFFERENT instance still bound to 5100. Stop every `dotnet` process and repeat. CLEAR THE VARIABLE before running anything else — while it is set, every subsequent test will fail to launch and every result will be meaningless.

> [!NOTE]
> A test in `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs` pins this message; a wording change there should be matched here.

### STARTUPCONFIG-23 — An external pipe client creates a Room live, with no page refresh

**Free** · about 7 min

*Proves the pipe surface end to end from a real external process, and that the sidebar updates without a reload.*

**Before you start**

- The app is running with the DEFAULT pipe name (no `Team__PipeName` set).
- Exactly ONE instance of the app is running (`Get-Process dotnet` shows only that one).
- `pwsh` is available.

**Steps**

1. Open http://localhost:5100 and leave the browser window visible. Note the current sidebar room list.
2. In a SECOND pwsh window at `E:\Repos\Huddle`, run `pwsh tools/echo-bot.ps1 -Name mybot`.
3. Read the first three lines the script prints.
4. Look at the browser sidebar WITHOUT refreshing or navigating.
5. Click the `mybot` Room, type exactly `hi @mybot` and press Enter.
6. Watch both the browser and the script window.
7. Press Ctrl+C in the script window to stop it.
8. In the browser, click `New chat` and look at the dot beside `mybot`; check the sidebar room list.

**Pass if — all of these**

- The script prints `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...` then `Sent hello. Listening for messages (Ctrl+C to exit)...` then a JSON line beginning `{"type":"welcome"` that contains `"name":"mybot"`.
- A Room named `mybot` appears in the sidebar immediately, with NO refresh and no navigation.
- `hi @mybot` gets a bold echoed reply in the browser, and the script window prints the incoming envelope.
- After Ctrl+C, the `mybot` Room REMAINS in the sidebar, and its dot in the `New chat` panel is grey/offline.

**Fail if — any of these**

- The Room only appears after pressing F5 -> the live rooms-changed event or a component subscription broke.
- The script prints an envelope of type `error` instead of `welcome` -> either the name failed the name rules, or the protocol version is not an exact match. The version check is strict equality, so a client one version behind is rejected outright rather than degraded.
- The Room disappears when the script stops -> Rooms are being tied to live connections instead of persisting.

**Inconclusive if**

The `version` field in the welcome envelope must equal the CURRENT protocol version in `src/Huddle.Contracts/ProtocolVersion.cs` — at the time of writing that is `3`. The example in `docs/AgencyTeam.md` shows `"version":2` and is STALE; do not file a mismatch against the doc. If the script hangs at `Connecting…`, confirm the app is running and that no `Team__PipeName` is set — see STARTUPCONFIG-24.

> [!NOTE]
> Leave the app running for STARTUPCONFIG-24.

### STARTUPCONFIG-24 — Team__PipeName changes the pipe and silently orphans any external client

**Free** · about 7 min

*Proves the pipe name is the whole address, and demonstrates that the browser gives NO hint when an external client is knocking on the wrong pipe.*

**Before you start**

- The app is stopped and `src\Huddle.App\App_Data` has been deleted, so the absence of a `mybot` Room is unambiguous.
- Only one instance will run.

**Steps**

1. Run `$env:Team__PipeName = 'team-test'`.
2. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open http://localhost:5100, wait 10 seconds, and read the sidebar room list.
4. In `T-B` run `pwsh tools/echo-bot.ps1 -Name mybot`.
5. Read what the script prints and how long it takes to finish or fail.
6. Look at the browser sidebar for a full 30 seconds WITHOUT refreshing.
7. Stop the app and run `Remove-Item Env:Team__PipeName`.

**Pass if — all of these**

- The two demo Rooms still appear — the demo agents dial whatever pipe name is configured, so they are unaffected.
- The script prints `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...` and then fails (a timeout after roughly five seconds) or blocks — it never prints a `welcome` line.
- NO `mybot` Room ever appears in the browser.
- The browser gives no error, no warning and no visible change of any kind.

**Fail if — any of these**

- A `mybot` Room DOES appear -> another instance of the app is still running on the default pipe name and answering the script; stop every `dotnet` process and repeat.
- The demo Rooms do NOT appear -> the pipe-name change broke the demo agents too, which should follow the configured name.
- The script connects successfully to `\\.\pipe\team` -> a server is still bound to the default name.

**Inconclusive if**

The complete silence in the browser is the POINT of this test, not a defect. To confirm you are testing the right thing, verify the positive control first: with no `Team__PipeName` set, the same command must make a `mybot` Room appear immediately (STARTUPCONFIG-23). If the script connects and you cannot account for it, run `Get-Process dotnet` — a second instance is the usual explanation.

> [!NOTE]
> `tools/echo-bot.ps1` also takes a `-Pipe` parameter (default `team`); this test deliberately leaves it at the default so the client and server disagree.

### STARTUPCONFIG-25 — Two app instances sharing one pipe name cross-wire the demo agents

**Free** · about 8 min

*Documents a confusing-but-correct configuration outcome so its four distinctive symptoms are never filed as product bugs, and establishes the single-instance precondition every pipe test depends on.*

**Before you start**

- One instance of the app is already running on port 5100 with the default pipe name and default DataDir.

**Steps**

1. Confirm exactly one instance is running: `Get-Process dotnet`.
2. In a SECOND pwsh window run `dotnet run --project src/Huddle.App --urls http://localhost:5101 --no-launch-profile` — note this deliberately uses the SAME default pipe name.
3. Open http://localhost:5100 in one browser window and http://localhost:5101 in another, side by side.
4. In each window, read the sidebar room list and click `New chat` to read the agent list and status dots.
5. In the 5100 window, open a Room and send `hi @echo`. Watch BOTH browser windows.
6. Record exactly which window the Room, the reply and the online dots appeared in.
7. Stop the second instance (Ctrl+C in its window).
8. Before launching the second instance, set `$env:Team__DataDir = 'App_Data2'` in that terminal. Without it both processes share one `team.db` and one set of transcripts, which ADR-0002 forbids and which corrupts the first instance's state.

**Pass if — all of these**

- You can articulate, from what you observed, that Rooms/replies/online status did not stay confined to the instance that launched the agent — for example a reply landing in the 'wrong' window, or an agent showing online in a browser whose own process never launched it.
- Stopping the second instance restores predictable single-instance behaviour in the 5100 window.

**Fail if — any of these**

- There is no fail condition here in the product sense. If you cannot reproduce any cross-wiring, record that as an observation — the pipe server accepts multiple server instances and the name is the whole address, so cross-wiring is possible but not guaranteed on every run.

**Inconclusive if**

This test is diagnostic, not a pass/fail gate. The symptoms it produces — 'replies go to the wrong room', 'an agent is online twice', 'my rooms doubled' — are configuration, not defects, and must never be filed. The takeaway is operational: ANY test involving pipes must first confirm only one instance is running, or give the second instance its own `Team__PipeName` AND its own `Team__DataDir`.

> [!NOTE]
> The second instance uses `--no-launch-profile`, so it runs in Production (ACP off) on the URL you pass — see STARTUPCONFIG-26. The cross-wiring this test demonstrates is a property of the shared pipe name alone, so a separate DataDir costs the test nothing.

### STARTUPCONFIG-26 — Environment selection decides whether ACP is on and which port is used

**Free** · about 6 min

*Prevents the two most common misreadings in this area: 'the app moved to a different port' and 'my Personas stopped starting', both of which are configuration rather than defects.*

**Before you start**

- No `Team__*` variables set.
- Nothing else on ports 5000 or 5100.

**Steps**

1. Run `dotnet run --project src/Huddle.App` (no `--urls`, no other flags).
2. Read the console's `Hosting environment:` and `Now listening on:` lines. Open the URL it names.
3. Stop the app with Ctrl+C.
4. Run `dotnet run --project src/Huddle.App --no-launch-profile`.
5. Read the console's `Hosting environment:` and `Now listening on:` lines again.
6. Try to open http://localhost:5100 in the browser and note what happens.
7. Open the URL the console actually named and confirm the app renders there.
8. Compare the two runs' console verbosity — note whether `Agency.Huddle` category lines differ in level.
9. Stop the app.

**Pass if — all of these**

- Run (a) reports `Hosting environment: Development` and `Now listening on: http://localhost:5100`.
- Run (b) reports `Hosting environment: Production` and listens on `http://localhost:5000` (not 5100).
- In run (b), http://localhost:5100 gives connection refused.
- In run (b), no Persona starts even if Persona files exist, because only `appsettings.json` applies and ACP is off there.
- Run (a)'s console is noticeably more verbose for `Agency.Huddle` categories than run (b)'s.

**Fail if — any of these**

- Run (a) reports Production -> the launch profile was not picked up; check that `src/Huddle.App/Properties/launchSettings.json` still holds the `http` profile.
- Run (b) reports Development -> `--no-launch-profile` is not being honoured, and every 'this is the stock configuration' assumption elsewhere is wrong.
- Run (b) starts a Persona process -> ACP is on in Production, which it must not be by default (it spends real money).

**Inconclusive if**

If you launched by running the built DLL (`dotnet src/Huddle.App/bin/Debug/net10.0/Huddle.App.dll`) rather than `dotnet run`, you also get Production and port 5000 — that is the same configuration outcome, not a second defect. ALWAYS read the `Hosting environment:` line before judging any ACP-related result anywhere in this script.

> [!NOTE]
> Development sets `Team:Acp:Enabled: true`; Production leaves it at its shipped default of false.

### STARTUPCONFIG-27 — Team__Acp__Enabled=false starts no agent process and costs nothing, while /teammates still works fully

**Free** · about 6 min

*Proves the recommended zero-cost configuration: no child process anywhere, and the Teammates page still renders and is browsable.*

**Before you start**

- `node` may or may not be installed — the test works either way.
- The app is stopped.

**Steps**

1. In `T-B` run `O-ADAPTERS`. It should print `0` — it counts only Adapters, so an unrelated node process can never be blamed on the app.
2. In the run window set `$env:Team__Acp__Enabled = 'false'` and launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Wait 20 seconds after the app is listening.
4. In `T-B` run `O-ADAPTERS` again.
5. In the browser click `Teammates` and read the heading, the button beside it, the intro paragraph and the body.
6. Search the console for `Persona` and for `adapter`.
7. Stop the app and run `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- No NEW `node` process exists compared to step 1.
- `/teammates` renders with heading `Teammates`, a `New teammate` button, and the intro paragraph beginning `A Persona is a Markdown file describing how one teammate should behave`.
- On a clean App_Data, the page shows `No Personas yet. Choose New teammate to add one.`
- The console contains no `Persona '…' failed to start.` line and no adapter lines.
- Merely loading `/teammates` spends nothing and spawns nothing.

**Fail if — any of these**

- A new `node` process appears with the flag off -> a serious bug: the supervisor must return immediately when the flag is off, and a spawned process means real money can be spent against the user's intent.
- Loading `/teammates` itself triggers a model probe (a transient node process, or an adapter line in the console on page load) -> the probe is deliberately deferred to opening a Create/Edit card, because it spawns a throwaway adapter process.
- `/teammates` errors or renders blank with ACP off -> the page must build and be browsable with no agent process at all.

**Inconclusive if**

`O-ADAPTERS` counts only processes whose command line contains `claude-agent-acp`, so unrelated node processes are already excluded and no baseline subtraction is needed.

> [!NOTE]
> This is the recommended default configuration for every free test in this area.

### STARTUPCONFIG-28 — Team__Acp__Enabled=true with zero Personas still starts no process

**Free** · about 5 min

*Prevents a false defect report: the documentation says a process starts per Persona at startup, which is true only if Persona files exist.*

**Before you start**

- `src\Huddle.App\App_Data\Teams` is empty — `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams` prints nothing. Run `P-RESET-ALL` if unsure.
- No `Team__Acp__Enabled` variable set, so Development's `true` applies.

**Steps**

1. Run `Get-ChildItem Env:Team__*` and confirm nothing is set.
2. In `T-B` run `O-ADAPTERS`. It should print `0`.
3. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Wait 20 seconds after the app is listening.
5. In `T-B` run `O-ADAPTERS` again.
6. Open the browser, click `Teammates`, and read the body.
7. In `T-B` run `Get-ChildItem -Recurse src\Huddle.App\App_Data\Teams` and confirm it is still empty.

**Pass if — all of these**

- The app starts normally with ACP enabled (Development).
- No new `node` process exists compared to the baseline.
- `/teammates` shows `No Personas yet. Choose New teammate to add one.`
- Nothing is spent.

**Fail if — any of these**

- A `node` process appears with an EMPTY Teams folder -> a real defect: there is no Persona to start, so nothing should spawn.
- The app fails to start with ACP enabled and no Personas -> an empty Persona library must be a supported state.

**Inconclusive if**

`docs/AgencyTeam.md` says `dotnet run` starts one node process per Persona at startup, before you type anything. That is true ONLY if Persona files exist. A tester expecting a spawn against an empty library will wrongly report the flag as broken — do not file that. If the Teams folder is not empty, this is a different test: see STARTUPCONFIG-31.

> [!NOTE]
> Together with STARTUPCONFIG-27 this brackets the ACP flag: off spawns nothing, on-with-nothing-to-start spawns nothing.

### STARTUPCONFIG-29 — A Persona file under a Team sub-folder is discovered, reloads on edit in place, and a broken file is named rather than silently dropped

**Free** · about 10 min

*Catches the documented easiest way to break the Persona library — a watcher that finds a file under a sub-folder once at startup and then never reloads it — and proves a file with bad frontmatter is surfaced with its reason rather than vanishing.*

**Before you start**

- The app is stopped.
- `Team__Acp__Enabled` will be set to `false` for this test, so NO agent process is ever spawned and nothing is spent.

**Steps**

1. Run `$env:Team__Acp__Enabled = 'false'`.
2. Create a sub-folder and a valid Persona file:
`New-Item -ItemType Directory -Force src\Huddle.App\App_Data\Teams\Business`
then write this exact content to `src\Huddle.App\App_Data\Teams\Business\coo.md`:
```
---
name: 'Chief of Staff'
title: 'Chief of Staff'
alias: 'coo'
teams: ['Business']
---
You are the Chief of Staff. You keep the team honest.
```
3. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open the browser and click `Teammates`. Read what is shown.
5. Leave the app RUNNING. In a text editor, change the file's `title:` line to `title: 'Chief of Operations'` and save it.
6. Watch the `/teammates` page for up to 5 seconds WITHOUT refreshing, and read the tile's second line.
7. Still with the app running, create a BROKEN file at `src\Huddle.App\App_Data\Teams\Business\broken.md` containing only the two lines `---` and `---` (empty frontmatter).
8. Watch the `/teammates` page again for up to 5 seconds and read anything that appears ABOVE the tiles.
9. Stop the app, delete `src\Huddle.App\App_Data\Teams\Business`, and run `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- After step 3, `/teammates` shows a tile with the monogram `CS`, the name `Chief of Staff`, and a second line reading `Chief of Staff · @coo` — a Persona under a sub-folder is found by the startup scan.
- After the edit in step 5, the tile's second line updates to `Chief of Operations · @coo` WITHIN about a second and with no page refresh.
- After step 7, a section headed exactly `Files that didn't load` appears ABOVE the tiles, listing the path of `broken.md` and a reason naming the offending frontmatter field.
- No `node` process is spawned at any point (ACP is off).

**Fail if — any of these**

- The tile appears at startup but does NOT update when the file is edited in place -> the directory watcher is not including sub-directories; a Persona under a Team folder would then be frozen at whatever it said when the app started, with no error, no log and nothing on screen. This is the single easiest way to break the Persona library and is exactly the documented trap.
- The broken file simply does not appear anywhere -> a rejected file is being dropped silently instead of being named with its reason, which is the whole point of the required-field decision.
- The tile does not appear at all at startup -> the Teams directory is not being scanned recursively.

**Inconclusive if**

If the tile never appears even at startup, first confirm the file really is under `src\Huddle.App\App_Data\Teams\` (the LIVE DataDir — read the path from `/settings` if unsure) and that the frontmatter has all three required fields `name`, `title` and `alias`. A file placed under `App_Data\Teams\` will never be read: that folder is a legacy leftover and the app reads `Teams\` only.

> [!NOTE]
> This test needs no ACP, no node and no money because the Persona library is indexed independently of whether any agent process runs.

### STARTUPCONFIG-30 — A stale Logging:LogLevel key silently removes the console evidence other tests rely on

**Free** · about 8 min

*Demonstrates that a log-filter key is a namespace prefix, so the wrong key deletes the oracle rather than the behaviour — and that several tests in this area would then read as failures for the wrong reason.*

**Before you start**

- The app is stopped.
- You are willing to temporarily edit `src/Huddle.App/appsettings.Development.json`, a TRACKED file, and revert it afterwards.

**Steps**

1. Run `git status` and confirm the working tree is clean, so you can revert cleanly.
2. Launch normally and confirm the baseline: the console contains `Demo agent echo connected.` Then stop the app.
3. Open `src/Huddle.App/appsettings.Development.json` and change the `Logging.LogLevel` key `"Agency.Huddle"` to the stale name `"Team"`, leaving its value `"Debug"` unchanged. Save.
4. Relaunch and open http://localhost:5100.
5. Read the sidebar room list and send `hi @echo` in the `echo` Room.
6. Search the WHOLE console output for `Demo agent`, for `Created direct room` and for any `Agency.Huddle` category line.
7. Stop the app and run `git checkout -- src/Huddle.App/appsettings.Development.json`.
8. Relaunch and confirm the `Demo agent echo connected.` line is back.

**Pass if — all of these**

- With the stale `"Team"` key, the app behaves IDENTICALLY in the browser: the two Rooms appear and `hi @echo` gets its bold reply.
- With the stale key, the `Demo agent … connected.` line and other `Agency.Huddle` category lines are ABSENT from the console.
- No warning of any kind reports that the key matched nothing.
- After reverting the file, the console lines return.

**Fail if — any of these**

- The app behaves differently in the browser with the stale key -> log configuration is affecting behaviour, which it must not.
- A warning IS emitted naming the unmatched key -> record as an observation; a loud failure would be an improvement over the documented silence.
- The `Agency.Huddle` lines are still present with the `"Team"` key -> log categories are not derived from the namespace as documented, and the whole prefix model is different from what the docs describe.

**Inconclusive if**

If `git status` is not clean at step 1, do NOT run this test — you risk reverting someone else's work. The key point to carry forward: several tests in this area use console lines as their ONLY oracle, so this failure quietly removes the evidence rather than the behaviour. If an expected console line is missing anywhere in this script, check the log filter before concluding the behaviour did not happen.

> [!NOTE]
> Log categories come from the type's full namespace, which moved to `Agency.Huddle.*`, while the `Team:` CONFIG root deliberately did not move — that mismatch is what makes a stale `"Team"` log key so plausible and so silent.

### STARTUPCONFIG-31 — With ACP on and one Persona present, that Persona starts at launch and its tile goes Starting then Online

**Free** · about 10 min

*Proves the supervisor really launches a Persona at startup and that all three start-failure causes reach the browser as readable tile text — the surface actually under test.*

**Before you start**

- `node --version` succeeds.
- `Test-Path tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js` returns True. If False, run `pwsh tools\acp\install.ps1` first.
- The `claude` CLI is installed and logged in.
- No `Team__Acp__Enabled` variable set, so Development's `true` applies.
- The app is stopped.

**Steps**

1. Create one Persona file at `src\Huddle.App\App_Data\Teams\tester.md` with exactly this content:
```
---
name: 'Tester'
title: 'Test teammate'
alias: 'tester'
---
You are a test teammate. Answer in one short sentence.
```
2. In `T-B` run `O-ADAPTERS`. It should print `0`.
3. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open the browser and go straight to `/teammates` — do not wait.
5. Read the tile: its monogram, its second line, and its status line (a coloured dot plus a word).
6. Keep watching the status line for up to 30 seconds and record every state it passes through.
7. Open the tile by clicking it, then click `Edit`. In the `Model` dropdown choose the option whose name contains `Haiku`; in the `Effort` dropdown choose `low`. Click `Save`.
8. Look at the sidebar room list.
9. In `T-B` run `O-ADAPTERS` again.
10. If the status ever reads `Offline`, hover the status line and read the tooltip text in full; record it verbatim.
11. When finished, stop the app and delete `src\Huddle.App\App_Data\Teams\tester.md`.

**Pass if — all of these**

- The tile shows monogram `T`, a second line reading `Test teammate · @tester`, and a status line that reads `Starting` with an amber dot, then `Online` with a green dot.
- Exactly one NEW `node` process exists compared to the baseline.
- A Room named `Tester` appears in the sidebar with no refresh.
- After saving Model = Haiku and Effort = low, the card shows those values and the teammate restarts (status passes through `Starting` again).
- No tokens are spent: no Turn is taken merely by coming Online.

**Fail if — any of these**

- The tile is stuck on `Starting` forever -> the start call never returned; the Human has no way to tell a slow start from a hung one.
- The tile flips to `Offline` with NO reason in the tooltip -> a start failure with no recorded reason is exactly the surface this page exists to fix; the Human can see the failure but not act on it.
- No `node` process appears despite ACP being on and a valid Persona present -> the supervisor did not launch anything.
- No `Tester` Room appears in the sidebar -> the Persona connected but did not get a Room, or the sidebar did not update live.

**Inconclusive if**

An `Offline` tile WITH a reason is a configuration result, not a product failure — read the tooltip and act on it: `The Adapter needs authentication: …` means the `claude` CLI is not logged in; `No ACP adapter is installed for Persona '…'. Run tools/acp/install.ps1 …` means the adapter probe found nothing; a process-start error usually means `node` is not on PATH. Fix the named cause and re-run. Note this test really does launch a child process and create a session — do not run it casually — but it takes no Turn, so it spends nothing.

> [!NOTE]
> If the Model dropdown offers no options and the hint reads `This agent advertises no models, so it will use its own default.`, the model probe returned empty. A failed probe and a cancelled one look identical here — that ambiguity is a documented limit of the picker, not a defect to file.

### STARTUPCONFIG-32 — Team__Acp__TraceWire=true dumps the tool server's bearer token to the console

**Free** · about 6 min

*Proves the debugging flag works and, more importantly, proves there is no UI signal that it is on — the reason it must be turned off immediately.*

**Before you start**

- STARTUPCONFIG-31 passed, so ACP is working and one Persona exists.
- This is a THROWAWAY session only: do not screen-record, do not paste the console anywhere, and do not run it in a shared terminal.

**Steps**

1. Stop the app. Run `$env:Team__Acp__TraceWire = 'true'`.
2. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Watch the console as the Persona starts.
4. Search the console output for the category `Agency.Huddle.Acp.Wire`.
5. Look for an `Authorization` header value in the traced JSON-RPC traffic.
6. Open http://localhost:5100 and `/teammates`, and look for ANY visual indication anywhere in the browser that tracing is enabled.
7. Stop the app IMMEDIATELY and run `Remove-Item Env:Team__Acp__TraceWire`.
8. Close the console window, or clear its scrollback, so the token is not left on screen.

**Pass if — all of these**

- The console fills with JSON-RPC wire traffic under the category `Agency.Huddle.Acp.Wire`.
- An `Authorization` bearer value is visible in that traffic.
- The browser UI is COMPLETELY unchanged — no banner, no badge, no indication anywhere that tracing is on.
- With the variable removed, the `Agency.Huddle.Acp.Wire` lines disappear on the next launch.

**Fail if — any of these**

- No `Agency.Huddle.Acp.Wire` lines appear -> either the flag did not bind, or the log filter is not letting that category through. Check BOTH before filing; the trace category is a hand-written string, so a `Logging:LogLevel` key that does not match it produces no trace at all and looks identical to the flag not working (see STARTUPCONFIG-30).
- The browser shows a warning that tracing is on -> record as an observation; that would be an improvement over the documented silence, not a defect.

**Inconclusive if**

If you see no trace lines, this test is inconclusive rather than failing until you have ruled out the log filter: confirm `appsettings.Development.json` still has the `Agency.Huddle` key at `Debug` and that no stale `"Team"` key has replaced it. CLEAR THE VARIABLE as soon as you are done — a tester who leaves it set will leak a bearer token into a shared log or a screen recording.

> [!NOTE]
> The trace category is `Agency.Huddle.Acp.Wire`, a hand-written string rather than one derived from a type, which is why a filter mismatch is so easy here.

### STARTUPCONFIG-33 — A spent per-Persona token Budget reads as Degraded on the Teammate tile and in the Room banner

**💰 Spends money** · about 12 min

*Proves the third layer of the spend cap reaches the Human on both surfaces with a readable reason, in this project's own vocabulary.*

**Before you start**

- STARTUPCONFIG-31 passed and the `Tester` Persona is present, configured with Model = Haiku and Effort = low.
- The `claude` CLI is logged in.
- The app is stopped.

**Steps**

1. Confirm the Persona's Model is Haiku and Effort is low: open `/teammates`, click the tile, and read the Model and Effort shown. Fix them via `Edit` if not.
2. Stop the app. Run `$env:Team__Acp__TokenBudget = '2000'` — small enough that one Turn exceeds it.
3. Relaunch and wait for the `Tester` tile to read `Online`.
4. Open the `Tester` Room in the sidebar. Type exactly `Say hi in five words.` and press Enter.
5. Wait for the reply and confirm it arrives normally.
6. Type exactly `Say bye in five words.` and press Enter.
7. Wait 30 seconds and record whether any reply arrives.
8. Go to `/teammates` and read the `Tester` tile's status line. Hover the status and read the tooltip in full.
9. Go back to the `Tester` Room and read the area between the message list and the message box.
10. Search the console for a line containing `token budget`.
11. Do NOT stop the app or clear the variable — STARTUPCONFIG-34 continues from this exact state.

**Pass if — all of these**

- The FIRST message gets a normal reply.
- The SECOND message gets NO reply.
- The `/teammates` tile reads `Degraded` with an amber dot.
- The tile's tooltip reads exactly `The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.`
- The Room shows an alert strip between the message list and the message box reading `Tester is Degraded: The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.`
- The console contains `Persona 'Tester' has spent its token budget of 2000 and is taking no more turns until a human speaks to it.`

**Fail if — any of these**

- The tile goes `Degraded` but shows NO reason -> before ADR-0008 this condition was log-only; a Degraded tile with nothing to read is exactly that regression returning.
- Nothing changes at all — no reply, no tile change, no banner -> the Human is left with silence and no explanation, which is the failure this whole surface exists to prevent.
- The reason uses words like `quota`, `rate limit`, `LLM` or `tokens remaining` instead of the defined vocabulary (Budget, Turn, Human, Persona) -> a copy defect; the wording is part of the contract.
- The reason appears on the tile but not in the Room, or vice versa -> the two surfaces are deriving status independently instead of from one resolver.

**Inconclusive if**

The check runs at the START of a work item, so the FIRST Turn ALWAYS completes no matter how small the Budget. A tester expecting the very first message to be refused will wrongly report the cap as broken — do not file that. If the second message DOES get a reply, the Turn simply did not exceed 2000 yet: send one more short message and re-check before judging. The Budget is also per Persona, not per Room, and has no Continue prompt: it only ever reads as Degraded.

> [!NOTE]
> COST: roughly two short Haiku turns at low effort — a few cents at most. Keep every prompt to a handful of words. A local model emits no usage updates at all, so this cap is inert against one; use the Claude adapter.

### STARTUPCONFIG-34 — A Human Message clears a token-Budget Degraded state and lets the Persona work again

**💰 Spends money** · about 6 min

*Proves the documented reset: the one condition a Human Message is specified to clear, and only that one.*

**Before you start**

- STARTUPCONFIG-33 has just left the `Tester` Persona Degraded, with the app still running and `Team__Acp__TokenBudget` still set to `2000`.

**Steps**

1. In the `Tester` Room, confirm the alert strip is still showing.
2. Type exactly `Hello again.` and press Enter.
3. Wait up to 60 seconds and watch the message area.
4. Look at the area between the message list and the message box.
5. Go to `/teammates` and read the `Tester` tile's status line and dot colour.
6. Stop the app. Run `Remove-Item Env:Team__Acp__TokenBudget`.
7. Delete the test Persona: `Remove-Item src\Huddle.App\App_Data\Teams\tester.md`.
8. Run `Get-ChildItem Env:Team__*` and confirm nothing remains set.

**Pass if — all of these**

- The Persona answers the new message.
- The Room's alert strip disappears.
- The `/teammates` tile returns to `Online` with a green dot.

**Fail if — any of these**

- The tile stays `Degraded` after a Human Message -> the reset signal was lost, and the only way back would be a restart or a manual `Restart` from the card.
- The alert strip disappears but no reply ever arrives -> the status was cleared without the runner actually resuming.
- The Room's strip clears while the tile stays Degraded (or vice versa) -> the two surfaces disagree, meaning status is not coming from one resolver.

**Inconclusive if**

A Human Message is documented to clear THIS specific condition and nothing else. If the tile stays Degraded for an UNRELATED reason (hover it and read the tooltip — an authentication or adapter message, say), that is correct behaviour, not this test's failure. In that case record the tooltip verbatim and mark the test inconclusive.

> [!NOTE]
> COST: one more short Haiku turn at low effort. This test must run immediately after STARTUPCONFIG-33 — restarting the app would clear the Degraded state on its own (the counter is in memory, per runner) and the test would prove nothing. Cleanup steps 6-8 return the environment to its default state for the next tester.

---

Back to [the manual test script](../manual-tests.md).
