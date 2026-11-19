# Application shell, navigation and layout

Prove the Agency.Huddle shell holds together: that the five routes this page exercises (`/`, `/rooms/{RoomId}`, `/library`, `/settings`, `/settings/{Tab}`) resolve, that the fixed 240px `MudDrawer` is identical on every one of them, and above all that the shell's own assets actually load. The drawer holds the `Rooms` group (the room list, with **New room** inside it), the `Teams` group, the `Tasks` and `Library` groups, and a bottom row of two icon-only links, Archived and **Settings**. There is no Agents page: `/agents` shows "not found". The Tasks and Teams pages (`/tasks…`, `/teams/…`) have no shell-level test here, and the Library page (`/library`) is used only as a page that is not a Room. The asset check is the reason this area is tested by hand: `@Assets["..."]` hands back an unresolved key verbatim instead of throwing, so a stale stylesheet or script name renders as an ordinary-looking href that 404s with no build warning, no startup error, no log line and no visual error. That exact failure shipped live for a month after the 2026-09-12 namespace rename. Nothing in this area needs a Claude turn, so every test below is free.

**43 tests** · 43 free, none paid · about 3.5 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

> [!NOTE]
> The shell is `MudLayout`/`MudDrawer`/`MudMainContent` with `MudNavMenu`, and the theming system
> the shell used to link as CSS files (`theme.css`, `themes/huddle-dark.css`, an inline per-token
> `<style>` override) is gone — see [ADR-0010](../../adr/0010-a-theme-is-a-mudblazor-theme.md) and
> `appearance-theme.md`, which owns every theme-application test. The two tests that covered that
> retired CSS cascade (SHELLNAV-21 and SHELLNAV-22) are removed; their ids are not reused. The
> `@Assets[...]` asset-loading checks this file leads with (SHELLNAV-01, -02) cover the current
> asset list: two MudBlazor static assets, `_content/MudBlazor/MudBlazor.min.css` and `.min.js`,
> are unfingerprinted, and a fingerprinted `app-vars.css` sits alongside `app.css`.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-UAT`](common.md#p-launch-uat) from
[Common procedures](common.md) (the free lane with Ada and Bo to talk to), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Browse to `http://localhost:5100` in Chrome or Edge. Open DevTools (F12) and keep the Console, Network and Elements tabs available.
2. In DevTools Network, tick **Disable cache** and leave the tab recording for the whole session. Several tests read its Status column.
3. Every `curl.exe` oracle in this area runs in `T-B`, so it never disturbs the app.
4. Confirm the baseline state: the `Rooms` group in the sidebar lists the one-to-one Rooms `Ada` and `Bo` once opened (groups start closed; the group holding the current page opens by itself, so open `Rooms` by clicking it if it is closed) (the built-in Chief of Staff's Room may be listed too), and both Agents show **Online** on their avatars in their Rooms. A different set means an earlier session left state behind — that is fine for most tests here, but note it, because tests that name `Ada` and `Bo` assume this pair. Re-run `P-LAUNCH-UAT` to return to it.
5. No test in this area needs a Model or an Effort. If a step below appears to ask you to pick one, that step is in the wrong area — stop and report it rather than picking.
6. Do NOT create or edit an Agent during this area (no **Add an Agent**, no **Edit**). Opening that card probes the model catalog, which is another area's business and the one place on these pages that can reach an Adapter.

Top Bar search (the box in the middle of the Top Bar) is not tested here. Its placement is
GLOBALSEARCH-09 in [global-search.md](global-search.md).

## Tests

### SHELLNAV-01 — Every stylesheet the shell links is fingerprinted (except the one that is deliberately static) and actually serves

**Free** · about 5 min

*Proves the three `@Assets[...]` stylesheet lookups resolved, catching the silent 404 that shipped for a month after the namespace rename — and confirms the one stylesheet that is NOT supposed to be fingerprinted, MudBlazor's own, is linked first and still serves.*

**Before you start**

- App running on http://localhost:5100.
- `T-B` open at `<repo>`.

**Steps**

1. In `T-B` run: `$html = curl.exe -s http://localhost:5100/settings | Out-String` (`/settings`, not `/`: the bare `/` answers `302` with no page, see SHELLNAV-04).
2. Run: `[regex]::Matches($html, '<link rel="stylesheet" href="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }`
3. Write down every href it printed, in order.
4. For each href printed, run: `curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5100/<paste the href here>"` and record the three-digit status it prints.
5. Run: `([regex]::Matches($html, 'Team\.App')).Count`
6. In the browser, press Ctrl+Shift+R to hard-reload. In DevTools Network, set the filter to **CSS**.
7. Read the Status column for every row in the filtered CSS list.

**Pass if — all of these**

- Step 2 printed exactly FOUR hrefs, in this order: `_content/MudBlazor/MudBlazor.min.css` (no fingerprint — see notes), then three fingerprinted ones for `app.css`, `app-vars.css` and `Huddle.App.styles.css`.
- Each of the LAST three hrefs contains a fingerprint - a dot-separated random-looking segment before `.css`, e.g. `app.okpt0txbqy.css`, `app-vars.a1b2c3d4e5.css`, `Huddle.App.z3g6d1kai5.styles.css`.
- Every status printed in step 4 is `200`.
- Step 5 printed `0`.
- Every row in the DevTools CSS list shows status 200; no row shows 404.

**Fail if — any of these**

- Any of the three `@Assets[...]` hrefs is the plain key with no hash in it - `app.css`, `app-vars.css` or `Huddle.App.styles.css` -> the `@Assets[...]` lookup for that key failed and returned the key verbatim; the browser will 404 on it with no error anywhere. This is the documented silent failure and is a defect.
- The FIRST href (`_content/MudBlazor/MudBlazor.min.css`) carries a fingerprint, or is missing entirely -> either someone routed it through the fingerprinting pipeline by mistake (it should not need to, and does not go through `@Assets[...]`), or the reference was dropped from `App.razor`.
- Any status in step 4 is 404 -> that stylesheet is not being served at all; expect the error-banner symptom in SHELLNAV-03 as well.
- Step 5 printed anything other than 0 -> the pre-rename bundle name `Team.App.styles.css` is back in App.razor. The scoped-CSS bundle is correctly named `Huddle.App.styles.css` because projects and assemblies are `Huddle.*` while namespaces are `Agency.Huddle.*`; a `Team.App` reference is the real bug.
- Fewer than four hrefs, or a different order -> a `<link>` was removed from, or reordered in, `App.razor`. Order matters here: `App.razor`'s own comment explains that MudBlazor's reset must load FIRST so the app's own bare-element CSS rules win source-order ties.

**Inconclusive if**

If `curl.exe` is not found, use the browser instead: View Source (Ctrl+U) on http://localhost:5100/settings, read the four `<link rel="stylesheet">` hrefs, and click each one - a 404 page rather than CSS text is a fail. If the app is not listening (connection refused), that is not a result for this test - fix the launch and re-run.

> [!NOTE]
> `theme.css` is gone — the hand-built theming system it belonged to was replaced by `MudTheme`
> (see `appearance-theme.md`). `app-vars.css` is new: it declares `--font-mono`, the one token
> MudBlazor's own theme has no equivalent for. `_content/MudBlazor/MudBlazor.min.css` is a static
> asset served straight from the NuGet package's `staticwebassets`, not through `@Assets[...]`, so
> it is correctly and deliberately unfingerprinted — do not report its bare filename as the same
> defect class as the other three.

> [!NOTE]
> This is the single highest-value check in the area. Run it first and re-run it after any change to App.razor. The automated guard `tests/Huddle.Tests.Hosted/Ui/AppStylesheetTests.cs` covers exactly this for stylesheets, so a failure here means that test also broke.

### SHELLNAV-02 — Every shell script is fingerprinted (except MudBlazor's own) and serves - the gap the automated guard does not cover

**Free** · about 5 min

*Proves `blazor.web.js` and the app's own JS modules resolved, which no test in the repo checks because the automated regex matches only `<link rel="stylesheet">` — and that MudBlazor's own script, linked BEFORE `blazor.web.js`, is present and correctly unfingerprinted. The app's JS is ES modules (`composer.js`, `scroll.js` and ten more), loaded through the import map rather than `<script>` tags, and imported when the circuit opens.*

**Before you start**

- App running.
- SHELLNAV-01 finished (it reuses the same page fetch technique).

**Steps**

1. In `T-B` run: `$html = curl.exe -s http://localhost:5100/settings | Out-String`
2. Run: `[regex]::Matches($html, '<script[^>]*src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }`
3. Write down every src printed, in order.
4. For each src, run: `curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5100/<paste the src here>"` and record the status.
5. In the browser, press Ctrl+Shift+R. In DevTools Network, set the filter to **JS**.
6. Read the Status column for every row in the filtered JS list.
7. Run: `[regex]::Matches($html, '"\./([a-z-]+)\.js":\s*"\./([^"]+)"') | ForEach-Object { "$($_.Groups[1].Value) -> $($_.Groups[2].Value)" }` to list the import map's module entries.
8. In DevTools Network (filter **JS**, after the Ctrl+Shift+R of step 5), find `composer.<hash>.js`, `scroll.<hash>.js`, `clipboard.<hash>.js` and `storage.<hash>.js`.
9. In the DevTools Console, type `typeof window.mudElementRef` and press Enter, to confirm MudBlazor's own script executed.

**Pass if — all of these**

- Step 2 printed, in this order: `Components/Layout/ReconnectModal.<hash>.razor.js` (the reconnect modal's module comes first, because `<ReconnectModal />` sits above the other scripts in `App.razor`), then `_content/MudBlazor/MudBlazor.min.js` (no fingerprint — deliberate, see notes), then `_framework/blazor.web.<hash>.js`. There is no `app.js`. A development host may append further scripts (for example browser-refresh); ignore any after `blazor.web.<hash>.js`.
- The ReconnectModal and `blazor.web` entries carry a fingerprint segment (the hash sits before `.razor.js` in the first one); neither is the bare key `_framework/blazor.web.js`.
- Step 7 lists, among others, `composer`, `scroll`, `feed`, `sidebar`, `tabs`, `search`, `storage`, `resize`, `clipboard`, `library-document`, `avatar-library` and `task-list-resize`, each mapped to a fingerprinted file (`composer -> composer.<hash>.js`).
- Every status in step 4 is `200`.
- No row in the DevTools JS list shows 404, and step 8 found all four modules with status 200 (every module is imported when the circuit opens, before you click anything).
- Step 9 printed `"object"`.

**Fail if — any of these**

- A module is missing from step 7, maps to an unfingerprinted name, or 404s in step 8 -> its `JsModule` path does not match a file in `wwwroot/`, or the `<ImportMap />` in `App.razor` is gone. Nothing on screen says so; for `composer.js` the symptom is that pressing Enter in the composer inserts a newline instead of sending, and for `scroll.js` that the transcript never auto-scrolls. Confirm with SHELLNAV-19 and report as a defect plus a test-coverage gap.
- `blazor.web.js` 404s -> nothing on the page will be interactive and no `_blazor` websocket will open. Loud, but the same root cause.
- `MudBlazor.min.js` 404s, or step 9 printed `"undefined"` -> every MudBlazor interactive feature (dialogs, popovers, ripples) will silently fail to initialise.
- The `ReconnectModal.<hash>.razor.js` entry never appears anywhere in the document -> the reconnect modal in SHELLNAV-26 will never appear at all.
- `MudBlazor.min.js`'s src carries a fingerprint -> it was routed through the fingerprinting pipeline by mistake; it is a static asset and should not need to be.

**Inconclusive if**

If the Console shows a Content-Security-Policy or extension error rather than a 404, disable browser extensions or use a private window and re-run. If a module shows 200 in Network but its feature does not work, the module loaded but threw - open the Console, find the exception (or the server log's "Unhandled exception in circuit"), and report THAT rather than an asset failure.

> [!NOTE]
> The automated stylesheet guard deliberately matches only stylesheets, so this test is the only thing standing between a stale script key and production. Note it as a coverage gap when reporting, regardless of outcome.

### SHELLNAV-03 — The scoped-CSS bundle is applied: the error banner stays hidden below the fold

**Free** · about 5 min

*Proves `Huddle.App.styles.css` reached the browser, seen from the one place a missing scoped bundle is visible without reading source.*

**Before you start**

- App running.
- A room page open (the default landing page).

**Steps**

1. In the browser, click `Ada` in the sidebar.
2. Click once on an empty part of the main column, then press the **End** key.
3. Scroll the window to its very bottom with the mouse wheel as well.
4. Look for yellow-backgrounded text reading `An unhandled error has occurred.` with a `Reload` link and a 🗙.
5. In the DevTools Console, type `getComputedStyle(document.getElementById('blazor-error-ui')).display` and press Enter.
6. Type `getComputedStyle(document.getElementById('blazor-error-ui')).position` and press Enter.
7. Type `document.getElementById('blazor-error-ui').getAttributeNames()` and press Enter.
8. Click the gear icon (**Settings**, icon-only, bottom of the sidebar) and repeat steps 2 to 6 on that route.

**Pass if — all of these**

- No yellow `An unhandled error has occurred.` text is visible anywhere on either route, at any scroll position.
- Step 5 printed `"none"` on both routes.
- Step 6 printed `"fixed"` on both routes.
- Step 7 printed an array that includes an attribute starting `b-` (ten random characters), alongside `id` and `data-nosnippet`.

**Fail if — any of these**

- The yellow banner text is visible as ordinary page text -> `Huddle.App.styles.css` is not loading; its `display: none` never arrived. Go straight back to SHELLNAV-01 - this is the same defect seen from the other side, and it went unnoticed for a month precisely because the `position: fixed` that would have floated it into view lives in the same unloaded file, leaving it one viewport below the fold.
- Step 5 printed anything but `"none"` -> the scoped rule is not applying even though the file may be served; check that the `b-` attribute in step 7 matches the one the stylesheet selects on.
- Step 7 shows no `b-` attribute -> scoped CSS was not compiled into MainLayout at all.
- The banner is visible AND the app console in `T-A` shows a real unhandled exception at the same moment -> that is not this defect; that is SHELLNAV-26, a genuine circuit fault. Check `T-A` before filing.

**Inconclusive if**

If `document.getElementById('blazor-error-ui')` returns `null`, the markup itself is missing from MainLayout - that is a different defect; report it as such rather than as a CSS failure. If the page has not finished loading, wait for the Network tab to go quiet and re-run the console commands.

> [!NOTE]
> The `lightyellow` background and `color-scheme: light only` on this element are a WRITTEN exemption from tokenisation - this is the one moment a theme cannot be trusted. Do not file them as token violations.

### SHELLNAV-04 — GET / redirects to the oldest room

**Free** · about 4 min

*Proves the bare root never rests as an empty page while any room exists, and that it picks the first room by creation order.*

**Before you start**

- At least one room exists (`P-LAUNCH-UAT` has been run, so the `Ada` and `Bo` Rooms are there).

**Steps**

1. In `T-B` run: `curl.exe -s -D - -o NUL http://localhost:5100/`
2. Read the first line of the response and the `Location:` header.
3. In the browser, click into the address bar, type `http://localhost:5100/` exactly, and press Enter.
4. Read the URL the address bar settles on.
5. Read the `<h1>` at the top of the main column.
6. Compare that `<h1>` text against the FIRST entry in the sidebar room list.

**Pass if — all of these**

- Step 1 printed `HTTP/1.1 302 Found` as its status line.
- The `Location:` header names `/rooms/` followed by a 32-character hexadecimal id.
- In the browser, the address bar ends on `http://localhost:5100/rooms/<32-hex-id>`, never on the bare `/`.
- The main column shows a transcript with an `<h1>` naming that room.
- With the `P-LAUNCH-UAT` pair present, the `<h1>` equals the first entry in the sidebar list, which is `Ada` (seeded before `Bo`) unless a built-in Room precedes it.

**Fail if — any of these**

- Status 200 with the address bar resting on `/` and an empty main column -> the redirect in `Chat.razor`'s `OnParametersSetAsync` did not run. Note that `BlazorDisableThrowNavigationException=true` is set for this project, so a broken redirect presents as a silently swallowed navigation rather than an exception - absence of an error in `T-A` does not mean absence of a bug.
- The `Location:` names a room that is NOT the first entry in the sidebar -> the `ORDER BY created, id` ordering behind the room list and the redirect have diverged.
- The browser bounces between URLs and never settles -> a redirect loop.
- Status 500 -> an unhandled exception; capture the stack from `T-A`.

**Inconclusive if**

If `sqlite3` is available you can confirm the target exactly with `sqlite3 <data>\team.db "SELECT id, name FROM rooms ORDER BY created, id LIMIT 1;"` (on `P-LAUNCH-UAT`, `<data>` is `C:\seeds\huddle\uat\data`). If `sqlite3` is NOT installed, do not treat that as a failure - compare the `<h1>` against the first sidebar entry instead, which reads from the same query. If the sidebar is empty, this test does not apply; run SHELLNAV-24 instead.

> [!NOTE]
> Rooms are ordered by creation time then id, never alphabetically and never by recent activity. `Ada` landing before `Bo` is correct because it is seeded first.

### SHELLNAV-05 — /rooms/{unknown-id} returns 200 and shows the shared empty state while the sidebar still lists rooms

**Free** · about 3 min

*Confirms the documented self-contradicting screen is exactly that, and not a crash or a lost address bar.*

**Before you start**

- At least one room exists, so the contradiction is visible.

**Steps**

1. In `T-B` run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/rooms/does-not-exist`
2. In the browser, navigate to `http://localhost:5100/rooms/does-not-exist`.
3. Read the sidebar room list.
4. Read the main column.
5. Look for an `<h1>` anywhere on the page.
6. Look for a message composer (a textarea with the placeholder `Message {Room name}… (/invite @Agent)`) at the bottom of the main column.
7. Check the address bar still reads `http://localhost:5100/rooms/does-not-exist`.

**Pass if — all of these**

- Step 1 printed `200`.
- The sidebar still lists `Ada` and `Bo`.
- The main column shows the single sentence `No rooms yet. Start an agent to create one.`
- There is no `<h1>` on the page.
- There is no composer.
- The address bar is unchanged.

**Fail if — any of these**

- A 500 or an unhandled-exception page -> `Chat.razor` tried to dereference a null room. Defect.
- A redirect that changes the address bar -> the unknown id was silently swallowed into a real room; a bookmark to a deleted room would then land somewhere unexpected without saying so. Defect.
- The sidebar disappears -> the page escaped MainLayout. Defect.

**Inconclusive if**

If the sidebar is empty because no rooms exist, the contradiction cannot be observed - create a room first (SHELLNAV-18) or run this after `P-LAUNCH-UAT` has restored the `Ada` and `Bo` Rooms.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: this 200-with-'No rooms yet' screen is the current deliberate behaviour - `Chat.razor` renders the same shared empty state whenever its room resolves to null. Record the misleading wording as a UX observation, never as a defect.

### SHELLNAV-06 — An unmatched route returns a bare HTTP 404 with a zero-byte body

**Free** · about 3 min

*Confirms the deliberate absence of an in-app Not Found page is a clean 404, not a 500 or a hang.*

**Before you start**

- App running.

**Steps**

1. In `T-B` run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/nope`
2. Run: `curl.exe -s -o NUL -w "%{size_download}" http://localhost:5100/nope`
3. Run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/rooms`
4. In the browser, navigate to `http://localhost:5100/nope`.
5. Describe what the browser shows.
6. Check `T-A` for any exception logged at the moment of the request.

**Pass if — all of these**

- Step 1 printed `404`.
- Step 2 printed `0`.
- Step 3 printed `404`.
- The browser shows its OWN error page - in Chrome, `This page isn't working` / `HTTP ERROR 404` - with no sidebar, no **Library** or **Settings** links, and no styling.
- `T-A` logged no exception.

**Fail if — any of these**

- A 500 -> routing threw instead of falling through. Defect.
- The request hangs or times out -> defect.
- An unmatched path renders some OTHER page's content (a room, Settings) -> a route template is over-matching. Defect.
- `T-A` logs an unhandled exception -> defect, capture the stack.

**Inconclusive if**

If a browser extension or a corporate proxy substitutes its own 404 page, the browser half is inconclusive - trust the two `curl.exe` results, which bypass it.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: `Routes.razor` has a `<Found>` branch only and no `<NotFound>`, by decision. `Settings.razor`'s own code comment names this as the reason its `{Tab}` parameter must fall back rather than throw. Do NOT file 'unknown URL shows an ugly browser error' as a defect - file it once as an observation.

### SHELLNAV-07 — Routes are case-insensitive

**Free** · about 2 min

*Proves a capitalised or shouted URL still reaches its page rather than the bare 404.*

**Before you start**

- App running.

**Steps**

1. In `T-B` run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/Settings`
2. Run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/LIBRARY`
3. In the browser, navigate to `http://localhost:5100/Settings`.
4. Read the `<h1>`.
5. Navigate to `http://localhost:5100/LIBRARY`.
6. Read the page.

**Pass if — all of these**

- Both `curl.exe` calls printed `200`.
- `/Settings` shows an `<h1>` reading `Settings` with the seven-tab bar (SHELLNAV-11).
- `/LIBRARY` shows the Library page, not the bare 404.

**Fail if — any of these**

- Either URL returns 404 -> routing lost its case-insensitive matching; a bookmark or a link with different casing now dead-ends on the bare browser 404 page, because there is no in-app Not Found to soften it. Defect.

**Inconclusive if**

None expected. If the app is not listening, fix the launch first.

> [!NOTE]
> Confirmed working today; treat any failure here as a routing-configuration regression in `Program.cs`.

### SHELLNAV-08 — The drawer is present, identical and fixed-width on every route

**Free** · about 8 min

*Proves no page declares its own layout and the shell's chrome never varies. The shell is now `MudLayout`/`MudDrawer`/`MudMainContent` (Stage 5 of the MudBlazor migration) — the old hand-rolled `.app-grid` CSS grid and its `.sidebar` / `.sidebar-link` classes are gone, and the drawer now holds the 48px Activity bar (`S5`) beside the 240px Sidebar (`S2`), 289px in all with its 1px edge.*

**Before you start**

- At least one room exists.

**Steps**

1. In the browser, navigate to `http://localhost:5100/` and let it redirect to a room.
2. In the DevTools Console, type `getComputedStyle(document.querySelector('.mud-drawer')).width` and press Enter. Record the value.
3. Type `[...document.querySelectorAll('.mud-drawer-content > *')].map(e => e.className)` and press Enter. Record the list in order (the drawer's own content sections; `.mud-drawer > *` would list MudBlazor's wrapper elements instead).
4. Type `[...document.querySelectorAll('.mud-drawer .mud-nav-link')].filter(a => ['/settings'].includes(a.getAttribute('href'))).map(a => (a.textContent.trim() || a.getAttribute('aria-label')) + ' -> ' + a.getAttribute('href'))` and press Enter. Record the result.
5. Type `getComputedStyle(document.querySelector('.main-column')).overflow` and press Enter.
6. Click the **Library** button in the Activity bar to open the Library page, and repeat steps 2 to 5.
7. Click the gear icon (**Settings**) at the bottom of the Activity bar and repeat steps 2 to 5.
8. Navigate to `http://localhost:5100/settings/appearance` and repeat steps 2 to 5.
9. On the Settings route, scroll the drawer with the mouse wheel while hovering over it, then scroll while hovering over the main column.

**Pass if — all of these**

- Step 2 printed `289px` on all four routes (the Activity bar, its edge and the open Sidebar).
- Step 3 lists exactly two entries, in this order, on all four routes: `activity-bar` and `sidebar-views` (`MainLayout.razor`: the Activity bar, whose bottom toolbar holds the Archived and Settings menu, then the Sidebar's views: Rooms, Teams, Tasks, Library and Scheduled, plus one Team's view).
- Step 4 printed exactly `Settings -> /settings` on all four routes — MudBlazor renders the link text as typed (title case), and the Settings link has no text, so its label comes from `aria-label`.
- Step 5 printed `"hidden"` on all four routes — `.main-column` is still the literal `Class` MudMainContent carries and its CSS rule is unchanged.
- The drawer scrolls independently; the browser window itself never grows a page-level horizontal or vertical scrollbar.

**Fail if — any of these**

- The drawer is missing on one route -> that page declared its own layout instead of inheriting MainLayout. Defect, and the route is unreachable from anywhere else.
- The Settings link label reads anything other than `Settings` -> the shell's navigation wording drifted.
- Fewer or more than six sections -> a drawer entry was added or removed in `MainLayout.razor`; confirm that is intended and update this test.
- `.mud-drawer`'s computed width is not `240px` -> the `Width="240px"` parameter on `MudDrawer` was changed or removed.
- The whole page scrolls instead of only the drawer and the transcript -> `.main-column`'s `height: 100vh; overflow: hidden` was lost.

**Inconclusive if**

If the console commands return `null`, the page had not finished rendering - wait for the Network tab to go quiet and re-run. If you resized the window very narrow, the main column will simply squeeze; that is expected, see notes.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: there is no responsive or collapsible drawer here — `MudDrawer`'s `Variant="DrawerVariant.Persistent"` and fixed `Width="240px"` mean a narrow window squeezes the main column exactly as the old `.app-grid` did. This is a single-user proof of concept with no mobile layout to regress - do not file it.

### SHELLNAV-09 — The sidebar marks exactly one room active, with aria-current

**Free** · about 5 min

*Proves the active-room highlight tracks the URL and carries the screen-reader attribute a visual check would miss.*

**Before you start**

- At least two rooms, so the active/inactive contrast is visible. The `Ada` and `Bo` pair from `P-LAUNCH-UAT` is enough.

**Steps**

1. In the browser, click `Ada` in the sidebar.
2. In the DevTools Console, type `document.querySelectorAll('.room-list a.active').length` and press Enter.
3. In the Console, type `[...document.querySelectorAll('.room-list a')].filter(a => a.getAttribute('aria-current')).map(a => a.textContent.trim() + ' | ' + a.getAttribute('aria-current'))` and press Enter.
4. Look at the sidebar: confirm `Ada` has a tinted background and heavier text, and `Bo` has neither.
5. Read the `<h1>` in the main column.
6. Click `Bo` in the sidebar.
7. Repeat steps 2, 3, 4 and 5, expecting `Bo` to be the highlighted one and `Ada` to be plain.
8. Read the `<h1>` in the main column again.

**Pass if — all of these**

- Step 2 printed `1` both times.
- Step 3 printed `Ada | page` the first time and `Bo | page` the second time - one entry, never two.
- Exactly one room link is tinted and bold at a time; the other is plain.
- The `<h1>` matches the highlighted room name each time.

**Fail if — any of these**

- Step 2 printed `0` -> no room is marked active; the `NavLink` match was lost and a reader cannot tell which room they are in.
- Step 2 printed `2` or more -> every room is highlighted; the `NavLink` Match mode regressed to prefix matching.
- The highlight stays on the previously-open room after clicking the other -> the active state is stale.

**Inconclusive if**

If only one room exists, the 'other room is plain' half cannot be judged - note it as partially inconclusive and run SHELLNAV-18 first to create a second room.

> [!NOTE]
> The highlight is no longer a hand-written `.room-list a.active` rule in `app.css` — the room list is now `MudNavMenu`/`MudNavLink` (Stage 5 of the MudBlazor migration), which carries its OWN active styling out of `MudBlazor.min.css`, keyed off the same literal `active` class `NavLink` has always applied. `.room-list a.active` as a selector still matches (`.room-list` still wraps the list, and `MudNavLink` still renders `<a class="mud-nav-link ... active">`), so the query in step 2 is unchanged. If the tint is invisible but the class is present, suspect a missing MudBlazor stylesheet - check SHELLNAV-01, not `app.css`. `aria-current="page"` IS emitted on the active link - verified on `main`, where the active room renders `class="mud-nav-link mud-ripple active"` and carries `aria-current="page"`, exactly one link at a time. So the CSS class and the attribute are two independent oracles and both must agree.

### SHELLNAV-10 — /agents renders, and a bare page load spawns no node process

> **Retired (2026-11-18).** The Agents page was removed; nothing to run.

### SHELLNAV-11 — /settings renders the seven-tab bar and defaults to Profile

**Free** · about 4 min

*Proves a bare /settings resolves its optional {Tab} parameter to exactly one active tab.*

**Before you start**

- App running.
- `O-ADAPTERS` reads `0`.

**Steps**

1. In the browser, click the gear icon (**Settings**) at the bottom of the sidebar.
2. Read the address bar.
3. Read the `<h1>`.
4. Read the labels of the tabs in the bar ABOVE the content — `MudTabs` renders horizontally at the top by default, not as a column down the left. Read left to right.
5. In the DevTools Console, type `[...document.querySelectorAll('.mud-tabs .mud-tab')].map(b => b.textContent.trim() + ' | active=' + b.classList.contains('mud-tab-active'))` and press Enter.
6. Read the pane below and confirm it describes the Human's profile - it should contain the sentence beginning `This is you, the Human.`, a profile card, and the fields `Name`, `Alias`, `Title` and `Description`.
7. In `T-B` run `O-ADAPTERS` and compare with the baseline.

**Pass if — all of these**

- The address bar reads exactly `http://localhost:5100/settings` with no segment appended on load.
- The `<h1>` reads exactly `Settings`.
- The tab bar holds exactly seven tabs, reading `Profile`, `Prompts`, `Appearance`, `Personas`, `Skills`, `Team types`, `Library` (title case as typed; MudBlazor does not upper-case them here), left to right, above the pane.
- Step 5 printed seven entries in that order: `Profile | active=true` and the other six `active=false`.
- The pane below is the Profile pane.
- The `node` count is unchanged from the baseline.

**Fail if — any of these**

- More than one tab reports `active=true`, or none does -> the active-tab computation broke; a reader cannot tell which pane they are on.
- A tab is missing or the order differs -> `Settings.razor`'s `MudTabPanel` list changed; confirm that is intended and update this test.
- The Prompts or Appearance pane shows on a bare `/settings` -> the fallback picked the wrong tab; Profile is the first tab and the fallback.
- The address bar gains `/profile` on load -> the page is redirecting where it should not, which would put a spurious entry in the browser history on every visit. (The first-run redirect to `/settings/profile` when no `profile.json` exists is a different, expected path and starts from `/`, not from `/settings`.)
- The `node` count rises -> Settings is spawning an adapter, which it must never do.
- The tab bar renders as a vertical column down the left rather than horizontally above the content -> `MudTabs.Position` was set away from its default; nothing in this area asks for that.

**Inconclusive if**

If the page shows an error line above the tab bar, read it and note it, but it does not invalidate the tab-bar observations - report both. If the Profile pane opens with a `Welcome.` line, `<data>\profile.json` does not exist yet; that is normal and does not change any check here.

> [!NOTE]
> `prompts.json` does not exist on a fresh install and the app never creates it just to read from it. Absent means 'nothing is overridden'. Do not file 'the settings file is missing' as a defect.

### SHELLNAV-12 — Clicking a settings tab changes the URL, and the URL round-trips as a bookmark

**Free** · about 6 min

*Proves tab selection is real navigation, so a bookmark and the Back button both work.*

**Before you start**

- App running.
- Browser history for this session starting from /settings.

**Steps**

1. In the browser, navigate to `http://localhost:5100/settings`.
2. Click the **Appearance** tab button.
3. Read the address bar.
4. Confirm the pane below now shows the theme lists (a `Light theme` list and a `Dark theme` list, with no group headings), a paragraph beginning `The selection is stored at` that names the file path, and, last, the paragraph `Your own avatar is set on the Profile tab.` with `Profile` as a link. There is no avatar editor on this pane.
5. Click the **Prompts** tab button.
6. Read the address bar.
7. Press the browser Back button once and read the address bar.
8. Press Back again and read the address bar.
9. Press Back a third time and read the address bar.
10. Open a brand-new browser tab and paste `http://localhost:5100/settings/appearance`, then press Enter.
11. Read which tab is active in that new tab.
12. In `T-B` run: `$s = curl.exe -s http://localhost:5100/settings/appearance | Out-String; [regex]::Match($s, 'mud-tab-active[^>]*>(?:\s*<[^>]+>)*\s*([A-Za-z ]+)').Groups[1].Value`

**Pass if — all of these**

- After step 2 the address bar reads `http://localhost:5100/settings/appearance`.
- After step 5 the address bar reads `http://localhost:5100/settings/prompts`.
- Back walks `…/settings/prompts` -> `…/settings/appearance` -> `…/settings`, one step at a time.
- The fresh tab from step 10 opens directly with **Appearance** active and the theme list showing.
- Step 12 printed `Appearance`.

**Fail if — any of these**

- The tab flips visually but the address bar does not change -> tab selection was reduced to local component state; a bookmark to `/settings/appearance` would then always land on Profile, and the Back button would leave the page entirely instead of returning to the previous tab. Defect.
- Back does nothing, or jumps straight past all three entries in one press -> the navigation used replace rather than push. Defect.
- The fresh tab opens on Profile despite the `/appearance` URL -> the route parameter is not being read on a cold load; step 12 will print `Profile` and confirm it.

**Inconclusive if**

If the browser was already deep in history from earlier tests, the Back sequence is polluted - open a fresh tab, navigate to `/settings`, and repeat steps 2 to 9 there. If `curl.exe` is unavailable, or step 12 prints nothing (the tab markup is not guaranteed to match the pattern), judge from the browser alone and note that the source-level oracle was skipped.

> [!NOTE]
> The tab segment is always emitted lowercase (`/settings/prompts`, `/settings/appearance`) regardless of the button's displayed capitalisation.

### SHELLNAV-13 — An unrecognised {Tab} segment silently falls back to Profile, and the tab name is matched without regard to case

**Free** · about 6 min

*Proves the deliberate fallback holds for an unknown segment, and that a known tab name in any case still selects that tab.*

**Before you start**

- App running.

**Steps**

1. In `T-B` run each of these and record the status and the active tab it reports:
2. `$u='http://localhost:5100/settings/bogus'; curl.exe -s -o NUL -w "%{http_code} " $u; $s = curl.exe -s $u | Out-String; [regex]::Match($s,'mud-tab-active[^>]*>(?:\s*<[^>]+>)*\s*([A-Za-z ]+)').Groups[1].Value`
3. Repeat the previous command with `$u='http://localhost:5100/settings/PROMPTS'`.
4. Repeat with `$u='http://localhost:5100/settings/Appearance'`.
5. Repeat with `$u='http://localhost:5100/settings/APPEARANCE'`.
6. Repeat with `$u='http://localhost:5100/settings/'`.
7. In the browser, navigate to `http://localhost:5100/settings/bogus` and confirm the page renders normally with no error banner.
8. In the browser, navigate to `http://localhost:5100/settings/APPEARANCE` and read which tab is active.

**Pass if — all of these**

- Every one of the five URLs returned `200`.
- `/settings/bogus` reports active tab `Profile`.
- `/settings/PROMPTS` reports `Prompts`.
- `/settings/Appearance` reports `Appearance`.
- `/settings/APPEARANCE` reports `Appearance`.
- `/settings/` reports `Profile`.
- The browser shows a normal Settings page for `/settings/bogus` - no 404, no error line, no blank content pane.

**Fail if — any of these**

- Any URL returns 404 -> the fallback was lost; because there is no in-app Not Found page, the user lands on the bare browser error page from a typo in a tab name. Defect.
- `/settings/PROMPTS`, `/settings/APPEARANCE` or `/settings/Appearance` falls back to Profile -> the parse lost `ignoreCase: true`. Defect.
- An unknown tab renders a blank content pane rather than the Profile pane -> the switch lost its default branch. Defect.
- `/settings/bogus` or `/settings/` reports `Prompts` -> the fallback still names Prompts, the default before the Profile tab existed. Defect.
- An unhandled exception page -> defect, capture the stack from `T-A`.

**Inconclusive if**

If the regex oracle prints nothing at all, the markup shape changed - fall back to the browser and read which tab button is visibly highlighted, noting that the source-level check could not be applied.

> [!NOTE]
> This fallback exists specifically because `Routes.razor` has no `NotFound` branch, and `Settings.razor`'s own comment says so. Any new tab must inherit the same behaviour. This test checks the fallback (Profile, the first tab) and case-insensitivity on Prompts and Appearance only; the other four tabs (Personas, Skills, Team types, Library) have no URL test in this area.

### SHELLNAV-14 — No route sets a browser tab title

**Free** · about 4 min

*Records, once for the whole shell, that nothing feeds <HeadOutlet /> - and catches the opposite regression, a title appearing on some routes only.*

**Before you start**

- App running.

**Steps**

1. In the browser, visit each of these in turn and after each one read the browser TAB label and run `document.querySelector('title')` in the DevTools Console: `http://localhost:5100/` (which lands on a room), then a different room via the sidebar, then `http://localhost:5100/library`, then `http://localhost:5100/settings`, then `http://localhost:5100/settings/appearance`.
2. Record the tab label and the console result for all five.
3. In `T-B` run: `foreach ($p in '/library','/settings','/settings/appearance') { $h = curl.exe -s "http://localhost:5100$p" | Out-String; "$p -> " + ([regex]::Matches($h,'<title')).Count }`

**Pass if — all of these**

- Every browser tab shows `localhost:5100/<path>` or just `localhost:5100` - never a human title such as `Settings` or `Huddle`.
- `document.querySelector('title')` returns `null` on all five routes.
- Step 3 printed `0` for each of its three paths (`/` is left out: it redirects and has no page of its own to count).

**Fail if — any of these**

- A title appears on SOME of the five routes but not others -> a `<PageTitle>` was added to one page only; the tab now lies about where the user is on every other page. Defect.
- A title from the PREVIOUS route persists after an enhanced navigation -> a stale `<PageTitle>` is not being cleared. Defect.

**Inconclusive if**

If the browser tab is too narrow to read the label, hover it for the tooltip or widen the window. If a browser extension injects a title, use a private window.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: none of the pages this test visits declares a `<PageTitle>`, so every tab reads as its URL. `<HeadOutlet />` is wired up in App.razor but nothing feeds these routes. One page does declare a title: the Team page (`/teams/{Team}`, `TeamPage.razor:49`), which this area does not visit; the tab there reads as that title, which is expected and is not the "title on some routes only" defect, as long as the five routes above stay title-free. Report the missing titles ONCE as a single finding about the whole shell - not once per route.

### SHELLNAV-15 — Focus moves to the page's <h1> after every navigation

**Free** · about 8 min

*Proves FocusOnNavigate is working for keyboard and screen-reader users, and records the two states that have no h1 to move to.*

**Before you start**

- The interactive circuit is connected - load any page and wait two seconds after the Network tab goes quiet before starting.

**Steps**

1. In the browser, open `http://localhost:5100/` and let it redirect to a room.
2. In the DevTools Console, type `window.__probe = 1` and press Enter (this also sets up SHELLNAV-16).
3. Click the **Library** button in the Activity bar.
4. In the Console, type `document.activeElement.tagName + ' | ' + document.activeElement.textContent.trim()` and press Enter. Record it.
5. In the Elements panel, select the `<h1>` and read its attributes.
6. Click the gear icon (**Settings**) at the bottom of the sidebar, then repeat step 4.
7. Click `Ada` in the sidebar, then repeat step 4.
8. Click into the page (not the console), then press Tab once and run `document.activeElement.tagName + ' | ' + (document.activeElement.className || document.activeElement.textContent.trim())`. Record where Tab landed.
9. Navigate to `http://localhost:5100/rooms/does-not-exist` and repeat step 4.

**Pass if — all of these**

- After each of steps 3, 6 and 7, `document.activeElement.tagName` is `H1`, and its text is the Library page's heading, `Settings`, and the room name respectively.
- The `<h1>` in the Elements panel carries `tabindex="-1"`.
- In step 8, Tab lands on a control INSIDE the main column - not back at the top of the sidebar.
- In step 9, on the unknown-room page, `document.activeElement.tagName` is `BODY` - there is no `<h1>` to receive focus.

**Fail if — any of these**

- Focus stays on the sidebar link that was clicked -> `FocusOnNavigate` was removed, or its `Selector` changed away from `h1`. A keyboard user then has to Tab through the entire sidebar again after every navigation. Defect.
- Focus lands on something other than the page heading -> that page grew a second `<h1>`; the first one now wins and it may not be the page's real title.
- A JavaScript error appears in the Console on every navigation -> defect, capture the message.

**Inconclusive if**

If `document.activeElement` reports the DevTools console itself or an `<input>` in DevTools, click once on the page body first, then re-navigate and re-check WITHOUT clicking into the console in between - use the Console's history arrow rather than retyping. If the circuit has not connected (nothing on the page responds to clicks), this test cannot run at all: reload, wait for the `_blazor` websocket in the Network tab, and start again.

> [!NOTE]
> The step-9 result is expected, not a defect - it is the observable consequence of `Chat.razor` rendering a heading-free empty state. Record it as an accessibility observation alongside the SHELLNAV-05 UX note.

### SHELLNAV-16 — Sidebar links use enhanced navigation, not a full page reload

**Free** · about 6 min

*Proves the circuit survives sidebar navigation, which is what keeps the room list live and the panel state intact.*

**Before you start**

- The circuit is connected.
- DevTools Network open with **Preserve log** OFF, so a reload is visible as a cleared log.

**Steps**

1. In the browser, open a room page and wait for the Network tab to go quiet.
2. In the DevTools Console, type `window.__probe = 42` and press Enter.
3. In the Network tab, set the filter to **WS** and note the `_blazor` websocket row and its start time.
4. Click **New room** in the sidebar so the panel opens. Leave it open.
5. Click the **Library** button in the Activity bar.
6. Watch the page as it changes: note whether it white-flashes.
7. In the Console, type `window.__probe` and press Enter.
8. In the Network WS filter, check whether the `_blazor` websocket row is the SAME one from step 3 or a new one.
9. Look at the sidebar: is the **New room** panel still open?
10. Click the **Settings** gear icon at the bottom of the sidebar (it has no text; `aria-label` `Settings`, `MainLayout.razor:28`) and repeat steps 7 and 8.

**Pass if — all of these**

- Step 7 printed `42` both times - the JavaScript context survived, so no document reload happened.
- The `_blazor` websocket row is the same single row throughout; it is not torn down and re-established.
- No white flash between routes.
- The **New room** panel is still open after navigating, and the sidebar's scroll position is unchanged.

**Fail if — any of these**

- `window.__probe` printed `undefined` -> the browser did a full document reload on the sidebar click. Enhanced navigation is broken; the usual cause is `blazor.web.js` failing to load - go to SHELLNAV-02 and check the JS filter for a 404 on `blazor.web.*.js`.
- A second `_blazor` websocket appears on every navigation -> the circuit is being rebuilt each time; expect the room list to lose live updates and the New room panel to reset.
- The panel closes and the sidebar scrolls back to the top on every navigation -> the same full-reload symptom seen from the UI side.

**Inconclusive if**

If the Console was cleared by a DevTools setting rather than by a reload, `window.__probe` will still be readable - a cleared console alone proves nothing, so judge on the `__probe` value and the websocket row, not on the console log. If no `_blazor` websocket appears at all, the circuit never connected; fix that first (see SHELLNAV-02) - this test is inconclusive until it does.

> [!NOTE]
> Do not use the `blazor-enhanced-nav` response header as an oracle from `curl.exe` - it is not reliably present on a plain request. The `window.__probe` survival check is decisive and needs no header.

### SHELLNAV-17 — The New room panel toggles, lists agents with status dots, and gates Open room

**Free** · about 7 min

*Proves the sidebar's one interactive control opens, reflects real agent status, and refuses to start an empty chat. The disclosure is now `MudCollapse` (Stage 5 of the MudBlazor migration) rather than an always-in-the-DOM panel toggled by the `hidden` attribute, and its checkboxes are `MudCheckBox`, but the status dots are unchanged — `StatusDot.razor` still renders a plain `<span class="agent-dot ...">` with a `title` attribute.*

**Before you start**

- At least one Agent registered. `P-LAUNCH-UAT` has been run, so `Ada` and `Bo` are listed (the built-in Chief of Staff may be listed too).

**Steps**

1. In the browser, open any room page.
2. Click **New room** in the sidebar. It sits inside the `Rooms` group, below the Room list (`RoomList.razor:76`), not at the top of the sidebar.
3. Confirm a panel opens below the button.
4. In the DevTools Console, type `document.querySelector('.new-chat .mud-collapse-container').getBoundingClientRect().height > 0` and press Enter.
5. Read the panel: confirm one checkbox per agent, each with a small coloured dot before the name.
6. Hover the mouse over one dot and wait for the tooltip. Read it.
7. In the Console, type `[...document.querySelectorAll('.agent-dot')].map(d => d.title)` and press Enter. Record the array.
8. In the Console, type `[...document.querySelectorAll('.new-chat button')].find(b => b.textContent.trim() === 'Open room').disabled` and press Enter.
9. Tick the checkbox beside `Ada`.
10. Re-run the Console command from step 8.
11. Untick `Ada`.
12. Re-run the Console command from step 8.
13. Click **New room** again.
14. Re-run the Console command from step 4.

**Pass if — all of these**

- Step 4 printed `true` while the panel is open, and `false` after step 13 collapses it.
- Every agent has a checkbox and a coloured dot.
- The tooltip text is exactly one of `online`, `offline`, `starting` or `degraded` - lowercase, single word.
- Step 7's array contains only those four words.
- Step 8 printed `true` - the button reads **Open room** (title case; buttons are not upper-cased here, `ThemeDefaults.cs:46`) and is disabled while nothing is ticked.
- Step 10 printed `false` - ticking one agent enables it.
- Step 12 printed `true` again after unticking.

**Fail if — any of these**

- The panel does not collapse on the second click -> the toggle is one-way. Defect.
- **Open room** is enabled with nothing ticked -> the gate is missing; clicking it would attempt a chat with no agents.
- Every dot shows the same colour and the same tooltip regardless of state -> status is being derived from pipe liveness alone rather than through the resolver that puts health FIRST. A deaf runner with an open pipe must read `degraded`, never `online`. Defect.
- A tooltip reads a capitalised or different word -> the tooltip text and the CSS class have diverged.

**Inconclusive if**

`MudCollapse` animates its height and the component renders the panel's content only while the panel is open (`NewChat.razor:15-16`), so do not judge open/closed from View Source or a `curl.exe` fetch — use the `.mud-collapse-container` height (or the eye) as in step 4, and wait a second after a toggle before reading it. If no agents are listed at all, this test cannot run; that is SHELLNAV-28's territory - confirm you launched with `P-LAUNCH-UAT` (the plain lanes have no Agents beyond the Chief of Staff) and restart it.

> [!NOTE]
> Cross-check the dot colours against the health and presence lines in `T-A` for the same agent name; they must agree.

### SHELLNAV-18 — Starting a chat creates a room named after its agents and navigates straight to it

**Free** · about 6 min

*Proves the one creation path in the shell produces a correctly named room, updates the sidebar and moves the user there.*

**Before you start**

- Two or more Agents listed in the New room panel (`Ada` and `Bo` from `P-LAUNCH-UAT`).
- SHELLNAV-17 passed, so the panel is known to work.

**Steps**

1. Note the current entries in the sidebar room list.
2. Click **New room** in the sidebar.
3. Tick the checkbox beside `Ada`.
4. Tick the checkbox beside `Bo`.
5. Click **Open room**.
6. Read the address bar.
7. Read the `<h1>` in the main column.
8. Read the member line directly below the `<h1>`.
9. Read the sidebar room list.
10. Click **New room** again and look at the checkboxes.
11. In `T-B` run: `Get-ChildItem C:\seeds\huddle\uat\data` and confirm `team.db` is present. That is `<data>` under `P-LAUNCH-UAT` (`common.md:325`), not `src\Huddle.App\App_Data`.

**Pass if — all of these**

- The panel closed by itself after **Open room**.
- The address bar reads `http://localhost:5100/rooms/<32-hex-id>`, with a real id and never a trailing empty `/rooms/`.
- The `<h1>` reads exactly `Ada, Bo` - the agent names comma-space joined in selection order.
- The member line below the heading lists `You` plus both agent names.
- The sidebar gained an entry reading `Ada, Bo`, with no page refresh.
- In step 10 every checkbox is clear - the previous selection did not stick.

**Fail if — any of these**

- Navigating to `/rooms/` with no id -> the created room's id was lost. Defect.
- The new room does not appear in the sidebar until F5 -> the rooms-changed event did not reach the list; see SHELLNAV-20.
- The room is named something other than the joined agent names -> the naming rule changed.
- The checkboxes stay ticked after **Open room** -> a second click silently makes a duplicate room.

**Inconclusive if**

If `sqlite3` is installed you can confirm with `sqlite3 C:\seeds\huddle\uat\data\team.db "SELECT id, name FROM rooms ORDER BY created DESC LIMIT 1;"`. If it is NOT installed, judge from the sidebar entry and the `<h1>` alone and say the DB check was skipped - do not call the test inconclusive on that account. If **Open room** shows an error line instead, read it and report it verbatim.

> [!NOTE]
> Ticking a SINGLE agent reuses that agent's existing room rather than creating a new one - so if you tick only `Ada` you land back on the existing `Ada` room and the sidebar does not grow. That is correct, not a failure. Leaves a new room behind; it does no harm to later tests.

### SHELLNAV-19 — composer.js and scroll.js are proven functionally: Enter sends and clears, and the transcript scrolls

**Free** · about 6 min

*Turns a silent module failure into something visible, because a module that fails to import produces no error on screen.*

**Before you start**

- A room with a composer is open.
- `P-LAUNCH-UAT` has been run and Ada is Online. This test is FREE - Ada runs on the mock adapter, not a Claude model, so no model, no effort and no spend are involved.

**Steps**

1. In the browser, click `Ada` in the sidebar.
2. Scroll to the bottom of the main column and find the textarea with the placeholder `Message {Room name}… (/invite @Agent)`.
3. Click into the textarea.
4. Type exactly: `hi there`
5. Press **Enter** (not Shift+Enter).
6. Observe the textarea immediately after.
7. Observe the transcript above it.
8. Wait up to five seconds and read whether a reply from `Ada` appears.
9. Click into the textarea again, type `second line test`, press **Shift+Enter**, then type `still typing`.
10. Observe whether the textarea now holds two lines rather than having sent.

**Pass if — all of these**

- Pressing Enter sent the message: the textarea is EMPTY afterwards and no newline was inserted.
- The typed message `hi there` appears in the transcript.
- The transcript is scrolled to the bottom so the newest message is visible without scrolling by hand.
- A reply from `Ada` appears in the transcript within a few seconds; it is plain text containing `hi there`.
- Shift+Enter inserted a newline and did NOT send - the textarea holds both lines.

**Fail if — any of these**

- Enter inserts a newline instead of sending, and the textarea keeps its text -> `composer.js` never attached. Either the module failed to import (go back to SHELLNAV-02 and confirm it in Network) or its `attach` threw (the server log shows "Unhandled exception in circuit"). Report BOTH the failure and the coverage gap.
- The message sends but the transcript does not auto-scroll -> `scroll.js` did not run; same causes, milder symptom.
- Nothing at all responds to clicks -> `blazor.web.js` failed and the whole page is non-interactive; SHELLNAV-02 will name it.

**Inconclusive if**

If the message sends but `Ada` never replies, the composer half still PASSED - record the reply timeout separately as a chat-area observation, not as a module failure. If the circuit has not connected yet, the textarea will not respond at all; wait for the `_blazor` websocket and retry once.

> [!NOTE]
> Leaves two messages in the `Ada` room. That is harmless and actively useful - SHELLNAV-24 looks for the text `hi there` in the prerendered HTML of the `Ada` room, so it needs this message on screen first.

### SHELLNAV-20 — The room list updates live, with no refresh, when a room appears from outside the app

**Free** · about 10 min

*Proves the sidebar's live subscription works across circuits, and gives the leaked-subscription failure a chance to show itself.*

**Before you start**

- App running.
- `pwsh` available and a THIRD terminal free.
- This test is FREE - `tools/echo-bot.ps1` is a PowerShell pipe client, not a Claude agent.

**Steps**

1. In the browser, open a room page in TAB 1.
2. Open a SECOND browser tab (TAB 2) on `http://localhost:5100/library`.
3. Return to TAB 1 and note the exact sidebar room list.
4. Open a third PowerShell terminal at `<repo>`. Call it TERMINAL C.
5. In TERMINAL C run: `pwsh tools/echo-bot.ps1 -Name mybot`
6. Read TERMINAL C's output and wait for a line containing `"type":"welcome"`.
7. Without touching the browser, watch TAB 1's sidebar for up to five seconds.
8. Switch to TAB 2 and watch its sidebar too, again without refreshing.
9. In TAB 1, click **New room** and read the agent checkbox list.
10. Now provoke the leak case: in TAB 1, click **Library**, then **Settings**, then a room, then **Library** again - four navigations in all (the **Settings** link is the gear icon at the bottom of the sidebar). Repeat the whole sequence once more so there are eight.
11. Read TAB 1's sidebar carefully for duplicate or stale entries.
12. Scroll `T-A` and look for repeated handler errors or exception stacks.
13. Close TAB 2 entirely, wait ten seconds, and scroll `T-A` again.

**Pass if — all of these**

- A new sidebar entry reading `mybot` appears in TAB 1 within about a second, with no page refresh and no navigation.
- The same entry appears in TAB 2 without a refresh.
- The **New room** checkbox list gained `mybot` as well.
- After the eight navigations the sidebar shows each room exactly once - no duplicates, no rooms that no longer exist.
- `T-A` shows the pipe connection for `mybot` and no repeated handler errors, before or after closing TAB 2.

**Fail if — any of these**

- `mybot` only appears after pressing F5 -> the rooms-changed subscription is broken; the sidebar is no longer live. Defect.
- Duplicate sidebar entries accumulate as you navigate -> a component is leaking its subscription because it did not unsubscribe on disposal. Defect.
- `T-A` fills with repeated handler errors after closing TAB 2 -> the same leak, seen from the server; a component that never dies is still being notified. Defect, and it grows worse the longer the app runs.

**Inconclusive if**

If TERMINAL C cannot connect to the pipe (`\\.\pipe\team`), the bot never registered and this test produced no result - check that the app in `T-A` is still running and that `Team:PipeName` is `team`. If `pwsh` is not installed, skip this test and say so explicitly rather than substituting a weaker check; there is no browser-only way to make a room appear from outside the app.

> [!NOTE]
> Leave TERMINAL C's bot running or press Ctrl+C in it when done - either is fine. The `mybot` room persists in `team.db` and will appear in later tests; that is expected.

### SHELLNAV-23 — A bad appearance.json does not break the shell: no theme link, no blank page, only a log warning

**Free** · about 5 min

*The detailed validation behaviour for a bad theme id or dark-mode value — what is logged, what is (and, per known-limits.md, is no longer) shown on the Appearance tab — belongs to `appearance-theme.md`'s APPEARANCETHEME-14 now. This test keeps only the shell-level question: does a malformed selection file ever break the CHROME itself (a blank page, a missing stylesheet link, a crash)?*

**Before you start**

- Write access to `<data>`, which under `P-LAUNCH-UAT` is `C:\seeds\huddle\uat\data` (`common.md:325`). `AppearanceStore` watches `{DataDir}\appearance.json` (`AppearanceStore.cs:99`), so a file under `src\Huddle.App\App_Data` would be watched by nothing.
- The app running - no restart is needed, the file is watched.

**Steps**

1. Open `C:\seeds\huddle\uat\data\appearance.json` in a text editor (create it if it does not exist).
2. Replace its entire contents with exactly: `{"theme": "not-a-theme", "dark": "not-a-mode"}`. Only `theme` is validated — `dark` was retired on 2026-09-21 ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)) and is now an unknown key, so it is kept without comment and warns about nothing.
3. Save the file and wait five seconds.
4. In `T-B` run: `Get-Content C:\seeds\huddle\uat\data\appearance.json`. Confirm it is byte-for-byte what you typed.
5. In the browser, press F5 on any page.
6. Observe whether the page renders normally on the default Theme, `Huddle Light`, with the sidebar and all four stylesheets from SHELLNAV-01 still present. The operating system's own light/dark setting is ignored and must change nothing.
7. Scroll `T-A` and find the warning lines logged when the file was read.
8. Re-run the command from step 4 and confirm the file is STILL unchanged.

**Pass if — all of these**

- The page renders normally with no blank or unstyled screen, and the shell (drawer, room list, nav links) is intact.
- `T-A` logged ONE warning, naming the file path and the bad `theme` value (see APPEARANCETHEME-14 for the exact wording). Nothing is logged about `dark`: it is an unknown key now, and unknown keys are kept silently.
- The on-disk `appearance.json` is byte-identical to what you typed, both before and after.

**Fail if — any of these**

- A blank or unstyled page, or the shell fails to render -> a bad value must never be able to break the chrome itself, only fail to apply.
- The app rewrote, reformatted or deleted your file -> it must leave a rejected entry exactly as it found it. Defect.
- No message anywhere, not even in `T-A` -> the human has no way to learn why their theme did nothing. Defect, and the worst kind: a silent one.

**Inconclusive if**

If the file watcher does not react within ten seconds, restart the app in `T-A` with the `dotnet run` command of `P-LAUNCH-UAT` (not the seeder line: it wipes the data folder, and the file with it) and re-check - a watcher that needs a restart is itself worth noting, but judge the messages after the restart. If the JSON you pasted is malformed (a stray quote), the app may report a parse problem instead; retype it exactly and re-run.

> [!NOTE]
> CLEANUP: delete `C:\seeds\huddle\uat\data\appearance.json` when finished. Absent is the correct fresh-install state. For the on-tab reporting question (there is none any more — see known-limits.md) and the exact log wording, run APPEARANCETHEME-14 in `appearance-theme.md` instead of trying to reconstruct it here.

### SHELLNAV-24 — A deep link renders complete content on a cold first request, before any circuit attaches

**Free** · about 7 min

*Proves prerendering works, so a pasted or bookmarked URL is not a blank frame until the websocket connects.*

**Before you start**

- At least one room with messages - run SHELLNAV-19 first so the `Ada` room has content.
- The room's 32-hex id copied from the address bar.

**Steps**

1. In the browser, open the `Ada` room and copy its full URL from the address bar.
2. In `T-B` run: `$r = curl.exe -s "<paste the room URL>" | Out-String`
3. Run: `([regex]::Matches($r, '<h1[\s>]')).Count` (the heading is `<h1 class="mud-typography …">`, `Chat.razor:37`, so a bare `'<h1>'` would count 0).
4. Run: `[regex]::Match($r, '<h1[^>]*>([^<]*)</h1>').Groups[1].Value`
5. Run: `$r.Contains('hi there')` - this checks the prerendered HTML already carries the transcript.
6. Run: `$r.Contains('No rooms yet')`
7. Open a NEW private/incognito window.
8. Paste the room URL and press Enter, watching the first paint closely.
9. Note whether the sidebar, the room heading and the transcript are all there in the FIRST frame, or whether they fill in a moment later.
10. Note the budget figure shown on the page, if one is visible.
11. In the same private window, navigate directly to `http://localhost:5100/settings/appearance` and watch the first paint.
12. Navigate directly to `http://localhost:5100/library` and watch the first paint.

**Pass if — all of these**

- Step 3 printed `1` - the prerendered HTML contains exactly one `<h1>`.
- Step 4 printed the room name.
- Step 5 printed `True` - the transcript is in the raw HTML, with no JavaScript involved at all.
- Step 6 printed `False`.
- In the private window, the sidebar, heading and transcript are complete in the first painted frame - no flash of empty layout.
- The Appearance tab and the Library page are likewise complete on first paint.
- Interactivity (clicking **New room**, ticking a checkbox) starts working a moment later, once the websocket connects.

**Fail if — any of these**

- A blank main column on first paint that fills in only after the circuit connects -> the page lost its prerender; every deep link now shows an empty frame first. Defect.
- Content appears, disappears, then reappears -> a prerender / interactive state mismatch. Defect.
- Step 5 printed `False` while the browser shows the transcript -> the messages are only rendered after the circuit attaches; same defect, caught by the curl half.
- The budget figure differs between the cold load and the already-open tab -> a fresh circuit is not reading the spent budget from storage. Defect.

**Inconclusive if**

If your machine is fast enough that you cannot judge the first paint by eye, trust the `curl.exe` results in steps 3 to 6 - they involve no JavaScript at all and are decisive. If the private window shows a login or extension interstitial, disable extensions for incognito and retry.

> [!NOTE]
> There is no authentication anywhere in this app by design at this stage, so a private window reaches every route exactly as the normal one does. That is expected, not a security finding for this area.

### SHELLNAV-25 — Two browser tabs on the same install stay in step through the shell

**Free** · about 8 min

*Proves the shared server-side state reaches every circuit. Before the MudBlazor migration a theme change needed a reload to reach a second tab's colours; now `AppearanceStore.AppearanceChanged` reaches every circuit and MudThemeProvider repaints immediately, so this test proves BOTH tabs update live — the detailed version of that proof lives in `appearance-theme.md`'s APPEARANCETHEME-20, and this test keeps only the shell-level room-list-plus-appearance combination.*

**Before you start**

- Two browser tabs against the same `http://localhost:5100`.
- `P-LAUNCH-UAT` has been run, so `Ada` and `Bo` are listed in the New room panel.

**Steps**

1. Open TAB A on a room page and TAB B on `http://localhost:5100/library`.
2. In TAB A, click **New room**, tick `Ada` and `Bo`, and click **Open room**.
3. Without touching TAB B, switch to it and read its sidebar.
4. Switch back to TAB A and navigate to `http://localhost:5100/settings/appearance`.
5. In the theme list, under the **Dark** heading, select `Huddle Dark` (there is no mode select since ADR-0017; the theme list is the only control, `Appearance.razor:66-80`).
6. Confirm TAB A repaints dark IMMEDIATELY, with no page load.
7. Switch to TAB B WITHOUT reloading or navigating it and note whether it is ALSO now dark.
8. In TAB A, select `Huddle Light` (the default, under the **Light** heading) again.

**Pass if — all of these**

- TAB B's sidebar gained the `Ada, Bo` room with no refresh and no navigation.
- TAB A repaints dark immediately, with no page load.
- TAB B ALSO repaints dark within about half a second, with no refresh and no navigation — this is the behaviour change from before the migration; a second tab is no longer stuck on the old appearance until its next load.

**Fail if — any of these**

- TAB B never picks up the new room, even after a navigation -> that circuit's rooms-changed subscription is broken. Defect.
- TAB B does NOT update live and needs a reload or navigation to go dark -> `MainLayout`'s subscription to `AppearanceStore.AppearanceChanged` is not reaching every circuit; see `appearance-theme.md`'s APPEARANCETHEME-20 for the isolated version of this check.
- TAB A does not change appearance at all -> see `appearance-theme.md`'s APPEARANCETHEME-04 first; this is the same defect.

**Inconclusive if**

If TAB B was on a route with no visible theme difference, you cannot judge the theme half - put TAB B on a room page with messages, where the surfaces are large, and repeat steps 4 to 7.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: the theme choice is per INSTALLATION, not per browser - it lives in `{DataDir}/appearance.json`. A second browser, a private window and a phone on the same install all see the same theme. What changed in this migration is that a second OPEN TAB on the same install no longer needs its own reload to see it — see `appearance-theme.md` for the full story.

### SHELLNAV-26 — The reconnect modal shows exactly one state paragraph at a time when the server goes away

**Free** · about 8 min

*Proves the scoped stylesheet for the modal reached the browser - an unstyled modal shows all five mutually exclusive paragraphs at once (`ReconnectModal.razor:9-26`).*

**Before you start**

- The circuit is connected.
- Run this LATE - it stops the app, and every later test needs it restarted.

**Steps**

1. In the browser, open a room page and wait for the `_blazor` websocket to appear in DevTools Network (filter WS).
2. In `T-B` run: `$h = curl.exe -s http://localhost:5100/settings | Out-String; [regex]::Matches($h, '[^"]*ReconnectModal[^"]*\.js') | ForEach-Object { $_.Value } | Select-Object -Unique` (`/settings`, because `/` answers with a bodyless redirect, as in SHELLNAV-01). Record the fingerprinted path from the module `<script>` tag - the one that carries a hash, e.g. `Components/Layout/ReconnectModal.abdmv1u4y3.razor.js`. The fingerprint sits BEFORE `.razor`, not after it.
3. Run: `curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5100/<paste that path>"`
4. Switch to `T-A` and press Ctrl+C to stop the app.
5. Watch the browser for up to ten seconds.
6. Read the modal that appears and write down every sentence you can see AT ONCE.
7. In the DevTools Console, type `[...document.querySelectorAll('#components-reconnect-modal p')].filter(p => getComputedStyle(p).display !== 'none').map(p => p.textContent.trim())` and press Enter. Record the array.
8. Wait about thirty seconds while the modal cycles through its states, re-running the step 7 command each time the wording changes.
9. When the wording settles on the final state, read the button label.
10. Restore the app: in `T-A` run the `dotnet run` command of `P-LAUNCH-UAT` again (not its seeder line, which wipes the data folder).
11. Once it is listening, click the button in the modal or reload the browser.

**Pass if — all of these**

- Step 3 printed `200`.
- A modal dialog appears within a few seconds of stopping the app - a contained, centred box, not a full-width block of text.
- Step 7's array has length 1 every time you run it.
- The wording progresses: first `Rejoining the server...`, then `Rejoin failed... trying again in N seconds.`, and finally `Failed to rejoin.` followed by `Please retry or reload the page.` (a `<br />` separates them, so `textContent` in step 7 reads `Failed to rejoin.Please retry or reload the page.` with no space, `ReconnectModal.razor:16`).
- The final state offers a button reading `Retry`.
- After restarting the app, the page recovers.

**Fail if — any of these**

- Step 7's array has length 5 (there are five paragraphs), showing `Rejoining the server...`, `Rejoin failed...`, `Failed to rejoin...`, `The session has been paused by the server.` and `Failed to resume the session.` all at once, plus both the `Retry` and `Resume` buttons -> the scoped-CSS bundle is not loading. This is the exact same defect as SHELLNAV-03, seen from the other side: none of the modal's scoped rules reached the browser. Go to SHELLNAV-01.
- No modal appears at all when the server stops -> either the module `<script>` tag is missing or the fingerprinted `ReconnectModal.<hash>.razor.js` 404s; step 3 will say which.
- The modal appears unstyled and full-width -> same scoped-CSS failure as the six-paragraph case.

**Inconclusive if**

If the browser simply shows a connection-refused error page instead of the modal, the page did a full document reload at the wrong moment - reload it while the app is running, wait for the websocket, and try again WITHOUT navigating in between. If the modal flashes past too fast to read, run the step 7 console command repeatedly rather than trying to read by eye.

> [!NOTE]
> Do NOT confuse this with the `#blazor-error-ui` yellow band. A stopped server produces this modal; a circuit-level exception produces the band (SHELLNAV-29). They are different mechanisms with different stylesheets.

### SHELLNAV-27 — With no rooms, the empty state renders TWICE and there is no heading at all

**Free** · about 12 min

*Proves the no-rooms shell does not redirect, does not throw, and says the same thing in both columns.*

**Before you start**

- Requires stopping the app and removing runtime state - run this near the end.
- No other test in progress.

**Steps**

1. In `T-A` press Ctrl+C to stop the app.
2. In `T-B` run: `Remove-Item -Recurse -Force C:\seeds\huddle\empty -ErrorAction SilentlyContinue` so the empty data folder starts from nothing. The `P-LAUNCH-UAT` data under `C:\seeds\huddle\uat` is not touched.
3. In `T-A` run: `$env:Team__Acp__Enabled = 'false'`
4. In `T-A` run: `Get-ChildItem Env:\Team__*` and confirm the only variable printed is `Team__Acp__Enabled`, so this is a clean lane with no seeded Agents.
5. In `T-A` run: `dotnet run --project src/Huddle.App --urls http://localhost:5100 --Team:DataDir=C:\seeds\huddle\empty\data` and wait for `Now listening on: http://localhost:5100`.
6. In `T-B` run: `curl.exe -s -D - -o NUL http://localhost:5100/` and read the status line.
7. In `T-B` run: `$h = curl.exe -s http://localhost:5100/ | Out-String; ([regex]::Matches($h, 'No rooms yet')).Count`
8. Run: `([regex]::Matches($h, '<h1[\s>]')).Count`
9. In the browser, navigate to `http://localhost:5100/` and read the sidebar.
10. Read the main column.
11. In the DevTools Console, type `document.activeElement.tagName` and press Enter.

**Pass if — all of these**

- Step 6's status line is `HTTP/1.1 200 OK` - there is NO redirect.
- Step 7 printed `2` - the same sentence appears twice, once from the sidebar's room list and once from the main column.
- Step 8 printed `0` - there is no heading anywhere on the page.
- The sidebar's room area shows the title `No rooms yet` with the line `Start an agent to create one.` under it.
- The main column shows the identical sentence.
- Step 11 printed `BODY` - with no `<h1>`, focus has nowhere to go.

**Fail if — any of these**

- Only one of the two sentences appears -> one of the two components lost its empty state; the user is left with a blank half-page. Defect.
- A 302 to `/rooms/` with an empty id -> the redirect ran even with no rooms to redirect to. Defect.
- A 500 or an exception page -> the page tried to load a null room. Defect, capture the stack from `T-A`.
- A completely blank main column with no message -> defect.

**Inconclusive if**

If step 6 still returns a redirect, the app did not start on the empty data folder - confirm `C:\seeds\huddle\empty` was removed in step 2 and that step 5 carried `--Team:DataDir=C:\seeds\huddle\empty\data`. If `Remove-Item` fails because the app still holds `team.db` open, wait for the process to exit fully and retry.

> [!NOTE]
> The step-11 `BODY` result is the same accessibility observation as SHELLNAV-15's last step - report it once, not twice. This test leaves the app in the empty state, which SHELLNAV-28 needs next. Restore afterwards with the cleanup in SHELLNAV-28.

### SHELLNAV-28 — The New room panel with no agents shows its own guidance instead of an empty list

**Free** · about 6 min

*Proves the panel explains what to do rather than presenting an empty box with an unusable button.*

**Before you start**

- SHELLNAV-27's setup is still in force: clean lane, app running on the empty data folder `C:\seeds\huddle\empty\data`.
- No `tools/echo-bot.ps1` connected - stop TERMINAL C's bot if it is running.

**Steps**

1. In the browser, navigate to `http://localhost:5100/`.
2. Click **New room** in the sidebar.
3. Read the panel contents.
4. In the DevTools Console, type `[...document.querySelectorAll('.new-chat button')].find(b => b.textContent.trim() === 'Open room')` and press Enter.
5. In the Console, type `document.querySelector('.new-chat .new-chat-agents')` and press Enter.
6. In the Console, type `[...document.querySelectorAll('.new-chat .empty-state code')].map(c => c.textContent)` and press Enter.
7. CLEANUP: in `T-A` press Ctrl+C.
8. In `T-B` run: `Remove-Item -Recurse -Force C:\seeds\huddle\empty`.
9. In `T-A` relaunch with the `P-LAUNCH-UAT` run line (the `dotnet run` block in `common.md`), so the original rooms return.
10. In the browser, reload and confirm the sidebar shows the original rooms again.

**Pass if — all of these**

- The panel shows exactly one empty state: the title `No agents have connected yet` with the line `Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.` under it.
- Each file name in that sentence is rendered in a monospace face.
- Step 4 printed `undefined` (`find` returns `undefined` when nothing matches) - the **Open room** button is not rendered at all in this state.
- Step 5 printed `null` - there is no agent list.
- Step 6 printed `['tools/echo-bot.ps1', 'appsettings.json']`, each a real file.
- After cleanup, the original rooms are back in the sidebar.

**Fail if — any of these**

- An empty panel with no explanation -> the user is given no way to learn what to do next. Defect.
- A **Open room** button is rendered (and clickable) with nothing to select -> a dead control. Defect.
- The guidance names a script or file that does not exist in the repository -> stale wording; check `tools/` for the real name.

**Inconclusive if**

If any agent is still listed, something is connected - stop any `echo-bot.ps1` in TERMINAL C, confirm `Get-ChildItem Env:\Team__*` in the terminal that launched the app shows only `Team__Acp__Enabled`, and relaunch. If the cleanup in steps 7 to 9 fails because a file is locked, wait for the dotnet process to exit fully and retry; do not force-delete while the app is running.

> [!NOTE]
> Run this immediately after SHELLNAV-27 - it reuses the same expensive setup. The cleanup steps are part of the test and must not be skipped, or every subsequent run starts from an empty install.

### SHELLNAV-29 — Record whether the yellow error band ever appears during a genuine circuit fault

**Free** · about 3 min

*Captures the band's real-world appearance opportunistically; there is no reliable way to provoke a circuit fault from a browser.*

**Before you start**

- Run this LAST, as a write-up of what you saw during every other test - not as a fresh exercise.

**Steps**

1. Review your notes from every test above and identify any moment when a yellow band appeared fixed to the BOTTOM of the viewport reading `An unhandled error has occurred.` with a `Reload` link and a 🗙.
2. For each such moment, check whether `T-A` logged an unhandled exception at the same time. Write down the exception.
3. If the band appeared at least once: click `Reload` and note where it takes you.
4. If the band appeared at least once: reproduce the moment if you can, click the 🗙 instead, and note whether the band dismisses.
5. If the band appeared while the Huddle Dark theme was selected, note whether it was still readable - pale yellow ground with dark text.

**Pass if — all of these**

- If the band appeared: it was fixed to the bottom of the viewport on a pale yellow ground, readable in both themes; `Reload` returned to the app root; 🗙 dismissed it; and `T-A` carried a matching unhandled exception at that moment.
- If the band never appeared during any test: that is the expected outcome and the result is INCONCLUSIVE, not a pass - record it as such.

**Fail if — any of these**

- The band appeared on a page where `T-A` logged NO exception -> that is not a circuit fault, that is the scoped-CSS 404 from SHELLNAV-03. File it there, not here.
- `Reload` does nothing, or 🗙 leaves the band in place -> the controls are dead when the user most needs them. Defect.
- The band rendered dark-on-dark or otherwise unreadable -> defect, and note the theme that was active.

**Inconclusive if**

The expected result. A circuit-level exception cannot be provoked deterministically from a browser, and this area has no supported way to inject one - do NOT invent a fake failure (do not kill the process, which produces the reconnect modal instead, and do not edit source to throw). If the band never showed, write 'INCONCLUSIVE - no circuit fault occurred during this session' and move on. That is a complete and correct result for this test.

> [!NOTE]
> The band's `lightyellow` background and `color-scheme: light only` are a WRITTEN exemption from tokenisation - this is the one moment a theme cannot be trusted, so the band deliberately stays light even under Dark. Do not file those two literals as token violations. Do not confuse this band with the reconnect modal from SHELLNAV-26: a stopped server produces the modal; only an exception produces this band.

---

### SHELLNAV-30 — The drawer's icons, and Settings pinned to the bottom

**Free** · about 4 min

*The pinning is CSS that no automated test can see. `ThemeSourceTests` reads stylesheets as source text and nothing in the suite renders a browser, so whether `margin-top: auto` actually pushes Settings down is decidable here and nowhere else.*

**Before you start**

- `P-LAUNCH-FREE`. Have at least two Rooms in the sidebar.

**Steps**

1. Look at the drawer. Note where **Settings** sits relative to the **Library** link and **Archived** and relative to the bottom of the viewport.
2. Note whether **Settings** shows any text, and hover it. Note what the tooltip says.
3. In DevTools, run `document.querySelector('[aria-label="Settings"]').getAttribute('href')`. Note the value.
4. Note the order of the buttons in the Activity bar, top to bottom.
5. Note whether **Archived** shows any text, and hover it. Note what the tooltip says.
6. Make the browser window SHORT (about 400px tall) so the drawer's content overflows, and scroll the drawer. Note where Settings is now.
7. Repeat steps 1 and 5 with the Huddle Dark theme selected (`Settings -> Appearance`, then **Huddle Dark** in the theme list).

**Pass if — all of these**

- **Settings** sits at the BOTTOM of the drawer, separated from the **Library** link by empty space rather than immediately below it.
- **Settings** shows no text, hovering it reveals a tooltip reading `Settings`, and step 3 returns `/settings`.
- The Activity bar lists Rooms, Teams, Tasks, Library, Scheduled, then Archived and Settings at the bottom (Feed and the Team shortcut tiles sit above them); there is no Agents button.
- **Archived** shows no text and hovering it reveals a tooltip reading `Archived`.
- The Settings and Archived icons are legible under Huddle Dark as well as light.

**Fail if — any of these**

- **Settings** sits immediately under the last button above it with no gap -> `margin-top: auto` is not applying. The likely cause is that the rule is in a SCOPED stylesheet: Blazor stamps its `[b-xxxxx]` scope only onto HTML elements written in the component's own markup, and `MainLayout.razor` has exactly one, `<div id="blazor-error-ui">`, which is not an ancestor of the drawer. The rules belong in `app.css`. Defect.
- Step 3 returns `null` or anything other than `/settings` -> the link lost its href or its accessible name. Defect, and note that `SettingsPageTests` asserts the href.
- Hovering **Settings** reveals no tooltip -> the `MudTooltip` is not wired. Defect.
- In the short window, Settings overlaps the room list or is unreachable by scrolling -> the flex column is fighting the drawer's own scrolling. Defect.

**Inconclusive if**

The drawer content is shorter than the viewport in step 6 no matter how small you make the window - some browsers clamp minimum height. Record what you could see for steps 1-5 and mark step 6 INCONCLUSIVE.

> [!NOTE]
> Settings and Archived are the only icon-only drawer entries (`MainLayout.razor:27-28`, `ArchivedLink.razor`). Rooms in the list have no leading icon (`RoomList.razor:53`).

---

### SHELLNAV-31 — One click on a left-menu item opens a preview tab, and a click on a different item replaces it

**Free** · about 5 min

*Tabs ([spec](../../Huddle.Tabs-Specifications.md) §3): browsing from the menu must not pile up tabs. The preview title (the greyer secondary text colour; a kept tab's title is the body colour, and weight does not mark it) and the replacement are rendering and click behaviour that only a browser shows.*

**Before you start**

- `P-LAUNCH-UAT` (Ada and Bo online, each with a Room). Open http://localhost:5100/ in a fresh browser tab, so no tab is open yet.

**Steps**

1. Click the **Library** button in the Activity bar once. Look at the strip above the page: note how many tabs there are, the title of the one that opened, and whether the title is in the greyer secondary text colour (a kept tab's title is the body colour; no tab is italic).
2. Click Ada's Room in the sidebar once. Note the tab count, the tab titles, and which has the greyer preview title.
3. Click **Library** once more, then Bo's Room once. Note the tab count each time.

**Pass if — all of these**

- Step 1 shows exactly one tab, `Library`, with a greyer (secondary-colour) title and no italics, and the page shows the Library.
- Step 2 shows exactly one tab: Ada's Room, with the greyer preview title, in place of `Library`.
- Step 3 ends with exactly one tab, Bo's Room, with the greyer preview title. The strip never holds more than one tab.

**Fail if — any of these**

- Each click adds another tab -> the preview tab is not being replaced.
- The tab title is upright on a single click -> a preview tab is being made permanent too early.
- The page does not change to match the clicked item.

---

### SHELLNAV-32 — Double-clicking a menu item keeps its tab, and the next click opens beside it

**Free** · about 5 min

*Tabs spec §3: a double-click on the menu item or on the tab itself makes a tab permanent. A permanent tab is not replaced by the next single click.*

**Before you start**

- `P-LAUNCH-UAT`. Open http://localhost:5100/ in a fresh browser tab so no tab is open.

**Steps**

1. Double-click the **Library** button in the Activity bar. Note the tab count and whether the title is in the greyer secondary text colour (a kept tab's title is the body colour; no tab is italic).
2. Click Ada's Room in the sidebar once. Note the tab count and which tab has the greyer preview title.
3. Double-click the greyer preview Ada tab in the strip. Note whether the title is still greyer.
4. Click Bo's Room once. Note the tab count and titles.

**Pass if — all of these**

- Step 1 shows one tab, `Library`, with a body-colour title.
- Step 2 shows two tabs: `Library` in the body colour and Ada's Room in the greyer preview colour, with Ada's active.
- Step 3 turns Ada's title from the greyer preview colour to the body colour.
- Step 4 shows three tabs, `Library`, Ada and Bo, with Bo's in the greyer preview colour. Nothing was replaced.

**Fail if — any of these**

- Step 2 replaces `Library` -> the double-click did not keep it.
- Double-clicking the tab itself does not turn the title from the greyer preview colour to the body colour of a permanent tab.

---

### SHELLNAV-33 — Clicking a tab switches the page and the address

**Free** · about 4 min

*Tabs spec §3: the active tab and the address bar always agree, so a tab can be bookmarked and the Back button keeps working.*

**Before you start**

- `P-LAUNCH-UAT`. Open http://localhost:5100/ and double-click **Library**, then double-click Ada's Room in the sidebar, so two permanent tabs are open.

**Steps**

1. Read the address bar. It names Ada's Room (`/rooms/<id>`).
2. Click the `Library` tab in the strip. Read the address bar and the page.
3. Click Ada's tab again. Read the address bar and the page.

**Pass if — all of these**

- Step 2 shows `/library` in the address bar, the Library page, and the `Library` tab marked active.
- Step 3 shows Ada's Room address and page again, with Ada's tab marked active.
- The page is not reloaded by a tab click (Ada's Room history is still visible at once).

**Fail if — any of these**

- The address and the page disagree with the active tab -> the location sync is stale.

---

### SHELLNAV-34 — Closing the active tab activates its neighbour, and closing the last one shows "No pages are open"

**Free** · about 5 min

*Tabs spec §3: closing is by the tab's **×** or a middle-click. The empty state is a real page, not a blank area.*

**Before you start**

- `P-LAUNCH-UAT`. Open http://localhost:5100/ and double-click `Library`, Ada's Room and Bo's Room in turn, so three permanent tabs are open with Bo's active.

**Steps**

1. Click the **×** on the active Bo tab. Note which tab becomes active and the address bar.
2. Middle-click the `Library` tab. Note which tabs are left.
3. Click the **×** on the last tab. Read the page.

**Pass if — all of these**

- Step 1 leaves two tabs, and the neighbour (Ada's Room) becomes active, with its page and address showing.
- Step 2 closes `Library`, leaving Ada's Room only.
- Step 3 leaves no tab, and the page reads `No pages are open`.

**Fail if — any of these**

- Closing leaves a blank page or an error, or activates a tab that is not the neighbour.
- A middle-click does not close the tab.
- A closed tab reappears after a click elsewhere.

---

### SHELLNAV-35 — Typing in a Room's message box keeps its tab, and the unsent text survives a tab switch and a page reload

**Free** · about 8 min

*Tabs spec §3 and §4: typing is a sign the Human is using the tab, so it stops being a preview; the half-written message belongs to the Room, not to the page, so it survives both a switch and a reload.*

**Before you start**

- `P-LAUNCH-UAT`. Open http://localhost:5100/ with no tab open.

**Steps**

1. Click Ada's Room once, so a preview tab (greyer title) opens. Click in the message box and type `draft for Ada` without sending.
2. Look at the Ada tab's title.
3. Click **Library** once. Note that a new tab opens beside Ada's instead of replacing it.
4. Click Ada's tab. Read the message box.
5. Press F5 to reload the page. Open Ada's Room again if the strip is empty and read the message box.

**Pass if — all of these**

- Step 2 shows Ada's title upright: typing made it permanent.
- Step 3 leaves two tabs, Ada and `Library`.
- Step 4 shows `draft for Ada` still in the box.
- Step 5 shows `draft for Ada` still in Ada's message box.

**Fail if — any of these**

- Ada's tab is replaced by the next click -> typing did not keep it.
- The draft text is gone after switching tabs or after the reload.
- The draft shows up in another Room's box -> drafts are not kept per Room.

**Note:** tabs are restored after a reload, so step 5 may start with the tabs from earlier in this test already open; that is expected and is not a failure.

---

### SHELLNAV-36 — A Room tab opened by pasting its URL gets the Room's name as its title within a moment

**Free** · about 4 min

*Tabs spec §3: a Room tab is titled from the Room, not from the address, and is renamed with the Room.*

**Before you start**

- `P-LAUNCH-UAT`. Click Ada's Room once and copy its URL from the address bar. Then close every tab.

**Steps**

1. Paste the copied Room URL into the address bar of a new browser tab and press Enter.
2. Read the tab strip straight away, then again after about two seconds.
3. Rename that Room from the sidebar and read the tab title again.

**Pass if — all of these**

- One tab is open for the Room.
- Within about two seconds its title is the Room's name, not a raw id or the word `Room`.
- After the rename the tab shows the new name.

**Fail if — any of these**

- The title stays as an id or a placeholder -> the title refresh is not running.
- No tab opens for a pasted URL.

---

### SHELLNAV-37 — Open tabs come back after a reload, with the tab for the current page active

**Free** · about 4 min

*Tabs spec §3 and §5: the tabs are stored in the browser and restored after the first render; the address in the bar decides which one is active.*

**Before you start**

- `P-LAUNCH-UAT`. Close every tab. Double-click `Library` and `Settings` in the menu so both are kept, then click Ada's Room once so it is a preview tab.

**Steps**

1. Read the tab strip: three tabs, `Library`, `Settings`, and Ada's Room in the greyer preview colour.
2. Navigate the browser to the `Library` address and press Enter, so the page reloads on a different tab from the one active before.
3. Read the strip again after about two seconds.
4. Stop the app and start it again with `P-LAUNCH-UAT`, open the app, and read the strip once more.

**Pass if — all of these**

- Step 3 shows the same three tabs in the same order, `Library` active, Ada's Room still in the greyer preview colour.
- Step 4 shows the same tabs again.

**Fail if — any of these**

- The strip starts with only the current page -> the stored tabs were not restored.
- The tab that was active before the reload is active instead of the one in the address bar.

---

### SHELLNAV-38 — Alt+W, Alt+Page Down and Alt+Page Up close and move between tabs

**Free** · about 3 min

*Tabs spec §3: the keyboard shortcuts use Alt because the browser keeps Ctrl+W and Ctrl+Tab.*

**Before you start**

- `P-LAUNCH-UAT`. Close every tab, then keep three: double-click `Library`, `Settings` and Ada's Room in the menu.

**Steps**

1. With Ada's Room active, press Alt+Page Down.
2. Press Alt+Page Up twice.
3. Press Alt+W.
4. Press Alt+W until no tab is left.

**Pass if — all of these**

- Step 1 moves to `Library` (the strip wraps round) and the page changes with it.
- Step 2 moves back through Ada's Room to `Settings`.
- Step 3 closes `Settings` and the page switches to its neighbour.
- Step 4 ends with "No pages are open".

**Fail if — any of these**

- The browser closes its own tab or switches its own tab -> the page did not take the shortcut.
- The page and the active tab disagree after a shortcut.

---

### SHELLNAV-39 — Right-clicking a tab offers Pin, Move left, Move right and Close, and Pin keeps the tab at the front as an icon

**Free** · about 3 min

*Tabs spec §9: the Tab menu.*

**Before you start**

- `P-LAUNCH-UAT`. Close every tab, then keep three: double-click `Library`, `Settings` and Ada's Room in the menu.

**Steps**

1. Right-click the `Settings` tab.
2. Choose **Pin tab**.
3. Right-click the pinned tab and read the first item, then close the menu with Escape.
4. Open `Tasks` with one click in the menu.
5. Right-click the pinned tab, choose **Unpin tab**.

**Pass if — all of these**

- Step 1 shows a menu at the pointer with, in order, Pin tab, Move left, Move right and Close, each with an icon.
- Step 2 moves `Settings` to the start of the strip as its gear icon alone, with no × and no title text; hovering shows "Settings".
- Step 3's first item reads **Unpin tab**.
- Step 4's new preview tab opens after the pinned tab and the others, never in front of it.
- Step 5 puts `Settings` back as a titled tab just after any pinned tabs, and it stays in the body colour (not the greyer preview colour).

**Fail if — any of these**

- The browser's own context menu appears instead of the tab menu.
- A pinned tab can be closed by middle-clicking it.
- A pinned tab ends up behind an unpinned one.

---

### SHELLNAV-40 — Move right opens a second pane beside the first, and both pages stay live

**Free** · about 4 min

*Tabs spec §9: groups and focus.*

**Before you start**

- `P-LAUNCH-UAT`. Close every tab, then keep two: double-click `Library` and `Settings`.

**Steps**

1. Right-click the `Settings` tab, choose **Move right**.
2. Look at the page and at the address bar.
3. Right-click the `Settings` tab again and read **Move right**.
4. Click anywhere in the `Library` pane.
5. Look at the address bar and at the `Settings` pane.
6. Right-click the only tab in the right pane, choose **Move left**.

**Pass if — all of these**

- Step 1 shows two panes side by side, `Library` on the left and `Settings` on the right, each with its own strip.
- Step 2's address bar reads `/settings`, and the right pane's active tab has the accent line while the left one's is dimmed.
- Step 3 shows **Move right** greyed out (no third pane is allowed).
- Step 4 makes the address bar read `/library` and moves the accent to the left pane. The `Settings` pane still shows its page, unchanged.
- Step 6 closes the right pane and leaves one pane holding both tabs.

**Fail if — any of these**

- Either page reloads or loses what was typed in it when the other pane is clicked.
- The address bar names the pane you did not click.
- A page opens in both panes at once.

---

### SHELLNAV-41 — Opening a page that is already open in the other pane takes you to it, and new pages open in the focused pane

**Free** · about 3 min

*Tabs spec §9: one Tab per key across groups.*

**Before you start**

- `P-LAUNCH-UAT`. Two panes as left by `SHELLNAV-40` step 1: `Library` on the left, `Settings` on the right, the right pane focused.

**Steps**

1. In the menu, click `Library`.
2. Click in the right pane, then in the menu click `Tasks` > `All Tasks`.

**Pass if — all of these**

- Step 1 focuses the left pane's existing `Library` tab; no second `Library` tab appears.
- Step 2 opens an `All Tasks` preview tab (greyer title) in the right pane, beside `Settings`, and not in the left pane.

**Fail if — any of these**

- A page is open twice.
- The new tab opens in the pane you did not click.

---

### SHELLNAV-42 — Both panes, pins and the focused pane come back after a reload

**Free** · about 2 min

*Tabs spec §9: persistence.*

**Before you start**

- `P-LAUNCH-UAT`. Two panes as in `SHELLNAV-40`, with `Settings` pinned and the left pane focused.

**Steps**

1. Reload the page.

**Pass if — all of these**

- Two panes come back, the same tabs in each, `Settings` still pinned, and the left pane focused with the address bar naming its tab.
- Each pane shows its own page.

**Fail if — any of these**

- Everything collapses into one pane.
- The pin is lost, or a pinned tab is no longer at the front.

---

### SHELLNAV-43 — Two Rooms side by side keep their own unsent text

**Free** · about 4 min

*Tabs spec §9: the audit of one-open-Room assumptions. Unproven live when this was written.*

**Before you start**

- `P-LAUNCH-UAT`. Keep Ada's Room and a second Room as tabs, and move the second to the right pane.

**Steps**

1. Type `left draft` into the left pane's message box without sending.
2. Click in the right pane and type `right draft` into its message box.
3. Click back in the left pane.
4. Send a message from the right pane.

**Pass if — all of these**

- Steps 1 to 3 leave `left draft` in the left box and `right draft` in the right box, each untouched by the other.
- Step 4 posts only in the right Room and clears only the right box.
- Each pane's Room name tab keeps showing its own Room.

**Fail if — any of these**

- Text appears in the wrong pane's box, or a send goes to the wrong Room.
- Streaming output or scrolling in one Room moves the other.

---

### SHELLNAV-44 — Every page in a tab has a breadcrumb bar, and an ancestor crumb navigates that tab in place

**Free** · about 5 min

*Tabs spec, Breadcrumb bar.*

**Before you start**

- `P-LAUNCH-UAT`. Open a Room from the `Rooms` group in the sidebar.

**Steps**

1. Look at the top of the Room's pane, between the tab strip and the Room.
2. Open **Library**, **Settings**, a Team and one of its Projects from the sidebar, and look at the top of each page.
3. On the Project page, click the Team's name in the bar.
4. Rename the Room, then switch to its tab.

**Pass if — all of these**

- Every page has a slim bar above it: *Rooms* alone on a Room (the Room's name is the page's own heading, not a crumb), *Teams › {Team} › {Project}* on a Project (the Project is the current crumb), *Teams* alone on a Team, a single crumb on Library and Settings.
- The page title text under the bar is not repeated on Settings and Library pages; a Room, Team or Project draws its own name as the page heading under the bar, and the bar holds only its ancestors.
- Step 3 changes the same tab to the Team page, leaves the tab count unchanged, and the Team's name in the bar is plain text afterwards.
- Step 4 shows the new name in the tab and in the Room's heading; the crumb still reads `Rooms`.

**Fail if — any of these**

- A page has no bar, or two (a second trail inside the page).
- Step 3 opens a new tab or leaves the bar showing the Project.
- The Room fills less of the pane than before, or the bar scrolls out of view with a long page.

---

### SHELLNAV-45 — With two panes each has its own bar, and a crumb acts on its own pane

**Free** · about 4 min

*Tabs spec §9 and the Breadcrumb bar.*

**Before you start**

- `P-LAUNCH-UAT`. Keep a Room tab and a Project tab, and move the Project tab to the right pane.

**Steps**

1. Look at the top of each pane.
2. Focus the right pane, then click the Team's name in the left pane's bar if it has one, otherwise click *Rooms* in the left Room's bar.
3. Look at both panes and the sidebar's highlight.

**Pass if — all of these**

- Each pane has its own bar showing its own page's trail.
- Step 2 changes only the left pane's tab, the left pane takes focus, and the right pane keeps its page.
- In a narrow pane a three-crumb trail shows the first and last crumb with an ellipsis between them.

**Fail if — any of these**

- Both panes show the same trail, or the click changes the right pane's tab.
- The browser URL does not name the left pane's page after the click.

---

Back to [the manual test script](../manual-tests.md).
