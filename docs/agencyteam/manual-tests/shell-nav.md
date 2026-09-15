# Application shell, navigation and layout

Prove the Agency.Huddle shell holds together: that the five routes (`/`, `/rooms/{RoomId}`, `/teammates`, `/settings`, `/settings/{Tab}`) resolve, that the fixed 240px `MudDrawer` with **New chat**, the live room list, **Teammates** and **Settings** is identical on every one of them, and above all that the shell's own assets actually load. The last part is the reason this area is tested by hand: `@Assets["..."]` hands back an unresolved key verbatim instead of throwing, so a stale stylesheet or script name renders as an ordinary-looking href that 404s with no build warning, no startup error, no log line and no visual error. That exact failure shipped live for a month after the 2026-09-12 namespace rename. Nothing in this area needs a Claude turn, so every test below is free.

**27 active, 2 retired** · 27 free, none paid · about 2.7 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

> [!NOTE]
> This area was updated on 2026-09-14 for the MudBlazor migration. The shell is now
> `MudLayout`/`MudDrawer`/`MudMainContent` with `MudNavMenu` (Stage 5), and the theming system the
> shell used to link as CSS files (`theme.css`, `themes/huddle-dark.css`, an inline per-token
> `<style>` override) is gone — see [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md) and
> `appearance-theme.md`, which now owns every theme-application test. SHELLNAV-21 and SHELLNAV-22
> tested that retired CSS cascade and are **retired in place**, not renumbered. The
> `@Assets[...]` asset-loading checks this file leads with (SHELLNAV-01, -02) are unaffected in
> kind, but the asset LIST changed: two MudBlazor static assets, `_content/MudBlazor/MudBlazor.min.css`
> and `.min.js`, were added, unfingerprinted, and a new fingerprinted stylesheet, `app-vars.css`,
> was added alongside `app.css`.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Browse to `http://localhost:5100` in Chrome or Edge. Open DevTools (F12) and keep the Console, Network and Elements tabs available.
2. In DevTools Network, tick **Disable cache** and leave the tab recording for the whole session. Several tests read its Status column.
3. Every `curl.exe` oracle in this area runs in `T-B`, so it never disturbs the app.
4. Confirm the baseline demo state: the sidebar lists exactly two Rooms, `echo` and `alpha`. A different set means an earlier session left state behind — that is fine for most tests here, but note it, because tests that name `echo` and `alpha` assume the default pair.
5. No test in this area needs a Model or an Effort. If a step below appears to ask you to pick one, that step is in the wrong area — stop and report it rather than picking.
6. Do NOT click **New teammate** on `/teammates` during this area. Opening that card probes the model catalog, which is another area's business and the one place on these pages that can reach an Adapter.

## Tests

### SHELLNAV-01 — Every stylesheet the shell links is fingerprinted (except the one that is deliberately static) and actually serves

**Free** · about 5 min

*Proves the three `@Assets[...]` stylesheet lookups resolved, catching the silent 404 that shipped for a month after the namespace rename — and confirms the one stylesheet that is NOT supposed to be fingerprinted, MudBlazor's own, is linked first and still serves.*

**Before you start**

- App running on http://localhost:5100.
- `T-B` open at the repository root.

**Steps**

1. In `T-B` run: `$html = curl.exe -s http://localhost:5100/ | Out-String`
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

If `curl.exe` is not found, use the browser instead: View Source (Ctrl+U) on http://localhost:5100/, read the four `<link rel="stylesheet">` hrefs, and click each one - a 404 page rather than CSS text is a fail. If the app is not listening (connection refused), that is not a result for this test - fix the launch and re-run.

> [!NOTE]
> `theme.css` is gone — the hand-built theming system it belonged to was replaced by `MudTheme`
> (see `appearance-theme.md`). `app-vars.css` is new: it declares `--font-mono`, the one token
> MudBlazor's own theme has no equivalent for. `_content/MudBlazor/MudBlazor.min.css` is a static
> asset served straight from the NuGet package's `staticwebassets`, not through `@Assets[...]`, so
> it is correctly and deliberately unfingerprinted — do not report its bare filename as the same
> defect class as the other three.

> [!NOTE]
> This is the single highest-value check in the area. Run it first and re-run it after any change to App.razor. The automated guard `tests/Huddle.Tests/Ui/AppStylesheetTests.cs` covers exactly this for stylesheets, so a failure here means that test also broke.

### SHELLNAV-02 — Every shell script is fingerprinted (except MudBlazor's own) and serves - the gap the automated guard does not cover

**Free** · about 5 min

*Proves `blazor.web.js` and `app.js` resolved, which no test in the repo checks because the automated regex matches only `<link rel="stylesheet">` — and that MudBlazor's own script, linked BEFORE both, is present and correctly unfingerprinted.*

**Before you start**

- App running.
- SHELLNAV-01 finished (it reuses the same page fetch technique).

**Steps**

1. In `T-B` run: `$html = curl.exe -s http://localhost:5100/ | Out-String`
2. Run: `[regex]::Matches($html, '<script[^>]*src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }`
3. Write down every src printed, in order.
4. For each src, run: `curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5100/<paste the src here>"` and record the status.
5. In the browser, press Ctrl+Shift+R. In DevTools Network, set the filter to **JS**.
6. Read the Status column for every row in the filtered JS list.
7. In the DevTools Console, type `typeof window.teamComposer` and press Enter.
8. Type `typeof window.teamScroll` and press Enter.
9. Type `typeof window.Mud` (or `typeof MudBlazor`, whichever the console autocompletes) and press Enter, to confirm MudBlazor's own script executed.

**Pass if — all of these**

- Step 2 printed, in this order: `_content/MudBlazor/MudBlazor.min.js` (no fingerprint — deliberate, see notes), then one ending `_framework/blazor.web.<hash>.js`, then one ending `app.<hash>.js`, and one ending `Components/Layout/ReconnectModal.razor.<hash>.js`.
- The LAST two of those carry a fingerprint segment; neither is the bare key `app.js` or `_framework/blazor.web.js`.
- Every status in step 4 is `200`.
- No row in the DevTools JS list shows 404.
- Step 7 and step 8 both printed `"object"`.
- Step 9 confirms MudBlazor's own script object exists (does not print `"undefined"`).

**Fail if — any of these**

- `app.js`'s src has no hash, or 404s, or step 7 printed `"undefined"` -> the `@Assets["app.js"]` key did not resolve. Nothing on screen says so; the symptom is that pressing Enter in the composer inserts a newline instead of sending, and the transcript never auto-scrolls. Confirm with SHELLNAV-19 and report as a defect plus a test-coverage gap.
- `blazor.web.js` 404s -> nothing on the page will be interactive and no `_blazor` websocket will open. Loud, but the same root cause.
- `MudBlazor.min.js` 404s, or step 9 shows nothing loaded -> every MudBlazor interactive feature (dialogs, popovers, ripples) will silently fail to initialise.
- `ReconnectModal.razor.js` never appears anywhere in the document -> the reconnect modal in SHELLNAV-26 will never appear at all.
- `MudBlazor.min.js`'s src carries a fingerprint -> it was routed through the fingerprinting pipeline by mistake; it is a static asset and should not need to be.

