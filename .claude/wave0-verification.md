# Wave 0 — verify seven UI issues against `main`

## Your objective

Seven open Gitea issues (#16, #20, #23, #24, #25, #26, #28) were found during a
manual test run on the branch `docs/manual-test-run-2026-09-14`. That branch
forked from `main` at `c1626e6` — *"Install MudBlazor 9.10.0, with no visual
conversion yet"* — and `main` has since replaced the entire UI and theming layer
in nine commits.

**Every one of those issues was therefore found against a UI that no longer
exists.** Your job is to run the app from `main` and determine, for each issue,
whether it still reproduces. You are not fixing anything.

Deliverable: seven verdicts plus the raw console output that justifies each one.

## Hard constraints

- **Do not edit any file under `src/`, `tests/` or `docs/`.** This is a
  verification task only.
- **Do not commit, push, or create a branch.** You will switch to `main`; that is
  the only git state change permitted.
- **Do not fix anything you find**, however small, and do not "tidy" code you
  pass through.
- **Do not guess a verdict.** If a check cannot be completed, record it as
  `BLOCKED` with the reason. A wrong verdict here causes wasted engineering work
  downstream; an honest `BLOCKED` costs nothing.
- Report exact strings. The issue comments written from your output will quote
  them verbatim.

## What you need

You must be able to **execute JavaScript in a live page** — via a browser
automation tool, DevTools protocol, or an IDE browser pane. Every oracle below
reads *computed* CSS or live DOM state.

`curl` cannot answer any of these checks. If you have no way to run JS in a
loaded page, stop now and report that, rather than substituting markup checks
that do not measure the same thing.

## Environment facts — already verified, do not re-check

| Fact | State | Consequence for you |
| --- | --- | --- |
| Working tree | Clean except untracked `.claude/` files | Safe to `git switch`; nothing will be lost |
| `App_Data` | Has `Teams/`, `hooks.json`, `rooms/`, `team.db`, `work/` | Real data; the UI migration never touched `src/Huddle.App/Data`, so the DB is compatible with `main` |
| Personas on disk | `Jarvis.md`, `Nova.md`, `Zellandine.md` | Check C uses **Nova** |
| `appearance.json` | **Absent** | Dark mode is **System**. Check B must set it explicitly |
| `hooks.json` | `{}` — empty | No hook is modified, so `Reset all to defaults` renders **disabled**. Check D must enable it first |
| `.md` file association | Absent (`assoc .md` reports *File association not found*) | Check F's precondition holds on this machine |

## Two traps that will silently waste your run

**1. Check order is load-bearing.** Check C needs a **cold model catalog**, which
only exists on the first teammate-card open of an app run. Check B **stops the
server**, ending the session. So C runs first and B runs last. Any other order
costs a relaunch.

**2. Issue #26's own reproduction steps are stale.** It instructs you to write
`{ "theme": "huddle-dark" }` into `appearance.json`. On `main` that is an
**unknown theme id**: it is logged as a warning, ignored, and leaves the app in
light mode — so the check measures nothing and looks like a pass. Theming moved
to MudBlazor (ADR-0010). There is now one theme, id `huddle`, and light/dark is a
separate key:

```json
{ "theme": "huddle", "dark": "dark" }
```

Valid `dark` values: `"system"`, `"light"`, `"dark"`.

---

## Step 1 — Stop any running instance and snapshot the data

An instance built from the old branch may be running and holding a handle on
`team.db` (SQLite connection pooling — a copy or delete will fail while it
lives).

```powershell
cd E:\Repos\Huddle
Get-Process Huddle.App -ErrorAction SilentlyContinue |
    Select-Object Id, StartTime, Path
# If one exists, stop it:
Get-Process Huddle.App -ErrorAction SilentlyContinue | Stop-Process -Force

Copy-Item -Recurse src\Huddle.App\App_Data src\Huddle.App\App_Data.wave0-backup
```

## Step 2 — Switch to `main` and build

```powershell
git switch main
git log --oneline -1        # expect: 79b5744 Run a first pass of the rewritten manual tests...
dotnet build Huddle.slnx    # expect 0 warnings, 0 errors
```

If the build is not clean, **stop and report**. Do not proceed against a broken
build.

## Step 3 — Warm run (sets up Check C only)

Check C needs a teammate whose stored Model **is** in the adapter's catalog.
Setting one warms the catalog, so it must happen in a throwaway run.

```powershell
dotnet run --project src\Huddle.App -- --urls http://localhost:5100 --Team:Acp:Enabled=false
```

1. Open `http://localhost:5100/teammates`, click **Nova**, click **Edit**.
2. Wait for the Model dropdown to populate (up to ~10 seconds). A
   `claude-agent-acp` adapter process appears for about a second — this is
   expected and documented in `docs/agencyteam/known-limits.md`. It is **not**
   issue #21, which concerns an adapter that *persists*.
3. Set **Model = Sonnet**. Save.
4. Stop the app (Ctrl+C, or stop the process).

## Step 4 — Cold run

```powershell
dotnet run --project src\Huddle.App -- --urls http://localhost:5100 --Team:Acp:Enabled=false
```

Open `http://localhost:5100`. **Run Check C before navigating anywhere else.**

---

# Check C — #23 and the incomplete picker · MUST BE FIRST

**Issue #23:** *Edit's Model select jumps to 'Default (recommended)' when the
catalog lands late, losing the stored model from the display.*

**Also under test:** a separate observation that the Model dropdown lists only
Haiku until the adapter probe returns.

### Action

Navigate to `/teammates`, click **Nova**, click **Edit** — within a second or two
of the page being ready. Immediately run:

```js
window.__probe = [];
window.__t = setInterval(() => {
  const ctl = [...document.querySelectorAll('.mud-input-control')]
      .find(c => c.textContent.includes('Model'));
  const input = ctl && ctl.querySelector('input');
  window.__probe.push([Math.round(performance.now()),
                       input ? input.value : null,
                       ctl ? ctl.querySelectorAll('option').length : -1]);
}, 250);
setTimeout(() => clearInterval(window.__t), 12000);
```

Wait 12 seconds, then collect:

```js
JSON.stringify(window.__probe)
```

Additionally: open the Model dropdown **early** (within ~2s) and again **after it
settles**, recording how many options each list shows.

If the `.mud-input-control` selector finds nothing, fall back to reading the
card's visible text. Do not spend time fighting MudSelect's internals — record
what you could and could not observe.

### Verdict

| Observation | Verdict |
| --- | --- |
| The value reads `Sonnet` throughout | `#23 FIXED` — the MudSelect migration resolved it |
| The value starts `Sonnet`, then becomes `Default (recommended)` | `#23 SURVIVES` — and note it is now a MudSelect fault, not the raw `<select>` one the issue describes |
| Option count starts small (Haiku only) then grows | `PICKER-INCOMPLETE CONFIRMED` |
| Option count is complete from the first sample | `PICKER-INCOMPLETE NOT REPRODUCED` |

---

# Check D — #28, the danger colour

**Issue #28:** *Every destructive button renders in plain text colour:
`.teammate-card-action` overrides `.teammates-danger`.*

### Action

Go to `/settings`. `Reset all to defaults` starts **disabled** because
`hooks.json` is empty — type one character into any hook textarea to enable it.
Then:

```js
[...document.querySelectorAll('button')]
  .map(b => [b.textContent.trim().slice(0, 30), getComputedStyle(b).color].join('  ::  '))
  .filter(s => /Reset|reset/i.test(s))
```

Afterwards, click that field's own per-field **Reset** to undo your stray
character.

### Verdict

| Observation | Verdict |
| --- | --- |
| `Reset all to defaults` renders a red/error colour, visibly different from the plain per-field `Reset` | `#28 FIXED` |
| Both render the same colour | `#28 SURVIVES` |

Expected outcome is FIXED: `.teammate-card-action` no longer exists on `main`,
and all four destructive buttons are now `<MudButton Color="Color.Error">`.

---

# Check E — #25, the keyboard focus ring

**Issue #25:** *Keyboard focus ring is invisible on links and buttons —
MudBlazor's `a:focus-visible{outline:none}` reset, in both themes.*

### Action

Click the page background to reset focus, then press **Tab**. After each of the
first four or five stops, run:

```js
(() => { const e = document.activeElement, s = getComputedStyle(e);
  return [e.tagName, (e.className || '').slice(0, 40), e.matches(':focus-visible'),
          s.outlineStyle, s.outlineWidth, s.outlineColor].join(' | '); })()
```

Use a real Tab keypress. Calling `.focus()` in script does not reliably set
`:focus-visible`.

### Verdict

| Observation | Verdict |
| --- | --- |
| Links and buttons report `outlineStyle: none` | `#25 SURVIVES` |
| A real outline is drawn | `#25 FIXED` — MudBlazor's component focus styling now covers it |

The composer `<textarea>` always had a visible ring; ignore it either way.

---

# Check F — #24 and #20, cheap confirmations

### F1 — #24, composer status line crossing Rooms

Open any Room, type `/invite @nobody` and press Enter. Expect a red error line
above the message box. Now click a **different** Room in the sidebar, then:

```js
document.querySelector('.composer-info, .composer-error')?.textContent ?? '(clear)'
```

| Observation | Verdict |
| --- | --- |
| The line from the previous Room is still shown | `#24 SURVIVES` |
| `(clear)` | `#24 FIXED` |

Expected: SURVIVES. `Composer.razor` is unchanged by the migration and has no
room-change guard.

### F2 — #20, `Open` silently doing nothing

Go to `/teammates`, click any teammate, click **Open**. Watch for: a window
opening, a red line on the card, and any new line in the app console.

| Observation | Verdict |
| --- | --- |
| Nothing happens anywhere — no window, no card message, no log line | `#20 SURVIVES` (this silence *is* the defect) |
| A `Could not open '<Name>': ...` message appears | `#20 FIXED` |
| A file actually opens | `BLOCKED` — this machine has gained a `.md` association and the precondition no longer holds |

---

# Check B — #26 and #16, the reconnect modal · MUST BE LAST

This check stops the server. Do everything else first.

### Set dark mode

Either use `/settings/appearance` and select **Dark**, or stop the app and write
the file:

```powershell
'{ "theme": "huddle", "dark": "dark" }' | Set-Content src\Huddle.App\App_Data\appearance.json
```

**Confirm the page is visibly dark before continuing.** If it is not, the check
measures nothing — re-read trap 2 at the top of this document.

### Action

Open a Room. Stop the server (Ctrl+C). The reconnect modal will appear.

**B1 — #26, the colours.** Immediately:

```js
(() => { const m = document.getElementById('components-reconnect-modal'), s = getComputedStyle(m);
  return s.backgroundColor + '  on  ' + s.color; })()
```

**B2 — #16, the paragraph count.** Run the following **repeatedly** — once on the
first attempt, then several times through the retry backoff as it grows from ~5s
toward ~30s:

```js
[...document.querySelectorAll('#components-reconnect-modal p')]
  .filter(p => getComputedStyle(p).display !== 'none')
  .map(p => p.textContent.trim())
```

Record the dialog's class alongside each sample:

```js
document.getElementById('components-reconnect-modal').className
```

Restart the app afterwards to recover the page.

### Verdict — B1 (#26)

| Background colour | Verdict |
| --- | --- |
| `rgb(42, 42, 49)` — the dark palette's `Surface` | `#26 FIXED-CLEAN` — the app's own rule won outright |
| `rgb(27, 27, 31)` — the dark palette's `Background` | `#26 FIXED-WITH-NIT` — MudBlazor's `!important` still wins, but both palettes now agree, so the modal is dark and readable. Note the nit: the modal receives `Background`, not the `Surface` its stylesheet asks for, so it does not stand out from the page behind it |
| `rgb(255, 255, 255)` | `#26 SURVIVES` |

Text should read `rgb(230, 230, 234)` in all three cases. The original defect was
white-on-near-white, a contrast ratio of about 1.06:1.

### Verdict — B2 (#16)

| Observation | Verdict |
| --- | --- |
| One paragraph while the class is `components-reconnect-show` alone; **two** once `components-reconnect-retrying` is added beside it | `#16 SURVIVES` |
| Exactly one paragraph at every stage | `#16 FIXED` |

Expected: SURVIVES. That selector block is byte-identical on `main`.

> **Do not chase a 404 on a `ReconnectModal` stylesheet.** Both `SHELLNAV-26` and
> `PIPEEXTERNAL-34` name a missing scoped stylesheet as the cause in their *Fail
> if* lines, and that attribution is **wrong** — the scoped bundle loads
> correctly (200), which the investigation on issue #16 already established. Two
> paragraphs is a CSS selector bug, not an asset failure.

---

## Step 5 — Restore

```powershell
Remove-Item src\Huddle.App\App_Data\appearance.json -ErrorAction SilentlyContinue
git switch docs/manual-test-run-2026-09-14
```

Leave `src\Huddle.App\App_Data.wave0-backup` in place; a human will remove it.

## Report format

Fill this in and return it as your final message, followed by the raw console
output for each check.

```text
BUILD:        clean | failed (paste output)

C  #23                 FIXED | SURVIVES | BLOCKED
C  picker-incomplete   CONFIRMED | NOT REPRODUCED | BLOCKED
D  #28                 FIXED | SURVIVES | BLOCKED
E  #25                 FIXED | SURVIVES | BLOCKED
F  #24                 FIXED | SURVIVES | BLOCKED
F  #20                 FIXED | SURVIVES | BLOCKED
B  #26                 FIXED-CLEAN | FIXED-WITH-NIT | SURVIVES | BLOCKED
                       background rgb observed: ...
B  #16                 FIXED | SURVIVES | BLOCKED
                       paragraph samples + dialog class at each:
```

Then, for each check, the verbatim console output. Exact strings matter — they
will be quoted in the issue comments.

## If you get stuck

Report `BLOCKED` with what you tried and what you observed. Do not:

- infer a verdict from reading source code instead of running the app;
- substitute a `curl` or markup check for a computed-style check;
- change the check order to work around a problem (tell me instead);
- fix, refactor or tidy anything you encounter.
