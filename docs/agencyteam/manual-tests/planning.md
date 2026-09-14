# Manual test script — planning

Which of the 464 tests to run, what each area covers, and which ones spend money.
Read this to **choose** a run. You do not need it while executing one: the rules
that bind a run live in [the script](../manual-tests.md), which is the page to
have open instead.

Results go in the [Tracker](tracker.md), not here — this page is what to run, that
one is what happened.

Back to [the manual test script](../manual-tests.md).

---

## 1. Test areas

Areas are independent: run one without running another, in any order, as long as its own setup
is done. Within an area, tests run top to bottom — free tests first, paid tests last.

| # | Area | Tests | 💰 | Est. |
| --- | --- | --- | --- | --- |
| 1 | [Application shell, navigation and layout](shell-nav.md) | 29 | — | 2.9h |
| 2 | [Startup, configuration and first-run state](startup-config.md) | 34 | 2 | 3.9h |
| 3 | [Room view, posting Messages and Transcript rendering](room-messaging.md) | 32 | 1 | 3.5h |
| 4 | [Turn streaming, Drafts, Stop and failure surfacing](streaming-turn.md) | 30 | 8 | 4.4h |
| 5 | [Reply Gate, Mentions and Room Budget](reply-gate-budget.md) | 36 | 6 | 5.9h |
| 6 | [Teammates page: tiles, Teams grouping, filter and rejected files](teammates-library.md) | 40 | 1 | 4.2h |
| 7 | [Teammate card: view, edit, create, delete](teammate-card.md) | 50 | 1 | 6.3h |
| 8 | [Model and Effort pickers, catalog probe and runner restart](model-effort.md) | 27 | 1 | 3.6h |
| 9 | [Creating Rooms, inviting Agents, Room naming](invite-rooms.md) | 31 | 4 | 3.5h |
| 10 | [Settings: the 22 Hooks, editing, per-field reset and Save](hooks-settings.md) | 40 | 3 | 4.7h |
| 11 | [Appearance tab, Themes, Tokens and overrides](appearance-theme.md) | 25 | — | 3.2h |
| 12 | [The named pipe: external agents and the wire](pipe-external.md) | 36 | — | 3.9h |
| 13 | [App Tools a real model calls (progressive discovery)](app-tools.md) | 22 | 16 | 3.5h |
| 14 | [Persona lifecycle: supervisor, work dirs, health and restarts](persona-lifecycle.md) | 32 | 7 | 6.5h |

💰 marks a test that spends real money. Estimates assume you already know the app; first time
through, roughly double them.

### 1.1 Smoke pass

Twenty free tests, about 1.7 hours, covering every area at least once. Run these after any
change, and before committing to a full area. A failure here means stop and fix, not continue.