**Inconclusive if**

If the Console shows a Content-Security-Policy or extension error rather than a 404, disable browser extensions or use a private window and re-run. If `typeof` returns `"undefined"` but Network shows `app.js` at 200, the script loaded but threw - open the Console, find the exception, and report THAT rather than an asset failure.

> [!NOTE]
> The automated stylesheet guard deliberately matches only stylesheets, so this test is the only thing standing between a stale script key and production. Note it as a coverage gap when reporting, regardless of outcome.

### SHELLNAV-03 — The scoped-CSS bundle is applied: the error banner stays hidden below the fold

**Free** · about 5 min

*Proves `Huddle.App.styles.css` reached the browser, seen from the one place a missing scoped bundle is visible without reading source.*

**Before you start**

- App running.
- A room page open (the default landing page).

**Steps**

1. In the browser, click `echo` in the sidebar.
2. Click once on an empty part of the main column, then press the **End** key.
3. Scroll the window to its very bottom with the mouse wheel as well.
4. Look for yellow-backgrounded text reading `An unhandled error has occurred.` with a `Reload` link and a 🗙.
5. In the DevTools Console, type `getComputedStyle(document.getElementById('blazor-error-ui')).display` and press Enter.
6. Type `getComputedStyle(document.getElementById('blazor-error-ui')).position` and press Enter.
7. Type `document.getElementById('blazor-error-ui').getAttributeNames()` and press Enter.
8. Click **Settings** in the sidebar and repeat steps 2 to 6 on that route.

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

- At least one room exists (the default `echo` and `alpha` demo state).

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
- With the default demo pair present, the `<h1>` reads `echo`, and `echo` is the first entry in the sidebar list.

**Fail if — any of these**

- Status 200 with the address bar resting on `/` and an empty main column -> the redirect in `Chat.razor`'s `OnParametersSetAsync` did not run. Note that `BlazorDisableThrowNavigationException=true` is set for this project, so a broken redirect presents as a silently swallowed navigation rather than an exception - absence of an error in `T-A` does not mean absence of a bug.
- The `Location:` names a room that is NOT the first entry in the sidebar -> the `ORDER BY created, id` ordering behind the room list and the redirect have diverged.
- The browser bounces between URLs and never settles -> a redirect loop.
- Status 500 -> an unhandled exception; capture the stack from `T-A`.

**Inconclusive if**

If `sqlite3` is available you can confirm the target exactly with `sqlite3 src/Huddle.App/App_Data/team.db "SELECT id, name FROM rooms ORDER BY created, id LIMIT 1;"`. If `sqlite3` is NOT installed, do not treat that as a failure - compare the `<h1>` against the first sidebar entry instead, which reads from the same query. If the sidebar is empty, this test does not apply; run SHELLNAV-24 instead.

> [!NOTE]
> Rooms are ordered by creation time then id, never alphabetically and never by recent activity. `echo` landing first is correct because it is seeded before `alpha`.

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
6. Look for a message composer (a textarea with the placeholder `Message… (/invite @agent)`) at the bottom of the main column.
7. Check the address bar still reads `http://localhost:5100/rooms/does-not-exist`.

**Pass if — all of these**

- Step 1 printed `200`.
- The sidebar still lists `echo` and `alpha`.
- The main column shows the single sentence `No rooms yet. Start an agent to create one.`
- There is no `<h1>` on the page.
- There is no composer.
- The address bar is unchanged.

**Fail if — any of these**

- A 500 or an unhandled-exception page -> `Chat.razor` tried to dereference a null room. Defect.
- A redirect that changes the address bar -> the unknown id was silently swallowed into a real room; a bookmark to a deleted room would then land somewhere unexpected without saying so. Defect.
- The sidebar disappears -> the page escaped MainLayout. Defect.

**Inconclusive if**

If the sidebar is empty because no rooms exist, the contradiction cannot be observed - create a room first (SHELLNAV-18) or run this after the default demo state is restored.

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
- The browser shows its OWN error page - in Chrome, `This page isn't working` / `HTTP ERROR 404` - with no sidebar, no **Teammates** or **Settings** links, and no styling.
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
2. Run: `curl.exe -s -o NUL -w "%{http_code}" http://localhost:5100/TEAMMATES`
3. In the browser, navigate to `http://localhost:5100/Settings`.
4. Read the `<h1>`.
5. Navigate to `http://localhost:5100/TEAMMATES`.
6. Read the `<h1>`.

**Pass if — all of these**

- Both `curl.exe` calls printed `200`.
- `/Settings` shows `<h1>Settings</h1>` with the two-button tab rail.
- `/TEAMMATES` shows `<h1>Teammates</h1>`.

**Fail if — any of these**

- Either URL returns 404 -> routing lost its case-insensitive matching; a bookmark or a link with different casing now dead-ends on the bare browser 404 page, because there is no in-app Not Found to soften it. Defect.

**Inconclusive if**

None expected. If the app is not listening, fix the launch first.

> [!NOTE]
> Confirmed working today; treat any failure here as a routing-configuration regression in `Program.cs`.

### SHELLNAV-08 — The drawer is present, identical and fixed-width on every route

**Free** · about 8 min

*Proves no page declares its own layout and the shell's chrome never varies. The shell is now `MudLayout`/`MudDrawer`/`MudMainContent` (Stage 5 of the MudBlazor migration) — the old hand-rolled `.app-grid` CSS grid and its `.sidebar` / `.sidebar-link` classes are gone, but the drawer is still pinned to exactly the same 240px the old grid column was.*

**Before you start**

- At least one room exists.

**Steps**

