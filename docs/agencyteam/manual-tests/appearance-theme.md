# Appearance tab, Themes, Tokens and overrides

The Development profile sets `Team:Acp:Enabled: true`, so the global cost guard in section 0.2 is the only thing holding ACP off. Set it before you launch.

**25 tests** · 25 free, none paid · about 3.2 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Start from the virgin first-run state: if `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` exists, delete it (`P-RESET-SETTINGS`). An absent file is normal and is not a broken install.
2. Set the operating system to LIGHT mode — Windows Settings → Personalisation → Colours → 'Choose your default app mode' → Light. Every dark-mode test below is deliberately run against a light OS so that a Theme failing to apply is maximally visible.
3. Open Chrome or Edge at `http://localhost:5100`, then DevTools (F12). Confirm DevTools → three-dot menu → More tools → Rendering → 'Emulate prefers-color-scheme' reads **No emulation**. A left-over emulation setting silently invalidates every OS-following test in this area.
4. Go to `http://localhost:5100/settings/appearance` and read the path inside the `<code>` element in the second paragraph. It should read `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json`. THE ON-SCREEN PATH IS THE AUTHORITY — if it differs, edit the file the page names, not the one in these steps.
5. Have a plain text editor (Notepad, VS Code) ready to create and save that file. A save is picked up by a filesystem watcher about 0.5 seconds later.
6. Throughout this area, 'full page load' means F5 or Ctrl+Shift+R. Clicking a sidebar link is IN-APP navigation, does NOT count, and cannot change `<head>`.
7. No test in this area needs a Model, and none spends money.

## Tests

### APPEARANCETHEME-01 — The Appearance tab is on the Settings rail and is reachable by its own route

**Free** · about 3 min

*Proves the Appearance tab exists, navigates by URL so it can be bookmarked and linked, and that an unknown tab segment falls back to Hooks rather than throwing or rendering blank.*

**Before you start**

- The application is running at http://localhost:5100.

**Steps**

1. Go to http://localhost:5100/settings.
2. Read the page heading at the top left. It must read `Settings`.
3. Count the buttons in the vertical tab rail on the left of the settings content. There must be exactly two, reading `Hooks` and `Appearance`, in that order.
4. Note which tab button looks selected (it carries the active styling). Note the URL in the address bar.
5. Click the `Appearance` button.
6. Read the address bar.
7. Read the tab rail again and note which button now carries the active styling.
8. Go directly to http://localhost:5100/settings/appearance by typing it into the address bar and pressing Enter.
9. Go directly to http://localhost:5100/settings/nonsense by typing it into the address bar and pressing Enter.
10. Go directly to http://localhost:5100/settings by typing it into the address bar and pressing Enter.
11. Look at the `dotnet run` console for any exception logged during the last four navigations.

**Pass if — all of these**

- The heading reads `Settings` and the rail holds exactly the two buttons `Hooks` and `Appearance`.
- Landing on /settings with no segment shows the Hooks panel with `Hooks` marked active.
- Clicking `Appearance` changes the address bar to http://localhost:5100/settings/appearance and marks `Appearance` active.
- Typing /settings/appearance directly lands on the same Appearance panel with `Appearance` active.
- /settings/nonsense renders the Hooks panel - a normal, fully styled page, not a blank page and not an error.
- No exception appears in the `dotnet run` console.

**Fail if — any of these**

- The `Appearance` button is missing from the rail -> the tab was never wired into Settings.razor's rail.
- Clicking `Appearance` swaps the panel but leaves the URL at /settings -> the tab is flipping local state instead of navigating, so the tab cannot be bookmarked, linked or reloaded into.
- /settings/nonsense renders blank, 404s, or throws -> Routes.razor has no NotFound branch, so an unrecognised tab MUST resolve to Hooks; a blank page means the fallback was removed.
- /settings with no segment renders blank -> the same fallback is broken for the null case.

**Inconclusive if**

If the page will not load at all (connection refused, or an ASP.NET error page), this test is INCONCLUSIVE: the app is not running or failed to start. Go back to the console, read the startup error, restart with `dotnet run --project src/Huddle.App --urls http://localhost:5100`, and re-run. Do not record a result from a page that never rendered.

> [!NOTE]
> Cheapest test in the area and a prerequisite for every other test - if you cannot reach the Appearance tab, stop here.

### APPEARANCETHEME-02 — The Appearance tab shows its own prose and the real absolute override path, and none of the Hooks tab's prose

**Free** · about 4 min

*Proves the fix for a real shipped bug where both intro paragraphs sat above the tab rail and appeared on every tab, and proves the path the Human is told to edit is the real absolute one.*

**Before you start**

- The application is running.
- No appearance.json yet (the virgin state from setup) - not required, but it makes the 'file does not exist' sentence true as written.

**Steps**

1. Go to http://localhost:5100/settings/appearance.
2. Read the FIRST paragraph on the panel, above the Theme control. Compare it word for word with: `Pick a theme, or leave it on System to follow your device's own light or dark setting.`
3. Read the paragraph BELOW the Theme dropdown. Compare it word for word with: `Overrides are stored at <path>, keyed by token name (for example --font-chat or --surface-base) rather than a friendly name - hand-edit it for fast prototyping if you like. The file does not exist until you save a theme choice here, so it being absent is expected, not a bug.`
4. Look closely at the two token examples rendered in monospace in that paragraph. They must read exactly `--font-chat` and `--surface-base`.
5. Read the path rendered in monospace in that same paragraph. Write it down. It must be an absolute path, e.g. `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json`.
6. Scan the whole Appearance panel for the sentence `A hook is one piece of wording this application sends to a model` and for any mention of `hooks.json`.
7. Click the `Hooks` button in the tab rail.
8. Confirm the Hooks panel DOES carry the sentence beginning `A hook is one piece of wording this application sends to a model` and its own `Overrides are stored at ...hooks.json` paragraph.
9. Click `Appearance` again and confirm those two Hooks paragraphs are gone.

**Pass if — all of these**

- The Appearance panel's two paragraphs match the quoted text word for word.
- The token examples render as `--font-chat` and `--surface-base` with no leading `@`, no doubled `@@`, and no stray escape character.
- The path shown is absolute and ends in `\src\Huddle.App\App_Data\appearance.json`.
- Neither the Hooks intro sentence nor any mention of `hooks.json` appears anywhere on the Appearance panel.
- Both of those DO appear on the Hooks panel.

**Fail if — any of these**

- The Hooks intro or the hooks.json path paragraph appears on the Appearance panel -> the regression this item fixed is back: a shared paragraph sits above the tab rail instead of inside the Hooks case.
- A token example renders as `@--font-chat` or with a visible stray escape -> a Razor escaping mistake is teaching the Human to type a key that ThemeOverrides.Build will then reject, so the page looks broken while the code is fine.
- The path shown is relative (e.g. `App_Data\appearance.json`) -> the Human cannot find the file, and every later test in this area would be edited against a guessed location.

**Inconclusive if**

If the path shown differs from `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json`, that is NOT automatically a failure - the content root or Team:DataDir may differ on this machine. Record the path you actually see, treat it as the authority for every later test, and mark this step PASS on the absoluteness of the path rather than on its exact value.

> [!NOTE]
> Write the on-screen path down now. Tests 10 through 21 all edit that exact file, and 'I edited the wrong App_Data' is the single most common false bug report in this area.

### APPEARANCETHEME-03 — The Theme dropdown offers exactly System, Light and Dark, in that order

**Free** · about 2 min

*Proves the dropdown is rendered from the theme catalogue rather than a hand-maintained second list, and in the catalogue's declared order.*

**Before you start**

- The application is running.
- You are on http://localhost:5100/settings/appearance.

**Steps**

1. Find the control labelled `Theme` on the Appearance panel.
2. Click the dropdown to open it.
3. Read every option top to bottom and write the list down.
4. Close the dropdown without changing the selection (press Escape).

**Pass if — all of these**

- The dropdown holds exactly three options.
- They read, in this order: `System`, `Light`, `Dark`.
- There is no fourth option and no blank-looking extra row below `Dark`.

**Fail if — any of these**

- A fourth option appears -> someone added a second theme list by hand instead of rendering the catalogue, and the two will drift.
- The order is `System`, `Dark`, `Light` -> the catalogue's declared order (light first, then dark) is not what the dropdown is reading, so the dropdown is not rendering the catalogue in order.
- An option's label is a raw id such as `huddle-dark` rather than `Dark` -> the id is leaking into the display, the mirror image of the id/label confusion test 04 checks for.

**Inconclusive if**

If the dropdown will not open, or shows options but the page is visibly still loading (the Blazor circuit has not connected yet), wait five seconds, reload with F5, and try again. Record INCONCLUSIVE only if it still will not open after a reload, and note that the interactive circuit may not be connecting at all.

> [!NOTE]
> Labels are what you see; ids are what get stored. Test 04 checks the id half.

### APPEARANCETHEME-04 — Choosing a Theme stores its id and forces a full document load, not an in-place repaint

**Free** · about 5 min

*Proves the selection reaches the file as an id (never a display label) and that the page does a real full document load, which is the only thing that can re-render <head> and actually apply the theme.*

**Before you start**

- The application is running.
- appearance.json does NOT exist (delete it if it does, per setup).

**Steps**

