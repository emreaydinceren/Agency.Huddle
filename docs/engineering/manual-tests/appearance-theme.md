# Appearance tab and Themes

Prove that the Appearance tab is an honest control for the Theme: that it is reachable by its own route, lists the whole built-in catalog under the right headings, stores the choice as an id in `appearance.json`, applies it at once with no page load and with no first-paint flash, shares it across browsers, and degrades safely when the file is wrong. The Development profile sets `Team:Acp:Enabled: true`, so the global cost guard in section 0.2 is the only thing holding ACP off. Set it before you launch.

**25 tests** · 25 free, none paid · about 2.9 hours.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

> [!IMPORTANT]
> A Theme is a MudBlazor theme ([ADR-0010](../../adr/0010-a-theme-is-a-mudblazor-theme.md)) and
> carries a single palette and its own mode ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)).
> There is **no light/dark control** on this tab: choosing a Theme chooses light or dark, and the
> application ignores the device's colour setting entirely. The per-token override layer
> (`wwwroot/theme.css`, `themes/*.css`, `ThemeOverrides`) no longer exists.
> `appearance.json` holds the keys `theme` (the Theme being shown), `lightTheme` and `darkTheme` (the Human's pair, [ADR-0054](../../adr/0054-a-light-theme-and-a-dark-theme.md)), and `accentColor` (Custom Accent only);
> a retired `dark` key, or a retired `libraryPaneSide` key, left in an old file is kept as an unknown key and ignored.
>
> **Since ADR-0054 the Appearance tab has two pickers**, `Light theme` and `Dark theme`, and the top
> bar has a mode button (APPEARANCETHEME-30 to 33 cover them). Tests 03, 04, 06, 28 and 29 were
> written for the single picker: read "the Theme picker" there as whichever of the two lists holds
> the Theme named; "stores its id as `theme`" as "stores it as `lightTheme` or `darkTheme`, and as
> `theme` too when it is of the mode currently shown"; and "the catalog's 21 Themes" as the 8 light
> and 13 dark entries the two lists split them into (each list repeats the `High contrast` heading
> for its own mode). Hand-editing `theme` to put a dark Theme on screen is unaffected.
>
> [`docs/engineering/known-limits.md`](../known-limits.md) is the source of truth for what a Theme
> can no longer do — read it before filing anything here as a defect.

## Setup

Run [`P-BUILD`](common.md#p-build) then [`P-LAUNCH-FREE`](common.md#p-launch-free) from
[Common procedures](common.md), which also defines the terminals `T-A` and `T-B`, the
oracles `O-LOG` / `O-ADAPTERS` / `O-TRANSCRIPT` / `O-DB`, the four resets, and the
standing conventions. This area adds:

1. Start from the virgin first-run state: if `<data>\appearance.json` exists (on the free lane `<data>` is `<repo>\src\Huddle.App\App_Data`), delete it (`P-RESET-SETTINGS`). An absent file is normal and is not a broken install.
2. Set the operating system to LIGHT mode — Windows Settings → Personalisation → Colours → 'Choose your default app mode' → Light. Every dark-Theme test below is deliberately run against a light OS so that a Theme failing to apply is maximally visible. The application ignores the OS setting entirely since 2026-09-21, so this is a contrast aid, not a variable under test.
3. Open Chrome or Edge at `http://localhost:5100`, then DevTools (F12). Confirm DevTools → three-dot menu → More tools → Rendering → 'Emulate prefers-color-scheme' reads **No emulation**. Nothing in the application reads that signal any more, but a left-over emulation setting can still repaint DevTools itself and confuse a colour reading.
4. Go to `http://localhost:5100/settings/appearance` and read the path in the paragraph that begins `The selection is stored at` (the first of the two file paths on the tab; the second, under `Your avatar`, is `avatars.json` and is not this file). It should read `<data>\appearance.json`. THE ON-SCREEN PATH IS THE AUTHORITY — if it differs, edit the file the page names, not the one in these steps. Every `<data>\appearance.json` below means that file.
5. Have a plain text editor (Notepad, VS Code) ready to create and save that file. A save is picked up by a filesystem watcher about 0.5 seconds later.
6. Selecting a Theme on this tab does not force a page reload — it applies immediately through MudBlazor's `MudThemeProvider`. No test below needs F5 to see a colour change; where a test does still want a full load (to prove one is no longer required, or to check first-paint) it says so explicitly.
7. The Theme picker is a **list**, not a dropdown: every Theme is on screen under a `Light`, `Dark` or `High contrast` heading, and "select a Theme" below means clicking its row. Nothing needs opening first. Below the picker the tab also holds a `Your avatar` section; no test in this part touches it.
8. No test in this area needs a Model, and none spends money.

## Tests

### APPEARANCETHEME-01 — The Appearance tab is on the Settings tab bar and is reachable by its own route

**Free** · about 3 min

*Proves the Appearance tab exists, navigates by URL so it can be bookmarked and linked, and that an unknown tab segment falls back to Prompts rather than throwing or rendering blank.*

**Before you start**

- The application is running at http://localhost:5100.

**Steps**

1. Go to http://localhost:5100/settings.
2. Read the page heading at the top left. It must read `Settings`.
3. Count the tab buttons in the horizontal tab bar under the heading. There must be exactly six, reading `Prompts`, `Appearance`, `Personas`, `Skills`, `Team Types` and `Library` (MudBlazor renders them in upper case — `PROMPTS`, `APPEARANCE` and so on — the underlying text is unchanged), in that order.
4. Note which tab button looks selected (it carries the active styling). Note the URL in the address bar.
5. Click the `Appearance` button.
6. Read the address bar.
7. Read the tab bar again and note which button now carries the active styling.
8. Go directly to http://localhost:5100/settings/appearance by typing it into the address bar and pressing Enter.
9. Go directly to http://localhost:5100/settings/nonsense by typing it into the address bar and pressing Enter.
10. Go directly to http://localhost:5100/settings by typing it into the address bar and pressing Enter.
11. Look at the `dotnet run` console for any exception logged during the last four navigations.

**Pass if — all of these**

- The heading reads `Settings` and the tab bar holds exactly the six buttons `PROMPTS`, `APPEARANCE`, `PERSONAS`, `SKILLS`, `TEAM TYPES` and `LIBRARY`, in that order.
- Landing on /settings with no segment shows the Prompts panel with `Prompts` marked active.
- Clicking `Appearance` changes the address bar to http://localhost:5100/settings/appearance and marks `Appearance` active.
- Typing /settings/appearance directly lands on the same Appearance panel with `Appearance` active.
- /settings/nonsense renders the Prompts panel - a normal, fully styled page, not a blank page and not an error.
- No exception appears in the `dotnet run` console.

**Fail if — any of these**

- The `Appearance` button is missing from the tab bar -> the tab was never wired into Settings.razor's tabs.
- Clicking `Appearance` swaps the panel but leaves the URL at /settings -> the tab is flipping local state instead of navigating, so the tab cannot be bookmarked, linked or reloaded into.
- /settings/nonsense renders blank, 404s, or throws -> Routes.razor has no NotFound branch, so an unrecognised tab MUST resolve to Prompts; a blank page means the fallback was removed.
- /settings with no segment renders blank -> the same fallback is broken for the null case.

**Inconclusive if**

If the page will not load at all (connection refused, or an ASP.NET error page), this test is INCONCLUSIVE: the app is not running or failed to start. Go back to the console, read the startup error, restart with `dotnet run --project src/Huddle.App --urls http://localhost:5100`, and re-run. Do not record a result from a page that never rendered.

> [!NOTE]
> Cheapest test in the area and a prerequisite for every other test - if you cannot reach the Appearance tab, stop here.

### APPEARANCETHEME-02 — The Appearance tab shows its own prose and the real absolute selection-file path, and none of the Prompts tab's prose

**Free** · about 4 min

*Proves the intro paragraph and the file path shown are the current, post-MudBlazor wording, and that the two tabs' prose still never bleeds into each other.*

**Before you start**

- The application is running.
- No appearance.json yet (the virgin state from setup) - not required, but it makes the 'file does not exist' sentence true as written.

**Steps**

1. Go to http://localhost:5100/settings/appearance.
2. Read the paragraph above the Theme picker. Compare it word for word with: `Pick a theme. Each theme is either a light or a dark one, so choosing it sets the application's light or dark colours too.`
3. Read the credit line: the paragraph below the picker (and, only while a Custom Accent theme is selected, below the `Accent colour` picker too). Compare it word for word with: `Custom Accent lets you pick your own accent colour, adjusted if needed so it stays readable. Every other theme except Huddle Light and Huddle Dark is one of the colour themes bundled with Visual Studio Code, mapped onto this application's palette. Visual Studio Code and its default themes are Microsoft's, under the MIT licence; Solarized is Ethan Schoonover's and Monokai is Wimer Hazenberg's.`
4. Read the paragraph directly after the credit line. Compare it word for word with: `The selection is stored at <path>. The file does not exist until you save a choice here, so it being absent is expected, not a bug.` (It is not the last paragraph: the `Your avatar` section and its own `Your avatar is stored at` paragraph follow it.)
5. Read the path rendered in monospace in the selection paragraph. Write it down. It must be an absolute path, e.g. `<repo>\src\Huddle.App\App_Data\appearance.json` written out in full.
6. Scan the whole Appearance panel for the sentence `A prompt is one piece of wording this application sends to a model` and for any mention of `prompts.json`, and for any mention of a token name such as `--font-chat` or an "Overrides" heading.
7. Click the `Prompts` button in the tab bar.
8. Confirm the Prompts panel DOES carry the sentence beginning `A prompt is one piece of wording this application sends to a model` and its own `Overrides are stored at ...prompts.json` paragraph.
9. Click `Appearance` again and confirm those two Prompts paragraphs are gone.

**Pass if — all of these**

- The Appearance panel's three paragraphs — intro, credit line, selection-file path — match the quoted text word for word.
- The path shown is absolute and ends in `\appearance.json`.
- Neither the Prompts intro sentence nor any mention of `prompts.json` appears anywhere on the Appearance panel, and there is no token name and no "Overrides" section — that whole layer is gone, not merely hidden.
- Both of the Prompts paragraphs DO appear on the Prompts panel.
- The credit line sits directly below the Theme picker (and the Accent colour picker, when shown), names Visual Studio Code, excepts both `Huddle Light` and `Huddle Dark`, and attributes only Solarized and Monokai. It must not attribute Abyss, Kimbie Dark, Red, Quiet Light, Monokai Dimmed or Tomorrow Night Blue to anyone — those ship in VS Code with no third-party attribution on disk, so naming an author would be inventing one.

**Fail if — any of these**

- The Prompts intro or the prompts.json path paragraph appears on the Appearance panel -> a shared paragraph is sitting above the tab bar instead of inside the Prompts case.
- Any token name or an "Overrides" heading is still shown -> stale prose from the retired per-token system was left behind.
- The path shown is relative (e.g. `App_Data\appearance.json`) -> the Human cannot find the file, and every later test in this area would be edited against a guessed location.

**Inconclusive if**

If the path shown differs from `<repo>\src\Huddle.App\App_Data\appearance.json`, that is NOT automatically a failure - the content root or Team:DataDir may differ on this machine. Record the path you actually see, treat it as the authority for every later test, and mark this step PASS on the absoluteness of the path rather than on its exact value.

> [!NOTE]
> Write the on-screen path down now. Every later test in this area edits that exact file, and 'I edited the wrong App_Data' is the single most common false bug report in this area.

### APPEARANCETHEME-03 — The Theme picker offers the whole built-in catalog, grouped, and is the tab's only control

**Free** · about 3 min

*Proves the picker is rendered from code (`ThemeCatalog.Grouped`) rather than a hand-maintained second list, in the declared order and under the declared headings — and that the light/dark control that used to sit beside it is gone ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)). The catalog holds twenty-one entries: seventeen imported on 2026-09-16 ([ADR-0016](../../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md)) plus this application's own four, the Huddle pair and the Custom Accent pair.*

