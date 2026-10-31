# Manual test script — planning

Which tests to run, what each area covers, and which ones spend money. Counts and hours per area
come from each area's header; `pwsh agents/scripts/Check-ManualTests.ps1 -Summary` prints the totals.
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
| 1 | [Application shell, navigation and layout](shell-nav.md) | 28 | — | 2.7h |
| 2 | [Startup, configuration and first-run state](startup-config.md) | 27 | 4 | 3.1h |
| 3 | [Room view, posting Messages and Transcript rendering](room-messaging.md) | 29 | 1 | 3.3h |
| 4 | [Turn streaming, Drafts, Stop and failure surfacing](streaming-turn.md) | 30 | 10 | 4.5h |
| 5 | [Reply Gate, Mentions and Room Budget](reply-gate-budget.md) | 36 | 6 | 5.9h |
| 6 | [Agents page: tiles, Teams grouping, filter and rejected files](agents-library.md) | 42 | 1 | 4.4h |
| 7 | [Agent card: view, edit, create, delete](agent-profile-card.md) | 53 | 1 | 6.9h |
| 8 | [Model and Effort pickers, catalog probe and runner restart](model-effort.md) | 28 | 1 | 3.8h |
| 9 | [Creating Rooms, inviting Agents, Room naming](invite-rooms.md) | 38 | 4 | 4.2h |
| — | [Global Search: the box, the dropdown and the jump](global-search.md) | 10 | — | 2.3h |
| — | [References in a Message: the @ picker, Copy link and the Referenced items header](at-tagging.md) | 12 | 0 | 2.1h |
| 10 | [Settings: the Prompts, editing, per-field reset and Save](prompts-settings.md) | 41 | 3 | 4.7h |
| 11 | [Appearance tab and Themes](appearance-theme.md) | 25 | — | 2.9h |
| 12 | [The named pipe: external agents and the wire](pipe-external.md) | 33 | — | 3.7h |
| 13 | [App Tools a real model calls (progressive discovery)](app-tools.md) | 28 | 21 | 4.3h |
| 14 | [Persona lifecycle: supervisor, work dirs, health and restarts](persona-lifecycle.md) | 32 | 7 | 6.6h |
| 15 | [Adapters: choosing one per Persona, and running two at once](adapters.md) | 4 | 1 | 1.5h |
| 16 | [Skills: the Chief of Staff, team-building, and the Family Health Advisor](skills.md) | 6 | — | 1.8h |
| 17 | [Work Mode: how much an Agent may do before it asks](work-mode.md) | 9 | 4 | 1.8h |
| 18 | [Questions: an Agent asks the Human by tapping, not typing](questions.md) | 10 | 9 | 3.1h |
| 19 | [Adapter commands](adapter-commands.md) | 7 | 6 | 1.8h |
| 20 | [Avatar library: choosing a face from the library](avatar-library.md) | 28 | — | 3.3h |
| 21 | [File Changes, Watched Folders and Memory](file-changes.md) | 12 | 10 | 3.2h |
| 22 | [Library explorer and editor](library.md) | 22 | 1 | 3.6h |
| 23 | [Prompt blocks](prompt-blocks.md) | 6 | 5 | 1.4h |
| 24 | [Room Sessions: per-Room context, resume and Stop](room-sessions.md) | 12 | — | 5.8h |
| 25 | [Tasks: creating, moving, viewing and referencing a unit of work](tasks.md) | 19 | 5 | 3.6h |
| 26 | [Teams: Sidebar, Members, Files, Tasks and Shared Memory](team-pages.md) | 17 | — | 0.6h |
| 27 | [Turn detail and Spend](turn-detail.md) | 5 | 4 | 1.8h |
| 28 | [Needs You: the sidebar card of Rooms waiting on the Human](needs-you.md) | 4 | — | 0.4h |
| 29 | [Right sidebar: the panels by Subject, Outline, collapse and focus](right-sidebar.md) | 8 | — | 0.7h |
| 29 | [Team chats: the Team Room, Team chats in the sidebar, the Members-tab Message, archiving and Task wakes and digests](team-chats.md) | 11 | — | 1.1h |
| 30 | [Chat uploads: the paperclip, paste and drop, the chips, the Message's path lines, the Uploads root and Room delete](uploads.md) | 8 | 0 | 1.0h |
| 31 | [Skill Packs: SKILLS.md, the AI tool, updates, the move and shared folders](skill-packs.md) | 7 | 1 | 2.5h |
| 32 | [Document history: the byline, the History, the History panel and the Stop hook](doc-history.md) | 8 | 4 | 1.6h |
| 33 | [Human gate: the Proof list, the decision card, Accept and Decline, the board drag and the Needs You page](human-gate.md) | 9 | — | 1.3h |
| 34 | [Helpers: the Helpers tab, Save and Reset, the Needs Human judgement, the override, the daily cap and a failing Adapter](helpers.md) | 8 | 5 | 1.4h |
| 35 | [Feed: the Notifications bell and panel, the Feed Tab, cards, filters, the new-items pill and the restart](feed.md) | 17 | 4 | 3.1h |
| 36 | [Remote access: the connect page, pairing, Tailscale Serve and Funnel, the LAN bind, revocation and the Open action](remote-access.md) | 15 | — | 3.0h |
| 37 | [Instructions and Memory: the Instructions files, the memory tool, Team Memory and the Memory protocol](instructions-memory.md) | 9 | 8 | 2.6h |
| 38 | [Team Memory Helper: proposals filed by a Helper, the History, the Feed record, the membership check and the daily cap](team-memory-helper.md) | 7 | 6 | 1.5h |

💰 marks a test that spends real money. Estimates assume you already know the app; first time
through, roughly double them.

### 1.1 Smoke pass

One free test from every area in the table above, so each area is touched at least once. Run these
after any change, and before committing to a full area. A failure here means stop and fix, not
continue. For the totals, run `pwsh agents/scripts/Check-ManualTests.ps1 -Summary`.

| Test id | What it proves |
| --- | --- |
| [SHELLNAV-01](shell-nav.md#shellnav-01--every-stylesheet-the-shell-links-is-fingerprinted-except-the-one-that-is-deliberately-static-and-actually-serves) | Every stylesheet the shell links is fingerprinted and actually serves |
| [SHELLNAV-02](shell-nav.md#shellnav-02--every-shell-script-is-fingerprinted-except-mudblazors-own-and-serves---the-gap-the-automated-guard-does-not-cover) | Both shell scripts are fingerprinted and serve - the gap the automated guard does not cover |
| [SHELLNAV-03](shell-nav.md#shellnav-03--the-scoped-css-bundle-is-applied-the-error-banner-stays-hidden-below-the-fold) | The scoped-CSS bundle is applied: the error banner stays hidden below the fold |
| [STARTUPCONFIG-01](startup-config.md#startupconfig-01--the-documented-launch-command-serves-the-styled-app-shell-on-httplocalhost5100) | The documented launch command serves the styled app shell on http://localhost:5100 |
| [ROOMMESSAGING-02](room-messaging.md#roommessaging-02--enter-sends-the-message-clears-the-textarea-and-appends-exactly-one-jsonl-line) | Enter sends the Message, clears the textarea, and appends exactly one JSONL line |
| [ROOMMESSAGING-03](room-messaging.md#roommessaging-03--shiftenter-inserts-a-newline-and-does-not-send) | Shift+Enter inserts a newline and does not send |
| [STREAMINGTURN-02](streaming-turn.md#streamingturn-02--a-draft-appears-as-a-distinct-live-row-while-a-turn-is-being-written) | A Draft appears as a distinct live row while a Turn is being written |
| [REPLYGATEBUDGET-02](reply-gate-budget.md#replygatebudget-02--three-or-more-members-makes-the-same-room-mention-gated) | Three or more Members makes the same Room Mention-gated |
| [AGENTSLIBRARY-01](agents-library.md#agentslibrary-01--the-page-loads-is-styled-and-spawns-nothing) | The page loads, is styled, and spawns nothing |
| [AGENTPROFILECARD-02](agent-profile-card.md#agentprofilecard-02--clicking-new-agent-opens-a-card-at-all-blank-name-crash-probe) | Clicking New agent opens a card at all (blank-name crash probe) |
| [INVITEROOMS-03](invite-rooms.md#inviterooms-03--the-new-chat-panel-toggles-lists-every-agent-with-a-status-dot-and-keeps-start-chat-disabled-until-something-is-ticked) | The New chat panel toggles, lists every Agent with a status dot, and keeps Start chat disabled until something is ticked |
| [PROMPTSSETTINGS-01](prompts-settings.md#promptssettings-01--settings-opens-on-the-profile-tab-with-a-seven-tab-rail) | /settings opens on the Profile tab |
| [PROMPTSSETTINGS-04](prompts-settings.md#promptssettings-04--every-prompt-is-reachable-in-six-named-groups-in-a-fixed-order) | Every prompt is reachable, in six named groups in a fixed order |
| [APPEARANCETHEME-03](appearance-theme.md#appearancetheme-03--the-theme-picker-offers-the-whole-built-in-catalog-grouped-and-is-the-tabs-only-control) | The Theme lists offer all 12 built-in Themes (six light, six dark, no group headings), and no light/dark/system control exists |
| [APPEARANCETHEME-04](appearance-theme.md#appearancetheme-04--choosing-a-theme-stores-its-id-and-applies-immediately-with-no-page-reload) | Choosing a Theme stores its id as `theme` in appearance.json and applies it in place, with no page load |
| [PIPEEXTERNAL-04](pipe-external.md#pipeexternal-04--a-room-appears-in-the-sidebar-the-moment-an-external-agent-says-hello-with-no-refresh) | A Room appears in the sidebar the moment an external agent says hello, with no refresh |
| [PERSONALIFECYCLE-01](persona-lifecycle.md#personalifecycle-01--with-acp-disabled-every-agent-reads-offline-with-an-empty-tooltip-and-no-room-raises-an-alert) | With ACP disabled every agent reads Offline with an empty tooltip and no Room raises an alert |
| [MODELEFFORT-01](model-effort.md#modeleffort-01--first-card-open-the-model-list-comes-from-the-adapter-and-one-adapter-spawn-fills-both-lists) | The Model list comes from the Adapter, and one spawn fills both lists |
| [APPTOOLS-01](app-tools.md#apptools-01--the-prompt-groups-are-readable-in-the-browser-and-no-default-contains-the-literal-mcp__team__) | The prompt groups are readable, and no default names the literal `mcp__team__` tool |
| [ADAPTERS-02](adapters.md#adapters-02--a-mock-adapters-reply-lands-whole-in-the-room-and-the-transcript-and-the-other-agent-is-unaffected) | A mock Adapter's reply lands whole in the Room, and the other Agent is unaffected |
| [SKILLS-01](skills.md#skills-01--the-chief-of-staff-reads-team-building-unprompted-when-asked-for-a-team) | The Chief of Staff calls `read_skill` on `team-building` unprompted when asked for a team |
| [SKILLPACKS-02](skill-packs.md#skillpacks-02--choosing-a-pack-copies-its-skills-then-changes-the-agent) | Choosing a Pack copies its Skills into `Skills/`, then changes the Agent |
| [WORKMODE-01](work-mode.md#workmode-01--the-picker-lists-what-the-adapter-advertises-minus-plan-auto-and-bypass-permissions) | The Work Mode picker lists what the Adapter advertises, minus Plan, Auto and Bypass |
| [QUESTIONS-07](questions.md#questions-07--a-scripted-adapters-forms-reach-the-human-as-cards-with-the-right-types) | A scripted Adapter's forms reach the Human as cards, with the right types |
| [COMMANDS-03](adapter-commands.md#commands-03--a-command-the-profile-does-not-allow-is-an-ordinary-message) | A command the profile does not allow is an ordinary Message |
| [AVATARLIB-01](avatar-library.md#avatarlib-01--a-library-switched-off-no-button-anywhere) | With `Dir` naming a folder that is not a library, there is no avatar-library button anywhere |
| [FM-0](file-changes.md#fm-0--an-agents-own-memory-edit-in-one-room-is-news-in-its-other-rooms) | An Agent's own memory edit in one Room is news in its other Rooms |
| [L0](library.md#l0--chat-message-with-absolute-path-becomes-a-library-link-and-opens-the-library-tab-on-that-file) | A chat Message with an absolute path becomes a Library link and opens the Library tab on that file |
| [PROMPTBLOCKS-01](prompt-blocks.md#promptblocks-01--a-named-image-is-seen-without-a-tool-call) | A named image is seen, without a tool call |
| [RS-M1](room-sessions.md#rs-m1--the-two-trip-stress-test) | Per-Room session context across the two-trip stress test |
| [TASKS-01](tasks.md#tasks-01--creating-a-task-in-the-ui-writes-the-file-at-the-right-path-with-the-canonical-frontmatter) | Creating a Task in the UI writes its file at the right path, with canonical frontmatter |
| [TP-T0](team-pages.md#tp-t0--click-business-team-and-open-members-tab) | Clicking Business team opens its Members tab |
| [TURNDETAIL-01](turn-detail.md#turndetail-01--an-edit-shows-a-live-expandable-preview-in-a-room) | An edit shows a live, expandable preview in a Room |

> [!TIP]
> Run `SHELLNAV-01` first, always. It catches the one defect in this application that produces no
> error anywhere: `@Assets["..."]` returns an unresolved key verbatim instead of throwing, so a
> stale stylesheet name renders as an ordinary-looking `href` that 404s silently. That exact
> failure shipped live for a month.

---

## Appendix A. Paid test register

The tests that spend money, with an estimate for each. This register is not yet complete:
`Check-ManualTests.ps1 -Summary` gives the paid count per area, and the register lists fewer. Read
[section 0.2](../manual-tests.md#02-the-cost-guard) and [section 0.3](../manual-tests.md#03-the-model-and-effort-convention) before running any of them.

| Test id | Area | What it proves | Est. |
| --- | --- | --- | --- |
| [STARTUPCONFIG-33](startup-config.md#startupconfig-33--a-spent-per-persona-token-budget-reads-as-degraded-on-the-agent-tile-and-in-the-room-banner) | `startup-config` | A spent per-Persona token Budget reads as Degraded on the Agent tile and in the Room banner | 12 min |
| [STARTUPCONFIG-34](startup-config.md#startupconfig-34--a-human-message-clears-a-token-budget-degraded-state-and-lets-the-persona-work-again) | `startup-config` | A Human Message clears a token-Budget Degraded state and lets the Persona work again | 6 min |
| [ROOMMESSAGING-32](room-messaging.md#roommessaging-32--a-real-claude-agents-reply-streams-and-renders-through-the-same-path-costs-money) | `room-messaging` | A real Claude Agent's reply streams and renders through the same path (COSTS MONEY) | 15 min |
| [STREAMINGTURN-23](streaming-turn.md#streamingturn-23--a-stored-model-the-adapter-does-not-advertise-degrades-the-persona-but-does-not-stop-it) | `streaming-turn` | A stored Model the adapter does not advertise degrades the Persona but does not stop it | 12 min |
| [STREAMINGTURN-24](streaming-turn.md#streamingturn-24--stop-actually-ends-a-real-turn-the-draft-goes-no-message-is-posted-nothing-is-written) | `streaming-turn` | Stop actually ends a real Turn: the Draft goes, no Message is posted, nothing is written | 10 min |
| [STREAMINGTURN-25](streaming-turn.md#streamingturn-25--a-stopped-turn-is-not-a-failure-no-alert-strip-no-degraded-badge-no-broken-streak) | `streaming-turn` | A stopped Turn is not a failure: no alert strip, no Degraded badge, no broken streak | 12 min |
| [STREAMINGTURN-26](streaming-turn.md#streamingturn-26--stop-ends-this-rooms-live-turn-and-discards-everything-queued-behind-it-in-that-room) | `streaming-turn` | Stop ends this Room's live Turn and discards everything queued behind it in that Room | 10 min |
| [STREAMINGTURN-27](streaming-turn.md#streamingturn-27--stopping-an-agent-in-one-room-does-not-touch-its-turn-in-another-room) | `streaming-turn` | Stopping an Agent in one Room does not touch its Turn in another Room | 12 min |
| [STREAMINGTURN-28](streaming-turn.md#streamingturn-28--a-spent-per-persona-token-budget-reads-as-degraded-and-a-human-message-clears-it) | `streaming-turn` | A spent per-Persona token Budget reads as Degraded, and a Human Message clears it | 10 min |
| [STREAMINGTURN-29](streaming-turn.md#streamingturn-29--a-turn-that-fails-mid-flight-reports-degraded-in-the-adapters-own-words-and-escalates-on-the-third-failure-in-a-row) | `streaming-turn` | A Turn that fails mid-flight reports Degraded in the Adapter's own words, and escalates on the third failure in a row | 15 min |
| [STREAMINGTURN-30](streaming-turn.md#streamingturn-30--killing-the-adapter-process-mid-turn-clears-the-draft-and-says-so-instead-of-deafening-the-agent-forever) | `streaming-turn` | Killing the adapter process mid-Turn clears the Draft and says so, instead of deafening the Agent forever | 15 min |
| [REPLYGATEBUDGET-31](reply-gate-budget.md#replygatebudget-31--paid-a-real-persona-answers-a-two-member-room-with-no-mention-at-all) | `reply-gate-budget` | PAID: a real Persona answers a two-Member Room with no Mention at all | 15 min |
| [REPLYGATEBUDGET-32](reply-gate-budget.md#replygatebudget-32--paid-an-un-mentioned-message-is-not-answered-but-rides-along-as-context-on-the-next-mention) | `reply-gate-budget` | PAID: an un-mentioned Message is not answered but rides along as context on the next Mention | 15 min |
| [REPLYGATEBUDGET-33](reply-gate-budget.md#replygatebudget-33--paid-a-two-agent-exchange-halts-at-the-budget-and-the-last-agent-declines-before-taking-a-turn) | `reply-gate-budget` | PAID: a two-Agent exchange halts at the Budget, and the last Agent declines BEFORE taking a Turn | 25 min |
| [REPLYGATEBUDGET-34](reply-gate-budget.md#replygatebudget-34--paid-a-message-declined-for-budget-is-held-for-re-delivery-not-kept-as-catch-up) | `reply-gate-budget` | PAID: a Message declined for Budget is held for re-delivery, not kept as Catch-up | 25 min |
| [REPLYGATEBUDGET-35](reply-gate-budget.md#replygatebudget-35--paid-the-agent-facing-post-tool-returns-the-same-terminal-refusal-and-the-model-obeys-it) | `reply-gate-budget` | PAID: the Agent-facing post tool returns the same terminal refusal, and the model obeys it | 20 min |
| [REPLYGATEBUDGET-36](reply-gate-budget.md#replygatebudget-36--paid-an-agent-can-mint-a-fresh-budget-by-creating-a-room-and-only-the-token-budget-catches-it) | `reply-gate-budget` | PAID: an Agent can mint a fresh Budget by creating a Room, and only the token Budget catches it | 25 min |
| [AGENTSLIBRARY-40](agents-library.md#agentslibrary-40--degraded-with-the-reason--and-the-two-things-that-must-not-badge) | `agents-library` | Degraded, with the persistence reason — and the two things that must NOT badge | 20 min |
| [AGENTPROFILECARD-50](agent-profile-card.md#agentprofilecard-50--editing-an-agent-really-does-lose-its-conversation-memory-costs-money) | `agent-profile-card` | Editing an agent really does lose its conversation memory (COSTS MONEY) | 12 min |
| [MODELEFFORT-27](model-effort.md#modeleffort-27--money-the-chosen-model-and-effort-actually-reach-the-model---ask-it) | `model-effort` | MONEY: the chosen Model and Effort actually reach the model - ask it | 15 min |
| [INVITEROOMS-28](invite-rooms.md#inviterooms-28--an-agent-creates-a-room-with-mcp__team__create_room-and-it-appears-live-in-the-sidebar) | `invite-rooms` | An Agent creates a Room with mcp__team__create_room and it appears live in the sidebar | 15 min |
| [INVITEROOMS-29](invite-rooms.md#inviterooms-29--an-agent-invites-another-with-mcp__team__invite_agent-using-the-room-id-from-its-own-room--label) | `invite-rooms` | An Agent invites another with mcp__team__invite_agent, using the room id from its own [Room: …] label | 15 min |
| [INVITEROOMS-30](invite-rooms.md#inviterooms-30--an-agent-can-invite-into-a-room-it-is-not-a-member-of) | `invite-rooms` | An Agent can invite into a Room it is not a Member of | 10 min |
| [INVITEROOMS-31](invite-rooms.md#inviterooms-31--every-agent-created-room-contains-the-human-so-none-is-hidden) | `invite-rooms` | Every Agent-created Room contains the Human, so none is hidden | 6 min |
| [PROMPTSSETTINGS-38](prompts-settings.md#promptssettings-38--costs-money-a-live-prompt-edit-reaches-the-very-next-turn-with-no-restart) | `prompts-settings` | COSTS MONEY: a Live prompt edit reaches the very next Turn with no restart | 15 min |
| [PROMPTSSETTINGS-39](prompts-settings.md#promptssettings-39--costs-money-a-next-session-prompt-edit-is-silently-inert-on-a-running-agent-until-it-restarts) | `prompts-settings` | COSTS MONEY: a "Next session" prompt edit is silently inert on a running agent until it restarts | 20 min |
| [PROMPTSSETTINGS-40](prompts-settings.md#promptssettings-40--costs-money-get_help-re-renders-on-every-call-so-its-get-help-prompts-land-on-the-next-call--while-a-tool-description-does-not) | `prompts-settings` | COSTS MONEY: get_help re-renders on every call, so its nine prompts land on the next call — while a tool DESCRIPTION does not | 20 min |
| [APPTOOLS-05](app-tools.md#apptools-05--an-agent-reads-its-own-rooms-id-off-the-room--label) | `app-tools` | An Agent reads its own Room's id off the [Room: ...] label | 5 min |
| [APPTOOLS-06](app-tools.md#apptools-06--get_helps-budget-and-messages-sections-prove-progressive-discovery-is-live) | `app-tools` | get_help's BUDGET and MESSAGES sections prove progressive discovery is live | 8 min |
| [APPTOOLS-07](app-tools.md#apptools-07--asked-what-tools-it-has-an-agent-calls-get_help-and-reports-the-real-catalog-with-descriptions) | `app-tools` | Asked what tools it has, an agent calls get_help and reports the real catalog with descriptions | 10 min |
| [APPTOOLS-08](app-tools.md#apptools-08--tool-activity-is-visible-under-the-draft-while-a-call-is-in-flight) | `app-tools` | Tool activity is visible under the Draft while a call is in flight | 5 min |
| [APPTOOLS-10](app-tools.md#apptools-10--list_agents-reports-real-names-real-online-state-and-real-job-descriptions) | `app-tools` | list_agents reports real names, real online state, and real job descriptions | 12 min |
| [APPTOOLS-11](app-tools.md#apptools-11--an-alias-is-advertised-by-list_agents-and-then-accepted-by-invite_agent) | `app-tools` | An Alias is advertised by list_agents and then accepted by invite_agent | 15 min |
| [APPTOOLS-12](app-tools.md#apptools-12--invite_agent-called-with-the-id-from-the-agents-own-label-renames-the-room-live) | `app-tools` | invite_agent, called with the id from the agent's own label, renames the room live | 12 min |
| [APPTOOLS-13](app-tools.md#apptools-13--invite_agent-is-idempotent-and-says-so-rather-than-duplicating-a-member) | `app-tools` | invite_agent is idempotent and says so rather than duplicating a member | 6 min |
| [APPTOOLS-14](app-tools.md#apptools-14--invite_agent-with-an-unknown-name-comes-back-with-the-names-that-do-exist) | `app-tools` | invite_agent with an unknown name comes back with the names that do exist | 6 min |
| [APPTOOLS-15](app-tools.md#apptools-15--an-agents-reply-is-delivered-once--it-must-not-also-post-the-same-text-with-post_message) | `app-tools` | An agent's reply is delivered once — it must not also post the same text with post_message | 6 min |
| [APPTOOLS-16](app-tools.md#apptools-16--create_room-makes-a-new-room-that-appears-in-the-sidebar-live-named-after-its-agents) | `app-tools` | create_room makes a new Room that appears in the sidebar live, named after its Agents | 10 min |
| [APPTOOLS-17](app-tools.md#apptools-17--post_message-delivers-into-a-room-other-than-the-one-the-agent-was-addressed-in-and-the-agent-does-not-answer-its-own-post) | `app-tools` | post_message delivers into a Room other than the one the agent was addressed in, and the agent does not answer its own post | 12 min |
| [APPTOOLS-18](app-tools.md#apptools-18--tools-are-the-source-of-truth--the-agent-must-not-answer-about-agents-or-rooms-from-the-codebase) | `app-tools` | Tools are the source of truth — the agent must not answer about agents or Rooms from the codebase | 10 min |
| [APPTOOLS-19](app-tools.md#apptools-19--a-budgetexhausted-refusal-from-post_message-is-terminal-the-agent-stops-does-not-retry-and-does-not-reroute) | `app-tools` | A budgetExhausted refusal from post_message is terminal: the agent stops, does not retry and does not reroute | 15 min |
| [APPTOOLS-21](app-tools.md#apptools-21--continue-re-delivers-the-paused-message-and-the-prompt-survives-a-page-reload) | `app-tools` | Continue re-delivers the paused Message, and the prompt survives a page reload | 12 min |
| [APPTOOLS-22](app-tools.md#apptools-22--editing-a-prompt-changes-model-facing-text-without-restarting-the-session-and-next-session-prompts-wait-for-a-restart) | `app-tools` | Editing a prompt changes model-facing text without restarting the session, and Next session prompts wait for a restart | 15 min |
| [APPTOOLS-23](app-tools.md#apptools-23--follow_room-wakes-an-agent-that-was-not-mentioned-and-a-non-follower-in-the-same-room-is-the-control) | `app-tools` | follow_room wakes an agent that was not mentioned, and a non-follower in the same Room is the control | 15 min |
| [APPTOOLS-24](app-tools.md#apptools-24--unfollow_room-restores-the-ordinary-mention-only-rule-and-the-room-confirms-the-decline-was-not-a-silent-drop) | `app-tools` | unfollow_room restores the ordinary mention-only rule, and the Room confirms the decline was not a silent drop | 10 min |
| [APPTOOLS-25](app-tools.md#apptools-25--following-does-not-buy-a-turn-past-the-budget) | `app-tools` | Following does not buy a Turn past the Budget, even when two followers would otherwise keep each other going | 15 min |
| [APPTOOLS-26](app-tools.md#apptools-26--create_room-with-seed-posts-the-opening-message-as-part-of-creation) | `app-tools` | create_room with seed posts the opening Message as part of creation | 10 min |
| [PERSONALIFECYCLE-26](persona-lifecycle.md#personalifecycle-26--money-saving-the-edit-card-with-nothing-changed-does-not-restart-the-agent) | `persona-lifecycle` | MONEY: saving the Edit card with nothing changed does NOT restart the Agent | 12 min |
| [PERSONALIFECYCLE-27](persona-lifecycle.md#personalifecycle-27--money-editing-the-persona-body-restarts-the-session-and-the-new-instruction-takes-effect) | `persona-lifecycle` | MONEY: editing the Persona body restarts the session and the new instruction takes effect | 12 min |
| [PERSONALIFECYCLE-28](persona-lifecycle.md#personalifecycle-28--money-changing-the-model-from-haiku-to-sonnet-restarts-the-session-and-clears-what-the-agent-remembers) | `persona-lifecycle` | MONEY: changing the Model from Haiku to Sonnet restarts the session and clears what the Agent remembers | 18 min |
| [PERSONALIFECYCLE-29](persona-lifecycle.md#personalifecycle-29--money-changing-the-effort-from-low-to-medium-restarts-the-session-and-a-model-change-clears-the-effort-selection) | `persona-lifecycle` | MONEY: changing the Effort from low to medium restarts the session, and a Model change clears the Effort selection | 15 min |
| [PERSONALIFECYCLE-30](persona-lifecycle.md#personalifecycle-30--money-what-an-agent-was-told-in-one-room-does-not-leak-into-another-one-process-serves-both-and-replies-land-in-the-right-room) | `persona-lifecycle` | MONEY: what an Agent was told in one Room does not leak into another; replies still land in the right Room | 12 min |
| [PERSONALIFECYCLE-31](persona-lifecycle.md#personalifecycle-31--money-the-per-persona-token-budget-shows-as-degraded-with-its-reason-and-any-human-message-clears-it) | `persona-lifecycle` | MONEY: the per-Persona token Budget shows as Degraded with its reason, and any Human Message clears it | 25 min |
| [PERSONALIFECYCLE-32](persona-lifecycle.md#personalifecycle-32--money-a-token-budget-of-zero-disables-the-per-persona-cap-and-the-per-room-budget-still-stops-the-exchange) | `persona-lifecycle` | MONEY: a token Budget of zero disables the per-Persona cap, and the per-Room Budget still stops the exchange | 20 min |
| [ADAPTERS-03](adapters.md#adapters-03--changing-an-agents-adapter-resets-model-and-effort-says-so-and-restarts-the-session) | `adapters` | MONEY: changing an Agent's Adapter resets Model and Effort with a status note, and the restart loses the conversation memory | 15 min |
| [ADAPTERS-04](adapters.md#adapters-04--does-a-real-local-model-call-get_help-unprompted-) | `adapters` | Whether a real local Model calls `get_help` unprompted — the question no automated test can settle | 45 min |
| [SKILLS-01](skills.md#skills-01--the-chief-of-staff-reads-team-building-unprompted-when-asked-for-a-team) | `skills` | The Chief of Staff calls `read_skill` on `team-building` unprompted, then interviews per its step 1 | 10 min |
| [SKILLS-02](skills.md#skills-02--approve-creates-the-agents-wakes-the-chief-of-staff-and-it-opens-a-seeded-room) | `skills` | Approve creates the Candidates, wakes the Chief of Staff through the ordinary Reply Gate, and a pipeline opens a seeded Room | 20 min |
| [SKILLS-03](skills.md#skills-03--decline-and-the-chief-of-staff-revises-and-proposes-again) | `skills` | Decline posts its documented Message and creates nothing; the Chief of Staff's revised Proposal replaces its own waiting one | 15 min |
| [SKILLS-04](skills.md#skills-04--over-the-limit-approve-creates-nothing-and-the-message-says-why) | `skills` | The Agent limit is re-checked at Approve time, not only when proposed, and Approve creates nothing over it | 15 min |
| [SKILLS-05](skills.md#skills-05--the-greeting-on-a--clean-install-and-no-second-greeting-on-restart) | `skills` | On a fresh install the Chief of Staff's Room holds an unprompted Greeting built from `onboarding.md`, and a restart never repeats it | 15 min |
| [SKILLS-06](skills.md#skills-06--the-family-health-advisors-safety-behaviour) | `skills` | The Family Health Advisor's starter Body leads with emergency advice, refuses dose changes, and re-reads its notes after a restart | 30 min |
| [SKILLPACKS-01](skill-packs.md#skillpacks-01--a-real-ai-tool-adds-a-pack-and-huddle-shows-it-without-a-reload) | `skill-packs` | A real AI tool follows `InstallSkillPack.md`, validates, writes `SKILLS.md` rows, and the Pack appears without a reload; the spend is the AI tool's | 25 min |
| [WORKMODE-06](work-mode.md#workmode-06--accept-edits-no-permission-request-for-an-edit) | `work-mode` | Accept edits: no permission request for an edit | 15 min |
| [WORKMODE-07](work-mode.md#workmode-07--plan-nothing-is-written-and-the-request-to-leave-plan-mode-is-refused) | `work-mode` | Plan: nothing is written, and the request to leave plan mode is refused | 15 min |
| [WORKMODE-08](work-mode.md#workmode-08--accept-edits-a-write-into-claude-is-still-refused) | `work-mode` | Accept edits: a write into `~/.claude` is still refused | 15 min |
| [WORKMODE-09](work-mode.md#workmode-09--a-mode-is-applied-again-after-an-app-restart) | `work-mode` | A mode is applied again after an app restart | 15 min |
| [QUESTIONS-01](questions.md#questions-01--a-model-frames-its-ask-and-calls-ask_human-with-one-to-three-questions) | `questions` | A model frames its ask and calls `ask_human` with one to three Questions | 15 min |
| [INSTRMEM-01](instructions-memory.md#instrmem-01--a-team-instructions-file-reaches-every-member-on-its-next-session) | `instructions-memory` | A Team Instructions file reaches every member on its next session | 15 min |
| [INSTRMEM-02](instructions-memory.md#instrmem-02--a-new-topic-file-is-written-with-a-description-and-appears-in-the-next-session-index) | `instructions-memory` | A new topic file is written with a description and appears in the next session index | 15 min |
| [INSTRMEM-03](instructions-memory.md#instrmem-03--two-rooms-write-one-memory-file-and-the-second-write-is-refused-with-the-current-text) | `instructions-memory` | Two Rooms write one memory file and the second write is refused with the current text | 20 min |
| [INSTRMEM-04](instructions-memory.md#instrmem-04--a-member-write-to-team-memory-is-refused-through-the-tool-and-through-write-while-its-read-is-allowed) | `instructions-memory` | A member write to Team Memory is refused through the tool and through Write while its read is allowed | 20 min |
| [INSTRMEM-05](instructions-memory.md#instrmem-05--a-human-edit-in-the-library-is-seen-at-the-next-read-and-an-old-version-is-refused) | `instructions-memory` | A Human edit in the Library is seen at the next read and an old version is refused | 15 min |
| [INSTRMEM-06](instructions-memory.md#instrmem-06--an-agent-deletes-a-memory-file-only-when-the-human-asks) | `instructions-memory` | An Agent deletes a memory file only when the Human asks | 15 min |
| [INSTRMEM-07](instructions-memory.md#instrmem-07--spike-s-1-whether-claude-code-loads-the-repository-claude-files-into-a-persona) | `instructions-memory` | Spike S-1 whether Claude Code loads the repository CLAUDE files into a Persona | 20 min |
| [INSTRMEM-08](instructions-memory.md#instrmem-08--a-restart-recalls-a-memory-from-the-index-and-the-files) | `instructions-memory` | A restart recalls a memory from the index and the files | 20 min |
| [TMHELP-02](team-memory-helper.md#tmhelp-02--a-member-proposal-is-filed-with-its-proposer-in-the-history) | `team-memory-helper` | A member proposal is filed with its proposer in the History | 20 min |
| [TMHELP-03](team-memory-helper.md#tmhelp-03--a-later-proposal-updates-the-same-topic-file) | `team-memory-helper` | A later proposal updates the same topic file | 15 min |
| [TMHELP-04](team-memory-helper.md#tmhelp-04--a-personal-preference-is-not-filed-and-the-feed-says-why) | `team-memory-helper` | A personal preference is not filed and the Feed says why | 10 min |
| [TMHELP-05](team-memory-helper.md#tmhelp-05--a-proposal-to-a-team-the-member-is-not-in-is-refused) | `team-memory-helper` | A proposal to a Team the member is not in is refused | 10 min |
| [TMHELP-06](team-memory-helper.md#tmhelp-06--instructions-inside-a-proposal-do-not-move-the-write-outside-the-team) | `team-memory-helper` | Instructions inside a proposal do not move the write outside the Team | 15 min |
| [TMHELP-07](team-memory-helper.md#tmhelp-07--the-daily-cap-pauses-the-helper-and-the-tool-says-so) | `team-memory-helper` | The daily cap pauses the Helper and the tool says so | 15 min |
| [QUESTIONS-02](questions.md#questions-02--the-asker-ends-its-turn-without-guessing-and-the-framing-lands-before-the-card-can-be-tapped) | `questions` | The asker ends its Turn without guessing, and the framing lands before the card can be tapped | 15 min |
| [QUESTIONS-03](questions.md#questions-03--a-factual-question-and-a-request-for-an-opinion-do-not-call-ask_human) | `questions` | A factual question and a request for an opinion do not call `ask_human` | 15 min |
| [QUESTIONS-04](questions.md#questions-04--the-same-on-an-agent-running-on-agency-acp) | `questions` | The same on an Agent running on `agency-acp` | 20 min |
| [QUESTIONS-05](questions.md#questions-05--in-a-room-of-three-an-answer-wakes-only-the-asker) | `questions` | In a Room of three, an answer wakes only the asker | 20 min |
| [QUESTIONS-06](questions.md#questions-06--a-claude-agent-has-no-built-in-askuserquestion) | `questions` | A Claude Agent has no built-in `AskUserQuestion` (with `Acp:AdvertiseElicitation` off) | 20 min |
| [QUESTIONS-08](questions.md#questions-08--claudes-askuserquestion-reaches-the-human-and-the-turn-survives-the-wait) | `questions` | Claude's `AskUserQuestion` reaches the Human, and the Turn survives the wait | 20 min |
| [QUESTIONS-09](questions.md#questions-09--dismiss-and-stop-end-a-waiting-form-cleanly) | `questions` | Skip and Stop end a waiting form cleanly | 20 min |
| [QUESTIONS-10](questions.md#questions-10--an-unanswered-form-is-dropped-after-acpuserinputtimeoutseconds) | `questions` | An unanswered form is dropped after `Acp:UserInputTimeoutSeconds` | 15 min |

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
| `STREAMINGTURN-15` drove both Agents with `drip.ps1`, whose Stop button then never re-enables. | `drip.ps1` reads only the welcome line and never drains the server's envelopes - setup note 6 says so. `stopTurn` is a WRITE to that client, so it can block, `StopDraftAsync` never returns, its `finally` never clears the busy flag, and the button stays disabled - indistinguishable from this test's own "stays disabled indefinitely" Fail-if. Verified both ways on 2026-09-14: with `drip.ps1` it never re-enabled; with the LISTENING client from STREAMINGTURN-17 it disabled and re-enabled cleanly on each click while the other Agent's button stayed enabled throughout. The step now names the listening client and says why. |
| `ROOMMESSAGING-30` tried to reach a per-Room Budget of 2 by sending two Human Messages in a row, and to provoke a refusal with a third. | Neither works, for the same reason `STARTUPCONFIG-33` did not: a Human Message RESETS the counter, so alternating Human/agent Messages can never exceed 1 - each Message you type zeroes it and draws exactly one reply. Verified on 2026-09-14: two `hi @echo` in a two-member Room both left the note at `1 of 2`. Two replies must arrive BETWEEN two Human Messages, which needs two Agents in the Room; inviting `alpha` and sending one `hi @echo and @alpha` produced the pause panel immediately. The refusal needs its own run at a budget of 1, where the second Agent's reply is refused - that produced the exact log line. Both steps now say so, and the note cross-references the same rule in `STARTUPCONFIG-33`. |
| `ROOMMESSAGING-12` expected the JSONL to hold a raw `"`, `&` and `—`, escaping only the emoji and CJK. | Everything is escaped: `"`, `&`, `—`, `😀`, `日本語`. That is `System.Text.Json`'s default `JavaScriptEncoder`, which escapes `"`, `&`, `<`, `>`, `'` and `+` as well as all non-ASCII. The mismatch is in the SAFE direction - more escaping, and the round trip is lossless - but as written three Pass-if lines fail against correct code. |
| `STARTUPCONFIG-33` tried to spend a per-Persona token Budget by sending two Human Messages in a row. | It cannot work, and the money is spent finding that out. Every Human Message zeroes the counter - `PersonaRunner` does `Interlocked.Exchange(ref this.tokensConsumed, 0)` on seeing one - and the cap is checked at the start of a work item, so a Turn you prompted yourself is always measured against zero. The cap bounds UNATTENDED spend by design. Worse, the Inconclusive clause told a tester who saw a reply to send ANOTHER Message, which resets it again; four paid Turns in a row were taken on 2026-09-14 with the state never changing. The test now uses a second Persona so one Agent's reply wakes the other with no Human Message in between - which produced the Degraded state, the exact tooltip and the exact console line on the first attempt. Usage is not the problem: the adapter's first `usage_update` reported `used=49628` of `size=200000`, twenty-five times the Budget. |
| `STARTUPCONFIG-32` set only `Team__Acp__TraceWire` and expected wire traffic in the console. | Nothing appears. `LoggerTraceListener` forwards the dotacp trace to `ILogger` at **Trace** level, and `appsettings.Development.json` pins `Agency.Huddle` at `Debug`, which does not include Trace - so the flag binds, the listener attaches, and every line is filtered out. Exactly the ambiguity the test's own Inconclusive clause warns about, except the mismatch is the LEVEL, not the category. The step now also sets `${env:Logging__LogLevel__Agency.Huddle} = 'Trace'`; with that, the run produced 117 `Agency.Huddle.Acp.Wire` lines, 81 `trce:` lines and one `Authorization: Bearer` line, against 0 of each without it. |
| `STARTUPCONFIG-30` renamed one `Logging:LogLevel` key in `appsettings.Development.json` and expected the `Agency.Huddle` console lines to disappear. | They do not, and its Fail-if then blames the wrong thing - "log categories are not derived from the namespace". Two independent reasons the rename is inert: the Development value is `Debug` and nothing in `Huddle.App` logs at `Debug`, and `appsettings.json` sets `"Agency.Huddle": "Information"` separately, which merges in and keeps the category alive. A control run proves the prefix model is healthy: `"Agency.Huddle": "Warning"` in the overlay makes the lines vanish at once. The step now makes all three edits - the stale rename in BOTH files plus `Default` to `Warning` - which reproduces a completely silent console against a working app, exactly the hazard the test is about. |
| `STARTUPCONFIG-26` step 7 said to open the Production URL and "confirm the app renders there". | It does not, and cannot. `dotnet run --no-launch-profile` runs the UNPUBLISHED build output in the Production environment, where Static Web Assets are off, so `Huddle.App.<hash>.styles.css`, `ReconnectModal.<hash>.razor.js` and `_framework/blazor.web.<hash>.js` all return **500** (`FileNotFoundException`, with ASP.NET Core's own warning naming the cause) and the page is unstyled and non-interactive. Confirmed on a SOLE instance, so it is not two-instance interference; and confirmed NOT a product defect - `dotnet publish` copies all three into `wwwroot` and a published Production run serves all six assets 200. The step now says what to expect and what to check instead. |
| `STARTUPCONFIG-26` required run (a)'s console to be "noticeably more verbose for `Agency.Huddle` categories" than run (b)'s, and [Common procedures](common.md#o-log) implied the same. | Development does set the level to `Debug` and Production leaves it at `Information`, but the consoles are identical in the free lane because `Huddle.App` and `Huddle.Contracts` contain **zero** `Debug`-level log statements - the only three in the solution are in `Huddle.Acp`, which is off in both runs. Across 28 captured Development-lane consoles in the 2026-09-14 run, not one `dbug:` line appeared. The step asked the tester to judge a level from output that cannot show it. |
| Four lines across three areas called the `offline` status dot **grey**. | It is RED, by design: `theme.css` defines `--status-offline: light-dark(#b32121, #e05a5a)` with a comment tying it to the design system's `charts.red`, and `.agent-dot.offline` uses it. A tester matching the stated colour would file a correctly-themed dot as broken. Corrected in `STARTUPCONFIG-18`, `APPTOOLS` (the Persona-tile step) and `INVITEROOMS-03`'s Fail-if, Inconclusive and note; the checkable oracle is the `agent-dot offline` class and the `offline` tooltip, not the shade. Superseded by round 2 of the UI change list: Offline is now a hollow grey ring (red is Degraded's dash), so the oracle is the class, the tooltip and the ring glyph. |
| `SHELLNAV-26` step 2 matched the reconnect script with `ReconnectModal\.razor\.[^"]*\.js`. | The fingerprint sits before `.razor`, not after it - the real name is `ReconnectModal.<hash>.razor.js` - so the regex returned nothing and step 3 had no path to test. A tester would read an empty result as a missing import-map entry, which is one of the test's own Fail-if lines. |
| `SHELLNAV-25` expected TAB B to pick up a new theme after clicking a sidebar link. | A sidebar click is an *enhanced* navigation, which replaces the render tree but not `<head>` - the same constraint [Section 0.6](../manual-tests.md#06-not-a-defect) names for why a theme change reloads the whole page. Only a full document load can add the `themes/…` link, so the step described something correct code cannot do. Now split into an enhanced navigation that must NOT change, and a full load that must. Step 5 also said "Dark", which is invisible on a dark OS; it now names whichever theme contrasts with the device. |
| Two tests instructed the tester to run `dotnet test Huddle.slnx --`. | Out of scope: this script tests through the browser, and `TEAM_E2E` makes running the suite a spending risk. |
| A Trace-logging step used `$env:Logging__LogLevel__Agency__Huddle` and a quoted variant. | Neither works. The form that parses is `${env:Logging__LogLevel__Agency.Huddle}`. |
| Steps wrote rejected Persona fixtures to `App_Data\personas\`. | The library is `{DataDir}/{Acp:AgentsDir}`, which is `App_Data\Agents\`. |
| `ANTHROPIC_API_KEY` was ordered unset without ever being set. | It reads as a dangling step and invites a reader to drop it. The variable is ambient — inherited from a shell profile — and the Adapter authenticates through the `claude` CLI login instead. [Section 0.1](../manual-tests.md#01-what-you-need) now carries that as a requirement. |
| "RESET" meant three different things across areas. | Delete all of `App_Data`, delete only `rooms` + `team.db`, or delete only `prompts.json` — same word, incompatible effects. A test that said "run RESET" was ambiguous outside its own file, and the wrong one leaves stale Rooms that read as a defect. Now four named resets in [Common procedures](common.md): `P-RESET-ALL`, `P-RESET-ROOMS`, `P-RESET-TEAMS`, `P-RESET-SETTINGS`. |
| Two areas counted Adapters with a bare `Get-Process node`. | A developer machine normally has a dozen unrelated `node` processes, so that count is not evidence. `O-ADAPTERS` filters on the `claude-agent-acp` command line and is the only valid count. |
| `PERSONALIFECYCLE-01` step 17 typed a plain, un-mentioned Message into the demo `echo` Room and asserted it gets a reply. | Neither demo agent implements the Reply Gate — `DemoAgentHost` and `tools/echo-bot.ps1` answer only a Mention — so that assertion could never pass against correct code. |
| `O-ADAPTERS` and the `E-FREE` state scoped the Model/Effort picker's probe Adapter to `E-PAID` only. | `docs/engineering/rules.md` documents the probe as running regardless of `Acp:Enabled`, so a tester who saw that same expected row in `E-FREE` would misfile it as a cost-guard failure. |
| [Known limits](../known-limits.md) and [Section 0.6](../manual-tests.md#06-not-a-defect) said nothing about the picker probe ignoring `Acp:Enabled`, only that the catalog is cached. | A tester who checks the tester-facing "not a defect" surface rather than `rules.md` had no documented cover for the transient `node` row that appears the moment a New/Edit card opens, and would file a working probe as the exact cost-guard breach `PERSONALIFECYCLE-01`'s Fail-if warns against. |
| `REPLYGATEBUDGET-01`'s Pass-if line quoted the demo reply body for `hello @echo` as `echo: hello`. | `DemoAgentHost` strips only the `@` character, never the mention word, so the real body is `echo: hello echo`. The line described text correct code cannot produce; a tester matching it byte-for-byte would file working code as broken. |
| `STREAMINGTURN-28` spent the per-Persona token Budget by typing two Messages at Nova in a row. | The third one of these found. `PersonaRunner`'s read loop zeroes `tokensConsumed` on every Human Message *before* queueing the work item, and `ProcessWorkItemAsync` checks the Budget at the start of that item — so a Turn the Human prompted is always measured against a freshly-zeroed counter and can never be refused. The cap bounds *unattended* spend only. Rewritten around a second Persona and an Agent-to-Agent bounce, the same shape `STARTUPCONFIG-33` and `ROOMMESSAGING-30` already carry. |
| `REPLYGATEBUDGET-07` called the Budget pause a `red-bordered block`. | There is no red border in the design, in any theme: `app.css` gives `.budget-prompt` a neutral `border-top: 1px solid var(--border-default)` and puts the red on the text, `.budget-prompt-text { color: var(--danger-fg) }`. A tester matching the phrase literally would file working code. Reworded to name the elements and the danger-coloured text instead. |
| `REPLYGATEBUDGET-13` read the restored allowance off "the grey line above the composer" under a configured budget of `1`. | One reply spends the whole allowance, so the Room is exhausted and shows the pause block, not the plain grey line - `REPLYGATEBUDGET-07` and `-09` establish exactly that. The un-prefixed grey line only appears BELOW the cap, so the figure the test needs cannot be read the way it said. Step 4 now says to click **Leave paused** (which grants nothing) and read `Paused — 1 of 1 …`. |
| `REPLYGATEBUDGET-24` and `INVITEROOMS-11` told the tester to find a wire line whose `"type"` is `protocolError`. | No such value is ever sent. `Messages.cs` carries `[JsonDerivedType(typeof(ProtocolError), "error")]`, so the envelope reads `{"type":"error","code":"budgetExhausted",...}`. A tester grepping the terminal for `protocolError` finds nothing and records a working refusal as Inconclusive. Both literal spellings corrected to `error`; the prose uses of the C# type name are left alone. |
| `REPLYGATEBUDGET-30` (and two of its sibling notes) called the offline dot `grey`. | Same correction already made in `app-tools.md` and `invite-rooms.md`: `theme.css` defines `--status-offline: light-dark(#b32121, #e05a5a)`, so `agent-dot offline` computes `rgb(224, 90, 90)` - red. There is no grey dot anywhere in the design, and a tester matching the word would record working code as a Fail. Superseded by round 2: the Offline badge is a hollow grey ring now, with the badge's class and tooltip as the oracle. |
| `REPLYGATEBUDGET-32` asked the Persona to `list every fact you were told before this message, verbatim.` | Catch-up Messages reach the model as a bracketed context block (`turn.catchUpHeader` + one `turn.catchUpLine` each), not as turns addressed to it, and Haiku answers "I wasn't told any facts - you only asked me a math question." to that phrasing. Asked instead for the facts themselves, the same Persona returned both correctly, so the buffer was in the prompt all along. The step now asks a pointed factual question; the old wording turned a working buffer into a Fail. |
| `REPLYGATEBUDGET-34` step 2 said to *confirm* each Persona still shows Haiku and low, and its PATH TWO clicked Continue without checking the held Message. | Two separate traps. `P-RESET-ROOMS` deletes `team.db`, which is where the stored Model and Effort live, so after step 1 both cards always read `Agent default` - the step now says to set them again. And Continue re-delivers the held Message, which in a Room of three draws a reply only if it `@`-mentions the declining Persona; Haiku often ends a turn without the Mention its Persona body asks for, so PATH TWO silently tests nothing. The step now says to check before clicking. |
| `REPLYGATEBUDGET-36` built its minted Room from ONE Persona, and tried to reach the token Budget by typing at it under a Room Budget of 2. | Three ways it could not run. `ChatService.CreateRoomForAsync` short-circuits a single-agent request to `EnsureRoomForAsync`, so a lone Persona asked to create a Room is handed the one it already has and no Budget is minted — the model duly called `create_room` and reported success while the `rooms` table gained nothing. The token Budget is reset by every Human Message before the work item is queued, so typing at a Persona can never spend it (the fourth test in this run to carry that mistake). And a Room that hits its own cap declines in the read loop, before `ProcessWorkItemAsync` runs the token check, so a Room Budget of 2 stops the Agent-to-Agent bounce too early. Now two Personas, a Room Budget of 6, and the bounce as the driver. |

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
- **`PromptCatalog` being the authority.** No test deletes `prompts.default.json` to prove the app
  still runs on the text it shipped with.