1. Go to http://localhost:5100/settings/appearance.
2. Open DevTools with F12 and click the `Network` tab. Click the 'Clear' button (circle-with-slash icon) to empty the request list.
3. Select `Dark` in the `Theme` dropdown.
4. Watch the browser tab's loading spinner as you make the selection.
5. In DevTools -> Network, set the filter to `Doc` and look for a request for `/settings/appearance`.
6. Click that request and read its Status.
7. Read the address bar and read which tab in the rail is active.
8. Look at the page: is the application now dark?
9. In PowerShell run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` (substitute the path you wrote down in test 02).
10. Read the file contents exactly, including line breaks.

**Pass if — all of these**

- The browser performs a visible full page load (spinner turns) rather than a silent in-place update.
- DevTools -> Network shows a `Doc` request for `/settings/appearance` with status 200.
- After the load you are still on /settings/appearance with `Appearance` active and `Dark` selected in the dropdown.
- The whole application is now dark.
- The file now exists and reads, indented across three lines:
{
  "theme": "huddle-dark"
}
- The stored value is the id `huddle-dark`, NOT the label `Dark`.

**Fail if — any of these**

- No page load and no colour change -> <head> belongs to the server and Blazor's render tree cannot reach it, so without the forced full load the theme <link> is never emitted and the selection silently does nothing.
- The reload happens but lands on the Hooks tab -> the forced reload is targeting the wrong URL, so the Human loses their place every time they change theme.
- The file contains `"theme": "Dark"` -> the display label is being persisted instead of the id; the value will not resolve on the next load and the app will silently fall back to System forever.
- The file is written on one line, or contains \uXXXX escapes instead of plain characters -> the writer is not using indented, relaxed-escaping options, and the file is no longer comfortably hand-editable, which is the whole point of it.
- The file is not written at all -> the save path is broken.

**Inconclusive if**

If DevTools was opened AFTER the selection, you cannot judge whether a real document request happened - clear the Network panel, select `Light`, then select `Dark` again, and read the result from that second change. If the Network panel shows the document request as `(disk cache)` rather than 200, that is still a real document load; record PASS and note the cache status.

> [!NOTE]
> Changing the Theme costing a full page load is BY DESIGN and must not be reported as a defect. Swapping the href over JavaScript was considered and rejected: this feature has no JavaScript and the server is the single writer of <head>.

### APPEARANCETHEME-05 — The three cascade layers appear in <head> in exactly the right order, and every stylesheet is actually served

**Free** · about 8 min

*Catches the documented silent failure with no other signal: a theme <link> emitted in the wrong position silently does nothing, and a mis-resolved asset key renders an ordinary-looking href that 404s with no build warning, no startup error and no log line.*

**Before you start**

- The application is running.
- Dark is selected (run test 04 first, or create the file by hand).

**Steps**

1. Create or edit the override file so it reads exactly:
{
  "theme": "huddle-dark",
  "overrides": {
    "--accent": "#c14bd0"
  }
}
and save it.
2. Go to http://localhost:5100/ and press Ctrl+Shift+R for a hard reload.
3. Press Ctrl+U to view the page source (the server-rendered HTML).
4. Find the `<head>` section and write down, in order, every `<link rel="stylesheet" ...>` and every `<style>` element you see.
5. Confirm the order is: a link whose href starts `theme.` and ends `.css`; then one starting `app.` ; then one starting `Huddle.App.styles.` ; then `<link rel="stylesheet" href="themes/huddle-dark.css" />` ; then a `<style>` element.
6. Look at the `themes/huddle-dark.css` href specifically: it must be exactly that, with NO hash or fingerprint in it.
7. Close the view-source tab. In the browser address bar, go to http://localhost:5100/themes/huddle-dark.css.
8. Read what comes back.
9. Go to http://localhost:5100/themes/huddle-light.css and read what comes back.
10. Go back to http://localhost:5100/, open DevTools -> Network, filter to `CSS`, and hard-reload with Ctrl+Shift+R.
11. Read the Status column for every CSS request.
12. Scroll to the very bottom of the page and look for a pale yellow banner reading `An unhandled error has occurred.`
13. Now change the override file to `{ "theme": "huddle-dark" }` (no overrides), save, hard-reload, and view source again.
14. Finally set the Theme to `System` on /settings/appearance, hard-reload, and view source again.

**Pass if — all of these**

- In <head> the four stylesheet links appear in exactly this order: theme.*.css, app.*.css, Huddle.App.styles.*.css, themes/huddle-dark.css.
- The `themes/huddle-dark.css` href carries no hash - it is hand-built, not fingerprinted.
- The `<style>` element comes AFTER all four links and contains `:root {` and `--accent: #c14bd0;`.
- http://localhost:5100/themes/huddle-dark.css returns CSS: a five-line comment followed by a `:root` block whose only declaration is `color-scheme: dark;`. The light file is identical but says `color-scheme: light;`.
- Every CSS request in DevTools -> Network is status 200. None is 404.
- No pale yellow `An unhandled error has occurred.` banner is at the bottom of the page.
- With the theme set but no overrides, there is NO `<style>` element in <head>.
- With `System` selected, there is NO `themes/` link and NO `<style>` element in <head>.

**Fail if — any of these**

- The `themes/huddle-dark.css` link appears BEFORE `theme.css` -> all three layers target plain `:root`, so specificity ties and source order alone decides; the wrong order silently disables the theme with no error anywhere.
- The `themes/...` href carries a hash (e.g. `themes/huddle-dark.a1b2c3.css`) -> the theme link was routed through the fingerprinting asset helper, which returns an unresolved key verbatim rather than throwing; the href looks normal and 404s with no build warning, no startup error and no log line. This exact bug was live for a month.
- Any CSS request returns 404 -> the same trap; the visible symptoms are subtle (an error banner one viewport below the fold, and the reconnect modal showing all six paragraphs at once).
- A `<style>` element is emitted when no override survived validation -> an empty or stray style block means the override pipeline is emitting unconditionally.
- A `themes/` link is present while `System` is selected -> a Theme narrows color-scheme to one mode and would pin the app to it, defeating System entirely.

**Inconclusive if**

If Ctrl+U opens a view-source page that looks like the Blazor-rendered DOM rather than raw HTML (i.e. it shows expanded component markup), you are looking at the wrong thing - use DevTools -> Network -> click the document request -> `Response` tab instead, which is always the raw server HTML. If the browser refuses to display the .css URL and downloads it instead, open the downloaded file in a text editor and judge the contents from there; record PASS/FAIL on the contents, not on the display behaviour.

> [!NOTE]
> This is the single best test in the area for catching the documented asset-key silent failure, because it checks BOTH the markup and the round-trip. A string assertion in CI cannot see it: a correctly resolved href is fingerprinted, so the literal key never appears in the document.

### APPEARANCETHEME-06 — The saved Theme is shown as selected in the dropdown after a full load

**Free** · about 4 min

*Proves the page render and the control agree about the same fact, so the next change the Human makes does not start from a wrong value.*

**Before you start**

- The application is running.

**Steps**

1. Set the override file to exactly `{ "theme": "huddle-dark" }` and save it.
2. Go to http://localhost:5100/settings/appearance and press Ctrl+Shift+R.
3. Watch the `Theme` dropdown closely from the moment the page paints until it has fully settled (about two seconds). Note whether it ever shows `System` before settling, and whether it ends on `Dark`.
4. Confirm the page itself is dark.
5. Set the file to exactly `{ "theme": "huddle-light" }`, save, and press Ctrl+Shift+R. Read the dropdown.
6. Delete the file entirely, then press Ctrl+Shift+R. Read the dropdown.
7. Press Ctrl+U and check whether a `themes/` link is present in each of the three cases (re-do the file states if you need to).

**Pass if — all of these**

- With `huddle-dark` in the file, the dropdown settles on `Dark`, the page is dark, and view-source carries `themes/huddle-dark.css`.
- With `huddle-light` in the file, the dropdown settles on `Light` and view-source carries `themes/huddle-light.css`.
- With no file, the dropdown settles on `System` and view-source carries no `themes/` link.
- All three - file value, dropdown selection, and the <head> link - agree in every case.

**Fail if — any of these**

- The dropdown PERSISTENTLY reads `System` while the page is visibly dark and the file says `huddle-dark` -> the render and the control disagree about the same fact; the next selection the Human makes will start from a wrong value.
- The dropdown reads a theme while view-source has no `themes/` link -> the control is reading a different source than <head> does.

**Inconclusive if**

A BRIEF flash of `System` between the prerendered HTML painting and the interactive circuit connecting is not a failure - record WHICH phase it happened in and how long it lasted. Only a mismatch that persists after the page has fully settled is a defect. If you cannot tell the phases apart, throttle DevTools -> Network to 'Slow 4G' and repeat; the two phases separate visibly.

> [!NOTE]
> The select's selection is driven by a value attribute rather than two-way binding, which is why the transient case is worth distinguishing from the persistent one.

### APPEARANCETHEME-07 — No flash of light on a hard reload with Dark chosen

**Free** · about 6 min

*Proves the Theme is a render-blocking link the server emits into <head>, so the very first painted frame is already dark - the check that catches a reintroduced client-side loader or a 404ing theme link.*

**Before you start**

- The operating system is in LIGHT mode (per setup), so a flash would be maximally visible.
- The override file reads `{ "theme": "huddle-dark" }`.
- The application is running.

**Steps**

1. Go to http://localhost:5100/ and press Ctrl+Shift+R five times in a row, watching the page each time.
2. Go to http://localhost:5100/teammates and press Ctrl+Shift+R five times, watching each time.
3. Go to http://localhost:5100/settings/appearance and press Ctrl+Shift+R five times, watching each time.
4. Open DevTools -> Network and set the throttling dropdown (it reads `No throttling` by default) to `Slow 4G`.
5. Press Ctrl+Shift+R on each of those three routes again and watch the paint closely - the slow load exaggerates any flash into something you cannot miss.
6. If you think you saw a flash: open DevTools -> Performance, click the reload-and-record button (circular arrow), and inspect the screenshot filmstrip frame by frame for the first painted frame.
7. Set throttling back to `No throttling`.

**Pass if — all of these**

- On every reload of every route, at both speeds, the page's first painted frame is already dark.
- No white or light-coloured frame appears at any point, not even for one frame in the Performance filmstrip.

**Fail if — any of these**

- A white flash appears before the dark paint -> the theme <link> is either not being emitted into <head> at all (check view-source) or is 404ing (check Network); a flash is also the signature of someone reintroducing a client-side or localStorage-driven theme loader, which this design deliberately has none of.

**Inconclusive if**

If you cannot tell whether what you saw was a flash or just the browser's own blank-page-before-first-paint (which is white by default in a light OS), this is INCONCLUSIVE by eye alone - resolve it with the DevTools Performance filmstrip, which timestamps each painted frame. Record INCONCLUSIVE rather than guessing. If the browser is serving the page from cache and never repaints, use Ctrl+Shift+R (not F5) or tick DevTools -> Network -> `Disable cache`.

> [!NOTE]
> Run this with the OS in LIGHT mode. With a dark OS, the browser's own blank page is dark too and the test proves nothing.

### APPEARANCETHEME-08 — System follows the operating system live with no reload; an explicit Theme ignores the OS entirely

**Free** · about 8 min

*Proves the System path is pure CSS - no script, no server - and that an explicit Theme pins the application to one mode, which is a deliberate limit and not a bug.*

**Before you start**

- The application is running.
- DevTools -> Rendering -> 'Emulate prefers-color-scheme' is `No emulation`.
- The browser has no per-site appearance override for localhost.

**Steps**

1. Delete the override file (or select `System` on the Appearance tab) so no Theme is layered.
2. Press Ctrl+U and confirm there is NO `themes/` link in <head>. If there is one, stop - System is not actually selected.
3. Go to http://localhost:5100/settings/appearance. Arrange the window so the browser is visible while you use Windows Settings.
4. Open Windows Settings -> Personalisation -> Colours -> `Choose your default app mode` and switch it to `Dark`. WATCH THE BROWSER as you click, and do not touch or reload the browser.
5. Observe whether the application repaints: surfaces, text, borders, scrollbars and the `Theme` dropdown itself.
6. Switch the OS app mode back to `Light`, again watching the browser without touching it.
7. Repeat the flip while sitting on http://localhost:5100/ and again on http://localhost:5100/teammates.
8. In DevTools -> Elements, select the `<body>` element and read `background-color` in the Computed panel before and after one more flip.
9. Now select `Dark` in the `Theme` dropdown on /settings/appearance and let the page reload.
10. With the OS still in LIGHT mode, confirm the application is dark.
11. Flip the OS to Dark and back to Light twice, watching the application.
12. Select `Light` in the dropdown, set the OS to Dark, and confirm the application is light and stays light.

**Pass if — all of these**

- With System selected, flipping the OS app mode repaints the application IMMEDIATELY - no reload, no navigation, no click.
- The whole page switches together: surfaces, text, borders, scrollbars and native controls.
- Computed `background-color` on `<body>` reads `rgb(255, 255, 255)` in light and `rgb(27, 27, 31)` in dark.
- With `Dark` explicitly selected, the application is dark on a light OS and does NOT change when the OS is flipped.
- With `Light` explicitly selected, the application is light on a dark OS and does not change.

**Fail if — any of these**

- With System selected, nothing changes until you reload -> something JavaScript-driven or server-driven has crept into a path the design says involves no script and no server at all.
- Only part of the page switches -> a rule is reading a literal colour instead of a token, so it cannot follow the OS.
- With an explicit Theme selected, the application still follows the OS -> the theme file's `color-scheme` declaration is not reaching `:root`, or the theme <link> is not being emitted.
- A `themes/*.css` link is present in <head> while System is selected -> a Theme narrows color-scheme to one mode and would pin the app to it, defeating System.

**Inconclusive if**

If the OS flip does nothing at all in either direction, the browser may be forcing its own colour scheme. Check DevTools -> Rendering -> 'Emulate prefers-color-scheme' is `No emulation`, and check the browser's own appearance setting is 'System'. As a repeatable substitute that exercises the identical CSS path, set that Rendering dropdown to `prefers-color-scheme: dark` / `light` and observe; if the emulation flip works but the real OS flip does not, the defect is in the OS or the browser, not in this application - record INCONCLUSIVE with that note.

> [!NOTE]
> A Theme having no per-mode pair is a documented limit: 'Dark' is dark even on a light OS. Following the device means choosing System, which layers no Theme at all. Do not report that as a defect.

### APPEARANCETHEME-09 — The Theme choice is per installation, not per browser

**Free** · about 6 min

*Proves the choice lives in a file on the server and not in localStorage or a cookie - the deliberate consequence of this feature having no JavaScript.*

**Before you start**

- The application is running.
- The override file reads `{ "theme": "huddle-dark" }` and Chrome shows the application as dark.

**Steps**

1. In Chrome, confirm http://localhost:5100/ is dark.
2. Open a completely different browser (Microsoft Edge or Firefox) and go to http://localhost:5100/ with no other setup.
3. Observe whether it is dark.
4. In Chrome, open a new Incognito/Private window (Ctrl+Shift+N) and go to http://localhost:5100/.
5. Observe whether it is dark.
6. In Chrome, open DevTools -> Application tab -> Storage -> Local Storage -> http://localhost:5100 and read every key present. Then check Cookies for the same origin.
7. Still in DevTools -> Application, click `Clear site data` (or `Storage` -> `Clear site data`), then reload http://localhost:5100/ with Ctrl+Shift+R.
8. Observe whether it is still dark.
9. Go to the `dotnet run` console and press Ctrl+C to stop the server. Wait for the prompt to return.
10. Run `dotnet run --project src/Huddle.App --urls http://localhost:5100` again and wait for `Now listening on`.
11. Reload http://localhost:5100/ and observe.

**Pass if — all of these**

- The second browser shows the application dark immediately, with no setup.
- The private/incognito window shows it dark.
- Local Storage and Cookies for localhost:5100 contain NOTHING about themes, appearance or colours.
- Clearing all site data and reloading leaves the application dark.
- Stopping and restarting the application leaves it dark.

**Fail if — any of these**

- The second browser or the private window shows System/light -> the choice has moved into localStorage or a cookie, which was rejected by name: it makes the same install look different in a private window, and needs a pre-paint script to avoid a flash.
- Clearing site data resets the theme -> same defect.
- The choice does not survive an application restart -> it is not reaching the file on disk.

**Inconclusive if**

If the second browser shows light and the OS is in light mode, first check the file still says `huddle-dark` and that Chrome is genuinely reading the same install (same port, same process). If a corporate policy or extension forces a colour scheme in the second browser, that browser cannot judge this test - record INCONCLUSIVE and repeat with the incognito window of the first browser instead.

> [!NOTE]
> This is the positive proof of a documented limit: a second browser, a private window and a phone on the same install all see the same Theme.

### APPEARANCETHEME-10 — Token-name overrides change exactly what they name and nothing else

**Free** · about 8 min

*Proves the override layer applies per token, that untouched tokens still fall through to the Theme, and that the override values reach the document as raw characters rather than HTML entities.*

**Before you start**

- The application is running.
- At least one Room with a Message exists (see steps - the demo echo agent supplies one for free).

**Steps**

1. Go to http://localhost:5100/. In the sidebar, click `New chat`.
2. In the panel that opens, tick the agent named `echo`, then click `Start chat`.
3. In the composer at the bottom (placeholder `Message… (/invite @agent)`), type `hello` and press Enter. Wait for echo's reply to appear as a Message.
4. With the application still running, set the override file to exactly:
{
  "theme": "huddle-dark",
  "overrides": {
    "--font-chat": "Georgia, serif",
    "--accent": "#c14bd0"
  }
}
and save it.
5. Press F5 for a FULL page load (clicking a sidebar link does not count).
6. Look at the message text in the Room. Note the typeface.
7. Click `Teammates` in the sidebar, then press F5.
8. Look at the `New teammate` button. Note its background colour.
9. In DevTools -> Elements, select the `New teammate` button and read `background-color` in the Computed panel.
10. Select the `<body>` element and read `background-color` in the Computed panel.
11. Press Ctrl+U to view source and find the `<style>` element in <head>. Read it character by character.
12. Go to http://localhost:5100/settings/appearance and check whether a section headed `Overrides that didn't load` is present.
13. Look at every other surface in the Room and on /teammates: sidebar, borders, secondary text. Note anything that changed besides the chat font and the accent colour.

**Pass if — all of these**

- Message text in the Room renders in a serif face (Georgia); UI text elsewhere is unchanged.
- The `New teammate` button's Computed `background-color` reads `rgb(193, 75, 208)` (magenta).
- Computed `background-color` on `<body>` is still `rgb(27, 27, 31)` - the dark palette is untouched.
- View-source's `<style>` element reads literally:
:root {
    --accent: #c14bd0;
    --font-chat: Georgia, serif;
}
with declarations sorted by token name, real characters, and no `&quot;` or other HTML entities.
- The Appearance tab shows NO `Overrides that didn't load` section.
- Nothing else in the application changed.

**Fail if — any of these**

- Nothing changes at all -> most often the wrong App_Data was edited (re-check the path printed on the Appearance tab) or only an in-app navigation was done instead of a full page load; if the path and the full load are both right, the override pipeline is not reaching <head>.
- Tokens other than the two named also change, or a surface goes blank -> a blank surface means a Theme is being treated as self-contained rather than layered, which is exactly the failure the layering design exists to prevent.
- View-source shows `&quot;` or other entities inside the <style> element -> the CSS is being HTML-escaped; CSS does not decode entities, so every such value is dropped by the browser and the override silently does nothing.
- The declarations are not sorted by token name -> the output is no longer stable and diffable, which it is deliberately ordered to be.

**Inconclusive if**

If echo never replies and you have no Message to check the chat font on, the font half is INCONCLUSIVE - check the accent half (the `New teammate` button) on /teammates instead, which needs no Room, and note that the Room could not be created. If `New chat` lists no agents at all, the demo agents are disabled: check `Team:DemoAgent:Enabled` in src/Huddle.App/appsettings.json is `true` and restart before recording anything.

> [!NOTE]
> Free: the demo echo agent needs no Claude turn and costs nothing. The override key is the Token name (`--font-chat`), never a friendly alias - test 13 checks the negative.

### APPEARANCETHEME-11 — Exotic-but-allowed override values survive, including a quoted font name

**Free** · about 10 min

*Proves the value allowlist has not been tightened into uselessness - a quoted font stack is the single most likely thing anyone types - and proves rarer legal forms (percentages, color-mix, a reference to another token) also reach the page.*

**Before you start**

- The application is running.
- A Room with at least one Message exists (from test 10) for the font checks.

**Steps**

1. Set the override file to exactly:
{
  "theme": "huddle-dark",
  "overrides": {
    "--font-ui": "\"Segoe UI\", sans-serif"
  }
}
and save. (Note the backslash-escaped quotes - that is how a quoted font name is written in JSON.)
2. Press F5 on http://localhost:5100/.
3. Observe the UI font across the sidebar and headings.
4. Press Ctrl+U and read the `<style>` element in <head> character by character.
5. Go to http://localhost:5100/settings/appearance and check for an `Overrides that didn't load` section.
6. Now set the overrides object to exactly `{ "--font-size-base": "125%" }` and save. Press F5 on http://localhost:5100/.
7. Observe whether the whole application scales up.
8. Set the overrides object to exactly `{ "--surface-base": "color-mix(in srgb, #ffffff 50%, #000000)" }` and save. Press F5.
9. In DevTools -> Elements, select `<body>` and read Computed `background-color`.
10. Set the overrides object to exactly `{ "--font-chat": "var(--font-mono)" }` and save. Press F5 and open the Room.
11. Observe the typeface of the message text.
12. After each of the four, check the Appearance tab for an `Overrides that didn't load` section.

**Pass if — all of these**

- `\"Segoe UI\", sans-serif` is ACCEPTED: the UI font changes and nothing is listed under `Overrides that didn't load`.
- View-source's <style> contains the raw characters `--font-ui: "Segoe UI", sans-serif;` with real double-quote characters, not `&quot;`.
- `125%` scales the entire application up (every measurement derives from that one token).
- `color-mix(in srgb, #ffffff 50%, #000000)` is accepted and `<body>`'s Computed background-color becomes a mid grey rather than the dark value.
- `var(--font-mono)` is accepted and message text becomes monospace - a value may reference another token.
- In all four cases the `Overrides that didn't load` section is ABSENT.

**Fail if — any of these**

- The quoted font name is REJECTED and listed on the Appearance tab -> the allowlist has been tightened into uselessness; quotes are allowed deliberately because a quoted font stack is what everyone types.
- The value arrives in the document as `&quot;Segoe UI&quot;` -> the CSS is being HTML-escaped and is dropped by every browser with no error at all; this is the silent failure the raw-markup rendering exists to prevent.
- Any of the four is accepted (no problem listed) but produces no visual change -> the declaration is reaching <head> but not winning the cascade, meaning the layer order is wrong (see test 05).

**Inconclusive if**

If you cannot tell whether the UI font actually changed (Segoe UI is close to the default stack on Windows), that half is INCONCLUSIVE by eye - judge it instead from DevTools -> Elements -> select `<body>` -> Computed -> `font-family`, which must show the overridden stack. For `color-mix`, some browsers do not support it; if Computed `background-color` shows an invalid/ignored value, check the browser's support before recording a defect - the app's job here is only to let the value through unmodified.

> [!NOTE]
> A genuinely valid CSS value MAY still be refused by the allowlist (anything needing ; { } < > & @ : or a backslash, or `url(`, or over 200 characters). That is a documented limit, reported on the tab, and the app is not broken when it happens.

### APPEARANCETHEME-12 — A hostile or malformed override value is rejected, reported on the tab, logged once, and the file is left byte-for-byte unchanged

**Free** · about 12 min

*The highest-value test in this area: the override CSS is emitted as raw markup that nothing downstream escapes, so the value allowlist is the SOLE defence between a hand-typed file and a blanked or hijacked page.*

**Before you start**

- The application is running.
- You can see the `dotnet run` console.

**Steps**

1. In PowerShell, record the file's timestamp: run `dir E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and write down the LastWriteTime.
2. Set the override file to exactly:
{
  "theme": "huddle-dark",
  "overrides": {
    "--surface-base": "red; } :root{"
  }
}
and save it.
3. Press F5 on http://localhost:5100/.
4. Look at the whole page: is it rendering normally in dark, with no blank surface, no broken layout and no stray text such as `red;` or `:root{` visible on screen?
5. Press Ctrl+U to view source. Press Ctrl+F in that view-source tab and search for the exact string `} :root{`.
6. In the same view-source, search for `<style` and note whether any style element is present in <head>.
7. Go to http://localhost:5100/settings/appearance. Find the section headed `Overrides that didn't load` and read the line it lists.
8. Switch to the `dotnet run` console and read the most recent warning lines.
9. In PowerShell, run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and compare it character for character with what you wrote. Run `dir` on it again and compare LastWriteTime with the value you recorded.
10. Now repeat the whole cycle (edit, F5, check page, check view-source, check the tab, check the console, check the file) for each of these values in turn, replacing only the overrides object:
11. (a) `{ "--surface-base": "url(banner.png)" }`
12. (b) `{ "--accent": "#fff /* hi */" }`
13. (c) `{ "--accent": "<script>" }`
14. (d) `{ "--accent": "   " }`
15. (e) `{ "--accent": "#fff\\a" }` (a value containing a backslash)
16. (f) `{ "--accent": "aaaa..." }` with exactly 201 letter `a` characters between the quotes

**Pass if — all of these**

- In every one of the seven cases the application renders completely normally in dark: no blank surface, no broken layout, no stray CSS text on screen.
- View-source never contains the hostile substring `} :root{`, and contains NO `<style>` element in <head> when nothing valid survived.
- The Appearance tab lists one line per rejected entry under `Overrides that didn't load`. For the first case it reads exactly: `'--surface-base': value 'red; } :root{' contains a character that is not allowed; it was left in the file and ignored.`
- For (a) the line reads `'--surface-base': value 'url(banner.png)' contains a disallowed sequence; it was left in the file and ignored.`
- For (b), (c) and (e) the line is the `contains a character that is not allowed` form.
- For (d) and (f) the line reads `'--accent': value must be 1-200 characters once trimmed; it was left in the file and ignored.`
- The `dotnet run` console carries a warning of the form `Appearance file '<path>' rejected an override: ...` for each case.
- appearance.json is byte-for-byte what you wrote, with the LastWriteTime you last saved - the application never rewrites it.

**Fail if — any of these**

- The hostile text reaches the document (the view-source search for `} :root{` finds it) -> CATASTROPHIC: the allowlist is the sole defence and it has been breached; a `}` or `<` closes the CSS rule or the style element and can blank the application or inject markup.
- The application blanks, loses its layout, or shows stray CSS text -> the value escaped the rule it was meant to be confined to.
- The rejection is swallowed: the app looks fine but the Appearance tab lists nothing and the console says nothing -> the Human has a typo they can never find; this is a real defect even though nothing is visibly broken.
- The application rewrites or 'fixes' appearance.json (contents or LastWriteTime change without you saving) -> fixing the typo must be the only thing left for the Human to do, and a rewrite destroys their text.
- For case (b), a `contains a disallowed sequence` message instead of `contains a character that is not allowed` is NOT a failure - see notes.

**Inconclusive if**

If the Appearance tab shows nothing under `Overrides that didn't load` AND the console shows no warning AND the page looks fine, you may simply have edited the wrong file - re-read the path printed on the Appearance tab and confirm you saved to it, then re-run. Record INCONCLUSIVE rather than FAIL until the path is confirmed. If your editor rewrites or reformats the file on save (some editors normalise JSON), the unchanged-file check is INCONCLUSIVE for that editor - repeat it using Notepad.

> [!NOTE]
> Case (b) `#fff /* hi */` is rejected by the CHARACTER rule, not the comment-sequence rule, because `*` is not in the allowlist at all. Both outcomes are correct behaviour; only 'not rejected' is a failure. Case (a) uses `url(banner.png)` rather than a full URL on purpose: a URL containing `:` and `//` would be caught by the character rule first and never exercise the `url(` sequence rule.

### APPEARANCETHEME-13 — An override key that is not a Token name is rejected per entry, reported, and left in the file

**Free** · about 6 min

*Proves rejection is per entry rather than wholesale, and proves no friendly alias vocabulary exists - a second naming vocabulary is forbidden by design.*

**Before you start**

- The application is running.

**Steps**

1. Set the override file to exactly:
{
  "overrides": {
    "chat_font": "Georgia, serif"
  }
}
and save. Press F5 on http://localhost:5100/.
2. Observe whether anything changed visually.
3. Go to http://localhost:5100/settings/appearance and read the `Overrides that didn't load` list.
4. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and confirm `chat_font` is still in it.
5. Repeat with the overrides object set to `{ "--not-a-token": "x" }`.
6. Repeat with `{ "--font_chat": "Georgia, serif" }` (underscore instead of hyphen).
7. Repeat with `{ "--Font-Chat": "Georgia, serif" }` (wrong case).
8. Finally set a MIXED file:
{
  "theme": "huddle-dark",
  "overrides": {
    "--accent": "#c14bd0",
    "chat_font": "Georgia, serif"
  }
}
and save. Press F5 and go to http://localhost:5100/teammates.
9. Check the `New teammate` button's Computed `background-color` in DevTools, and check the Appearance tab's rejected list.

**Pass if — all of these**

- `chat_font` changes nothing visually and is listed as: `'chat_font' is not a theme token; it was left in the file and ignored.`
- `--not-a-token`, `--font_chat` and `--Font-Chat` are each rejected with the same message form naming that key.
- Every rejected key is still present in appearance.json afterwards - none is deleted or corrected.
- In the mixed file, `--accent` IS applied (the `New teammate` button reads `rgb(193, 75, 208)`) AND `chat_font` is listed as rejected.

**Fail if — any of these**

- `chat_font` silently WORKS -> a second naming vocabulary has been added, which is forbidden by name: it is a second thing to keep in step and fails by appearing only on whichever screen uses the forgotten Token.
- `--Font-Chat` is accepted -> the key comparison has been made case-insensitive; the token vocabulary is ordinal and case-exact.
- A rejected key is deleted from the file -> the Human's text is being destroyed instead of left for them to fix.
- The good `--accent` key is dropped because a sibling key was bad -> rejection must be per entry, not wholesale.

**Inconclusive if**

If the Appearance tab lists nothing at all for a case you expected to be rejected, first confirm you are editing the file the tab names and that you did a full page load. If the tab lists nothing AND the value visibly applied, that is a FAIL, not INCONCLUSIVE. If the tab lists nothing and the value did not apply either, record INCONCLUSIVE and check the console for a warning - a warning with no on-screen report is itself a reportable gap.

> [!NOTE]
> The 39 legal key names are exactly the custom properties declared in src/Huddle.App/wwwroot/theme.css. Friendliness belongs on the Appearance tab as a labelled control that writes the token name for you, never as an alias in the file.

### APPEARANCETHEME-14 — An unknown theme id is a warning, never a failure, and the file keeps saying it

**Free** · about 8 min

*Proves an unrecognised or ill-shaped theme id degrades to the built-in pair with a report, never a crash, and that the Human's value is preserved so the choice returns intact when that theme is added or imported later.*

**Before you start**

- The application is running.
- You can see the `dotnet run` console.

**Steps**

1. Set the override file to exactly:
{
  "theme": "dracula",
  "overrides": {
    "--accent": "#c14bd0"
  }
}
and save. Press F5 on http://localhost:5100/.
2. Observe the application: does it render normally, and does it follow the operating system's light/dark setting (flip the OS mode once to check)?
3. Press Ctrl+U and search the <head> for `themes/`.
4. Go to http://localhost:5100/settings/appearance. Read the `Theme` dropdown's selected value and read the problem list.
5. Read the most recent warning in the `dotnet run` console.
6. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and confirm it still says `dracula`.
7. Repeat the cycle for each of these `theme` values in turn, checking the page renders, the dropdown, the tab's problem list, the console, and the file each time:
8. (a) `"theme": "Huddle-Dark"` (uppercase - fails the id shape)
9. (b) `"theme": "huddle dark"` (contains a space)
10. (c) `"theme": "../../etc/passwd"` (path traversal)
11. (d) `"theme": ""` (empty string)
12. (e) `"theme": 42` (a number, not a string)
13. (f) `"theme": null`
14. For (c) specifically, press Ctrl+U and search the whole document for `..` inside any href.

**Pass if — all of these**

- In every case the application renders normally on the built-in pair, following the operating system, with no crash and no blank page.
- View-source contains NO `themes/` link in any of these cases.
- For (c), no href anywhere in the document contains `..` - the traversal-shaped id never becomes a URL.
- The Appearance dropdown shows `System` selected in every case.
- The Appearance tab lists `Theme 'dracula' is not a known theme; the built-in theme is used instead.` (and the equivalent naming each other bad value).
- The console carries `Appearance file '<path>' selects theme 'dracula', which is not a known theme; the built-in theme is used instead and the file is left unchanged.`
- appearance.json still contains the bad value, unchanged, in every case.

**Fail if — any of these**

- The application refuses to start, throws, or renders blank -> an unknown theme id must be a warning, never a failure.
- The application REWRITES the file to remove or correct the unknown id -> leaving it untouched is what lets the choice come back intact when that theme is added or imported later.
- A `<link href="themes/../../something.css">` appears in the document for case (c) -> the id is being turned into an href without being validated for shape AND catalogue membership; both halves matter.
- Nothing is listed on the Appearance tab and nothing is logged -> the Human has a typo with no signal anywhere.

**Inconclusive if**

For (e) and (f), the problem line quotes the raw JSON rather than a string; if the exact wording differs from the string cases, that is not a failure - record the wording you see. If the application renders but you cannot tell whether it is following the OS, flip the OS mode once and observe; if it does not follow, re-check view-source for a stray `themes/` link before recording anything.

> [!NOTE]
> Keeping an unknown id in the file is deliberate and is a documented limit, not an omission.

### APPEARANCETHEME-15 — Malformed JSON falls back wholesale - and reports NOTHING on the Appearance tab

**Free** · about 7 min

*Documents a designed near-silent failure so the tester records it as observed behaviour rather than guessing, and proves the app neither crashes nor repairs the Human's file.*

**Before you start**

- The application is running with a working theme AND at least one override in place (e.g. `{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }`), so you can watch them vanish.
- You can see the `dotnet run` console.

**Steps**

1. Confirm the application is dark with the magenta accent visible on /teammates' `New teammate` button.
2. Set the override file to the deliberately broken text `{"theme":"huddle-dark",` (truncated, no closing brace) and save it.
3. Wait about 2 seconds, then read the `dotnet run` console.
4. Press F5 on http://localhost:5100/.
5. Observe the application: is it on the built-in pair following the OS, with the theme and the override both gone?
6. Go to http://localhost:5100/settings/appearance and look for a section headed `Overrides that didn't load`.
7. Read the `Theme` dropdown's selected value.
8. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and confirm it is exactly the broken text you wrote.
9. Repeat with a top-level array: set the file to `[]`, save, wait 2 seconds, F5, and check the same four things (page, tab, console, file).
10. Now test the mid-write race: put a valid file back (`{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }`), open it in your editor, and press Ctrl+S repeatedly - ten saves in about five seconds - while watching an open /settings/appearance page and a second window on http://localhost:5100/.
11. Watch for any flicker of the dropdown back to `System`, or of the rejected list appearing and disappearing.

**Pass if — all of these**

- The application renders normally on the built-in pair after the malformed edit - theme and overrides both gone.
- The Appearance tab shows NO `Overrides that didn't load` section, and the dropdown reads `System`.
- The `dotnet run` console carries exactly one warning of the form `Could not parse appearance file '<path>'; falling back to no theme and no overrides.` (or, for a watcher-triggered rebuild, `...after a filesystem change, even after retrying; keeping the previously resolved appearance...`).
- appearance.json is exactly the broken text you wrote - unmodified, unrepaired.
- Repeated rapid saves of a VALID file do not flicker the dropdown or the reported problems.

**Fail if — any of these**

- The application crashes, throws, or fails to start on malformed JSON -> a bad file must never prevent the app running.
- The application rewrites or repairs the file -> the Human's text is being destroyed.
- NO warning appears in the console -> there is then no signal anywhere at all, and the Human's theme has 'randomly' reset with nothing to find.
- Rapid valid saves flicker every token back to default -> the mid-write retry is not working.

**Inconclusive if**

THIS IS A DOCUMENTED-BY-DESIGN NEAR-SILENT FAILURE. A malformed file parses to an empty document, so there are no per-entry problems to list and the UI says nothing at all. Record 'the tab reports nothing' as OBSERVED BEHAVIOUR, not as a defect, unless the console warning is also missing. If the console scrolled past and you cannot find the warning, re-save the broken file and watch the console live before recording INCONCLUSIVE.

> [!NOTE]
> The only on-screen signal of a broken file is the theme silently reverting. If the repo owner wants that surfaced on the tab, this test is where that gap gets recorded.

### APPEARANCETHEME-16 — Saving a theme while the file is malformed replaces its contents

**Free** · about 5 min

*Records the one place the otherwise-absolute promise 'the file is left exactly as it was' does not hold, with exact before/after contents so the repo owner can judge it.*

**Before you start**

- The application is running.

**Steps**

1. Set the override file to deliberately broken JSON that CONTAINS an override, exactly:
{"overrides":{"--accent":"#c14bd0"},,}
and save it.
2. Copy that exact text into your test notes as the BEFORE state.
3. Press F5 on http://localhost:5100/settings/appearance.
4. Read the `dotnet run` console and confirm a `Could not parse appearance file` warning appeared.
5. In the `Theme` dropdown, select `Dark`. Let the page reload.
6. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and copy the exact contents into your notes as the AFTER state.
7. Compare BEFORE and AFTER.

**Pass if — all of these**

- The save does not throw; no error banner appears and no unhandled exception is logged.
- The file afterwards is VALID, indented JSON reading:
{
  "theme": "huddle-dark"
}
- Both the broken text and the `--accent` override that was inside it are gone.
- The application is dark afterwards.

**Fail if — any of these**

- The save throws, or shows an error banner -> saving over a bad file must still work.
- The file afterwards is invalid JSON -> the writer produced something the app itself cannot read back.
- The application is not dark afterwards -> the save did not take effect.

**Inconclusive if**

This test has no 'correct' answer to assert against beyond not-throwing: losing the overrides is the logical consequence of 'malformed falls back wholesale' meeting 'Save re-reads then writes'. RECORD the exact before/after contents and hand them to the repo owner rather than judging it yourself. Mark the test PASS if the save worked and the file is valid; mark it INCONCLUSIVE only if you could not capture both file states.

> [!NOTE]
> Do not file this as a defect on your own judgement. It is the one documented exception to 'the app never rewrites your file', and it needs a human decision.

### APPEARANCETHEME-17 — Selecting System removes the theme key but preserves overrides and unknown keys

**Free** · about 7 min

*Proves the save re-reads the file under its write lock and edits only the theme key, so a concurrent hand-edit and any key the app does not recognise both survive.*

**Before you start**

- The application is running.

**Steps**

1. Set the override file to exactly:
{
  "theme": "huddle-dark",
  "overrides": {
    "--accent": "#c14bd0"
  },
  "note": "keep me"
}
and save it.
2. Press F5 on http://localhost:5100/settings/appearance so the app picks it up. Confirm the dropdown reads `Dark`.
3. Go to http://localhost:5100/teammates and confirm the `New teammate` button is magenta (Computed `background-color` = `rgb(193, 75, 208)`).
4. Go back to /settings/appearance and select `System` in the `Theme` dropdown. Let the page reload.
5. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and read the exact contents.
6. Confirm the `theme` key is GONE ENTIRELY - not present as `null`, not present as an empty string.
7. Confirm `overrides` with `--accent` is still present, and that `note` with the value `keep me` is still present.
8. Go to /teammates, press F5, and confirm the magenta accent is still applied.
9. Go back to /settings/appearance and select `Light`. Let the page reload.
10. Run `type` on the file again and read the exact contents.
11. Confirm the file is still indented and readable, with plain characters and no `\uXXXX` escapes.

**Pass if — all of these**

- After choosing System the file contains `overrides` and `note` unchanged and no `theme` key at all.
- After choosing Light the file contains `"theme": "huddle-light"` AND still contains `overrides` and `note`, untouched.
- The magenta accent persists through every one of these selections.
- The file stays indented and human-readable throughout, with no escape sequences in place of ordinary characters.

**Fail if — any of these**

- The file is clobbered down to just `{ "theme": "..." }` -> the save is not re-reading the file before editing, so a concurrent hand-edit to overrides is destroyed.
- The `note` key is silently deleted -> an unknown top-level key must be kept.
- `theme` is written as `null` or `""` rather than removed -> System is being persisted as a value instead of as the absence of a choice.
- Characters come back as `"` or similar escapes -> the writer is using the protocol JSON options rather than the relaxed, human-editable ones, and the file is no longer comfortable to hand-edit.

**Inconclusive if**

If your editor holds the file open with a lock while the app tries to write, the save may fail in a way that is about your editor, not the app - close the editor, repeat, and only then record a result. If the file contents look right but you cannot judge indentation from `type`, open it in Notepad and look.

> [!NOTE]
> Removing the key rather than storing a sentinel is what makes 'System' mean 'no Theme layered at all'.

### APPEARANCETHEME-18 — A hand-edit updates the open tab live, but the page's colours only change on a full load

**Free** · about 6 min

*Prevents the single most likely FALSE bug report in this area, and proves the filesystem watcher and the tab's subscription are both alive.*

**Before you start**

- The application is running.
- http://localhost:5100/settings/appearance is open in the browser.
- An editor is open on the override file.

**Steps**

1. Set the override file to exactly `{ "theme": "huddle-light" }` and save. Press F5 so the page is light and the dropdown reads `Light`.
2. Now WITHOUT touching the browser at all, change the file in your editor to exactly `{ "theme": "huddle-dark" }` and save it.
3. Keep your eyes on the browser's `Theme` dropdown for the next 2 seconds. Note whether it flips to `Dark` by itself.
4. Note whether the page's COLOURS changed. They should not have.
5. Click `Teammates` in the sidebar (an in-app navigation). Note whether the colours changed.
6. Press F5. Note whether the colours changed now.
7. Go back to /settings/appearance. Without touching the browser, add a bad override by hand: change the file to `{ "theme": "huddle-dark", "overrides": { "nope": "x" } }` and save.
8. Watch the page for 2 seconds: note whether an `Overrides that didn't load` section appears by itself.
9. Change the file to `{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }` and save. Watch for 2 seconds: the rejected section should disappear, and the colours should NOT change.
10. Press F5 and confirm the magenta accent now applies.
11. Check the `dotnet run` console for any unhandled exception during all of the above.

**Pass if — all of these**

- Within about half a second of each save, the `Theme` dropdown and the `Overrides that didn't load` section update BY THEMSELVES with no reload and no click.
- The page's colours do NOT change on a hand-edit, and do NOT change on in-app navigation.
- The colours DO change after pressing F5.
- No unhandled exception appears in the console.

**Fail if — any of these**

- The dropdown does NOT update after a hand-edit -> either the filesystem watcher is dead or the tab did not subscribe to the store's change event; the Human then has no feedback at all while hand-editing.
- The page throws, or the console logs an unhandled exception on disconnect afterwards -> a component that subscribes to a singleton store and does not unsubscribe when disposed leaks and never dies.
- The colours change without a full load -> a second writer of <head> exists, which this design deliberately does not have.

**Inconclusive if**

If the dropdown does not update within 2 seconds, wait another 3 seconds before judging - the watcher debounces for half a second and some editors write in a burst. If it still has not updated, check the console for a `FileSystemWatcher reported an error` warning; if one is present, the watcher lost events and this run is INCONCLUSIVE - reload and repeat.

> [!NOTE]
> THE HIGH-VALUE FALSE BUG REPORT: a tester edits the file, sees the dropdown update, sees no colour change, and files 'overrides do not work'. They do. <head> is server-rendered and Blazor's render tree cannot reach it, and in-app navigation is client-side so it does not re-render <head> either. Only a full document load does.

### APPEARANCETHEME-19 — Deleting appearance.json while the app runs returns it to System, and the app never recreates it

**Free** · about 5 min

*Proves the watcher handles deletion and proves an absent file is treated as the normal first-run state rather than something to be written back.*

**Before you start**

- The application is running with `{ "theme": "huddle-dark" }` saved and the page visibly dark.
- http://localhost:5100/settings/appearance is open.

**Steps**

1. Confirm the `Theme` dropdown reads `Dark` and the page is dark.
2. In PowerShell, run `del E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` while the application is still running.
3. Keep your eyes on the browser's `Theme` dropdown for 2 seconds without touching the page.
4. Press F5 and observe whether the application now follows the operating system (flip the OS mode once to confirm).
5. Run `dir E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and confirm the file is still absent.
6. Wait 30 seconds, navigate around the application, then run `dir` on it again.
7. Now select `Dark` in the `Theme` dropdown and let the page reload.
8. Run `dir` and `type` on the file again.

**Pass if — all of these**

- Within about half a second of the delete, the dropdown returns to `System` on its own.
- After F5 the application follows the operating system.
- The file stays ABSENT - the application does not recreate it, not immediately and not after navigating around.
- Selecting `Dark` afterwards recreates the file with `{ "theme": "huddle-dark" }`.

**Fail if — any of these**

- The application recreates appearance.json just to read from it -> an absent file is the normal first-run state, and writing defaults at startup admits a state where the file and the code disagree about what the app does bare.
- The dropdown does not return to `System` -> the watcher does not handle deletion.
- The page throws when the file disappears -> a missing file is being treated as an error rather than the default.

**Inconclusive if**

If the delete fails because the file is locked by your editor, close the editor and retry before recording anything. If the dropdown does not update within 2 seconds, wait 3 more; if it still has not, this overlaps with test 18's watcher check - record INCONCLUSIVE here and resolve it there.

> [!NOTE]
> Same rule the hooks file carries: the app never creates a config file just to read from it.

### APPEARANCETHEME-20 — Two browser windows stay in step on the tab, and the change propagates through the file

**Free** · about 6 min

*Proves the store is a single instance raising its change event after the write, and that a second circuit receives it and does not leak or throw.*

**Before you start**

- The application is running.

**Steps**

1. Open http://localhost:5100/settings/appearance in two browser windows and arrange them side by side. Call them A and B.
2. Confirm both dropdowns show the same value and both pages look the same.
3. In window A, select `Dark` in the `Theme` dropdown.
4. Watch window A: it should perform a full page load and go dark.
5. Watch window B's `Theme` dropdown for 2 seconds WITHOUT touching window B.
6. Note whether window B's dropdown flipped to `Dark` by itself, and whether window B's COLOURS changed.
7. Press F5 in window B and observe the colours.
8. In window B, select `Light`. Watch window A's dropdown for 2 seconds without touching it.
9. Close window B entirely. Watch the `dotnet run` console for 10 seconds for any unhandled exception on circuit disposal.
10. Interact with window A (select `Dark` again) and confirm it still works after B was closed.

**Pass if — all of these**

- Window A reloads and goes dark on the selection.
- Window B's dropdown flips to `Dark` by itself within about half a second.
- Window B's COLOURS stay as they were until window B gets its own full page load - then they match.
- The reverse direction works too (B's selection updates A's dropdown).
- Closing window B logs no unhandled exception, and window A keeps working afterwards.

**Fail if — any of these**

- Window B's dropdown does not update -> the change event is not reaching a second circuit, so two open tabs can disagree about the same saved fact.
- Window B throws an error -> a second subscriber is not being handled safely.
- The console logs an unhandled exception when window B closes -> a subscription to the singleton store is not being released when the component is disposed, so it leaks and never dies.
- Window B's COLOURS change without a reload -> a second writer of <head> exists, which this design does not have.

**Inconclusive if**

If both windows are in the same browser process and one is backgrounded, the browser may throttle its timers and delay the update - bring window B to the front (or use two side-by-side, both visible) and repeat before recording a slow update as a failure.

> [!NOTE]
> Window B's colours NOT changing without a reload is the correct result, not a defect - see test 18's note.

### APPEARANCETHEME-21 — Re-selecting the Theme that is already selected is harmless

**Free** · about 3 min

*Checks the no-op path does not loop, error, or destroy the overrides sitting alongside the theme key.*

**Before you start**

- The application is running.
- The override file reads `{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }` and the app is dark with the magenta accent.

**Steps**

1. Go to http://localhost:5100/settings/appearance and confirm the dropdown reads `Dark`.
2. Open the `Theme` dropdown and choose `Dark` again.
3. Watch the page for 5 seconds.
4. Check whether the page reloaded once, or not at all, or repeatedly.
5. Run `type E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json` and read the contents.
6. Go to /teammates, press F5, and confirm the `New teammate` button is still magenta.
7. Check the `dotnet run` console for errors, and scroll to the bottom of the page for the pale yellow `An unhandled error has occurred.` banner.

**Pass if — all of these**

- Either nothing happens, or the page reloads exactly once and stays dark. Both are acceptable.
- The file still reads `{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }` - the override survived.
- No reload loop, no error banner, no console error.

**Fail if — any of these**

- The page reloads repeatedly in a loop -> the save-and-reload path is retriggering itself.
- The file loses its `overrides` -> the save is clobbering the file rather than editing only the theme key (see test 17).
- An error banner appears at the bottom of the page -> an unhandled error occurred during the no-op save.

**Inconclusive if**

If the dropdown will not let you re-select the same value at all (some browsers fire no change event), that is the 'nothing happens' branch and is a PASS - note which branch you observed. Record INCONCLUSIVE only if you cannot tell whether a reload happened; use DevTools -> Network filtered to `Doc` to settle it.

> [!NOTE]
> Cheap regression check for the reload path, best run immediately after test 17.

### APPEARANCETHEME-22 — Dark mode walked across every page and every state - the acceptance test for the whole item

**Free** · about 25 min

*Hunts for the ONE element that did not switch: every literal colour that survived tokenisation shows up here and nowhere else, because no test in the suite renders a browser.*

**Before you start**

- The application is running.
- The override file reads exactly `{ "theme": "huddle-dark" }` - NO overrides, so you are judging the built-in dark palette.
- The operating system is in LIGHT mode, so anything that failed to switch is obvious.
- For the rejected-persona part, you will hand-edit a Persona file (see steps). If you create or edit a Persona through the UI, set Model = Haiku and Effort = low - the standing convention. Nothing here needs a model and nothing here costs money.

**Steps**

1. Press Ctrl+Shift+R on http://localhost:5100/ and confirm the whole application is dark.
2. In DevTools -> Elements, select `<body>` and read Computed `background-color`. It must be `rgb(27, 27, 31)`.
3. (a) With no room selected, inspect: the empty state reading `No rooms yet. Start an agent to create one.`; the sidebar; the `New chat` button; the room list; and the two sidebar links `Teammates` and `Settings`. Note anything light.
4. (b) Click `New chat`. Inspect the panel: the agent rows for `echo` and `alpha`, their coloured status dots, and the `Start chat` button.
5. (c) Tick `echo` and click `Start chat`. In the Room, inspect: the chat header, the member line, each message row (sender name, timestamp, body), the composer textarea including its placeholder `Message… (/invite @agent)`, and the budget note line.
6. (d) Type `hello` and press Enter. WATCH THE DRAFT STREAM IN: inspect the streaming row, its caret, the stop control, and any tool-activity line while they are on screen.
7. (e) Type a fenced code block: type three backticks, then `code`, then three backticks, and press Enter. Inspect the rendered code block - it exercises the monospace token.
8. (f) Click `Teammates` in the sidebar. Inspect the tiles, the avatar monograms, the team filter select, and the `New teammate` button.
9. (g) Click a teammate tile to open the card. Inspect the card overlay, the dimming scrim behind it, the `Persona`, `Model` and `Effort` sections, and the Edit / Open / Remove buttons. Close the card.
10. (h) Create a rejected persona file so the rejected block renders: in `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\`, create `broken.md` containing exactly:
---
name: broken
---
A persona with no title.
Save it, then press F5 on /teammates.
11. (i) Find the section headed `Files that didn't load` and inspect its heading, the path and the reason text.
12. (j) Go to http://localhost:5100/settings/hooks and inspect the whole panel, then click `Appearance` and inspect that panel.
13. Now check native controls specifically: open every `<select>` you can find (the Theme dropdown, the team filter, the card's Model and Effort selects) and check the DROPDOWN POPUP itself is dark, not a white system menu.
14. Click into the composer textarea and check it is dark.
15. Scroll any scrollable area (the room list, the message list) and check the SCROLLBAR renders dark.
16. For any element you suspect: select it in DevTools -> Elements, read the suspect colour property in Computed, then click through to the declaring rule. Note whether the value is a literal hex or a `var(--token)` reference.
17. Finally, scroll to the very bottom of each page and check for the pale yellow `An unhandled error has occurred.` banner.
18. Delete `broken.md` afterwards.

**Pass if — all of these**

- Every surface, border, text colour and control listed in (a) through (j) uses the dark palette.
- Computed `background-color` on `<body>` is `rgb(27, 27, 31)`.
- Native controls are dark: every `<select>`'s popup menu, the textarea, and the scrollbars.
- No white or pale surface appears anywhere except the two documented exemptions in the notes.
- No element renders blank or with no colour at all.
- No pale yellow error banner at the bottom of any page.

**Fail if — any of these**

- ANY element that stays light -> a colour literal survived tokenisation; that is the entire point of this step and the item's own stated acceptance test.
- A white page ground behind everything -> the html/body background and colour declarations are missing; a pure token substitution leaves the ground white.
- A light hover highlight on a sidebar row, or a light scrim behind the teammate card -> a literal escaped in that specific rule.
- White scrollbars or a white native dropdown popup -> `color-scheme` is not reaching `:root`, so the browser is still painting its light-mode widgets.
- A blank or empty surface where a colour should be -> a token resolved to nothing, which is worse than the wrong colour.

**Inconclusive if**

If you cannot create a Room because `New chat` lists no agents, parts (c), (d) and (e) are INCONCLUSIVE - check `Team:DemoAgent:Enabled` is `true` in src/Huddle.App/appsettings.json and restart, then repeat. If /teammates shows `No Personas yet. Choose New teammate to add one.`, parts (f) and (g) are INCONCLUSIVE until you add a Persona - either create one via `New teammate` (Model = Haiku, Effort = low) or hand-write a valid .md file under App_Data\Teams. Record which parts you could not reach rather than marking the whole test PASS.

> [!NOTE]
> TWO THINGS MUST NOT SWITCH AND ARE NOT BUGS. (1) The `An unhandled error has occurred.` banner stays LIGHTYELLOW in dark mode on purpose - it is shown when the application has already failed, the one moment a Theme cannot be trusted, so it stays a literal to stay readable. (2) The Persona's Markdown source shown on the teammate card deliberately keeps the inherited font rather than the monospace token. Do not file either as 'the element that did not switch'. Free: the demo echo agent needs no Claude turn.

### APPEARANCETHEME-23 — Hover, selection and focus surfaces all switch, and the four old hover colours are now one

**Free** · about 8 min

*Four different pale colours all meant 'this row is under the pointer' before tokenisation; any hover that now differs from the others is a literal that escaped.*

**Before you start**

- The application is running with `{ "theme": "huddle-dark" }` and the operating system in LIGHT mode.
- At least one Room exists and at least one Teammate tile is visible (the demo agents supply the Room for free; if you add a Persona, use Model = Haiku and Effort = low).

**Steps**

1. Go to http://localhost:5100/ and hover a room row in the sidebar. Note the highlight colour.
2. In DevTools -> Elements, select that room row, click the `:hov` button in the Styles pane, tick `:hover`, and read Computed `background-color`. Write the value down.
3. Click `New chat` and hover a row in the agent list. Force `:hover` on it the same way and read Computed `background-color`. Write it down.
4. Click `Teammates` in the sidebar and hover a teammate tile. Force `:hover` and read Computed `background-color`. Write it down.
5. Compare the three values.
6. Go back to a Room and click it so it shows as the ACTIVE/selected room in the sidebar. Read that row's Computed `background-color` and confirm it is a dark selected-surface colour, visibly different from the hover colour but still dark.
7. Press Tab repeatedly to move keyboard focus through the composer textarea, then through the Appearance tab's `Theme` select, then through a teammate card's fields. At each stop, look at whether the focus outline is clearly visible against the dark ground.

**Pass if — all of these**

- All three hover highlights resolve to the SAME Computed `background-color`, and that value is a dark colour (not a pale one).
- The selected room row is a dark selected-surface colour, distinct from hover but still dark.
- Every focus outline is clearly visible against the dark ground at every Tab stop.

**Fail if — any of these**

- A hover highlight is pale (e.g. `rgb(238, 243, 255)`, `rgb(232, 232, 232)`, `rgb(243, 246, 251)` or `rgb(240, 240, 240)`) -> that is one of the four original literals; it escaped tokenisation.
- Two of the three hover values differ from each other -> they are not both reading the single hover token, so one is still a literal.
- A focus outline is invisible against the dark ground -> the focus rule is reading a colour that does not switch, or no token at all.

**Inconclusive if**

If forcing `:hover` in DevTools has no effect (some hover rules are on a parent or a pseudo-element), hover the element with the mouse and take a screenshot instead, then compare the three screenshots by eye and with a colour picker. Record INCONCLUSIVE if you cannot obtain a Computed value for a given row, and say which row.

> [!NOTE]
> The four pale literals became one hover token in this item, so sameness across all three surfaces IS the assertion.

### APPEARANCETHEME-24 — The reconnect modal shows one themed state paragraph over a dimmed backdrop, in both Themes

**Free** · about 10 min

*Catches the documented silent failure where the scoped-CSS bundle stops loading: the modal then shows all six paragraphs at once, unstyled, and nothing else in the application says a thing.*

**Before you start**

- The application is running with `{ "theme": "huddle-dark" }`.
- A browser window is open on a Room at http://localhost:5100/.
- You will stop and restart the server during this test. It is free.

**Steps**

1. Confirm the browser is on a Room and the application is dark.
2. Press Ctrl+U and confirm a link whose href starts `Huddle.App.styles.` and ends `.css` is present in <head>. Also open DevTools -> Network, filter to `CSS`, hard-reload, and confirm no CSS request returns 404.
3. Go to the `dotnet run` console and press Ctrl+C to stop the server. Immediately look at the browser.
4. Read the dialog that appears. COUNT the paragraphs visible at once.
5. Note the first paragraph's text and whether a two-dot animation is running.
6. Wait and watch: the text should change to `Rejoin failed... trying again in N seconds.` with a counting number, and eventually to `Failed to rejoin.` / `Please retry or reload the page.` with a `Retry` button.
7. Look at the panel itself: is it a raised surface with rounded corners and a shadow, and is the page BEHIND it visibly dimmed?
8. In DevTools -> Elements, find `<dialog id="components-reconnect-modal">`, expand it to reveal the `::backdrop` pseudo-element, select `::backdrop`, and read Computed `background-color`.
9. Look at the `Retry` button's colour - it must be the application's accent (a dark purple in the dark theme), not a light framework blue.
10. Restart the server with `dotnet run --project src/Huddle.App --urls http://localhost:5100`, reload the page.
11. Go to /settings/appearance, select `Light`, and let the page reload.
12. Open a Room again, stop the server again with Ctrl+C, and repeat the same observations with the light panel.
13. Read Computed `background-color` on `::backdrop` again.
14. Restart the server.

**Pass if — all of these**

- Exactly ONE state paragraph is visible at any moment - never all six at once.
- The sequence is `Rejoining the server...` (with the two-dot animation), then `Rejoin failed... trying again in N seconds.`, then `Failed to rejoin.` / `Please retry or reload the page.` with a `Retry` button.
- The panel is a themed raised surface with rounded corners and a shadow.
- The page behind it is visibly DIMMED.
- Computed `background-color` on `#components-reconnect-modal::backdrop` reads `rgba(0, 0, 0, 0.4)` in the light theme and `rgba(0, 0, 0, 0.6)` in the dark theme.
- The `Retry` button uses the application's accent colour, not a framework blue.
- Both themes behave identically apart from the palette.

**Fail if — any of these**

- ALL SIX paragraphs show at once, unstyled (`Rejoining the server`, `Rejoin failed`, `Failed to rejoin`, `The session has been paused by the server`, `Failed to resume the session`, plus both buttons) -> the scoped-CSS bundle is not loading; that is the asset-key trap, and the whole modal stylesheet silently never reaches the browser with no build warning, no startup error and no log line.
- The panel renders but the backdrop does NOT dim -> the backdrop pseudo-element is no longer inheriting the scrim token; this was the one assumption genuinely in doubt when the feature shipped, it was measured to work, and NO fallback was added, so nothing else catches a regression here.
- The `Retry` button renders in a light framework blue rather than the accent -> the framework's own default styles are winning, another symptom of the scoped bundle not loading.

**Inconclusive if**

If the dialog never appears when you stop the server, the browser may already have disconnected or the page may have been idle - reload the page, interact with it once, then stop the server again. If you cannot get DevTools to expose the `::backdrop` pseudo-element (support varies by browser version), judge the dimming by eye against a screenshot taken before the stop, and record the backdrop measurement as INCONCLUSIVE rather than FAIL.

> [!NOTE]
> Free: stopping and restarting the app costs nothing. This is the second-best test in the area for the asset-key silent failure, after test 05.

### APPEARANCETHEME-25 — The layering probe: a Theme that sets one Token inherits the whole built-in dark palette for the rest

**Free** · about 20 min

*Proves the cascade fall-through that the whole design rests on: a Theme is a thin layer over the tokens, not a self-contained palette, so an unmapped token resolves to the built-in value rather than to nothing.*

**Before you start**

- THIS IS THE ONE TEST IN THIS AREA THAT NEEDS A SOURCE EDIT AND A REBUILD. It is still free - no agent, no model, no money.
- The application is running; you will stop it.
- You can edit C# and CSS files and run `dotnet build`.

**Steps**

1. Stop the application: press Ctrl+C in the `dotnet run` console.
2. Create the file `E:\Repos\Huddle\src\Huddle.App\wwwroot\themes\probe.css` containing exactly:
/* Temporary probe theme for manual checklist step 34. Delete after testing. */
:root {
    color-scheme: dark;
    --accent: #ff00ff;
}
3. Open `E:\Repos\Huddle\src\Huddle.App\Themes\ThemeCatalog.cs`. Inside the `BuiltIn` collection expression, after the `huddle-dark` entry, add a new line reading:
        new ThemeDescriptor(Id: "probe", Label: "Probe", IsDark: true),
4. Run `dotnet build Huddle.slnx`. It must report 0 Warning(s) and 0 Error(s).
5. Run `dotnet run --project src/Huddle.App --urls http://localhost:5100` and wait for `Now listening on`.
6. Go to http://localhost:5100/settings/appearance. Confirm the `Theme` dropdown now offers a fourth option, `Probe`.
7. Select `Probe` and let the page reload.
8. In DevTools -> Elements, select `<body>` and read Computed `background-color`.
9. Go to /teammates and read Computed `background-color` on the `New teammate` button.
10. Look at the whole application: is every surface the built-in DARK palette, with only the accent magenta?
11. Now STOP the app, edit probe.css to REMOVE the `color-scheme: dark;` line entirely (leaving only the `--accent` declaration inside the `:root` block), and save.
12. If the build itself succeeded, start the app, set the operating system to LIGHT mode, and select `Probe` again.
13. Read Computed `background-color` on `<body>` and look at the application's surfaces.
14. Confirm the `Theme` dropdown is back to exactly `System`, `Light`, `Dark`.

**Pass if — all of these**

- With `color-scheme: dark;` present, selecting `Probe` gives a magenta accent (`rgb(255, 0, 255)` on the `New teammate` button) AND every other surface is the built-in DARK palette - `<body>` Computed `background-color` is `rgb(27, 27, 31)`.
- No surface is blank, transparent, or unstyled.
- With `color-scheme` REMOVED and the OS in light mode, the unmapped tokens render LIGHT (`<body>` background `rgb(255, 255, 255)`) while the accent stays magenta.
- The clean-up leaves `dotnet build Huddle.slnx` green and the dropdown back to three options.

**Fail if — any of these**

- Blank or transparent surfaces with the probe selected -> the exact failure a self-contained per-Theme stylesheet would cause, and the reason layering was chosen: a background reading a token with no value behind it is not 'the light value', it is nothing.
- Everything renders LIGHT while `color-scheme: dark;` IS declared -> the fall-through is broken, so an unmapped token is not resolving against the Theme's declared scheme.

**Inconclusive if**

If the build fails for an unrelated reason (a stale SDK image, a pre-existing compile error), this test is INCONCLUSIVE - revert your probe edits, get a green `dotnet build Huddle.slnx` on a clean tree first, then start over.

> [!NOTE]
> THE SECOND HALF IS A DOCUMENTED TRAP THAT READS AS A BUG AND IS NOT: a Theme that omits `color-scheme` leaves the base stylesheet's `light dark` in force, so its unmapped tokens follow the OPERATING SYSTEM rather than the Theme - a dark Theme with three unmapped tokens renders those three surfaces light. Expected; do not file it. Also: DO NOT leave the probe behind. It is a temporary artefact and the clean-up step is part of the test.

---

Back to [the manual test script](../manual-tests.md).