**Before you start**

- The application is running.
- You are on http://localhost:5100/settings/appearance.

**Steps**

1. Find the Theme picker on the Appearance panel. It is a list, already showing its entries — nothing to open.
2. Read every heading and every row top to bottom and write the list down. Scroll the list if it does not all fit.
3. Look over the whole panel for any control offering light, dark or system, a select or a toggle. Note the other control that is there: the `Your avatar` section. Click `Custom Accent Light` and note that an `Accent colour` picker appears below the list, then click `Huddle Light` and note that it goes away. Both clicks write `<data>\appearance.json`; delete it afterwards (`P-RESET-SETTINGS`).

**Pass if — all of these**

- The list holds exactly twenty-one Themes under exactly three headings, in this order:
  - **Light** — `Huddle Light`, `Custom Accent Light`, `Light 2026`, `Light Modern`, `Light+`, `Quiet Light`, `Solarized Light`
  - **Dark** — `Huddle Dark`, `Custom Accent Dark`, `Dark 2026`, `Dark Modern`, `Dark+`, `Abyss`, `Kimbie Dark`, `Monokai`, `Monokai Dimmed`, `Red`, `Solarized Dark`, `Tomorrow Night Blue`
  - **High contrast** — `Dark High Contrast`, `Light High Contrast`
- `Huddle Light` is **first** under `Light`, and `Light` is the first heading. Three call sites read `BuiltIn[0]` to mean "the default Theme", so a Theme appearing above it is a real defect, not a cosmetic one.
- The two High Contrast Themes are under their **own** heading, not under `Dark` and `Light`. `ThemeGroup` is deliberately not `ThemeMode`.
- There is no `Dark (Visual Studio)` and no `Light (Visual Studio)`. Those two were deliberately not imported: their palettes resolve byte-identically to `Dark+` and `Light+`, because upstream they differ only in `tokenColors` — syntax highlighting, which this application does not render.
- **There is no light/dark/system control.** Besides the intro paragraph, the picker and the credit line, the panel holds only: the `Accent colour` picker (shown only while `Custom Accent Light` or `Custom Accent Dark` is selected), the selection-file path, the `Your avatar` section and its file path.

**Fail if — any of these**

- A light/dark/system control of any kind is present, or the `Accent colour` picker shows for a theme other than the two Custom Accent ones -> ADR-0017 was reverted, or half-reverted; the conflict this whole change removed is back.
- A row's label is a raw id such as `huddle-dark` rather than `Huddle Dark` -> an id is leaking into the display.
- The list shows no Themes, or throws -> the catalog failed to enumerate.
- A Theme appears under the wrong heading — a light Theme under `Dark`, or either High Contrast Theme outside `High contrast` -> `ThemeGroup` and the palette disagree.
- `Dark (Visual Studio)` or `Light (Visual Studio)` appears -> somebody re-added a duplicate of `Dark+` / `Light+`; see ADR-0016.
- A Theme appears above `Huddle Light` -> `BuiltIn[0]` no longer means the default Theme, which silently changes the fallback for an unknown id.

**Inconclusive if**

If the list renders but the page is visibly still loading (the Blazor circuit has not connected yet), wait five seconds, reload with F5, and read it again. Record INCONCLUSIVE only if it still will not render after a reload.

> [!NOTE]
> Labels are what you see; ids are what get stored (`huddle`, `huddle-dark`, `custom-accent`, `custom-accent-dark`, `dark-modern`, `solarized-light`). Test 04 checks the id half. An imported Theme's id is its label lowercased and hyphenated, with two worth knowing: `Dark+` stores `dark-plus` because an id must match `[a-z0-9-]`, and `Huddle Light` stores the bare `huddle` — the id it has always had, kept so a file written before the Theme was split in two still resolves.

### APPEARANCETHEME-04 — Choosing a Theme stores its id and applies immediately, with no page reload

**Free** · about 5 min

*Proves the selection reaches the file as an id (never a display label), and that MudThemeProvider applies it in place — the full-document-load mechanism the old CSS-based system needed no longer exists.*

**Before you start**

- The application is running.
- appearance.json does NOT exist (delete it if it does, per setup).

**Steps**

1. Go to http://localhost:5100/settings/appearance.
2. Open DevTools with F12 and click the `Network` tab. Click the 'Clear' button (circle-with-slash icon) to empty the request list. Set the filter to `Doc`.
3. Click `Huddle Dark` in the Theme picker, under the `Dark` heading.
4. Watch the page: does it repaint immediately with no spinner, no blank frame and no new entry in the `Doc`-filtered Network list?
5. Read the address bar and confirm it is unchanged and still shows `Appearance` active in the tab bar.
6. Look at the page: is the application now dark?
7. In PowerShell run `type <data>\appearance.json`, with `<data>` replaced by the folder of the path you wrote down in test 02.
8. Read the file contents exactly, including line breaks.

**Pass if — all of these**

- No `Doc` request appears in the Network panel — there is no page load at all.
- The whole application repaints dark within a fraction of a second, still on /settings/appearance with `Appearance` active in the tab bar and `Huddle Dark` marked selected in the picker.
- The file now exists and reads exactly:
```
{
  "theme": "huddle-dark"
}
```
  **There is no `dark` key, and that is correct** — the light/dark preference was
  retired on 2026-09-21; the Theme carries its own palette
  ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)).
- The stored value is the lowercase id `huddle-dark`, NOT the label `Huddle Dark`.

**Fail if — any of these**