1. In the browser, navigate to `http://localhost:5100/` and let it redirect to a room.
2. In the DevTools Console, type `getComputedStyle(document.querySelector('.mud-drawer')).width` and press Enter. Record the value.
3. Type `[...document.querySelectorAll('.mud-drawer > *')].map(e => e.className || e.tagName)` and press Enter. Record the order (the drawer's own direct children — `NewChat`, `RoomList` and `MudNavMenu`, each rendering as a `<div>`).
4. Type `[...document.querySelectorAll('.mud-drawer .mud-nav-link')].filter(a => a.getAttribute('href') === '/teammates' || a.getAttribute('href') === '/settings').map(a => a.textContent.trim() + ' -> ' + a.getAttribute('href'))` and press Enter. Record the result.
5. Type `getComputedStyle(document.querySelector('.main-column')).overflow` and press Enter.
6. Click **Teammates** in the sidebar and repeat steps 2 to 5.
7. Click **Settings** in the sidebar and repeat steps 2 to 5.
8. Navigate to `http://localhost:5100/settings/appearance` and repeat steps 2 to 5.
9. On the Settings route, scroll the drawer with the mouse wheel while hovering over it, then scroll while hovering over the main column.

**Pass if — all of these**

- Step 2 printed `240px` on all four routes.
- Step 3 shows exactly three direct children, in order, on all four routes.
- Step 4 printed exactly `Teammates -> /teammates` and `Settings -> /settings`, in that order, on all four routes — MudBlazor renders the link text as typed (title case), not upper case, since `MudNavLink` is not a button or tab label.
- Step 5 printed `"hidden"` on all four routes — `.main-column` is still the literal `Class` MudMainContent carries and its CSS rule is unchanged.
- The drawer scrolls independently; the browser window itself never grows a page-level horizontal or vertical scrollbar.

**Fail if — any of these**

- The drawer is missing on one route -> that page declared its own layout instead of inheriting MainLayout. Defect, and the route is unreachable from anywhere else.
- A link label reads anything other than `Teammates` or `Settings` -> the shell's navigation wording drifted.
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

- At least two rooms, so the active/inactive contrast is visible. The default `echo` and `alpha` pair is enough.

**Steps**

1. In the browser, click `echo` in the sidebar.
2. In the DevTools Console, type `document.querySelectorAll('.room-list a.active').length` and press Enter.
3. Look at the sidebar: confirm `echo` has a tinted background and heavier text, and `alpha` has neither.
4. Read the `<h1>` in the main column.
5. Click `alpha` in the sidebar.
6. Repeat steps 2, 3 and 4, expecting `alpha` to be the highlighted one and `echo` to be plain.
7. Read the `<h1>` in the main column again.

**Pass if — all of these**

- Step 2 printed `1` both times.
- Step 3 printed `echo | page` the first time and `alpha | page` the second time.
- Exactly one room link is tinted and bold at a time; the other is plain.
- The `<h1>` matches the highlighted room name each time.

**Fail if — any of these**

- Step 2 printed `0` -> no room is marked active; the `NavLink` match was lost and a reader cannot tell which room they are in.
- Step 2 printed `2` or more -> every room is highlighted; the `NavLink` Match mode regressed to prefix matching.
- The highlight stays on the previously-open room after clicking the other -> the active state is stale.

**Inconclusive if**

If only one room exists, the 'other room is plain' half cannot be judged - note it as partially inconclusive and run SHELLNAV-18 first to create a second room.

> [!NOTE]
> The highlight is no longer a hand-written `.room-list a.active` rule in `app.css` — the room list is now `MudNavMenu`/`MudNavLink` (Stage 5 of the MudBlazor migration), which carries its OWN active styling out of `MudBlazor.min.css`, keyed off the same literal `active` class `NavLink` has always applied. `.room-list a.active` as a selector still matches (`.room-list` still wraps the list, and `MudNavLink` still renders `<a class="mud-nav-link ... active">`), so the query in step 2 is unchanged. If the tint is invisible but the class is present, suspect a missing MudBlazor stylesheet - check SHELLNAV-01, not `app.css`. An assertion on `aria-current` was removed: Blazor's built-in NavLink has never emitted that attribute, so the assertion would fail against correct code. Judge the active-link state by the applied CSS class alone.

### SHELLNAV-10 — /teammates renders, and a bare page load spawns no node process

**Free** · about 5 min

*Proves the Teammates page loads inside the shell and, critically, that simply visiting it costs nothing.*

**Before you start**

- App running with the cost guard set (`Team__Acp__Enabled = 'false'`).
- `O-ADAPTERS` reads `0` (the `E-FREE` resting value).

**Steps**

1. In `T-B` run `O-ADAPTERS`. Record the number.
2. In the browser, click **Teammates** in the sidebar.
3. Read the `<h1>`.
4. Read the text of the button at the top right of the main column.
5. Read the first sentence of the intro paragraph below the heading.
6. Confirm the sidebar is still present with the room list intact.
7. In `T-B`, re-run the command from step 1 and compare the number.
8. Scroll `T-A` and look for any new line mentioning probing an adapter or starting a model catalog.
9. Press F5 to reload `/teammates`, then re-run step 1 one more time.

**Pass if — all of these**

- The `<h1>` reads exactly `Teammates`.
- The button reads exactly `New teammate`.
- The intro paragraph begins `A Persona is a Markdown file describing how one teammate should behave, plus the model it thinks with.`
- The sidebar is unchanged and still lists the rooms.
- The `node` process count in steps 7 and 9 is identical to step 1.
- `T-A` logged nothing about probing an adapter.

**Fail if — any of these**

- The `node` count rises on a bare page load -> the model catalog is being probed on render rather than when a card opens. This is a real regression AND it spends money whenever ACP is enabled. Report it as high severity.
- The `<h1>` or the button label differs from the exact text above -> wording drift.
- The page renders without the sidebar -> it escaped MainLayout; see SHELLNAV-08.

**Inconclusive if**

If a `node` process was already running before the app started (another tool on the machine), the count comparison is noisy - close that tool, restart the app with the cost guard, and re-run. If the Teammates list shows no personas, that is expected with ACP disabled and is not a result for this test.

> [!NOTE]
> Do NOT click **New teammate** here. That card opens the model picker, which is the one control on this page that probes an adapter, and it belongs to the Teammates area's tests, not this one. No model or effort selection is needed anywhere in this area.

### SHELLNAV-11 — /settings renders the two-button tab rail and defaults to Hooks

**Free** · about 4 min

*Proves a bare /settings resolves its optional {Tab} parameter to exactly one active tab.*

**Before you start**

- App running.
- `O-ADAPTERS` reads `0`.

**Steps**

1. In the browser, click **Settings** in the sidebar.
2. Read the address bar.
3. Read the `<h1>`.
4. Read the labels of the buttons in the tab rail ABOVE the content — `MudTabs` renders horizontally at the top by default; it is no longer a column down the left. Read left to right.
5. In the DevTools Console, type `[...document.querySelectorAll('.mud-tabs .mud-tab')].map(b => b.textContent.trim() + ' | active=' + b.classList.contains('mud-tab-active'))` and press Enter.
6. Read the pane below and confirm it describes hooks - it should contain the sentence beginning `A hook is one piece of wording this application sends to a model`.
7. In `T-B` run `O-ADAPTERS` and compare with the baseline.

**Pass if — all of these**

- The address bar reads exactly `http://localhost:5100/settings` with no segment appended on load.
- The `<h1>` reads exactly `Settings`.
- The tab rail holds exactly two buttons, reading `HOOKS` then `APPEARANCE` (MudBlazor renders tab labels upper case; step 5's `textContent` still reads `Hooks` / `Appearance`), left to right, above the pane.
- Step 5 printed `Hooks | active=true` and `Appearance | active=false`.
- The pane below is the Hooks pane.
- A reset control is present in the page header beside the heading.
- The `node` count is unchanged from the baseline.

**Fail if — any of these**

- Both tabs report `active=true`, or neither does -> the active-tab computation broke; a reader cannot tell which pane they are on.
- The Appearance pane shows on a bare `/settings` -> the fallback picked the wrong tab.
- The address bar gains `/hooks` on load -> the page is redirecting where it should not, which would put a spurious entry in the browser history on every visit.
- The `node` count rises -> Settings is spawning an adapter, which it must never do.
- The tab rail renders as a vertical column down the left rather than horizontally above the content -> `MudTabs.Position` was set away from its default; nothing in this area asks for that.

**Inconclusive if**

If the Hooks pane shows an error line above the tab rail, read it and note it, but it does not invalidate the tab-rail observations - report both. If `App_Data/hooks.json` is absent, that is normal and the pane says so on screen.

> [!NOTE]
> `hooks.json` does not exist on a fresh install and the app never creates it just to read from it. Absent means 'nothing is overridden'. Do not file 'the settings file is missing' as a defect.

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
4. Confirm the pane below now shows a `Theme` select, a second `Appearance`-labelled select, and a paragraph naming the selection-file path.
5. Click the **Hooks** tab button.
6. Read the address bar.
7. Press the browser Back button once and read the address bar.
8. Press Back again and read the address bar.
9. Press Back a third time and read the address bar.
10. Open a brand-new browser tab and paste `http://localhost:5100/settings/appearance`, then press Enter.
11. Read which tab is active in that new tab.
12. In `T-B` run: `$s = curl.exe -s http://localhost:5100/settings/appearance | Out-String; [regex]::Match($s, 'mud-tab-active"[^>]*>([A-Za-z]+)').Groups[1].Value`

**Pass if — all of these**

- After step 2 the address bar reads `http://localhost:5100/settings/appearance`.
- After step 5 the address bar reads `http://localhost:5100/settings/hooks`.
- Back walks `…/settings/hooks` -> `…/settings/appearance` -> `…/settings`, one step at a time.
- The fresh tab from step 10 opens directly with **Appearance** active and the `Theme` select showing.
- Step 12 printed `Appearance`.

**Fail if — any of these**

- The tab flips visually but the address bar does not change -> tab selection was reduced to local component state; a bookmark to `/settings/appearance` would then always land on Hooks, and the Back button would leave the page entirely instead of returning to the previous tab. Defect.
- Back does nothing, or jumps straight past all three entries in one press -> the navigation used replace rather than push. Defect.
- The fresh tab opens on Hooks despite the `/appearance` URL -> the route parameter is not being read on a cold load; step 12 will print `Hooks` and confirm it.

**Inconclusive if**

If the browser was already deep in history from earlier tests, the Back sequence is polluted - open a fresh tab, navigate to `/settings`, and repeat steps 2 to 9 there. If `curl.exe` is unavailable, judge from the browser alone and note that the source-level oracle was skipped.

> [!NOTE]
> The tab segment is always emitted lowercase (`/settings/hooks`, `/settings/appearance`) regardless of the button's displayed capitalisation.

### SHELLNAV-13 — An unrecognised or miscased {Tab} segment silently falls back to Hooks

**Free** · about 6 min

*Proves the deliberate fallback holds, and that the parse is still case-insensitive in BOTH directions.*

**Before you start**

- App running.

**Steps**

1. In `T-B` run each of these and record the status and the active tab it reports:
2. `$u='http://localhost:5100/settings/bogus'; curl.exe -s -o NUL -w "%{http_code} " $u; $s = curl.exe -s $u | Out-String; [regex]::Match($s,'mud-tab-active"[^>]*>([A-Za-z]+)').Groups[1].Value`
3. Repeat the previous command with `$u='http://localhost:5100/settings/HOOKS'`.
4. Repeat with `$u='http://localhost:5100/settings/Appearance'`.
5. Repeat with `$u='http://localhost:5100/settings/APPEARANCE'`.
6. Repeat with `$u='http://localhost:5100/settings/'`.
7. In the browser, navigate to `http://localhost:5100/settings/bogus` and confirm the page renders normally with no error banner.
8. In the browser, navigate to `http://localhost:5100/settings/APPEARANCE` and read which tab is active.

**Pass if — all of these**

- Every one of the five URLs returned `200`.
- `/settings/bogus` reports active tab `Hooks`.
- `/settings/HOOKS` reports `Hooks`.
- `/settings/Appearance` reports `Appearance`.
- `/settings/APPEARANCE` reports `Appearance`.
- `/settings/` reports `Hooks`.
- The browser shows a normal Settings page for `/settings/bogus` - no 404, no error line, no blank content pane.

**Fail if — any of these**

- Any URL returns 404 -> the fallback was lost; because there is no in-app Not Found page, the user lands on the bare browser error page from a typo in a tab name. Defect.
- `/settings/APPEARANCE` or `/settings/Appearance` falls back to Hooks -> the parse lost `ignoreCase: true`. Defect.
- An unknown tab renders a blank content pane rather than the Hooks pane -> the switch lost its default branch. Defect.
- An unhandled exception page -> defect, capture the stack from `T-A`.

**Inconclusive if**

If the regex oracle prints nothing at all, the markup shape changed - fall back to the browser and read which tab button is visibly highlighted, noting that the source-level check could not be applied.

> [!NOTE]
> This fallback exists specifically because `Routes.razor` has no `NotFound` branch, and `Settings.razor`'s own comment says so. A future third tab must inherit the same behaviour.

### SHELLNAV-14 — No route sets a browser tab title

**Free** · about 4 min

*Records, once for the whole shell, that nothing feeds <HeadOutlet /> - and catches the opposite regression, a title appearing on some routes only.*

**Before you start**

- App running.

**Steps**

1. In the browser, visit each of these in turn and after each one read the browser TAB label and run `document.querySelector('title')` in the DevTools Console: `http://localhost:5100/`, then a room via the sidebar, then `http://localhost:5100/teammates`, then `http://localhost:5100/settings`, then `http://localhost:5100/settings/appearance`.
2. Record the tab label and the console result for all five.
3. In `T-B` run: `foreach ($p in '/','/teammates','/settings','/settings/appearance') { $h = curl.exe -s "http://localhost:5100$p" | Out-String; "$p -> " + ([regex]::Matches($h,'<title')).Count }`

**Pass if — all of these**

- Every browser tab shows `localhost:5100/<path>` or just `localhost:5100` - never a human title such as `Settings` or `Huddle`.
- `document.querySelector('title')` returns `null` on all five routes.
- Step 3 printed `0` for every path.

**Fail if — any of these**

- A title appears on SOME routes but not others -> a `<PageTitle>` was added to one page only; the tab now lies about where the user is on every other page. Defect.
- A title from the PREVIOUS route persists after an enhanced navigation -> a stale `<PageTitle>` is not being cleared. Defect.

**Inconclusive if**

If the browser tab is too narrow to read the label, hover it for the tooltip or widen the window. If a browser extension injects a title, use a private window.

> [!NOTE]
> KNOWN LIMIT, NOT A BUG: no page anywhere declares a `<PageTitle>`, so every tab reads as its URL. `<HeadOutlet />` is wired up in App.razor but nothing feeds it. Report this ONCE as a single finding about the whole shell - not once per route.

### SHELLNAV-15 — Focus moves to the page's <h1> after every navigation

**Free** · about 8 min

*Proves FocusOnNavigate is working for keyboard and screen-reader users, and records the two states that have no h1 to move to.*

**Before you start**

- The interactive circuit is connected - load any page and wait two seconds after the Network tab goes quiet before starting.

**Steps**

1. In the browser, open `http://localhost:5100/` and let it redirect to a room.
2. In the DevTools Console, type `window.__probe = 1` and press Enter (this also sets up SHELLNAV-16).
3. Click **Teammates** in the sidebar.
4. In the Console, type `document.activeElement.tagName + ' | ' + document.activeElement.textContent.trim()` and press Enter. Record it.
5. In the Elements panel, select the `<h1>` and read its attributes.
6. Click **Settings** in the sidebar, then repeat step 4.
7. Click `echo` in the sidebar, then repeat step 4.
8. Click into the page (not the console), then press Tab once and run `document.activeElement.tagName + ' | ' + (document.activeElement.className || document.activeElement.textContent.trim())`. Record where Tab landed.
9. Navigate to `http://localhost:5100/rooms/does-not-exist` and repeat step 4.

**Pass if — all of these**

- After each of steps 3, 6 and 7, `document.activeElement.tagName` is `H1`, and its text is `Teammates`, `Settings`, and the room name respectively.
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
4. Click **New chat** in the sidebar so the panel opens. Leave it open.
5. Click **Teammates** in the sidebar.
6. Watch the page as it changes: note whether it white-flashes.
7. In the Console, type `window.__probe` and press Enter.
8. In the Network WS filter, check whether the `_blazor` websocket row is the SAME one from step 3 or a new one.
9. Look at the sidebar: is the **New chat** panel still open?
10. Click **Settings** in the sidebar and repeat steps 7 and 8.

**Pass if — all of these**

- Step 7 printed `42` both times - the JavaScript context survived, so no document reload happened.
- The `_blazor` websocket row is the same single row throughout; it is not torn down and re-established.
- No white flash between routes.
- The **New chat** panel is still open after navigating, and the sidebar's scroll position is unchanged.

**Fail if — any of these**

- `window.__probe` printed `undefined` -> the browser did a full document reload on the sidebar click. Enhanced navigation is broken; the usual cause is `blazor.web.js` failing to load - go to SHELLNAV-02 and check the JS filter for a 404 on `blazor.web.*.js`.
- A second `_blazor` websocket appears on every navigation -> the circuit is being rebuilt each time; expect the room list to lose live updates and the New chat panel to reset.
- The panel closes and the sidebar scrolls back to the top on every navigation -> the same full-reload symptom seen from the UI side.

**Inconclusive if**

If the Console was cleared by a DevTools setting rather than by a reload, `window.__probe` will still be readable - a cleared console alone proves nothing, so judge on the `__probe` value and the websocket row, not on the console log. If no `_blazor` websocket appears at all, the circuit never connected; fix that first (see SHELLNAV-02) - this test is inconclusive until it does.

> [!NOTE]
> Do not use the `blazor-enhanced-nav` response header as an oracle from `curl.exe` - it is not reliably present on a plain request. The `window.__probe` survival check is decisive and needs no header.

### SHELLNAV-17 — The New chat panel toggles, lists agents with status dots, and gates Start chat

**Free** · about 7 min

*Proves the sidebar's one interactive control opens, reflects real agent status, and refuses to start an empty chat. The disclosure is now `MudCollapse` (Stage 5 of the MudBlazor migration) rather than an always-in-the-DOM panel toggled by the `hidden` attribute, and its checkboxes are `MudCheckBox`, but the status dots are unchanged — `StatusDot.razor` still renders a plain `<span class="agent-dot ...">` with a `title` attribute.*

**Before you start**

- At least one Agent registered. With the demo agents on, `echo` and `alpha` are listed.

**Steps**

1. In the browser, open any room page.
2. Click **New chat** at the top of the sidebar.
3. Confirm a panel opens below the button.
4. In the DevTools Console, type `document.querySelector('.new-chat .mud-collapse-container').getBoundingClientRect().height > 0` and press Enter.
5. Read the panel: confirm one checkbox per agent, each with a small coloured dot before the name.
6. Hover the mouse over one dot and wait for the tooltip. Read it.
7. In the Console, type `[...document.querySelectorAll('.agent-dot')].map(d => d.title)` and press Enter. Record the array.
8. In the Console, type `[...document.querySelectorAll('.new-chat button')].find(b => b.textContent.trim() === 'Start chat').disabled` and press Enter.
9. Tick the checkbox beside `echo`.
10. Re-run the Console command from step 8.
11. Untick `echo`.
12. Re-run the Console command from step 8.
13. Click **New chat** again.
14. Re-run the Console command from step 4.

**Pass if — all of these**

- Step 4 printed `true` while the panel is open, and `false` after step 13 collapses it.
- Every agent has a checkbox and a coloured dot.
- The tooltip text is exactly one of `online`, `offline`, `starting` or `degraded` - lowercase, single word.
- Step 7's array contains only those four words.
- Step 8 printed `true` - the button reads **Start chat** (MudBlazor renders it upper case, `START CHAT`; `textContent` still reads `Start chat`) and is disabled while nothing is ticked.
- Step 10 printed `false` - ticking one agent enables it.
- Step 12 printed `true` again after unticking.

**Fail if — any of these**

- The panel does not collapse on the second click -> the toggle is one-way. Defect.
- **Start chat** is enabled with nothing ticked -> the gate is missing; clicking it would attempt a chat with no agents.
- Every dot shows the same colour and the same tooltip regardless of state -> status is being derived from pipe liveness alone rather than through the resolver that puts health FIRST. A deaf runner with an open pipe must read `degraded`, never `online`. Defect.
- A tooltip reads a capitalised or different word -> the tooltip text and the CSS class have diverged.

**Inconclusive if**

`MudCollapse` keeps its content in the DOM at all times and animates height, so do not judge open/closed from View Source or a `curl.exe` fetch — use the `.mud-collapse-container` height (or the eye) as in step 4. If no agents are listed at all, this test cannot run; that is SHELLNAV-28's territory - confirm `Team:DemoAgent:Enabled` is true in `appsettings.json` and restart.

> [!NOTE]
> Cross-check the dot colours against the health and presence lines in `T-A` for the same agent name; they must agree.

### SHELLNAV-18 — Starting a chat creates a room named after its agents and navigates straight to it

**Free** · about 6 min

*Proves the one creation path in the shell produces a correctly named room, updates the sidebar and moves the user there.*

**Before you start**

- Two or more Agents listed in the New chat panel (the default `echo` and `alpha`).
- SHELLNAV-17 passed, so the panel is known to work.

**Steps**

1. Note the current entries in the sidebar room list.
2. Click **New chat** in the sidebar.
3. Tick the checkbox beside `echo`.
4. Tick the checkbox beside `alpha`.
5. Click **Start chat**.
6. Read the address bar.
7. Read the `<h1>` in the main column.
8. Read the member line directly below the `<h1>`.
9. Read the sidebar room list.
10. Click **New chat** again and look at the checkboxes.
11. In `T-B` run: `Get-ChildItem src/Huddle.App/App_Data` and confirm `team.db` is present.

**Pass if — all of these**

- The panel closed by itself after **Start chat**.
- The address bar reads `http://localhost:5100/rooms/<32-hex-id>`, with a real id and never a trailing empty `/rooms/`.
- The `<h1>` reads exactly `echo, alpha` - the agent names comma-space joined in selection order.
- The member line below the heading lists `You` plus both agent names.
- The sidebar gained an entry reading `echo, alpha`, with no page refresh.
- In step 10 every checkbox is clear - the previous selection did not stick.

**Fail if — any of these**

- Navigating to `/rooms/` with no id -> the created room's id was lost. Defect.
- The new room does not appear in the sidebar until F5 -> the rooms-changed event did not reach the list; see SHELLNAV-20.
- The room is named something other than the joined agent names -> the naming rule changed.
- The checkboxes stay ticked after **Start chat** -> a second click silently makes a duplicate room.

**Inconclusive if**

If `sqlite3` is installed you can confirm with `sqlite3 src/Huddle.App/App_Data/team.db "SELECT id, name FROM rooms ORDER BY created DESC LIMIT 1;"`. If it is NOT installed, judge from the sidebar entry and the `<h1>` alone and say the DB check was skipped - do not call the test inconclusive on that account. If **Start chat** shows an error line instead, read it and report it verbatim.

> [!NOTE]
> Ticking a SINGLE agent reuses that agent's existing room rather than creating a new one - so if you tick only `echo` you land back on the existing `echo` room and the sidebar does not grow. That is correct, not a failure. Leaves a new room behind; it does no harm to later tests.

### SHELLNAV-19 — app.js is proven functionally: Enter sends and clears, and the transcript scrolls

**Free** · about 6 min

*Turns the silent app.js asset failure into something visible, because a stale script key produces no error anywhere.*

**Before you start**

- A room with a composer is open.
- The demo `echo` agent is running (default). This test is FREE - the demo agent is an in-process pipe client, not a Claude agent, so no model, no effort and no spend are involved.

**Steps**

1. In the browser, click `echo` in the sidebar.
2. Scroll to the bottom of the main column and find the textarea with the placeholder `Message… (/invite @agent)`.
3. Click into the textarea.
4. Type exactly: `hi @echo`
5. Press **Enter** (not Shift+Enter).
6. Observe the textarea immediately after.
7. Observe the transcript above it.
8. Wait up to five seconds and read whether a reply from `echo` appears.
9. Click into the textarea again, type `second line test`, press **Shift+Enter**, then type `still typing`.
10. Observe whether the textarea now holds two lines rather than having sent.

**Pass if — all of these**

- Pressing Enter sent the message: the textarea is EMPTY afterwards and no newline was inserted.
- The typed message `hi @echo` appears in the transcript.
- The transcript is scrolled to the bottom so the newest message is visible without scrolling by hand.
- A reply from `echo` appears in the transcript within a few seconds.
- Shift+Enter inserted a newline and did NOT send - the textarea holds both lines.

**Fail if — any of these**

- Enter inserts a newline instead of sending, and the textarea keeps its text -> `window.teamComposer` never attached. Almost always a stale `@Assets["app.js"]` key that 404ed silently. Go back to SHELLNAV-02, confirm the 404, and report BOTH the asset failure and the coverage gap.
- The message sends but the transcript does not auto-scroll -> `window.teamScroll` is missing; same root cause, milder symptom.
- Nothing at all responds to clicks -> `blazar.web.js` failed and the whole page is non-interactive; SHELLNAV-02 will name it.

**Inconclusive if**

If the message sends but `echo` never replies, the composer half still PASSED - record the reply timeout separately as a chat-area observation, not as an app.js failure. If the circuit has not connected yet, the textarea will not respond at all; wait for the `_blazor` websocket and retry once.

> [!NOTE]
> Leaves two messages in the `echo` room. That is harmless and actively useful - SHELLNAV-22 needs at least one message body on screen to see a font override take effect.

### SHELLNAV-20 — The room list updates live, with no refresh, when a room appears from outside the app

**Free** · about 10 min

*Proves the sidebar's live subscription works across circuits, and gives the leaked-subscription failure a chance to show itself.*

**Before you start**

- App running.
- `pwsh` available and a THIRD terminal free.
- This test is FREE - `tools/echo-bot.ps1` is a PowerShell pipe client, not a Claude agent.

**Steps**

1. In the browser, open a room page in TAB 1.
2. Open a SECOND browser tab (TAB 2) on `http://localhost:5100/teammates`.
3. Return to TAB 1 and note the exact sidebar room list.
4. Open a third PowerShell terminal at `E:\Repos\Huddle`. Call it TERMINAL C.
5. In TERMINAL C run: `pwsh tools/echo-bot.ps1 -Name mybot`
6. Read TERMINAL C's output and wait for a line containing `"type":"welcome"`.
7. Without touching the browser, watch TAB 1's sidebar for up to five seconds.
8. Switch to TAB 2 and watch its sidebar too, again without refreshing.
9. In TAB 1, click **New chat** and read the agent checkbox list.
10. Now provoke the leak case: in TAB 1, click **Teammates**, then **Settings**, then a room, then **Teammates** again - six navigations in all.
11. Read TAB 1's sidebar carefully for duplicate or stale entries.
12. Scroll `T-A` and look for repeated handler errors or exception stacks.
13. Close TAB 2 entirely, wait ten seconds, and scroll `T-A` again.

**Pass if — all of these**

- A new sidebar entry reading `mybot` appears in TAB 1 within about a second, with no page refresh and no navigation.
- The same entry appears in TAB 2 without a refresh.
- The **New chat** checkbox list gained `mybot` as well.
- After the six navigations the sidebar shows each room exactly once - no duplicates, no rooms that no longer exist.
- `T-A` shows the pipe connection for `mybot` and no repeated handler errors, before or after closing TAB 2.

**Fail if — any of these**

- `mybot` only appears after pressing F5 -> the rooms-changed subscription is broken; the sidebar is no longer live. Defect.
- Duplicate sidebar entries accumulate as you navigate -> a component is leaking its subscription because it did not unsubscribe on disposal. Defect.
- `T-A` fills with repeated handler errors after closing TAB 2 -> the same leak, seen from the server; a component that never dies is still being notified. Defect, and it grows worse the longer the app runs.

**Inconclusive if**

If TERMINAL C cannot connect to the pipe (`\\.\pipe\team`), the bot never registered and this test produced no result - check that the app in `T-A` is still running and that `Team:PipeName` is `team`. If `pwsh` is not installed, skip this test and say so explicitly rather than substituting a weaker check; there is no browser-only way to make a room appear from outside the app.

> [!NOTE]
> Leave TERMINAL C's bot running or press Ctrl+C in it when done - either is fine. The `mybot` room persists in `team.db` and will appear in later tests; that is expected.

### SHELLNAV-21 — RETIRED: choosing a theme layering a fourth stylesheet after the base one, and reloading the page

**Retired 2026-09-14.** This test proved the `<head>` layering order of a hand-built `themes/huddle-dark.css` stylesheet against the base `theme.css`, and that selecting a theme forced a full page reload. Neither exists any more: there is no `themes/` folder, and `appearance-theme.md`'s APPEARANCETHEME-04 now proves the opposite of the reload assertion — a Theme or dark-mode change applies immediately with NO page load, through `MudThemeProvider`. See [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md). No successor test lives here; `appearance-theme.md` owns every theme-application test now.

### SHELLNAV-22 — RETIRED: a hand-edited token override injected as an inline `<style>` after both stylesheet links

**Retired 2026-09-14.** Per-token overrides (`--font-chat` and the other 37 custom properties `wwwroot/theme.css` used to declare) no longer exist, and nothing in this application emits a Human-supplied value into a `<style>` element any more. See `appearance-theme.md`'s APPEARANCETHEME-10 through -13 (also retired) and [known-limits.md](../known-limits.md). No successor test lives here.

### SHELLNAV-23 — A bad appearance.json does not break the shell: no theme link, no blank page, only a log warning

**Free** · about 5 min

*The detailed validation behaviour for a bad theme id or dark-mode value — what is logged, what is (and, per known-limits.md, is no longer) shown on the Appearance tab — belongs to `appearance-theme.md`'s APPEARANCETHEME-14 now. This test keeps only the shell-level question: does a malformed selection file ever break the CHROME itself (a blank page, a missing stylesheet link, a crash)?*

**Before you start**

- Write access to `src/Huddle.App/App_Data`.
- The app running - no restart is needed, the file is watched.

**Steps**

1. Open `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` in a text editor (create it if it does not exist).
2. Replace its entire contents with exactly: `{"theme": "not-a-theme", "dark": "not-a-mode"}`
3. Save the file and wait five seconds.
4. In `T-B` run: `Get-Content src/Huddle.App/App_Data/appearance.json`. Confirm it is byte-for-byte what you typed.
5. In the browser, press F5 on any page.
6. Observe whether the page renders normally, following your operating system's light/dark setting, with the sidebar and all four stylesheets from SHELLNAV-01 still present.
7. Scroll `T-A` and find the warning lines logged when the file was read.
8. Re-run the command from step 4 and confirm the file is STILL unchanged.

**Pass if — all of these**

- The page renders normally with no blank or unstyled screen, and the shell (drawer, room list, nav links) is intact.
- `T-A` logged two warnings naming the file path and the bad values (see APPEARANCETHEME-14 for the exact wording).
- The on-disk `appearance.json` is byte-identical to what you typed, both before and after.

**Fail if — any of these**

- A blank or unstyled page, or the shell fails to render -> a bad value must never be able to break the chrome itself, only fail to apply.
- The app rewrote, reformatted or deleted your file -> it must leave a rejected entry exactly as it found it. Defect.
- No message anywhere, not even in `T-A` -> the human has no way to learn why their theme did nothing. Defect, and the worst kind: a silent one.

**Inconclusive if**

If the file watcher does not react within ten seconds, restart the app in `T-A` (remembering `$env:Team__Acp__Enabled = 'false'` first) and re-check - a watcher that needs a restart is itself worth noting, but judge the messages after the restart. If the JSON you pasted is malformed (a stray quote), the app may report a parse problem instead; retype it exactly and re-run.

> [!NOTE]
> CLEANUP: delete `src/Huddle.App/App_Data/appearance.json` when finished. Absent is the correct fresh-install state. For the on-tab reporting question (there is none any more — see known-limits.md) and the exact log wording, run APPEARANCETHEME-14 in `appearance-theme.md` instead of trying to reconstruct it here.

### SHELLNAV-24 — A deep link renders complete content on a cold first request, before any circuit attaches

**Free** · about 7 min

*Proves prerendering works, so a pasted or bookmarked URL is not a blank frame until the websocket connects.*

**Before you start**

- At least one room with messages - run SHELLNAV-19 first so the `echo` room has content.
- The room's 32-hex id copied from the address bar.

**Steps**

1. In the browser, open the `echo` room and copy its full URL from the address bar.
2. In `T-B` run: `$r = curl.exe -s "<paste the room URL>" | Out-String`
3. Run: `([regex]::Matches($r, '<h1>')).Count`
4. Run: `[regex]::Match($r, '<h1>([^<]*)</h1>').Groups[1].Value`
5. Run: `$r.Contains('hi @echo')` - this checks the prerendered HTML already carries the transcript.
6. Run: `$r.Contains('No rooms yet')`
7. Open a NEW private/incognito window.
8. Paste the room URL and press Enter, watching the first paint closely.
9. Note whether the sidebar, the room heading and the transcript are all there in the FIRST frame, or whether they fill in a moment later.
10. Note the budget figure shown on the page, if one is visible.
11. In the same private window, navigate directly to `http://localhost:5100/settings/appearance` and watch the first paint.
12. Navigate directly to `http://localhost:5100/teammates` and watch the first paint.

**Pass if — all of these**

- Step 3 printed `1` - the prerendered HTML contains exactly one `<h1>`.
- Step 4 printed the room name.
- Step 5 printed `True` - the transcript is in the raw HTML, with no JavaScript involved at all.
- Step 6 printed `False`.
- In the private window, the sidebar, heading and transcript are complete in the first painted frame - no flash of empty layout.
- The Appearance tab and the Teammates page are likewise complete on first paint.
- Interactivity (clicking **New chat**, ticking a checkbox) starts working a moment later, once the websocket connects.

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
- Two or more agents listed in the New chat panel.

**Steps**

1. Open TAB A on a room page and TAB B on `http://localhost:5100/teammates`.
2. In TAB A, click **New chat**, tick `echo` and `alpha`, and click **Start chat**.
3. Without touching TAB B, switch to it and read its sidebar.
4. Switch back to TAB A and navigate to `http://localhost:5100/settings/appearance`.
5. Select `Dark` in the `Appearance` select.
6. Confirm TAB A repaints dark IMMEDIATELY, with no page load.
7. Switch to TAB B WITHOUT reloading or navigating it and note whether it is ALSO now dark.
8. In TAB A, set the `Appearance` select back to `System`.

**Pass if — all of these**

- TAB B's sidebar gained the `echo, alpha` room with no refresh and no navigation.
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

*Proves the scoped stylesheet for the modal reached the browser - an unstyled modal shows all six mutually exclusive paragraphs at once.*

**Before you start**

- The circuit is connected.
- Run this LATE - it stops the app, and every later test needs it restarted.

**Steps**

1. In the browser, open a room page and wait for the `_blazor` websocket to appear in DevTools Network (filter WS).
2. In `T-B` run: `$h = curl.exe -s http://localhost:5100/ | Out-String; [regex]::Matches($h, 'ReconnectModal\.razor\.[^"]*\.js') | ForEach-Object { $_.Value }`. Record the fingerprinted path from the import map.
3. Run: `curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5100/<paste that path>"`
4. Switch to `T-A` and press Ctrl+C to stop the app.
5. Watch the browser for up to ten seconds.
6. Read the modal that appears and write down every sentence you can see AT ONCE.
7. In the DevTools Console, type `[...document.querySelectorAll('#components-reconnect-modal p')].filter(p => getComputedStyle(p).display !== 'none').map(p => p.textContent.trim())` and press Enter. Record the array.
8. Wait about thirty seconds while the modal cycles through its states, re-running the step 7 command each time the wording changes.
9. When the wording settles on the final state, read the button label.
10. Restore the app: in `T-A` run `$env:Team__Acp__Enabled = 'false'` then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
11. Once it is listening, click the button in the modal or reload the browser.

**Pass if — all of these**

- Step 3 printed `200`.
- A modal dialog appears within a few seconds of stopping the app - a contained, centred box, not a full-width block of text.
- Step 7's array has length 1 every time you run it.
- The wording progresses: first `Rejoining the server...`, then `Rejoin failed... trying again in N seconds.`, and finally `Failed to rejoin. Please retry or reload the page.`
- The final state offers a button reading `Retry`.
- After restarting the app, the page recovers.

**Fail if — any of these**

- Step 7's array has length 5 or 6, showing `Rejoining the server...`, `Rejoin failed...`, `Failed to rejoin...`, `The session has been paused by the server.` and `Failed to resume the session.` all at once, plus both the `Retry` and `Resume` buttons -> the scoped-CSS bundle is not loading. This is the exact same defect as SHELLNAV-03, seen from the other side: none of the modal's scoped rules reached the browser. Go to SHELLNAV-01.
- No modal appears at all when the server stops -> either the import map entry is missing or `ReconnectModal.razor.js` 404s; step 3 will say which.
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
2. In `T-B` run: `Rename-Item src/Huddle.App/App_Data App_Data_backup` so the existing state is preserved rather than destroyed.
3. In `T-A` run: `$env:Team__Acp__Enabled = 'false'`
4. In `T-A` run: `$env:Team__DemoAgent__Enabled = 'false'`
5. In `T-A` run: `dotnet run --project src/Huddle.App --urls http://localhost:5100` and wait for `Now listening on: http://localhost:5100`.
6. In `T-B` run: `curl.exe -s -D - -o NUL http://localhost:5100/` and read the status line.
7. In `T-B` run: `$h = curl.exe -s http://localhost:5100/ | Out-String; ([regex]::Matches($h, 'No rooms yet')).Count`
8. Run: `([regex]::Matches($h, '<h1>')).Count`
9. In the browser, navigate to `http://localhost:5100/` and read the sidebar.
10. Read the main column.
11. In the DevTools Console, type `document.activeElement.tagName` and press Enter.

**Pass if — all of these**

- Step 6's status line is `HTTP/1.1 200 OK` - there is NO redirect.
- Step 7 printed `2` - the same sentence appears twice, once from the sidebar's room list and once from the main column.
- Step 8 printed `0` - there is no heading anywhere on the page.
- The sidebar's room area shows `No rooms yet. Start an agent to create one.`
- The main column shows the identical sentence.
- Step 11 printed `BODY` - with no `<h1>`, focus has nowhere to go.

**Fail if — any of these**

- Only one of the two sentences appears -> one of the two components lost its empty state; the user is left with a blank half-page. Defect.
- A 302 to `/rooms/` with an empty id -> the redirect ran even with no rooms to redirect to. Defect.
- A 500 or an exception page -> the page tried to load a null room. Defect, capture the stack from `T-A`.
- A completely blank main column with no message -> defect.

**Inconclusive if**

If step 6 still returns a redirect, `team.db` was not actually removed - confirm `src/Huddle.App/App_Data` no longer exists (the rename in step 2 worked) and that the app recreated an EMPTY one on launch. If `Rename-Item` fails because the app still holds `team.db` open, wait for the process to exit fully and retry.

> [!NOTE]
> The step-11 `BODY` result is the same accessibility observation as SHELLNAV-15's last step - report it once, not twice. This test leaves the app in the empty state, which SHELLNAV-28 needs next. Restore afterwards with the cleanup in SHELLNAV-28.

### SHELLNAV-28 — The New chat panel with no agents shows its own guidance instead of an empty list

**Free** · about 6 min

*Proves the panel explains what to do rather than presenting an empty box with an unusable button.*

**Before you start**

- SHELLNAV-27's setup is still in force: demo agents disabled, App_Data removed, app running.
- No `tools/echo-bot.ps1` connected - stop TERMINAL C's bot if it is running.

**Steps**

1. In the browser, navigate to `http://localhost:5100/`.
2. Click **New chat** in the sidebar.
3. Read the panel contents.
4. In the DevTools Console, type `[...document.querySelectorAll('.new-chat button')].find(b => b.textContent.trim() === 'Start chat')` and press Enter.
5. In the Console, type `document.querySelector('.new-chat .new-chat-agents')` and press Enter.
6. In the Console, type `[...document.querySelectorAll('.new-chat .empty-state code')].map(c => c.textContent)` and press Enter.
7. CLEANUP: in `T-A` press Ctrl+C.
8. In `T-B` run: `Remove-Item -Recurse -Force src/Huddle.App/App_Data` then `Rename-Item src/Huddle.App/App_Data_backup App_Data`.
9. In `T-A` run: `$env:Team__Acp__Enabled = 'false'` then `Remove-Item Env:\Team__DemoAgent__Enabled` then `dotnet run --project src/Huddle.App --urls http://localhost:5100`.
10. In the browser, reload and confirm the sidebar shows the original rooms again.

**Pass if — all of these**

- The panel shows the single sentence `No agents have connected yet. Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.`
- `tools/echo-bot.ps1` and `appsettings.json` are rendered in a monospace face.
- Step 4 printed `null` - the **Start chat** button is not rendered at all in this state.
- Step 5 printed `null` - there is no agent list.
- Step 6 printed exactly `['tools/echo-bot.ps1', 'appsettings.json']`.
- After cleanup, the original rooms are back in the sidebar.

**Fail if — any of these**

- An empty panel with no explanation -> the user is given no way to learn what to do next. Defect.
- A **Start chat** button is rendered (and clickable) with nothing to select -> a dead control. Defect.
- The guidance names a script or file that does not exist in the repository -> stale wording; check `tools/` for the real name.

**Inconclusive if**

If any agent is still listed, something is connected - stop any `echo-bot.ps1` in TERMINAL C, confirm `$env:Team__DemoAgent__Enabled` is `false` in the terminal that launched the app, and relaunch. If the cleanup in steps 7 to 9 fails because a file is locked, wait for the dotnet process to exit fully and retry; do not force-delete while the app is running.

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
5. If the band appeared while the Dark theme was selected, note whether it was still readable - pale yellow ground with dark text.

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

Back to [the manual test script](../manual-tests.md).
