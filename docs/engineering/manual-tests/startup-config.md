# Startup, configuration and first-run state

Prove that Agency.Huddle launches on http://localhost:5100 with the state a first-time user is promised — no Rooms yet, an App_Data directory built from nothing, a styled two-column shell — and that every `Team:` configuration key and `Team__*` environment variable that a tester can reach actually changes what the browser shows. The area exists because nearly everything in it is a startup side effect (a directory created, a pipe bound, a Persona process spawned) that no unit test observes end to end, and because this repo's most expensive failures here are SILENT: a single-underscore environment variable that binds to nothing, a stale `Logging:LogLevel` key that deletes the console evidence other tests rely on, and an `@Assets` key that 404s a stylesheet with no build warning, no startup error and no log line. Several tests below exist purely as controls for those silences.

**27 tests** · 23 free, 4 paid 💰 · about 3.1 hours.

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
5. A fresh install has no Rooms with ACP off, which is what `P-LAUNCH-FREE` gives. The built-in **Chief of Staff** (alias `cos`) is seeded at every start whatever `Acp:Enabled` says, so its tile is always on **Teammates**; with ACP on it also connects and gets a direct Room (a paid Greeting Turn on the real adapter), which is why a launch with ACP on that is not `P-LAUNCH-UAT` is never "empty". Tests that need a Teammate to answer name [`P-LAUNCH-UAT`](common.md#p-launch-uat) in **Before you start** and use Ada and Bo. That procedure keeps its data in `C:\seeds\huddle\uat\data`, not `src\Huddle.App\App_Data`, so a disk, database or reset step in such a test points there, and re-running the seeder line of `P-LAUNCH-UAT` is its reset.
6. For the ACP tests at the end only: `node --version` must print a version, `Test-Path tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js` must be True (if not, run `pwsh tools\acp\install.ps1`), and the Claude login requirement in [§0.1](../manual-tests.md#01-what-you-need) must be met. Tests that need this say so in their preconditions.

## Tests

### STARTUPCONFIG-01 — The documented launch command serves the styled app shell on http://localhost:5100

**Free** · about 5 min

*Proves the app starts in Development on the documented port and renders its two-column shell, and establishes which process you are actually looking at for every later test.*

**Before you start**

- `dotnet build Huddle.slnx` reported 0 warnings.
- Nothing is listening on port 5100 and no leftover `dotnet` process of this app is running.

**Steps**

1. In `T-A`, at `<repo>`, run `Get-ChildItem Env:Team__*` and confirm it prints nothing. If it prints anything, clear each with `Remove-Item Env:<name>` before continuing.
2. Launch with [`P-LAUNCH-FREE`](common.md#p-launch-free): set `$env:Team__Acp__Enabled = 'false'` (the cost guard; leaving it unset would start the Chief of Staff's adapter, which is paid), then run `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Read the console output as it appears and find the lines beginning `Now listening on:` and `Hosting environment:`.
4. If a browser did not open by itself, open one and navigate to http://localhost:5100.
5. Look at the left-hand sidebar and read its controls top to bottom.
6. Look at the main column on the right.

**Pass if — all of these**

- The console prints `Now listening on: http://localhost:5100`.
- The console prints `Hosting environment: Development`.
- The page renders as TWO columns: a narrow sidebar on the left and a wide main column on the right.
- The sidebar contains, in this order: a button labelled exactly `New chat`, a room list that reads `No rooms yet. Start an agent to create one.`, a link labelled exactly `Teammates`, and, last, an icon-only Settings link (a cog with the accessible name `Settings`, tooltip `Settings`). Other entries (Teams, Tasks, Library, Archived chats) may sit between them; their order and content are not judged here.
- The main column shows the same empty state, `No rooms yet. Start an agent to create one.`, and no Room.

**Fail if — any of these**

- The console throws an address-in-use / `Failed to bind to address` exception and the process exits -> something else already holds 5100; the browser is showing a DIFFERENT process and every later result would be about that one. Stop it and restart.
- The console prints `Hosting environment: Production` -> the launch profile was bypassed (you ran the built DLL, or passed `--no-launch-profile`); ACP will be off and the port will be 5000. Relaunch with the documented command.
- The page renders as unstyled black-on-white text with no sidebar layout -> a stylesheet did not load. This is a real, documented regression shape, not a rendering hiccup. Run STARTUPCONFIG-02 immediately and file from there.
- The `Teammates` link text, or the `Settings` accessible name, reads anything else -> navigation copy has drifted from the shipped labels.
- A Room is listed in the sidebar -> the cost guard did not take (`O-ADAPTERS` should print `0`), so the Chief of Staff connected and got a Room. Stop, fix the variable and relaunch.

**Inconclusive if**

If the browser shows the page but the console window you started shows no `Now listening on:` line at all, you are looking at a different (older) instance. Close every browser tab, stop every `dotnet` process (`Get-Process dotnet | Stop-Process`), and start again from step 2. Do not record a result until the console line and the page agree.

> [!NOTE]
> Leave this instance running — tests 02, 08, 09 and 10 use it. Tests 06 and 07 need [`P-LAUNCH-UAT`](common.md#p-launch-uat) instead.

### STARTUPCONFIG-02 — Every stylesheet in the document head returns 200 — the silent @Assets regression

**Free** · about 5 min

*Catches this repo's most expensive documented silent failure: an unresolved `@Assets` key renders as an ordinary href that 404s, with no build warning, no startup error and no log line, so the only witness is an HTTP round-trip. The stylesheet list changed with the MudBlazor migration: `theme.css` and the hand-built theming system it belonged to are gone (Stage 2 — see [ADR-0010](../../adr/0010-a-theme-is-a-mudblazor-theme.md)), and two new stylesheets were added — MudBlazor's own, and a new `app-vars.css` carrying the one token (`--font-mono`) that has no MudBlazor equivalent. A fifth request, Google Fonts' Inter stylesheet, is third-party and is listed first.*

**Before you start**

- The app from STARTUPCONFIG-01 is running and http://localhost:5100 is open.

**Steps**

1. Press F12 to open DevTools and select the **Network** tab.
2. Tick **Disable cache**, then press Ctrl+F5 to hard-reload the page.
3. Set the Network filter to **CSS**.
4. Read the Status column for every row, and note the ORDER they appear in.
5. Click each CSS row and read its full Request URL.
6. Close DevTools. Scroll the page all the way to the very bottom of the viewport and look for a strip reading `An unhandled error has occurred.` with a `Reload` link and a `🗙` character.

**Pass if — all of these**

- Exactly FIVE stylesheet requests appear, all returning status 200, in this order: the font stylesheet `https://fonts.googleapis.com/css2?family=Inter…` first (`Components/App.razor:13`), then `_content/MudBlazor/MudBlazor.min.css`, then `app`, `app-vars`, and `Huddle.App.styles`. The font row is third-party and is excluded from every check below that talks about "the four".
- The first of the four (`MudBlazor.min.css`) is NOT fingerprinted — it is a static asset served straight from the NuGet package, not through `@Assets[...]`, so this is correct and deliberate.
- Each of the LAST three request URLs IS fingerprinted — the filename carries a hash segment (for example `app.ce2n94aiaf.css`), and is therefore not byte-identical to the key written in the markup (`app.css`).
- No `An unhandled error has occurred.` strip is visible anywhere on the page, top or bottom.
- The layout is genuinely styled: a `MudDrawer` sidebar with a visible background and a separate main column. (A first-run page has no Messages, so there is nothing else to judge.)

**Fail if — any of these**

- Any CSS request returns 404 -> an `@Assets["…"]` key in `Components/App.razor` no longer matches a real asset; it was emitted verbatim as a plain href instead of throwing. This is the documented month-long regression; file it with the exact 404 URL.
- Any of the LAST three request URLs is NOT fingerprinted and exactly equals the key in the markup (e.g. `/app.css`) -> the same unresolved-key failure, even if the server happens to answer 200 for it.
- The FIRST URL (`MudBlazor.min.css`) carries a fingerprint, or is missing entirely -> either it was routed through the fingerprinting pipeline by mistake, or the reference was dropped from `App.razor`.
- The error strip is visible at the bottom of the page on a fresh load with no interaction -> the page is running without `app.css`, whose `position: fixed` is what would normally float that strip; a stylesheet is missing.
- Fewer than four of the app's own stylesheets are requested, or they appear in a different order -> a `<link>` was dropped from, or reordered in, the head. Order matters: MudBlazor's reset must load FIRST among the four so the app's own bare-element CSS rules win source-order ties.

**Inconclusive if**

If DevTools shows CSS rows served `(from disk cache)` with no status code, you did not disable the cache. Re-tick **Disable cache** and hard-reload before judging. If the machine is offline or your browser blocks third-party requests, the Google Fonts row fails or is missing; that is not a result, so judge the four app stylesheets and ignore the font row.

> [!NOTE]
> This behaviour genuinely cannot be judged without a browser network panel — 'the page looks fine' is not evidence, because a partially-loaded shell still renders text. See SHELLNAV-01 for the same check driven from `curl.exe` instead of DevTools — the automated guard `tests/Huddle.Tests/Ui/AppStylesheetTests.cs` covers exactly this for stylesheets, so a failure here means that test also broke.

### STARTUPCONFIG-06 — Every navigation surface is reachable from a cold start

**Free** · about 6 min

*Proves the shell's four navigation controls all resolve, with the exact labels and empty-state copy a first-time user sees.*

**Before you start**

- `P-LAUNCH-UAT` has been run (stop any other instance first) and Ada and Bo are Online, so at least one Room exists.

**Steps**

1. Click `New chat` in the sidebar.
2. Read the panel that opens: note whether it lists agents with checkboxes and a coloured dot each, and whether a button is present at the bottom.
3. Without ticking anything, try to click the `Start chat` button.
4. Tick the checkbox beside `Ada`.
5. Try the `Start chat` button again — note whether it is now clickable. Then click `New chat` again to close the panel WITHOUT starting a chat.
6. Click `Teammates` in the sidebar and read the address bar and the page heading.
7. Click the Settings icon (the cog at the bottom of the sidebar, accessible name `Settings`) and read the address bar and the page heading.
8. Click a Room in the sidebar room list and confirm the main column shows that Room.

**Pass if — all of these**

- The `New chat` panel lists `Ada` and `Bo`, each with a checkbox and a small status dot. The built-in `Chief of Staff` is listed too once it is Online; ignore it.
- With nothing ticked, the `Start chat` button is present but DISABLED (clicking does nothing).
- With `Ada` ticked, `Start chat` becomes enabled.
- `Teammates` navigates to `/teammates` and the page heading reads exactly `Teammates`.
- The Settings icon navigates to `/settings` and the page heading reads exactly `Settings`.
- Clicking a Room navigates to `/rooms/<id>` and the main column heading is that Room's name.

**Fail if — any of these**

- `Start chat` is clickable with nothing ticked -> the selection guard is gone and an empty Room can be created.
- `Teammates` or the Settings icon leads to a blank page or an error -> the route was dropped from `Routes.razor` or the page throws on load.
- The `Teammates` link text differs, or the Settings icon's accessible name is not `Settings` -> shipped navigation copy has drifted.

**Inconclusive if**

If the `New chat` panel says `No agents have connected yet…` instead of listing agents, Ada and Bo have not come Online yet (wait a few seconds and reopen the panel) or `P-LAUNCH-UAT` was not the launch used. Fix that first and re-run; record this test as inconclusive meanwhile.

> [!NOTE]
> Do not actually click `Start chat` here — STARTUPCONFIG-07 owns that path and needs a known Room count first.

### STARTUPCONFIG-07 — Start chat on one known agent reuses its Room; two agents create one new named Room

**Free** · about 6 min

*Proves the Room-reuse rule that prevents a duplicate two-Member Room appearing every time the Human starts a chat with an agent that already has one.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada and Bo are Online, so exactly three Rooms exist: `Ada`, `Bo` (both empty) and the built-in Chief of Staff's own Room (it is seeded at every start, connects on the mock adapter and gets a direct Room; `common.md` names it as present but not part of the cast).

**Steps**

1. Click the `Ada` Room, type `hello ada` and press Enter, and wait for Ada's reply to settle. Then count the entries in the sidebar room list and write the number down (expected: 3, counting the Chief of Staff's Room).
2. Click `New chat`, tick ONLY `Ada`, and click `Start chat`.
3. Read the address bar and the main column heading, then count the sidebar room list again.
4. Click `New chat`, tick BOTH `Ada` and `Bo`, and click `Start chat`.
5. Read the main column heading and count the sidebar room list again.
6. Click `New chat`, tick BOTH `Ada` and `Bo` again, and click `Start chat` once more.
7. Count the sidebar room list a final time.

**Pass if — all of these**

- After step 2, the room list still shows the same 3 entries — no new Room was created.
- After step 2, the browser lands on the EXISTING `Ada` Room, and its transcript still contains `hello ada` and Ada's reply from step 1.
- After step 4, exactly one new Room exists (4 total) and its heading reads `Ada, Bo`.
- After step 6, the count behaves consistently with step 4's rule — record exactly what it does.

**Fail if — any of these**

- Step 2 produces a SECOND Room also named `Ada` (4 entries) -> single-agent selection stopped routing through the exact-membership lookup; every `Start chat` will now mint duplicates.
- Step 2 lands on an empty transcript rather than the existing `Ada` history -> a new Room was created even though the sidebar count looks right; check the Room id in the address bar against the original.
- The two-agent Room's heading is not `Ada, Bo` -> the multi-member Room naming rule changed.

**Inconclusive if**

If you have already created extra Rooms by hand in earlier exploration, the counts will not match. Re-run the seeder line of `P-LAUNCH-UAT` (that is the reset), relaunch, and start this test from step 1. If step 6's behaviour is ambiguous, record it verbatim as an observation rather than a verdict — the reuse rule is defined for single-agent selection; multi-agent Room reuse is not specified here.

> [!NOTE]
> Optional oracle if `sqlite3` is available: `sqlite3 C:\seeds\huddle\uat\data\team.db "SELECT COUNT(*) FROM rooms;"` before and after each Start chat.

### STARTUPCONFIG-08 — /teammates lists Personas only — pipe clients never appear there

**Free** · about 4 min

*Pins the category boundary that is most often misread as a bug: a Teammate tile comes from a Persona file, an Agent comes from anything that said hello on the pipe.*

**Before you start**

- The app is running on `P-LAUNCH-FREE` with the default `src\Huddle.App\App_Data`.
- No Persona of your own exists: `Get-ChildItem src\Huddle.App\App_Data\Teammates -Directory` lists no folder other than `Chief of Staff` (the built-in Chief of Staff is a Persona, is seeded at every start and is expected). A Persona is `Teammates\<Name>\<Name>.md`.

**Steps**

1. In `T-C`, run `pwsh tools/echo-bot.ps1 -Name mybot` and wait for its welcome line.
2. Confirm the sidebar shows a `mybot` Room.
3. Click `Teammates` in the sidebar.
4. Read the page heading, the button beside it, the intro paragraph, and whatever appears below.
5. Scan the whole page for the word `mybot`.
6. Press Ctrl+C in `T-C`.

**Pass if — all of these**

- The heading reads exactly `Teammates` and a text button first in the toolbar beside it reads `New teammate` (not upper-cased).
- The intro paragraph begins `A Persona is a Markdown file describing how one teammate should behave`.
- The page lists only Personas that have a definition file at `App_Data\Teammates\<Name>\<Name>.md`: the built-in Chief of Staff, which is always there, and nothing you did not create. (`No Personas yet. Choose New teammate to add one.` cannot appear on this launch; the Chief of Staff is seeded whatever `Acp:Enabled` says.)
- `mybot` does not appear anywhere on the page, although its Room is in the sidebar.

**Fail if — any of these**

- `mybot` appears as a Teammate tile -> pipe-connected Agents are leaking into the Persona list; the two concepts have been conflated.
- The page shows tiles for names that have no `Teammates\<Name>\<Name>.md` file -> the Persona index is reading somewhere other than the Teammates directory (a `.md` under `App_Data\Teams` must never produce a tile).

**Inconclusive if**

Expecting `mybot` here is a category error, not a defect — do not file it. If `App_Data\Teammates` holds a folder an earlier test left there, remove it (or note it) before judging the list.

> [!NOTE]
> `App_Data\Teams\` holds Team folders, notes and Tasks, never Personas, so nothing there produces a tile.

### STARTUPCONFIG-09 — Settings' Prompts tab on a first run says prompts.json is absent, and offers nothing to save or reset

**Free** · about 8 min

*Proves the app never creates its override file until the Human saves, that Save/Reset are correctly disabled with nothing edited, and that the tab route falls back rather than 404ing. Also establishes /settings as the quickest oracle for which DataDir is live.*

**Before you start**

- The app is running on `P-LAUNCH-FREE` with the default `src\Huddle.App\App_Data` (if STARTUPCONFIG-06 or -07 left `P-LAUNCH-UAT` running, stop it and relaunch `P-LAUNCH-FREE`).
- No `prompts.json` exists: `Test-Path src\Huddle.App\App_Data\prompts.json` returns False. If it returns True, run `P-RESET-ALL` and relaunch.

**Steps**

1. In `T-B` run `Test-Path src\Huddle.App\App_Data\prompts.json` and note the result.
2. In the browser click the Settings icon (accessible name `Settings`) in the sidebar.
3. Read the page heading and the tab buttons in the tab rail, in order.
4. Read the paragraph that names where overrides are stored, and note the absolute path it prints.
5. Try to click the `Save` button at the bottom of the form.
6. Try to click the `Reset all to defaults` button in the header.
7. Click the `Prompts` tab button and read the address bar.
8. Navigate the browser directly to http://localhost:5100/settings/nonsense and read what renders.
9. Go back to `/settings`, edit ANY one text field on the Prompts tab (add a single character), and look at the `Save` button again.
10. Click `Save`. Then in `T-B` run `Test-Path src\Huddle.App\App_Data\prompts.json` again.

**Pass if — all of these**

- Step 1 returns False — the file does not exist before any save.
- The heading reads exactly `Settings`; the tab rail holds six tabs, in this order: `Prompts`, `Appearance`, `Personas`, `Skills`, `Team Types`, `Library`, with `Prompts` active by default (`Settings.razor:36-76`). Only the Prompts tab is judged here; the others are not opened.
- The overrides paragraph contains the sentence `The file does not exist until the first time you save here, so it being absent is expected, not a bug.`
- The absolute path printed in that paragraph ends `\src\Huddle.App\App_Data\prompts.json` — matching the live DataDir.
- With nothing edited, `Save` is disabled and `Reset all to defaults` is disabled.
- Clicking the `Prompts` tab changes the address bar to `/settings/prompts`.
- `/settings/nonsense` renders the Prompts tab rather than a 404 or an error page.
- After one edit, `Save` becomes enabled.
- After clicking `Save`, step 10 returns True.

**Fail if — any of these**

- Step 1 returns True on a clean App_Data -> the app created `prompts.json` on its own; it must never do that.
- `Save` is enabled with nothing edited, or `Reset all to defaults` is enabled with nothing modified -> the dirty/modified state tracking is broken and the page invites a write that changes nothing.
- The printed path does NOT point at the App_Data you believe is live -> either `Team__DataDir` is still set from an earlier test, or the working directory is not the project folder. Resolve that before trusting ANY other test's disk oracle.
- `/settings/nonsense` 404s or throws -> the unknown-tab fallback was removed.

**Inconclusive if**

Do not confuse `prompts.json` (the override file, under App_Data, absent until saved) with `prompts.default.json` (generated, sits beside the binary at `src\Huddle.App\bin\Debug\net10.0\prompts.default.json`, always present). Finding `prompts.default.json` is not a result. If you cannot tell which field you edited, undo by clicking that field's own reset control and re-reading the Save button state.

> [!NOTE]
> This test deliberately leaves a `prompts.json` behind. Delete it (`Remove-Item src\Huddle.App\App_Data\prompts.json`) if a later test needs a first-run state.

### STARTUPCONFIG-10 — Appearance tab on a first run reads Huddle Light; picking a dark Theme writes appearance.json and applies immediately, with no reload

**Free** · about 8 min

*Proves the Theme choice persists to disk, that it applies live, and that the app never creates appearance.json unbidden. Per [ADR-0010](../../adr/0010-a-theme-is-a-mudblazor-theme.md) the full-page reload is gone — `MudThemeProvider` lives in the render tree, so a change is an ordinary re-render, not a navigation. Per [ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md) there is **no** light/dark control: a Theme carries its own palette, so choosing a dark Theme is how you get dark. The tab's Theme picker is a list, not a select, and the tab also holds an accent picker (Custom Accent only), a Library pane Right/Left choice and a **Your avatar** section; this test judges only the Theme list and the file.*

**Before you start**

- The app is running on `P-LAUNCH-FREE` with the default `src\Huddle.App\App_Data` (if `P-LAUNCH-UAT` is still running, stop it and relaunch `P-LAUNCH-FREE`).
- `Test-Path src\Huddle.App\App_Data\appearance.json` returns False. If True, delete the file and reload the page before starting.

**Steps**

1. In `T-B` run `Test-Path src\Huddle.App\App_Data\appearance.json` and note the result.
2. In the browser go to `/settings` and click the `Appearance` tab.
3. Read the intro paragraph.
4. Read the Theme list (labelled `Theme`): its group headings, which entry is first and which entry is selected. Count the entries.
5. Look for any other control that chooses light or dark (a select, a radio group, a switch) and note whether one exists.
6. Read the paragraph naming where the selection is stored.
7. Click `Huddle Dark` in the Theme list.
8. Watch the browser closely: note whether the whole page reloads (tab spinner, a full repaint from the server) or whether just the colours change in place with no navigation.
9. Read the page's colours after the change settles.
10. In `T-B` run `Test-Path src\Huddle.App\App_Data\appearance.json` and then `Get-Content src\Huddle.App\App_Data\appearance.json`.
11. Click `Huddle Light` in the Theme list to put the app back.

**Pass if — all of these**

- Step 1 returns False.
- The intro reads exactly `Pick a theme. Each theme is either a light or a dark one, so choosing it sets the application's light or dark colours too.`
- The Theme list holds 21 Themes (Custom Accent Light and Dark included) under `Light`, `Dark` and `High contrast` headings, with `Huddle Light` first and already selected. `APPEARANCETHEME-03` pins the full list; here only the count of 21 and `Huddle Light` being first and selected matter.
- **There is no light/dark control** — no `Appearance` select, no light/dark/system choice of any kind.- The file-path paragraph reads `The selection is stored at <path>`, where `<path>` is the absolute path ending `\src\Huddle.App\App_Data\appearance.json`, and states the file does not exist until a choice is saved here, so its absence is expected, not a bug. The path is rendered, not the literal text `this.AppearanceStore.FilePath` (that would be the missing-`@` defect).
- Choosing `Huddle Dark` applies IMMEDIATELY — the page repaints dark with no navigation, no tab spinner, no address-bar or history change. The page body is `#1c1c1b`.
- Step 10 returns True, and the file is indented JSON whose `theme` key is `huddle-dark` — the lowercase id, never the display label `Huddle Dark`, and with no `dark` key. Whitespace is not judged (the app writes it indented; `AppearanceStore.cs:73-75`).

**Fail if — any of these**

- Step 1 returns True on a clean App_Data -> the app created `appearance.json` on its own; it must never do that.
- The whole page reloads (spinner, full repaint, or the address bar flickers) -> the deleted full-page-reload path was reintroduced; ADR-0010 explicitly removed it because `MudThemeProvider` renders in the tree now.
- The file stores the display label (`Huddle Dark`) rather than the id `huddle-dark` -> a display name was persisted where the stored value belongs.
- The app writes a `dark` key of its own -> the retired preference is back; see ADR-0017.
- A light/dark/system control is present -> the retired preference is back on the UI side.
- The picker offers a different count than 21 or a different first entry -> the catalog changed; re-check `ThemeCatalog.BuiltIn` before filing, since a new built-in Theme is a real feature addition, not necessarily a defect.
- Choosing `Huddle Dark` on a light-mode OS leaves the page light -> `MainLayout` is not deriving `MudThemeProvider`'s `IsDarkMode` from `ThemeDescriptor.Mode`.

**Inconclusive if**

The app ignores the OS colour mode (ADR-0017): a dark Theme stays dark on a light OS and a light one stays light on a dark OS. The choice is per installation, so a second browser or a private window showing the same theme is also correct, not a session bug.

> [!NOTE]
> There is no `Overrides that didn't load` section any more — per-token customisation and its allowlist are gone with `theme.css` (see `docs/engineering/known-limits.md`). A bad `theme` id in the file is now logged only, never shown on the Appearance tab; do not look for an on-screen refusal message.

### STARTUPCONFIG-11 — Rooms and Transcripts survive a restart; Drafts and the Budget do not

**Free** · about 8 min

*Proves durability of the things that must persist and confirms the two documented in-memory losses, so neither is misfiled.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada and Bo are Online. In each of their Rooms send `hello` and wait for the reply, so at least four messages exist across the two Rooms.

**Steps**

1. Open `Ada` and count its messages; write down the count, the first sender name, and the first timestamp shown.
2. Open `Bo`, send `hello again`, wait for the reply, and count its messages the same way.
3. Count the entries in the sidebar room list.
4. Switch to the run window and press Ctrl+C to stop the app. Wait for the prompt to return.
5. Relaunch by re-running ONLY the `$mock = ...` line and the `dotnet run` command of `P-LAUNCH-UAT`. Do NOT re-run the seeder line: it wipes `C:\seeds\huddle\uat` and would erase the very Rooms this test checks.
6. Reopen http://localhost:5100 and count the sidebar room list again.
7. Open `Ada` and compare its message count, order, sender names and the first timestamp against what you wrote down.
8. Open `Bo` and do the same.

**Pass if — all of these**

- The sidebar room list holds the SAME number of Rooms after the restart as before — it does not grow.
- Each Room's messages are all present, in the same order, with the same sender names and the same displayed times.
- No streaming row, caret or `Stop` button is present after the restart.

**Fail if — any of these**

- Messages are missing after the restart -> transcript durability is broken. Before filing, confirm the DataDir did not change (open `/settings` and read the path in the Prompts tab's overrides paragraph).
- A DUPLICATE Room appears for `Ada` or `Bo` after the restart -> reconnecting under the same name created a second two-Member Room instead of re-attaching to the existing one by exact membership. This is a real bug.
- The room count grows by more than the agents that reconnected -> the same duplication bug in another shape.

**Inconclusive if**

A Draft that was mid-stream when you pressed Ctrl+C is simply gone — that is a documented limit (Drafts are in memory, per process), not a result. Likewise, any Budget pause you had reached is cleared by the restart; that is STARTUPCONFIG-15's subject, not a failure here. If `Get-ChildItem C:\seeds\huddle\uat\data\rooms` shows no `.jsonl` files at all, you are looking at the wrong data folder — re-read the path from `/settings`.

> [!NOTE]
> Optional oracle: count the lines of `C:\seeds\huddle\uat\data\rooms\<roomId>.jsonl` before and after with `(Get-Content <path> | Measure-Object -Line).Lines`.

### STARTUPCONFIG-12 — Deleting App_Data while running fails; stopping first makes the clean slate reliable

**Free** · about 8 min

*Establishes the reset procedure every later configuration test depends on, and proves a clean run rebuilds App_Data, the Teammates folder with its built-in Chief of Staff, and team.db from nothing, with no Rooms in it.*

**Before you start**

- The app is running and `src\Huddle.App\App_Data` exists.

**Steps**

1. With the app STILL RUNNING, in a second pwsh window run `Remove-Item -Recurse -Force src\Huddle.App\App_Data` and read the error.
2. Switch to the run window and press Ctrl+C. Wait for the prompt.
3. Run `Remove-Item -Recurse -Force src\Huddle.App\App_Data` again.
4. Run `Test-Path src\Huddle.App\App_Data` and confirm it returns False.
5. Relaunch with [`P-LAUNCH-FREE`](common.md#p-launch-free) (the cost guard `$env:Team__Acp__Enabled = 'false'` must be set in that window first; an unguarded relaunch would start the Chief of Staff's adapter and give it a Room).
6. Open http://localhost:5100 and read the sidebar room list.
7. In `T-B` run `Get-ChildItem -Recurse -Force src\Huddle.App\App_Data` and read the full listing.

**Pass if — all of these**

- Step 1 fails with a file-in-use error. It usually names `team.db`, but the open log file under `App_Data\logs` is locked too and the recursive delete may report that one first; either is correct.
- After the app is stopped, step 3 succeeds and step 4 returns False.
- After relaunch with the guard, the sidebar room list reads `No rooms yet. Start an agent to create one.` — a fresh App_Data holds no Rooms.
- The App_Data listing contains `team.db`, `team.db-wal` and `team.db-shm`, a `Teammates` folder holding `Chief of Staff\Chief of Staff.md` and the marker file `.layout-migrated`, a `Skills` folder, an `avatars` folder, and (because Development enables `Team:FileLog`) a `logs` folder. Other folders created by features (a `Teams` folder, for one) may appear; they are not judged.
- The listing does NOT contain `prompts.json`, `appearance.json`, or a top-level `work` directory.
- No `rooms` directory exists yet; it appears only after the first Message is sent in some Room.

**Fail if — any of these**

- `Teammates\Chief of Staff\Chief of Staff.md` is missing from the fresh App_Data -> the Persona store or the built-in seeder never ran; `/teammates` will be broken too.
- App_Data is created somewhere other than `src\Huddle.App\` -> the working directory was not the project folder. Testers report this as 'my rooms disappeared' when the data simply moved. Check where you launched from.
- Startup throws an exception naming `Directory.CreateDirectory` -> the path is not writable.
- `prompts.json` or `appearance.json` exists on a clean run -> the app created an override file it must never create (see STARTUPCONFIG-09 and -10).

**Inconclusive if**

On some runs the SQLite handle lingers briefly even after the app has exited — retry the delete once after a few seconds before filing anything. The failure in step 1 is a KNOWN, pre-existing, deliberately-unfixed bug (the team directory is not disposable and SQLite pooling holds the file); it is the expected result here, not a defect to report. Never delete only `team.db` and leave `rooms\` behind — that orphans transcripts against Room ids that no longer exist.

> [!NOTE]
> Every test below that says 'clean App_Data' means exactly steps 2-4 of this test.

### STARTUPCONFIG-13 — Team__AgentMessageBudget=1 pauses the Room after one agent reply and shows the Continue prompt

**Free** · about 8 min

*Proves the runaway-loop guard reaches the browser, in the right place on the page, with the right copy, and that a Human Message resets it.*

**Before you start**

- No other `Team__*` variables set.
- Ada and Bo are needed, so this test launches `P-LAUNCH-UAT` itself (below), with the budget variable set first. The test uses a three-Member Room (`Ada, Bo`) so that `anything` is not answered: in the two-Member `Ada` Room Ada would answer it, exhaust a budget of 1 again, and bring the pause block straight back.

**Steps**

1. Stop the app (Ctrl+C).
2. In the run window set `$env:Team__AgentMessageBudget = '1'`.
3. Relaunch in that SAME window by running all of `P-LAUNCH-UAT` (seeder line, `$mock` line and `dotnet run` command), so the Rooms start empty and the variable is inherited by the app.
4. Open http://localhost:5100. Click `New chat`, tick BOTH `Ada` and `Bo`, click `Start chat`, and confirm the Room is headed `Ada, Bo`. This Room has three Members (you, Ada and Bo), so a Message is answered only when it @Mentions someone; Ada is the only one that will be mentioned.
5. Type exactly `hi @Ada` and press Enter. Wait for Ada's reply (a long plain-text reply that repeats your Message back, streamed in three chunks). Bo does not answer.
6. Read what appears between the message list and the message box at the bottom.
7. Note the two buttons in that block and their exact labels.
8. Click `Leave paused` and read what replaces the block.
9. Type exactly `anything` and press Enter (no @Mention, so nobody is woken and no second reply can re-exhaust the budget).
10. Read the area between the message list and the message box again, immediately and again after 10 seconds.
11. When finished, stop the app and run `Remove-Item Env:Team__AgentMessageBudget`.

**Pass if — all of these**

- After the reply lands, a block appears BETWEEN the message list and the message box reading exactly `Agents have sent 1 replies since you last spoke, and are paused.`
- That block holds exactly two buttons labelled `Continue` and `Leave paused`, with a grey line
  above them warning that Continue will wake nobody — the Room's last Message is the agent's own
  reply, and an Agent is never delivered its own Message. STARTUPCONFIG-14 exercises that line.
- No context-only note appears after `hi @Ada`: Ada was mentioned, so there is nothing to explain.
- Clicking `Leave paused` replaces the block with the single line `Paused — 1 of 1 agent replies since you last spoke.`
- After sending `anything`, both the paused line and the prompt are gone, no budget note is shown at all (the counter is back to 0), no reply arrives, and the Room shows the note `No teammate was @-mentioned — name one to ask for a reply.` (`Chat.razor:99`).

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
- The `Ada, Bo` Room from STARTUPCONFIG-13 is open (any state; step 1 pauses it again).

**Steps**

1. In the `Ada, Bo` Room, type exactly `hi @Ada` and press Enter, and wait for the reply so the Room is paused again with the AGENT's reply as the last Message.
2. Confirm the `Agents have sent 1 replies since you last spoke, and are paused.` block is showing with its two buttons, and read the grey line above them.
3. Click `Continue`.
4. Watch the button's own label while the request is in flight.
5. Read what replaces the alert block.
6. Wait 15 seconds and watch the message list.
7. Switch to the console and search for a line containing `was extended to`.

**Pass if — all of these**

- BEFORE the click, the pause block carries a grey line reading
  `Continue will deliver the last message again, and it is a teammate's own — a teammate is
  never delivered its own message, so this will wake nobody. Say something instead.` (with an em dash, `Chat.razor:143`). This is the answer to the trap in
  this test's own title: the Human is told the button cannot wake anybody BEFORE pressing it.
- While in flight, the button's label reads `Continuing…` and is not clickable.
- The alert block is replaced by the line `1 of 2 agent replies since you last spoke.` — the second number has RISEN from 1 to 2.
- No new message arrives in the 15 seconds after clicking, and a quiet blue note reads `Budget
  granted, but there was nobody to wake. Say something to start the room again.`
- The console contains a line of the form `Room '<id>' was extended to 2 agent messages.`

**Fail if — any of these**

- The second number does NOT rise (still reads `1 of 1`) -> the grant did not land; this really is a dead button.
- The button stays clickable while the request is in flight -> the busy guard is gone and a double-click can grant twice, which spends real money on a paid Persona.
- The console shows no `was extended to` line and the number did not rise -> the extend path is broken.

**Inconclusive if**

'No new reply' is CORRECT and must not be filed — and as of #40 the Room says so itself, both before the click and after it, so this paragraph is now a cross-reference rather than the only place the fact is written down. Continue re-delivers only the Room's most recent Message, and delivery always skips that Message's own sender — so when the last Message is the agent's own reply, there is nobody left to wake, regardless of Mention or Member count. The rising allowance figure is still the authoritative proof the grant worked; a missing `was extended to` line is a real defect even if both notes render. To see Continue actually wake something you would need the last Message to be a Human Message instead of the agent's own reply, which only happens if the agent was paused mid-run — and in this three-Member `Ada, Bo` Room a re-delivered Human Message wakes `Ada` only if it @Mentions her; otherwise Continue reports `Budget granted and the last message was delivered again, but it names no teammate. Mention one by name to get an answer.`

> [!NOTE]
> Also documented: a Message declined for Budget is held for re-delivery, not kept as Catch-up. If you type something instead of clicking Continue, the agent's next prompt will not carry the Message it was paused on, even though that Message is still visible in the transcript.

### STARTUPCONFIG-15 — Team__AgentMessageBudget=0 removes the guard entirely and hides every budget surface; a restart un-pauses everything

**Free** · about 8 min

*Proves the documented escape hatch back to pre-guard behaviour, and confirms the deliberate in-memory-counter gap so it is not filed as data loss.*

**Before you start**

- The app from STARTUPCONFIG-13 and -14 is running on `P-LAUNCH-UAT` with `Team__AgentMessageBudget = '1'`.

**Steps**

1. With the app still running at `Team__AgentMessageBudget = '1'` and the `Ada, Bo` Room paused, press Ctrl+C in the run window.
2. Relaunch in the same window (the variable is still `1`) by re-running ONLY the `$mock = ...` line and the `dotnet run` command of `P-LAUNCH-UAT`. Do NOT re-run the seeder line: it wipes the data folder and the transcript this step checks.
3. Reopen the `Ada, Bo` Room and look at the area between the message list and the message box, and at the transcript itself.
4. Stop the app. Run `$env:Team__AgentMessageBudget = '0'` and relaunch in the same window the same way (`$mock` line and `dotnet run` command only).
5. Open the `Ada, Bo` Room and send `hi @Ada`. Wait for the reply. Repeat four more times, for five agent replies in total.
6. After each reply, look at the area between the message list and the message box.
7. Stop the app and run `Remove-Item Env:Team__AgentMessageBudget`.

**Pass if — all of these**

- After the restart at step 3, the transcript is fully intact but the pause alert and the budget note are BOTH gone, and the next agent reply is allowed.
- With the budget set to `0`, no budget note, no pause alert and no `Continue` button appears at any point across all five agent replies.

**Fail if — any of these**

- Any budget text at all appears with the value set to `0` -> zero-or-less is documented as the only way back to pre-guard behaviour; a counter showing here means the disable path is broken.
- The transcript is missing messages after the restart at step 3 -> that is a REAL bug (Messages are durable in `C:\seeds\huddle\uat\data\rooms\*.jsonl`), unlike the lost pause.
- With `0` set, a reply is ever refused -> the cap is still enforcing despite being disabled.

**Inconclusive if**

A restart clearing the pause is DELIBERATE — the counter is in memory and per Room, the same trade the Catch-up buffer makes. Do not file 'the budget reset after a restart'. Conversely, do not file 'the budget counter is missing' when the value is `0`; that is the point of the setting. If you are unsure whether the variable took, confirm with `Get-ChildItem Env:Team__AgentMessageBudget`.

> [!NOTE]
> Two behaviours are combined here deliberately: they share one restart and would otherwise duplicate five minutes of setup.

### STARTUPCONFIG-17 — A mis-spelled configuration key changes nothing and reports nothing

**Free** · about 6 min

*The highest-value negative test in this area: it demonstrates the silent-binding failure that can invalidate every other configuration result in this script.*

**Before you start**

- No `Team__*` variables set (`Get-ChildItem Env:Team__*` prints nothing).
- Clean or existing App_Data — either is fine.

**Steps**

1. Run `$env:Team_PipeName = 'team-test'` — note the SINGLE underscores, which is the mistake under test. (The correctly spelled variable would move the pipe off `team`; see STARTUPCONFIG-24.)
2. In that same window set the cost guard `$env:Team__Acp__Enabled = 'false'` (see [`P-LAUNCH-FREE`](common.md#p-launch-free); without it the Chief of Staff's adapter starts and spends money), then launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Read the whole console output from the first line to `Now listening on:`.
4. Open http://localhost:5100. In `T-C` run `pwsh tools/echo-bot.ps1 -Name mybot` (default pipe `team`) and read what it prints.
5. Stop the bot (Ctrl+C) and the app. Run `Remove-Item Env:Team_PipeName`.
6. Now run `$env:Team__PipeNam = 'team-test'` — correct separators, misspelled leaf key — keep `Team__Acp__Enabled` set to `false`, and relaunch.
7. Read the console, then run the bot again as in step 4.
8. Stop the bot and the app and run `Remove-Item Env:Team__PipeNam` and `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- With the single-underscore spelling, the app starts normally, the bot prints a `welcome` line on the default pipe `team`, and a `mybot` Room appears in the sidebar — exactly as with no variable set.
- With the misspelled leaf key, the same: the app starts normally and the bot connects on `team`.
- In BOTH cases the console prints no warning, no error and no line mentioning the variable — the value simply bound to nothing.

**Fail if — any of these**

- The app refuses to start, or prints a warning naming the mis-spelled key -> record it as an OBSERVATION, not a defect: a loud failure here would be an improvement, but the documented current behaviour is silence. Note that three RETIRED keys ARE guarded loudly (`Team:Acp:PersonaDir`, `Team:Acp:TeamsDir`, `Team:Tasks:Dir`; `ServiceCollectionExtensions.cs:42-68`) — see STARTUPCONFIG-22. A key that merely mis-spells a live one is not.
- The mis-spelled variable DOES move the pipe (the bot times out on `team`) -> configuration is being bound by something other than the documented double-underscore scheme; every other test's spelling assumptions are then wrong.

**Inconclusive if**

This test has no failing outcome in the usual sense; its job is to calibrate the tester. The rule it teaches is binding on every other test here: NEVER conclude that a configuration value took effect because you set a variable. Always confirm positively — a Room that is or is not there, a budget note, a file on disk, a console line. STARTUPCONFIG-24 is the positive control to pair this with: the same key spelled correctly DOES move the pipe.

> [!NOTE]
> This is why every configuration test above and below names an observable positive effect rather than saying 'set the variable and check it worked'.

### STARTUPCONFIG-20 — Team__DataDir points the whole application at a different folder — a clean slate without deleting anything

**Free** · about 8 min

*Proves the softer reset path, and proves the Settings page is a reliable oracle for which DataDir is live.*

**Before you start**

- `P-LAUNCH-UAT` has been run once, and Ada's Room holds at least one exchange (send `hello` and wait for the reply), so `C:\seeds\huddle\uat\data` has Rooms and messages in it.
- The app is stopped.

**Steps**

1. Relaunch with a DIFFERENT data folder by running the `$mock = ...` line of `P-LAUNCH-UAT` and then its `dotnet run` command with the one argument `--Team:DataDir=C:\seeds\huddle\uat\data` changed to `--Team:DataDir=C:\seeds\huddle\uat\data_test`. Do NOT re-run the seeder line, and do NOT add a second `DataDir` setting or an `$env:Team__DataDir`: a command-line argument outranks an environment variable, so the argument in the command is the one to replace.
2. Open http://localhost:5100 and read the main column and the sidebar immediately, then again after 10 seconds.
3. Click `New chat` and read the panel.
4. Click the Settings icon (accessible name `Settings`) and read the absolute path printed in the Prompts tab's overrides paragraph.
5. In `T-B` run `Get-ChildItem -Recurse -Force C:\seeds\huddle\uat\data_test`.
6. Stop the app and relaunch with the ORIGINAL `P-LAUNCH-UAT` `dotnet run` command (DataDir back to `...\uat\data`, still no seeder line).
7. Open the `Ada` Room and read its transcript again.

**Pass if — all of these**

- With `data_test` live, the sidebar room list holds no `Ada` or `Bo` Room and none of the earlier messages (it reads `No rooms yet. Start an agent to create one.`, or at most shows the built-in Chief of Staff's own Room, which the app creates itself) — because the seeder never wrote there.
- The path printed on the Settings page ends `\uat\data_test\prompts.json`.
- `C:\seeds\huddle\uat\data_test\` exists and holds its own `team.db` and its own `Teammates\` folder with a freshly seeded `Chief of Staff\Chief of Staff.md` — and no `Ada` or `Bo` folder.
- After relaunching with the original DataDir, the `Ada` and `Bo` Rooms and Ada's full history are back, untouched.

**Fail if — any of these**

- Old messages or the `Ada` Room still appear with `data_test` set -> the new DataDir did not take effect (check that you replaced the argument rather than added a second one); do not file this as a DataDir defect until the Settings path confirms which directory is live.
- The Settings path names a different directory from the one that actually gained files -> the page is reporting a path the app is not using, which would make it useless as an oracle everywhere else.
- The original data is missing after removing the variable -> a real data-loss bug; confirm the working directory has not changed before filing.

**Inconclusive if**

This test uses an absolute path on purpose. A RELATIVE `Team:DataDir` is resolved against the process working directory, so it lands under `src\Huddle.App`, NOT the repo root, and a tester looking for it at the repo root will find nothing and wrongly conclude the setting did nothing. Always read the absolute path from the Settings page rather than guessing where the folder went.

> [!NOTE]
> Clean up afterwards with `Remove-Item -Recurse -Force C:\seeds\huddle\uat\data_test` once the app is stopped.

### STARTUPCONFIG-21 — Team__HumanName renames the Human everywhere new, while old transcript lines keep the old name

**Free** · about 7 min

*Proves the rename reaches the members line and new message attribution, and pins the mixed attribution so it is not filed as a half-applied rename.*

**Before you start**

- `P-LAUNCH-UAT` has been run and Ada is Online.
- The app is running (it is stopped and relaunched below).

**Steps**

1. Open the `Ada` Room in the browser first (before changing anything), type `hello` and press Enter, wait for the reply, and note that your Message shows sender `You` and the members line reads `You, Ada`.
2. Stop the app. Run `$env:Team__HumanName = 'Emre'`.
3. Relaunch in the same window by re-running ONLY the `$mock = ...` line and the `dotnet run` command of `P-LAUNCH-UAT` (not the seeder line, which would wipe the old transcript), and reopen the same Room.
4. Read the members line under the Room heading.
5. Read the sender name on the OLD messages already in the transcript.
6. Type exactly `hello again` and press Enter.
7. Read the sender name on the message you just sent.
8. Stop the app and run `Remove-Item Env:Team__HumanName`.

**Pass if — all of these**

- The members line now reads exactly `Emre, Ada`.
- Every message sent BEFORE the change still shows sender `You`.
- The message sent AFTER the change shows sender `Emre`.
- No error appears anywhere.

**Fail if — any of these**

- The members line still reads `You, Ada` -> the rename did not reach the directory; check the variable spelling before filing.
- The NEW message still shows `You` -> the sender name is not being read from the renamed directory row at post time.
- Old messages were rewritten to `Emre` -> transcript lines are being mutated after the fact, which they must never be.

**Inconclusive if**

The mixed attribution is CORRECT and must not be filed as 'the rename only half applied'. The directory row is renamed in place, but each transcript line carries a sender name captured at post time and is never rewritten. If you cannot tell old messages from new, note the time on each message row.

> [!NOTE]
> Optional oracle if sqlite3 is available: `sqlite3 C:\seeds\huddle\uat\data\team.db "SELECT name FROM users WHERE id='human';"` returns the new name, while older lines in `C:\seeds\huddle\uat\data\rooms\*.jsonl` still carry `"senderName":"You"`.

### STARTUPCONFIG-22 — The renamed Team__Acp__PersonaDir key refuses to start the app rather than scanning nothing

**Free** · about 5 min

*Proves the three retired configuration keys that fail LOUDLY — written precisely because binding-to-nothing is this repo's signature failure shape. The positive key is `Team:Acp:TeammatesDir` (default `Teammates`, `AcpOptions.cs:35`).*

**Before you start**

- The app is stopped and nothing else holds port 5100.

**Steps**

1. Run `$env:Team__Acp__Enabled = 'false'` (the cost guard: the checks below never start a Teammate, but the final relaunch does).
2. Run `$env:Team__Acp__PersonaDir = 'personas'`.
3. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Read the console output in full.
5. Try to open http://localhost:5100 in the browser and note what it shows.
6. Run `Remove-Item Env:Team__Acp__PersonaDir`, then repeat steps 2-5 with `$env:Team__Acp__TeamsDir = 'teams'` (remove it afterwards), then once more with `$env:Team__Tasks__Dir = 'tasks'` (remove it afterwards).
7. With none of the three set, relaunch and confirm the app starts normally again.
8. Stop the app and run `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- For `PersonaDir` the app does NOT start. The console shows an `InvalidOperationException` whose message reads: `Configuration key 'Team:Acp:PersonaDir' was renamed to 'Team:Acp:TeammatesDir'. Update the configuration source that sets it (environment variable, user secret, etc.) - there is no automatic fallback.` (`ServiceCollectionExtensions.cs:42-47`)
- For `TeamsDir` the same, with `'Team:Acp:TeamsDir'` as the first key and `'Team:Acp:TeammatesDir'` as the replacement (`ServiceCollectionExtensions.cs:50-55`).
- For `Tasks__Dir` the message reads: `Configuration key 'Team:Tasks:Dir' was replaced by 'Team:Teams:Dir'. Tasks now live in each Team folder's _tasks/ folder; remove the key (the start-up migration reads {DataDir}/Tasks). There is no automatic fallback.` (`ServiceCollectionExtensions.cs:62-67`)
- Each stack trace names `AddTeamServices`.
- The browser at http://localhost:5100 shows a connection-refused page each time.
- With all three cleared, the app starts normally.

**Fail if — any of these**

- The app starts normally with any of the three variables set -> the loud guard has regressed into exactly the silent degradation it exists to prevent; the Persona or Task directory would quietly scan nothing and `/teammates` would show no Teammates with no explanation anywhere.
- The message names the OLD key as the replacement, or omits the new key (`Team:Acp:TeammatesDir`, or `Team:Teams:Dir` for the Tasks key) -> the guard fires but does not tell the user how to fix it.

**Inconclusive if**

If the browser still shows a working app after the crash, you are looking at a DIFFERENT instance still bound to 5100. Stop every `dotnet` process and repeat. CLEAR THE VARIABLE (all three, if you ran all three) before running anything else — while one is set, every subsequent test will fail to launch and every result will be meaningless.

> [!NOTE]
> A test in `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs` pins this message; a wording change there should be matched here.

### STARTUPCONFIG-23 — An external pipe client creates a Room live, with no page refresh

**Free** · about 7 min

*Proves the pipe surface end to end from a real external process, and that the sidebar updates without a reload.*

**Before you start**

- `P-LAUNCH-FREE` has been run with the DEFAULT pipe name (no `Team__PipeName` set), so the Chief of Staff gets no Room and nothing is spent.
- Exactly ONE instance of the app is running: `Get-Process Huddle.App` shows one process. (`dotnet run` also leaves SDK and MSBuild `dotnet.exe` nodes, so do not count `dotnet`.)
- `pwsh` is available.

**Steps**

1. Open http://localhost:5100 and leave the browser window visible. Note the current sidebar room list.
2. In `T-B`, at `<repo>`, run `pwsh tools/echo-bot.ps1 -Name mybot`.
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

The `version` field in the welcome envelope must equal the CURRENT protocol version in `src/Huddle.Contracts/ProtocolVersion.cs` — at the time of writing that is `3`, the same as the example in `docs/Huddle.EngineeringGuide.md`. If the script hangs at `Connecting…`, confirm the app is running and that no `Team__PipeName` is set — see STARTUPCONFIG-24.

> [!NOTE]
> STARTUPCONFIG-24 starts by stopping the app.

### STARTUPCONFIG-24 — Team__PipeName changes the pipe and silently orphans any external client

**Free** · about 7 min

*Proves the pipe name is the whole address, and demonstrates that the browser gives NO hint when an external client is knocking on the wrong pipe.*

**Before you start**

- The app is stopped and `src\Huddle.App\App_Data` has been deleted, so the absence of a `mybot` Room is unambiguous.
- Only one instance will run.

**Steps**

1. Run `$env:Team__Acp__Enabled = 'false'` (the cost guard, and the reason no Chief of Staff Room appears: with ACP on its runner connects on the configured pipe name and would get a Room, `PersonaRunner.cs:737`), then `$env:Team__PipeName = 'team-test'`.
2. Launch in the same window: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
3. Open http://localhost:5100, wait 10 seconds, and read the sidebar room list.
4. In `T-B` run `pwsh tools/echo-bot.ps1 -Name mybot`.
5. Read what the script prints and how long it takes to finish or fail.
6. Look at the browser sidebar for a full 30 seconds WITHOUT refreshing.
7. Stop the app and run `Remove-Item Env:Team__PipeName` and `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- The sidebar room list reads `No rooms yet. Start an agent to create one.` and stays that way.
- The script prints `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...` and then fails (a timeout after roughly five seconds) or blocks — it never prints a `welcome` line.
- NO `mybot` Room ever appears in the browser.
- The browser gives no error, no warning and no visible change of any kind.

**Fail if — any of these**

- A `mybot` Room DOES appear -> another instance of the app is still running on the default pipe name and answering the script; stop every `dotnet` process and repeat.
- The script connects successfully to `\\.\pipe\team` -> a server is still bound to the default name.

**Inconclusive if**

The complete silence in the browser is the POINT of this test, not a defect. To confirm you are testing the right thing, verify the positive control first: with no `Team__PipeName` set, the same command must make a `mybot` Room appear immediately (STARTUPCONFIG-23). If the script connects and you cannot account for it, run `Get-Process Huddle.App` — a second instance is the usual explanation.

> [!NOTE]
> `tools/echo-bot.ps1` also takes a `-Pipe` parameter (default `team`); this test deliberately leaves it at the default so the client and server disagree.

### STARTUPCONFIG-26 — Environment selection decides whether ACP is on and which port is used

**Free** · about 6 min

*Prevents the two most common misreadings in this area: 'the app moved to a different port' and 'my Personas stopped starting', both of which are configuration rather than defects.*

**Before you start**

- No `Team__*` variables set.
- Nothing else on ports 5000 or 5100.

**Steps**

1. Run `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=false` (no `--urls`, no other flags). Run (a) is Development, where ACP is on, so this one command-line guard keeps the Chief of Staff from starting its adapter and taking a paid Greeting Turn; it changes nothing this test reads in run (a). Run (b) has NO guard: ACP is off there by default, and that is what pass bullet 4 checks.
2. Read the console's `Hosting environment:` and `Now listening on:` lines. Open the URL it names.
3. Stop the app with Ctrl+C.
4. Run `dotnet run --project src/Huddle.App --no-launch-profile`.
5. Read the console's `Hosting environment:` and `Now listening on:` lines again, then in `T-B` run `O-ADAPTERS`.
6. Try to open http://localhost:5100 in the browser and note what happens.
7. Open the URL the console actually named. The app will answer, but it will render UNSTYLED and non-interactive - see the pass condition. Confirm the routes resolve (`/` answers and the app responds) rather than that the page looks right.
8. Compare the two runs' configured log level for `Agency.Huddle`: `appsettings.Development.json` sets it to `Debug`, `appsettings.json` leaves it at `Information`. Do NOT expect run (a)'s console to be visibly longer - see the pass condition.
9. Stop the app.

**Pass if — all of these**

- Run (a) reports `Hosting environment: Development` and `Now listening on: http://localhost:5100`.
- Run (b) reports `Hosting environment: Production` and listens on `http://localhost:5000` (not 5100).
- In run (b), http://localhost:5100 gives connection refused.
- In run (b), no Teammate starts (`O-ADAPTERS` prints `0`, the built-in Chief of Staff included), because only `appsettings.json` applies and ACP is off there.
- In run (b) the app answers but renders UNSTYLED and non-interactive, and exactly three assets return **500**: `Huddle.App.<hash>.styles.css`, `Components/Layout/ReconnectModal.<hash>.razor.js` and `_framework/blazor.web.<hash>.js`. The console says why - `The application is not running against the published output and Static Web Assets are not enabled.` followed by a `FileNotFoundException` per asset. This is stock ASP.NET Core behaviour for an UNPUBLISHED build run in the Production environment, NOT a defect: those three assets live outside `wwwroot` until `dotnet publish` copies them there. Verified on 2026-09-14 - a published build in Production serves all six assets 200.
- The configured `Agency.Huddle` level differs between the two: `Debug` from `appsettings.Development.json` in run (a), `Information` from `appsettings.json` in run (b). The two consoles will nonetheless look nearly the SAME in the free lane, and that is correct - `Huddle.App` and `Huddle.Contracts` log very little at `Debug` (a handful of `LogDebug` calls, for example `PersonaSpend.cs:57` and `AgentConnection.cs:363`), so the extra level buys almost nothing until ACP is on. Do not judge the run by console length.

**Fail if — any of these**

- Run (a) reports Production -> the launch profile was not picked up; check that `src/Huddle.App/Properties/launchSettings.json` still holds the `http` profile.
- Run (b) reports Development -> `--no-launch-profile` is not being honoured, and every 'this is the stock configuration' assumption elsewhere is wrong.
- Run (b) starts a Teammate process (`O-ADAPTERS` above `0`) -> ACP is on in Production, which it must not be by default (it spends real money).

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
- The Chief of Staff is seeded whatever the ACP flag says (`BuiltinTeammateSeeder.cs`), so its tile is on the page even though nothing starts.

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
- `/teammates` renders with heading `Teammates`, a `New teammate` button (a text button, not upper-cased), and the intro paragraph beginning `A Persona is a Markdown file describing how one teammate should behave`.
- On a clean App_Data, the page shows exactly one tile, the built-in Chief of Staff (alias `@cos`), whose status does not read `Starting` or `Online` because nothing was started. `No Personas yet.` never appears, because the Chief of Staff is always seeded (`Teammates.razor:67-70`).
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

### STARTUPCONFIG-28 — Team__Acp__Enabled=true with only the built-in Chief of Staff starts exactly one process

**💰 Spends money** · about 5 min

*Prevents a false defect report: the documentation says a process starts per Persona at startup, so a tester with "no Personas" expects none. A library with no Personas cannot exist: the built-in Chief of Staff is seeded at every start, so with ACP on exactly one adapter starts, and only that one.*

**Before you start**

- Read [§0.2](../manual-tests.md#02-the-cost-guard) and [§0.3](../manual-tests.md#03-the-model-and-effort-convention): the Chief of Staff takes one paid Greeting Turn on the real adapter (`PersonaRunner.cs:481-495`).
- `src\Huddle.App\App_Data\Teammates` holds no Teammate but the Chief of Staff — `Get-ChildItem -Directory src\Huddle.App\App_Data\Teammates` prints only `Chief of Staff` (or nothing yet, on a clean start). Run `P-RESET-ALL` if unsure.
- The node adapter requirements of the Setup (step 6) are met.

**Steps**

1. Set the Chief of Staff to Haiku / low as the first step of [`P-LAUNCH-PAID`](common.md#p-launch-paid-) (launch free, Edit on its card, Save, `P-STOP`).
2. In `T-B` run `O-ADAPTERS`. It should print `0`.
3. Launch with `P-LAUNCH-PAID`.
4. Wait 20 seconds after the app is listening.
5. In `T-B` run `O-ADAPTERS` again.
6. Open the browser, click `Teammates`, and read the body.
7. Read the sidebar room list.

**Pass if — all of these**

- The app starts normally with ACP enabled.
- `O-ADAPTERS` prints exactly `1` after the wait, against `0` before: the Chief of Staff's adapter, and no other.
- `/teammates` shows exactly one tile, `Chief of Staff` (`@cos`), reading `Online` (or `Starting` for a few seconds first); it never shows `No Personas yet.`
- A `Chief of Staff` direct Room is in the sidebar.
- Nothing beyond that one Haiku / low Greeting Turn is spent.

**Fail if — any of these**

- `O-ADAPTERS` prints more than `1` -> something other than the seeded Chief of Staff was started; check `Get-ChildItem -Directory src\Huddle.App\App_Data\Teammates` for a Teammate left behind by another test.
- `O-ADAPTERS` prints `0` with the tile stuck `Starting` or `Offline` -> read the tile's tooltip and act on it (see STARTUPCONFIG-31).
- The app fails to start with ACP enabled and only the built-in Teammate -> that is the first-run state of every install.

**Inconclusive if**

`docs/Huddle.EngineeringGuide.md` says `dotnet run` starts one node process per Persona at startup, before you type anything. With only the Chief of Staff that is exactly one. If other Teammate folders exist, this is a different test: see STARTUPCONFIG-31.

> [!NOTE]
> Together with STARTUPCONFIG-27 this brackets the ACP flag: off spawns nothing, on spawns exactly one process per Teammate present, and the Chief of Staff is always present. COST: one short Haiku turn at low effort — a cent or two.

### STARTUPCONFIG-29 — A Persona file in its own Teammates folder is discovered, reloads on edit in place, and a broken file is named rather than silently dropped

**Free** · about 10 min

*Catches the documented easiest way to break the Persona library — a watcher that finds a file under a sub-folder once at startup and then never reloads it — and proves a file with bad frontmatter, and a stray file in a teammate's folder, are surfaced with their reasons rather than vanishing. A Persona lives at `Teammates\<Name>\<Name>.md` (`TeammatePaths.cs:15`).*

**Before you start**

- The app is stopped.
- `Team__Acp__Enabled` will be set to `false` for this test, so NO agent process is ever spawned and nothing is spent.

**Steps**

1. Run `$env:Team__Acp__Enabled = 'false'`.
2. Create a Teammate folder and a valid Persona file. The name `Ops Lead` is deliberate: never reuse `Chief of Staff`, which the app seeds itself. Run `New-Item -ItemType Directory -Force "src\Huddle.App\App_Data\Teammates\Ops Lead"`, then write this exact content to `src\Huddle.App\App_Data\Teammates\Ops Lead\Ops Lead.md`:
```
---
name: 'Ops Lead'
title: 'Operations Lead'
alias: 'ops'
---
You are the Operations Lead. You keep the team honest.
```
3. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Open the browser and click `Teammates`. Read what is shown.
5. Leave the app RUNNING. In a text editor, change the file's `title:` line to `title: 'Chief of Operations'` and save it.
6. Watch the `/teammates` page for up to 5 seconds WITHOUT refreshing, and read the `Ops Lead` tile.
7. Still with the app running, create a stray file: `Set-Content "src\Huddle.App\App_Data\Teammates\Ops Lead\notes.md" 'scratch'`.
8. Watch the `/teammates` page for up to 5 seconds and read anything that appears ABOVE the tiles.
9. Still with the app running, create a BROKEN definition: `New-Item -ItemType Directory -Force "src\Huddle.App\App_Data\Teammates\Broken One"`, then write a file `src\Huddle.App\App_Data\Teammates\Broken One\Broken One.md` containing only the two lines `---` and `---` (empty frontmatter).
10. Watch the page again for up to 5 seconds and read the list ABOVE the tiles.
11. In `T-B` run `O-ADAPTERS`.
12. Stop the app, delete both folders (`Remove-Item -Recurse "src\Huddle.App\App_Data\Teammates\Ops Lead", "src\Huddle.App\App_Data\Teammates\Broken One"`), and run `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- After step 3, `/teammates` shows, besides the built-in Chief of Staff tile, a tile with the name `Ops Lead`, the title `Operations Lead` and an alias chip `@ops` (the name, title and chip are separate elements, not one `·`-joined line, `Teammates.razor:98-104`) — a Persona in its own folder is found by the startup scan.
- After the edit in step 5, the tile's title updates to `Chief of Operations` within 5 seconds and with no page refresh.
- After step 7, a section headed exactly `Files that didn't load` appears ABOVE the tiles, listing the path of `notes.md` with the reason `A teammate folder holds only its definition; use work/.` (`PersonaStore.cs:773`). The `Ops Lead` tile stays.
- After step 9, the same section also lists the path of `Broken One.md` with a reason beginning `Persona frontmatter is missing required field` (`PersonaFrontmatter.cs:595`).
- `O-ADAPTERS` prints `0` (ACP is off, so no process was spawned at any point).

**Fail if — any of these**

- The tile appears at startup but does NOT update when the file is edited in place -> the directory watcher is not including sub-directories (`PersonaStore.cs:171`); a Persona would then be frozen at whatever it said when the app started, with no error, no log and nothing on screen. This is the single easiest way to break the Persona library and is exactly the documented trap.
- A rejected file simply does not appear anywhere -> a rejected file is being dropped silently instead of being named with its reason, which is the whole point of the required-field decision.
- The tile does not appear at all at startup -> the Teammates directory is not being scanned.

**Inconclusive if**

If the tile never appears even at startup, first confirm the file really is at `src\Huddle.App\App_Data\Teammates\Ops Lead\Ops Lead.md` (the LIVE DataDir — read the path from `/settings` if unsure; the folder and the file must carry the same name) and that the frontmatter has all three required fields `name`, `title` and `alias`. A `.md` placed directly in `Teammates\` is rejected with `A definition must be inside its teammate's folder.`, and nothing under `App_Data\Teams\` is ever read as a Persona (that folder holds Teams).

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
2. Run `$env:Team__Acp__Enabled = 'false'` (the cost guard; an environment variable outranks the edited JSON files, so it holds for the whole test), then launch normally. In `T-C` run `pwsh tools/echo-bot.ps1 -Name logbase` and confirm the baseline: the console contains `Created direct room '<id>' for agent 'logbase'.` Then stop the bot and the app.
3. Make the stale rename in BOTH settings files, and drop the fallback that would otherwise rescue the category. Three edits, all of which are needed - see the note below for why one alone does nothing:
   - `src/Huddle.App/appsettings.json`: change the `Logging.LogLevel` key `"Agency.Huddle"` to the stale name `"Team"`, value `"Information"` unchanged.
   - `src/Huddle.App/appsettings.Development.json`: change the `Logging.LogLevel` key `"Agency.Huddle"` to the stale name `"Team"`, value unchanged.
   - `src/Huddle.App/appsettings.Development.json`: change `"Default": "Information"` to `"Default": "Warning"`.
   Save both.
4. Relaunch and open http://localhost:5100.
5. In `T-C` run `pwsh tools/echo-bot.ps1 -Name logstale`, read the sidebar room list, then open the `logstale` Room and send `hi @logstale`.
6. Search the WHOLE console output for `Created direct room` and for any `Agency.Huddle` category line.
7. Stop the bot and the app and run `git checkout -- src/Huddle.App/appsettings.json src/Huddle.App/appsettings.Development.json`.
8. Relaunch, run `pwsh tools/echo-bot.ps1 -Name logfinal`, and confirm the `Created direct room '<id>' for agent 'logfinal'.` line is back. (Use a new bot name each time: reconnecting under an existing name re-attaches to its Room and logs no creation line.) Then stop the app and run `Remove-Item Env:Team__Acp__Enabled`.

**Pass if — all of these**

- With the stale `"Team"` key, the app behaves IDENTICALLY in the browser: the `logstale` Room appears with no refresh and `hi @logstale` gets its bold reply.
- With the stale key, the `Created direct room … for agent 'logstale'.` line and other `Agency.Huddle` category lines are ABSENT from the console.
- No warning of any kind reports that the key matched nothing.
- After reverting the file, the console lines return.

**Fail if — any of these**

- The app behaves differently in the browser with the stale key -> log configuration is affecting behaviour, which it must not.
- A warning IS emitted naming the unmatched key -> record as an observation; a loud failure would be an improvement over the documented silence.
- The `Agency.Huddle` lines are still present after ALL THREE edits -> log categories are not derived from the namespace as documented, and the whole prefix model is different from what the docs describe. Before concluding that, run the control: put `"Agency.Huddle": "Warning"` in `appsettings.Development.json` on its own. If the `Created direct room` lines vanish for that, the prefix model is fine and one of the three edits was missed.

**Inconclusive if**

If `git status` is not clean at step 1, do NOT run this test — you risk reverting someone else's work. The key point to carry forward: several tests in this area use console lines as their ONLY oracle, so this failure quietly removes the evidence rather than the behaviour. If an expected console line is missing anywhere in this script, check the log filter before concluding the behaviour did not happen.

> [!NOTE]
> Log categories come from the type's full namespace, which moved to `Agency.Huddle.*`, while the `Team:` CONFIG root deliberately did not move — that mismatch is what makes a stale `"Team"` log key so plausible and so silent.
>
> WHY ALL THREE EDITS: renaming the key in `appsettings.Development.json` alone changes nothing observable, for two independent reasons. First, that file's value is `Debug`, and `Huddle.App` and `Huddle.Contracts` have only a handful of `Debug`-level statements (for example `PersonaSpend.cs:57`, `AgentConnection.cs:363`) — every line this script quotes is `info:`. Second, `appsettings.json` sets `"Agency.Huddle": "Information"` in its own right, and configuration MERGES, so that base key keeps the category alive whatever the overlay is called. The category only goes dark once no file names it AND `Default` no longer rescues it. Verified 2026-09-14: all three edits together produce a completely silent console against a working app; any one of them alone produces the full console.

### STARTUPCONFIG-31 — With ACP on and one Persona present, that Persona starts at launch and its tile goes Starting then Online

**💰 Spends money** · about 10 min

*Proves the supervisor really launches a Persona at startup and that all three start-failure causes reach the browser as readable tile text — the surface actually under test. The Edit card in step 7 is a real `MudDialog` and its Model/Effort dropdowns are `MudSelect` (Stages 3-4 of the MudBlazor migration); neither changes what this test is checking.*

**Before you start**

- `node --version` succeeds.
- `Test-Path tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js` returns True. If False, run `pwsh tools\acp\install.ps1` first.
- The `claude` CLI is installed and logged in.
- With ACP on, the seeded Chief of Staff starts too and takes ONE paid Greeting Turn (`PersonaRunner.cs:481-495`), which is why this test is marked 💰. Read [§0.2](../manual-tests.md#02-the-cost-guard) and [§0.3](../manual-tests.md#03-the-model-and-effort-convention). Tester itself takes no Turn.
- The app is stopped, and `src\Huddle.App\App_Data` has no Teammate folder but `Chief of Staff` (`P-RESET-ALL` if unsure).

**Steps**

1. Set the Chief of Staff to Haiku / low as the first step of [`P-LAUNCH-PAID`](common.md#p-launch-paid-), then `P-STOP`. Create one Persona at `src\Huddle.App\App_Data\Teammates\Tester\Tester.md` (create the `Tester` folder first) with exactly this content:
```
---
name: 'Tester'
title: 'Test teammate'
alias: 'tester'
---
You are a test teammate. Answer in one short sentence.
```
2. In `T-B` run `O-ADAPTERS`. It should print `0`.
3. Launch with `P-LAUNCH-PAID`.
4. Open the browser and go straight to `/teammates` — do not wait.
5. Read the `Tester` tile: its name, its title, its alias chip, and its status (a coloured dot plus a word).
6. Keep watching the status for up to 30 seconds and record every state it passes through.
7. Open the tile by clicking it, then click `Edit`. In the `Model` dropdown choose the option whose name contains `Haiku`; in the `Effort` dropdown choose `Low`. Click `Save`.
8. Look at the sidebar room list.
9. In `T-B` run `O-ADAPTERS` again.
10. If the status ever reads `Offline`, hover the status and read the tooltip text in full; record it verbatim.
11. When finished, stop the app. Leave the `Tester` folder in place: STARTUPCONFIG-32 and -33 use it.

**Pass if — all of these**

- The tile shows the name `Tester`, the title `Test teammate`, the chip `@tester` (separate elements, `Teammates.razor:98-104`), and a status that reads `Starting` with an amber dot, then `Online` with a green dot.
- `O-ADAPTERS` prints `2` after the start: `Tester`'s adapter and the Chief of Staff's, against `0` before.
- A Room named `Tester` appears in the sidebar with no refresh (next to the Chief of Staff's).
- After saving Model = Haiku and Effort = Low, the card shows those values and the teammate restarts (status passes through `Starting` again).
- No Turn is taken by `Tester` merely by coming Online; the only spend is the Chief of Staff's one Haiku / low Greeting.

**Fail if — any of these**

- The tile is stuck on `Starting` forever -> the start call never returned; the Human has no way to tell a slow start from a hung one.
- The tile flips to `Offline` with NO reason in the tooltip -> a start failure with no recorded reason is exactly the surface this page exists to fix; the Human can see the failure but not act on it.
- No `node` process appears despite ACP being on and a valid Persona present -> the supervisor did not launch anything.
- No `Tester` Room appears in the sidebar -> the Persona connected but did not get a Room, or the sidebar did not update live.

**Inconclusive if**

An `Offline` tile WITH a reason is a configuration result, not a product failure — read the tooltip and act on it: `The Adapter needs authentication: …` means the `claude` CLI is not logged in; `No ACP adapter is installed for Persona '…'. Run tools/acp/install.ps1 …` means the adapter probe found nothing; a process-start error usually means `node` is not on PATH. Fix the named cause and re-run. Note this test really does launch a child process and create a session — do not run it casually — but Tester takes no Turn, so the Chief of Staff's one Greeting is the only spend.

> [!NOTE]
> If the Model dropdown offers no options and the hint reads `This agent advertises no models, so it will use its own default.`, the model probe returned empty. A failed probe and a cancelled one look identical here — that ambiguity is a documented limit of the picker, not a defect to file.

### STARTUPCONFIG-32 — Team__Acp__TraceWire=true dumps the tool server's bearer token to the console

**Free** · about 6 min

*Proves the debugging flag works and, more importantly, proves there is no UI signal that it is on — the reason it must be turned off immediately.*

**Before you start**

- STARTUPCONFIG-31 passed, so ACP is working, `Tester` exists, and the Chief of Staff's one Greeting Turn has already been taken (its Room is no longer empty), so this launch takes no Turn.
- This is a THROWAWAY session only: do not screen-record, do not paste the console anywhere, and do not run it in a shared terminal.

**Steps**

1. Stop the app. Run `$env:Team__Acp__TraceWire = 'true'`.
2. Also raise the log level, or you will see nothing at all: `${env:Logging__LogLevel__Agency.Huddle} = 'Trace'`. `LoggerTraceListener` forwards the wire trace at **Trace**, while `appsettings.Development.json` pins `Agency.Huddle` at `Debug`, which does not include it. Note the spelling - a literal dot before `Huddle`, and `${env:...}` braces because of that dot.
3. Launch: `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
4. Watch the console as the Persona starts.
5. Search the console output for the category `Agency.Huddle.Acp.Wire`.
6. Look for an `Authorization` header value in the traced JSON-RPC traffic.
7. Open http://localhost:5100 and `/teammates`, and look for ANY visual indication anywhere in the browser that tracing is enabled.
8. Stop the app IMMEDIATELY, then run `Remove-Item Env:Team__Acp__TraceWire` and `Remove-Item ${env:Logging__LogLevel__Agency.Huddle}`.
9. Close the console window, or clear its scrollback, so the token is not left on screen.

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

- STARTUPCONFIG-31 passed and the `Tester` Persona is present (`Teammates\Tester\Tester.md`), configured with Model = Haiku and Effort = Low, and the Chief of Staff is Haiku / low too.
- The `claude` CLI is logged in.
- The app is stopped.

> [!IMPORTANT]
> This cap can only be reached by an **Agent-to-Agent** exchange, never by typing at it. Every
> Human Message resets the counter to zero (`PersonaRunner` does `Interlocked.Exchange(ref
> this.tokensConsumed, 0)` the moment it sees one), and the check runs at the START of a work item
> — so a Turn you prompted yourself is always measured against a freshly-zeroed counter. That is
> the point of the cap: it bounds *unattended* spend. The test therefore needs a SECOND Persona,
> so one Agent's reply wakes the other with no Human Message in between.

**Steps**

1. Confirm the Persona's Model is Haiku and Effort is low: open `/teammates`, click the tile, and read the Model and Effort shown. Fix them via `Edit` if not.
2. Stop the app. Rewrite the body of `src\Huddle.App\App_Data\Teammates\Tester\Tester.md` to `You are a test teammate. Reply with one very short sentence that ends with @tester2.` and add a second Persona: create the folder `src\Huddle.App\App_Data\Teammates\Tester2` and write `Tester2.md` in it:
```
---
name: 'Tester2'
title: 'Second test teammate'
alias: 'tester2'
---
You are a second test teammate. Reply with one very short sentence that ends with @tester.
```
3. Run the variable lines of [`P-LAUNCH-PAID`](common.md#p-launch-paid-), then override two: `$env:Team__Acp__TokenBudget = '2000'` — small enough that one Turn exceeds it — and `$env:Team__AgentMessageBudget = '4'`, which bounds the ping-pong and therefore the spend.
4. Launch with the `dotnet run` line of `P-LAUNCH-PAID` and wait for BOTH tiles to read `Online`. Set Model = Haiku and Effort = low on `Tester2` as well.
5. Click **New chat**, tick `Tester` and `Tester2` only, and click **Start chat**. The Room heading reads `Tester, Tester2`.
6. Type exactly `Say hi @tester` and press Enter.
7. Watch the exchange: `Tester` answers mentioning `@tester2`, then `Tester2` answers mentioning `@tester`. That second reply is an AGENT Message, so it wakes `Tester` with no reset — and that is the Turn the cap refuses.
8. Wait 30 seconds and record that no third Agent Message arrives.
9. Go to `/teammates` and read the `Tester` tile's status line. Hover the status and read the tooltip in full.
10. Go back to the Room and read the area between the message list and the message box.
11. Search the console for a line containing `token budget`.
12. Do NOT stop the app or clear the variable — STARTUPCONFIG-34 continues from this exact state.

**Pass if — all of these**

- The Human Message gets a normal reply from `Tester`, and `Tester2` answers that.
- `Tester` does NOT answer `Tester2` — the Agent-triggered Turn is the one refused.
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

The check runs at the START of a work item, so the FIRST Turn ALWAYS completes no matter how small the Budget. A tester expecting the very first message to be refused will wrongly report the cap as broken — do not file that. If `Tester` DOES answer `Tester2`, do NOT reach for the keyboard: typing another Message resets the counter and moves you further from the state under test. Let the ping-pong run instead — `Team__AgentMessageBudget` stops it — or check that both Personas really are mentioning each other by alias. The Budget is per Persona, not per Room, and has no Continue prompt: it only ever reads as Degraded.

> [!NOTE]
> COST: roughly three short Haiku turns at low effort — a few cents at most. Keep every prompt to a handful of words. A local model emits no usage updates at all, so this cap is inert against one; use the Claude adapter. For reference, the Claude adapter's first `usage_update` on a fresh session reported `used=49628` of `size=200000`, so a Budget of `2000` is exceeded by the opening Turn — the cap fires on the very next Agent-triggered Turn, not later.

### STARTUPCONFIG-34 — A Human Message clears a token-Budget Degraded state and lets the Persona work again

**💰 Spends money** · about 6 min

*Proves the documented reset: the one condition a Human Message is specified to clear, and only that one.*

**Before you start**

- STARTUPCONFIG-33 has just left the `Tester` Persona Degraded, with the app still running and `Team__Acp__TokenBudget` still set to `2000`.

**Steps**

1. Open the one-to-one `Tester` Room (not the `Tester, Tester2` Room: with three Members a Message wakes nobody without an @Mention, whereas a two-Member Room answers every Message) and confirm the alert strip is still showing.
2. Type exactly `Hello again.` and press Enter.
3. Wait up to 60 seconds and watch the message area.
4. Look at the area between the message list and the message box.
5. Go to `/teammates` and read the `Tester` tile's status line and dot colour.
6. Stop the app. Run `Remove-Item Env:Team__Acp__TokenBudget` and `Remove-Item Env:Team__AgentMessageBudget`.
7. Delete both test Personas: `Remove-Item -Recurse src\Huddle.App\App_Data\Teammates\Tester, src\Huddle.App\App_Data\Teammates\Tester2`. Their Rooms and history stay in the database and the Teammates go offline (`Teammates.razor:25`); `P-RESET-ALL` gives a fully clean slate.
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
