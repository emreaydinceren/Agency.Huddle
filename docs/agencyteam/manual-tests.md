# Manual test script

This is the browser test script for **Agency.Huddle** — every check a person or an agent can
make against the running app that the automated suite cannot make for you. Follow it cold: it
assumes a terminal, a browser and no knowledge of the codebase.

Applies to the repo as of 2026-09-13. Scope is the chat surface (`src/Huddle.App`) through a web
browser at `http://localhost:5100`. It does **not** cover the automated suite — you never run
`dotnet test` from this script — and it does not cover `Huddle.Console` or the ACP library in
isolation. For why a check is here rather than in code, see [Testing](testing.md); for the
vocabulary every step uses, see [Language](language.md).

**464 tests in 14 areas.** 414 are free. 50 spend real money, are marked 💰 everywhere they appear,
and are listed together in [Appendix A](#appendix-a-paid-test-register).

This page is the whole of what you need to read before testing: the cost guard, the Model and
Effort convention, and the rules for concluding a result. The tests themselves live one file per
area under [`manual-tests/`](manual-tests/), because a single file holding all
464 would be too large for a git web UI to render.

---

## 0. Read this first

### 0.1 What you need

| Requirement | Detail |
| --- | --- |
| Shell | PowerShell 7 (`pwsh`), opened at the repository root |
| Browser | Chrome or Edge, with DevTools (F12) available |
| Build | `dotnet build Huddle.slnx` succeeds with zero warnings |
| App URL | `http://localhost:5100` |
| State directory | `src\Huddle.App\App_Data` |
| Node | `node --version` prints a version (only for tests that spawn an Adapter) |
| Claude login | `claude` starts without prompting you to log in. The Adapter authenticates through that CLI login — there is no API key to set (same scope as Node: every test that spawns an Adapter, paid or not) |

Two terminals are assumed throughout. **Terminal A** runs the app and is the log oracle —
keep its output visible. **Terminal B** runs `curl.exe` and filesystem checks so they never
disturb the app.

### 0.2 The cost guard

> [!CAUTION]
> A plain `dotnet run` spends real money. `launchSettings.json` pins
> `ASPNETCORE_ENVIRONMENT=Development`, and `appsettings.Development.json` sets
> `Team:Acp:Enabled: true` — so the app starts one real `node` Adapter per Persona at startup and
> bills your Claude subscription before you type anything. This is the opposite of the shipped
> `appsettings.json` default, and it is the single easiest way to spend money by accident.

Run this in Terminal A **before every `dotnet run`** except the tests marked 💰:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Confirm the guard took, in Terminal B:

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Measure-Object | Select-Object -ExpandProperty Count
```

```text
0
```

Anything above `0` means the guard did not take. Stop the app, set the variable in the same
window, and relaunch. The variable lives only in that terminal session — reopen the terminal and
you must set it again.

Three more rules that cost money when broken:

| Rule | Why |
| --- | --- |
| Never set `TEAM_E2E`. | It switches on eight tests that drive a real Adapter. This script never runs the suite, so leave it unset. |
| Set `Team__AgentMessageBudget` low before any paid test. | The default is 40 agent Messages between Human Messages. A test hunting a loop should cap at 2. |
| Unset `ANTHROPIC_API_KEY` before paid tests. | Nothing in this script ever sets it, and it is never needed — the check is against one you inherited from a shell profile or a machine-wide variable. If one is set it silently overrides the `claude` CLI login and bills that key's account instead of your subscription. The Appendix A pre-flight block clears it. |

> [!WARNING]
> `Team:Acp:TraceWire` prints the tool server's bearer token to the console. Enable it only in a
> throwaway terminal that is not shared or recorded, clear the scrollback afterwards, and unset it
> with `Remove-Item Env:Team__Acp__TraceWire` before any other test.

### 0.3 The Model and Effort convention

These are the app's own per-Persona settings on the Teammate card, not your own tooling. They
decide what every paid test costs, so treat them as binding.

| Situation | Model | Effort |
| --- | --- | --- |
| **Every test, unless it says otherwise** | **Haiku** | **low** |
| A test that exercises *switching the Model* | Haiku **→ Sonnet** | leave at low |
| A test that exercises *switching the Effort* | leave on Haiku | low **→ medium** |

Three rules follow from that table:

- **Never select Opus**, anywhere, including when a test says "walk every entry in the list".
  Skip any entry whose label contains Opus.
- **Never raise Effort above `medium`.** `high`, `xhigh` and `max` are out of scope.
- **Restore afterwards.** A test that switches to Sonnet or to `medium` ends by switching back to
  Haiku and `low`. Later tests state Haiku/low as a precondition and their cost estimates assume it.

If the Model list offers no Haiku entry, that is an **inconclusive** result for any test that
names one — record it and move on rather than substituting a larger model.

### 0.4 Rollback

Many tests delete or edit files under `App_Data`. Take a copy before you start, and restore it
between areas:

```powershell
# Back up before the first test.
Copy-Item src\Huddle.App\App_Data "$env:TEMP\App_Data.bak" -Recurse -Force

# Restore: stop the app first, or SQLite will hold team.db open.
Remove-Item src\Huddle.App\App_Data -Recurse -Force
Copy-Item "$env:TEMP\App_Data.bak" src\Huddle.App\App_Data -Recurse -Force
```

> [!NOTE]
> `App_Data` is not tracked by git, so nothing there is recoverable from the repository. The copy
> above is the only rollback you have.

Two files are **absent on a clean install and that is correct**: `App_Data\hooks.json` appears
only on the first save from Settings, and `App_Data\appearance.json` only on the first Theme
change. Finding neither is a pass, not a defect.

### 0.5 How to conclude a result

Every test records exactly one of four outcomes. Choose by this order — the first row that
applies wins.

| Outcome | Choose it when | What to do |
| --- | --- | --- |
| **Blocked** | A precondition could not be met, or an earlier test this one depends on failed. | Record the blocking test id. Do not guess the result. |
| **Inconclusive** | You ran the steps but the environment cannot answer — no Adapter installed, no Haiku in the list, `curl.exe` missing. | Record which condition was absent. Not a defect. |
| **Fail** | Any line under *Fail if* was observed, **or** any line under *Pass if* was not. | Record what you saw, verbatim, plus the implication the *Fail if* line gives. |
| **Pass** | Every line under *Pass if* held, and no *Fail if* line was observed. | Record it and move on. |

Four rules make results comparable between testers:

1. **Pass requires all of the pass conditions**, not most. A partial pass is a Fail.
2. **Quote what you saw.** "the tile read `Degraded: the last Turn ended without a reply`" is a
   result; "the tile looked wrong" is not.
3. **A silent failure is still a failure.** Several tests in this script exist because the defect
   they hunt produces no error message anywhere. Absence of an error is never evidence of a pass —
   only the stated pass condition is.
4. **Check section 0.6 before filing a defect.** Some behaviour that looks broken is a documented
   decision.

### 0.6 Not a defect

These are documented decisions in [Known limits](known-limits.md). Observing one is a pass.

| You will see | Why it is correct |
| --- | --- |
| An Agent answering in one Room seems to know about another Room | One session per Persona spans every Room it is in, so context bleeds. A session per Room would multiply processes and cost. |
| A restart un-pauses a Room that had spent its Budget | The Budget counter is in memory and per Room, by decision. |
| A restart loses a Turn that was mid-stream | A Draft is in memory only and is never written to the Transcript. |
| Choosing **Dark** stays dark on a light OS | A Theme carries one `color-scheme`. Following the device means choosing **System**. |
| Changing the Theme reloads the whole page | `<head>` belongs to the server and Blazor's render tree cannot reach it. |
| The same Theme appears in a second browser and a private window | The choice lives in `appearance.json`, per installation, not in `localStorage`. |
| Renaming a Teammate leaves its old Rooms and Transcripts behind | Removing or renaming a Persona does not cascade into the chat surface. Only its Model and Effort follow. |
| Stopping an Agent stops it in every Room | One session spans every Room, so there is nothing narrower to stop. |
| Tool activity never appears in scrollback | It belongs to the Draft and goes when the Draft does. |
| An editing Hook does not restart a running Teammate | A `Next session` Hook is deliberately inert until that Teammate restarts. The badge says so. |
| The Model picker is stale after upgrading the Adapter | The catalog is probed once per app run and cached. Restart the app. |
| `mcp__team__` appears in tool names | Deliberate. The `Team:` config root and the `mcp__team__` prefix are the two identifiers that keep the old code name. |

### 0.7 Recording results

Copy this table per area and fill one row per test.

```text
| Test id | Outcome | Observed | Tester | Date |
| --- | --- | --- | --- | --- |
| SHELLNAV-01 | Pass |  |  |  |
```

---

## 1. Test areas

Areas are independent: run one without running another, in any order, as long as its own setup
is done. Within an area, tests run top to bottom — free tests first, paid tests last.

| # | Area | Tests | 💰 | Est. |
| --- | --- | --- | --- | --- |
| 1 | [Application shell, navigation and layout](manual-tests/shell-nav.md) | 29 | — | 2.9h |
| 2 | [Startup, configuration and first-run state](manual-tests/startup-config.md) | 34 | 2 | 3.9h |
| 3 | [Room view, posting Messages and Transcript rendering](manual-tests/room-messaging.md) | 32 | 1 | 3.5h |
| 4 | [Turn streaming, Drafts, Stop and failure surfacing](manual-tests/streaming-turn.md) | 30 | 8 | 4.4h |
| 5 | [Reply Gate, Mentions and Room Budget](manual-tests/reply-gate-budget.md) | 36 | 6 | 5.9h |
| 6 | [Teammates page: tiles, Teams grouping, filter and rejected files](manual-tests/teammates-library.md) | 40 | 1 | 4.2h |
| 7 | [Teammate card: view, edit, create, delete](manual-tests/teammate-card.md) | 50 | 1 | 6.3h |
| 8 | [Model and Effort pickers, catalog probe and runner restart](manual-tests/model-effort.md) | 27 | 1 | 3.6h |
| 9 | [Creating Rooms, inviting Agents, Room naming](manual-tests/invite-rooms.md) | 31 | 4 | 3.5h |
| 10 | [Settings: the 22 Hooks, editing, per-field reset and Save](manual-tests/hooks-settings.md) | 40 | 3 | 4.7h |
| 11 | [Appearance tab, Themes, Tokens and overrides](manual-tests/appearance-theme.md) | 25 | — | 3.2h |
| 12 | [The named pipe: external agents and the wire](manual-tests/pipe-external.md) | 36 | — | 3.9h |
| 13 | [App Tools a real model calls (progressive discovery)](manual-tests/app-tools.md) | 22 | 16 | 3.5h |
| 14 | [Persona lifecycle: supervisor, work dirs, health and restarts](manual-tests/persona-lifecycle.md) | 32 | 7 | 6.5h |

💰 marks a test that spends real money. Estimates assume you already know the app; first time
through, roughly double them.

### 1.1 Smoke pass

Twenty free tests, about 1.7 hours, covering every area at least once. Run these after any
change, and before committing to a full area. A failure here means stop and fix, not continue.

| Test id | What it proves |
| --- | --- |
| [SHELLNAV-01](manual-tests/shell-nav.md#shellnav-01-every-stylesheet-the-shell-links-is-fingerprinted-and-actually-serves) | Every stylesheet the shell links is fingerprinted and actually serves |
| [SHELLNAV-02](manual-tests/shell-nav.md#shellnav-02-both-shell-scripts-are-fingerprinted-and-serve---the-gap-the-automated-guard-does-not-cover) | Both shell scripts are fingerprinted and serve - the gap the automated guard does not cover |
| [SHELLNAV-03](manual-tests/shell-nav.md#shellnav-03-the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold) | The scoped-CSS bundle is applied: the error banner stays hidden below the fold |
| [STARTUPCONFIG-01](manual-tests/startup-config.md#startupconfig-01-the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100) | The documented launch command serves the styled app shell on http://localhost:5100 |
| [STARTUPCONFIG-03](manual-tests/startup-config.md#startupconfig-03-two-demo-rooms-echo-and-alpha-exist-at-startup-with-no-user-action) | Two demo Rooms, echo and alpha, exist at startup with no user action |
| [STARTUPCONFIG-04](manual-tests/startup-config.md#startupconfig-04-a-demo-agent-answers-only-when--mentioned-and-answers-in-bold) | A demo agent answers only when @-mentioned, and answers in bold |
| [ROOMMESSAGING-02](manual-tests/room-messaging.md#roommessaging-02-enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line) | Enter sends the Message, clears the textarea, and appends exactly one JSONL line |
| [ROOMMESSAGING-03](manual-tests/room-messaging.md#roommessaging-03-shiftenter-inserts-a-newline-and-does-not-send) | Shift+Enter inserts a newline and does not send |
| [STREAMINGTURN-02](manual-tests/streaming-turn.md#streamingturn-02-a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written) | A Draft appears as a distinct live row while a Turn is being written |
| [REPLYGATEBUDGET-01](manual-tests/reply-gate-budget.md#replygatebudget-01-a-demo-two-member-room-no-mention-is-silent-a-mention-streams-a-reply) | A demo two-Member Room: no Mention is silent, a Mention streams a reply |
| [REPLYGATEBUDGET-02](manual-tests/reply-gate-budget.md#replygatebudget-02-three-or-more-members-makes-the-same-room-mention-gated) | Three or more Members makes the same Room Mention-gated |
| [TEAMMATESLIBRARY-01](manual-tests/teammates-library.md#teammateslibrary-01-the-page-loads-is-styled-and-spawns-nothing) | The page loads, is styled, and spawns nothing |
| [TEAMMATECARD-02](manual-tests/teammate-card.md#teammatecard-02-clicking-new-teammate-opens-a-card-at-all-blank-name-crash-probe) | Clicking New teammate opens a card at all (blank-name crash probe) |
| [INVITEROOMS-03](manual-tests/invite-rooms.md#inviterooms-03-the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked) | The New chat panel toggles, lists every Agent with a status dot, and keeps Start chat disabled until something is ticked |
| [HOOKSSETTINGS-01](manual-tests/hooks-settings.md#hookssettings-01-settings-opens-on-the-hooks-tab-with-a-two-button-tab-rail) | /settings opens on the Hooks tab with a two-button tab rail |
| [HOOKSSETTINGS-04](manual-tests/hooks-settings.md#hookssettings-04-exactly-22-hook-fields-in-four-named-groups-in-a-fixed-order) | Exactly 22 hook fields, in four named groups, in a fixed order |
| [APPEARANCETHEME-03](manual-tests/appearance-theme.md#appearancetheme-03-the-theme-dropdown-offers-exactly-system-light-and-dark-in-that-order) | The Theme dropdown offers exactly System, Light and Dark, in that order |
| [APPEARANCETHEME-04](manual-tests/appearance-theme.md#appearancetheme-04-choosing-a-theme-stores-its-id-and-forces-a-full-document-load-not-an-in-place-repaint) | Choosing a Theme stores its id and forces a full document load, not an in-place repaint |
| [PIPEEXTERNAL-04](manual-tests/pipe-external.md#pipeexternal-04-a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh) | A Room appears in the sidebar the moment an external agent says hello, with no refresh |
| [PERSONALIFECYCLE-01](manual-tests/persona-lifecycle.md#personalifecycle-01-with-acp-disabled-every-teammate-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert) | With ACP disabled every teammate reads Offline with an empty tooltip and no Room raises an alert |

> [!TIP]
> Run `SHELLNAV-01` first, always. It catches the one defect in this application that produces no
> error anywhere: `@Assets["..."]` returns an unresolved key verbatim instead of throwing, so a
> stale stylesheet name renders as an ordinary-looking `href` that 404s silently. That exact
> failure shipped live for a month.

---

## Appendix A. Paid test register

Every test that spends money, in one place. 50 tests, about 11.3 hours of
wall clock. Read section 0.2 and section 0.3 before running any of them.

| Test id | Area | What it proves | Est. |
| --- | --- | --- | --- |
| [STARTUPCONFIG-33](manual-tests/startup-config.md#startupconfig-33-a-spent-per-persona-token-budget-reads-as-degraded-on-the-teammate-tile-and-in-the-room-banner) | `startup-config` | A spent per-Persona token Budget reads as Degraded on the Teammate tile and in the Room banner | 12 min |
| [STARTUPCONFIG-34](manual-tests/startup-config.md#startupconfig-34-a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again) | `startup-config` | A Human Message clears a token-Budget Degraded state and lets the Persona work again | 6 min |
| [ROOMMESSAGING-32](manual-tests/room-messaging.md#roommessaging-32-a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money) | `room-messaging` | A real Claude Agent's reply streams and renders through the same path (COSTS MONEY) | 15 min |
| [STREAMINGTURN-23](manual-tests/streaming-turn.md#streamingturn-23-a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it) | `streaming-turn` | A stored Model the adapter does not advertise degrades the Persona but does not stop it | 12 min |
| [STREAMINGTURN-24](manual-tests/streaming-turn.md#streamingturn-24-stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written) | `streaming-turn` | Stop actually ends a real Turn: the Draft goes, no Message is posted, nothing is written | 10 min |
| [STREAMINGTURN-25](manual-tests/streaming-turn.md#streamingturn-25-a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak) | `streaming-turn` | A stopped Turn is not a failure: no alert strip, no Degraded badge, no broken streak | 12 min |
| [STREAMINGTURN-26](manual-tests/streaming-turn.md#streamingturn-26-stop-means-this-agent-now-everything-queued-behind-the-live-turn-is-discarded-too) | `streaming-turn` | Stop means this Agent now: everything queued behind the live Turn is discarded too | 10 min |
| [STREAMINGTURN-27](manual-tests/streaming-turn.md#streamingturn-27-stopping-an-agent-stops-it-in-every-room-not-just-the-one-you-clicked-in) | `streaming-turn` | Stopping an Agent stops it in every Room, not just the one you clicked in | 12 min |
| [STREAMINGTURN-28](manual-tests/streaming-turn.md#streamingturn-28-a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it) | `streaming-turn` | A spent per-Persona token Budget reads as Degraded, and a Human Message clears it | 10 min |
| [STREAMINGTURN-29](manual-tests/streaming-turn.md#streamingturn-29-a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row) | `streaming-turn` | A Turn that fails mid-flight reports Degraded in the Adapter's own words, and escalates on the third failure in a row | 15 min |
| [STREAMINGTURN-30](manual-tests/streaming-turn.md#streamingturn-30-killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever) | `streaming-turn` | Killing the adapter process mid-Turn clears the Draft and says so, instead of deafening the Agent forever | 15 min |
| [REPLYGATEBUDGET-31](manual-tests/reply-gate-budget.md#replygatebudget-31-paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all) | `reply-gate-budget` | PAID: a real Persona answers a two-Member Room with no Mention at all | 15 min |
| [REPLYGATEBUDGET-32](manual-tests/reply-gate-budget.md#replygatebudget-32-paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention) | `reply-gate-budget` | PAID: an un-mentioned Message is not answered but rides along as context on the next Mention | 15 min |
| [REPLYGATEBUDGET-33](manual-tests/reply-gate-budget.md#replygatebudget-33-paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn) | `reply-gate-budget` | PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn | 25 min |
| [REPLYGATEBUDGET-34](manual-tests/reply-gate-budget.md#replygatebudget-34-paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up) | `reply-gate-budget` | PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up | 25 min |
| [REPLYGATEBUDGET-35](manual-tests/reply-gate-budget.md#replygatebudget-35-paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it) | `reply-gate-budget` | PAID: the Agent-facing post tool returns the same terminal refusal, and the model obeys it | 20 min |
| [REPLYGATEBUDGET-36](manual-tests/reply-gate-budget.md#replygatebudget-36-paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it) | `reply-gate-budget` | PAID: an Agent can mint a fresh Budget by creating a Room, and only the token Budget catches it | 25 min |
| [TEAMMATESLIBRARY-40](manual-tests/teammates-library.md#teammateslibrary-40-degraded-with-the-persistence-reason-and-the-two-things-that-must-not-badge) | `teammates-library` | Degraded, with the persistence reason — and the two things that must NOT badge | 20 min |
| [TEAMMATECARD-50](manual-tests/teammate-card.md#teammatecard-50-editing-a-teammate-really-does-lose-its-conversation-memory-costs-money) | `teammate-card` | Editing a teammate really does lose its conversation memory (COSTS MONEY) | 12 min |
| [MODELEFFORT-27](manual-tests/model-effort.md#modeleffort-27-money-the-chosen-model-and-effort-actually-reach-the-model---ask-it) | `model-effort` | MONEY: the chosen Model and Effort actually reach the model - ask it | 15 min |
| [INVITEROOMS-28](manual-tests/invite-rooms.md#inviterooms-28-an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar) | `invite-rooms` | An Agent creates a Room with mcp__team__create_room and it appears live in the sidebar | 15 min |
| [INVITEROOMS-29](manual-tests/invite-rooms.md#inviterooms-29-an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room-label) | `invite-rooms` | An Agent invites another with mcp__team__invite_agent, using the room id from its own [Room: …] label | 15 min |
| [INVITEROOMS-30](manual-tests/invite-rooms.md#inviterooms-30-an-agent-can-invite-into-a-room-it-is-not-a-member-of) | `invite-rooms` | An Agent can invite into a Room it is not a Member of | 10 min |
| [INVITEROOMS-31](manual-tests/invite-rooms.md#inviterooms-31-every-agent-created-room-contains-the-human-so-none-is-hidden) | `invite-rooms` | Every Agent-created Room contains the Human, so none is hidden | 6 min |
| [HOOKSSETTINGS-38](manual-tests/hooks-settings.md#hookssettings-38-costs-money-a-live-hook-edit-reaches-the-very-next-turn-with-no-restart) | `hooks-settings` | COSTS MONEY: a Live hook edit reaches the very next Turn with no restart | 15 min |
| [HOOKSSETTINGS-39](manual-tests/hooks-settings.md#hookssettings-39-costs-money-a-next-session-hook-edit-is-silently-inert-on-a-running-teammate-until-it-restarts) | `hooks-settings` | COSTS MONEY: a "Next session" hook edit is silently inert on a running teammate until it restarts | 20 min |
| [HOOKSSETTINGS-40](manual-tests/hooks-settings.md#hookssettings-40-costs-money-get_help-re-renders-on-every-call-so-its-nine-hooks-land-on-the-next-call-while-a-tool-description-does-not) | `hooks-settings` | COSTS MONEY: get_help re-renders on every call, so its nine hooks land on the next call — while a tool DESCRIPTION does not | 20 min |
| [APPTOOLS-05](manual-tests/app-tools.md#apptools-05-an-agent-reads-its-own-rooms-id-off-the-room-label) | `app-tools` | An Agent reads its own Room's id off the [Room: ...] label | 5 min |
| [APPTOOLS-06](manual-tests/app-tools.md#apptools-06-get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live) | `app-tools` | get_help's BUDGET and MESSAGES sections prove progressive discovery is live | 8 min |
| [APPTOOLS-07](manual-tests/app-tools.md#apptools-07-asked-what-tools-it-has-a-teammate-calls-get_help-and-reports-the-real-catalog-with-descriptions) | `app-tools` | Asked what tools it has, a teammate calls get_help and reports the real catalog with descriptions | 10 min |
| [APPTOOLS-08](manual-tests/app-tools.md#apptools-08-tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight) | `app-tools` | Tool activity is visible under the Draft while a call is in flight | 5 min |
| [APPTOOLS-10](manual-tests/app-tools.md#apptools-10-list_agents-reports-real-names-real-online-state-and-real-job-descriptions) | `app-tools` | list_agents reports real names, real online state, and real job descriptions | 12 min |
| [APPTOOLS-11](manual-tests/app-tools.md#apptools-11-an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent) | `app-tools` | An Alias is advertised by list_agents and then accepted by invite_agent | 15 min |
| [APPTOOLS-12](manual-tests/app-tools.md#apptools-12-invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live) | `app-tools` | invite_agent, called with the id from the agent's own label, renames the room live | 12 min |
| [APPTOOLS-13](manual-tests/app-tools.md#apptools-13-invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member) | `app-tools` | invite_agent is idempotent and says so rather than duplicating a member | 6 min |
| [APPTOOLS-14](manual-tests/app-tools.md#apptools-14-invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist) | `app-tools` | invite_agent with an unknown name comes back with the names that do exist | 6 min |
| [APPTOOLS-15](manual-tests/app-tools.md#apptools-15-an-agents-reply-is-delivered-once-it-must-not-also-post-the-same-text-with-post_message) | `app-tools` | An agent's reply is delivered once — it must not also post the same text with post_message | 6 min |
| [APPTOOLS-16](manual-tests/app-tools.md#apptools-16-create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents) | `app-tools` | create_room makes a new Room that appears in the sidebar live, named after its Agents | 10 min |
| [APPTOOLS-17](manual-tests/app-tools.md#apptools-17-post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post) | `app-tools` | post_message delivers into a Room other than the one the agent was addressed in, and the agent does not answer its own post | 12 min |
| [APPTOOLS-18](manual-tests/app-tools.md#apptools-18-tools-are-the-source-of-truth-the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase) | `app-tools` | Tools are the source of truth — the agent must not answer about agents or Rooms from the codebase | 10 min |
| [APPTOOLS-19](manual-tests/app-tools.md#apptools-19-a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute) | `app-tools` | A budgetExhausted refusal from post_message is terminal: the agent stops, does not retry and does not reroute | 15 min |
| [APPTOOLS-21](manual-tests/app-tools.md#apptools-21-continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload) | `app-tools` | Continue re-delivers the paused Message, and the prompt survives a page reload | 12 min |
| [APPTOOLS-22](manual-tests/app-tools.md#apptools-22-editing-a-hook-changes-model-facing-text-without-restarting-the-session-and-next-session-hooks-wait-for-a-restart) | `app-tools` | Editing a hook changes model-facing text without restarting the session, and Next session hooks wait for a restart | 15 min |
| [PERSONALIFECYCLE-26](manual-tests/persona-lifecycle.md#personalifecycle-26-money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-teammate) | `persona-lifecycle` | MONEY: saving the Edit card with nothing changed does NOT restart the Teammate | 12 min |
| [PERSONALIFECYCLE-27](manual-tests/persona-lifecycle.md#personalifecycle-27-money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect) | `persona-lifecycle` | MONEY: editing the Persona body restarts the session and the new instruction takes effect | 12 min |
| [PERSONALIFECYCLE-28](manual-tests/persona-lifecycle.md#personalifecycle-28-money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-teammate-remembers) | `persona-lifecycle` | MONEY: changing the Model from Haiku to Sonnet restarts the session and clears what the Teammate remembers | 18 min |
| [PERSONALIFECYCLE-29](manual-tests/persona-lifecycle.md#personalifecycle-29-money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection) | `persona-lifecycle` | MONEY: changing the Effort from low to medium restarts the session, and a Model change clears the Effort selection | 15 min |
| [PERSONALIFECYCLE-30](manual-tests/persona-lifecycle.md#personalifecycle-30-money-context-bleeds-between-rooms-confirm-the-known-limit-and-that-replies-still-land-in-the-right-room) | `persona-lifecycle` | MONEY: context bleeds between Rooms — confirm the known limit, and that replies still land in the right Room | 12 min |
| [PERSONALIFECYCLE-31](manual-tests/persona-lifecycle.md#personalifecycle-31-money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it) | `persona-lifecycle` | MONEY: the per-Persona token Budget shows as Degraded with its reason, and any Human Message clears it | 25 min |
| [PERSONALIFECYCLE-32](manual-tests/persona-lifecycle.md#personalifecycle-32-money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange) | `persona-lifecycle` | MONEY: a token Budget of zero disables the per-Persona cap, and the per-Room Budget still stops the exchange | 20 min |

Before the first paid test of a session:

```powershell
Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue   # clears an inherited key; the script never sets one
Remove-Item Env:TEAM_E2E -ErrorAction SilentlyContinue
$env:Team__AgentMessageBudget = '4'        # the test will name a smaller number if it needs one
$env:Team__Acp__TokenBudget   = '200000'   # a ceiling per Persona, not a target
$env:Team__Acp__Enabled       = 'true'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

After the last one, confirm nothing is still running and billing:

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Select-Object ProcessId, CommandLine
```

```text

```

Empty output is the pass. Any row is an orphaned Adapter holding a live session open — stop it
with `Stop-Process -Id <ProcessId>`.

## Appendix B. Corrections applied to this script

This script was drafted from the source and the documentation, then reviewed by three independent
passes — completeness, followability, and cost safety. These corrections were applied. They are
listed because each one is a mistake a reader might otherwise reintroduce.

| Correction | Reason |
| --- | --- |
| Two areas launched the app without the cost guard, and three asserted that `Team:Acp:Enabled` defaults to `false`. | It does not. The Development profile sets it `true`. The claim would have made 65 "free" tests spend money. |
| `STREAMINGTURN-23` was marked free but contained a paid step. | A test cannot be half-free, or filtering this script by cost is not trustworthy. |
| `MODELEFFORT-02` walked every entry in the Model list, including Opus. | Section 0.3 excludes Opus everywhere. |
| `PERSONALIFECYCLE-28` and `-29` switched to Sonnet and to `medium` and never switched back. | Later tests state Haiku/low as a precondition. |
| `ROOMMESSAGING-32` cleared `Team__AgentMessageBudget` instead of lowering it. | Clearing restores the default of 40, which is 40 Turns of exposure on a test needing one. |
| `APPTOOLS-17` hunted an echo loop at a Budget of 40. | Pinned to 2. A loop caught at 2 proves the same defect for a twentieth of the cost. |
| `STARTUPCONFIG-25` ran a second instance against the same `App_Data`. | ADR-0002 requires a single writer per Room. Now uses `Team__DataDir=App_Data2`. |
| `SHELLNAV-09` asserted an `aria-current` attribute. | Blazor's `NavLink` has never emitted it; the assertion would fail against correct code. |
| Two tests instructed the tester to run `dotnet test Huddle.slnx --`. | Out of scope: this script tests through the browser, and `TEAM_E2E` makes running the suite a spending risk. |
| A Trace-logging step used `$env:Logging__LogLevel__Agency__Huddle` and a quoted variant. | Neither works. The form that parses is `${env:Logging__LogLevel__Agency.Huddle}`. |
| Steps wrote rejected Persona fixtures to `App_Data\personas\`. | The library is `{DataDir}/{Acp:TeamsDir}`, which is `App_Data\Teams\`. |
| `ANTHROPIC_API_KEY` was ordered unset without ever being set. | It reads as a dangling step and invites a reader to drop it. The variable is ambient — inherited from a shell profile — and the Adapter authenticates through the `claude` CLI login instead. Section 0.1 now carries that as a requirement. |

## Appendix C. Known gaps in this script

Stated so nobody assumes coverage that is not here.

- **A real model writing a multi-word Mention.** Every multi-word Mention test has the *Human*
  typing `@Chief of Staff`. The documented crux — a real model writing it out in full so the
  server resolves it — is not covered by any test here.
- **`mcp__team__create_room` with an Alias.** Three of the four doors an Alias must open are
  tested; `create_room` is not, and it carries its own copy of the alias fallback.
- **What `list_agents` composes into a job description.** No test checks that `_`-prefixed
  frontmatter fields are withheld. A regression there sends reserved data to every model and is
  invisible on every UI surface.
- **A Turn that completes without producing a reply.** `MaxTokens`, `MaxTurnRequests` and
  `Refusal` each produce a Degraded state with its own wording. No test covers that family, and
  it is the one failure where the Room shows nothing at all.
- **The tool server's bearer token being required.** Nothing proves an unauthenticated loopback
  call is refused.
- **A Name with a trailing newline.** `NameRules` anchors with `\A`/`\z` precisely because .NET's
  `$` matches before a trailing newline. No test sends `"name":"mybot\n"` on the wire.
- **`HookCatalog` being the authority.** No test deletes `hooks.default.json` to prove the app
  still runs on the text it shipped with.