| Test id | What it proves |
| --- | --- |
| [SHELLNAV-01](shell-nav.md#shellnav-01-every-stylesheet-the-shell-links-is-fingerprinted-and-actually-serves) | Every stylesheet the shell links is fingerprinted and actually serves |
| [SHELLNAV-02](shell-nav.md#shellnav-02-both-shell-scripts-are-fingerprinted-and-serve---the-gap-the-automated-guard-does-not-cover) | Both shell scripts are fingerprinted and serve - the gap the automated guard does not cover |
| [SHELLNAV-03](shell-nav.md#shellnav-03-the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold) | The scoped-CSS bundle is applied: the error banner stays hidden below the fold |
| [STARTUPCONFIG-01](startup-config.md#startupconfig-01-the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100) | The documented launch command serves the styled app shell on http://localhost:5100 |
| [STARTUPCONFIG-03](startup-config.md#startupconfig-03-two-demo-rooms-echo-and-alpha-exist-at-startup-with-no-user-action) | Two demo Rooms, echo and alpha, exist at startup with no user action |
| [STARTUPCONFIG-04](startup-config.md#startupconfig-04-a-demo-agent-answers-only-when--mentioned-and-answers-in-bold) | A demo agent answers only when @-mentioned, and answers in bold |
| [ROOMMESSAGING-02](room-messaging.md#roommessaging-02-enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line) | Enter sends the Message, clears the textarea, and appends exactly one JSONL line |
| [ROOMMESSAGING-03](room-messaging.md#roommessaging-03-shiftenter-inserts-a-newline-and-does-not-send) | Shift+Enter inserts a newline and does not send |
| [STREAMINGTURN-02](streaming-turn.md#streamingturn-02-a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written) | A Draft appears as a distinct live row while a Turn is being written |
| [REPLYGATEBUDGET-01](reply-gate-budget.md#replygatebudget-01-a-demo-two-member-room-no-mention-is-silent-a-mention-streams-a-reply) | A demo two-Member Room: no Mention is silent, a Mention streams a reply |
| [REPLYGATEBUDGET-02](reply-gate-budget.md#replygatebudget-02-three-or-more-members-makes-the-same-room-mention-gated) | Three or more Members makes the same Room Mention-gated |
| [TEAMMATESLIBRARY-01](teammates-library.md#teammateslibrary-01-the-page-loads-is-styled-and-spawns-nothing) | The page loads, is styled, and spawns nothing |
| [TEAMMATECARD-02](teammate-card.md#teammatecard-02-clicking-new-teammate-opens-a-card-at-all-blank-name-crash-probe) | Clicking New teammate opens a card at all (blank-name crash probe) |
| [INVITEROOMS-03](invite-rooms.md#inviterooms-03-the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked) | The New chat panel toggles, lists every Agent with a status dot, and keeps Start chat disabled until something is ticked |
| [HOOKSSETTINGS-01](hooks-settings.md#hookssettings-01-settings-opens-on-the-hooks-tab-with-a-two-button-tab-rail) | /settings opens on the Hooks tab with a two-button tab rail |
| [HOOKSSETTINGS-04](hooks-settings.md#hookssettings-04-exactly-22-hook-fields-in-four-named-groups-in-a-fixed-order) | Exactly 22 hook fields, in four named groups, in a fixed order |
| [APPEARANCETHEME-03](appearance-theme.md#appearancetheme-03-the-theme-dropdown-offers-exactly-system-light-and-dark-in-that-order) | The Theme dropdown offers exactly System, Light and Dark, in that order |
| [APPEARANCETHEME-04](appearance-theme.md#appearancetheme-04-choosing-a-theme-stores-its-id-and-forces-a-full-document-load-not-an-in-place-repaint) | Choosing a Theme stores its id and forces a full document load, not an in-place repaint |
| [PIPEEXTERNAL-04](pipe-external.md#pipeexternal-04-a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh) | A Room appears in the sidebar the moment an external agent says hello, with no refresh |
| [PERSONALIFECYCLE-01](persona-lifecycle.md#personalifecycle-01-with-acp-disabled-every-teammate-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert) | With ACP disabled every teammate reads Offline with an empty tooltip and no Room raises an alert |

> [!TIP]
> Run `SHELLNAV-01` first, always. It catches the one defect in this application that produces no
> error anywhere: `@Assets["..."]` returns an unresolved key verbatim instead of throwing, so a
> stale stylesheet name renders as an ordinary-looking `href` that 404s silently. That exact
> failure shipped live for a month.

---

## Appendix A. Paid test register

Every test that spends money, in one place. 50 tests, about 11.3 hours of
wall clock. Read [section 0.2](../manual-tests.md#02-the-cost-guard) and [section 0.3](../manual-tests.md#03-the-model-and-effort-convention) before running any of them.

| Test id | Area | What it proves | Est. |
| --- | --- | --- | --- |
| [STARTUPCONFIG-33](startup-config.md#startupconfig-33-a-spent-per-persona-token-budget-reads-as-degraded-on-the-teammate-tile-and-in-the-room-banner) | `startup-config` | A spent per-Persona token Budget reads as Degraded on the Teammate tile and in the Room banner | 12 min |
| [STARTUPCONFIG-34](startup-config.md#startupconfig-34-a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again) | `startup-config` | A Human Message clears a token-Budget Degraded state and lets the Persona work again | 6 min |
| [ROOMMESSAGING-32](room-messaging.md#roommessaging-32-a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money) | `room-messaging` | A real Claude Agent's reply streams and renders through the same path (COSTS MONEY) | 15 min |
| [STREAMINGTURN-23](streaming-turn.md#streamingturn-23-a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it) | `streaming-turn` | A stored Model the adapter does not advertise degrades the Persona but does not stop it | 12 min |
| [STREAMINGTURN-24](streaming-turn.md#streamingturn-24-stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written) | `streaming-turn` | Stop actually ends a real Turn: the Draft goes, no Message is posted, nothing is written | 10 min |
| [STREAMINGTURN-25](streaming-turn.md#streamingturn-25-a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak) | `streaming-turn` | A stopped Turn is not a failure: no alert strip, no Degraded badge, no broken streak | 12 min |
| [STREAMINGTURN-26](streaming-turn.md#streamingturn-26-stop-means-this-agent-now-everything-queued-behind-the-live-turn-is-discarded-too) | `streaming-turn` | Stop means this Agent now: everything queued behind the live Turn is discarded too | 10 min |
| [STREAMINGTURN-27](streaming-turn.md#streamingturn-27-stopping-an-agent-stops-it-in-every-room-not-just-the-one-you-clicked-in) | `streaming-turn` | Stopping an Agent stops it in every Room, not just the one you clicked in | 12 min |
| [STREAMINGTURN-28](streaming-turn.md#streamingturn-28-a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it) | `streaming-turn` | A spent per-Persona token Budget reads as Degraded, and a Human Message clears it | 10 min |
| [STREAMINGTURN-29](streaming-turn.md#streamingturn-29-a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row) | `streaming-turn` | A Turn that fails mid-flight reports Degraded in the Adapter's own words, and escalates on the third failure in a row | 15 min |
| [STREAMINGTURN-30](streaming-turn.md#streamingturn-30-killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever) | `streaming-turn` | Killing the adapter process mid-Turn clears the Draft and says so, instead of deafening the Agent forever | 15 min |
| [REPLYGATEBUDGET-31](reply-gate-budget.md#replygatebudget-31-paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all) | `reply-gate-budget` | PAID: a real Persona answers a two-Member Room with no Mention at all | 15 min |
| [REPLYGATEBUDGET-32](reply-gate-budget.md#replygatebudget-32-paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention) | `reply-gate-budget` | PAID: an un-mentioned Message is not answered but rides along as context on the next Mention | 15 min |
| [REPLYGATEBUDGET-33](reply-gate-budget.md#replygatebudget-33-paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn) | `reply-gate-budget` | PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn | 25 min |
| [REPLYGATEBUDGET-34](reply-gate-budget.md#replygatebudget-34-paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up) | `reply-gate-budget` | PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up | 25 min |
| [REPLYGATEBUDGET-35](reply-gate-budget.md#replygatebudget-35-paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it) | `reply-gate-budget` | PAID: the Agent-facing post tool returns the same terminal refusal, and the model obeys it | 20 min |
| [REPLYGATEBUDGET-36](reply-gate-budget.md#replygatebudget-36-paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it) | `reply-gate-budget` | PAID: an Agent can mint a fresh Budget by creating a Room, and only the token Budget catches it | 25 min |
| [TEAMMATESLIBRARY-40](teammates-library.md#teammateslibrary-40-degraded-with-the-persistence-reason-and-the-two-things-that-must-not-badge) | `teammates-library` | Degraded, with the persistence reason — and the two things that must NOT badge | 20 min |
| [TEAMMATECARD-50](teammate-card.md#teammatecard-50-editing-a-teammate-really-does-lose-its-conversation-memory-costs-money) | `teammate-card` | Editing a teammate really does lose its conversation memory (COSTS MONEY) | 12 min |
| [MODELEFFORT-27](model-effort.md#modeleffort-27-money-the-chosen-model-and-effort-actually-reach-the-model---ask-it) | `model-effort` | MONEY: the chosen Model and Effort actually reach the model - ask it | 15 min |
| [INVITEROOMS-28](invite-rooms.md#inviterooms-28-an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar) | `invite-rooms` | An Agent creates a Room with mcp__team__create_room and it appears live in the sidebar | 15 min |
| [INVITEROOMS-29](invite-rooms.md#inviterooms-29-an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room-label) | `invite-rooms` | An Agent invites another with mcp__team__invite_agent, using the room id from its own [Room: …] label | 15 min |
| [INVITEROOMS-30](invite-rooms.md#inviterooms-30-an-agent-can-invite-into-a-room-it-is-not-a-member-of) | `invite-rooms` | An Agent can invite into a Room it is not a Member of | 10 min |
| [INVITEROOMS-31](invite-rooms.md#inviterooms-31-every-agent-created-room-contains-the-human-so-none-is-hidden) | `invite-rooms` | Every Agent-created Room contains the Human, so none is hidden | 6 min |
| [HOOKSSETTINGS-38](hooks-settings.md#hookssettings-38-costs-money-a-live-hook-edit-reaches-the-very-next-turn-with-no-restart) | `hooks-settings` | COSTS MONEY: a Live hook edit reaches the very next Turn with no restart | 15 min |
| [HOOKSSETTINGS-39](hooks-settings.md#hookssettings-39-costs-money-a-next-session-hook-edit-is-silently-inert-on-a-running-teammate-until-it-restarts) | `hooks-settings` | COSTS MONEY: a "Next session" hook edit is silently inert on a running teammate until it restarts | 20 min |
| [HOOKSSETTINGS-40](hooks-settings.md#hookssettings-40-costs-money-get_help-re-renders-on-every-call-so-its-nine-hooks-land-on-the-next-call-while-a-tool-description-does-not) | `hooks-settings` | COSTS MONEY: get_help re-renders on every call, so its nine hooks land on the next call — while a tool DESCRIPTION does not | 20 min |
| [APPTOOLS-05](app-tools.md#apptools-05-an-agent-reads-its-own-rooms-id-off-the-room-label) | `app-tools` | An Agent reads its own Room's id off the [Room: ...] label | 5 min |
| [APPTOOLS-06](app-tools.md#apptools-06-get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live) | `app-tools` | get_help's BUDGET and MESSAGES sections prove progressive discovery is live | 8 min |
| [APPTOOLS-07](app-tools.md#apptools-07-asked-what-tools-it-has-a-teammate-calls-get_help-and-reports-the-real-catalog-with-descriptions) | `app-tools` | Asked what tools it has, a teammate calls get_help and reports the real catalog with descriptions | 10 min |
| [APPTOOLS-08](app-tools.md#apptools-08-tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight) | `app-tools` | Tool activity is visible under the Draft while a call is in flight | 5 min |
| [APPTOOLS-10](app-tools.md#apptools-10-list_agents-reports-real-names-real-online-state-and-real-job-descriptions) | `app-tools` | list_agents reports real names, real online state, and real job descriptions | 12 min |
| [APPTOOLS-11](app-tools.md#apptools-11-an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent) | `app-tools` | An Alias is advertised by list_agents and then accepted by invite_agent | 15 min |
| [APPTOOLS-12](app-tools.md#apptools-12-invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live) | `app-tools` | invite_agent, called with the id from the agent's own label, renames the room live | 12 min |
| [APPTOOLS-13](app-tools.md#apptools-13-invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member) | `app-tools` | invite_agent is idempotent and says so rather than duplicating a member | 6 min |
| [APPTOOLS-14](app-tools.md#apptools-14-invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist) | `app-tools` | invite_agent with an unknown name comes back with the names that do exist | 6 min |
| [APPTOOLS-15](app-tools.md#apptools-15-an-agents-reply-is-delivered-once-it-must-not-also-post-the-same-text-with-post_message) | `app-tools` | An agent's reply is delivered once — it must not also post the same text with post_message | 6 min |
| [APPTOOLS-16](app-tools.md#apptools-16-create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents) | `app-tools` | create_room makes a new Room that appears in the sidebar live, named after its Agents | 10 min |
| [APPTOOLS-17](app-tools.md#apptools-17-post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post) | `app-tools` | post_message delivers into a Room other than the one the agent was addressed in, and the agent does not answer its own post | 12 min |
| [APPTOOLS-18](app-tools.md#apptools-18-tools-are-the-source-of-truth-the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase) | `app-tools` | Tools are the source of truth — the agent must not answer about agents or Rooms from the codebase | 10 min |
| [APPTOOLS-19](app-tools.md#apptools-19-a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute) | `app-tools` | A budgetExhausted refusal from post_message is terminal: the agent stops, does not retry and does not reroute | 15 min |
| [APPTOOLS-21](app-tools.md#apptools-21-continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload) | `app-tools` | Continue re-delivers the paused Message, and the prompt survives a page reload | 12 min |
| [APPTOOLS-22](app-tools.md#apptools-22-editing-a-hook-changes-model-facing-text-without-restarting-the-session-and-next-session-hooks-wait-for-a-restart) | `app-tools` | Editing a hook changes model-facing text without restarting the session, and Next session hooks wait for a restart | 15 min |
| [PERSONALIFECYCLE-26](persona-lifecycle.md#personalifecycle-26-money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-teammate) | `persona-lifecycle` | MONEY: saving the Edit card with nothing changed does NOT restart the Teammate | 12 min |
| [PERSONALIFECYCLE-27](persona-lifecycle.md#personalifecycle-27-money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect) | `persona-lifecycle` | MONEY: editing the Persona body restarts the session and the new instruction takes effect | 12 min |
| [PERSONALIFECYCLE-28](persona-lifecycle.md#personalifecycle-28-money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-teammate-remembers) | `persona-lifecycle` | MONEY: changing the Model from Haiku to Sonnet restarts the session and clears what the Teammate remembers | 18 min |
| [PERSONALIFECYCLE-29](persona-lifecycle.md#personalifecycle-29-money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection) | `persona-lifecycle` | MONEY: changing the Effort from low to medium restarts the session, and a Model change clears the Effort selection | 15 min |
| [PERSONALIFECYCLE-30](persona-lifecycle.md#personalifecycle-30-money-context-bleeds-between-rooms-confirm-the-known-limit-and-that-replies-still-land-in-the-right-room) | `persona-lifecycle` | MONEY: context bleeds between Rooms — confirm the known limit, and that replies still land in the right Room | 12 min |
| [PERSONALIFECYCLE-31](persona-lifecycle.md#personalifecycle-31-money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it) | `persona-lifecycle` | MONEY: the per-Persona token Budget shows as Degraded with its reason, and any Human Message clears it | 25 min |
| [PERSONALIFECYCLE-32](persona-lifecycle.md#personalifecycle-32-money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange) | `persona-lifecycle` | MONEY: a token Budget of zero disables the per-Persona cap, and the per-Room Budget still stops the exchange | 20 min |

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
| `MODELEFFORT-02` walked every entry in the Model list, including Opus. | [Section 0.3](../manual-tests.md#03-the-model-and-effort-convention) excludes Opus everywhere. |
| `PERSONALIFECYCLE-28` and `-29` switched to Sonnet and to `medium` and never switched back. | Later tests state Haiku/low as a precondition. |
| `ROOMMESSAGING-32` cleared `Team__AgentMessageBudget` instead of lowering it. | Clearing restores the default of 40, which is 40 Turns of exposure on a test needing one. |
| `APPTOOLS-17` hunted an echo loop at a Budget of 40. | Pinned to 2. A loop caught at 2 proves the same defect for a twentieth of the cost. |
| `STARTUPCONFIG-25` ran a second instance against the same `App_Data`. | ADR-0002 requires a single writer per Room. Now uses `Team__DataDir=App_Data2`. |
| `SHELLNAV-09`'s `aria-current` assertion was removed on the grounds that Blazor's `NavLink` never emits it. | Reinstated 2026-09-14. `NavLink` has emitted `aria-current="page"` since .NET 6, and this build does: the room list is a plain `<NavLink>` in `Components/Shared/RoomList.razor`, and the active link carries the attribute. Removing the check left the test's own Pass-if line quoting output no step produced, and dropped the one oracle a visual check cannot give. |
| `ROOMMESSAGING-30` tried to reach a per-Room Budget of 2 by sending two Human Messages in a row, and to provoke a refusal with a third. | Neither works, for the same reason `STARTUPCONFIG-33` did not: a Human Message RESETS the counter, so alternating Human/agent Messages can never exceed 1 - each Message you type zeroes it and draws exactly one reply. Verified on 2026-09-14: two `hi @echo` in a two-member Room both left the note at `1 of 2`. Two replies must arrive BETWEEN two Human Messages, which needs two Agents in the Room; inviting `alpha` and sending one `hi @echo and @alpha` produced the pause panel immediately. The refusal needs its own run at a budget of 1, where the second Agent's reply is refused - that produced the exact log line. Both steps now say so, and the note cross-references the same rule in `STARTUPCONFIG-33`. |
| `ROOMMESSAGING-12` expected the JSONL to hold a raw `"`, `&` and `—`, escaping only the emoji and CJK. | Everything is escaped: `"`, `&`, `—`, `😀`, `日本語`. That is `System.Text.Json`'s default `JavaScriptEncoder`, which escapes `"`, `&`, `<`, `>`, `'` and `+` as well as all non-ASCII. The mismatch is in the SAFE direction - more escaping, and the round trip is lossless - but as written three Pass-if lines fail against correct code. |
| `STARTUPCONFIG-33` tried to spend a per-Persona token Budget by sending two Human Messages in a row. | It cannot work, and the money is spent finding that out. Every Human Message zeroes the counter - `PersonaRunner` does `Interlocked.Exchange(ref this.tokensConsumed, 0)` on seeing one - and the cap is checked at the start of a work item, so a Turn you prompted yourself is always measured against zero. The cap bounds UNATTENDED spend by design. Worse, the Inconclusive clause told a tester who saw a reply to send ANOTHER Message, which resets it again; four paid Turns in a row were taken on 2026-09-14 with the state never changing. The test now uses a second Persona so one Agent's reply wakes the other with no Human Message in between - which produced the Degraded state, the exact tooltip and the exact console line on the first attempt. Usage is not the problem: the adapter's first `usage_update` reported `used=49628` of `size=200000`, twenty-five times the Budget. |
| `STARTUPCONFIG-32` set only `Team__Acp__TraceWire` and expected wire traffic in the console. | Nothing appears. `LoggerTraceListener` forwards the dotacp trace to `ILogger` at **Trace** level, and `appsettings.Development.json` pins `Agency.Huddle` at `Debug`, which does not include Trace - so the flag binds, the listener attaches, and every line is filtered out. Exactly the ambiguity the test's own Inconclusive clause warns about, except the mismatch is the LEVEL, not the category. The step now also sets `${env:Logging__LogLevel__Agency.Huddle} = 'Trace'`; with that, the run produced 117 `Agency.Huddle.Acp.Wire` lines, 81 `trce:` lines and one `Authorization: Bearer` line, against 0 of each without it. |
| `STARTUPCONFIG-30` renamed one `Logging:LogLevel` key in `appsettings.Development.json` and expected the `Agency.Huddle` console lines to disappear. | They do not, and its Fail-if then blames the wrong thing - "log categories are not derived from the namespace". Two independent reasons the rename is inert: the Development value is `Debug` and nothing in `Huddle.App` logs at `Debug`, and `appsettings.json` sets `"Agency.Huddle": "Information"` separately, which merges in and keeps the category alive. A control run proves the prefix model is healthy: `"Agency.Huddle": "Warning"` in the overlay makes the lines vanish at once. The step now makes all three edits - the stale rename in BOTH files plus `Default` to `Warning` - which reproduces a completely silent console against a working app, exactly the hazard the test is about. |
| `STARTUPCONFIG-26` step 7 said to open the Production URL and "confirm the app renders there". | It does not, and cannot. `dotnet run --no-launch-profile` runs the UNPUBLISHED build output in the Production environment, where Static Web Assets are off, so `Huddle.App.<hash>.styles.css`, `ReconnectModal.<hash>.razor.js` and `_framework/blazor.web.<hash>.js` all return **500** (`FileNotFoundException`, with ASP.NET Core's own warning naming the cause) and the page is unstyled and non-interactive. Confirmed on a SOLE instance, so it is not two-instance interference; and confirmed NOT a product defect - `dotnet publish` copies all three into `wwwroot` and a published Production run serves all six assets 200. The step now says what to expect and what to check instead. |
| `STARTUPCONFIG-26` required run (a)'s console to be "noticeably more verbose for `Agency.Huddle` categories" than run (b)'s, and [Common procedures](common.md#o-log) implied the same. | Development does set the level to `Debug` and Production leaves it at `Information`, but the consoles are identical in the free lane because `Huddle.App` and `Huddle.Contracts` contain **zero** `Debug`-level log statements - the only three in the solution are in `Huddle.Acp`, which is off in both runs. Across 28 captured Development-lane consoles in the 2026-09-14 run, not one `dbug:` line appeared. The step asked the tester to judge a level from output that cannot show it. |
| Four lines across three areas called the `offline` status dot **grey**. | It is RED, by design: `theme.css` defines `--status-offline: light-dark(#b32121, #e05a5a)` with a comment tying it to the design system's `charts.red`, and `.agent-dot.offline` uses it. A tester matching the stated colour would file a correctly-themed dot as broken. Corrected in `STARTUPCONFIG-18`, `APPTOOLS` (the Persona-tile step) and `INVITEROOMS-03`'s Fail-if, Inconclusive and note; the checkable oracle is the `agent-dot offline` class and the `offline` tooltip, not the shade. |
| `SHELLNAV-26` step 2 matched the reconnect script with `ReconnectModal\.razor\.[^"]*\.js`. | The fingerprint sits before `.razor`, not after it - the real name is `ReconnectModal.<hash>.razor.js` - so the regex returned nothing and step 3 had no path to test. A tester would read an empty result as a missing import-map entry, which is one of the test's own Fail-if lines. |
| `SHELLNAV-25` expected TAB B to pick up a new theme after clicking a sidebar link. | A sidebar click is an *enhanced* navigation, which replaces the render tree but not `<head>` - the same constraint [Section 0.6](../manual-tests.md#06-not-a-defect) names for why a theme change reloads the whole page. Only a full document load can add the `themes/…` link, so the step described something correct code cannot do. Now split into an enhanced navigation that must NOT change, and a full load that must. Step 5 also said "Dark", which is invisible on a dark OS; it now names whichever theme contrasts with the device. |
| Two tests instructed the tester to run `dotnet test Huddle.slnx --`. | Out of scope: this script tests through the browser, and `TEAM_E2E` makes running the suite a spending risk. |
| A Trace-logging step used `$env:Logging__LogLevel__Agency__Huddle` and a quoted variant. | Neither works. The form that parses is `${env:Logging__LogLevel__Agency.Huddle}`. |
| Steps wrote rejected Persona fixtures to `App_Data\personas\`. | The library is `{DataDir}/{Acp:TeamsDir}`, which is `App_Data\Teams\`. |
| `ANTHROPIC_API_KEY` was ordered unset without ever being set. | It reads as a dangling step and invites a reader to drop it. The variable is ambient — inherited from a shell profile — and the Adapter authenticates through the `claude` CLI login instead. [Section 0.1](../manual-tests.md#01-what-you-need) now carries that as a requirement. |
| "RESET" meant three different things across areas. | Delete all of `App_Data`, delete only `rooms` + `team.db`, or delete only `hooks.json` — same word, incompatible effects. A test that said "run RESET" was ambiguous outside its own file, and the wrong one leaves stale Rooms that read as a defect. Now four named resets in [Common procedures](common.md): `P-RESET-ALL`, `P-RESET-ROOMS`, `P-RESET-TEAMS`, `P-RESET-SETTINGS`. |
| Two areas counted Adapters with a bare `Get-Process node`. | A developer machine normally has a dozen unrelated `node` processes, so that count is not evidence. `O-ADAPTERS` filters on the `claude-agent-acp` command line and is the only valid count. |
| `PERSONALIFECYCLE-01` step 17 typed a plain, un-mentioned Message into the demo `echo` Room and asserted it gets a reply. | Neither demo agent implements the Reply Gate — `DemoAgentHost` and `tools/echo-bot.ps1` answer only a Mention — so that assertion could never pass against correct code. |
| `O-ADAPTERS` and the `E-FREE` state scoped the Model/Effort picker's probe Adapter to `E-PAID` only. | `docs/agencyteam/rules.md` documents the probe as running regardless of `Acp:Enabled`, so a tester who saw that same expected row in `E-FREE` would misfile it as a cost-guard failure. |
| [Known limits](../known-limits.md) and [Section 0.6](../manual-tests.md#06-not-a-defect) said nothing about the picker probe ignoring `Acp:Enabled`, only that the catalog is cached. | A tester who checks the tester-facing "not a defect" surface rather than `rules.md` had no documented cover for the transient `node` row that appears the moment a New/Edit card opens, and would file a working probe as the exact cost-guard breach `PERSONALIFECYCLE-01`'s Fail-if warns against. |
| `REPLYGATEBUDGET-01`'s Pass-if line quoted the demo reply body for `hello @echo` as `echo: hello`. | `DemoAgentHost` strips only the `@` character, never the mention word, so the real body is `echo: hello echo`. The line described text correct code cannot produce; a tester matching it byte-for-byte would file working code as broken. |

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
