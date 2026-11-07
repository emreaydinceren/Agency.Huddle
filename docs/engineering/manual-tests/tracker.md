# Manual test tracker

Where every test stands. One row per test, updated as you run it. What happened on every run,
including the ones before the current status, is in the [Run log](run-log.md).

> [!NOTE]
> **The demo-agent cases were removed on 2026-10-06**, when `Team:DemoAgent:Enabled` became `false`
> by default: STARTUPCONFIG-03, 04, 05, 16, 18, 19 and 25; PIPEEXTERNAL-01, 02, 06 and 33;
> INVITEROOMS-01; ROOMMESSAGING-18, 19 and 20; REPLYGATEBUDGET-01; AGENTSLIBRARY-26. Other cases
> used `echo` and `alpha` as the way to get a Room and a reply; they now run on Ada and Bo, two mock-adapter
> Agents built by [`P-LAUNCH-UAT`](common.md#p-launch-uat), which is an explicit opt-in and never part
> of a normal install. Their rewritten Pass-if lines have not been run yet, so treat them as Active.

> [!NOTE]
> **An earlier full run exists, against the previous UI, and its results are deliberately not
> carried into this table.** Between 2026-09-14 and 2026-09-15 the whole suite was executed on
> branch `docs/manual-test-run-2026-09-14` — 444 Pass, 13 Fail, 7 Active — but against the
> pre-MudBlazor UI and the *previous* wording of these scripts, which `394baa9` rewrote. Same test
> ids, different tests: those verdicts do not validate the scripts in this repository now, so every
> row here is Active until someone re-runs it.
>
> That run was not wasted and is worth reading before you start. Its detailed per-test notes are in
> git at `553a107` (`git show 553a107:docs/engineering/manual-tests/tracker.md`), it closed 17
> issues, and the behaviour it found that no test asks about is written up in
> [product-observations.md](../product-observations.md), which carries a status line per entry
> checked against `main`.

This is a **recording** page, like [Planning](planning.md) — you need it when you start a
run and when you finish one, not while executing. Keep [the script](../manual-tests.md),
[Common procedures](common.md) and one area file open for that.

## Removed tests

A removed test's heading is deleted and its id is listed here, so no id is ever reused (see
[a test id is permanent](#a-test-id-is-permanent--ids-are-append-only)). Rows for these ids have been dropped.

- **2026-10-06, demo agent off by default:** STARTUPCONFIG-03, 04, 05, 16, 18, 19, 25; PIPEEXTERNAL-01, 02, 06, 33;
  INVITEROOMS-01; ROOMMESSAGING-18, 19, 20; REPLYGATEBUDGET-01; AGENTSLIBRARY-26.
- **2026-10, manual-tests repair:** SHELLNAV-21 and 22; APPEARANCETHEME-05, 08, 10, 11, 12, 13, 19 and 25 (retired in
  place before, now deleted per decision D11); TP-T13 (team-pages).
## Status

| Status | Means | What to do |
| --- | --- | --- |
| **Active** | Nobody knows yet. Either never run, or run and the environment could not answer. | Run it. |
| **Testing** | A run is in progress right now. | Finish it, then set Pass or Fail. |
| **Pass** | Every *Pass if* line held and no *Fail if* line was observed. | Nothing. |
| **Fail** | A *Fail if* line was observed, or a *Pass if* line was not. | Open an issue and put its number in the Issue column. |

Every row starts **Active**. Set it to **Testing** when you pick the test up, so a second
tester does not start the same one, and to **Pass** or **Fail** when you conclude.

### Blocked and Inconclusive come back here as Active

[§0.5](../manual-tests.md#05-how-to-conclude-a-result) gives a run four possible outcomes:
Blocked, Inconclusive, Fail and Pass. Two of those have no status of their own here, on
purpose — **Blocked** and **Inconclusive** both mean the test is still unanswered, which is
what **Active** means. Record the outcome by leaving the row Active and writing the reason
in Notes: `Inconclusive: no sqlite3`, or `Blocked by SHELLNAV-01`. A row is only Pass or
Fail when the test actually reached a verdict.

That keeps the tracker answering one question — what still needs running — instead of two.

## Failures are tracked as Gitea issues

A **Fail** gets an issue. The result in this table is the index; the issue carries what you
saw, quoted verbatim per [§0.5](../manual-tests.md#05-how-to-conclude-a-result) rule 2, and
the implication the *Fail if* line gives.

Issues live on this repo's Gitea instance under `/emre/Huddle/issues`. The host is
deliberately not written here — `.gitleaks.toml`'s `internal-mdns-host` rule fails the
`secret-scan` job on any `*.local` name in a tracked file, and `gitleaks dir .` scans the
whole tree, so one hardcoded hostname would break CI on somebody else's unrelated commit.
Build the URL from the remote instead, the way [GiteaOperations.md](../../../agents/GiteaOperations.md)
does everywhere else:

```powershell
$base = (git remote get-url origin) -replace '\.git$', ''
Start-Process "$base/issues"          # opens the issue list
```

Put the number in the Issue column as `#123`, linked the same way if you like. Before
opening one, check [§0.6](../manual-tests.md#06-not-a-defect) — some behaviour that looks
broken is a documented decision, and those are a Pass.

Title an issue with the test id first, so the tracker and the issue list line up:
`SHELLNAV-01: app.css href has no fingerprint and 404s`.

### A test id is permanent — ids are append-only

Never renumber a test that exists, and never reuse an id a retired test held. Issues cite ids
in their titles and bodies, commit messages cite them, and this table is keyed on them, so a
renumber silently re-points every one of those references at a different test. Nothing catches
it: the table still parses, the anchors still resolve, and the results are simply wrong.

This is easy to get wrong in good faith. Every row in an area can read **Active** on the branch
in front of you while a long-running test branch elsewhere holds a full set of recorded verdicts
for the same ids. Checking the file you are editing is not enough.

So a new test is **appended** with the next free number, even when it belongs beside an existing
one. If that puts a free test after the paid ones and breaks an area's free-tests-first ordering,
say so in the new test's own header and add a forward pointer from the test it belongs with —
`INVITEROOMS-14` and `INVITEROOMS-32` are the worked example. Ordering is a convention; an id is
an identifier.

---

## Where the areas stand

Counts are not maintained here — they would go stale on the first edit. This is a map to
the per-area tables below.

| Area | Table |
| --- | --- |
| [Application shell, navigation and layout](shell-nav.md) | [below](#shell-nav) |
| [Startup, configuration and first-run state](startup-config.md) | [below](#startup-config) |
| [Room view, posting Messages and Transcript rendering](room-messaging.md) | [below](#room-messaging) |
| [Turn streaming, Drafts, Stop and failure surfacing](streaming-turn.md) | [below](#streaming-turn) |
| [Reply Gate, Mentions and Room Budget](reply-gate-budget.md) | [below](#reply-gate-budget) |
| [Agents page: tiles, Teams grouping, filter and rejected files](agents-library.md) | [below](#agents-library) |
| [Agent card: view, edit, create, delete](agent-profile-card.md) | [below](#agent-profile-card) |
| [Library explorer and editor](library.md) | [below](#library) |
| [Model and Effort pickers, catalog probe and runner restart](model-effort.md) | [below](#model-effort) |
| [Creating Rooms, inviting Agents, Room naming](invite-rooms.md) | [below](#invite-rooms) |
| [Settings: the 24 Prompts, editing, per-field reset and Save](prompts-settings.md) | [below](#prompts-settings) |
| [Appearance tab, Themes, Tokens and overrides](appearance-theme.md) | [below](#appearance-theme) |
| [The named pipe: external agents and the wire](pipe-external.md) | [below](#pipe-external) |
| [App Tools a real model calls (progressive discovery)](app-tools.md) | [below](#app-tools) |
| [Persona lifecycle: supervisor, work dirs, health and restarts](persona-lifecycle.md) | [below](#persona-lifecycle) |
| [Tasks: creating, moving, viewing and referencing a unit of work](tasks.md) | [below](#tasks) |
| [Adapters: choosing one per Persona, and running two at once](adapters.md) | [below](#adapters) |
| [Skills: the Chief of Staff, team-building, and the Family Health Advisor](skills.md) | [below](#skills) |
| [Skill Packs: SKILLS.md, the AI tool, updates, the move and shared folders](skill-packs.md) | [below](#skill-packs) |
| [Work Mode: choosing how much an Agent may do before it asks](work-mode.md) | [below](#work-mode) |
| [Questions: an Agent asks the Human by tapping, not typing](questions.md) | [below](#questions) |
| [Prompt blocks: an image the Human names reaches the Agent](prompt-blocks.md) | [below](#prompt-blocks) |
| [Adapter commands](adapter-commands.md) | [below](#adapter-commands) |
| [Avatar library: choosing a face from the library](avatar-library.md) | [below](#avatar-library) |
| [File Changes, Watched Folders and Memory](file-changes.md) | [below](#file-changes) |
| [Room Sessions: per-Room context, resume and Stop](room-sessions.md) | [below](#room-sessions) |
| [Team chats: the Team Room, Team chats in the sidebar, the Members-tab Message, archiving and Task wakes and digests](team-chats.md) | [below](#team-chats) |
| [Chat uploads: the paperclip, paste and drop, the chips, the Message's path lines, the Uploads root and Room delete](uploads.md) | [below](#uploads) |
| [Teams: Sidebar, Members, Files, Tasks and Shared Memory](team-pages.md) | [below](#team-pages) |
| [Turn detail and Spend](turn-detail.md) | [below](#turn-detail) |
| [Global Search: the box, the dropdown and the jump](global-search.md) | [below](#global-search) |
| [Needs You: the sidebar card of Rooms waiting on the Human](needs-you.md) | [below](#needs-you) |
| [Right sidebar: the panels by Subject, Outline, collapse and focus](right-sidebar.md) | [below](#right-sidebar) |
| [References in a Message: the @ picker, Copy link and the Referenced items header](at-tagging.md) | [below](#at-tagging) |
| [Document history: the byline, the History and the History panel](doc-history.md) | [below](#doc-history) |
| [Human gate: the Proof list, the decision card, Accept and Decline, the board drag and the Needs You page](human-gate.md) | [below](#human-gate) |
| [Helpers: the Helpers tab, Save and Reset, the Needs Human judgement, the override, the daily cap and a failing Adapter](helpers.md) | [below](#helpers) |
| [AGENTS.md changes: a running Agent hears an edit, the skipped typo, the notice's wording, `/compact`, a real restart and a save loop](agents-md-changes.md) | [below](#agents-md-changes) |
| [Feed: the Notifications bell and panel, the Feed Tab, cards, filters, the new-items pill and the restart](feed.md) | [below](#feed) |
| [Remote access: the connect page, pairing, Tailscale Serve and Funnel, the LAN bind, revocation and the Open action](remote-access.md) | [below](#remote-access) |
| [Instructions and Memory: the Instructions files, the memory tool, Team Memory and the Memory protocol](instructions-memory.md) | [below](#instructions-memory) |
| [Team Memory Helper: proposals filed by a Helper, the History, the Feed record, the membership check and the daily cap](team-memory-helper.md) | [below](#team-memory-helper) |
| [Long-running Rooms: the experiment on the Chief of Staff's Room, recall after a fresh start, repeats and the hand-played flush](long-running-rooms.md) | [below](#long-running-rooms) |
| [Second Brain: the use cases as a Human's checklist, raw uploads, Team Memory, AGENTS.md, prompts, archive and an old data folder](second-brain.md) | [below](#second-brain) |
| [Second Brain from zero: three Teams hired by hand, documents ingested into memory, and a Development Team building an app](second-brain-zero.md) | [below](#second-brain-zero) |
---

## at-tagging

References in a Message: the @ picker, Copy link and the Referenced items header — [area file](at-tagging.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [ATTAG-01](at-tagging.md#attag-01--the-picker-opens-on-an-at-sign-at-a-word-start-at-m1) |  | Active |  |  |
| [ATTAG-02](at-tagging.md#attag-02--picking-an-agent-writes-its-name-and-wakes-only-that-agent-at-m2) |  | Active |  |  |
| [ATTAG-03](at-tagging.md#attag-03--a-task-pick-writes-the-plain-id-and-the-hash-key-gives-the-same-text-at-m3) |  | Active |  |  |
| [ATTAG-04](at-tagging.md#attag-04--room-and-team-picks-write-links-and-labels-tell-same-named-rooms-apart-at-m4) |  | Active |  |  |
| [ATTAG-05](at-tagging.md#attag-05--a-library-pick-writes-a-code-span-path-and-the-upload-line-matches-at-m5) |  | Active |  |  |
| [ATTAG-06](at-tagging.md#attag-06--renames-and-deletes-show-the-chips-and-an-old-transcript-opens-unchanged-at-m6) |  | Active |  |  |
| [ATTAG-07](at-tagging.md#attag-07--the-agent-prompt-carries-the-referenced-items-header-at-m7) |  | Active |  |  |
| [ATTAG-08](at-tagging.md#attag-08--keyboard-alone-picks-sends-nothing-while-open-and-announces-the-count-at-m8) |  | Active |  |  |
| [ATTAG-09](at-tagging.md#attag-09--the-picker-height-and-a-narrow-window-at-m9) |  | Active |  |  |
| [ATTAG-10](at-tagging.md#attag-10--undo-after-a-pick-restores-the-typed-token-at-m10) |  | Active |  |  |
| [ATTAG-11](at-tagging.md#attag-11--the-picker-lists-rooms-and-library-files-by-name-only-at-m11) |  | Active |  |  |
| [ATTAG-12](at-tagging.md#attag-12--copy-link-from-the-header-the-sidebar-and-a-team-room-at-m12) |  | Active |  |  |

## global-search

Global Search: the box, the dropdown and the jump to a Message — [area file](global-search.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [GLOBALSEARCH-01](global-search.md#globalsearch-01--ctrl-k-slash-and-a-click-open-the-box-gs-m1) |  | Active |  |  |
| [GLOBALSEARCH-02](global-search.md#globalsearch-02--ctrl-k-reaches-the-app-in-edge-and-not-the-address-bar-gs-m2) |  | Active |  |  |
| [GLOBALSEARCH-03](global-search.md#globalsearch-03--groups-fill-one-by-one-on-a-large-pinned-root-and-stale-results-never-show-gs-m3) |  | Active |  |  |
| [GLOBALSEARCH-04](global-search.md#globalsearch-04--content-hits-show-the-bold-match-the-1-mb-cut-off-and-the-incomplete-note-gs-m4) |  | Active |  |  |
| [GLOBALSEARCH-05](global-search.md#globalsearch-05--a-room-hit-jumps-to-its-message-gs-m5) |  | Active |  |  |
| [GLOBALSEARCH-06](global-search.md#globalsearch-06--archived-rooms-and-closed-tasks-come-last-and-dimmed-gs-m6) |  | Active |  |  |
| [GLOBALSEARCH-07](global-search.md#globalsearch-07--a-locked-folder-in-a-pinned-root-does-not-hide-the-other-groups-gs-m7) |  | Active |  |  |
| [GLOBALSEARCH-08](global-search.md#globalsearch-08--the-keyboard-alone-reaches-every-chip-and-card-gs-m8) |  | Active |  |  |
| [GLOBALSEARCH-09](global-search.md#globalsearch-09--the-search-box-sits-in-the-top-bar-beside-the-breadcrumb-gs-m9) |  | Active |  |  |
| [GLOBALSEARCH-10](global-search.md#globalsearch-10--the-dropdown-height-and-width-gs-m10) |  | Active |  |  |

## shell-nav

Application shell, navigation and layout — [area file](shell-nav.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [SHELLNAV-01](shell-nav.md#shellnav-01--every-stylesheet-the-shell-links-is-fingerprinted-except-the-one-that-is-deliberately-static-and-actually-serves) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [SHELLNAV-02](shell-nav.md#shellnav-02--every-shell-script-is-fingerprinted-except-mudblazors-own-and-serves---the-gap-the-automated-guard-does-not-cover) |  | Active |  |  |
| [SHELLNAV-03](shell-nav.md#shellnav-03--the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold) |  | Active |  |  |
| [SHELLNAV-04](shell-nav.md#shellnav-04--get--redirects-to-the-oldest-room) |  | Active |  |  |
| [SHELLNAV-05](shell-nav.md#shellnav-05--roomsunknown-id-returns-200-and-shows-the-shared-empty-state-while-the-sidebar-still-lists-rooms) |  | Active |  |  |
| [SHELLNAV-06](shell-nav.md#shellnav-06--an-unmatched-route-returns-a-bare-http-404-with-a-zero-byte-body) |  | Active |  |  |
| [SHELLNAV-07](shell-nav.md#shellnav-07--routes-are-case-insensitive) |  | Active |  |  |
| [SHELLNAV-08](shell-nav.md#shellnav-08--the-drawer-is-present-identical-and-fixed-width-on-every-route) |  | Active |  |  |
| [SHELLNAV-09](shell-nav.md#shellnav-09--the-sidebar-marks-exactly-one-room-active-with-aria-current) |  | Active |  |  |
| [SHELLNAV-10](shell-nav.md#shellnav-10--agents-renders-and-a-bare-page-load-spawns-no-node-process) |  | Active |  |  |
| [SHELLNAV-11](shell-nav.md#shellnav-11--settings-renders-the-seven-tab-bar-and-defaults-to-profile) |  | Active |  |  |
| [SHELLNAV-12](shell-nav.md#shellnav-12--clicking-a-settings-tab-changes-the-url-and-the-url-round-trips-as-a-bookmark) |  | Active |  |  |
| [SHELLNAV-13](shell-nav.md#shellnav-13--an-unrecognised-tab-segment-silently-falls-back-to-profile-and-the-tab-name-is-matched-without-regard-to-case) |  | Active |  |  |
| [SHELLNAV-14](shell-nav.md#shellnav-14--no-route-sets-a-browser-tab-title) |  | Active |  |  |
| [SHELLNAV-15](shell-nav.md#shellnav-15--focus-moves-to-the-pages-h1-after-every-navigation) |  | Active |  |  |
| [SHELLNAV-16](shell-nav.md#shellnav-16--sidebar-links-use-enhanced-navigation-not-a-full-page-reload) |  | Active |  |  |
| [SHELLNAV-17](shell-nav.md#shellnav-17--the-new-chat-panel-toggles-lists-agents-with-status-dots-and-gates-start-chat) |  | Active |  |  |
| [SHELLNAV-18](shell-nav.md#shellnav-18--starting-a-chat-creates-a-room-named-after-its-agents-and-navigates-straight-to-it) |  | Active |  |  |
| [SHELLNAV-19](shell-nav.md#shellnav-19--appjs-is-proven-functionally-enter-sends-and-clears-and-the-transcript-scrolls) |  | Active |  |  |
| [SHELLNAV-20](shell-nav.md#shellnav-20--the-room-list-updates-live-with-no-refresh-when-a-room-appears-from-outside-the-app) |  | Active |  |  |
| [SHELLNAV-23](shell-nav.md#shellnav-23--a-bad-appearancejson-does-not-break-the-shell-no-theme-link-no-blank-page-only-a-log-warning) |  | Active |  |  |
| [SHELLNAV-24](shell-nav.md#shellnav-24--a-deep-link-renders-complete-content-on-a-cold-first-request-before-any-circuit-attaches) |  | Active |  |  |
| [SHELLNAV-25](shell-nav.md#shellnav-25--two-browser-tabs-on-the-same-install-stay-in-step-through-the-shell) |  | Active |  |  |
| [SHELLNAV-26](shell-nav.md#shellnav-26--the-reconnect-modal-shows-exactly-one-state-paragraph-at-a-time-when-the-server-goes-away) |  | Active |  |  |
| [SHELLNAV-27](shell-nav.md#shellnav-27--with-no-rooms-the-empty-state-renders-twice-and-there-is-no-heading-at-all) |  | Active |  |  |
| [SHELLNAV-28](shell-nav.md#shellnav-28--the-new-chat-panel-with-no-agents-shows-its-own-guidance-instead-of-an-empty-list) |  | Active |  |  |
| [SHELLNAV-29](shell-nav.md#shellnav-29--record-whether-the-yellow-error-band-ever-appears-during-a-genuine-circuit-fault) |  | Active |  |  |
| [SHELLNAV-30](shell-nav.md#shellnav-30--the-drawers-icons-and-settings-pinned-to-the-bottom) |  | Active |  |  |
| [SHELLNAV-31](shell-nav.md#shellnav-31--one-click-on-a-left-menu-item-opens-a-preview-tab-and-a-click-on-a-different-item-replaces-it) |  | Active |  |  |
| [SHELLNAV-32](shell-nav.md#shellnav-32--double-clicking-a-menu-item-keeps-its-tab-and-the-next-click-opens-beside-it) |  | Active |  |  |
| [SHELLNAV-33](shell-nav.md#shellnav-33--clicking-a-tab-switches-the-page-and-the-address) |  | Active |  |  |
| [SHELLNAV-34](shell-nav.md#shellnav-34--closing-the-active-tab-activates-its-neighbour-and-closing-the-last-one-shows-no-pages-are-open) |  | Active |  |  |
| [SHELLNAV-35](shell-nav.md#shellnav-35--typing-in-a-rooms-message-box-keeps-its-tab-and-the-unsent-text-survives-a-tab-switch-and-a-page-reload) |  | Active |  |  |
| [SHELLNAV-36](shell-nav.md#shellnav-36--a-room-tab-opened-by-pasting-its-url-gets-the-rooms-name-as-its-title-within-a-moment) |  | Active |  |  |
| [SHELLNAV-37](shell-nav.md#shellnav-37--open-tabs-come-back-after-a-reload-with-the-tab-for-the-current-page-active) |  | Active |  |  |
| [SHELLNAV-38](shell-nav.md#shellnav-38--altw-altpage-down-and-altpage-up-close-and-move-between-tabs) |  | Active |  |  |
| [SHELLNAV-39](shell-nav.md#shellnav-39--right-clicking-a-tab-offers-pin-move-left-move-right-and-close-and-pin-keeps-the-tab-at-the-front-as-an-icon) |  | Active |  |  |
| [SHELLNAV-40](shell-nav.md#shellnav-40--move-right-opens-a-second-pane-beside-the-first-and-both-pages-stay-live) |  | Active |  |  |
| [SHELLNAV-41](shell-nav.md#shellnav-41--opening-a-page-that-is-already-open-in-the-other-pane-takes-you-to-it-and-new-pages-open-in-the-focused-pane) |  | Active |  |  |
| [SHELLNAV-42](shell-nav.md#shellnav-42--both-panes-pins-and-the-focused-pane-come-back-after-a-reload) |  | Active |  |  |
| [SHELLNAV-43](shell-nav.md#shellnav-43--two-rooms-side-by-side-keep-their-own-unsent-text) |  | Active |  |  |
| [SHELLNAV-44](shell-nav.md#shellnav-44--every-page-in-a-tab-has-a-breadcrumb-bar-and-an-ancestor-crumb-navigates-that-tab-in-place) |  | Active |  |  |
| [SHELLNAV-45](shell-nav.md#shellnav-45--with-two-panes-each-has-its-own-bar-and-a-crumb-acts-on-its-own-pane) |  | Active |  |  |

## startup-config

Startup, configuration and first-run state — [area file](startup-config.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [STARTUPCONFIG-01](startup-config.md#startupconfig-01--the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [STARTUPCONFIG-02](startup-config.md#startupconfig-02--every-stylesheet-in-the-document-head-returns-200--the-silent-assets-regression) |  | Active |  |  |
| [STARTUPCONFIG-06](startup-config.md#startupconfig-06--every-navigation-surface-is-reachable-from-a-cold-start) |  | Active |  |  |
| [STARTUPCONFIG-07](startup-config.md#startupconfig-07--start-chat-on-one-known-agent-reuses-its-room-two-agents-create-one-new-named-room) |  | Active |  |  |
| [STARTUPCONFIG-08](startup-config.md#startupconfig-08--agents-lists-personas-only--pipe-clients-never-appear-there) |  | Active |  |  |
| [STARTUPCONFIG-09](startup-config.md#startupconfig-09--settings-prompts-tab-on-a-first-run-says-promptsjson-is-absent-and-offers-nothing-to-save-or-reset) |  | Active |  |  |
| [STARTUPCONFIG-10](startup-config.md#startupconfig-10--appearance-tab-on-a-first-run-reads-lumen-light-picking-a-dark-theme-writes-appearancejson-and-applies-immediately-with-no-reload) |  | Active |  |  |
| [STARTUPCONFIG-11](startup-config.md#startupconfig-11--rooms-and-transcripts-survive-a-restart-drafts-and-the-budget-do-not) |  | Active |  |  |
| [STARTUPCONFIG-12](startup-config.md#startupconfig-12--deleting-app_data-while-running-fails-stopping-first-makes-the-clean-slate-reliable) |  | Active |  |  |
| [STARTUPCONFIG-13](startup-config.md#startupconfig-13--team__agentmessagebudget1-pauses-the-room-after-one-agent-reply-and-shows-the-continue-prompt) |  | Active |  |  |
| [STARTUPCONFIG-14](startup-config.md#startupconfig-14--continue-grants-budget-and-produces-no-new-reply--the-trap-that-reads-as-a-dead-button) |  | Active |  |  |
| [STARTUPCONFIG-15](startup-config.md#startupconfig-15--team__agentmessagebudget0-removes-the-guard-entirely-and-hides-every-budget-surface-a-restart-un-pauses-everything) |  | Active |  |  |
| [STARTUPCONFIG-17](startup-config.md#startupconfig-17--a-mis-spelled-configuration-key-changes-nothing-and-reports-nothing) |  | Active |  |  |
| [STARTUPCONFIG-20](startup-config.md#startupconfig-20--team__datadir-points-the-whole-application-at-a-different-folder--a-clean-slate-without-deleting-anything) |  | Active |  |  |
| [STARTUPCONFIG-21](startup-config.md#startupconfig-21--renaming-yourself-on-settings--profile-renames-you-everywhere-old-messages-included-without-rewriting-a-transcript) |  | Active |  |  |
| [STARTUPCONFIG-22](startup-config.md#startupconfig-22--the-renamed-team__acp__personadir-key-refuses-to-start-the-app-rather-than-scanning-nothing) |  | Active |  |  |
| [STARTUPCONFIG-23](startup-config.md#startupconfig-23--an-external-pipe-client-creates-a-room-live-with-no-page-refresh) |  | Active |  |  |
| [STARTUPCONFIG-24](startup-config.md#startupconfig-24--team__pipename-changes-the-pipe-and-silently-orphans-any-external-client) |  | Active |  |  |
| [STARTUPCONFIG-26](startup-config.md#startupconfig-26--environment-selection-decides-whether-acp-is-on-and-which-port-is-used) |  | Active |  |  |
| [STARTUPCONFIG-27](startup-config.md#startupconfig-27--team__acp__enabledfalse-starts-no-agent-process-and-costs-nothing-while-agents-still-works-fully) |  | Active |  |  |
| [STARTUPCONFIG-28](startup-config.md#startupconfig-28--team__acp__enabledtrue-with-only-the-built-in-chief-of-staff-starts-exactly-one-process) | 💰 | Active |  |  |
| [STARTUPCONFIG-29](startup-config.md#startupconfig-29--a-persona-file-in-its-own-agents-folder-is-discovered-reloads-on-edit-in-place-and-a-broken-file-is-named-rather-than-silently-dropped) |  | Active |  |  |
| [STARTUPCONFIG-30](startup-config.md#startupconfig-30--a-stale-loggingloglevel-key-silently-removes-the-console-evidence-other-tests-rely-on) |  | Active |  |  |
| [STARTUPCONFIG-31](startup-config.md#startupconfig-31--with-acp-on-and-one-persona-present-that-persona-starts-at-launch-and-its-tile-goes-starting-then-online) | 💰 | Active |  |  |
| [STARTUPCONFIG-32](startup-config.md#startupconfig-32--team__acp__tracewiretrue-dumps-the-tool-servers-bearer-token-to-the-console) |  | Active |  |  |
| [STARTUPCONFIG-33](startup-config.md#startupconfig-33--a-spent-per-persona-token-budget-reads-as-degraded-on-the-agent-tile-and-in-the-room-banner) | 💰 | Active |  |  |
| [STARTUPCONFIG-34](startup-config.md#startupconfig-34--a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again) | 💰 | Active |  |  |
| [STARTUPCONFIG-35](startup-config.md#startupconfig-35--a-blank-system-opens-on-settings--profile-until-you-save-a-profile) |  | Active |  |  |

## room-messaging

Room view, posting Messages and Transcript rendering — [area file](room-messaging.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [ROOMMESSAGING-01](room-messaging.md#roommessaging-01--landing-on--redirects-into-the-first-room-and-roomsid-deep-links) |  | Active |  |  |
| [ROOMMESSAGING-02](room-messaging.md#roommessaging-02--enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [ROOMMESSAGING-03](room-messaging.md#roommessaging-03--shiftenter-inserts-a-newline-and-does-not-send) |  | Active |  | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |
| [ROOMMESSAGING-04](room-messaging.md#roommessaging-04--an-empty-or-whitespace-only-enter-posts-nothing-and-shows-no-error) |  | Active |  |  |
| [ROOMMESSAGING-05](room-messaging.md#roommessaging-05--leading-and-trailing-whitespace-is-trimmed-before-the-message-is-stored) |  | Active |  |  |
| [ROOMMESSAGING-06](room-messaging.md#roommessaging-06--a-soft-line-break-collapses-to-a-space-a-blank-line-makes-two-paragraphs-a-list-still-works) |  | Active |  |  |
| [ROOMMESSAGING-07](room-messaging.md#roommessaging-07--core-markdown-renders-bold-italic-heading-lists-blockquote-inline-and-fenced-code-rule) |  | Active |  |  |
| [ROOMMESSAGING-08](room-messaging.md#roommessaging-08--the-four-enabled-markdig-extensions-render-pipe-tables-task-lists-autolinks-emphasis-extras) |  | Active |  |  |
| [ROOMMESSAGING-09](room-messaging.md#roommessaging-09--advanced-markdown-extensions-are-absent-generic-attributes-stay-literal-and-never-become-live-handlers) |  | Active |  |  |
| [ROOMMESSAGING-10](room-messaging.md#roommessaging-10--raw-html-inside-a-message-is-escaped-never-executed) |  | Active |  |  |
| [ROOMMESSAGING-11](room-messaging.md#roommessaging-11--link-schemes-are-rewritten-only-http-https-and-mailto-survive) |  | Active |  |  |
| [ROOMMESSAGING-12](room-messaging.md#roommessaging-12--non-ascii-and-html-sensitive-characters-are-escaped-in-the-jsonl-but-render-correctly-on-screen) |  | Active |  |  |
| [ROOMMESSAGING-13](room-messaging.md#roommessaging-13--a-long-message-a-very-long-unbroken-token-and-a-wide-code-block) |  | Active |  |  |
| [ROOMMESSAGING-14](room-messaging.md#roommessaging-14--messages-render-in-post-order-and-the-order-on-screen-equals-the-order-in-the-file) |  | Active |  |  |
| [ROOMMESSAGING-15](room-messaging.md#roommessaging-15--the-transcript-survives-a-browser-reload-and-is-present-in-the-prerendered-html) |  | Active |  |  |
| [ROOMMESSAGING-16](room-messaging.md#roommessaging-16--sender-name-avatar-and-time-on-an-agent-row-the-human-bubble-grouping-and-the-day-separator) |  | Active |  |  |
| [ROOMMESSAGING-17](room-messaging.md#roommessaging-17--the-transcript-autoscrolls-on-a-new-message-and-while-a-draft-grows) |  | Active |  |  |
| [ROOMMESSAGING-21](room-messaging.md#roommessaging-21--invite-name-from-the-composer-green-info-line-room-rename-live-sidebar-update) |  | Active |  |  |
| [ROOMMESSAGING-22](room-messaging.md#roommessaging-22--composer-errors-unknown-command-unknown-agent-and-the-typed-text-is-lost) |  | Active |  |  |
| [ROOMMESSAGING-23](room-messaging.md#roommessaging-23--typed-but-unsent-composer-text-carries-over-into-the-room-you-switch-to) |  | Active |  |  |
| [ROOMMESSAGING-24](room-messaging.md#roommessaging-24--the-jsonl-transcript-shape-append-only-and-one-file-per-room) |  | Active |  |  |
| [ROOMMESSAGING-25](room-messaging.md#roommessaging-25--an-external-pipe-clients-room-and-messages-appear-live-with-no-refresh) |  | Active |  |  |
| [ROOMMESSAGING-26](room-messaging.md#roommessaging-26--a-mention-of-a-name-containing-spaces-resolves-against-the-rooms-members) |  | Active |  |  |
| [ROOMMESSAGING-27](room-messaging.md#roommessaging-27--appearancejson-no-longer-customises-the-message-font-the-body-just-follows-the-theme-and-a-legacy-override-key-is-silently-inert) |  | Active |  |  |
| [ROOMMESSAGING-28](room-messaging.md#roommessaging-28--a-torn-or-corrupt-line-in-the-transcript-is-skipped-with-a-warning-not-fatal) |  | Active |  |  |
| [ROOMMESSAGING-29](room-messaging.md#roommessaging-29--the-transcript-survives-an-application-restart-drafts-and-the-budget-do-not) |  | Active |  |  |
| [ROOMMESSAGING-30](room-messaging.md#roommessaging-30--the-budget-pause-panel-sits-between-transcript-and-composer-and-a-human-message-resumes-it) |  | Active |  |  |
| [ROOMMESSAGING-31](room-messaging.md#roommessaging-31--the-room-view-survives-a-server-stop-and-restart-with-the-browser-open) |  | Active |  |  |
| [ROOMMESSAGING-32](room-messaging.md#roommessaging-32--a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money) | 💰 | Active |  |  |

## streaming-turn

Turn streaming, Drafts, Stop and failure surfacing — [area file](streaming-turn.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [STREAMINGTURN-01](streaming-turn.md#streamingturn-01--in-the-stock-configuration-no-room-shows-a-member-health-alert-strip) |  | Active |  |  |
| [STREAMINGTURN-02](streaming-turn.md#streamingturn-02--a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [STREAMINGTURN-03](streaming-turn.md#streamingturn-03--draft-text-renders-as-plain-text-and-becomes-markdown-only-when-the-message-lands) |  | Active |  |  |
| [STREAMINGTURN-04](streaming-turn.md#streamingturn-04--the-draft-is-replaced-by-exactly-one-message-and-never-shown-twice) |  | Active |  |  |
| [STREAMINGTURN-05](streaming-turn.md#streamingturn-05--the-stop-button-exists-beside-a-live-draft-and-nowhere-else-styled-as-an-ordinary-secondary-action) |  | Active |  |  |
| [STREAMINGTURN-06](streaming-turn.md#streamingturn-06--a-page-reload-mid-turn-shows-the-whole-draft-so-far-not-an-orphaned-suffix) |  | Active |  |  |
| [STREAMINGTURN-07](streaming-turn.md#streamingturn-07--two-browser-tabs-on-the-same-room-show-and-update-the-same-draft) |  | Active |  |  |
| [STREAMINGTURN-08](streaming-turn.md#streamingturn-08--a-draft-in-one-room-never-leaks-into-another-and-survives-navigating-away-and-back) |  | Active |  |  |
| [STREAMINGTURN-09](streaming-turn.md#streamingturn-09--draft-rows-always-render-below-every-message-whatever-the-true-chronology) |  | Active |  |  |
| [STREAMINGTURN-10](streaming-turn.md#streamingturn-10--the-message-list-auto-scrolls-as-a-draft-grows-not-only-when-a-message-arrives) |  | Active |  |  |
| [STREAMINGTURN-11](streaming-turn.md#streamingturn-11--a-draft-dies-with-its-agents-connection---no-frozen-half-reply-is-left-behind) |  | Active |  |  |
| [STREAMINGTURN-12](streaming-turn.md#streamingturn-12--tool-calls-show-as-rows-under-the-draft-at-most-six-and-never-reach-the-transcript) |  | Active |  |  |
| [STREAMINGTURN-13](streaming-turn.md#streamingturn-13--a-draft-written-for-a-room-the-sender-is-not-a-member-of-is-refused-and-nothing-renders) |  | Active |  |  |
| [STREAMINGTURN-14](streaming-turn.md#streamingturn-14--two-agents-stream-into-one-room-at-once-and-neither-draft-is-dropped) |  | Active |  |  |
| [STREAMINGTURN-15](streaming-turn.md#streamingturn-15--stop-disables-itself-while-the-request-is-in-flight-per-agent-rather-than-per-room) |  | Active |  |  |
| [STREAMINGTURN-16](streaming-turn.md#streamingturn-16--clicking-stop-on-an-agent-with-no-live-connection-is-a-silent-no-op) |  | Active |  |  |
| [STREAMINGTURN-17](streaming-turn.md#streamingturn-17--a-stop-click-reaches-the-wire-even-when-the-client-ignores-it) |  | Active |  |  |
| [STREAMINGTURN-18](streaming-turn.md#streamingturn-18--a-draft-stops-growing-at-256-kb-but-the-posted-message-still-arrives-intact) |  | Active |  |  |
| [STREAMINGTURN-19](streaming-turn.md#streamingturn-19--restarting-the-application-loses-every-draft-in-flight-and-nothing-resurrects-as-a-message) |  | Active |  |  |
| [STREAMINGTURN-20](streaming-turn.md#streamingturn-20--a-spent-room-budget-shows-the-continue-prompt-and-never-a-health-strip) |  | Active |  |  |
| [STREAMINGTURN-21](streaming-turn.md#streamingturn-21--a-failed-persona-start-surfaces-as-a-rolealert-strip-in-every-room-it-is-a-member-of) | 💰 | Active |  |  |
| [STREAMINGTURN-22](streaming-turn.md#streamingturn-22--restart-from-the-agent-card-clears-the-failure-and-the-rooms-strip-goes-with-it) | 💰 | Active |  |  |
| [STREAMINGTURN-23](streaming-turn.md#streamingturn-23--a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it) | 💰 | Active |  |  |
| [STREAMINGTURN-24](streaming-turn.md#streamingturn-24--stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written) | 💰 | Active |  |  |
| [STREAMINGTURN-25](streaming-turn.md#streamingturn-25--a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak) | 💰 | Active |  |  |
| [STREAMINGTURN-26](streaming-turn.md#streamingturn-26--stop-ends-this-rooms-live-turn-and-discards-everything-queued-behind-it-in-that-room) | 💰 | Active |  |  |
| [STREAMINGTURN-27](streaming-turn.md#streamingturn-27--stopping-an-agent-in-one-room-does-not-touch-its-turn-in-another-room) | 💰 | Active |  |  |
| [STREAMINGTURN-28](streaming-turn.md#streamingturn-28--a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it) | 💰 | Active |  |  |
| [STREAMINGTURN-29](streaming-turn.md#streamingturn-29--a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row) | 💰 | Active |  |  |
| [STREAMINGTURN-30](streaming-turn.md#streamingturn-30--killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever) | 💰 | Active |  |  |

## reply-gate-budget

Reply Gate, Mentions and Room Budget — [area file](reply-gate-budget.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [REPLYGATEBUDGET-02](reply-gate-budget.md#replygatebudget-02--three-or-more-members-makes-the-same-room-mention-gated) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [REPLYGATEBUDGET-03](reply-gate-budget.md#replygatebudget-03--invite-from-the-composer-and-its-two-error-strips-never-touch-the-transcript) |  | Active |  |  |
| [REPLYGATEBUDGET-04](reply-gate-budget.md#replygatebudget-04--a-mention-is-case-insensitive-and-ends-at-trailing-punctuation) |  | Active |  |  |
| [REPLYGATEBUDGET-05](reply-gate-budget.md#replygatebudget-05--repeating-a-mention-wakes-an-agent-once-and-the-human-can-be-mentioned-harmlessly) |  | Active |  |  |
| [REPLYGATEBUDGET-06](reply-gate-budget.md#replygatebudget-06--the-budget-note-counts-agent-replies-and-a-human-message-resets-it-to-nothing) |  | Active |  |  |
| [REPLYGATEBUDGET-07](reply-gate-budget.md#replygatebudget-07--a-spent-budget-halts-the-room-and-asks-the-human-visibly) |  | Active |  |  |
| [REPLYGATEBUDGET-08](reply-gate-budget.md#replygatebudget-08--the-write-path-refuses-an-over-budget-post-two-agents-are-woken-only-one-message-lands) |  | Active |  |  |
| [REPLYGATEBUDGET-09](reply-gate-budget.md#replygatebudget-09--leave-paused-hides-the-question-but-keeps-the-pause-legible) |  | Active |  |  |
| [REPLYGATEBUDGET-10](reply-gate-budget.md#replygatebudget-10--a-dismissal-is-per-view-navigation-reload-and-the-next-message-all-bring-the-question-back) |  | Active |  |  |
| [REPLYGATEBUDGET-11](reply-gate-budget.md#replygatebudget-11--the-pause-survives-a-reload-and-shows-in-a-brand-new-tab) |  | Active |  |  |
| [REPLYGATEBUDGET-12](reply-gate-budget.md#replygatebudget-12--continue-grants-exactly-one-more-budget-once-per-click-and-duplicates-nothing-on-screen) |  | Active |  |  |
| [REPLYGATEBUDGET-13](reply-gate-budget.md#replygatebudget-13--any-human-message-resets-the-budget-is-never-refused-and-expires-an-earlier-grant) |  | Active |  |  |
| [REPLYGATEBUDGET-14](reply-gate-budget.md#replygatebudget-14--the-budget-is-per-room-a-paused-room-does-not-starve-the-one-beside-it) |  | Active |  |  |
| [REPLYGATEBUDGET-15](reply-gate-budget.md#replygatebudget-15--a-restart-un-pauses-every-room-over-a-transcript-that-already-spent-its-budget) |  | Active |  |  |
| [REPLYGATEBUDGET-16](reply-gate-budget.md#replygatebudget-16--a-budget-of-zero-removes-the-cap-entirely) |  | Active |  |  |
| [REPLYGATEBUDGET-17](reply-gate-budget.md#replygatebudget-17--the-model-facing-wording-of-this-area-is-readable-and-editable-at-settingsprompts) |  | Active |  |  |
| [REPLYGATEBUDGET-18](reply-gate-budget.md#replygatebudget-18--the-server-labels-a-delivery-correctly-even-when-the-client-chooses-to-stay-silent) |  | Active |  |  |
| [REPLYGATEBUDGET-19](reply-gate-budget.md#replygatebudget-19--the-longest-handle-wins-emily-lee-reaches-emily-lee-never-emily) |  | Active |  |  |
| [REPLYGATEBUDGET-20](reply-gate-budget.md#replygatebudget-20--a-mention-falls-back-to-the-shorter-name-when-the-longer-one-is-not-in-this-room) |  | Active |  |  |
| [REPLYGATEBUDGET-21](reply-gate-budget.md#replygatebudget-21--two-mentions-in-one-message-do-not-run-together-and-a-multi-word-name-stops-at-punctuation) |  | Active |  |  |
| [REPLYGATEBUDGET-22](reply-gate-budget.md#replygatebudget-22--a-mention-ends-at-a-word-boundary-and-an-email-address-is-not-a-mention) |  | Active |  |  |
| [REPLYGATEBUDGET-23](reply-gate-budget.md#replygatebudget-23--an-agent-never-receives-its-own-message-so-no-self-echo-loop-can-start) |  | Active |  |  |
| [REPLYGATEBUDGET-24](reply-gate-budget.md#replygatebudget-24--the-refusal-an-over-budget-agent-reads-is-worded-as-terminal-on-the-pipe-door) |  | Active |  |  |
| [REPLYGATEBUDGET-25](reply-gate-budget.md#replygatebudget-25--continue-re-delivers-the-last-message-to-the-other-agents-once-with-the-raised-budget) |  | Active |  |  |
| [REPLYGATEBUDGET-26](reply-gate-budget.md#replygatebudget-26--continue-in-a-two-member-room-grants-budget-and-wakes-nobody--and-that-is-correct) |  | Active |  |  |
| [REPLYGATEBUDGET-27](reply-gate-budget.md#replygatebudget-27--an-alias-resolves-a-mention-to-the-persona-that-owns-it) |  | Active |  |  |
| [REPLYGATEBUDGET-28](reply-gate-budget.md#replygatebudget-28--a-longer-alias-beats-a-shorter-name-and-an-equal-length-name-beats-an-alias) |  | Active |  |  |
| [REPLYGATEBUDGET-29](reply-gate-budget.md#replygatebudget-29--reconnecting-re-attaches-to-the-same-room-but-after-an-invite-a-fresh-two-member-room-appears) |  | Active |  |  |
| [REPLYGATEBUDGET-30](reply-gate-budget.md#replygatebudget-30--a-mention-that-reaches-nobody-is-completely-silent--and-how-to-tell-that-apart-from-a-bug) |  | Active |  |  |
| [REPLYGATEBUDGET-31](reply-gate-budget.md#replygatebudget-31--paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-32](reply-gate-budget.md#replygatebudget-32--paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-33](reply-gate-budget.md#replygatebudget-33--paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-34](reply-gate-budget.md#replygatebudget-34--paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-35](reply-gate-budget.md#replygatebudget-35--paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-36](reply-gate-budget.md#replygatebudget-36--paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it) | 💰 | Active |  |  |
| [REPLYGATEBUDGET-37](reply-gate-budget.md#replygatebudget-37--continue-on-a-paused-room-whose-transcript-is-empty-says-there-is-nothing-to-re-deliver) |  | Active |  |  |

## agents-library

Agents page: tiles, Teams grouping, filter and rejected files — [area file](agents-library.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [AGENTSLIBRARY-01](agents-library.md#agentslibrary-01--the-page-loads-is-styled-and-spawns-nothing) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [AGENTSLIBRARY-02](agents-library.md#agentslibrary-02--a-library-of-only-the-chief-of-staff-reads-as-one-tile-then-as-empty) |  | Active |  |  |
| [AGENTSLIBRARY-03](agents-library.md#agentslibrary-03--the-fixture-library-loads-and-a-tile-shows-monogram-name-title-an-alias-caption-and-status) |  | Active |  |  |
| [AGENTSLIBRARY-04](agents-library.md#agentslibrary-04--one-heading-per-team-ordinal-order-no-team-last-no-empty-groups) |  | Active |  |  |
| [AGENTSLIBRARY-05](agents-library.md#agentslibrary-05--a-persona-in-two-teams-renders-in-full-under-both-headings) |  | Active |  |  |
| [AGENTSLIBRARY-06](agents-library.md#agentslibrary-06--a-personas-team-comes-from-its-field-never-from-its-folder) |  | Active |  |  |
| [AGENTSLIBRARY-07](agents-library.md#agentslibrary-07--the-team-filters-label-options-order-and-de-duplication) |  | Active |  |  |
| [AGENTSLIBRARY-08](agents-library.md#agentslibrary-08--choosing-a-team-narrows-to-one-heading-and-drops-the-no-team-personas) |  | Active |  |  |
| [AGENTSLIBRARY-09](agents-library.md#agentslibrary-09--a-team-whose-membership-drops-to-zero-says-so) |  | Active |  |  |
| [AGENTSLIBRARY-10](agents-library.md#agentslibrary-10--team-names-match-case-insensitively-and-appear-once) |  | Active |  |  |
| [AGENTSLIBRARY-11](agents-library.md#agentslibrary-11--all-three-yaml-shapes-of-teams-group-identically-and-the-documented-comma-cost) |  | Active |  |  |
| [AGENTSLIBRARY-12](agents-library.md#agentslibrary-12--a-rejected-file-is-named-by-path-and-reason-above-the-tiles-live) |  | Active |  |  |
| [AGENTSLIBRARY-13](agents-library.md#agentslibrary-13--every-distinct-rejection-reason-word-for-word) |  | Active |  |  |
| [AGENTSLIBRARY-14](agents-library.md#agentslibrary-14--a-duplicate-alias-rejects-both-files-each-naming-the-other) |  | Active |  |  |
| [AGENTSLIBRARY-15](agents-library.md#agentslibrary-15--a-duplicate-name--including-one-differing-only-in-case--rejects-both) |  | Active |  |  |
| [AGENTSLIBRARY-16](agents-library.md#agentslibrary-16--one-files-alias-equal-to-another-files-name-rejects-both-with-two-different-sentences--and-a-self-matching-alias-is-legal) |  | Active |  |  |
| [AGENTSLIBRARY-17](agents-library.md#agentslibrary-17--the-rejected-block-is-ordered-survives-the-filter-and-coexists-with-the-empty-library-sentence) |  | Active |  |  |
| [AGENTSLIBRARY-18](agents-library.md#agentslibrary-18--non-markdown-files-are-ignored-entirely--not-loaded-not-rejected) |  | Active |  |  |
| [AGENTSLIBRARY-19](agents-library.md#agentslibrary-19--the-watcher-is-live-an-edit-to-an-agents-definition-reloads-it-and-a-write-under-work-does-not) |  | Active |  |  |
| [AGENTSLIBRARY-20](agents-library.md#agentslibrary-20--the-watcher-notices-creations-and-deletions-while-the-page-is-open) |  | Active |  |  |
| [AGENTSLIBRARY-21](agents-library.md#agentslibrary-21--renaming-or-deleting-an-agent-folder-from-outside-is-noticed-and-the-card-follows-the-file) |  | Active |  |  |
| [AGENTSLIBRARY-22](agents-library.md#agentslibrary-22--a-burst-of-file-writes-becomes-one-update-not-five) |  | Active |  |  |
| [AGENTSLIBRARY-23](agents-library.md#agentslibrary-23--a-dropped-event-storm-still-converges-on-what-is-on-disk) |  | Active |  |  |
| [AGENTSLIBRARY-24](agents-library.md#agentslibrary-24--a-tile-click-opens-the-card-and-the-card-shows-the-real-file-path) |  | Active |  |  |
| [AGENTSLIBRARY-25](agents-library.md#agentslibrary-25--offline-is-the-honest-default-with-no-invented-reason) |  | Active |  |  |
| [AGENTSLIBRARY-27](agents-library.md#agentslibrary-27--badges-repaint-live-on-connect-and-disconnect-with-no-refresh) |  | Active |  |  |
| [AGENTSLIBRARY-41](agents-library.md#agentslibrary-41--a-definition-file-at-the-agents-root-is-rejected-naming-its-path) |  | Active |  |  |
| [AGENTSLIBRARY-42](agents-library.md#agentslibrary-42--a-second-md-in-an-agent-folder-is-rejected-and-work-is-the-place-for-notes) |  | Active |  |  |
| [AGENTSLIBRARY-43](agents-library.md#agentslibrary-43--files-in-_unsorted-are-rejected-as-migrated-leftovers-other-underscore-folders-are-skipped) |  | Active |  |  |
| [AGENTSLIBRARY-28](agents-library.md#agentslibrary-28--an-edit-to-an-agents-file-from-outside-changes-only-what-the-file-says-the-stored-model-and-effort-stay) |  | Active |  |  |
| [AGENTSLIBRARY-29](agents-library.md#agentslibrary-29--new-agent-writes-its-own-folder-and-file-under-agents-with-the-teams-field-it-was-given) |  | Active |  |  |
| [AGENTSLIBRARY-30](agents-library.md#agentslibrary-30--a-save-that-would-create-a-rejected-file-is-refused-before-anything-is-written) |  | Active |  |  |
| [AGENTSLIBRARY-31](agents-library.md#agentslibrary-31--renaming-through-the-frontmatter-the-tile-renames-model-and-effort-follow-and-the-folder-and-file-are-renamed-with-it) |  | Active |  |  |
| [AGENTSLIBRARY-32](agents-library.md#agentslibrary-32--remove-takes-the-file-the-model-and-the-effort--and-not-the-chats) |  | Active |  |  |
| [AGENTSLIBRARY-33](agents-library.md#agentslibrary-33--open-launches-the-persona-file-on-the-server-from-its-agent-folder) |  | Active |  |  |
| [AGENTSLIBRARY-34](agents-library.md#agentslibrary-34--neither-a-page-load-a-filter-change-nor-a-view-card-ever-spawns-an-adapter) |  | Active |  |  |
| [AGENTSLIBRARY-35](agents-library.md#agentslibrary-35--restart-with-acp-switched-off-reports-why-and-starts-nothing) |  | Active |  |  |
| [AGENTSLIBRARY-36](agents-library.md#agentslibrary-36--restart-is-busy-flagged-and-cannot-be-double-fired) |  | Active |  |  |
| [AGENTSLIBRARY-37](agents-library.md#agentslibrary-37--offline-with-a-reason-in-both-of-its-homes) |  | Active |  |  |
| [AGENTSLIBRARY-38](agents-library.md#agentslibrary-38--health-outranks-pipe-liveness-a-live-pipe-with-a-failed-start-must-read-offline) |  | Active |  |  |
| [AGENTSLIBRARY-39](agents-library.md#agentslibrary-39--the-whole-page-in-dark-mode-with-a-card-open-and-a-rejected-file-present) |  | Active |  |  |
| [AGENTSLIBRARY-40](agents-library.md#agentslibrary-40--degraded-with-the-reason--and-the-two-things-that-must-not-badge) | 💰 | Active |  |  |

## agent-profile-card

Agent card: view, edit, create, delete — [area file](agent-profile-card.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [AGENTPROFILECARD-01](agent-profile-card.md#agentprofilecard-01--empty-agents-page-and-the-new-agent-entry-point) |  | Active |  |  |
| [AGENTPROFILECARD-02](agent-profile-card.md#agentprofilecard-02--clicking-new-agent-opens-a-card-at-all-blank-name-crash-probe) |  | Active |  |  |
| [AGENTPROFILECARD-03](agent-profile-card.md#agentprofilecard-03--create-card-every-label-placeholder-and-hint) |  | Active |  |  |
| [AGENTPROFILECARD-04](agent-profile-card.md#agentprofilecard-04--monogram-tracks-the-name-as-you-type) |  | Active |  |  |
| [AGENTPROFILECARD-05](agent-profile-card.md#agentprofilecard-05--create-the-exact-name-acceptreject-set) |  | Active |  |  |
| [AGENTPROFILECARD-06](agent-profile-card.md#agentprofilecard-06--create-alias-validation-uses-the-same-rules-and-its-own-wording) |  | Active |  |  |
| [AGENTPROFILECARD-07](agent-profile-card.md#agentprofilecard-07--create-validation-precedence--which-error-wins) |  | Active |  |  |
| [AGENTPROFILECARD-08](agent-profile-card.md#agentprofilecard-08--create-the-title-is-prose--blank-rejected-commas-preserved) |  | Active |  |  |
| [AGENTPROFILECARD-09](agent-profile-card.md#agentprofilecard-09--create-a-failed-save-never-throws-away-what-you-typed) |  | Active |  |  |
| [AGENTPROFILECARD-10](agent-profile-card.md#agentprofilecard-10--enter-in-an-identity-field-does-not-submit-and-saving-never-reloads-the-page) |  | Active |  |  |
| [AGENTPROFILECARD-11](agent-profile-card.md#agentprofilecard-11--a-successful-create-lands-on-the-new-agents-view-card) |  | Active |  |  |
| [AGENTPROFILECARD-12](agent-profile-card.md#agentprofilecard-12--view-card-every-section-and-action-in-order) |  | Active |  |  |
| [AGENTPROFILECARD-13](agent-profile-card.md#agentprofilecard-13--view-card-agent-default-and-model-default-when-nothing-is-stored) |  | Active |  |  |
| [AGENTPROFILECARD-14](agent-profile-card.md#agentprofilecard-14--closing-the-dialog-focus-trap-on-open-escape-the-close-icon-cancel-scroll-locking-and-focus-return) |  | Active |  |  |
| [AGENTPROFILECARD-15](agent-profile-card.md#agentprofilecard-15--closing-a-card-clears-every-bit-of-its-state) |  | Active |  |  |
| [AGENTPROFILECARD-16](agent-profile-card.md#agentprofilecard-16--remove-is-a-two-step-confirm-and-cancel-backs-out-cleanly) |  | Active |  |  |
| [AGENTPROFILECARD-17](agent-profile-card.md#agentprofilecard-17--team-headings-and-the-team-filter-come-from-the-teams-field-not-the-folder) |  | Active |  |  |
| [AGENTPROFILECARD-18](agent-profile-card.md#agentprofilecard-18--create-exactly-what-lands-on-disk) |  | Active |  |  |
| [AGENTPROFILECARD-19](agent-profile-card.md#agentprofilecard-19--create-an-apostrophe-in-the-title-round-trips-through-yaml-quoting) |  | Active |  |  |
| [AGENTPROFILECARD-20](agent-profile-card.md#agentprofilecard-20--create-the-teams-field-trims-drops-blanks-and-collapses-duplicates-on-read-back) |  | Active |  |  |
| [AGENTPROFILECARD-21](agent-profile-card.md#agentprofilecard-21--create-a-duplicate-filename-is-caught-before-anything-is-written) |  | Active |  |  |
| [AGENTPROFILECARD-22](agent-profile-card.md#agentprofilecard-22--edit-card-what-is-editable-and-what-a-rename-announces) |  | Active |  |  |
| [AGENTPROFILECARD-23](agent-profile-card.md#agentprofilecard-23--edit-save-writes-back-to-the-same-file-path--never-a-rename-never-a-move) |  | Active |  |  |
| [AGENTPROFILECARD-24](agent-profile-card.md#agentprofilecard-24--edit-breaking-the-frontmatter-is-refused-with-the-file-untouched) |  | Active |  |  |
| [AGENTPROFILECARD-25](agent-profile-card.md#agentprofilecard-25--edit-renaming-into-a-collision-is-refused-with-the-file-untouched) |  | Active |  |  |
| [AGENTPROFILECARD-26](agent-profile-card.md#agentprofilecard-26--edit-renaming-moves-the-folder-and-keeps-the-agent-whole-adr-0011) |  | Active |  |  |
| [AGENTPROFILECARD-27](agent-profile-card.md#agentprofilecard-27--create-a-name-colliding-with-a-persona-whose-folder-has-another-name) |  | Active |  |  |
| [AGENTPROFILECARD-28](agent-profile-card.md#agentprofilecard-28--create-alias-collisions-and-the-one-that-is-legal) |  | Active |  |  |
| [AGENTPROFILECARD-29](agent-profile-card.md#agentprofilecard-29--files-that-didnt-load-names-the-path-and-the-reason-above-the-list) |  | Active |  |  |
| [AGENTPROFILECARD-30](agent-profile-card.md#agentprofilecard-30--an-external-file-edit-repaints-the-page--including-inside-agent-folders) |  | Active |  |  |
| [AGENTPROFILECARD-31](agent-profile-card.md#agentprofilecard-31--a-card-whose-file-vanished-underneath-it-reports-it-instead-of-crashing) |  | Active |  |  |
| [AGENTPROFILECARD-32](agent-profile-card.md#agentprofilecard-32--open-launches-the-os-editor-on-the-server) |  | Active |  |  |
| [AGENTPROFILECARD-33](agent-profile-card.md#agentprofilecard-33--a-windows-reserved-device-name-as-a-name-documented-limit--record-the-outcome) |  | Active |  |  |
| [AGENTPROFILECARD-34](agent-profile-card.md#agentprofilecard-34--two-browser-tabs-on-agents-stay-in-step) |  | Active |  |  |
| [AGENTPROFILECARD-52](agent-profile-card.md#agentprofilecard-52--the-chief-of-staff-offers-reset-to-default-and-no-remove) |  | Active |  |  |
| [AGENTPROFILECARD-35](agent-profile-card.md#agentprofilecard-35--remove-deletes-the-file-and-all-three-stored-settings) |  | Active |  |  |
| [AGENTPROFILECARD-36](agent-profile-card.md#agentprofilecard-36--a-stored-model-or-effort-the-catalog-does-not-advertise-survives-an-edit) |  | Active |  |  |
| [AGENTPROFILECARD-37](agent-profile-card.md#agentprofilecard-37--the-model-probe-never-runs-on-a-plain-page-load) |  | Active |  |  |
| [AGENTPROFILECARD-38](agent-profile-card.md#agentprofilecard-38--model-and-effort-pickers-labels-loading-text-and-the-empty-catalog-text) |  | Active |  |  |
| [AGENTPROFILECARD-39](agent-profile-card.md#agentprofilecard-39--changing-the-model-clears-the-effort-and-re-probes-the-ladder-haiku---sonnet) |  | Active |  |  |
| [AGENTPROFILECARD-40](agent-profile-card.md#agentprofilecard-40--model-is-stored-as-an-id-in-teamdb-only) |  | Active |  |  |
| [AGENTPROFILECARD-41](agent-profile-card.md#agentprofilecard-41--effort-low---medium-is-stored-as-an-id-and-the-blank-option-deletes-the-row) |  | Active |  |  |
| [AGENTPROFILECARD-42](agent-profile-card.md#agentprofilecard-42--restart-with-teamacpenabledfalse-reports-offline-and-starts-nothing) |  | Active |  |  |
| [AGENTPROFILECARD-43](agent-profile-card.md#agentprofilecard-43--a-newly-created-agent-comes-online-by-itself-when-acp-is-on) |  | Active |  |  |
| [AGENTPROFILECARD-44](agent-profile-card.md#agentprofilecard-44--the-message-action-is-read-once-when-the-card-opens) |  | Active |  |  |
| [AGENTPROFILECARD-45](agent-profile-card.md#agentprofilecard-45--restart-is-offered-in-every-state-except-starting) |  | Active |  |  |
| [AGENTPROFILECARD-46](agent-profile-card.md#agentprofilecard-46--a-save-that-changes-nothing-does-not-restart-the-agent) |  | Active |  |  |
| [AGENTPROFILECARD-47](agent-profile-card.md#agentprofilecard-47--changing-only-the-model-or-only-the-effort-restarts-the-session) |  | Active |  |  |
| [AGENTPROFILECARD-48](agent-profile-card.md#agentprofilecard-48--remove-does-not-cascade-the-agent-and-its-room-survive) |  | Active |  |  |
| [AGENTPROFILECARD-49](agent-profile-card.md#agentprofilecard-49--delete-then-recreate-under-the-same-name-fresh-settings-old-room) |  | Active |  |  |
| [AGENTPROFILECARD-50](agent-profile-card.md#agentprofilecard-50--editing-an-agent-really-does-lose-its-conversation-memory-costs-money) | 💰 | Active |  |  |
| [AGENTPROFILECARD-51](agent-profile-card.md#agentprofilecard-51--the-edit-cards-name-title-and-alias-boxes-write-into-the-frontmatter-and-preserve-every-other-field) |  | Active |  |  |
| [AGENTPROFILECARD-53](agent-profile-card.md#agentprofilecard-53--an-avatar-only-save-does-not-restart-the-agent) |  | Active |  |  |

## library

Library explorer and editor — [area file](library.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [L0](library.md#l0--chat-message-with-absolute-path-becomes-a-library-link-and-opens-the-library-tab-on-that-file) |  | Active |  |  |
| [L1](library.md#l1--human-edits-a-file-in-the-library-and-saves-it-the-agents-next-turn-lists-changed-) |  | Active |  |  |
| [L2](library.md#l2--human-creates-a-project-folder-in-the-library-from-the-team-node) |  | Active |  |  |
| [L3](library.md#l3--a-persona-gains-a-team-label-the-team-folder-is-created-when-the-persona-is-saved) |  | Active |  |  |
| [L4](library.md#l4--wikilinks-are-rewritten-when-a-file-is-renamed-the-rename-dialog-reports-the-link-count) |  | Active |  |  |
| [L5](library.md#l5--editing-a-persona-definition-file-shows-a-save-time-warning) |  | Active |  |  |
| [L6](library.md#l6--team-and-project-folders-are-protected-rename-move-and-delete-are-shown-disabled-with-a-reason) |  | Active |  |  |
| [L7](library.md#l7--crafted-paths-and-junctions-that-leave-the-root-are-refused-by-the-resolver) |  | Active |  |  |
| [L8](library.md#l8--deleting-a-file-sends-it-to-the-windows-recycle-bin-with-a-confirm-dialog) |  | Active |  |  |
| [L9](library.md#l9--when-a-file-changes-on-disk-and-the-human-has-no-unsaved-edits-the-new-text-is-shown-on-focus) |  | Active |  |  |
| [L10](library.md#l10--when-a-file-changes-on-disk-while-the-human-has-unsaved-edits-the-human-is-warned-and-the-humans-text-wins) |  | Active |  |  |
| [L11](library.md#l11--opening-different-file-types-images-preview-json-displays-read-only-powerpoint-offers-open-in-default-app) |  | Active |  |  |
| [L12](library.md#l12--copy-a-document-and-paste-it-into-a-chat-agents-receive-the-library-document-block) |  | Active |  |  |
| [L13](library.md#l13--pasting-a-library-path-outside-huddle-eg-in-claude-code-is-an-ordinary-absolute-path) | 💰 | Active |  |  |
| [L14](library.md#l14--the-divider-position-of-the-library-pages-explorer-is-remembered) |  | Active |  |  |
| [L15](library.md#l15--pop-out-opens-the-library-page-with-tree-and-document-side-by-side) |  | Active |  |  |
| [L16](library.md#l16--old-teamsmd--work-install-is-migrated-to-the-agents-folder-layout-on-startup) |  | Active |  |  |
| [L17](library.md#l17--a-real-obsidian-vault-is-pinned-as-a-library-root-and-can-be-edited-without-reformatting) |  | Active |  |  |
| [L18](library.md#l18--runps1--cleanonly-deletes-the-database-and-agent-definitions-and-keeps-everything-else) |  | Active |  |  |
| [L19](library.md#l19--the-markdown-toolbar-and-its-shortcuts-toggle-markers-undo-in-one-step-and-are-keyboard-reachable) |  | Active |  |  |
| [L20](library.md#l20--a-toolbar-edit-saves-byte-exact-on-a-crlf-note-with-a-bom) |  | Active |  |  |
| [L21](library.md#l21---suggests-notes-from-the-notes-own-root-nearest-first-and-does-not-double-) |  | Active |  |  |

## model-effort

Model and Effort pickers, catalog probe and runner restart — [area file](model-effort.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [MODELEFFORT-01](model-effort.md#modeleffort-01--first-card-open-the-model-list-comes-from-the-adapter-and-one-adapter-spawn-fills-both-lists) |  | Active |  |  |
| [MODELEFFORT-02](model-effort.md#modeleffort-02--fixture-walk-record-every-models-effort-ladder-and-one-spawn-per-newly-seen-model) |  | Active |  |  |
| [MODELEFFORT-03](model-effort.md#modeleffort-03--saving-a-new-agent-stores-wire-ids-never-display-names) |  | Active |  |  |
| [MODELEFFORT-04](model-effort.md#modeleffort-04--two-different-default-choices-in-the-model-select-and-none-in-the-effort-select) |  | Active |  |  |
| [MODELEFFORT-05](model-effort.md#modeleffort-05--the-card-shows-the-personas-real-values-in-view-and-edit-never-a-bare-field-name) |  | Active |  |  |
| [MODELEFFORT-06](model-effort.md#modeleffort-06--neither-catalog-is-probed-on-a-plain-page-load-or-a-tile-click) |  | Active |  |  |
| [MODELEFFORT-07](model-effort.md#modeleffort-07--changing-the-model-re-probes-the-effort-list-changes-and-the-chosen-level-resets) |  | Active |  |  |
| [MODELEFFORT-08](model-effort.md#modeleffort-08--a-superseded-effort-probe-never-lands-on-the-model-the-user-ended-up-with) |  | Active |  |  |
| [MODELEFFORT-09](model-effort.md#modeleffort-09--closing-the-card-mid-probe-leaves-no-stuck-state) |  | Active |  |  |
| [MODELEFFORT-10](model-effort.md#modeleffort-10--a-model-with-no-effort-support-says-so-and-the-log-proves-it-was-not-a-failed-probe) |  | Active |  |  |
| [MODELEFFORT-11](model-effort.md#modeleffort-11--edit-preselects-the-stored-model-and-effort-and-keeps-them-when-the-catalog-lands-late) |  | Active |  |  |
| [MODELEFFORT-12](model-effort.md#modeleffort-12--a-stored-value-the-adapter-no-longer-advertises-is-still-offered-and-still-saved) |  | Active |  |  |
| [MODELEFFORT-13](model-effort.md#modeleffort-13--remove-an-agent-and-recreate-it-with-the-same-name---nothing-resurrects) |  | Active |  |  |
| [MODELEFFORT-14](model-effort.md#modeleffort-14--editing-the-frontmatter-name-moves-the-model-and-effort-rows-and-only-those) |  | Active |  |  |
| [MODELEFFORT-15](model-effort.md#modeleffort-15--the-view-cards-model-and-effort-sections-and-the-label-versus-id-inconsistency) |  | Active |  |  |
| [MODELEFFORT-16](model-effort.md#modeleffort-16--the-probe-runs-in-app_dataagents-never-in-the-repository-root) |  | Active |  |  |
| [MODELEFFORT-17](model-effort.md#modeleffort-17--two-browser-tabs-opening-cards-at-once-spawn-one-adapter-not-two) |  | Active |  |  |
| [MODELEFFORT-18](model-effort.md#modeleffort-18--the-model-catalog-is-cached-for-the-whole-app-run-and-goes-fresh-on-restart) |  | Active |  |  |
| [MODELEFFORT-19](model-effort.md#modeleffort-19--no-adapter-installed-the-page-still-works-and-a-failed-probe-is-never-cached) |  | Active |  |  |
| [MODELEFFORT-20](model-effort.md#modeleffort-20--adapter-present-but-broken-launch-failure-and-auth-failure-degrade-to-the-same-picker-with-distinct-log-lines) |  | Active |  |  |
| [MODELEFFORT-21](model-effort.md#modeleffort-21--an-existing-app_data-opens-with-the-two-tables-added-in-place-no-wipe) |  | Active |  |  |
| [MODELEFFORT-22](model-effort.md#modeleffort-22--restart-lane-changing-the-model-restarts-the-runner-exactly-once) |  | Active |  |  |
| [MODELEFFORT-23](model-effort.md#modeleffort-23--restart-lane-changing-effort-low-to-medium-restarts-with-no-catalog-warning) |  | Active |  |  |
| [MODELEFFORT-24](model-effort.md#modeleffort-24--restart-lane-saving-with-nothing-changed-does-not-restart-the-runner) |  | Active |  |  |
| [MODELEFFORT-25](model-effort.md#modeleffort-25--restart-lane-a-stale-stored-model-or-effort-warns-but-never-blocks-the-agent) |  | Active |  |  |
| [MODELEFFORT-26](model-effort.md#modeleffort-26--restart-lane-the-manual-restart-button-is-offered-in-every-state-except-starting-and-works-on-an-offline-or-degraded-agent) |  | Active |  |  |
| [MODELEFFORT-27](model-effort.md#modeleffort-27--money-the-chosen-model-and-effort-actually-reach-the-model---ask-it) | 💰 | Active |  |  |
| [MODELEFFORT-28](model-effort.md#modeleffort-28--both-pickers-are-inert-while-their-own-catalog-is-being-probed-and-the-model-list-lands-without-waiting-on-the-effort-one) |  | Active |  |  |

## invite-rooms

Creating Rooms, inviting Agents, Room naming — [area file](invite-rooms.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [INVITEROOMS-02](invite-rooms.md#inviterooms-02--routing--redirects-to-the-first-room-and-an-unknown-room-id-shows-the-empty-state-instead-of-crashing) |  | Active |  |  |
| [INVITEROOMS-03](invite-rooms.md#inviterooms-03--the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked) |  | Active |  |  |
| [INVITEROOMS-04](invite-rooms.md#inviterooms-04--a-pipe-client-connecting-creates-its-room-and-it-appears-in-the-sidebar-with-no-page-refresh) |  | Active |  |  |
| [INVITEROOMS-05](invite-rooms.md#inviterooms-05--reconnecting-under-the-same-name-re-attaches-to-the-existing-room--it-never-mints-a-second-one) |  | Active |  |  |
| [INVITEROOMS-06](invite-rooms.md#inviterooms-06--a-name-differing-only-in-case-re-attaches-to-the-existing-agent-instead-of-creating-a-second-one) |  | Active |  |  |
| [INVITEROOMS-07](invite-rooms.md#inviterooms-07--an-invalid-agent-name-is-refused-at-the-handshake-and-no-room-appears-anywhere) |  | Active |  |  |
| [INVITEROOMS-08](invite-rooms.md#inviterooms-08--new-chat-with-exactly-one-agent-opens-that-agents-existing-room-and-creates-nothing) |  | Active |  |  |
| [INVITEROOMS-09](invite-rooms.md#inviterooms-09--new-chat-with-two-agents-creates-a-room-named-after-them-and-navigates-to-it-live) |  | Active |  |  |
| [INVITEROOMS-10](invite-rooms.md#inviterooms-10--the-same-two-agent-selection-twice-creates-a-second-room-with-the-identical-name) |  | Active |  |  |
| [INVITEROOMS-11](invite-rooms.md#inviterooms-11--invite-agent-on-the-room-header-offers-only-agents-that-are-not-already-members-and-starts-closed) |  | Active |  |  |
| [INVITEROOMS-12](invite-rooms.md#inviterooms-12--clicking-a-candidate-invites-it-renames-the-room-and-repaints-all-four-surfaces-with-no-reload) |  | Active |  |  |
| [INVITEROOMS-13](invite-rooms.md#inviterooms-13--invite-agent-says-so-when-every-agent-is-already-a-member-and-hides-the-team-filter-in-that-state) |  | Active |  |  |
| [INVITEROOMS-14](invite-rooms.md#inviterooms-14--the-invite-panels-open-state-and-its-message-do-not-survive-a-room-switch) |  | Active |  |  |
| [INVITEROOMS-15](invite-rooms.md#inviterooms-15--invite-name-in-the-composer-does-the-same-thing-as-the-header-control) |  | Active |  |  |
| [INVITEROOMS-16](invite-rooms.md#inviterooms-16--invite-accepts-a-multi-word-name-with-and-without-the--and-never-truncates-at-the-space) |  | Active |  |  |
| [INVITEROOMS-17](invite-rooms.md#inviterooms-17--invite-of-an-agent-already-in-the-room-reports-success-and-changes-nothing) |  | Active |  |  |
| [INVITEROOMS-18](invite-rooms.md#inviterooms-18--invite-rejects-an-unknown-name-and-rejects-the-human) |  | Active |  |  |
| [INVITEROOMS-19](invite-rooms.md#inviterooms-19--any-other-leading-slash-text-is-refused-as-unknown-command-and-the-typed-text-is-lost) |  | Active |  |  |
| [INVITEROOMS-20](invite-rooms.md#inviterooms-20--a-rooms-name-is-always-its-agent-members-joined-by---in-membership-order-with-the-human-excluded) |  | Active |  |  |
| [INVITEROOMS-21](invite-rooms.md#inviterooms-21--an-agent-belongs-to-at-most-one-two-member-room-after-an-invite-the-next-registration-mints-a-fresh-direct-room) |  | Active |  |  |
| [INVITEROOMS-22](invite-rooms.md#inviterooms-22--two-browser-tabs-stay-in-step-on-a-membership-change-including-an-open-candidate-list) |  | Active |  |  |
| [INVITEROOMS-23](invite-rooms.md#inviterooms-23--adding-a-third-member-flips-a-room-from-answer-everything-to-mention-gated) |  | Active |  |  |
| [INVITEROOMS-24](invite-rooms.md#inviterooms-24--rooms-names-and-membership-survive-a-restart-the-room-budget-does-not) |  | Active |  |  |
| [INVITEROOMS-25](invite-rooms.md#inviterooms-25--the-team-filter-narrows-candidates-and-an-agent-with-no-persona-only-ever-shows-under-all-teams) |  | Active |  |  |
| [INVITEROOMS-26](invite-rooms.md#inviterooms-26--invite-accepts-a-personas-alias-wherever-it-accepts-its-name) |  | Active |  |  |
| [INVITEROOMS-27](invite-rooms.md#inviterooms-27--the-agent-cards-message-action-disappears-once-that-personas-direct-room-has-been-invited-into) |  | Active |  |  |
| [INVITEROOMS-28](invite-rooms.md#inviterooms-28--an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar) | 💰 | Active |  |  |
| [INVITEROOMS-29](invite-rooms.md#inviterooms-29--an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room--label) | 💰 | Active |  |  |
| [INVITEROOMS-30](invite-rooms.md#inviterooms-30--an-agent-can-invite-into-a-room-it-is-not-a-member-of) | 💰 | Active |  |  |
| [INVITEROOMS-31](invite-rooms.md#inviterooms-31--every-agent-created-room-contains-the-human-so-none-is-hidden) | 💰 | Active |  |  |
| [INVITEROOMS-32](invite-rooms.md#inviterooms-32--the-composers-own-status-line-does-not-survive-a-room-switch) |  | Active |  |  |
| [INVITEROOMS-33](invite-rooms.md#inviterooms-33--a-renamed-room-keeps-its-name-across-an-invitation-an-un-renamed-one-still-re-derives) |  | Active |  |  |
| [INVITEROOMS-34](invite-rooms.md#inviterooms-34--archiving-a-room-from-its-row-menu-removes-it-from-the-sidebar-and-lists-it-under-archived-chats) |  | Active |  |  |
| [INVITEROOMS-35](invite-rooms.md#inviterooms-35--unarchiving-returns-the-room-to-the-sidebar-with-its-transcript-intact) |  | Active |  |  |
| [INVITEROOMS-36](invite-rooms.md#inviterooms-36--deleting-a-room-needs-a-confirmation-and-removes-its-transcript-file-from-disk) |  | Active |  |  |
| [INVITEROOMS-37](invite-rooms.md#inviterooms-37--archiving-or-deleting-the-room-you-are-viewing-moves-you-somewhere-valid) |  | Active |  |  |
| [INVITEROOMS-38](invite-rooms.md#inviterooms-38--an-archived-two-member-room-is-not-reused-starting-a-chat-with-that-agent-mints-a-second-one) |  | Active |  |  |
| [INVITEROOMS-39](invite-rooms.md#inviterooms-39--archived-and-deleted-rooms-survive-a-restart) |  | Active |  |  |

## prompts-settings

Settings: the 24 Prompts, editing, per-field reset and Save — [area file](prompts-settings.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PROMPTSSETTINGS-01](prompts-settings.md#promptssettings-01--settings-opens-on-the-profile-tab-with-a-seven-tab-rail) |  | Active |  |  |
| [PROMPTSSETTINGS-02](prompts-settings.md#promptssettings-02--tab-clicks-change-the-url-and-an-unknown-tab-segment-falls-back-to-profile-instead-of-404ing) |  | Active |  |  |
| [PROMPTSSETTINGS-03](prompts-settings.md#promptssettings-03--the-prompts-tab-prints-the-real-absolute-path-of-promptsjson-and-says-the-files-absence-is-expected) |  | Active |  |  |
| [PROMPTSSETTINGS-04](prompts-settings.md#promptssettings-04--every-prompt-is-reachable-in-six-named-groups-in-a-fixed-order) |  | Active |  |  |
| [PROMPTSSETTINGS-05](prompts-settings.md#promptssettings-05--the-next-session-badge-appears-on-exactly-the-prompts-whose-edits-cannot-reach-a-running-agent) |  | Active |  |  |
| [PROMPTSSETTINGS-06](prompts-settings.md#promptssettings-06--placeholder-chips-are-listed-on-exactly-the-prompts-that-take-placeholders-with-full-braces) |  | Active |  |  |
| [PROMPTSSETTINGS-07](prompts-settings.md#promptssettings-07--no-prompts-default-text-contains-the-literal-string-mcp__team__) |  | Active |  |  |
| [PROMPTSSETTINGS-08](prompts-settings.md#promptssettings-08--textarea-height-tracks-the-line-count-and-clamps-at-14-rows) |  | Active |  |  |
| [PROMPTSSETTINGS-09](prompts-settings.md#promptssettings-09--typing-raises-modified-and-unsaved-together-and-enables-save-reset-and-reset-all--while-writing-nothing) |  | Active |  |  |
| [PROMPTSSETTINGS-10](prompts-settings.md#promptssettings-10--modified-and-unsaved-are-independent-flags--walk-all-three-combinations) |  | Active |  |  |
| [PROMPTSSETTINGS-11](prompts-settings.md#promptssettings-11--per-field-reset-stages-the-shipped-default-without-writing-anything-to-disk) |  | Active |  |  |
| [PROMPTSSETTINGS-12](prompts-settings.md#promptssettings-12--promptsjson-does-not-exist-until-the-first-save-and-then-holds-only-the-keys-you-changed) |  | Active |  |  |
| [PROMPTSSETTINGS-13](prompts-settings.md#promptssettings-13--the-saved-file-is-human-readable-indented-with-literal-em-dashes-and-angle-brackets-not-uxxxx-escapes) |  | Active |  |  |
| [PROMPTSSETTINGS-14](prompts-settings.md#promptssettings-14--saving-a-field-back-to-its-default-removes-the-key-rather-than-storing-a-redundant-copy) |  | Active |  |  |
| [PROMPTSSETTINGS-15](prompts-settings.md#promptssettings-15--save-is-the-only-writer--and-a-successful-save-gives-no-confirmation) |  | Active |  |  |
| [PROMPTSSETTINGS-16](prompts-settings.md#promptssettings-16--reset-all-to-defaults-is-disabled-until-something-is-modified-and-guards-itself-with-an-inline-confirm) |  | Active |  |  |
| [PROMPTSSETTINGS-17](prompts-settings.md#promptssettings-17--reset-all-stages-every-default-but-writes-nothing--and-the-reset-is-silently-lost-if-you-navigate-away) |  | Active |  |  |
| [PROMPTSSETTINGS-18](prompts-settings.md#promptssettings-18--reset-all-followed-by-save-leaves-promptsjson-present-and-empty--never-deleted) |  | Active |  |  |
| [PROMPTSSETTINGS-19](prompts-settings.md#promptssettings-19--a-failed-save-shows-a-red-could-not-save-line-under-the-heading-and-keeps-every-pending-edit) |  | Active |  |  |
| [PROMPTSSETTINGS-20](prompts-settings.md#promptssettings-20--validation-reports-and-never-refuses-an-empty-field-raises-an-error-and-still-saves) |  | Active |  |  |
| [PROMPTSSETTINGS-21](prompts-settings.md#promptssettings-21--removing-a-required-placeholder-raises-an-error-naming-that-placeholder--and-an-optional-one-does-not) |  | Active |  |  |
| [PROMPTSSETTINGS-22](prompts-settings.md#promptssettings-22--an-unrecognised-token-raises-a-warning-a-malformed-one-raises-nothing-at-all) |  | Active |  |  |
| [PROMPTSSETTINGS-23](prompts-settings.md#promptssettings-23--validation-runs-live-against-the-pending-value-and-clears-without-saving) |  | Active |  |  |
| [PROMPTSSETTINGS-24](prompts-settings.md#promptssettings-24--prompt-text-is-rendered-as-text-never-as-markup) |  | Active |  |  |
| [PROMPTSSETTINGS-25](prompts-settings.md#promptssettings-25--typing-in-a-long-prompt-field-stays-responsive-and-loses-no-characters) |  | Active |  |  |
| [PROMPTSSETTINGS-26](prompts-settings.md#promptssettings-26--uncommitted-edits-survive-a-tab-switch-and-a-theme-change-but-are-silently-discarded-by-reload-or-leaving-the-page) |  | Active |  |  |
| [PROMPTSSETTINGS-27](prompts-settings.md#promptssettings-27--reset-all-to-defaults-is-also-rendered-on-the-appearance-tab-where-it-acts-on-prompts) |  | Active |  |  |
| [PROMPTSSETTINGS-28](prompts-settings.md#promptssettings-28--a-save-in-one-browser-tab-repaints-settings-open-in-another-without-eating-that-tabs-typing) |  | Active |  |  |
| [PROMPTSSETTINGS-29](prompts-settings.md#promptssettings-29--a-hand-edit-to-promptsjson-reaches-the-open-page-within-about-a-second-with-no-restart-and-no-refresh) |  | Active |  |  |
| [PROMPTSSETTINGS-30](prompts-settings.md#promptssettings-30--deleting-promptsjson-while-the-app-runs-reverts-every-field-to-its-shipped-default-live) |  | Active |  |  |
| [PROMPTSSETTINGS-31](prompts-settings.md#promptssettings-31--silent-a-malformed-promptsjson-edited-while-running-changes-nothing-on-screen-and-says-nothing--check-the-log) |  | Active |  |  |
| [PROMPTSSETTINGS-32](prompts-settings.md#promptssettings-32--silent-a-malformed-promptsjson-at-startup-falls-back-to-defaults-wholesale-with-no-ui-clue-and-the-file-left-intact) |  | Active |  |  |
| [PROMPTSSETTINGS-33](prompts-settings.md#promptssettings-33--an-unknown-key-in-promptsjson-is-kept-forever-ignored-for-resolution-and-logged-whenever-the-file-is-read-or-saved) |  | Active |  |  |
| [PROMPTSSETTINGS-34](prompts-settings.md#promptssettings-34--a-pending-browser-edit-beats-a-concurrent-hand-edit-to-the-same-key-and-save-merges-rather-than-overwrites) |  | Active |  |  |
| [PROMPTSSETTINGS-35](prompts-settings.md#promptssettings-35--a-stored-prompt-equal-to-its-default-apart-from-crlf-is-ignored-no-badge-and-dropped-by-the-next-save) |  | Active |  |  |
| [PROMPTSSETTINGS-36](prompts-settings.md#promptssettings-36--loading-settings-never-starts-an-adapter-node-process) |  | Active |  |  |
| [PROMPTSSETTINGS-37](prompts-settings.md#promptssettings-37--saving-a-prompt-must-not-restart-any-agents-session) |  | Active |  |  |
| [PROMPTSSETTINGS-38](prompts-settings.md#promptssettings-38--costs-money-a-live-prompt-edit-reaches-the-very-next-turn-with-no-restart) | 💰 | Active |  |  |
| [PROMPTSSETTINGS-39](prompts-settings.md#promptssettings-39--costs-money-a-next-session-prompt-edit-is-silently-inert-on-a-running-agent-until-it-restarts) | 💰 | Active |  |  |
| [PROMPTSSETTINGS-40](prompts-settings.md#promptssettings-40--costs-money-get_help-re-renders-on-every-call-so-its-get-help-prompts-land-on-the-next-call--while-a-tool-description-does-not) | 💰 | Active |  |  |
| [PROMPTSSETTINGS-41](prompts-settings.md#promptssettings-41--the-library-tab-opens-its-own-pane-and-does-not-fall-back-to-prompts) |  | Active |  |  |

## appearance-theme

Appearance tab, Themes, Tokens and overrides — [area file](appearance-theme.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [APPEARANCETHEME-01](appearance-theme.md#appearancetheme-01--the-appearance-tab-is-on-the-settings-tab-bar-and-is-reachable-by-its-own-route) |  | Active |  |  |
| [APPEARANCETHEME-02](appearance-theme.md#appearancetheme-02--the-appearance-tab-shows-its-own-prose-and-the-real-absolute-selection-file-path-and-none-of-the-prompts-tabs-prose) |  | Active |  |  |
| [APPEARANCETHEME-03](appearance-theme.md#appearancetheme-03--the-theme-picker-offers-the-whole-built-in-catalog-grouped-and-is-the-tabs-only-control) |  | Active |  |  |
| [APPEARANCETHEME-04](appearance-theme.md#appearancetheme-04--choosing-a-theme-stores-its-id-and-applies-immediately-with-no-page-reload) |  | Active |  |  |
| [APPEARANCETHEME-06](appearance-theme.md#appearancetheme-06--the-saved-theme-is-shown-as-selected-in-the-picker-after-a-full-load) |  | Active |  |  |
| [APPEARANCETHEME-07](appearance-theme.md#appearancetheme-07--a-dark-theme-shows-no-flash-of-a-light-palette-on-a-hard-reload-ever) |  | Active |  |  |
| [APPEARANCETHEME-09](appearance-theme.md#appearancetheme-09--the-choice-is-per-installation-not-per-browser) |  | Active |  |  |
| [APPEARANCETHEME-14](appearance-theme.md#appearancetheme-14--an-unknown-theme-id-is-a-warning-logged-on-every-rebuild-and-save-never-shown-on-the-tab-and-the-file-keeps-saying-it) |  | Active |  |  |
| [APPEARANCETHEME-15](appearance-theme.md#appearancetheme-15--malformed-json-keeps-the-last-good-theme-while-the-app-runs-falls-back-wholesale-at-the-next-start-is-logged-and-the-file-is-left-untouched) |  | Active |  |  |
| [APPEARANCETHEME-16](appearance-theme.md#appearancetheme-16--saving-a-choice-while-the-file-is-malformed-replaces-its-contents-with-valid-json) |  | Active |  |  |
| [APPEARANCETHEME-17](appearance-theme.md#appearancetheme-17--save-re-reads-the-file-under-its-write-lock-so-an-unrelated-hand-added-key-survives--and-so-does-the-retired-dark-key) |  | Active |  |  |
| [APPEARANCETHEME-18](appearance-theme.md#appearancetheme-18--a-hand-edit-updates-the-open-tab-live-and-now-the-pages-colours-change-too--no-full-load-required) |  | Active |  |  |
| [APPEARANCETHEME-20](appearance-theme.md#appearancetheme-20--two-browser-windows-stay-in-step-colours-included--the-change-propagates-through-the-file-to-every-open-circuit) |  | Active |  |  |
| [APPEARANCETHEME-21](appearance-theme.md#appearancetheme-21--re-selecting-the-value-that-is-already-selected-is-harmless-and-produces-no-reload) |  | Active |  |  |
| [APPEARANCETHEME-22](appearance-theme.md#appearancetheme-22--dark-mode-walked-across-every-page-and-every-state---the-acceptance-test-for-the-whole-item) |  | Active |  |  |
| [APPEARANCETHEME-23](appearance-theme.md#appearancetheme-23--hover-selection-and-focus-surfaces-all-switch-and-the-four-old-hover-colours-are-still-one) |  | Active |  |  |
| [APPEARANCETHEME-24](appearance-theme.md#appearancetheme-24--the-reconnect-modal-shows-one-themed-state-paragraph-over-a-dimmed-backdrop-in-both-light-and-dark) |  | Active |  |  |
| [APPEARANCETHEME-26](appearance-theme.md#appearancetheme-26--the-credit-line-names-this-applications-own-themes-and-attributes-nobody) |  | Active |  | Added 2026-09-16 with the VS Code theme import (ADR-0016). |
| [APPEARANCETHEME-27](appearance-theme.md#appearancetheme-27--every-theme-paints-its-own-palette-across-the-whole-application) |  | Active |  | Added 2026-09-16 with the VS Code theme import (ADR-0016). The acceptance walk for the imported catalog. |
| [APPEARANCETHEME-28](appearance-theme.md#appearancetheme-28--an-accent-customization-is-stored-per-theme-clamped-for-contrast-and-removed-by-reset) |  | Active |  |  |
| [APPEARANCETHEME-29](appearance-theme.md#appearancetheme-29--there-is-no-library-pane-setting-and-a-leftover-librarypaneside-key-is-ignored-without-a-warning-and-kept-in-the-file) |  | Active |  |  |
| [APPEARANCETHEME-30](appearance-theme.md#appearancetheme-30--the-top-bar-button-switches-between-the-chosen-light-and-dark-themes-and-is-the-last-control-in-the-bar) |  | Active |  | Added 2026-10-12 with ADR-0054. |
| [APPEARANCETHEME-31](appearance-theme.md#appearancetheme-31--the-appearance-tab-has-a-light-picker-and-a-dark-picker-each-holding-only-its-own-mode) |  | Active |  | Added 2026-10-12 with ADR-0054. |
| [APPEARANCETHEME-32](appearance-theme.md#appearancetheme-32--picking-a-theme-changes-the-screen-only-when-it-is-of-the-mode-already-shown) |  | Active |  | Added 2026-10-12 with ADR-0054. |
| [APPEARANCETHEME-33](appearance-theme.md#appearancetheme-33--old-files-hand-edits-and-bad-values-the-pair-resolves-safely) |  | Active |  | Added 2026-10-12 with ADR-0054. |
| [APPEARANCETHEME-34](appearance-theme.md#appearancetheme-34--a-stored-huddle-or-custom-accent-id-migrates-to-lumen-with-its-accent-kept) |  | Active |  | Added 2026-10-17 with ADR-0058. |
| [APPEARANCETHEME-35](appearance-theme.md#appearancetheme-35--the-theme-lab-edits-a-theme-live-in-development-and-is-absent-elsewhere) |  | Active |  | Added 2026-10-17 with ADR-0058. |

## pipe-external

The named pipe: external agents and the wire — [area file](pipe-external.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PIPEEXTERNAL-03](pipe-external.md#pipeexternal-03--new-chat-builds-a-group-room-named-after-its-agents-and-that-room-is-mention-gated) |  | Active |  |  |
| [PIPEEXTERNAL-04](pipe-external.md#pipeexternal-04--a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh) |  | Active |  | rewritten 2026-10. Earlier Pass. |
| [PIPEEXTERNAL-05](pipe-external.md#pipeexternal-05--the-welcome-envelope-shape-fields-and-version-3) |  | Active |  |  |
| [PIPEEXTERNAL-07](pipe-external.md#pipeexternal-07--an-agent-never-receives-its-own-message-and-nothing-loops) |  | Active |  |  |
| [PIPEEXTERNAL-08](pipe-external.md#pipeexternal-08--hello-property-order-does-not-matter-so-connecting-is-not-intermittently-refused) |  | Active |  |  |
| [PIPEEXTERNAL-09](pipe-external.md#pipeexternal-09--reconnecting-under-the-same-name-keeps-the-agent-id-and-creates-no-second-room) |  | Active |  |  |
| [PIPEEXTERNAL-10](pipe-external.md#pipeexternal-10--reconnecting-with-different-letter-case-is-the-same-agent-and-the-stored-name-does-not-change) |  | Active |  |  |
| [PIPEEXTERNAL-11](pipe-external.md#pipeexternal-11--disconnecting-an-external-agent-changes-nothing-visible-in-the-room--the-silent-case) |  | Active |  |  |
| [PIPEEXTERNAL-12](pipe-external.md#pipeexternal-12--where-a-disconnect-is-visible-the-agent-dot-in-new-chat-and-invite-agent) |  | Active |  |  |
| [PIPEEXTERNAL-13](pipe-external.md#pipeexternal-13--an-offline-agent-receives-nothing-and-nothing-is-replayed-on-reconnect) |  | Active |  |  |
| [PIPEEXTERNAL-14](pipe-external.md#pipeexternal-14--an-invalid-name-is-refused-with-invalidname-the-connection-closes-and-no-room-appears) |  | Active |  |  |
| [PIPEEXTERNAL-15](pipe-external.md#pipeexternal-15--the-humans-name-is-reserved-on-the-pipe) |  | Active |  |  |
| [PIPEEXTERNAL-16](pipe-external.md#pipeexternal-16--a-name-with-a-single-interior-space-is-accepted-and-works-end-to-end) |  | Active |  |  |
| [PIPEEXTERNAL-17](pipe-external.md#pipeexternal-17--dialling-the-wrong-pipe-name-fails-outside-the-browser-and-the-pipe-name-is-configuration) |  | Active |  |  |
| [PIPEEXTERNAL-18](pipe-external.md#pipeexternal-18--members-is-filled-on-every-messageposted-and-grows-when-the-room-does) |  | Active |  |  |
| [PIPEEXTERNAL-19](pipe-external.md#pipeexternal-19--an-external-agent-joins-a-room-live-without-reconnecting) |  | Active |  |  |
| [PIPEEXTERNAL-20](pipe-external.md#pipeexternal-20--the-welcome-lists-every-room-the-agent-is-in-and-a-reconnect-after-an-invite-mints-a-second-room-of-the-same-name) |  | Active |  |  |
| [PIPEEXTERNAL-21](pipe-external.md#pipeexternal-21--drafts-are-never-delivered-to-another-agent) |  | Active |  |  |
| [PIPEEXTERNAL-22](pipe-external.md#pipeexternal-22--the-mentioned-flag-is-per-recipient-and-unmentioned-members-still-receive-the-envelope) |  | Active |  |  |
| [PIPEEXTERNAL-23](pipe-external.md#pipeexternal-23--loop-safety-two-bots-in-one-room-reply-once-each-and-go-quiet) |  | Active |  |  |
| [PIPEEXTERNAL-24](pipe-external.md#pipeexternal-24--a-second-connection-under-the-same-name-displaces-the-first-which-is-told-by-having-its-pipe-closed) |  | Active |  |  |
| [PIPEEXTERNAL-25](pipe-external.md#pipeexternal-25--three-clients-connect-at-once-and-the-accept-loop-does-not-jam) |  | Active |  |  |
| [PIPEEXTERNAL-26](pipe-external.md#pipeexternal-26--the-budget-labels-ride-on-every-messageposted-and-a-refused-post-comes-back-as-budgetexhausted) |  | Active |  |  |
| [PIPEEXTERNAL-27](pipe-external.md#pipeexternal-27--continue-re-delivers-the-same-message-to-the-agents-without-showing-it-twice) |  | Active |  |  |
| [PIPEEXTERNAL-28](pipe-external.md#pipeexternal-28--a-wrong-protocol-version-in-hello-is-refused-and-the-browser-never-learns) |  | Active |  |  |
| [PIPEEXTERNAL-29](pipe-external.md#pipeexternal-29--a-first-message-that-is-not-hello-and-a-silent-client-are-both-refused-as-expectedhello) |  | Active |  |  |
| [PIPEEXTERNAL-30](pipe-external.md#pipeexternal-30--after-the-handshake-a-bad-line-or-a-bad-request-is-reported-and-the-connection-stays-open) |  | Active |  |  |
| [PIPEEXTERNAL-31](pipe-external.md#pipeexternal-31--an-agent-cannot-write-a-draft-into-a-room-it-is-not-a-member-of-even-by-reusing-a-message-id) |  | Active |  |  |
| [PIPEEXTERNAL-32](pipe-external.md#pipeexternal-32--an-agent-disconnecting-mid-draft-clears-its-draft-rather-than-freezing-it-on-screen) |  | Active |  |  |
| [PIPEEXTERNAL-34](pipe-external.md#pipeexternal-34--stopping-the-app-closes-every-agent-connection-cleanly-and-the-browsers-reconnect-dialog-is-readable) |  | Active |  |  |
| [PIPEEXTERNAL-35](pipe-external.md#pipeexternal-35--an-empty-install-shows-its-two-empty-state-strings-and-the-main-pane-does-not-auto-open-the-first-new-room) |  | Active |  |  |
| [PIPEEXTERNAL-36](pipe-external.md#pipeexternal-36--two-app-instances-on-one-pipe-name-split-clients-unpredictably) |  | Active |  |  |
| [PIPEEXTERNAL-37](pipe-external.md#pipeexternal-37--pressing-stop-on-an-external-agents-draft-sends-it-a-stopturn-for-that-room) |  | Active |  |  |

## app-tools

App Tools a real model calls (progressive discovery) — [area file](app-tools.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [APPTOOLS-01](app-tools.md#apptools-01--the-prompt-groups-are-readable-in-the-browser-and-no-default-contains-the-literal-mcp__team__) |  | Active |  |  |
| [APPTOOLS-02](app-tools.md#apptools-02--promptvalidator-reports-a-missing-placeholder-and-a-typo-and-never-blocks-the-save) |  | Active |  |  |
| [APPTOOLS-03](app-tools.md#apptools-03--with-teamacpenabledfalse-no-app-tool-server-exists-at-all-and-the-chat-surface-still-works) |  | Active |  |  |
| [APPTOOLS-04](app-tools.md#apptools-04--the-tool-server-binds-and-completes-the-mcp-handshake-before-any-model-call--the-wiring-pre-flight) | 💰 | Active |  |  |
| [APPTOOLS-05](app-tools.md#apptools-05--an-agent-reads-its-own-rooms-id-off-the-room--label) | 💰 | Active |  |  |
| [APPTOOLS-06](app-tools.md#apptools-06--get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live) | 💰 | Active |  |  |
| [APPTOOLS-07](app-tools.md#apptools-07--asked-what-tools-it-has-an-agent-calls-get_help-and-reports-the-real-catalog-with-descriptions) | 💰 | Active |  |  |
| [APPTOOLS-08](app-tools.md#apptools-08--tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight) | 💰 | Active |  |  |
| [APPTOOLS-09](app-tools.md#apptools-09--tool-activity-is-not-written-to-the-transcript-and-does-not-survive-the-turn) |  | Active |  |  |
| [APPTOOLS-10](app-tools.md#apptools-10--list_agents-reports-real-names-real-online-state-and-real-job-descriptions) | 💰 | Active |  |  |
| [APPTOOLS-11](app-tools.md#apptools-11--an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent) | 💰 | Active |  |  |
| [APPTOOLS-12](app-tools.md#apptools-12--invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live) | 💰 | Active |  |  |
| [APPTOOLS-13](app-tools.md#apptools-13--invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member) | 💰 | Active |  |  |
| [APPTOOLS-14](app-tools.md#apptools-14--invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist) | 💰 | Active |  |  |
| [APPTOOLS-15](app-tools.md#apptools-15--an-agents-reply-is-delivered-once--it-must-not-also-post-the-same-text-with-post_message) | 💰 | Active |  |  |
| [APPTOOLS-16](app-tools.md#apptools-16--create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents) | 💰 | Active |  |  |
| [APPTOOLS-17](app-tools.md#apptools-17--post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post) | 💰 | Active |  |  |
| [APPTOOLS-18](app-tools.md#apptools-18--tools-are-the-source-of-truth--the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase) | 💰 | Active |  |  |
| [APPTOOLS-19](app-tools.md#apptools-19--a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute) | 💰 | Active |  |  |
| [APPTOOLS-20](app-tools.md#apptools-20--a-turn-declined-for-budget-never-reaches-the-model-at-all--the-decline-path-is-not-the-refusal-path) |  | Active |  |  |
| [APPTOOLS-21](app-tools.md#apptools-21--continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload) |  | Active |  |  |
| [APPTOOLS-22](app-tools.md#apptools-22--editing-a-prompt-changes-model-facing-text-without-restarting-the-session-and-next-session-prompts-wait-for-a-restart) | 💰 | Active |  |  |
| [APPTOOLS-23](app-tools.md#apptools-23--follow_room-wakes-an-agent-that-was-not-mentioned-and-a-non-follower-in-the-same-room-is-the-control) | 💰 | Active |  |  |
| [APPTOOLS-24](app-tools.md#apptools-24--unfollow_room-restores-the-ordinary-mention-only-rule-and-the-room-confirms-the-decline-was-not-a-silent-drop) | 💰 | Active |  |  |
| [APPTOOLS-25](app-tools.md#apptools-25--following-does-not-buy-a-turn-past-the-budget) | 💰 | Active |  |  |
| [APPTOOLS-26](app-tools.md#apptools-26--create_room-with-seed-posts-the-opening-message-as-part-of-creation) | 💰 | Active |  |  |
| [APPTOOLS-27](app-tools.md#apptools-27--get_help-carries-a-tasks-section-while-the-task-tools-are-offered) | 💰 | Active |  |  |
| [APPTOOLS-28](app-tools.md#apptools-28--a-prefix-less-adapter-is-told-tool-names-bare-adr-0014) |  | Active |  |  |
| [APPTOOLS-29](app-tools.md#apptools-29--search-finds-only-an-agents-own-rooms-until-it-asks-for-all-adr-0066) | 💰 | Pass |  | 2026-10-28, uncommitted `feat/agent-search-tool`. Default search sent `{"query":"zebra-ledger-7"}`, header `yours and your Teams' only`, no hit for Bo's Room, no log line. After "Search every Room" it sent `scope:"all"`, found Bo's Room, and the `searched every Room` line appeared once. Haiku |
| [APPTOOLS-30](app-tools.md#apptools-30--the-default-search-also-reads-a-room-scoped-to-the-agents-own-team-that-it-is-not-a-member-of-adr-0066) | 💰 | Pass |  | 2026-10-28, uncommitted `feat/agent-search-tool`. Fresh one-to-one, Ada in Team `Design`, Team chat of Bo and the Human without her. Input `{"query":"quokka-ledger-9"}` (no scope), header `yours and your Teams' only`, Team chat listed, no log line. Haiku |
| [APPTOOLS-31](app-tools.md#apptools-31--a-task-id-and-a-library-path-from-search-are-usable-by-the-next-tool-call) | 💰 | Pass |  | 2026-10-28, uncommitted `feat/agent-search-tool`. `filter:"tasks"` returned `DESI-0001 \| To Do \| Bo \| Design \| ...`, `filter:"library"` returned `teams:Design/launch-plan.md` with snippet, `get_task` was called with `DESI-0001`. No Rooms header. Haiku |
| [APPTOOLS-32](app-tools.md#apptools-32--get_help-lists-the-search-tool-while-the-system-prompt-names-none) | 💰 | Pass |  | 2026-10-28, uncommitted `feat/agent-search-tool`. The decoded `get_help` TOOLS section holds `mcp__team__search` with the Rooms-default sentence and the `scope 'all'` logging sentence; Ada quoted it. Haiku |

## persona-lifecycle

Persona lifecycle: supervisor, work dirs, health and restarts — [area file](persona-lifecycle.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PERSONALIFECYCLE-01](persona-lifecycle.md#personalifecycle-01--with-acp-disabled-every-agent-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert) |  | Active |  | rewritten 2026-10. Earlier Pass: Re-run 2026-09-14: `Nova` created Haiku/low, card and tile read Offline with no tooltip and no reason line, no `Nova` Room, no alert strip, adapters 0 before and after, `work` empty. Corrected step 17 (`hello there @echo`) drew the `**echo:**` reply. |
| [PERSONALIFECYCLE-02](persona-lifecycle.md#personalifecycle-02--a-persona-name-with-a-leading-trailing-or-doubled-space-is-refused-on-the-card-and-never-written-to-disk) |  | Active |  |  |
| [PERSONALIFECYCLE-03](persona-lifecycle.md#personalifecycle-03--browsing-agents-spawns-no-adapter-opening-a-card-spawns-exactly-one-and-reopening-spawns-none) |  | Active |  |  |
| [PERSONALIFECYCLE-04](persona-lifecycle.md#personalifecycle-04--a-missing-adapter-degrades-the-model-and-effort-pickers-with-a-plain-language-hint-and-restoring-it-works-with-no-app-restart) |  | Active |  |  |
| [PERSONALIFECYCLE-05](persona-lifecycle.md#personalifecycle-05--restart-with-teamacpenabledfalse-starts-nothing-and-says-why-on-the-card) |  | Active |  |  |
| [PERSONALIFECYCLE-06](persona-lifecycle.md#personalifecycle-06--a-persona-added-while-the-app-is-running-comes-online-and-gets-a-room-named-after-it-with-no-page-refresh) |  | Active |  |  |
| [PERSONALIFECYCLE-07](persona-lifecycle.md#personalifecycle-07--a-definition-that-is-renamed-or-that-sits-outside-its-agents-folder-is-rejected-with-a-reason-and-restoring-it-brings-the-agent-back) |  | Active |  |  |
| [PERSONALIFECYCLE-08](persona-lifecycle.md#personalifecycle-08--a-name-containing-spaces-works-for-the-monogram-the-room-and-the-work-dir) |  | Active |  |  |
| [PERSONALIFECYCLE-09](persona-lifecycle.md#personalifecycle-09--a-missing-adapter-reports-the-same-actionable-reason-on-the-tile-the-card-and-the-room-strip-and-harms-no-other-agent) |  | Active |  |  |
| [PERSONALIFECYCLE-10](persona-lifecycle.md#personalifecycle-10--restart-on-the-agent-card-recovers-a-failed-persona-with-no-app-restart-and-one-click-starts-one-adapter) |  | Active |  |  |
| [PERSONALIFECYCLE-11](persona-lifecycle.md#personalifecycle-11--restart-is-offered-in-every-state-except-starting-and-is-accompanied-by-the-memory-loss-warning) |  | Active |  |  |
| [PERSONALIFECYCLE-12](persona-lifecycle.md#personalifecycle-12--killing-the-adapter-process-flips-the-badge-to-offline-with-a-loop-reason-even-though-the-pipe-stays-open) |  | Active |  |  |
| [PERSONALIFECYCLE-13](persona-lifecycle.md#personalifecycle-13--one-persona-failing-leaves-every-other-persona-online-and-messageable) |  | Active |  |  |
| [PERSONALIFECYCLE-14](persona-lifecycle.md#personalifecycle-14--an-external-edit-to-an-agents-definition-restarts-it-once-per-save-and-a-non-md-file-restarts-nobody) |  | Active |  |  |
| [PERSONALIFECYCLE-15](persona-lifecycle.md#personalifecycle-15--a-write-under-an-agents-work-folder-restarts-nothing) |  | Active |  |  |
| [PERSONALIFECYCLE-16](persona-lifecycle.md#personalifecycle-16--a-persona-file-that-becomes-malformed-is-named-in-files-that-didnt-load-and-an-alias-collision-names-both-files) |  | Active |  |  |
| [PERSONALIFECYCLE-17](persona-lifecycle.md#personalifecycle-17--a-stored-model-the-adapter-no-longer-advertises-is-degraded-with-a-reason-never-a-failed-start) |  | Active |  |  |
| [PERSONALIFECYCLE-18](persona-lifecycle.md#personalifecycle-18--editing-a-prompt-at-settings-does-not-restart-a-running-agent) |  | Active |  |  |
| [PERSONALIFECYCLE-19](persona-lifecycle.md#personalifecycle-19--removing-a-persona-takes-it-offline-but-leaves-its-agent-room-and-transcript-and-raises-no-alert) |  | Active |  |  |
| [PERSONALIFECYCLE-20](persona-lifecycle.md#personalifecycle-20--re-creating-a-removed-persona-under-the-same-name-does-not-resurrect-its-old-model-or-effort) |  | Active |  |  |
| [PERSONALIFECYCLE-21](persona-lifecycle.md#personalifecycle-21--editing-the-frontmatter-name-renames-the-agent-in-place-its-folder-file-room-model-and-effort-all-follow) |  | Active |  |  |
| [PERSONALIFECYCLE-22](persona-lifecycle.md#personalifecycle-22--health-and-presence-repaint-every-open-surface-live-in-every-browser-tab) |  | Active |  |  |
| [PERSONALIFECYCLE-23](persona-lifecycle.md#personalifecycle-23--an-app-restart-brings-every-persona-back-orphans-no-process-and-deliberately-loses-only-in-memory-state) |  | Active |  |  |
| [PERSONALIFECYCLE-24](persona-lifecycle.md#personalifecycle-24--five-personas-the-chief-of-staff-and-four-more-cost-five-adapter-processes-all-reach-online-and-all-exit-on-shutdown) |  | Active |  |  |
| [PERSONALIFECYCLE-25](persona-lifecycle.md#personalifecycle-25--an-adapter-that-needs-authentication-reports-that-as-its-own-distinct-reason) |  | Active |  |  |
| [PERSONALIFECYCLE-26](persona-lifecycle.md#personalifecycle-26--money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-agent) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-27](persona-lifecycle.md#personalifecycle-27--money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-28](persona-lifecycle.md#personalifecycle-28--money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-agent-remembers) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-29](persona-lifecycle.md#personalifecycle-29--money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-30](persona-lifecycle.md#personalifecycle-30--money-what-an-agent-was-told-in-one-room-does-not-leak-into-another-one-process-serves-both-and-replies-land-in-the-right-room) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-31](persona-lifecycle.md#personalifecycle-31--money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it) | 💰 | Active |  |  |
| [PERSONALIFECYCLE-32](persona-lifecycle.md#personalifecycle-32--money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange) | 💰 | Active |  |  |

## tasks

Tasks: creating, moving, viewing and referencing a unit of work — [area file](tasks.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TASKS-01](tasks.md#tasks-01--creating-a-task-in-the-ui-writes-the-file-at-the-right-path-with-the-canonical-frontmatter) |  | Active |  |  |
| [TASKS-02](tasks.md#tasks-02--a-hand-edit-in-a-text-editor-appears-within-a-second-with-an-edited-outside-huddle-entry) |  | Active |  |  |
| [TASKS-03](tasks.md#tasks-03--board-drag-ghost-buckets-the-duplicate-picker-and-cancelling-snaps-the-card-back) |  | Active |  |  |
| [TASKS-04](tasks.md#tasks-04--a-view-saves-every-change-as-it-is-made-and-the-fields-dialog-reorders-fields) |  | Active |  |  |
| [TASKS-05](tasks.md#tasks-05--a-broken-viewsjson-shows-the-error-and-the-file-is-untouched) |  | Active |  |  |
| [TASKS-06](tasks.md#tasks-06--the-last-view-reopens-after-a-browser-restart-and-a-private-window-falls-back-to-all-tasks) |  | Active |  |  |
| [TASKS-07](tasks.md#tasks-07--a-conflict-shows-both-versions-and-merging-changes-to-different-fields-is-silent) |  | Active |  |  |
| [TASKS-08](tasks.md#tasks-08--renaming-a-persona-rewrites-assignee-in-every-task-file-and-view-filter-with-no-wake-up) |  | Active |  |  |
| [TASKS-09](tasks.md#tasks-09--keyboard-only-move-to-the-fields-dialog-and-the-task-panel-with-no-dragging) |  | Active |  |  |
| [TASKS-10](tasks.md#tasks-10--assigning-to-a-live-claude-persona-wakes-it-in-the-right-room-and-it-calls-update_task-to-set-in-progress) | 💰 | Active |  |  |
| [TASKS-11](tasks.md#tasks-11--two-personas-reassigning-to-each-other-stop-at-the-wake-budget-and-allow-10-more-resumes-them) | 💰 | Active |  |  |
| [TASKS-12](tasks.md#tasks-12--an-agent-that-creates-a-task-with-originroomid-wakes-the-assignee-in-that-room) | 💰 | Active |  |  |
| [TASKS-13](tasks.md#tasks-13--the-copy-button-works-on-localhost-and-from-another-machine-and-the-pasted-id-shows-as-a-link) |  | Active |  |  |
| [TASKS-14](tasks.md#tasks-14--the--picker-keyboard-only) |  | Active |  |  |
| [TASKS-15](tasks.md#tasks-15--an-agent-sent-please-look-at-plat-0042-calls-get_task-with-that-id-without-being-told-the-tool-name) | 💰 | Active |  |  |
| [TASKS-16](tasks.md#tasks-16--typing-a-new-sprint-in-the-task-panel-writes-sprintsmd-on-save-and-moving-the-last-task-out-removes-its-line) |  | Active |  |  |
| [TASKS-17](tasks.md#tasks-17--a-hand-edit-of-sprintsmd-shows-within-a-second-and-changes-no-task-file-deleting-a-task-file-removes-no-sprint) |  | Active |  |  |
| [TASKS-18](tasks.md#tasks-18--the-toolbar-sprint-filter-a-saved-view-with-current-sprint-and-a-board-grouped-by-sprint) |  | Active |  |  |
| [TASKS-19](tasks.md#tasks-19--an-agent-given-a-sprint-name-sets-it-with-update_task-a-typo-is-refused-with-the-known-list-and-the-assignee-is-woken) | 💰 | Active |  |  |
| [TASKS-20](tasks.md#tasks-20--the-task-opens-in-the-right-sidebar-edit-opens-the-edit-page-in-its-own-tab-and-a-new-task-opens-at-the-top) |  | Active |  |  |
| [TASKS-21](tasks.md#tasks-21--a-hand-off-held-by-a-spent-wake-budget-survives-a-restart-and-wake-starts-the-agent-once) | | Pass |  | run 2026-11-06 at `eb1b8c78`, mock lane, see the Run log |
| [TASKS-22](tasks.md#tasks-22--a-request-held-by-a-spent-room-budget-survives-a-restart-and-wake-starts-the-agent-once) | | Pass |  | run by an agent 2026-11-06, mock lane, see the Run log |

## adapters

| Test id | 💰 | Status | Run | Notes |
| --- | --- | --- | --- | --- |
| [ADAPTERS-01](adapters.md#adapters-01--two-personas-in-one-room-on-different-adapters-and-neither-room-nor-gate-can-tell) |  | Active |  | Needs two Adapter Profiles configured — see the area's setup |
| [ADAPTERS-02](adapters.md#adapters-02--a-mock-adapters-reply-lands-whole-in-the-room-and-the-transcript-and-the-other-agent-is-unaffected) |  | Active |  | A persisted Message shorter than what rendered is the diagnosed race in [Known limits](../known-limits.md), not a new defect |
| [ADAPTERS-03](adapters.md#adapters-03--changing-an-agents-adapter-resets-model-and-effort-says-so-and-restarts-the-session) |  | Active |  |  |
| [ADAPTERS-04](adapters.md#adapters-04--does-a-real-local-model-call-get_help-unprompted-) | 💰 | Active |  | Unblocked 2026-09-18 — D-1 is fixed in `AgencyDotNet.Acp` 0.1.197, published on nuget.org as `0.1.198-ga453511f0e`. **Check `agentInfo.version` first**: on 0.1.195 or earlier this test is meaningless, not merely failing. Needs a real local Adapter and a reachable inference endpoint. "Calls no tool at all" is INCONCLUSIVE and expected for a small model — record the model name |

## skills

Skills: the Chief of Staff, team-building, and the Family Health Advisor — [area file](skills.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [SKILLS-01](skills.md#skills-01--the-chief-of-staff-reads-team-building-unprompted-when-asked-for-a-team) |  | Active |  |  |
| [SKILLS-02](skills.md#skills-02--approve-creates-the-agents-wakes-the-chief-of-staff-and-it-opens-a-seeded-room) |  | Active |  |  |
| [SKILLS-03](skills.md#skills-03--decline-and-the-chief-of-staff-revises-and-proposes-again) |  | Active |  |  |
| [SKILLS-04](skills.md#skills-04--over-the-limit-approve-creates-nothing-and-the-message-says-why) |  | Active |  |  |
| [SKILLS-05](skills.md#skills-05--the-greeting-on-a--clean-install-and-no-second-greeting-on-restart) |  | Active |  |  |
| [SKILLS-06](skills.md#skills-06--the-family-health-advisors-safety-behaviour) |  | Active |  |  |

## skill-packs

Skill Packs: SKILLS.md, the AI tool, updates, the move and shared folders — [area file](skill-packs.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [SKILLPACKS-01](skill-packs.md#skillpacks-01--a-real-ai-tool-adds-a-pack-and-huddle-shows-it-without-a-reload) | 💰 | Active |  |  |
| [SKILLPACKS-02](skill-packs.md#skillpacks-02--choosing-a-pack-copies-its-skills-then-changes-the-agent) |  | Active |  |  |
| [SKILLPACKS-03](skill-packs.md#skillpacks-03--a-changed-skill-shows-update-available-and-update-copies-then-changes-membership) |  | Active |  |  |
| [SKILLPACKS-04](skill-packs.md#skillpacks-04--the-move-turns-a-copy-of-a-real-data-folder-into-skillsmd-and-changes-no-agent) |  | Active |  | Move half run by an agent 2026-10-18 on a 0.6 MB copy with no imported Skills: 6 catalogs, 263 Skills, no problems, no Agent changed. Not yet run by a person |
| [SKILLPACKS-05](skill-packs.md#skillpacks-05--two-huddles-share-one-skill-sources-folder-each-with-its-own-paths) |  | Active |  |  |
| [SKILLPACKS-06](skill-packs.md#skillpacks-06--a-pointer-on-an-unplugged-drive-shows-not-downloaded-and-agents-keep-working) |  | Active |  |  |
| [SKILLPACKS-07](skill-packs.md#skillpacks-07--a-withdrawn-source-blocks-a-new-hire-and-its-holders-keep-working) |  | Active |  |  |


## work-mode

Work Mode: choosing how much an Agent may do before it asks — [area file](work-mode.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [WORKMODE-01](work-mode.md#workmode-01--the-picker-lists-what-the-adapter-advertises-minus-plan-auto-and-bypass-permissions) |  | Active |  |  |
| [WORKMODE-02](work-mode.md#workmode-02--changing-the-adapter-clears-the-work-mode-and-says-so) |  | Active |  |  |
| [WORKMODE-03](work-mode.md#workmode-03--a-hand-written-row-for-a-hidden-mode-is-dropped-with-a-warning) |  | Active |  |  |
| [WORKMODE-04](work-mode.md#workmode-04--hiding-nothing-is-a-list-of-one-empty-string) |  | Active |  |  |
| [WORKMODE-05](work-mode.md#workmode-05--an-adapter-that-advertises-no-mode-shows-no-picker) |  | Active |  |  |
| [WORKMODE-06](work-mode.md#workmode-06--accept-edits-no-permission-request-for-an-edit) | 💰 | Pass |  | 2026-10-01, in the app. A `Write` and an `Edit` ran under Accept edits with no `session/request_permission` in the whole run; the file held `beta`. Run on a resumed session, with a second file name |
| [WORKMODE-07](work-mode.md#workmode-07--plan-nothing-is-written-and-the-request-to-leave-plan-mode-is-refused) | 💰 | Pass |  | 2026-10-01, in the app. One `switch_mode` request refused (`reject`), the guard's log line present, no file, stop reason `cancelled`, and nothing posted to the Room (OQ-2 confirmed in the app) |
| [WORKMODE-08](work-mode.md#workmode-08--accept-edits-a-write-into-claude-is-still-refused) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: 2026-10-01, in the app, Haiku, Accept edits, on `017d684` (the `SerializeRaw` fix). One `session/request_permission` for the real `~/.claude/huddle-wm-test.txt`; the app logged "Refused a tool call writing to …", answered `reject`, the tool call failed, the file does not exist and the Agent said permission was denied. **The first two runs failed** (Accept edits, then the Adapter's default): the app answered `allow-once` and the file was created, because `SerializeRaw` turned `rawInput` into `{"file_path":[]}`, so the guard never saw a path. That bug predates Work Modes and is fixed on this branch |
| [WORKMODE-09](work-mode.md#workmode-09--a-mode-is-applied-again-after-an-app-restart) | 💰 | Pass |  | 2026-10-01, in the app. `session/resume`, then `set_config_option` model, whose response showed the mode back at `default`, then `set_config_option` `mode=acceptEdits`, all before the prompt |

## questions

Questions: an Agent asks the Human by tapping, not typing — [area file](questions.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [QUESTIONS-01](questions.md#questions-01--a-model-frames-its-ask-and-calls-ask_human-with-one-to-three-questions) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | **Re-run 2026-10-01 after the fix, in the app, Haiku, one sample: Pass.** With `systemPrompt.askHuman` in the system prompt, a fresh Nova (body *"You are a fitness coach."*, never told about the tool) given the exact prompt called ToolSearch for `mcp__team__ask_human` unprompted, then the tool; the wire shows one `ask_human` call and no `elicitation`. It wrote one framing message (three sentences, over the "one or two" line, said in the past tense *"I've asked you"*) and a card of three single-select questions with 3 to 4 options each, no `@`, and wrote no routine and no guessed answers. **Before the fix, 2026-10-01: Fail, twice** (Coach, then a fresh Nova): each wrote its questions as a numbered list and never called `ask_human`. Cause: the Claude Adapter defers every MCP tool, so the model saw only the name, and the description that carried the guidance was never loaded. One sample proves the path works, not that it is reliable |
| [QUESTIONS-02](questions.md#questions-02--the-asker-ends-its-turn-without-guessing-and-the-framing-lands-before-the-card-can-be-tapped) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | 2026-10-01, in the app, on an explicitly requested call. An in-page observer recorded the card appearing with `still writing` and 3/3 options disabled, then enabled 2.6 s later when Coach's Turn ended; Coach's last words were that it would wait for the answers, with no routine and no guessed answers. Not shown for a spontaneous call, which never happened (QUESTIONS-01) |
| [QUESTIONS-03](questions.md#questions-03--a-factual-question-and-a-request-for-an-opinion-do-not-call-ask_human) | 💰 | Pass |  | | 2026-10-01, in the app, Haiku. Sable answered Paris and the Python/JavaScript question directly, no card. Weak evidence: with the description deferred (QUESTIONS-01) the model could not have been restrained by it |
| [QUESTIONS-04](questions.md#questions-04--the-same-on-an-agent-running-on-agency-acp) | 💰 | Active |  | | Inconclusive 2026-10-01: no `agency-acp` Adapter Profile is configured here |
| [QUESTIONS-05](questions.md#questions-05--in-a-room-of-three-an-answer-wakes-only-the-asker) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | 2026-10-01, in the app, Room of Coach, Nova, Sable. One tap on Blue posted `> What's your favourite colour?`, a blank line, `Blue`, `@Coach`; only Coach replied (Nova's and Sable's Transcripts hold nothing from it), and Nova, asked next, read the answer from the Transcript. A multi-select and ranking card also posted `Mon, Fri` and `1. Speed · 2. Cost · 3. Quality` exactly |
| [QUESTIONS-06](questions.md#questions-06--a-claude-agent-has-no-built-in-askuserquestion) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | **Re-run 2026-10-01 after the elicitation bridge, in the app, Haiku, with `Team__Acp__AdvertiseElicitation=false`: Pass.** Coach said it cannot use `AskUserQuestion` (*"it's been disabled for this session"*); no card of either kind, no Stop button, the Turn ended on its own. The wire trace's `initialize` carried `fs` and `terminal` only, and the trace holds no `elicitation` text at all. The default is now `true`, so this test needs the switch set. Original run, before the bridge existed: 2026-10-01, in the app, Haiku. Nova said it has no `AskUserQuestion` and offered `ask_human`; no second card, the Turn ended on its own, no idle-timeout line. The wire trace's `initialize` carries `fs` and `terminal` (both false) and no `elicitation`, and the log has no `elicitation` text and no `elicitation/create` |
| [QUESTIONS-07](questions.md#questions-07--a-scripted-adapters-forms-reach-the-human-as-cards-with-the-right-types) |  | Active |  | rewritten 2026-10. Earlier Pass: | 2026-10-01, in the app against `mock-acp`, Chief of Staff. `[elicit:form]`: Send stayed disabled until Title, Due date and Boxes were set and while Boxes was 25 (maximum 20); the answer reached the Adapter as `{"title":"Move the office","due":"2026-10-15","count":7,"budget":12.5,"agree":true,"priority":"high","tags":["fragile","cold"],"notes":"Pianos <b>first</b> & \"quotes\""}` (typed, `<b>` shown as text), the Transcript Message quoted every label, and there was one reply. `[elicit:ask]`: typing **Other** cleared and disabled the selection; the Adapter got `{"question_0_custom":"Teal","question_1":["Rugs","Plants"]}`. `[elicit:unsupported]` showed no card and the Adapter got `decline`. `[elicit:refusal]` returned `{"choice":"cancelled"}`; `[elicit:one]` skipped (`decline`). A typed Message with a card open removed the card and the Adapter got `cancel`. **Found and fixed on screen:** the card had no height limit (Send and the composer were off the page on a 720 px window), a Transcript taller than the window could not be scrolled, multi-select options were centred, a one-question form showed its prompt twice, the refusal dialog showed its key `choice`, and an out-of-range number gave no reason |
| [QUESTIONS-08](questions.md#questions-08--claudes-askuserquestion-reaches-the-human-and-the-turn-survives-the-wait) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | 2026-10-01, in the app, Coach on Haiku, real `claude-agent-acp` 0.75.1, idle bound 20 s. Coach called `AskUserQuestion`; the card showed *Colour*, the question once, three options with descriptions and Other. Left alone for ~47 s (more than twice the bound): still open, Stop still offered, no hung-Adapter alert. The trace shows the Adapter's request as `_elicitation/create` with `question_0` as a `oneOf` of three and `question_0_custom`. Choosing Green and Send: card gone, Transcript Message quoting the question and `Green`, and Coach continued in the **same** Turn (*"So green is your favourite colour"*). Coach's session received exactly one `session/prompt`, the Human's |
| [QUESTIONS-09](questions.md#questions-09--dismiss-and-stop-end-a-waiting-form-cleanly) | 💰 | Pass |  | | 2026-10-01, in the app, Haiku. **Skip:** the card went, nothing was posted as the Human, and Coach carried on in the same Turn (*"…whenever you're ready"*). **Stop** on Coach's Draft with the card open: the card and the Stop button went, no failure alert, and an ordinary message afterwards got a normal reply, so the slot and session were intact |
| [QUESTIONS-10](questions.md#questions-10--an-unanswered-form-is-dropped-after-acpuserinputtimeoutseconds) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: | 2026-10-01, in the app, Haiku, `UserInputTimeoutSeconds=30`, idle bound 20 s. The card appeared at 06:45:32 and was gone at the next check, about 35 s later; Coach said the tool use *"didn't go through"* and offered to ask again, the Turn ended, and there was no hung-Adapter line or failure alert. With the default `Acp:AdvertiseElicitation=true` and no other flag, in addition: when asked naturally to plan a workout with both tools available, Haiku chose `ask_human` (a three-Question `QuestionCard`) over `AskUserQuestion`, one sample |

## prompt-blocks

Prompt blocks: an image the Human names reaches the Agent — [area file](prompt-blocks.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PROMPTBLOCKS-01](prompt-blocks.md#promptblocks-01--a-named-image-is-seen-without-a-tool-call) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: 2026-10-01, in the app, Haiku, one sample. The reply read `BANANA 4821` and the circle as red-orange from a PNG named by full path, with the Turn's prompt carrying the image block. |
| [PROMPTBLOCKS-02](prompt-blocks.md#promptblocks-02--six-images-four-are-seen) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: 2026-10-01, in the app, Haiku, one sample. Six PNGs named in one Message: the Agent said it could see 4 pictures and listed Red, Blue, Yellow, Purple (the first four); the log held one line ending `ImageCountCap=2`. |
| [PROMPTBLOCKS-03](prompt-blocks.md#promptblocks-03--an-image-over-the-size-limit-is-a-path-line) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: 2026-10-01, in the app, one sample. A 4.3 MB PNG named in a Message: the Agent replied `PATHONLY`, the Turn completed, and the log held one Information line ending `TooLarge=1` with no path in it. |
| [PROMPTBLOCKS-04](prompt-blocks.md#promptblocks-04--an-image-mentioned-only-in-catch-up-is-not-re-sent) | 💰 | Active |  |  |
| [PROMPTBLOCKS-05](prompt-blocks.md#promptblocks-05--an-adapter-profile-can-turn-blocks-off) | 💰 | Active |  | rewritten 2026-10. Earlier Pass: 2026-10-01, in the app, Haiku, one sample, relaunched with `Adapters:0` (`Id=claude`, `Command=node`, `PromptBlocks=false`; the locator found the stock adapter). A fresh PNG's banner text was asked for with no tools: the Agent replied `CANNOT SEE`. |
| [PROMPTBLOCKS-06](prompt-blocks.md#promptblocks-06--an-adapter-that-never-advertised-image-gets-a-path-line) |  | Active |  |  |

## adapter-commands

Adapter commands — [area file](adapter-commands.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [COMMANDS-01](adapter-commands.md#commands-01--scratch-compact-compacts-the-conversation-and-says-so) | 💰 | Active | |  |
| [COMMANDS-02](adapter-commands.md#commands-02--a-bare-slash-is-still-huddles-own-command) |  | Active | |  |
| [COMMANDS-03](adapter-commands.md#commands-03--a-command-the-profile-does-not-allow-is-an-ordinary-message) | 💰 | Active | |  |
| [COMMANDS-04](adapter-commands.md#commands-04--the-card-lists-what-the-agent-offers-and-only-while-it-is-running) | 💰 | Active | |  |
| [COMMANDS-05](adapter-commands.md#commands-05--stop-during-a-compaction-leaves-the-agent-usable) | 💰 | Active | |  |
| [COMMANDS-06](adapter-commands.md#commands-06--an-agents-kai-compact-is-an-ordinary-message-c7) | 💰 | Active | |  |
| [COMMANDS-07](adapter-commands.md#commands-07--a-compaction-is-not-charged-to-the-token-budget-c15) | 💰 | Active | |  |

## avatar-library

Avatar library: choosing a face from the library — [area file](avatar-library.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [AVATARLIB-01](avatar-library.md#avatarlib-01--a-library-switched-off-no-button-anywhere) |  | Active | |  |
| [AVATARLIB-02](avatar-library.md#avatarlib-02--masterjson-missing-no-button-but-the-picture-mount-stays) |  | Active | |  |
| [AVATARLIB-03](avatar-library.md#avatarlib-03--the-button-appears-only-under-an-image-and-the-dialog-opens-at-48-of-324-l1-m1-m2) |  | Active | |  |
| [AVATARLIB-04](avatar-library.md#avatarlib-04--the-dialog-layout-matches-the-mock-up-and-the-filters-stay-in-view-m2) |  | Active | |  |
| [AVATARLIB-05](avatar-library.md#avatarlib-05--pictures-carry-no-visible-text-caption-or-tooltip-d-4-l15) |  | Active | |  |
| [AVATARLIB-06](avatar-library.md#avatarlib-06--screen-reader-announces-each-picture-l15) |  | Active | |  |
| [AVATARLIB-07](avatar-library.md#avatarlib-07--skin-switch-narrows-to-one-group-l2) |  | Active | |  |
| [AVATARLIB-08](avatar-library.md#avatarlib-08--search-matches-the-start-of-a-word-not-the-middle-l3) |  | Active | |  |
| [AVATARLIB-09](avatar-library.md#avatarlib-09--a-selected-profession-floats-left-with-an-x-the-x-restores-it-l4-l7) |  | Active | |  |
| [AVATARLIB-10](avatar-library.md#avatarlib-10--two-professions-combine-by-or-and-both-chips-stay-at-the-left-l5-d-6) |  | Active | |  |
| [AVATARLIB-11](avatar-library.md#avatarlib-11--two-tags-combine-by-and-l6-d-6) |  | Active | |  |
| [AVATARLIB-12](avatar-library.md#avatarlib-12--filters-combine-across-the-four-controls-as-and-l8-setup) |  | Active | |  |
| [AVATARLIB-13](avatar-library.md#avatarlib-13--clear-all-resets-everything-l8) |  | Active | |  |
| [AVATARLIB-14](avatar-library.md#avatarlib-14--nothing-matches-the-empty-state-and-clear-all-stays-l12-m4) |  | Active | |  |
| [AVATARLIB-15](avatar-library.md#avatarlib-15--the-narrowed-state-matches-the-mock-up-and-other-chips-do-not-move-m3-d-5) |  | Active | |  |
| [AVATARLIB-16](avatar-library.md#avatarlib-16--show-more-adds-48-and-any-filter-change-returns-to-48-at-the-top-grid-paging) |  | Active | |  |
| [AVATARLIB-17](avatar-library.md#avatarlib-17--the-tag-cloud-shows-40-show-all-reveals-the-rest-and-a-selected-tag-stays-visible-tag-cap) |  | Active | |  |
| [AVATARLIB-18](avatar-library.md#avatarlib-18--picking-copies-the-file-the-card-shows-the-new-image-and-nothing-is-written-until-save-l9-l10) |  | Active | |  |
| [AVATARLIB-19](avatar-library.md#avatarlib-19--settings--profile-the-avatar-windows-save-writes-at-once-l11) |  | Active | |  |
| [AVATARLIB-20](avatar-library.md#avatarlib-20--a-malformed-masterjson-shows-the-unreadable-message-and-one-warning-l14-m5) |  | Active | |  |
| [AVATARLIB-21](avatar-library.md#avatarlib-21--skipped-records-are-reported-once-with-a-count-and-examples) |  | Active | |  |
| [AVATARLIB-22](avatar-library.md#avatarlib-22--reopening-reads-the-file-afresh-r-1-l16-escape-and-enter-behave) |  | Active | |  |
| [AVATARLIB-23](avatar-library.md#avatarlib-23--the-picker-sits-above-the-avatar-window-and-the-card-and-focus-and-escape-stay-in-order) |  | Active | |  |
| [AVATARLIB-24](avatar-library.md#avatarlib-24--real-library-first-look-324-pictures-each-the-right-size-and-shape) |  | Active | |  |
| [AVATARLIB-25](avatar-library.md#avatarlib-25--on-settings--profile-a-half-made-choice-in-the-avatar-window-stays-chosen-and-an-outside-edit-still-reaches-the-card) |  | Active | |  |
| [AVATARLIB-26](avatar-library.md#avatarlib-26--a-library-file-that-vanishes-before-the-pick-that-image-could-not-be-read) |  | Active | |  |
| [AVATARLIB-27](avatar-library.md#avatarlib-27--the-avatar-window-opens-from-the-avatar-in-every-card-mode-and-applies-or-saves-as-the-mode-says) |  | Active | |  |
| [AVATARLIB-28](avatar-library.md#avatarlib-28--nothing-configured-the-bundled-library-is-offered) |  | Active | |  |

## file-changes

File Changes, Watched Folders and Memory — [area file](file-changes.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [FM-0](file-changes.md#fm-0--an-agents-own-memory-edit-in-one-room-is-news-in-its-other-rooms) | 💰 | Active | |  |
| [FM-1](file-changes.md#fm-1--a-file-edited-outside-huddle-is-listed-on-the-next-turn) |  | Active | |  |
| [FM-2](file-changes.md#fm-2--the-same-edit-made-while-huddle-was-closed) |  | Active | |  |
| [FM-3](file-changes.md#fm-3--a-note-the-agent-writes-itself-is-not-listed-back-on-its-next-turn) | 💰 | Active | |  |
| [FM-4](file-changes.md#fm-4--watch_folder-on-a-shared-folder-then-a-restart-then-an-outside-edit) | 💰 | Active | |  |
| [FM-5](file-changes.md#fm-5--renaming-a-watched-agent-does-not-spuriously-list-anything) | 💰 | Active | |  |
| [FM-6](file-changes.md#fm-6--isolation-and-memory-across-a-restart-with-a-real-preference) | 💰 | Active | |  |
| [FM-7](file-changes.md#fm-7--room-only-instructions-stay-out-of-memory) | 💰 | Active | |  |
| [FM-8](file-changes.md#fm-8--an-agent-describes-its-own-memory-honestly) | 💰 | Active | |  |
| [FC-V1](file-changes.md#fc-v1--does-isolation-actually-drop-the-humans-own-settings-and-claudemd) | 💰 | Active | |  |
| [FC-V2](file-changes.md#fc-v2--does-automemoryenabled-false-actually-stop-auto-memory) | 💰 | Active | |  |
| [FC-V4](file-changes.md#fc-v4--does-a-per-profile-claude_config_dir-move-the-login-only-if-fc-v1-or-fc-v2-fails) | 💰 | Active | |  |

## room-sessions

Room Sessions: per-Room context, resume and Stop — [area file](room-sessions.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [RS-M1](room-sessions.md#rs-m1--the-two-trip-stress-test) |  | Active | |  |
| [RS-M2](room-sessions.md#rs-m2--a-room-scoped-preference-crosses-rooms-only-through-memory) |  | Active | |  |
| [RS-M3](room-sessions.md#rs-m3--asked-about-another-room-the-agent-says-it-cannot-see-it) |  | Active | |  |
| [RS-M4](room-sessions.md#rs-m4--stop-in-one-room-leaves-anothers-turn-running) |  | Active | |  |
| [RS-M5](room-sessions.md#rs-m5--idle-close-resume-and-an-app-restart-both-remembering) |  | Active | |  |
| [RS-M6](room-sessions.md#rs-m6--restart-forgets-the-next-turn-is-fresh-with-catch-up) |  | Active | |  |
| [RS-M7](room-sessions.md#rs-m7--a-shared-session-adapter-profile-is-unchanged) |  | Active | |  |
| [RS-M8](room-sessions.md#rs-m8--process-and-memory-cost-at-the-live-cap) |  | Active | |  |
| [RS-M9](room-sessions.md#rs-m9--the-humans-own-claude-code-settings-do-not-leak-in) |  | Active | |  |
| [RS-M10](room-sessions.md#rs-m10--a-coordinators-overview-deliberately-reassembled) |  | Active | |  |
| [RS-V3](room-sessions.md#rs-v3--resume-survives-a-rename) |  | Active | |  |
| [RS-V5](room-sessions.md#rs-v5--does-agency-acp-hold-several-sessions-and-advertise-resume) |  | Active | |  |

## team-chats

Team chats: the Team Room, Team chats in the sidebar, the Members-tab Message, archiving and Task wakes and digests — [area file](team-chats.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TEAMCHATS-01](team-chats.md#teamchats-01--the-team-room-appears-under-its-team-with-every-member-and-rooms-does-not-list-it) |  | Active |  |  |
| [TEAMCHATS-02](team-chats.md#teamchats-02--an-agent-who-joins-the-team-is-invited-into-the-team-room-and-the-transcript-says-so) |  | Active |  |  |
| [TEAMCHATS-03](team-chats.md#teamchats-03--new-chat-on-a-team-scopes-the-room-to-that-team-and-a-non-member-can-be-ticked) |  | Active |  |  |
| [TEAMCHATS-04](team-chats.md#teamchats-04--message-on-the-members-tab-opens-the-teams-own-one-to-one-not-the-root-one) |  | Active |  |  |
| [TEAMCHATS-05](team-chats.md#teamchats-05--new-chat-in-the-rooms-group-still-makes-a-root-room) |  | Active |  |  |
| [TEAMCHATS-06](team-chats.md#teamchats-06--the-team-room-has-no-rename-pencil-and-a-rename-through-the-chat-header-is-refused) |  | Active |  |  |
| [TEAMCHATS-07](team-chats.md#teamchats-07--renaming-the-team-folder-in-explorer-archives-its-rooms-history-intact) |  | Active |  |  |
| [TEAMCHATS-08](team-chats.md#teamchats-08--an-archived-team-room-is-not-recreated-or-refilled) |  | Active |  |  |
| [TEAMCHATS-09](team-chats.md#teamchats-09--a-task-wake-whose-origin-room-is-gone-lands-in-the-teams-room) |  | Active |  |  |
| [TEAMCHATS-10](team-chats.md#teamchats-10--a-digest-posts-in-the-team-room-with-a-mention-of-its-recipient) |  | Active |  |  |
| [TEAMCHATS-11](team-chats.md#teamchats-11--a-digest-falls-back-to-the-recipients-root-one-to-one-when-the-team-room-is-archived) |  | Active |  |  |

## uploads

Chat uploads: the paperclip, paste and drop, the chips, the Message's path lines, the Uploads root and Room delete — [area file](uploads.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [UPLOADS-01](uploads.md#uploads-01--the-paperclip-attaches-a-file-as-a-chip-saved-into-the-rooms-uploads-folder) |  | Active |  |  |
| [UPLOADS-02](uploads.md#uploads-02--a-pasted-screenshot-is-saved-under-a-time-stamped-name) |  | Active |  |  |
| [UPLOADS-03](uploads.md#uploads-03--dragging-a-file-onto-the-composer-shows-the-drop-hint-and-attaches-it) |  | Active |  |  |
| [UPLOADS-04](uploads.md#uploads-04--a-refused-file-shows-its-reason-on-the-chip-and-is-not-saved) |  | Active |  |  |
| [UPLOADS-05](uploads.md#uploads-05--send-posts-the-path-lines-with-the-typed-text-and-the-path-is-a-library-link) |  | Active |  |  |
| [UPLOADS-06](uploads.md#uploads-06--removing-a-chip-deletes-its-saved-file-and-nothing-is-posted) |  | Active |  |  |
| [UPLOADS-07](uploads.md#uploads-07--deleting-a-room-sends-its-uploads-to-the-recycle-bin-and-says-how-many) |  | Active |  |  |
| [UPLOADS-08](uploads.md#uploads-08--the-uploads-root-and-the-attached-files-look-right-in-light-and-dark-themes) |  | Active |  |  |

## team-pages

Teams: Sidebar, Members, Files, Tasks and Shared Memory — [area file](team-pages.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TP-T0](team-pages.md#tp-t0--click-business-team-and-open-members-tab) |  | Active | |  |
| [TP-T1](team-pages.md#tp-t1--members-tab-shows-the-roster-with-status-and-message) |  | Active | |  |
| [TP-T2](team-pages.md#tp-t2--remove-from-team-keeps-the-agent) |  | Active | |  |
| [TP-T4](team-pages.md#tp-t4--open-files-tab-and-create-new-note) |  | Active | |  |
| [TP-T5](team-pages.md#tp-t5--search-files-by-name) |  | Active | |  |
| [TP-T6](team-pages.md#tp-t6--open-project-page-scoped-to-project-folder) |  | Active | |  |
| [TP-T7](team-pages.md#tp-t7--open-tasks-tab-on-team-and-create-new-task) |  | Active | |  |
| [TP-T8](team-pages.md#tp-t8--open-tasks-tab-on-project-and-create-new-task) |  | Active | |  |
| [TP-T10](team-pages.md#tp-t10--create-new-project-from-team-actions-menu) |  | Active | |  |
| [TP-T11](team-pages.md#tp-t11--refuse-project-name-memory) |  | Active | |  |
| [TP-T12](team-pages.md#tp-t12--team-memory-file-appears-in-another-members-next-turn) |  | Active | |  |
| [TP-T14](team-pages.md#tp-t14--team-exists-with-no-members-but-has-members-page) |  | Active | |  |
| [TP-T15](team-pages.md#tp-t15--team-with-no-folder-yet-creates-folder-on-first-save) |  | Active | |  |
| [TP-T16](team-pages.md#tp-t16--details-tab-saves-the-purpose-the-members-tab-hires-and-deletes) |  | Active | |  |
| [TP-REAL-02](team-pages.md#tp-real-02--dark-theme-and-narrow-window-styling) |  | Active | |  |

## turn-detail

Turn detail and Spend — [area file](turn-detail.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TURNDETAIL-01](turn-detail.md#turndetail-01--an-edit-shows-a-live-expandable-preview-in-a-room) | 💰 | Active | |  |
| [TURNDETAIL-02](turn-detail.md#turndetail-02--spend-is-the-adapters-running-total-across-two-turns) | 💰 | Active | |  |
| [TURNDETAIL-03](turn-detail.md#turndetail-03--a-persona-on-agency-acp-shows-rows-with-no-preview-and-no-spend-line) |  | Active | |  |
| [TURNDETAIL-04](turn-detail.md#turndetail-04--the-list-and-preview-at-320-px-and-under-three-themes) | 💰 | Active | |  |
| [TURNDETAIL-05](turn-detail.md#turndetail-05--a-burst-of-ten-quick-tool-calls-neither-reorders-nor-flickers-the-list) | 💰 | Active | |  |

## right-sidebar

Right sidebar: the panels by Subject, Outline scrolling, collapse and focus — [area file](right-sidebar.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [RSIDE-01](right-sidebar.md#rside-01--a-room-shows-members-tasks-and-documents) |  | Active |  |  |
| [RSIDE-02](right-sidebar.md#rside-02--a-message-that-references-a-task-appears-and-its-jump-works) |  | Active |  |  |
| [RSIDE-03](right-sidebar.md#rside-03--a-markdown-document-shows-outline-and-linked-documents) |  | Active |  |  |
| [RSIDE-04](right-sidebar.md#rside-04--an-outline-heading-scrolls-the-document-in-read-split-and-edit) |  | Active |  |  |
| [RSIDE-05](right-sidebar.md#rside-05--typing-a-heading-in-split-updates-the-outline) |  | Active |  |  |
| [RSIDE-06](right-sidebar.md#rside-06--a-linked-note-opens-from-the-linked-documents-panel) |  | Active |  |  |
| [RSIDE-07](right-sidebar.md#rside-07--a-collapsed-panel-stays-collapsed-after-a-reload) |  | Active |  |  |
| [RSIDE-08](right-sidebar.md#rside-08--two-panes-follow-focus) |  | Active |  |  |
| [RSIDE-09](right-sidebar.md#rside-09--an-agents-context-bar-appears-grows-during-a-turn-and-falls-after-compact) | 💰 | Pass |  |  |
| [RSIDE-10](right-sidebar.md#rside-10--the-backlinks-panel-lists-the-open-tasks-that-link-to-a-document) |  | Active |  |  |

## needs-you

Needs You: the sidebar card of Rooms waiting on the Human — [area file](needs-you.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [NEEDSYOU-01](needs-you.md#needsyou-01--a-mention-is-rebuilt-at-startup-listed-oldest-first-and-the-card-is-hidden-when-nothing-waits) |  | Active |  | | not yet run by a person; checked once in the app 2026-10-12 |
| [NEEDSYOU-02](needs-you.md#needsyou-02--opening-a-room-does-not-clear-it-posting-in-it-does) |  | Active |  | | not yet run by a person; checked once in the app 2026-10-12 |
| [NEEDSYOU-03](needs-you.md#needsyou-03--dismiss-hides-a-room-without-answering-what-is-behind-it) |  | Active |  | | not yet run by a person; checked once in the app 2026-10-12 |
| [NEEDSYOU-04](needs-you.md#needsyou-04--archiving-hides-a-room-from-the-card-and-unarchiving-brings-it-back) |  | Active |  | | not yet run by a person; checked once in the app 2026-10-12 |

## jobs

Jobs: scheduled instructions in their own Rooms — [area file](jobs.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [JOBS-01](jobs.md#jobs-01--a-job-for-an-agent-runs-in-its-own-room) |  | Pass |  | 2026-11-02, uncommitted `feat/schedules`. Once at 16:49 for Ben ran on its tick into `⏰ Invoice check` (Ben and the Human); Ben replied; the one-off then left Scheduled. Time-input seconds bug found and fixed in the run |
| [JOBS-02](jobs.md#jobs-02--a-job-for-the-human-goes-on-needs-you) |  | Active |  | | not yet run by a person |
| [JOBS-03](jobs.md#jobs-03--the-file-is-the-source-of-truth) |  | Active |  | | not yet run by a person |
| [JOBS-04](jobs.md#jobs-04--a-restart-neither-repeats-nor-sends-late) |  | Active |  | | not yet run by a person |
| [JOBS-05](jobs.md#jobs-05--only-the-chief-of-staff-schedules-for-others) | 💰 | Pass |  | 2026-11-02, uncommitted `feat/schedules`. Ravi was refused with the exact text; the Chief of Staff scheduled `Bins check` for Ravi (`by: Chief of Staff`) |
| [JOBS-06](jobs.md#jobs-06--a-monthly-job-runs-on-its-day-or-on-the-last-day-of-a-short-month) |  | Active |  | | added 2026-11-04, not yet run by a person |
| [JOBS-07](jobs.md#jobs-07--a-manager-schedules-for-its-team-and-for-no-one-else) | 💰 | Active |  | | added 2026-11-04, not yet run by a person |

## second-brain

Second Brain: the use cases as a Human's checklist — [area file](second-brain.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [SB-01](second-brain.md#sb-01--a-dropped-file-lands-in-the-team-raw-folder) |  | Pass |  | 2026-11-03: path, size and path line held; modified time is not kept, which the Spec does not ask for |
| [SB-02](second-brain.md#sb-02--a-fact-the-human-states-is-proposed-to-team-memory) | 💰 | Pass |  | 2026-11-03 rerun after `8fdb82ed`: filed on the first try, Feed names Finn |
| [SB-03](second-brain.md#sb-03--a-standing-rule-is-proposed-as-an-agentsmd-change) | 💰 | Pass |  | 2026-11-03 rerun after `0ac2df3e`: Finn said the rule belongs in AGENTS.md and proposed it; Accept wrote the line |
| [SB-04](second-brain.md#sb-04--an-edit-to-home-agentsmd-reaches-the-next-session) | 💰 | Pass |  | 2026-11-03 rerun with the restart step, on Home with Nova |
| [SB-05](second-brain.md#sb-05--nova-prompt-parts-show-their-source) |  | Pass |  | 2026-11-03: every part sourced; three rows share the label "AGENTS.md block" |
| [SB-06](second-brain.md#sb-06--chat-rules-overridden-on-settings-and-restored-by-deleting-the-file) |  | Pass |  | 2026-11-03 |
| [SB-07](second-brain.md#sb-07--archiving-a-team-counts-its-open-tasks-and-moves-the-folder) |  | Pass |  | 2026-11-03 rerun after the pipe fix: Rooms left the list and are recorded in `archivedRooms`; "1 open Tasks" plural slip |
| [SB-08](second-brain.md#sb-08--an-archived-file-opens-read-only-and-unarchiving-restores-the-team) |  | Pass |  | 2026-11-03 rerun after `4f7d883c` and `176c037e`: Archived chip shown; unarchive no longer wakes the manager |
| [SB-09](second-brain.md#sb-09--an-old-data-folder-logs-warnings-at-start-up) |  | Pass |  | 2026-11-03 |
| [SB-10](second-brain.md#sb-10--an-agent-cannot-overwrite-a-raw-file-and-writes-an-extract-beside-it) | 💰 | Pass |  | 2026-11-03: hash unchanged, extract written; rerun reached the permission handler, which refused the Write and logged the reason |

## second-brain-zero

Second Brain from zero: three Teams hired by hand, documents ingested into memory, and a Development Team building an app — [area file](second-brain-zero.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [ZERO-01](second-brain-zero.md#zero-01--an-empty-folder-starts-on-the-profile-page) |  | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-02](second-brain-zero.md#zero-02--the-chief-of-staff-greets-a-new-human) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-03](second-brain-zero.md#zero-03--hire-the-family-team) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-04](second-brain-zero.md#zero-04--the-manager-starts-with-its-process-and-second-brain-skills) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-05](second-brain-zero.md#zero-05--onboard-the-family-team-from-a-profile-document) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-06](second-brain-zero.md#zero-06--ingest-the-familys-documents) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-07](second-brain-zero.md#zero-07--a-later-document-corrects-a-fact-in-place) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-08](second-brain-zero.md#zero-08--hire-and-set-up-the-bakery-team) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-09](second-brain-zero.md#zero-09--ingest-the-bakerys-documents) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-10](second-brain-zero.md#zero-10--a-member-who-never-saw-the-files-uses-what-was-filed) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-11](second-brain-zero.md#zero-11--family-members-use-the-filed-memory-too) | 💰 | Fail |  | run 2026-11-05, script fault in the grocery total, rerun it, see the Run log |
| [ZERO-12](second-brain-zero.md#zero-12--prepare-a-repository-and-hire-the-dev-team) | 💰 | Pass | #347, #348 | rerun 2026-11-05 on the script's models, Pass, see the Run log |
| [ZERO-13](second-brain-zero.md#zero-13--a-gate-refuses-done-with-no-evidence) | 💰 | Active |  | run 2026-11-05, no verdict on the edit-form clause, see the Run log |
| [ZERO-14](second-brain-zero.md#zero-14--ingest-the-spec-and-the-teams-conventions) | 💰 | Pass | #342 | rerun 2026-11-05 on the script's models, Pass, see the Run log |
| [ZERO-15](second-brain-zero.md#zero-15--the-dev-team-builds-the-app) | 💰 | Pass | #396, #399, #401, #404 | rerun 2026-11-05 on the script's models, Pass with a finding, see the Run log |
| [ZERO-16](second-brain-zero.md#zero-16--run-the-app-yourself) |  | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-17](second-brain-zero.md#zero-17--capture-through-the-chief-of-staff) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-18](second-brain-zero.md#zero-18--a-huddle-wide-fact-reaches-every-team-and-other-teams-facts-do-not) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-19](second-brain-zero.md#zero-19--a-member-proposes-a-fact-and-the-helper-files-it) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-20](second-brain-zero.md#zero-20--a-rule-goes-to-agentsmd-through-you) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-21](second-brain-zero.md#zero-21--an-agent-cannot-change-an-original) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-22](second-brain-zero.md#zero-22--wrapping-up-a-session) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-23](second-brain-zero.md#zero-23--the-weekly-check-finds-planted-drift) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-24](second-brain-zero.md#zero-24--the-chief-of-staff-offers-upkeep-and-runs-a-daily-review) | 💰 | Pass |  | run 2026-11-05, see the Run log |
| [ZERO-25](second-brain-zero.md#zero-25--everything-survives-a-restart) | 💰 | Pass |  | run 2026-11-05, Pass with a finding, see the Run log |
| [ZERO-26](second-brain-zero.md#zero-26--a-request-carries-work-between-teams) | 💰 | Pass |  | rerun 2026-11-06 at `108dfef8`, every Agent on Sonnet; early merge and early close not exercised, see the Run log |
| [ZERO-27](second-brain-zero.md#zero-27--find-it-all-and-add-up-the-cost) |  | Fail |  | run 2026-11-05, Task search matches titles only, see the Run log |

## doc-history

Document history: the byline, the History and the History panel — [area file](doc-history.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [DOCHISTORY-01](doc-history.md#dochistory-01--two-agents-create-and-edit-a-document-the-byline-and-the-panel-show-both) | 💰 | Active |  |  |
| [DOCHISTORY-02](doc-history.md#dochistory-02--a-review-that-changes-nothing-is-a-reviewed-by) | 💰 | Active |  |  |
| [DOCHISTORY-03](doc-history.md#dochistory-03--saving-in-the-library-adds-an-entry-under-the-humans-name-without-a-reload) |  | Active |  |  |
| [DOCHISTORY-04](doc-history.md#dochistory-04--renaming-and-moving-a-document-carries-its-history) |  | Active |  |  |
| [DOCHISTORY-05](doc-history.md#dochistory-05--how-often-do-real-agents-log-their-edits) | 💰 | Active |  |  |
| [DOCHISTORY-06](doc-history.md#dochistory-06--the-history-is-grouped-by-day-whole-and-the-byline-leads-to-it) |  | Active |  |  |
| [DOCHISTORY-07](doc-history.md#dochistory-07--an-agent-offered-log_change-gets-the-stop-hook-and-existing-settings-stay) |  | Active |  |  |
| [DOCHISTORY-08](doc-history.md#dochistory-08--the-hook-sends-an-agent-back-to-log-and-leaves-other-turns-alone) | 💰 | Pass |  | run 2026-11-06 by an agent on `779b1aa5`, Sonnet 5.5, 0.087 USD. Keeper logged unprompted, so an extra Turn told it to use only Write: the hook sent it back and it logged |

## human-gate

Human gate: the Proof list, the decision card, Accept and Decline, the board drag and the Needs You page — [area file](human-gate.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [HUMANGATE-01](human-gate.md#humangate-01--requires-your-acceptance-only-the-human-can-mark-the-task-done-u-1) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-02](human-gate.md#humangate-02--an-action-locks-the-gate-on-and-accept-wakes-the-assignee-u-2) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-03](human-gate.md#humangate-03--proof-34-accept-is-locked-request-changes-works-ask-posts-a-mention-u-3) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-04](human-gate.md#humangate-04--an-agent-cannot-mark-a-gated-task-done-u-4) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-05](human-gate.md#humangate-05--dragging-a-gated-card-to-done-on-the-board-u-5) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-06](human-gate.md#humangate-06--editing-the-task-after-accept-takes-the-acceptance-away-u-6) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-07](human-gate.md#humangate-07--decline-closes-the-task-with-its-reason-u-7) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-08](human-gate.md#humangate-08--an-outside-edit-that-removes-the-action-is-put-back-u-8) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-09](human-gate.md#humangate-09--restart-keeps-every-waiting-task-on-the-needs-you-page-u-9) |  | Pass |  | 2026-10-19 paid run (Haiku) |
| [HUMANGATE-10](human-gate.md#humangate-10--an-agent-that-rewrites-its-own-settings-still-waits-for-the-merge-gate-issue-432) | 💰 | Pass |  | run 2026-11-06 by an agent at the handler level, see the Run log |

## helpers

Helpers: the Helpers tab, Save and Reset, the Needs Human judgement, the override, the daily cap and a failing Adapter — [area file](helpers.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [HELPERS-01](helpers.md#helpers-01--the-helpers-tab-shows-the-needs-human-card-u-1-setup) |  | Active |  |  |
| [HELPERS-02](helpers.md#helpers-02--save-and-reset-to-defaults-and-a-restart-keeps-both-u-7-setup) |  | Active |  |  |
| [HELPERS-03](helpers.md#helpers-03--an-agents-task-that-leaves-the-team-gets-the-gate-u-1-u-5) | 💰 | Active |  |  |
| [HELPERS-04](helpers.md#helpers-04--a-harmless-task-is-left-alone-u-2-u-4-u-11) | 💰 | Active |  |  |
| [HELPERS-05](helpers.md#helpers-05--the-humans-override-is-final-u-5) | 💰 | Active |  |  |
| [HELPERS-06](helpers.md#helpers-06--the-daily-cap-pauses-the-helper-and-continue-allows-a-few-more-u-7) | 💰 | Active |  |  |
| [HELPERS-07](helpers.md#helpers-07--a-failure-leaves-the-task-as-saved-and-says-why-u-8-u-10) |  | Active |  |  |
| [HELPERS-08](helpers.md#helpers-08--the-wake-up-is-huddles-and-it-leaves-your-mentions-alone-helpers-in-the-feed-56) | 💰 | Active |  |  |


## feed

Feed: the Notifications bell and panel, the Feed Tab, cards, filters, the new-items pill and the restart — [area file](feed.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [FEED-01](feed.md#feed-01--the-bell-counts-unseen-notifications-and-opening-the-panel-marks-them-seen) |  | Active |  | not yet run by a person |
| [FEED-02](feed.md#feed-02--the-panel-opens-on-needs-you-and-its-categories-switch) |  | Active |  | not yet run by a person |
| [FEED-03](feed.md#feed-03--the-dismiss-cross-appears-on-hover-and-hides-a-notification-from-the-bell-only) |  | Active |  | not yet run by a person |
| [FEED-04](feed.md#feed-04--a-task-assigned-to-the-human-appears-and-resolves-when-it-is-reassigned) |  | Active |  | not yet run by a person |
| [FEED-05](feed.md#feed-05--a-gated-task-moved-to-needs-you-appears-with-its-proof-count-and-resolves-when-accepted) | 💰 | Active |  | not yet run by a person |
| [FEED-06](feed.md#feed-06--a-mention-appears-and-resolves-when-the-human-posts-in-the-room) | 💰 | Active |  | not yet run by a person |
| [FEED-07](feed.md#feed-07--the-feed-tab-header-shows-the-live-chip-the-read-only-note-seven-counted-segments-and-the-team-select) |  | Active |  | not yet run by a person |
| [FEED-08](feed.md#feed-08--a-card-expands-to-a-task-preview-with-buttons-that-change-nothing) |  | Active |  | not yet run by a person |
| [FEED-09](feed.md#feed-09--clicking-an-avatar-filters-the-feed-to-that-agent) |  | Active |  | not yet run by a person |
| [FEED-10](feed.md#feed-10--new-items-wait-behind-a-pill-when-the-list-is-scrolled) |  | Active |  | not yet run by a person |
| [FEED-11](feed.md#feed-11--the-unseen-bar-clears-on-the-next-visit) |  | Active |  | not yet run by a person |
| [FEED-12](feed.md#feed-12--a-restart-resolves-a-form-notification) |  | Active |  | not yet run by a person |
| [FEED-13](feed.md#feed-13--turning-the-feed-off-hides-the-bell-and-the-link) |  | Active |  | not yet run by a person |
| [FEED-14](feed.md#feed-14--no-message-text-appears-anywhere-in-the-feed) |  | Active |  | not yet run by a person |
| [FEED-15](feed.md#feed-15--a-helpers-gate-change-is-a-notification-with-its-reason-and-the-wake-up-is-huddles) | 💰 | Active |  | not yet run by a person |
| [FEED-16](feed.md#feed-16--a-helper-that-keeps-failing-reaches-the-feed-and-turning-it-off-clears-it) |  | Active |  | not yet run by a person |
| [FEED-17](feed.md#feed-17--a-helper-at-its-cap-reaches-the-feed-once-and-continue-clears-it) | 💰 | Active |  | not yet run by a person |

## remote-access

Remote access: the connect page, pairing, Tailscale Serve and Funnel, the LAN bind, revocation and the Open action — [area file](remote-access.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [REMOTE-01](remote-access.md#remote-01--the-installed-app-opens-signed-in-and-a-second-launch-opens-another-signed-in-tab) |  | Active |  | not yet run by a person |
| [REMOTE-02](remote-access.md#remote-02--a-second-browser-on-the-host-sees-the-connect-page-and-dashboard-and-auth-token-work-as-documented) |  | Active |  | not yet run by a person |
| [REMOTE-03](remote-access.md#remote-03--tailscale-serve-a-phone-gets-in-with-no-token-and-no-approval-limited-then-asks-for-admin) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-04](remote-access.md#remote-04--tailscale-serve-a-shared-tailnet-user-is-marked-and-revoking-disconnects-them) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-05](remote-access.md#remote-05--lan-the-qr-setup-link-on-iphone-and-android-with-approval-and-the-downgraded-warnings) |  | Active |  | not yet run by a person |
| [REMOTE-06](remote-access.md#remote-06--rate-limit-ten-wrong-tokens-lock-a-phone-out-for-five-minutes-and-loopback-is-only-slowed) |  | Active |  | not yet run by a person |
| [REMOTE-07](remote-access.md#remote-07--revoking-a-phone-with-a-room-streaming-disconnects-it-at-once) |  | Active |  | not yet run by a person |
| [REMOTE-08](remote-access.md#remote-08--two-copies-on-ports-5101-and-5102-keep-separate-devices-and-no-sign-outs) |  | Active |  | not yet run by a person |
| [REMOTE-09](remote-access.md#remote-09--a-hand-made-serve-route-is-refused-and-shown-in-the-security-check) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-10](remote-access.md#remote-10--funnel-refused-in-token-mode-and-in-password-mode-a-phone-off-the-tailnet-needs-the-password-and-approval) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-11](remote-access.md#remote-11--with-remote-access-configured-the-open-action-and-library-open-and-reveal-are-gone) |  | Active |  | not yet run by a person |
| [REMOTE-12](remote-access.md#remote-12--a-huddle-crash-kills-the-tailscale-child-job-object) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-13](remote-access.md#remote-13--killing-the-tailscale-child-removes-the-serve-route) |  | Active |  | not yet run by a person; needs Tailscale |
| [REMOTE-14](remote-access.md#remote-14--strict-local-default) |  | Active |  | not yet run by a person |
| [REMOTE-15](remote-access.md#remote-15--turning-strict-local-on-disconnects-remote-devices) |  | Active |  | not yet run by a person; needs a second device |

---

Back to [the manual test script](../manual-tests.md).

## instructions-memory

Instructions and Memory: the Instructions files, the memory tool, Team Memory and the Memory protocol — [area file](instructions-memory.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [INSTRMEM-01](instructions-memory.md#instrmem-01--a-team-instructions-file-reaches-every-member-on-its-next-session) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-02](instructions-memory.md#instrmem-02--a-new-topic-file-is-written-with-a-description-and-appears-in-the-next-session-index) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-03](instructions-memory.md#instrmem-03--two-rooms-write-one-memory-file-and-the-second-write-is-refused-with-the-current-text) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-04](instructions-memory.md#instrmem-04--a-member-write-to-team-memory-is-refused-through-the-tool-and-through-write-while-its-read-is-allowed) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-05](instructions-memory.md#instrmem-05--a-human-edit-in-the-library-is-seen-at-the-next-read-and-an-old-version-is-refused) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-06](instructions-memory.md#instrmem-06--an-agent-deletes-a-memory-file-only-when-the-human-asks) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-07](instructions-memory.md#instrmem-07--spike-s-1-whether-claude-code-loads-the-repository-claude-files-into-a-persona) | 💰 | Active |  | not yet run; this is spike S-1 and needs a live, paid Claude Code session |
| [INSTRMEM-08](instructions-memory.md#instrmem-08--a-restart-recalls-a-memory-from-the-index-and-the-files) | 💰 | Active |  | not yet run by a person |
| [INSTRMEM-09](instructions-memory.md#instrmem-09--an-adapter-with-no-file-tools-still-gets-the-memory-tool-and-the-memory-block) |  | Active |  | not yet run by a person |

## team-memory-helper

Team Memory Helper: proposals filed by a Helper, the History, the Feed record, the membership check and the daily cap — [area file](team-memory-helper.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TMHELP-01](team-memory-helper.md#tmhelp-01--the-helpers-tab-shows-the-team-memory-card) |  | Active |  | not yet run by a person |
| [TMHELP-02](team-memory-helper.md#tmhelp-02--a-member-proposal-is-filed-with-its-proposer-in-the-history) | 💰 | Active |  | not yet run by a person |
| [TMHELP-03](team-memory-helper.md#tmhelp-03--a-later-proposal-updates-the-same-topic-file) | 💰 | Active |  | not yet run by a person |
| [TMHELP-04](team-memory-helper.md#tmhelp-04--a-personal-preference-is-not-filed-and-the-feed-says-why) | 💰 | Active |  | not yet run by a person |
| [TMHELP-05](team-memory-helper.md#tmhelp-05--a-proposal-to-a-team-the-member-is-not-in-is-refused) | 💰 | Active |  | not yet run by a person |
| [TMHELP-06](team-memory-helper.md#tmhelp-06--instructions-inside-a-proposal-do-not-move-the-write-outside-the-team) | 💰 | Active |  | not yet run by a person |
| [TMHELP-07](team-memory-helper.md#tmhelp-07--the-daily-cap-pauses-the-helper-and-the-tool-says-so) | 💰 | Active |  | not yet run by a person |
| [TMHELP-08](team-memory-helper.md#tmhelp-08--a-new-manager-writes-team-memory-during-onboarding) | 💰 | Active |  | not yet run by a person |

## long-running-rooms

Long-running Rooms: the experiment on the Chief of Staff's Room, recall after a fresh start, repeats and the hand-played flush — [area file](long-running-rooms.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [LONGROOMS-01](long-running-rooms.md#longrooms-01--claude-codes-own-memory-stays-off-for-the-chief-of-staff) | 💰 | Active |  | run 2026-10-31 on a seeded Room (spec §9.3); real Room still to do |
| [LONGROOMS-02](long-running-rooms.md#longrooms-02--recall-after-a-fresh-start-at-the-default-catch-up) | 💰 | Active |  | run 2026-10-31 on a seeded Room (spec §9.3); real Room still to do |
| [LONGROOMS-03](long-running-rooms.md#longrooms-03--recall-after-a-fresh-start-with-a-40000-token-catch-up) | 💰 | Active |  | run 2026-10-31 on a seeded Room (spec §9.3); real Room still to do |
| [LONGROOMS-04](long-running-rooms.md#longrooms-04--repeats-in-a-live-session-after-compact-and-after-a-fresh-start) | 💰 | Active |  | run 2026-10-31 on a seeded Room (spec §9.3); real Room still to do |
| [LONGROOMS-05](long-running-rooms.md#longrooms-05--a-fresh-start-with-and-without-a-save-request-first) | 💰 | Active |  | run 2026-10-31 on a seeded Room (spec §9.3); real Room still to do |

## agents-md-changes

AGENTS.md changes: a running Agent hears an edit, the skipped typo, the notice's wording, `/compact`, a real restart and a save loop — [area file](agents-md-changes.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [AGENTSMDCHANGES-01](agents-md-changes.md#agentsmdchanges-01--the-helpers-tab-shows-the-agentsmd-changes-card) |  | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Three cards (Needs Human, Team Memory, AGENTS.md Changes). Card: On, Haiku 5.5, Low, Daily cap, `0 of 20 calls today`. Edit the prompt opened `helper.agentsMdChange.system`. Haiku |
| [AGENTSMDCHANGES-02](agents-md-changes.md#agentsmdchanges-02--a-running-session-follows-a-new-team-rule-in-two-rooms) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Passed on the rerun with an existing file. First run: file new, so it settled free as a Comparison (card stayed 0), which is by design. Rerun: card 0 -> 1 of 20 for two Rooms. Room A "10\n\nNoted, Research.", Room B "Circle.\n\nNoted, Research." Haiku |
| [AGENTSMDCHANGES-03](agents-md-changes.md#agentsmdchanges-03--a-running-session-follows-a-new-huddle-wide-rule) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Appended `Start every reply with the word "Understood."` at 22:28:47; settled 22:30:07 as a Summary; card 2 of 20. Room A "Understood. 6\n\nNoted, Research.", Room B "Understood. Apple.\n\nNoted, Research." Haiku |
| [AGENTSMDCHANGES-04](agents-md-changes.md#agentsmdchanges-04--a-typo-fix-is-skipped-and-the-feed-lists-it) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Passed on the final build: Skipped ledger entry and a Feed item; Leo told nothing of the fix. An earlier run got `meaningful:true` with no changes, which `Validate` rejected, and the Agent got a truthful comparison; the prompt now rules it out. Haiku |
| [AGENTSMDCHANGES-05](agents-md-changes.md#agentsmdchanges-05--the-notices-wording-is-followed) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. No pushback across 3 rounds in Room C. Each reply ended with the new words; no mention of injection or refusal. Haiku |
| [AGENTSMDCHANGES-06](agents-md-changes.md#agentsmdchanges-06--after-compact-the-next-turn-hears-the-notices-again) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. `/compact`: "Compacted my conversation: 33,791 -> 5,021 tokens in 5 s." Replies "Understood. Mars. Noted, Research." and "Understood. Europa. Noted, Research."; step 4 named both rules. Whether the notices were re-told at step 2 is not observable from the UI. Haiku |
| [AGENTSMDCHANGES-07](agents-md-changes.md#agentsmdchanges-07--with-the-helper-off-the-comparison-arrives) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Helper off; ledger Comparison with `+Never use the word "very".` settled at the send; reply had no "very"; step 4 quoted the line; card stayed 8 of 20. Haiku |
| [AGENTSMDCHANGES-08](agents-md-changes.md#agentsmdchanges-08--a-real-restart-then-resume-still-tells-what-was-missed) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Edit made while the app was stopped; settled at start-up (~78 s after launch). "Understood. Oak. Noted, Research. Restart-ok" (Room A), Room B "... Rose. ... Restart-ok", Room A "... Robin ... Restart-ok" with no mention of the file. Card 8 -> 9. Haiku |
| [AGENTSMDCHANGES-09](agents-md-changes.md#agentsmdchanges-09--does-a-cold-resume-honour-the-changed-prompt) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Helper off, app restarted: "Understood. I call the Research Team "the lab." ..." The answer is the notice: the ledger held the Comparison and no Read happened in that Turn. No Turn-detail control in the UI. Haiku |
| [AGENTSMDCHANGES-10](agents-md-changes.md#agentsmdchanges-10--a-save-loop-and-the-cards-call-count) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. 36 saves 5 s apart (23:22:43 to 23:25:39); card 9, 9, 9, then 10 of 20 after quiet; Leo described one net change. Haiku |
| [AGENTSMDCHANGES-11](agents-md-changes.md#agentsmdchanges-11--a-waiting-turn-holds-the-agents-other-room) | 💰 | Pass |  | 2026-11-04 to 05, `0d3dbabe` + uncommitted `feat/agentsmd-changes`. Holding clause Pass by timestamps (8.7 s wait; Room B reply 2.3 s after Room A's). Stop during a wait Pass on the build after the fix. OPEN: Stop just after the prompt was published is not settled. Haiku |
| [AGCONN-01](agent-connectors.md#agconn-01--a-new-agent-has-account-connectors-off) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-02](agent-connectors.md#agconn-02--turning-on-asks-first-and-cancel-leaves-it-off) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-03](agent-connectors.md#agconn-03--give-access-restarts-the-agent-and-records-it-in-the-feed) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-04](agent-connectors.md#agconn-04--turning-off-applies-at-once-and-a-rename-drops-access) |  | Partial |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-05](agent-connectors.md#agconn-05--an-adapter-huddle-cannot-control-shows-text-not-a-switch) |  | Skipped |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-06](agent-connectors.md#agconn-06--the-one-time-notice-shows-after-an-upgrade-and-stays-dismissed) |  | Partial |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-07](agent-connectors.md#agconn-07--a-new-agent-lists-no-claudeai-tool-by-turn-3) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-08](agent-connectors.md#agconn-08--with-the-switch-on-the-connectors-appear-and-every-use-asks-first) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-09](agent-connectors.md#agconn-09--an-agent-cannot-switch-its-own-connectors-on) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-10](agent-connectors.md#agconn-10--a-turn-running-when-the-switch-flips) |  | Partial |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-11](agent-connectors.md#agconn-11--a-connector-call-held-past-the-wait) |  | Partial |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-12](agent-connectors.md#agconn-12--the-switch-flipped-while-a-card-is-open) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
| [AGCONN-13](agent-connectors.md#agconn-13--hostile-arguments) |  | Pass |  | run 2026-11-06 by an agent, see the Run log |
