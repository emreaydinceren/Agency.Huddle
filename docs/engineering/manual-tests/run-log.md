# Manual test run log

Every manual test, the file that holds it, and every recorded run of it. One section per test,
in the order of [the tracker](tracker.md); a run is one row, newest last.

The [tracker](tracker.md) answers *where does each test stand now*; this page answers *what
happened each time it ran*. When you conclude a run, set the tracker row and add a row here.
When you add a test, add its section here too: `agents/scripts/Check-ManualTests.ps1` fails when a
test has no section, or a section has no test.

| Column | Write |
| --- | --- |
| Date | The day the run concluded, `yyyy-MM-dd` |
| Result | `Pass`, `Fail`, `Inconclusive` or `Blocked` (see [§0.5](../manual-tests.md#05-how-to-conclude-a-result)) |
| Commit | The short sha of the build you tested |
| Issue | The Gitea issue for a Fail, as `#123` |
| Notes | What you saw, quoted where the test asks for it |

> [!NOTE]
> **Rows marked *(earlier script)* were run against an earlier wording of the test.** They come
> from the tracker's git history up to `edbf2363` (2026-10-09), which reset every result when the
> suite was rewritten; most are the full run of 2026-09-14/15 against the pre-MudBlazor UI. Same
> id, different script: read them as history, not as a verdict on the test as written now. Their
> Commit is the commit that recorded the result, and issue links are written as `#n`.

---

## Application shell, navigation and layout

### SHELLNAV-01 — Every stylesheet the shell links is fingerprinted (except the one that is deliberately static) and actually serves

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-01--every-stylesheet-the-shell-links-is-fingerprinted-except-the-one-that-is-deliberately-static-and-actually-serves)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### SHELLNAV-02 — Every shell script is fingerprinted (except MudBlazor's own) and serves - the gap the automated guard does not cover

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-02--every-shell-script-is-fingerprinted-except-mudblazors-own-and-serves---the-gap-the-automated-guard-does-not-cover)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### SHELLNAV-03 — The scoped-CSS bundle is applied: the error banner stays hidden below the fold

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-03--the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### SHELLNAV-04 — GET / redirects to the oldest room

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-04--get--redirects-to-the-oldest-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | 302 to /rooms/01a091de37237374a89cffc96a9617c4 (32 hex); browser settles there; h1 `echo`, first sidebar entry. |

### SHELLNAV-05 — /rooms/{unknown-id} returns 200 and shows the shared empty state while the sidebar still lists rooms

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-05--roomsunknown-id-returns-200-and-shows-the-shared-empty-state-while-the-sidebar-still-lists-rooms)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | 200; sidebar still echo+alpha; main read exactly `No rooms yet. Start an agent to create one.`; no h1, no composer, address bar unchanged. |

### SHELLNAV-06 — An unmatched route returns a bare HTTP 404 with a zero-byte body

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-06--an-unmatched-route-returns-a-bare-http-404-with-a-zero-byte-body)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | /nope 404 with 0 bytes, /rooms 404, no exception in T-A. Embedded browser pane paints the zero-byte body blank rather than Chrome's stock `HTTP ERROR 404` interstitial - no sidebar, links or styling either way. |

### SHELLNAV-07 — Routes are case-insensitive

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-07--routes-are-case-insensitive)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | /Settings and /AGENTS both 200; h1 `Settings` with the Hooks/Appearance rail, h1 `Agents`. |

### SHELLNAV-08 — The drawer is present, identical and fixed-width on every route

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-08--the-drawer-is-present-identical-and-fixed-width-on-every-route)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | All four routes: gridTemplateColumns `240px 1040px`, sidebar children new-chat/room-list/sidebar-link/sidebar-link, links exactly `Agents -> /agents` and `Settings -> /settings`, .main-column overflow `hidden`, scrollWidth/Height equal to client on every route. |

### SHELLNAV-09 — The sidebar marks exactly one room active, with aria-current

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-09--the-sidebar-marks-exactly-one-room-active-with-aria-current)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | activeCount 1 both times; `echo \| page` then `alpha \| page`; active link bg rgb(47,61,87) weight 600, the other transparent/400; h1 matched each time. Script NOTE is stale: NavLink DOES emit aria-current=page here. |

### SHELLNAV-10 — /agents renders, and a bare page load spawns no node process

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-10--agents-renders-and-a-bare-page-load-spawns-no-node-process)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | h1 `Agents`, button `New agent`, intro opens with the exact sentence; sidebar intact with the room list; no probe/catalog line in O-LOG. O-ADAPTERS read 0 at baseline, 0 after the page load and 0 after a fresh full document load of /agents. HARNESS NOTE: the F5 key does not reload this browser pane - the re-load was done by navigating to the URL again. |

### SHELLNAV-11 — /settings renders the seven-tab bar and defaults to Profile

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-11--settings-renders-the-seven-tab-bar-and-defaults-to-profile)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | URL stays `http://localhost:5100/settings`; h1 `Settings`; exactly two tabs `Hooks \| active=true`, `Appearance \| active=false`; Hooks pane present; `Reset all to defaults` in the header; adapters unchanged at 0. |

### SHELLNAV-12 — Clicking a settings tab changes the URL, and the URL round-trips as a bookmark

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-12--clicking-a-settings-tab-changes-the-url-and-the-url-round-trips-as-a-bookmark)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Appearance click -> /settings/appearance, Hooks click -> /settings/hooks; Back walked hooks -> appearance -> /settings one step at a time; fresh tab on /settings/appearance opened with Appearance active and the Theme dropdown; curl oracle printed `Appearance`. |

### SHELLNAV-13 — An unrecognised {Tab} segment silently falls back to Profile, and the tab name is matched without regard to case

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-13--an-unrecognised-tab-segment-silently-falls-back-to-profile-and-the-tab-name-is-matched-without-regard-to-case)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | All five 200. bogus->Hooks, HOOKS->Hooks, Appearance->Appearance, APPEARANCE->Appearance, trailing slash->Hooks. /settings/bogus renders the normal Hooks pane, error banner display:none. |

### SHELLNAV-14 — No route sets a browser tab title

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-14--no-route-sets-a-browser-tab-title)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | document.querySelector("title") returned null on all five routes and document.title was the empty string; curl printed 0 `<title>` tags for /, /agents, /settings, /settings/appearance and for the followed redirect. Tab label is the URL everywhere. Known limit recorded once for the whole shell. |

### SHELLNAV-15 — Focus moves to the page's <h1> after every navigation

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-15--focus-moves-to-the-pages-h1-after-every-navigation)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | After clicking Agents, Settings and echo the activeElement read `H1 \| Agents`, `H1 \| Settings`, `H1 \| echo`; the h1 carries tabindex="-1" on each. Tab from the heading landed on BUTTON.invite-toggle inside `<main>`, not the sidebar. On /rooms/does-not-exist activeElement was BODY and there is no h1 - the expected accessibility observation. |

### SHELLNAV-16 — Sidebar links use enhanced navigation, not a full page reload

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-16--sidebar-links-use-enhanced-navigation-not-a-full-page-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | window.__probe stayed 42 (and a timestamp marker survived) across sidebar clicks to Agents and Settings; the _blazor negotiate count stayed at 11 over both clicks, so no circuit was rebuilt; the New chat panel stayed open (hidden=false) and sidebar scrollTop stayed 0. No white flash - judged by the surviving JS context and unchanged circuit rather than by eye. |

### SHELLNAV-17 — The New chat panel toggles, lists agents with status dots, and gates Start chat

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-17--the-new-chat-panel-toggles-lists-agents-with-status-dots-and-gates-start-chat)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Panel .hidden false when open, true after the second click. Two checkboxes, one per agent, each with an .agent-dot; every dot title read exactly `online` (lowercase) with class `agent-dot online` and colour rgb(63,191,90). Start chat disabled=true with nothing ticked, false after ticking echo, true again after unticking. Agrees with T-A, which logged only `Demo agent echo/alpha connected.` |

### SHELLNAV-18 — Starting a chat creates a room named after its agents and navigates straight to it

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-18--starting-a-chat-creates-a-room-named-after-its-agents-and-navigates-straight-to-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Ticking echo+alpha and pressing Start chat closed the panel by itself, landed on /rooms/01a0a146d7557aa8a1a32504d7245a1d, h1 read exactly `echo, alpha`, member line `You, echo, alpha`, and the sidebar gained `echo, alpha` with no refresh. Reopening the panel showed both boxes clear and Start chat disabled again. team.db present (53248 bytes). sqlite3 check skipped - no client on PATH. |

### SHELLNAV-19 — app.js is proven functionally: Enter sends and clears, and the transcript scrolls

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-19--appjs-is-proven-functionally-enter-sends-and-clears-and-the-transcript-scrolls)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Enter posted `hi @echo`, emptied the textarea (defaultPrevented true on the keydown) and echo replied `echo: hi echo` within a second, with `1 of 40 agent replies since you last spoke.` The newest message was visible with no hand-scrolling (nothing in `<main>` overflows at this pane height). Shift+Enter arrived as key Enter shift=true defaultPrevented=false - the app correctly declined to send - and the textarea holds two lines. HARNESS NOTE: the browser tool maps "Return" to a keydown with key:"" so it does nothing; "Enter" is the spelling that works. teamComposer and teamScroll both typeof object; app.ovcl84kama.js fingerprinted. |

### SHELLNAV-20 — The room list updates live, with no refresh, when a room appears from outside the app

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-20--the-room-list-updates-live-with-no-refresh-when-a-room-appears-from-outside-the-app)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | echo-bot as `mybot` connected; O-WIRE printed the welcome envelope with room `mybot`. A `mybot` entry appeared in TAB 1 and TAB 2 with no refresh and no navigation, and the New chat list gained `mybot` (dot `online`). After six sidebar navigations the list still held exactly four rooms, each once. O-LOG carried `Created direct room ... for agent mybot.` and stayed at 18 lines with zero warn/fail/exception lines before and 12s after closing TAB 2. |

### SHELLNAV-23 — A bad appearance.json does not break the shell: no theme link, no blank page, only a log warning

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-23--a-bad-appearancejson-does-not-break-the-shell-no-theme-link-no-blank-page-only-a-log-warning)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Page rendered normally; no themes/ link was emitted at all (server HTML carried none and the DOM filter returned an empty array); the Theme dropdown read `System`. The `Overrides that didn't load` section carried both lines verbatim: `Theme 'not-a-theme' is not a known theme; the built-in theme is used instead.` and `'--not-a-token' is not a theme token; it was left in the file and ignored.` O-LOG carried two AppearanceStore warnings, the first reading `Appearance file '...appearance.json' selects theme 'not-a-theme', which is not a known theme; the built-in theme is used instead and the file is left unchanged.` The file stayed byte-identical at 63 bytes before and after. Cleaned up: appearance.json deleted. |

### SHELLNAV-24 — A deep link renders complete content on a cold first request, before any circuit attaches

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-24--a-deep-link-renders-complete-content-on-a-cold-first-request-before-any-circuit-attaches)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Prerendered HTML from curl: exactly one h1 reading `echo`, contains `hi @echo` True, `No rooms yet` False, and the budget line `1 of 40 agent replies since you last spoke.` /settings/appearance and /agents are likewise complete in the raw HTML (Theme select, tab rail, h1 and intro all present). A cold tab showed sidebar, heading and both message bodies on the first probe and identical three seconds later - no flash, no appear/disappear/reappear - and its budget line matched the already-open tab exactly. Interactivity followed: New chat opened, a checkbox ticked and Start chat enabled. |

### SHELLNAV-25 — Two browser tabs on the same install stay in step through the shell

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-25--two-browser-tabs-on-the-same-install-stay-in-step-through-the-shell)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Run against the CORRECTED steps 5-11 (see Appendix B). TAB B, untouched on /agents, gained the new `echo, alpha` room with no refresh - Start chat with two agents creates a new Room even when one with the same members exists. TAB A reloaded into Light: body bg rgb(255,255,255) and a fourth href themes/huddle-light.css. TAB B kept the old dark appearance with three hrefs, and STILL did after clicking Settings in the sidebar; a full document load of the same URL turned it light with the fourth href. Dark was not judgeable on this dark OS - System already resolves to rgb(27,27,31), the same value huddle-dark gives - so Light was used. Restored to System. |

### SHELLNAV-26 — The reconnect modal shows exactly one state paragraph at a time when the server goes away

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-26--the-reconnect-modal-shows-exactly-one-state-paragraph-at-a-time-when-the-server-goes-away)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `66ffe58d` | #16 | Step 7 returned TWO paragraphs for the whole retry phase, not one: `["Rejoining the server...", "Rejoin failed... trying again in N seconds."]` sampled at N = 0, 5, 2 and 29 seconds, both visible on screen stacked. Dialog class was `components-reconnect-show components-reconnect-retrying`; ReconnectModal.razor.css gates the first-attempt paragraph on `components-reconnect-show` alone and Blazor keeps that class when it adds `components-reconnect-retrying`. NOT the scoped-CSS 404 the Fail-if warns about: ReconnectModal.abdmv1u4y3.razor.js returns 200, the modal is a contained centred 320x181 box, and the first attempt showed exactly one paragraph. The terminal `Failed to rejoin` state and its Retry button were not reached - backoff was past 29s and climbing. Restarting the app recovered the page. SCRIPT DEFECT: step 2's regex `ReconnectModal\.razor\.[^"]*\.js` matches nothing; the real name is `ReconnectModal.`<hash>`.razor.js`, hash BEFORE `.razor`. |

### SHELLNAV-27 — With no rooms, the empty state renders TWICE and there is no heading at all

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-27--with-no-rooms-the-empty-state-renders-twice-and-there-is-no-heading-at-all)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With App_Data renamed aside and Team__DemoAgent__Enabled=false, GET / answered `HTTP/1.1 200 OK` - no redirect. `No rooms yet` appeared exactly twice in the served HTML and `h1` zero times. In the browser both the sidebar room area and the main column read `No rooms yet. Start an agent to create one.` and document.activeElement was BODY. Adapters 0 throughout. |

### SHELLNAV-28 — The New chat panel with no agents shows its own guidance instead of an empty list

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-28--the-new-chat-panel-with-no-agents-shows-its-own-guidance-instead-of-an-empty-list)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | New chat panel opened to the single sentence `No agents have connected yet. Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.` Both code spans render in ui-monospace/Cascadia Mono. document.querySelector(".new-chat-start") returned null and ".new-chat-panel ul" returned null - no dead control, no empty list. The code array was exactly [tools/echo-bot.ps1, appsettings.json], and tools/echo-bot.ps1 does exist in the repo. Cleanup done: App_Data restored, demo agents back on, all five rooms and the echo transcript present. |

### SHELLNAV-29 — Record whether the yellow error band ever appears during a genuine circuit fault

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-29--record-whether-the-yellow-error-band-ever-appears-during-a-genuine-circuit-fault)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Inconclusive *(earlier script)* | `66ffe58d` |  | Inconclusive - no circuit fault occurred during this session. The yellow `#blazor-error-ui` band never appeared in any of the 29 tests; it was present in the DOM but computed display:none throughout, and O-LOG carried zero exception lines. Per the test's own Inconclusive clause this is the expected outcome and no issue is opened. (Stopping the app in SHELLNAV-26 produced the reconnect modal, which is the other mechanism, not this band.) |

### SHELLNAV-30 — The drawer's icons, and Settings pinned to the bottom

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-30--the-drawers-icons-and-settings-pinned-to-the-bottom)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-31 — One click on a left-menu item opens a preview tab, and a click on a different item replaces it

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-31--one-click-on-a-left-menu-item-opens-a-preview-tab-and-a-click-on-a-different-item-replaces-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-32 — Double-clicking a menu item keeps its tab, and the next click opens beside it

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-32--double-clicking-a-menu-item-keeps-its-tab-and-the-next-click-opens-beside-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-33 — Clicking a tab switches the page and the address

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-33--clicking-a-tab-switches-the-page-and-the-address)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-34 — Closing the active tab activates its neighbour, and closing the last one shows "No pages are open"

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-34--closing-the-active-tab-activates-its-neighbour-and-closing-the-last-one-shows-no-pages-are-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-35 — Typing in a Room's message box keeps its tab, and the unsent text survives a tab switch and a page reload

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-35--typing-in-a-rooms-message-box-keeps-its-tab-and-the-unsent-text-survives-a-tab-switch-and-a-page-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-36 — A Room tab opened by pasting its URL gets the Room's name as its title within a moment

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-36--a-room-tab-opened-by-pasting-its-url-gets-the-rooms-name-as-its-title-within-a-moment)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-37 — Open tabs come back after a reload, with the tab for the current page active

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-37--open-tabs-come-back-after-a-reload-with-the-tab-for-the-current-page-active)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-38 — Alt+W, Alt+Page Down and Alt+Page Up close and move between tabs

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-38--altw-altpage-down-and-altpage-up-close-and-move-between-tabs)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-39 — Right-clicking a tab offers Pin, Move left, Move right and Close, and Pin keeps the tab at the front as an icon

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-39--right-clicking-a-tab-offers-pin-move-left-move-right-and-close-and-pin-keeps-the-tab-at-the-front-as-an-icon)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-40 — Move right opens a second pane beside the first, and both pages stay live

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-40--move-right-opens-a-second-pane-beside-the-first-and-both-pages-stay-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-41 — Opening a page that is already open in the other pane takes you to it, and new pages open in the focused pane

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-41--opening-a-page-that-is-already-open-in-the-other-pane-takes-you-to-it-and-new-pages-open-in-the-focused-pane)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-42 — Both panes, pins and the focused pane come back after a reload

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-42--both-panes-pins-and-the-focused-pane-come-back-after-a-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-43 — Two Rooms side by side keep their own unsent text

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-43--two-rooms-side-by-side-keep-their-own-unsent-text)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-44 — Every page in a tab has a breadcrumb bar, and an ancestor crumb navigates that tab in place

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-44--every-page-in-a-tab-has-a-breadcrumb-bar-and-an-ancestor-crumb-navigates-that-tab-in-place)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SHELLNAV-45 — With two panes each has its own bar, and a crumb acts on its own pane

File: [`docs/engineering/manual-tests/shell-nav.md`](shell-nav.md#shellnav-45--with-two-panes-each-has-its-own-bar-and-a-crumb-acts-on-its-own-pane)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Startup, configuration and first-run state

### STARTUPCONFIG-01 — The documented launch command serves the styled app shell on http://localhost:5100

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-01--the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### STARTUPCONFIG-02 — Every stylesheet in the document head returns 200 — the silent @Assets regression

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-02--every-stylesheet-in-the-document-head-returns-200--the-silent-assets-regression)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Exactly three application stylesheets, each fingerprinted and none equal to its markup key: theme.ce2n94aiaf.css, app.okpt0txbqy.css, Huddle.App.z3g6d1kai5.styles.css - all 200 by curl and 200 on every one of the 88 CSS requests the network panel recorded. No `An unhandled error has occurred.` strip: #blazor-error-ui computes display:none with position:fixed. Layout genuinely styled - .app-grid `240px ...`, sidebar background rgb(35,35,40), message rows carrying meta lines `You 11:57` and `echo 11:57`. |

### STARTUPCONFIG-06 — Every navigation surface is reachable from a cold start

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-06--every-navigation-surface-is-reachable-from-a-cold-start)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | New chat panel listed `echo` and `alpha` (plus `mybot`, the pipe client left connected by SHELLNAV-20), each with a checkbox and an .agent-dot - 3 checkboxes, 3 dots. With nothing ticked, Start chat was present and disabled=true, and clicking it did nothing: the URL and the room count of 5 were unchanged. Ticking echo set disabled=false; the second New chat click closed the panel with no Room created. Agents -> /agents with h1 `Agents`, Settings -> /settings with h1 `Settings`, and clicking a Room -> /rooms/01a091de37bb78e2beb9d6bcb3414994 with h1 `alpha`. |

### STARTUPCONFIG-07 — Start chat on one known agent reuses its Room; two agents create one new named Room

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-07--start-chat-on-one-known-agent-reuses-its-room-two-agents-create-one-new-named-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | From a clean App_Data with exactly 2 Rooms. Ticking ONLY echo and pressing Start chat left the count at 2 and landed back on the SAME room id /rooms/01a0a15467a47b5ea4f594381f786754 with its `hi @echo` / `echo: hi echo` transcript intact - the exact-membership lookup reused it. Ticking BOTH created exactly one new Room (3 total) with heading `echo, alpha`. Step 6 repeated: one further new Room, 4 total, a second entry also named `echo, alpha` at a distinct id 01a0a1552e5c7d9bac8e3ba846d0c916 - consistent with step 4's rule (two agents always mint a new Room), and recorded as an observation since multi-agent reuse is not specified. Two same-named Rooms are distinguishable only by id in the sidebar. |

### STARTUPCONFIG-08 — /agents lists Personas only — pipe clients never appear there

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-08--agents-lists-personas-only--pipe-clients-never-appear-there)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | App_Data\Teams empty. /agents showed h1 `Agents`, button `New agent`, the intro opening `A Persona is a Markdown file describing how one agent should behave`, and exactly `No Personas yet. Choose New agent to add one.` A word-boundary scan of the whole main column found neither `echo` nor `alpha` - the pipe-connected demo agents do not leak into the Persona list. |

### STARTUPCONFIG-09 — Settings' Prompts tab on a first run says prompts.json is absent, and offers nothing to save or reset

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-09--settings-prompts-tab-on-a-first-run-says-promptsjson-is-absent-and-offers-nothing-to-save-or-reset)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | hooks.json absent before the test - the app never created it. h1 `Settings`, exactly two tabs with Hooks active by default, 22 hook fields. The overrides paragraph named E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json - matching the live DataDir - and carried `The file does not exist until the first time you save here, so it being absent is expected, not a bug.` With nothing edited both Save and `Reset all to defaults` were disabled=true. Clicking Hooks moved the URL to /settings/hooks. /settings/nonsense rendered the Hooks pane with the tab rail intact, no 404 and no error banner. One typed character flipped Save and Reset all to enabled and raised an `Unsaved` badge; Save wrote hooks.json holding only the edited hook, and Save went back to disabled. hooks.json deleted afterwards to restore the first-run state. |

### STARTUPCONFIG-10 — Appearance tab on a first run reads Lumen Light; picking a dark Theme writes appearance.json and applies immediately, with no reload

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-10--appearance-tab-on-a-first-run-reads-lumen-light-picking-a-dark-theme-writes-appearancejson-and-applies-immediately-with-no-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | appearance.json absent before the test. Intro read exactly `Pick a theme, or leave it on System to follow your device's own light or dark setting.` Dropdown offered exactly System / Light=huddle-light / Dark=huddle-dark with System selected. The overrides paragraph named E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json and carried `The file does not exist until you save a theme choice here, so it being absent is expected, not a bug.` Choosing Dark was a FULL navigation - a JS marker set beforehand was gone - and the page came back dark (bg rgb(27,27,31), text rgb(230,230,234)) with themes/huddle-dark.css layered fourth. The file was then present containing `{ "theme": "huddle-dark" }` - the id, never the label `Dark`. Restored to System. |

### STARTUPCONFIG-11 — Rooms and Transcripts survive a restart; Drafts and the Budget do not

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-11--rooms-and-transcripts-survive-a-restart-drafts-and-the-budget-do-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Before: echo held 6 messages beginning `You 11:57` / `hi @echo`, alpha held 2 beginning `You 12:10` / `hi @alpha`, sidebar 5 Rooms, jsonl line counts 6 and 2. After Ctrl+C and relaunch the sidebar still held exactly 5 Rooms in the same order - no duplicate echo or alpha from the demo agents reconnecting - and both transcripts came back identical: same count, same order, same sender names, same displayed times. No draft caret or Stop button anywhere after the restart. |

### STARTUPCONFIG-12 — Deleting App_Data while running fails; stopping first makes the clean slate reliable

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-12--deleting-app_data-while-running-fails-stopping-first-makes-the-clean-slate-reliable)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With the app running the delete failed with `The process cannot access the file 'E:\Repos\Huddle\src\Huddle.App\App_Data\team.db' because it is being used by another process.` - the known, deliberately-unfixed SQLite-pooling behaviour, naming team.db as required. After stopping, the delete succeeded first try and Test-Path returned False. The relaunch rebuilt App_Data containing exactly an EMPTY Teams directory plus team.db, team.db-wal and team.db-shm - no hooks.json, no appearance.json, no work directory - and no rooms directory before any message was sent. The sidebar showed exactly echo and alpha with empty transcripts. No CreateDirectory or exception line in O-LOG. |

### STARTUPCONFIG-13 — Team__AgentMessageBudget=1 pauses the Room after one agent reply and shows the Continue prompt

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-13--team__agentmessagebudget1-pauses-the-room-after-one-agent-reply-and-shows-the-continue-prompt)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With Team__AgentMessageBudget=1, the reply was followed by a `.budget-prompt` block reading exactly `Agents have sent 1 replies since you last spoke, and are paused.` with exactly two buttons, `Continue` and `Leave paused`. Placement verified by geometry, not by eye: prompt top 1594 sits below the last message bottom 301 and above the composer top 1654 - between the message list and the message box. `Leave paused` replaced the block with the single line `Paused — 1 of 1 agent replies since you last spoke.` Sending `anything` cleared both: no .budget-prompt and no budget text of any kind remained. |

### STARTUPCONFIG-14 — Continue grants Budget and produces no new reply — the trap that reads as a dead button

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-14--continue-grants-budget-and-produces-no-new-reply--the-trap-that-reads-as-a-dead-button)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Caught with a MutationObserver: while the request was in flight the button read exactly `Continuing…` with disabled=true (and `Leave paused` disabled too, so a double-grant is impossible). The alert block was then replaced by the line `1 of 2 agent replies since you last spoke.` - the second number RISEN from 1 to 2, which is the proof the grant landed. No new message arrived in the following 13 seconds (message count stayed at 7), which is the documented correct outcome, not a dead button. O-LOG carried `Room '01a0a15467a47b5ea4f594381f786754' was extended to 2 agent messages.` |

### STARTUPCONFIG-15 — Team__AgentMessageBudget=0 removes the guard entirely and hides every budget surface; a restart un-pauses everything

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-15--team__agentmessagebudget0-removes-the-guard-entirely-and-hides-every-budget-surface-a-restart-un-pauses-everything)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Restarting with the budget still 1 and the echo Room paused: the transcript came back fully intact at 9 messages while both the pause alert and the budget note were gone, and the next agent reply was allowed (11 messages). Relaunched with Team__AgentMessageBudget=0 and sent five mentions: message count went 13, 15, 17, 19, 21 - every reply allowed, no reply refused - and after each one a scan of every leaf element in main for `agent replies`, `Paused` or `budget` returned an empty array, with no .budget-prompt and no Continue button at any point. Variable cleared afterwards. |

### STARTUPCONFIG-17 — A mis-spelled configuration key changes nothing and reports nothing

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-17--a-mis-spelled-configuration-key-changes-nothing-and-reports-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Both mis-spellings bound to nothing, silently. With `Team_DemoAgent_Enabled=false` (single underscores) the app started normally, logged `Demo agent echo connected.` and `Demo agent alpha connected.`, created both direct Rooms and the sidebar showed echo and alpha. With `Team__DemoAgent__Enable=false` (correct separators, misspelled leaf) the result was identical. In BOTH runs a scan of the whole console for the variable name, `warn` and `error` returned nothing - no warning, no error, no mention. Paired with STARTUPCONFIG-16's positive control, this calibrates every other configuration result in the area. |

### STARTUPCONFIG-20 — Team__DataDir points the whole application at a different folder — a clean slate without deleting anything

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-20--team__datadir-points-the-whole-application-at-a-different-folder--a-clean-slate-without-deleting-anything)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With Team__DataDir=App_Data_test the two demo Rooms were freshly recreated with EMPTY transcripts (0 message bodies) while the real App_Data was untouched. The Settings overrides paragraph printed `E:\Repos\Huddle\src\Huddle.App\App_Data_test\hooks.json` - resolved against the process working directory, so it landed under src\Huddle.App and NOT at the repo root (E:\Repos\Huddle\App_Data_test did not exist). src\Huddle.App\App_Data_test held its own team.db, team.db-wal, team.db-shm and an empty Teams folder. Removing the variable and relaunching brought the original Rooms and their full four-message history back untouched - including the `Emre` sender line from STARTUPCONFIG-21, corroborating that transcript lines are never rewritten. App_Data_test deleted afterwards. |

### STARTUPCONFIG-21 — Renaming yourself on Settings > Profile renames you everywhere, old messages included, without rewriting a transcript

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-21--renaming-yourself-on-settings--profile-renames-you-everywhere-old-messages-included-without-rewriting-a-transcript)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Before: members line `You, echo`, old message sender `You`. With Team__HumanName=Emre the members line read exactly `Emre, echo` while the two pre-existing rows still read `You 12:20` and `echo 12:20` - untouched. The message sent after the change rendered as `Emre 12:20`, giving the mixed attribution `You, echo, Emre, echo` down the transcript, which is the documented correct outcome: the directory row is renamed in place, each transcript line keeps the sender name captured at post time. #blazor-error-ui stayed display:none throughout. |

### STARTUPCONFIG-22 — The renamed Team__Acp__PersonaDir key refuses to start the app rather than scanning nothing

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-22--the-renamed-team__acp__personadir-key-refuses-to-start-the-app-rather-than-scanning-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | The app refused to start. stderr carried `Unhandled exception. System.InvalidOperationException: Configuration key 'Team:Acp:PersonaDir' was renamed to 'Team:Acp:TeamsDir'. Update the configuration source that sets it (environment variable, user secret, etc.) - there is no automatic fallback.` word for word, and the stack named `Agency.Huddle.App.ServiceCollectionExtensions.AddTeamServices ... ServiceCollectionExtensions.cs:line 30` then `Program.`<Main>`$`. The process exited 0xE0434352 and curl on the app URL returned 000 - connection refused. Clearing the variable and relaunching started the app normally (302 on the root). The one key in this surface that fails loudly instead of binding to nothing. |

### STARTUPCONFIG-23 — An external pipe client creates a Room live, with no page refresh

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-23--an-external-pipe-client-creates-a-room-live-with-no-page-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | echo-bot printed `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...`, then `Sent hello. Listening for messages (Ctrl+C to exit)...`, then a welcome envelope carrying `"name":"mybot"` and `"version":3` - matching ProtocolVersion.cs, not the stale `"version":2` in docs/AgencyTeam.md. A `mybot` Room appeared in the sidebar with no refresh and no navigation. `hi @mybot` drew `mybot: hi mybot` with `mybot:` rendered bold, and O-WIRE printed the matching messagePosted envelope with `"mentioned":true`. After stopping the bot the `mybot` Room REMAINED in the sidebar and its New chat dot read `offline` (`agent-dot offline`, red by design) while echo and alpha stayed `online`. |

### STARTUPCONFIG-24 — Team__PipeName changes the pipe and silently orphans any external client

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-24--team__pipename-changes-the-pipe-and-silently-orphans-any-external-client)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With Team__PipeName=team-test the two demo Rooms still appeared - the demo agents dial whatever name is configured, and O-LOG logged both connecting. tools/echo-bot.ps1, left at its default -Pipe team, printed `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...` and then died with `Exception calling "Connect" with "1" argument(s): "The operation has timed out."` - it never printed a welcome line. No `mybot` Room ever appeared, watched for a further 30 seconds without refreshing, and the browser showed no error, no warning, no role=alert element and #blazor-error-ui display:none. The complete browser silence is the point of the test. Positive control for the same command is STARTUPCONFIG-23, which passed immediately before. |

### STARTUPCONFIG-26 — Environment selection decides whether ACP is on and which port is used

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-26--environment-selection-decides-whether-acp-is-on-and-which-port-is-used)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Run against the CORRECTED steps 7-8 (see Appendix B). Run (a) reported `Hosting environment: Development` / `Now listening on: http://localhost:5100`; run (b) with --no-launch-profile reported `Hosting environment: Production` / `Now listening on: http://localhost:5000`, curl on 5100 returned 000 and on 5000 returned 302. O-ADAPTERS 0 in run (b), though with no Persona files present the `even if Persona files exist` clause is recorded as not-checked. Run (b) renders UNSTYLED: Huddle.App.z3g6d1kai5.styles.css, Components/Layout/ReconnectModal.abdmv1u4y3.razor.js and _framework/blazor.web.wyu7y4jcvb.js each return 500 with `The application is not running against the published output and Static Web Assets are not enabled.` plus a FileNotFoundException. Reproduced on a SOLE instance and shown NOT to be a product defect - a `dotnet publish` build in Production serves all six assets 200. Verbosity: configured level differs (Debug vs Information) but both consoles were identical at 2 `info:` Agency.Huddle lines, correct because Huddle.App has no Debug-level statements. |

### STARTUPCONFIG-27 — Team__Acp__Enabled=false starts no agent process and costs nothing, while /agents still works fully

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-27--team__acp__enabledfalse-starts-no-agent-process-and-costs-nothing-while-agents-still-works-fully)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | With Team__Acp__Enabled=false: O-ADAPTERS 0 at baseline, 0 twenty seconds after the app was listening, and 0 again after loading the Agents page - merely browsing it spawns nothing and spends nothing. The page rendered with h1 `Agents`, a `New agent` button, the intro opening `A Persona is a Markdown file describing how one agent should behave`, and the body `No Personas yet. Choose New agent to add one.` - not blank, no error. A scan of the whole console for `Persona` and `adapter` returned nothing: no `Persona ... failed to start.` line, no adapter lines. |

### STARTUPCONFIG-28 — Team__Acp__Enabled=true with only the built-in Chief of Staff starts exactly one process

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-28--team__acp__enabledtrue-with-only-the-built-in-chief-of-staff-starts-exactly-one-process)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | ACP ON - no Team__Acp__Enabled set, so Development's `true` applied and the console confirmed `Hosting environment: Development`. With App_Data\Teams empty the app started normally and O-ADAPTERS stayed 0 at baseline and 20 seconds after listening: nothing to start, so nothing spawned. The Agents page showed `No Personas yet. Choose New agent to add one.` and the Teams folder was still empty afterwards. Together with STARTUPCONFIG-27 this brackets the flag: off spawns nothing, on-with-an-empty-library spawns nothing. |

### STARTUPCONFIG-29 — A Persona file in its own Agents folder is discovered, reloads on edit in place, and a broken file is named rather than silently dropped

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-29--a-persona-file-in-its-own-agents-folder-is-discovered-reloads-on-edit-in-place-and-a-broken-file-is-named-rather-than-silently-dropped)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | A Persona under Teams\Business was found by the startup scan: tile with monogram `CS`, name `Chief of Staff`, second line `Chief of Staff · @coo`, grouped under a `Business` heading. Editing `title:` in place while the app ran updated the tile to `Chief of Operations · @coo` with NO page refresh - the directory watcher does include sub-directories, which is the trap this test exists to catch. Adding broken.md with empty frontmatter raised a section headed exactly `Files that didn't load`, positioned ABOVE the tiles, naming `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\broken.md` and the reason `Persona frontmatter is missing required field 'Name'.` O-ADAPTERS stayed 0 throughout (ACP off). Cleaned up: Teams\Business deleted. |

### STARTUPCONFIG-30 — A stale Logging:LogLevel key silently removes the console evidence other tests rely on

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-30--a-stale-loggingloglevel-key-silently-removes-the-console-evidence-other-tests-rely-on)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Run against the CORRECTED step 3 (see Appendix B). Working tree held only this run's own doc edits; both appsettings files were clean, so each was reverted by path. As the step was WRITTEN - renaming only the Development key - the `Demo agent echo/alpha connected.` lines stayed put, because that value is Debug (nothing in Huddle.App logs at Debug) and appsettings.json sets `Agency.Huddle: Information` separately, which merges in. Control proving the prefix model is healthy: `"Agency.Huddle": "Warning"` in the overlay made both lines vanish immediately. Full reproduction with all three edits - stale `Team` rename in BOTH files plus `Default` to `Warning` - gave a COMPLETELY silent console while the app still answered 302 on the root and worked normally. No warning of any kind ever reported the unmatched key. After `git checkout` of both files the `Demo agent` lines returned. |

### STARTUPCONFIG-31 — With ACP on and one Persona present, that Persona starts at launch and its tile goes Starting then Online

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-31--with-acp-on-and-one-persona-present-that-persona-starts-at-launch-and-its-tile-goes-starting-then-online)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | ACP on (Development default), one Persona at Teams\tester.md. Tile showed monogram `T`, second line `Test agent · @tester`, status `Starting` then `Online` with a green dot rgb(63,191,90). Exactly ONE new adapter appeared (`node ...claude-agent-acp\dist\index.js`, pid 100880) against a baseline of 0, and a `Tester` Room appeared in the sidebar with no refresh - O-LOG logged `Created direct room ... for agent 'Tester'.` Saving Model=Haiku / Effort=low showed `MODEL Haiku` and `EFFORT low` on the card and really did restart the agent: the adapter pid changed from 100880 to 37132 while the count stayed at 1. A second restart, forced by editing the persona body in place, was sampled with a MutationObserver and gave the full sequence `Online` green -> `Offline` red -> `Starting` AMBER rgb(232,196,106) (`agent-dot starting`) -> `Online` green. No Turn was taken, so nothing was spent. |

### STARTUPCONFIG-32 — Team__Acp__TraceWire=true dumps the tool server's bearer token to the console

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-32--team__acp__tracewiretrue-dumps-the-tool-servers-bearer-token-to-the-console)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `66ffe58d` |  | Run against the CORRECTED step 2 (see Appendix B). With Team__Acp__TraceWire=true ALONE the console carried 0 `Agency.Huddle.Acp.Wire` lines, 0 `Authorization` and 0 `jsonrpc` - the flag binds but LoggerTraceListener forwards at Trace while Development pins Agency.Huddle at Debug. Adding `${env:Logging__LogLevel__Agency.Huddle} = 'Trace'` produced 117 Agency.Huddle.Acp.Wire lines, 81 `trce:` lines, 12 jsonrpc lines and exactly one line carrying `Authorization` / `Bearer` - the tool server's token, dumped as documented. The browser was COMPLETELY unchanged: no banner, no badge, no role=alert element, and no occurrence of trace/tracing/wire anywhere in the page text. Handled as a throwaway: the token value was never printed into this session - only counts were - and both redirected console files were deleted immediately afterwards. |

### STARTUPCONFIG-33 — A spent per-Persona token Budget reads as Degraded on the Agent tile and in the Room banner

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-33--a-spent-per-persona-token-budget-reads-as-degraded-on-the-agent-tile-and-in-the-room-banner)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e7863e44` |  | Run against the CORRECTED steps (see Appendix B) - as written the test cannot reach the state, because every Human Message zeroes tokensConsumed. Two Personas at Haiku/low in one Room: `Say hi @tester` drew `Hi there, happy to help with testing! @tester2`, Tester2 answered `Hello, good to be testing alongside you @tester.`, and that AGENT Message woke Tester with no reset - the Turn the cap refused. No third agent message arrived. Tile read `Degraded` with an amber dot rgb(224,163,62) (`agent-dot degraded`); tooltip exactly `The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.`; the Room carried a `member-health-alert` strip reading exactly `Tester is Degraded: The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.`, verified by geometry to sit between the message list and the composer; console line exactly `Persona 'Tester' has spent its token budget of 2000 and is taking no more turns until a human speaks to it.` Vocabulary is Budget/Turn/Human/Persona - no quota, rate limit, LLM or tokens-remaining. Both surfaces agree, so one resolver. |

### STARTUPCONFIG-34 — A Human Message clears a token-Budget Degraded state and lets the Persona work again

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-34--a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e7863e44` |  | Continued from 33 without restarting. The strip was still showing. `Hello again @tester` drew a reply from the Degraded Persona - `Let's get started on those tests! @tester2` - the `member-health-alert` strip disappeared, and the /agents tile returned to `Online` with a green dot rgb(63,191,90) and no tooltip. Room and tile agreed at every step. The ping-pong then hit the OTHER cap, `Agents have sent 4 replies since you last spoke, and are paused.` - Team__AgentMessageBudget doing its job, not this test's concern. Cleanup done: app stopped, both Personas deleted, Teams empty, no Team__ variables left, 0 adapters. |

### STARTUPCONFIG-35 — A blank system opens on Settings > Profile until you save a profile

File: [`docs/engineering/manual-tests/startup-config.md`](startup-config.md#startupconfig-35--a-blank-system-opens-on-settings--profile-until-you-save-a-profile)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Room view, posting Messages and Transcript rendering

### ROOMMESSAGING-01 — Landing on / redirects into the first Room, and /rooms/{id} deep-links

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-01--landing-on--redirects-into-the-first-room-and-roomsid-deep-links)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | GET / redirected to /rooms/01a0a188f66e74d5be0d7890db5351d4 - 32 hex, matched by regex. h1 read `echo`, the muted line beneath it exactly `You, echo`, and an `Add agent` button sat at the right of the header row. Sidebar children in order: new-chat, room-list (echo, alpha), sidebar-link `Agents`, sidebar-link `Settings`. Pasting the URL into a second tab opened the same Room with the same h1 and the same transcript (`marker`). The open Room's entry carried class `active` with background rgb(47,61,87) against rgba(0,0,0,0) for the other. Clicking `alpha` changed the h1 to `alpha` and the id to 01a0a188f7057c639ca37ef5cfa0a030. |

### ROOMMESSAGING-02 — Enter sends the Message, clears the textarea, and appends exactly one JSONL line

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-02--enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### ROOMMESSAGING-03 — Shift+Enter inserts a newline and does not send

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-03--shiftenter-inserts-a-newline-and-does-not-send)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Inconclusive *(earlier script)* | `a5bfb1a7` |  | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Shift+Enter arrived as a real keydown and posted NOTHING: the textarea still held `line one`, the transcript was unchanged and Lines stayed at 1. Completing the two-line value and pressing Enter posted exactly one row and Lines grew 1 -> 2. The row rendered as `<p>line one\nline two</p>` occupying a single rendered line (getClientRects().length === 1) - the CommonMark soft break, correct and not a bug. The JSONL line carried `"text":"line one\nline two"` with the literal backslash-n escape, so the newline really is stored. HARNESS NOTE: the synthetic shift+Enter does not perform the browser's default newline INSERTION, so the second line was entered as text; the app-side halves - does not send, newline preserved - are what this proves. |
| 2026-09-14 | Inconclusive *(earlier script)* | `394baa99` |  | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |
| 2026-09-14 | Pass *(earlier script)* | `f42db410` |  | Shift+Enter arrived as a real keydown and posted NOTHING: the textarea still held `line one`, the transcript was unchanged and Lines stayed at 1. Completing the two-line value and pressing Enter posted exactly one row and Lines grew 1 -> 2. The row rendered as `<p>line one\nline two</p>` occupying a single rendered line (getClientRects().length === 1) - the CommonMark soft break, correct and not a bug. The JSONL line carried `"text":"line one\nline two"` with the literal backslash-n escape, so the newline really is stored. HARNESS NOTE: the synthetic shift+Enter does not perform the browser's default newline INSERTION, so the second line was entered as text; the app-side halves - does not send, newline preserved - are what this proves. |
| 2026-09-15 | Inconclusive *(earlier script)* | `26ca4d46` |  | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |
| 2026-09-15 | Pass *(earlier script)* | `596f95a8` |  | Shift+Enter arrived as a real keydown and posted NOTHING: the textarea still held `line one`, the transcript was unchanged and Lines stayed at 1. Completing the two-line value and pressing Enter posted exactly one row and Lines grew 1 -> 2. The row rendered as `<p>line one\nline two</p>` occupying a single rendered line (getClientRects().length === 1) - the CommonMark soft break, correct and not a bug. The JSONL line carried `"text":"line one\nline two"` with the literal backslash-n escape, so the newline really is stored. HARNESS NOTE: the synthetic shift+Enter does not perform the browser's default newline INSERTION, so the second line was entered as text; the app-side halves - does not send, newline preserved - are what this proves. |
| 2026-09-15 | Inconclusive *(earlier script)* | `9e5eaf54` |  | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |

### ROOMMESSAGING-04 — An empty or whitespace-only Enter posts nothing and shows no error

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-04--an-empty-or-whitespace-only-enter-posts-nothing-and-shows-no-error)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Baseline Lines 2, against a non-empty file. Three attempts - bare Enter, three spaces then Enter, a blank line then Enter - each posted nothing: message-body count stayed at 2 and Lines stayed at 2. No red or green text appeared above the composer at any point (a scan of every leaf element for warning-coloured text returned empty). The textarea was empty after each attempt, including after the three spaces - the documented unconditional clear. |

### ROOMMESSAGING-05 — Leading and trailing whitespace is trimmed before the Message is stored

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-05--leading-and-trailing-whitespace-is-trimmed-before-the-message-is-stored)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Typed exactly `   padded   ` (verified in the textarea before sending). The transcript row rendered as `<p>padded</p>` with no leading indent, and the JSONL line carried exactly `"text":"padded"` - no space characters inside the quotes on either side. Trimming happens in the data, not just in the CSS. |

### ROOMMESSAGING-06 — A soft line break collapses to a space; a blank line makes two paragraphs; a list still works

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-06--a-soft-line-break-collapses-to-a-space-a-blank-line-makes-two-paragraphs-a-list-still-works)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Soft breaks: `<p>alpha\nbeta\ngamma</p>` - exactly one p, and getClientRects().length 1, so it really renders on ONE line. That is the CommonMark rule, not a bug. Blank line: `<p>alpha`</p>``<p>`beta</p>` - exactly two sibling p elements. List: `<ul>` with three `<li>` (one, two, three). No literal backslash-n or visible escape sequence leaked into any rendered body. |

### ROOMMESSAGING-07 — Core Markdown renders: bold, italic, heading, lists, blockquote, inline and fenced code, rule

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-07--core-markdown-renders-bold-italic-heading-lists-blockquote-inline-and-fenced-code-rule)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Every element present and no literal source characters anywhere: `<p>`<strong>`bold`</strong>`</p>`, `<p>`<em>`italic`</em>`</p>`, `<h1>Heading</h1>` at 32px against a 16px body, `<ol>`<li>`first`</li>`</ol>`, `<blockquote>`<p>`quoted`</p>`</blockquote>`, `<p>`<code>`inline code`</code>`</p>` in ui-monospace, `<ul>`<li>`a`</li>``<li>`b`</li>`</ul>`, `<pre><code class="language-cs">int x;`</code>`</pre>` in ui-monospace, and `<hr>`. No `&lt;strong&gt;`-style double escaping. |

### ROOMMESSAGING-08 — The four enabled Markdig extensions render: pipe tables, task lists, autolinks, emphasis extras

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-08--the-four-enabled-markdig-extensions-render-pipe-tables-task-lists-autolinks-emphasis-extras)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | All four extensions live. Pipe table: a real `<table>` with thead/tbody, 2 `<th>`, 2 `<td>`, 2 `<tr>` - not literal pipes. Task list: `<ul class="contains-task-list">` with two `<input type="checkbox">`, the first checked, BOTH disabled. Autolinks: `<a href="https://example.com">` and `<a href="http://www.example.com">` from bare text. Emphasis extras: `<del>`, `<mark>`, `<sup>`, `<sub>`, `<ins>` all rendered as elements. |

### ROOMMESSAGING-09 — Advanced Markdown extensions are absent: generic attributes stay literal and never become live handlers

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-09--advanced-markdown-extensions-are-absent-generic-attributes-stay-literal-and-never-become-live-handlers)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | The advanced bundle is absent and nothing executes. `# Hi {onclick="alert(1)"}` rendered as `<h1>Hi {onclick="alert(1)"}</h1>` with the braces as literal TEXT; the mouseover form likewise. Clicking the heading and hovering the paragraph for three seconds produced nothing - window.alert was trapped and counted, and the count stayed 0. A scan of every attribute on every element inside every message-body found ZERO attributes beginning `on`. `:smile:`, `^[a footnote]`, `Term / :  definition` and `::: warning` all stayed literal - no emoji, no footnote ref, no `<dl>`, no callout. The YouTube URL rendered as an ordinary `<a>`; the document contains no `<iframe>` at all. No console errors and no window.onerror events. |

### ROOMMESSAGING-10 — Raw HTML inside a Message is escaped, never executed

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-10--raw-html-inside-a-message-is-escaped-never-executed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | All four escaped to entity text and nothing ran. Bodies: `<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>`, `<p>&lt;b&gt;bold?&lt;/b&gt;</p>`, `<p>&lt;img src=x onerror=alert(1)&gt;</p>`, `<p>&lt;div style="position:fixed;inset:0;background:red"&gt;x&lt;/div&gt;</p>`. querySelectorAll for script, b, img and div inside each body returned 0 every time; `bold?` computes font-weight 400, so it is NOT bold. A screenshot confirmed no red overlay and an intact layout. window.alert was trapped and counted - 0 calls - and there were no console errors or window.onerror events. |

### ROOMMESSAGING-11 — Link schemes are rewritten: only http, https and mailto survive

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-11--link-schemes-are-rewritten-only-http-https-and-mailto-survive)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | hrefs read back exactly as required: `click me` -> `#`, `a` -> `https://example.com`, `b` -> `http://example.com`, `c` -> `mailto:someone@example.com`, `d` (data:) -> `#`, `e` (file:///) -> `#`. Clicking `click me` produced no dialog (trapped alert count stayed 0), no console error, and the URL was byte-identical before and after - it navigated nowhere. The three legitimate schemes were left verbatim, so the rewriter is not over-broad. |

### ROOMMESSAGING-12 — Non-ASCII and HTML-sensitive characters are escaped in the JSONL but render correctly on screen

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-12--non-ascii-and-html-sensitive-characters-are-escaped-in-the-jsonl-but-render-correctly-on-screen)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Run against the CORRECTED escaping expectation (see Appendix B). On screen, before and after a reload, the row read exactly `He said "go" - A&B (emoji) (CJK)` with real characters - an identity comparison against the source string returned true. In the JSONL every sensitive character is escaped, not just the non-ASCII ones: `\u0022` for the quotes, `\u0026` for the ampersand, `\u2014` for the em dash, `\uD83D\uDE00` for the emoji, `\u65E5\u672C\u8A9E` for the CJK. Select-String for the literal CJK found 0 matches. No raw `<` or `>` in any text field. The rendered HTML carried only `&amp;` for the ampersand, so no escaping leaked onto the screen. |

### ROOMMESSAGING-13 — A long Message, a very long unbroken token and a wide code block

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-13--a-long-message-a-very-long-unbroken-token-and-a-wide-code-block)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | A 5400-character prose message posted and rendered complete - rendered length 5399 and the stored text length 5399, the one-character difference being the trailing space correctly trimmed, so nothing was truncated on screen OR in the file. A 400-character unbroken token also stored complete at 400. In all three cases the sidebar stayed at left 0 and the composer bottom at 1715 against a 1731 viewport - neither was pushed off screen - and the page never gained a horizontal scrollbar (scrollWidth 746 == clientWidth 746). OBSERVATION on step 6: the long token does NOT wrap (overflow-wrap and word-break are both `normal`); it overflows to 2938px and `.message-list` takes a horizontal scrollbar of its own (overflow-x: auto), with `.main-column` overflow:hidden containing it. The wide `<pre>` behaves the same way at 2813px. Overflow is confined to the transcript column, which is the designed containment. |

### ROOMMESSAGING-14 — Messages render in post order, and the order on screen equals the order in the file

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-14--messages-render-in-post-order-and-the-order-on-screen-equals-the-order-in-the-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | The five rows read top to bottom `1`, `2`, `3`, `4`, `5`, and the echo reply sat immediately below the `hi @echo` that prompted it. After a reload the on-screen order was identical, 52 rows. The texts parsed out of the JSONL, read top to bottom, matched the on-screen list row for row at 52 lines each. Duplicate hunt: sending `hi @echo again` and switching to alpha and back within a second produced exactly ONE `echo: hi echo again` row (filtered count 1) and 54 rows total - no duplication. |

### ROOMMESSAGING-15 — The transcript survives a browser reload and is present in the prerendered HTML

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-15--the-transcript-survives-a-browser-reload-and-is-present-in-the-prerendered-html)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | After a reload every Message was still present, same order, same sender names and same HH:mm times (54 rows, meta lines `echo 13:15`, `You 13:16`, `echo 13:16`). The view was scrolled to the BOTTOM after the reload - scrollTop 3906 equal to scrollHeight-clientHeight - not parked at the top. A second tab on the same URL showed an identical 54-row transcript. `curl.exe` on the room URL produced a prerendered HTML file containing both `hi echo again` and `lorem ipsum`, proving the transcript is in the server HTML before any live connection. (No private-window mode in this browser pane; the second tab covers the same concern, and there is no auth or per-browser storage in this path.) |

### ROOMMESSAGING-16 — Sender name, avatar and time on an agent row; the Human bubble; grouping and the day separator

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-16--sender-name-avatar-and-time-on-an-agent-row-the-human-bubble-grouping-and-the-day-separator)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Meta lines read `You 13:16` for a Human Message and `echo 13:16` for an agent reply - the agent's Name, never a 32-hex id. The time is 24-hour HH:mm: four digits and a colon, no seconds, no AM/PM, no date. Messages a minute apart showed 13:15 and 13:16. Neither time changed across a reload or in a second tab. The stored UTC timestamp for the last row converted to local time gives 13:16, matching the screen exactly, and its stored senderName `echo` matches the Name shown. No avatar, circle or monogram anywhere in the transcript (selector count 0). |

### ROOMMESSAGING-17 — The transcript autoscrolls on a new Message and while a Draft grows

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-17--the-transcript-autoscrolls-on-a-new-message-and-while-a-draft-grows)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `51f30fa2` |  | Sending while at the bottom kept the new row fully visible and left scrollTop exactly equal to scrollHeight-clientHeight (delta 0). Scrolled deliberately to scrollTop 0, sending `hi @echo ...` snapped the view back to the bottom WHILE THE DRAFT WAS STILL LIVE - a MutationObserver caught the jump from top 0 to top 4058 on a sample whose draft flag was true - so the scroll follows Draft growth, not just settled Messages. It came to rest at scrollTop 4078 == max 4078 with the last row visible. No console errors in the test window; the only errors in the buffer are the ERR_CONNECTION_REFUSED reconnect attempts from the deliberate app stop at 20:08, well before this test. |

### ROOMMESSAGING-21 — /invite @name from the composer: green info line, Room rename, live sidebar update

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-21--invite-name-from-the-composer-green-info-line-room-rename-live-sidebar-update)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | No transcript row for the command - body count stayed 70 and no row contains `/invite`. A GREEN line appeared above the composer, class `composer-info`, colour rgb(111,201,122), reading exactly `Invited alpha. Room is now "echo, alpha".` The h1 became `echo, alpha`, the members line `You, echo, alpha`, and the sidebar entry `echo, alpha` with NO page refresh. Lines was unchanged at 70. O-LOG carried `Invited agent 'alpha' (01a0a188f667743296797d17d53da959) into room '01a0a188f66e74d5be0d7890db5351d4'.` Sending `after invite` cleared the green line, and `hi @alpha` then drew `alpha: hi alpha` in this Room. |

### ROOMMESSAGING-22 — Composer errors: unknown command, unknown agent, and the typed text is lost

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-22--composer-errors-unknown-command-unknown-agent-and-the-typed-text-is-lost)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | All four rejected submits produced a RED `composer-error` line, colour rgb(255,138,138), with the exact wording required: `/hello` and bare `/invite` and `/usr/local/bin is where it lives` each gave `Unknown command`, and `/invite @nobody` gave `Unknown agent @nobody`. Nothing was posted - the body count stayed 73 across all four and no row contains `/usr/local/bin`. In every case the textarea was already empty, so the typed text is gone (the documented cost this test exists to pin). Lines was unchanged across all four. Sending `recovered` cleared the red line and posted normally (74). O-LOG still holds exactly ONE `Invited agent` line - the legitimate one from ROOMMESSAGING-21. |

### ROOMMESSAGING-23 — Typed-but-unsent composer text carries over into the Room you switch to

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-23--typed-but-unsent-composer-text-carries-over-into-the-room-you-switch-to)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | After switching from echo to alpha the composer STILL held `this was meant for echo`. Pressing Enter posted it into the Room whose id was in the address bar at that moment - alpha - and the text is in alpha's JSONL (1 match) and NOT in echo's (0 matches). Navigating away to a non-room page and back into a Room left the composer EMPTY. NOTE on procedure: immediately after a sidebar click, focus sits on the clicked link (SHELLNAV-15's FocusOnNavigate), so a bare Enter does nothing until you click into the composer - the first Enter posted nothing and that is not a defect. I used Settings rather than Agents for the navigate-away leg; same substance. |

### ROOMMESSAGING-24 — The JSONL Transcript: shape, append-only, and one file per Room

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-24--the-jsonl-transcript-shape-append-only-and-one-file-per-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Both files under App_Data\rooms are named exactly 32 hex characters plus .jsonl (regex-checked). Every one of the echo file's 74 lines parses as a single JSON object carrying exactly the five fields id, senderId, senderName, text, timestamp - zero lines with any other field set. Every timestamp ends `+00:00` (first: 2026-09-14T20:08:47.2168442+00:00). Human lines carry senderId `human` / senderName `You`; the agent line carries senderId 01a0a188f667760f8b2c37665f5d920f (32 hex) / senderName `echo`. Append-only proven by Compare-Object against a snapshot taken before three more posts: 3 differences, EVERY side indicator `=>`, none `<=` - no existing line changed or vanished. Posting in echo left alpha's file at 1 line, unchanged. |

### ROOMMESSAGING-25 — An external pipe client's Room and Messages appear live, with no refresh

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-25--an-external-pipe-clients-room-and-messages-appear-live-with-no-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Starting tools/echo-bot.ps1 as `mybot` made a `mybot` Room appear in the sidebar with no page refresh, listed exactly ONCE. Opening it and sending `hi @mybot` drew `mybot: hi mybot` with no reload. O-LOG carried `Created direct room '01a0a1951b3675b388bcd083a4f738b0' for agent 'mybot'.` and a JSONL file for the new Room exists under App_Data\rooms. After stopping the bot the Room stayed in the sidebar, the transcript stayed intact, and `still here` posted successfully with no reply. #blazor-error-ui computed display:none at every check - no yellow `An unhandled error has occurred.` banner at any point, including across the disconnect. |

### ROOMMESSAGING-26 — A Mention of a Name containing spaces resolves against the Room's Members

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-26--a-mention-of-a-name-containing-spaces-resolves-against-the-rooms-members)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | A `Chief of Staff` Room appeared in the sidebar with no refresh and its members line read exactly `You, Chief of Staff`. `hello @Chief of Staff` drew `Chief of Staff: hello Chief of Staff` - the whole multi-word Name was treated as ONE Mention. `hello @Chief` drew NO reply (a prefix of a Member Name does not match) and `hello @Chief of Staffing` drew NO reply (the Mention ends at a word boundary), each watched for ten seconds. Message counts went 2, 3, 4 - only the first added an agent line. The JSONL stores each Message verbatim, at-signs and spaces included. NOTE: this covers the HUMAN-typed multi-word Mention; Appendix C's gap is a real model writing one, which is still uncovered. |

### ROOMMESSAGING-27 — appearance.json no longer customises the Message font: the body just follows the Theme, and a legacy override key is silently inert

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-27--appearancejson-no-longer-customises-the-message-font-the-body-just-follows-the-theme-and-a-legacy-override-key-is-silently-inert)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | With the hand-written override in place, ONLY the Message bodies changed: computed font-family on `.message-body` was `Georgia, serif`, while `.message-meta`, the composer textarea, the sidebar room links and the Room h1 all kept `-apple-system, Segoe UI, Roboto, ...` sans. The streaming Draft was sampled four times by MutationObserver while the caret was live and was `Georgia, serif` every time - correct, it shares message-body styling. The `theme` key in the same file took effect too: themes/huddle-dark.css was layered fourth and one head `<style>` carried the token. The file's SHA256 was byte-identical before and after the app read it (186C92FC...FCF6) - the app did not rewrite a hand-edited file. Deleting it and reloading restored the sans font everywhere, 3 stylesheets and 0 head styles. |

### ROOMMESSAGING-28 — A torn or corrupt line in the Transcript is skipped with a warning, not fatal

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-28--a-torn-or-corrupt-line-in-the-transcript-is-skipped-with-a-warning-not-fatal)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Three bad lines injected into a 79-line transcript: garbage at line 3, a truncated object at line 81, a blank line at 82. The Room opened normally - no exception page, no error banner (#blazor-error-ui display:none), main column populated - and all 79 well-formed Messages rendered in the original order, first three `marker` / `line one\nline two` / `padded` and last three `append three` / `hi @echo` / `echo: hi echo`. No malformed line appeared as a row. O-LOG named exactly the right positions: `Skipping unparsable line 3 in room 01a0a188f66e74d5be0d7890db5351d4.` and `Skipping unparsable line 81 ...`. The blank line produced no warning and no row. OBSERVATION, script corrected: the pair repeats once per READ of the file - twelve lines across six renders - so a tester must count distinct line numbers, not warning lines. |

### ROOMMESSAGING-29 — The transcript survives an application restart; Drafts and the Budget do not

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-29--the-transcript-survives-an-application-restart-drafts-and-the-budget-do-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Before: 79 rows, file hash EAF83484...7C36. Sent `hi @echo` and killed the app immediately; the exchange had completed, so the file grew to 81 lines (hash 2DCC12E5...EA8B) carrying `hi @echo` and `**echo:** hi echo`. After relaunch the transcript came back at 81 rows in the same order with the same senders and times (`echo 13:24`, `You 13:26`, `echo 13:26`), and the file was unchanged by the act of restarting. NO draft row, caret or Stop button survived (selector count 0) and NO budget panel or note was present (count 0). Deleting team.db* and relaunching made the old Rooms vanish from the sidebar - freshly seeded echo/alpha at new ids, the old id absent - while all four .jsonl files stayed on disk, so the two stores really are separate. NOTE: the demo reply completes in about 150ms, so the stop could not be landed inside the streaming window; the Draft-loss half rests on no Draft surviving any restart plus ROOMMESSAGING-18's proof that Draft text never reaches the file. |

### ROOMMESSAGING-30 — The Budget pause panel sits between transcript and composer, and a Human Message resumes it

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-30--the-budget-pause-panel-sits-between-transcript-and-composer-and-a-human-message-resumes-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Run against the CORRECTED steps 7 and 11 (see Appendix B) - as written the test cannot reach a budget of 2, because a Human Message resets the counter. At budget 2: the first reply gave the grey line exactly `1 of 2 agent replies since you last spoke.`; a SECOND `hi @echo` left it at `1 of 2` (the reset). Inviting alpha and sending one `hi @echo and @alpha` drew two replies and raised the panel reading exactly `Agents have sent 2 replies since you last spoke, and are paused.` with buttons `Continue` and `Leave paused`, verified by geometry to sit BELOW the last transcript row and ABOVE the composer. The panel survived F5 unchanged. `Leave paused` replaced it with exactly `Paused — 2 of 2 agent replies since you last spoke.`; `resuming` cleared the pause and a following `@echo` mention was answered. A MutationObserver caught the Continue grant in flight: `Continuing…` with disabled=true on BOTH buttons, then the note `2 of 4 agent replies since you last spoke.`, and O-LOG carried `Room '01a0a19a000b755fa9f49c87a163b138' was extended to 4 agent messages.` Refusal reached on a separate run at budget 1: O-LOG carried `Room '...' refused a message from 'echo': its budget of 1 agent messages since a human last spoke is spent.`, the refused reply is absent from the JSONL (only alpha's reply was stored after that Human Message), and echo's unrendered Draft `**echo:** hi echo and alpha` stayed on screen - expected, since a Draft clears only when its Message posts. |

### ROOMMESSAGING-31 — The Room view survives a server stop and restart with the browser open

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-31--the-room-view-survives-a-server-stop-and-restart-with-the-browser-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `3dc675cf` | #16 | Same defect as SHELLNAV-26, reached from another angle - no duplicate issue opened, a comment was added to #16 instead. Step 3 required exactly ONE state paragraph; TWO were visible six seconds after the stop: `["Rejoining the server...", "Rejoin failed... trying again in 4 seconds."]`, dialog class `components-reconnect-show components-reconnect-retrying`. NOTE the Fail-if here only describes the SIX-paragraph case (scoped CSS missing) and would send a tester after a 404 that is not there. Everything else PASSED: dimmed backdrop present (::backdrop rgba(0,0,0,0.6)); no yellow banner at any point; after the restart the browser reconnected on its own with the modal gone and the transcript intact at 20 rows; `back online` posted (21); all three shell stylesheets returned 200 including Huddle.App.z3g6d1kai5.styles.css; and `sent while down` appears neither on screen nor in the JSONL - typed while disconnected, it was neither posted nor queued. |

### ROOMMESSAGING-32 — A real Claude Agent's reply streams and renders through the same path (COSTS MONEY)

File: [`docs/engineering/manual-tests/room-messaging.md`](room-messaging.md#roommessaging-32--a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3dc675cf` |  | Real Haiku/low Persona `Scribe`, ONE prompt, one turn. A MutationObserver caught 19 states: a Draft row appeared with sender `Scribe`, a `Stop` button and a `.draft-caret`, and its text grew incrementally as PLAIN text with literal `#` and `-` markers (`# Formatting Test`, then `- This is the first item...`) - never rendered mid-stream. It settled into `<h1>Formatting Test`</h1>``<ul>``<li>`x3`</li>``</ul>``<pre>``<code>`echo "Hello, World!"`</code>``</pre>``<p>`<a href="https://example.com">Example Link`</a>`</p>` with caret and Stop both gone. No tool-activity line appeared at any point (an italic-leaf scan returned empty on every sample) and none is in the file. Lines grew by exactly 2 and the reply line carries senderName `Scribe`. No model-catalog warning. OBSERVATION worth its own follow-up: O-LOG did warn `Requested effort 'low' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.` - the MODEL selection reached the adapter but the EFFORT did not, silently except for that line. The same warning appeared in STARTUPCONFIG-31 and -33. |

## Turn streaming, Drafts, Stop and failure surfacing

### STREAMINGTURN-01 — In the stock configuration no Room shows a member-health alert strip

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-01--in-the-stock-configuration-no-room-shows-a-member-health-alert-strip)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | No `failed to start` line in O-LOG since startup. `document.querySelectorAll(".member-health-alert").length` was 0 in every Room, and a fetch of each of the four room URLs found the string nowhere in the served HTML either - so nothing is in the prerender being stripped by the interactive render. Invoke-WebRequest against each room printed 0 matches. Connecting `echo2` with tools/echo-bot.ps1 and then disconnecting it left the count at 0 - mere pipe absence stays silent in the Room, which is the point. /agents showed `No Personas yet.` (no tiles at all), which the pass condition allows. Also checked the Room of a Persona whose file had been deleted - still no strip. |

### STREAMINGTURN-02 — A Draft appears as a distinct live row while a Turn is being written

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-02--a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### STREAMINGTURN-03 — Draft text renders as plain text and becomes Markdown only when the Message lands

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-03--draft-text-renders-as-plain-text-and-becomes-markdown-only-when-the-message-lands)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | While streaming, the row read the literal `This reply arrives **slowly**.` with the asterisks visible and zero `<strong>` elements inside the body. The instant the Message posted it re-rendered as `<p>This reply arrives `<strong>`slowly`</strong>`.</p>` with no asterisks. The persisted line holds the RAW text `"text":"This reply arrives **slowly**. "` - asterisks intact, no HTML - so only the rendering differed. |

### STREAMINGTURN-04 — The Draft is replaced by exactly one Message and never shown twice

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-04--the-draft-is-replaced-by-exactly-one-message-and-never-shown-twice)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | After the post exactly ONE row on the page contains `This reply arrives`, `document.querySelectorAll(".streaming").length` is 0, and the surviving row carries sender + timestamp `drip 13:36` plus rendered text with one `.message-time` element. The transcript holds exactly one line and its `id` field equals the MESSAGEID the deltas carried (eb5d4a7ef35a4375ab07629852b9f943) - a single Select-String match, so the Turn was persisted once. |

### STREAMINGTURN-05 — The Stop button exists beside a live Draft and nowhere else, styled as an ordinary secondary action

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-05--the-stop-button-exists-beside-a-live-draft-and-nowhere-else-styled-as-an-ordinary-secondary-action)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | With no Draft in flight `document.querySelectorAll(".draft-stop").length` was 0. With one live Draft it was exactly 1, sitting inside that Draft's meta line beside the sender - the parent reads `drip Stop`. No settled `.message-row` carried one (filter count 0). Computed styles are muted neutrals, not a danger colour: background rgb(42,42,49), border rgb(74,74,86), colour rgb(195,195,204) - the same secondary greys as the rest of the page. Clicking Stop raised nothing: #blazor-error-ui stayed display:none and there were 0 .composer-error / [role=alert] / .member-health-alert elements. |

### STREAMINGTURN-06 — A page reload mid-Turn shows the whole Draft so far, not an orphaned suffix

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-06--a-page-reload-mid-turn-shows-the-whole-draft-so-far-not-an-orphaned-suffix)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | The first painted content after F5 already held ALL the text that had arrived - `This reply` after two chunks, and on a second run the full `This reply arrives **slowly**.` - never an empty row filled only by later chunks. It carried on growing live after the reload (`This reply` -> `This reply arrives`) with no second reload. The server-rendered HTML contains the Draft: `draft-stop` 1, `draft-caret` 1, `message-row streaming` 1 and the draft text 2 matches. Clicking Stop immediately after a reload produced no error and no yellow `An unhandled error has occurred.` bar. |

### STREAMINGTURN-07 — Two browser tabs on the same Room show and update the same Draft

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-07--two-browser-tabs-on-the-same-room-show-and-update-the-same-draft)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | Two tabs on the same Room both showed the same growing Draft text (`This reply arrives **slowly**.` in each, sampled together), and when the Message posted BOTH replaced the Draft with the settled `<p>This reply arrives `<strong>`slowly`</strong>`.</p>` - .streaming count 0 and 2 rows in each tab. O-LOG printed zero `A DraftChanged handler threw and was skipped.` lines. |

### STREAMINGTURN-08 — A Draft in one Room never leaks into another and survives navigating away and back

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-08--a-draft-in-one-room-never-leaks-into-another-and-survives-navigating-away-and-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | While drip streamed, navigating to the `echo` Room showed 0 `.streaming` elements, 0 `.draft-stop`, and no drip text anywhere in the page - watched for ten seconds while another chunk landed. Returning to the drip Room showed the Draft still present and carrying the chunk that had arrived while away: it read `This reply` on leaving and `This reply arrives` on return, then kept growing. |

### STREAMINGTURN-09 — Draft rows always render below every Message, whatever the true chronology

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-09--draft-rows-always-render-below-every-message-whatever-the-true-chronology)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | Sent while drip was mid-Turn, the Human Message rendered ABOVE the still-streaming row - DOM order was [drip settled, drip settled, `You 13:39 posted while drip is still writing`, streaming `drip Stop This reply arrives **slowly**.`]. When drip's Message posted it settled BELOW the Human Message (`drip 13:40` last) and .streaming went to 0. The last two transcript lines are in that same order: `You :: posted while drip is still writing` then `drip :: This reply arrives **slowly**.` |

### STREAMINGTURN-10 — The message list auto-scrolls as a Draft grows, not only when a Message arrives

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-10--the-message-list-auto-scrolls-as-a-draft-grows-not-only-when-a-message-arrives)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | Viewport shrunk to 900x520 so the list really scrolled (max 736). Through six long chunks the list stayed pinned to the bottom without the scrollbar being touched: scrollTop tracked scrollHeight-clientHeight at every sample (737/736, then 759/759 as the draft grew), and the newest draft text stayed visible. Scrolling UP to 336 while chunks were still arriving pulled the view back to the bottom on the next chunk (top 737, atBottom true) - the documented by-design behaviour, not a fault. No erratic jumps and the list never went to the top. |

### STREAMINGTURN-11 — A Draft dies with its Agent's connection - no frozen half-reply is left behind

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-11--a-draft-dies-with-its-agents-connection---no-frozen-half-reply-is-left-behind)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | Killing the drip client mid-Draft cleared the streaming row from the open page with no reload: `.streaming` 0 and `.draft-caret` 0 within ten seconds, no error banner (#blazor-error-ui display:none). The `drip` Room is still in the sidebar. The transcript's last line is the earlier filler Message, and `scroll test` - the partial draft text - appears 0 times in the file. Step 6's `Agent connection `<id>` ended.` line did NOT appear: AgentConnection logs it only from its catch(IOException), and a killed client can end the read loop without one. It is not a pass condition; the step now says so. |

### STREAMINGTURN-12 — Tool calls show as rows under the Draft, at most six, and never reach the Transcript

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-12--tool-calls-show-as-rows-under-the-draft-at-most-six-and-never-reach-the-transcript)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | A tool call alone opened the Draft: BEFORE any text arrived the row existed with sender `drip`, a `.draft-caret`, a `Stop` button, an empty message body, and exactly one `.tool-activity` beneath it reading `Reading Persona.cs`. The line shows the title only - no status word (a regex for inProgress/completed/pending/failed found none) and no JSON - and it is a DIV.tool-activity, italic, 12.8px, muted rgb(125,125,136), nested inside the streaming row. The second tool call REPLACED the first: count stayed 1 and the text became `Writing notes.md` with `Reading Persona.cs` gone from the page. The no-title envelope left ZERO tool-activity elements. After the post the settled row had none, and the transcript contains 0 matches for `Reading Persona.cs`, `Writing notes.md`, `toolCallId` and `toolActivity`. |

### STREAMINGTURN-13 — A Draft written for a Room the sender is not a Member of is refused and nothing renders

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-13--a-draft-written-for-a-room-the-sender-is-not-a-member-of-is-refused-and-nothing-renders)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | Streaming into a Room the sender is not a Member of rendered nothing: the `echo` Room showed 0 `.streaming` elements and no drip text through 10 seconds of chunks, and drip's OWN Room showed 0 too, so the Draft did not land elsewhere either. With the message id changed to the literal `not a valid id!` no Draft rendered anywhere (streaming 0, caret 0). Path safety held: every file under App_Data\rooms still matches ^[0-9a-f]{32}\.jsonl$ - 7 files, none with an unsafe name. |

### STREAMINGTURN-14 — Two Agents stream into one Room at once and neither Draft is dropped

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-14--two-agents-stream-into-one-room-at-once-and-neither-draft-is-dropped)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | In the Group Room `drip, drop` (members `You, drip, drop`) TWO separate streaming rows were visible at once - `.message-row.streaming` count 2 - one labelled `drip` and one `drop`, each with its own `.draft-caret` and its own `.draft-stop`. Neither text contained the other's words: `drip one drip two drip three drip four` against `drop ONE drop TWO drop THREE drop FOUR`, no interleaving. Killing the drip client removed ONLY drip's row; drop's stayed and later settled normally. The transcript gained exactly ONE line - `drop :: drop ONE drop TWO drop THREE drop FOUR` - because drip was killed before posting. |

### STREAMINGTURN-15 — Stop disables itself while the request is in flight, per Agent rather than per Room

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-15--stop-disables-itself-while-the-request-is-in-flight-per-agent-rather-than-per-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | Run with the LISTENING client for both Agents (see Appendix B - `drip.ps1` cannot answer this test). A MutationObserver caught the full cycle on a double click: [drip:false, drop:false] -> [drip:TRUE, drop:false] -> [drip:false] -> [drip:TRUE] -> [drip:false]. So drip's Stop disables while the request is in flight and re-enables afterwards, twice, and drop's Stop stayed enabled at every single sample - the busy flag is per AGENT, not per Room. With drip.ps1 instead, its computed style while disabled was opacity 0.6 and cursor `default` against opacity 1 / cursor `pointer` for the enabled one. The double click produced no additional effect and no error (#blazor-error-ui display:none). |

### STREAMINGTURN-16 — Clicking Stop on an Agent with no live connection is a silent no-op

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-16--clicking-stop-on-an-agent-with-no-live-connection-is-a-silent-no-op)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e14a49de` |  | A JS timer fired the Stop click 1.5 s AFTER the client was killed, so it landed on a connection that was already gone (`clickResult: "clicked"`). Nothing happened: #blazor-error-ui display:none, 0 .composer-error or [role=alert] elements, and an installed window.onerror + console.error trap recorded zero entries. The button then disappeared with the row (.streaming 0, .draft-stop absent). O-LOG carried only the ordinary AgentConnection warning. |

### STREAMINGTURN-17 — A Stop click reaches the wire even when the client ignores it

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-17--a-stop-click-reaches-the-wire-even-when-the-client-ignores-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | A listening pipe client printed exactly ONE envelope after a single Stop click: `{"type":"stopTurn","roomId":"01a0a1a25dfb7a1cba0afcccf78bad2c","version":3}` - the roomId is the Room the click was made in, and the stopTurn count in the whole capture is 1, so the button fires once per click. The Draft stayed on screen afterwards (.streaming still 2, text unchanged) because this client ignores stopTurn - expected, not a failure. |

### STREAMINGTURN-18 — A Draft stops growing at 256 KB but the posted Message still arrives intact

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-18--a-draft-stops-growing-at-256-kb-but-the-posted-message-still-arrives-intact)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | A 300-chunk client sent 307200 characters. The Draft row grew and then stopped lengthening at EXACTLY 262144 characters (256 KB), holding that length across four samples spread over 24 seconds - text already shown was never removed or shortened, and the row stayed visible. The browser stayed responsive throughout (sidebar still enumerated 9 rooms during the load). After the post the settled row carried the full 307200 characters and the persisted `text` length is 307200, NOT the 262144 cap - the Draft cap bounds only the live view, never the Message. |

### STREAMINGTURN-19 — Restarting the application loses every Draft in flight, and nothing resurrects as a Message

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-19--restarting-the-application-loses-every-draft-in-flight-and-nothing-resurrects-as-a-message)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | With a Draft two chunks in, stopping the app and the client and relaunching left NO trace of it: `.streaming` 0 and `.draft-caret` 0, and no partial text resurrected as a Message. The transcript line count was identical before and after (6 -> 6) and the `drip` Room was still in the sidebar with all 9 rooms intact. |

### STREAMINGTURN-20 — A spent Room Budget shows the Continue prompt and never a health strip

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-20--a-spent-room-budget-shows-the-continue-prompt-and-never-a-health-strip)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | Run against the CORRECTED steps 3-5 and 9 (same reset rule as ROOMMESSAGING-30 / STARTUPCONFIG-33; the written three-messages recipe left the note at `1 of 2` and never paused - observed). With alpha invited and ONE Message mentioning both, the Room showed exactly `Agents have sent 2 replies since you last spoke, and are paused.` with `Continue` and `Leave paused`, and `document.querySelectorAll(".member-health-alert").length` was 0 - no health strip alongside the pause. /agents showed zero `Degraded` labels and does not list echo at all (a demo agent is not a Persona). Clicking Continue moved the note to `2 of 4` and the next `@echo hello four` was answered. Refusal captured on a separate budget-1 run in this same Room: `warn: Agency.Huddle.App.Services.ChatService` / `Room '01a0a19cc643771081fb96d9b9bf46eb' refused a message from 'echo': its budget of 1 agent messages since a human last spoke is spent.` - with no health line anywhere alongside it and still 0 member-health-alert elements. |

### STREAMINGTURN-21 — A failed Persona start surfaces as a role="alert" strip in every Room it is a Member of

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-21--a-failed-persona-start-surfaces-as-a-rolealert-strip-in-every-room-it-is-a-member-of)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | Nova created through the UI at Haiku/low came up Online with one adapter and NO strip in her Room. Relaunched with Team__Acp__Command=definitely-not-node: the Room showed a bordered DIV reading exactly `Nova is Offline: Failed to start 'definitely-not-node'.`, carrying class `member-health-alert` and `role="alert"`, border 0.8px solid, positioned by geometry BELOW the message list and ABOVE the composer. O-LOG carried `warn: Agency.Huddle.App.Acp.PersonaSupervisor` / `Persona 'Nova' failed to start.` with `AgentProcessStartException: Failed to start 'definitely-not-node'.` /agents showed Nova **Offline** with a RED dot rgb(224,90,90), the same reason as the tile tooltip, a `Restart` button, and the exact hint `Like saving an edit, this restarts the agent, which clears what it remembers.` Offline, not Degraded. |

### STREAMINGTURN-22 — Restart from the Agent card clears the failure and the Room's strip goes with it

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-22--restart-from-the-agent-card-clears-the-failure-and-the-rooms-strip-goes-with-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `01998e94` |  | A `Restart` button was offered for a Persona that had NEVER started. A MutationObserver caught the in-flight label: `Restart\|d=false` -> `Restarting…\|d=true` -> back to `Restart\|d=false`, so it is disabled while the request runs. With the broken command still set the card settled back to Offline with the same reason. After fixing the cause and relaunching, /agents showed Nova **Online** with a green dot rgb(63,191,90) and NO reason tooltip, and the second Room tab - opened earlier and never reloaded - no longer had a `.member-health-alert` element. `src\Huddle.App\App_Data\work\Nova` exists. |

### STREAMINGTURN-23 — A stored Model the adapter does not advertise degrades the Persona but does not stop it

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-23--a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `9a952cb0` | #17 | No strip at all between transcript and composer; the card read Online with MODEL claude-not-a-real-model; @Nova say ok -> ok. Only trace: warn DotAcpAgentHost Requested model 'claude-not-a-real-model' is not in the agent's advertised model catalog; continuing on the agent's default. PersonaRunner raises Degraded inside StartAsync; PersonaSupervisor reports Online on the next statement and PersonaHealth is last-write-wins. |

### STREAMINGTURN-24 — Stop actually ends a real Turn: the Draft goes, no Message is posted, nothing is written

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-24--stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `9a952cb0` |  | Stopped at '1..18'; the streaming row was gone on the first poll after the click and no Draft reappeared in a 30 s MutationObserver watch. Partial text nowhere on the page. Transcript 2 -> 3 lines (my prompt only). Log: 'info: Agency.Huddle.App.Acp.PersonaRunner[0] / Persona ''Nova'' turn in room 01a0a1b70a11735ab4f1dd68b7653f5b was stopped.' and zero 'failed to process a turn'. Follow-up '@Nova say ok' answered 'ok'. |

### STREAMINGTURN-25 — A stopped Turn is not a failure: no alert strip, no Degraded badge, no broken streak

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-25--a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `9a952cb0` |  | Three consecutive Stops (at 20, 28, 15 numbers): member-health-alert count 0 after each, no budget prompt, no red text. Card and tile stayed Online/green with no reason line and zero Restart buttons after the third. Log: exactly 3 new 'Persona ''Nova'' turn in room ... was stopped.' at info and 0 'failed to process a turn'. '@Nova say ok' answered 'ok'. |

### STREAMINGTURN-26 — Stop ends this Room's live Turn and discards everything queued behind it in that Room

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-26--stop-ends-this-rooms-live-turn-and-discards-everything-queued-behind-it-in-that-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `9a952cb0` |  | Four prompts sent within ~0.8 s; Stop clicked once at '23,24,25'. A MutationObserver on the whole chat-page recorded 0 Draft appearances over the following minute and the Room stayed silent. Transcript 10 -> 14 lines - my four Human Messages, zero agent lines. Log: exactly one new 'was stopped.' and nothing at all logged after it. |

### STREAMINGTURN-27 — Stopping an Agent in one Room does not touch its Turn in another Room

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-27--stopping-an-agent-in-one-room-does-not-touch-its-turn-in-another-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `9a952cb0` |  | ROOM B 01a0a1c1f4fa7cb69634097a7fb2dc9d (Nova+echo), ROOM A 01a0a1b70a11735ab4f1dd68b7653f5b. First attempt hit the script's own retry note - 'count to 300' finished before the Stop - so retried with 'count to 3000'. Stopped in B at 1637; the Draft went at once. A's queued Turn produced 0 Drafts across 45 s of MutationObserver watching. Log: exactly one new 'Persona ''Nova'' turn in room 01a0a1c1f4fa7cb69634097a7fb2dc9d was stopped.' - the Room clicked in. Transcripts A 16 -> 17 and B 2 -> 3, both my own prompt only. |

### STREAMINGTURN-28 — A spent per-Persona token Budget reads as Degraded, and a Human Message clears it

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-28--a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f46222b1` |  | Run against the corrected steps (Nova + Nova2 bouncing). Exactly two Drafts - Nova then Nova2 - and no third across a 25 s MutationObserver watch. Strip verbatim: 'Nova is Degraded: The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.' on an element with class member-health-alert, role=alert, sitting between message-list and budget-note. No 'rate limit' / 'quota' / 'cap'. Log: 'warn: Agency.Huddle.App.Acp.PersonaRunner[0] / Persona ''Nova'' has spent its token budget of 1 and is taking no more turns until a human speaks to it.' The next Human Message was answered ('Hello again @nova2!'), proving the reset. |

### STREAMINGTURN-29 — A Turn that fails mid-flight reports Degraded in the Adapter's own words, and escalates on the third failure in a row

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-29--a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Inconclusive *(earlier script)* | `f46222b1` |  | Inconclusive - could not break the provider cleanly, so not run, per the test's own Inconclusive clause. Tried both non-destructive breaks the script sanctions as 'an adapter configuration that fails at prompt time': ANTHROPIC_BASE_URL at a dead port, then at a local endpoint answering 401 authentication_error. Get-NetTCPConnection confirms the agent reached that endpoint repeatedly, so the break landed - but neither produced a FAILED Turn. The prompt simply never returns: 60+ s with no Draft, no strip, no reply and not one new line in the app log. Network disconnection and revoking the claude CLI login are the only options left and both are destructive to this machine, so they were not attempted. Not filed: the hang is the adapter's own retry behaviour, which this test's NOTE forbids filing. |

### STREAMINGTURN-30 — Killing the adapter process mid-Turn clears the Draft and says so, instead of deafening the Agent forever

File: [`docs/engineering/manual-tests/streaming-turn.md`](streaming-turn.md#streamingturn-30--killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f46222b1` |  | O-ADAPTERS-LIST gave exactly one Adapter (pid 57660); killed it at '12,13'. The Draft was gone on the first poll after the kill - no freeze. Strip: 'Nova is Offline: Its event reader ended unexpectedly: The agent process disconnected.' on member-health-alert / role=alert directly after message-list, later 'Nova is Offline: The Adapter process disconnected.' - both are accepted reasons. Tile went Offline with a red dot. Log carried BOTH 'Persona ''Nova'' agent event stream ended unexpectedly.' and 'Persona ''Nova'' failed to process a turn in room 01a0a1b70a11735ab4f1dd68b7653f5b.' at warn. '@Nova are you there' drew no reply while dead. Restart from the card went Starting -> Online and '@Nova say ok' answered 'Ok'. Transcript 19 -> 23: my three prompts plus the one reply, no agent line for the killed Turn. |

## Reply Gate, Mentions and Room Budget

### REPLYGATEBUDGET-02 — Three or more Members makes the same Room Mention-gated

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-02--three-or-more-members-makes-the-same-room-mention-gated)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### REPLYGATEBUDGET-03 — /invite from the composer, and its two error strips, never touch the Transcript

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-03--invite-from-the-composer-and-its-two-error-strips-never-touch-the-transcript)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d262b50a` |  | Room alpha 01a0a1ce2d8a7b0da372e89949f25376. '/invite @echo' -> .composer-info 'Invited echo. Room is now \"alpha, echo\".' colour rgb(111,201,122) (green); sidebar and h1 both became 'alpha, echo' with no refresh. '/invite @nobody' -> .composer-error 'Unknown agent @nobody' rgb(255,138,138) (red). '/shout hello' -> .composer-error 'Unknown command' - not treated as an invite. message-list stayed at 0 children and the .jsonl was never created. Log carried 'Invited agent ''echo'' (01a0a1ce2d837784b73780840593d7ac) into room ''01a0a1ce2d8a7b0da372e89949f25376''.' exactly once. Composer emptied on every send. |

### REPLYGATEBUDGET-04 — A Mention is case-insensitive and ends at trailing punctuation

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-04--a-mention-is-case-insensitive-and-ends-at-trailing-punctuation)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d262b50a` |  | Room 'echo, alpha', member line 'You, echo, alpha'. All six spellings - @ECHO hi, @Echo hi, @echo, @echo. @echo: @echo? - drew exactly one reply each, sender 'echo' every time. alpha never replied to any of the six. The grey line read '1 of 40 agent replies since you last spoke.' after every one, never above 1. |

### REPLYGATEBUDGET-05 — Repeating a Mention wakes an Agent once, and the Human can be Mentioned harmlessly

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-05--repeating-a-mention-wakes-an-agent-once-and-the-human-can-be-mentioned-harmlessly)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d262b50a` |  | '@echo @echo @echo hi' produced exactly ONE reply, from echo (body 'echo: echo echo echo hi'). '@You are needed' posted normally with sender 'You' and drew nothing at all in 10 s - no reply, no strip, zero composer-info/composer-error/alert elements, and the app log held 0 warn lines for the whole run. Transcript tail: human '@echo @echo @echo hi' / echo reply / human '@You are needed' with no agent line after it. |

### REPLYGATEBUDGET-06 — The Budget note counts agent replies and a Human Message resets it to nothing

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-06--the-budget-note-counts-agent-replies-and-a-human-message-resets-it-to-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d262b50a` |  | After 'starting fresh' there was no .budget-note at all. After '@echo hi' it read exactly '1 of 40 agent replies since you last spoke.' On '@echo @alpha go' the element vanished as my Message posted, then a MutationObserver caught it returning as '1 of 40 ...' and then '2 of 40 ...' 3 ms apart - no refresh at any point. (A 60 ms poll missed the '1 of 40' step, which is why the observer was used; the transition is real and just fast.) After '.' the line was gone again. File check: 2 agent lines after the last senderId human line, matching the 2 the line last showed. |

### REPLYGATEBUDGET-07 — A spent Budget halts the Room and asks the Human, visibly

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-07--a-spent-budget-halts-the-room-and-asks-the-human-visibly)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d34fde9e` |  | P-RESET-ROOMS then PROFILE A with budget 1. echo replied, then a .budget-prompt appeared between message-list and composer reading exactly 'Agents have sent 1 replies since you last spoke, and are paused.' with exactly two buttons, Continue and Leave paused. Script said 'red-bordered'; corrected - app.css gives .budget-prompt a neutral border-top 1px var(--border-default) (computed rgb(58,58,68)) and puts the red on .budget-prompt-text via var(--danger-fg) (computed rgb(255,138,138)). Zero member-health-alert elements and zero composer strips. Transcript senders were only You and echo - no system line. Log: 0 'refused a message from' and 0 warn lines at all. /agents listed only the Nova Persona (the demo agents are not Personas), so echo was certainly not Degraded - the parenthetical the Pass-if allows. |

### REPLYGATEBUDGET-08 — The write path refuses an over-Budget post: two Agents are woken, only one Message lands

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-08--the-write-path-refuses-an-over-budget-post-two-agents-are-woken-only-one-message-lands)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `d34fde9e` | #18 | Every Pass-if line met: alpha won the race and exactly ONE agent Message landed, the budget-prompt appeared with Continue and Leave paused, the log held exactly one 'Room ''01a0a1d364bf77ceb1f03b2d815762cd'' refused a message from ''echo'': its budget of 1 agent messages since a human last spoke is spent.', and the .jsonl held exactly one agent line after my prompt. BUT the refused Agent is destroyed: echo's Draft froze on screen showing raw '**echo:** echo alpha go' with a live Stop button, survived a full reload, and echo never replied again (alpha still did). Outside this test's Fail-if lines, so recorded Pass and filed separately as #18 - a Budget refusal is written from inside the read loop on a zero-buffer pipe, so it deadlocks against a client still sending its terminator. |

### REPLYGATEBUDGET-09 — Leave paused hides the question but keeps the pause legible

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-09--leave-paused-hides-the-question-but-keeps-the-pause-legible)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f42db410` |  | Re-established -08's exact state first (alpha won again, echo refused - the same one refusal warning in the log). Clicking Leave paused removed .budget-prompt entirely and left a single .budget-note reading exactly 'Paused - 1 of 1 agent replies since you last spoke.' in the muted colour rgb(154,154,165). Log held 0 'was extended to' lines. Transcript stayed at 2 lines, unchanged from -08. |

### REPLYGATEBUDGET-10 — A dismissal is per-view: navigation, reload and the next Message all bring the question back

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-10--a-dismissal-is-per-view-navigation-reload-and-the-next-message-all-bring-the-question-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f42db410` |  | Run after merging origin/main (MudBlazor 9.10.0 stage 1); build 0 warnings. -08/-09 state rebuilt in room 01a0a2e3304e7eae945a5b156428b43c (alpha refused this time, echo alive). Dismissed to 'Paused - 1 of 1 agent replies since you last spoke.', then: navigating to the alpha Room and back brought the two-button block straight back; dismissing again and reloading brought it back again; 'carry on' emptied the pause area completely; '@echo hi' raised the block a second time rather than staying suppressed. Dismissal is per-view on all three mechanisms. |

### REPLYGATEBUDGET-11 — The pause survives a reload and shows in a brand-new tab

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-11--the-pause-survives-a-reload-and-shows-in-a-brand-new-tab)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f42db410` |  | Paused room http://localhost:5100/rooms/01a0a2e3304e7eae945a5b156428b43c. After a reload the block was present, identical text and both buttons. A brand-new tab that never witnessed the Message showed the same block, same figures, same two buttons - so the view reads the Budget on load, not only from the live event. Log gained 0 'was extended to' lines. |

### REPLYGATEBUDGET-12 — Continue grants exactly one more Budget, once per click, and duplicates nothing on screen

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-12--continue-grants-exactly-one-more-budget-once-per-click-and-duplicates-nothing-on-screen)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f42db410` |  | A MutationObserver caught the in-flight state 'Continuing... disabled=true , Leave paused disabled=true' - label changed and BOTH buttons disabled. The block then went and was replaced by a .budget-note reading exactly '1 of 2 agent replies since you last spoke.' Visible message bodies stayed at 6, .jsonl stayed at 5 lines - a re-delivery is neither rendered nor persisted. Log: exactly one 'Room ''01a0a2e3304e7eae945a5b156428b43c'' was extended to 2 agent messages.' for the click. Afterwards there was no Continue button left to click (count 0). |

### REPLYGATEBUDGET-13 — Any Human Message resets the Budget, is never refused, and expires an earlier grant

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-13--any-human-message-resets-the-budget-is-never-refused-and-expires-an-earlier-grant)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `239d3781` |  | '.' posted normally, sender You, no refusal and zero composer strips; both the pause block and the budget note vanished immediately. '@echo hi' drew echo's reply and the allowance read 'Paused - 1 of 1 agent replies since you last spoke.' - back to the configured default, NOT 1 of 2, so the Continue grant expired. Script corrected: under a budget of 1 one reply exhausts the Room, so the figure is carried by the pause block's dismissed form, not by a plain grey line. Transcript tail showed both my lines with senderId human. Log held no refusal naming the Human (the only refusal is alpha's, from -10's setup). |

### REPLYGATEBUDGET-14 — The Budget is per Room: a paused Room does not starve the one beside it

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-14--the-budget-is-per-room-a-paused-room-does-not-starve-the-one-beside-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `239d3781` |  | echo room 01a0a2e646007f9da57ffe56c84930cd paused after '@echo hi'. Switching to the alpha room 01a0a2e6469e704087a4c52458e5d474 showed no pause block and no budget line at all - a MutationObserver over the whole switch recorded exactly ONE distinct state, so echo's figures never flashed under alpha's name. '@alpha hi' was answered immediately and only then did that Room reach its own exhausted state. Back in echo the pause block was still there with its own figures. The counter is per Room. |

### REPLYGATEBUDGET-15 — A restart un-pauses every Room over a Transcript that already spent its Budget

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-15--a-restart-un-pauses-every-room-over-a-transcript-that-already-spent-its-budget)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `239d3781` |  | Restarted with the identical PROFILE A command and budget 1, no reset. Room id unchanged (01a0a2e646007f9da57ffe56c84930cd). Transcript fully intact and in order: You '@echo hi' then echo 'echo: echo hi'. No .budget-prompt and no .budget-note at all - the Room reads as though no agent has spoken. The .jsonl still holds the 1 agent line after the last senderId human line, so file and view deliberately disagree. Documented behaviour; not filed. |

### REPLYGATEBUDGET-16 — A Budget of zero removes the cap entirely

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-16--a-budget-of-zero-removes-the-cap-entirely)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `239d3781` |  | Both halves run from a fresh P-RESET-ROOMS. Budget 0: six rounds of '@echo @alpha go N' gave 6 human + 12 agent lines in the .jsonl - two replies every round - with no .budget-prompt and no .budget-note after any round, and 0 'refused a message from' / 0 'was extended to' in the log. Budget -1: identical - 6 human + 12 agent lines, all six rounds showed 'none' in the pause area, 0 refusals and 0 extensions. The negative value behaves exactly like zero. |

### REPLYGATEBUDGET-17 — The model-facing wording of this area is readable and editable at /settings/prompts

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-17--the-model-facing-wording-of-this-area-is-readable-and-editable-at-settingsprompts)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `28986944` |  | Test-Path hooks.json printed False before any save. Hooks tab selected by default, 22 hook fields. Chat rules default begins 'A Room with two members is a private conversation with the human: answer every message. A Room with three or more members is a group: answer only when you are @-mentioned.' (wrapped across lines). Help: budget contains 'A refusal that says the budget is spent is final: do not retry it, and do not work around it by posting to another Room.' Chat rules carries the badge 'Next session' with title 'Applies to agents started after the change'; all four Help: fields carry no such badge. With nothing edited Save was disabled=true; appending ' TESTEDIT' added an 'Unsaved' badge and enabled it. After Save, hooks.json held ONLY systemPrompt.chatRules - one key, not the 22 defaults. Undid the edit and then used the field's Reset so the file went back to {}; deleted it afterwards to restore the first-run state. |

### REPLYGATEBUDGET-18 — The server labels a delivery correctly even when the client chooses to stay silent

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-18--the-server-labels-a-delivery-correctly-even-when-the-client-chooses-to-stay-silent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `28986944` |  | PROFILE B, budget 40, DemoAgent off (log carried no 'Demo agent' line). tools/echo-bot.ps1 printed 'Sent hello. Listening for messages...' and the Room 'echo' appeared in the sidebar with no refresh. Plain 'hello' envelope: type messagePosted, mentioned false, mentions [], members exactly 2 (human 'You' + agent 'echo'), agentMessagesSinceHuman 0, budget 40 - and the Room stayed empty apart from my own line, so the bot got a correct label and chose not to act. 'hello @echo' envelope: mentioned true with a mentions array of exactly one entry naming echo (kind agent), and echo's reply 'echo: hello echo' landed in the Room. |

### REPLYGATEBUDGET-19 — The longest handle wins: @Emily Lee reaches Emily Lee, never Emily

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-19--the-longest-handle-wins-emily-lee-reaches-emily-lee-never-emily)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `005b5a28` |  | PROFILE B with two bots, 'Emily' and 'Emily Lee'; both Rooms appeared. '/invite @Emily Lee' gave the green strip 'Invited Emily Lee. Room is now \"Emily, Emily Lee\".' and the heading renamed to 'Emily, Emily Lee' - the Name was NOT truncated at the space. Wire oracle, same envelope read on both bots: '@Emily Lee any news?' -> Emily Lee mentioned=true mentions=[Emily Lee], Emily mentioned=false; '@Emily any news?' -> exactly the other way round; '@emily lee any news?' -> Emily Lee mentioned=true again, so the multi-word match is case-insensitive across the space. Exactly one reply in the Room each time, from the winner, never both. |

### REPLYGATEBUDGET-20 — A Mention falls back to the shorter Name when the longer one is not in this Room

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-20--a-mention-falls-back-to-the-shorter-name-when-the-longer-one-is-not-in-this-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `005b5a28` |  | Fresh PROFILE B with bots 'Emily' and 'echo' only; no 'Emily Lee' anywhere. '/invite @echo' made it 'Emily, echo' with member line 'You, Emily, echo' (3 members, no Emily Lee). '@Emily Lee any news?' drew exactly ONE reply, from Emily. Wire oracle on the same envelope: Emily mentioned=true, echo mentioned=false, mentions=['Emily'] on both, members=3. The trailing 'Lee' was plain text and caused nothing; zero composer strips, so an unresolvable-looking Mention is not treated as an error. |

### REPLYGATEBUDGET-21 — Two Mentions in one Message do not run together, and a multi-word Name stops at punctuation

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-21--two-mentions-in-one-message-do-not-run-together-and-a-multi-word-name-stops-at-punctuation)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `005b5a28` |  | Bots 'Chief of Staff' and 'echo'; after '/invite @echo' the heading was 'Chief of Staff, echo' and the member line named three. '@Chief of Staff and @echo' drew exactly TWO replies, one each, and the single envelope carried mentions=['Chief of Staff','echo'] with mentioned=true in BOTH terminals - the word 'and' was not eaten and the scan did not overshoot. 'thanks @Chief of Staff!' and '@Chief of Staff.' each drew exactly ONE reply, from Chief of Staff, with mentions=['Chief of Staff'] and echo mentioned=false both times - trailing ! and . end a Name that contains spaces. |

### REPLYGATEBUDGET-22 — A Mention ends at a word boundary, and an email address is not a Mention

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-22--a-mention-ends-at-a-word-boundary-and-an-email-address-is-not-a-mention)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `dfafb7b6` |  | Three bots ech / example / echo, all four names on the member line 'You, ech, example, echo'. Read from the terminals: '@echo' -> echo mentioned=true, ech mentioned=false, one reply from echo; 'mail me@example.com' -> mentions=[] on all three, example mentioned=false, no reply at all, so a pasted email address wakes nobody; 'see-@echo' -> echo mentioned=true, one reply, so a hyphen before the @ is not a blocker; 'you@@echo' -> mentions=[] on all three and no reply, so a doubled @ joins rather than mentions. |

### REPLYGATEBUDGET-23 — An Agent never receives its own Message, so no self-echo loop can start

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-23--an-agent-never-receives-its-own-message-so-no-self-echo-loop-can-start)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `dfafb7b6` |  | Room echo, alpha (01a0a2f2163e7a91ac6e8eb8ab4a2d09). '@echo hi' drew exactly one reply and nothing further arrived in 30 s. echo's terminal held only ONE messagePosted - my own - and no envelope carrying its own reply text, so a sender is never delivered its own Message. alpha's terminal DID hold that envelope, from echo, with mentioned=false. Transcript line count was 2 and still 2 twenty seconds later. echo's reply body '**echo:** echo hi' contains no @ character. |

### REPLYGATEBUDGET-24 — The refusal an over-Budget Agent reads is worded as terminal, on the pipe door

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-24--the-refusal-an-over-budget-agent-reads-is-worded-as-terminal-on-the-pipe-door)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6943c90e` |  | PROFILE B, budget 1, bots echo and alpha. alpha's post was accepted and exactly one agent Message was visible. Only echo's terminal printed a refusal envelope; alpha's printed none. Its code was budgetExhausted and its message read character for character: 'This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.' - terminal wording, no 'try again later', 'temporarily' or any wait. Log held exactly one 'refused a message from ''echo'''. Script corrected: the envelope's type is 'error', not 'protocolError' (Messages.cs JsonDerivedType), so the step as written finds nothing. |

### REPLYGATEBUDGET-25 — Continue re-delivers the last Message to the other Agents, once, with the raised Budget

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-25--continue-re-delivers-the-last-message-to-the-other-agents-once-with-the-raised-budget)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `ca574a33` |  | Last Message was alpha's 'alpha: echo alpha go'. After Continue, echo (the non-sender) printed a SECOND messagePosted carrying the SAME message id ad062bae1c... and budget=2 - the raised allowance, not the configured 1. alpha (the sender) printed no envelope for its own Message at any point. Log held exactly one 'Room ''01a0a2f3e45d763694db0ffe99863ad0'' was extended to 2 agent messages.' The .jsonl stayed at 2 lines and the visible message count stayed at 2. Block replaced by the grey '1 of 2 agent replies since you last spoke.' |

### REPLYGATEBUDGET-26 — Continue in a two-Member Room grants Budget and wakes nobody — and that is correct

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-26--continue-in-a-two-member-room-grants-budget-and-wakes-nobody--and-that-is-correct)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `ca574a33` |  | Two-Member Room, member line 'You, echo'. '@echo hi' drew the reply and the pause block. After Continue: log held exactly one 'Room ''01a0a2f5b8157954a692586524ac99a1'' was extended to 2 agent messages.', the block was replaced by the grey '1 of 2 agent replies since you last spoke.', the bot's whole session held only TWO envelopes (welcome + my own messagePosted) with no copy of its own reply, and both the visible message count and the .jsonl stayed at 2. The grant happened and there was simply nobody left to wake - not filed. |

### REPLYGATEBUDGET-27 — An Alias resolves a Mention to the Persona that owns it

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-27--an-alias-resolves-a-mention-to-the-persona-that-owns-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `86b24146` |  | Persona Jarvis / alias jar created through the UI; no 'Files that didn't load' section appeared. Room 'You, Jarvis, echo'. Wire oracle on the Jarvis bot: '@jar do the thing' -> mentioned=true, mentions=['Jarvis'] (the Member, not the alias string), one reply from Jarvis; '@Jarvis do the thing' -> identical, so Alias and Name are interchangeable; 'x@jar' -> mentioned=false, mentions=[], no reply; '@jarring' -> mentioned=false, mentions=[], no reply, so an Alias respects word boundaries. In the echo Room '/invite @jar' gave the green strip 'Invited jar. Room is now \"echo, Jarvis\".' - the invite path resolves Aliases too. |

### REPLYGATEBUDGET-28 — A longer Alias beats a shorter Name, and an equal-length Name beats an Alias

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-28--a-longer-alias-beats-a-shorter-name-and-an-equal-length-name-beats-an-alias)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `86b24146` |  | Teams emptied first; both Personas loaded with no 'Files that didn't load' section. CASE A - Persona 'Other' with alias 'Emily Lee', bots Emily and Other, Room 'You, Emily, Other': '@Emily Lee any news?' drew exactly one reply from Other, and the wire showed Other mentioned=true / Emily mentioned=false with mentions=['Other'] on both. The longer Alias beat the shorter Name. CASE B - Persona 'Jarvis' with alias 'Jar', bots Jar and Jarvis, Room 'You, Jar, Jarvis': '@Jar are you there?' drew exactly one reply from Jar, wire showed Jar mentioned=true / Jarvis mentioned=false with mentions=['Jar'] on both. The equal-length Name beat the Alias. Never both. |

### REPLYGATEBUDGET-29 — Reconnecting re-attaches to the same Room, but after an invite a fresh two-Member Room appears

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-29--reconnecting-re-attaches-to-the-same-room-but-after-an-invite-a-fresh-two-member-room-appears)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `fa9f63fd` |  | Plain reconnect: sidebar Room count 1 before and 1 after, and the welcome re-attached to the SAME room id 01a0a2fb6ec871d3be43d6352019692f - no duplicate. After '/invite @alpha' made it 'echo, alpha', reconnecting echo produced a NEW Room: the welcome listed both the group room and a fresh 01a0a2fc7d7c7484a0f635752293006d named 'echo', and the sidebar showed 'echo, alpha' / 'alpha' / 'echo'. The new Room's member line reads 'You, echo' - two Members. Log carried exactly one 'Created direct room ''01a0a2fc7d7c7484a0f635752293006d'' for agent ''echo''.' for that reconnect (the other two lines belong to the first echo registration and to alpha). |

### REPLYGATEBUDGET-30 — A Mention that reaches nobody is completely silent — and how to tell that apart from a bug

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-30--a-mention-that-reaches-nobody-is-completely-silent--and-how-to-tell-that-apart-from-a-bug)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `fa9f63fd` |  | '@nobodyhere hello' posted normally and produced absolutely nothing else - zero composer strips, error, alert, note or prompt elements - and both bots saw that envelope with mentioned=false. With the echo bot stopped, its dot in the New chat panel read 'agent-dot offline', computed rgb(224,90,90); the script said grey and was corrected (theme.css has no grey dot). '@echo are you there?' while echo was down: alpha received the envelope with mentioned=false, echo received nothing at all, and no reply came. After restarting echo the same Message drew a reply, and echo's fresh session held exactly ONE messagePosted envelope - the missed Messages were not replayed. |

### REPLYGATEBUDGET-31 — PAID: a real Persona answers a two-Member Room with no Mention at all

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-31--paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `473f13aa` |  | PROFILE C, Persona Ada at Haiku/low, Room 'You, Ada' (two Members). Typed 'what is 2 plus 2' with no @ anywhere. A MutationObserver caught a streaming row with a Stop button beside the name, which settled into the Message 'Ada: 2 plus 2 is 4.' - a real answer to the question. The grey line read exactly '1 of 4 agent replies since you last spoke.' Log held 0 'declined a turn in room' warnings. (The unrelated 'Requested effort ''low'' is not in the agent's advertised effort catalog' warning is present as in every paid run; carried forward to the model-effort area.) |

### REPLYGATEBUDGET-32 — PAID: an un-mentioned Message is not answered but rides along as context on the next Mention

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-32--paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `473f13aa` |  | Room 'You, Ada, filler' (three Members). Four un-mentioned Messages across two rounds produced zero replies, zero streaming rows (MutationObserver count 0) and nothing at all in the log. As written, step 6 FAILED: '@Ada list every fact you were told before this message, verbatim.' drew 'I wasn't told any facts - you only asked me a math question.' Re-run with two fresh un-mentioned facts and a pointed question, '@Ada what day did the launch slip to, and what number was headcount frozen at?', she answered 'The launch slipped to Tuesday and headcount was frozen at 12.' - both facts, so the Catch-up buffer was in the prompt all along and the first wording was a model artefact. Step 6 corrected. Grey line stayed '1 of 4 agent replies since you last spoke.' - one Turn for three Messages. Zero 'declined a turn' lines. |

### REPLYGATEBUDGET-33 — PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-33--paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `c4341a76` |  | Personas Ana and Ben, both Haiku/low, budget 6, Room 'You, Ana, Ben'. The exchange halted after exactly SIX agent Messages (Ana, Ben, Ana, Ben, Ana, Ben) - never seven - and a MutationObserver recorded exactly six Draft appearances, so no Draft was started after the cap. Pause block read exactly 'Agents have sent 6 replies since you last spoke, and are paused.' with Continue and Leave paused. Log: 'Persona ''Ana'' declined a turn in room 01a0a307eaa17754b1605cd6cb73487d: the room has spent its budget of 6 agent messages.' - the cheap layer fired - and no refusal naming Ana. Ben's queued second Turn did hit the write path ('refused a message from ''Ben'''), which is the documented price of a queue already in flight, not this test's Fail-if (that needs a refusal with NO matching declined line). Transcript: 1 human + exactly 6 agent lines. |

### REPLYGATEBUDGET-34 — PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-34--paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e4b008f5` |  | Personas Ana and Ben at Haiku/low, budget 2. PATH ONE: with Ana paused on Ben's Message, '@Ana summarise everything you have been told in this room so far' listed only the Human's Message and her own reply and ended 'That's all that's happened in this room.' - the declined Message is not in her view, the documented behaviour. PATH TWO needed two attempts because Ben twice wrote no Mention, so the held Message named nobody and Catch-up was correctly the decision (not a defect - REPLYGATEBUDGET-25's note). Once the held Message did mention the declining Persona, Continue produced exactly ONE answer to it: '@Ana, I'm Ben - I just asked you that question about meeting length in my last message! Should I plan for 30 minutes, an hour, or something else?' - answered once, never restated as context and answered again. Log: 5 'declined a turn' across the pauses and exactly 2 'was extended to 4 agent messages.' for the 2 Continue clicks. Note for testers: P-RESET-ROOMS deletes team.db, so both Personas lost their stored Haiku/low and had to be set again at step 2. |

### REPLYGATEBUDGET-35 — PAID: the Agent-facing post tool returns the same terminal refusal, and the model obeys it

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-35--paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f83e28a8` |  | Persona Ana at Haiku/low, budget 1. '@Ana reply once, then immediately post a second short message into this same room.' produced exactly ONE agent Message and the pause block. The model DID attempt the second post - the log shows exactly one 'Room ... refused a message from ''Ana''' - and then stopped: no repeated refusals. The tool-door wording is proven identical to REPLYGATEBUDGET-24's pipe door by 'Persona ''Ana'' had a message refused: budgetExhausted - This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.' Sidebar gained no Room and App_Data\rooms held exactly one .jsonl, so the model did not route around the cap. Transcript: 1 human + 1 agent line. |

### REPLYGATEBUDGET-36 — PAID: an Agent can mint a fresh Budget by creating a Room, and only the token Budget catches it

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-36--paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f83e28a8` |  | Run against the corrected steps. MINTED ROOM: with two Personas, '@Ana create a new room containing yourself and Ben' logged 'Created room ''01a0a31b52067b3988219592d7f1a8a0'' (Ana, Ben) with 3 members.', the rooms table gained the row, and that Room took 2 fresh agent replies of its own while the first Room was already at its cap - the documented gap, not filed. TOKEN BUDGET: reached only by the Agent-to-Agent bounce. Log: 'Persona ''Ana'' has spent its token budget of 2000 and is taking no more turns until a human speaks to it.' The Room strip read 'Ana is Degraded: The per-Persona token Budget of 2000 is spent; no more Turns until a Human speaks.' (and the same for Ben); both tiles read Degraded with agent-dot degraded, and Ana's card carried that reason line under the status. After '@Ana reply with just the word ok' she answered 'ok', the strip cleared and both tiles returned to Online. Two script defects corrected: one Persona can never mint a Room (CreateRoomForAsync short-circuits a single agent to EnsureRoomForAsync), and a Room Budget of 2 stops the bounce before the token check ever runs. |

### REPLYGATEBUDGET-37 — Continue on a paused Room whose Transcript is empty says there is nothing to re-deliver

File: [`docs/engineering/manual-tests/reply-gate-budget.md`](reply-gate-budget.md#replygatebudget-37--continue-on-a-paused-room-whose-transcript-is-empty-says-there-is-nothing-to-re-deliver)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Agents page: tiles, Teams grouping, filter and rejected files

### AGENTSLIBRARY-01 — The page loads, is styled, and spawns nothing

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-01--the-page-loads-is-styled-and-spawns-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### AGENTSLIBRARY-02 — A library of only the Chief of Staff reads as one tile, then as empty

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-02--a-library-of-only-the-chief-of-staff-reads-as-one-tile-then-as-empty)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `0d79f995` |  | Teams folder confirmed empty (Get-ChildItem -Recurse returned 0). After F5 the page showed exactly 'No Personas yet. Choose New agent to add one.' with the HTML 'No Personas yet. Choose `<strong>`New agent`</strong>` to add one.' - New agent in bold. Zero select elements, zero labels, zero h2/h3 group headings and no 'Files that didn't load' section anywhere. The h1, the New agent button and the .agents-intro paragraph were all still present. |

### AGENTSLIBRARY-03 — The fixture library loads, and a tile shows monogram, Name, Title, an @alias caption and status

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-03--the-fixture-library-loads-and-a-tile-shows-monogram-name-title-an-alias-caption-and-status)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `0d79f995` |  | All six fixture tiles present: Chief of Staff, Emily Lee, Nova, Quill, Rune, Vale. Nova's second line is 'Research Lead · @nov' - codepoints confirm a real U+00B7 middle dot and a leading @ on the alias. Every second line matched its frontmatter (Ops Lead · @val, Home Steward · @run, Scribe · @qui, Analyst · @emi, Chief of Staff · @cos). Monograms: EL, CS, N, Q, R, V - 'Chief of Staff' gives CS, first-and-last, not CO. Tiles are real tiles: border-radius 8px, 1px solid rgb(58,58,68), with a monogram square. No 'this.cardName' literal anywhere. The 'Chief of Staff' tile exists and is headed by its frontmatter Name even though the file is cos.md. Every status read 'Offline'; the dot is RED (agent-dot offline, rgb(224,90,90)), not grey as the script said - corrected here and in two later tests. |

### AGENTSLIBRARY-04 — One heading per Team, ordinal order, "No team" last, no empty groups

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-04--one-heading-per-team-ordinal-order-no-team-last-no-empty-groups)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6b09c072` |  | Walking main in document order gave exactly three h2.agents-group-heading elements in this order: Business, Household, No team. Business held Chief of Staff, Emily Lee, Nova, Vale; Household held Rune, Vale; No team held Quill and was the LAST heading. No section.agents-group had an empty grid. Quill.md confirmed on disk to have no teams: line. |

### AGENTSLIBRARY-05 — A Persona in two Teams renders in full under both headings

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-05--a-persona-in-two-teams-renders-in-full-under-both-headings)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6b09c072` |  | A full Vale tile appears under BOTH Business and Household, and the two copies are identical: name 'Vale', second line 'Ops Lead · @val', monogram 'V', status 'Offline'. Clicking either copy opened the SAME card - Vale / Ops Lead / Offline / 'Alias: val' / 'Teams: Business, Household' - so the click target is bound to the Persona, not the group index. |

### AGENTSLIBRARY-06 — A Persona's Team comes from its field, never from its folder

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-06--a-personas-team-comes-from-its-field-never-from-its-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6b09c072` |  | Household\zzz-misfiled.md (name 'Orin', teams ['Business']) appeared WITHOUT a refresh - the watcher caught it. Orin sits under the Business heading (Business: Chief of Staff, Emily Lee, Nova, Orin, Vale) and appears nowhere under Household; Household still holds only Rune and Vale. The word Household does not occur on Orin's tile or its group heading. The card reads 'Orin / Strategy Lead / Teams: Business' while its PERSONA FILE line prints E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Household\zzz-misfiled.md - folder and filename are both cosmetic. The tile is headed 'Orin', not 'zzz-misfiled'. |

### AGENTSLIBRARY-07 — The Team filter's label, options, order and de-duplication

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-07--the-team-filters-label-options-order-and-de-duplication)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6b09c072` |  | With Zed (Zulu) and Ada (admin) added to the core fixtures, a label reading exactly 'Team' wraps the dropdown and sits above the first group heading (verified by compareDocumentPosition). Options in order: All teams, Business, Household, Zulu, admin - 'All teams' first and selected, upper-case Zulu before lower-case admin, so the sort is ordinal as documented. Business appears exactly once although four Personas name it, and no option exists for a Team no Persona names. |

### AGENTSLIBRARY-08 — Choosing a Team narrows to one heading and drops the no-team Personas

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-08--choosing-a-team-narrows-to-one-heading-and-drops-the-no-team-personas)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `6b09c072` |  | Selecting Business narrowed the page immediately with no reload (a beforeunload listener never fired, and no F5 was pressed) to exactly ONE heading, 'Business', holding Chief of Staff, Emily Lee, Nova and Vale - no Household, Zulu, admin or No team heading, and Quill gone. Household gave exactly one heading holding Rune and Vale. 'All teams' restored the full grouped view: Business, Household, Zulu, admin, No team with Quill last. |

### AGENTSLIBRARY-09 — A Team whose membership drops to zero says so

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-09--a-team-whose-membership-drops-to-zero-says-so)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f13ff522` |  | With Zulu selected the page showed one heading 'Zulu' holding Zed. Editing Zed.md on disk to teams ['Business'] repainted the page on its own with no refresh: it then showed exactly 'No agents in this team.' with ZERO .agents-group sections - no empty Zulu heading - and never the 'No Personas yet' sentence. The #blazor-error-ui banner stayed display:none. Zulu had already vanished from the dropdown (All teams, Business, Household, admin). Choosing All teams brought the library back intact with Zed now under Business. |

### AGENTSLIBRARY-10 — Team names match case-insensitively and appear once

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-10--team-names-match-case-insensitively-and-appear-once)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f13ff522` |  | Casey declares teams ['Nightshift'] and Dana declares ['nightshift']. Exactly ONE heading matched in any casing - 'Nightshift', Casey's casing (first Persona by Name) - out of headings Business, Household, Nightshift, No team. Both Casey and Dana sit under that single heading. The dropdown offered it exactly once (All teams, Business, Household, Nightshift) and selecting it showed both Casey and Dana. |

### AGENTSLIBRARY-11 — All three YAML shapes of `teams` group identically, and the documented comma cost

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-11--all-three-yaml-shapes-of-teams-group-identically-and-the-documented-comma-cost)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f13ff522` |  | ShapeA (comma scalar), ShapeB (flow list) and ShapeC (block list) all appear under BOTH Business and Household - the three shapes group identically. ShapeD's teams ['Sales, EMEA'] produced two headings, Sales and EMEA, each holding ShapeD: the documented cost, recorded as a pass. No heading read 'Business, Household' or 'Business; Household'. Nothing from ShapeD's role: line leaked - no Router, triage or continuity heading, and the dropdown held only All teams, Business, EMEA, Household, Ops, Sales. ShapeE (teams ['Ops','ops']) appeared under exactly one heading, Ops, and its card's Teams: line read 'Ops' once. No 'Files that didn't load' section. |

### AGENTSLIBRARY-12 — A rejected file is named by path and reason, above the tiles, live

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-12--a-rejected-file-is-named-by-path-and-reason-above-the-tiles-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `492a9445` |  | Deleting Nova's title line made the Nova tile vanish and a section appear WITHOUT a refresh. Heading reads exactly 'Files that didn't load' (an h2) and sits above BOTH the Team filter label and the first tile (compareDocumentPosition). The block lists 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md' on one line and under it 'Persona frontmatter is missing required field ''Title''.' It is visually distinct - .agents-rejected, 1px solid rgb(107,53,53) on background rgb(58,31,31), radius 6px - not a bare sentence. Zero exceptions or stack traces in the app log and #blazor-error-ui stayed display:none. Restoring the line made the block disappear on its own and brought Nova back with 'Research Lead · @nov'. |

### AGENTSLIBRARY-13 — Every distinct rejection reason, word for word

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-13--every-distinct-rejection-reason-word-for-word)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `492a9445` |  | All six reasons word for word, each under that file's own absolute path. bad1 (no name) and bad2 (no frontmatter at all) both gave 'Persona frontmatter is missing required field ''Name''.' and bad2 produced no tile. bad3 (blank title) gave 'Persona frontmatter is missing required field ''Title''.' - blank and missing are the same failure. bad4 gave 'Persona frontmatter has a duplicate ''Name'' field.' bad5 gave 'Persona frontmatter field ''Name'' has an invalid value: ''Emily  Lee''.' - textContent codepoints confirm BOTH spaces survive (20 20); HTML merely collapses them on screen. bad6 gave 'Persona frontmatter field ''Alias'' has an invalid value: ''-nope''.' The six valid fixtures kept rendering as tiles throughout, the rejected block cleared once the last bad file was deleted, and the app log held 0 unhandled/stack-trace lines with #blazor-error-ui display:none. |

### AGENTSLIBRARY-14 — A duplicate Alias rejects BOTH files, each naming the other

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-14--a-duplicate-alias-rejects-both-files-each-naming-the-other)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `2bf871c4` |  | Twin.md declaring alias 'nov' made BOTH tiles disappear with no refresh - Nova and Twin - while the other five fixtures stayed. The rejected block listed both absolute paths, and each reason named the OTHER file: under Nova.md 'Persona Alias ''nov'' is also used by ''...\Teams\Twin.md''.' and under Twin.md 'Persona Alias ''nov'' is also used by ''...\Teams\Nova.md''.' Nova.md's md5 was identical before and after (9ece8a6c269edcf994025c8abc91ac13), so rejection never wrote to disk. Changing Twin's alias to 'twi' brought both tiles back on their own. |

### AGENTSLIBRARY-15 — A duplicate Name — including one differing only in case — rejects both

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-15--a-duplicate-name--including-one-differing-only-in-case--rejects-both)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `2bf871c4` |  | jarvis-a.md (name 'Jarvis') and jarvis-b.md (name 'jarvis') produced NO tile for either - not two, not one; the six valid fixtures were untouched. The rejected block listed both absolute paths with each naming the other: 'Persona Name ''Jarvis'' is also used by ''...\jarvis-b.md''.' and 'Persona Name ''jarvis'' is also used by ''...\jarvis-a.md''.' O-DB: select count(*) from persona_models where lower(persona_name)='jarvis' returned 0, and the table was empty overall - a rejected file wrote no row. |

### AGENTSLIBRARY-16 — One file's Alias equal to another file's Name rejects both, with two different sentences — and a self-matching alias is legal

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-16--one-files-alias-equal-to-another-files-name-rejects-both-with-two-different-sentences--and-a-self-matching-alias-is-legal)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `1497225c` |  | shadow.md (alias 'Nova') removed BOTH the Nova and Shadow tiles and listed both paths, with DIFFERENT direction-specific sentences: under Nova.md "Persona Name 'Nova' matches the Alias used by '`<path>`\shadow.md'." and under shadow.md "Persona Alias 'Nova' matches the Name used by '`<path>`\Nova.md'." Deleting shadow.md brought Nova back. selfsame.md (name 'Echoic', alias 'Echoic') was NOT rejected - no rejected block at all - and rendered a tile whose second line reads 'Self Named @Echoic', so a self-matching alias is legal. |

### AGENTSLIBRARY-17 — The rejected block is ordered, survives the filter, and coexists with the empty-library sentence

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-17--the-rejected-block-is-ordered-survives-the-filter-and-coexists-with-the-empty-library-sentence)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `1497225c` |  | Three broken files listed sorted by full path: aaa-broken.md, mmm-broken.md, zzz-broken.md. Selecting Business left the rejected block fully visible and still listing all three while the tiles narrowed to the four Business ones - the filter does not touch it. With every title line stripped (9 .md files on disk, all invalid) the page showed BOTH the rejected block, with 9 entries, AND 'No Personas yet. Choose New agent to add one.', with zero tiles and NO Team dropdown rendered (0 select elements). Entry order in that state was ordinal by full path: Emily Lee.md, Household\Rune.md, Nova.md, Quill.md, Vale.md, aaa-broken.md, cos.md, mmm-broken.md, zzz-broken.md. Disk count equalled tiles + rejected entries at every point (9 = 0 + 9, then 6 = 6 + 0 after restoring). #blazor-error-ui stayed display:none. |

### AGENTSLIBRARY-18 — Non-markdown files are ignored entirely — not loaded, not rejected

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-18--non-markdown-files-are-ignored-entirely--not-loaded-not-rejected)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3617fa79` |  | Created notes.txt, an empty .gitkeep and Household\readme.rst while a MutationObserver watched main. Over six seconds it recorded ZERO mutations - no repaint at all. Tiles were identical before and after, no 'Files that didn't load' block appeared, and none of the three filenames occurs anywhere on the page. The app log gained 0 lines across the whole step - no reload or restart was triggered. |

### AGENTSLIBRARY-19 — The watcher is live: an edit to an Agent's definition reloads it, and a write under work\ does not

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-19--the-watcher-is-live-an-edit-to-an-agents-definition-reloads-it-and-a-write-under-work-does-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3617fa79` |  | No F5 and no restart at any point. NESTED Household\Rune.md: title edit gave 'House Captain @run' within ~1 s; alias edit then gave 'House Captain @rn'; teams edit moved the tile from under Household to under Business on its own. The card's Persona block showed the whole raw file, frontmatter included, matching the buffer exactly (name Rune / title House Captain / alias rn / teams Business). ROOT Nova.md reloaded the same way - 'Research Chief @nov'. IncludeSubdirectories is clearly on. All four values restored afterwards. |

### AGENTSLIBRARY-20 — The watcher notices creations and deletions while the page is open

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-20--the-watcher-notices-creations-and-deletions-while-the-page-is-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `3617fa79` |  | No refresh at any point. Copying Pike.md in added a Pike tile under a new Logistics heading within about a second, and the Team dropdown gained a Logistics option in the same repaint (headings Business/Household/Logistics/No team, options All teams/Business/Household/Logistics). Deleting the file removed the Pike tile, the Logistics heading (its only member) and the Logistics option together - headings and options both back to Business/Household/No team. The grid and the filter never disagreed. |

### AGENTSLIBRARY-21 — Renaming or deleting an Agent folder from outside is noticed, and the card follows the file

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-21--renaming-or-deleting-an-agent-folder-from-outside-is-noticed-and-the-card-follows-the-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `13f2c690` |  | PART A - renaming Household to HouseholdOps left both the Rune and Bram tiles on the page with identity intact ('Home Steward @run', 'Home Second @bra'), and the card's Persona file line updated to the NEW folder, ...\Teams\HouseholdOps\Rune.md. Edit then Save succeeded with zero error elements on the card, and the trailing space landed on disk at the new path (verified with cat -A). PART B - moving the folder out of the tree made BOTH tiles vanish on their own within a second, with 0 new log lines and #blazor-error-ui display:none. The Household heading correctly REMAINED, holding only Vale, because Vale.md declares teams ['Business','Household'] - the script's pass line said the heading disappears, which contradicts its own core fixture set; corrected. Step 6 (Open launching an external editor) not exercised - it is AGENTSLIBRARY-33's subject and needs a GUI editor. |

### AGENTSLIBRARY-22 — A burst of file writes becomes one update, not five

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-22--a-burst-of-file-writes-becomes-one-update-not-five)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `13f2c690` |  | Five valid Personas prepared outside App_Data and copied in as ONE operation, with ACP off. A MutationObserver recording only distinct tile-set changes saw exactly ONE repaint - straight from 7 tiles to 12, all five Burst tiles appearing together about half a second after the copy settled. No further repaints followed. The app log gained 0 lines for the whole burst, so no per-file churn was logged either. |

### AGENTSLIBRARY-23 — A dropped-event storm still converges on what is on disk

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-23--a-dropped-event-storm-still-converges-on-what-is-on-disk)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `13f2c690` |  | PASS-with-note: could not provoke a dropped-event storm; convergence verified for a 50-file burst. Writing 50 Personas at once gave 56 .md on disk and, with no refresh, a Storm heading holding exactly 50 tiles, 56 unique Personas on screen and 0 rejected entries - 56 = 56 + 0, so the page agreed exactly with the disk. (57 tile elements because Vale renders under two Teams.) Deleting all 50 at once brought it back to 6 .md on disk, 6 unique tiles, 0 rejected, and the Storm heading gone. The overflow warning 'PersonaStore's FileSystemWatcher reported an error...' never appeared - the 64 KB buffer makes it rare by design - so that specific backstop path was NOT exercised and is not being reported as verified. |

### AGENTSLIBRARY-24 — A tile click opens the card, and the card shows the real file path

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-24--a-tile-click-opens-the-card-and-the-card-shows-the-real-file-path)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `4823bf5c` |  | Clicking the monogram square and clicking the status line both opened the card. Header reads exactly 'Agent'. Nova's card: Name Nova, Title Research Lead, a status line with a dot and 'Offline', 'Alias: nov', 'Teams: Business'; section headings in order PERSONA, MODEL, EFFORT, PERSONA FILE; the Persona block held the whole raw file including the --- frontmatter; MODEL 'Agent default', EFFORT 'Model default'. Clicking inside .agent-profile-card-panel did NOT close it; clicking the .agent-overlay backdrop DID. Quill's card has no Teams: line at all, not an empty one. Switching Quill to Rune showed no leftover Nova text. Rune's card printed ...\App_Data\Teams\Household\Rune.md and Test-Path on it returned True. O-ADAPTERS stayed 0 across every card open. |

### AGENTSLIBRARY-25 — Offline is the honest default, with no invented reason

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-25--offline-is-the-honest-default-with-no-invented-reason)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `4823bf5c` |  | App started without --Team:Acp:Enabled=true. All seven tile elements read 'Offline' with agent-dot offline, computed rgb(224,90,90) - red, not grey. Neither the status line nor the dot carries a title attribute, so hovering shows no tooltip and no reason is invented. Nova's card showed the same Offline status line with no reason paragraph, and an action row of exactly Edit, Open, Restart, Remove followed by the hint 'Like saving an edit, this restarts the agent, which clears what it remembers.' O-ADAPTERS printed 0. In users the only agent rows are the demo agents echo and alpha. |

### AGENTSLIBRARY-27 — Badges repaint live on connect and disconnect, with no refresh

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-27--badges-repaint-live-on-connect-and-disconnect-with-no-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `663a2416` |  | Scout Persona plus a pipe client of the same name. One F5 (the documented first-connection refresh) gave tile Online with a green dot and card Online. Stopping the bot flipped BOTH the tile (agent-dot offline) AND the open card to Offline on their own with no refresh; the two never disagreed. Restarting the bot took both back to Online by themselves. Navigating Agents then Settings then Agents produced zero new log lines, zero exceptions, and the error banner stayed display none - nothing leaked a subscription. |

### AGENTSLIBRARY-41 — A definition file at the Agents root is rejected, naming its path

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-41--a-definition-file-at-the-agents-root-is-rejected-naming-its-path)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AGENTSLIBRARY-42 — A second .md in an Agent folder is rejected, and work\ is the place for notes

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-42--a-second-md-in-an-agent-folder-is-rejected-and-work-is-the-place-for-notes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AGENTSLIBRARY-43 — Files in _unsorted are rejected as migrated leftovers; other underscore folders are skipped

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-43--files-in-_unsorted-are-rejected-as-migrated-leftovers-other-underscore-folders-are-skipped)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AGENTSLIBRARY-28 — An edit to an Agent's file from outside changes only what the file says; the stored Model and Effort stay

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-28--an-edit-to-an-agents-file-from-outside-changes-only-what-the-file-says-the-stored-model-and-effort-stay)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `663a2416` |  | Mira created in Teams\Business\, set to Haiku / low through the card. Baseline: Name Mira, Title Field Analyst, alias mir, heading Business, MODEL haiku, EFFORT low, path ...\Teams\Business\Mira.md, and persona_models/persona_efforts rows keyed 'Mira' = haiku / low. Moved to the Teams ROOT: page repainted on its own, all six facts identical, only the path changed to ...\Teams\Mira.md. Moved into Household\: same again - still under the Business heading while its file sits in the Household folder, MODEL still haiku, EFFORT still low, path ...\Teams\Household\Mira.md. Database rows unchanged after both moves, still keyed by the frontmatter Name. No path-derived key. (Immediately after Save the card renders the catalog's display name 'Haiku'; on any later open it shows the raw stored id 'haiku'. That is the probe catalog being in memory or not, identical before and after each move, not a move effect.) |

### AGENTSLIBRARY-29 — New agent writes its own folder and file under Agents, with the Teams field it was given

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-29--new-agent-writes-its-own-folder-and-file-under-agents-with-the-teams-field-it-was-given)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `e628f811` |  | Teams field hint reads exactly 'Comma-separated. Leave blank for no team.' After Add agent the card switched to View mode: header 'Agent', Name Wren, and a line reading 'Teams: Business, Household'. A Wren tile appeared under BOTH the Business and Household headings. The file is at src\Huddle.App\App_Data\Teams\Wren.md - at the ROOT, the only Wren.md in the tree, not inside Household\ even though that folder exists. Contents are lowercase single-quoted frontmatter: name 'Wren', title 'Ops Lead', alias 'wre', teams ['Business', 'Household'], then --- then the body - the comma list was split into two Teams, not one. No 'Files that didn't load' entry. The Model dropdown did offer real models (the probe runs with Acp off by design) so Haiku / low were selected as the convention requires. |

### AGENTSLIBRARY-30 — A save that would create a rejected file is refused before anything is written

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-30--a-save-that-would-create-a-rejected-file-is-refused-before-anything-is-written)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `198e8078` |  | Every one of the seven attempts kept the card open, kept every typed field intact, and showed the message in red (.composer-error, rgb(255,138,138)) - nothing was ever written. Wording word for word: Name Nova -> "Persona 'Nova' already exists."; Name Rune -> "Persona Name 'Rune' is also used by '`<path>`\Household\Rune.md'." - the sub-folder collision is caught with no Rune.md at the root; Name rune -> the same sentence with 'rune', so a case-only difference still collides; Alias nov -> "Persona Alias 'nov' is also used by '`<path>`\Nova.md'."; blank body -> "Persona text must not be blank."; Name with two spaces -> "'Emily  Lee' is not a valid Persona name."; Alias -nope -> "'-nope' is not a valid Persona alias." The .md count was 8 before and 8 after all seven refusals, and no entry ever appeared in the rejected block. Cancel closed the card. |

### AGENTSLIBRARY-31 — Renaming through the frontmatter: the tile renames, Model and Effort follow, and the folder and file are renamed with it

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-31--renaming-through-the-frontmatter-the-tile-renames-model-and-effort-follow-and-the-folder-and-file-are-renamed-with-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `198e8078` |  | Edit hint reads exactly 'Markdown. This is the whole Persona file, front matter included, and becomes the agent's system prompt. Saving restarts it, which clears what it remembers.' Changing only name 'Mira' to 'Mira Prime' in the textarea and saving: a tile now reads 'Mira Prime' with monogram MP and no tile reads 'Mira'. The card shows the SAME MODEL haiku and EFFORT low as before. On disk the file is STILL Mira.md and the card's PERSONA FILE line still ends Mira.md - identity is frontmatter, not filename. persona_models and persona_efforts hold rows keyed 'Mira Prime' = haiku / low and NO row keyed 'Mira'. Mira never had a Room so the documented ghost could not be observed here; the sidebar holds only echo, alpha and Scout. |

### AGENTSLIBRARY-32 — Remove takes the file, the Model and the Effort — and not the chats

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-32--remove-takes-the-file-the-model-and-the-effort--and-not-the-chats)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `198e8078` |  | echo set up as the sole member of a Demo team, with Model Haiku / Effort low, and a Room holding two messages. Remove did NOT delete on the first click: the row swapped from x/Message/Edit/Open/Remove to x/Message/Edit/Open/Confirm/Cancel. Cancel restored the original row and deleted nothing. Confirm closed the card, removed the tile, removed the Demo heading (its last member) and removed Demo from the Team dropdown. echo.md is gone from disk and persona_models / persona_efforts hold no row for it (only Wren and Mira Prime remain). The echo Room is still in the sidebar, still in the rooms table, and its .jsonl still holds both messages - removal did not cascade. Recreating a Persona with the same Name, Title and Alias gave MODEL 'Agent default' and EFFORT 'Model default' - no resurrected setting. |

### AGENTSLIBRARY-33 — Open launches the Persona file on the SERVER, from its Agent folder

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-33--open-launches-the-persona-file-on-the-server-from-its-agent-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Inconclusive *(earlier script)* | `40d6e47a` | #20 | Inconclusive - this machine has no .md handler (assoc .md reports 'File association not found'), so the test's main assertion, that Open launches the NESTED file rather than a reconstructed root path, cannot be exercised at all. What was observed: clicking Open on Nova launched nothing, opened no shell dialog, and showed NO message - the card was unchanged, the error banner stayed display none and the log gained no line. That is the Fail-if wording, but not the branch the Inconclusive clause expects (it assumes a handled 'Could not open ...' line). Root cause found and filed as #20: with UseShellExecute true and no association Process.Start RETURNS NULL rather than throwing, so Agents.razor's catch never runs and cardError is set to null. Reproduced directly in PowerShell. |

### AGENTSLIBRARY-34 — Neither a page load, a filter change, nor a View card ever spawns an adapter

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-34--neither-a-page-load-a-filter-change-nor-a-view-card-ever-spawns-an-adapter)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `1070222e` |  | Adapter installed (tools\acp\node_modules present); app restarted first so the catalog cache was cold. O-ADAPTERS stayed 0 and the log stayed at 14 lines through: the /agents page load, cycling the Team filter through every option and back to All teams, and opening a View card. Clicking Edit changed it - sampling every 0.9 s caught exactly ONE claude-agent-acp node process at t+1s, gone by t+2s, and the log jumped from 14 to 40 lines with AgentProcessLauncher and DotAcpClientAdapter entries. The probe is confined to the Create/Edit card. (A first pass showed 0 even at Edit because the catalog was already cached from earlier tests; the restart is what made this observable.) |

### AGENTSLIBRARY-35 — Restart with ACP switched off reports why and starts nothing

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-35--restart-with-acp-switched-off-reports-why-and-starts-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `1070222e` | #21 | Recorded precisely, as the test asks. App started with Team__Acp__Enabled false; O-ADAPTERS 0 and every tile Offline. Clicking Restart once on Nova started a claude-agent-acp adapter that STAYED UP - sampled every 0.9 s for 12 s, present throughout, so not the short-lived catalog probe of -34 - and the badge walked Offline, 'Restarting...' (button disabled), Starting, Online on both the card and the tile. No crash, no error line, error banner display none. The hint read exactly 'Like saving an edit, this restarts the agent, which clears what it remembers.' App_Data\work\Nova already existed from earlier tests so its creation could not be attributed to this click. Restart is NOT gated by the cost guard; flagged as #21. No message was sent and the adapter was stopped immediately, so nothing was spent. |

### AGENTSLIBRARY-36 — Restart is busy-flagged and cannot be double-fired

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-36--restart-is-busy-flagged-and-cannot-be-double-fired)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `821f02b6` |  | O-ADAPTERS 0 before. Clicked Restart on the Offline Vale card and immediately clicked twice more. A MutationObserver caught the button as 'Restarting...' AND disabled - the extra clicks did nothing - and the adapter count rose by exactly ONE, holding at 1 across ten samples over nine seconds, never 2 or 3. When it settled the Restart button was gone entirely because Vale is now Online, and the row read x/Message/Edit/Open/Remove. No error banner. Adapter stopped immediately afterwards by stopping the app; no message was sent, so nothing was spent. |

### AGENTSLIBRARY-37 — Offline WITH a reason, in both of its homes

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-37--offline-with-a-reason-in-both-of-its-homes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `821f02b6` |  | node_modules renamed aside, app started with -- --Team:Acp:Enabled=true. Every tile read Offline. Each tile's status line carried the tooltip verbatim, e.g. for Nova: "No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona." The card showed that SAME text as a full paragraph, P.agent-profile-card-reason, under the status line - not as a tooltip. Console logged "Persona 'Nova' failed to start." at warn with the matching InvalidOperationException. No banned vocabulary (bot / backend / LLM / rate limit / quota). O-ADAPTERS stayed 0 while the adapter was renamed aside, so nothing was spent. After restoring node_modules and clicking Restart on the open card, a MutationObserver caught Offline then Starting then Online, and the reason vanished from BOTH the card paragraph and the tile tooltip (title attribute now null). |

### AGENTSLIBRARY-38 — Health outranks pipe liveness: a live pipe with a failed start must read Offline

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-38--health-outranks-pipe-liveness-a-live-pipe-with-a-failed-start-must-read-offline)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `821f02b6` |  | Observed in the same run as AGENTSLIBRARY-37: node_modules renamed aside, app started with -- --Team:Acp:Enabled=true, echo.md present with name 'echo'. BOTH console lines were there at once - "Demo agent echo connected." (the pipe is live) and "Persona 'echo' failed to start." with the InvalidOperationException naming echo. The echo tile read Offline, NOT Online, and its status line carried the tooltip "No ACP adapter is installed for Persona 'echo'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona." The users table still holds the connected echo agent row (id 01a0a3213d987cf794ec599fb9cae5b9, kind agent), so the pipe genuinely was up. Health outranks pipe liveness. Entirely free - O-ADAPTERS stayed 0 throughout. |

### AGENTSLIBRARY-39 — The whole page in Dark mode, with a card open and a rejected file present

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-39--the-whole-page-in-dark-mode-with-a-card-open-and-a-rejected-file-present)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `1dbda99b` |  | Theme set to Dark (appearance.json written with theme huddle-dark) with Quill broken so the rejected block was on screen. Every surface measured dark, computed: body bg rgb(27,27,31) text rgb(230,230,234); .agents-rejected bg rgb(58,31,31) border rgb(107,53,53) - a clearly distinct red-family danger treatment against the dark page, still obviously a warning; Team label rgb(195,195,204) and select bg rgb(59,59,59) on white text; group heading rgb(195,195,204) with underline rgb(48,48,58); tile bg rgb(42,42,49) border rgb(58,58,68); monogram bg rgb(94,43,96) white text; status dot rgb(224,90,90). Card: backdrop rgba(0,0,0,0.6), panel bg rgb(42,42,49), the pre Persona block rgb(23,23,27), all with light text. Edit: textarea and both Model and Effort dropdowns rgb(59,59,59) on white. Tile hover resolves --surface-hover to the dark branch #32323b (from light-dark(#eef3ff, #32323b)) - a visible dark change, not a white flash. Nothing stayed light and no text was dark-on-dark. Note: this machine's System theme already resolves dark, so the Dark choice looked identical; appearance.json confirms the choice was written, which the Inconclusive clause accepts. Theme returned to System and Quill's title restored. |

### AGENTSLIBRARY-40 — Degraded, with the reason — and the two things that must NOT badge

File: [`docs/engineering/manual-tests/agents-library.md`](agents-library.md#agentslibrary-40--degraded-with-the-reason--and-the-two-things-that-must-not-badge)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Inconclusive *(earlier script)* | `72496a5a` |  | Inconclusive - NOT EXECUTED, per the test's own Inconclusive clause. The Degraded half needs three consecutive GENUINE Turn failures, and this provider cannot be interrupted mid-Turn by any non-destructive means available here: STREAMINGTURN-29 established that pointing ANTHROPIC_BASE_URL at a dead port and then at a local endpoint answering 401 both make the agent SDK retry and hang rather than fail the Turn (the endpoint was confirmed reached via Get-NetTCPConnection). Disabling the network adapter or revoking the claude CLI login are destructive to this machine and were not attempted, so the failures never land as failures and no money was spent here. The two negatives are already verified on this exact surface by earlier tests and were not re-bought: STREAMINGTURN-25 showed the /agents tile and card staying Online with a green dot and no reason line through THREE consecutive Human-clicked Stops, and REPLYGATEBUDGET-07 showed /agents not marking an Agent Degraded when its Room Budget was spent. |

## Agent card: view, edit, create, delete

### AGENTPROFILECARD-01 — Empty Agents page and the New agent entry point

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-01--empty-agents-page-and-the-new-agent-entry-point)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | h1 `Agents`, button `New agent`, intro opens with the exact sentence, body reads `No Personas yet. Choose New agent to add one.` with `New agent` in <strong>. document.querySelectorAll('select').length was 0 - the Team filter is ABSENT from the DOM, not merely empty. Neither `echo` nor `alpha` is in <main>; both appear only in the sidebar room list, which P-RESET-TEAMS deliberately keeps (team.db is not deleted). |

### AGENTPROFILECARD-02 — Clicking New agent opens a card at all (blank-name crash probe)

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-02--clicking-new-agent-opens-a-card-at-all-blank-name-crash-probe)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f034c32a` |  | Re-run 2026-09-14: card opens on both attempts and survives an empty Name and a single-space Name; no exception in the console. Fix confirmed. Unblocks the Create tests 05-11, 18-21, 27, 28, 33. |

### AGENTPROFILECARD-03 — Create card: every label, placeholder and hint

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-03--create-card-every-label-placeholder-and-hint)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Header `New agent`; close button `x` with title AND aria-label `Close`. Four inputs in order Name/Title/Alias/Teams with the exact placeholders (`Chief of Staff`, `Chief of Staff`, `coo`, `Business, Household`) and all four hints verbatim. Textarea labelled `Persona body`, placeholder `You are the Chief of Staff. You keep the team honest.`, hint verbatim including `no front matter needed here.` Selects `Model` then `Effort`. Buttons `Add agent` (type=submit, .agent-profile-card-primary) and `Cancel` (type=button). Zero status dots, zero status words. SCRIPT WORDING: the avatar is a 64px ROUNDED SQUARE (border-radius 10px), not circular - app.css calls it a stand-in for Slack's avatar and tiles use 8px; deliberate, and no Fail-if line covers shape. |

### AGENTPROFILECARD-04 — Monogram tracks the Name as you type

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-04--monogram-tracks-the-name-as-you-type)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | empty -> `?`, `echo` -> `E`, `Emily Lee` -> `EL`, `Chief of Staff` -> `CS`. Clearing the Name back to empty left the card alive and returned the monogram to `?`, which re-confirms AGENTPROFILECARD-02. |

### AGENTPROFILECARD-05 — Create: the exact Name accept/reject set

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-05--create-the-exact-name-acceptreject-set)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All ten rejects produced exactly one red .composer-error line quoting the raw input back verbatim, e.g. `' Nova' is not a valid Persona name.` and `'Emily  Lee' is not a valid Persona name.`; the card stayed open every time; 0 .md files after the whole reject set. All six accepts saved and switched to View, each writing one file at the Teams root: Nova.md, Emily Lee.md, 1Nova.md, nova-2.md, nova_2.md and the 64-char AAAA...GGGG.md. Teams empty again at the end. |

### AGENTPROFILECARD-06 — Create: Alias validation uses the same rules and its own wording

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-06--create-alias-validation-uses-the-same-rules-and-its-own-wording)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All five invalid aliases produced `'<input>' is not a valid Persona alias.` - the word is alias, not name: '', ' jar', 'jar jar ', 'jar.1', '-jar'. 0 .md files after the reject set. `jar`, `Jar Jar` and `jar-1` all saved, each showing `Alias: <value>` on the View card. |

### AGENTPROFILECARD-07 — Create: validation precedence — which error wins

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-07--create-validation-precedence--which-error-wins)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Exactly one error at each step, in the stated order: `'' is not a valid Persona name.` then `'' is not a valid Persona alias.` then `Persona text must not be blank.` then `Persona frontmatter is missing required field 'Title'.` errCount was 1 at every step and Teams held 0 .md files throughout. |

### AGENTPROFILECARD-08 — Create: the Title is prose — blank rejected, commas preserved

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-08--create-the-title-is-prose--blank-rejected-commas-preserved)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Empty Title and a three-space Title both gave `Persona frontmatter is missing required field 'Title'.` `Router, triage, and cross-workstation continuity` saved; the View card's second line read it back with both commas intact. Nova.md line 3 is exactly `title: 'Router, triage, and cross-workstation continuity'` - one line, single-quoted. File is 105 bytes, LF endings, no BOM. |

### AGENTPROFILECARD-09 — Create: a failed save never throws away what you typed

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-09--create-a-failed-save-never-throws-away-what-you-typed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | After the `'jar.1' is not a valid Persona alias.` error the card stayed open and every field was identical to what was left: Name `Atlas`, Title `Ops Lead`, Teams `Business, Household`, the full three-line body ending `MARKER-9F3A.`, Model still haiku and Effort still low (the selects did NOT snap back). Changing Alias to `atl` then saved, and the View card's Persona block still carried `MARKER-9F3A.`, with Model `Haiku` / Effort `low`. |

### AGENTPROFILECARD-10 — Enter in an identity field does not submit, and saving never reloads the page

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-10--enter-in-an-identity-field-does-not-submit-and-saving-never-reloads-the-page)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Enter in Name, Title, Alias and Teams did nothing at all - card still open, no error, no navigation, fields unchanged, performance.getEntriesByType('navigation').length stayed 1. `Add agent` saved with no page reload: two JS markers set beforehand survived, navigation entries stayed 1 and the URL was unchanged. HARNESS NOTE: this browser's synthetic Enter never inserts text into ANY textarea - proved with a plain probe textarea appended to the page, which also stayed `abc`. The newline check was therefore made the SHELLNAV-19 way instead: a window-level keydown listener recorded key `Enter` on the Persona body TEXTAREA with defaultPrevented false, so nothing intercepts it and the browser's own newline applies. |

### AGENTPROFILECARD-11 — A successful create lands on the new agent's View card

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-11--a-successful-create-lands-on-the-new-agents-view-card)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Card stayed open and the header switched to `Agent`. Identity block read `Nova` / `Ops Lead` / dot + `Offline` / `Alias: nov` / `Teams: Business`; sections `Persona`, `Model`, `Effort`, `Persona file`. Behind the overlay a `Nova` tile appeared under a `Business` heading (`Ops Lead - @nov`, `Offline`) and the Team filter appeared with `All teams` + `Business`. App_Data\Teams\Nova.md written. |

### AGENTPROFILECARD-12 — View card: every section and action, in order

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-12--view-card-every-section-and-action-in-order)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run against `Nova2`, not `Nova`: a Room named `Nova` already exists in team.db from the agents-library area (P-RESET-TEAMS keeps team.db), so Nova's card correctly showed a `Message` link per AGENTPROFILECARD-44 and could not satisfy this test's `Message is absent` line. Common procedures' standing convention - append a digit when the Name is already in the sidebar - was applied. With Nova2: header exactly `Agent`; identity `Nova2` / `Ops Lead` / agent-dot offline + `Offline` / `Alias: nov` / `Teams: Business`; actions exactly `Edit`, `Open`, `Restart`, `Remove` with no `Message`; the hint under them reads `Like saving an edit, this restarts the agent, which clears what it remembers.`; sections in order Persona/Model/Effort/Persona file; the Persona block equals the file character for character (86 bytes, both --- lines and the name/title/alias/teams lines present); path `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova2.md`. No literal `this.card...` text anywhere. |

### AGENTPROFILECARD-13 — View card: 'Agent default' and 'Model default' when nothing is stored

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-13--view-card-agent-default-and-model-default-when-nothing-is-stored)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Model section reads exactly `Agent default`, Effort section exactly `Model default`. Neither blank and the two differ. Confirms the known wording gap the NOTE flags: the Effort SELECT's blank option is labelled `Use the agent's default` while the View section says `Model default`. |

### AGENTPROFILECARD-14 — Closing the dialog: focus trap on open, Escape, the close icon, Cancel, scroll locking, and focus return

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-14--closing-the-dialog-focus-trap-on-open-escape-the-close-icon-cancel-scroll-locking-and-focus-return)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Real clicks throughout. Backdrop click closed the card; clicks on the header text, the avatar and the hint line each left it open; `x` closed it; `Edit` then `Cancel` closed it and wrote nothing. Panel carries role=dialog, aria-modal=true, and an aria-label tracking the header - `Agent` in View and `Edit Nova2` in Edit. Both deliberate absences observed and NOT filed: Escape left the card open, and Tab walked straight out of the panel into the sidebar (focus landed on the `echo` room link after six Tabs, `Wren` after six more) - no focus trap. |

### AGENTPROFILECARD-15 — Closing a card clears every bit of its state

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-15--closing-a-card-clears-every-bit-of-its-state)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Reopened Create after Cancel had all four inputs and the body empty, both selects back on `Use the agent's default` (the Haiku/low choices did NOT survive) and no error line. A pending Remove closed via the backdrop reopened as a single `Remove`, not `Confirm`. Clearing the Edit textarea (86 chars to 0) and pressing Save produced `Persona text must not be blank.`; closing with `x` and reopening showed no error line at all. |

### AGENTPROFILECARD-16 — Remove is a two-step confirm and Cancel backs out cleanly

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-16--remove-is-a-two-step-confirm-and-cancel-backs-out-cleanly)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run against `Nova2` (see AGENTPROFILECARD-12 for why). `Remove` is itself the danger-styled button; clicking it swapped it for `Confirm` (class `agent-profile-card-action agents-danger`) plus `Cancel`, leaving Edit/Open/Restart in place. `Cancel` collapsed them back to the single `Remove` with the card still open, and Nova2.md was untouched - 86 bytes, LastWriteTimeUtc 2026-09-15T04:51:22.2598438Z before and after. `Remove` then `Confirm` closed the card, the tile vanished and the page fell back to `No Personas yet.`; Nova2.md no longer exists. |

### AGENTPROFILECARD-17 — Team headings and the Team filter come from the teams field, not the folder

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-17--team-headings-and-the-team-filter-come-from-the-teams-field-not-the-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Headings and membership came only from frontmatter: Business held Iris + Nova, Household held Iris, No team held Atlas - Iris appears once under each of her two teams. Team select offered exactly `All teams`, `Business`, `Household`. Business narrowed to one heading, Household to one, `All teams` restored all three. O-ADAPTERS printed 0 before AND after the filter changes - no adapter spawned. Moving Nova.md into App_Data\Teams\Household without editing it left Nova STILL under the `Business` heading after the repaint. |

### AGENTPROFILECARD-18 — Create: exactly what lands on disk

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-18--create-exactly-what-lands-on-disk)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | File written at the Teams root as Nova.md. Content is exactly ---, name: 'Nova', title: 'Ops Lead', alias: 'nov', teams: ['Business'], ---, You are Nova., You keep the team honest. All keys lowercase, all scalars single-quoted, teams a bracketed flow list. 0 CR and 7 LF characters, so LF endings with no BOM. No `model:`, `effort:` or model id anywhere in the file - `haiku` and `low` went to team.db instead (persona_models `Nova \| haiku`, persona_efforts `Nova \| low` confirmed via O-DB). |

### AGENTPROFILECARD-19 — Create: an apostrophe in the Title round-trips through YAML quoting

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-19--create-an-apostrophe-in-the-title-round-trips-through-yaml-quoting)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Typed one U+0027. Save succeeded with no error. The View card's second line read `Chief of Staff's deputy` with exactly one apostrophe (char code 39, not 8217). File line 3 is exactly `title: 'Chief of Staff''s deputy'` - codes ...,39,39,... confirm TWO consecutive apostrophes on disk. After a full document reload the reopened card still showed one apostrophe, and the Persona block showed the doubled form as it appears in the file. |

### AGENTPROFILECARD-20 — Create: the Teams field trims, drops blanks and collapses duplicates on read-back

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-20--create-the-teams-field-trims-drops-blanks-and-collapses-duplicates-on-read-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | ` Business ,, Household , ` gave card `Teams: Business, Household` and file `teams: ['Business', 'Household']` - no blank entry, no stray space. `Ops, ops` gave card `Teams: Ops` (one entry) and file `teams: ['Ops', 'ops']` (both) - the deliberate write/read asymmetry, not filed. An empty Teams field gave a card with NO `Teams:` line (metas were just `Alias: iri`) and a file with no `teams:` key at all - not `teams: []`. |

### AGENTPROFILECARD-21 — Create: a duplicate filename is caught before anything is written

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-21--create-a-duplicate-filename-is-caught-before-anything-is-written)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `Nova` gave `Persona 'Nova' already exists.` and `nova` gave `Persona 'nova' already exists.` - each naming exactly what was typed. Card stayed open in Create mode with Title `Different`, Alias `zzz` and body `Different body.` intact after both. Nova.md was untouched: 65 bytes, mtime 2026-09-15T04:55:51.0285023Z and SHA256 B12953A3...07747 identical before and after. No nova.md or any second file appeared. |

### AGENTPROFILECARD-22 — Edit card: what is editable, and what a rename announces

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-22--edit-card-what-is-editable-and-what-a-rename-announces)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Header `Edit Nova`. Identity area is a read-only h3 `Nova` plus `agent-dot offline` and the word `Offline` - zero inputs inside it and no Alias:/Teams: lines at all. Text box labelled `Persona text` with the hint verbatim: `Markdown. This is the whole Persona file, front matter included, and becomes the agent's system prompt. Saving restarts it, which clears what it remembers.` Its value equals the whole 65-byte file including both --- lines. Buttons `Save` and `Cancel`. The card contains zero <input> elements, so there is no rename control - the deliberate absence. |

### AGENTPROFILECARD-23 — Edit save writes back to the SAME file path — never a rename, never a move

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-23--edit-save-writes-back-to-the-same-file-path--never-a-rename-never-a-move)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Hand-placed Business\ops-lead.md with CRLF (6 CRLF, 105 bytes) appeared as a `Nova` tile under `Business` within ~1.5s. Card showed `Persona file` = ...\Teams\Business\ops-lead.md. Edit changed ORIGINALWORD to CHANGEDWORD and Save landed in View with CHANGEDWORD in the Persona block. Afterwards the Teams tree held exactly ONE .md - still Business\ops-lead.md - with a fresh mtime (04:56:17 -> 04:56:29) and the new word; no Nova.md anywhere, no rename, no move, no duplicate, and the card still showed the Business\ops-lead.md path. EXPECTED, recorded not filed: the file came back LF (0 CR, 6 LF) after the save; content otherwise intact with no doubled blank lines. |

### AGENTPROFILECARD-24 — Edit: breaking the frontmatter is refused with the file untouched

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-24--edit-breaking-the-frontmatter-is-refused-with-the-file-untouched)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All five rejected saves gave exactly the stated message: missing title -> `Persona frontmatter is missing required field 'Title'.`; body only -> `Persona frontmatter is missing required field 'Name'.`; duplicate alias -> `Persona frontmatter has a duplicate 'Alias' field.`; `name: 'Emily  Lee'` -> `Persona frontmatter field 'Name' has an invalid value: 'Emily  Lee'.`; empty box -> `Persona text must not be blank.` The card stayed in `Edit Nova [form]` with the typed text still in the box every time. The file was byte-identical throughout - 98 bytes, mtime 2026-09-15T04:56:29.7394801Z before and after all five. |

### AGENTPROFILECARD-25 — Edit: renaming into a collision is refused with the file untouched

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-25--edit-renaming-into-a-collision-is-refused-with-the-file-untouched)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All four messages named the real absolute path of the other file (...\Teams\Business\ops-lead.md): `Persona Name 'Nova' is also used by '<path>'.`, `Persona Name 'nova' is also used by '<path>'.` (so case-insensitive), `Persona Alias 'nov' is also used by '<path>'.`, and `Persona Alias 'Nova' matches the Name used by '<path>'.` Card stayed in `Edit Atlas [form]` with the typed text after every attempt. Atlas.md unchanged - 60 bytes, mtime 2026-09-15T04:56:55.3532801Z before and after - and both the Nova and Atlas tiles were still in the list. |

### AGENTPROFILECARD-26 — Edit: renaming moves the folder and keeps the agent whole (ADR-0011)

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-26--edit-renaming-moves-the-folder-and-keeps-the-agent-whole-adr-0011)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All four pass lines held. The list behind showed `Aria` and no `Nova`. The file KEPT its original name (Business\ops-lead.md) and now reads `name: 'Aria'`. The Aria tile opens a complete card - title, Alias: nov, Teams: Business, full Persona block, Model `haiku`, Effort `low`, correct path. O-DB: persona_models and persona_efforts MOVED from `Nova \| haiku` / `Nova \| low` to `Aria \| haiku` / `Aria \| low`, same values, nothing left under Nova. KNOWN ROUGH EDGE reproduced exactly and recorded, not filed: the card left open after the save still headed `Agent` for the OLD name showed `Nova` with an empty Persona block, blank Title, an empty `Alias: ` line, `Agent default`, `Model default` and NO `Persona file` section; the `Message` action that was there before the rename was also gone. JUDGEMENT: yes, this reads to a user as `my save wiped the agent` - every field the user can see is blank and the file path is missing, which is precisely the shape of a destroyed record. |

### AGENTPROFILECARD-27 — Create: a Name colliding with a Persona whose folder has another name

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-27--create-a-name-colliding-with-a-persona-whose-folder-has-another-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Business\anything.md (name: 'Nova') appeared as one `Nova` tile under `Business`. Both Create attempts were refused naming the REAL discovered path: `Persona Name 'Nova' is also used by 'E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\anything.md'.` and the same with 'nova', so the comparison is case-insensitive and reaches into sub-folders. No Nova.md or nova.md at the Teams root - the only .md in the tree was still Business\anything.md. Exactly one Nova tile and no `Files that didn't load` section. |

### AGENTPROFILECARD-28 — Create: Alias collisions, and the one that is legal

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-28--create-alias-collisions-and-the-one-that-is-legal)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `nov` and `NOV` both gave `Persona Alias '<typed>' is also used by '...\Teams\Business\anything.md'.` - case-insensitive. `Nova` gave `Persona Alias 'Nova' matches the Name used by '...\Teams\Business\anything.md'.` `Atlas` (its own Name) SAVED with no error and Atlas.md contains `alias: 'Atlas'`; the card read `Alias: Atlas`. Nova's file was unchanged through steps 1-3 (85 bytes, mtime 04:57:57) and no file was written for any of them - Atlas.md first appeared at 04:58:21 with the successful save. |

### AGENTPROFILECARD-29 — 'Files that didn't load' names the path and the reason, above the list

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-29--files-that-didnt-load-names-the-path-and-the-reason-above-the-list)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `Files that didn't load` renders as the THIRD child of .agents-page - after the header and intro, ABOVE the list/empty-state. broken.md listed by real absolute path with `Persona frontmatter is missing required field 'Title'.` and no tile. With the twins added, the section listed all three, and twin-a.md and twin-b.md each named the OTHER's absolute path - `Persona Name 'Twin' is also used by '...\twin-b.md'.` and `...\twin-a.md'.` - with NEITHER getting a tile (tiles array was empty). |

### AGENTPROFILECARD-30 — An external file edit repaints the page — including inside agent folders

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-30--an-external-file-edit-repaints-the-page--including-inside-agent-folders)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Latencies measured against the file's own write timestamp. Root title edit: written 04:59:02.521, on screen 04:59:03.029 = 508ms. Sub-folder edit: written 04:59:08.387, on screen 04:59:08.924 = 537ms. Both with navigation entries still 1, so no browser reload. Business -> BusinessOps rename: no error, no agent lost (2 tiles, no rejected section) and the card's `Persona file` showed the NEW ...\BusinessOps\Nested.md path, so nothing went stale; the rename itself produced 0 DOM mutations only because nothing visible changes (folders carry no display meaning - AGENTPROFILECARD-17). Proved the watcher survived it by editing a file INSIDE the renamed folder: detected in 526ms. Rename back likewise clean, path back to ...\Business\Nested.md. Moving Rooty.md from the root into Business repainted and left it under the `Business` heading, unchanged. Deleting Nested.md removed its tile in 523ms. With a View card open the TILE behind updated (`Card Open Edit` within 540ms). O-LOG carries zero `FileSystemWatcher` lines and zero exception lines across the whole area. EXPECTED ROUGH EDGE recorded not filed: the open card kept the OLD Title and Persona block (`Root Changed`) while the tile behind read `Card Open Edit`. |

### AGENTPROFILECARD-31 — A card whose file vanished underneath it reports it instead of crashing

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-31--a-card-whose-file-vanished-underneath-it-reports-it-instead-of-crashing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | With the card open and Nova.md deleted underneath it: the list behind emptied but the card stayed open with Edit/Open/Restart/Remove. `Remove` then `Confirm` gave `Persona 'Nova' does not exist.` and the pair collapsed straight back to a single `Remove`. `Open` gave the same message. `Edit` opened with the last-known text still in the box and `Save` gave the same message again, staying in `Edit Nova [form]` with the typed text. O-LOG held 64 lines with ZERO Unhandled/Exception lines, and #blazor-error-ui stayed computed display:none throughout. Note this `Open` path errors correctly because the does-not-exist guard runs before Process.Start - which is why AGENTPROFILECARD-32's silent case needs the file to be present. |

### AGENTPROFILECARD-32 — 'Open' launches the OS editor on the server

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-32--open-launches-the-os-editor-on-the-server)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `af564cca` | #20 | SILENT FAILURE - the exact Fail-if line. `assoc .md` reports `File association not found for extension .md` and HKCU\...\FileExts\.md has no UserChoice, so no handler exists. Clicking `Open` on an agent whose file IS present produced NOTHING: no window (the set of processes with a MainWindowTitle was identical before and after), no red line on the card (.composer-error empty before and after), no new O-LOG line (64 before, 64 after) and #blazor-error-ui still display:none. Neither of the two allowed outcomes happened. Same root cause as the already-open issue #20 from AGENTSLIBRARY-33 (Process.Start returns null instead of throwing when there is no handler), so this was recorded as a second test hitting that issue rather than a duplicate. |

### AGENTPROFILECARD-33 — A Windows reserved device name as a Name (documented limit — record the outcome)

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-33--a-windows-reserved-device-name-as-a-name-documented-limit--record-the-outcome)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | OUTCOME RECORDED: none of the feared results occurred - on Windows 10.0.26200 all three reserved names saved cleanly. `CON`/cn1, `NUL`/nl1 and `COM1`/cm1 each switched the card to View mode with no red line, no yellow `An unhandled error has occurred.` bar and zero Unhandled/Exception lines in O-LOG (still 64). Real files landed at the Teams root - CON.md 45 bytes, NUL.md 45, COM1.md 46 - readable back with their exact composed content, and all three rendered as tiles under `No team` with no `Files that didn't load` section. No other Persona file was changed or removed (the folder was empty beforehand). |

### AGENTPROFILECARD-34 — Two browser tabs on /agents stay in step

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-34--two-browser-tabs-on-agents-stay-in-step)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Tab B, untouched on /agents, gained the `Sync` tile 4.7s after tab A's create and lost it 5.3s after tab A's Remove+Confirm - both with performance.getEntriesByType('navigation').length still 1, so no reload in either direction. With an Edit card open in BOTH tabs on the same agent, O-ADAPTERS sampled eight times over 3.2s read 0 every time - never more than one (the catalog was already probed and cached for this app run, so neither card open re-probed). Closing tab B with its Edit card still open added ZERO lines to the console (64 before, 64 after four seconds) and zero ObjectDisposedException/InvalidOperationException lines. |

### AGENTPROFILECARD-52 — The Chief of Staff offers Reset to default and no Remove

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-52--the-chief-of-staff-offers-reset-to-default-and-no-remove)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AGENTPROFILECARD-35 — Remove deletes the file and ALL THREE stored settings

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-35--remove-deletes-the-file-and-all-three-stored-settings)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Route used: rows seeded directly in team.db (no sqlite3 on PATH - a small read/write helper built on the app's own Microsoft.Data.Sqlite was used instead, see the run notes). Before: persona_models `Nova \| claude-haiku-test`, persona_efforts `Nova \| low`, and the View card's sections read `MODEL \| claude-haiku-test` and `EFFORT \| low`. After Remove then Confirm: Nova.md is gone, and SELECT COUNT(*) returns 0 for BOTH tables - no row, not a blank row. |

### AGENTPROFILECARD-36 — A stored Model or Effort the catalog does not advertise survives an edit

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-36--a-stored-model-or-effort-the-catalog-does-not-advertise-survives-an-edit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | With persona_models `Nova \| claude-ghost-9` and persona_efforts `Nova \| ghost-effort` seeded and the page reloaded, the View card read `MODEL \| claude-ghost-9` and `EFFORT \| ghost-effort` - the raw ids. In Edit the Model select had `claude-ghost-9` SELECTED as a synthesised entry inserted directly after the blank option and BEFORE the real catalog (`` , claude-ghost-9, default, opus[1m], claude-fable-5-1[1m], sonnet, haiku``), and the Effort select had `ghost-effort` selected. Changing one word of the body and saving WITHOUT touching either select left both rows intact - still `claude-ghost-9` and `ghost-effort` - and the View card still showed them. Card opened normally with no error. |

### AGENTPROFILECARD-37 — The model probe never runs on a plain page load

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-37--the-model-probe-never-runs-on-a-plain-page-load)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run from a freshly restarted app so the catalog cache was cold. Baseline 0 adapters, 14 log lines. After four full document loads of /agents: still 0 adapters, still 14 log lines, zero `Model catalog probe` lines. After filtering to `Business` and back to `All teams`: still 0 adapters, still 14 lines. After opening the VIEW card: still 0 adapters, still 14 lines. Only on clicking `Edit` did it move - the log jumped 14 -> 40 lines with a full AgentProcessLauncher spawn sequence (`[agent stderr] [session/create] ... phase=validate-cwd` through `phase=register`, then `The agent process disconnected.`) and the Model hint changed to `Reading the models this agent offers…` before settling. The process is short-lived, so an O-ADAPTERS sample taken afterwards reads 0; the log is the reliable oracle for it. |

### AGENTPROFILECARD-38 — Model and Effort pickers: labels, loading text and the empty-catalog text

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-38--model-and-effort-pickers-labels-loading-text-and-the-empty-catalog-text)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Model loading hint observed verbatim on the cold-cache Edit open: `Reading the models this agent offers…` at 05:08:01.926, settling 4.4s later to `Which model this agent thinks with. Changing it restarts the agent, which clears what it remembers.` The Effort loading hint `Reading the effort levels this model offers…` was captured on a model change rather than on the Edit open - with no model stored there is no ladder to probe, so Effort went straight to `This model offers no effort choice, so it will think as it normally does.` Both hints always settled well inside 25s and no `timed out` line was ever logged. Both selects' first option is `Use the agent's default`. The Effort list contained NO option valued or labelled `default` - for Sonnet it was exactly [``, low, medium, high, xhigh, max] - while the Model list legitimately does carry `default::Default (recommended)`. Wording inconsistency recorded once as agreed: Effort's blank option says `Use the agent's default` while the View card calls the same state `Model default`. |

### AGENTPROFILECARD-39 — Changing the Model clears the Effort and re-probes the ladder (Haiku -> Sonnet)

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-39--changing-the-model-clears-the-effort-and-re-probes-the-ladder-haiku---sonnet)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All five lines held. Switching Haiku -> Sonnet cleared the Effort select from `low` to the blank `Use the agent's default` within ~120ms (it is still `low` in the same synchronous tick, which is the render round trip, not a carry-over - the Fail-if is about it STAYING on low, which it did not). The Effort hint showed `Reading the effort levels this model offers…` and then settled to `How hard this agent thinks…`. The Model select stayed on Sonnet throughout. Probe accounting from the console: exactly 3 adapter sessions this app run - one catalog probe on Edit-open, one for haiku's ladder, one for sonnet's - i.e. one the first time each model id was chosen, and the later repeat switches spawned none (cached). After the rapid Haiku -> Sonnet double switch the list that FINALLY landed was Sonnet's [``, low, medium, high, xhigh, max]; a transient intermediate state showing Haiku's empty ladder was visible for a few seconds first, but the abandoned model's answer did not win. |

### AGENTPROFILECARD-40 — Model is stored as an id, in team.db only

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-40--model-is-stored-as-an-id-in-teamdb-only)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | View card `Model` section reads the display name `Haiku`; the database row is `Modelcheck \| haiku` - the catalog id, lowercase, not the display name. Modelcheck.md is exactly `---/name: 'Modelcheck'/title: 'T'/alias: 'mdc'/---/x` with no `model:` line and neither `haiku` nor `Haiku` anywhere (case-sensitive check). No persona_efforts row was written for the blank Effort - COUNT(*) was 0, not a row holding ''. NOTE on the example in the script: this catalog advertises short alias ids (`haiku`, `sonnet`, `opus[1m]`), so the stored id is `haiku` rather than a hyphenated `claude-haiku-4-5-...`; the assertion that matters - id stored, display name shown - holds. |

### AGENTPROFILECARD-41 — Effort low -> medium is stored as an id, and the blank option deletes the row

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-41--effort-low---medium-is-stored-as-an-id-and-the-blank-option-deletes-the-row)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Blocked *(earlier script)* | `af564cca` |  | BLOCKED by the test's own Inconclusive clause. With Model = Haiku the Effort select offers ONLY the blank option and the hint reads `This model offers no effort choice, so it will think as it normally does.` - Haiku advertises no effort ladder on this adapter, so the low -> medium switch this test requires cannot be performed on Haiku, and the script forbids reaching for a populated ladder. SCRIPT GAP worth recording: AGENTPROFILECARD-41 requires an agent on Haiku, but Haiku has no ladder, so the test as written is unrunnable here. SUPPLEMENTARY (not the verdict, gathered because the row-deletion behaviour is otherwise untested anywhere): the same mechanics were exercised on Sonnet, which does advertise a ladder. Effort low -> card `Low`, row `Modelcheck \| low`. Effort medium -> card `Medium`, row `Modelcheck \| medium` (the lowercase id, never the display name). Modelcheck.md contained no `effort`, `low`, `medium` or `sonnet` at any point. Setting Effort back to the blank option -> SELECT COUNT(*) returns 0, the row is DELETED not blanked, and the card's Effort section reads `Model default`. Model restored to Haiku afterwards per section 0.3. |

### AGENTPROFILECARD-42 — Restart with Team:Acp:Enabled=false reports Offline and starts nothing

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-42--restart-with-teamacpenabledfalse-reports-offline-and-starts-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Its own pass lines all held: the button label changed to `Restarting…` with disabled=true (visible for about 10ms before the status flips - a 10ms sampling loop was needed to catch it), the outcome was reported, a double-click produced exactly ONE adapter, and #blazor-error-ui stayed display:none. HEADLINE CHARACTERISATION, exactly as observed: with `Team__Acp__Enabled=false` and 0 adapters at rest, ONE click of `Restart` took Modelcheck Offline -> Starting -> **Online**, spawned a real adapter (pid 68236), wrote a `users` row (`Modelcheck \| agent`) and created a Room - console line `Created direct room '01a0a379dfbd7508ac9335e592db0527' for agent 'Modelcheck'.` So Restart IS a route around the money guard: the agent would take real billed Turns the moment anyone messaged it. This is the already-open issue #21 (AGENTSLIBRARY-35), reproduced from a second test rather than filed again. Double-click detail: once the first click lands the button is REMOVED (status goes Starting, and AGENTPROFILECARD-45 hides Restart while Starting), so a second click cannot even reach it - adapter count held at 1 across six samples over 4.2s. |

### AGENTPROFILECARD-43 — A newly created agent comes online by itself when ACP is on

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-43--a-newly-created-agent-comes-online-by-itself-when-acp-is-on)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App restarted WITHOUT the flag (Development turns ACP on) with Teams empty; 0 adapters at rest. Creating `Live` moved the status `` -> `Starting` at 151ms -> `Online` at 1701ms, identically on the open card AND the tile behind it, with performance.getEntriesByType('navigation').length still 1 - no reload. Exactly ONE new node process appeared for the one agent (pid 83988), and the console carried `Created direct room '01a0a37bc8177b79855c4670c174cd59' for agent 'Live'.` After Remove + Confirm the adapter was gone within 10 seconds (O-ADAPTERS back to 0) and Live.md was deleted. HARNESS NOTE: the Model select is populated by the probe, so a value set too soon after the card opens is silently dropped - the first create landed on `Agent default`; waiting for the catalog before choosing Haiku fixed it, and the recreate stored `Live \| haiku` / `Live \| low`. |

### AGENTPROFILECARD-44 — The Message action is read once, when the card opens

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-44--the-message-action-is-read-once-when-the-card-opens)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App restarted with `Team__Acp__Enabled=false` and Teams empty. Creating the never-before-seen `Freshone` gave a card reading `Offline` with an action row of exactly `Edit`, `Open`, `Restart`, `Remove` - NO `Message` - and team.db held COUNT 0 in both `users` and `rooms` for that name, so the link was absent rather than broken. The card was then left OPEN and `Restart` clicked: 153ms later, with the status still `Starting` and WITHOUT the card being closed or reopened, a `Message` link appeared with href `/rooms/01a0a384a42777ad9d5b1d9ccaff291e`. Clicking it navigated to that Room, which opened with h1 `Freshone` and member line `You, Freshone`. team.db then held users `01a0a384a41f749cadc0d13b34511e3c \| Freshone \| agent`, rooms `01a0a384a42777ad9d5b1d9ccaff291e \| Freshone`, and a two-member room_members join of `You \| human` + `Freshone \| agent`. (Restart was the route used to bring it online, which is itself the issue #21 bypass characterised in AGENTPROFILECARD-42.) |

### AGENTPROFILECARD-45 — Restart is offered in every state except Starting

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-45--restart-is-offered-in-every-state-except-starting)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | ONLINE half: with Live `Online` the action row was exactly `Message`, `Edit`, `Open`, `Remove` - no `Restart` - and the element immediately after the row was the `Persona` section, i.e. no hint at all; the string `Like saving an edit` appeared nowhere on the card. STARTING half caught by sampling the row every 60ms through a save-triggered restart: from 566ms to 2140ms the status read `Starting` and the row was still `Message`, `Edit`, `Open`, `Remove` with no `Restart` and no hint. OFFLINE half: covered under AGENTPROFILECARD-42, where the same card with the agent `Offline` showed `Restart` present AND the hint `Like saving an edit, this restarts the agent, which clears what it remembers.` directly beneath the row - button and hint move together in both directions. Also seen at AGENTPROFILECARD-42: Restart is removed the instant the status reaches `Starting`, which is why the `Restarting…` label only exists for about 10ms. |

### AGENTPROFILECARD-46 — A save that changes nothing does not restart the agent

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-46--a-save-that-changes-nothing-does-not-restart-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Adapter pid before: 83988. Opening Edit, touching NOTHING and clicking Save left the badge on `Online` for the whole 15-second watch - it never showed `Starting` - and the adapter pid was still 83988 afterwards, with no fresh launch sequence in the console. Then adding a single trailing space to the body and saving DID restart it: `Online` -> `Starting` at 619ms -> `Online` at 2119ms, and the pid changed 83988 -> 10128. The View card's Persona block shows the trailing space preserved. |

### AGENTPROFILECARD-47 — Changing only the Model or only the Effort restarts the session

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-47--changing-only-the-model-or-only-the-effort-restarts-the-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | MODEL half: adapter pid 10128 before. Changing ONLY Model Haiku -> Sonnet and saving moved the badge `Online` -> `Starting` at 619ms -> `Online` at 2111ms, replaced the process (10128 -> 44812) and left persona_models holding `Live \| sonnet`. The change also cleared the Effort, exactly as AGENTPROFILECARD-39 requires (persona_efforts COUNT 0, card `Model default`). EFFORT half: changing ONLY Effort from the blank option to `low` and saving moved the badge `Online` -> `Starting` at 624ms -> `Online` at 2366ms, replaced the process again (44812 -> 64640) and left persona_efforts holding `Live \| low` - the lowercase id. Card read `Sonnet` / `Low`. RESTORE, and a real finding about section 0.3: setting the Model back to Haiku cleared the Effort again, and `low` could NOT be re-selected because Haiku advertises NO effort ladder on this adapter (the Effort select collapses to the blank option alone). Live therefore ends this test on Model `Haiku` / Effort `Model default`, which is the only reachable Haiku state here - section 0.3's `restore to Haiku and low` is unachievable for this model, the same script gap recorded under AGENTPROFILECARD-41. |

### AGENTPROFILECARD-48 — Remove does NOT cascade: the Agent and its Room survive

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-48--remove-does-not-cascade-the-agent-and-its-room-survive)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run with a REAL transcript present, because AGENTPROFILECARD-50 was run first on purpose - Room 01a0a37bc8177b79855c4670c174cd59 held four messages (1561-byte .jsonl). After Remove + Confirm on `Live`: the tile is gone and Live.md is gone from disk, but the Room is STILL in the sidebar at the same href and STILL opens at its URL with h1 `Live`, member line `You, Live`, and ALL FOUR messages intact - both PELICAN exchanges and the post-restart `I don't have context...` reply. team.db still holds users `01a0a37bc8137fd99d2d3fd3ed469af9 \| Live \| agent` AND rooms `01a0a37bc8177b79855c4670c174cd59 \| Live`. The transcript file is untouched at 1561 bytes. Only the settings went: persona_models and persona_efforts both COUNT 0. Adapter stopped (0). ONE PASS LINE WAS NOT OBSERVABLE, recorded rather than counted against the app: `The removed agent shows as permanently Offline inside the Room.` The Room surface carries NO per-member status at all - `p.chat-members` is the plain text `You, Live`, message senders are bare `span.message-sender`, and the only .agent-dot elements in <main> belong to the invite panel, which does not list Live because it is already a member. So no member, online or offline, has a status indicator in a Room; this is a script expectation that does not match the current Room UI (room-messaging's surface, not Remove's), and none of this test's Fail-if lines was triggered. |

### AGENTPROFILECARD-49 — Delete then recreate under the same Name: fresh settings, old Room

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-49--delete-then-recreate-under-the-same-name-fresh-settings-old-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Before the recreate: persona_models and persona_efforts both COUNT 0 for `Live`, while the users row `01a0a37bc8137fd99d2d3fd3ed469af9 \| Live` was still present - the delete took the settings and left the identity. Recreated the same Name/Title/Alias with BOTH selects verified blank ('' and '') at submit time: the View card reads `Agent default` and `Model default`, so the Haiku (and earlier Sonnet/low) choices did NOT come back. Both settings queries still return 0 after the recreate. The card's `Message` href is the SAME Room id 01a0a37bc8177b79855c4670c174cd59 as before the delete, the users id is unchanged, and opening it shows the earlier conversation intact - both PELICAN exchanges still there. Both opposite regressions absent: settings did not survive, chat facts did. |

### AGENTPROFILECARD-50 — Editing an agent really does lose its conversation memory (COSTS MONEY)

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-50--editing-an-agent-really-does-lose-its-conversation-memory-costs-money)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run once for the whole area, with the user's explicit go-ahead. Cost confirmed first: card read Model `Haiku`; Effort read `Model default` rather than `low` because Haiku advertises no effort ladder at all on this adapter (see AGENTPROFILECARD-41/47), so there is no effort level to raise and the cost is Haiku's own default. Guards in place: Team__AgentMessageBudget=4, Team__Acp__TokenBudget=200000, no ANTHROPIC_API_KEY set in any scope, TEAM_E2E unset. BEFORE the edit: `Remember the codeword is PELICAN.` -> `I've got it - codeword noted as PELICAN.`; `What is the codeword?` -> `The codeword is PELICAN.` AFTER changing one word of the Persona body (`You are Live.` -> `You are Alive.`) and saving: badge `Online` -> `Starting` at 616ms -> `Online` at 2238ms, and the adapter process was replaced, pid 91408 -> 22040. The same question then returned `I don't have context for what codeword you're referring to. Could you provide more details?` - the session had forgotten, so the card's promise holds. Three Turns spent. METHOD NOTE, important for anyone re-running this: on the FIRST Turn the Live agent used its Write tool to persist the codeword to a file OUTSIDE its Work Dir - filed as issue #22 and said `The codeword PELICAN is saved to memory`. That file was removed before the edit; had it been left, the restarted session could have read it back and answered PELICAN, which would have produced a false Fail on a test about session memory. |

### AGENTPROFILECARD-51 — The Edit card's Name, Title and Alias boxes write into the frontmatter and preserve every other field

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-51--the-edit-cards-name-title-and-alias-boxes-write-into-the-frontmatter-and-preserve-every-other-field)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AGENTPROFILECARD-53 — An avatar-only Save does not restart the agent

File: [`docs/engineering/manual-tests/agent-profile-card.md`](agent-profile-card.md#agentprofilecard-53--an-avatar-only-save-does-not-restart-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Library explorer and editor

### L0 — Chat message with absolute path becomes a Library link and opens the Library tab on that file

File: [`docs/engineering/manual-tests/library.md`](library.md#l0--chat-message-with-absolute-path-becomes-a-library-link-and-opens-the-library-tab-on-that-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L1 — Human edits a file in the Library and saves it; the Agent's next Turn lists `changed …`

File: [`docs/engineering/manual-tests/library.md`](library.md#l1--human-edits-a-file-in-the-library-and-saves-it-the-agents-next-turn-lists-changed-)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L2 — Human creates a Project folder in the Library from the Team node

File: [`docs/engineering/manual-tests/library.md`](library.md#l2--human-creates-a-project-folder-in-the-library-from-the-team-node)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L3 — A Persona gains a Team label; the Team folder is created when the Persona is saved

File: [`docs/engineering/manual-tests/library.md`](library.md#l3--a-persona-gains-a-team-label-the-team-folder-is-created-when-the-persona-is-saved)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L4 — Wikilinks are rewritten when a file is renamed; the rename dialog reports the link count

File: [`docs/engineering/manual-tests/library.md`](library.md#l4--wikilinks-are-rewritten-when-a-file-is-renamed-the-rename-dialog-reports-the-link-count)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L5 — Editing a Persona definition file shows a save-time warning

File: [`docs/engineering/manual-tests/library.md`](library.md#l5--editing-a-persona-definition-file-shows-a-save-time-warning)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L6 — Team and Project folders are protected: rename, move and delete are shown disabled with a reason

File: [`docs/engineering/manual-tests/library.md`](library.md#l6--team-and-project-folders-are-protected-rename-move-and-delete-are-shown-disabled-with-a-reason)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L7 — Crafted paths and junctions that leave the root are refused by the resolver

File: [`docs/engineering/manual-tests/library.md`](library.md#l7--crafted-paths-and-junctions-that-leave-the-root-are-refused-by-the-resolver)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L8 — Deleting a file sends it to the Windows Recycle Bin with a confirm dialog

File: [`docs/engineering/manual-tests/library.md`](library.md#l8--deleting-a-file-sends-it-to-the-windows-recycle-bin-with-a-confirm-dialog)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L9 — When a file changes on disk and the Human has no unsaved edits, the new text is shown on focus

File: [`docs/engineering/manual-tests/library.md`](library.md#l9--when-a-file-changes-on-disk-and-the-human-has-no-unsaved-edits-the-new-text-is-shown-on-focus)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L10 — When a file changes on disk while the Human has unsaved edits, the Human is warned and the Human's text wins

File: [`docs/engineering/manual-tests/library.md`](library.md#l10--when-a-file-changes-on-disk-while-the-human-has-unsaved-edits-the-human-is-warned-and-the-humans-text-wins)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L11 — Opening different file types: images preview, JSON displays read-only, PowerPoint offers "Open in default app"

File: [`docs/engineering/manual-tests/library.md`](library.md#l11--opening-different-file-types-images-preview-json-displays-read-only-powerpoint-offers-open-in-default-app)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L12 — Copy a document and paste it into a chat; Agents receive the Library document block

File: [`docs/engineering/manual-tests/library.md`](library.md#l12--copy-a-document-and-paste-it-into-a-chat-agents-receive-the-library-document-block)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L13 — Pasting a Library path outside Huddle (e.g. in Claude Code) is an ordinary absolute path

File: [`docs/engineering/manual-tests/library.md`](library.md#l13--pasting-a-library-path-outside-huddle-eg-in-claude-code-is-an-ordinary-absolute-path)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L14 — The divider position of the Library page's explorer is remembered

File: [`docs/engineering/manual-tests/library.md`](library.md#l14--the-divider-position-of-the-library-pages-explorer-is-remembered)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L15 — Pop out opens the `/library` page with tree and document side by side

File: [`docs/engineering/manual-tests/library.md`](library.md#l15--pop-out-opens-the-library-page-with-tree-and-document-side-by-side)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L16 — Old Teams/*.md + work/ install is migrated to the Agents/ folder layout on startup

File: [`docs/engineering/manual-tests/library.md`](library.md#l16--old-teamsmd--work-install-is-migrated-to-the-agents-folder-layout-on-startup)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L17 — A real Obsidian vault is pinned as a Library root and can be edited without reformatting

File: [`docs/engineering/manual-tests/library.md`](library.md#l17--a-real-obsidian-vault-is-pinned-as-a-library-root-and-can-be-edited-without-reformatting)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L18 — run.ps1 -CleanOnly deletes the database and Agent definitions, and keeps everything else

File: [`docs/engineering/manual-tests/library.md`](library.md#l18--runps1--cleanonly-deletes-the-database-and-agent-definitions-and-keeps-everything-else)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L19 — The Markdown toolbar and its shortcuts toggle markers, undo in one step and are keyboard-reachable

File: [`docs/engineering/manual-tests/library.md`](library.md#l19--the-markdown-toolbar-and-its-shortcuts-toggle-markers-undo-in-one-step-and-are-keyboard-reachable)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L20 — A toolbar edit saves byte-exact on a CRLF note with a BOM

File: [`docs/engineering/manual-tests/library.md`](library.md#l20--a-toolbar-edit-saves-byte-exact-on-a-crlf-note-with-a-bom)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### L21 — `[[` suggests notes from the note's own root, nearest first, and does not double `]]`

File: [`docs/engineering/manual-tests/library.md`](library.md#l21---suggests-notes-from-the-notes-own-root-nearest-first-and-does-not-double-)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Model and Effort pickers, catalog probe and runner restart

### MODELEFFORT-01 — First card open: the Model list comes from the adapter, and one adapter spawn fills both lists

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-01--first-card-open-the-model-list-comes-from-the-adapter-and-one-adapter-spawn-fills-both-lists)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | First card open of a freshly restarted app, with App_Data\work deleted first (Test-Path False). Model select's first option is exactly `Use the agent's default`, followed by five real entries reading as human labels - `Default (recommended)`, `Opus (1M context)`, `Fable`, `Sonnet`, `Haiku` - not raw ids of the `claude-sonnet-4-5` shape. The loading state was captured by sampling inside the same call as the click: at 185ms the Model hint read exactly `Reading the models this agent offers…` with only the blank option present; at 4775ms both lists arrived and the Model hint read exactly `Which model this agent thinks with. Changing it restarts the agent, which clears what it remembers.` The continuous 100ms spawn watcher recorded EXACTLY ONE rise for the whole card open - 0 -> 1 (pid 88068) at 22:30:42.500, back to 0 at 22:30:47.214 - never 2, and never two rises in sequence. App_Data\work exists again afterwards. The app log carries no `No ACP adapter is installed; the model catalog is empty.` line and no line beginning `Model catalog probe `. SECOND card open after Cancel: the watcher logged nothing at all for the full 15 seconds (still 0) and both selects were populated 58ms after the click. NOTE on the Effort hint during loading: it read `This model offers no effort choice, so it will think as it normally does.` rather than `Reading the effort levels this model offers…`, which is correct - no model is selected yet, so there is no ladder to probe. |

### MODELEFFORT-02 — Fixture walk: record every model's effort ladder, and one spawn per newly seen model

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-02--fixture-walk-record-every-models-effort-ladder-and-one-spawn-per-newly-seen-model)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | FIXTURE TABLE (the reference later tests use). `Default (recommended)` / id `default` -> Low, Medium, High, Xhigh, Max; hint `How hard this agent thinks…`. `Fable` / id `claude-fable-5-1[1m]` -> same five levels; same hint. `Sonnet` / id `sonnet` -> same five levels; same hint. `Haiku` / id `haiku` -> BLANK OPTION ONLY, hint `This model offers no effort choice, so it will think as it normally does.` `Opus (1M context)` / id `opus[1m]` was NOT walked - section 0.3 forbids selecting Opus anywhere, including on a walk-every-entry instruction. So: **LADDER-MODEL = Sonnet**, **NO-LADDER-MODEL = Haiku**, HAIKU = `haiku`, SONNET = `sonnet`. No model left its hint stuck on `Reading the effort levels this model offers…` - each settled in 5.3-5.4s. SPAWNS: the watcher recorded exactly one rise to 1 and back to 0 per newly-seen model id, timestamps matching each selection to within ~0.6s (default 22:31:34.328, fable 22:31:39.806, sonnet 22:31:45.438, haiku 22:31:50.952), and the count never exceeded 1. Re-selecting Sonnet, already walked, spawned NOTHING in 10 seconds and returned its ladder from cache in 84ms. This is also the runtime fact behind AGENTPROFILECARD-41's block: Haiku genuinely has no effort ladder on this adapter. |

### MODELEFFORT-03 — Saving a new agent stores wire ids, never display names

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-03--saving-a-new-agent-stores-wire-ids-never-display-names)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run on SONNET, not Haiku, exactly as step 9 and MODELEFFORT-02's Inconclusive clause direct: Haiku is NO-LADDER-MODEL here, so `Low` is unselectable on it. Baseline rows were only Wren/Mira Prime. After saving `Probe One` (Model Sonnet, Effort Low) the card did NOT close - it showed the saved agent with Persona, Model and Effort sections. persona_models gained exactly one row `Probe One \| sonnet` and persona_efforts exactly one row `Probe One \| low` - both lowercase wire ids, NOT the select labels `Sonnet` and `Low`, and neither is an empty string. App_Data\Teams\`Probe One.md` reads exactly `---/name: 'Probe One'/title: 'Tester'/alias: 'p1'/---/You are a tester.`; the CRLF check printed False (LF-only) and there is no `teams:` line despite Teams being left empty. NOTE on the pass example: this adapter advertises short alias ids, so the stored id is `sonnet` rather than a hyphenated `claude-sonnet-4-5`; the assertion that matters - id stored, never the display name - holds. |

### MODELEFFORT-04 — Two different 'default' choices in the Model select, and none in the Effort select

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-04--two-different-default-choices-in-the-model-select-and-none-in-the-effort-select)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Both halves ran: this adapter DOES carry its own default entry, `Default (recommended)` with value `default`, so steps 3-9 were not inconclusive. Model select's blank first option reads exactly `Use the agent's default`. `Probe Default` (that entry chosen, Effort left blank) -> persona_models holds exactly one row whose model is the literal string `default`, and persona_efforts holds ZERO rows. `Probe Blank` (both selects left blank) -> ZERO rows in persona_models AND ZERO in persona_efforts - no empty-string row in either - and its View card read exactly `Agent default` / `Model default`. No Effort select seen at any point in this test offered an option labelled `Default`: for the `default` model the ladder was Use the agent's default / Low / Medium / High / Xhigh / Max, and an explicit check for an option whose visible text is `Default` returned false on both selects. Cleanup done: both fixtures removed through the card. |

### MODELEFFORT-05 — The card shows the Persona's real values in View and Edit, never a bare field name

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-05--the-card-shows-the-personas-real-values-in-view-and-edit-never-a-bare-field-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Searched the whole rendered page, not just the card: `document.body.innerText.match(/this\.card\w*/g)` returned an EMPTY array in View mode and again in Edit mode - zero matches for `this.card`, so none of this.cardName/Model/Effort/Title/Alias/Teams/Text/RoomId/FilePath/Error is leaking as a literal. View heading reads `Agent` with the name line `Probe One`, `Alias: p1`, and the Persona section showing the real file text `---/name: 'Probe One'/title: 'Tester'/alias: 'p1'/---/You are a tester.` Edit heading reads `Edit Probe One`, its textarea holds that same real file text, and both selects come up preselected on the stored values (Sonnet / Low). OBSERVATION carried to MODELEFFORT-15: a View card reopened from its tile shows the Model and Effort sections as the RAW IDS `sonnet` and `low`, whereas the card shown immediately after a save reads `Sonnet` and `Low` - the label mapping is only available when a catalog has been probed in that card, and a View card deliberately probes nothing. |

### MODELEFFORT-06 — Neither catalog is probed on a plain page load or a tile click

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-06--neither-catalog-is-probed-on-a-plain-page-load-or-a-tile-click)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App restarted and App_Data\work deleted first (Test-Path False). PLAIN PAGE LOAD: the 100ms watcher stayed at 0 for the full 20 seconds, work still did not exist, and the app log stayed at its 14 baseline lines. TILE CLICK into the View card for `Probe One`: watcher still 0 for another 20 seconds, work still absent, log still 14 lines. TEAM FILTER changed from `All teams` to `Business` (the page narrowed to the single `Business` heading): watcher still 0 for 10 seconds, work still absent. Only clicking EDIT moved anything - the watcher rose 0 -> 1 (pid 53416) at 22:35:33.855, matching the Edit click at 05:35:33.099Z, fell back to 0 at 22:35:42.965, and App_Data\work exists again. The Model select was populated, so the probe is still reachable from the Edit path. |

### MODELEFFORT-07 — Changing the Model re-probes: the Effort list changes and the chosen level resets

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-07--changing-the-model-re-probes-the-effort-list-changes-and-the-chosen-level-resets)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Cold app restart first, so spawn counts are unambiguous. HAIKU ladder recorded: blank option only, hint `This model offers no effort choice, so it will think as it normally does.` HAIKU -> SONNET: the Effort hint read exactly `Reading the effort levels this model offers…` from 80ms through 4.4s, then settled to Sonnet's ladder `::Use the agent's default, low::Low, medium::Medium, high::High, xhigh::Xhigh, max::Max` with the hint `How hard this agent thinks…`; the Model select stayed on `sonnet` throughout. The two ladders DIFFER as required - one model offers five levels where the other offers none. RESET HALF run in the SONNET -> HAIKU direction, as MODELEFFORT-02's Inconclusive clause directs (Haiku is NO-LADDER-MODEL, so `Low` cannot be chosen on it): with Sonnet + `Low` selected, switching the Model to Haiku cleared the Effort select to `Use the agent's default` within 80ms - the `Low` was NOT carried over. Switching back to HAIKU restored exactly the list recorded at step 3 (blank only, same hint) in 712ms. SPAWNS: exactly two rises this run, never above 1 - 22:38:17.930-22:38:27.680 for the card open (Haiku was selected at 22:38:22, INSIDE that window, so its ladder came from the same probe session rather than a second spawn, which is the efficient path MODELEFFORT-01's Fail-if exists to protect) and 22:38:33.094-22:38:37.810 for the newly-seen `sonnet`. Switching back to the already-probed Haiku spawned NOTHING - the watcher logged no further entry. |

### MODELEFFORT-08 — A superseded effort probe never lands on the model the user ended up with

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-08--a-superseded-effort-probe-never-lands-on-the-model-the-user-ended-up-with)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | The race WAS forced in both runs - each switch was made while the previous Effort hint still read `Reading the effort levels this model offers…`, confirmed by sampling the hint at every step, so this is not the Inconclusive `probes answered too fast` case. RUN 1, HAIKU last (haiku -> +1s -> sonnet -> +1s -> haiku, all mid-probe): after a 30-second settle the Effort select held exactly HAIKU's ladder - the blank option alone, hint `This model offers no effort choice, so it will think as it normally does.` - and the Model select still read `haiku`/`Haiku`. RUN 2 after another cold restart, SONNET last (sonnet -> haiku -> sonnet): after 30 seconds the Effort select held exactly SONNET's ladder `::Use the agent's default, low, medium, high, xhigh, max` with the hint `How hard this agent thinks…`, and the Model select still read `sonnet`/`Sonnet`. Neither run ended stuck on `Reading…`, and in neither did the abandoned model's answer win. |

### MODELEFFORT-09 — Closing the card mid-probe leaves no stuck state

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-09--closing-the-card-mid-probe-leaves-no-stuck-state)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All three close gestures exercised mid-probe against a model not yet probed in that app run, each confirmed by reading the Effort hint as `Reading the effort levels this model offers…` at the instant of the close: Cancel (on `sonnet`), the x button (on `claude-fable-5-1[1m]`) and the dark backdrop (on `haiku`). Every gesture closed the card immediately. A MutationObserver on .agents-page counted ZERO mutations across the full 30-second wait after each close - no flicker, no repaint from a superseded probe. On each re-open the Model select was populated instantly from cache (6 options, no fresh spawn) and showed `Use the agent's default`; the Effort select showed `Use the agent's default`; and the Effort hint was the normal `How hard this agent thinks…`, never `Reading…`. NOTE so this is not misread: the re-opened Effort select legitimately lists low/medium/high/xhigh/max. That is the blank/agent-default model's own ladder, seeded by the catalog probe exactly as seen on the very first card open in MODELEFFORT-01 - it is NOT Sonnet's ladder surviving the close. Had Haiku's state leaked from the third run, the select would have shown the blank option alone. |

### MODELEFFORT-10 — A model with no effort support says so, and the log proves it was not a failed probe

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-10--a-model-with-no-effort-support-says-so-and-the-log-proves-it-was-not-a-failed-probe)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run against NO-LADDER-MODEL = Haiku, the fixture MODELEFFORT-02 recorded. After settling (3.1s) the Effort select contained ONLY `Use the agent's default` - a single option - and the hint read exactly `This model offers no effort choice, so it will think as it normally does.` The disambiguator holds: the app log carries ZERO occurrences of all five probe-failure lines (`Model catalog probe failed to start or talk to the adapter.`, `Model catalog probe timed out after`, `Model catalog probe skipped: the adapter needs authentication.`, `Model catalog probe failed talking to the adapter process.`, `No ACP adapter is installed; the model catalog is empty.`), so the sentence is a real answer about the model rather than a broken probe wearing the same words. Saving `Probe NoEffort` wrote persona_models `Probe NoEffort \| haiku` and ZERO rows in persona_efforts, and the saved card's Effort section read exactly `Model default`. Cleaned up through the card. |

### MODELEFFORT-11 — Edit preselects the stored Model and Effort, and keeps them when the catalog lands late

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-11--edit-preselects-the-stored-model-and-effort-and-keeps-them-when-the-catalog-lands-late)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `af564cca` | #23 | The Model select LOSES the stored model when the real catalog lands. Reproduced twice from a cold restart, deterministically. Stored: persona_models `Probe One \| sonnet`, persona_efforts `Probe One \| low`. While the Model hint read `Reading the models this agent offers…` (185ms) the select correctly showed the stored value as a synthesised entry - value `sonnet`, label rendered as the raw id `sonnet` exactly as the first pass line predicts. When the catalog arrived 9.3s later and replaced the option list, the Model select jumped to value `default`, label `Default (recommended)` - NOT `Sonnet`, which is present in the list as `sonnet::Sonnet`. That breaks the second pass line. The Effort select behaved correctly throughout, holding `low` before and after. NOT the catastrophic consequence the Fail-if describes, and this is the important qualifier: the DATABASE SURVIVED BOTH SAVES. A no-op Save left `sonnet`/`low` untouched, and a real Save (Effort low -> medium made from the mis-displayed state) wrote `medium` while LEAVING model at `sonnet`; the resulting View card read `Sonnet` / `Medium` correctly. So the `<select>`'s DOM selection is out of sync with Blazor's bound field, not the bound field itself - display-only, but it tells the user their agent is on `Default (recommended)` when it is on Sonnet. DISCRIMINATOR: with a WARM catalog the same Edit card is correct - it renders `sonnet`/`Sonnet` within 66ms and never shows `default`. The fault is specific to the option list being replaced late. |

### MODELEFFORT-12 — A stored value the adapter no longer advertises is still offered and still saved

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-12--a-stored-value-the-adapter-no-longer-advertises-is-still-offered-and-still-saved)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App stopped, rows set to persona_models `Probe One \| claude-nonexistent-9` and persona_efforts `Probe One \| vintage`, app restarted. The View card read the raw stored values `claude-nonexistent-9` and `vintage`, and the card opened normally with no error. In Edit, after the catalog settled (10.2s), the Model select offered `claude-nonexistent-9::claude-nonexistent-9` as its own entry inserted directly after the blank option and ahead of the real catalog, WITH IT SELECTED; the Effort select likewise offered `vintage::vintage` selected, ahead of low/medium/high/xhigh/max. Saving without changing anything left both rows exactly as they were - still `claude-nonexistent-9` and `vintage` - and the card returned to View mode showing both raw values. IMPORTANT DISCRIMINATOR for issue #23: here the synthesised entry KEEPS its selection when the real catalog lands, whereas in MODELEFFORT-11 a stored value that also exists in the catalog (`sonnet`) loses it. So #23 is specifically about the synthesised option being removed once the real list contains an equivalent, with the DOM selection then falling to `default` - not about synthesised options in general. Posted as a comment on #23. |

### MODELEFFORT-13 — Remove an agent and recreate it with the same Name - nothing resurrects

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-13--remove-an-agent-and-recreate-it-with-the-same-name---nothing-resurrects)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | LADDER-MODEL = Sonnet. `Probe Two` created with Sonnet + Low stored exactly one row in each table (`Probe Two \| sonnet`, `Probe Two \| low`). Remove showed the two-step confirm (the row became Edit/Open/Restart/Confirm/Cancel) and after Confirm BOTH queries returned COUNT 0 for `Probe Two`. Recreating `Probe Two` with the same Title/Alias/body and BOTH selects left on `Use the agent's default` still returned COUNT 0 from both tables, and its card read `Agent default` / `Model default` - nothing resurrected. CASE-INSENSITIVE HALF: removing it again and creating `probe two` all-lowercase produced a card reading `Agent default` / `Model default`, and a `WHERE lower(persona_name)='probe two'` query returned COUNT 0 from both tables, so no row survived under either casing. Cleaned up. |

### MODELEFFORT-14 — Editing the frontmatter name moves the Model and Effort rows, and only those

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-14--editing-the-frontmatter-name-moves-the-model-and-effort-rows-and-only-those)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `Probe Three` created with Sonnet + Low: rows `Probe Three \| sonnet` and `Probe Three \| low`. The Edit textarea carried the whole file with the expected hint, and only the `name:` line was changed to `Probe Four`. After Save the tile list showed `Probe Four` and no `Probe Three`. THE ROWS MOVED AND ONLY THEY MOVED: persona_models holds exactly one row `Probe Four \| sonnet`, persona_efforts exactly one row `Probe Four \| low`, and a COUNT for `Probe Three` returned 0 in BOTH tables - moved, not copied, nothing left behind. Reopening the `Probe Four` tile showed Model `sonnet` and Effort `low` (raw ids, per MODELEFFORT-15's tile path), i.e. the choice survived the rename. The filename correctly did NOT change - the file is still `Probe Three.md` holding `name: 'Probe Four'`, exactly as the NOTE says it should be. ONE STEP NOT OBSERVABLE AS WRITTEN: step 8 reads `the card now showing` immediately after Save, and the pass line expects it to show the Sonnet/Low choice. It does not - it is the ghost card on the OLD name, with an empty Persona block and `Agent default` / `Model default`. That is the known rough edge already characterised and recorded under AGENTPROFILECARD-26, which its NOTE says to record rather than file, and it is a property of the rename flow rather than of the row move. Every Fail-if line here is about the rows, and none was triggered. |

### MODELEFFORT-15 — The View card's Model and Effort sections, and the label-versus-id inconsistency

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-15--the-view-cards-model-and-effort-sections-and-the-label-versus-id-inconsistency)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Probe One restored through the UI to Sonnet + Low first (MODELEFFORT-12 had left it stale), stored as `Probe One \| sonnet` and `Probe One \| low`. RIGHT AFTER SAVE the View card read the friendly labels - `MODEL \| Sonnet`, `EFFORT \| Low`. CLOSED with the x and REOPENED FROM THE TILE, the same card read the raw stored ids - `MODEL \| sonnet`, `EFFORT \| low`. All four values are therefore either the exact stored id or that id's catalog label; none is a third string, and neither section ever showed `Agent default` / `Model default` for an agent that has stored values. The switch is the documented consequence of the tile path never probing (MODELEFFORT-06), so the catalogs are empty there and only the raw id is available - recorded as a copy observation, not a defect. |

### MODELEFFORT-16 — The probe runs in App_Data\Agents, never in the repository root

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-16--the-probe-runs-in-app_dataagents-never-in-the-repository-root)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App_Data\work deleted first (Test-Path False), then a cold card open probed for 5.8s and populated both selects. App_Data\work exists again afterwards. The repository root is untouched: `git status --porcelain` returned the SAME two lines before and after the probe (` M docs/agencyteam/manual-tests/tracker.md` and `?? .claude/launch.json`, both this session's own work - no new entry appeared), and `Get-ChildItem -Force E:\Repos\Huddle` returned a byte-identical 22-name listing before and after, so no .claude artifact, session file or log was created at the root. Every `[agent stderr]` line emitted during the probe is a `[session/create]` phase line (validate-cwd, settings, prepare-query, sdk-initialize, models, modes, agents, register); none references CLAUDE.md, agents/CSharpPrinciples.md or any repository-root instruction file. |

### MODELEFFORT-17 — Two browser tabs opening cards at once spawn one adapter, not two

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-17--two-browser-tabs-opening-cards-at-once-spawn-one-adapter-not-two)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Both catalogs cold (app restarted, no card opened in the run). Two tabs on /agents; `New agent` clicked in each 6 MILLISECONDS apart (05:53:11.776Z and 05:53:11.782Z), so the overlap definitely happened. Across the full 45 seconds the 100ms watcher recorded exactly one rise - 0 -> 1 (pid 62976) at 22:53:12.453, back to 0 at 22:53:17.270 - PEAK 1, never 2. Both tabs ended with the identical 6-entry Model list (`::Use the agent's default, default, opus[1m], claude-fable-5-1[1m], sonnet, haiku`) and the identical 6-entry Effort list, both hints settled to the normal wording, neither stuck on `Reading…`. The app log has zero error-level lines and no stack trace, and #blazor-error-ui was computed display:none in BOTH tabs. No cross-circuit bleed - the two Effort lists matched because both selects were on the same blank model, not because one leaked. |

### MODELEFFORT-18 — The model catalog is cached for the whole app run, and goes fresh on restart

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-18--the-model-catalog-is-cached-for-the-whole-app-run-and-goes-fresh-on-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Step 1 list recorded: the 6 real entries. With the adapter renamed aside to node_modules.off and the app NOT restarted, re-opening the card returned a list IDENTICAL to step 1, the watcher stayed at 3 lines (no new rise at all), and no probe spawned - the successful catalog is cached for the app run exactly as documented. After restarting with the adapter still aside, the Model select contained ONLY `::Use the agent's default` and the Model hint read exactly `This agent advertises no models, so it will use its own default.` - so the list genuinely comes from the adapter and is not hardcoded or persisted, which is the serious failure this test exists to expose. Restoring node_modules and re-probing brought the real 6-entry list back. The documented staleness is recorded here, not filed. |

### MODELEFFORT-19 — No adapter installed: the page still works, and a failed probe is never cached

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-19--no-adapter-installed-the-page-still-works-and-a-failed-probe-is-never-cached)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Adapter renamed aside while the app was STOPPED, then started, and the observed card open was the FIRST of that run - the false-pass trap avoided. /agents rendered normally: 12 tiles, intro paragraph present, #blazor-error-ui display:none. The card opened; Model hint read exactly `This agent advertises no models, so it will use its own default.` and Effort hint exactly `This model offers no effort choice, so it will think as it normally does.`, with BOTH selects containing only `::Use the agent's default`. The log carries `No ACP adapter is installed; the model catalog is empty.` at Information from category `Agency.Huddle.App.Acp.ModelCatalogProbe`. The watcher never left 0 - nothing is spawned when there is nothing to spawn. `Probe NoAdapter` saved successfully with COUNT 0 in BOTH persona_models and persona_efforts. THE KEY GUARANTEE: after restoring node_modules and re-opening the card WITHOUT any app restart, the real 6-entry model list appeared - a failed probe is never cached, the deliberate opposite of MODELEFFORT-18. Cleaned up. |

### MODELEFFORT-20 — Adapter present but broken: launch failure and auth failure degrade to the same picker, with distinct log lines

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-20--adapter-present-but-broken-launch-failure-and-auth-failure-degrade-to-the-same-picker-with-distinct-log-lines)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | PART A (launch failure) ran in its own app run with `Team__Acp__Command=node-does-not-exist`, and the observed card open was the first of that run. The page rendered normally (12 tiles, intro paragraph, #blazor-error-ui display:none) and the card opened. Both selects contained ONLY `::Use the agent's default`; the Model hint read exactly `This agent advertises no models, so it will use its own default.` and the Effort hint exactly `This model offers no effort choice, so it will think as it normally does.` The log carries `Model catalog probe failed to start or talk to the adapter.` at WARN from `Agency.Huddle.App.Acp.ModelCatalogProbe` - a different line from Part A's no-adapter counterpart in MODELEFFORT-19, which logged `No ACP adapter is installed; the model catalog is empty.` at Information, so the log does disambiguate the two causes behind identical on-screen text. The hint settled in 104ms - nowhere near the 20s timeout, no hang - and the page stayed usable afterwards (Cancel, then a tile click opened a card, then it closed cleanly, still no error bar). BONUS corroboration for MODELEFFORT-16: the inner exception names the working directory explicitly - `An error occurred trying to start process 'node-does-not-exist' with working directory 'E:\Repos\Huddle\src\Huddle.App\App_Data\work'`. PART B (authentication) INCONCLUSIVE, as the test's own Inconclusive clause directs: there is no safe way to put the Claude CLI into an unauthenticated state on this machine without disturbing the user's real login, and the test says to record it rather than improvise with credentials. PART C (timeout) NOT OBSERVED: no probe hung during any test in this area - every one settled in 3-11 seconds - so there is nothing to report, which is the expected outcome. Env var cleared afterwards. |

### MODELEFFORT-21 — An existing App_Data opens with the two tables added in place, no wipe

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-21--an-existing-app_data-opens-with-the-two-tables-added-in-place-no-wipe)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | team.db backed up first. Before the drop: users COUNT 12, rooms COUNT 11. Both persona tables dropped and confirmed gone - sqlite_master listed only room_members, rooms, users. The app then started with NO exception (error-level log empty) and /agents rendered with the SAME 12 agent tiles as before. After startup sqlite_master lists BOTH `persona_models` and `persona_efforts` again, recreated in place. Nothing was wiped: users is still 12 and rooms is still 11. Creating `Probe Migrate` with Sonnet + Low wrote straight into the freshly created tables - `Probe Migrate \| sonnet` and `Probe Migrate \| low` - so the stores are writing into them, not just creating them. Cleaned up through the card; team.db.backup left in App_Data and removed after the pass. |

### MODELEFFORT-22 — RESTART LANE: changing the Model restarts the runner, exactly once

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-22--restart-lane-changing-the-model-restarts-the-runner-exactly-once)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Restart lane, 10 runners up, Probe One Online on `haiku`. Changing ONLY the Model to Sonnet and saving replaced its runner EXACTLY ONCE: the 100ms watcher shows the two picker probes first (+89852 at 22:59:34 then gone, +47136 at 22:59:45 then gone - those are the Edit card's model-catalog and effort-ladder probes, before the Save), then a single runner replacement - 50420 disappears and 92696 takes its place at 22:59:51-53 - with the total back to 10. Probe One's PID therefore changed and no second replacement followed. persona_models is now `Probe One \| sonnet` (Sonnet's wire id) and the card read `MODEL \| Sonnet`, status back to `Online`. The log carries `The agent process disconnected.` followed by a fresh `[agent stderr]` burst (36 lines), and ZERO `Requested model '...' is not in the agent's advertised model catalog` warnings. The on-screen status transition was judged from watcher + PID + log evidence because the browser sampling call timed out, which the test's Inconclusive clause explicitly permits. FIXTURE NOTE: the test asks to start from HAIKU with Effort Low, but Haiku advertises no effort ladder here (MODELEFFORT-02), so the start state was Haiku with Effort unset - this is a Model test and the effort plays no part. |

### MODELEFFORT-23 — RESTART LANE: changing Effort Low to Medium restarts, with no catalog warning

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-23--restart-lane-changing-effort-low-to-medium-restarts-with-no-catalog-warning)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Run on LADDER-MODEL = Sonnet, per MODELEFFORT-02, with Effort set to `Low` first. Changing ONLY the Effort from `Low` to `Medium` and saving: the status left `Online` at 619ms, showed `Starting`, and returned to `Online` at 2606ms - EXACTLY ONE transition, no storm. The adapter PID changed 13420 -> 100976. The log carries one `The agent process disconnected.` followed by a fresh 9-line `[agent stderr]` burst. persona_efforts holds exactly `medium`, lowercase - not `Medium`. The whole log contains ZERO `Requested effort 'medium' is not in the agent's advertised effort catalog for the current model` warnings and zero model-catalog warnings, so the stored effort resolved against the CURRENT model's advertised catalog rather than a pre-switch snapshot. |

### MODELEFFORT-24 — RESTART LANE: saving with nothing changed does NOT restart the runner

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-24--restart-lane-saving-with-nothing-changed-does-not-restart-the-runner)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | False-defect trap checked FIRST: `Probe One.md` is UI-created and the CRLF test printed False, so the file is LF-only and a no-op save genuinely has nothing to write. Two consecutive no-op saves (Edit opened, both selects left settled and untouched, Save clicked) each produced: status `Online` continuously for the full 15+ second watch with no `Starting`, `Offline` or `Degraded` flicker on card or tile; ZERO `The agent process disconnected.` lines; ZERO new `[agent stderr]` lines; in fact ZERO new log lines at all; and the adapter PID list byte-identical before and after both saves (27404,50448,51760,60888,78016,81504,92336,93756,100976,101864). The 100ms watcher logged nothing new either, so the 500ms-debounced watcher event that the file write also fires likewise decided `no change`. The second save behaved identically to the first. |

### MODELEFFORT-25 — RESTART LANE: a stale stored Model or Effort warns but never blocks the agent

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-25--restart-lane-a-stale-stored-model-or-effort-warns-but-never-blocks-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | App stopped, persona_models set to `claude-nonexistent-9` and persona_efforts to `vintage` for Probe One, then started in the restart lane. The log carries BOTH warnings verbatim: `Requested model 'claude-nonexistent-9' is not in the agent's advertised model catalog; continuing on the agent's default.` and `Requested effort 'vintage' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.` Despite both, the `Probe One` tile reached `Online` and all 10 runners started - a stale value warns, it does not brick. Zero startup exceptions. The card showed the STORED values, `MODEL \| claude-nonexistent-9` and `EFFORT \| vintage`, not defaults. Fixture restored afterwards through the UI. |

### MODELEFFORT-26 — RESTART LANE: the manual Restart button is offered in every state except Starting, and works on an Offline or Degraded agent

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-26--restart-lane-the-manual-restart-button-is-offered-in-every-state-except-starting-and-works-on-an-offline-or-degraded-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | HEALTHY HALF: an `Online` agent's action row is exactly `Message`, `Edit`, `Open`, `Remove` - no `Restart` - and the element immediately after the row is the Persona section, so no hint; the string `Like saving an edit` appears nowhere on the card. UNHEALTHY FIXTURE made by `Stop-Process` on one adapter (pid 73352); `Quill` went `Offline` and stayed there - no self-recovery, which is documented and correct. Its card's action row gained `Restart` and the hint below read exactly `Like saving an edit, this restarts the agent, which clears what it remembers.` Clicking `Restart` removed the button within 10ms as the status went `Starting` (the `Restarting…` + disabled state is the same one captured at 10ms granularity in AGENTPROFILECARD-42; here the transition was faster than the 10ms sample). The immediate SECOND click found no button at all, so only one restart ran - stronger than `does nothing`. NOT a no-op where it matters most: the status reached `Online`, the adapter count returned to 10 with a NEW pid 77724 while the killed 73352 stayed dead, the log gained 30 lines including a fresh 9-line `[agent stderr]` burst, and zero `failed to start` lines. |

### MODELEFFORT-27 — MONEY: the chosen Model and Effort actually reach the model - ask it

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-27--money-the-chosen-model-and-effort-actually-reach-the-model---ask-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | PAID - three prompt turns on a tight `Team__AgentMessageBudget=6`, roughly single-digit cents. The load-bearing half is decisive: with Model = Haiku the agent replied `I'm running as Claude Haiku 4.5 (model ID: claude-haiku-4-5-20251001).`; after switching ONLY the Model to Sonnet and saving, the identical message returned `I'm running as Sonnet 5 (model ID claude-sonnet-5)`. The two answers name DIFFERENT models, so the picker's value reaches session/new rather than stopping at the database. Restart evidence between turns: `The agent process disconnected.` lines with fresh `[agent stderr]` bursts, and the tile returned to `Online` each time. CATALOG WARNINGS: the run's log holds three, at lines 280, 282 and 407, and every one names `claude-nonexistent-9` or `vintage` - the MODELEFFORT-25 fixture seeded earlier in the same app run. NONE names haiku, sonnet or medium, so there is no warning for any value under test here. Database ends holding `Probe One \| sonnet` and `Probe One \| medium`. EFFORT SELF-REPORT INCONCLUSIVE, exactly as the test directs: the model said `thinking effort is not something I have visibility into from inside the session - that's a harness-level setting`, so the absent effort-catalog warning is the oracle for that half. DEVIATION: step 9 asks to change Effort Low -> Medium, but the step-6 model switch clears the effort (MODELEFFORT-07), so the change made was blank -> Medium on Sonnet. |

### MODELEFFORT-28 — Both pickers are inert while their own catalog is being probed, and the Model list lands without waiting on the Effort one

File: [`docs/engineering/manual-tests/model-effort.md`](model-effort.md#modeleffort-28--both-pickers-are-inert-while-their-own-catalog-is-being-probed-and-the-model-list-lands-without-waiting-on-the-effort-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Creating Rooms, inviting Agents, Room naming

### INVITEROOMS-02 — Routing: / redirects to the first Room, and an unknown room id shows the empty state instead of crashing

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-02--routing--redirects-to-the-first-room-and-an-unknown-room-id-shows-the-empty-state-instead-of-crashing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `/rooms/deadbeef` returned a page whose main column reads exactly `No rooms yet. Start an agent to create one.`, with the sidebar still listing `echo` and `alpha` - unaffected by the bad id - and no `An unhandled error has occurred.` bar (#blazor-error-ui computed display:none). The address bar stayed on /rooms/deadbeef. Returning to `/` redirected to `/rooms/01a0a3ae26dd774f934b0e01fae58bdf` again with h1 `echo`. The misleading copy is noted as documented behaviour, NOT filed: the sentence says `No rooms yet` while two rooms are plainly visible in the sidebar. |

### INVITEROOMS-03 — The New chat panel toggles, lists every Agent with a status dot, and keeps Start chat disabled until something is ticked

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-03--the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  | The panel is hidden on first page load (.new-chat-panel hidden=true). Opening it lists exactly two checkbox rows, `echo` then `alpha`, in that order, each with a dot whose class is `agent-dot online` and whose title reads exactly `online` - both demo agents are connected, matching T-A. `You` does not appear anywhere in the panel. Before anything is ticked `Start chat` has the `disabled` attribute AND disabled=true, and clicking it did nothing (the URL was unchanged afterwards). Ticking `echo` removed the disabled attribute and made it clickable; unticking restored disabled=true. Clicking `New chat` a second time closed the panel (hidden=true). DB order check: users kind='agent' by rowid returns `echo` then `alpha`, matching the panel order. The documented known limit about dots not repainting on presence change was respected - not tested. |

### INVITEROOMS-04 — A pipe client connecting creates its Room and it appears in the sidebar with no page refresh

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-04--a-pipe-client-connecting-creates-its-room-and-it-appears-in-the-sidebar-with-no-page-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | T-C printed `Connecting to pipe '\\.\pipe\team' as agent 'mybot'...`, then `Sent hello. Listening for messages (Ctrl+C to exit)...`, then the welcome envelope `{"type":"welcome","agentId":"01a0a3aeea2b71cca4933481e98fad96","name":"mybot","rooms":[{"id":"01a0a3aeea2f77f98be45eb4cabde6b0",...}],"version":3}`. A `mybot` link appeared in the already-rendered sidebar with NO interaction and NO reload - performance.getEntriesByType('navigation').length stayed at 1 across the whole test. T-A logged `Created direct room '01a0a3aeea2f77f98be45eb4cabde6b0' for agent 'mybot'.` Clicking the link landed on that exact /rooms/<id> with h1 `mybot` and members line `You, mybot`, and the Transcript was empty (0 message elements). DB: users holds `mybot \| agent`. |

### INVITEROOMS-05 — Reconnecting under the same Name re-attaches to the existing Room — it never mints a second one

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-05--reconnecting-under-the-same-name-re-attaches-to-the-existing-room--it-never-mints-a-second-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Recorded before the restart: mybot's room /rooms/01a0a3aeea2f77f98be45eb4cabde6b0, 3 sidebar links, 20 log lines, 3 rooms in team.db. Killed T-C and re-ran the identical command. The reconnect welcome carried the SAME agentId (01a0a3aeea2b71cca4933481e98fad96) and the SAME room id. Afterwards: still exactly ONE `mybot` link, sidebar link count still 3, T-A still at 20 lines with `Created direct room` still totalling 3 (no new line during the reconnect), room count still 3, and the `mybot` link still points at the identical /rooms/<id>. FindRoomWithExactMembersAsync re-attached rather than minting. |

### INVITEROOMS-06 — A Name differing only in case re-attaches to the existing Agent instead of creating a second one

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-06--a-name-differing-only-in-case-re-attaches-to-the-existing-agent-instead-of-creating-a-second-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `MYBOT` (upper case) connected successfully and was NOT rejected - it printed `Sent hello. Listening for messages…` and received a welcome. Crucially the welcome echoed the identity back in LOWER case with the same id: `"agentId":"01a0a3aeea2b71cca4933481e98fad96","name":"mybot"` and the same room 01a0a3aeea2f77f98be45eb4cabde6b0, so it re-attached rather than creating a second identity. T-C, which was running as `mybot`, printed `Server closed the connection.` - the server closed the stale connection when the same identity re-registered. No new sidebar link appeared (still echo, alpha, mybot; no `MYBOT` entry), T-A logged no new `Created direct room` (still 3), room count still 3, and `SELECT name FROM users WHERE name LIKE 'mybot'` returns exactly ONE row still spelled `mybot`. |

### INVITEROOMS-07 — An invalid Agent Name is refused at the handshake and no Room appears anywhere

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-07--an-invalid-agent-name-is-refused-at-the-handshake-and-no-room-appears-anywhere)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All three names rejected at the handshake with the exact envelope shape. `bad name!` -> `{"type":"error","code":"invalidName","message":"'bad name!' is not a valid agent name.","version":3}`; `Emily  Lee` (two spaces) -> the same with `'Emily  Lee'`; `bot.exe` -> the same with `'bot.exe'`. Each connection then ended - `Server closed the connection.` and the script process exited on its own, no hang. The sidebar was IDENTICAL afterwards (echo, alpha, mybot), agent users are still exactly echo/alpha/mybot with none of the three bad names, room count still 3, and T-A logged no `Created direct room` for any of them. METHOD NOTE, the trap the Inconclusive clause warns about: passing a name containing a space through Start-Process -ArgumentList as an array silently split it - the script echoed `as agent 'bad'` and `as agent 'Emily'`, connecting to a mangled pipe name. Those two runs were discarded as inconclusive and re-run with the quoting preserved in a single argument string, at which point the `Connecting to pipe '\\.\pipe\team' as agent '<name>'` line echoed the exact intended string and the real rejection was observed. |

### INVITEROOMS-08 — New chat with exactly ONE Agent opens that Agent's existing Room and creates nothing

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-08--new-chat-with-exactly-one-agent-opens-that-agents-existing-room-and-creates-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | THE REUSE BRANCH HOLDS. ROOM-ECHO recorded as /rooms/01a0a3ae26dd774f934b0e01fae58bdf. Posted `hi @echo`; echo replied `echo: hi echo`, giving a two-message Transcript plus the budget line `1 of 40 agent replies since you last spoke.` Baseline taken: 3 sidebar links, 20 log lines, 3 rooms. Then New chat with ONLY `echo` ticked (verified: echo checked, alpha and mybot unchecked) and Start chat clicked. RESULT: the address bar reads exactly ROOM-ECHO - unchanged; both messages are still in the Transcript with identical text; the sidebar count is still 3 with exactly one `echo`; room count in team.db still 3; and T-A is still at 20 lines with ZERO `Created room` lines and `Created direct room` still totalling 3. The panel closed itself, and reopening it showed all three checkboxes unticked. |

### INVITEROOMS-09 — New chat with two Agents creates a Room named after them and navigates to it, live

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-09--new-chat-with-two-agents-creates-a-room-named-after-them-and-navigates-to-it-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Ticked `echo` FIRST then `alpha`. Start chat navigated to a NEW room /rooms/01a0a3b1aba9766a96530559380fbcb5, different from every id recorded so far. The h1 reads exactly `echo, alpha` - the tick order, with no `You` in it - and the grey line beneath reads exactly `You, echo, alpha` with `You` first. A new sidebar link `echo, alpha` appeared WITHOUT a reload (navigation entries stayed at 1), and that link carries the `active` class while the other three carry none. The panel closed itself and reopening showed both checkboxes cleared. T-A printed `Created room '01a0a3b1aba9766a96530559380fbcb5' (echo, alpha) with 3 members.` DB check: room_members in rowid order returns `You`, then `echo`, then `alpha`. |

### INVITEROOMS-10 — The same two-Agent selection twice creates a SECOND Room with the identical name

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-10--the-same-two-agent-selection-twice-creates-a-second-room-with-the-identical-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | ROOM-EA1 = /rooms/01a0a3b1aba9766a96530559380fbcb5. Ticking the identical pair in the identical order again navigated to /rooms/01a0a3b1d85b75ebba9fe97efcaf5a43 - a DIFFERENT id. The sidebar now shows TWO links both reading `echo, alpha` pointing at the two different ids (the new one carrying `active`), total links 5, with no reload. T-A printed exactly one new `Created room '01a0a3b1d85b75ebba9fe97efcaf5a43' (echo, alpha) with 3 members.` The duplicate NAME is correct behaviour and is not filed; what this test hunts - the second click doing nothing - did not happen. |

### INVITEROOMS-11 — Invite agent on the Room header offers only Agents that are not already Members, and starts closed

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-11--invite-agent-on-the-room-header-offers-only-agents-that-are-not-already-members-and-starts-closed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | In mybot's Room (h1 `mybot`, no comma, so the Direct Room was still unconverted). Before any click `div.invite-panel` carried the `hidden` attribute and no candidate list was visible. The button clicked carries class `invite-toggle` and reads `Add agent` - the Room-header control, not the /agents submit button. After the click the panel showed a `<select>` labelled `Team` whose first option is `All teams`, and the candidate list contained exactly `echo` and `alpha` - the two Agents not already in this Room. `mybot` itself is ABSENT and `You` is ABSENT. Each entry carries a status dot with class `agent-dot online` and title `online`. |

### INVITEROOMS-12 — Clicking a candidate invites it, renames the Room, and repaints all four surfaces with no reload

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-12--clicking-a-candidate-invites-it-renames-the-room-and-repaints-all-four-surfaces-with-no-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All four live surfaces repainted with no reload. Seeded the Transcript first: `hi @mybot` -> `mybot: hi mybot`, giving a 2-line rooms/01a0a3aeea2f77f98be45eb4cabde6b0.jsonl. Clicking `alpha` in the candidate list produced the panel line reading EXACTLY `Invited alpha. Room is now "mybot, alpha".` (class composer-info, straight double quotes as specified). `alpha` disappeared from the candidate list, leaving only `echo`. The h1 changed `mybot` -> `mybot, alpha`; the grey members line changed to `You, mybot, alpha`; the sidebar link RENAMED IN PLACE from `mybot` to `mybot, alpha` with no new link added (still 5 links, room count still 5). The address bar was unchanged and navigation entries stayed at 1 - no reload. T-A printed `Invited agent 'alpha' (01a0a3ae26d673fda244a3e29b9f1a62) into room '01a0a3aeea2f77f98be45eb4cabde6b0'.` THE PINNED BEHAVIOUR HOLDS: the .jsonl line count is IDENTICAL at 2 before and after - an Invitation persists no Message. mybot's Direct Room is now a group Room, which is what INVITEROOMS-21 needs. |

### INVITEROOMS-13 — Invite agent says so when every Agent is already a Member, and hides the Team filter in that state

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-13--invite-agent-says-so-when-every-agent-is-already-a-member-and-hides-the-team-filter-in-that-state)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Continuing in the `mybot, alpha` Room with one candidate left. Clicking `echo` produced `Invited echo. Room is now "mybot, alpha, echo".` and emptied the list, which was then replaced by the sentence `Every agent is already in this room.` The `Team` <select> is NOT rendered in that state - querySelector('select') inside .invite-panel returned null, where it had been present with `All teams` while candidates existed. The h1 now reads `mybot, alpha, echo` and the members line `You, mybot, alpha, echo`. T-A shows exactly one `Invited agent ...` line per click and no more (two in total for this Room: alpha then echo). DB check: room_members for this room = 4 and total users = 4, so every registered user is a Member. |

### INVITEROOMS-14 — The invite panel's open state and its message do not survive a Room switch

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-14--the-invite-panels-open-state-and-its-message-do-not-survive-a-room-switch)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Room A (`mybot, alpha, echo`) had its panel OPEN carrying the leftover line `Invited echo. Room is now "mybot, alpha, echo".` plus `Every agent is already in this room.` Navigating to Room B (an `echo, alpha` Room) arrived with the panel CLOSED - .invite-panel carries the `hidden` attribute and hidden=true - and nothing visible beneath Add agent. Opening Room B's panel showed NO carried-over info or error line (composer-info/composer-error both empty) and a candidate list containing only `mybot` - correct, since echo and alpha are Members of Room B and were therefore excluded even though they had been offered in Room A. Returning to Room A showed its panel closed again AND the stale `Invited echo…` line gone; the panel text was just `Every agent is already in this room.` |

### INVITEROOMS-15 — /invite @name in the composer does the same thing as the header control

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-15--invite-name-in-the-composer-does-the-same-thing-as-the-header-control)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `gamma` registered (T-F) and its link appeared in the sidebar. In the first `echo, alpha` Room (01a0a3b1aba9766a96530559380fbcb5, composer placeholder `Message… (/invite @agent)`, no .jsonl file yet), typing `/invite @gamma` and pressing Enter produced a `composer-info` line reading EXACTLY `Invited gamma. Room is now "echo, alpha, gamma".` NOTHING was added to the Transcript - no message bubble for the command, and the composer cleared itself. The h1 became `echo, alpha, gamma`, the members line `You, echo, alpha, gamma`, and the sidebar link renamed IN PLACE, all with the URL unchanged and navigation entries still 1. T-A printed `Invited agent 'gamma' (01a0a3b308e57248b0a8b4741684627a) into room '01a0a3b1aba9766a96530559380fbcb5'.` The room's .jsonl file STILL does not exist, so the command persisted nothing. |

### INVITEROOMS-16 — /invite accepts a multi-word Name, with and without the @, and never truncates at the space

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-16--invite-accepts-a-multi-word-name-with-and-without-the--and-never-truncates-at-the-space)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | THE DOCUMENTED SILENT FAILURE DID NOT OCCUR - the two-word Name is captured whole, not truncated at the space, in BOTH forms. `Emily Lee` registered (T-E) with a sidebar link reading exactly `Emily Lee`. WITH the @: in the second `echo, alpha` Room, `/invite @Emily Lee` produced the info line `Invited Emily Lee. Room is now "echo, alpha, Emily Lee".` - the full two-word Name - and the h1 became `echo, alpha, Emily Lee` with members `You, echo, alpha, Emily Lee`. No `Unknown agent @Emily` error, no truncation. WITHOUT the @: in a different Room (`echo, alpha, gamma`), `/invite Emily Lee` produced `Invited Emily Lee. Room is now "echo, alpha, gamma, Emily Lee".` and the h1 became `echo, alpha, gamma, Emily Lee` with members `You, echo, alpha, gamma, Emily Lee`. Neither command left a message bubble in the Transcript. |

### INVITEROOMS-17 — /invite of an Agent already in the Room reports success and changes nothing

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-17--invite-of-an-agent-already-in-the-room-reports-success-and-changes-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Used the `echo` Room (h1 `echo`, members `You, echo`) and the Agent `echo`, already a Member. `/invite @echo` produced a `composer-info` line reading exactly `Invited echo. Room is now "echo".` The h1 is UNCHANGED at exactly `echo` - not `echo, echo`; the members line is unchanged at exactly `You, echo` with `echo` appearing once; the sidebar link is unchanged. DB: room_members for that room is still 2 and rooms.name is still `echo`, so the insert is still INSERT OR IGNORE and no Member was duplicated. The `Invited echo` wording over a no-op is the KNOWN rough edge and is not filed. |

### INVITEROOMS-18 — /invite rejects an unknown Name, and rejects the Human

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-18--invite-rejects-an-unknown-name-and-rejects-the-human)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | `/invite @nobody` produced a line of class `composer-error` reading exactly `Unknown agent @nobody`. `/invite @You` produced `composer-error` reading exactly `Unknown agent @You` - the Human was NOT added. After both, the h1 is still `echo`, the members line still `You, echo` (with `You` once), and the sidebar unchanged. No `Invited agent` line was logged for either attempt (the running total stayed at 6, all accounted for by the five real invites plus the idempotent one from INVITEROOMS-17). #blazor-error-ui stayed display:none - the unknown-name path raises a ChatException rather than throwing. Judged on the CSS class as the Inconclusive clause directs: composer-error, not composer-info. |

### INVITEROOMS-19 — Any other leading-slash text is refused as Unknown command, and the typed text is lost

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-19--any-other-leading-slash-text-is-refused-as-unknown-command-and-the-typed-text-is-lost)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All three inputs produced a `composer-error` line reading exactly `Unknown command`: `/help`, bare `/invite`, and `/invite ` with one trailing space. Nothing was added to the Transcript for any of them - the echo Room's .jsonl stayed at 2 lines across all three - and the composer textarea was EMPTY after each Enter. No `An unhandled error has occurred.` bar (#blazor-error-ui display:none). BOTH known limits recorded, not filed: (a) an ordinary message beginning with a slash cannot be posted at all, since every leading-slash text goes through the parser; (b) the client-side Enter handler clears the textarea before the server answers, so a rejected command loses what was typed - confirmed here, the text was gone in all three cases. |

### INVITEROOMS-20 — A Room's name is always its Agent Members joined by ", " in membership order, with the Human excluded

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-20--a-rooms-name-is-always-its-agent-members-joined-by---in-membership-order-with-the-human-excluded)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Ticked `alpha` FIRST then `echo` - the reverse of INVITEROOMS-09. Immediately after Start chat the h1 read exactly `alpha, echo` (tick order, not alphabetical and not the New chat list order) and the members line exactly `You, alpha, echo`. After `/invite @gamma` the info line read `Invited gamma. Room is now "alpha, echo, gamma".`, the h1 became exactly `alpha, echo, gamma` - gamma APPENDED with alpha and echo in their original order, no reshuffle - and the members line `You, alpha, echo, gamma` with `You` first and once. The sidebar link for this room reads `alpha, echo, gamma`, agreeing with the h1. DB check: agent members ORDER BY m.rowid returns `alpha`, `echo`, `gamma` in that order, and joining them with ', ' equals rooms.name exactly (`alpha, echo, gamma`). No `You` and no id fragment anywhere in the name. |

### INVITEROOMS-21 — An Agent belongs to at most one two-Member Room: after an invite, the next registration mints a fresh Direct Room

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-21--an-agent-belongs-to-at-most-one-two-member-room-after-an-invite-the-next-registration-mints-a-fresh-direct-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | ADR-0003's consequence holds. OLD-ROOM = 01a0a3aeea2f77f98be45eb4cabde6b0, by then named `mybot, alpha, echo` with 4 Members and a 2-line .jsonl holding the `hi @mybot` exchange. Stopping T-C and re-running `mybot` produced a SECOND sidebar link reading exactly `mybot` with no reload (sidebar went 8 -> 9, navigation entries stayed 1). T-A printed a new `Created direct room '01a0a3b55c467cf3873485fe2cdd9706' for agent 'mybot'.` naming an id not seen before, and mybot's welcome envelope now lists BOTH rooms - the group room `mybot, alpha, echo` and the fresh 2-member `mybot`. The new Room's h1 reads `mybot`, its members line `You, mybot`, and its Transcript is empty. OLD-ROOM is still present, still named `mybot, alpha, echo`, still holds `hi @mybot` / `mybot: hi mybot` - not renamed back, not re-keyed. Two sidebar entries for mybot is the DESIGNED outcome and is not filed. |

### INVITEROOMS-22 — Two browser tabs stay in step on a membership change, including an open candidate list

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-22--two-browser-tabs-stay-in-step-on-a-membership-change-including-an-open-candidate-list)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Created a fresh Room without gamma (01a0a3b620007b88abefe89ea344bb20, `echo, alpha`) and opened it in BOTH tabs. Tab B's invite panel was left OPEN with `gamma` listed among its candidates (mybot, gamma, Emily Lee). Tab A then ran `/invite @gamma` and got `Invited gamma. Room is now "echo, alpha, gamma".` Switching to tab B WITHOUT reloading it (navigation entries still 1): its sidebar link for this Room had renamed to `echo, alpha, gamma`, its h1 read `echo, alpha, gamma`, its members line `You, echo, alpha, gamma`, and `gamma` had VANISHED from its still-open candidate list, leaving `mybot` and `Emily Lee`. So the open panel reloaded its candidates rather than merely repainting. T-A printed exactly ONE `Invited agent 'gamma' ... into room '01a0a3b620007b88abefe89ea344bb20'.` line, and the whole run's log contains ZERO occurrences of `A RoomsChanged handler threw and was skipped.` - no leaked subscription. No error bar in tab B. |

### INVITEROOMS-23 — Adding a third Member flips a Room from answer-everything to mention-gated

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-23--adding-a-third-member-flips-a-room-from-answer-everything-to-mention-gated)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Fresh `delta` bot (T-G) gave a brand-new two-Member Direct Room 01a0a3b685367b628ea082f2144495d8 with members `You, delta`. TWO-MEMBER: `hi @delta` drew exactly ONE reply, `delta: hi delta`. Then `alpha` was invited from the header control, making it `delta, alpha` with members `You, delta, alpha`. THREE-MEMBER GATE: a plain `hi` with no @ anywhere drew NO reply at all from either Agent across a 7-second wait - the senders list went `You, delta, You` with nothing after it. `hi @alpha` then drew exactly ONE reply, `alpha: hi alpha`, and `delta` stayed silent. The .jsonl confirms it: five lines with senderName `You`, `delta`, `You`, `You`, `alpha` - every reply line names exactly the Agent that was Mentioned in the message before it, and there is never a line from an unmentioned Agent. NO ECHO LOOP: the file was still 5 lines after a further 6-second quiet wait, so the Transcript stopped growing once each reply landed. |

### INVITEROOMS-24 — Rooms, names and membership survive a restart; the Room Budget does not

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-24--rooms-names-and-membership-survive-a-restart-the-room-budget-does-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Recorded 11 sidebar names in order plus three Rooms in full, then stopped the app AND every bot, and restarted in the free lane. AFTER THE RESTART the sidebar lists exactly the same 11 names in exactly the same order: echo, alpha, `mybot, alpha, echo`, `echo, alpha, gamma, Emily Lee`, `echo, alpha, Emily Lee`, gamma, `Emily Lee`, `alpha, echo, gamma`, mybot, `echo, alpha, gamma`, `delta, alpha`. Every renamed Room kept its renamed name - none reverted to a single-Agent name. The three recorded Rooms are byte-identical: `echo` still h1 `echo` / members `You, echo` / 2 messages `hi @echo`..`echo: hi echo`; `delta, alpha` still h1 `delta, alpha` / members `You, delta, alpha` / 5 messages `hi @delta`..`alpha: hi alpha`; `mybot, alpha, echo` still h1 `mybot, alpha, echo` / members `You, mybot, alpha, echo` / 2 messages. T-A printed ZERO `Created direct room` lines on restart, so echo and alpha re-attached to their existing two-Member Direct Rooms rather than minting new ones, and no duplicate echo/alpha appeared. No Room was paused before the restart, so the Budget half had nothing to observe; the documented expectation that a restart un-pauses is recorded, not filed. |

### INVITEROOMS-25 — The Team filter narrows candidates, and an Agent with no Persona only ever shows under All teams

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-25--the-team-filter-narrows-candidates-and-an-agent-with-no-persona-only-ever-shows-under-all-teams)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Baseline with no Persona files: the Team select offered only `All teams`. Created App_Data\Teams\coo.md with the six frontmatter lines (Name coo, Title Chief of Staff, Alias coo, Teams Business) plus the body, and reloaded. The select then offered exactly `::All teams` and `Business::Business`. Under `All teams` the candidate list held the five raw pipe clients alpha, mybot, gamma, `Emily Lee`, delta. Selecting `Business` replaced the list with the sentence `No agents in this team.` - correct, since every candidate is a raw pipe client with no Persona and therefore no Teams, and `coo` has never registered as an Agent with Acp off. Selecting `All teams` again brought back the identical five-entry list. NO SPAWN: total node.exe was 18 before and 18 after the filter changes, and acp adapters stayed 0 - the filter is pure display narrowing. No error bar. |

### INVITEROOMS-26 — /invite accepts a Persona's Alias wherever it accepts its Name

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-26--invite-accepts-a-personas-alias-wherever-it-accepts-its-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Ran with Team__Acp__Enabled=true. Persona `Jarvis` (Title Butler, Alias jar, Model Haiku) created through the Create card; it reached `Online` and registered as an Agent - T-A logged `Created direct room '01a0a3b96fb57d8b83e2f250ab0d7ddc' for agent 'Jarvis'.` and a `Jarvis` sidebar link appeared. BOTH alias forms resolved to a BLUE composer-info line, never red. Lower case in the `echo, alpha, Emily Lee` Room: `Invited jar. Room is now "echo, alpha, Emily Lee, Jarvis".` Upper case in the `echo, alpha, gamma` Room: `Invited JAR. Room is now "echo, alpha, gamma, Jarvis".` Both halves behave as specified - the first half echoes the handle typed (`jar` / `JAR`) while the second half quotes the real Room name containing `Jarvis`. Both h1s and both sidebar links end in `Jarvis`, never `jar` or `JAR`. T-A printed `Invited agent 'Jarvis' (01a0a3b96fb375748099b67a30551566) into room '<id>'.` for EACH invite - naming the Persona, not the alias - and zero lines naming 'jar'. DB: rooms are named `echo, alpha, Emily Lee, Jarvis`, `echo, alpha, gamma, Jarvis` and `Jarvis`; no room is named `jar`. DEVIATION: step 11 asks for Effort `low`, but Haiku advertises no effort ladder on this adapter (MODELEFFORT-02), so Effort was left at the model default. No tokens spent - starting a Persona opens a session and never prompts. |

### INVITEROOMS-27 — The Agent card's Message action disappears once that Persona's Direct Room has been invited into

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-27--the-agent-cards-message-action-disappears-once-that-personas-direct-room-has-been-invited-into)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | BEFORE the invite, Jarvis's card (status Online, its Direct Room `Jarvis` still two-Member with members `You, Jarvis`) offered the actions `Message` -> /rooms/01a0a3b96fb57d8b83e2f250ab0d7ddc, `Edit`, `Open`, `Remove`. Inviting `alpha` into that Direct Room from the Room header made it `Jarvis, alpha` with members `You, Jarvis, alpha`. AFTER the invite, reopening the Jarvis tile shows the actions `Edit`, `Open`, `Remove` - `Message` is GONE, exactly as designed, with `Edit` and `Open` both still present so the row was not suppressed wholesale. The card opened normally with no error line and #blazor-error-ui display:none - the lookup returned null rather than throwing, and there is no dead link where Message used to be. This disappearance is the DESIGNED consequence of membership-defined Direct Rooms and is recorded, not filed. |

### INVITEROOMS-28 — An Agent creates a Room with mcp__team__create_room and it appears live in the sidebar

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-28--an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | PAID. ACP on, Team__AgentMessageBudget=6 (confirmed live - the Room budget line read `1 of 6 agent replies since you last spoke.`). Two Personas online on Haiku: `Jarvis` and a new `Friday` (Analyst / fri). New chat with Jarvis + Friday + echo gave room 01a0a3bb157a7263a40f0528772d7fc7 (`Jarvis, Friday, echo`). ONE Turn: `@Jarvis start a separate room with Friday and give it the context` -> Jarvis called the tool and replied `Done - created room "Jarvis, Friday" (01a0a3bb65d674b093f8af91707caeb0) and posted Agency.Huddle project context.` A NEW sidebar link `Jarvis, Friday` appeared with NO reload (15 -> 16 links). T-A printed `Created room '01a0a3bb65d674b093f8af91707caeb0' (Jarvis, Friday) with 3 members.` THE MOST SERIOUS CONDITION HOLDS: the new Room's members line BEGINS with `You` (`You, Jarvis, Friday`) and the DB confirms a room_members row with user_id='human'. Jarvis did not report a missing tool, so the full `mcp__team__create_room` spelling is reaching it. The Transcript is readable and the Room stopped growing after the Turn. OBSERVATION worth recording (not a defect of this test): asked to `give it the context`, Jarvis read the repository - CLAUDE.md, the docs tree and live `git status` - and PERSISTED that into the new Room's transcript, including the current branch, modified files and last commit subject. Same tool-reach concern as issue #22, but written to a Room rather than the user's home directory. |

### INVITEROOMS-29 — An Agent invites another with mcp__team__invite_agent, using the room id from its own [Room: …] label

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-29--an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room--label)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | PAID, one Turn. Target Room 01a0a3bc712478eba33089bda004d837 (`Jarvis, echo, alpha`, members `You, Jarvis, echo, alpha`) chosen because Friday was not in it. `@Jarvis bring Friday into this room` -> Jarvis replied `Friday is now in this room.` The h1 and the sidebar link BOTH renamed to `Jarvis, echo, alpha, Friday` with no reload, and the members line became `You, Jarvis, echo, alpha, Friday` - still beginning with `You`. THE DOCUMENTED SILENT FAILURE DID NOT OCCUR: T-A printed `Invited agent 'Friday' (01a0a3badaf774e49e4d0c8364a4487a) into room '01a0a3bc712478eba33089bda004d837'.` naming EXACTLY the room id recorded beforehand, so the model passed the right id it could only have learned from its own [Room: ...] label. Friday is genuinely wired into the fan-out: a second Turn, `@Friday say hello`, drew `Hello! Friday here, ready to help.` from Friday. |

### INVITEROOMS-30 — An Agent can invite into a Room it is not a Member of

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-30--an-agent-can-invite-into-a-room-it-is-not-a-member-of)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | PAID, one Turn. TARGET = 01a0a3bdccc67a53afe226d8328bd26f (`echo, alpha`, members `You, echo, alpha`), a Room Jarvis is NOT in. Typing from a different Room (`Jarvis, echo, alpha, Friday`), `@Jarvis invite Friday into the room whose id is 01a0a3bdccc67a53afe226d8328bd26f` -> Jarvis replied `Friday is now in that room.` TARGET's sidebar link renamed live to `echo, alpha, Friday` with no reload. T-A printed `Invited agent 'Friday' (...) into room '01a0a3bdccc67a53afe226d8328bd26f'.` - the right Room, not a different one. TARGET's members in rowid order are `You`, `echo`, `alpha`, `Friday`: it gained Friday, still begins with `You`, and did NOT gain `Jarvis` - the caller did not add itself. The Room being typed in was unchanged (`Jarvis, echo, alpha, Friday`, same members). Zero unhandled exceptions in the log. The cross-Room invite itself is the DOCUMENTED known limit and is not filed; the two genuine failure modes (a crash, and the wrong Room renaming) both did not occur. |

### INVITEROOMS-31 — Every Agent-created Room contains the Human, so none is hidden

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-31--every-agent-created-room-contains-the-human-so-none-is-hidden)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | No extra Turns needed - rode along with INVITEROOMS-28 and 30. The Agent-created Room from -28 (01a0a3bb65d674b093f8af91707caeb0, created by `mcp__team__create_room`) has a members line reading `You, Jarvis, Friday` - it BEGINS with `You`. Its Transcript is readable: the context Message Jarvis posted is visible in full. Posting `hello` into it as the Human SUCCEEDED - the message appeared with sender `You` and the composer showed NO error, so no NotMember refusal; the Human can both read and post. ADR-0005's structural-auditability property holds on the Agent-driven path: no Room the Agents made is one the Human cannot reach. |

### INVITEROOMS-32 — The composer's own status line does not survive a Room switch

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-32--the-composers-own-status-line-does-not-survive-a-room-switch)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-33 — A renamed Room keeps its name across an Invitation; an un-renamed one still re-derives

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-33--a-renamed-room-keeps-its-name-across-an-invitation-an-un-renamed-one-still-re-derives)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-34 — Archiving a Room from its row menu removes it from the sidebar and lists it under Archived chats

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-34--archiving-a-room-from-its-row-menu-removes-it-from-the-sidebar-and-lists-it-under-archived-chats)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-35 — Unarchiving returns the Room to the sidebar with its Transcript intact

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-35--unarchiving-returns-the-room-to-the-sidebar-with-its-transcript-intact)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-36 — Deleting a Room needs a confirmation and removes its Transcript file from disk

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-36--deleting-a-room-needs-a-confirmation-and-removes-its-transcript-file-from-disk)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-37 — Archiving or deleting the Room you are viewing moves you somewhere valid

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-37--archiving-or-deleting-the-room-you-are-viewing-moves-you-somewhere-valid)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-38 — An archived two-Member Room is not reused: starting a chat with that Agent mints a second one

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-38--an-archived-two-member-room-is-not-reused-starting-a-chat-with-that-agent-mints-a-second-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INVITEROOMS-39 — Archived and deleted Rooms survive a restart

File: [`docs/engineering/manual-tests/invite-rooms.md`](invite-rooms.md#inviterooms-39--archived-and-deleted-rooms-survive-a-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Settings: the 24 Prompts, editing, per-field reset and Save

### PROMPTSSETTINGS-01 — /settings opens on the Profile tab with a seven-tab rail

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-01--settings-opens-on-the-profile-tab-with-a-seven-tab-rail)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-02 — Tab clicks change the URL, and an unknown tab segment falls back to Profile instead of 404ing

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-02--tab-clicks-change-the-url-and-an-unknown-tab-segment-falls-back-to-profile-instead-of-404ing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-03 — The Prompts tab prints the real absolute path of prompts.json, and says the file's absence is expected

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-03--the-prompts-tab-prints-the-real-absolute-path-of-promptsjson-and-says-the-files-absence-is-expected)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-04 — Every prompt is reachable, in six named groups, in a fixed order

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-04--every-prompt-is-reachable-in-six-named-groups-in-a-fixed-order)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-05 — The "Next session" badge appears on exactly the prompts whose edits cannot reach a running agent

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-05--the-next-session-badge-appears-on-exactly-the-prompts-whose-edits-cannot-reach-a-running-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-06 — Placeholder chips are listed on exactly the prompts that take placeholders, with full braces

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-06--placeholder-chips-are-listed-on-exactly-the-prompts-that-take-placeholders-with-full-braces)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-07 — No prompt's DEFAULT text contains the literal string mcp__team__

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-07--no-prompts-default-text-contains-the-literal-string-mcp__team__)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-08 — Textarea height tracks the line count and clamps at 14 rows

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-08--textarea-height-tracks-the-line-count-and-clamps-at-14-rows)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-09 — Typing raises "Modified" and "Unsaved" together and enables Save, Reset and Reset all — while writing nothing

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-09--typing-raises-modified-and-unsaved-together-and-enables-save-reset-and-reset-all--while-writing-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-10 — "Modified" and "Unsaved" are independent flags — walk all three combinations

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-10--modified-and-unsaved-are-independent-flags--walk-all-three-combinations)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-11 — Per-field "Reset" stages the shipped default without writing anything to disk

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-11--per-field-reset-stages-the-shipped-default-without-writing-anything-to-disk)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-12 — prompts.json does not exist until the first Save, and then holds ONLY the keys you changed

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-12--promptsjson-does-not-exist-until-the-first-save-and-then-holds-only-the-keys-you-changed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-13 — The saved file is human-readable: indented, with literal em-dashes and angle brackets, not \uXXXX escapes

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-13--the-saved-file-is-human-readable-indented-with-literal-em-dashes-and-angle-brackets-not-uxxxx-escapes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-14 — Saving a field back to its default removes the key rather than storing a redundant copy

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-14--saving-a-field-back-to-its-default-removes-the-key-rather-than-storing-a-redundant-copy)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-15 — Save is the ONLY writer — and a successful Save gives no confirmation

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-15--save-is-the-only-writer--and-a-successful-save-gives-no-confirmation)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-16 — "Reset all to defaults" is disabled until something is modified and guards itself with an inline confirm

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-16--reset-all-to-defaults-is-disabled-until-something-is-modified-and-guards-itself-with-an-inline-confirm)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-17 — "Reset all" stages every default but writes nothing — and the reset is silently lost if you navigate away

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-17--reset-all-stages-every-default-but-writes-nothing--and-the-reset-is-silently-lost-if-you-navigate-away)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-18 — "Reset all" followed by Save leaves prompts.json present and empty ({}), never deleted

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-18--reset-all-followed-by-save-leaves-promptsjson-present-and-empty--never-deleted)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-19 — A failed Save shows a red "Could not save:" line under the heading and keeps every pending edit

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-19--a-failed-save-shows-a-red-could-not-save-line-under-the-heading-and-keeps-every-pending-edit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-20 — Validation reports and never refuses: an empty field raises an Error and still saves

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-20--validation-reports-and-never-refuses-an-empty-field-raises-an-error-and-still-saves)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-21 — Removing a required placeholder raises an Error naming that placeholder — and an optional one does not

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-21--removing-a-required-placeholder-raises-an-error-naming-that-placeholder--and-an-optional-one-does-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-22 — An unrecognised {{token}} raises a Warning; a malformed one raises nothing at all

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-22--an-unrecognised-token-raises-a-warning-a-malformed-one-raises-nothing-at-all)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-23 — Validation runs live against the pending value and clears without saving

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-23--validation-runs-live-against-the-pending-value-and-clears-without-saving)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-24 — Prompt text is rendered as text, never as markup

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-24--prompt-text-is-rendered-as-text-never-as-markup)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-25 — Typing in a long prompt field stays responsive and loses no characters

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-25--typing-in-a-long-prompt-field-stays-responsive-and-loses-no-characters)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-26 — Uncommitted edits survive a tab switch and a theme change, but are silently discarded by reload or leaving the page

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-26--uncommitted-edits-survive-a-tab-switch-and-a-theme-change-but-are-silently-discarded-by-reload-or-leaving-the-page)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-27 — "Reset all to defaults" is also rendered on the Appearance tab, where it acts on Prompts

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-27--reset-all-to-defaults-is-also-rendered-on-the-appearance-tab-where-it-acts-on-prompts)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-28 — A Save in one browser tab repaints /settings open in another, without eating that tab's typing

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-28--a-save-in-one-browser-tab-repaints-settings-open-in-another-without-eating-that-tabs-typing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-29 — A hand-edit to prompts.json reaches the open page within about a second, with no restart and no refresh

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-29--a-hand-edit-to-promptsjson-reaches-the-open-page-within-about-a-second-with-no-restart-and-no-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-30 — Deleting prompts.json while the app runs reverts every field to its shipped default, live

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-30--deleting-promptsjson-while-the-app-runs-reverts-every-field-to-its-shipped-default-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-31 — SILENT: a malformed prompts.json edited while running changes nothing on screen and says nothing — check the log

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-31--silent-a-malformed-promptsjson-edited-while-running-changes-nothing-on-screen-and-says-nothing--check-the-log)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-32 — SILENT: a malformed prompts.json at STARTUP falls back to defaults wholesale, with no UI clue and the file left intact

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-32--silent-a-malformed-promptsjson-at-startup-falls-back-to-defaults-wholesale-with-no-ui-clue-and-the-file-left-intact)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-33 — An unknown key in prompts.json is kept forever, ignored for resolution, and logged whenever the file is read or saved

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-33--an-unknown-key-in-promptsjson-is-kept-forever-ignored-for-resolution-and-logged-whenever-the-file-is-read-or-saved)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-34 — A pending browser edit beats a concurrent hand-edit to the same key, and Save merges rather than overwrites

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-34--a-pending-browser-edit-beats-a-concurrent-hand-edit-to-the-same-key-and-save-merges-rather-than-overwrites)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-35 — A stored prompt equal to its default apart from CRLF is ignored (no badge) and dropped by the next Save

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-35--a-stored-prompt-equal-to-its-default-apart-from-crlf-is-ignored-no-badge-and-dropped-by-the-next-save)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-36 — Loading /settings never starts an adapter (node) process

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-36--loading-settings-never-starts-an-adapter-node-process)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-37 — Saving a Prompt must NOT restart any agent's session

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-37--saving-a-prompt-must-not-restart-any-agents-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-38 — COSTS MONEY: a Live prompt edit reaches the very next Turn with no restart

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-38--costs-money-a-live-prompt-edit-reaches-the-very-next-turn-with-no-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-39 — COSTS MONEY: a "Next session" prompt edit is silently inert on a running agent until it restarts

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-39--costs-money-a-next-session-prompt-edit-is-silently-inert-on-a-running-agent-until-it-restarts)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-40 — COSTS MONEY: get_help re-renders on every call, so its Get help prompts land on the next call — while a tool DESCRIPTION does not

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-40--costs-money-get_help-re-renders-on-every-call-so-its-get-help-prompts-land-on-the-next-call--while-a-tool-description-does-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTSSETTINGS-41 — The Library tab opens its own pane and does not fall back to Prompts

File: [`docs/engineering/manual-tests/prompts-settings.md`](prompts-settings.md#promptssettings-41--the-library-tab-opens-its-own-pane-and-does-not-fall-back-to-prompts)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Appearance tab, Themes, Tokens and overrides

### APPEARANCETHEME-01 — The Appearance tab is on the Settings tab bar and is reachable by its own route

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-01--the-appearance-tab-is-on-the-settings-tab-bar-and-is-reachable-by-its-own-route)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | /settings shows h1 `Settings` and a rail of EXACTLY two buttons, `Hooks` then `Appearance`, with `Hooks` carrying `settings-tab-active` and the Hooks panel rendered. Clicking `Appearance` changed the address bar to http://localhost:5100/settings/appearance and moved the active class to `Appearance` - it navigates rather than flipping local state, so the tab is bookmarkable. Typing /settings/appearance directly lands on the same panel with `Appearance` active. /settings/nonsense renders the full, styled HOOKS panel (4530 characters of main content, Hooks intro present, Hooks marked active) - not blank, not a 404, not an error - so the fallback branch is intact, and the same holds for /settings with no segment. Zero exception/fail lines in T-A across all four navigations (log still 14 lines). #blazor-error-ui display:none throughout. |

### APPEARANCETHEME-02 — The Appearance tab shows its own prose and the real absolute selection-file path, and none of the Prompts tab's prose

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-02--the-appearance-tab-shows-its-own-prose-and-the-real-absolute-selection-file-path-and-none-of-the-prompts-tabs-prose)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Appearance panel's first paragraph matches word for word: `Pick a theme, or leave it on System to follow your device's own light or dark setting.` The paragraph below the Theme dropdown matches word for word too, including the trailing sentence `The file does not exist until you save a theme choice here, so it being absent is expected, not a bug.` The three monospace <code> elements read exactly `E:\Repos\Huddle\src\Huddle.App\App_Data\appearance.json`, `--font-chat` and `--surface-base` - no leading `@`, no doubled `@@`, no stray escape. The path is ABSOLUTE and ends in \src\Huddle.App\App_Data\appearance.json, and it matches the machine's real data directory, so it is the authority for tests 10-21. The Appearance panel contains NEITHER the Hooks intro sentence (`A hook is one piece of wording this application sends to a model`) NOR any mention of `hooks.json` - both checks returned false - while the Hooks panel does carry the Hooks intro. The shipped regression (shared paragraphs above the rail) has not returned. |

### APPEARANCETHEME-03 — The Theme picker offers the whole built-in catalog, grouped, and is the tab's only control

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-03--the-theme-picker-offers-the-whole-built-in-catalog-grouped-and-is-the-tabs-only-control)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  | The Theme control is a <select> holding EXACTLY three options in this order: `System` (value ''), `Light` (value `huddle-light`), `Dark` (value `huddle-dark`). No fourth option and no blank extra row. The visible labels are the friendly names `System`/`Light`/`Dark`, not the raw ids `huddle-light`/`huddle-dark` - the ids live only in the option values, so nothing leaks into the display. |

### APPEARANCETHEME-04 — Choosing a Theme stores its id and applies immediately, with no page reload

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-04--choosing-a-theme-stores-its-id-and-applies-immediately-with-no-page-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  | Selecting `Dark` forced a REAL full document load, proved by a JS marker set immediately before the change being gone afterwards (window.__marker survived=false) while performance navigation type read `navigate`. After the load the URL is still /settings/appearance with `Appearance` carrying settings-tab-active and the dropdown showing value `huddle-dark` / label `Dark`. The application is now dark - body background rgb(27,27,31) - and <head> gained a fourth stylesheet `themes/huddle-dark.css`. The file was written and reads EXACTLY, across three lines:\n{\n  "theme": "huddle-dark"\n}\n30 bytes. The stored value is the ID `huddle-dark`, NOT the label `Dark`; the file is indented rather than one line and contains no \\uXXXX escapes. The full page load is BY DESIGN and is not filed. |

### APPEARANCETHEME-06 — The saved Theme is shown as selected in the picker after a full load

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-06--the-saved-theme-is-shown-as-selected-in-the-picker-after-a-full-load)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All three states checked against the RAW server HTML, so the `selected` attribute read is the PRERENDERED value - which also means the dropdown never transiently shows `System` before settling, since the correct option arrives already marked selected in the first byte of HTML. `{ "theme": "huddle-dark" }` -> head carries themes/huddle-dark.css and the selected option value is `huddle-dark` (Dark), page dark. `{ "theme": "huddle-light" }` -> head carries themes/huddle-light.css and the selected option is `huddle-light` (Light). No file at all -> ZERO themes/ links and the selected option value is the empty string (System). File value, dropdown selection and the <head> link agree in every one of the three cases. |

### APPEARANCETHEME-07 — A dark Theme shows no flash of a light palette on a hard reload, ever

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-07--a-dark-theme-shows-no-flash-of-a-light-palette-on-a-hard-reload-ever)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Judged structurally rather than by eye, because this harness cannot capture a first-paint filmstrip - and the structural oracle is exactly what the Fail-if names. Both mechanisms that would cause a flash are ruled out. (1) THE LINK IS EMITTED AND EARLY: with `{ "theme": "huddle-dark" }`, the raw server HTML for /, /agents and /settings/appearance all carry `themes/huddle-dark.css` inside <head>, and on every one of the three routes NO <script> tag precedes it - so it is a render-blocking stylesheet applied before first paint by definition. (2) IT IS NOT 404ing: APPEARANCETHEME-05 confirmed themes/huddle-dark.css returns 200. (3) NO CLIENT-SIDE LOADER EXISTS: grepping every .js under wwwroot for `localStorage`, `prefers-color-scheme`, `color-scheme` or `theme` returned ZERO hits, so nothing applies the theme after paint - the signature of the flash this test hunts. Recorded honestly: the frame-by-frame visual confirmation and the Slow-4G throttled reload were not performed. |

### APPEARANCETHEME-09 — The choice is per installation, not per browser

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-09--the-choice-is-per-installation-not-per-browser)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | The strongest available evidence that the choice is per INSTALLATION: a completely stateless client - Invoke-WebRequest, which has no cookies, no localStorage and no profile at all - fetched / and got `themes/huddle-dark.css` in <head>. Browser storage is empty in every sense: localStorage has 0 keys, sessionStorage 0 keys, document.cookie is the empty string - nothing about themes, appearance or colours anywhere. A brand-new browser tab (fresh circuit, no prior state) rendered dark immediately with the themes/huddle-dark.css link and body rgb(27,27,31), with no setup. Clearing localStorage + sessionStorage + all cookies and reloading left the app DARK (rgb(27,27,31)) with storage still empty. Stopping and restarting the application left it dark - the stateless fetch after restart still carried the themes link. SUBSTITUTION NOTE: a second physically different browser (Edge/Firefox) and a real Incognito window were not available in this harness; the stateless HTTP client plus the empty-storage evidence cover the same claim more directly, since there is no client-side store for a per-browser choice to live in. |

### APPEARANCETHEME-14 — An unknown theme id is a warning, logged on every rebuild and save, never shown on the tab, and the file keeps saying it

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-14--an-unknown-theme-id-is-a-warning-logged-on-every-rebuild-and-save-never-shown-on-the-tab-and-the-file-keeps-saying-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | All seven bad theme values handled as warnings, never failures: `dracula`, `Huddle-Dark` (uppercase), `huddle dark` (space), `../../etc/passwd` (traversal), `""`, `42` (number) and `null`. In EVERY case the app rendered normally (full page served), <head> carried ZERO `themes/` links, the Appearance dropdown's selected option value was the empty string (System), and the file was left BYTE-IDENTICAL. THE TRAVERSAL CASE IS SAFE: no href anywhere in the document contains `..` - checked with a regex over every href attribute - so the traversal-shaped id never became a URL. The console carries `Appearance file '<path>' selects theme '<value>', which is not a known theme; the built-in theme is used instead and the file is left unchanged.` naming dracula, Huddle-Dark, `huddle dark`, ../../etc/passwd, the empty string and 42. `null` alone logs nothing, which is correct rather than a gap - JSON null is the absence of a choice, the same state System writes (see APPEARANCETHEME-17, where choosing System REMOVES the key), so there is no bad value to report. |

### APPEARANCETHEME-15 — Malformed JSON keeps the last good Theme while the app runs, falls back wholesale at the next start, is logged, and the file is left untouched

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-15--malformed-json-keeps-the-last-good-theme-while-the-app-runs-falls-back-wholesale-at-the-next-start-is-logged-and-the-file-is-left-untouched)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | TRUNCATED JSON (`{"theme":"huddle-dark",`): the app kept running and served a normal page with ZERO themes/ links and NO <style> element - theme and override both gone, falling back to the built-in pair. The Appearance tab showed NO `Overrides that didn't load` section (correct - a wholesale parse failure is not a per-entry rejection) and the dropdown read System. The file is byte-identical to the broken text written. TOP-LEVEL ARRAY (`[]`): identical behaviour, file still `[]`. The console carries the watcher-triggered variant of the expected warning: `Could not parse appearance file '<path>' after a filesystem change, even after retrying; keeping the previously resolved appearance rather than reverting to no theme and no overrides over what may be a mid-write race.` - which the test explicitly allows as the alternative form. Zero unhandled exceptions. MID-WRITE RACE: ten rapid saves of a VALID file over ~5 seconds, sampling the served <head> after each one, produced 10/10 samples still carrying BOTH `themes/huddle-dark.css` and `--accent: #c14bd0` - no flicker back to default on any sample, so the retry is working. |

### APPEARANCETHEME-16 — Saving a choice while the file is malformed replaces its contents with valid JSON

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-16--saving-a-choice-while-the-file-is-malformed-replaces-its-contents-with-valid-json)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | BEFORE (malformed, with an override inside): `{ "theme": "huddle-light", "overrides": { "--accent": "#c14bd0" }` - missing the closing brace. Selecting `Dark` over that file did NOT throw: no error banner (#blazor-error-ui display:none) and zero unhandled exceptions in the log. AFTER, the file is VALID, indented JSON reading exactly {\n  "theme": "huddle-dark"\n} - it round-trips through ConvertFrom-Json. Both the broken text (`huddle-light` fragment) and the `--accent` override that was inside it are GONE, which is the documented consequence of saving over an unparseable file. The application is dark afterwards (body rgb(27,27,31)) and the dropdown reads huddle-dark. |

### APPEARANCETHEME-17 — Save re-reads the file under its write lock, so an unrelated hand-added key survives — and so does the retired `dark` key

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-17--save-re-reads-the-file-under-its-write-lock-so-an-unrelated-hand-added-key-survives--and-so-does-the-retired-dark-key)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Starting file had `theme: huddle-dark`, an unknown top-level key `note: "keep me"`, and `overrides: { --accent: #c14bd0 }`; the dropdown read huddle-dark and the accent was applied. AFTER CHOOSING SYSTEM the file reads {\n  "note": "keep me",\n  "overrides": {\n    "--accent": "#c14bd0"\n  }\n} - the `theme` key is GONE ENTIRELY, not present as null and not as an empty string; `note` survives with its value; `overrides` survives. The magenta accent is still applied (`--accent: #c14bd0` still emitted) and there is no themes/ link. AFTER THEN CHOOSING LIGHT the file reads {\n  "note": "keep me",\n  "overrides": {\n    "--accent": "#c14bd0"\n  },\n  "theme": "huddle-light"\n} - theme added back while note and overrides are untouched. The file stayed indented and human-readable throughout with no \\uXXXX escapes, so the writer is using the relaxed options. Nothing was clobbered down to just the theme key. |

### APPEARANCETHEME-18 — A hand-edit updates the open tab live, and now the page's colours change too — no full load required

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-18--a-hand-edit-updates-the-open-tab-live-and-now-the-pages-colours-change-too--no-full-load-required)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | THE TAB UPDATES LIVE, THE COLOURS DO NOT. Starting light with the dropdown on huddle-light: hand-editing the file to huddle-dark WITHOUT touching the browser flipped the dropdown to `huddle-dark` by itself (detected on the first 50ms poll, so well inside half a second) while the page's colours stayed LIGHT at rgb(255,255,255) - and the JS marker survived with navigation entries still 1, proving no reload. Clicking `Agents` in the sidebar (in-app navigation) still left the colours light at rgb(255,255,255) with the marker alive. Only a full document load turned it dark: body rgb(27,27,31) and the marker gone. THE REJECTED SECTION IS ALSO LIVE: hand-adding `"overrides": { "nope": "x" }` made an `Overrides that didn't load` section appear BY ITSELF reading `'nope' is not a theme token; it was left in the file and ignored.`, again with no reload (marker alive, nav 1); replacing it with a valid `--accent` made the section disappear by itself, still with no colour change; and only after a full load did the magenta apply (button background rgb(193,75,208)). Zero unhandled exceptions. |

### APPEARANCETHEME-20 — Two browser windows stay in step, colours included — the change propagates through the file to every open circuit

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-20--two-browser-windows-stay-in-step-colours-included--the-change-propagates-through-the-file-to-every-open-circuit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Two windows on /settings/appearance, both starting on huddle-dark with body rgb(27,27,31). Window A selecting `Light` reloaded A (its JS marker was gone) and turned A light at rgb(255,255,255). Window B, UNTOUCHED, flipped its dropdown to `huddle-light` BY ITSELF - detected on the first 50ms poll - while B's COLOURS correctly stayed dark at rgb(27,27,31) with B's marker alive and navigation entries still 1. B only turned light after its own full load. THE REVERSE DIRECTION WORKS TOO: B then selecting `Dark` flipped A's dropdown to huddle-dark by itself, again with A's colours unchanged. CLOSING WINDOW B logged NOTHING - the app log stayed at 82 lines across a 12-second watch with zero Unhandled/ObjectDisposed/InvalidOperation exceptions, so the subscription to the singleton store is released on disposal rather than leaking. Window A kept working afterwards: selecting Dark again reloaded it to rgb(27,27,31) with #blazor-error-ui display:none. |

### APPEARANCETHEME-21 — Re-selecting the value that is already selected is harmless and produces no reload

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-21--re-selecting-the-value-that-is-already-selected-is-harmless-and-produces-no-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | With `{ "theme": "huddle-dark", "overrides": { "--accent": "#c14bd0" } }` in place, re-selecting the ALREADY-SELECTED `Dark` was harmless: over a 6-second watch the navigation entry count stayed at 1 (no reload at all, which the test accepts as one of the two valid outcomes - and definitively NO reload loop), the dropdown still read huddle-dark, the page stayed dark at rgb(27,27,31), and #blazor-error-ui stayed display:none. THE OVERRIDE SURVIVED: the file still reads the theme AND the `--accent` block, and /agents still emits `--accent: #c14bd0`, so the no-op save edited only the theme key rather than clobbering the file. Zero unhandled exceptions in the log. |

### APPEARANCETHEME-22 — Dark mode walked across every page and every state - the acceptance test for the whole item

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-22--dark-mode-walked-across-every-page-and-every-state---the-acceptance-test-for-the-whole-item)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `af564cca` |  | Acceptance walk with `{ "theme": "huddle-dark" }` (no overrides, so this judges the built-in dark palette) against a LIGHT OS. Method: rather than eyeballing, every element on each page was scanned programmatically and any element whose computed background-color averages >140 per channel at >=0.2 alpha AND has a client rect was reported. THE KEY STRUCTURAL FACT: `color-scheme` computes to `dark` on :root, which is what makes the browser paint dark scrollbars and dark native <select> popups. RESULTS - Room: body and html rgb(27,27,31), sidebar rgb(35,35,40), New chat button rgb(42,42,49), composer textarea rgb(59,59,59), chat header/message body/message sender all on the dark foreground rgb(230,230,234). New chat panel open: transparent ground on dark, status dots rgb(63,191,90) online and rgb(224,90,90) offline, Start chat rgb(53,53,61). /agents: ZERO visible light surfaces; tile rgb(42,42,49), avatar rgb(94,43,96), New agent button rgb(94,43,96), and the `Files that didn't load` block rgb(58,31,31) on rgb(230,230,234). Agent card: scrim rgba(0,0,0,0.6) - dark, not a light scrim - panel rgb(42,42,49), Persona block rgb(23,23,27); ZERO visible light surfaces. /settings/hooks: zero visible light surfaces, textarea rgb(23,23,27). /settings/appearance: zero visible light surfaces, select rgb(59,59,59). NOTHING rendered blank or with no colour. No pale yellow error banner on any page. THE ONLY TWO LIGHT ELEMENTS IN THE WHOLE DOM ARE BOTH HIDDEN AND BOTH EXPECTED: #blazor-error-ui at lightyellow rgb(255,255,224) - documented exemption (1), deliberately a literal so it stays readable when the app has already failed - and #components-reconnect-modal at white; both compute display:none with zero client rects, so neither APPEARS. Documented exemption (2) also confirmed present rather than treated as a defect: the card's Persona markdown keeps the inherited sans-serif stack rather than the monospace token. NOT OBSERVED, recorded honestly: the genuinely visual sub-checks this harness cannot make - the native <select> POPUP menu being dark, and scrollbar rendering - are covered only indirectly, by color-scheme:dark reaching :root. |

### APPEARANCETHEME-23 — Hover, selection and focus surfaces all switch, and the four old hover colours are still one

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-23--hover-selection-and-focus-surfaces-all-switch-and-the-four-old-hover-colours-are-still-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `af564cca` | #25 | TWO OF THREE PASS CONDITIONS HOLD; THE FOCUS ONE DOES NOT. (1) HOVER IS ONE TOKEN: enumerating every :hover rule that sets a background across all loaded stylesheets, EVERY app-authored one reads a token, never a literal - .room-list li a:hover, .invite-agent:hover and .agent-tile:hover (the three this test names) all read the SAME `var(--surface-hover)`, as do .new-chat-toggle, .sidebar-link, .invite-toggle, .draft-stop, .agent-profile-card-action and .settings-tab. That token resolves to rgb(50,50,59) in dark - a dark value, and none of the four pale literals the Fail-if lists. The only non-token hover backgrounds are a transparent rgba(0,0,0,0) in third-party MudBlazor.min.css and a ::-webkit-scrollbar-thumb:hover grey; neither is a row highlight. (2) SELECTED IS DISTINCT AND DARK: --surface-selected resolves to rgb(47,61,87) and the active room row computes exactly that - dark, and different from the hover value. (3) FOCUS OUTLINE IS NOT VISIBLE - THIS IS THE FAILURE. After a REAL keyboard Tab, the focused element matches :focus-visible yet computes `outline-style: none`, so no ring is drawn: anchors and buttons both. Cause located precisely: `a:focus-visible { outline: none }` in the THIRD-PARTY MudBlazor.min.css; the app's own app.css and theme.css contain zero `outline` and zero `:focus` rules. The composer textarea is the exception and does get a visible ring (outline-style `auto`, rgb(238,238,238) against the rgb(27,27,31) ground). THE CAUSE IS NOT THE ONE THE FAIL-IF PREDICTS: it is not a colour that fails to switch - the outline COLOUR resolves to a high-contrast light value in dark. It is the outline being suppressed outright, and it is THEME-INDEPENDENT: re-checked under huddle-light on a white ground, links and buttons still compute outline-style `none`. So this is an accessibility defect in the shell rather than a tokenisation regression, which is why it is filed separately rather than against the theme layer. |

### APPEARANCETHEME-24 — The reconnect modal shows one themed state paragraph over a dimmed backdrop, in both light and dark

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-24--the-reconnect-modal-shows-one-themed-state-paragraph-over-a-dimmed-backdrop-in-both-light-and-dark)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `af564cca` | #26, #16 | TWO OF THE PASS CONDITIONS HOLD, TWO DO NOT. HOLDS (1) THE ASSET-KEY TRAP IS ABSENT: the scoped bundle Huddle.App.j8c8gsv1bg.styles.css is linked and all five CSS requests return 200, so the display:none/display:block state machine is loaded and the six state paragraphs are NOT all showing at once. HOLDS (2) THE BACKDROP IS THEMED AND DIMMED: ::backdrop reads var(--scrim) and resolves to the exact values the test names - rgba(0,0,0,0.6) under huddle-dark and rgba(0,0,0,0.4) under huddle-light - so the dimming really is per-theme, not a hardcoded scrim. FAILS (1) EXACTLY ONE STATE PARAGRAPH: at first drop the modal is `components-reconnect-show` with ONE visible paragraph `Rejoining the server...` plus the animation - correct - but after ~9s it becomes `components-reconnect-show components-reconnect-retrying` and TWO paragraphs are visible simultaneously, `Rejoining the server...` AND `Rejoin failed... trying again in N seconds.`, because the retrying class is additive and never clears the show-state rule. Reproduced in BOTH themes (dark at 4s remaining, light at 30s remaining). This is the already-filed #16 / SHELLNAV-26 defect, linked rather than duplicated. FAILS (2) THE PANEL IS A THEMED RAISED SURFACE - NEW, filed as #26: under huddle-dark the dialog computes background-color rgb(255,255,255) while its text computes rgb(230,230,234), i.e. near-white on white, about 1.06:1 contrast - the state text is unreadable precisely when it matters. CAUSE LOCATED EXACTLY: the app's scoped rule #components-reconnect-modal[b-3dgaftw3nf] sets background-color: var(--surface-raised), but _content/MudBlazor/MudBlazor.min.css ships `#components-reconnect-modal{z-index:9999 !important;background-color:var(--mud-palette-background) !important}` and !important outranks the higher specificity; --mud-palette-background computes rgba(255,255,255,1) because MudBlazor is never switched to its dark palette. It is NOT a token or light-dark() failure: `color: var(--text-primary)` from the SAME rule block wins and resolves to the dark branch, border-radius 8px and the box-shadow from that block apply, and a probe div with color-scheme:dark resolves light-dark(#ffffff,#2a2a31) to rgb(42,42,49) correctly. The light theme MASKS this completely, since --surface-raised's light branch is #ffffff anyway. Retry button rgb(94,43,96) on rgb(255,255,255) survives because MudBlazor restyles the button separately. |

### APPEARANCETHEME-26 — The credit line names this application's own Themes, and attributes nobody

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-26--the-credit-line-names-this-applications-own-themes-and-attributes-nobody)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-27 — Every Theme paints its own palette, across the whole application

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-27--every-theme-paints-its-own-palette-across-the-whole-application)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-28 — An accent customization is stored per Theme, clamped for contrast, and removed by Reset

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-28--an-accent-customization-is-stored-per-theme-clamped-for-contrast-and-removed-by-reset)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-29 — There is no Library pane setting, and a leftover `libraryPaneSide` key is ignored without a warning and kept in the file

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-29--there-is-no-library-pane-setting-and-a-leftover-librarypaneside-key-is-ignored-without-a-warning-and-kept-in-the-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-30 — The top-bar button switches between the chosen light and dark Themes, and is the last control in the bar

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-30--the-top-bar-button-switches-between-the-chosen-light-and-dark-themes-and-is-the-last-control-in-the-bar)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-31 — The Appearance tab has a Light picker and a Dark picker, each holding only its own mode

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-31--the-appearance-tab-has-a-light-picker-and-a-dark-picker-each-holding-only-its-own-mode)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-32 — Picking a Theme changes the screen only when it is of the mode already shown

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-32--picking-a-theme-changes-the-screen-only-when-it-is-of-the-mode-already-shown)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-33 — Old files, hand edits and bad values: the pair resolves safely

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-33--old-files-hand-edits-and-bad-values-the-pair-resolves-safely)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-34 — A stored Huddle or Custom Accent id migrates to Lumen, with its accent kept

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-34--a-stored-huddle-or-custom-accent-id-migrates-to-lumen-with-its-accent-kept)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPEARANCETHEME-35 — The Theme lab edits a Theme live in Development and is absent elsewhere

File: [`docs/engineering/manual-tests/appearance-theme.md`](appearance-theme.md#appearancetheme-35--the-theme-lab-edits-a-theme-live-in-development-and-is-absent-elsewhere)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## The named pipe: external agents and the wire

### PIPEEXTERNAL-03 — New chat builds a group Room named after its Agents, and that Room is mention-gated

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-03--new-chat-builds-a-group-room-named-after-its-agents-and-that-room-is-mention-gated)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | THE EMPTY-SELECTION GUARD IS LIVE: with nothing ticked the `Start chat` button's .disabled is TRUE; after ticking ONE box it is false, and stays false with two. The panel lists `echo` and `alpha` each as a checkbox with a coloured dot and a name - both dots read class `agent-dot online` computing rgb(63,191,90), while the seven leftover directory entries (mybot, gamma, Emily Lee, delta, coo, Jarvis, Friday) all read `agent-dot offline` rgb(224,90,90), so the dot really is tracking connection state. Ticking echo + alpha and clicking Start chat navigated to /rooms/01a0a3d4a1847ce39002b1e0eb2b67e7 with heading EXACTLY `echo, alpha` and members line EXACTLY `You, echo, alpha`. The app logged EXACTLY `Created room '01a0a3d4a1847ce39002b1e0eb2b67e7' (echo, alpha) with 3 members.` MENTION GATING HOLDS BOTH WAYS: `hi @echo` produced exactly ONE new Message, sender `echo`, body <p><strong>echo:</strong> hi echo</p> - the Room went from 0 rows to 2 (the human's and echo's), so `alpha` posted nothing even though it is a Member and received the envelope. The bare `hi` with no @ produced NOTHING over a full 11 seconds: the Room stayed at 3 rows, the third being the human's own `hi`. A Room named `echo, alpha` already existed from earlier work and a new second one was minted, which the test states is expected for a multi-agent New chat. |

### PIPEEXTERNAL-04 — A Room appears in the sidebar the moment an external agent says hello, with no refresh

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-04--a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `a5bfb1a7` |  |  |

### PIPEEXTERNAL-05 — The welcome envelope: shape, fields and version 3

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-05--the-welcome-envelope-shape-fields-and-version-3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | Run as `mybot2` (the digit convention, see PIPEEXTERNAL-04); every `mybot` below means that. The welcome line was parsed rather than eyeballed. STARTS `{"type":"welcome",` - true by StartsWith. ENDS `,"version":3}` - true by EndsWith, so the strict version is 3, NOT the stale 2 that docs/AgencyTeam.md's sample shows (the NOTE's warning; the doc is wrong, the app is right). agentId = 01a0a3d524087d4b90b5cb4aae7b5296, LENGTH EXACTLY 32 and matching ^[0-9a-f]{32}$ - so lowercase hex, no dashes, no uppercase. name = `mybot2`, exactly the -Name argument. rooms holds EXACTLY ONE entry, name `mybot2`, id 01a0a3d5240e7a92821c1af407642ae0 - WHICH IS THE ID IN THE BROWSER ADDRESS BAR with that Room open (/rooms/01a0a3d5240e7a92821c1af407642ae0), so the two sides agree rather than merely looking similar. Its members array holds EXACTLY TWO objects: {id:human, name:You, kind:human} and {id:01a0a3d524087d4b90b5cb4aae7b5296, name:mybot2, kind:agent} - the agent entry's id is the same agentId, and the human entry is present, so a client reading the member count for its reply gate sees 2. NO PROPERTY IS MISSPELLED: the top level is exactly type,agentId,name,rooms,version (so the `type` discriminator IS there and it is not agent_id); a room is exactly id,name,members (not roomName); a member is exactly id,name,kind. |

### PIPEEXTERNAL-07 — An Agent never receives its own Message, and nothing loops

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-07--an-agent-never-receives-its-own-message-and-nothing-loops)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | THE CONFIRM-THE-ABSENCE EVIDENCE WAS TAKEN OVER THE FULL 30 SECONDS THE NOTE DEMANDS, not shortened. Immediately after PIPEEXTERNAL-06's reply the bot console held EXACTLY ONE line beginning {"type":"messagePosted" - and parsing it shows its message.text is `hi @mybot2`, i.e. THE HUMAN'S Message. The bot was never sent its own reply back, so the fan-out is still skipping the sender and the structural echo loop is absent. Over a 31-second watch with nothing typed: the bot console stayed at 4 lines total (3 handshake + 1 messagePosted) and 1 messagePosted; the Room's .jsonl stayed at exactly 2 lines, which is exactly two more than the 0 it held before the exchange, so nothing reached persistence either; and the browser gained no row. The app log contains ZERO lines matching `refused a message`, so the budget guard was never reached - which matters, because a silent runaway would show up there as `its budget of 40 agent messages since a human last spoke is spent.` |

### PIPEEXTERNAL-08 — hello property order does not matter, so connecting is not intermittently refused

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-08--hello-property-order-does-not-matter-so-connecting-is-not-intermittently-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | TEN runs, not the minimum five - the NOTE says ten is better and an intermittent failure is exactly what this test exists to surface. The unmodified shipped tools/echo-bot.ps1 was started and killed ten times in a row under the SAME new name `ordercheck`. ALL TEN printed a first line beginning {"type":"welcome", - runs 1-5 read individually and runs 6-10 counted, welcome=10, other=0. ZERO runs printed {"type":"error","code":"expectedHello", so the JSON options still handle out-of-order metadata and a client whose serialiser does not put `type` first is not refused at random. (The outgoing hello cannot be seen, so the judgement is made only on what came back, as the test instructs.) No run timed out on connect, so no run had to be discarded. The sidebar afterwards holds EXACTLY ONE `ordercheck` entry across all ten connections, and the browser was never refreshed (marker alive). Incidentally the same agentId came back on every run, which is PIPEEXTERNAL-09's property showing up here for free. |

### PIPEEXTERNAL-09 — Reconnecting under the same name keeps the Agent id and creates no second Room

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-09--reconnecting-under-the-same-name-keeps-the-agent-id-and-creates-no-second-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | Run as `mybot2`, whose Room held 2 Messages and 2 Members (never invited into, so the PIPEEXTERNAL-20 confound does not apply). First connection's agentId: 01a0a3d524087d4b90b5cb4aae7b5296. After Ctrl+C and a fresh `pwsh tools/echo-bot.ps1 -Name mybot2`, the NEW welcome line's agentId is 01a0a3d524087d4b90b5cb4aae7b5296 - CHARACTER-FOR-CHARACTER IDENTICAL, so the row is upserted rather than re-inserted. Its rooms array still holds one entry, id 01a0a3d5240e7a92821c1af407642ae0 - the SAME Room, not a fresh one. The sidebar (not refreshed; marker alive) still shows EXACTLY ONE entry reading `mybot2`, and opening it shows both earlier Messages in the original order (`You: hi @mybot2` then `mybot2: **mybot2:** hi mybot2`) - so the Transcript is not keyed on anything that changed across the reconnect. ONLY ONE .jsonl HOLDS THIS CONVERSATION: the rooms directory held 9 .jsonl files before the reconnect and 9 after, so no second file was minted. The app log still contains EXACTLY ONE line matching `for agent 'mybot2'` - counted before (1) and after (1) - so no second `Created direct room` was emitted. |

### PIPEEXTERNAL-10 — Reconnecting with different letter-case is the same Agent, and the stored Name does not change

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-10--reconnecting-with-different-letter-case-is-the-same-agent-and-the-stored-name-does-not-change)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | Reconnected deliberately as `MYBOT2` in upper case; the bot's own console confirms it dialled `as agent 'MYBOT2'`. THE CONNECTION WAS ACCEPTED, not refused, so case handling is still tolerant rather than strict. The welcome line's agentId is 01a0a3d524087d4b90b5cb4aae7b5296 - identical to the existing `mybot2` id, so no second Agent was created and the case-insensitive uniqueness on the Name column is intact. THE APP KEPT ITS OWN SPELLING: the welcome reads "name":"mybot2" in LOWER CASE, not the MYBOT2 that was typed, and the room it lists is also named `mybot2` - so the client cannot label itself with a spelling the UI does not use. The sidebar was not refreshed (marker alive) and still holds exactly one entry matching /^mybot2$/i, spelled `mybot2`; the total Room count is unchanged at 21. The New chat panel lists `mybot2` ONCE in lower case and contains no `MYBOT2` - the full list is echo, alpha, mybot, gamma, Emily Lee, delta, coo, Jarvis, Friday, mybot2, ordercheck, with no near-duplicate pair differing only in case. |

### PIPEEXTERNAL-11 — Disconnecting an external agent changes nothing visible in the Room — the silent case

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-11--disconnecting-an-external-agent-changes-nothing-visible-in-the-room--the-silent-case)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Fail *(earlier script)* | `44be4908` | #27 | FIVE OF THE SIX PASS CONDITIONS HOLD; THE LOG ONE DOES NOT. HOLDS: the page is byte-for-byte unchanged across the disconnect - the sidebar Room list compares equal element-by-element (21 entries, `mybot2` still listed), the heading still reads `mybot2`, the members line still reads `You, mybot2`, and the only thing between the transcript and the message box is the same P.budget-note reading `1 of 40 agent replies since you last spoke.` NO ALERT ANYWHERE: a regex over the whole rendered document HTML finds ZERO occurrences of role="alert", document.querySelectorAll('[role="alert"]') is empty, and no visible element matching .alert/.banner/.badge exists - so the always-on-offline-banner failure this test exists to catch is absent. POSTING IS NOT GATED ON PRESENCE: `hi @mybot2` posted normally, appeared in the transcript, was never answered over 15 seconds, and produced no error strip; the Room's .jsonl ends with that human line and NO agent line after it. THE FAILURE: the app logs NOTHING AT ALL on disconnect. No `Agent connection <id> ended.` at Warning or any level, stderr empty, and the log is 20 lines total across THIRTEEN disconnects this process (ten ordercheck cycles plus three mybot2). Logging config is not the cause - appsettings.Development.json sets Agency.Huddle to Debug. BUT THE TEST'S STATED CONSEQUENCE IS DISPROVEN, which is why this is filed narrowly as an observability gap rather than as the teardown defect: the Fail-if says 'the connection teardown path is not running, and the Agent may still be registered as online' - it IS running, because immediately after the kill the New chat panel reported mybot2 as `agent-dot offline` / title `offline` while echo and alpha stayed online, so gateway.Unregister(this) in the finally executed. Cause located at src/Huddle.App/Pipes/AgentConnection.cs:106 - the Warning sits in `catch (IOException)` only, and a client closing its end cleanly (Ctrl+C or a killed process) ends ReadLoopAsync at end of stream, so RunAsync returns normally and no catch is entered; OperationCanceledException and ObjectDisposedException are deliberately silent. Filed as #27 with the suggestion to log one Information line in the finally beside the Unregister. |

### PIPEEXTERNAL-12 — Where a disconnect IS visible: the agent-dot in New chat and Invite agent

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-12--where-a-disconnect-is-visible-the-agent-dot-in-new-chat-and-invite-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | WHILE CONNECTED: mybot2's dot in the New chat panel is class `agent-dot online`, title `online`, computed rgb(63,191,90) - and echo and alpha read exactly the same, so the three live clients all show green. AFTER Ctrl+C AND REOPENING THE PANEL: mybot2 is `agent-dot offline`, title `offline`, rgb(224,90,90), while echo and alpha are still online in the same render - so liveness is read live and not from a cache or a persisted value. ADD AGENT AGREES, CHECKED IN BOTH STATES: with Team filter on `All teams`, that panel showed mybot2 `agent-dot online`/`online`/rgb(63,191,90) while connected and `agent-dot offline`/`offline`/rgb(224,90,90) after the disconnect, matching New chat each time. COLOUR AND LABEL AGREE IN EVERY CASE - no green dot titled offline and no red dot titled online across all the samples taken. THE KNOWN STALENESS REPRODUCES AS DOCUMENTED, NOT AS A FAIL: with the New chat panel left OPEN, the bot was killed and the dot sampled once a second for 10 seconds - it stayed `agent-dot online` on all ten samples and only flipped after the panel was closed and reopened. The Inconclusive clause states this is expected (these panels do not subscribe to presence changes), so it is recorded rather than filed. ONE WORDING MISMATCH WORTH NAMING, NOT A DEFECT: the test calls the offline dot `grey`, but it renders RED, rgb(224,90,90) - the authoritative part of the same condition (class `agent-dot offline`, title `offline`) matches exactly, and colour and label still agree, so the test's adjective is stale rather than the app being wrong. |

### PIPEEXTERNAL-13 — An offline Agent receives nothing, and nothing is replayed on reconnect

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-13--an-offline-agent-receives-nothing-and-nothing-is-replayed-on-reconnect)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `44be4908` |  | THE NO-QUEUE DECISION HOLDS. With mybot2 disconnected, `hi @mybot2 while you were away` posted normally, appeared in the transcript and sat there unanswered for 10 seconds; the app log gained NO exception (still 20 lines, zero matches for Exception/fail/Unhandled), so delivery skips absent members safely rather than throwing. On reconnect the bot printed its welcome line and then NOTHING for a full 22 seconds - its console holds exactly 3 lines (Connecting, Sent hello, welcome) with zero messagePosted lines, so no queued Message was replayed and it did not answer. THE WELCOME DOES NOT CARRY TRANSCRIPT: a literal String.Contains for `while you were away` over the welcome line is FALSE, and its top-level properties are exactly type,agentId,name,rooms,version - Rooms and Members only, no Transcript, so the handshake payload has not grown. The browser transcript still shows the Message unanswered and gained nothing on reconnect, and the Room's .jsonl reads, in order, `You :: hi @mybot2`, `mybot2 :: **mybot2:** hi mybot2`, `You :: hi @mybot2`, `You :: hi @mybot2 while you were away` - the human line last with no agent line after it, and the earlier Messages still present, so persistence is not coupled to delivery. |

### PIPEEXTERNAL-14 — An invalid Name is refused with invalidName, the connection closes, and no Room appears

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-14--an-invalid-name-is-refused-with-invalidname-the-connection-closes-and-no-room-appears)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b0624137` |  | ALL SIX shapes refused with `invalidName`, each naming the exact Name passed, then `Server closed the connection.`, and the script exited every time: `bad name!`, `my  bot` (doubled space), ` mybot` (leading space), `mybot ` (trailing space), `-bot` (leading hyphen) and `bot.1` (dot). Envelope form, e.g. {"type":"error","code":"invalidName","message":"\u0027bad name!\u0027 is not a valid agent name.","version":3} - the apostrophes arrive as \u0027 rather than raw, which is the same wire-JSON escaping the area's own NOTE says is correct and not a fail. THE CODE IS `invalidName`, NOT `badMessage`, in all six - so a client can tell a bad Name from a bad envelope. THE CONNECTION IS ACTUALLY CLOSED, not merely refused: the script's own Dispose then throws `Cannot access a closed pipe.`, so no rejected client is left holding a pipe instance. NOTHING WAS CREATED: the sidebar held 21 Rooms before the six attempts and 21 after, INCLUDING after a hard F5 refresh, and none of the six spellings appears as a Room; the New chat panel lists exactly echo, alpha, mybot, gamma, Emily Lee, delta, coo, Jarvis, Friday, mybot2, ordercheck, so team.db gained no row for any of them either. (A pre-existing `mybot` Room from an earlier area is NOT one of these - it does not match any of the six spellings exactly.) HARNESS NOTE: the first attempt at this test hit exactly the documented Inconclusive trap - Start-Process -ArgumentList passes its string through the Windows command-line parser, which understands only DOUBLE quotes, so `-Name 'bad name!'` split on the space and the client dialled pipe `\\.\pipe\name!'` as agent `'bad`, timing out without ever reaching the server. That run was discarded and every name re-run through a path that quotes correctly; the results above are from the clean runs. |

### PIPEEXTERNAL-15 — The Human's Name is reserved on the pipe

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-15--the-humans-name-is-reserved-on-the-pipe)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b0624137` |  | Team__HumanName is unset, so the reserved Name is the default `You`. BOTH CASINGS REFUSED, and with the right code: `-Name You` returned {"type":"error","code":"nameReserved","message":"\u0027You\u0027 is reserved.","version":3} and `-Name you` returned the same with \u0027you\u0027 - the message quotes the spelling that was PASSED, while the refusal itself is case-insensitive. That rules out the specific asymmetry this test warns about, where the lower-case squat succeeds because the reservation is case-sensitive but the Name table is not. The code is `nameReserved`, NOT `invalidName`, so a client can tell 'pick a different Name' from 'fix your Name's shape'. Both connections were then closed by the server (`Server closed the connection.` followed by the client's own `Cannot access a closed pipe.`). NOTHING WAS CREATED: the sidebar stayed at 21 Rooms with no entry matching /^you$/i, and the New chat panel lists no `You` or `you` among echo, alpha, mybot, gamma, Emily Lee, delta, coo, Jarvis, Friday, mybot2, ordercheck - so the Human is not offered as a chat partner. |

### PIPEEXTERNAL-16 — A Name with a single interior space is accepted and works end to end

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-16--a-name-with-a-single-interior-space-is-accepted-and-works-end-to-end)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b0624137` |  | A single interior space is legitimate. `-Name "my bot"` was ACCEPTED - not refused with invalidName - and the welcome line reads "name":"my bot" with the space intact, agentId 01a0a3dc73fc71f3bfa3c305272f272a. A sidebar entry reading exactly `my bot` appeared at the bottom with NO refresh: performance navigation entries are still 1 and the Room was created after the last document load, so it was appended live. Room heading reads `my bot` and the members line `You, my bot` - the rendered and stored forms agree about the spacing. MENTION RESOLUTION GOES THROUGH THE MEMBER LIST, NOT A PATTERN, proven in both directions. `hi @my bot`: the delivered envelope carries "mentioned":true with a mentions array of exactly ONE member whose name is `my bot`, and the bot replied <p><strong>my bot:</strong> hi my bot</p> in bold - so the mention is not truncated at the space. `hi @my` (the bare prefix): the delivered envelope carries "mentioned":false with an EMPTY mentions array, and nothing replied over a full 11 seconds - so a prefix does not match a longer Name, and @my cannot wake `my bot` the way @Emily would otherwise wake `Emily Lee`. THE COMPOSER DID NOT COLLAPSE THE SPACE (the Inconclusive clause's check): the human row in the transcript reads `hi @my bot` verbatim, so the mention genuinely reached the server as typed. |

### PIPEEXTERNAL-17 — Dialling the wrong pipe name fails outside the browser, and the pipe name is configuration

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-17--dialling-the-wrong-pipe-name-fails-outside-the-browser-and-the-pipe-name-is-configuration)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b0624137` |  | THE PIPE NAME IS REAL CONFIGURATION, PROVEN IN BOTH DIRECTIONS. Against the app on its default name, `-Pipe wrongname` failed after 5.5 SECONDS - timed, not estimated - with exactly `Exception calling "Connect" with "1" argument(s): "The operation has timed out."`, so the client-side connect timeout is intact and a misconfigured agent does not hang forever. THE APP SAW NOTHING: its log gained ZERO lines for that attempt (counted before and after) and no Room appeared - which is the whole diagnostic point, the failure is loud at the client and invisible at the app. AFTER RESTARTING WITH Team__PipeName='huddle': `-Pipe team` now fails with the SAME timeout - so the old name is genuinely no longer served and the name is not hardcoded - while `-Pipe huddle` connects, prints a welcome line with agentId 01a0a3de4d007378b9c99deb3c5e20c4 and name `pipecheck`, and a `pipecheck` Room appears in the sidebar (23 entries, last one `pipecheck`). Nothing else was listening on either name. THE INCONCLUSIVE CLAUSE'S STARTUP RACE DID NOT OCCUR: on the renamed run the log reads `Demo agent alpha connected.` and `Demo agent echo connected.`, not the 30-attempt failure. AFTER CLEARING THE VARIABLE AND RESTARTING on P-LAUNCH-FREE, `pwsh tools/echo-bot.ps1 -Name mybot` connects to \\.\pipe\team and receives its welcome, so the default is served again. |

### PIPEEXTERNAL-18 — Members is filled on every messagePosted, and grows when the Room does

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-18--members-is-filled-on-every-messageposted-and-grows-when-the-room-does)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b0624137` |  | Run in the two-member `mybot` Room (01a0a3b55c467cf3873485fe2cdd9706), Team filter on `All teams`. FIRST DELIVERY: the envelope's members array holds EXACTLY TWO objects - {id:human, name:You, kind:human} and {name:mybot, kind:agent, id:01a0a3aeea2b71cca4933481e98fad96}. The Human IS included, so the count is not off by one at the two-versus-three boundary the reply gate turns on. THE INVITE: the panel's info line reads EXACTLY `Invited echo. Room is now "mybot, echo".` The heading became `mybot, echo`, the members line `You, mybot, echo`, and THE SIDEBAR ENTRY FOR THAT SAME ROOM ID RENAMED ITSELF to `mybot, echo` - all three with NO refresh, performance navigation entries still 1. The app logged EXACTLY `Invited agent 'echo' (01a0a3ae26d670d5af2f636c71464078) into room '01a0a3b55c467cf3873485fe2cdd9706'.` SECOND DELIVERY: the NEW envelope's members array holds EXACTLY THREE objects - You/human, mybot/agent AND echo/agent - so membership is resolved per delivery rather than cached in the gateway, and a new Member is visible to an already-connected client without it reconnecting. Both messagePosted lines carry the full list, not only the first. The three-member Room is LEFT IN PLACE for PIPEEXTERNAL-20 and -21 as the NOTE requires. |

### PIPEEXTERNAL-19 — An external agent joins a Room live, without reconnecting

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-19--an-external-agent-joins-a-room-live-without-reconnecting)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | THE BOT WAS NEVER RESTARTED - the same process that connected before this Room existed is the one that received its Messages, which is the whole point. New chat with echo + alpha made Room 01a0a3e04cdf74219cbd8cc3b2081c9f (`echo, alpha`, members `You, echo, alpha`), which mybot was not in. DOOR ONE, Add agent on `All teams`: the info line reads EXACTLY `Invited mybot. Room is now "echo, alpha, mybot".`; the heading became `echo, alpha, mybot`, the members line `You, echo, alpha, mybot`, and the sidebar entry FOR THAT SAME ROOM ID renamed itself - all with navigation entries still 1, so live and with no refresh. The app logged EXACTLY `Invited agent 'mybot' (01a0a3aeea2b71cca4933481e98fad96) into room '01a0a3e04cdf74219cbd8cc3b2081c9f'.` DELIVERY REACHED THE LONG-RUNNING CLIENT: its next messagePosted line carries roomId 01a0a3e04cdf74219cbd8cc3b2081c9f - the id copied from the address bar - and roomName `echo, alpha, mybot`, with members=4 and mentioned=true, and the bot's reply `mybot: hi mybot` appeared in that Room. So membership is resolved per delivery, not once at connect, and the inbound membership check did NOT come back `notMember`. DOOR TWO IS THE SAME CODE PATH: in `alpha`'s own two-member Room, `/invite @mybot` produced an info line reading EXACTLY `Invited mybot. Room is now "alpha, mybot".`, the heading went `alpha` -> `alpha, mybot`, the members line `You, alpha` -> `You, alpha, mybot`, and the sidebar entry for that id renamed itself live - identical behaviour to the button, with no red `Unknown agent` strip. |

### PIPEEXTERNAL-20 — The welcome lists every Room the Agent is in, and a reconnect after an invite mints a second Room of the same name

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-20--the-welcome-lists-every-room-the-agent-is-in-and-a-reconnect-after-an-invite-mints-a-second-room-of-the-same-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | THE DOCUMENTED CONSEQUENCE REPRODUCES EXACTLY AND IS RECORDED AS EXPECTED, NOT FILED. Before the reconnect the sidebar had no plain `mybot` entry - PIPEEXTERNAL-18's invite had renamed it to `mybot, echo` - and the mybot-prefixed entries were `mybot, alpha, echo`, `mybot, echo` and the unrelated `mybot2`. After Ctrl+C and a fresh connect, the welcome's rooms array holds FIVE entries, well past the required two: `alpha, mybot` (3 members), `mybot, alpha, echo` (4), `mybot, echo` (3), `echo, alpha, mybot` (4) and a brand-new `mybot` (2). So a client DOES learn about Rooms it was invited into while away. EVERY ENTRY INCLUDES THE HUMAN: each members array begins with `You`, so the handshake path has the same correct count as the delivery path. THE SECOND ROOM WAS MINTED: the app logged `Created direct room '01a0a3e107767365bd9c19e13836955c' for agent 'mybot'.` - a NEW id, not the old direct Room 01a0a3b55c467cf3873485fe2cdd9706, which now has three Members and so no longer satisfies the exact-two-member reuse rule. The sidebar shows BOTH `mybot, echo` and a plain `mybot` and the new one appeared with navigation entries still 1, i.e. no refresh. NAMES AGREE ACROSS BOTH PATHS: mapping each of the five welcome room ids to its sidebar entry gives an exact match for all five, so the Room name is not computed differently on the two paths. The new plain `mybot` Room opens with heading `mybot`, members `You, mybot` and an EMPTY transcript (0 rows). NOT THE BROKEN-LOOKUP CASE: PIPEEXTERNAL-09 passed, where a reconnect into an untouched two-member Room minted nothing - so this duplicate follows the invite, not every reconnect. |

### PIPEEXTERNAL-21 — Drafts are never delivered to another Agent

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-21--drafts-are-never-delivered-to-another-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | Run in the three-member `mybot, echo` Room (01a0a3b55c467cf3873485fe2cdd9706, heading `mybot, echo`, members `You, mybot, echo`) with mybot connected throughout - NOT in the fresh two-member `mybot` Room the Inconclusive clause warns about. THE PRECONDITION HELD: a MutationObserver confirmed a `.message-row.streaming` node really did appear for echo's reply, so deltas were definitely flowing on the server side and this test is measuring an absence rather than a non-event. THE EXTERNAL CLIENT RECEIVED EXACTLY TWO LINES, counted from its console against a baseline taken immediately before: one messagePosted for the human's `hi @echo` and one messagePosted for echo's FINISHED reply `**echo:** hi echo`. Grouping the new lines by their `type` discriminator gives messagePosted=2 and nothing else: ZERO lines containing "type":"messageDelta" and ZERO containing "type":"toolActivity". Echo's reply arrived ONCE, not twice, so the finished Message is not also being delivered down the Draft path. Both lines carry mentioned=false for mybot, which is correct - the human addressed echo. |

### PIPEEXTERNAL-22 — The mentioned flag is per recipient, and unmentioned Members still receive the envelope

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-22--the-mentioned-flag-is-per-recipient-and-unmentioned-members-still-receive-the-envelope)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | bot1 and bot2 connected in separate processes; Add agent on `All teams` made the Room 01a0a3e1bf7a7411b991175e3e111167 with heading `bot1, bot2` and members line `You, bot1, bot2`. THE SERVER LABELS RATHER THAN DECIDES. BOTH consoles received a messagePosted line for `hello @bot1` - bot2 was NOT narrowed out, so an unmentioned Agent keeps the conversation's context. THE FLAG IS PER RECIPIENT: bot1's copy carries "mentioned":true and bot2's copy of the SAME Message carries "mentioned":false. THE mentions ARRAY IS IDENTICAL IN BOTH: exactly one member object, name `bot1`, kind `agent`, id 01a0a3e1bedf73769a13c5b6eea56a6c - the same id in both lines, so the two recipients are labelled from the same data rather than computed separately. Both lines also carry members=3. EXACTLY ONE agent Message appeared in the browser, from `bot1` reading `bot1: hello bot1`, and NOTHING further arrived over 16 seconds - so bot2 did not act on a false flag. (bot2 additionally received bot1's reply with mentioned=false and an empty mentions array, which is the ordinary fan-out and not a second delivery of the human's Message.) |

### PIPEEXTERNAL-23 — Loop safety: two bots in one Room reply once each and go quiet

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-23--loop-safety-two-bots-in-one-room-reply-once-each-and-go-quiet)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | THE AMPLIFICATION GUARD HOLDS. Starting from 2 rows, `hi @bot1 @bot2` produced EXACTLY TWO agent Messages and the transcript gained EXACTLY THREE rows in total - measured 2 -> 5, both on screen and in the Room's .jsonl, which holds exactly five lines. The replies are `<p><strong>bot1:</strong> hi bot1 bot2</p>` and `<p><strong>bot2:</strong> hi bot1 bot2</p>` - both with the sender prefix in real bold markup. NEITHER REPLY CONTAINS AN `@` ANYWHERE: the only row in the exchange carrying one is the human's own `hi @bot1 @bot2`, so both clients stripped it from the quoted text and neither can re-trigger the other. THE ROOM WENT COMPLETELY QUIET: both replies landed within about 200ms and the row count stayed at 5 for the remaining ~15.8 seconds of the watch, so there is no slow-but-real loop. THE BUDGET WAS NEVER REACHED, which is the decisive tell the test names: the app log contains ZERO lines matching `refused a message` - no `its budget of 40 agent messages since a human last spoke is spent.` warning - and no pause prompt appeared anywhere on the page. A storm would have shown up as that warning with nothing typed. |

### PIPEEXTERNAL-24 — A second connection under the same name displaces the first, which is told by having its pipe closed

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-24--a-second-connection-under-the-same-name-displaces-the-first-which-is-told-by-having-its-pipe-closed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | Name `dup1`. First connection got agentId 01a0a3e30576746b8aaf2ccd7c61602d and its Room 01a0a3e3057e74448ea3cbf424b01eb7 answered `hi @dup1` normally. A SECOND process then connected under the same Name WITHOUT the first being stopped. THE SECOND IS ACCEPTED, NOT REFUSED: it received a normal `welcome` carrying the SAME agentId 01a0a3e30576746b8aaf2ccd7c61602d - so a client that crashed without a clean disconnect can get back in without an app restart. THE DISPLACED CLIENT IS TOLD, not left hanging - this is the half the test calls the silent failure: its console's last line is `Server closed the connection.` (its own Dispose then reports `Cannot access a closed pipe.`) and the PROCESS EXITED, confirmed by Get-Process returning nothing for its pid while the second process is still alive. THE BROWSER SHOWED NO CHANGE AT ALL: a snapshot of the full sidebar (27 entries), the heading `dup1`, the members line `You, dup1` and every transcript row was taken before the second connect and compared by exact string equality afterwards - IDENTICAL, so displacement is not being treated as a Room-level event. NO ZOMBIE: `hi @dup1 after displacement` was delivered to the SECOND console only (its messagePosted count went to 1 with that text; the first console stayed at the 1 line it had from before displacement), exactly ONE new agent Message appeared, and the Room's .jsonl went from 2 lines to 4 - exactly two added, never three. |

### PIPEEXTERNAL-25 — Three clients connect at once and the accept loop does not jam

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-25--three-clients-connect-at-once-and-the-accept-loop-does-not-jam)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b9b6c346` |  | THREE ROUNDS, ALL THREE PASS - and the test is explicit that one failing round would be a FAIL rather than a flake. Each round launched three clients back to back in a tight loop with no wait between them, which is tighter than pressing Enter by hand in three terminals. ROUND b1/b2/b3, ROUND c1/c2/c3, ROUND d1/d2/d3: in every round all three printed a {"type":"welcome" line - NINE welcomes, zero failures. NO ROUND PRODUCED `All pipe instances are busy`: every client's stderr was read, not just its stdout, and none carried that exception or any other - so the next pipe server instance is being created before I/O starts on the accepted one. THE IDS ARE DISTINCT WITHIN EVERY ROUND: 3 unique of 3 each time (b: ...4bc2e97f / ...aa49ff93 / ...2c4a83a3; c: ...08677efc / ...79563bda / ...d2b48d8e; d: ...5a66ae77 / ...b60eaa3d / ...35939f46), so distinct Names are not collapsing onto one Agent. THE APP LOGGED EXACTLY THREE `Created direct room` lines PER ROUND, counted as a delta around each round - so no connection was accepted and then left without completing its handshake. ALL NINE Rooms are in the sidebar (b1,b2,b3,c1,c2,c3,d1,d2,d3 all present, none missing) and they arrived with performance navigation entries still 1, i.e. with no refresh. The app stayed up throughout, so no round is disqualified as a timeout. |

### PIPEEXTERNAL-26 — The Budget labels ride on every messagePosted, and a refused post comes back as budgetExhausted

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-26--the-budget-labels-ride-on-every-messageposted-and-a-refused-post-comes-back-as-budgetexhausted)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `853806e7` |  | App restarted with Team__AgentMessageBudget=1 (ACP still off, so nothing here spends). Room 01a0a3e4ebb37743b2e122edeac3e21c, heading `bud1, bud2`, members `You, bud1, bud2`. THE LABELS RIDE ON THE WIRE: the line delivered to BOTH bots for the human's Message ends `"agentMessagesSinceHuman":0,"budget":1` - used 0, granted 1, so a client can apply the rule itself. THE GUARD IS ENFORCED, NOT JUST ADVERTISED: exactly ONE reply landed (bud2's, this run - which of the two wins is documented as non-deterministic and either is a pass) and it is the only agent row in the browser. THE OTHER BOT GOT THE SPECIFIC, TERMINAL CODE: bud1's console printed {"type":"error","code":"budgetExhausted","message":"This room has reached its budget of 1 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.","relatedMessageId":"3b420b7d06db48d280536ebf4c2fe069","version":3} - `budgetExhausted`, NOT a generic badMessage, and it carries the do-not-retry instruction and the id of the post it refused. THE COUNTER MOVED: the line delivered to bud1 for bud2's reply ends `"agentMessagesSinceHuman":1,"budget":1`. The app logged the Warning EXACTLY as specified: `Room '01a0a3e4ebb37743b2e122edeac3e21c' refused a message from 'bud1': its budget of 1 agent messages since a human last spoke is spent.` THE HUMAN IS ASKED, AND IN THE RIGHT PLACE: walking the DOM from the message list to the composer finds exactly one element between them, div.budget-prompt, reading `Agents have sent 1 replies since you last spoke, and are paused.` with two enabled buttons, `Continue` and `Leave paused` - below the transcript, not above it. THE REFUSAL HAPPENED BEFORE PERSISTENCE: the Room's .jsonl holds only the human line and bud2's reply; bud1's refused text is absent. |

### PIPEEXTERNAL-27 — Continue re-delivers the same Message to the Agents without showing it twice

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-27--continue-re-delivers-the-same-message-to-the-agents-without-showing-it-twice)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `853806e7` |  | THE GRANT REALLY WAKES THE ROOM. Before Continue: 2 transcript rows, last row `bud2: **bud2:** hi bud1 bud2`; the refused bot bud1's last delivered messagePosted had message.id 44db3ad2cd264c30b3f90c6b27d69729, used=1, budget=1. AFTER CLICKING CONTINUE, bud1's console printed a THIRD messagePosted line for a Message IT HAD ALREADY SEEN - same message.id 44db3ad2cd264c30b3f90c6b27d69729 and same text `**bud2:** hi bud1 bud2` - and that line now ends `"budget":2`, so the granted figure went up on the wire. THE RE-DELIVERY IS INVISIBLE: the browser row count is UNCHANGED at 2, with the same two rows in the same order - nothing rendered twice. The app logged `Room '01a0a3e4ebb37743b2e122edeac3e21c' was extended to 2 agent messages.` The pause block was replaced by div.budget-note reading EXACTLY `1 of 2 agent replies since you last spoke.` NO NEW REPLY FOLLOWED, which the Inconclusive clause says is CORRECT rather than a fail - the re-delivered Message is bud2's reply, whose `@` was stripped, so a mention-gated client rightly stays silent; the evidence here is the re-delivered line plus the unchanged transcript, and bud1's console was definitely not silent. THE DOUBLE-CLICK GUARD HOLDS: on a fresh pause the button was clicked once and then eight more times at 5ms intervals; a MutationObserver recorded the state sequence `Continue\|false` -> `Continuing…\|true` -> (prompt gone), so the button both RELABELS to `Continuing…` and is DISABLED while the grant is in flight. The app log holds exactly TWO `was extended to` lines in total - one from the earlier single click and one from this whole double-click burst - so nine clicks granted once. |

### PIPEEXTERNAL-28 — A wrong protocol version in hello is refused, and the browser never learns

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-28--a-wrong-protocol-version-in-hello-is-refused-and-the-browser-never-learns)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `853806e7` |  | Done with the RAW-CLIENT rather than by editing tools/echo-bot.ps1, which the NOTE forbids. A version-2 hello returned EXACTLY {"type":"error","code":"expectedHello","message":"Unsupported protocol version \u00272\u0027.","version":3} - the \u0027 are the ordinary wire-JSON escaping of the quotes around the 2. The NEXT ReadLine() returned $null, so the server closed the connection rather than leaving a mismatched client hanging. STRICT EQUALITY IS INTACT: no `vtest` Room was created. THE BROWSER LEARNED NOTHING, before or after an F5: the Room list stayed at 40 entries with no `vtest`, and a scan of the page text found zero occurrences of error/unsupported/version - no Room, no error, no visible trace, exactly as documented. THE TWO SIDES AGREE ON THE NUMBER: src/Huddle.Contracts/ProtocolVersion.cs reads `public const int Current = 3;` (its doc comment even says 'A strict-equality check against every parsed envelope') and tools/echo-bot.ps1 sends `version = 3` in both places it appears - so the shipped client was not left behind by a version bump. |

### PIPEEXTERNAL-29 — A first message that is not hello, and a silent client, are both refused as expectedHello

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-29--a-first-message-that-is-not-hello-and-a-silent-client-are-both-refused-as-expectedhello)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `853806e7` |  | AN UNAUTHENTICATED CLIENT CANNOT POST. Sending {"type":"postMessage",...,"roomId":"x","text":"hi"} as the FIRST message returned {"type":"error","code":"expectedHello","message":"Expected \u0027hello\u0027 as the first message.","version":3} - the specific code, not a generic one - and the next ReadLine() returned $null, so the connection was closed rather than allowed to continue. THE SILENT CLIENT IS TIMED OUT, NOT LEFT HOLDING A PIPE: a second raw connection that sent nothing had its first ReadLine() return after 5.00 SECONDS - measured with a Stopwatch - carrying {"type":"error","code":"expectedHello","message":"Timed out waiting for \u0027hello\u0027.","version":3}. So the hello timeout is alive and a stuck or hostile client cannot hold an instance indefinitely. It did NOT return instantly with nothing, which the Inconclusive clause says would have been a distinct finding. The following ReadLine() returned $null there too. THE BROWSER GAINED NOTHING from either attempt, before or after an F5: 40 Rooms both times, no Room named `x`, and since no such Room exists no transcript could have grown. |

### PIPEEXTERNAL-30 — After the handshake, a bad line or a bad request is reported and the connection STAYS OPEN

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-30--after-the-handshake-a-bad-line-or-a-bad-request-is-reported-and-the-connection-stays-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `853806e7` |  | One raw connection, handshake as `raw1` (agentId 01a0a3e7656d754b96d50d32a4f4aa12, Room 01a0a3e765767e0ca584b172ee0c99c1), then five bad requests back to back on that SAME connection. (a) bad JSON -> "code":"badMessage" with the JSON library's own complaint, `'not json at all' is an invalid JSON literal. Expected the literal 'null'. Path: $ \| LineNumber: 0 \| BytePositionInLine: 1.` (not pinned, as instructed). (b) second hello -> "code":"badMessage", "message":"already registered". (c) unknown room -> "code":"unknownRoom", "message":"Unknown room 'nosuchroom'.", "relatedMessageId":"m9". (d) empty text -> "code":"badMessage", "message":"empty message", "relatedMessageId":"m10". (e) bad message id -> "code":"badMessage", "message":"Invalid message id.", "relatedMessageId":"m.1". Every one uses the SPECIFIC documented code, and every refusal of a POST carries relatedMessageId so the client knows which in-flight post failed. (a) and (b) carry none, which is correct rather than the omission the Fail-if describes: unparseable text has no id to quote and a hello is not a post.) THE CONNECTION SURVIVED ALL FIVE: the sixth request, a valid post with messageId m11, SUCCEEDED - `still alive` appears in the raw1 Room in the browser with NO refresh (performance navigation entries still 1) and the Room opened straight from the sidebar entry that had appeared live. VALIDATION IS BEFORE PERSISTENCE: the Room's .jsonl holds EXACTLY ONE line, the m11 `still alive` Message; none of the five refused requests reached it. |

### PIPEEXTERNAL-31 — An Agent cannot write a Draft into a Room it is not a Member of, even by reusing a message id

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-31--an-agent-cannot-write-a-draft-into-a-room-it-is-not-a-member-of-even-by-reusing-a-message-id)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `9368a0bc` |  | ECHOROOM = 01a0a3ae26dd774f934b0e01fae58bdf (8 existing rows). A raw client handshook as `raw2` (agentId 01a0a3e7ee5273ceb84e8cbac7954361, own Room 01a0a3e7ee56784499fbe97117f57ee1) - not a Member of the echo Room. THE INTRUSION IS REFUSED WITH THE RIGHT DIAGNOSIS: {"type":"error","code":"notMember","message":"'01a0a3e7ee5273ceb84e8cbac7954361' is not a member of room '01a0a3ae26dd774f934b0e01fae58bdf'.","relatedMessageId":"m1","version":3} - `notMember`, NOT `unknownRoom`, so the room lookup is not failing ahead of the membership check. THE LEGITIMATE DRAFT IS ACCEPTED: the same messageId `m1` written into raw2's OWN Room produced no error line and a real streaming row - a second browser tab watching that Room recorded 67 samples carrying `message-row streaming` reading `raw2\|Stop\|mine`. THE LAUNDERING ATTEMPT STILL FAILS: retrying the intrusion with that same id `m1`, immediately after it had been accepted elsewhere, returned the IDENTICAL `notMember` error with relatedMessageId `m1` - so the verdict is keyed on the Room, not cached on the message id. THE ECHO ROOM NEVER SHOWED ANYTHING: a poller on that Room across the whole exchange recorded a constant 8 rows, ZERO streaming rows and ZERO samples where the page text contained `intruder`, and a direct check afterwards confirms `intruder`/`intruder again` are absent. (Honest note on resolution: the echo tab was backgrounded for part of the run so its poller was throttled to roughly 1 sample/second, 43 samples; the row count and the text check are unambiguous at that rate, and a refused delta is never stored so there was nothing that could have rendered between samples.) |

### PIPEEXTERNAL-32 — An Agent disconnecting mid-Draft clears its Draft rather than freezing it on screen

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-32--an-agent-disconnecting-mid-draft-clears-its-draft-rather-than-freezing-it-on-screen)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Fail *(earlier script)* | `9368a0bc` | #27 | FOUR OF THE FIVE PASS CONDITIONS HOLD, AND THE CLEANUP THIS TEST EXISTS TO PROVE IS CORRECT. A raw client handshook as `rawdraft` (Room 01a0a3e8e2c4778fb323ee5d448ebfd6) and sent ONE messageDelta `half a sentence` with isFinal:false and no terminator. THE ROW APPEARED with everything specified: class `message-row streaming`, sender `rawdraft`, a `.draft-stop` Stop button, the PLAIN text `half a sentence`, and a `.draft-caret` element present (its blink is the `draft-caret-blink 1s step-end infinite` animation confirmed in PIPEEXTERNAL-02). THE DRAFT IS SERVER-SIDE, NOT PER-BROWSER: a SECOND tab navigated to the same Room URL AFTER the delta had been sent, and it rendered the row on first load (1 row immediately) - stronger than merely having it too. THE CLEANUP IS FAST AND REACHES EVERY VIEWER: both tabs' pollers go 1 row -> 0 rows with no refresh, and correlating the page clock with the file the client writes immediately after $c.Dispose() puts the first zero-row sample 21 MILLISECONDS after the dispose - far inside the 'about a second' bar. A THIRD, freshly opened tab shows 0 rows, no streaming row, and no `half a sentence` anywhere in the page - so the server-side Draft store really was cleared, not just the two live circuits. NOTHING WAS PERSISTED: the Room has NO .jsonl file at all, so the Draft never reached the transcript. THE ONE FAILING CONDITION is `T-A logs the connection ending (Agent connection <id> ended.)`: the app logged only `Created direct room '01a0a3e8e2c4778fb323ee5d448ebfd6' for agent 'rawdraft'.` and nothing at all for the disconnect. This is NOT a second defect - it is the same already-filed #27 (the Warning sits only in catch (IOException) at AgentConnection.cs:106, and a clean close never throws), reached here through a different client. Recorded as a Fail because the condition is unmet, with the link rather than a new issue. |

### PIPEEXTERNAL-34 — Stopping the app closes every Agent connection cleanly, and the browser's reconnect dialog is readable

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-34--stopping-the-app-closes-every-agent-connection-cleanly-and-the-browsers-reconnect-dialog-is-readable)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Fail *(earlier script)* | `9368a0bc` | #16 | FOUR OF THE FIVE PASS CONDITIONS HOLD. SHUTDOWN TELLS EVERY CLIENT: both shut1 and shut2 printed `Server closed the connection.` as their last line and BOTH PROCESSES EXITED - a scan for pwsh processes running echo-bot afterwards returns zero - so no agent has to be killed by hand. THE DIALOG SEQUENCE IS RIGHT: sampled every 200ms across the whole shutdown, it goes `components-reconnect-show` -> `components-reconnect-show components-reconnect-retrying` (with the 5s then 30s backoff counting down) -> `components-reconnect-failed` showing `Failed to rejoin.Please retry or reload the page.` with a Retry button, about 81 seconds in. STATE SURVIVED THE RESTART: after Retry, the same Room URL, the sidebar still holds 45 Rooms and the transcript still holds its 10 rows ending `echo: **echo:** hi echo before shutdown` - nothing lost. THE COUNTERS RESET: the div.budget-note reading `1 of 40 agent replies since you last spoke.` that was showing before the shutdown is GONE afterwards, so the budget is in-memory as documented. THE FAILING CONDITION is `At each stage the dialog shows exactly ONE state paragraph`: the FIRST stage shows one and the FAILED stage shows one, but the RETRYING stage shows TWO stacked - `Rejoining the server...` AND `Rejoin failed... trying again in N seconds.` - and it stays that way for the entire ~44-second retry window, not for an instant. THE CAUSE IS NOT THE ONE THIS TEST PREDICTS: its Fail-if blames a 404 on the ReconnectModal CSS, but fetching every linked stylesheet returns 200 for ALL FIVE including the scoped bundle Huddle.App.j8c8gsv1bg.styles.css, and the dialog picks up border-radius and box-shadow from that same bundle. The real cause is that Blazor ADDS `components-reconnect-retrying` alongside `components-reconnect-show` rather than replacing it, so the first-attempt rule keeps matching. Linked to the existing #16 (also SHELLNAV-26 and APPEARANCETHEME-24) rather than filed again; the stage table and the corrected root cause were posted there as a comment. |

### PIPEEXTERNAL-35 — An empty install shows its two empty-state strings, and the main pane does not auto-open the first new Room

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-35--an-empty-install-shows-its-two-empty-state-strings-and-the-main-pane-does-not-auto-open-the-first-new-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `8f5b0e4b` |  | App_Data was genuinely DELETED (copied to a backup first, as the test's own warning suggests, and restored afterwards so later areas keep their Teams and Transcripts). A repo-wide `Get-ChildItem -Recurse -Filter team.db` returned NOTHING after the delete, which rules out the 'you deleted the wrong folder' explanation for any ghost data. THE SWITCH IS READ: with Team__DemoAgent__Enabled=false the log carries ZERO `Demo agent ... connected.` lines and no Room was created at startup. THE THREE EMPTY-STATE STRINGS MATCH EXACTLY. Sidebar Room list: `No rooms yet. Start an agent to create one.` Main pane: the SAME sentence, `No rooms yet. Start an agent to create one.` New chat panel: `No agents have connected yet. Start one with tools/echo-bot.ps1, or enable the demo agent in appsettings.json.` No ghost agents were listed. The main pane did NOT throw - no Blazor error (#blazor-error-ui display:none) and no error text anywhere. THE DATA DIRECTORY IS RECREATED AT STARTUP with team.db (plus -shm/-wal and an empty Teams folder), and `App_Data\rooms` did NOT exist yet at that point. AFTER `pwsh tools/echo-bot.ps1 -Name mybot`: the sidebar gained `mybot` with NO refresh (performance navigation entries still 1) while THE MAIN PANE STILL READ `No rooms yet. Start an agent to create one.` - the documented, deliberately static behaviour the Inconclusive clause says is expected and not a fail, since the auto-navigate only runs on a page load with no Room selected. Clicking the entry opened the Room with heading `mybot` and members `You, mybot`. AFTER THE FIRST MESSAGE, `App_Data\rooms\` appeared holding exactly one file, 01a0a3f3c23270a0bbd19e15a80a6a57.jsonl - so the rooms directory really is created lazily on first persistence. Reset afterwards: App_Data restored from the backup (24 files), Team__DemoAgent__Enabled cleared, app restarted on P-LAUNCH-FREE with both demo agents connecting again. |

### PIPEEXTERNAL-36 — Two app instances on one pipe name split clients unpredictably

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-36--two-app-instances-on-one-pipe-name-split-clients-unpredictably)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `671ab479` |  | THE HAZARD REPRODUCES AND THE SPLIT IS GENUINELY UNPREDICTABLE. Second instance started with Team__DataDir=App_Data2 on port 5101, same default pipe name; it started cleanly with no pipe error, and its own log shows it even took over one of the demo agents (`Created direct room ... for agent 'alpha'`) - the collision starts before any external client. FOUR NAMES, TWO INSTANCES, ALTERNATING LANDINGS: `splitter` -> 5100, `splitter1` -> 5101, `splitter2` -> 5100, `splitter3` -> 5101, read from each instance's own `Created direct room '<id>' for agent '<name>'.` line and cross-checked against each client's welcome room id. So the landings are NOT all in one window and the race is genuinely two-sided. EACH ROOM APPEARS IN EXACTLY ONE WINDOW, NEVER BOTH: the 5100 window's Room list holds `splitter` and `splitter2` and nothing else matching; the 5101 window's holds `alpha`, `splitter1`, `splitter3`. THE WINDOW THAT MISSED A CLIENT SHOWS NOTHING ABOUT IT: zero role="alert" elements in either, and neither page's text mentions the names that went to the other instance - no error, no warning, no clue, which is exactly why this gets misfiled as a delivery bug. FOUR host processes (two dotnet.exe launchers plus two Huddle.App.exe hosts), i.e. more than one. BOTH DATABASES EXIST AND EACH HOLDS ONLY ITS OWN AGENTS: querying the Users table directly, App_Data\team.db has `splitter` and `splitter2`; App_Data2\team.db has `splitter1` and `splitter3` - `splitter` is in one database only. Step 7's reply check also worked: reconnecting `splitter` landed on 5100 again with the SAME agentId, and `hi @splitter` in the 5100 window produced `splitter: **splitter:** hi splitter`. Cleaned up: the 5101 instance stopped, its window closed, and 5100 still serving. HARNESS NOTE: the second instance could not be started with a plain `dotnet run` because the first instance holds a lock on the shared build output (MSB3027 on Huddle.Contracts.dll); `dotnet run --no-build` against the already-built binaries was used instead. That is a build-output collision in this harness, not app behaviour, and it does not touch what the test measures. |

### PIPEEXTERNAL-37 — Pressing Stop on an external agent's Draft sends it a stopTurn for that Room

File: [`docs/engineering/manual-tests/pipe-external.md`](pipe-external.md#pipeexternal-37--pressing-stop-on-an-external-agents-draft-sends-it-a-stopturn-for-that-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## App Tools a real model calls (progressive discovery)

### APPTOOLS-01 — The prompt groups are readable in the browser and no default contains the literal mcp__team__

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-01--the-prompt-groups-are-readable-in-the-browser-and-no-default-contains-the-literal-mcp__team__)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `613dce5c` |  | Run on P-LAUNCH-FREE with no hooks.json present. /settings loads with <h1> `Settings`, a two-button rail with `Hooks` active by default (no click needed), and the second intro paragraph reads `Overrides are stored at E:\Repos\Huddle\src\Huddle.App\App_Data\hooks.json, … The file does not exist until the first time you save here, so it being absent is expected, not a bug.` FOUR GROUPS IN ORDER - System prompt, Turn, Get help, Tool descriptions - with their fields in exactly the specified order: Orientation/Identity/Chat rules/Tools; Room label/Message/Catch-up header/Catch-up line; the nine Help: * fields; and the five `… description` fields. THE TWO CONSOLE CHECKS RUN VERBATIM: `document.querySelectorAll('textarea.hooks-field-value').length` returns **22**, and the mcp__team__ filter returns an EMPTY ARRAY - no hook default anywhere contains the literal prefix, so nothing has been hard-coded away from the `ToolServerName` constant. THE TOKENS ARE INTACT: `Tools` contains `{{toolNames}}` and it sits on a line of its own (not replaced by five rendered tool names); `Orientation` contains `{{helpTool}}`; `Help: tool entry` contains BOTH `{{toolName}}` and `{{toolDescription}}`; and `Room label` is EXACTLY `[Room: {{roomName}} (id: {{roomId}})]`, so the id half is present and no agent is cut off from learning a Room id. EXACTLY NINE `Next session` badges, on Orientation, Identity, Chat rules, Tools and all five Tool descriptions fields - none on any Turn or Get help field. App_Data holds NO hooks.json. No red composer-error paragraph and the tab rail is present, so the Inconclusive clause does not apply. |

### APPTOOLS-02 — PromptValidator reports a missing placeholder and a typo, and never blocks the save

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-02--promptvalidator-reports-a-missing-placeholder-and-a-typo-and-never-blocks-the-save)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `613dce5c` |  | THE VALIDATOR ADVISES, NEVER REFUSES. Replacing `Room label` with `[Room: {{roomName}}]` raised a red `hooks-field-issue-error` naming the missing `{{roomId}}` and repeating the helper sentence about it being the only way an agent learns a Room's id - immediately, with no reload and no Save - and the field gained `Modified` + `Unsaved`. `Save` STAYED ENABLED with the error showing, and the save SUCCEEDED: no `Could not save:` paragraph appeared, the badge row dropped to `Modified` only, and hooks.json was created holding EXACTLY ONE key, `turn.roomLabel`, with the edited value and no other hook key. THE TYPO CHECK ADDS A SECOND, DIFFERENT SEVERITY: typing `{{roomID}}` (capital D) produced TWO issue lines - the red missing-{{roomId}} error AND an amber `hooks-field-issue-warning` naming the token `{{roomID}}` and saying it is most likely a typo. RESET STAGES, IT DOES NOT WRITE: clicking `Reset` returned the textarea to exactly `[Room: {{roomName}} (id: {{roomId}})]`, cleared every issue line, and left the badge row reading `Unsaved` only. AFTER THE FINAL SAVE both badges are gone and hooks.json is exactly `{}` - the key was REMOVED rather than stored as a redundant copy of the catalog default. The default is restored, as the NOTE requires, so the later invite/post tests are not silently broken. |

### APPTOOLS-03 — With Team:Acp:Enabled=false no App Tool server exists at all, and the chat surface still works

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-03--with-teamacpenabledfalse-no-app-tool-server-exists-at-all-and-the-chat-surface-still-works)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `613dce5c` |  | Run with Team__Acp__Enabled=false. `Nova` (Title Coordinator, Alias nova) was created through the UI for this area, giving four Personas - coo, Friday, Jarvis, Nova. THE MODEL DROPDOWN'S FREE PROBE WORKED and is not filed, per the Inconclusive clause: opening the New agent card briefly spawned a node probe that returned the real roster (Default, Opus 1M, Fable, Sonnet, Haiku); Haiku was chosen, and the Effort control then settled on `Use the agent's default` with the helper `This model offers no effort choice, so it will think as it normally does.` - Haiku advertises no effort ladder, the same state Friday and Jarvis are in (no persona_efforts rows), so Haiku/low means Haiku at its only setting. EVERY PERSONA TILE READS `Offline` WITH NO REASON: the status markup is <span class="agent-tile-status"><span class="agent-dot offline"></span>Offline</span>, the dot computes rgb(179,33,33) - the --status-offline light value #b32121, RED by design and not grey - and NO element inside any tile carries a title attribute, so hovering shows no tooltip reason. Nova's card shows `Offline` with no reason line beneath it; a scan for reason/because/failed/degraded text returns nothing. NO TOOL SERVER AND NO ADAPTER: `App tool server listening at` appears ZERO times in the app log, the adapter count is 0, and node.exe stayed at the same 18 unrelated processes throughout - including across creating the Persona, opening the card, and the whole chat exercise. THE CHAT SURFACE WORKS FOR FREE: `echo` connected and replied `echo: hello echo` in bold. HONEST NOTE ON STEP 13: the test says to type a bare `hello` and expect a reply; the bare `hello` got NO reply, and `hello @echo` did. That is not a defect - PIPEEXTERNAL-01's NOTE states the demo clients reply only when mentioned, even in a two-member Room - so the step's wording is inconsistent with the documented client behaviour, and the pass condition (echo connects and replies, proving the surface works with zero spend) is met. NO member-health-alert STRIP appeared anywhere in the Room (zero such elements, and no visible alert-classed element at all). |

### APPTOOLS-04 — The tool server binds and completes the MCP handshake before any model call — the wiring pre-flight

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-04--the-tool-server-binds-and-completes-the-mcp-handshake-before-any-model-call--the-wiring-pre-flight)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `26896162` |  | THE WIRING PRE-FLIGHT PASSES - the gate that separates a configuration fault from a discovery fault, and the one the area says must pass before any token is spent. Preconditions met: tools/acp/node_modules exists, ANTHROPIC_API_KEY is NOT set, and the app was started in a shell with no Team__Acp__Enabled=false and Logging:LogLevel:Agency.Huddle at Debug. NO PROMPT WAS SENT - no Room was opened to type in and no message exists anywhere, so no tokens were spent. FOUR ADAPTERS STARTED, one per Persona, and all four tiles went to `Online` with class `agent-dot online` computing rgb(46,158,68) - coo, Friday, Jarvis and Nova. None went `Degraded`, the word does not appear on the page, and there is no member-health-alert strip anywhere. A room named `Nova` appeared in the sidebar; opening it shows heading `Nova`, members `You, Nova`, ZERO message rows and no alert strip. THE TOOL SERVER BOUND ON LOOPBACK, four times - one per Persona, which the Inconclusive clause says is the design and not a leak: `App tool server listening at http://127.0.0.1:52435/mcp.`, `…:56071/mcp.`, `…:56072/mcp.`, `…:61852/mcp.` Every one is on 127.0.0.1, so there is no non-loopback binding to report. THE ADAPTER ACTUALLY TALKED TO IT - the high-value check: the log contains `App tool server received initialize.` FOUR times and `App tool server received tools/list.` FOUR times (plus four `server/discover`), i.e. once per Persona. So the MCP handshake completed and the tools are registered; the silent failure where the tile says Online while the model has no tools at all is absent. App_Data\work\Nova\ exists on disk (alongside Friday, Jarvis and coo). |

### APPTOOLS-05 — An Agent reads its own Room's id off the [Room: ...] label

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-05--an-agent-reads-its-own-rooms-id-off-the-room--label)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b10f6dc3` |  | Lane P-LAUNCH-PAID on a clean App_Data with Team__DemoAgent__Enabled=false and Logging:LogLevel:Agency.Huddle at Trace; ANTHROPIC_API_KEY unset, so the turn bills the Claude login. Personas `Nova` (Coordinator, @nova) and `Jarvis` (Researcher, @jar) created on Haiku, both Online. The `Nova` Room's URL id is 01a0a61709c874f9bbcb59325cdf3686 - 32 characters, matching ^[0-9a-f]{32}$. Asked `What is the id of the room we are in right now? Answer with just the id, nothing else.`, Nova replied with EXACTLY `01a0a61709c874f9bbcb59325cdf3686` - character for character the URL id, same lowercase, no dashes, and nothing else. EXACTLY ONE message row from Nova (no duplicate post, so APPTOOLS-15's failure shape is absent here too). So the {{roomId}} placeholder really is reaching the rendered [Room: …] label, and the invite/post tests below rest on a verified foundation. |

### APPTOOLS-06 — get_help's BUDGET and MESSAGES sections prove progressive discovery is live

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-06--get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b10f6dc3` |  | Run at TRACE, so both oracles are available. THE REPLY COVERS ALL FOUR BUDGET POINTS, in Nova's own words: `it stops accepting agent messages and tells the human`; `The human can then allow more`; `If a refusal says the budget is spent, that's final—do not retry`; and `do not work around it by posting to another Room`. IT ALSO DEFINES CONTEXT ONLY correctly: `These are messages you were not addressed in. Read them for background, but do not answer them.` Neither section exists in the system prompt, so this could only have come from a get_help call. THE TRACE ORACLE CONFIRMS IT RATHER THAN INFERRING IT: the log carries `App tool server received tools/call.` and `App tool server replied to tools/call:` whose JSON payload was parsed out and compared to tests/Huddle.Tests/Acp/Golden/getHelp.txt - EXACT MATCH once newlines are normalised (the wire payload carries CRLF, the golden file LF; 3663 vs 3628 bytes, identical text). The payload contains both the BUDGET and MESSAGES sections. So no hook override is in force and the model called the real tool. NOT the index-miss case the Inconclusive clause describes: the only tools/call in the log is this one, and its payload IS get_help's output. |

### APPTOOLS-07 — Asked what tools it has, an agent calls get_help and reports the real catalog with descriptions

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-07--asked-what-tools-it-has-an-agent-calls-get_help-and-reports-the-real-catalog-with-descriptions)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b10f6dc3` |  | Judged on a FRESH session so the answer could not be recycled: the first attempt was run against `Nova`, which had called get_help in the previous test, and the log's tools/call count did NOT rise - the model answered from its own session context. That run was discarded and the question put to `Jarvis`, whose session had never called the tool; the tools/call count rose 1 -> 2, so this turn really did call it. ALL FIVE NAMES, EXACTLY SPELLED, with the full prefix: mcp__team__get_help, mcp__team__list_agents, mcp__team__create_room, mcp__team__invite_agent, mcp__team__post_message - no `mcp__team_get_help` single-underscore variant. THE DESCRIPTIONS ARE THE REAL ONES, which is the part that separates a get_help call from a recital of the system prompt's names-only Tools block. post_message carries the exact phrase the test names: `because your reply in the current turn is only ever delivered to that Room, never to another one`. invite_agent says `Give the Room's id and the agent's exact name`. list_agents says `Each entry includes its job description, drawn from the Persona's frontmatter when one is present`. A streaming row with sender `Jarvis` and a **Stop** button was visible during the turn. ONE SUB-OBSERVATION RECORDED HONESTLY: the tool-activity line was not sampled on THIS turn, because the observing script was cut off by the harness's 45-second limit; the identical mechanism was then observed directly on the same Persona in APPTOOLS-08, where the line read `mcp__team__list_agents`. Not failure mode (a), (b), (c) or (d): the descriptions are verbatim tool text, not invented, not absent, not from another product, and not quoted from source files. |

### APPTOOLS-08 — Tool activity is visible under the Draft while a call is in flight

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-08--tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b10f6dc3` |  | Run standalone on `Jarvis` with `Call mcp__team__list_agents and tell me how many agents it returned.`, watched by a MutationObserver on the message list. THE FULL SEQUENCE WAS CAPTURED: at +1121ms a streaming row exists with sender `Jarvis` AND a `.draft-stop` **Stop** button AND a tool-activity line beneath the draft reading `mcp__team__list_agents`; at +1231ms the activity line is gone while the draft still streams; at +3617ms the streaming row itself is gone. AT MOST ONE ACTIVITY LINE AT A TIME - the maximum count observed across the whole turn is 1, so nothing accumulates into a growing list. NOTHING IS LEFT BEHIND: after the turn there are ZERO .tool-activity elements and ZERO .message-row.streaming rows, and the transcript holds a single ordinary message row from Jarvis reading `It returned 2 agents: Nova (Coordinator) and Jarvis (Researcher), both online.` - which is also correct, since exactly those two Personas exist with demo agents disabled. THE LOG CORRELATES: the `App tool server replied to tools/call:` count rose from 2 to 3 across this turn, i.e. the reply landed inside the window in which the activity line was on screen. (Wording of the activity title is not judged - it comes from the adapter.) |

### APPTOOLS-09 — Tool activity is not written to the Transcript and does not survive the turn

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-09--tool-activity-is-not-written-to-the-transcript-and-does-not-survive-the-turn)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b10f6dc3` |  | Run immediately after APPTOOLS-08's tool-calling turn in the `Jarvis` Room. NOTHING SURVIVES THE TURN: with the turn finished, the Room shows ZERO .tool-activity elements and ZERO streaming rows - only four ordinary message rows, each with a sender name, a time (.message-meta) and a body. AFTER A FULL PAGE RELOAD: still zero activity elements, still zero streaming rows, still the same four message rows. AFTER STOPPING AND RESTARTING THE APPLICATION and reopening the Room: still zero activity, zero streaming, and the same four rows each with meta and body. THE TRANSCRIPT FILE HOLDS ONLY MESSAGE RECORDS: parsing every line of both Rooms' .jsonl (6 lines and 4 lines) gives exactly one record shape - keys id, senderId, senderName, text, timestamp - with no record describing a tool call. (A naive text search does match `mcp__team__list_agents` in that file, but only inside the HUMAN's own message text `Call mcp__team__list_agents and tell me…`, not in any tool-activity record.) The disappearance of tool-call history is the recorded decision in known-limits.md and is not filed. |

### APPTOOLS-10 — list_agents reports real names, real online state, and real job descriptions

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-10--list_agents-reports-real-names-real-online-state-and-real-job-descriptions)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `d3b49a9a` |  | THE UI TRUTH WAS WRITTEN DOWN FIRST, as the NOTE insists. /agents: `Jarvis \| Researcher · @jar \| Online` and `Nova \| Coordinator · @nova \| Online`. New chat panel: Nova, Jarvis, alpha, echo - all four with `agent-dot online` computing rgb(46,158,68). Demo agents were re-enabled for this test, so echo and alpha are in scope. THE REPLY MATCHES BOTH LISTS EXACTLY, in both directions: Nova answered `Agents: Nova (Coordinator, online), Jarvis (Researcher, online), alpha (online), echo (online). Personas: Jarvis (Researcher), Nova (Coordinator). Everyone's online.` Every name it gives is on the UI lists, and every UI name is in the reply. NO INVENTED NAMES - no Alice, no Bob, no Chief of Staff, no `bananas`; nothing traceable to docs/ or tests/ fixtures, which is the plausible-looking wrong answer this test exists to catch. Agents are separated from Personas and every Agent carries an online state, and all four states agree with the green dots on screen. THE TRACE PAYLOAD SETTLES IT rather than leaving it to inference - the tool's literal output has exactly the documented shape: `Agents:` then `- Nova (online)` / `  Title: Coordinator` / `  Alias: nova`, `- Jarvis (online)` with its Title and Alias, then `- alpha (online)` and `- echo (online)` with no job description (they have no Persona), then a blank line and `Personas:` listing Jarvis and Nova with their Titles and Aliases. So ComposeJobDescription is emitting real job descriptions. |

### APPTOOLS-11 — An Alias is advertised by list_agents and then accepted by invite_agent

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-11--an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `d3b49a9a` |  | Both paths verified, with the P-RESET-ALL between them that the NOTE says is what makes the tool path meaningful. THE TILE ADVERTISES THE HANDLE: `Jarvis` reads `Researcher · @jar`. FREE PATH: in a three-member Room (`Nova, echo`), the human typing `/invite @jar` produced the info line `Invited jar. Room is now "Nova, echo, Jarvis".` and renamed the header, the sidebar entry and the members line to `You, Nova, echo, Jarvis` with NO refresh (navigation entries 1). TOOL PATH, on a clean install: App_Data deleted, app relaunched, `Nova` and `Jarvis` recreated on Haiku, and a FRESH three-member Room `Nova, echo` created - so Jarvis was genuinely not a member and the `already a member` shortcut could not apply. THE TOOL ACCEPTED THE ALIAS IT HAD JUST ADVERTISED - the documented trap is absent: Nova's reply lists the handles it read out of list_agents (`Nova nova online`, `Jarvis jar online`, with echo and alpha showing no alias) and then reports `Now inviting Jarvis (jar) to this room: Done! Jarvis has been added to this room. It now has 4 members.` There is no `Unknown agent 'jar'` refusal. THE RENAME IS LIVE AND COMPLETE: header `Nova, echo, Jarvis`, sidebar entry `Nova, echo, Jarvis`, and `.chat-members` `You, Nova, echo, Jarvis` - all three, with navigation entries still 1. The room count stayed at 5, so it invited rather than calling create_room. THE LOG CONFIRMS THE TOOL PATH: `Invited agent 'Jarvis' (01a0a620c1ee7048bae6422f27a12cf2) into room '01a0a6210c1777abb1947476dcbf6792'.` ONE DEVIATION FROM THE SCRIPT, RECORDED: step 5's prompt as written produced NO reply at all, because the Room has three members and the prompt carries no @-mention - the mention gate correctly treated it as context-only. Re-sent prefixed with `@Nova`, which is the same request addressed properly; that is the run reported above. |

### APPTOOLS-12 — invite_agent, called with the id from the agent's own label, renames the room live

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-12--invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4a9400bb` |  | THE ROOM ID WAS WRITTEN DOWN BEFORE SENDING, as the NOTE insists: a fresh three-member Room (`Nova, echo`, members `You, Nova, echo`, Jarvis NOT a member) at /rooms/01a0a622d5287b09b9e13efab9674b7e, with Jarvis `Online` beforehand so the Inconclusive clause does not apply. Nova was asked to invite Jarvis taking the id from its own [Room: …] label and to report the id it used. IT READ ITS OWN LABEL CORRECTLY: the reply is `Done! I invited Jarvis to the room using the id: 01a0a622d5287b09b9e13efab9674b7e` - character for character the id from the URL. It did not ask the human for the id, did not pass the room NAME, and did not call create_room (the sidebar room count went 5 -> 6 only because this Room was newly created before the test; no second room appeared during the turn). THE RENAME IS LIVE ON ALL THREE SURFACES with NO refresh (navigation entries 1): header `Nova, echo, Jarvis`, sidebar entry `Nova, echo, Jarvis`, and .chat-members `You, Nova, echo, Jarvis`. THE CONSOLE IS THE AUTHORITY AND IT AGREES: `Invited agent 'Jarvis' (01a0a620c1ee7048bae6422f27a12cf2) into room '01a0a622d5287b09b9e13efab9674b7e'.` - the same room id as the URL. DELIVERY REACHES THE NEW MEMBER: `@Jarvis are you there?` was answered by Jarvis with `Yes, here and ready to help.`, so Jarvis is a member in fact and not just on paper. |

### APPTOOLS-13 — invite_agent is idempotent and says so rather than duplicating a member

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-13--invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4a9400bb` |  | Run immediately after APPTOOLS-12, with the Room in the right state. Asked to invite Jarvis AGAIN, Nova quoted the tool verbatim: `The tool said back: "Jarvis is already a member of room '01a0a622d5287b09b9e13efab9674b7e'. Nothing to do."` - exactly the documented form, returned as TEXT the model can read rather than as a thrown exception, and the Persona stayed Online rather than flipping to Degraded. NOTHING CHANGED: .chat-members is still `You, Nova, echo, Jarvis` (Jarvis appears ONCE, not twice), the header `<h1>` still reads `Nova, echo, Jarvis` and has NOT become `Nova, echo, Jarvis, Jarvis`, and the sidebar entry is identical - all compared string-for-string against the values noted before the turn. THE ALREADY-A-MEMBER CHECK RAN BEFORE THE WRITE, not after: the console still holds exactly TWO `Invited agent 'Jarvis'` lines, both from earlier tests, and no new one for this attempt. |

### APPTOOLS-14 — invite_agent with an unknown name comes back with the names that do exist

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-14--invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4a9400bb` |  | `Zephyr` was confirmed absent from both /agents and the New chat panel (which hold only Nova, Jarvis, alpha, echo). Nova called the tool rather than refusing, and quoted the result verbatim: `The tool replied word for word: "Unknown agent 'Zephyr'. Agents that do exist: echo, alpha, Nova, Jarvis."` THE SELF-CORRECTION AFFORDANCE IS INTACT - it is not a bare `unknown agent` message: the error carries a list of alternatives, and that list is EXACTLY the four real agents visible in the UI (echo, alpha, Nova, Jarvis), neither empty, nor `none`, nor containing names absent from the UI. NOTHING HAPPENED AS A RESULT: the header, the .chat-members line and the sidebar entry are unchanged from before the turn (`Nova, echo, Jarvis` / `You, Nova, echo, Jarvis`), and the console still holds only the two pre-existing `Invited agent` lines - no new one. THE MODEL DID NOT CLAIM SUCCESS: it reported the failure plainly by quoting the tool, with no assertion that Zephyr was added. |

### APPTOOLS-15 — An agent's reply is delivered once — it must not also post the same text with post_message

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-15--an-agents-reply-is-delivered-once--it-must-not-also-post-the-same-text-with-post_message)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `e513d5da` |  | Run in Nova's own TWO-member direct Room (01a0a62048e07ba7a24d814a3dda9218), so no mention was needed and no budget was already spent. Asked `In one sentence, what is this application for?` and waited a further ten seconds after the streaming row disappeared. EXACTLY ONE NEW ROW FROM NOVA: the Room holds two rows in total - the human's question and `Team is a chat application for coordinating conversations between a human and multiple agents in shared Rooms.` - with no second, duplicate message seconds later. EXACTLY ONE NEW LINE IN STORAGE: the Room's .jsonl holds exactly two records, one per row, with distinct ids and no repeated text - so there is no double post at the storage layer either. THE BUDGET COUNTED IT ONCE: the grey note below the transcript reads `1 of 40 agent replies since you last spoke.` - not `2 of 40`, which is the tell that would show a double delivery even if the two rows differed slightly. So the Replying rule holds against a real model: the reply is just answer text and was not also posted with post_message. |

### APPTOOLS-16 — create_room makes a new Room that appears in the sidebar live, named after its Agents

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-16--create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `e513d5da` |  | THE SIDEBAR WAS RECORDED BEFORE SENDING, as the NOTE insists: echo, alpha, Nova, Jarvis, and two rooms named `Nova, echo, Jarvis` - six entries. Asked from Nova's own Room to `Use mcp__team__create_room to start a new room with Jarvis, then tell me the id it returned.` EXACTLY ONE NEW ENTRY APPEARED, with NO refresh (navigation entries still 1): the list went from six to seven, the new one named `Nova, Jarvis` - the calling Agent first, then the named one, and the human deliberately NOT in the name. Nova reported `The room id returned is: 01a0a6270a5a71eba6cad517d4283187`. THE ID IS REAL: clicking the new entry lands on /rooms/01a0a6270a5a71eba6cad517d4283187 - the same id, character for character. THE HUMAN IS A MEMBER: .chat-members reads `You, Nova, Jarvis`, three members, so the Room is mention-gated as intended. THE CONSOLE CONFIRMS IT RATHER THAN TAKING THE MODEL'S WORD: `Created room '01a0a6270a5a71eba6cad517d4283187' (Nova, Jarvis) with 3 members.` NOT the single-agent fall-through the Inconclusive clause warns about - Jarvis was named explicitly and a genuinely new Room was created rather than Nova's existing direct Room being returned. |

### APPTOOLS-17 — post_message delivers into a Room other than the one the agent was addressed in, and the agent does not answer its own post

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-17--post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `d1ace7e9` |  | Run with Team__AgentMessageBudget=2 set before launch, as the test's own cost guard requires - a loop caught at 2 agent messages proves the same defect as one caught at 40. One turn from Nova's direct Room did both halves. POST_MESSAGE REACHED THE OTHER ROOM, NOT THIS ONE: exactly ONE new sidebar entry appeared with no refresh (navigation entries 1), Nova's plain reply in the ORIGINAL Room is only the id `01a0a62902a277d798047928b330c286` as an ordinary message row, and NO message from any agent in that Room contains `kickoff` (the word appears there only inside the human's own instruction text, which is why the check was made per-row by sender rather than by searching the page). THE NEW ROOM HOLDS IT: /rooms/01a0a62902a277d798047928b330c286 - the id Nova reported, character for character - heading `Nova, Jarvis`, members `You, Nova, Jarvis`, and exactly one message row, sender `Nova`, body exactly `kickoff`. NO ECHO LOOP - the structural guard holds: the Room was watched for over 100 seconds with the page open; it still holds ONE row, no streaming row ever reappeared, and the .jsonl holds exactly ONE line whose senderName is `Nova` and did not grow across a 75-second file watch. Nova did not reply to its own post, and Jarvis did not either (correct - it is a three-member Room and Jarvis was not mentioned). The budget was never even reached: ZERO `refused a message` lines, so nothing had to be stopped. THE TRACE LOG SHOWS BOTH CALLS and nothing else: exactly two tools/call replies this run, `Created room 'Nova, Jarvis' (id 01a0a62902a277d798047928b330c286).` and `Posted to room '01a0a62902a277d798047928b330c286'.` |

### APPTOOLS-18 — Tools are the source of truth — the agent must not answer about agents or Rooms from the codebase

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-18--tools-are-the-source-of-truth--the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `10a1cc1b` |  | RUN WITH THE UNIQUE-NAME TRICK the INCONCLUSIVE note recommends, which turns this from a judgement call into a fact: a third Persona `Zellandine` (Archivist, @zell) was created first. `Zellandine` appears NOWHERE in docs/ or tests/ - a repo-wide grep matches only App_Data (the live Persona file I had just written), so no amount of grepping the checkout could produce it. UI TRUTH RECORDED FIRST: tiles `Jarvis \| Researcher · @jar \| Online`, `Nova \| Coordinator · @nova \| Online`, `Zellandine \| Archivist · @zell \| Online`; New chat checkboxes echo, alpha, Nova, Jarvis, Zellandine, all `agent-dot online`. TURN 1 (tools-only framing) MATCHES THE UI EXACTLY, both directions: `echo (online), alpha (online), Jarvis (online) - Researcher, alias: jar, Zellandine (online) - Archivist, alias: zell`, room members `You (the human), Nova (me)`, Personas `Jarvis, Zellandine (plus myself, Nova)` - every UI name present, none absent, none extra, and NO fixture name (no Alice, no Bob, no Chief of Staff, no `bananas`). TURN 2 (unconstrained) IS IDENTICAL IN CONTENT - the same five agents with the same titles and aliases - so the `second answer differs from the first` failure is absent. NEITHER ANSWER NAMES A FILE, A PATH OR A SHELL COMMAND, and neither quotes source. TURN 1'S TOOL CALL IS IN THE TRACE LOG, payload beginning `Agents:` and listing all five with Titles and Aliases. ONE PASS CONDITION WAS NOT OBSERVED AS WRITTEN, recorded openly: NO tools/call occurred during the SECOND turn (the log's reply count stayed at 2 across it). This is in-session reuse, not a codebase read - the list_agents output from turn 1 is still in Nova's own context one turn later, and the test issues turn 2 into that same session by design, so the condition is structurally unsatisfiable rather than failed. The same carry-over already invalidated a first attempt at APPTOOLS-07. A CONTROL RUN SETTLES WHAT THE CONDITION IS A PROXY FOR: the unconstrained question alone, put to `Zellandine` in its own two-member Room on a session that had never called a tool, DID drive the call - the reply count rose 2 -> 4 (get_help + list_agents, payload beginning `Agents:`) and the answer again matched the UI (`echo, alpha, Nova (Coordinator), and Jarvis (Researcher)` plus itself in the Room). So the unprompted phrasing does reach for the tools; Nova simply had no reason to ask twice. Recommend the test be amended to require the second turn on a fresh session, or to drop the second-turn log condition. |

### APPTOOLS-19 — A budgetExhausted refusal from post_message is terminal: the agent stops, does not retry and does not reroute

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-19--a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `10a1cc1b` |  | Run exactly as the NOTE demands - the app was STOPPED and relaunched with Team__AgentMessageBudget=2 set in the launching process, so this cost one turn rather than twenty, and nothing was restarted mid-test. The sidebar was written down first: 9 rooms (echo, alpha, Nova, Jarvis, `Nova, echo, Jarvis` x2, `Nova, Jarvis` x2, Zellandine). Sent from Nova's own TWO-member direct Room (01a0a62048e07ba7a24d814a3dda9218, members `You, Nova`), so no mention was needed. EXACTLY TWO AGENT ROWS LANDED, bodies exactly `one` and `two` - no third or fourth row anywhere in the Room. THE ALERT PANEL IS WORD FOR WORD what the test specifies: a `.budget-prompt` element reading `Agents have sent 2 replies since you last spoke, and are paused.` with the two buttons **Continue** and **Leave paused**, and it appeared with NO refresh (navigation entries 1). NO NEW ROOM - the sidebar still holds the same 9 entries with the same names, so the most important failure this test can find (minting a fresh Room with a full Budget to carry on in) did not happen. NOR DID IT REROUTE INTO AN EXISTING ROOM: every .jsonl in App_Data/rooms was parsed and NO record in ANY of the 5 files has the text `three` or `four`. THE REFUSAL IS TERMINAL AND WAS OBEYED FIRST TIME: the third post_message came back as readable TEXT with isError:false - `Could not post the message: This room has reached its budget of 2 agent messages since a human last spoke. Do not retry: further posts to this room will be refused until a human speaks here.` - and the log holds exactly THREE tools/call replies in total (two `Posted to room '01a0a62048e07ba7a24d814a3dda9218'.` then that one refusal), i.e. Nova stopped rather than attempting a fourth post. EXACTLY TWO refusal warnings, not a run: both read `Room '01a0a62048e07ba7a24d814a3dda9218' refused a message from 'Nova': its budget of 2 agent messages since a human last spoke is spent.` - one for the third post, one for Nova's own explanatory reply, which is the documented confusing-but-correct case and is also why no closing message from Nova appears in the transcript. `Persona 'Nova' had a message refused: budgetExhausted - …` is present as the test says it should be. THE FILE AGREES WITH THE SCREEN: the Room's .jsonl ends with exactly two agent-authored lines for this turn, `one` and `two`. NOT INCONCLUSIVE - the budget was genuinely reached (the model posted twice and was then refused; it did not stop of its own accord). THE TILE STAYS ONLINE: /agents still reads `Nova \| Coordinator · @nova \| Online`, NOT Degraded - a spent Budget is correctly not treated as a fault. |

### APPTOOLS-20 — A turn declined for Budget never reaches the model at all — the decline path is not the refusal path

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-20--a-turn-declined-for-budget-never-reaches-the-model-at-all--the-decline-path-is-not-the-refusal-path)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `10a1cc1b` |  | Run immediately after APPTOOLS-19 with Team__AgentMessageBudget=2 still in force and the app NOT restarted. **Leave paused** BEHAVED EXACTLY AS SPECIFIED: clicking it removed the red `.budget-prompt` panel and left a grey `.budget-note` reading, word for word, `Paused — 2 of 2 agent replies since you last spoke.`, with no refresh (navigation entries 1). THE SCRIPT'S STEPS 3-7 CANNOT BE PERFORMED AS WRITTEN, and finding out why is itself the interesting part: no message can arrive in an already-paused Room. The only mechanism that delivers one without a human speaking is an agent's post_message from elsewhere, and that is stopped one layer earlier by the REFUSAL path - asking `Zellandine` from its own Room to post `@Nova ping` into the paused Room produced `Room '01a0a62048e07ba7a24d814a3dda9218' refused a message from 'Zellandine': its budget of 2 agent messages since a human last spoke is spent.`, Zellandine reported that refusal back in its own Room, and the paused Room was watched for 69 seconds across 69 samples with ZERO streaming rows, ZERO tool-activity elements and no change from 13 rows. So the decline path is reachable only DURING the turn that spends the Budget, by a SECOND agent that is a Member of the same Room - which is what ReplyGate.Decide implies, since it is consulted per delivery and checks the Budget BEFORE the member-count/Mention rule. THAT CONDITION WAS THEN BUILT DELIBERATELY: a fresh three-member Room (`You, Nova, Jarvis`, id 01a0a636704678f4897aec23e334b60c, new Budget of 2) and one human message mentioning ONLY Nova - `@Nova Using mcp__team__post_message, post two separate short messages into this room, one at a time, with the texts "one" and "two".` Sampled every 400ms for 38 seconds. THE DECLINE PATH IS CLEAN: the ONLY sender ever to own a streaming row was `Nova`; `Jarvis` NEVER produced a draft row, never produced a tool-activity line (the only activity text seen all turn was `mcp__team__post_message`, never more than one at a time), and posted NOTHING - the Room ends with exactly three rows, `You`, `Nova: one`, `Nova: two`. THE WARNING IS THERE, at Warning level and exactly once: `warn: Agency.Huddle.App.Acp.PersonaRunner` / `Persona 'Jarvis' declined a turn in room 01a0a636704678f4897aec23e334b60c: the room has spent its budget of 2 agent messages.` ITS POSITION IN THE LOG PROVES THE ORDERING the test exists to protect: it sits BETWEEN the tools/call reply for `one` (id 5) and the reply for `two` (id 6) - i.e. Jarvis was handed the delivery that took the count to 2 and refused the turn before any prompt was built, rather than after the Room rule. NO PROMPT WAS SENT: no draft row, no tools/call attributable to Jarvis, and no post. THE PAUSED AGENT DID NOT ANNOUNCE ITS PAUSE, which is correct and is NOT recorded as a defect. Nova's own closing reply was then refused (`Room '…' refused a message from 'Nova'` + `Persona 'Nova' had a message refused: budgetExhausted - …`), the documented confusing-but-correct case. TWO DEVIATIONS RECORDED: (1) `echo` was deliberately NOT added to the Room - a third member would have made the decline ambiguous between the Budget and the Mention rule, and the whole point is to show the Budget wins; the second agent used instead is `Jarvis`, a real Persona that could otherwise have answered. (2) The test is labelled Free, but creating the condition at all costs one short Haiku turn (Nova's two posts) - nothing is spent on the DECLINED agent, which is exactly what the test asserts. |

### APPTOOLS-21 — Continue re-delivers the paused Message, and the prompt survives a page reload

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-21--continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `5547cd5f` |  | THE PANEL SURVIVES A FULL DOCUMENT LOAD, checked BEFORE any click as the NOTE insists: the room URL was loaded fresh and after 4.5s the `.budget-prompt` was still on screen reading `Agents have sent 2 replies since you last spoke, and are paused.` with **Continue** and **Leave paused** - so the prompt is read from the Room's stored state, not from a missed event, and the prerender path keeps it. A FIRST ATTEMPT WAS DISCARDED AND WHY IS THE INTERESTING PART: pressing Continue in the APPTOOLS-20 room granted the budget (`2 of 4 agent replies since you last spoke.`) but produced NO reply in 36 seconds. That is the documented INCONCLUSIVE case, and the cause is exact: ExtendBudgetAsync re-publishes `history[^1]` with `MentionParser.Parse` run AGAIN, so the held Message wakes whoever it Mentions - and that Room's last Message (`two`, from Nova) Mentioned nobody, which in a three-member Room is CatchUp, not Reply. Silence there is correct. The test was therefore re-staged so the held Message carries a Mention. DECISIVE RUN: a fresh three-member Room (`You, Nova, Jarvis`, id 01a0a639379773118b828393580223d6, Budget 2), one human message Mentioning only @Nova, which drove Nova to post `one` and then `@Jarvis use mcp__team__post_message to post two short messages into this room, the first exactly three and the second exactly four.` - the second post took the count to 2, so `Persona 'Jarvis' declined a turn in room …: the room has spent its budget of 2 agent messages.` and the panel appeared. THE DOUBLE-CLICK GUARD IS PROVEN HARDER THAN THE TEST ASKS: **Continue** was clicked THREE times in a single synchronous tick, before Blazor could re-render and disable it - and the log holds EXACTLY ONE `Room '01a0a639379773118b828393580223d6' was extended to 4 agent messages.` line. One intent, one grant. (Two guards stand behind that: the `disabled="@this.extending"` binding AND a re-entrancy check at the top of `ContinueAsync`; the three clicks exercised the second, which is the one that survives a race.) THE RE-DELIVERY WORKS WITH NO HUMAN MESSAGE TYPED: the only streaming sender after the press was `Jarvis`, and the Room gained `Jarvis :: three` and `Jarvis :: four` - Jarvis did the thing it had been paused on, and nothing was typed into the Room between the pause and the reply. NO DUPLICATION AND NO SELF-QUOTING: Jarvis's two messages are exactly `three` and `four`; it did not answer twice, did not quote Nova's Message back, and did not restate the instruction - so the held Message reached the prompt once, which is exactly why a Message declined for Budget is NOT also buffered as catch-up. THE ROOM HALTS AGAIN ONE BUDGET LATER, not indefinitely: the grant was bounded at 4 (`extended to 4 agent messages`, note `2 of 4`), Jarvis's two posts took it to 4, the alert panel returned, and `Persona 'Nova' declined a turn in room …: the room has spent its budget of 4 agent messages.` shows the new cap being enforced at the higher figure. ONE PASS CONDITION IS NOT OBSERVABLE AND IS NOT A DEFECT, recorded openly: the button never reads `Continuing…`. `ContinueAsync` sets and clears `extending` inside one handler invocation with no intervening StateHasChanged, and ExtendBudgetAsync is an in-memory dictionary update, so Blazor's next render happens only after the flag is false again - by which time `budget.Exhausted` is false and the whole panel has gone (measured: present at t=0, gone by t=144ms, never showing the intermediate label). The label is live code that would show against a slow store; the protection it stands for is what the three-click result proves. |

### APPTOOLS-22 — Editing a prompt changes model-facing text without restarting the session, and Next session prompts wait for a restart

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-22--editing-a-prompt-changes-model-facing-text-without-restarting-the-session-and-next-session-prompts-wait-for-a-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Fail *(earlier script)* | `4ee6f119` | pending-issues.md #1 | FAILS ONE PASS CONDITION: a **Next session** hook is served to a model with no restart, because get_help renders it live. Everything else in this test passes. TWO DIFFERENT SENTINELS WERE USED, the deviation that made the finding visible at all: `The word of the day is marmalade.` appended to the LIVE **Help: budget** field, `The word of the day is quince.` appended to the **Next session** **list_agents description** field. The test scripts `marmalade` for both, and with one word the INCONCLUSIVE clause swallows the result - the model may simply be repeating the live hook it read a turn earlier. THE PROOF IS THE WIRE PAYLOAD, NOT THE MODEL'S ANSWER, and that distinction matters here: the Trace log's `App tool server replied to tools/call:` for get_help contains, inside its TOOLS catalog, `mcp__team__list_agents … The word of the day is quince.` - the edited Next-session text, served by the tool server minutes after the save with no restart in between. THE MECHANISM IS IN THE CODE: every tool exposes `public string Description => this.hooks.Render("tool.<name>.description", NoValues)`, and GetHelpTool.BuildHelp() runs per call and reads each tool's Description at that moment, so the hook has two readers with two lifetimes - MCP tools/list, read once at session start, which DOES honour the badge (all three tools/list payloads in this run carry the ORIGINAL description; zero contain `quince`), and get_help's catalog, which does not. The second is the surface that decides what a model knows, because progressive discovery means the system prompt names tools without describing them: asked the same question, `Jarvis` - whose session had not called get_help since the edit - correctly answered that it does not have the description in its context at all. THE SAVE ITSELF RESTARTS NOTHING: no session/create appears anywhere in the log between startup and the two turns following the save, all three tiles stayed **Online**, and Nova answered `Pumpkin.` to `What word did I ask you to remember?`. THE LIVE HOOK IS LIVE: on the very next get_help call Nova quoted `The last line of the BUDGET section is: "The word of the day is marmalade."`, matching the payload. HOOKS.JSON HELD EXACTLY THE TWO CHANGED KEYS, `getHelp.budget` and `tool.listAgents.description`. AFTER A RESTART the Next-session hook is reported too (`The word of the day is quince.`), so the badge's direction works - it is simply not exclusive. ONE THING I CANNOT CLAIM, recorded rather than glossed: Nova answered `quince` from the UI as well, but the log shows TWO unexplained session replacements later in this run (sessionIds db631773 and bfd7205b, each followed by `The agent process disconnected.`, the first arriving right after a `status_category":"blocked"` turn summary), so I cannot prove WHICH session produced that answer and the verdict does not rest on it. That adapter churn is itself worth watching - it is not settings-driven and nothing in the UI showed it. THREE DEVIATIONS: (a) the room named `Nova` is no longer two-member - it reads `You, Nova, Zellandine` - so it is mention-gated and every message here was prefixed `@Nova`; unprefixed messages are correctly context-only and drew no reply, which cost two false starts. (b) Step 15's **Restart** button does not exist on the agent card, which offers only **Edit**, **Open** and **Remove**; the restart was done with **Edit** -> **Save**, which the test itself names as legitimate, and it worked. Filed as pending-issues.md #2. (c) Nova also wrote to its own memory directory during the pumpkin turn and posted its reply twice (once as answer text, once via post_message) - neither is judged by this test. CLEANUP DONE: both fields Reset and Saved, hooks.json back to `{}`. |
| 2026-09-15 | Fail *(earlier script)* | `b6493ee3` | #31 | FAILS ONE PASS CONDITION: a **Next session** hook is served to a model with no restart, because get_help renders it live. Everything else in this test passes. TWO DIFFERENT SENTINELS WERE USED, the deviation that made the finding visible at all: `The word of the day is marmalade.` appended to the LIVE **Help: budget** field, `The word of the day is quince.` appended to the **Next session** **list_agents description** field. The test scripts `marmalade` for both, and with one word the INCONCLUSIVE clause swallows the result - the model may simply be repeating the live hook it read a turn earlier. THE PROOF IS THE WIRE PAYLOAD, NOT THE MODEL'S ANSWER, and that distinction matters here: the Trace log's `App tool server replied to tools/call:` for get_help contains, inside its TOOLS catalog, `mcp__team__list_agents … The word of the day is quince.` - the edited Next-session text, served by the tool server minutes after the save with no restart in between. THE MECHANISM IS IN THE CODE: every tool exposes `public string Description => this.hooks.Render("tool.<name>.description", NoValues)`, and GetHelpTool.BuildHelp() runs per call and reads each tool's Description at that moment, so the hook has two readers with two lifetimes - MCP tools/list, read once at session start, which DOES honour the badge (all three tools/list payloads in this run carry the ORIGINAL description; zero contain `quince`), and get_help's catalog, which does not. The second is the surface that decides what a model knows, because progressive discovery means the system prompt names tools without describing them: asked the same question, `Jarvis` - whose session had not called get_help since the edit - correctly answered that it does not have the description in its context at all. THE SAVE ITSELF RESTARTS NOTHING: no session/create appears anywhere in the log between startup and the two turns following the save, all three tiles stayed **Online**, and Nova answered `Pumpkin.` to `What word did I ask you to remember?`. THE LIVE HOOK IS LIVE: on the very next get_help call Nova quoted `The last line of the BUDGET section is: "The word of the day is marmalade."`, matching the payload. HOOKS.JSON HELD EXACTLY THE TWO CHANGED KEYS, `getHelp.budget` and `tool.listAgents.description`. AFTER A RESTART the Next-session hook is reported too (`The word of the day is quince.`), so the badge's direction works - it is simply not exclusive. ONE THING I CANNOT CLAIM, recorded rather than glossed: Nova answered `quince` from the UI as well, but the log shows TWO unexplained session replacements later in this run (sessionIds db631773 and bfd7205b, each followed by `The agent process disconnected.`, the first arriving right after a `status_category":"blocked"` turn summary), so I cannot prove WHICH session produced that answer and the verdict does not rest on it. Those extra sessions are the documented model/effort catalog probe, not adapter churn - proven by reproducing the same triple with Team:Acp:Enabled=false, where no Persona session exists. THREE DEVIATIONS: (a) the room named `Nova` is no longer two-member - it reads `You, Nova, Zellandine` - so it is mention-gated and every message here was prefixed `@Nova`; unprefixed messages are correctly context-only and drew no reply, which cost two false starts. (b) Step 15's **Restart** button does not exist on the agent card, which offers only **Edit**, **Open** and **Remove**; the restart was done with **Edit** -> **Save**, which the test itself names as legitimate, and it worked. Filed as #32. (c) Nova also wrote to its own memory directory during the pumpkin turn and posted its reply twice (once as answer text, once via post_message) - neither is judged by this test. CLEANUP DONE: both fields Reset and Saved, hooks.json back to `{}`. |

### APPTOOLS-23 — follow_room wakes an agent that was not mentioned, and a non-follower in the same Room is the control

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-23--follow_room-wakes-an-agent-that-was-not-mentioned-and-a-non-follower-in-the-same-room-is-the-control)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-24 — unfollow_room restores the ordinary mention-only rule, and the Room confirms the decline was not a silent drop

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-24--unfollow_room-restores-the-ordinary-mention-only-rule-and-the-room-confirms-the-decline-was-not-a-silent-drop)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-25 — Following does not buy a Turn past the Budget

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-25--following-does-not-buy-a-turn-past-the-budget)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-26 — create_room with seed posts the opening Message as part of creation

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-26--create_room-with-seed-posts-the-opening-message-as-part-of-creation)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-27 — get_help carries a TASKS section while the Task tools are offered

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-27--get_help-carries-a-tasks-section-while-the-task-tools-are-offered)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-28 — A prefix-less Adapter is told tool names bare (ADR-0014)

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-28--a-prefix-less-adapter-is-told-tool-names-bare-adr-0014)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### APPTOOLS-29 — search finds only an Agent's own Rooms until it asks for all (ADR-0066)

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-29--search-finds-only-an-agents-own-rooms-until-it-asks-for-all-adr-0066)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-28 | Pass | `75d43fb9` + uncommitted `feat/agent-search-tool` | | Isolated `uat` seed under `C:\seeds\huddle\paid-search`, Release build, Trace, claude-agent-acp, Ada (Team `Design`) and Bo on Haiku (session `message.model` all `claude-haiku-4-5-20251001`; Haiku offers no effort levels, so Effort stayed `Agent default`). Other Room: Bo's one-to-one with `Remember the zebra-ledger-7 invoice is due on Friday.` Turn 1: after a `ToolSearch` load, the model sent `mcp__team__search {"query":"zebra-ledger-7"}` (no scope); reply began `Rooms searched: yours and your Teams' only. Pass scope "all" to search every Room.` and listed only Ada's own Room (1 match, the question itself); Ada said she found nothing. No `searched every Room` line. Turn 2 `Search every Room for it.`: `{"query":"zebra-ledger-7","scope":"all"}`, header `Rooms searched: every Room.`, Bo's Room listed with the message, Ada named Bo's Room; console `Agent Ada searched every Room for 'zebra-ledger-7'.` appeared once. O-TOOLCALLS `arguments` is empty for this tool, so the input came from the Adapter session file. Two turns. |

### APPTOOLS-30 — the default search also reads a Room scoped to the Agent's own Team that it is not a Member of (ADR-0066)

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-30--the-default-search-also-reads-a-room-scoped-to-the-agents-own-team-that-it-is-not-a-member-of-adr-0066)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-28 | Pass | `75d43fb9` + uncommitted `feat/agent-search-tool` | | Same lane. Team chat `Bo, vendor chat` scoped to `Design`, members the Human and Bo (Ada absent), message `The quokka-ledger-9 vendor list is final.` The first attempt in Ada's old Room is not evidence: that session had already passed `scope:"all"` and did so again (the Inconclusive case, recorded in the test). In a fresh one-to-one with Ada: `{"query":"quokka-ledger-9"}` (no scope), header `yours and your Teams' only`, the Team chat listed (`1 match`, sender You) beside Ada's own Rooms; Ada named `Bo, vendor chat`. No `searched every Room` line for that call (only the two earlier calls logged). Fixture note: the Team chat and its `room_teams` row were created by a direct write to `team.db` (the New chat panel was not driven), so the Team chat's creation path is not part of this result. One turn. |

### APPTOOLS-31 — a Task id and a Library path from search are usable by the next tool call

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-31--a-task-id-and-a-library-path-from-search-are-usable-by-the-next-tool-call)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-28 | Pass | `75d43fb9` + uncommitted `feat/agent-search-tool` | | Same lane, one turn, Haiku. Task `DESI-0001` (`Order the narwhal-ledger-5 banners`, assignee Bo here, not Jarvis as the test says; the test text was written for Jarvis and is a fixture choice) and `Teams\Design\launch-plan.md` written by hand into the data folder. Calls: `search {"query":"narwhal-ledger-5","filter":"tasks"}` replied `Tasks:\n- DESI-0001 \| To Do \| Bo \| Design \| Order the narwhal-ledger-5 banners`; `search {"query":"pelican-ledger-3","filter":"library"}` replied `Library:\n- teams:Design/launch-plan.md\n    The pelican-ledger-3 checklist is owned by Design.`; `get_task {"taskId":"DESI-0001"}` replied `DESI-0001 \| To Do \| Medium \| Bo \| Design \| Order the narwhal-ledger-5 banners`. No `Rooms searched` header on either filtered call. Ada's answer: DESI-0001, Bo, To Do, `teams:Design/launch-plan.md`. |

### APPTOOLS-32 — get_help lists the search tool while the system prompt names none

File: [`docs/engineering/manual-tests/app-tools.md`](app-tools.md#apptools-32--get_help-lists-the-search-tool-while-the-system-prompt-names-none)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-28 | Pass | `75d43fb9` + uncommitted `feat/agent-search-tool` | | Same lane, one turn, Haiku. O-TOOLCALLS has `mcp__team__get_help` for the turn. The decoded reply (12,446 characters) holds the TOOLS entry `mcp__team__search` with the full description including `By default Rooms are limited to those you are a Member of and those scoped to one of your Teams. Pass scope 'all' only when you genuinely need another Room's history ... every such search is logged`. Ada quoted the entry. The free mock adapter cannot show this (it never calls an App Tool), so there is no free variant. |

## Persona lifecycle: supervisor, work dirs, health and restarts

### PERSONALIFECYCLE-01 — With ACP disabled every agent reads Offline with an empty tooltip and no Room raises an alert

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-01--with-acp-disabled-every-agent-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-14 | Pass *(earlier script)* | `f034c32a` |  | Re-run 2026-09-14: `Nova` created Haiku/low, card and tile read Offline with no tooltip and no reason line, no `Nova` Room, no alert strip, adapters 0 before and after, `work` empty. Corrected step 17 (`hello there @echo`) drew the `**echo:**` reply. |

### PERSONALIFECYCLE-02 — A Persona Name with a leading, trailing or doubled space is refused on the card and never written to disk

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-02--a-persona-name-with-a-leading-trailing-or-doubled-space-is-refused-on-the-card-and-never-written-to-disk)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `b3608dd3` |  | VALIDATION RUNS BEFORE THE WRITE. All THREE bad Names were refused with the card left open and an inline red message: ` Nova Two` -> `' Nova Two' is not a valid Persona name.`; `Nova Two ` -> `'Nova Two ' is not a valid Persona name.`; `Nova  Two` -> the same message naming the doubled-space value. NOTHING THE TESTER TYPED WAS THROWN AWAY: after every refusal the Title still read `Test agent`, the Alias still `novatwo` and the Persona body still `You are Nova Two.` THE ERROR PATH REPORTS RATHER THAN THROWS: no browser alert box, and `An unhandled error has occurred.` never appeared. THE LEGITIMATE NAME IS ACCEPTED: `Nova Two` with exactly one interior space produced no error, the card switched to the `Agent` view showing `Nova Two`, and a tile `NT \| Nova Two \| Test agent · @novatwo \| Offline` appeared - so the rule is not over-tightened and a display Name like `Chief of Staff` stays possible. THE DIRECTORY GAINED EXACTLY ONE FILE: walking App_Data\Teams recursively gives `Nova.md` and `Nova Two.md` and nothing else; a programmatic check for any filename with a leading, trailing or doubled space returns an empty list - so none of the three refusals left a file behind. ORACLE NOTE: the doubled-space case's on-screen message reads as a single space when extracted via innerText, because HTML collapses consecutive whitespace when rendering; the value submitted was verifiably `Nova  Two` and the refusal is what matters. |

### PERSONALIFECYCLE-03 — Browsing /agents spawns no adapter; opening a card spawns exactly one, and reopening spawns none

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-03--browsing-agents-spawns-no-adapter-opening-a-card-spawns-exactly-one-and-reopening-spawns-none)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `39435ef2` |  | Timed with a 400ms background poller over the O-ADAPTERS filter, so every row counted is a node.exe whose CommandLine contains claude-agent-acp - the 18 unrelated node processes on this machine are excluded by construction, which the Inconclusive clause requires. RUN ON A FRESH APP PROCESS: the first attempt had to be discarded because earlier tests in the same run had already populated the catalog, which is itself the caching this test checks; the app was restarted so the first card-open really was the first. NAVIGATING, RELOADING AND FILTERING SPAWN NOTHING: from the poller's start at 01:14:03 through three full loads of /agents and a Team-filter round trip, the count stayed 0 until 01:14:18 - and the card was opened at 01:14:17. (The Team filter was rendered but offers only `All teams` because no Persona has a Team, so the filter change was a no-op; recorded rather than skipped.) ONE CARD-OPEN SPAWNS EXACTLY ONE PROBE, AND IT DIES: the count went 0 -> 1 at 01:14:18.6 (pid 87248) and back to 0 at 01:14:23.0 - about 4.4 seconds, far inside the 25-second bar, and it NEVER reached 2, so the Effort ladder is not spawning a second session. THE LOADING HINT APPEARS AND CLEARS IN TIME: `Reading the models this agent offers…` was sampled at 01:14:20 and 01:14:22 and was gone by 01:14:24 - about 7 seconds after the card opened, well inside 20. ONCE SETTLED the Model select lists Use the agent's default, Default (recommended), Opus (1M context), Fable, Sonnet, **Haiku**, and the Effort select lists Use the agent's default, **Low**, Medium, High, Xhigh, Max. THE SECOND CARD-OPEN IS SERVED FROM CACHE: after Cancel and a re-open at 01:14:56, NO loading hint appeared in ten samples over 14 seconds and the poller recorded NO new process at any point after 01:14:23 (max count for the whole 71-sample run is 1). ONE WORDING NOTE: the test expects the Effort hint to read `Reading the effort levels this model offers…` during load; with no Model yet chosen it instead reads `This model offers no effort choice…`. That string does appear - it was observed in APPTOOLS-03 immediately after selecting Haiku - so the hint exists and is shown when an effort probe actually runs. |

### PERSONALIFECYCLE-04 — A missing Adapter degrades the Model and Effort pickers with a plain-language hint, and restoring it works with no app restart

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-04--a-missing-adapter-degrades-the-model-and-effort-pickers-with-a-plain-language-hint-and-restoring-it-works-with-no-app-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `39435ef2` |  | Run on an app process STARTED with the adapter already renamed aside, because a catalog cached by an earlier successful probe would have masked the whole test (the first attempt showed exactly that and was discarded). THE PAGE DEGRADES, IT DOES NOT CRASH: with tools\acp\node_modules renamed to node_modules.off, /agents renders completely - heading `Agents`, the `New agent` button, and both existing tiles (Nova, Nova Two) - with no `Files that didn't load` block and no `An unhandled error has occurred.` THE PICKERS SAY SO IN WORDS: the Model select contains EXACTLY ONE option, `Use the agent's default`, with the hint reading exactly `This agent advertises no models, so it will use its own default.`; the Effort select likewise has exactly one option with the hint `This model offers no effort choice, so it will think as it normally does.` Neither hint was left on `Reading the models this agent offers…` - both had settled within the 22-second wait, so the probe timeout fires. SAVING STILL WORKS: `Orphan` was created with no error at all, the card switched to the `Agent` view showing Orphan, its MODEL section reads `Agent default` and its EFFORT section reads `Model default`, and a tile `O \| Orphan \| No adapter · @orphan \| Offline` appeared. THE LOG SAYS WHY, at Information: `info: Agency.Huddle.App.Acp.ModelCatalogProbe[0]` / `No ACP adapter is installed; the model catalog is empty.` NO PROBE PROCESS WAS SPAWNED while the folder was aside - the poller's maximum count across the whole window is 0. THE FAILURE IS NOT CACHED: after renaming node_modules back (verified, including …\@agentclientprotocol\claude-agent-acp\dist\index.js) and reopening Orphan's Edit card with NO app restart, the Model select lists the real roster again - Default (recommended), Opus (1M context), Fable, Sonnet, **Haiku** - and the Effort select lists Low through Max. |

### PERSONALIFECYCLE-05 — Restart with Team:Acp:Enabled=false starts nothing and says why on the card

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-05--restart-with-teamacpenabledfalse-starts-nothing-and-says-why-on-the-card)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4f8d9eda` |  | OUTCOME (A) IS WHAT THIS BUILD DOES, recorded as a measurement: **the Restart button starts a real adapter even with Team:Acp:Enabled=false.** The app was launched with the flag false (and started no adapters at boot - the poller's baseline is 0). `Nova`'s card read `Offline` with a `Restart` button in the action row. ONE CLICK at 01:18:29 produced EXACTLY ONE new persistent node.exe row matching claude-agent-acp: pid 54492, appearing at 01:18:30.2 - about one second later - and STILL ALIVE 47 seconds later at the end of the poll. The maximum count across the whole run is 1, so no duplicate start slipped past the supervisor's guard. The status went from `Offline` to `Online` and the `Restart` button DISAPPEARED from the card (it is offered only for an unhealthy agent), so the button did not stick on `Restarting…` and nothing is left disabled. No `An unhandled error has occurred.` banner at any point, and no reason line. THE AGENT IS NOW GENUINELY LIVE IN A CONFIGURATION THE OPERATOR SWITCHED OFF - which the Inconclusive clause says is THE FINDING to report, not a bug to fix on the spot: reopening the card now shows a `Message` link (action row Message, Edit, Open, Remove), a Room named `Nova` has appeared in the sidebar, and the app logged `Created direct room '01a0a425679973afb1abfa670e15d8ac' for agent 'Nova'.` So the next Message typed in that Room would spend real money despite Team:Acp:Enabled=false. Consistent with the NOTE: the flag is consulted where the supervisor starts everything at boot, and the manual restart path never sees it. THE BADGE DID NOT LIE - `Online` coincided with a real live process. NOTHING WAS ORPHANED: after stopping the app, the adapter count is 0 and node.exe is back to the same 18 unrelated processes. |

### PERSONALIFECYCLE-06 — A Persona added while the app is running comes Online and gets a Room named after it, with no page refresh

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-06--a-persona-added-while-the-app-is-running-comes-online-and-gets-a-room-named-after-it-with-no-page-refresh)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `cbe5c4da` |  | Run from a clean App_Data on a fresh app process with NO Team__ variables set, so nothing was inherited. Adapter baseline before the Persona existed: ZERO. THE FULL BADGE WALK WAS CAPTURED with a MutationObserver rather than by eye, timed from the `Add agent` click: **Offline at +13ms, Starting at +14ms, Online at +1625ms** - all three states, in that order, reaching Online in 1.6 seconds against a 30-second bar. Seeing `Starting` at all proves both that the pipe registered and that health reported it. The card switched to the `Agent` view showing `Nova` immediately, and the tile reads `N \| Nova \| Test agent · @nova \| Online` with class `agent-dot online` computing rgb(46,158,68). THE ROOM APPEARED WITH NO REFRESH: the sidebar went from [alpha, echo] to [alpha, echo, **Nova**] with performance navigation entries still 1. EXACTLY ONE tile and EXACTLY ONE Room, and the adapter count went 0 -> 1 - one persistent node.exe matching claude-agent-acp, never two, so the duplicate-start race did not fire. THE FILE IS EXACTLY AS SPECIFIED: `---`, `name: 'Nova'`, `title: 'Test agent'`, `alias: 'nova'`, `---`, then `You are Nova. Answer in one short sentence.` verbatim. THE LOG carries `Created direct room '01a0a4280a9171bfbaabf56914b0c092' for agent 'Nova'.` and contains NO `failed to start` line for any Persona. O-DB WAS CHECKED (a .NET helper stands in for the missing sqlite3): `users` holds `01a0a4280a8f7df1bd25d7b2e4afffd1 \| Nova \| agent`, `rooms` holds `01a0a4280a9171bfbaabf56914b0c092 \| Nova` - the same id as the log line - and `persona_models` holds `Nova \| haiku`. THAT IS THE ID, NOT THE LABEL: the picker's option value is `haiku` while its display label is `Haiku`, and this build's adapter advertises short slugs (haiku, sonnet, default, opus[1m], claude-fable-5-1[1m]) rather than the longer `claude-haiku-…` form the condition's example assumes. Empirically confirmed too - the start succeeded and the agent is Online, so the stored string IS in the advertised catalog and no `not in the agent's advertised model catalog` line appears. (Step 7's Effort `low` could not be chosen: with Haiku selected the Effort control offers only `Use the agent's default`.) |

### PERSONALIFECYCLE-07 — A definition that is renamed, or that sits outside its Agent's folder, is rejected with a reason, and restoring it brings the Agent back

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-07--a-definition-that-is-renamed-or-that-sits-outside-its-agents-folder-is-rejected-with-a-reason-and-restoring-it-brings-the-agent-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `cbe5c4da` |  | IDENTITY COMES FROM THE FRONTMATTER, NOT THE PATH. Before the rename, App_Data\work contained exactly one entry, a folder named exactly `Nova`, and it was EMPTY (checked with -Force, so hidden entries count). Renaming Teams\Nova.md to nova-renamed.md, then waiting 12 seconds without touching the browser: THE TILE IS STILL `N \| Nova \| Test agent · @nova \| Online` - not renamed to `nova-renamed`, and it did not disappear, so the recursive watcher saw the rename and the Persona stayed visible. THE CARD FOLLOWED THE FILE: its PERSONA FILE section now reads `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\nova-renamed.md`, and that is the only .md path anywhere on the page - so the card shows the new path while the identity is unchanged. THE ROOM named `Nova` is still in the sidebar, and the page was never reloaded (navigation entries still 1). THE WORK DIR DID NOT FOLLOW THE FILENAME: after the rename App_Data\work still contains exactly `Nova` and NO folder named `nova-renamed` exists - checked both after the rename and again after the restore, including across the restart the rename triggers - so the Work Dir key is not path-derived and nothing the previous session wrote would be stranded. It is under App_Data, not the repository root or the bin folder. The frontmatter in the renamed file still reads `name: 'Nova'`, and the file was renamed back to Nova.md with the adapter still at exactly 1. |

### PERSONALIFECYCLE-08 — A Name containing spaces works for the monogram, the Room and the Work Dir

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-08--a-name-containing-spaces-works-for-the-monogram-the-room-and-the-work-dir)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4ced2ca1` |  | NOTHING IN THE PIPELINE TRUNCATES AT A SPACE. `Chief of Staff` (Title `Keeps the team honest`, Alias `cos`, Haiku) was accepted without complaint - so the interior-space rule from PERSONALIFECYCLE-02 holds here too. THE MONOGRAM READS `CS` - the first letter of the FIRST word and of the LAST word, not `CO` from the first two words, so the connective `of` correctly gets no letter. The tile reads `CS \| Chief of Staff \| Keeps the team honest · @cos \| Online`, with the full name intact, and it reached Online on the same badge walk as Nova: **Offline at +19ms, Starting at +26ms, Online at +1687ms** - captured with a MutationObserver from the `Add agent` click, so it neither stalled at Starting nor skipped it. THE FULL NAME SURVIVES EVERY SURFACE, spaces intact and nothing cut at `Chief`: the sidebar gained a Room named exactly `Chief of Staff` (with NO page refresh - navigation entries still 1); App_Data\work gained a folder named exactly `Chief of Staff` (alongside `Nova`); App_Data\Teams gained a file named exactly `Chief of Staff.md`; and the database's `rooms` table holds the row `Chief of Staff` in full. O-DB was checked with a .NET helper standing in for sqlite3. NOVA WAS NOT DISTURBED: it is still Online, and the adapter count is now exactly 2 - one per Persona - so the spaces in the Work Dir path were passed to the adapter process correctly rather than splitting the command line. |

### PERSONALIFECYCLE-09 — A missing Adapter reports the same actionable reason on the tile, the card and the Room strip, and harms no other agent

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-09--a-missing-adapter-reports-the-same-actionable-reason-on-the-tile-the-card-and-the-room-strip-and-harms-no-other-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `1ccc7a0e` |  | ROUTE NOTE FIRST, because step 2 could not be followed literally: with `Nova` HEALTHY the card offers no `Restart` button at all (Message, Edit, Open, Remove only) - that is PERSONALIFECYCLE-11's rule - so the agent was first made unhealthy by killing its adapter process, which put `Restart` on the card, and THEN Restart was clicked with tools\acp\node_modules renamed aside. That reaches exactly the state this test describes. ALL THREE SURFACES AGREE, WORD FOR WORD. The `Nova` card reads `Offline` and the reason on the page is exactly `No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.` The `Nova` TILE reads `Offline` and its status tooltip carries that IDENTICAL string (read from the title attribute). The `Nova` ROOM shows a red strip above the composer whose text is `Nova is Offline: No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 …`, rendered as .member-health-alert / -list / -item. The reason names an action, not an exception type. THE FAILURE IS CONTAINED: `Chief of Staff` is still `Online`, its Room shows ZERO alert strips and zero visible alert-classed elements, and its node.exe row is still in the adapter list (count 1). The `echo` Room - which Nova is not a member of - also shows NO strip, so the strip is listing by membership. `An unhandled error has occurred.` never appeared, and the Restart button came back enabled rather than sticking. THE WORK DIR SURVIVES: App_Data\work still holds both `Nova` and `Chief of Staff`, so the directory really is created before the adapter is located and a tester can still tell `the adapter is missing` from `the Persona never started`. THE LOG names ONE Persona only: exactly ONE `Persona 'Nova' failed to start.` warning. O-DB confirms registration happened - `users` still lists alpha, Chief of Staff, echo, Nova, You - so only the adapter launch failed. Adapter folder left renamed aside for PERSONALIFECYCLE-10. |

### PERSONALIFECYCLE-10 — Restart on the Agent card recovers a failed Persona with no app restart, and one click starts one adapter

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-10--restart-on-the-agent-card-recovers-a-failed-persona-with-no-app-restart-and-one-click-starts-one-adapter)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `1ccc7a0e` |  | THE FAILED ATTEMPT IS HARMLESS: clicking `Restart` while tools\acp\node_modules was still renamed aside left the card on `Offline` with the SAME reason, unchanged word for word - `No ACP adapter is installed for Persona 'Nova'. Run tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.` on both the card and the tile tooltip - produced NO error banner (`An unhandled error has occurred.` absent), and left the button ENABLED rather than stuck. THE SUCCESSFUL ATTEMPT, after renaming node_modules back and confirming …\claude-agent-acp\dist\index.js is present. Captured with a MutationObserver rather than by eye: the button read **`Restarting…` and was DISABLED** 3ms after the click, then vanished from the card 1ms later as the agent recovered. THE STATUS WALKED `Offline` -> `Starting` (at +5ms) -> `Online` (at +1548ms) - about 1.5 seconds against a 40-second bar - and the reason line disappeared at the same moment (tracked as a separate flag in the timeline; it goes false and stays false once Online). EXACTLY ONE NEW ADAPTER: before the click the list held only Chief of Staff's pid 13592 (created 01:24:31); afterwards it holds 13592 plus ONE new row, pid 48356 with a fresh CreationDate of 01:27:37. Never two, so the restart guard serialised. THE ROOM STRIP CLEARED WITHOUT NAVIGATION: the `Nova` Room now shows ZERO .member-health-alert elements and no visible alert-classed element at all, with performance navigation entries still 1 before and after - no reload. NO NEW FAILURE WARNING after the successful click: the log holds exactly two `Persona 'Nova' failed to start.` lines, one from PERSONALIFECYCLE-09 and one from this test's deliberate step-1 click, both before the success, and none after. |

### PERSONALIFECYCLE-11 — Restart is offered in every state except Starting, and is accompanied by the memory-loss warning

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-11--restart-is-offered-in-every-state-except-starting-and-is-accompanied-by-the-memory-loss-warning)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `be0b3375` |  | FOR THE HEALTHY AGENT the action row is EXACTLY `Message, Edit, Open, Remove` - no `Restart` button anywhere on the card - and there is NO hint line under the row (a search for `Like saving an edit…` returns nothing). So a working session's memory cannot be thrown away for no reason. AFTER KILLING Nova's adapter process by pid (route recorded: killed the process directly, having attributed it by CreationDate - the newest row, created at the moment Nova came Online - rather than using the rename-aside fallback), the SAME card shows `Message, Edit, Open, Restart, Remove` with **Restart between Open and Remove**, and the line under the action row reads EXACTLY `Like saving an edit, this restarts the agent, which clears what it remembers.` THE GATE AND THE BADGE AGREE: Restart appears only while the status reads `Offline`, never alongside `Online`, judged from the text label rather than the dot colour as the NOTE advises. `Message` is present because this agent HAS connected and has a Room - it is not being offered for a Room that does not exist. |

### PERSONALIFECYCLE-12 — Killing the adapter process flips the badge to Offline with a loop reason, even though the pipe stays open

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-12--killing-the-adapter-process-flips-the-badge-to-offline-with-a-loop-reason-even-though-the-pipe-stays-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `be0b3375` |  | THE HEADLINE SILENT FAILURE IS ABSENT - killing the adapter does NOT leave the badge reading Online. With /agents in one tab and the `Nova` Room in another, both instrumented with MutationObservers and neither touched afterwards: **3.14 seconds** after `Stop-Process` the Nova tile changed from `Online` to `Offline` with no interaction at all, and 3.54s after the kill the Nova Room grew a red strip - so the health event repaints open surfaces live, in both tabs, with navigation entries still 1 (no reload). THE REASON NAMES THE LOOP, on every surface: the tile's status tooltip and the card both read `Its event reader ended unexpectedly: The agent process disconnected.`, and the Room strip reads `Nova is Offline: Its event reader ended unexpectedly: The agent process disconnected.` (it later settles to `The Adapter process disconnected.`). So a killed adapter is distinguishable from a never-started one. THE LOG AGREES: `warn: Agency.Huddle.Acp.DotAcp.DotAcpAgentHost` / `The agent process disconnected.` and `warn: Agency.Huddle.App.Acp.PersonaRunner` / `Persona 'Nova' agent event stream ended unexpectedly.` with an AgentDisconnectedException beneath. CONTAINED: `Chief of Staff` stayed `Online` throughout and its Room shows ZERO alert strips. THE DEAD ROOM DOES NOT HANG - the bug that has shipped once before: typing `are you there` posted the human row normally and then, over a full 35 seconds, produced NO reply, ZERO `.message-row.streaming` rows (no draft spinning forever), no error text, and the page stayed responsive. RESTART RECOVERS IT: the tile went back to `Online` and the adapter list gained a fresh row, pid 98588 created 01:30:18, alongside Chief of Staff's unchanged pid 13592. THE ROOM AND TRANSCRIPT SURVIVED the whole test - the .jsonl holds the `are you there` line - which the NOTE says is deliberate and not to be filed. |

### PERSONALIFECYCLE-13 — One Persona failing leaves every other Persona Online and messageable

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-13--one-persona-failing-leaves-every-other-persona-online-and-messageable)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `67b5fa28` |  | BOTH HALVES RUN, with three Personas Online first - Nova, Chief of Staff and a new `Ada` (Third agent, @ada, Haiku) - and exactly THREE claude-agent-acp rows (13592, 98588, 77196). HALF ONE, A KILLED ADAPTER: stopping Ada's process (attributed by CreationDate as the newest) left ONLY Ada changed - the tiles read `Ada => Offline`, `Chief of Staff => Online`, `Nova => Online` - with no page reload (navigation entries still 1). Nova's and Chief of Staff's Rooms show ZERO alert strips. THE LOG NAMES EXACTLY ONE PERSONA: grouping every `Persona '<name>'` occurrence in the new warning lines gives `1 Persona 'Ada'` and nothing else - `The agent process disconnected.` and `Persona 'Ada' agent event stream ended unexpectedly.` The adapter list keeps exactly TWO rows, the two healthy ones. HALF TWO, A FAILED START: with tools\acp\node_modules renamed aside, adding `Ghost` (Fourth, @ghost) completed in about 23 seconds and the page stayed fully responsive - the tile list rendered throughout, so the failed start was not awaited on a blocking path. Ghost came up `Offline` with the actionable tooltip `No ACP adapter is installed for Persona 'Ghost'. Run tools/acp/install.ps1 …`, while `Chief of Staff => Online` and `Nova => Online` were untouched, both with a null status tooltip and no alert strips in their Rooms. So a second, different failure was also contained. RESTORING THE ADAPTER FOLDER RESTARTED NOBODY: the two healthy adapter pids and their CreationDates are byte-identical before and after the rename back (13592@01:24:31, 98588@01:30:18) - nothing was restarted on an unrelated filesystem event, so no memory was destroyed across the board. Rooms exist for all four Personas. |

### PERSONALIFECYCLE-14 — An external edit to an Agent's definition restarts it once per save, and a non-.md file restarts nobody

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-14--an-external-edit-to-an-agents-definition-restarts-it-once-per-save-and-a-non-md-file-restarts-nobody)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `7b4bb99c` |  | THE WATCHER IS RECURSIVE - the highest-value defect in this area is absent. Moving Nova.md into Teams\Business\ changed nothing visible: the tile is still `Nova`, still `Online`, its Room is still in the sidebar, and the card's PERSONA FILE now reads `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Business\Nova.md` - all with performance navigation entries still 1. EDITING THE NESTED FILE RESTARTS IT: appending `Always mention that you are Nova.` (one write, via a shell append) made the tile walk `Offline` -> `Starting` -> `Online`, captured by MutationObserver, beginning about 3.5 seconds after the save and complete about 5.1 seconds after, with NO page reload. Timing note, recorded rather than smoothed over: the test says `within about a second`; the measured first visible change is ~3.5s, which is the ~500ms debounce plus adapter teardown and restart. THE ADAPTER WAS REPLACED: Nova's pid 98588 is gone and a new row 60164 (created 01:34:10) took its place, with the total staying at 3. THE CARD SHOWS THE NEW TEXT: its PERSONA section reads `You are Nova. Answer in one short sentence.Always mention that you are Nova.` THE DEBOUNCE HOLDS - tested twice, and the first attempt is reported too because it is informative. (a) Two writes 89ms apart whose NET content equalled what was already loaded (add a space, remove it) produced ZERO restarts and ZERO new processes over 33 seconds - better than the expected one, since a no-op edit does not destroy memory. (b) Two CONTENT-CHANGING writes 1ms apart (append ` X`, then ` XY`) produced EXACTLY ONE restart cycle - a single Offline -> Starting -> Online pass, no second cycle in 33 seconds - and EXACTLY ONE new adapter process: pid 60164 replaced by 21196, total still 3. So the debounce coalesces rather than restarting twice. A NON-.md FILE RESTARTS NOBODY: creating Teams\notes.txt produced ZERO status changes across all four tiles over 17 seconds (the observer's timeline is empty) - Chief of Staff, Ghost and Nova all stayed Online and Ada stayed Offline - and the file was then removed. No watcher buffer-overflow line appears in the log, so the page is not stale. Nova.md is left in Teams\Business\ for PERSONALIFECYCLE-15. |

### PERSONALIFECYCLE-15 — A write under an Agent's work folder restarts nothing

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-15--a-write-under-an-agents-work-folder-restarts-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `30c6d050` |  | THE SECOND SILENT WATCHER FAILURE IS ABSENT - a directory rename IS seen, even though a directory name has no .md extension. After renaming Teams\Business to Teams\BusinessOps, with no page reload (navigation entries still 1): `Nova` is still listed, still named `Nova`, still `Test agent · @nova`, still `Online`, still grouped under `No team`, and its Room is still in the sidebar. THE CARD FOLLOWED THE FOLDER: its PERSONA FILE section reads `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\BusinessOps\Nova.md` - the NEW path - and its MODEL section still reads `haiku`, so the stored Model is keyed on the Name and not on the path. No `An unhandled error has occurred.` after the directory event. THE CACHE IS NOT STALE - the decisive check: appending `Renamed folder marker.` to the file AT ITS NEW PATH reached the card within 13 seconds, whose PERSONA section now ends `XYRenamed folder marker.` So the watcher is still following the file after the folder rename rather than serving cached prompt text forever. MOVING THE FOLDER OUT OF Teams\ ENTIRELY IS CLEAN: the `Nova` tile DISAPPEARED from the list (tiles are now Ada, Chief of Staff, Ghost), and its adapter row left the process list - pid 21196 is gone and the count dropped from 3 to 2 - so a Persona whose file is gone is not left live and able to spend money. THE ROOM REMAINS in the sidebar, which is the deliberate no-cascade behaviour, and opening that orphaned `Nova` Room shows ZERO alert strips - an agent that is merely not running is not reported as a failure. Restored afterwards: Nova.md moved back to the Teams root, BusinessOps removed, and the adapter count is back to 3. |

### PERSONALIFECYCLE-16 — A Persona file that becomes malformed is named in "Files that didn't load", and an Alias collision names BOTH files

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-16--a-persona-file-that-becomes-malformed-is-named-in-files-that-didnt-load-and-an-alias-collision-names-both-files)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `28085c54` |  | A FILE THAT FAILS TO BECOME A AGENT IS IMPOSSIBLE TO MISS. Deleting the `title:` line from Nova.md removed the `Nova` tile from the list within the 11-second watch, with no page reload (navigation entries 1), and a `Files that didn't load` block appeared listing the exact path `E:\Repos\Huddle\src\Huddle.App\App_Data\Teams\Nova.md` with the reason `Persona frontmatter is missing required field 'Title'.` - naming the MISSING FIELD, not an exception type and not a line number. THE BLOCK IS AT THE TOP: measured by geometry, its top edge is at y=116 against the first tile at y=335, so it sits above the tile list. THE ROOM SURVIVES AND STAYS QUIET: `Nova` is still in the sidebar and opening it shows ZERO alert strips - an agent that did not load is not alarmed about. THE REJECTED PERSONA'S ADAPTER IS STOPPED: the process list dropped from 3 rows to 2, so nothing rejected is left live and able to spend money. RESTORING `title:` brought the tile back and it reached `Online` again, with the `Files that didn't load` block gone. THE COLLISION REJECTS BOTH SIDES - no silent winner. Setting Chief of Staff's alias to `nova`, matching Nova's alias AND Name, made BOTH tiles vanish (leaving only Ada and Ghost) and listed BOTH files in the block, each reason naming the OTHER file's path: for Chief of Staff.md, `Persona Alias 'nova' is also used by '…\Nova.md'. Persona Alias 'nova' matches the Name used by '…\Nova.md'.`; for Nova.md, `Persona Alias 'nova' is also used by '…\Chief of Staff.md'. Persona Name 'Nova' matches the Alias used by '…\Chief of Staff.md'.` So the alias-versus-Name case the NOTE mentions is covered in the same run. Restoring the alias brought both back Online with the block gone. |

### PERSONALIFECYCLE-17 — A stored Model the Adapter no longer advertises is Degraded with a reason, never a failed start

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-17--a-stored-model-the-adapter-no-longer-advertises-is-degraded-with-a-reason-never-a-failed-start)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Fail *(earlier script)* | `c43ee00c` | #30 | O-DB WAS AVAILABLE (a .NET helper stands in for sqlite3), so the test ran. HALF THE DESIGN HOLDS: with `persona_models` set to `claude-does-not-exist` for Nova, the Persona did NOT fail to start - it reached a live adapter process and a settled state, so a stale stored Model does not brick an agent, and any adapter upgrade that renames a model would not take the team down. THE LOG CARRIES THE WARNING, exactly as specified: `Requested model 'claude-does-not-exist' is not in the agent's advertised model catalog; continuing on the agent's default.` THE FAILURE: the settled state is `Online`, NOT `Degraded`, and the reason reaches NO surface at all - the tile status tooltip is null (no title attribute), the card has no reason line, the Room has no strip, and the word `Degraded` appears nowhere on the page. That is this test's named Fail condition: the mismatch reached only the log, so an agent is silently running on a model nobody chose - and the card actively asserts the wrong one, still showing `claude-does-not-exist` under MODEL. NOT A FLASH THAT WAS MISSED: re-run with Nova as the ONLY Persona on disk (the other three parked aside) and a MutationObserver watching /agents from page load through Nova coming up, across 30 seconds - ZERO `Degraded` states recorded, and zero occurrences of the reason text. NOT THE DOCUMENTED EMPTY-CATALOG EXEMPTION EITHER, which is why this is a Fail rather than the Inconclusive the clause allows: storing `sonnet` instead - a model the adapter DOES advertise - on the same machine and the same session path produced ZERO `not in the agent's advertised model catalog` warnings and ZERO `Failed to set model` warnings, so the id resolved against a POPULATED ConfigOptions list. PersonaRunner builds session.Models from that same response.ConfigOptions and guards on `Models.Count > 0`, so the guard was satisfied and the Degraded report should have fired. Filed as #30. RESTORED: persona_models back to `haiku` for Nova, the three parked Persona files returned, app restarted with zero catalog warnings and four adapters running. |

### PERSONALIFECYCLE-18 — Editing a Prompt at /settings does not restart a running Agent

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-18--editing-a-prompt-at-settings-does-not-restart-a-running-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `5294433c` |  | Four agents Online before the save - Ada, Chief of Staff, Ghost, Nova - with four adapter processes recorded to the millisecond: 71580@01:45:45.313, 21224@01:45:45.323, 100824@01:45:45.329, 16408@01:45:45.422. hooks.json was ABSENT beforehand, which is the expected pre-first-save state. THE EDIT: /settings opened on the `Hooks` tab by default; the first field is `Orientation`, which DOES carry a `Next session` badge - the app saying in advance that this edit will not reach a running agent. Appending ` (edited)` raised `Modified` + `Unsaved` on that field and enabled `Save`. AFTER SAVING, NOTHING RESTARTED: a MutationObserver watched all four tiles for 32 seconds and recorded ZERO status changes - no flicker through Offline or Starting, every tile still `Online`. THE PROCESSES ARE THE SAME ONES, not merely the same count: all four ProcessIds AND their CreationDates (to the millisecond) are identical before and after the save. The field now shows `Modified` (Unsaved cleared), `Save` went disabled, and no `An unhandled error has occurred.` appeared. hooks.json EXISTS after the first save, holding only `systemPrompt.orientation` with the edited text. RESETTING RESTARTS NOTHING EITHER: clicking that field's `Reset` and saving again left the four adapter pids and CreationDates unchanged once more, cleared both badges, and returned hooks.json to `{}`. The inertness of a reworded system-prompt Hook on an already-running agent is the documented trade and is not filed. |

### PERSONALIFECYCLE-19 — Removing a Persona takes it offline but leaves its Agent, Room and Transcript, and raises no alert

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-19--removing-a-persona-takes-it-offline-but-leaves-its-agent-room-and-transcript-and-raises-no-alert)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `1fe512cc` |  | REMOVAL IS A DELIBERATE NO-CASCADE. The inline confirm works and uses no native dialog: the action row went `Message, Edit, Open, Remove` -> `Message, Edit, Open, Confirm, Cancel` (an in-place swap) -> back to `Message, Edit, Open, Remove` after `Cancel`, with nothing removed and all four tiles still present; window.confirm was stubbed to record a call and NEVER fired. AFTER `Remove` -> `Confirm`: the card closed and the `Chief of Staff` tile is gone from /agents, leaving Ada, Ghost and Nova - the page did not need a reload (navigation entries still 1). WHAT SURVIVES, all of it deliberate: the Room named `Chief of Staff` is still in the sidebar, opens with heading `Chief of Staff` and its member list intact (`You, Chief of Staff`), and shows ZERO alert strips; the rooms directory is unchanged (still just the one .jsonl); App_Data\work\Chief of Staff is LEFT BEHIND alongside Ada, Ghost and Nova; and O-DB still holds the `users` row and the `rooms` row for that Name. WHAT GOES: App_Data\Teams\Chief of Staff.md is gone (Teams now holds Ada.md, Ghost.md, Nova.md), the adapter count dropped by exactly one (4 -> 3) so the removed agent is not still live and able to spend money, and - the part that IS a bug if it survives - the `persona_models` row for `Chief of Staff` is GONE (the table now holds only Nova and Ada; persona_efforts was empty throughout, since Haiku advertises no effort ladder). |

### PERSONALIFECYCLE-20 — Re-creating a removed Persona under the same Name does not resurrect its old Model or Effort

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-20--re-creating-a-removed-persona-under-the-same-name-does-not-resurrect-its-old-model-or-effort)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `1fe512cc` |  | Run immediately after PERSONALIFECYCLE-19 removed `Chief of Staff`, which had Model = Haiku stored (persona_models held `Chief of Staff \| haiku` before the removal), so the Name really did have a stored Model to resurrect. Re-created with the SAME Name, Title `Keeps the team honest`, Alias `cos` and the same body, with BOTH selects left untouched at `Use the agent's default` (confirmed: both select values are the empty string at submit time). NOTHING WAS RESURRECTED: the resulting Agent card's MODEL section reads exactly `Agent default` and its EFFORT section reads exactly `Model default` - not `Haiku`, not `low`, and not a blank where the default sentinel should be, so `no choice` is distinguishable from `a choice that failed to display`. O-DB AGREES: querying persona_models and persona_efforts for `Chief of Staff` returns NO ROW at all - not a row holding an empty string, which would never resolve and would warn on every restart. The agent came back `Online` alongside the other three. |

### PERSONALIFECYCLE-21 — Editing the frontmatter name renames the Agent in place: its folder, file, Room, Model and Effort all follow

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-21--editing-the-frontmatter-name-renames-the-agent-in-place-its-folder-file-room-model-and-effort-all-follow)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `30d26bee` |  | IDENTITY MOVED, STORAGE DID NOT. Editing only the frontmatter line `name: 'Nova'` to `name: 'Nova Prime'` in the Edit card and saving: the `Nova` tile was REPLACED by `NP \| Nova Prime \| Test agent · @nova \| Online`, with no `An unhandled error has occurred.` and no refusal message. THE MODEL AND EFFORT CARRIED ACROSS: the card shows MODEL `haiku` (the same choice the old Name had - it read `Haiku` before) and EFFORT `Model default` (unchanged; no effort was ever stored, since Haiku advertises no ladder). O-DB CONFIRMS THE ROW MOVED RATHER THAN BEING DROPPED OR DUPLICATED: persona_models now holds `Nova Prime \| haiku` and `Ada \| haiku`, with NO row left under `Nova`. `users` lists BOTH `Nova` and `Nova Prime`, which is the documented ghost. A NEW Room named `Nova Prime` appeared in the sidebar and a NEW App_Data\work\Nova Prime\ folder was created, alongside the old ones. THE FILE ON DISK KEPT ITS ORIGINAL NAME: Teams still holds `Nova.md` (with Ada.md, Chief of Staff.md, Ghost.md) - no file was renamed, so identity and storage are not conflated (the mirror of PERSONALIFECYCLE-07). THE AGENT RESTARTED UNDER ITS NEW IDENTITY: a fresh adapter row exists, pid 80140 created 01:50:16, replacing the previous one - so the new system prompt really takes effect. THE OLD ROOM IS INTACT AND QUIET: `Nova` is still in the sidebar, opens with members `You, Nova`, still holds its `are you there` message, and shows ZERO alert strips - the documented no-cascade limit, not filed. ONE TRANSIENT OBSERVATION worth recording: the card rendered immediately after Save briefly showed MODEL `Agent default`; reopening the card shows `haiku`. The stored row was correct throughout, so this is a stale render of the just-saved card rather than a lost setting - it would read as data loss to a user who did not reopen the card. |

### PERSONALIFECYCLE-22 — Health and presence repaint every open surface live, in every browser tab

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-22--health-and-presence-repaint-every-open-surface-live-in-every-browser-tab)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `bd762d83` |  | THREE TABS, NONE RELOADED THROUGHOUT (navigation entries stayed at 1 in all three). Tab 1 and tab 2 on /agents, tab 3 on the `Nova Prime` Room, each instrumented with a MutationObserver. KILLING THE ADAPTER REPAINTED ALL THREE WITHOUT ANY INTERACTION: tab 1's tile went to `Offline` carrying the reason `Its event reader ended unexpectedly: The agent process disconnected.`; tab 3's Room grew the red strip `Nova Prime is Offline: Its event reader ended unexpectedly: The agent process disconnected.` about 4.1 seconds after its observer armed; and TAB 2 - brought forward for the first time afterwards, with its JS marker still alive and no reload - read `Nova Prime:Offline` with the IDENTICAL tooltip text. So both surfaces and every tab agree; neither the tile nor the strip lags the other. THE RESTART, DRIVEN FROM TAB 1, WAS FOLLOWED BY THE UNTOUCHED TABS: tab 2's observer recorded the full sequence `Offline` (+4624ms) -> `Starting` (+4625ms) -> `Online` (+6143ms) without the tab being clicked, and tab 3's strip timeline shows it clearing to empty at +3831ms, ending with zero .member-health-alert elements. NO SUBSCRIPTION LEAK: navigating tab 3 away to /agents and back to the Room TWICE, then waiting 20 seconds, produced no error in ANY tab - #blazor-error-ui computes display:none and the text `An unhandled error has occurred` appears in none of the three, with all three still at navigation entry 1. |

### PERSONALIFECYCLE-23 — An app restart brings every Persona back, orphans no process, and deliberately loses only in-memory state

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-23--an-app-restart-brings-every-persona-back-orphans-no-process-and-deliberately-loses-only-in-memory-state)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `76e4662c` |  | SHUTDOWN ORPHANS NOTHING: with four adapters running (57616, 71580, 89084, 100824), stopping the app left ZERO rows matching claude-agent-acp immediately afterwards AND still zero ten seconds later, with node.exe back to the same 18 unrelated processes - so no adapter survives to hold a session open and keep spending. STARTUP IS COMPLETE AND AUTOMATIC: on restart all four Personas came back with NO human action - Ada, Chief of Staff, Ghost and Nova Prime all read `Online`, and the adapter count is 4 again. (The Offline -> Starting -> Online walk itself was not re-sampled here because the page could only be loaded after the server was listening, by which time the tiles had settled; that exact walk was captured directly in PERSONALIFECYCLE-06, -08, -10 and -22.) THE SIDEBAR IS IDENTICAL AND HAS NO DUPLICATES: alpha, echo, Nova, Chief of Staff, Ada, Ghost, Nova Prime - the same seven Rooms in the same order as before the restart - and the log contains ZERO `Created direct room` lines for this run, so Rooms were FOUND rather than minted again. THE TRANSCRIPT IS INTACT: the `Nova` Room still shows `You: are you there` as its only message, and App_Data\rooms holds the same single .jsonl at the same 151 bytes - no shrinkage, so no real data loss. NO `failed to start` WARNINGS for any agent that was healthy before. |

### PERSONALIFECYCLE-24 — Five Personas (the Chief of Staff and four more) cost five adapter processes, all reach Online, and all exit on shutdown

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-24--five-personas-the-chief-of-staff-and-four-more-cost-five-adapter-processes-all-reach-online-and-all-exit-on-shutdown)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `be5ab262` |  | Measured from a genuinely fresh start (app stopped, zero adapters, then launched). FOUR PERSONAS ON DISK - Ada.md, Chief of Staff.md, Ghost.md, Nova.md - whose frontmatter Names are Ada, Chief of Staff, Ghost and Nova Prime (the last renamed in PERSONALIFECYCLE-21). These stand in for the test's Nova/Ada/Bram/Cleo; the count and the per-Persona cost are what it measures. ALL FOUR REACHED `Online` VERY FAST: the server answered /agents 5.8 seconds after launch, and by the time the page rendered all four tiles already read Online - so the last one settled within roughly 8 seconds against a 120-second bar, and none was stuck on `Starting`. EXACTLY FOUR ADAPTERS, one per Online Persona and no more, and FOUR tool servers on distinct loopback ports - 127.0.0.1:58861, :58862, :63925, :63926 - with ZERO bind/port/EADDRINUSE errors and ZERO `failed to start` warnings, so the per-Persona loopback server is not exhausting anything at this scale. THE UI STAYED USABLE: five navigations timed in the page (Nova Room, Chief of Staff Room, Agents, Ada Room, Agents) took 628, 1002, 998, 1005 and 992 ms - none over two seconds, and no stalls. SHUTDOWN LEAKS NOTHING: after stopping the app, zero claude-agent-acp rows immediately AND zero ten seconds later. ONE COUNT TO EXPLAIN: App_Data\work holds FIVE subfolders - Ada, Chief of Staff, Ghost, Nova and Nova Prime - not four. Each of the four CURRENT frontmatter Names has one; the extra `Nova` is the deliberate leftover from the rename in PERSONALIFECYCLE-21, and the same no-cascade rule that PERSONALIFECYCLE-19 states explicitly (a removed agent's Work Dir is left behind). So it is not the `a host factory never ran` failure this condition guards against - that would show as FEWER folders than Personas. |

### PERSONALIFECYCLE-25 — An Adapter that needs authentication reports that as its own distinct reason

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-25--an-adapter-that-needs-authentication-reports-that-as-its-own-distinct-reason)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Not run *(earlier script)* | `be5ab262` |  | NOT TESTED - recorded explicitly rather than passed, exactly as the Inconclusive clause requires. The test needs an adapter that is INSTALLED but whose Claude login is absent or expired. The only ways to produce that on this machine would be to log the operator out of their own Claude session or to move/delete their credential files, both of which the test forbids in terms (`Never log the tester out of their own Claude session to provoke it, and never move or delete credential files`), and no second OS user or never-logged-in machine is available in this harness. The adapter here is installed AND authenticated - four Personas start and reach Online, and the model catalog probe returns a real roster - so the precondition is the opposite of what the test needs. WHAT IS ALREADY KNOWN FROM THE ADJACENT TESTS, and is why this is a gap rather than a blind spot: the MISSING-adapter reason is verified word for word in PERSONALIFECYCLE-04 and -09 (`No ACP adapter is installed for Persona 'X'. Run tools/acp/install.ps1 …`) on the tile tooltip, the card and the Room strip, and the killed-adapter reason is verified in -12 (`Its event reader ended unexpectedly: …`). So two of the three failure classes are confirmed to be narrowed apart and to carry actionable text; only the authentication class is unverified. NOT MARKED PASS: the authentication reason string, its log Warning, and the `Model catalog probe skipped: the adapter needs authentication.` line have not been observed. |

### PERSONALIFECYCLE-26 — MONEY: saving the Edit card with nothing changed does NOT restart the Agent

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-26--money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `63264ea7` |  | Run on a FRESH app start and a FRESH two-member Room (`You, Nova`, id 01a0a67616d57c46990cb6e2bfe1bfa2) - the old Room named `Nova` is no longer two-member, because in APPTOOLS-20 Zellandine used invite_agent to add ITSELF so it could post there (`Invited agent 'Zellandine' … into room '01a0a62048e07ba7a24d814a3dda9218'.`), which is legitimate tool use, not a defect - membership is enforced, PostAsync throws NotMember before it ever reaches the Budget check. THE TILE NEVER FLICKERED: sampled every 1.5s for 30 seconds after **Save** (19 samples), the Nova tile read `Nova Coordinator · @nova Online` in EVERY ONE - no Offline, no Starting, no error banner, and no `An unhandled error has occurred.` THE ADAPTER IS UNTOUCHED: the three `claude-agent-acp` node.exe processes were 49796, 9784 and 5632, all CreationDate 12:30:28, both before and after the save - same ProcessIds, same CreationDates. MEMORY SURVIVED: `Remember the word ZEBRA. Reply with just OK.` -> `OK`, and after the save `What word did I ask you to remember?` -> `ZEBRA`. THE PROCESSID ORACLE ALONE CANNOT DECIDE THIS TEST IN THIS BUILD, and that is worth knowing for -27 and -28: one `claude-agent-acp` process is started per Persona at APP start and lives for the app's lifetime; a Persona restart creates a new ACP SESSION inside that same process, so an unchanged ProcessId is necessary but not sufficient. The real oracle is the `[session/create] … phase=register` line, and a CONTROLLED REPEAT was run for it: with the register count at 5, a second no-op Edit -> Save was performed and the count stayed at 5 - ZERO sessions created by a no-op save. That matches PersonaSupervisor.NeedsRestart, which is record value equality over the whole Persona (`persona != started`), so unchanged text, Model and Effort compare equal and nothing restarts. The ZEBRA answer is therefore real evidence rather than transcript replay, since no session was replaced to need replaying. ONE THING RECORDED HONESTLY AND NOT ATTRIBUTED TO THE SAVE: earlier in the same run the log shows TWO extra session registers (d3e9fd82, dbcf6b47), each followed by `The agent process disconnected.` and the first by `Dropping update for unknown session`. These are NOT Persona restarts - they are the documented model/effort catalog probe, proven by reproducing the same triple with Team:Acp:Enabled=false where no Persona session exists. The controlled repeat above proves these are NOT caused by a no-op save; they are the documented model/effort catalog probe: each probe spawns an adapter, creates a real ACP session (hence the register line), reads the catalog and exits. Confirmed by re-running with Team:Acp:Enabled=false, where no Persona session can exist - the same triple appears with ZERO Persona starts in the log. Not a defect; #33 closed. A 75-second idle watch produced none, because the probe runs only when a New/Edit card opens or the Model changes - never on a plain page load. It is invisible in the UI - the tile stayed Online throughout - and is worth its own investigation. |

### PERSONALIFECYCLE-27 — MONEY: editing the Persona body restarts the session and the new instruction takes effect

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-27--money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `63264ea7` |  | THE LABEL AND HINT ARE WORD FOR WORD what the test specifies: the textarea is labelled **Persona text** and the `.agent-profile-card-hint` under it reads `Markdown. This is the whole Persona file, front matter included, and becomes the agent's system prompt. Saving restarts it, which clears what it remembers.` THE TEXTAREA HOLDS THE WHOLE FILE, frontmatter included - it opens with `---`, `name: 'Nova'`, `title: 'Coordinator'`, `alias: 'nova'`, `---`, then the body. THE RESTART IS REAL AND SINGULAR, confirmed on both oracles. Tile, sampled every 1.2s for 40 seconds (31 samples): `Starting` then back to `Online`, exactly that sequence and nothing else. Adapter processes BEFORE: 49796, 9784, 5632, all CreationDate 12:30:28. AFTER: 49796, 5632 unchanged and 9784 REPLACED by 34668 at 12:36:32 - exactly ONE new ProcessId, the old one gone, the other two Personas untouched. Session count went 5 -> 6: exactly ONE new `[session/create] … phase=register`, so this is NOT the `Two restarts from one Save` failure. THIS ALSO CALIBRATES THE ORACLE for -26 and -28: a genuine restart DOES replace the adapter process, so -26's unchanged ProcessIds are real evidence that a no-op save restarts nothing, rather than an artefact of a process that never changes. THE FILE ROUND-TRIPS: `App_Data/Teams/Nova.md` still carries its complete frontmatter block (`---`, name, title, alias, `---`), the original body line, and now ends with `Always begin every reply with the word PLUM.` THE INSTRUCTION TOOK EFFECT WITH NO APP RESTART: asked `Say hello in one sentence.` BEFORE the edit Nova answered `Hello! How can I help you today?` - no PLUM; asked the identical question AFTER, it answered `PLUM Hello, I'm Nova, your coordinator for this chat room.` So both halves pass - the restart half and the obedience half - and no INCONCLUSIVE fallback was needed. THE FRONTMATTER-GUARD CHECK THE TEST ASKS FOR WAS ALSO RUN: deleting the `title:` line and clicking **Save** was REFUSED INLINE with the parser's own reason - a `.composer-error` reading `Persona frontmatter is missing required field 'Title'.` The card stayed open with the text still in it, `Nova.md` on disk was left completely untouched (its `title:` line is still there), the session count stayed at 6 so nothing restarted, and there was no `An unhandled error has occurred.` The edit was then cancelled. |

### PERSONALIFECYCLE-28 — MONEY: changing the Model from Haiku to Sonnet restarts the session and clears what the Agent remembers

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-28--money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-agent-remembers)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `e36f75c0` |  | THE SONNET ENTRY IS PRESENT (option value `sonnet`, label `Sonnet`); Opus was never selected. An earlier look at the picker showed only Haiku - that is the Model-catalog probe cache noted in -03/-04, and the full catalog appears once the adapter has been probed. THE RESTART IS REAL: tile sampled every 1.2s for 40 seconds (40 samples) went `Starting` then back to `Online`; Nova's adapter process 34668 (12:36:32) is GONE and replaced by 42720 (12:41:40), while Jarvis's and Zellandine's (49796, 5632) were untouched - exactly ONE new ProcessId. NOT THE `TWO RESTARTS` FAILURE, and the reasoning is explicit because the session counter alone would have suggested otherwise: `phase=register` went 6 -> 8, but only ONE new adapter process appeared. -27 established the 1:1 rule on this build - a genuine restart replaces the Persona's node process - so two registers against one new process means one restart plus one catalog-probe session of the kind recorded in -26 - the probe registers a session and exits, which is why it carries a `The agent process disconnected.` line and replaces no adapter process. THE STORED CHOICE READS BACK: the card's MODEL section reads `sonnet`, and reopening **Edit** still shows `Sonnet` selected - NOT `Use the agent's default`, so a later save would not silently wipe it. THE SESSION WAS GENUINELY REPLACED: asked `What word did I ask you to remember?` Nova answered `PLUM I don't have any record of you asking me to remember a specific word in this conversation.` - the word was gone. A DIFFERENT MODEL IS ANSWERING, unambiguously: before the change `PLUM I'm Claude Haiku 4.5, a fast and efficient model.`, after it `PLUM I'm Sonnet 5 (model ID claude-sonnet-5).` So the self-report is decisive here rather than inconclusive. NO CATALOG WARNING: zero lines matching `not in the agent's advertised model catalog` anywhere in the run. O-DB: `persona_models` holds `Nova \| sonnet` (Jarvis and Zellandine still `haiku`). That is the adapter's own catalog identifier - the picker's option VALUE - not the display label `Sonnet`; combined with zero catalog warnings and a model that demonstrably changed, this is an ID the adapter accepts, which is what the condition is protecting. TWO DEVIATIONS RECORDED: (a) the remembered word was changed to `QUOKKA` and the prompt was extended to `…for this conversation only. Do NOT write it to any file and do NOT save it to your memory.` The scripted ZEBRA run had to be discarded because Nova PERSISTED it - it wrote `remembered_word.md` into the human's own `~/.claude/projects/…/memory/` directory AND added a line to `MEMORY.md`, which would have let a brand-new session recall the word from disk and made step 12 prove nothing. With the no-persist instruction the memory directory stayed clean (zero `quokka` hits), so the negative result is real. This is a fresh, concrete reproduction of the memory-directory finding already filed as issue #22; the file Nova wrote is kept in the scratchpad and the human's memory index was restored. (b) The mandatory restore to Haiku is deferred to the end of PERSONALIFECYCLE-29, which runs immediately after and REQUIRES a model that advertises an effort ladder - Haiku advertises none. One restore satisfies both tests. |

### PERSONALIFECYCLE-29 — MONEY: changing the Effort from low to medium restarts the session, and a Model change clears the Effort selection

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-29--money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `e36f75c0` |  | RUN ON SONNET, NOT HAIKU, which the test's own INCONCLUSIVE clause directs: Haiku advertises NO effort ladder on this adapter (the select offers only `Use the agent's default`, hint `This model offers no effort choice, so it will think as it normally does.`), so the test is not applicable there. On Sonnet the select offers Low, Medium, High, Xhigh, Max. Nothing above medium was ever selected. THE LADDER IS READ PER MODEL, NOT CACHED PER APP RUN - the same select, same app run, offers 6 entries under Sonnet and 1 under Haiku. EXACTLY ONE WAY TO SAY DEFAULT: the Effort options are `Use the agent's default`, Low, Medium, High, Xhigh, Max - the adapter's own `default` entry is NOT offered alongside the blank one (the Model select does carry both `Use the agent's default` and `Default (recommended)`, but this condition is about Effort). AN EFFORT CHANGE RESTARTS, exactly once: Effort was first saved as `low` to establish the precondition (the stored value was the blank default), then `Low` -> `Medium`. Tile sampled every 1.2s for 40s (38 samples): `Starting` then `Online`. Nova's adapter 19904 (12:45:00) GONE, replaced by 24636 (12:46:34) - ONE new ProcessId, Jarvis's and Zellandine's untouched - and `phase=register` went 9 -> 10, a clean 1:1. THE CARD READS THE STORED VALUE: `MODEL Sonnet` / `EFFORT Medium`. THE SESSION WAS REPLACED: `What word did I ask you to remember?` -> `PLUM I have no record of you asking me to remember a word.` A MODEL CHANGE CLEARS THE EFFORT SELECTION: with `Medium` selected (6 options), changing the Model reset the select to `Use the agent's default` - the level did not survive, so the human is not left holding a level they never chose for the new model. THE TRANSIENT PROBE HINT WAS CAUGHT VERBATIM, and catching it needed thought: switching Sonnet -> Haiku showed nothing because Haiku's catalog was already probed in this run and the swap was instant. Switching to an UNPROBED model instead and sampling every 150ms captured the whole sequence - at t=164ms the hint read exactly `Reading the effort levels this model offers…`, and at t=4382ms the ladder arrived (6 options) with the steady hint `How hard this agent thinks. Changing it restarts the agent, which clears what it remembers.` A 4.2-second probe, which is why it is invisible against a cached model. THE ABANDONED-PROBE RACE THE TEST ASKS ABOUT IS HANDLED: switching models twice within 400ms (an unprobed entry, then Haiku) settled on HAIKU's ladder - 1 option, `This model offers no effort choice…` - and stayed there for the full 11.5 seconds sampled. The superseded probe never landed on the current selection. NO WARNING: zero lines matching `not in the agent's advertised effort catalog`. O-DB: `persona_efforts` held `Nova \| medium` - a lowercase effort ID, not the display label `Medium`. **Cancel** was verified to discard: after cancelling the model change the card still read `MODEL sonnet` / `EFFORT medium`. TWO DEVIATIONS: (a) the remembered word used the no-persist wording from -28 (`…for this conversation only. Do NOT write it to any file and do NOT save it to your memory.`), without which Nova writes the word into the human's own memory directory and a fresh session recalls it from disk. (b) Step 17's restore sets Effort back to the blank default rather than `low`, because the same restore returns the Model to Haiku, which has no ladder at all - `persona_efforts` is now EMPTY and `persona_models` reads haiku for all three Personas, which is the correct precondition for every later test. That empty table is also independent confirmation at the storage layer that a Model change clears the Effort. The `PLUM` line added in -27 was removed in the same save, so `Nova.md` is back to its original text with its frontmatter intact. |

### PERSONALIFECYCLE-30 — MONEY: what an Agent was told in one Room does not leak into another, one process serves both, and replies land in the right Room

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-30--money-what-an-agent-was-told-in-one-room-does-not-leak-into-another-one-process-serves-both-and-replies-land-in-the-right-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4f2c714d` |  | THE KNOWN LIMIT IS CONFIRMED AND IS **NOT** FILED, as the NOTE insists. In the two-member Room `You, Nova` the human said `My favourite fruit is a quince. Reply with just OK.` and Nova answered `OK`. Asked in a SEPARATE group Room (`You, echo, Nova`) `@Nova what is my favourite fruit?`, Nova answered `Your favourite fruit is a quince.` - knowledge carried from a Room the question was not asked in. That is the documented one-session-per-Persona design (a session per Persona-and-Room was rejected on cost), so it is recorded as the KNOWN LIMIT and no issue was raised. Nova's session had been restarted moments earlier by -29's restore, so the answer came from the new statement rather than from `QUINCE` earlier in that Room's transcript - a restart clears session memory and there is no transcript replay, which -28 established directly. EVERY ROUTING CONDITION - the part that actually decides this test - PASSES. The reply appeared in the GROUP Room where the question was asked. NO STRAY REPLY reached the two-member Room: it held 26 rows before the group question and 26 after, still ending `You :: My favourite fruit is a quince. Reply with just OK.` / `Nova :: OK`. NO REPLY LANDED IN ANY ROOM NOVA IS NOT A MEMBER OF: every one of the 11 `.jsonl` transcripts was parsed for `quince`, and it appears in exactly three - the two-member Room (the human's own two messages), the group Room (Nova's one reply, filed there and nowhere else), and one older Room where `quince` was the APPTOOLS-22 hook sentinel, unrelated to this test. So the Transcript and the UI agree about where every message lives. ONE SESSION PER PERSONA HOLDS: exactly THREE `claude-agent-acp` node.exe processes for the three Personas (Nova, Jarvis, Zellandine) while Nova is a member of several Rooms - the cost per agent does not scale with Rooms. Each of the 11 Rooms has its own `.jsonl`. |

### PERSONALIFECYCLE-31 — MONEY: the per-Persona token Budget shows as Degraded with its reason, and any Human Message clears it

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-31--money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4f2c714d` |  | Run on a FRESH app started with `Team__Acp__TokenBudget=1` and `Team__AgentMessageBudget=4` - the cap the NOTE calls mandatory was in force throughout. DEVIATION: the second Persona is `Jarvis`, not `Ada`; the test needs two Personas in one Room at Haiku/low and names Ada only as an example. THE BADGE IS DEGRADED, NOT OFFLINE, AND IT CARRIES ITS REASON ON ALL THREE SURFACES, word for word as specified. Tile: `Jarvis Researcher · @jar Degraded`, reached 12 seconds into the exchange (sampled every 2s from the moment the human spoke; both tiles were Online at 2s, 6s and 8s, so the flip is real and observed, not inferred). Tooltip (the tile's `title`): `The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.` Card: a `.agent-profile-card-reason` element with exactly that same sentence. Room strip: a `.member-health-alert-item` reading `Jarvis is Degraded: The per-Persona token Budget of 1 is spent; no more Turns until a Human speaks.` - the `<Name> is Degraded: …` form the test specifies. THE NUMBER IS THE CONFIGURED 1, so the double-underscore override was read correctly. THE CONSOLE WARNING IS EXACT AND AT WARNING LEVEL: `warn: Agency.Huddle.App.Acp.PersonaRunner` / `Persona 'Jarvis' has spent its token budget of 1 and is taking no more turns until a human speaks to it.` THE AGENT DID NOT GO SILENT WITHOUT A BADGE - which is the whole point of the test - and it did NOT read Offline, so no pointless Restart is invited. A HUMAN MESSAGE CLEARS IT, fast and completely: typing `hello` in the Room returned the tile to **Online** within 2 seconds, the tooltip reason became null, the `.agent-profile-card-reason` element disappeared entirely, and the Room's health strip emptied - no element anywhere on /agents still contained the phrase `token Budget`. Sampled for a further 66 seconds with no relapse. So the cap is per unattended run, not permanent. THE DEGRADED BADGE WAS NEVER RAISED FOR A SPENT ROOM BUDGET: at the moment Jarvis went Degraded the Room's own note read `3 of 4 agent replies since you last spoke.` - the Room Budget was NOT spent, so the two states are cleanly separated and the badge is not being conflated with the pause. ONE PASS CONDITION WAS NOT REACHABLE IN THIS RUN, recorded rather than glossed: the exchange never hit the per-Room Budget of 4, because a token Budget of 1 bites first by design - it stopped at 3 replies. The `Agents have sent 4 replies since you last spoke, and are paused.` prompt is therefore not observed here. That same pause, and the fact that it leaves every member HEALTHY, was proven directly in APPTOOLS-19 (the tile stayed **Online** at a fully spent Room Budget) and is exercised again in PERSONALIFECYCLE-32, where the token cap is disabled and the Room Budget is the only backstop. |

### PERSONALIFECYCLE-32 — MONEY: a token Budget of zero disables the per-Persona cap, and the per-Room Budget still stops the exchange

File: [`docs/engineering/manual-tests/persona-lifecycle.md`](persona-lifecycle.md#personalifecycle-32--money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-15 | Pass *(earlier script)* | `4f2c714d` |  | Run on a FRESH app with `Team__Acp__TokenBudget=0` and `Team__AgentMessageBudget=4`. Second Persona is `Jarvis`, not `Ada` (same deviation as -31). ZERO REALLY DOES DISABLE THE PER-PERSONA CAP: across TWO complete agent-to-agent exchanges, sampled every 2 seconds throughout (36+ samples), NEITHER tile ever read **Degraded** or **Offline** - both stayed `Online` the whole time - and not one tile carried a `title` tooltip at any sample. The console contains ZERO lines matching `has spent its token budget`. Under an identical prompt with TokenBudget=1, -31 produced a Degraded Jarvis within 12 seconds, so this is a real difference and not a run that simply never got that far. THE ROOM BUDGET IS STILL THE BACKSTOP, and it stopped the exchange both times at exactly four agent replies with the prompt `Agents have sent 4 replies since you last spoke, and are paused.` and both **Continue** and **Leave paused** buttons. So nothing ran away: the disable switch removes one cap without removing the other. THE PAUSE LEAVES EVERY MEMBER HEALTHY - the condition -31 could not reach: at the moment the prompt appeared, both tiles read Online and the Room's `.member-health-alert-item` strip was EMPTY. A spent Room Budget is never dressed as a fault. THE PROMPT SURVIVES A FULL PAGE LOAD: reloading the Room URL brought back the same prompt with both buttons, so it is rendered from stored state and a refresh cannot hide the fact that the Room is paused. **LEAVE PAUSED** WORKS EXACTLY AS SPECIFIED: the prompt disappeared and was replaced by the quieter note `Paused — 4 of 4 agent replies since you last spoke.`, with both tiles still Online and the health strip still empty. ONE CONDITION IS ONLY PARTLY OBSERVED, and the reason is structural rather than a defect: **Continue** granted the Budget correctly - the note went straight to `4 of 8 agent replies since you last spoke.` - but the exchange did NOT resume, on either of the two attempts. ExtendBudgetAsync re-publishes `history[^1]` with `MentionParser.Parse` run again, so the held Message only wakes whoever it MENTIONS; in this three-member Room both agents happened to end their last message with no @-mention (the final one was a long reflective answer addressed to nobody), which is CatchUp, not Reply. Silence there is correct. The resume path itself - Continue re-delivering the held Message, the agent acting on it with NO human message typed, and the Room halting again exactly one Budget later - was proven decisively in APPTOOLS-21 on the same code path, in a Room deliberately staged so the held Message carried a mention. The grant here is bounded the same way: 4 -> 8, one more Budget's worth, not unbounded. |

## Tasks: creating, moving, viewing and referencing a unit of work

### TASKS-01 — Creating a Task in the UI writes the file at the right path with the canonical frontmatter

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-01--creating-a-task-in-the-ui-writes-the-file-at-the-right-path-with-the-canonical-frontmatter)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-02 — A hand edit in a text editor appears within a second, with an "edited outside Huddle" entry

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-02--a-hand-edit-in-a-text-editor-appears-within-a-second-with-an-edited-outside-huddle-entry)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-03 — Board drag, ghost buckets, the Duplicate picker, and cancelling snaps the card back

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-03--board-drag-ghost-buckets-the-duplicate-picker-and-cancelling-snaps-the-card-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-04 — A View saves every change as it is made and the Fields dialog reorders fields

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-04--a-view-saves-every-change-as-it-is-made-and-the-fields-dialog-reorders-fields)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-05 — A broken `views.json` shows the error and the file is untouched

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-05--a-broken-viewsjson-shows-the-error-and-the-file-is-untouched)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-06 — The last View reopens after a browser restart, and a private window falls back to All Tasks

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-06--the-last-view-reopens-after-a-browser-restart-and-a-private-window-falls-back-to-all-tasks)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-07 — A conflict shows both versions, and merging changes to different fields is silent

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-07--a-conflict-shows-both-versions-and-merging-changes-to-different-fields-is-silent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-08 — Renaming a Persona rewrites `assignee:` in every task file and View filter, with no wake-up

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-08--renaming-a-persona-rewrites-assignee-in-every-task-file-and-view-filter-with-no-wake-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-09 — Keyboard only: Move to, the Fields dialog, and the Task panel, with no dragging

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-09--keyboard-only-move-to-the-fields-dialog-and-the-task-panel-with-no-dragging)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-10 — Assigning to a live Claude Persona wakes it in the right Room, and it calls `update_task` to set In Progress

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-10--assigning-to-a-live-claude-persona-wakes-it-in-the-right-room-and-it-calls-update_task-to-set-in-progress)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-11 — Two Personas reassigning to each other stop at the wake budget, and Allow 10 more resumes them

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-11--two-personas-reassigning-to-each-other-stop-at-the-wake-budget-and-allow-10-more-resumes-them)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-12 — An Agent that creates a Task with `originRoomId` wakes the assignee in that Room

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-12--an-agent-that-creates-a-task-with-originroomid-wakes-the-assignee-in-that-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `bfe93189` |  | By an agent on the paid lane; Keeper on Sonnet low as creator and assignee (no Rae), woken by the Needs Human Helper's gate-on, the case that made a hidden Growth chat at 03:13 that morning. In the root `Keeper` Room: GROW-0009, where the Keeper passed `originRoomId` itself, and GROW-0010, where it was asked to leave it out (its `create_task` input had no `originRoomId`). Both Tasks record `origin: 01a1b922…` (the root Room), both wake-ups landed there, and the lane still had four Rooms. About 0.09 USD. |

### TASKS-13 — The copy button works on `localhost` and from another machine, and the pasted id shows as a link

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-13--the-copy-button-works-on-localhost-and-from-another-machine-and-the-pasted-id-shows-as-a-link)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-14 — The `#` picker, keyboard only

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-14--the--picker-keyboard-only)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-15 — An Agent sent "please look at PLAT-0042" calls `get_task` with that id, without being told the tool name

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-15--an-agent-sent-please-look-at-plat-0042-calls-get_task-with-that-id-without-being-told-the-tool-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-16 — Typing a new Sprint in the Task panel writes `sprints.md` on Save, and moving the last Task out removes its line

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-16--typing-a-new-sprint-in-the-task-panel-writes-sprintsmd-on-save-and-moving-the-last-task-out-removes-its-line)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-17 — A hand edit of `sprints.md` shows within a second and changes no Task file; deleting a Task file removes no Sprint

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-17--a-hand-edit-of-sprintsmd-shows-within-a-second-and-changes-no-task-file-deleting-a-task-file-removes-no-sprint)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-18 — The toolbar Sprint filter, a saved View with Current sprint, and a Board grouped by Sprint

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-18--the-toolbar-sprint-filter-a-saved-view-with-current-sprint-and-a-board-grouped-by-sprint)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-19 — An Agent given a Sprint name sets it with `update_task`, a typo is refused with the known list, and the assignee is woken

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-19--an-agent-given-a-sprint-name-sets-it-with-update_task-a-typo-is-refused-with-the-known-list-and-the-assignee-is-woken)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-20 — Escape closes the Task drawer, Expand opens the dialog over the view, and a new Task opens at the top

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-20--the-task-opens-in-the-right-sidebar-edit-opens-the-edit-page-in-its-own-tab-and-a-new-task-opens-at-the-top)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TASKS-21 — A hand-off held by a spent wake budget survives a restart, and Wake starts the Agent once

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-21--a-hand-off-held-by-a-spent-wake-budget-survives-a-restart-and-wake-starts-the-agent-once)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `eb1b8c78` | #353 | Mock lane, free. A wrapper around `mock-acp` counted Turns, and `update_task` was called over the Agent's tool server, because a mock calls no tools. The Notification survived the restart, no Turn ran at startup, and one click on Wake Ada ran one Turn and closed the card. Also tried a Task hand-off held on a Room with a spent Budget: same result, with the Room's Budget card resolved at startup. Finding, fixed in the same PR: that card still told the Human to press a Continue that was gone. Not covered: a chat Mention refused by a spent Budget (see the follow-up issue). No pre-restart Turn count for the first case. |

### TASKS-22 — A request held by a spent Room Budget survives a restart, and Wake starts the Agent once

File: [`docs/engineering/manual-tests/tasks.md`](tasks.md#tasks-22--a-request-held-by-a-spent-room-budget-survives-a-restart-and-wake-starts-the-agent-once)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass *(run by an agent, mock lane)* | `ca083f0f` + uncommitted #428 fix | #428 | Mock lane, free, `Team__AgentMessageBudget=1`. A wrapper around `mock-acp` counted `session/prompt` lines, and Ada's Message was posted through her `post_message` tool. Case 1 (no restart): Continue gave Bo one Turn and the Notification closed. Case 2 (restart): the Notification stayed with the Room no longer paused and no Budget card, zero Turns at startup, one click on Wake Ada gave one Turn and one Message from You, the Notification closed and the button went. Case 3 (restart, then "@Bo go ahead and merge it" typed): one Turn, the Notification closed. Finding fixed in the same run: the card said "the Room is paused, press Continue" after a restart when neither was true; it now says the Budget ran out and names Continue and Wake. A mock echoes the Room's history, so its replies mention people; that made extra held requests, not a defect. |

## Adapters: choosing one per Persona, and running two at once

### ADAPTERS-01 — Two Personas in one Room on different Adapters, and neither Room nor Gate can tell

File: [`docs/engineering/manual-tests/adapters.md`](adapters.md#adapters-01--two-personas-in-one-room-on-different-adapters-and-neither-room-nor-gate-can-tell)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ADAPTERS-02 — A mock Adapter's reply lands whole in the Room and the Transcript, and the other Agent is unaffected

File: [`docs/engineering/manual-tests/adapters.md`](adapters.md#adapters-02--a-mock-adapters-reply-lands-whole-in-the-room-and-the-transcript-and-the-other-agent-is-unaffected)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ADAPTERS-03 — Changing an Agent's Adapter resets Model and Effort, says so, and restarts the session

File: [`docs/engineering/manual-tests/adapters.md`](adapters.md#adapters-03--changing-an-agents-adapter-resets-model-and-effort-says-so-and-restarts-the-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ADAPTERS-04 — Does a real local Model call `get_help` unprompted? 💰

File: [`docs/engineering/manual-tests/adapters.md`](adapters.md#adapters-04--does-a-real-local-model-call-get_help-unprompted-)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-09-18 | Blocked *(earlier script)* | `41c2fcb1` |  | **Blocked by D-1** (2026-09-17): against `AgencyDotNet.Acp` 0.1.195 the system prompt never reaches the agent, so the Model is never told `get_help` exists and any result would measure the missing prompt rather than the Model. Do not record one. Once unblocked: needs a real local Adapter and a reachable inference endpoint. "Calls no tool at all" is INCONCLUSIVE and expected for a small model — record the model name |

## Skills: the Chief of Staff, team-building, and the Family Health Advisor

### SKILLS-01 — The Chief of Staff reads `team-building` unprompted when asked for a team

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-01--the-chief-of-staff-reads-team-building-unprompted-when-asked-for-a-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLS-02 — Approve creates the Agents, wakes the Chief of Staff, and it opens a seeded Room

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-02--approve-creates-the-agents-wakes-the-chief-of-staff-and-it-opens-a-seeded-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLS-03 — Decline, and the Chief of Staff revises and proposes again

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-03--decline-and-the-chief-of-staff-revises-and-proposes-again)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLS-04 — Over the limit, Approve creates nothing and the Message says why

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-04--over-the-limit-approve-creates-nothing-and-the-message-says-why)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLS-05 — The Greeting on a `-Clean` install, and no second Greeting on restart

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-05--the-greeting-on-a--clean-install-and-no-second-greeting-on-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLS-06 — The Family Health Advisor's safety behaviour

File: [`docs/engineering/manual-tests/skills.md`](skills.md#skills-06--the-family-health-advisors-safety-behaviour)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Skill Packs: SKILLS.md, the AI tool, updates, the move and shared folders

### SKILLPACKS-01 — A real AI tool adds a Pack, and Huddle shows it without a reload

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-01--a-real-ai-tool-adds-a-pack-and-huddle-shows-it-without-a-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-02 — Choosing a Pack copies its Skills, then changes the Agent

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-02--choosing-a-pack-copies-its-skills-then-changes-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-03 — A changed Skill shows Update available, and Update copies then changes membership

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-03--a-changed-skill-shows-update-available-and-update-copies-then-changes-membership)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-04 — The move turns a copy of a real data folder into SKILLS.md and changes no Agent

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-04--the-move-turns-a-copy-of-a-real-data-folder-into-skillsmd-and-changes-no-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-05 — Two Huddles share one Skill Sources folder, each with its own paths

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-05--two-huddles-share-one-skill-sources-folder-each-with-its-own-paths)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-06 — A pointer on an unplugged drive shows Not downloaded, and Agents keep working

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-06--a-pointer-on-an-unplugged-drive-shows-not-downloaded-and-agents-keep-working)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### SKILLPACKS-07 — A withdrawn Source blocks a new hire, and its holders keep working

File: [`docs/engineering/manual-tests/skill-packs.md`](skill-packs.md#skillpacks-07--a-withdrawn-source-blocks-a-new-hire-and-its-holders-keep-working)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Work Mode: choosing how much an Agent may do before it asks

### WORKMODE-01 — The picker lists what the Adapter advertises, minus Plan, Auto and Bypass permissions

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-01--the-picker-lists-what-the-adapter-advertises-minus-plan-auto-and-bypass-permissions)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### WORKMODE-02 — Changing the Adapter clears the Work Mode and says so

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-02--changing-the-adapter-clears-the-work-mode-and-says-so)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### WORKMODE-03 — A hand-written row for a hidden mode is dropped, with a warning

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-03--a-hand-written-row-for-a-hidden-mode-is-dropped-with-a-warning)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### WORKMODE-04 — Hiding nothing is a list of one empty string

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-04--hiding-nothing-is-a-list-of-one-empty-string)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### WORKMODE-05 — An Adapter that advertises no mode shows no picker

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-05--an-adapter-that-advertises-no-mode-shows-no-picker)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### WORKMODE-06 — Accept edits: no permission request for an edit

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-06--accept-edits-no-permission-request-for-an-edit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `be871f26` |  | 2026-10-01, in the app. A `Write` and an `Edit` ran under Accept edits with no `session/request_permission` in the whole run; the file held `beta`. Run on a resumed session, with a second file name |

### WORKMODE-07 — Plan: nothing is written, and the request to leave plan mode is refused

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-07--plan-nothing-is-written-and-the-request-to-leave-plan-mode-is-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `be871f26` |  | 2026-10-01, in the app. One `switch_mode` request refused (`reject`), the guard's log line present, no file, stop reason `cancelled`, and nothing posted to the Room (OQ-2 confirmed in the app) |

### WORKMODE-08 — Accept edits: a write into `~/.claude` is still refused

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-08--accept-edits-a-write-into-claude-is-still-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `3bf2640d` |  | 2026-10-01, in the app, Haiku, Accept edits, on `017d684` (the `SerializeRaw` fix). One `session/request_permission` for the real `~/.claude/huddle-wm-test.txt`; the app logged "Refused a tool call writing to …", answered `reject`, the tool call failed, the file does not exist and the Agent said permission was denied. **The first two runs failed** (Accept edits, then the Adapter's default): the app answered `allow-once` and the file was created, because `SerializeRaw` turned `rawInput` into `{"file_path":[]}`, so the guard never saw a path. That bug predates Work Modes and is fixed on this branch |

### WORKMODE-09 — A mode is applied again after an app restart

File: [`docs/engineering/manual-tests/work-mode.md`](work-mode.md#workmode-09--a-mode-is-applied-again-after-an-app-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `be871f26` |  | 2026-10-01, in the app. `session/resume`, then `set_config_option` model, whose response showed the mode back at `default`, then `set_config_option` `mode=acceptEdits`, all before the prompt |

## Questions: an Agent asks the Human by tapping, not typing

### QUESTIONS-01 — A model frames its ask and calls `ask_human` with one to three Questions

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-01--a-model-frames-its-ask-and-calls-ask_human-with-one-to-three-questions)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `c83491ea` |  | | **Re-run 2026-10-01 after the fix, in the app, Haiku, one sample: Pass.** With `systemPrompt.askHuman` in the system prompt, a fresh Nova (body *"You are a fitness coach."*, never told about the tool) given the exact prompt called ToolSearch for `mcp__team__ask_human` unprompted, then the tool; the wire shows one `ask_human` call and no `elicitation`. It wrote one framing message (three sentences, over the "one or two" line, said in the past tense *"I've asked you"*) and a card of three single-select questions with 3 to 4 options each, no `@`, and wrote no routine and no guessed answers. **Before the fix, 2026-10-01: Fail, twice** (Coach, then a fresh Nova): each wrote its questions as a numbered list and never called `ask_human`. Cause: the Claude Adapter defers every MCP tool, so the model saw only the name, and the description that carried the guidance was never loaded. One sample proves the path works, not that it is reliable |

### QUESTIONS-02 — The asker ends its Turn without guessing, and the framing lands before the card can be tapped

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-02--the-asker-ends-its-turn-without-guessing-and-the-framing-lands-before-the-card-can-be-tapped)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `c83491ea` |  | | 2026-10-01, in the app, on an explicitly requested call. An in-page observer recorded the card appearing with `still writing` and 3/3 options disabled, then enabled 2.6 s later when Coach's Turn ended; Coach's last words were that it would wait for the answers, with no routine and no guessed answers. Not shown for a spontaneous call, which never happened (QUESTIONS-01) |

### QUESTIONS-03 — A factual question and a request for an opinion do not call `ask_human`

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-03--a-factual-question-and-a-request-for-an-opinion-do-not-call-ask_human)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `c83491ea` |  | | 2026-10-01, in the app, Haiku. Sable answered Paris and the Python/JavaScript question directly, no card. Weak evidence: with the description deferred (QUESTIONS-01) the model could not have been restrained by it |

### QUESTIONS-04 — The same on an Agent running on `agency-acp`

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-04--the-same-on-an-agent-running-on-agency-acp)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### QUESTIONS-05 — In a Room of three, an answer wakes only the asker

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-05--in-a-room-of-three-an-answer-wakes-only-the-asker)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `c83491ea` |  | | 2026-10-01, in the app, Room of Coach, Nova, Sable. One tap on Blue posted `> What's your favourite colour?`, a blank line, `Blue`, `@Coach`; only Coach replied (Nova's and Sable's Transcripts hold nothing from it), and Nova, asked next, read the answer from the Transcript. A multi-select and ranking card also posted `Mon, Fri` and `1. Speed · 2. Cost · 3. Quality` exactly |

### QUESTIONS-06 — A Claude Agent has no built-in `AskUserQuestion`

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-06--a-claude-agent-has-no-built-in-askuserquestion)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `c83491ea` |  | | **Re-run 2026-10-01 after the elicitation bridge, in the app, Haiku, with `Team__Acp__AdvertiseElicitation=false`: Pass.** Coach said it cannot use `AskUserQuestion` (*"it's been disabled for this session"*); no card of either kind, no Stop button, the Turn ended on its own. The wire trace's `initialize` carried `fs` and `terminal` only, and the trace holds no `elicitation` text at all. The default is now `true`, so this test needs the switch set. Original run, before the bridge existed: 2026-10-01, in the app, Haiku. Nova said it has no `AskUserQuestion` and offered `ask_human`; no second card, the Turn ended on its own, no idle-timeout line. The wire trace's `initialize` carries `fs` and `terminal` (both false) and no `elicitation`, and the log has no `elicitation` text and no `elicitation/create` |

### QUESTIONS-07 — A scripted Adapter's forms reach the Human as cards, with the right types

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-07--a-scripted-adapters-forms-reach-the-human-as-cards-with-the-right-types)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `198d8cc0` |  | | 2026-10-01, in the app against `mock-acp`, Chief of Staff. `[elicit:form]`: Send stayed disabled until Title, Due date and Boxes were set and while Boxes was 25 (maximum 20); the answer reached the Adapter as `{"title":"Move the office","due":"2026-10-15","count":7,"budget":12.5,"agree":true,"priority":"high","tags":["fragile","cold"],"notes":"Pianos <b>first</b> & \"quotes\""}` (typed, `<b>` shown as text), the Transcript Message quoted every label, and there was one reply. `[elicit:ask]`: typing **Other** cleared and disabled the selection; the Adapter got `{"question_0_custom":"Teal","question_1":["Rugs","Plants"]}`. `[elicit:unsupported]` showed no card and the Adapter got `decline`. `[elicit:refusal]` returned `{"choice":"cancelled"}`; `[elicit:one]` skipped (`decline`). A typed Message with a card open removed the card and the Adapter got `cancel`. **Found and fixed on screen:** the card had no height limit (Send and the composer were off the page on a 720 px window), a Transcript taller than the window could not be scrolled, multi-select options were centred, a one-question form showed its prompt twice, the refusal dialog showed its key `choice`, and an out-of-range number gave no reason |

### QUESTIONS-08 — Claude's `AskUserQuestion` reaches the Human, and the Turn survives the wait

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-08--claudes-askuserquestion-reaches-the-human-and-the-turn-survives-the-wait)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `198d8cc0` |  | | 2026-10-01, in the app, Coach on Haiku, real `claude-agent-acp` 0.75.1, idle bound 20 s. Coach called `AskUserQuestion`; the card showed *Colour*, the question once, three options with descriptions and Other. Left alone for ~47 s (more than twice the bound): still open, Stop still offered, no hung-Adapter alert. The trace shows the Adapter's request as `_elicitation/create` with `question_0` as a `oneOf` of three and `question_0_custom`. Choosing Green and Send: card gone, Transcript Message quoting the question and `Green`, and Coach continued in the **same** Turn (*"So green is your favourite colour"*). Coach's session received exactly one `session/prompt`, the Human's |

### QUESTIONS-09 — Dismiss and Stop end a waiting form cleanly

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-09--dismiss-and-stop-end-a-waiting-form-cleanly)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `198d8cc0` |  | | 2026-10-01, in the app, Haiku. **Skip:** the card went, nothing was posted as the Human, and Coach carried on in the same Turn (*"…whenever you're ready"*). **Stop** on Coach's Draft with the card open: the card and the Stop button went, no failure alert, and an ordinary message afterwards got a normal reply, so the slot and session were intact |

### QUESTIONS-10 — An unanswered form is dropped after `Acp:UserInputTimeoutSeconds`

File: [`docs/engineering/manual-tests/questions.md`](questions.md#questions-10--an-unanswered-form-is-dropped-after-acpuserinputtimeoutseconds)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `198d8cc0` |  | | 2026-10-01, in the app, Haiku, `UserInputTimeoutSeconds=30`, idle bound 20 s. The card appeared at 06:45:32 and was gone at the next check, about 35 s later; Coach said the tool use *"didn't go through"* and offered to ask again, the Turn ended, and there was no hung-Adapter line or failure alert. With the default `Acp:AdvertiseElicitation=true` and no other flag, in addition: when asked naturally to plan a workout with both tools available, Haiku chose `ask_human` (a three-Question `QuestionCard`) over `AskUserQuestion`, one sample |

## Prompt blocks: an image the Human names reaches the Agent

### PROMPTBLOCKS-01 — A named image is seen, without a tool call

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-01--a-named-image-is-seen-without-a-tool-call)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `392b1c88` |  | 2026-10-01, in the app, Haiku, one sample. The reply read `BANANA 4821` and the circle as red-orange from a PNG named by full path, with the Turn's prompt carrying the image block. |

### PROMPTBLOCKS-02 — Six images, four are seen

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-02--six-images-four-are-seen)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `392b1c88` |  | 2026-10-01, in the app, Haiku, one sample. Six PNGs named in one Message: the Agent said it could see 4 pictures and listed Red, Blue, Yellow, Purple (the first four); the log held one line ending `ImageCountCap=2`. |

### PROMPTBLOCKS-03 — An image over the size limit is a path line

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-03--an-image-over-the-size-limit-is-a-path-line)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `392b1c88` |  | 2026-10-01, in the app, one sample. A 4.3 MB PNG named in a Message: the Agent replied `PATHONLY`, the Turn completed, and the log held one Information line ending `TooLarge=1` with no path in it. |

### PROMPTBLOCKS-04 — An image mentioned only in catch-up is not re-sent

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-04--an-image-mentioned-only-in-catch-up-is-not-re-sent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### PROMPTBLOCKS-05 — An Adapter Profile can turn blocks off

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-05--an-adapter-profile-can-turn-blocks-off)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-01 | Pass *(earlier script)* | `392b1c88` |  | 2026-10-01, in the app, Haiku, one sample, relaunched with `Adapters:0` (`Id=claude`, `Command=node`, `PromptBlocks=false`; the locator found the stock adapter). A fresh PNG's banner text was asked for with no tools: the Agent replied `CANNOT SEE`. |

### PROMPTBLOCKS-06 — An Adapter that never advertised image gets a path line

File: [`docs/engineering/manual-tests/prompt-blocks.md`](prompt-blocks.md#promptblocks-06--an-adapter-that-never-advertised-image-gets-a-path-line)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Adapter commands

### COMMANDS-01 — `@Scratch /compact` compacts the conversation and says so

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-01--scratch-compact-compacts-the-conversation-and-says-so)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-02 — A bare slash is still Huddle's own command

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-02--a-bare-slash-is-still-huddles-own-command)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-03 — A command the profile does not allow is an ordinary Message

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-03--a-command-the-profile-does-not-allow-is-an-ordinary-message)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-04 — The card lists what the Agent offers, and only while it is running

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-04--the-card-lists-what-the-agent-offers-and-only-while-it-is-running)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-05 — Stop during a compaction leaves the Agent usable

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-05--stop-during-a-compaction-leaves-the-agent-usable)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-06 — An Agent's `@Kai /compact` is an ordinary Message (C7)

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-06--an-agents-kai-compact-is-an-ordinary-message-c7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### COMMANDS-07 — A compaction is not charged to the token Budget (C15)

File: [`docs/engineering/manual-tests/adapter-commands.md`](adapter-commands.md#commands-07--a-compaction-is-not-charged-to-the-token-budget-c15)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Avatar library: choosing a face from the library

### AVATARLIB-01 — A library switched off: no button anywhere

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-01--a-library-switched-off-no-button-anywhere)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-02 — `master.json` missing: no button, but the picture mount stays

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-02--masterjson-missing-no-button-but-the-picture-mount-stays)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-03 — The button appears only under **An image**, and the dialog opens at 48 of 324 (L1, M1, M2)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-03--the-button-appears-only-under-an-image-and-the-dialog-opens-at-48-of-126-l1-m1-m2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-04 — The dialog layout matches the mock-up, and the filters stay in view (M2)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-04--the-dialog-layout-matches-the-mock-up-and-the-filters-stay-in-view-m2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-05 — Pictures carry no visible text, caption or tooltip (D-4, L15)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-05--pictures-carry-no-visible-text-caption-or-tooltip-d-4-l15)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-06 — Screen reader announces each picture (L15)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-06--screen-reader-announces-each-picture-l15)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-07 — Skin switch narrows to one group (L2)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-07--skin-switch-narrows-to-one-group-l2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-08 — Search matches the start of a word, not the middle (L3)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-08--search-matches-the-start-of-a-word-not-the-middle-l3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-09 — A selected profession floats left with an X; the X restores it (L4, L7)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-09--a-selected-profession-floats-left-with-an-x-the-x-restores-it-l4-l7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-10 — Two professions combine by OR, and both chips stay at the left (L5, D-6)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-10--two-professions-combine-by-or-and-both-chips-stay-at-the-left-l5-d-6)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-11 — Two tags combine by AND (L6, D-6)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-11--two-tags-combine-by-and-l6-d-6)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-12 — Filters combine across the four controls as AND (L8 setup)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-12--filters-combine-across-the-four-controls-as-and-l8-setup)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-13 — Clear all resets everything (L8)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-13--clear-all-resets-everything-l8)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-14 — Nothing matches: the empty state, and **Clear all** stays (L12, M4)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-14--nothing-matches-the-empty-state-and-clear-all-stays-l12-m4)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-15 — The narrowed state matches the mock-up, and other chips do not move (M3, D-5)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-15--the-narrowed-state-matches-the-mock-up-and-other-chips-do-not-move-m3-d-5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-16 — **Show more** adds 48, and any filter change returns to 48 at the top (grid paging)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-16--show-more-adds-48-and-any-filter-change-returns-to-48-at-the-top-grid-paging)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-17 — The tag cloud shows 40, **Show all** reveals the rest, and a selected tag stays visible (tag cap)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-17--the-tag-cloud-shows-12-show-all-reveals-the-rest-and-a-selected-tag-stays-visible-tag-cap)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-18 — Picking copies the file, the card shows the new image, and nothing is written until Save (L9, L10)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-18--picking-copies-the-file-the-card-shows-the-new-image-and-nothing-is-written-until-save-l9-l10)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-19 — Settings → Profile: the Avatar window's Save writes at once (L11)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-19--settings--profile-the-avatar-windows-save-writes-at-once-l11)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-20 — A malformed `master.json` shows the unreadable message and one warning (L14, M5)

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-20--a-malformed-masterjson-shows-the-unreadable-message-and-one-warning-l14-m5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-21 — Skipped records are reported once, with a count and examples

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-21--skipped-records-are-reported-once-with-a-count-and-examples)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-22 — Reopening reads the file afresh (R-1, L16); Escape and Enter behave

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-22--reopening-reads-the-file-afresh-r-1-l16-escape-and-enter-behave)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-23 — The picker sits above the Avatar window and the card, and focus and Escape stay in order

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-23--the-picker-sits-above-the-avatar-window-and-the-card-and-focus-and-escape-stay-in-order)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-24 — Real library, first look: 324 pictures, each the right size and shape

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-24--real-library-first-look-126-pictures-each-the-right-size-and-shape)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-25 — On Settings → Profile, a half-made choice in the Avatar window stays chosen, and an outside edit still reaches the card

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-25--on-settings--profile-a-half-made-choice-in-the-avatar-window-stays-chosen-and-an-outside-edit-still-reaches-the-card)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-26 — A library file that vanishes before the pick: "That image could not be read."

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-26--a-library-file-that-vanishes-before-the-pick-that-image-could-not-be-read)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-27 — The Avatar window opens from the avatar in every card mode, and applies or saves as the mode says

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-27--the-avatar-window-opens-from-the-avatar-in-every-card-mode-and-applies-or-saves-as-the-mode-says)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### AVATARLIB-28 — Nothing configured: the bundled library is offered

File: [`docs/engineering/manual-tests/avatar-library.md`](avatar-library.md#avatarlib-28--nothing-configured-the-bundled-library-is-offered)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## File Changes, Watched Folders and Memory

### FM-0 — An Agent's own memory edit in one Room is news in its other Rooms

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-0--an-agents-own-memory-edit-in-one-room-is-news-in-its-other-rooms)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-1 — A file edited outside Huddle is listed on the next Turn

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-1--a-file-edited-outside-huddle-is-listed-on-the-next-turn)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-2 — The same edit, made while Huddle was closed

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-2--the-same-edit-made-while-huddle-was-closed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-3 — A note the Agent writes itself is not listed back on its next Turn

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-3--a-note-the-agent-writes-itself-is-not-listed-back-on-its-next-turn)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-4 — `watch_folder` on a shared folder, then a restart, then an outside edit

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-4--watch_folder-on-a-shared-folder-then-a-restart-then-an-outside-edit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-5 — Renaming a watched Agent does not spuriously list anything

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-5--renaming-a-watched-agent-does-not-spuriously-list-anything)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-6 — Isolation and Memory across a restart, with a real preference

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-6--isolation-and-memory-across-a-restart-with-a-real-preference)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `9d2c785f` |  | By an agent, run with FC-V2 on the paid lane; Keeper on Sonnet low, `"autoMemoryEnabled": true` in the owner's `~/.claude/settings.json` (owner's consent). The Room B with Aide was the Keeper's Growth chat instead (no Aide on the lane). Step 1: the Keeper first declined, saying a standing rule belongs in AGENTS.md, and offered its own notes; told "Yes, keep it in your own notes", it wrote `memory\tester-preferences.md` (266 bytes). After a restart from the Agent card (new session in the log), it answered in another Room with a C# `Add` and named `tester-preferences.md`. Step 5: see FC-V2. Script updated for the AGENTS.md offer. |

### FM-7 — Room-only instructions stay out of Memory

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-7--room-only-instructions-stay-out-of-memory)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FM-8 — An Agent describes its own memory honestly

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fm-8--an-agent-describes-its-own-memory-honestly)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FC-V1 — Does isolation actually drop the Human's own settings and `CLAUDE.md`?

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fc-v1--does-isolation-actually-drop-the-humans-own-settings-and-claudemd)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `9d2c785f` |  | By an agent with the owner's consent; stock Adapter profile; Keeper on Sonnet low. Markers in the owner's `~/.claude`: `"outputStyle": "Explanatory"` (its replies carry `★ Insight` blocks; the user-level Explanatory plugin was already on too) and a new `CLAUDE.md` telling an Agent named Keeper in Huddle to end every reply with PINEAPPLE. The lane started after both were set. The reply to "What is a good name for a spreadsheet of client notes?" was `I'd name it client-notes.xlsx. Keep it plain, and add a date or quarter (for example client-notes-2026-q4) if you keep one per period.`: no PINEAPPLE, no Insight block. Two later replies, one after a restart, carried neither. `~/.claude` restored afterwards (`settings.json` hash matches the backup; `CLAUDE.md` removed, as there was none). |

### FC-V2 — Does `autoMemoryEnabled: false` actually stop auto-memory?

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fc-v2--does-automemoryenabled-false-actually-stop-auto-memory)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `9d2c785f` |  | By an agent with FM-6, Keeper on Sonnet low, `"autoMemoryEnabled": true` in the owner's `~/.claude/settings.json`. From the markers (21:28) to the end, the only new files under `~\.claude\projects\C--seeds-huddle-paid-data-*` were session transcripts (`.jsonl`), and no file under any `memory\` folder in `~\.claude\projects` changed. `C--seeds-huddle-paid-data-Agents\memory\` exists but is empty, made at the lane's first start (19:09), before this run. Settings restored afterwards. |

### FC-V4 — Does a per-profile `CLAUDE_CONFIG_DIR` move the login? (only if FC-V1 or FC-V2 fails)

File: [`docs/engineering/manual-tests/file-changes.md`](file-changes.md#fc-v4--does-a-per-profile-claude_config_dir-move-the-login-only-if-fc-v1-or-fc-v2-fails)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Room Sessions: per-Room context, resume and Stop

### RS-M1 — The two-trip stress test

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m1--the-two-trip-stress-test)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M2 — A Room-scoped preference crosses Rooms only through Memory

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m2--a-room-scoped-preference-crosses-rooms-only-through-memory)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M3 — Asked about another Room, the Agent says it cannot see it

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m3--asked-about-another-room-the-agent-says-it-cannot-see-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M4 — Stop in one Room leaves another's Turn running

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m4--stop-in-one-room-leaves-anothers-turn-running)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M5 — Idle close, resume, and an app restart, both remembering

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m5--idle-close-resume-and-an-app-restart-both-remembering)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M6 — Restart forgets; the next Turn is fresh with Catch-up

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m6--restart-forgets-the-next-turn-is-fresh-with-catch-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M7 — A shared-session Adapter Profile is unchanged

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m7--a-shared-session-adapter-profile-is-unchanged)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M8 — Process and memory cost at the live cap

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m8--process-and-memory-cost-at-the-live-cap)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M9 — The Human's own Claude Code settings do not leak in

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m9--the-humans-own-claude-code-settings-do-not-leak-in)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-M10 — A coordinator's overview, deliberately reassembled

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-m10--a-coordinators-overview-deliberately-reassembled)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-V3 — Resume survives a rename

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-v3--resume-survives-a-rename)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### RS-V5 — Does `agency-acp` hold several sessions, and advertise resume?

File: [`docs/engineering/manual-tests/room-sessions.md`](room-sessions.md#rs-v5--does-agency-acp-hold-several-sessions-and-advertise-resume)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Team chats: the Team Room, Team chats in the sidebar, the Members-tab Message, archiving and Task wakes and digests

### TEAMCHATS-01 — The Team Room appears under its Team with every member, and Rooms does not list it

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-01--the-team-room-appears-under-its-team-with-every-member-and-rooms-does-not-list-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-02 — An Agent who joins the Team is invited into the Team Room and the Transcript says so

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-02--an-agent-who-joins-the-team-is-invited-into-the-team-room-and-the-transcript-says-so)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-03 — New chat on a Team scopes the Room to that Team, and a non-member can be ticked

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-03--new-chat-on-a-team-scopes-the-room-to-that-team-and-a-non-member-can-be-ticked)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-04 — Message on the Members tab opens the Team's own one-to-one, not the root one

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-04--message-on-the-members-tab-opens-the-teams-own-one-to-one-not-the-root-one)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-05 — New chat in the Rooms group still makes a root Room

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-05--new-chat-in-the-rooms-group-still-makes-a-root-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-06 — The Team Room has no rename pencil, and a rename through the Chat header is refused

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-06--the-team-room-has-no-rename-pencil-and-a-rename-through-the-chat-header-is-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-07 — Renaming the Team folder in Explorer archives its Rooms, history intact

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-07--renaming-the-team-folder-in-explorer-archives-its-rooms-history-intact)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-08 — An archived Team Room is not recreated or refilled

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-08--an-archived-team-room-is-not-recreated-or-refilled)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-09 — A Task wake whose origin Room is gone lands in the Team's Room

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-09--a-task-wake-whose-origin-room-is-gone-lands-in-the-teams-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-10 — A digest posts in the Team Room with a Mention of its recipient

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-10--a-digest-posts-in-the-team-room-with-a-mention-of-its-recipient)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TEAMCHATS-11 — A digest falls back to the recipient's root one-to-one when the Team Room is archived

File: [`docs/engineering/manual-tests/team-chats.md`](team-chats.md#teamchats-11--a-digest-falls-back-to-the-recipients-root-one-to-one-when-the-team-room-is-archived)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Chat uploads: the paperclip, paste and drop, the chips, the Message's path lines, the Uploads root and Room delete

### UPLOADS-01 — The paperclip attaches a file as a chip, saved into the Room's Uploads folder

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-01--the-paperclip-attaches-a-file-as-a-chip-saved-into-the-rooms-uploads-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-02 — A pasted screenshot is saved under a time-stamped name

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-02--a-pasted-screenshot-is-saved-under-a-time-stamped-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-03 — Dragging a file onto the composer shows the drop hint and attaches it

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-03--dragging-a-file-onto-the-composer-shows-the-drop-hint-and-attaches-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-04 — A refused file shows its reason on the chip and is not saved

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-04--a-refused-file-shows-its-reason-on-the-chip-and-is-not-saved)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-05 — Send posts the path lines with the typed text, and the path is a Library link

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-05--send-posts-the-path-lines-with-the-typed-text-and-the-path-is-a-library-link)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-06 — Removing a chip deletes its saved file, and nothing is posted

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-06--removing-a-chip-deletes-its-saved-file-and-nothing-is-posted)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-07 — Deleting a Room sends its uploads to the Recycle Bin and says how many

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-07--deleting-a-room-sends-its-uploads-to-the-recycle-bin-and-says-how-many)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### UPLOADS-08 — The Uploads root and the attached files look right in light and dark Themes

File: [`docs/engineering/manual-tests/uploads.md`](uploads.md#uploads-08--the-uploads-root-and-the-attached-files-look-right-in-light-and-dark-themes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Teams: Sidebar, Members, Files, Tasks and Shared Memory

### TP-T0 — Click Business team and open Members tab

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t0--click-business-team-and-open-members-tab)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T1 — Members tab shows the roster with status and Message

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t1--members-tab-shows-the-roster-with-status-and-message)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T2 — Remove from team keeps the Agent

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t2--remove-from-team-keeps-the-agent)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T4 — Open Files tab and create new note

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t4--open-files-tab-and-create-new-note)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T5 — Search files by name

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t5--search-files-by-name)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T6 — Open Project page scoped to project folder

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t6--open-project-page-scoped-to-project-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T7 — Open Tasks tab on Team and create new task

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t7--open-tasks-tab-on-team-and-create-new-task)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T8 — Open Tasks tab on Project and create new task

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t8--open-tasks-tab-on-project-and-create-new-task)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T10 — Create new Project from Team actions menu

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t10--create-new-project-from-team-actions-menu)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T11 — Refuse project name "memory"

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t11--refuse-project-name-memory)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T12 — Team Memory file appears in another member's next Turn

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t12--team-memory-file-appears-in-another-members-next-turn)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T14 — Team exists with no members but has Members page

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t14--team-exists-with-no-members-but-has-members-page)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T15 — Team with no folder yet creates folder on first save

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t15--team-with-no-folder-yet-creates-folder-on-first-save)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-T16 — Details tab saves the purpose; the Members tab hires and deletes

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-t16--details-tab-saves-the-purpose-the-members-tab-hires-and-deletes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TP-REAL-02 — Dark theme and narrow-window styling

File: [`docs/engineering/manual-tests/team-pages.md`](team-pages.md#tp-real-02--dark-theme-and-narrow-window-styling)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Turn detail and Spend

### TURNDETAIL-01 — An edit shows a live, expandable preview in a Room

File: [`docs/engineering/manual-tests/turn-detail.md`](turn-detail.md#turndetail-01--an-edit-shows-a-live-expandable-preview-in-a-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TURNDETAIL-02 — Spend is the Adapter's running total across two Turns

File: [`docs/engineering/manual-tests/turn-detail.md`](turn-detail.md#turndetail-02--spend-is-the-adapters-running-total-across-two-turns)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TURNDETAIL-03 — A Persona on `agency-acp` shows rows with no preview and no Spend line

File: [`docs/engineering/manual-tests/turn-detail.md`](turn-detail.md#turndetail-03--a-persona-on-agency-acp-shows-rows-with-no-preview-and-no-spend-line)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TURNDETAIL-04 — The list and preview at 320 px and under three Themes

File: [`docs/engineering/manual-tests/turn-detail.md`](turn-detail.md#turndetail-04--the-list-and-preview-at-320-px-and-under-three-themes)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TURNDETAIL-05 — A burst of ten quick tool calls neither reorders nor flickers the list

File: [`docs/engineering/manual-tests/turn-detail.md`](turn-detail.md#turndetail-05--a-burst-of-ten-quick-tool-calls-neither-reorders-nor-flickers-the-list)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Global Search: the box, the dropdown and the jump

### GLOBALSEARCH-01 — Ctrl K, slash and a click open the box (GS-M1)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-01--ctrl-k-slash-and-a-click-open-the-box-gs-m1)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-02 — Ctrl K reaches the app in Edge and not the address bar (GS-M2)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-02--ctrl-k-reaches-the-app-in-edge-and-not-the-address-bar-gs-m2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-03 — Groups fill one by one on a large pinned root, and stale results never show (GS-M3)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-03--groups-fill-one-by-one-on-a-large-pinned-root-and-stale-results-never-show-gs-m3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-04 — Content hits show the bold match, the 1 MB cut-off and the incomplete note (GS-M4)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-04--content-hits-show-the-bold-match-the-1-mb-cut-off-and-the-incomplete-note-gs-m4)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-05 — A Room hit jumps to its Message (GS-M5)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-05--a-room-hit-jumps-to-its-message-gs-m5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-06 — Archived Rooms and Closed Tasks come last and dimmed (GS-M6)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-06--archived-rooms-and-closed-tasks-come-last-and-dimmed-gs-m6)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-07 — A locked folder in a pinned root does not hide the other groups (GS-M7)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-07--a-locked-folder-in-a-pinned-root-does-not-hide-the-other-groups-gs-m7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-08 — The keyboard alone reaches every chip and card (GS-M8)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-08--the-keyboard-alone-reaches-every-chip-and-card-gs-m8)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-09 — The search box sits in the Top Bar beside the breadcrumb (GS-M9)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-09--the-search-box-sits-in-the-top-bar-beside-the-breadcrumb-gs-m9)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### GLOBALSEARCH-10 — The dropdown height and width (GS-M10)

File: [`docs/engineering/manual-tests/global-search.md`](global-search.md#globalsearch-10--the-dropdown-height-and-width-gs-m10)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Needs You: the sidebar card of Rooms waiting on the Human

### NEEDSYOU-01 — A Mention is rebuilt at startup, listed oldest first, and the card is hidden when nothing waits

File: [`docs/engineering/manual-tests/needs-you.md`](needs-you.md#needsyou-01--a-mention-is-rebuilt-at-startup-listed-oldest-first-and-the-card-is-hidden-when-nothing-waits)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### NEEDSYOU-02 — Opening a Room does not clear it; posting in it does

File: [`docs/engineering/manual-tests/needs-you.md`](needs-you.md#needsyou-02--opening-a-room-does-not-clear-it-posting-in-it-does)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### NEEDSYOU-03 — Dismiss hides a Room without answering what is behind it

File: [`docs/engineering/manual-tests/needs-you.md`](needs-you.md#needsyou-03--dismiss-hides-a-room-without-answering-what-is-behind-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### NEEDSYOU-04 — Archiving hides a Room from the card, and unarchiving brings it back

File: [`docs/engineering/manual-tests/needs-you.md`](needs-you.md#needsyou-04--archiving-hides-a-room-from-the-card-and-unarchiving-brings-it-back)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Jobs: scheduled instructions in their own Rooms

### JOBS-01 — A Job for an Agent runs in its own Room

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-01--a-job-for-an-agent-runs-in-its-own-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-02 | Pass | `8c89c795` + uncommitted `feat/schedules` | | `software-co` seed, `run.ps1 -Seed` with ACP on, claude-agent-acp. New job from the Scheduled page for Ben, Once, today 16:49, America/Los_Angeles (the form's new default), instruction `Reply in one short sentence: say which day of the week it is today. Do not use any tools.` Log `Job job-137e1d ran in Room 01a19ee1…` within the 16:49 tick. Room `⏰ Invoice check`, members Ben and the Human; Huddle's Message `@Ben ⏰ Invoice check` then the instruction; Ben replied `Today is Monday, 2 November 2026.` Ben's one-to-one unchanged and still listed apart. The one-off then left Scheduled and Ben's `schedule.md` was deleted. Found and fixed during the run: the browser's time input handed back `16:48:00`, which the form refused; `JobForm` now drops the seconds (`TryBuild_TimeWithSeconds_IsRead`). One Turn. |

### JOBS-02 — A Job for the Human goes on Needs You

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-02--a-job-for-the-human-goes-on-needs-you)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### JOBS-03 — The file is the source of truth

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-03--the-file-is-the-source-of-truth)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### JOBS-04 — A restart neither repeats nor sends late

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-04--a-restart-neither-repeats-nor-sends-late)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### JOBS-05 — Only the Chief of Staff schedules for others

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-05--only-the-chief-of-staff-schedules-for-others)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-02 | Pass | `8c89c795` + uncommitted `feat/schedules` | | Same run. In Ravi's one-to-one, asked for a daily 09:00 Job for the Chief of Staff: Ravi called `jobs` and quoted `You can only schedule Jobs for yourself. Ask the Chief of Staff to schedule one for another Agent or for the Human.` Nothing was written. In the Chief of Staff's one-to-one, asked for a weekday 09:00 Job for Ravi: the tool answered `Scheduled job-cd16de "Bins check" for Ravi: weekdays at 09:00. It runs next on Tue 3 Nov 2026 at 09:00 (Pacific Standard Time).`; `Agents/Ravi/schedule.md` held it with `by: Chief of Staff`; Scheduled listed `Bins check · Ravi`. The zone in the answer was the Windows id; fixed after the run to name the IANA zone. Both test Jobs were then deleted by removing their files, and the view emptied within a second. Two Turns. |

### JOBS-06 — A monthly Job runs on its day, or on the last day of a short month

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-06--a-monthly-job-runs-on-its-day-or-on-the-last-day-of-a-short-month)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### JOBS-07 — A manager schedules for its Team, and for no one else

File: [`docs/engineering/manual-tests/jobs.md`](jobs.md#jobs-07--a-manager-schedules-for-its-team-and-for-no-one-else)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Right sidebar: the panels by Subject, Outline, collapse and focus

### RSIDE-01 — A Room shows Members, Tasks and Documents

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-01--a-room-shows-members-tasks-and-documents)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent on the UAT lane. Order, counts, 280px width and right-edge placement hold at 90% zoom. The heading icon is hidden by the default skin's hairline surface (app.css, `data-hu-surface="hairline"`), so the Pass-if line about an icon needs rewording for skins. |

### RSIDE-02 — A Message that references a Task appears, and its jump works

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-02--a-message-that-references-a-task-appears-and-its-jump-works)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. The Task form has no Room field, so the precondition cannot be met as written and is not needed. The `#` picker inserts the Task id. |

### RSIDE-03 — A Markdown document shows Outline and Linked documents

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-03--a-markdown-document-shows-outline-and-linked-documents)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. A Markdown document now shows five panels (Outline, Linked documents, Backlinks, Tags, History), not the two the test names. |

### RSIDE-04 — An Outline heading scrolls the document in Read, Split and Edit

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-04--an-outline-heading-scrolls-the-document-in-read-split-and-edit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. Read: Scope at offset 0. Split: the preview scrolls and the editor does not, as the spec says. Edit: the editor scrolls (offset 3). |

### RSIDE-05 — Typing a heading in Split updates the Outline

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-05--typing-a-heading-in-split-updates-the-outline)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. Risks appeared after Scope within 2 s and stayed one entry. |

### RSIDE-06 — A linked note opens from the Linked documents panel

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-06--a-linked-note-opens-from-the-linked-documents-panel)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. The panel names notes without `.md`. |

### RSIDE-07 — A collapsed panel stays collapsed after a reload

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-07--a-collapsed-panel-stays-collapsed-after-a-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. Real reload; the collapsed panel stayed collapsed. |

### RSIDE-08 — Two panes follow focus

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-08--two-panes-follow-focus)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | fix/451-pane-focus | #451 | run 2026-11-07 by an agent after the fix for #451 (`FocusOnNavigate` now targets `.tab-group.focused h1`): all three steps hold, and focus lands on the focused pane's heading. The first run that day failed. |
| 2026-11-07 | Fail | `83da2ce2` |  | By an agent. Clicking the right pane focuses it, then `FocusOnNavigate Selector="h1"` (Routes.razor) focuses the left pane's Room heading, whose focusin refocuses the left pane and navigates back, so the Right sidebar keeps the Room panels. Issue to file. |

### RSIDE-09 — An Agent's context bar appears, grows during a Turn and falls after /compact

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-09--an-agents-context-bar-appears-grows-during-a-turn-and-falls-after-compact)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-25 | Pass | `7144f483` + uncommitted `feat/context-meter` |  | Isolated `uat` seed, Ada and Bo on Haiku for `Scratch` and `Sidekick`, claude-agent-acp. Step 1: both `—`. Step 2: Ada's tooltip went 24,129 → 25,971 of 200,000 during the Turn (bar 12%). Step 3: Bo `—`. Step 5: 1%, 2,659 tokens "in this Room", matching "Compacted my conversation: 26,057 → 2,659 tokens". Step 2's Pass-if was reworded from "the percentage rises" to "the token count in its tooltip rises", because a short Turn stays within 12%. Also checked: a 50-character name ellipsises beside the percentage in the 280px drawer and the row stays 32px. The bar renders 4px, not the 3px in `app.css`. |

### RSIDE-10 — The Backlinks panel lists the open Tasks that link to a document

File: [`docs/engineering/manual-tests/right-sidebar.md`](right-sidebar.md#rside-10--the-backlinks-panel-lists-the-open-tasks-that-link-to-a-document)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. The Task description has no `@` picker, so the path was typed in a code span. |

## References in a Message: the @ picker, Copy link and the Referenced items header

### ATTAG-01 — The picker opens on an at sign at a word start (AT-M1)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-01--the-picker-opens-on-an-at-sign-at-a-word-start-at-m1)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-02 — Picking an Agent writes its Name and wakes only that Agent (AT-M2)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-02--picking-an-agent-writes-its-name-and-wakes-only-that-agent-at-m2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-03 — A Task pick writes the plain id and the hash key gives the same text (AT-M3)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-03--a-task-pick-writes-the-plain-id-and-the-hash-key-gives-the-same-text-at-m3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-04 — Room and Team picks write links and labels tell same-named Rooms apart (AT-M4)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-04--room-and-team-picks-write-links-and-labels-tell-same-named-rooms-apart-at-m4)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-05 — A Library pick writes a code span path and the upload line matches (AT-M5)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-05--a-library-pick-writes-a-code-span-path-and-the-upload-line-matches-at-m5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-06 — Renames and deletes show the chips and an old Transcript opens unchanged (AT-M6)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-06--renames-and-deletes-show-the-chips-and-an-old-transcript-opens-unchanged-at-m6)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-07 — The Agent prompt carries the Referenced items header (AT-M7)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-07--the-agent-prompt-carries-the-referenced-items-header-at-m7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-08 — Keyboard alone picks, sends nothing while open and announces the count (AT-M8)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-08--keyboard-alone-picks-sends-nothing-while-open-and-announces-the-count-at-m8)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-09 — The picker height and a narrow window (AT-M9)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-09--the-picker-height-and-a-narrow-window-at-m9)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-10 — Undo after a pick restores the typed token (AT-M10)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-10--undo-after-a-pick-restores-the-typed-token-at-m10)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-11 — The picker lists Rooms and Library files by name only (AT-M11)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-11--the-picker-lists-rooms-and-library-files-by-name-only-at-m11)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### ATTAG-12 — Copy link from the header, the sidebar and a Team Room (AT-M12)

File: [`docs/engineering/manual-tests/at-tagging.md`](at-tagging.md#attag-12--copy-link-from-the-header-the-sidebar-and-a-team-room-at-m12)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Document history: the byline, the History and the Recent edits panel

### DOCHISTORY-01 — Two Agents create and edit a document; the byline and the panel show both

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-01--two-agents-create-and-edit-a-document-the-byline-and-the-panel-show-both)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-02 — A review that changes nothing is a "Reviewed by"

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-02--a-review-that-changes-nothing-is-a-reviewed-by)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-03 — Saving in the Library adds an entry under the Human's Name without a reload

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-03--saving-in-the-library-adds-an-entry-under-the-humans-name-without-a-reload)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-04 — Renaming and moving a document carries its History

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-04--renaming-and-moving-a-document-carries-its-history)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-05 — How often do real Agents log their edits?

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-05--how-often-do-real-agents-log-their-edits)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-06 — The History is grouped by day, whole, and the byline leads to it

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-06--the-history-is-grouped-by-day-whole-and-the-byline-leads-to-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-07 — An Agent offered log_change gets the Stop hook, and existing settings stay

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-07--an-agent-offered-log_change-gets-the-stop-hook-and-existing-settings-stay)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### DOCHISTORY-08 — The hook sends an Agent back to log, and leaves other Turns alone

File: [`docs/engineering/manual-tests/doc-history.md`](doc-history.md#dochistory-08--the-hook-sends-an-agent-back-to-log-and-leaves-other-turns-alone)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Human gate: the Proof list, the decision card, Accept and Decline, the board drag and the Needs You page

### HUMANGATE-01 — Requires your acceptance: only the Human can mark the Task Done (U-1)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-01--requires-your-acceptance-only-the-human-can-mark-the-task-done-u-1)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Paid run, Research Team on Haiku (RESE-0001): early move to Needs You refused at 0/4, allowed at 4/4; Accept took it to Done, logged by the Human. Proof list appeared only after a reload right after turning the gate on (bugs/hg-01-1). |

### HUMANGATE-02 — An Action locks the gate on, and Accept wakes the assignee (U-2)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-02--an-action-locks-the-gate-on-and-accept-wakes-the-assignee-u-2)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Placeholder, locked switch and its aria-label match; after Accept the card read "Accepted by You. Ethan can now send the reply to Acme." and the Task stayed in Needs You; Ethan then moved it to Done. |

### HUMANGATE-03 — Proof 3/4: Accept is locked, Request changes works, Ask posts a Mention (U-3)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-03--proof-34-accept-is-locked-request-changes-works-ask-posts-a-mention-u-3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Ran with Sources checked (owner Claire) as the open item instead of the checklist detail: "Proof 3/4 — Sources checked still open (Claire)." Accept disabled; Ask and Request changes as written. |

### HUMANGATE-04 — An Agent cannot mark a gated Task Done (U-4)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-04--an-agent-cannot-mark-a-gated-task-done-u-4)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | The Agent's move to Done was refused with the H-4 text; the Task stayed in Review. Copy reads "needs You's acceptance" when the Human has no name (bugs/hg-04-1). |

### HUMANGATE-05 — Dragging a gated card to Done on the board (U-5)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-05--dragging-a-gated-card-to-done-on-the-board-u-5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Real drag: from Review "RESE-0008 goes to Needs you first (Proof 3/4)."; from Needs You "Your decision on RESE-0009", Cancel kept the card, Accept moved it to Done. |

### HUMANGATE-06 — Editing the Task after Accept takes the acceptance away (U-6)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-06--editing-the-task-after-accept-takes-the-acceptance-away-u-6)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Chip "Accepted"; after the description edit "Changed since you accepted it." and the Task listed again "Waiting on you · Proof 4/4". The brief absent-from-the-list state was not observed. |

### HUMANGATE-07 — Decline closes the Task with its reason (U-7)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-07--decline-closes-the-task-with-its-reason-u-7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | Decline disabled with an empty Reason; "Wrong customer" closed it as Rejected, logged "declined: Wrong customer", and woke Ethan with the reason. |

### HUMANGATE-08 — An outside edit that removes the Action is put back (U-8)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-08--an-outside-edit-that-removes-the-action-is-put-back-u-8)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | The action: line was restored within 4 s and the panel kept the Action; the log text says "Only You can turn off the Human gate…" (spec shares H-1's text; bugs/hg-08-1). |

### HUMANGATE-09 — Restart keeps every waiting Task on the Needs You page (U-9)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-09--restart-keeps-every-waiting-task-on-the-needs-you-page-u-9)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-19 | Pass | 7b46143c |  | The same three Tasks listed before and after a restart; page and Tab title "Needs you". Empty state not exercised. |

### HUMANGATE-10 — An Agent that rewrites its own settings still waits for the merge gate (issue #432)

File: [`docs/engineering/manual-tests/human-gate.md`](human-gate.md#humangate-10--an-agent-that-rewrites-its-own-settings-still-waits-for-the-merge-gate-issue-432)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `db2a10d0` + uncommitted fix | #432 | By an agent (Sonnet at low Effort, 51 sessions, 1.80 USD), at the handler and Adapter level with the real `_meta`, not through the running app. Steps 1 to 5 as written: merge refused, also after a `settings.local.json` rewrite, a hook-script overwrite and `disableAllHooks` (same and new session, and with no hook); `git -c`, `bash -c` and `git push` refused; `Edit` of `AGENTS.md` and `settings.local.json` refused; ordinary `Edit`, `Write` and `echo` ran. Known limit confirmed: `bash merge.sh` still merges. PowerShell and `acceptEdits` and resume also checked. No Human prompt tested. |

## Helpers: the Helpers tab, Save and Reset, the Needs Human judgement, the override, the daily cap and a failing Adapter

### HELPERS-01 — The Helpers tab shows the Needs Human card (U-1 setup)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-01--the-helpers-tab-shows-the-needs-human-card-u-1-setup)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-02 — Save and Reset to defaults, and a restart keeps both (U-7 setup)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-02--save-and-reset-to-defaults-and-a-restart-keeps-both-u-7-setup)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-03 — An Agent's Task that leaves the team gets the gate (U-1, U-5)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-03--an-agents-task-that-leaves-the-team-gets-the-gate-u-1-u-5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-04 — A harmless Task is left alone (U-2, U-4, U-11)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-04--a-harmless-task-is-left-alone-u-2-u-4-u-11)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-05 — The Human's override is final (U-5)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-05--the-humans-override-is-final-u-5)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-06 — The daily cap pauses the Helper and Continue allows a few more (U-7)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-06--the-daily-cap-pauses-the-helper-and-continue-allows-a-few-more-u-7)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### HELPERS-07 — A failure leaves the Task as saved and says why (U-8, U-10)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-07--a-failure-leaves-the-task-as-saved-and-says-why-u-8-u-10)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `bfe93189` |  | By an agent on the paid lane with `Team__Helpers__NeedsHuman__Adapter=nope` and **Reset to defaults** on the card (the saved row had overridden the setting in the first run). The Keeper (Sonnet low) made GROW-0011 `Reply to Acme about the renewal price`: the file has only the Keeper's `created` entry and no gate; the card shows `Last error 22:59 · The Adapter 'nope' is not configured. Pick another on this card.`; calls today stayed at 8. Found: the card's picker said `It will run on 'claude'`, which is false for a Helper; fixed in this change. |

### HELPERS-08 — The wake-up is Huddle's, and it leaves your Mentions alone (Helpers in the Feed §5.6)

File: [`docs/engineering/manual-tests/helpers.md`](helpers.md#helpers-08--the-wake-up-is-huddles-and-it-leaves-your-mentions-alone-helpers-in-the-feed-56)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Partial | `83da2ce2` |  | By an agent; Keeper on Sonnet low. Step 2 passes: a muted one-line row by Huddle, no avatar or name of yours, and Keeper answered; it lands in a new Task Room, not the Room the Task came from. Step 3: the Budget was not reset (1 of 6). The Mention part cannot be judged as written, since the Human's ask answers it. |

## Second Brain: the use cases as a Human's checklist

### SB-01 — A dropped file lands in the Team raw folder

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-01--a-dropped-file-lands-in-the-team-raw-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `d7a9de8d` |  | Saved as `Teams\Finances\raw\<roomId>\2025 return.pdf`, 45 bytes as sent, and the Message carries the path line. The modified time is the upload time, not the original's: the test asks for a match, the Spec does not. The Team Room exists only once a member is connected, so this needs `Team__Acp__Enabled=true` although it is marked free. |

### SB-02 — A fact the Human states is proposed to Team Memory

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-02--a-fact-the-human-states-is-proposed-to-team-memory)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `d7a9de8d` |  | Finn (Haiku) called `propose_team_memory` with the memory tool's arguments (`title`, `content`, `description`) and no `team` or `fact`. The tool replied "You are not a member of . You can propose to: Finances." and Finn told the Human "Done. I've saved that Dana Lee is our accountant." Asked to retry, Finn passed the right arguments: the Helper filed `Finances/memory/team-roles.md` and the Feed shows "Team Memory (Helper) created … Proposed by Finn". Two defects: the refusal for a missing `team` should say it is missing, and the Agent reported a refusal as a success. |
| 2026-11-03 | Pass | `7c882730` |  | Rerun with "Remember that the tax adviser is Sam Ortiz.": Finn loaded the tool's schema (`ToolSearch`) and called `propose_team_memory` once; the Helper merged the fact into `Finances/memory/team-roles.md` and the Feed reads "Proposed by Finn: New financial role to track". Finn did not write the topic itself. |

### SB-03 — A standing rule is proposed as an AGENTS.md change

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-03--a-standing-rule-is-proposed-as-an-agentsmd-change)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `d7a9de8d` |  | `AGENTS.md` stayed unchanged, but no change was ever offered. Without Skills, Finn (Haiku) offered to "set it up in your Claude Code settings using the update-config skill", so the Adapter's own Skills reach the Agent. `propose_retro_changes` is offered only to an Agent with the `retrospective` Skill, which this page's setup does not give; with it, Finn said "I'll lead every summary with a number from this point forward" and called no tool. |
| 2026-11-03 | Pass | `7c882730` |  | Rerun after the summary named `propose_retro_changes`. Finn first asked whether the rule should replace the leftover "SB-04 marker: lead with the date." (test residue); told yes, it called `propose_retro_changes`, the Room showed "Finn is proposing changes" with the diff, `AGENTS.md` was unchanged until **Accept**, and then held "Lead every summary with the number.". A clean second rule drew "This should be a standing rule in the Team AGENTS.md. Should I propose it as a new rule addition?". |

### SB-04 — An edit to Home AGENTS.md reaches the next session

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-04--an-edit-to-home-agentsmd-reaches-the-next-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `d7a9de8d` |  | Run on Finances with Finn: Nova never came online. The running session reported `Teams\Finances\AGENTS.md` "changed" as a File Change (pass). **Stop** cancels the Turn but keeps the session, and the one-to-one Room's session was resumed from before the edit, so both answered "I don't see an SB-04 marker line". "What this Agent reads" does show the marker, so the next session would carry it; step 3 needs another way to open a new session. |
| 2026-11-03 | Pass | `7c882730` |  | Rerun on Home with Nova, with the restart step. Step 3: "Yes—two files: AGENTS.md changed, and _team.md was added." After the app restart: "SB-04 marker: lead with the date." No Agent was restarted from its card. |

### SB-05 — Nova prompt parts show their source

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-05--nova-prompt-parts-show-their-source)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `d7a9de8d` |  | Every part has a source, and the Home row names `…\Teams\Home\AGENTS.md`. Observations: the three `AGENTS.md` rows all read "AGENTS.md block"; file sources show the full `C:\…` path; Team Memory reads "1 Teams" rather than a topic count (the topics are listed in its text); the Prompt links land on the Prompts tab but not on the row, since no element carries the anchor id. |

### SB-06 — Chat rules overridden on Settings and restored by deleting the file

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-06--chat-rules-overridden-on-settings-and-restored-by-deleting-the-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `d7a9de8d` |  | `Prompts\systemPrompt.chatRules.md` held `SB-06 override.`, the warning shows on the row, and after the delete and a restart the row shows the default. The first **Save** after typing did nothing: the text box commits on blur, so it took a second click. |

### SB-07 — Archiving a Team counts its open Tasks and moves the folder

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-07--archiving-a-team-counts-its-open-tasks-and-moves-the-folder)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Blocked | `d7a9de8d` |  | The confirmation read "1 open Tasks and 0 waiting on you will leave the Boards. These Agents will have no Team: Pia." (count right, plural wrong). The folder moved to `Archive\Property Investment\`, and `_team.md` gained `archivedMembers: ['Pia']` and `archivedManager`; Pia's label went. The Team had no Room, so the Room half was not seen. The sidebar listed the Team as live until a reload. |
| 2026-11-03 | Pass | `c8e15b5e` |  | Rerun with every Agent online (own `Team__PipeName`). Same confirmation; the Team Room and Pia's Team-scoped Room left the open list and were recorded as `archivedRooms` beside `archivedMembers` and `archivedManager`; Pia's own one-to-one Room stayed open (D-46). The left rail updated without a reload this time. |

### SB-08 — An archived file opens read-only and unarchiving restores the Team

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-08--an-archived-file-opens-read-only-and-unarchiving-restores-the-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Blocked | `d7a9de8d` |  | A Message naming the old `Teams\Property Investment\outputs\teardown.md` linked to `root=archive`, and the Library opened it with no Read/Edit/Split controls (read-only). Nothing marks it *archived* beyond the path header, which SB-R20 asks for. Unarchive brought the folder back, restored `manager: 'Pia'`, removed the archive keys and gave Pia the label. Pia stayed Offline, so the Rooms half was not seen; a second one-to-one Room for Pia was created. |
| 2026-11-03 | Pass | `c8e15b5e` |  | Rerun with every Agent online. The old link opened the archived file read-only (no Edit control); it is marked only by its `Archive/Property Investment/…` path header, with no separate *archived* label. Unarchive brought back the folder, `manager: 'Pia'`, Pia's label and both archived Rooms. Defect seen: moving the Task file back made the Task watcher post "Task PROP-0001 … was changed by You: edited outside Huddle" to Pia, who spent a Turn and asked the Human a Question; the Feed also showed "You created #PROP-0001" again. It happened on both unarchives. No "unarchived" Feed item is posted. |
| 2026-11-03 | Pass | `7c882730` |  | Rerun after the fixes: the confirmation read "1 open Task … This Agent will have no Team: Pia."; the archived file showed the **Archived** chip beside its path; after unarchive Pia's Room gained no message and PROP-0001's change log gained no "edited outside Huddle" entry. The Spec defines no "unarchived" Feed item, so none is expected. |

### SB-09 — An old data folder logs warnings at start-up

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-09--an-old-data-folder-logs-warnings-at-start-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `d7a9de8d` |  | The old seed with `Agents/Alan/work/Instructions.md` and `prompts.json` added logged four warnings naming `Instructions.md`, `Teams/Home and Family/Process.md`, `Uploads` and `prompts.json`. A folder re-seeded with the current seeder logged none. |

### SB-10 — An Agent cannot overwrite a raw file and writes an extract beside it

File: [`docs/engineering/manual-tests/second-brain.md`](second-brain.md#sb-10--an-agent-cannot-overwrite-a-raw-file-and-writes-an-extract-beside-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `d7a9de8d` |  | The SHA-256 of `statement.pdf` was unchanged and `statement - extract.md` was written beside it. Finn first asked where the file was, then said "I can't modify files in the `raw/` folder"; told to try anyway, it wrote the extract with no visible overwrite attempt, so the permission handler's refusal was not seen. An Adapter error ("API Error: a document in the conversation could not be processed…") leaked into the chat Message. |
| 2026-11-03 | Pass | `7c882730` |  | Rerun of the guard on a readable original: asked to read `raw\<roomId>\balance.txt` and Write over it, Finn quoted "User refused permission to run tool"; the log reads "Refused a tool call for 'Finn' on '…\raw\…\balance.txt': Files in raw/ are kept as they arrived…" and the SHA-256 was unchanged. The stand-in `statement.pdf` is not a valid PDF, so Read failed on it first; use a real PDF or a text original. |

## Second Brain from zero: three Teams hired by hand, documents ingested into memory, and a Development Team building an app

### ZERO-01 — An empty folder starts on the Profile page

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-01--an-empty-folder-starts-on-the-profile-page)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Profile first with the welcome note; listing matched; Chief of Staff kept Haiku and low after a reload. The Packs and Skills notice was not seen by the first screenshot. |
| 2026-11-05 | Pass | `50f223af` |  | Profile first with the welcome note; Sam Rivera saved; the listing matched (Teams memory and AGENTS.md; Agents only the Chief of Staff); the Chief of Staff showed Haiku and Low after a reload. Free launch on a renamed lane, port 5120. |

### ZERO-02 — The Chief of Staff greets a new Human

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-02--the-chief-of-staff-greets-a-new-human)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | One Message with all five parts, ending in one question. |
| 2026-11-05 | Pass | `50f223af` |  | One Message posted within seconds of launch with all five parts, ending in one question; no Team created. Spend 0.007 USD, because ZERO-01 had already set Haiku. A blank install that has not set a model yet greets on the default model (0.33 USD seen on a second lane; draft issue e). |

### ZERO-03 — Hire the Family Team

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-03--hire-the-family-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Three Agents online; folder and _team.md as expected, no workspace line. The role chip stays strongest/balanced after Model changes. |
| 2026-11-05 | Pass | `50f223af` |  | Second attempt; the first ran out of tool calls with no verdict. Family hired: Hana (Sonnet 5.5, Low), Milo and Fern (Haiku 5.5, Low); _team.md as scripted; no Task folder; no other Agent woke. Draft names are random, and one drew Hana for the wrong role, so Save draft failed until it was renamed. |

### ZERO-04 — The manager starts with its process and Second Brain Skills

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-04--the-manager-starts-with-its-process-and-second-brain-skills)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Skills as listed; AGENTS.md hash unchanged after Use this process; every part names a source. Use this process is in the Edit form, not the read card. |
| 2026-11-05 | Pass | `50f223af` |  | skills line as scripted; retrospective ticked and saved; Use this process shows the Team already has one and the Family AGENTS.md hash was identical before and after; What this Agent reads names the three AGENTS.md and Persona sources. |

### ZERO-05 — Onboard the Family Team from a profile document

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-05--onboard-the-family-team-from-a-profile-document)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Onboarding arrived unprompted; nothing written before approval; every note has a description; AM-7741 not stored. |
| 2026-11-05 | Pass with a finding | `50f223af` |  | The onboarding Message arrived with no action and listed the AGENTS.md steps; the file landed in raw; the proposal covered allergies, bin day, check-in day, emergency contact, who owns what and the blue folder, with no policy number; after approval six notes each with a description; the AM-7741 search printed nothing. The clause that Hana has replied was not observed: the app was stopped for a checkpoint while her first Turn was pending. Finding: Hana printed "Wrong parameter name; retrying with text." in chat (draft issue a). |

### ZERO-06 — Ingest the family's documents

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-06--ingest-the-familys-documents)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Answer key covered, PDF read, nothing written before approval, outputs have description and Based on, raw hashes unchanged. Approval set off 12 Agent replies between Hana and Milo. |
| 2026-11-05 | Pass with a finding | `50f223af` |  | Nothing written before approval; the key matched except the Harbor Zoo $129 renewal on 1 December, which is a line inside FAMI-0006 and not its own Task; raw originals unchanged. Expect not met: FAMI-0001 and FAMI-0004 carry action lines but sit in Backlog, so the Tasks page showed 6 open, 0 need you, and due dates appear only in titles (draft issue h). Three files attached together timed out and retries left "(2)" and "(3)" copies in raw (draft issue b). |

### ZERO-07 — A later document corrects a fact in place

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-07--a-later-document-corrects-a-fact-in-place)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | One current date, 20 November; slip stays 13 November; newsletter hash unchanged. Hana corrected without an approval step, as the text told it to. |
| 2026-11-05 | Pass with a finding | `50f223af` |  | One current date, 20 November, with the 19th noted as changed; the slip stays 13 November; the newsletter copies are unedited. Hana corrected and wrote at once with no proposal, so step 2 had nothing to approve. She said FAMI-0001 may still carry the old date; the file has no acceptance line. |

### ZERO-08 — Hire and set up the Bakery Team

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-08--hire-and-set-up-the-bakery-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Connector warning did not block the hire. The listing also held _tasks because Bea filed starter Task HARB-0001: the script listing needs it. |
| 2026-11-05 | Pass | `50f223af` |  | Harbor Lane Bakery hired: Bea (Sonnet, Low), Cole and Cass (Haiku, Low). Cass showed "Needs setup: connector email:read not connected" and Approve & hire still worked. team-management ticked on Bea and saved; no _tasks folder; the onboarding Turn wrote no Memory and created no Task. |

### ZERO-09 — Ingest the bakery's documents

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-09--ingest-the-bakerys-documents)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Key covered, notice Task carries 2026-12-01, hidden SYSTEM NOTE flagged and not obeyed, nothing written before approval. |
| 2026-11-05 | Pass with a finding | `50f223af` |  | Key covered; Bea quoted the SYSTEM NOTE and said she ignored it; nothing written before approval; HARB-0001..0005 in Backlog, none assigned; HARB-0005 holds Due 2026-12-01. Findings: Bea printed "Memory needs text instead of content, so I'm retrying" in chat (draft issue a), and the Room panel lists INV-2043 to INV-2050 as "(unavailable)" Tasks (draft issue c). Files attached one at a time. |

### ZERO-10 — A member who never saw the files uses what was filed

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-10--a-member-who-never-saw-the-files-uses-what-was-filed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #341 | Cole right on every figure. Cass right on Mara and Tess, but the Jonas draft left a placeholder instead of $409.50 ("prices are the owner's"). Asked again, Cass gave exactly $409.50 from memory/wholesale-price-sheet.md, so recall works and the first draft was over-cautious. |
| 2026-11-05 | Pass | `50f223af` |  | Issue 341 verified. Cole: unpaid $3,584.75, overdue $1,715.00 = INV-2043 + INV-2045; INV-2047 not overdue; INV-2050 void. Cass: three drafts, nothing sent, no refund promised, the SYSTEM NOTE flagged; Jonas quote $409.50 with a real price Cass read from Memory. Cole named a raw upload, not a Memory note. |

### ZERO-11 — Family members use the filed memory too

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-11--family-members-use-the-filed-memory-too)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #346 | Milo passed every limit. Fern gave $659.47 and, after correcting itself, $867.22, but named Piano and Zoo as the overlap and Streamline/CineBox only as an alternative. The word overlap in the script is ambiguous. |
| 2026-11-05 | Fail | `50f223af` |  | Fern passed: $659.47, $867.22, CineBox and Streamline Video together $27.98, source named. Milo passed every limit (no tree nut, lactose-free swap, no pork, 30 minutes, did not ask about allergies) but gave no grocery total: "The Family Team has no pantry list or price source I can read". No fixture holds prices, so the clause could not be met honestly: a script fault, fixed on the page. Recorded Fail because the Pass clause as run was not met; the total needs a rerun. |

### ZERO-12 — Prepare a repository and hire the Dev Team

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-12--prepare-a-repository-and-hire-the-dev-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Route B; _team.md matched; all online. Route A not tried. Findings: held-Skill rows swap place on untick, so a second click re-ticks; Copy prompt is overlapped by Re-roll; onboarding created and assigned DEVT-0001 to Ivy, who started paid work unapproved; Devon said it cannot write Team Memory from the one-to-one Room. |
| 2026-11-05 | Pass with a finding | `50f223af` |  | Route B in the main run; Route A tested separately at 365cd12f in its own lane: pass. Route B: the two Skills unticked in the Skills select; _team.md matched; Devon read the repo and proposed a first Task without creating one (#347); Devon wrote three Team Memory notes from the one-to-one Room (#348). Route A: the copied prompt named InstallSkillPack.md; both downloads cleared Needs setup by themselves; hire and _team.md matched. Findings: the fetched test-driven-development differs from the pinned upstream file and was accepted (draft issue d). The Dev Team ran on the default model, Opus, at default Effort, because the Model and Effort picks did not take; this is why the run cost much more. |
| 2026-11-05 | Pass | `079c3256` |  | Rerun on the script's models: Devon and Ivy Sonnet, Rex and Quinn Haiku, lane C:\HuddleZero2, Route B. Model and Effort took this time: both read back before hiring, and persona_models and persona_efforts match the cast table. _team.md matched the expected block. Devon proposed a first Task without creating one (#347) and wrote Team Memory from the one-to-one Room (#348); both verified. Route A not tried. This is a separate run from the Opus row above, which stands as it was. |

### ZERO-13 — A gate refuses Done with no evidence

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-13--a-gate-refuses-done-with-no-evidence)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #346 | Both moves refused and named the gate; Move anyway offered; Cancelled saved. But creating the unassigned Task woke Devon in a new Room for a paid Turn (by design, OR-2, TaskTriggerService), and Devon tried to close it from its title. The script is wrong that no Agent wakes and that this case is free. Board Done needs about 2200px. |
| 2026-11-05 | Inconclusive | `50f223af` |  | Board drag to Done refused with "Could not move DEVT-0001: Gate qa-evidence-before-done ..." and the card stayed in Backlog. No verdict on the edit-form clause: saving Done in the edit form moved the Task from Backlog to Review with no warning and no Move anyway, because the Task carries the Human gate; it never reached Done, so the Fail clause did not fire. Cancelled saved. Under review (draft issue f). Ran on Opus. |

### ZERO-14 — Ingest the spec and the Team's conventions

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-14--ingest-the-spec-and-the-teams-conventions)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #342 | Memory and Task with acceptance line right; nothing written before approval. Devon kept the spec in raw/ and did not propose it for the repository. |
| 2026-11-05 | Pass | `50f223af` |  | Issue 342 evidenced. The spec was proposed for docs/ as a copy, not for outputs; nothing written before approval; DEVT-0002 filed with a human gate and an acceptance line. Devon asked three Questions the script does not answer; Dismissed. Files attached one at a time (three at once timed out). Ran on Opus. |
| 2026-11-05 | Pass | `49f47e1d` |  | Rerun on the script's models (Devon Sonnet). Files attached one at a time; raw\ at fixture sizes; no Questions. Devon proposed copying the spec to docs/ with the price sheet's rules written in, and wrote nothing before approval. DEVT-0001 was filed after approval with an acceptance line and a human gate; the Helper set the Action "Merge feat/quote-calculator into main". An earlier attempt at 71c39eb1 filed a Task with no acceptance line, because its Helper did not gate (#399). This is a separate run from the Opus row above, which stands as it was. |

### ZERO-15 — The Dev Team builds the app

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-15--the-dev-team-builds-the-app)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | DEVT-0003 Done; merge 11daa33 after the Evidence lines; Rex 0 commits. Needed help: Continue x1, nudge x1, a Human diagnosis, and asking for Needs you. Bug: add_evidence folds the note into Command (AddEvidenceTool.cs:167) and the proof compares it to dotnet test exactly (ProofEvaluator.cs:65), so four real runs read "not run" until a bare line was posted. Agents misread which item was open. DEVT-0001 left in Review. |
| 2026-11-05 | Pass | `50f223af` |  | Issue 349 evidenced. Budget Continue pressed once. Huddle moved DEVT-0002 to Needs You at 22:10:11Z, nobody asked; Accept 22:11:37Z; fast-forward merge 22:12:02Z; Done 22:12:49Z. Ines bounced it once; Felix posted dotnet test exit 0, 26 passed; Ines made 0 commits; no refused add_evidence. About 7.30 USD at this point. Ran on Opus. |
| 2026-11-05 | Pass with a finding | `49f47e1d` |  | Rerun on the script's models (Devon and Ivy Sonnet, Rex and Quinn Haiku). Merge waited for Accept: no Agent tried a merge before it, and main stayed at 0a119f0 until Sam pressed Accept. Huddle set Needs You in the same second as Quinn's second evidence line, and nobody asked in chat. The Task reached Done with the merge in place. Finding: a move to Done that came with an edit got a misleading refusal ("needs Sam Rivera's acceptance") after the post-merge evidence, the Agent then asked the Human in chat, and a status-only move a second later worked (#404). Rex made 0 commits; dotnet test on main exit 0, 17 passed. Three earlier attempts failed before this one, leading to #396, #399 and #401. This is a separate run from the Opus row above, which stands as it was. |

### ZERO-16 — Run the app yourself

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-16--run-the-app-yourself)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | All ten rows and the six-line first case match the spec; 14 tests pass on main. dotnet test $bake from the Huddle root reads Huddle's global.json and exits 1, so the script must run it inside $bake. |
| 2026-11-05 | Pass | `50f223af` |  | dotnet test on main passed 26 of 26; all ten cases matched; the six-line first case equals the spec. |

### ZERO-17 — Capture through the Chief of Staff

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-17--capture-through-the-chief-of-staff)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #343 | Yogurt filed in Family due Saturday but assigned to Hana, waking her. Hugo, the box and tennis went to the Chief of Staff's own work\memory. Millstone answered with its source. Open question answered: yes, it can file a Task into a Team it is not in. |
| 2026-11-05 | Pass | `365cd12f` |  | Rerun after the fix in #384; the first run at 50f223af failed because the Helper declined the Hugo Brandt fact and the box idea. Now Hugo Brandt is filed in Harbor Lane Bakery Memory within 30 seconds (the reply says proposed); yogurt is FAMI-0007 due Saturday; the box is HARB-0006; Millstone answered with HARB-0005; tennis gets one question. The Chief can file a Task into a Team it is not in. The Team moved on HARB-0006 within seconds, so the reply saying it was unassigned went stale. |

### ZERO-18 — A Huddle-wide fact reaches every Team, and other Teams' facts do not

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-18--a-huddle-wide-fact-reaches-every-team-and-other-teams-facts-do-not)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #344 | Teams\memory stayed empty: the fact went to the Chief of Staff's own memory and was offered as an AGENTS.md rule. Cole did not know the time zone. Isolation held: Cole did not know the allergies. |
| 2026-11-05 | Pass | `365cd12f` |  | Rerun. The Huddle-wide fact is in Teams\memory with its description, and the Chief proposed the AGENTS.md line for you to add; Cole in a new session (checked in the log) said Pacific Time from the Huddle-wide index and did not know the allergies; mid-conversation, Cole answered Priya from one change notice and did not repeat it. The first run, which had no AGENTS.md proposal, is superseded. |

### ZERO-19 — A member proposes a fact and the Helper files it

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-19--a-member-proposes-a-fact-and-the-helper-files-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #346 | Milo saved privately, said it would ask Hana, did not. Bug: the card listed propose_team_memory but the live session lacked it (ToolSearch: no match). After Restart the tool worked, the Helper updated people-and-diet.md, and the Feed showed Proposed by Milo. Members hired by Build a team cannot propose until restarted. Feed Helpers filter stayed at 0. |
| 2026-11-05 | Pass | `365cd12f` |  | Milo said he proposed it and had not edited the file; the hard-cheese line was filed in allergies-and-diet.md within about 60 seconds; doc-history shows Team Memory (Helper), "Proposed by Milo". |

### ZERO-20 — A rule goes to AGENTS.md through you

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-20--a-rule-goes-to-agentsmd-through-you)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Family file changed only after Accept; Cole deferred to AGENTS.md and quoted the rule after Start afresh. Script: a second New chat with only Cole reuses the Room and session. The Library editor shows no Save or dirty state; only Ctrl+S saved. |
| 2026-11-05 | Pass | `365cd12f` |  | Family AGENTS.md changed only after Accept; Cole said the rule belongs in AGENTS.md and proposed a line; a new session quoted the invoice rule exactly; a running session followed two later rules at the next Turn from one change notice each, with no repeat. |

### ZERO-21 — An Agent cannot change an original

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-21--an-agent-cannot-change-an-original)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Hash unchanged; overwrite refused; corrected copy beside the original and the summary updated to $647.48. |
| 2026-11-05 | Pass | `365cd12f` |  | Hash unchanged; Hana refused to overwrite and asked to go ahead; after "@hana Yes, go ahead." the corrected copy sat beside the original and the cancellation was recorded in outputs. |

### ZERO-22 — Wrapping up a session

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-22--wrapping-up-a-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #346 | Grouped, waited, wrote only what was approved. Bea read "the Task" as new HARB-0008 due 25 November and said so, so no Task carries 20 November. The script wording is ambiguous. |
| 2026-11-05 | Pass | `365cd12f` |  | Proposal grouped Memory, Tasks, Outputs and Rules and waited; after approval the Millstone decision and HARB-0007 (Due 2026-11-20) were written; the declined items were not. Bea warned that the earlier end with Paid rule would mark unpaid invoices Paid. |

### ZERO-23 — The weekly check finds planted drift

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-23--the-weekly-check-finds-planted-drift)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Four of four findings: FAMI-0011 to 0013 and an AGENTS.md proposal; planted hashes unchanged. Script: Repeats has no weekly option, use On chosen days. |
| 2026-11-05 | Pass with a finding | `365cd12f` |  | Weekly check reported all three planted problems and a stale count, filed Tasks FAMI-0008 to 0011, changed no file. Timed check ran by itself 6 seconds after its set time, no click. Issue 355 could not be compared: the Scheduled page held no monthly job before this test. Finding: the retro proposal card appears in the Timed check Room (draft issue i). |

### ZERO-24 — The Chief of Staff offers upkeep and runs a daily review

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-24--the-chief-of-staff-offers-upkeep-and-runs-a-daily-review)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Only the accepted Job was created; brief named real Tasks from two Teams. Thinner than expected: no three things for today, no 13 November date. |
| 2026-11-05 | Pass | `365cd12f` |  | One job created (Daily review, weekdays 09:00); the weekly and monthly ones declined and not created; every Task the brief names exists on disk. The brief has no Needs you section, no filed-since-last-review section and no three things to do today; those Expect lines are the script and not the job prompt (script fault, fixed). |

### ZERO-25 — Everything survives a restart

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-25--everything-survives-a-restart)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Fail | `b018a212` | #345 | Teams, Tasks and Jobs survived; 0 adapters after stop. Fern gave both school dates with sources but did not find the allergy review, held only in Task FAMI-0005, because it did not search Tasks. |
| 2026-11-05 | Pass with a finding | `365cd12f` |  | After the restart the three Teams, 20 Tasks and three Scheduled jobs were there; Fern, in a new session, gave Friday 20 November, Friday 13 November and Tuesday 8 December 10:30, each with its source. Finding: she searched twice with "No results" and found the Task fact only through list_tasks, and told Sam she found no active Task for the slip although FAMI-0001 exists (draft issue g). Issue 353 was not exercised. |

### ZERO-26 — A Request carries work between Teams

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-26--a-request-carries-work-between-teams)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | Chips right; triage accepted; 19 tests; both outputs match; Devon refused with "A Request closes only after the requesting Team's QA confirms it". Needed two nudges, sign-off, and a Human move to Done. Log: "Not waking the assignee: BudgetPaused" stalls hand-offs silently while the panel read Wakes: 0 of 10. Ivy moved to Review uncommitted. |
| 2026-11-05 | Pass with a finding | `365cd12f` |  | Issue 350 evidenced: Bea filed DEVT-0003 as a feature Request; Devon triaged within about a minute; Ines bounced it once; one Human move, Accept; Devon moved it to Done; Bea confirmed it with confirm_request and it closed. dotnet test on main 41 passed; both outputs matched. Findings: the Feed showed "Huddle paused wake-ups on DEVT-0003: Agents changed it too many times" and the flow carried on without action (draft issue i). Not exercised: refusal of a close before confirmation. The Dev Team ran on Opus; this test cost about 10.1 USD. |
| 2026-11-05 | Fail | `49f47e1d` | #403 | Rerun on the script's models (Devon, Ivy and Bea Sonnet, Rex and Quinn Haiku). Stuck: three attempts did not get through to a merge, an Accept or Bea's wake. The first moved the Task Review -> Done before any merge, with the Room budget then spent; the second stalled in Review after Rex's approval, which had no @mention; the third sat in Review with Ivy as assignee and was stopped after 55 minutes, one nudge sent. No merge, no Refused line, and the Helper set no gate or Action. #350's wake and confirm were not reached on these models: Bea was never woken with the turn-to-confirm line and confirm_request was never called. This is a separate run from the Opus row above, which stands as it was. |
| 2026-11-06 | Fail | `4ac25930` | #416 | One run on the script's models, after #414. Every Agent session ran in `default`, no classifier refusal. The #403 wake fired: Rex woken 7 seconds after Devon moved the Task to Review with Ivy as assignee, and review and verify were on the Task within 3 minutes. Then stuck in Review: Bea and Devon asked the Human to accept in chat, no Action was set, so no Needs You, no Accept, no merge, no Bea wake. No Agent tried to merge, so the early-merge refusal is still not exercised. Continue pressed once, woke nobody; two nudges answered about DEVT-0001. Output checks not run. 1.64 USD. |
| 2026-11-06 | Fail | `987bf475` | #416 | Second run that day, on the owner's yes, with a Human allowed to answer an `ask_human` card; none appeared, so that route is still untested. Every Agent session in `default`, no classifier refusal. The #403 wake fired again: Rex woken 4 seconds after the move to Review. Rex's approval stayed in chat, because `update_task` refused his 414-character reason. Stuck in Review from 18:58Z: Devon said "only the Human can accept a Task as Done", and nothing set an Action. Proof was complete at 18:57:52Z. No Needs You, Accept, merge or Bea wake; no merge tried, so the refusal path is still not exercised. Two nudges answered about DEVT-0001. Output checks not run. 1.45 USD. |
| 2026-11-06 | Pass | `108dfef8` |  | Every Agent on Sonnet. Eight minutes, 1.97 USD. The #403 wake reached Rex 8 seconds after the move to Review. Huddle named the Action and moved the Task to Needs You with Proof 2/2 (PR 420). One Accept, then Devon merged unprompted and moved it to Done; Bea's wake named her as the verifier and `confirm_request` closed it with no other Human move. On main at `4a2e027`: 22 tests passed, both outputs match. One `ask_human` card, about the delivery test, not acceptance. Not exercised: an early merge, so the gate's refusal is still not seen in a full run, and an Agent closing early. "Needs triage" chip not seen. |

### ZERO-27 — Find it all, and add up the cost

File: [`docs/engineering/manual-tests/second-brain-zero.md`](second-brain-zero.md#zero-27--find-it-all-and-add-up-the-cost)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-03 | Pass | `b018a212` |  | All three searches found their items; Feed shows no message text. Spent: Family 1.250, Harbor Lane Bakery 1.151, Dev Team 3.798 USD; Helpers 17 and 1 calls, 0.0076 USD; Chief of Staff not shown in the UI. Raw/Memory/Outputs/Tasks: Dev 3/1/0/4, Family 6/8/3/13, Bakery 5/4/2/8. Run took about 1h45 on models from the cast table; session logs show no Opus Turn. |
| 2026-11-05 | Fail | `365cd12f` |  | task:permission finds nothing: Nothing in Tasks matches "permission", while FAMI-0001 holds permission in its action line; task:museum finds it, so Task search matches titles only (draft issue g). lib:Millstone and room:Blue Gull passed; the Feed shows no Message text; no "nothing to log" in 30 room files. Spending: Dev Team 15.954, Family 1.338, Harbor Lane Bakery 1.354, Chief of Staff 0.033, Helpers 0.018, Total 18.697 USD; with the discarded 0.23 and Route A 0.335, 19.262 USD. Counts Raw/Memory/Outputs/Tasks: Dev Team 6/4/0/3, Family 9/8/3/11, Harbor Lane Bakery 5/5/2/7. |

## Feed: the Notifications bell and panel, the Feed Tab, cards, filters, the new-items pill and the restart

### FEED-01 — The bell counts unseen Notifications and opening the panel marks them seen

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-01--the-bell-counts-unseen-notifications-and-opening-the-panel-marks-them-seen)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-02 — The panel opens on Needs you and its Categories switch

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-02--the-panel-opens-on-needs-you-and-its-categories-switch)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-03 — The dismiss cross appears on hover and hides a Notification from the bell only

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-03--the-dismiss-cross-appears-on-hover-and-hides-a-notification-from-the-bell-only)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-04 — A Task assigned to the Human appears and resolves when it is reassigned

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-04--a-task-assigned-to-the-human-appears-and-resolves-when-it-is-reassigned)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-05 — A gated Task moved to Needs You appears with its Proof count and resolves when accepted

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-05--a-gated-task-moved-to-needs-you-appears-with-its-proof-count-and-resolves-when-accepted)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-06 — A Mention appears and resolves when the Human posts in the Room

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-06--a-mention-appears-and-resolves-when-the-human-posts-in-the-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-07 — The Feed Tab header shows the Live chip, the read-only note, seven counted segments and the Team select

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-07--the-feed-tab-header-shows-the-live-chip-the-read-only-note-seven-counted-segments-and-the-team-select)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-08 — A card expands to a Task preview with buttons that change nothing

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-08--a-card-expands-to-a-task-preview-with-buttons-that-change-nothing)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-09 — Clicking an avatar filters the Feed to that Agent

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-09--clicking-an-avatar-opens-the-profile-and-the-agent-filter-lives-in-the-address)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-10 — New items wait behind a pill when the list is scrolled

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-10--new-items-wait-behind-a-pill-when-the-list-is-scrolled)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-11 — The unseen bar clears on the next visit

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-11--the-unseen-bar-clears-on-the-next-visit)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-12 — A restart resolves a Form Notification

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-12--a-restart-resolves-a-form-notification)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-13 — Turning the Feed off hides the bell and the link

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-13--turning-the-feed-off-hides-the-bell-and-the-link)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-14 — No Message text appears anywhere in the Feed

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-14--no-message-text-appears-anywhere-in-the-feed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### FEED-15 — A Helper's gate change is a Notification with its reason, and the wake-up is Huddle's

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-15--a-helpers-gate-change-is-a-notification-with-its-reason-and-the-wake-up-is-huddles)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Fail | `83da2ce2` |  | By an agent; Keeper on Sonnet low (owner rule), Helper on Haiku. Step 3: the summary reads `set the Action ... on`, not `turned on Requires your acceptance for`, and the preview line ends `customer..` (double stop). Step 5 cannot be judged as written: the Human's ask answers the Mention, and the wake-up goes to a separate Task Room. Step 6 blocked: the Helper also set an Action, which locks the gate switch (`This Task names an Action that leaves the team.`) while the card and the Task say you can turn it off. A Task with no assignee gets no wake-up at all. Issue to file. |
| 2026-11-07 | Partial | `63fea285` | #452 | Paid rerun by an agent after #452; Keeper on Sonnet low, Helper on Haiku, about 0.07 USD. Asked for Globex, not Acme: GROW-0001 already had that title and the Keeper refused a duplicate. Step 2 passes. Step 3: summary `set the Action "Email the pricing sheet to Globex" on` with the reason, Helper mark, `Waiting for you`, status `Asked 20:52 · accept or decline it on the Task`, no doubled stop; but the expanded preview still said `If you turn it off`, because `FeedTaskPreview` was a second caller #452 missed. Fixed on `fix/452-feed-preview-line` and checked in the browser on GROW-0002. Steps 4 and 5 as in the first run: an unassigned Task gets no wake-up, and the step-1 message answers the Mention. Step 6: Decline only shows in Review or Needs You, so the Task was moved to Review (the card turned `Done` then, and left Needs you 3 to 2), then declined; script updated. |
| 2026-11-07 | Pass | `b887218a` | #452 | Paid run of the rewritten script by an agent; Keeper on Sonnet low, Helper on Haiku, about 0.06 USD. In the Growth `Keeper` chat the Keeper made GROW-0008 "Email the price list to Initech", assigned to itself, and Mentioned `@Tester`; then came Huddle's wake-up. Step 2: bell `3 new`; card under `Gates` and `Needs you`. Step 3: Helper mark; `set the Action "Email the price list to Initech" on` GROW-0008 with the reason; `Waiting for you`; `Asked 21:41 · accept or decline it on the Task`; the preview says to decide the Task, with one stop. Step 4: after the Mention, a `.system-row` naming `Needs Human (Helper)`, not under the Human's Name. Step 5: `Keeper mentioned you in Keeper` (Growth) still Waiting. Step 6: Review, Save, Decline: card `Done · 21:42`, left Needs you, still under All and Gates. The Save's own wake-up is the Human's and answered the Mention, after step 5 was judged. |

### FEED-16 — A Helper that keeps failing reaches the Feed, and turning it off clears it

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-16--a-helper-that-keeps-failing-reaches-the-feed-and-turning-it-off-clears-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Active | `83da2ce2` |  | Blocked: an unknown Needs Human Adapter now falls back to claude (`The adapter 'nope' is not configured. It will run on 'claude'.`), so the Helper never fails this way. HELPERS-07's expected text is stale for the same reason. |
| 2026-11-07 | Pass | `bfe93189` |  | By an agent, set up as HELPERS-07 (whose Task was failure 1). The first run's "blocked" was wrong: `HelperRunner` fails an unknown Adapter; the paid lane's saved card row (from FEED-17) overrode the configuration, and the picker's `It will run on 'claude'` was misleading. Failures 1 and 2 (GROW-0011, GROW-0012): no failing item. Failure 3 (GROW-0013): one item, actor `Huddle`, `Needs Human failed 3 times in a row` + the last error, `Since 23:06 · Tasks are not being judged`, ErrorOutline badge, preview `Status On`, `Runs on nope · haiku · low`, calls today, `Last error 23:06 · …`; `Open Helper settings` opens `/settings/helpers#helper-NeedsHuman` with the card in view. Failure 4 (GROW-0014): still one item. Off and Save: `Done · 23:12`, Needs you 6 to 5. About 0.05 USD of Keeper Turns; the Helper made no call. Lane restored (relaunch, Reset to defaults). |

### FEED-17 — A Helper at its cap reaches the Feed once, and Continue clears it

File: [`docs/engineering/manual-tests/feed.md`](feed.md#feed-17--a-helper-at-its-cap-reaches-the-feed-once-and-continue-clears-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent; cap set to 3 because 2 calls were already used. One item by Huddle, `2 skipped while paused`, resolved by Continue (+10 today); Needs you fell 3 to 2. |

## Remote access: the connect page, pairing, Tailscale Serve and Funnel, the LAN bind, revocation and the Open action

### REMOTE-01 — The installed app opens signed in, and a second launch opens another signed-in tab

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-01--the-installed-app-opens-signed-in-and-a-second-launch-opens-another-signed-in-tab)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-02 — A second browser on the host sees the connect page, and dashboard and auth-token work as documented

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-02--a-second-browser-on-the-host-sees-the-connect-page-and-dashboard-and-auth-token-work-as-documented)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent on a dev run. `dashboard` and `stop` find the copy through `instance.json`, which a dev run writes only with `--Team:Desktop:Enabled=true`. |

### REMOTE-03 — Tailscale Serve: a phone gets in with no token and no approval, limited, then asks for admin

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-03--tailscale-serve-a-phone-gets-in-with-no-token-and-no-approval-limited-then-asks-for-admin)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-04 — Tailscale Serve: a shared tailnet user is marked, and revoking disconnects them

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-04--tailscale-serve-a-shared-tailnet-user-is-marked-and-revoking-disconnects-them)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-05 — LAN: the QR setup link on iPhone and Android, with approval and the downgraded warnings

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-05--lan-the-qr-setup-link-on-iphone-and-android-with-approval-and-the-downgraded-warnings)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-06 — Rate limit: ten wrong tokens lock a phone out for five minutes, and loopback is only slowed

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-06--rate-limit-ten-wrong-tokens-lock-a-phone-out-for-five-minutes-and-loopback-is-only-slowed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Partial | `83da2ce2` |  | By an agent. Step 4 passes: loopback 401s slowed 267, 506, 1005, 2018 ms, then a 5 s cap, and the correct token signed in on the 11th try. Steps 1 to 3 need a phone. |

### REMOTE-07 — Revoking a phone with a Room streaming disconnects it at once

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-07--revoking-a-phone-with-a-room-streaming-disconnects-it-at-once)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-08 — Two copies on ports 5101 and 5102 keep separate devices and no sign-outs

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-08--two-copies-on-ports-5101-and-5102-keep-separate-devices-and-no-sign-outs)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Pass | `83da2ce2` |  | By an agent. Both tabs stayed signed in after reloads; each copy lists only its own device. |

### REMOTE-09 — A hand-made Serve route is refused and shown in the Security check

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-09--a-hand-made-serve-route-is-refused-and-shown-in-the-security-check)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-10 — Funnel: refused in token mode, and in password mode a phone off the tailnet needs the password and approval

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-10--funnel-refused-in-token-mode-and-in-password-mode-a-phone-off-the-tailnet-needs-the-password-and-approval)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-11 — With remote access configured, the Open action and Library open and reveal are gone

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-11--with-remote-access-configured-the-open-action-and-library-open-and-reveal-are-gone)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-12 — A Huddle crash kills the Tailscale child (Job object)

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-12--a-huddle-crash-kills-the-tailscale-child-job-object)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-13 — Killing the Tailscale child removes the Serve route

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-13--killing-the-tailscale-child-removes-the-serve-route)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### REMOTE-14 — Strict local default

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-14--strict-local-default)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-07 | Partial | `83da2ce2` |  | By an agent. Steps 1, 3, 4, 5 and 6 pass. Step 2 is inconclusive: with `--urls http://localhost:<port>` nothing listens on the LAN address, so the request is refused at TCP rather than answered 403. |

### REMOTE-15 — Turning Strict local on disconnects remote devices

File: [`docs/engineering/manual-tests/remote-access.md`](remote-access.md#remote-15--turning-strict-local-on-disconnects-remote-devices)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

## Instructions and Memory: the Instructions files, the memory tool, Team Memory and the Memory protocol

### INSTRMEM-01 — A Team Instructions file reaches every member on its next session

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-01--a-team-instructions-file-reaches-every-member-on-its-next-session)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-02 — A new topic file is written with a description and appears in the next session index

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-02--a-new-topic-file-is-written-with-a-description-and-appears-in-the-next-session-index)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-03 — Two Rooms write one memory file and the second write is refused with the current text

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-03--two-rooms-write-one-memory-file-and-the-second-write-is-refused-with-the-current-text)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-04 — A member write to Team Memory is refused through the tool and through Write while its read is allowed

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-04--a-member-write-to-team-memory-is-refused-through-the-tool-and-through-write-while-its-read-is-allowed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-05 — A Human edit in the Library is seen at the next read and an old version is refused

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-05--a-human-edit-in-the-library-is-seen-at-the-next-read-and-an-old-version-is-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-06 — An Agent deletes a memory file only when the Human asks

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-06--an-agent-deletes-a-memory-file-only-when-the-human-asks)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-07 — Spike S-1 whether Claude Code loads the repository CLAUDE files into a Persona

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-07--spike-s-1-whether-claude-code-loads-the-repository-claude-files-into-a-persona)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-08 — A restart recalls a memory from the index and the files

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-08--a-restart-recalls-a-memory-from-the-index-and-the-files)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### INSTRMEM-09 — An Adapter with no file tools still gets the memory tool and the Memory block

File: [`docs/engineering/manual-tests/instructions-memory.md`](instructions-memory.md#instrmem-09--an-adapter-with-no-file-tools-still-gets-the-memory-tool-and-the-memory-block)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-01 — The Helpers tab shows the Team Memory card

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-01--the-helpers-tab-shows-the-team-memory-card)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-02 — A member proposal is filed with its proposer in the History

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-02--a-member-proposal-is-filed-with-its-proposer-in-the-history)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-03 — A later proposal updates the same topic file

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-03--a-later-proposal-updates-the-same-topic-file)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-04 — A personal preference is not filed and the Feed says why

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-04--a-personal-preference-is-not-filed-and-the-feed-says-why)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-05 — A proposal to a Team the member is not in is refused

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-05--a-proposal-to-a-team-the-member-is-not-in-is-refused)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-06 — Instructions inside a proposal do not move the write outside the Team

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-06--instructions-inside-a-proposal-do-not-move-the-write-outside-the-team)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-07 — The daily cap pauses the Helper and the tool says so

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-07--the-daily-cap-pauses-the-helper-and-the-tool-says-so)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |

### TMHELP-08 — A new manager writes Team Memory during onboarding

File: [`docs/engineering/manual-tests/team-memory-helper.md`](team-memory-helper.md#tmhelp-08--a-new-manager-writes-team-memory-during-onboarding)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | f2874883 |  | Agent, isolated lane, Haiku. One-role Development Team (manager only) on a repo with two conventions. Manager wrote `Teams\Tm8Dev\memory\tm8-repo-conventions-and-build.md` with a description and both conventions, proposed a starter Task and created none; only its Room had a Turn. 0.008 USD. Effort stayed Agent default in the draft |

## Long-running Rooms: the experiment on the Chief of Staff's Room, recall after a fresh start, repeats and the hand-played flush

### LONGROOMS-01 — Claude Code's own memory stays off for the Chief of Staff

File: [`docs/engineering/manual-tests/long-running-rooms.md`](long-running-rooms.md#longrooms-01--claude-codes-own-memory-stays-off-for-the-chief-of-staff)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-31 | Pass | 79f84a16 |  | Seeded Room, by an agent. "heron" saved through `memory`; no Claude Code memory written. Checked the snapshot's project folder too (page fixed since) |

### LONGROOMS-02 — Recall after a fresh start at the default Catch-up

File: [`docs/engineering/manual-tests/long-running-rooms.md`](long-running-rooms.md#longrooms-02--recall-after-a-fresh-start-at-the-default-catch-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-31 | Ran | 79f84a16 |  | Seeded Room, stand-in fresh start. 8/10 and 10/10; one run wrote a false "never discussed" note to Memory. Spec §9.3 |

### LONGROOMS-03 — Recall after a fresh start with a 40000-token Catch-up

File: [`docs/engineering/manual-tests/long-running-rooms.md`](long-running-rooms.md#longrooms-03--recall-after-a-fresh-start-with-a-40000-token-catch-up)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-31 | Ran | 79f84a16 |  | Seeded Room, 82-Message cap standing in for 40,000 tokens. 9/10 twice. Spec §9.3 |

### LONGROOMS-04 — Repeats in a live session, after compact and after a fresh start

File: [`docs/engineering/manual-tests/long-running-rooms.md`](long-running-rooms.md#longrooms-04--repeats-in-a-live-session-after-compact-and-after-a-fresh-start)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-31 | Ran | 79f84a16 |  | Seeded Room. Repeats 1 / 2 / 7 (live / after /compact / after a fresh start). /compact was cancelled at the 180 s idle bound; took 203 s at 1200. Live round not fully isolated. Spec §9.3 |

### LONGROOMS-05 — A fresh start with and without a save request first

File: [`docs/engineering/manual-tests/long-running-rooms.md`](long-running-rooms.md#longrooms-05--a-fresh-start-with-and-without-a-save-request-first)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-10-31 | Ran | 79f84a16 |  | Seeded Room; the week simulated. A/B 5/5 vs 5/5, then 4/5 vs 5/5; save Turn $0.83, $1.49. Spec §9.3 |

### AGENTSMDCHANGES-01 — The Helpers tab shows the AGENTS.md Changes card

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-01--the-helpers-tab-shows-the-agentsmd-changes-card)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | The Helpers tab showed three cards: Needs Human, Team Memory and AGENTS.md Changes. The AGENTS.md Changes card showed the On chip and switch, Haiku 5.5, Low, the Daily cap and `0 of 20 calls today` (the cap value 20 was read only through "0 of 20"). Edit the prompt opened `/settings/prompts?highlight=helper.agentsMdChange.system`, "AGENTS.md Changes: system prompt". Run by an agent. |

### AGENTSMDCHANGES-02 — A running session follows a new Team rule in two Rooms

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-02--a-running-session-follows-a-new-team-rule-in-two-rooms)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | First run (22:20): the Research file did not exist; the line settled free in 2 s as a Comparison with no Helper call and the card stayed at 0, by design. Replies: Room A "4\n\nNoted, Research.", Room B "Teal.\n\nNoted, Research.", Room A "Owl\n\nNoted, Research." with no mention of a file change. Rerun with an existing file: added `Keep every reply under twenty words.` at 22:26:00; settled 22:27:15 as a Summary ("Replies must now stay under twenty words."). Card 0 -> 1 of 20 (0.0005 USD). Room A `@Leo what is 5 + 5?` -> "10\n\nNoted, Research."; Room B `@Leo name a shape.` -> "Circle.\n\nNoted, Research." |

### AGENTSMDCHANGES-03 — A running session follows a new Huddle-wide rule

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-03--a-running-session-follows-a-new-huddle-wide-rule)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Teams\AGENTS.md was non-empty (2040 chars). Settled 22:30:07 ("Every reply must start with the word Understood."); card 2 of 20. Room A `@Leo what is 3 + 3?` -> "Understood. 6\n\nNoted, Research."; Room B `@Leo name a fruit.` -> "Understood. Apple.\n\nNoted, Research." Both start with Understood. and both keep the Team ending. |

### AGENTSMDCHANGES-04 — A typo fix is skipped and the Feed lists it

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-04--a-typo-fix-is-skipped-and-the-feed-lists-it)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Feed clause: "recieve" -> "receive" at 22:38:30, settled 22:39:54 as Skipped ("Only a spelling correction, recieve to receive; meaning unchanged."); the Feed item read "didn't tell Agents about Research/AGENTS.md: ...". Run 2: `shrt` -> `short` at 01:04:35, judged 01:05:57, Skipped ("Only a spelling fix: 'shrt' became 'short', same meaning."), Feed Helpers count 3; the Turn log showed "added 0 characters". An earlier run (00:01:53) got `{"meaningful": true, ... "changes": []}` with a reason saying not meaningful; `Validate` rejected it, the next Turn got a Comparison with the spelling lines and Leo said "Yes: the typo conciese was corrected". The Helper prompt now rules that answer out. The page's question was reworded to `since your last reply, were you told of any change to your rules?`, since the old wording mixed in earlier rules. |

### AGENTSMDCHANGES-05 — The notice's wording is followed

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-05--the-notices-wording-is-followed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Room C (Leo + Ada, as a Direct Room reopens). Step 1 "Understood. Canada. Noted, Research." Rounds 1 to 3 each ended with the new words ("Wording check.", "... two.", "... three."), no pushback, no "injection" or "untrusted". Edits at 22:45:00, 22:51:30, 22:57:59, each 5+ minutes after the last judgement; settled 22:46:25, 22:52:54, 22:59:14. Card 7 of 20 after this test. The replies were doubled (see test 11 and the plan's Retrospective log); fixed by the DocHistory hook exemption. |

### AGENTSMDCHANGES-06 — After /compact the next Turn hears the notices again

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-06--after-compact-the-next-turn-hears-the-notices-again)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | `@Leo /compact` -> "Compacted my conversation: 33,791 -> 5,021 tokens in 5 s." `name a planet.` -> "Understood. Mars. Noted, Research."; `name a moon.` -> "Understood. Europa. Noted, Research."; step 4 -> "Understood. Start with "Understood." End with "Noted, Research." Keep replies under twenty words. Noted, Research." Not observable from the UI: whether the notices were re-told at step 2. |

### AGENTSMDCHANGES-07 — With the Helper off the comparison arrives

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-07--with-the-helper-off-the-comparison-arrives)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Card Off, 8 of 20. Added `Never use the word "very".` at 23:09:55; sent 23:10:35, reply in 6 s: "Understood. The sky is clear, mild, and breezy today. Noted, Research." (doubled before the hook fix). Ledger settled the instant the Turn was sent, kind Comparison, lines ["+Never use the word \"very\"."]. Step 4 -> "Understood. Line 4: "Never use the word "very"." Noted, Research." Card 8 of 20, unmoved. Switched On. |

### AGENTSMDCHANGES-08 — A real restart then resume still tells what was missed

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-08--a-real-restart-then-resume-still-tells-what-was-missed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Appended `Add the word "Restart-ok" to the end of every reply.` at 23:12:13 with the app stopped; started 23:16:15; settled 07:17:33 UTC (Summary). Room A `name a tree.` -> "Understood. Oak. Noted, Research. Restart-ok" (3 s); Room B `name a flower.` -> "Understood. Rose. Noted, Research. Restart-ok" (11 s); Room A `name a bird.` -> "Understood. Robin. Noted, Research. Restart-ok" (2 s). Card 8 -> 9 of 20 (+1). |

### AGENTSMDCHANGES-09 — Does a cold resume honour the changed prompt

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-09--does-a-cold-resume-honour-the-changed-prompt)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Helper Off, app stopped, appended `Always call the Research Team "the lab".` at 23:48:46 and started. Room A 23:49:05 `@Leo what do you call the Research Team?` -> 23:49:07 "Understood. I call the Research Team "the lab." Noted, Research. Restart-ok". Ledger entry settled at the send, Comparison, `+Always call the Research Team "the lab".`; the tool log shows no Read in that Turn. There is no Turn-detail control in the UI, so the ledger and the tool log were checked instead. |

### AGENTSMDCHANGES-10 — A save loop and the card's call count

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-10--a-save-loop-and-the-cards-call-count)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Baseline 9 of 20. Card at 23:22:45, 23:23:45, 23:24:48 = 9; at 23:27:25 (100 s after the last save) = 10 (0.0059 USD). One ledger entry, settled 07:27:01 UTC (77 s after the last save), Summary "Feedback handling changed from receiving it gracefully to receiving it honestly36." Step 4 -> "Understood. Feedback now: "receive honestly36," not "gracefully." Noted, Research. Restart-ok" (one net change). |

### AGENTSMDCHANGES-11 — A waiting Turn holds the Agent's other Room

File: [`docs/engineering/manual-tests/agents-md-changes.md`](agents-md-changes.md#agentsmdchanges-11--a-waiting-turn-holds-the-agents-other-room)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-04 | Pass | `0d3dbabe` + uncommitted `feat/agentsmd-changes` | | Holding: edit 23:37:49; Room A `name a metal.` sent 07:37:51.5, reply 07:38:00.2 ("Understood. Copper. Noted, Research. Restart-ok"), a wait of 8.7 s; Room B `name a gas.` sent 3.2 s after A, reply 07:38:02.5 ("Understood. Oxygen. ..."), 2.3 s after A's reply. Stop during a wait: first build, no Stop shown (a stuck path); after the fix (early empty Draft) the Stop was present 0.8 s after Return, clicked 00:33:07.2; no Leo reply, no Turn hook line, Room free within 11 s. The Helper call ran to its end (ledger 00:33:14.7) and was not cancelled. Next `name a metal.` -> "Understood. Iron. Noted, Research. Restart-ok Stone-check." Open: Stop just after publishing could not be landed (Leo replies in 2 to 4 s); in the one case seen earlier the hook added 472 characters and the next Turn 0, so heard moved. Card 13 of 20 on the first day, 9 after rollover. |

---

Back to [the manual test script](../manual-tests.md).

### AGCONN-01 — A new Agent has account connectors off

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-01--a-new-agent-has-account-connectors-off)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. A new Agent's switch reads off. |

### AGCONN-02 — Turning on asks first, and Cancel leaves it off

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-02--turning-on-asks-first-and-cancel-leaves-it-off)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Failed at `a07e0a0e`: after Cancel the switch was drawn on while the line said Off. Fixed, then passed on the rebuilt app. |

### AGCONN-03 — Give access restarts the Agent and records it in the Feed

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-03--give-access-restarts-the-agent-and-records-it-in-the-feed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Give access with real mouse clicks, 4 of 4, coordinates checked first. An earlier failure most likely clicked Cancel, about 30 px away, in a downscaled screenshot; that cause is not proven. |

### AGCONN-04 — Turning off applies at once, and a rename drops access

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-04--turning-off-applies-at-once-and-a-rename-drops-access)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Partial | `a07e0a0e` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Turning off applied at once. The rename step was not run; `PersonaConnectorsTests` covers it. |

### AGCONN-05 — An Adapter Huddle cannot control shows text, not a switch

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-05--an-adapter-huddle-cannot-control-shows-text-not-a-switch)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Skipped | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Only `claude-agent-acp` is configured on the lane. |

### AGCONN-06 — The one-time notice shows after an upgrade and stays dismissed

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-06--the-one-time-notice-shows-after-an-upgrade-and-stays-dismissed)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Partial | `a07e0a0e` | #357 | By an agent acting as the Human, on Sonnet at low Effort. The first two clauses passed. The rest of the case was not run; `AgentConnectorsPageTests` covers the dismissal. |

### AGCONN-07 — A new Agent lists no claude.ai tool by Turn 3

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-07--a-new-agent-lists-no-claudeai-tool-by-turn-3)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `a07e0a0e` | #357 | By an agent acting as the Human, on Sonnet at low Effort. 0 `claude_ai` strings in a new Agent's log at Turn 3. |

### AGCONN-08 — With the switch on the connectors appear, and every use asks first

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-08--with-the-switch-on-the-connectors-appear-and-every-use-asks-first)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `a07e0a0e` | #357 | By an agent acting as the Human, on Sonnet at low Effort. The card appeared for `mcp__claude_ai_ms365__get_me`, and one Allow once ran it: the one real connector call of all these runs. With an Agent-written `allow` in `settings.local.json`, a card still appeared; refused. Logged: McpServerName `claude.ai ms365`, McpServerSource `claudeai`, Kind Other. |

### AGCONN-09 — An Agent cannot switch its own connectors on

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-09--an-agent-cannot-switch-its-own-connectors-on)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Asked to turn its own connectors on, Devon said it had no way and made no tool call. `persona_connectors` and `settings.local.json` unchanged. |

### AGCONN-10 — A Turn running when the switch flips

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-10--a-turn-running-when-the-switch-flips)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Partial | `a07e0a0e` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Switched off mid-Turn: the card showed off at once, and the running Turn and its pending call did not change. No pass line was reached. |

### AGCONN-11 — A connector call held past the wait

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-11--a-connector-call-held-past-the-wait)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Partial | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Timeout lowered to 30 s. Refused with "no answer", the call did not run, and the Turn was not killed. The model tried no second call, so "refused with no new card" is pinned only by tests. |

### AGCONN-12 — The switch flipped while a card is open

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-12--the-switch-flipped-while-a-card-is-open)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. Flipping the switch off closed the card as refused and the call never ran. The Turn then ended with no message to the Human. |

### AGCONN-13 — Hostile arguments

File: [`docs/engineering/manual-tests/agent-connectors.md`](agent-connectors.md#agconn-13--hostile-arguments)

| Date | Result | Commit | Issue | Notes |
| --- | --- | --- | --- | --- |
| 2026-11-06 | Pass | `bca0f249` | #357 | By an agent acting as the Human, on Sonnet at low Effort. On the stand-in `claude_ai_probe`. Markup, `**bold**` and a `javascript:` link showed as plain text, short fields were all shown, and a 6000-character body was cut with a count. Bidi was not tested: the model dropped the character. An earlier apparent hang was most likely a stale binary. |