- The browser performs a full document load (a `Doc` request appears, or the tab's spinner turns) -> the old reload mechanism is back; it is no longer needed and no longer correct, because `<head>` is no longer where the theme lives.
- No colour change at all -> `MudThemeProvider`'s bound `IsDarkMode` is not being updated from the store.
- The file contains `"theme": "Huddle Dark"` -> the display label is being persisted instead of the id.
- The file contains a `dark` key that the application just wrote -> the retired preference is being re-added. (A `dark` key you put there yourself by hand is a different matter: it is kept deliberately, as an unknown key. See test 17.)
- The file is written on one line, or contains `\uXXXX` escapes instead of plain characters -> the writer is not using indented, relaxed-escaping options.
- The file is not written at all -> the save path is broken.

**Inconclusive if**

If DevTools was opened AFTER the selection, redo it: clear the Network panel, select `Huddle Light`, then select `Huddle Dark` again, and read the result from that second change.

> [!NOTE]
> No page reload on a Theme change is BY DESIGN now — the opposite of what this same test asserted before the MudBlazor migration. Do not report the absence of a reload as a defect; report its PRESENCE as one.

### APPEARANCETHEME-06 — The saved Theme is shown as selected in the picker after a full load

**Free** · about 4 min

*Proves the page render and the picker agree about the same fact, so the next change the Human makes does not start from a wrong value.*

**Before you start**

- The application is running.

**Steps**

1. Set the file to exactly `{ "theme": "huddle-dark" }` and save it. Wait one second for the watcher.
2. Go to http://localhost:5100/settings/appearance and press Ctrl+Shift+R.
3. Watch the picker from the moment the page paints until it has fully settled (about two seconds). Note which row is marked selected.
4. Confirm the page itself is dark.
5. Set the file to exactly `{ "theme": "solarized-light" }`, save, wait one second, and press Ctrl+Shift+R. Read the picker.
6. Delete the file entirely, wait one second, then press Ctrl+Shift+R. Read the picker.

**Pass if — all of these**

- With `"theme": "huddle-dark"` in the file, the picker marks `Huddle Dark` selected and the page is dark.
- With `"theme": "solarized-light"` in the file, it marks `Solarized Light` and the page is light, on Solarized Light's own tan ground.
- With no file, it marks `Huddle Light` — the catalog's default entry — and the page is light.
- The file value, the marked row and the page's colours agree in every case, at every moment after the page has settled.

**Fail if — any of these**

- The picker PERSISTENTLY marks a row that disagrees with the page's own colours after the page has fully settled -> the render and the control disagree about the same fact. This is the exact shape of the defect ADR-0017 removed, so treat it as serious.
- A dark Theme is selected and the page is light, or the reverse -> `MainLayout` is not deriving `IsDarkMode` from `ThemeDescriptor.Mode`.

**Inconclusive if**

If the marked row is ambiguous — MudBlazor's selected-row styling is a background tint, not a tick — read `appearance.json` instead and confirm the page's colours match that Theme.

### APPEARANCETHEME-07 — A dark Theme shows no flash of a light palette on a hard reload, ever

**Free** · about 6 min

*Proves the Theme is resolved before the first frame paints — `MainLayout.OnInitialized` reads it synchronously from `AppearanceStore`. Since 2026-09-21 this holds for **every** Theme with no exception: the JavaScript round trip that made a first-paint flash possible under `System` is gone along with `System` itself ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)), so any flash seen here is now a defect.*

**Before you start**

- The operating system is in LIGHT mode (per setup), so a flash would be maximally visible.
- The file reads `{ "theme": "huddle-dark" }`.
- The application is running.

**Steps**

1. Go to http://localhost:5100/ and press Ctrl+Shift+R five times in a row, watching the page each time.
2. Go to http://localhost:5100/teammates and press Ctrl+Shift+R five times, watching each time.
3. Go to http://localhost:5100/settings/appearance and press Ctrl+Shift+R five times, watching each time.
4. Open DevTools -> Network and set the throttling dropdown to `Slow 4G`.
5. Press Ctrl+Shift+R on each of those three routes again and watch the paint closely - the slow load exaggerates any flash into something you cannot miss.
6. If you think you saw a flash: open DevTools -> Performance, click the reload-and-record button, and inspect the screenshot filmstrip frame by frame for the first painted frame.
7. Set throttling back to `No throttling`.

**Pass if — all of these**

- On every reload of every route, at both speeds, the page's first painted frame is already dark.
- No white or light-coloured frame appears at any point, not even for one frame in the Performance filmstrip.

**Fail if — any of these**

- A white flash appears before the dark paint -> `AppearanceStore.Current` is not being read (or not being applied) before `MainLayout`'s first render. There is no longer a documented exception to this: if anything reintroduced a post-render JavaScript read of the device preference, that is the defect.

**Inconclusive if**

If you cannot tell whether what you saw was a flash or just the browser's own blank-page-before-first-paint (white by default on a light OS), resolve it with the DevTools Performance filmstrip rather than guessing.

> [!NOTE]
> Run this with the OS in LIGHT mode. With a dark OS, the browser's own blank page is dark too and the test proves nothing.
>
> A flash is a finding on every route and every Theme: the application no longer reads the device's colour setting, so nothing is left to excuse one.

### APPEARANCETHEME-09 — The choice is per installation, not per browser

**Free** · about 6 min

*Proves the choice lives in a file on the server and not in localStorage or a cookie.*

**Before you start**

- The application is running.
- The file reads `{ "theme": "huddle-dark" }` and Chrome shows the application as dark.

**Steps**

1. In Chrome, confirm http://localhost:5100/ is dark.
2. Open a completely different browser (Microsoft Edge or Firefox) and go to http://localhost:5100/ with no other setup.
3. Observe whether it is dark.
4. In Chrome, open a new Incognito/Private window (Ctrl+Shift+N) and go to http://localhost:5100/.
5. Observe whether it is dark.
6. In Chrome, open DevTools -> Application tab -> Storage -> Local Storage -> http://localhost:5100 and read every key present. Then check Cookies for the same origin.
7. Still in DevTools -> Application, click `Clear site data`, then reload http://localhost:5100/ with Ctrl+Shift+R.
8. Observe whether it is still dark.
9. Go to the `dotnet run` console and press Ctrl+C to stop the server. Wait for the prompt to return.
10. Run `P-LAUNCH-FREE` again in `T-A` and wait for `Now listening on`.
11. Reload http://localhost:5100/ and observe.

**Pass if — all of these**

- The second browser shows the application dark immediately, with no setup.
- The private/incognito window shows it dark.
- Local Storage and Cookies for localhost:5100 contain NOTHING about themes, appearance or colours.
- Clearing all site data and reloading leaves the application dark.
- Stopping and restarting the application leaves it dark.

**Fail if — any of these**

- The second browser or the private window shows the default light Theme -> the choice has moved into localStorage or a cookie.
- Clearing site data resets the theme -> same defect.
- The choice does not survive an application restart -> it is not reaching the file on disk.

**Inconclusive if**

If the second browser shows light, first check the file still says `"theme": "huddle-dark"` and that Chrome is genuinely reading the same install (same port, same process).

> [!NOTE]
> This is the positive proof of a documented limit: a second browser, a private window and a phone on the same install all see the same choice.

### APPEARANCETHEME-14 — An unknown theme id is a warning, logged on every rebuild and save, never shown on the tab, and the file keeps saying it

**Free** · about 8 min

*Proves an unrecognised or ill-shaped value degrades to the default Theme with a LOG-ONLY report, never a crash, and that the Human's value is preserved. Unlike the old system, nothing is reported on the Appearance tab any more — see [known-limits.md](../known-limits.md): "A bad Theme id is logged, not shown."*

**Before you start**

- The application is running.
- You can see the `dotnet run` console.

**Steps**

1. Set the file to exactly `{ "theme": "dracula" }` and save. Press Ctrl+Shift+R on http://localhost:5100/.
2. Observe the application: does it render normally on the default Theme?
3. Go to http://localhost:5100/settings/appearance. Read which row the picker marks selected. Look for ANY on-page mention of `dracula` being rejected — a problem list, an alert, anything.
4. Read the most recent warning in the `dotnet run` console.
5. Run `type <data>\appearance.json` and confirm it still says `dracula`.
6. Repeat the cycle for `{ "theme": "Huddle-Dark" }` (wrong case — the id comparison is ordinal), checking the page, the picker, the console and the file each time.
7. Repeat for `{ "theme": "" }` (an empty string).
8. Repeat for `{ "theme": 42 }` (the wrong JSON type).
9. Repeat for `{ "dark": "midnight" }` — the retired key, carrying a value that was never valid even when it was read. Nothing may be logged about it at all.

**Pass if — all of these**

- In every case the application renders normally on the default Theme, `Huddle Light`, with no crash and no blank page.
- The picker marks `Huddle Light` selected in every case.
- NOTHING is reported anywhere on the Appearance tab for any of these cases — this is the documented behaviour, not a gap in this test.
- The console carries a warning for each bad `theme` (the app re-resolves the file on every start, every watcher rebuild and every save, and logs each time, so a second copy of the same line is expected, not a failure), reading `Appearance file '<path>' selects theme 'dracula', which is not a known theme; the default theme is used instead and the file is left unchanged.` (the quoted value is the one in the file: `Huddle-Dark`, an empty string, or `42`)
- **Step 9 logs nothing at all.** `dark` is an unknown top-level key now, and an unknown key is kept without comment — it is not validated, so there is nothing to warn about.
- appearance.json still contains the bad value, unchanged, in every case — the app never rewrites it.

**Fail if — any of these**

