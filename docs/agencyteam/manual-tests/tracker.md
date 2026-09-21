# Manual test tracker

Where every one of the 484 tests stands. One row per test, updated as you run it.

> [!NOTE]
> **An earlier full run exists, against the previous UI, and its results are deliberately not
> carried into this table.** Between 2026-09-14 and 2026-09-15 the whole suite was executed on
> branch `docs/manual-test-run-2026-09-14` — 444 Pass, 13 Fail, 7 Active — but against the
> pre-MudBlazor UI and the *previous* wording of these scripts, which `394baa9` rewrote. Same test
> ids, different tests: those verdicts do not validate the scripts in this repository now, so every
> row here is Active until someone re-runs it.
>
> That run was not wasted and is worth reading before you start. Its detailed per-test notes are in
> git at `553a107` (`git show 553a107:docs/agencyteam/manual-tests/tracker.md`), it closed 17
> issues, and the behaviour it found that no test asks about is written up in
> [product-observations.md](../product-observations.md), which carries a status line per entry
> checked against `main`.

This is a **recording** page, like [Planning](planning.md) — you need it when you start a
run and when you finish one, not while executing. Keep [the script](../manual-tests.md),
[Common procedures](common.md) and one area file open for that.

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

| Area | Tests | 💰 | Table |
| --- | --- | --- | --- |
| [Application shell, navigation and layout](shell-nav.md) | 29 | — | [below](#shell-nav) |
| [Startup, configuration and first-run state](startup-config.md) | 34 | 2 | [below](#startup-config) |
| [Room view, posting Messages and Transcript rendering](room-messaging.md) | 32 | 1 | [below](#room-messaging) |
| [Turn streaming, Drafts, Stop and failure surfacing](streaming-turn.md) | 30 | 8 | [below](#streaming-turn) |
| [Reply Gate, Mentions and Room Budget](reply-gate-budget.md) | 36 | 6 | [below](#reply-gate-budget) |
| [Teammates page: tiles, Teams grouping, filter and rejected files](teammates-library.md) | 40 | 1 | [below](#teammates-library) |
| [Teammate card: view, edit, create, delete](teammate-card.md) | 50 | 1 | [below](#teammate-card) |
| [Model and Effort pickers, catalog probe and runner restart](model-effort.md) | 27 | 1 | [below](#model-effort) |
| [Creating Rooms, inviting Agents, Room naming](invite-rooms.md) | 32 | 4 | [below](#invite-rooms) |
| [Settings: the 24 Hooks, editing, per-field reset and Save](hooks-settings.md) | 40 | 3 | [below](#hooks-settings) |
| [Appearance tab, Themes, Tokens and overrides](appearance-theme.md) | 25 | — | [below](#appearance-theme) |
| [The named pipe: external agents and the wire](pipe-external.md) | 36 | — | [below](#pipe-external) |
| [App Tools a real model calls (progressive discovery)](app-tools.md) | 26 | 20 | [below](#app-tools) |
| [Persona lifecycle: supervisor, work dirs, health and restarts](persona-lifecycle.md) | 32 | 7 | [below](#persona-lifecycle) |

---

## shell-nav

Application shell, navigation and layout — [area file](shell-nav.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [SHELLNAV-01](shell-nav.md#shellnav-01-every-stylesheet-the-shell-links-is-fingerprinted-except-the-one-that-is-deliberately-static-and-actually-serves) |  | Pass | |  |
| [SHELLNAV-02](shell-nav.md#shellnav-02-every-shell-script-is-fingerprinted-except-mudblazors-own-and-serves---the-gap-the-automated-guard-does-not-cover) |  | Active | |  |
| [SHELLNAV-03](shell-nav.md#shellnav-03-the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold) |  | Active | |  |
| [SHELLNAV-04](shell-nav.md#shellnav-04-get-redirects-to-the-oldest-room) |  | Active | |  |
| [SHELLNAV-05](shell-nav.md#shellnav-05-roomsunknown-id-returns-200-and-shows-the-shared-empty-state-while-the-sidebar-still-lists-rooms) |  | Active | |  |
| [SHELLNAV-06](shell-nav.md#shellnav-06-an-unmatched-route-returns-a-bare-http-404-with-a-zero-byte-body) |  | Active | |  |
| [SHELLNAV-07](shell-nav.md#shellnav-07-routes-are-case-insensitive) |  | Active | |  |
| [SHELLNAV-08](shell-nav.md#shellnav-08-the-drawer-is-present-identical-and-fixed-width-on-every-route) |  | Active | |  |
| [SHELLNAV-09](shell-nav.md#shellnav-09-the-sidebar-marks-exactly-one-room-active-with-aria-current) |  | Active | |  |
| [SHELLNAV-10](shell-nav.md#shellnav-10-teammates-renders-and-a-bare-page-load-spawns-no-node-process) |  | Active | |  |
| [SHELLNAV-11](shell-nav.md#shellnav-11-settings-renders-the-two-button-tab-rail-and-defaults-to-hooks) |  | Active | |  |
| [SHELLNAV-12](shell-nav.md#shellnav-12-clicking-a-settings-tab-changes-the-url-and-the-url-round-trips-as-a-bookmark) |  | Active | |  |
| [SHELLNAV-13](shell-nav.md#shellnav-13-an-unrecognised-or-miscased-tab-segment-silently-falls-back-to-hooks) |  | Active | |  |
| [SHELLNAV-14](shell-nav.md#shellnav-14-no-route-sets-a-browser-tab-title) |  | Active | |  |
| [SHELLNAV-15](shell-nav.md#shellnav-15-focus-moves-to-the-pages-h1-after-every-navigation) |  | Active | |  |
| [SHELLNAV-16](shell-nav.md#shellnav-16-sidebar-links-use-enhanced-navigation-not-a-full-page-reload) |  | Active | |  |
| [SHELLNAV-17](shell-nav.md#shellnav-17-the-new-chat-panel-toggles-lists-agents-with-status-dots-and-gates-start-chat) |  | Active | |  |
| [SHELLNAV-18](shell-nav.md#shellnav-18-starting-a-chat-creates-a-room-named-after-its-agents-and-navigates-straight-to-it) |  | Active | |  |
| [SHELLNAV-19](shell-nav.md#shellnav-19-appjs-is-proven-functionally-enter-sends-and-clears-and-the-transcript-scrolls) |  | Active | |  |
| [SHELLNAV-20](shell-nav.md#shellnav-20-the-room-list-updates-live-with-no-refresh-when-a-room-appears-from-outside-the-app) |  | Active | |  |
| [SHELLNAV-21](shell-nav.md#shellnav-21-retired-choosing-a-theme-layering-a-fourth-stylesheet-after-the-base-one-and-reloading-the-page) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - theme CSS cascade/reload test, no successor. See area file. |
| [SHELLNAV-22](shell-nav.md#shellnav-22-retired-a-hand-edited-token-override-injected-as-an-inline-style-after-both-stylesheet-links) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - per-token override injection test, no successor. See area file. |
| [SHELLNAV-23](shell-nav.md#shellnav-23-a-bad-appearancejson-does-not-break-the-shell-no-theme-link-no-blank-page-only-a-log-warning) |  | Active | |  |
| [SHELLNAV-24](shell-nav.md#shellnav-24-a-deep-link-renders-complete-content-on-a-cold-first-request-before-any-circuit-attaches) |  | Active | |  |
| [SHELLNAV-25](shell-nav.md#shellnav-25-two-browser-tabs-on-the-same-install-stay-in-step-through-the-shell) |  | Active | |  |
| [SHELLNAV-26](shell-nav.md#shellnav-26-the-reconnect-modal-shows-exactly-one-state-paragraph-at-a-time-when-the-server-goes-away) |  | Active | |  |
| [SHELLNAV-27](shell-nav.md#shellnav-27-with-no-rooms-the-empty-state-renders-twice-and-there-is-no-heading-at-all) |  | Active | |  |
| [SHELLNAV-28](shell-nav.md#shellnav-28-the-new-chat-panel-with-no-agents-shows-its-own-guidance-instead-of-an-empty-list) |  | Active | |  |
| [SHELLNAV-29](shell-nav.md#shellnav-29-record-whether-the-yellow-error-band-ever-appears-during-a-genuine-circuit-fault) |  | Active | |  |
| [SHELLNAV-30](shell-nav.md#shellnav-30-the-drawers-icons-and-settings-pinned-to-the-bottom) |  | Active | |  |

## startup-config

Startup, configuration and first-run state — [area file](startup-config.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [STARTUPCONFIG-01](startup-config.md#startupconfig-01-the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100) |  | Pass | |  |
| [STARTUPCONFIG-02](startup-config.md#startupconfig-02-every-stylesheet-in-the-document-head-returns-200-the-silent-assets-regression) |  | Active | | |
| [STARTUPCONFIG-03](startup-config.md#startupconfig-03-two-demo-rooms-echo-and-alpha-exist-at-startup-with-no-user-action) |  | Pass | |  |
| [STARTUPCONFIG-04](startup-config.md#startupconfig-04-a-demo-agent-answers-a-two-member-room-with-or-without-a-mention-and-answers-in-bold) |  | Pass | |  |
| [STARTUPCONFIG-05](startup-config.md#startupconfig-05-the-demo-agent-streams-a-draft-before-posting-the-real-streaming-path-free) |  | Active | | |
| [STARTUPCONFIG-06](startup-config.md#startupconfig-06-every-navigation-surface-is-reachable-from-a-cold-start) |  | Active | | |
| [STARTUPCONFIG-07](startup-config.md#startupconfig-07-start-chat-on-one-known-agent-reuses-its-room-two-agents-create-one-new-named-room) |  | Active | | |
| [STARTUPCONFIG-08](startup-config.md#startupconfig-08-teammates-lists-personas-only-demo-agents-and-pipe-clients-never-appear-there) |  | Active | | |
| [STARTUPCONFIG-09](startup-config.md#startupconfig-09-settings-hooks-tab-on-a-first-run-says-hooksjson-is-absent-and-offers-nothing-to-save-or-reset) |  | Active | | |
| [STARTUPCONFIG-10](startup-config.md#startupconfig-10-appearance-tab-on-a-first-run-reads-system-picking-dark-writes-appearancejson-and-reloads-the-page) |  | Active | | |
| [STARTUPCONFIG-11](startup-config.md#startupconfig-11-rooms-and-transcripts-survive-a-restart-drafts-and-the-budget-do-not) |  | Active | | |
| [STARTUPCONFIG-12](startup-config.md#startupconfig-12-deleting-app_data-while-running-fails-stopping-first-makes-the-clean-slate-reliable) |  | Active | | |
| [STARTUPCONFIG-13](startup-config.md#startupconfig-13-team__agentmessagebudget1-pauses-the-room-after-one-agent-reply-and-shows-the-continue-prompt) |  | Active | | |
| [STARTUPCONFIG-14](startup-config.md#startupconfig-14-continue-grants-budget-and-produces-no-new-reply-the-trap-that-reads-as-a-dead-button) |  | Active | | |
| [STARTUPCONFIG-15](startup-config.md#startupconfig-15-team__agentmessagebudget0-removes-the-guard-entirely-and-hides-every-budget-surface-a-restart-un-pauses-everything) |  | Active | | |
| [STARTUPCONFIG-16](startup-config.md#startupconfig-16-team__demoagent__enabledfalse-on-a-clean-app_data-leaves-the-app-with-no-rooms-and-no-agents) |  | Active | | |
| [STARTUPCONFIG-17](startup-config.md#startupconfig-17-a-mis-spelled-configuration-key-changes-nothing-and-reports-nothing) |  | Active | | |
| [STARTUPCONFIG-18](startup-config.md#startupconfig-18-disabling-the-demo-agents-against-an-existing-app_data-keeps-the-rooms-but-leaves-them-dead) |  | Active | | |
| [STARTUPCONFIG-19](startup-config.md#startupconfig-19-team__demoagent__names-changes-which-demo-rooms-exist-and-must-replace-rather-than-append) |  | Active | | |
| [STARTUPCONFIG-20](startup-config.md#startupconfig-20-team__datadir-points-the-whole-application-at-a-different-folder-a-clean-slate-without-deleting-anything) |  | Active | | |
| [STARTUPCONFIG-21](startup-config.md#startupconfig-21-team__humanname-renames-the-human-everywhere-new-while-old-transcript-lines-keep-the-old-name) |  | Active | | |
| [STARTUPCONFIG-22](startup-config.md#startupconfig-22-the-renamed-team__acp__personadir-key-refuses-to-start-the-app-rather-than-scanning-nothing) |  | Active | | |
| [STARTUPCONFIG-23](startup-config.md#startupconfig-23-an-external-pipe-client-creates-a-room-live-with-no-page-refresh) |  | Active | | |
| [STARTUPCONFIG-24](startup-config.md#startupconfig-24-team__pipename-changes-the-pipe-and-silently-orphans-any-external-client) |  | Active | | |
| [STARTUPCONFIG-25](startup-config.md#startupconfig-25-two-app-instances-sharing-one-pipe-name-cross-wire-the-demo-agents) |  | Active | | |
| [STARTUPCONFIG-26](startup-config.md#startupconfig-26-environment-selection-decides-whether-acp-is-on-and-which-port-is-used) |  | Active | | |
| [STARTUPCONFIG-27](startup-config.md#startupconfig-27-team__acp__enabledfalse-starts-no-agent-process-and-costs-nothing-while-teammates-still-works-fully) |  | Active | | |
| [STARTUPCONFIG-28](startup-config.md#startupconfig-28-team__acp__enabledtrue-with-zero-personas-still-starts-no-process) |  | Active | | |
| [STARTUPCONFIG-29](startup-config.md#startupconfig-29-a-persona-file-under-a-team-sub-folder-is-discovered-reloads-on-edit-in-place-and-a-broken-file-is-named-rather-than-silently-dropped) |  | Active | | |
| [STARTUPCONFIG-30](startup-config.md#startupconfig-30-a-stale-loggingloglevel-key-silently-removes-the-console-evidence-other-tests-rely-on) |  | Active | | |
| [STARTUPCONFIG-31](startup-config.md#startupconfig-31-with-acp-on-and-one-persona-present-that-persona-starts-at-launch-and-its-tile-goes-starting-then-online) |  | Active | | |
| [STARTUPCONFIG-32](startup-config.md#startupconfig-32-team__acp__tracewiretrue-dumps-the-tool-servers-bearer-token-to-the-console) |  | Active | | |
| [STARTUPCONFIG-33](startup-config.md#startupconfig-33-a-spent-per-persona-token-budget-reads-as-degraded-on-the-teammate-tile-and-in-the-room-banner) | 💰 | Active | | |
| [STARTUPCONFIG-34](startup-config.md#startupconfig-34-a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again) | 💰 | Active | | |

## room-messaging

Room view, posting Messages and Transcript rendering — [area file](room-messaging.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [ROOMMESSAGING-01](room-messaging.md#roommessaging-01-landing-on-redirects-into-the-first-room-and-roomsid-deep-links) |  | Active | | |
| [ROOMMESSAGING-02](room-messaging.md#roommessaging-02-enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line) |  | Pass | |  |
| [ROOMMESSAGING-03](room-messaging.md#roommessaging-03-shiftenter-inserts-a-newline-and-does-not-send) |  | Active | | Inconclusive: browser automation cannot insert a newline into any textarea, so the "two lines" condition is unanswerable. Shift+Enter posted nothing, and a newline survived storage as a two-character escape in the JSONL. |
| [ROOMMESSAGING-04](room-messaging.md#roommessaging-04-an-empty-or-whitespace-only-enter-posts-nothing-and-shows-no-error) |  | Active | | |
| [ROOMMESSAGING-05](room-messaging.md#roommessaging-05-leading-and-trailing-whitespace-is-trimmed-before-the-message-is-stored) |  | Active | | |
| [ROOMMESSAGING-06](room-messaging.md#roommessaging-06-a-soft-line-break-collapses-to-a-space-a-blank-line-makes-two-paragraphs-a-list-still-works) |  | Active | | |
| [ROOMMESSAGING-07](room-messaging.md#roommessaging-07-core-markdown-renders-bold-italic-heading-lists-blockquote-inline-and-fenced-code-rule) |  | Active | | |
| [ROOMMESSAGING-08](room-messaging.md#roommessaging-08-the-four-enabled-markdig-extensions-render-pipe-tables-task-lists-autolinks-emphasis-extras) |  | Active | | |
| [ROOMMESSAGING-09](room-messaging.md#roommessaging-09-advanced-markdown-extensions-are-absent-generic-attributes-stay-literal-and-never-become-live-handlers) |  | Active | | |
| [ROOMMESSAGING-10](room-messaging.md#roommessaging-10-raw-html-inside-a-message-is-escaped-never-executed) |  | Active | | |
| [ROOMMESSAGING-11](room-messaging.md#roommessaging-11-link-schemes-are-rewritten-only-http-https-and-mailto-survive) |  | Active | | |
| [ROOMMESSAGING-12](room-messaging.md#roommessaging-12-non-ascii-and-html-sensitive-characters-are-escaped-in-the-jsonl-but-render-correctly-on-screen) |  | Active | | |
| [ROOMMESSAGING-13](room-messaging.md#roommessaging-13-a-long-message-a-very-long-unbroken-token-and-a-wide-code-block) |  | Active | | |
| [ROOMMESSAGING-14](room-messaging.md#roommessaging-14-messages-render-in-post-order-and-the-order-on-screen-equals-the-order-in-the-file) |  | Active | | |
| [ROOMMESSAGING-15](room-messaging.md#roommessaging-15-the-transcript-survives-a-browser-reload-and-is-present-in-the-prerendered-html) |  | Active | | |
| [ROOMMESSAGING-16](room-messaging.md#roommessaging-16-sender-name-and-timestamp-in-the-message-meta-line) |  | Active | | |
| [ROOMMESSAGING-17](room-messaging.md#roommessaging-17-the-transcript-autoscrolls-on-a-new-message-and-while-a-draft-grows) |  | Active | | |
| [ROOMMESSAGING-18](room-messaging.md#roommessaging-18-a-demo-echo-reply-streams-as-a-plain-text-draft-and-settles-into-rendered-markdown) |  | Active | | |
| [ROOMMESSAGING-19](room-messaging.md#roommessaging-19-the-echo-reply-strips-every-from-the-quoted-text-loop-safety) |  | Active | | |
| [ROOMMESSAGING-20](room-messaging.md#roommessaging-20-the-demo-agent-replies-only-to-a-real-mention-at-a-word-boundary) |  | Active | | |
| [ROOMMESSAGING-21](room-messaging.md#roommessaging-21-invite-name-from-the-composer-green-info-line-room-rename-live-sidebar-update) |  | Active | | |
| [ROOMMESSAGING-22](room-messaging.md#roommessaging-22-composer-errors-unknown-command-unknown-agent-and-the-typed-text-is-lost) |  | Active | | |
| [ROOMMESSAGING-23](room-messaging.md#roommessaging-23-typed-but-unsent-composer-text-carries-over-into-the-room-you-switch-to) |  | Active | | |
| [ROOMMESSAGING-24](room-messaging.md#roommessaging-24-the-jsonl-transcript-shape-append-only-and-one-file-per-room) |  | Active | | |
| [ROOMMESSAGING-25](room-messaging.md#roommessaging-25-an-external-pipe-clients-room-and-messages-appear-live-with-no-refresh) |  | Active | | |
| [ROOMMESSAGING-26](room-messaging.md#roommessaging-26-a-mention-of-a-name-containing-spaces-resolves-against-the-rooms-members) |  | Active | | |
| [ROOMMESSAGING-27](room-messaging.md#roommessaging-27-appearancejson-no-longer-customises-the-message-font-the-body-just-follows-the-theme-and-a-legacy-override-key-is-silently-inert) |  | Active | | |
| [ROOMMESSAGING-28](room-messaging.md#roommessaging-28-a-torn-or-corrupt-line-in-the-transcript-is-skipped-with-a-warning-not-fatal) |  | Active | | |
| [ROOMMESSAGING-29](room-messaging.md#roommessaging-29-the-transcript-survives-an-application-restart-drafts-and-the-budget-do-not) |  | Active | | |
| [ROOMMESSAGING-30](room-messaging.md#roommessaging-30-the-budget-pause-panel-sits-between-transcript-and-composer-and-a-human-message-resumes-it) |  | Active | | |
| [ROOMMESSAGING-31](room-messaging.md#roommessaging-31-the-room-view-survives-a-server-stop-and-restart-with-the-browser-open) |  | Active | | |
| [ROOMMESSAGING-32](room-messaging.md#roommessaging-32-a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money) | 💰 | Active | | |

## streaming-turn

Turn streaming, Drafts, Stop and failure surfacing — [area file](streaming-turn.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [STREAMINGTURN-01](streaming-turn.md#streamingturn-01-in-the-stock-configuration-no-room-shows-a-member-health-alert-strip) |  | Active | | |
| [STREAMINGTURN-02](streaming-turn.md#streamingturn-02-a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written) |  | Pass | |  |
| [STREAMINGTURN-03](streaming-turn.md#streamingturn-03-draft-text-renders-as-plain-text-and-becomes-markdown-only-when-the-message-lands) |  | Active | | |
| [STREAMINGTURN-04](streaming-turn.md#streamingturn-04-the-draft-is-replaced-by-exactly-one-message-and-never-shown-twice) |  | Active | | |
| [STREAMINGTURN-05](streaming-turn.md#streamingturn-05-the-stop-button-exists-beside-a-live-draft-and-nowhere-else-styled-as-an-ordinary-secondary-action) |  | Active | | |
| [STREAMINGTURN-06](streaming-turn.md#streamingturn-06-a-page-reload-mid-turn-shows-the-whole-draft-so-far-not-an-orphaned-suffix) |  | Active | | |
| [STREAMINGTURN-07](streaming-turn.md#streamingturn-07-two-browser-tabs-on-the-same-room-show-and-update-the-same-draft) |  | Active | | |
| [STREAMINGTURN-08](streaming-turn.md#streamingturn-08-a-draft-in-one-room-never-leaks-into-another-and-survives-navigating-away-and-back) |  | Active | | |
| [STREAMINGTURN-09](streaming-turn.md#streamingturn-09-draft-rows-always-render-below-every-message-whatever-the-true-chronology) |  | Active | | |
| [STREAMINGTURN-10](streaming-turn.md#streamingturn-10-the-message-list-auto-scrolls-as-a-draft-grows-not-only-when-a-message-arrives) |  | Active | | |
| [STREAMINGTURN-11](streaming-turn.md#streamingturn-11-a-draft-dies-with-its-agents-connection---no-frozen-half-reply-is-left-behind) |  | Active | | |
| [STREAMINGTURN-12](streaming-turn.md#streamingturn-12-tool-activity-shows-as-one-muted-italic-line-inside-the-draft-and-is-never-persisted) |  | Active | | |
| [STREAMINGTURN-13](streaming-turn.md#streamingturn-13-a-draft-written-for-a-room-the-sender-is-not-a-member-of-is-refused-and-nothing-renders) |  | Active | | |
| [STREAMINGTURN-14](streaming-turn.md#streamingturn-14-two-agents-stream-into-one-room-at-once-and-neither-draft-is-dropped) |  | Active | | |
| [STREAMINGTURN-15](streaming-turn.md#streamingturn-15-stop-disables-itself-while-the-request-is-in-flight-per-agent-rather-than-per-room) |  | Active | | |
| [STREAMINGTURN-16](streaming-turn.md#streamingturn-16-clicking-stop-on-an-agent-with-no-live-connection-is-a-silent-no-op) |  | Active | | |
| [STREAMINGTURN-17](streaming-turn.md#streamingturn-17-a-stop-click-reaches-the-wire-even-when-the-client-ignores-it) |  | Active | | |
| [STREAMINGTURN-18](streaming-turn.md#streamingturn-18-a-draft-stops-growing-at-256-kb-but-the-posted-message-still-arrives-intact) |  | Active | | |
| [STREAMINGTURN-19](streaming-turn.md#streamingturn-19-restarting-the-application-loses-every-draft-in-flight-and-nothing-resurrects-as-a-message) |  | Active | | |
| [STREAMINGTURN-20](streaming-turn.md#streamingturn-20-a-spent-room-budget-shows-the-continue-prompt-and-never-a-health-strip) |  | Active | | |
| [STREAMINGTURN-21](streaming-turn.md#streamingturn-21-a-failed-persona-start-surfaces-as-a-rolealert-strip-in-every-room-it-is-a-member-of) |  | Active | | |
| [STREAMINGTURN-22](streaming-turn.md#streamingturn-22-restart-from-the-teammate-card-clears-the-failure-and-the-rooms-strip-goes-with-it) |  | Active | | |
| [STREAMINGTURN-23](streaming-turn.md#streamingturn-23-a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it) | 💰 | Active | | |
| [STREAMINGTURN-24](streaming-turn.md#streamingturn-24-stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written) | 💰 | Active | | |
| [STREAMINGTURN-25](streaming-turn.md#streamingturn-25-a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak) | 💰 | Active | | |
| [STREAMINGTURN-26](streaming-turn.md#streamingturn-26-stop-means-this-agent-now-everything-queued-behind-the-live-turn-is-discarded-too) | 💰 | Active | | |
| [STREAMINGTURN-27](streaming-turn.md#streamingturn-27-stopping-an-agent-stops-it-in-every-room-not-just-the-one-you-clicked-in) | 💰 | Active | | |
| [STREAMINGTURN-28](streaming-turn.md#streamingturn-28-a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it) | 💰 | Active | | |
| [STREAMINGTURN-29](streaming-turn.md#streamingturn-29-a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row) | 💰 | Active | | |
| [STREAMINGTURN-30](streaming-turn.md#streamingturn-30-killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever) | 💰 | Active | | |

## reply-gate-budget

Reply Gate, Mentions and Room Budget — [area file](reply-gate-budget.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [REPLYGATEBUDGET-01](reply-gate-budget.md#replygatebudget-01-a-demo-two-member-room-answers-with-or-without-a-mention) |  | Pass |  | Re-run 2026-09-14 on the corrected step-8 expectation: body read `echo: hello echo`, `echo:` bold, no `@`. Silence held 15s on the bare `hello`; transcript held exactly three lines. |
| [REPLYGATEBUDGET-02](reply-gate-budget.md#replygatebudget-02-three-or-more-members-makes-the-same-room-mention-gated) |  | Pass | |  |
| [REPLYGATEBUDGET-03](reply-gate-budget.md#replygatebudget-03-invite-from-the-composer-and-its-two-error-strips-never-touch-the-transcript) |  | Active | | |
| [REPLYGATEBUDGET-04](reply-gate-budget.md#replygatebudget-04-a-mention-is-case-insensitive-and-ends-at-trailing-punctuation) |  | Active | | |
| [REPLYGATEBUDGET-05](reply-gate-budget.md#replygatebudget-05-repeating-a-mention-wakes-an-agent-once-and-the-human-can-be-mentioned-harmlessly) |  | Active | | |
| [REPLYGATEBUDGET-06](reply-gate-budget.md#replygatebudget-06-the-budget-note-counts-agent-replies-and-a-human-message-resets-it-to-nothing) |  | Active | | |
| [REPLYGATEBUDGET-07](reply-gate-budget.md#replygatebudget-07-a-spent-budget-halts-the-room-and-asks-the-human-visibly) |  | Active | | |
| [REPLYGATEBUDGET-08](reply-gate-budget.md#replygatebudget-08-the-write-path-refuses-an-over-budget-post-two-agents-are-woken-only-one-message-lands) |  | Active | | |
| [REPLYGATEBUDGET-09](reply-gate-budget.md#replygatebudget-09-leave-paused-hides-the-question-but-keeps-the-pause-legible) |  | Active | | |
| [REPLYGATEBUDGET-10](reply-gate-budget.md#replygatebudget-10-a-dismissal-is-per-view-navigation-reload-and-the-next-message-all-bring-the-question-back) |  | Active | | |
| [REPLYGATEBUDGET-11](reply-gate-budget.md#replygatebudget-11-the-pause-survives-a-reload-and-shows-in-a-brand-new-tab) |  | Active | | |
| [REPLYGATEBUDGET-12](reply-gate-budget.md#replygatebudget-12-continue-grants-exactly-one-more-budget-once-per-click-and-duplicates-nothing-on-screen) |  | Active | | |
| [REPLYGATEBUDGET-13](reply-gate-budget.md#replygatebudget-13-any-human-message-resets-the-budget-is-never-refused-and-expires-an-earlier-grant) |  | Active | | |
| [REPLYGATEBUDGET-14](reply-gate-budget.md#replygatebudget-14-the-budget-is-per-room-a-paused-room-does-not-starve-the-one-beside-it) |  | Active | | |
| [REPLYGATEBUDGET-15](reply-gate-budget.md#replygatebudget-15-a-restart-un-pauses-every-room-over-a-transcript-that-already-spent-its-budget) |  | Active | | |
| [REPLYGATEBUDGET-16](reply-gate-budget.md#replygatebudget-16-a-budget-of-zero-removes-the-cap-entirely) |  | Active | | |
| [REPLYGATEBUDGET-17](reply-gate-budget.md#replygatebudget-17-the-model-facing-wording-of-this-area-is-readable-and-editable-at-settingshooks) |  | Active | | |
| [REPLYGATEBUDGET-18](reply-gate-budget.md#replygatebudget-18-the-server-labels-a-delivery-correctly-even-when-the-client-chooses-to-stay-silent) |  | Active | | |
| [REPLYGATEBUDGET-19](reply-gate-budget.md#replygatebudget-19-the-longest-handle-wins-emily-lee-reaches-emily-lee-never-emily) |  | Active | | |
| [REPLYGATEBUDGET-20](reply-gate-budget.md#replygatebudget-20-a-mention-falls-back-to-the-shorter-name-when-the-longer-one-is-not-in-this-room) |  | Active | | |
| [REPLYGATEBUDGET-21](reply-gate-budget.md#replygatebudget-21-two-mentions-in-one-message-do-not-run-together-and-a-multi-word-name-stops-at-punctuation) |  | Active | | |
| [REPLYGATEBUDGET-22](reply-gate-budget.md#replygatebudget-22-a-mention-ends-at-a-word-boundary-and-an-email-address-is-not-a-mention) |  | Active | | |
| [REPLYGATEBUDGET-23](reply-gate-budget.md#replygatebudget-23-an-agent-never-receives-its-own-message-so-no-self-echo-loop-can-start) |  | Active | | |
| [REPLYGATEBUDGET-24](reply-gate-budget.md#replygatebudget-24-the-refusal-an-over-budget-agent-reads-is-worded-as-terminal-on-the-pipe-door) |  | Active | | |
| [REPLYGATEBUDGET-25](reply-gate-budget.md#replygatebudget-25-continue-re-delivers-the-last-message-to-the-other-agents-once-with-the-raised-budget) |  | Active | | |
| [REPLYGATEBUDGET-26](reply-gate-budget.md#replygatebudget-26-continue-in-a-two-member-room-grants-budget-and-wakes-nobody-and-that-is-correct) |  | Active | | |
| [REPLYGATEBUDGET-27](reply-gate-budget.md#replygatebudget-27-an-alias-resolves-a-mention-to-the-persona-that-owns-it) |  | Active | | |
| [REPLYGATEBUDGET-28](reply-gate-budget.md#replygatebudget-28-a-longer-alias-beats-a-shorter-name-and-an-equal-length-name-beats-an-alias) |  | Active | | |
| [REPLYGATEBUDGET-29](reply-gate-budget.md#replygatebudget-29-reconnecting-re-attaches-to-the-same-room-but-after-an-invite-a-fresh-two-member-room-appears) |  | Active | | |
| [REPLYGATEBUDGET-30](reply-gate-budget.md#replygatebudget-30-a-mention-that-reaches-nobody-is-completely-silent-and-how-to-tell-that-apart-from-a-bug) |  | Active | | |
| [REPLYGATEBUDGET-31](reply-gate-budget.md#replygatebudget-31-paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all) | 💰 | Active | | |
| [REPLYGATEBUDGET-32](reply-gate-budget.md#replygatebudget-32-paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention) | 💰 | Active | | |
| [REPLYGATEBUDGET-33](reply-gate-budget.md#replygatebudget-33-paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn) | 💰 | Active | | |
| [REPLYGATEBUDGET-34](reply-gate-budget.md#replygatebudget-34-paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up) | 💰 | Active | | |
| [REPLYGATEBUDGET-35](reply-gate-budget.md#replygatebudget-35-paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it) | 💰 | Active | | |
| [REPLYGATEBUDGET-36](reply-gate-budget.md#replygatebudget-36-paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it) | 💰 | Active | | |

## teammates-library

Teammates page: tiles, Teams grouping, filter and rejected files — [area file](teammates-library.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TEAMMATESLIBRARY-01](teammates-library.md#teammateslibrary-01-the-page-loads-is-styled-and-spawns-nothing) |  | Pass | |  |
| [TEAMMATESLIBRARY-02](teammates-library.md#teammateslibrary-02-empty-library-shows-its-sentence-and-no-team-filter) |  | Active | | |
| [TEAMMATESLIBRARY-03](teammates-library.md#teammateslibrary-03-the-fixture-library-loads-and-one-tile-shows-monogram-name-title-alias-and-status) |  | Active | | |
| [TEAMMATESLIBRARY-04](teammates-library.md#teammateslibrary-04-one-heading-per-team-ordinal-order-no-team-last-no-empty-groups) |  | Active | | |
| [TEAMMATESLIBRARY-05](teammates-library.md#teammateslibrary-05-a-persona-in-two-teams-renders-in-full-under-both-headings) |  | Active | | |
| [TEAMMATESLIBRARY-06](teammates-library.md#teammateslibrary-06-folders-are-cosmetic-a-nested-file-is-grouped-by-its-field-not-its-folder) |  | Active | | |
| [TEAMMATESLIBRARY-07](teammates-library.md#teammateslibrary-07-the-team-filters-label-options-order-and-de-duplication) |  | Active | | |
| [TEAMMATESLIBRARY-08](teammates-library.md#teammateslibrary-08-choosing-a-team-narrows-to-one-heading-and-drops-the-no-team-personas) |  | Active | | |
| [TEAMMATESLIBRARY-09](teammates-library.md#teammateslibrary-09-a-team-whose-membership-drops-to-zero-says-so) |  | Active | | |
| [TEAMMATESLIBRARY-10](teammates-library.md#teammateslibrary-10-team-names-match-case-insensitively-and-appear-once) |  | Active | | |
| [TEAMMATESLIBRARY-11](teammates-library.md#teammateslibrary-11-all-three-yaml-shapes-of-teams-group-identically-and-the-documented-comma-cost) |  | Active | | |
| [TEAMMATESLIBRARY-12](teammates-library.md#teammateslibrary-12-a-rejected-file-is-named-by-path-and-reason-above-the-tiles-live) |  | Active | | |
| [TEAMMATESLIBRARY-13](teammates-library.md#teammateslibrary-13-every-distinct-rejection-reason-word-for-word) |  | Active | | |
| [TEAMMATESLIBRARY-14](teammates-library.md#teammateslibrary-14-a-duplicate-alias-rejects-both-files-each-naming-the-other) |  | Active | | |
| [TEAMMATESLIBRARY-15](teammates-library.md#teammateslibrary-15-a-duplicate-name-including-one-differing-only-in-case-rejects-both) |  | Active | | |
| [TEAMMATESLIBRARY-16](teammates-library.md#teammateslibrary-16-one-files-alias-equal-to-another-files-name-rejects-both-with-two-different-sentences-and-a-self-matching-alias-is-legal) |  | Active | | |
| [TEAMMATESLIBRARY-17](teammates-library.md#teammateslibrary-17-the-rejected-block-is-ordered-survives-the-filter-and-coexists-with-the-empty-library-sentence) |  | Active | | |
| [TEAMMATESLIBRARY-18](teammates-library.md#teammateslibrary-18-non-markdown-files-are-ignored-entirely-not-loaded-not-rejected) |  | Active | | |
| [TEAMMATESLIBRARY-19](teammates-library.md#teammateslibrary-19-the-watcher-is-live-and-recursive-a-nested-persona-reloads-on-edit) |  | Active | | |
| [TEAMMATESLIBRARY-20](teammates-library.md#teammateslibrary-20-the-watcher-notices-creations-and-deletions-while-the-page-is-open) |  | Active | | |
| [TEAMMATESLIBRARY-21](teammates-library.md#teammateslibrary-21-renaming-or-deleting-a-team-sub-folder-keeps-everyone-inside-reachable) |  | Active | | |
| [TEAMMATESLIBRARY-22](teammates-library.md#teammateslibrary-22-a-burst-of-file-writes-becomes-one-update-not-five) |  | Active | | |
| [TEAMMATESLIBRARY-23](teammates-library.md#teammateslibrary-23-a-dropped-event-storm-still-converges-on-what-is-on-disk) |  | Active | | |
| [TEAMMATESLIBRARY-24](teammates-library.md#teammateslibrary-24-a-tile-click-opens-the-card-and-the-card-shows-the-real-file-path) |  | Active | | |
| [TEAMMATESLIBRARY-25](teammates-library.md#teammateslibrary-25-offline-is-the-honest-default-with-no-invented-reason) |  | Active | | |
| [TEAMMATESLIBRARY-26](teammates-library.md#teammateslibrary-26-online-proved-for-free-through-a-demo-agent) |  | Active | | |
| [TEAMMATESLIBRARY-27](teammates-library.md#teammateslibrary-27-badges-repaint-live-on-connect-and-disconnect-with-no-refresh) |  | Active | | |
| [TEAMMATESLIBRARY-28](teammates-library.md#teammateslibrary-28-moving-a-persona-file-between-team-sub-folders-is-a-complete-no-op) |  | Active | | |
| [TEAMMATESLIBRARY-29](teammates-library.md#teammateslibrary-29-new-teammate-writes-to-the-teams-root-with-the-teams-field-it-was-given) |  | Active | | |
| [TEAMMATESLIBRARY-30](teammates-library.md#teammateslibrary-30-a-save-that-would-create-a-rejected-file-is-refused-before-anything-is-written) |  | Active | | |
| [TEAMMATESLIBRARY-31](teammates-library.md#teammateslibrary-31-renaming-through-the-frontmatter-the-tile-renames-model-and-effort-follow-the-file-does-not) |  | Active | | |
| [TEAMMATESLIBRARY-32](teammates-library.md#teammateslibrary-32-remove-takes-the-file-the-model-and-the-effort-and-nothing-else) |  | Active | | |
| [TEAMMATESLIBRARY-33](teammates-library.md#teammateslibrary-33-open-launches-the-persona-file-on-the-server-including-for-a-nested-file) |  | Active | | |
| [TEAMMATESLIBRARY-34](teammates-library.md#teammateslibrary-34-neither-a-page-load-a-filter-change-nor-a-view-card-ever-spawns-an-adapter) |  | Active | | |
| [TEAMMATESLIBRARY-35](teammates-library.md#teammateslibrary-35-restart-from-the-card-is-not-gated-by-teamacpenabled-judge-and-record) |  | Active | | |
| [TEAMMATESLIBRARY-36](teammates-library.md#teammateslibrary-36-restart-is-busy-flagged-and-cannot-be-double-fired) |  | Active | | |
| [TEAMMATESLIBRARY-37](teammates-library.md#teammateslibrary-37-offline-with-a-reason-in-both-of-its-homes) |  | Active | | |
| [TEAMMATESLIBRARY-38](teammates-library.md#teammateslibrary-38-health-outranks-pipe-liveness-a-live-pipe-with-a-failed-start-must-read-offline) |  | Active | | |
| [TEAMMATESLIBRARY-39](teammates-library.md#teammateslibrary-39-the-whole-page-in-dark-mode-with-a-card-open-and-a-rejected-file-present) |  | Active | | |
| [TEAMMATESLIBRARY-40](teammates-library.md#teammateslibrary-40-degraded-with-the-persistence-reason-and-the-two-things-that-must-not-badge) | 💰 | Active | | |

## teammate-card

Teammate card: view, edit, create, delete — [area file](teammate-card.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [TEAMMATECARD-01](teammate-card.md#teammatecard-01-empty-teammates-page-and-the-new-teammate-entry-point) |  | Active | |  |
| [TEAMMATECARD-02](teammate-card.md#teammatecard-02-clicking-new-teammate-opens-a-card-at-all-blank-name-crash-probe) |  | Active | |  |
| [TEAMMATECARD-03](teammate-card.md#teammatecard-03-create-card-every-label-placeholder-and-hint) |  | Active | |  |
| [TEAMMATECARD-04](teammate-card.md#teammatecard-04-monogram-tracks-the-name-as-you-type) |  | Active | |  |
| [TEAMMATECARD-05](teammate-card.md#teammatecard-05-create-the-exact-name-acceptreject-set) |  | Active | |  |
| [TEAMMATECARD-06](teammate-card.md#teammatecard-06-create-alias-validation-uses-the-same-rules-and-its-own-wording) |  | Active | |  |
| [TEAMMATECARD-07](teammate-card.md#teammatecard-07-create-validation-precedence-which-error-wins) |  | Active | |  |
| [TEAMMATECARD-08](teammate-card.md#teammatecard-08-create-the-title-is-prose-blank-rejected-commas-preserved) |  | Active | |  |
| [TEAMMATECARD-09](teammate-card.md#teammatecard-09-create-a-failed-save-never-throws-away-what-you-typed) |  | Active | |  |
| [TEAMMATECARD-10](teammate-card.md#teammatecard-10-enter-in-an-identity-field-does-not-submit-and-saving-never-reloads-the-page) |  | Active | |  |
| [TEAMMATECARD-11](teammate-card.md#teammatecard-11-a-successful-create-lands-on-the-new-teammates-view-card) |  | Active | |  |
| [TEAMMATECARD-12](teammate-card.md#teammatecard-12-view-card-every-section-and-action-in-order) |  | Active | |  |
| [TEAMMATECARD-13](teammate-card.md#teammatecard-13-view-card-agent-default-and-model-default-when-nothing-is-stored) |  | Active | |  |
| [TEAMMATECARD-14](teammate-card.md#teammatecard-14-closing-the-dialog-focus-trap-on-open-escape-the-close-icon-cancel-scroll-locking-and-focus-return) |  | Active | |  |
| [TEAMMATECARD-15](teammate-card.md#teammatecard-15-closing-a-card-clears-every-bit-of-its-state) |  | Active | |  |
| [TEAMMATECARD-16](teammate-card.md#teammatecard-16-remove-is-a-two-step-confirm-and-cancel-backs-out-cleanly) |  | Active | |  |
| [TEAMMATECARD-17](teammate-card.md#teammatecard-17-team-headings-and-the-team-filter-come-from-the-teams-field-not-the-folder) |  | Active | |  |
| [TEAMMATECARD-18](teammate-card.md#teammatecard-18-create-exactly-what-lands-on-disk) |  | Active | |  |
| [TEAMMATECARD-19](teammate-card.md#teammatecard-19-create-an-apostrophe-in-the-title-round-trips-through-yaml-quoting) |  | Active | |  |
| [TEAMMATECARD-20](teammate-card.md#teammatecard-20-create-the-teams-field-trims-drops-blanks-and-collapses-duplicates-on-read-back) |  | Active | |  |
| [TEAMMATECARD-21](teammate-card.md#teammatecard-21-create-a-duplicate-filename-is-caught-before-anything-is-written) |  | Active | |  |
| [TEAMMATECARD-22](teammate-card.md#teammatecard-22-edit-card-what-is-editable-and-what-deliberately-is-not) |  | Active | |  |
| [TEAMMATECARD-23](teammate-card.md#teammatecard-23-edit-save-writes-back-to-the-same-file-path-never-a-rename-never-a-move) |  | Active | |  |
| [TEAMMATECARD-24](teammate-card.md#teammatecard-24-edit-breaking-the-frontmatter-is-refused-with-the-file-untouched) |  | Active | |  |
| [TEAMMATECARD-25](teammate-card.md#teammatecard-25-edit-renaming-into-a-collision-is-refused-with-the-file-untouched) |  | Active | |  |
| [TEAMMATECARD-26](teammate-card.md#teammatecard-26-edit-renaming-via-the-name-field-leaves-a-ghost-card) |  | Active | |  |
| [TEAMMATECARD-27](teammate-card.md#teammatecard-27-create-a-name-colliding-with-a-persona-in-a-sub-folder) |  | Active | |  |
| [TEAMMATECARD-28](teammate-card.md#teammatecard-28-create-alias-collisions-and-the-one-that-is-legal) |  | Active | |  |
| [TEAMMATECARD-29](teammate-card.md#teammatecard-29-files-that-didnt-load-names-the-path-and-the-reason-above-the-list) |  | Active | |  |
| [TEAMMATECARD-30](teammate-card.md#teammatecard-30-an-external-file-edit-repaints-the-page-including-inside-sub-folders) |  | Active | |  |
| [TEAMMATECARD-31](teammate-card.md#teammatecard-31-a-card-whose-file-vanished-underneath-it-reports-it-instead-of-crashing) |  | Active | |  |
| [TEAMMATECARD-32](teammate-card.md#teammatecard-32-open-launches-the-os-editor-on-the-server) |  | Active | |  |
| [TEAMMATECARD-33](teammate-card.md#teammatecard-33-a-windows-reserved-device-name-as-a-name-documented-limit-record-the-outcome) |  | Active | |  |
| [TEAMMATECARD-34](teammate-card.md#teammatecard-34-two-browser-tabs-on-teammates-stay-in-step) |  | Active | |  |
| [TEAMMATECARD-35](teammate-card.md#teammatecard-35-remove-deletes-the-file-and-both-stored-settings) |  | Active | |  |
| [TEAMMATECARD-36](teammate-card.md#teammatecard-36-a-stored-model-or-effort-the-catalog-does-not-advertise-survives-an-edit) |  | Active | |  |
| [TEAMMATECARD-37](teammate-card.md#teammatecard-37-the-model-probe-never-runs-on-a-plain-page-load) |  | Active | |  |
| [TEAMMATECARD-38](teammate-card.md#teammatecard-38-model-and-effort-pickers-labels-loading-text-and-the-empty-catalog-text) |  | Active | |  |
| [TEAMMATECARD-39](teammate-card.md#teammatecard-39-changing-the-model-clears-the-effort-and-re-probes-the-ladder-haiku---sonnet) |  | Active | |  |
| [TEAMMATECARD-40](teammate-card.md#teammatecard-40-model-is-stored-as-an-id-in-teamdb-only) |  | Active | |  |
| [TEAMMATECARD-41](teammate-card.md#teammatecard-41-effort-low---medium-is-stored-as-an-id-and-the-blank-option-deletes-the-row) |  | Active | |  |
| [TEAMMATECARD-42](teammate-card.md#teammatecard-42-restart-starts-an-adapter-even-with-teamacpenabledfalse) |  | Active | |  |
| [TEAMMATECARD-43](teammate-card.md#teammatecard-43-a-newly-created-teammate-comes-online-by-itself-when-acp-is-on) |  | Active | |  |
| [TEAMMATECARD-44](teammate-card.md#teammatecard-44-the-message-action-appears-only-once-a-room-exists) |  | Active | |  |
| [TEAMMATECARD-45](teammate-card.md#teammatecard-45-restart-is-hidden-for-a-healthy-or-starting-teammate) |  | Active | |  |
| [TEAMMATECARD-46](teammate-card.md#teammatecard-46-a-save-that-changes-nothing-does-not-restart-the-teammate) |  | Active | |  |
| [TEAMMATECARD-47](teammate-card.md#teammatecard-47-changing-only-the-model-or-only-the-effort-restarts-the-session) |  | Active | |  |
| [TEAMMATECARD-48](teammate-card.md#teammatecard-48-remove-does-not-cascade-the-agent-and-its-room-survive) |  | Active | |  |
| [TEAMMATECARD-49](teammate-card.md#teammatecard-49-delete-then-recreate-under-the-same-name-fresh-settings-old-room) |  | Active | |  |
| [TEAMMATECARD-50](teammate-card.md#teammatecard-50-editing-a-teammate-really-does-lose-its-conversation-memory-costs-money) | 💰 | Active | |  |
| [TEAMMATECARD-51](teammate-card.md#teammatecard-51-the-edit-cards-name-title-and-alias-boxes-write-into-the-frontmatter-and-preserve-every-other-field) |  | Active | |  |

## model-effort

Model and Effort pickers, catalog probe and runner restart — [area file](model-effort.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [MODELEFFORT-01](model-effort.md#modeleffort-01-first-card-open-the-model-list-comes-from-the-adapter-and-one-adapter-spawn-fills-both-lists) |  | Active | | |
| [MODELEFFORT-02](model-effort.md#modeleffort-02-fixture-walk-record-every-models-effort-ladder-and-one-spawn-per-newly-seen-model) |  | Active | | |
| [MODELEFFORT-03](model-effort.md#modeleffort-03-saving-a-new-teammate-stores-wire-ids-never-display-names) |  | Active | | |
| [MODELEFFORT-04](model-effort.md#modeleffort-04-two-different-default-choices-in-the-model-select-and-none-in-the-effort-select) |  | Active | | |
| [MODELEFFORT-05](model-effort.md#modeleffort-05-no-card-field-shows-a-literal-parameter-name-the-missing--binding-trap) |  | Active | | |
| [MODELEFFORT-06](model-effort.md#modeleffort-06-neither-catalog-is-probed-on-a-plain-page-load-or-a-tile-click) |  | Active | | |
| [MODELEFFORT-07](model-effort.md#modeleffort-07-changing-the-model-re-probes-the-effort-list-changes-and-the-chosen-level-resets) |  | Active | | |
| [MODELEFFORT-08](model-effort.md#modeleffort-08-a-superseded-effort-probe-never-lands-on-the-model-the-user-ended-up-with) |  | Active | | |
| [MODELEFFORT-09](model-effort.md#modeleffort-09-closing-the-card-mid-probe-leaves-no-stuck-state) |  | Active | | |
| [MODELEFFORT-10](model-effort.md#modeleffort-10-a-model-with-no-effort-support-says-so-and-the-log-proves-it-was-not-a-failed-probe) |  | Active | | |
| [MODELEFFORT-11](model-effort.md#modeleffort-11-edit-preselects-the-stored-model-and-effort-and-keeps-them-when-the-catalog-lands-late) |  | Active | | |
| [MODELEFFORT-12](model-effort.md#modeleffort-12-a-stored-value-the-adapter-no-longer-advertises-is-still-offered-and-still-saved) |  | Active | | |
| [MODELEFFORT-13](model-effort.md#modeleffort-13-remove-a-teammate-and-recreate-it-with-the-same-name---nothing-resurrects) |  | Active | | |
| [MODELEFFORT-14](model-effort.md#modeleffort-14-editing-the-frontmatter-name-moves-the-model-and-effort-rows-and-only-those) |  | Active | | |
| [MODELEFFORT-15](model-effort.md#modeleffort-15-the-view-cards-model-and-effort-sections-and-the-label-versus-id-inconsistency) |  | Active | | |
| [MODELEFFORT-16](model-effort.md#modeleffort-16-the-probe-runs-in-app_datawork-never-in-the-repository-root) |  | Active | | |
| [MODELEFFORT-17](model-effort.md#modeleffort-17-two-browser-tabs-opening-cards-at-once-spawn-one-adapter-not-two) |  | Active | | |
| [MODELEFFORT-18](model-effort.md#modeleffort-18-the-model-catalog-is-cached-for-the-whole-app-run-and-goes-fresh-on-restart) |  | Active | | |
| [MODELEFFORT-19](model-effort.md#modeleffort-19-no-adapter-installed-the-page-still-works-and-a-failed-probe-is-never-cached) |  | Active | | |
| [MODELEFFORT-20](model-effort.md#modeleffort-20-adapter-present-but-broken-launch-failure-and-auth-failure-degrade-to-the-same-picker-with-distinct-log-lines) |  | Active | | |
| [MODELEFFORT-21](model-effort.md#modeleffort-21-an-existing-app_data-opens-with-the-two-tables-added-in-place-no-wipe) |  | Active | | |
| [MODELEFFORT-22](model-effort.md#modeleffort-22-restart-lane-changing-the-model-restarts-the-runner-exactly-once) |  | Active | | |
| [MODELEFFORT-23](model-effort.md#modeleffort-23-restart-lane-changing-effort-low-to-medium-restarts-with-no-catalog-warning) |  | Active | | |
| [MODELEFFORT-24](model-effort.md#modeleffort-24-restart-lane-saving-with-nothing-changed-does-not-restart-the-runner) |  | Active | | |
| [MODELEFFORT-25](model-effort.md#modeleffort-25-restart-lane-a-stale-stored-model-or-effort-warns-but-never-blocks-the-teammate) |  | Active | | |
| [MODELEFFORT-26](model-effort.md#modeleffort-26-restart-lane-the-manual-restart-button-appears-only-on-an-offline-or-degraded-teammate) |  | Active | | |
| [MODELEFFORT-27](model-effort.md#modeleffort-27-money-the-chosen-model-and-effort-actually-reach-the-model---ask-it) | 💰 | Active | | |
| [MODELEFFORT-28](model-effort.md#modeleffort-28-both-pickers-are-inert-while-their-own-catalog-is-being-probed-and-the-model-list-lands-without-waiting-on-the-effort-one) |  | Active | | |

## invite-rooms

Creating Rooms, inviting Agents, Room naming — [area file](invite-rooms.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [INVITEROOMS-01](invite-rooms.md#inviterooms-01-a-virgin-install-seeds-one-room-per-demo-agent-named-after-it-and-lands-you-in-the-first) |  | Active | | |
| [INVITEROOMS-02](invite-rooms.md#inviterooms-02-routing-redirects-to-the-first-room-and-an-unknown-room-id-shows-the-empty-state-instead-of-crashing) |  | Active | | |
| [INVITEROOMS-03](invite-rooms.md#inviterooms-03-the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked) |  | Active | |  |
| [INVITEROOMS-04](invite-rooms.md#inviterooms-04-a-pipe-client-connecting-creates-its-room-and-it-appears-in-the-sidebar-with-no-page-refresh) |  | Active | | |
| [INVITEROOMS-05](invite-rooms.md#inviterooms-05-reconnecting-under-the-same-name-re-attaches-to-the-existing-room-it-never-mints-a-second-one) |  | Active | | |
| [INVITEROOMS-06](invite-rooms.md#inviterooms-06-a-name-differing-only-in-case-re-attaches-to-the-existing-agent-instead-of-creating-a-second-one) |  | Active | | |
| [INVITEROOMS-07](invite-rooms.md#inviterooms-07-an-invalid-agent-name-is-refused-at-the-handshake-and-no-room-appears-anywhere) |  | Active | | |
| [INVITEROOMS-08](invite-rooms.md#inviterooms-08-new-chat-with-exactly-one-agent-opens-that-agents-existing-room-and-creates-nothing) |  | Active | | |
| [INVITEROOMS-09](invite-rooms.md#inviterooms-09-new-chat-with-two-agents-creates-a-room-named-after-them-and-navigates-to-it-live) |  | Active | | |
| [INVITEROOMS-10](invite-rooms.md#inviterooms-10-the-same-two-agent-selection-twice-creates-a-second-room-with-the-identical-name) |  | Active | | |
| [INVITEROOMS-11](invite-rooms.md#inviterooms-11-add-teammate-on-the-room-header-offers-only-agents-that-are-not-already-members-and-starts-closed) |  | Active | | |
| [INVITEROOMS-12](invite-rooms.md#inviterooms-12-clicking-a-candidate-invites-it-renames-the-room-and-repaints-all-four-surfaces-with-no-reload) |  | Active | | |
| [INVITEROOMS-13](invite-rooms.md#inviterooms-13-add-teammate-says-so-when-every-agent-is-already-a-member-and-hides-the-team-filter-in-that-state) |  | Active | | |
| [INVITEROOMS-14](invite-rooms.md#inviterooms-14-the-invite-panels-open-state-and-its-message-do-not-survive-a-room-switch) |  | Active | | |
| [INVITEROOMS-15](invite-rooms.md#inviterooms-15-invite-name-in-the-composer-does-the-same-thing-as-the-header-control) |  | Active | | |
| [INVITEROOMS-16](invite-rooms.md#inviterooms-16-invite-accepts-a-multi-word-name-with-and-without-the-and-never-truncates-at-the-space) |  | Active | | |
| [INVITEROOMS-17](invite-rooms.md#inviterooms-17-invite-of-an-agent-already-in-the-room-reports-success-and-changes-nothing) |  | Active | | |
| [INVITEROOMS-18](invite-rooms.md#inviterooms-18-invite-rejects-an-unknown-name-and-rejects-the-human) |  | Active | | |
| [INVITEROOMS-19](invite-rooms.md#inviterooms-19-any-other-leading-slash-text-is-refused-as-unknown-command-and-the-typed-text-is-lost) |  | Active | | |
| [INVITEROOMS-20](invite-rooms.md#inviterooms-20-a-rooms-name-is-always-its-agent-members-joined-by-in-membership-order-with-the-human-excluded) |  | Active | | |
| [INVITEROOMS-21](invite-rooms.md#inviterooms-21-an-agent-belongs-to-at-most-one-two-member-room-after-an-invite-the-next-registration-mints-a-fresh-direct-room) |  | Active | | |
| [INVITEROOMS-22](invite-rooms.md#inviterooms-22-two-browser-tabs-stay-in-step-on-a-membership-change-including-an-open-candidate-list) |  | Active | | |
| [INVITEROOMS-23](invite-rooms.md#inviterooms-23-adding-a-third-member-flips-a-room-from-answer-everything-to-mention-gated) |  | Active | | |
| [INVITEROOMS-24](invite-rooms.md#inviterooms-24-rooms-names-and-membership-survive-a-restart-the-room-budget-does-not) |  | Active | | |
| [INVITEROOMS-25](invite-rooms.md#inviterooms-25-the-team-filter-narrows-candidates-and-an-agent-with-no-persona-only-ever-shows-under-all-teams) |  | Active | | |
| [INVITEROOMS-26](invite-rooms.md#inviterooms-26-invite-accepts-a-personas-alias-wherever-it-accepts-its-name) |  | Active | | |
| [INVITEROOMS-27](invite-rooms.md#inviterooms-27-the-teammate-cards-message-action-disappears-once-that-personas-direct-room-has-been-invited-into) |  | Active | | |
| [INVITEROOMS-28](invite-rooms.md#inviterooms-28-an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar) | 💰 | Active | | |
| [INVITEROOMS-29](invite-rooms.md#inviterooms-29-an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room-label) | 💰 | Active | | |
| [INVITEROOMS-30](invite-rooms.md#inviterooms-30-an-agent-can-invite-into-a-room-it-is-not-a-member-of) | 💰 | Active | | |
| [INVITEROOMS-31](invite-rooms.md#inviterooms-31-every-agent-created-room-contains-the-human-so-none-is-hidden) | 💰 | Active | | |
| [INVITEROOMS-32](invite-rooms.md#inviterooms-32-the-composers-own-status-line-does-not-survive-a-room-switch) |  | Active | | |
| [INVITEROOMS-33](invite-rooms.md#inviterooms-33-a-renamed-room-keeps-its-name-across-an-invitation-an-un-renamed-one-still-re-derives) |  | Active | | |
| [INVITEROOMS-34](invite-rooms.md#inviterooms-34-archiving-a-room-from-its-row-menu-removes-it-from-the-sidebar-and-lists-it-under-archived-chats) |  | Active | | |
| [INVITEROOMS-35](invite-rooms.md#inviterooms-35-unarchiving-returns-the-room-to-the-sidebar-with-its-transcript-intact) |  | Active | | |
| [INVITEROOMS-36](invite-rooms.md#inviterooms-36-deleting-a-room-needs-a-confirmation-and-removes-its-transcript-file-from-disk) |  | Active | | |
| [INVITEROOMS-37](invite-rooms.md#inviterooms-37-archiving-or-deleting-the-room-you-are-viewing-moves-you-somewhere-valid) |  | Active | | |
| [INVITEROOMS-38](invite-rooms.md#inviterooms-38-an-archived-two-member-room-is-not-reused-starting-a-chat-with-that-teammate-mints-a-second-one) |  | Active | | |
| [INVITEROOMS-39](invite-rooms.md#inviterooms-39-archived-and-deleted-rooms-survive-a-restart) |  | Active | | |

## hooks-settings

Settings: the 24 Hooks, editing, per-field reset and Save — [area file](hooks-settings.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [HOOKSSETTINGS-01](hooks-settings.md#hookssettings-01-settings-opens-on-the-hooks-tab-with-a-two-button-tab-rail) |  | Active | |  |
| [HOOKSSETTINGS-02](hooks-settings.md#hookssettings-02-tab-clicks-change-the-url-and-an-unknown-tab-segment-falls-back-to-hooks-instead-of-404ing) |  | Active | |  |
| [HOOKSSETTINGS-03](hooks-settings.md#hookssettings-03-the-hooks-tab-prints-the-real-absolute-path-of-hooksjson-and-says-the-files-absence-is-expected) |  | Active | |  |
| [HOOKSSETTINGS-04](hooks-settings.md#hookssettings-04-exactly-24-hook-fields-in-four-named-groups-in-a-fixed-order) |  | Active | |  |
| [HOOKSSETTINGS-05](hooks-settings.md#hookssettings-05-the-next-session-badge-appears-on-exactly-the-11-hooks-whose-edits-cannot-reach-a-running-teammate) |  | Active | |  |
| [HOOKSSETTINGS-06](hooks-settings.md#hookssettings-06-placeholder-chips-are-listed-on-exactly-the-8-hooks-that-take-placeholders-with-full-braces) |  | Active | |  |
| [HOOKSSETTINGS-07](hooks-settings.md#hookssettings-07-no-hooks-default-text-contains-the-literal-string-mcp__team__) |  | Active | |  |
| [HOOKSSETTINGS-08](hooks-settings.md#hookssettings-08-textarea-height-tracks-the-line-count-and-clamps-at-14-rows) |  | Active | |  |
| [HOOKSSETTINGS-09](hooks-settings.md#hookssettings-09-typing-raises-modified-and-unsaved-together-and-enables-save-reset-and-reset-all-while-writing-nothing) |  | Active | |  |
| [HOOKSSETTINGS-10](hooks-settings.md#hookssettings-10-modified-and-unsaved-are-independent-flags-walk-all-three-combinations) |  | Active | |  |
| [HOOKSSETTINGS-11](hooks-settings.md#hookssettings-11-per-field-reset-stages-the-shipped-default-without-writing-anything-to-disk) |  | Active | |  |
| [HOOKSSETTINGS-12](hooks-settings.md#hookssettings-12-hooksjson-does-not-exist-until-the-first-save-and-then-holds-only-the-keys-you-changed) |  | Active | |  |
| [HOOKSSETTINGS-13](hooks-settings.md#hookssettings-13-the-saved-file-is-human-readable-indented-with-literal-em-dashes-and-angle-brackets-not-uxxxx-escapes) |  | Active | |  |
| [HOOKSSETTINGS-14](hooks-settings.md#hookssettings-14-saving-a-field-back-to-its-default-removes-the-key-rather-than-storing-a-redundant-copy) |  | Active | |  |
| [HOOKSSETTINGS-15](hooks-settings.md#hookssettings-15-save-is-the-only-writer-and-a-successful-save-gives-no-confirmation) |  | Active | |  |
| [HOOKSSETTINGS-16](hooks-settings.md#hookssettings-16-reset-all-to-defaults-is-disabled-until-something-is-modified-and-guards-itself-with-an-inline-confirm) |  | Active | |  |
| [HOOKSSETTINGS-17](hooks-settings.md#hookssettings-17-yes-reset-everything-stages-all-24-defaults-but-writes-nothing-and-the-reset-is-silently-lost-if-you-navigate-away) |  | Active | |  |
| [HOOKSSETTINGS-18](hooks-settings.md#hookssettings-18-reset-all-followed-by-save-leaves-hooksjson-present-and-empty-never-deleted) |  | Active | |  |
| [HOOKSSETTINGS-19](hooks-settings.md#hookssettings-19-a-failed-save-shows-a-red-could-not-save-line-under-the-heading-and-keeps-every-pending-edit) |  | Active | |  |
| [HOOKSSETTINGS-20](hooks-settings.md#hookssettings-20-validation-reports-and-never-refuses-an-empty-field-raises-an-error-and-still-saves) |  | Active | |  |
| [HOOKSSETTINGS-21](hooks-settings.md#hookssettings-21-removing-a-required-placeholder-raises-an-error-naming-that-placeholder-and-an-optional-one-does-not) |  | Active | |  |
| [HOOKSSETTINGS-22](hooks-settings.md#hookssettings-22-an-unrecognised-token-raises-a-warning-a-malformed-one-raises-nothing-at-all) |  | Active | |  |
| [HOOKSSETTINGS-23](hooks-settings.md#hookssettings-23-validation-runs-live-against-the-pending-value-and-clears-without-saving) |  | Active | |  |
| [HOOKSSETTINGS-24](hooks-settings.md#hookssettings-24-hook-text-is-rendered-as-text-never-as-markup) |  | Active | |  |
| [HOOKSSETTINGS-25](hooks-settings.md#hookssettings-25-typing-in-a-long-hook-field-stays-responsive-and-loses-no-characters) |  | Active | |  |
| [HOOKSSETTINGS-26](hooks-settings.md#hookssettings-26-uncommitted-edits-survive-a-tab-switch-and-a-theme-change-but-are-silently-discarded-by-reload-or-leaving-the-page) |  | Active | |  |
| [HOOKSSETTINGS-27](hooks-settings.md#hookssettings-27-reset-all-to-defaults-is-also-rendered-on-the-appearance-tab-where-it-acts-on-hooks) |  | Active | |  |
| [HOOKSSETTINGS-28](hooks-settings.md#hookssettings-28-a-save-in-one-browser-tab-repaints-settings-open-in-another-without-eating-that-tabs-typing) |  | Active | |  |
| [HOOKSSETTINGS-29](hooks-settings.md#hookssettings-29-a-hand-edit-to-hooksjson-reaches-the-open-page-within-about-a-second-with-no-restart-and-no-refresh) |  | Active | |  |
| [HOOKSSETTINGS-30](hooks-settings.md#hookssettings-30-deleting-hooksjson-while-the-app-runs-reverts-every-field-to-its-shipped-default-live) |  | Active | |  |
| [HOOKSSETTINGS-31](hooks-settings.md#hookssettings-31-silent-a-malformed-hooksjson-edited-while-running-changes-nothing-on-screen-and-says-nothing-check-the-log) |  | Active | |  |
| [HOOKSSETTINGS-32](hooks-settings.md#hookssettings-32-silent-a-malformed-hooksjson-at-startup-falls-back-to-defaults-wholesale-with-no-ui-clue-and-the-file-left-intact) |  | Active | |  |
| [HOOKSSETTINGS-33](hooks-settings.md#hookssettings-33-an-unknown-key-in-hooksjson-is-kept-forever-ignored-for-resolution-and-logged-once) |  | Active | |  |
| [HOOKSSETTINGS-34](hooks-settings.md#hookssettings-34-a-pending-browser-edit-beats-a-concurrent-hand-edit-to-the-same-key-and-save-merges-rather-than-overwrites) |  | Active | |  |
| [HOOKSSETTINGS-35](hooks-settings.md#hookssettings-35-a-crlf-hand-edit-makes-a-field-show-modified-while-looking-identical-and-reset-fixes-it) |  | Active | |  |
| [HOOKSSETTINGS-36](hooks-settings.md#hookssettings-36-loading-settings-never-starts-an-adapter-node-process) |  | Active | |  |
| [HOOKSSETTINGS-37](hooks-settings.md#hookssettings-37-saving-a-hook-must-not-restart-any-teammates-session) |  | Active | |  |
| [HOOKSSETTINGS-38](hooks-settings.md#hookssettings-38-costs-money-a-live-hook-edit-reaches-the-very-next-turn-with-no-restart) | 💰 | Active | |  |
| [HOOKSSETTINGS-39](hooks-settings.md#hookssettings-39-costs-money-a-next-session-hook-edit-is-silently-inert-on-a-running-teammate-until-it-restarts) | 💰 | Active | |  |
| [HOOKSSETTINGS-40](hooks-settings.md#hookssettings-40-costs-money-get_help-re-renders-on-every-call-so-its-nine-hooks-land-on-the-next-call-while-a-tool-description-does-not) | 💰 | Active | |  |

## appearance-theme

Appearance tab, Themes, Tokens and overrides — [area file](appearance-theme.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [APPEARANCETHEME-01](appearance-theme.md#appearancetheme-01-the-appearance-tab-is-on-the-settings-rail-and-is-reachable-by-its-own-route) |  | Active | |  |
| [APPEARANCETHEME-02](appearance-theme.md#appearancetheme-02-the-appearance-tab-shows-its-own-prose-and-the-real-absolute-selection-file-path-and-none-of-the-hooks-tabs-prose) |  | Active | |  |
| [APPEARANCETHEME-03](appearance-theme.md#appearancetheme-03-the-theme-select-offers-the-built-in-catalog-and-the-appearance-select-offers-exactly-system-light-and-dark-in-that-order) |  | Active | |  |
| [APPEARANCETHEME-04](appearance-theme.md#appearancetheme-04-choosing-a-value-in-either-select-stores-its-id-and-applies-immediately-with-no-page-reload) |  | Active | |  |
| [APPEARANCETHEME-05](appearance-theme.md#appearancetheme-05-retired-the-three-stylesheet-cascade-layering-test) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - CSS cascade layering test, no successor. See area file. |
| [APPEARANCETHEME-06](appearance-theme.md#appearancetheme-06-the-saved-theme-and-dark-mode-preference-are-shown-as-selected-in-both-selects-after-a-full-load) |  | Active | |  |
| [APPEARANCETHEME-07](appearance-theme.md#appearancetheme-07-an-explicit-dark-or-light-choice-shows-no-flash-of-the-other-palette-on-a-hard-reload) |  | Active | |  |
| [APPEARANCETHEME-08](appearance-theme.md#appearancetheme-08-system-follows-the-operating-system-live-with-no-reload-an-explicit-choice-ignores-the-os-entirely-and-a-first-paint-flash-under-system-is-expected) |  | Active | |  |
| [APPEARANCETHEME-09](appearance-theme.md#appearancetheme-09-the-choice-is-per-installation-not-per-browser) |  | Active | |  |
| [APPEARANCETHEME-10](appearance-theme.md#appearancetheme-10-retired-token-name-overrides-changing-exactly-what-they-name) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - per-token overrides removed, no successor. See area file. |
| [APPEARANCETHEME-11](appearance-theme.md#appearancetheme-11-retired-exotic-but-allowed-override-values-quoted-fonts-percentages-color-mix-token-references) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - per-token overrides removed, no successor. See area file. |
| [APPEARANCETHEME-12](appearance-theme.md#appearancetheme-12-retired-a-hostile-override-value-being-rejected-rather-than-reaching-the-document-as-raw-css) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - per-token overrides removed, no successor. See area file. |
| [APPEARANCETHEME-13](appearance-theme.md#appearancetheme-13-retired-an-override-key-that-is-not-a-token-name-being-rejected-per-entry) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - per-token overrides removed, no successor. See area file. |
| [APPEARANCETHEME-14](appearance-theme.md#appearancetheme-14-an-unknown-theme-id-or-dark-mode-value-is-a-warning-logged-once-never-shown-on-the-tab-and-the-file-keeps-saying-it) |  | Active | |  |
| [APPEARANCETHEME-15](appearance-theme.md#appearancetheme-15-malformed-json-falls-back-wholesale-is-logged-once-and-the-file-is-left-untouched) |  | Active | |  |
| [APPEARANCETHEME-16](appearance-theme.md#appearancetheme-16-saving-a-choice-while-the-file-is-malformed-replaces-its-contents-with-valid-json) |  | Active | |  |
| [APPEARANCETHEME-17](appearance-theme.md#appearancetheme-17-save-re-reads-the-file-under-its-write-lock-so-an-unrelated-hand-added-key-survives-and-a-concurrent-edit-to-the-other-field-is-not-clobbered) |  | Active | |  |
| [APPEARANCETHEME-18](appearance-theme.md#appearancetheme-18-a-hand-edit-updates-the-open-tab-live-and-now-the-pages-colours-change-too-no-full-load-required) |  | Active | |  |
| [APPEARANCETHEME-19](appearance-theme.md#appearancetheme-19-deleting-appearancejson-while-the-app-runs-returns-it-to-the-default-live-and-the-app-never-recreates-it) |  | Active | |  |
| [APPEARANCETHEME-20](appearance-theme.md#appearancetheme-20-two-browser-windows-stay-in-step-colours-included-the-change-propagates-through-the-file-to-every-open-circuit) |  | Active | |  |
| [APPEARANCETHEME-21](appearance-theme.md#appearancetheme-21-re-selecting-the-value-that-is-already-selected-is-harmless-and-produces-no-reload) |  | Active | |  |
| [APPEARANCETHEME-22](appearance-theme.md#appearancetheme-22-dark-mode-walked-across-every-page-and-every-state---the-acceptance-test-for-the-whole-item) |  | Active | |  |
| [APPEARANCETHEME-23](appearance-theme.md#appearancetheme-23-hover-selection-and-focus-surfaces-all-switch-and-the-four-old-hover-colours-are-still-one) |  | Active | |  |
| [APPEARANCETHEME-24](appearance-theme.md#appearancetheme-24-the-reconnect-modal-shows-one-themed-state-paragraph-over-a-dimmed-backdrop-in-both-light-and-dark) |  | Active | |  |
| [APPEARANCETHEME-25](appearance-theme.md#appearancetheme-25-retired-the-layering-probe-a-theme-that-sets-one-token-inherits-the-rest-from-the-built-in-palette) |  | Active | | Retired 2026-09-14 (MudBlazor migration) - CSS cascade layering probe, no successor. See area file. |
| [APPEARANCETHEME-26](appearance-theme.md#appearancetheme-26--the-credit-line-names-visual-studio-code-and-attributes-nobody-it-should-not) |  | Active | | Added 2026-09-16 with the VS Code theme import (ADR-0016). |
| [APPEARANCETHEME-27](appearance-theme.md#appearancetheme-27--an-imported-theme-applies-in-its-native-mode-and-falls-back-to-huddles-palette-in-the-other) |  | Active | | Added 2026-09-16 with the VS Code theme import (ADR-0016). The acceptance walk for the imported catalog. |

## pipe-external

The named pipe: external agents and the wire — [area file](pipe-external.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PIPEEXTERNAL-01](pipe-external.md#pipeexternal-01-the-two-demo-agents-are-already-there-and-they-reply-to-a-mention) |  | Active | | |
| [PIPEEXTERNAL-02](pipe-external.md#pipeexternal-02-a-demo-agent-streams-a-draft-then-replaces-it-with-the-finished-message) |  | Active | | |
| [PIPEEXTERNAL-03](pipe-external.md#pipeexternal-03-new-chat-builds-a-group-room-named-after-its-agents-and-that-room-is-mention-gated) |  | Active | | |
| [PIPEEXTERNAL-04](pipe-external.md#pipeexternal-04-a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh) |  | Pass | |  |
| [PIPEEXTERNAL-05](pipe-external.md#pipeexternal-05-the-welcome-envelope-shape-fields-and-version-3) |  | Active | | |
| [PIPEEXTERNAL-06](pipe-external.md#pipeexternal-06-the-echo-round-trip-a-mention-produces-a-bold-reply-in-the-room) |  | Active | | |
| [PIPEEXTERNAL-07](pipe-external.md#pipeexternal-07-an-agent-never-receives-its-own-message-and-nothing-loops) |  | Active | | |
| [PIPEEXTERNAL-08](pipe-external.md#pipeexternal-08-hello-property-order-does-not-matter-so-connecting-is-not-intermittently-refused) |  | Active | | |
| [PIPEEXTERNAL-09](pipe-external.md#pipeexternal-09-reconnecting-under-the-same-name-keeps-the-agent-id-and-creates-no-second-room) |  | Active | | |
| [PIPEEXTERNAL-10](pipe-external.md#pipeexternal-10-reconnecting-with-different-letter-case-is-the-same-agent-and-the-stored-name-does-not-change) |  | Active | | |
| [PIPEEXTERNAL-11](pipe-external.md#pipeexternal-11-disconnecting-an-external-agent-changes-nothing-visible-in-the-room-the-silent-case) |  | Active | | |
| [PIPEEXTERNAL-12](pipe-external.md#pipeexternal-12-where-a-disconnect-is-visible-the-agent-dot-in-new-chat-and-add-teammate) |  | Active | | |
| [PIPEEXTERNAL-13](pipe-external.md#pipeexternal-13-an-offline-agent-receives-nothing-and-nothing-is-replayed-on-reconnect) |  | Active | | |
| [PIPEEXTERNAL-14](pipe-external.md#pipeexternal-14-an-invalid-name-is-refused-with-invalidname-the-connection-closes-and-no-room-appears) |  | Active | | |
| [PIPEEXTERNAL-15](pipe-external.md#pipeexternal-15-the-humans-name-is-reserved-on-the-pipe) |  | Active | | |
| [PIPEEXTERNAL-16](pipe-external.md#pipeexternal-16-a-name-with-a-single-interior-space-is-accepted-and-works-end-to-end) |  | Active | | |
| [PIPEEXTERNAL-17](pipe-external.md#pipeexternal-17-dialling-the-wrong-pipe-name-fails-outside-the-browser-and-the-pipe-name-is-configuration) |  | Active | | |
| [PIPEEXTERNAL-18](pipe-external.md#pipeexternal-18-members-is-filled-on-every-messageposted-and-grows-when-the-room-does) |  | Active | | |
| [PIPEEXTERNAL-19](pipe-external.md#pipeexternal-19-an-external-agent-joins-a-room-live-without-reconnecting) |  | Active | | |
| [PIPEEXTERNAL-20](pipe-external.md#pipeexternal-20-the-welcome-lists-every-room-the-agent-is-in-and-a-reconnect-after-an-invite-mints-a-second-room-of-the-same-name) |  | Active | | |
| [PIPEEXTERNAL-21](pipe-external.md#pipeexternal-21-drafts-are-never-delivered-to-another-agent) |  | Active | | |
| [PIPEEXTERNAL-22](pipe-external.md#pipeexternal-22-the-mentioned-flag-is-per-recipient-and-unmentioned-members-still-receive-the-envelope) |  | Active | | |
| [PIPEEXTERNAL-23](pipe-external.md#pipeexternal-23-loop-safety-two-bots-in-one-room-reply-once-each-and-go-quiet) |  | Active | | |
| [PIPEEXTERNAL-24](pipe-external.md#pipeexternal-24-a-second-connection-under-the-same-name-displaces-the-first-which-is-told-by-having-its-pipe-closed) |  | Active | | |
| [PIPEEXTERNAL-25](pipe-external.md#pipeexternal-25-three-clients-connect-at-once-and-the-accept-loop-does-not-jam) |  | Active | | |
| [PIPEEXTERNAL-26](pipe-external.md#pipeexternal-26-the-budget-labels-ride-on-every-messageposted-and-a-refused-post-comes-back-as-budgetexhausted) |  | Active | | |
| [PIPEEXTERNAL-27](pipe-external.md#pipeexternal-27-continue-re-delivers-the-same-message-to-the-agents-without-showing-it-twice) |  | Active | | |
| [PIPEEXTERNAL-28](pipe-external.md#pipeexternal-28-a-wrong-protocol-version-in-hello-is-refused-and-the-browser-never-learns) |  | Active | | |
| [PIPEEXTERNAL-29](pipe-external.md#pipeexternal-29-a-first-message-that-is-not-hello-and-a-silent-client-are-both-refused-as-expectedhello) |  | Active | | |
| [PIPEEXTERNAL-30](pipe-external.md#pipeexternal-30-after-the-handshake-a-bad-line-or-a-bad-request-is-reported-and-the-connection-stays-open) |  | Active | | |
| [PIPEEXTERNAL-31](pipe-external.md#pipeexternal-31-an-agent-cannot-write-a-draft-into-a-room-it-is-not-a-member-of-even-by-reusing-a-message-id) |  | Active | | |
| [PIPEEXTERNAL-32](pipe-external.md#pipeexternal-32-an-agent-disconnecting-mid-draft-clears-its-draft-rather-than-freezing-it-on-screen) |  | Active | | |
| [PIPEEXTERNAL-33](pipe-external.md#pipeexternal-33-the-displacement-trap-an-external-bot-named-echo-kills-that-demo-agent-for-the-life-of-the-process) |  | Active | | |
| [PIPEEXTERNAL-34](pipe-external.md#pipeexternal-34-stopping-the-app-closes-every-agent-connection-cleanly-and-the-browsers-reconnect-dialog-is-readable) |  | Active | | |
| [PIPEEXTERNAL-35](pipe-external.md#pipeexternal-35-an-empty-install-shows-its-two-empty-state-strings-and-the-main-pane-does-not-auto-open-the-first-new-room) |  | Active | | |
| [PIPEEXTERNAL-36](pipe-external.md#pipeexternal-36-two-app-instances-on-one-pipe-name-split-clients-unpredictably) |  | Active | | |

## app-tools

App Tools a real model calls (progressive discovery) — [area file](app-tools.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [APPTOOLS-01](app-tools.md#apptools-01-every-model-facing-hook-is-readable-in-the-browser-and-no-default-contains-the-literal-mcp__team__) |  | Active | | |
| [APPTOOLS-02](app-tools.md#apptools-02-hookvalidator-reports-a-missing-placeholder-and-a-typo-and-never-blocks-the-save) |  | Active | | |
| [APPTOOLS-03](app-tools.md#apptools-03-with-teamacpenabledfalse-no-app-tool-server-exists-at-all-and-the-chat-surface-still-works) |  | Active | | |
| [APPTOOLS-04](app-tools.md#apptools-04-the-tool-server-binds-and-completes-the-mcp-handshake-before-any-model-call-the-wiring-pre-flight) |  | Active | | |
| [APPTOOLS-05](app-tools.md#apptools-05-an-agent-reads-its-own-rooms-id-off-the-room-label) | 💰 | Active | | |
| [APPTOOLS-06](app-tools.md#apptools-06-get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live) | 💰 | Active | | |
| [APPTOOLS-07](app-tools.md#apptools-07-asked-what-tools-it-has-a-teammate-calls-get_help-and-reports-the-real-catalog-with-descriptions) | 💰 | Active | | |
| [APPTOOLS-08](app-tools.md#apptools-08-tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight) | 💰 | Active | | |
| [APPTOOLS-09](app-tools.md#apptools-09-tool-activity-is-not-written-to-the-transcript-and-does-not-survive-the-turn) |  | Active | | |
| [APPTOOLS-10](app-tools.md#apptools-10-list_agents-reports-real-names-real-online-state-and-real-job-descriptions) | 💰 | Active | | |
| [APPTOOLS-11](app-tools.md#apptools-11-an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent) | 💰 | Active | | |
| [APPTOOLS-12](app-tools.md#apptools-12-invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live) | 💰 | Active | | |
| [APPTOOLS-13](app-tools.md#apptools-13-invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member) | 💰 | Active | | |
| [APPTOOLS-14](app-tools.md#apptools-14-invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist) | 💰 | Active | | |
| [APPTOOLS-15](app-tools.md#apptools-15-an-agents-reply-is-delivered-once-it-must-not-also-post-the-same-text-with-post_message) | 💰 | Active | | |
| [APPTOOLS-16](app-tools.md#apptools-16-create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents) | 💰 | Active | | |
| [APPTOOLS-17](app-tools.md#apptools-17-post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post) | 💰 | Active | | |
| [APPTOOLS-18](app-tools.md#apptools-18-tools-are-the-source-of-truth-the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase) | 💰 | Active | | |
| [APPTOOLS-19](app-tools.md#apptools-19-a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute) | 💰 | Active | | |
| [APPTOOLS-20](app-tools.md#apptools-20-a-turn-declined-for-budget-never-reaches-the-model-at-all-the-decline-path-is-not-the-refusal-path) |  | Active | | |
| [APPTOOLS-21](app-tools.md#apptools-21-continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload) | 💰 | Active | | |
| [APPTOOLS-22](app-tools.md#apptools-22-editing-a-hook-changes-model-facing-text-without-restarting-the-session-and-next-session-hooks-wait-for-a-restart) | 💰 | Active | | |
| [APPTOOLS-23](app-tools.md#apptools-23-follow_room-wakes-an-agent-that-was-not-mentioned-and-a-non-follower-in-the-same-room-is-the-control) | 💰 | Active | | |
| [APPTOOLS-24](app-tools.md#apptools-24-unfollow_room-restores-the-ordinary-mention-only-rule-and-the-room-confirms-the-decline-was-not-a-silent-drop) | 💰 | Active | | |
| [APPTOOLS-25](app-tools.md#apptools-25-following-does-not-buy-a-turn-past-the-budget-even-when-two-followers-would-otherwise-keep-each-other-going) | 💰 | Active | | |
| [APPTOOLS-26](app-tools.md#apptools-26-create_room-with-seed-posts-the-opening-message-as-part-of-creation) | 💰 | Active | | |

## persona-lifecycle

Persona lifecycle: supervisor, work dirs, health and restarts — [area file](persona-lifecycle.md)

| Test | 💰 | Status | Issue | Notes |
| --- | --- | --- | --- | --- |
| [PERSONALIFECYCLE-01](persona-lifecycle.md#personalifecycle-01-with-acp-disabled-every-teammate-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert) |  | Pass |  | Re-run 2026-09-14: `Nova` created Haiku/low, card and tile read Offline with no tooltip and no reason line, no `Nova` Room, no alert strip, adapters 0 before and after, `work` empty. Corrected step 17 (`hello there @echo`) drew the `**echo:**` reply. |
| [PERSONALIFECYCLE-02](persona-lifecycle.md#personalifecycle-02-a-persona-name-with-a-leading-trailing-or-doubled-space-is-refused-on-the-card-and-never-written-to-disk) |  | Active | | |
| [PERSONALIFECYCLE-03](persona-lifecycle.md#personalifecycle-03-browsing-teammates-spawns-no-adapter-opening-a-card-spawns-exactly-one-and-reopening-spawns-none) |  | Active | | |
| [PERSONALIFECYCLE-04](persona-lifecycle.md#personalifecycle-04-a-missing-adapter-degrades-the-model-and-effort-pickers-with-a-plain-language-hint-and-restoring-it-works-with-no-app-restart) |  | Active | | |
| [PERSONALIFECYCLE-05](persona-lifecycle.md#personalifecycle-05-restart-with-teamacpenabledfalse-record-whether-it-starts-a-real-adapter-anyway) |  | Active | | |
| [PERSONALIFECYCLE-06](persona-lifecycle.md#personalifecycle-06-a-persona-added-while-the-app-is-running-comes-online-and-gets-a-room-named-after-it-with-no-page-refresh) |  | Active | | |
| [PERSONALIFECYCLE-07](persona-lifecycle.md#personalifecycle-07-the-work-dir-is-named-from-the-frontmatter-name-and-does-not-follow-a-renamed-file) |  | Active | | |
| [PERSONALIFECYCLE-08](persona-lifecycle.md#personalifecycle-08-a-name-containing-spaces-works-for-the-monogram-the-room-and-the-work-dir) |  | Active | | |
| [PERSONALIFECYCLE-09](persona-lifecycle.md#personalifecycle-09-a-missing-adapter-reports-the-same-actionable-reason-on-the-tile-the-card-and-the-room-strip-and-harms-no-other-teammate) |  | Active | | |
| [PERSONALIFECYCLE-10](persona-lifecycle.md#personalifecycle-10-restart-on-the-teammate-card-recovers-a-failed-persona-with-no-app-restart-and-one-click-starts-one-adapter) |  | Active | | |
| [PERSONALIFECYCLE-11](persona-lifecycle.md#personalifecycle-11-restart-is-offered-only-for-an-unhealthy-teammate-and-is-accompanied-by-the-memory-loss-warning) |  | Active | | |
| [PERSONALIFECYCLE-12](persona-lifecycle.md#personalifecycle-12-killing-the-adapter-process-flips-the-badge-to-offline-with-a-loop-reason-even-though-the-pipe-stays-open) |  | Active | | |
| [PERSONALIFECYCLE-13](persona-lifecycle.md#personalifecycle-13-one-persona-failing-leaves-every-other-persona-online-and-messageable) |  | Active | | |
| [PERSONALIFECYCLE-14](persona-lifecycle.md#personalifecycle-14-an-external-edit-to-a-persona-nested-in-a-team-sub-folder-reloads-and-restarts-it-once-per-save) |  | Active | | |
| [PERSONALIFECYCLE-15](persona-lifecycle.md#personalifecycle-15-renaming-or-moving-a-team-sub-folder-keeps-its-teammates-reachable-or-takes-them-offline-cleanly) |  | Active | | |
| [PERSONALIFECYCLE-16](persona-lifecycle.md#personalifecycle-16-a-persona-file-that-becomes-malformed-is-named-in-files-that-didnt-load-and-an-alias-collision-names-both-files) |  | Active | | |
| [PERSONALIFECYCLE-17](persona-lifecycle.md#personalifecycle-17-a-stored-model-the-adapter-no-longer-advertises-is-degraded-with-a-reason-never-a-failed-start) |  | Active | | |
| [PERSONALIFECYCLE-18](persona-lifecycle.md#personalifecycle-18-editing-a-hook-at-settings-does-not-restart-a-running-teammate) |  | Active | | |
| [PERSONALIFECYCLE-19](persona-lifecycle.md#personalifecycle-19-removing-a-persona-takes-it-offline-but-leaves-its-agent-room-and-transcript-and-raises-no-alert) |  | Active | | |
| [PERSONALIFECYCLE-20](persona-lifecycle.md#personalifecycle-20-re-creating-a-removed-persona-under-the-same-name-does-not-resurrect-its-old-model-or-effort) |  | Active | | |
| [PERSONALIFECYCLE-21](persona-lifecycle.md#personalifecycle-21-editing-the-frontmatter-name-renames-the-teammate-and-moves-its-model-and-effort-leaving-a-documented-ghost) |  | Active | | |
| [PERSONALIFECYCLE-22](persona-lifecycle.md#personalifecycle-22-health-and-presence-repaint-every-open-surface-live-in-every-browser-tab) |  | Active | | |
| [PERSONALIFECYCLE-23](persona-lifecycle.md#personalifecycle-23-an-app-restart-brings-every-persona-back-orphans-no-process-and-deliberately-loses-only-in-memory-state) |  | Active | | |
| [PERSONALIFECYCLE-24](persona-lifecycle.md#personalifecycle-24-four-personas-cost-four-adapter-processes-all-reach-online-and-all-exit-on-shutdown) |  | Active | | |
| [PERSONALIFECYCLE-25](persona-lifecycle.md#personalifecycle-25-an-adapter-that-needs-authentication-reports-that-as-its-own-distinct-reason) |  | Active | | |
| [PERSONALIFECYCLE-26](persona-lifecycle.md#personalifecycle-26-money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-teammate) | 💰 | Active | | |
| [PERSONALIFECYCLE-27](persona-lifecycle.md#personalifecycle-27-money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect) | 💰 | Active | | |
| [PERSONALIFECYCLE-28](persona-lifecycle.md#personalifecycle-28-money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-teammate-remembers) | 💰 | Active | | |
| [PERSONALIFECYCLE-29](persona-lifecycle.md#personalifecycle-29-money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection) | 💰 | Active | | |
| [PERSONALIFECYCLE-30](persona-lifecycle.md#personalifecycle-30-money-context-bleeds-between-rooms-confirm-the-known-limit-and-that-replies-still-land-in-the-right-room) | 💰 | Active | | |
| [PERSONALIFECYCLE-31](persona-lifecycle.md#personalifecycle-31-money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it) | 💰 | Active | | |
| [PERSONALIFECYCLE-32](persona-lifecycle.md#personalifecycle-32-money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange) | 💰 | Active | | |

## adapters

| Test id | 💰 | Status | Run | Notes |
| --- | --- | --- | --- | --- |
| [ADAPTERS-01](adapters.md#adapters-01--two-personas-in-one-room-on-different-adapters-and-neither-room-nor-gate-can-tell) |  | Active | | Needs two Adapter Profiles configured — see the area's setup |
| [ADAPTERS-02](adapters.md#adapters-02--a-local-adapters-reply-streams-into-the-room-incrementally-and-stop-leaves-both-teammates-resumable) |  | Active | | A persisted Message shorter than what rendered is the diagnosed race in [Known limits](../known-limits.md), not a new defect |
| [ADAPTERS-03](adapters.md#adapters-03--changing-a-teammates-adapter-resets-model-and-effort-says-so-and-restarts-the-session-) | 💰 | Active | | |
| [ADAPTERS-04](adapters.md#adapters-04--does-a-real-local-model-call-get_help-unprompted-) | 💰 | Active | | Unblocked 2026-09-18 — D-1 is fixed in `AgencyDotNet.Acp` 0.1.197, published on nuget.org as `0.1.198-ga453511f0e`. **Check `agentInfo.version` first**: on 0.1.195 or earlier this test is meaningless, not merely failing. Needs a real local Adapter and a reachable inference endpoint. "Calls no tool at all" is INCONCLUSIVE and expected for a small model — record the model name |

---

Back to [the manual test script](../manual-tests.md).