- The application refuses to start, throws, or renders blank -> an unknown value must be a warning, never a failure.
- The application REWRITES the file to remove or correct the unknown value -> leaving it untouched is what lets the choice come back intact once it is fixed or once a matching theme is added.
- Something IS shown on the Appearance tab about the bad value -> a regression put the old per-token reporting UI back, or invented a new one; per known-limits, this surface is deliberately log-only now.
- Nothing is logged either -> now the failure is genuinely undiagnosable.
- Step 9 logs a warning about `dark` -> the retired key is still being validated; it should be as invisible as any other unknown key.

**Inconclusive if**

For the numeric/null case, the log line may quote the raw JSON rather than a string; if the exact wording differs from the string cases, record the wording you see rather than failing on it.

> [!NOTE]
> Keeping an unknown value in the file, and reporting it ONLY to the log, are both deliberate — see known-limits.md. This is a weaker guarantee than the old design's on-tab report, and is called out there as such; do not "fix" it here.

### APPEARANCETHEME-15 — Malformed JSON keeps the last good Theme while the app runs, falls back wholesale at the next start, is logged, and the file is left untouched

**Free** · about 9 min

*Proves the app neither crashes nor repairs the Human's file when appearance.json is not valid JSON, and records the two different behaviours: a running app keeps the Theme it already resolved (`AppearanceStore.cs:405-425`), while a fresh start has nothing to keep and uses the default (`:344-360`). Compare PROMPTSSETTINGS-31 and -32, which split the same way.*

**Before you start**

- The application is running with a working Theme in place: the file reads `{ "theme": "huddle-dark" }` and the page is dark.
- You can see the console of the terminal that launched the app (`T-A`).

**Steps**

1. Open http://localhost:5100/settings/appearance and confirm the application is dark and the picker marks `Huddle Dark`.
2. Set `<data>\appearance.json` to the deliberately broken text `{"theme":"huddle",` (truncated, no closing brace) and save it.
3. Wait about 2 seconds, then read the console in `T-A`.
4. Observe the application without reloading: it stays dark and the picker still marks `Huddle Dark`. Then press Ctrl+Shift+R and confirm it is still dark (a reload does not re-read the file; the resolved Theme is held by the running app).
5. Run `type <data>\appearance.json` and confirm it is exactly the broken text you wrote.
6. Repeat steps 2 to 5 with a top-level array: set the file to `[]`, save, wait 2 seconds, and check the same three things (page and picker, console, file).
7. Put the broken text `{"theme":"huddle",` back, then stop the app (`P-STOP`) and start it again with [`P-LAUNCH-FREE`](common.md#p-launch-free). Do not run a reset: the broken file must stay.
8. Open http://localhost:5100/settings/appearance. Observe the application: it is on the default Theme, `Huddle Light`, and the picker marks `Huddle Light`. Read the console from the start of the launch.
9. Run `type <data>\appearance.json` again and confirm it is still exactly the broken text.
10. Now test the mid-write race: put a valid file back (`{ "theme": "huddle-dark" }`), open it in your editor, and press Ctrl+S repeatedly - ten saves in about five seconds - while watching the open /settings/appearance page.
11. Watch for any flicker of the picker's marked row back to `Huddle Light`.

**Pass if — all of these**

- After the malformed edit in steps 2 and 6, the running application stays on `Huddle Dark` and the picker keeps marking it.
- The console carries a warning of the form `Could not parse appearance file '<path>' after a filesystem change, even after retrying; keeping the previously resolved appearance...` for each malformed edit.
- After the restart in step 8 the application renders normally on `Huddle Light`, the picker marks `Huddle Light`, and the console carries the warning `Could not parse appearance file '<path>'; falling back to the default appearance.`
- appearance.json is exactly the broken text you wrote in steps 5 and 9 - unmodified, unrepaired.
- Repeated rapid saves of a VALID file do not flicker the picker.

**Fail if — any of these**

- The application crashes, throws, or fails to start on malformed JSON -> a bad file must never prevent the app running.
- The running application drops to `Huddle Light` when the file is broken -> a mid-write race would flicker the Human's Theme away.
- The application rewrites or repairs the file -> the Human's text is being destroyed.
- NO warning appears in the console -> there is then no signal anywhere at all.
- Rapid valid saves flicker the picker back to `Huddle Light` -> the mid-write retry is not working.

**Inconclusive if**

If the console scrolled past and you cannot find the warning, re-save the broken file and watch the console live before recording INCONCLUSIVE.

### APPEARANCETHEME-16 — Saving a choice while the file is malformed replaces its contents with valid JSON

**Free** · about 5 min

*Records that the otherwise-absolute promise 'the file is left exactly as it was' does not hold across a Save issued while the on-disk file cannot be parsed — the save re-reads the file, gets an empty document back, and writes cleanly over it.*

**Before you start**

- The application is running.

**Steps**

1. Set the file to deliberately broken JSON, exactly:
{"theme":"huddle-dark",,}
and save it.
2. Copy that exact text into your test notes as the BEFORE state.
3. Press Ctrl+Shift+R on http://localhost:5100/settings/appearance.
4. Read the `dotnet run` console and confirm a `Could not parse appearance file` warning appeared.
5. In the Theme picker, click `Huddle Dark`.
6. Run `type <data>\appearance.json` and copy the exact contents into your notes as the AFTER state.
7. Compare BEFORE and AFTER.

**Pass if — all of these**

- The save does not throw; no error banner appears and no unhandled exception is logged.
- The file afterwards is VALID, indented JSON reading:
{
  "theme": "huddle-dark"
}
- The application is dark afterwards.

**Fail if — any of these**

- The save throws, or shows an error banner -> saving over a bad file must still work.
- The file afterwards is invalid JSON -> the writer produced something the app itself cannot read back.
- The application is not dark afterwards -> the save did not take effect.

**Inconclusive if**

This test has no 'correct' answer to assert against beyond not-throwing. Mark PASS if the save worked and the file is valid; mark INCONCLUSIVE only if you could not capture both file states.

### APPEARANCETHEME-17 — Save re-reads the file under its write lock, so an unrelated hand-added key survives — and so does the retired `dark` key

**Free** · about 6 min

*Proves the save edits only `theme`, leaving anything else in the document alone. This is also the whole migration story for a file written before 2026-09-21: there is no migration code, because `dark` is now just another unknown key ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)).*

**Before you start**

- The application is running.

**Steps**

1. Set the file to exactly:
{
  "theme": "solarized-dark",
  "dark": "light",
  "note": "keep me"
}
and save it. This is precisely the contradictory file the old two-control design could produce: a dark Theme and a light preference.
2. Press Ctrl+Shift+R on http://localhost:5100/settings/appearance so the app picks it up.
3. **Confirm the application is on Solarized Dark's own dark palette**, not a light one, and that the picker marks `Solarized Dark`. The `dark` key is ignored entirely.
4. Click `Huddle Light` in the picker.
5. Run `type <data>\appearance.json` and read the exact contents.
6. Confirm `theme` is now `"huddle"`, and that BOTH `dark` and `note` are still present, untouched.
7. Confirm the file stays indented and readable, with plain characters and no `\uXXXX` escapes.

**Pass if — all of these**

- In step 3 the application is dark, on Solarized Dark. The `"dark": "light"` beside it changes nothing.
- After the Save, the file contains `"theme": "huddle"`, `"dark": "light"` and `"note": "keep me"` — all three.
- Both the `dark` and `note` keys are byte-for-byte what you wrote.
- The file stays indented and human-readable.

**Fail if — any of these**

- Step 3 renders a LIGHT page -> the retired `dark` key is still being read, which is the exact defect ADR-0017 removed.
- The `note` key is silently deleted -> an unknown top-level key must be kept; the save is overwriting the whole document rather than re-reading and editing one key.
- The `dark` key is silently deleted -> it is an unknown key now and gets the same protection as any other. Removing it would also throw away the Human's old answer, which a future device-preference feature would want.
- The file is clobbered down to just `theme` -> same defect, the save is not re-reading the file before editing.
- Characters come back as escaped sequences -> the writer is using the protocol JSON options rather than the relaxed, human-editable ones.

**Inconclusive if**

If your editor holds the file open with a lock while the app tries to write, close the editor and repeat before recording a result.

> [!NOTE]
> There is no UI action that ever removes the `theme` key — the picker always has a real selection, and clicking a row always writes its id. The `theme` key can only be absent in a file nobody has saved from this UI yet, or one hand-edited to omit it.

### APPEARANCETHEME-18 — A hand-edit updates the open tab live, and now the page's colours change too — no full load required

**Free** · about 6 min

*The old area had a test proving colours did NOT change without a full load, because `<head>` was server-rendered and out of Blazor's reach. That constraint is gone: `MainLayout` subscribes to `AppearanceStore.AppearanceChanged` and repaints through `MudThemeProvider`'s own parameters. This test proves the opposite of what it once did — record that inversion, don't assume the old expectation still holds.*

**Before you start**

- The application is running.
- http://localhost:5100/settings/appearance is open in the browser.
- An editor is open on `<data>\appearance.json`.

**Steps**

1. Set the file to exactly `{ "theme": "huddle" }` and save. Confirm the page is light and the picker marks `Huddle Light`.
2. Now WITHOUT touching the browser at all, change the file in your editor to exactly `{ "theme": "huddle-dark" }` and save it.
3. Keep your eyes on the browser for the next 2 seconds. Note whether the picker's marked row moves to `Huddle Dark` by itself AND whether the page's COLOURS change, with no click and no reload.
4. Click `Teammates` in the sidebar (an in-app navigation). Confirm the colours stay dark.
5. Check the `dotnet run` console for any unhandled exception during the above.

**Pass if — all of these**

- Within about half a second of the save, BOTH the picker's marked row and the page's actual colours update by themselves — no reload, no click, no navigation.
- In-app navigation afterwards keeps the same (correct) colours.
- No unhandled exception appears in the console.

**Fail if — any of these**

- The select updates but the colours do not -> `MainLayout` is not subscribed to the same store the tab reads, or its `StateHasChanged` is not reaching `MudThemeProvider`'s parameters. This IS a regression now, even though it was the designed behaviour before this migration.
- The page throws, or the console logs an unhandled exception on disconnect afterwards -> a leaked subscription to a singleton store never dies.

**Inconclusive if**

If nothing updates within 2 seconds, wait another 3 seconds before judging — the watcher debounces for half a second. If it still has not updated, check the console for a `FileSystemWatcher reported an error` warning.

> [!NOTE]
> Do NOT reuse the old area's framing here ("a tester sees the dropdown update but not the colours and files a false bug"). That framing described the retired design. Under MudTheme, colours not changing live IS the bug.

### APPEARANCETHEME-20 — Two browser windows stay in step, colours included — the change propagates through the file to every open circuit

**Free** · about 6 min

*Proves the store is a single instance raising its change event after the write, that a second circuit receives it, applies it visually with no reload, and does not leak or throw. Unlike the pre-migration version of this test, BOTH the control and the colours are now expected to update in the second window.*

**Before you start**

- The application is running.

**Steps**

1. Open http://localhost:5100/settings/appearance in two browser windows and arrange them side by side. Call them A and B.
2. Confirm both pickers mark the same Theme and both pages look the same.
3. In window A, click `Huddle Dark` in the Theme picker.
4. Watch window A: it should repaint dark immediately, with no page load.
5. Watch window B for 2 seconds WITHOUT touching it.
6. Note whether window B's picker moves its marked row to `Huddle Dark` by itself, AND whether window B's colours change too.
7. In window B, click `Huddle Light` in the picker. Watch window A for 2 seconds without touching it.
8. Close window B entirely. Watch the `dotnet run` console for 10 seconds for any unhandled exception on circuit disposal.
9. Interact with window A (click `Huddle Dark` again) and confirm it still works after B was closed.

**Pass if — all of these**

- Window A repaints dark immediately on the selection, no reload.
- Window B's picker (its marked row) AND its colours flip to `Huddle Dark` by themselves within about half a second.
- The reverse direction works too (B's selection of `Huddle Light` updates both A's marked row and A's colours).
- Closing window B logs no unhandled exception, and window A keeps working afterwards.

**Fail if — any of these**

- Window B's marked row updates but its colours do not (or vice versa) -> the two are now reading from different places; they must move together, because both come from the same `AppearanceChanged` event.
- Window B throws an error -> a second subscriber is not being handled safely.
- The console logs an unhandled exception when window B closes -> a leaked subscription to the singleton store.

**Inconclusive if**

If both windows are in the same browser process and one is backgrounded, the browser may throttle its timers and delay the update - bring window B to the front and repeat before recording a slow update as a failure.

### APPEARANCETHEME-21 — Re-selecting the value that is already selected is harmless and produces no reload

**Free** · about 3 min

*Checks the no-op path does not loop, error, or destroy the file.*

**Before you start**

- The application is running.
- The file reads `{ "theme": "huddle-dark" }` and the app is dark.

**Steps**

1. Go to http://localhost:5100/settings/appearance and confirm the picker marks `Huddle Dark`.
2. Click `Huddle Dark` again.
3. Watch the page for 5 seconds. Confirm there is no page load (DevTools -> Network, `Doc` filter, stays empty) and no repeated repaint flicker.
4. Run `type <data>\appearance.json` and read the contents.
5. Check the `dotnet run` console for errors, and scroll to the bottom of the page for the pale yellow `An unhandled error has occurred.` banner.

**Pass if — all of these**

- No page load happens, and the page does not flicker or repaint repeatedly.
- The file still reads `{ "theme": "huddle-dark" }`, OR it reads `{}` with the picker back on `Huddle Light` and the page light. Both are correct: MudBlazor's single-selection list clears the selection when the selected row is clicked again, and the store reads a cleared selection as "no Theme chosen", which resolves to the catalog default. Record which branch you saw.
- No error banner, no console error.

**Fail if — any of these**

- The page reloads, or repaints in a visible loop -> the save-and-apply path is retriggering itself.
- An error banner appears at the bottom of the page -> an unhandled error occurred during the no-op save.

**Inconclusive if**

If the click produced neither of the two outcomes above — no file change AND no return to `Huddle Light` — read the file before deciding; the list may simply not have registered the click.

### APPEARANCETHEME-22 — Dark mode walked across every page and every state - the acceptance test for the whole item

**Free** · about 20 min

*Hunts for the ONE element that did not switch: every literal colour that survived migration onto `--mud-*` variables shows up here and nowhere else, because no automated test in the suite renders a browser.*

**Before you start**

- The application is running in the free lane with Teammates: `P-LAUNCH-UAT` has been run and Ada and Bo are Online.
- The file reads exactly `{ "theme": "huddle-dark" }`.
- The operating system is in LIGHT mode, so anything that failed to switch is obvious.
- For the rejected-persona part, you will hand-edit a Persona file (see steps). If you create or edit a Persona through the UI, set Model = Haiku and Effort = low - the standing convention. Nothing here needs a model and nothing here costs money.

**Steps**

1. Press Ctrl+Shift+R on http://localhost:5100/ and confirm the whole application is dark.
2. In DevTools -> Elements, select `<body>` and read Computed `background-color`. It must be `rgb(28, 28, 27)` (Huddle Dark's Background, `#1c1c1b`; `HuddleTheme.cs:82`).
3. (a) With no room selected, inspect: the page body; the sidebar drawer; the `New chat` button; the room list; and the two nav links `Teammates` and `Settings`. Note anything light.
4. (b) Click `New chat`. Inspect the panel: the agent rows for `Ada` and `Bo`, their coloured status dots, and the `Start chat` button.
5. (c) Close the panel and click the `Ada` Room in the sidebar. In the Room, inspect: the chat header, the member line, each message row (sender name, timestamp, body), the composer textarea including its placeholder `Message {Room name}… (/invite @Teammate)`, and the budget note line.
6. (d) Type `hello` and press Enter. WATCH THE DRAFT STREAM IN: inspect the streaming row, its caret, the stop control (a small outlined button), and any tool-activity line while they are on screen.
7. (e) Type a fenced code block: type three backticks, then `code`, then three backticks, and press Enter. Inspect the rendered code block - it exercises the monospace token, still declared in `wwwroot/app-vars.css` since MudBlazor's theme has no monospace equivalent.
8. (f) Click `Teammates` in the sidebar. Inspect the tiles, the avatar monograms, the team filter select, and the `New teammate` button.
9. (g) Click a teammate tile to open the card. It is now a real dialog with a dimmed, scrollable-page-behind backdrop: inspect the dialog surface, the scrim behind it, the `Persona`, `Model` and `Effort` sections, and the Edit / Open / Remove buttons (and the Close icon). Close the dialog with Escape.
10. (h) Create a rejected persona file so the rejected block renders: in `C:\seeds\huddle\uat\data\Teammates\`, create `broken.md` containing exactly:
---
name: broken
---
A persona with no title.
Save it, then press F5 on /teammates.
11. (i) Find the section headed `Files that didn't load` and inspect its heading, the path and the reason text.
12. (j) Go to http://localhost:5100/settings/prompts and inspect the whole panel, then click `Appearance` and inspect that panel.
13. Now check native controls specifically: open every MudSelect popup you can find (the team filter, the card's Model and Effort selects) and check the popup itself is dark, not a white system menu. Check the Theme picker's own list and its group headings too — it is a `MudList`, not a popup, so it is always on screen.
14. Click into the composer textarea and check it is dark.
15. Scroll any scrollable area (the room list, the message list) and check the SCROLLBAR renders dark.
16. For any element you suspect: select it in DevTools -> Elements, read the suspect colour property in Computed, then click through to the declaring rule. Note whether the value is a literal hex or a `var(--mud-palette-...)` reference.
17. Finally, scroll to the very bottom of each page and check for the pale yellow `An unhandled error has occurred.` banner.
18. Delete `broken.md` afterwards.

**Pass if — all of these**

- Every surface, border, text colour and control listed in (a) through (j) uses the dark palette.
- Computed `background-color` on `<body>` is `rgb(28, 28, 27)`.
- Native controls are dark: every select popup, the textarea, and the scrollbars.
- No white or pale surface appears anywhere except the two documented exemptions in the notes.
- No element renders blank or with no colour at all.
- No pale yellow error banner at the bottom of any page.

**Fail if — any of these**

- ANY element that stays light -> a colour literal survived the migration onto `--mud-*` variables; that is the entire point of this step.
- A white page ground behind everything -> the body background is missing or wrong.
- A light hover highlight on a sidebar row, or a light scrim behind the teammate dialog -> a literal escaped in that specific rule.
- White scrollbars or a white native select popup -> `color-scheme` is not reaching `:root`.
- A blank or empty surface where a colour should be -> a `--mud-*` variable resolved to nothing.

**Inconclusive if**

If the sidebar has no `Ada` Room or `New chat` lists no agents, parts (c), (d) and (e) are INCONCLUSIVE - the app was not launched with `P-LAUNCH-UAT`; relaunch it (the seeder run is the reset) and repeat. If /teammates shows `No Personas yet. Choose New teammate to add one.`, parts (f) and (g) are INCONCLUSIVE until you add a Persona.

> [!NOTE]
> TWO THINGS MUST NOT SWITCH AND ARE NOT BUGS. (1) The `An unhandled error has occurred.` banner stays LIGHT YELLOW in dark mode on purpose - it is shown when the application has already failed, the one moment a Theme cannot be trusted. (2) The Persona's Markdown source shown on the teammate dialog deliberately keeps the inherited font rather than the monospace token. Free: Ada runs on the mock adapter and needs no Claude turn.

### APPEARANCETHEME-23 — Hover, selection and focus surfaces all switch, and the four old hover colours are still one

**Free** · about 8 min

*Four different pale colours meant 'this row is under the pointer' before tokenisation, then collapsed to one hover token, then that token moved onto `--mud-*` variables in this migration. Any hover that now differs from the others is a literal that escaped.*

**Before you start**

- The application is running with `{ "theme": "huddle-dark" }` and the operating system in LIGHT mode.
- At least one Room exists and at least one Teammate tile is visible (`P-LAUNCH-UAT` supplies Ada and Bo and their Rooms for free; if you add a Persona, use Model = Haiku and Effort = low).

**Steps**

1. Go to http://localhost:5100/ and hover a room row in the sidebar. Note the highlight colour.
2. In DevTools -> Elements, select that room row, click the `:hov` button in the Styles pane, tick `:hover`, and read Computed `background-color`. Write the value down.
3. Click `New chat` and hover a row in the agent list. Force `:hover` on it the same way and read Computed `background-color`. Write it down.
4. Click `Teammates` in the sidebar and hover a teammate tile. Force `:hover` and read Computed `background-color`. Write it down.
5. Compare the three values.
6. Go back to a Room and click it so it shows as the ACTIVE/selected room in the sidebar. Read that row's Computed `background-color` and confirm it is a dark selected-surface colour, visibly different from the hover colour but still dark.
7. Press Tab repeatedly to move keyboard focus through the composer textarea, then through the Appearance tab's Theme picker rows, then through a teammate dialog's fields. At each stop, look at whether the focus outline is clearly visible against the dark ground.

**Pass if — all of these**

- All three hover highlights resolve to the SAME Computed `background-color`, and that value is a dark colour (not a pale one).
- The selected room row is a dark selected-surface colour, distinct from hover but still dark.
- Every focus outline is clearly visible against the dark ground at every Tab stop.

**Fail if — any of these**

- A hover highlight is pale -> that is one of the four original literals; it escaped tokenisation.
- Two of the three hover values differ from each other -> they are not both reading the single hover variable.
- A focus outline is invisible against the dark ground -> the focus rule is reading a colour that does not switch, or no variable at all.

**Inconclusive if**

If forcing `:hover` in DevTools has no effect, hover the element with the mouse and take a screenshot instead, then compare the three screenshots by eye and with a colour picker.

> [!NOTE]
> The four pale literals became one hover token, and sameness across all three surfaces IS the assertion — unchanged by this migration.

### APPEARANCETHEME-24 — The reconnect modal shows one themed state paragraph over a dimmed backdrop, in both light and dark

**Free** · about 10 min

*Catches the documented silent failure where the scoped-CSS bundle stops loading: the modal then shows all six paragraphs at once, unstyled, and nothing else in the application says a thing. `ReconnectModal.razor.css` now reads its colours from `--mud-palette-*` variables rather than the retired custom tokens, but its structure and its two literal backdrop values are unchanged.*

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
9. Look at the `Retry` button's colour - it must be the application's Primary MudTheme colour (in Huddle Dark the periwinkle `#8f97e6`, `HuddleTheme.cs`; its text is near-black `#141413`, the theme's accent text, not a framework blue), not a light framework blue.
10. Restart the server with the same [`P-LAUNCH-FREE`](common.md#p-launch-free) command (it carries the cost-guard variable), reload the page.
11. Go to /settings/appearance and click `Huddle Light` in the Theme picker.
12. Open a Room again, stop the server again with Ctrl+C, and repeat the same observations with the light panel.
13. Read Computed `background-color` on `::backdrop` again.
14. Restart the server.

**Pass if — all of these**

- Exactly ONE state paragraph is visible at any moment - never all six at once.
- The sequence is `Rejoining the server...` (with the two-dot animation), then `Rejoin failed... trying again in N seconds.`, then `Failed to rejoin.` / `Please retry or reload the page.` with a `Retry` button.
- The panel is a themed raised surface with rounded corners and a shadow.
- The page behind it is visibly DIMMED.
- Computed `background-color` on `#components-reconnect-modal::backdrop` reads `rgba(0, 0, 0, 0.4)` in the light theme and `rgba(0, 0, 0, 0.6)` in the dark theme — `PaletteLight.OverlayDark` / `PaletteDark.OverlayDark` in `HuddleTheme.cs:67,120`.
- The `Retry` button uses the theme's Primary colour, not a framework blue.
- Both themes behave identically apart from the palette.

**Fail if — any of these**

- ALL SIX paragraphs show at once, unstyled -> the scoped-CSS bundle is not loading; that is the asset-key trap.
- The panel renders but the backdrop does NOT dim -> `--mud-palette-overlay-dark` is not resolving on `::backdrop`.
- The `Retry` button renders in a light framework blue rather than the theme's Primary -> `--mud-palette-primary` is not resolving on that scoped rule.

**Inconclusive if**

If the dialog never appears when you stop the server, reload the page, interact with it once, then stop the server again. If you cannot get DevTools to expose the `::backdrop` pseudo-element, judge the dimming by eye against a screenshot taken before the stop.

> [!NOTE]
> Free: stopping and restarting the app costs nothing.

### APPEARANCETHEME-26 — The credit line names Visual Studio Code, and attributes nobody it should not

**Free** · about 3 min

*Proves the attribution shipped with the imported Themes is present, correctly placed, and claims only what the sources actually support — the bundled themes other than Solarized and Monokai carry no third-party attribution on disk, so naming an author for them would be inventing one.*

**Before you start**

- The application is running.
- You are on http://localhost:5100/settings/appearance.

**Steps**

1. Find the paragraph that begins `Custom Accent lets you pick your own accent colour`. It sits after the Theme picker, the Accent colour picker (shown only for Custom Accent), and above the paragraph that begins `The selection is stored at` (`Appearance.razor:101-111`).
2. Read it word for word.
3. Click `Huddle Dark` in the picker. Read the same paragraph again.
4. Scan the paragraph for the names `Abyss`, `Kimbie`, `Red`, `Quiet Light`, `Monokai Dimmed` and `Tomorrow Night Blue`.

**Pass if — all of these**

- The paragraph sits BETWEEN the Theme picker (and the Accent colour picker, when shown) and the `The selection is stored at` line, not above the picker.
- It names Visual Studio Code, states the themes are Microsoft's under the MIT licence, and credits Solarized to Ethan Schoonover and Monokai to Wimer Hazenberg.
- It excepts **both** `Huddle Light` and `Huddle Dark` from the Visual Studio Code claim — they are this application's own, and since 2026-09-21 there are two of them.
- It attributes **no other** theme to any person or organisation.
- It remains legible in Dark — it is muted text (`--mud-palette-text-secondary`), which must still read comfortably against the dark surface.

**Fail if — any of these**

- The paragraph claims this application is MIT-licensed -> the licence statement is about VS Code's themes, not about Huddle.
- Any other bundled theme (Abyss, Kimbie Dark, Red, Quiet Light, Monokai Dimmed, Tomorrow Night Blue and the rest) is attributed to a named author -> that attribution is not in the source and was invented; see [ADR-0016](../../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md).
- The paragraph is invisible or near-invisible in either mode -> a colour literal crept into `.settings-theme-credit`, which `AppCss_DeclaresNoColourLiteral` should have caught.

**Inconclusive if**

If the Appearance panel will not render, this is INCONCLUSIVE, not a failure — re-run APPEARANCETHEME-01 first.

> [!NOTE]
> The file headers under `src/Huddle.App/Themes/VsCode/` are deliberately stricter than this line: they record only the MIT declaration in each extension's `package.json` and assert no upstream author at all. This paragraph is an acknowledgement, which is a different register from a provenance record.

### APPEARANCETHEME-27 — Every imported Theme paints its own palette, across the whole application

**Free** · about 12 min

*Proves the imported catalog actually paints — not just that the labels enumerate. This is the acceptance walk for [ADR-0016](../../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md), rewritten on 2026-09-21: the half of it that exercised a single-mode Theme's **borrowed** palette is gone, because borrowing is gone ([ADR-0017](../../adr/0017-a-theme-is-a-palette-not-a-pair.md)). What replaces it is stricter — a Theme must now show its own colours in every reachable state, with no fallback to Huddle's palette available as an excuse.*

**Before you start**

- The application is running, with at least one Room holding a few Messages and one fenced code block.

**Steps**

1. Select Theme `Dark Modern`. Without reloading, look at the sidebar, the transcript, a Teammate card, and the fenced code block.
2. Open DevTools → Elements → `<body>` → Computed, and read `background-color`. It must be `rgb(31, 31, 31)` — Dark Modern's `editor.background`, `#1f1f1f`.
3. Hover a Room in the sidebar and confirm the row changes colour. Tab to a link or button and confirm a visible focus outline.
4. Select Theme `Monokai`. Confirm the whole surface changes again, and that nothing is left painted in Dark Modern's colours.
5. Select Theme `Solarized Dark` and read `background-color` on `<body>` again. It must be `rgb(0, 43, 54)` — Solarized Dark's own `#002b36`, **not** `rgb(255, 255, 255)` and not Huddle Dark's `rgb(28, 28, 27)`.
6. Select Theme `Solarized Light`. Confirm the surface becomes Solarized Light's own tan palette, and that `background-color` is not plain white.
7. Select Theme `Dark High Contrast`. Read the transcript and the sidebar.
8. Return to Theme `Huddle Light`.

**Pass if — all of these**

- Each Theme selection repaints immediately, with no page reload and no F5.
- `<body>`'s computed background under `Dark Modern` is `rgb(31, 31, 31)`, and under `Solarized Dark` is `rgb(0, 43, 54)`.
- **No Theme ever shows Huddle's palette.** Step 5 is the specific check: before 2026-09-21, `Solarized Dark` with the light preference set rendered Huddle Light under Solarized Dark's name, which is the defect ADR-0017 exists to make unreachable.
- Under every Theme tried, body text is comfortably readable against its background, the hover state is visible, and the focus outline is visible.
- `Dark High Contrast` renders as a legible, strong-contrast dark Theme. It is **not** expected to look like VS Code's high-contrast mode, which draws borders everywhere from `contrastBorder`; MudBlazor has no equivalent.

**Fail if — any of these**

- Any surface stays painted in the previous Theme's colours after a switch -> something is reading a palette once rather than through `MudThemeProvider`.
- Any Theme renders Huddle's palette instead of its own -> a borrowed palette is reachable again.
- A surface renders blank, transparent, or black-on-black under any Theme -> a palette slot resolved to nothing, which the conversion is specifically designed to make impossible.
- Selecting a Theme forces a full page load -> the ADR-0010 render-tree behaviour regressed.
- The fenced code block loses its monospace family -> `--font-mono` in `app-vars.css` is the one app-owned custom property and no Theme should touch it.

**Inconclusive if**

If the Blazor circuit drops mid-walk (the reconnect modal appears), reload and start again. Record INCONCLUSIVE rather than FAIL — a dropped circuit invalidates every colour observation after it.

> [!NOTE]
> Three Themes are known to sit just under the 4.5:1 contrast floor for secondary text and are shipped that way on purpose: `Light+`, `Quiet Light` (4.40:1) and `Solarized Light` (3.98:1). Slightly-dim secondary text on those three is **not** a defect — `ThemeCatalogTests` records each with its measured ratio.

### APPEARANCETHEME-28 — Custom Accent stores the picked colour, clamps it for contrast, and shows its picker only for the two accent Themes

**Free** · about 10 min

*Custom Accent Light and Dark paint Huddle's own neutrals with a Human-picked accent. This proves the picker appears only for them, that a pick is stored as `accentColor` beside `theme`, that a colour with no contrast is adjusted at render time rather than painted as picked (`AccentPalette.ClampForContrast`, 4.5:1 floor, `AccentPalette.cs:15`), and that a malformed `accentColor` is logged and ignored.*

**Before you start**

- The application is running and you can see the console in `T-A`.
- `<data>\appearance.json` is absent (`P-RESET-SETTINGS`, then `P-LAUNCH-FREE`), and http://localhost:5100/settings/appearance is open on `Huddle Light`.

**Steps**

1. Confirm the tab shows no `Accent colour` field while `Huddle Light` is marked. Click `Huddle Dark` and confirm there is still none.
2. Click `Custom Accent Light`. An `Accent colour` field appears under the picker (`Appearance.razor:82-93`). Run `type <data>\appearance.json` and confirm it holds `"theme": "custom-accent"` and no `accentColor` yet.
3. In the `Accent colour` field enter the pale yellow `#ffeb3b` and press Tab. Run `type <data>\appearance.json` and confirm it now holds `"accentColor": "#ffeb3b"` beside the theme.
4. Look at the primary-coloured controls: the `New chat` button and the active sidebar link. In DevTools -> Elements, select `<html>` and read the custom property `--mud-palette-primary` in Computed. It must NOT be `#ffeb3b`: it is a darker yellow of the same hue. Check that the `New chat` label or the active link text is readable against its background.
5. Click `Custom Accent Dark`. The field stays and the stored `accentColor` is unchanged. A pale yellow needs no adjustment on a dark ground, so `--mud-palette-primary` now reads `#ffeb3b` (`rgb(255, 235, 59)`).
6. Enter the dark navy `#1a1a40` in the field and press Tab. Read `--mud-palette-primary` again: it is a LIGHTER blue of the same hue, not `#1a1a40`, and the primary controls are readable on the dark ground (`rgb(28, 28, 27)`).
7. Click `Huddle Light`. The `Accent colour` field disappears. Run `type <data>\appearance.json` and confirm `accentColor` is still in the file: it is kept, and ignored, while another Theme is chosen.
8. Click `Custom Accent Light` again and confirm the field returns showing `#1a1a40`.
9. Hand-edit the file to `{ "theme": "custom-accent", "accentColor": "yellow" }` and save. Wait 2 seconds and read the console in `T-A`.
10. Run `type <data>\appearance.json` and confirm it is exactly what you wrote.
11. Delete `<data>\appearance.json` when you are done.

**Pass if — all of these**

- The `Accent colour` field is present only while `Custom Accent Light` or `Custom Accent Dark` is marked.
- A pick is stored as `accentColor` in `#rrggbb` form, beside `theme`, and survives a switch to another Theme.
- `--mud-palette-primary` is the picked colour adjusted until it reads at 4.5:1 or better against the page: darker on Custom Accent Light, lighter on Custom Accent Dark. A colour that already reads (the yellow on the dark ground) is left alone.
- The console carries `Appearance file '<path>' sets accentColor 'yellow', which is not a valid #rrggbb colour; no accent colour is used instead and the file is left unchanged.`, the page still renders, and the file is unchanged.

**Fail if — any of these**

- The field shows for a Theme that is not a Custom Accent -> it must ignore the colour entirely.
- The pale yellow is painted as picked on the light ground, with unreadable primary text -> the contrast clamp is not applied.
- The malformed colour is repaired or removed from the file, or nothing is logged -> the Human's text is destroyed or silent.

**Inconclusive if**

If you cannot read `--mud-palette-primary` in DevTools, compare the `New chat` button against a colour picker instead and record the two values you measured.

### APPEARANCETHEME-29 — There is no Library pane setting, and a leftover `libraryPaneSide` key is ignored without a warning and kept in the file

**Free** · about 6 min

*The docked Library pane and its Right/Left side setting were retired on 2026-10-10: the `/library` page is the only Library surface. This proves the Appearance tab no longer offers the choice, and that an old `appearance.json` still carrying `libraryPaneSide` loads quietly and keeps the key when another setting is saved.*

**Before you start**

- The application is running and you can see the console in `T-A`.
- `<data>\appearance.json` is absent (`P-RESET-SETTINGS`), and http://localhost:5100/settings/appearance is open.

**Steps**

1. Read the whole Appearance tab top to bottom. Look for any control or label mentioning `Library pane`, `Right`, `Left` or a side.
2. Hand-edit `<data>\appearance.json` to `{ "theme": "huddle-dark", "libraryPaneSide": "left" }` and save. Wait 2 seconds and read the console in `T-A`.
3. Confirm the page still renders `Huddle Dark` as the marked Theme, and the console carries no warning that mentions `Appearance file`, `libraryPaneSide` or `side`.
4. Select `Custom Accent Dark`, enter `#3366cc` in the `Accent colour` field and press Tab.
5. Run `type <data>\appearance.json`. Confirm it now holds `"theme": "custom-accent"` and `"accentColor": "#3366cc"`, and that `"libraryPaneSide": "left"` is still in the file, unchanged.
6. Delete `<data>\appearance.json` when you are done.

**Pass if — all of these**

- The Appearance tab has no `Library pane` control, no Right/Left choice and no mention of a pane side.
- Step 2 produces no warning in the console, and the page renders normally.
- After the accent colour change, the file still contains `"libraryPaneSide": "left"` exactly as written, beside the new `theme` and `accentColor`.

**Fail if — any of these**

- A `Library pane` radio group is still on the tab -> the retired setting was not fully removed.
- The console warns about `libraryPaneSide` -> the retired key is still being validated.
- The key disappears from the file after a save -> the store is dropping unknown keys it should preserve.

**Inconclusive if**

If the Appearance tab will not render, this is INCONCLUSIVE, not a failure; re-run APPEARANCETHEME-01 first.

### APPEARANCETHEME-30 — The top-bar button switches between the chosen light and dark Themes, and is the last control in the bar

**Free** · about 6 min

*Proves the button exists at the right of the top bar, names where a click leads, switches the whole application and stores the result, starting from the virgin first-run state ([ADR-0054](../../adr/0054-a-light-theme-and-a-dark-theme.md)).*

**Before you start**

- The application is running and `<data>\appearance.json` is absent (`P-RESET-SETTINGS`).
- Open http://localhost:5100/ and wait for the shell to load.

**Steps**

1. Look at the right-hand end of the top bar (the bar holding the breadcrumb and the search box). Find the round icon button there and hover it for a second. Note the icon and the tooltip.
2. Click it. Note the page colours, the icon and the tooltip.
3. Run `type <data>\appearance.json`.
4. Click the button again. Note the colours, icon and tooltip, and run `type <data>\appearance.json` again.
5. Press Ctrl+Shift+R while the page is light, then again while it is dark (click the button between the two). Watch the first paint.

**Pass if — all of these**

- The button is the right-most thing in the top bar. Its icon is a moon and its tooltip reads `Switch to dark theme` while the page is light.
- After the first click the page is dark (Huddle Dark), the icon is a sun and the tooltip reads `Switch to light theme`.
- After the first click the file holds `"theme": "huddle-dark"` together with `"lightTheme": "huddle"` and `"darkTheme": "huddle-dark"`. After the second click `theme` is `huddle` again and the other two keys are unchanged.
- No page load happens on a click, and a hard reload paints the right mode from the first frame with no flash of the other one.

**Fail if — any of these**

- The button is missing, or sits in the middle or at the left of the bar -> `MainLayout` is not filling `TopBar`'s `Actions` slot.
- The icon or tooltip names the mode already shown rather than the mode a click leads to.
- A click changes the file but not the page, or the reverse -> the button is flipping a flag instead of calling `AppearanceStore.ToggleMode`.

**Inconclusive if**

If the shell will not load, re-run APPEARANCETHEME-01 first.

### APPEARANCETHEME-31 — The Appearance tab has a Light picker and a Dark picker, each holding only its own mode

**Free** · about 5 min

*Proves each slot can only be filled by a Theme of its own mode, which is what keeps ADR-0017's guarantee (a mode never contradicts its Theme) true now that there are two slots.*

**Before you start**

- The application is running and `<data>\appearance.json` is absent (`P-RESET-SETTINGS`).
- http://localhost:5100/settings/appearance is open.

**Steps**

1. Read the intro paragraph, then the two list titles, `Light theme` and `Dark theme`.
2. Write down every heading and row in each list, and which row is marked selected in each.
3. Click the marked row in the `Dark theme` list once more, then read the list and the page.

**Pass if — all of these**

- `Light theme` holds 8 Themes: under `Light` — `Huddle Light`, `Custom Accent Light`, `Light 2026`, `Light Modern`, `Light+`, `Quiet Light`, `Solarized Light`; under `High contrast` — `Light High Contrast`.
- `Dark theme` holds 13: under `Dark` — `Huddle Dark`, `Custom Accent Dark`, `Dark 2026`, `Dark Modern`, `Dark+`, `Abyss`, `Kimbie Dark`, `Monokai`, `Monokai Dimmed`, `Red`, `Solarized Dark`, `Tomorrow Night Blue`; under `High contrast` — `Dark High Contrast`.
- With no file, `Huddle Light` is marked in the first list and `Huddle Dark` in the second.
- Clicking the marked row again leaves it marked: a slot is never left empty.
- The intro paragraph mentions the button in the top bar.

**Fail if — any of these**

- A dark Theme appears in the light list, or the reverse -> `GroupedFor` is not filtering on `ThemeMode`.
- Clicking the marked row empties the list or writes nothing back -> the cleared selection is reaching the store.

**Inconclusive if**

If the page is still loading, wait five seconds, press F5 and read again.

### APPEARANCETHEME-32 — Picking a Theme changes the screen only when it is of the mode already shown

**Free** · about 8 min

*Proves `AppearanceStore.SavePreferred`: choosing the other mode's Theme records it for the button without surprising the Human with a repaint, while choosing a Theme of the mode on screen repaints at once.*

**Before you start**

- The application is running and `<data>\appearance.json` is absent (`P-RESET-SETTINGS`).
- http://localhost:5100/settings/appearance is open and the page is light.

**Steps**

1. In `Dark theme` click `Solarized Dark`. Look at the page colours, the mode button's tooltip and `<data>\appearance.json`.
2. Click the mode button. Look at the page, both lists and the file.
3. While dark, click `Abyss` in `Dark theme`. Look at the page.
4. While dark, click `Quiet Light` in `Light theme`. Look at the page and the file.
5. Click the mode button.

**Pass if — all of these**

- Step 1: the page stays light and the tooltip still reads `Switch to dark theme`; the file gains `"darkTheme": "solarized-dark"` and `theme` is absent or `huddle`.
- Step 2: the page turns Solarized Dark's deep teal ground (`#002b36`); `Huddle Light` and `Solarized Dark` are the marked rows.
- Step 3: the page repaints at once to Abyss's near-black blue ground (`#000c18`) and `theme` becomes `abyss`.
- Step 4: the page stays dark, and the file now holds `"lightTheme": "quiet-light"` with `theme` still `abyss`.
- Step 5: the page becomes Quiet Light's pale grey ground (`#f5f5f5`).

**Fail if — any of these**

- Step 1 turns the page dark -> a pick of the other mode is being shown, and the Human loses their place.
- Step 3 does not repaint -> a same-mode pick is only being recorded.
- The page ends on a Theme other than the one marked in the matching list -> a slot and `theme` have diverged.

**Inconclusive if**

If a click on a row does nothing at all, check APPEARANCETHEME-04 first.

### APPEARANCETHEME-33 — Old files, hand edits and bad values: the pair resolves safely

**Free** · about 8 min

*Proves a file written before the pair existed still toggles back to the Theme it was using, a hand-edit updates the open page and button live, and a wrong-mode or unknown choice is a warning that leaves the file alone.*

**Before you start**

- The application is running with the console visible in `T-A`, and http://localhost:5100/settings/appearance is open.

**Steps**

1. Hand-write `<data>\appearance.json` as `{ "theme": "solarized-dark", "dark": "light", "accentColor": "#e2762d" }` and save. Wait 2 seconds. Read the page, both lists and the button tooltip.
2. Click the mode button twice. Then run `type <data>\appearance.json`.
3. Hand-write `{ "theme": "huddle", "lightTheme": "solarized-dark", "darkTheme": "nonsense" }`, save and wait 2 seconds. Read the console in `T-A` and both lists.
4. Run `type <data>\appearance.json`. Then delete the file.

**Pass if — all of these**

- Step 1: the page repaints to Solarized Dark by itself, `Solarized Dark` is marked in `Dark theme`, `Huddle Light` in `Light theme`, and the tooltip reads `Switch to light theme`.
- Step 2: the first click goes to Huddle Light, the second back to Solarized Dark. The file keeps `"dark": "light"` and `"accentColor"`, and gains `"lightTheme": "huddle"` and `"darkTheme": "solarized-dark"`.
- Step 3: the console carries two warnings naming `lightTheme` ... `not a known Light theme` and `darkTheme` ... `not a known Dark theme`; the lists mark `Huddle Light` and `Huddle Dark`.
- Step 4: the file is exactly as hand-written in step 3 — the warnings never rewrite it.

**Fail if — any of these**

- Step 2 lands on Huddle Dark the second time -> the Theme being left is not recorded, so an old file loses its Theme at the first toggle.
- Step 3 shows `Solarized Dark` in the light list -> a wrong-mode choice is being honoured, and a mode can again contradict its Theme.
- The retired `dark` key or `accentColor` disappears -> the store is dropping keys it should preserve.

**Inconclusive if**

If the hand edit never reaches the page after 5 seconds, the filesystem watcher is not firing; see APPEARANCETHEME-18.

---

Back to [the manual test script](../manual-tests.md).
