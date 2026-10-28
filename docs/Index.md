# Documentation index

A router, not a document. Read this page, find your task or feature, and open only
the files on that row. Nothing here is explained twice: each row names the one page
that owns the answer. Paths are relative to `docs/`; `../` leaves it.

`Check-DocIndex.ps1` (in `agents/scripts/`) fails when a page under `docs/` is not
listed here, or a listed page does not exist. If you add, rename or delete a doc,
change this file in the same commit.

**Cost** is roughly the tokens a page spends when read whole. **Owner** says whose
subtree a page is in: **chat** is the chat surface, **acp** is the ACP effort
([CLAUDE.md](../CLAUDE.md) has the rule: neither side edits the other's pages).

> [!IMPORTANT]
> Two pages are **binding**: [rules.md](engineering/rules.md) before editing
> anything in `src/Huddle.App`, and [traps.md](engineering/traps.md) before touching
> `Huddle.Acp`, `Huddle.Contracts` or the wire protocol. Do not orient from
> [README.md](../README.md): it belongs to the ACP effort and its chat section is
> several milestones behind.

## 1. By task

| I am about to… | Read, in this order |
| --- | --- |
| Edit code in `src/Huddle.App` | [rules.md](engineering/rules.md) (binding), [code-map.md](engineering/code-map.md), then the feature row in §2 |
| Touch `Huddle.Acp`, `Huddle.Contracts` or the wire | [traps.md](engineering/traps.md) (binding), [acp-agent-guide.md](engineering/acp-agent-guide.md), [architecture.md](engineering/architecture.md) |
| Find which file does a thing | [code-map.md](engineering/code-map.md) |
| Understand how a Message travels | [architecture.md](engineering/architecture.md) |
| Name something, or write prose or interface copy | [language.md](engineering/language.md), then [CONTEXT.md](engineering/CONTEXT.md) for the vocabulary in dialogue |
| Write or change a test | [testing.md](engineering/testing.md), then [../agents/Testing.md](../agents/Testing.md); for a Razor component also [../agents/BlazorTesting.md](../agents/BlazorTesting.md). Which test project a test goes in: the *Where a test goes* section of [../agents/Testing.md](../agents/Testing.md) |
| Test the running app by hand | [manual-tests.md](engineering/manual-tests.md), [manual-tests/common.md](engineering/manual-tests/common.md), then the feature's page in §5 |
| Choose which manual tests to run, or record a result | [manual-tests/planning.md](engineering/manual-tests/planning.md), [manual-tests/tracker.md](engineering/manual-tests/tracker.md), [manual-tests/run-log.md](engineering/manual-tests/run-log.md) |
| See every past run of a manual test | [manual-tests/run-log.md](engineering/manual-tests/run-log.md) |
| Build, run, or find a `Team:` configuration key | [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) (Configuration, Build test run) |
| Debug a red CI run | [../agents/CIPipeline.md](../agents/CIPipeline.md) |
| Open a PR or talk to the Gitea remote | [../agents/GiteaOperations.md](../agents/GiteaOperations.md) |
| Write a spec or plan with UI | [../agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) |
| Name a UI container, or point at part of a screen: the shell, a pane, a Room's parts, a dialog | [engineering/wireframes.md](engineering/wireframes.md) |
| Build, change or review anything a Human sees: where every UI resource is, by task | [engineering/UXDesignGuidelines.md](engineering/UXDesignGuidelines.md) |
| Design a screen or style a component: colour, type, spacing, layout containers, overlays, copy | [engineering/design-system.md](engineering/design-system.md) (binding), then [../agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) |
| Code or test a MudBlazor component | [../agents/MudBlazorImplementation.md](../agents/MudBlazorImplementation.md), [engineering/mudblazor.md](engineering/mudblazor.md) |
| Run a project plan with parallel subagents | [../agents/DeliveryPlaybook.md](../agents/DeliveryPlaybook.md) |
| Build a test data folder | [../agents/DataSeeding.md](../agents/DataSeeding.md) |
| Change anything a Human sees | [Huddle.UserGuide.md](Huddle.UserGuide.md): update it in the same change |
| "Fix" something that looks missing | [known-limits.md](engineering/known-limits.md) first |
| Team types and hiring: a team type, a role, a Candidate, a hire card, Start Team | [Huddle.UserGuide.md](Huddle.UserGuide.md) (Hiring a Team from a type), [adr/0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md), then [code-map.md](engineering/code-map.md) (`TeamTypes/`, `Teams/HireService`) |
| Adding Skills or Packs, SKILLS.md, a Source, provenance, or a Skill's "modified locally" chip | [Huddle.UserGuide.md](Huddle.UserGuide.md) (Adding Skills with your AI tool), [Huddle.SkillPacks-Specifications.md](Huddle.SkillPacks-Specifications.md), then [code-map.md](engineering/code-map.md) (`SkillSources/`, `Huddle.SkillPacks`, `SkillProvenance`) |
| Process gates: `Process.md`, a gate's refusal, the checks command | [adr/0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md), [Huddle.UserGuide.md](Huddle.UserGuide.md) (Process and gates), then [code-map.md](engineering/code-map.md) (`Teams/ProcessGateService`) |
| Requests between Teams: filing, triage, confirming | [adr/0040](adr/0040-a-request-is-a-task-in-the-receiving-teams-backlog.md), [Huddle.UserGuide.md](Huddle.UserGuide.md) (Requests between Teams), then [code-map.md](engineering/code-map.md) (`Tasks/RequestLifecycle`, `Acp/Tools/*RequestTool`) |
| Retrospectives: the cadence, retro proposals, the register | [Huddle.UserGuide.md](Huddle.UserGuide.md) (Retrospectives), then [code-map.md](engineering/code-map.md) (`Teams/CadenceService`, `RetroProposalService`, `RetroFiles`) |
| Global Search: the top bar, the dropdown, a search provider or the jump to a Message | [Huddle.GlobalSearch-Specifications.md](Huddle.GlobalSearch-Specifications.md), then [Huddle.Tabs-Specifications.md](Huddle.Tabs-Specifications.md) for the breadcrumb it moves |
| References in a Message: the `@` picker, a Room or Team link, the reference snapshot, the Referenced items header | [Huddle.AtTagging-Specifications.md](Huddle.AtTagging-Specifications.md), then [Huddle.GlobalSearch-Specifications.md](Huddle.GlobalSearch-Specifications.md) for the providers it reuses; built |
| Revisit a decision | [decisions.md](engineering/decisions.md), then the ADR in §4 |
| Enable, disable or suppress an analyzer rule | [analyzer-decisions.md](engineering/analyzer-decisions.md) first |
| Add to the product plan | [roadmap.md](engineering/roadmap.md) |
| Pick up a small agreed refactor, or check whether a package was already evaluated | [backlog.md](engineering/backlog.md) |
| Learn what the product is for | [why-agency-huddle.md](why-agency-huddle.md), then [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) |

## 2. By feature

Each row is one feature: its design, the decisions behind it, and the by-hand tests
that exercise it. All are **chat** unless stated.

| Feature | Design | ADRs | Manual tests | Status |
| --- | --- | --- | --- | --- |
| Rooms, Messages, Mentions, the reply rule and the Budget | [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md), [architecture.md](engineering/architecture.md) | [0001](adr/0001-app-hosts-named-pipe-server.md), [0002](adr/0002-jsonl-file-per-room.md), [0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md), [0004](adr/0004-direct-rooms-reply-without-mention.md), [0006](adr/0006-a-room-has-a-budget-for-agent-replies.md), [0012](adr/0012-a-room-says-why-it-stayed-quiet.md), [0018](adr/0018-a-room-can-be-archived-or-deleted.md) | [room-messaging](engineering/manual-tests/room-messaging.md), [invite-rooms](engineering/manual-tests/invite-rooms.md), [reply-gate-budget](engineering/manual-tests/reply-gate-budget.md), [pipe-external](engineering/manual-tests/pipe-external.md) | Built |
| A Turn: streaming, Stop, failures | [architecture.md](engineering/architecture.md) | [0008](adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md) | [streaming-turn](engineering/manual-tests/streaming-turn.md) | Built |
| Teammates: Persona files, rename, avatar, the card | [Huddle.UserGuide.md](Huddle.UserGuide.md) | [0011](adr/0011-a-rename-moves-the-teammate-not-its-history.md), [0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md), [0047](adr/0047-a-teammates-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md) | [persona-lifecycle](engineering/manual-tests/persona-lifecycle.md), [teammate-card](engineering/manual-tests/teammate-card.md) | Built |
| The Human's profile: Name, Alias, Title, avatar; the first-run redirect | [Huddle.UserGuide.md](Huddle.UserGuide.md) (Settings > Profile), [rules.md](engineering/rules.md) | [0011](adr/0011-a-rename-moves-the-teammate-not-its-history.md), [0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) | [startup-config](engineering/manual-tests/startup-config.md) (STARTUPCONFIG-21, -35) | Built 2026-10-16 |
| Avatar library: choosing an Avatar from 324 ready-made images | [Huddle.AvatarLibrary-Specifications.md](Huddle.AvatarLibrary-Specifications.md) | [0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md); [0044](adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md); [0047](adr/0047-a-teammates-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md); [0048](adr/0048-the-avatar-library-ships-with-the-app-and-is-the-default.md) | [avatar-library](engineering/manual-tests/avatar-library.md) | Built 2026-10-08 |
| Adapters, Model and Effort | [Huddle.Adapters-Specifications.md](Huddle.Adapters-Specifications.md), [acp-session-config.md](engineering/acp-session-config.md) (acp) | [0013](adr/0013-an-adapter-is-a-property-of-the-persona.md), [0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md), [0015](adr/0015-model-and-effort-reset-when-the-adapter-changes.md) | [adapters](engineering/manual-tests/adapters.md), [model-effort](engineering/manual-tests/model-effort.md) | Built |
| More Adapters: Codex, Antigravity and Copilot over ACP (Gemini CLI as a secondary preset); Adapter presets, launch pins from the ACP registry, the tool-name format, how the system prompt reaches a non-Claude Adapter, the Turn Budget | [Huddle.Adapters-Vendors-Specifications.md](Huddle.Adapters-Vendors-Specifications.md), then [Huddle.Adapters-Specifications.md](Huddle.Adapters-Specifications.md) for the profile it extends | amends [0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md) when accepted; three candidate ADRs in its §12 | none yet (`adapter-vendors.md` planned, its phase 7) | Designed 2026-10-19, reviewed twice, not built; Codex and Antigravity specified in full, spikes and checks listed in its section 11, Antigravity handshake spike run |
| Adapter commands (`@Nova /compact`) | [Huddle.Commands-Specifications.md](Huddle.Commands-Specifications.md) | [0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) | [adapter-commands](engineering/manual-tests/adapter-commands.md) | V1 built 2026-09-30 |
| Work Mode | [Huddle.WorkModes-Specifications.md](Huddle.WorkModes-Specifications.md) | [0033](adr/0033-a-persona-has-a-work-mode.md) | [work-mode](engineering/manual-tests/work-mode.md) | Phase 1 built; phases 2 and 3 designed |
| Turn detail and Spend | [Huddle.TurnDetail-Specifications.md](Huddle.TurnDetail-Specifications.md) | [0034](adr/0034-turn-detail-rides-on-tool-activity-as-optional-fields.md) | [turn-detail](engineering/manual-tests/turn-detail.md) | V1 built 2026-09-30 |
| One session per Room | [Huddle.RoomSessions-Specifications.md](Huddle.RoomSessions-Specifications.md) | [0024](adr/0024-an-agent-holds-one-session-per-room.md) | [room-sessions](engineering/manual-tests/room-sessions.md) | Built |
| File Changes and Memory | [Huddle.FileChanges-Specifications.md](Huddle.FileChanges-Specifications.md) | [0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md) | [file-changes](engineering/manual-tests/file-changes.md) | Built; paid live checks not run |
| Skills, Proposals and the Chief of Staff | [Huddle.Skills-Specifications.md](Huddle.Skills-Specifications.md) | [0020](adr/0020-a-hook-is-a-prompt.md), [0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md) | [skills](engineering/manual-tests/skills.md) | Built |
| Skills and Skill Packs fetched by your own AI tool: `InstallSkillPack.md`, the `huddle-packs` validate command, the SKILLS.md registry of local-path pointers, updates, retiring a Pack, Source notes, and moving today's Packs and catalog | [Huddle.SkillPacks-Specifications.md](Huddle.SkillPacks-Specifications.md) (design); [Huddle.SkillPacksSpec.md](Huddle.SkillPacksSpec.md) (the vision it was designed from: superseded by the specification, kept for its reasoning) | [0059](adr/0059-skills-and-packs-are-fetched-by-the-humans-ai-tool.md), partly superseding [0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md) | [skill-packs](engineering/manual-tests/skill-packs.md) | Built 2026-10-18; manual tests not run |
| [0060](adr/0060-a-documents-history-is-what-its-editors-report.md) | A Markdown document's history is what its editors report: Agents log with `log_change(path, kind, summary)`, the Human's Library saves log themselves, and Huddle never verifies, infers or snapshots. JSON Lines under `{DataDir}/doc-history/`; a Library rename carries it; edits outside Huddle are not recorded. | Accepted |
| Agent tools (`mcp__team__*`) | [code-map.md](engineering/code-map.md) | none | [app-tools](engineering/manual-tests/app-tools.md) | Built |
| Questions (`ask_human`) | [Huddle.Questions-Specifications.md](Huddle.Questions-Specifications.md) | [0022](adr/0022-an-agent-asks-the-human-with-a-question.md) | [questions](engineering/manual-tests/questions.md) | Built 2026-10-01 |
| Team chats: a Room's Scope (root or one Team) and the Team Room | [Huddle.TeamChats-Specifications.md](Huddle.TeamChats-Specifications.md) | [0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) (revises TeamPages D-1, D-9) | [team-chats](engineering/manual-tests/team-chats.md) | Built 2026-10-12 (uploads: separate spec, not built) |
| Chat uploads: attach, paste or drop a file in the composer; it is saved to the Library and sent as its path | [Huddle.ChatUploads-Specifications.md](Huddle.ChatUploads-Specifications.md) | [0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) (amends 0027) | [uploads](engineering/manual-tests/uploads.md) | Built |
| References in a Message: the `@` picker, a Room or Team link, the reference snapshot, the Referenced items header | [Huddle.AtTagging-Specifications.md](Huddle.AtTagging-Specifications.md) | [0055](adr/0055-a-reference-is-text-with-a-stable-key.md) | [at-tagging](engineering/manual-tests/at-tagging.md) | Built |
| Needs You: the sidebar card of Rooms waiting on the Human | [Huddle.NeedsYou-Specifications.md](Huddle.NeedsYou-Specifications.md) | [0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) | [needs-you](engineering/manual-tests/needs-you.md) | Built 2026-10-12 |
| Feed: one stored history of what Teammates and Huddle did, shown as the Notifications panel (Top Bar button, Right sidebar) and the Feed Tab; each item posted by the component that knows it | [Huddle.Feed-Specifications.md](Huddle.Feed-Specifications.md) | [0063](adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md); beside [0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) | [feed](engineering/manual-tests/feed.md) | Built 2026-10-20 on feat/feed |
| Prompt blocks: a Library image reaches the Agent as an image | [Huddle.PromptBlocks-Specifications.md](Huddle.PromptBlocks-Specifications.md) | [0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) | [prompt-blocks](engineering/manual-tests/prompt-blocks.md) | Built 2026-10-01 |
| Tasks, Views, Board and List | [Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md) | [0025](adr/0025-in-tasks-a-team-is-a-folder-by-convention.md), [0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) | [tasks](engineering/manual-tests/tasks.md) | Built |
| Helpers: background agents that make one stateless judgement (no tools, no Room) and let Huddle apply the Verdict; the first, the Needs Human Helper, turns the Human gate on for Teammate-written Tasks that leave the Team; daily call cap with Continue | [Huddle.Helpers-Specifications.md](Huddle.Helpers-Specifications.md) | [0064](adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md); builds on [0061](adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md), [0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md), [0007](adr/0007-model-facing-text-is-configuration.md) | [helpers](engineering/manual-tests/helpers.md) (not yet run by a person) | Built 2026-10-20; see the spec's Appendix B for what differs; the Feed hooks (§12) were built in slice 3 |
| Helpers in the Feed (slice 3): a Helper's gate change as a Notification with its reason, Helper paused and failing items in a Helpers Category, and the Helper's wake-up posted by Huddle instead of the Human | [Huddle.HelpersInFeed-Specifications.md](Huddle.HelpersInFeed-Specifications.md) | [0065](adr/0065-huddle-posts-as-itself.md); builds on [0063](adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md), [0064](adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md) | [feed](engineering/manual-tests/feed.md) (FEED-15 to FEED-17), [helpers](engineering/manual-tests/helpers.md) (HELPERS-08) | Built 2026-10-20 on feat/helpers-in-feed; see the spec's Deviations; manual tests not run |
| Human gate: a Task with an Action, or marked Requires your acceptance, waits in the Needs You status until its Proof (the Team's Definition of Done) is complete and the Human accepts, requests changes or declines | [Huddle.HumanGate-Specifications.md](Huddle.HumanGate-Specifications.md) | [0061](adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md), [0062](adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md); amend [0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md) and [0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) | [human-gate](engineering/manual-tests/human-gate.md) | Built 2026-10-19 |
| Task attachments: link a Library file into a Task, or upload one to the Task's own folder | [Huddle.TaskAttachments-Specifications.md](Huddle.TaskAttachments-Specifications.md) | [0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md), [0055](adr/0055-a-reference-is-text-with-a-stable-key.md) | none yet | Built 2026-10-18 |
| Task read view and Task edit page: the detail drawer (`T7`) shows a dense read-only view of a Task, and its Edit opens the Task in edit mode in a new Tab (UX only; no new fields, List and Kanban unchanged) | [Huddle.TaskReadView-Specifications.md](Huddle.TaskReadView-Specifications.md) | none | none yet | Built 2026-10-19; the edit page is the only editor since 2026-10-20; OP-1 closing guard built, switching away still drops edits |
| Sprints: a named group of Tasks, listed in `sprints.md` | [Huddle.Sprints-Specifications.md](Huddle.Sprints-Specifications.md) | [0037](adr/0037-a-sprint-is-a-named-group-of-tasks-in-one-markdown-file.md) | [tasks](engineering/manual-tests/tasks.md) (TASKS-16 to TASKS-19 cover Sprints) | Implemented 2026-10-05 |
| Library | [Huddle.Library-Specifications.md](Huddle.Library-Specifications.md) | [0027](adr/0027-the-library-sees-only-configured-roots.md), [0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md), [0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md) | [library](engineering/manual-tests/library.md), [teammates-library](engineering/manual-tests/teammates-library.md) | Built |
| Document history: who created, edited, reviewed or objected to a Library document, as its editors logged it with `log_change`; the byline, the History at the bottom of the document, and the History panel | [Huddle.DocHistory-Specifications.md](Huddle.DocHistory-Specifications.md) | [0060](adr/0060-a-documents-history-is-what-its-editors-report.md) | [doc-history](engineering/manual-tests/doc-history.md) | Built 2026-10-18; paid live probe not run |
| Teams, Projects, Team Memory | [Huddle.TeamPages-Specifications.md](Huddle.TeamPages-Specifications.md), [Huddle.TeamPages-RetroActions.md](Huddle.TeamPages-RetroActions.md) | [0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md), [0031](adr/0031-teammates-and-teams-are-sibling-folders.md), [0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) | [team-pages](engineering/manual-tests/team-pages.md) | Built 2026-09-29 |
| Team types, hiring, process gates, Requests and retrospectives | [Huddle.UserGuide.md](Huddle.UserGuide.md), [code-map.md](engineering/code-map.md) | [0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md), [0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md), [0040](adr/0040-a-request-is-a-task-in-the-receiving-teams-backlog.md) | none yet | Built 2026-10-08 |
| Hiring pool: one catalog of jobs, two filter clouds, and the 26 built-in team types | [Huddle.HiringPool-Specifications.md](Huddle.HiringPool-Specifications.md) | [0049](adr/0049-a-hiring-pool-holds-the-jobs-and-a-team-type-lists-them.md) (amends 0038) | none yet | Built 2026-10-10 |
| Themes and Appearance | [theme-authoring.md](engineering/theme-authoring.md), [language.md](engineering/language.md) (Theme, Appearance) | [0009](adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) (superseded), [0010](adr/0010-a-theme-is-a-mudblazor-theme.md), [0016](adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md) (superseded), [0017](adr/0017-a-theme-is-a-palette-not-a-pair.md) | [appearance-theme](engineering/manual-tests/appearance-theme.md) | Built |
| Prompts and Settings | [language.md](engineering/language.md) (Prompt) | [0007](adr/0007-model-facing-text-is-configuration.md) | [prompts-settings](engineering/manual-tests/prompts-settings.md) | Built |
| Tabs: left-menu items open in preview and permanent Tabs | [Huddle.Tabs-Specifications.md](Huddle.Tabs-Specifications.md) | [0045](adr/0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md) | [shell-nav](engineering/manual-tests/shell-nav.md) | Built 2026-10-10; version 2 (split, pinned Tabs, Tab menu): [0046](adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md) |
| Right sidebar: the `S4` drawer and its contextual panels (Subjects, the panel catalog, Page commands) | [Huddle.RightSidebar-Specifications.md](Huddle.RightSidebar-Specifications.md) | none yet | [right-sidebar.md](engineering/manual-tests/right-sidebar.md) | Framework and five v1 panels built 2026-10-16 |
| Global Search: one box in the Top Bar finds Teams, Teammates, Library entries, Tasks and Rooms | [Huddle.GlobalSearch-Specifications.md](Huddle.GlobalSearch-Specifications.md) | [0046](adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md) (the Top Bar); [0051](adr/0051-search-keeps-no-index-of-its-own.md) | [global-search](engineering/manual-tests/global-search.md) | Built 2026-10-12; the Top Bar is built (PR #162); OP-2 is still assumed. Also offered to Agents as the `search` tool (§18, [0066](adr/0066-an-agents-search-reads-its-own-rooms-unless-it-asks-for-all.md)) |
| References in the Composer (`@` tagging): `@` picks a Teammate, Task, Room, Team or Library file; Messages keep a reference snapshot; Agents get a fresh Referenced items header | [Huddle.AtTagging-Specifications.md](Huddle.AtTagging-Specifications.md) | [0055](adr/0055-a-reference-is-text-with-a-stable-key.md); builds on [0051](adr/0051-search-keeps-no-index-of-its-own.md), [0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) and [0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) | [at-tagging](engineering/manual-tests/at-tagging.md) | Built |
| App shell, navigation and startup | [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) | none | [shell-nav](engineering/manual-tests/shell-nav.md), [startup-config](engineering/manual-tests/startup-config.md) | Built |
| Agent topologies (mesh or tree) | [why-agency-huddle.md](why-agency-huddle.md) | [0005](adr/0005-agent-topologies-are-emergent.md) | none | Built |

## 3. Engineering reference

| Page | Open it when you need | Owner | Cost |
| --- | --- | --- | --- |
| [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) | The orientation: what a team is for, solution layout, every `Team:` configuration key, build and test commands, toolchain rules | chat | ~14k |
| [rules.md](engineering/rules.md) | **Binding.** The rules for `src/Huddle.App` that the compiler does not check | chat | ~3.9k |
| [traps.md](engineering/traps.md) | **Binding.** What silently fails in `Huddle.Acp`, `Huddle.Contracts` and the wire | chat | ~2.6k |
| [language.md](engineering/language.md) | The defined terms, and the words that are wrong here | chat | ~3.1k |
| [CONTEXT.md](engineering/CONTEXT.md) | The vocabulary used in dialogue, not defined | chat | ~0.6k |
| [code-map.md](engineering/code-map.md) | Which file does a thing | chat | ~2.8k |
| [architecture.md](engineering/architecture.md) | How a Message travels at run time | chat | ~1.1k |
| [testing.md](engineering/testing.md) | How to add a test, and what no test can prove | chat | ~2.3k |
| [known-limits.md](engineering/known-limits.md) | What is deliberately absent, before you "fix" it | chat | ~1.9k |
| [backlog.md](engineering/backlog.md) | Agreed but unscheduled engineering work, and the packages evaluated and declined | chat | ~1k |
| [roadmap.md](engineering/roadmap.md) | The numbered plan, and what exists before work in `PersonaRunner`, `ReplyGate`, `app.css` or `Themes/` | chat | ~10.7k |
| [installer.md](engineering/installer.md) | How to install Agency.Huddle on Windows or macOS, what the installer checks and why it stays idempotent, release process, integrity verification | chat | ~4k |
| [decisions.md](engineering/decisions.md) | The decision record and the old-to-new vocabulary mapping | chat | ~6.3k |
| [analyzer-decisions.md](engineering/analyzer-decisions.md) | Which compiler and Sonar rules are off or on, what each group rests on, and which are proven to fire | chat | ~4.5k |
| [duplication-log.md](engineering/duplication-log.md) | Before acting on a row of `Report-Duplication.ps1`: which duplicates were extracted, which are kept as copies on purpose, and why, one section per iteration | chat | small |
| [wireframes.md](engineering/wireframes.md) | The code and name of every UI container, drawn as SVG wireframes from the shell down to each page and overlay | chat | ~4k |
| [UXDesignGuidelines.md](engineering/UXDesignGuidelines.md) | The hub for UI work: which document owns which answer, the reading order by task, the order of authority, and how to check a change in the running app; a catalog of every reusable component, helper and stylesheet class | chat | ~7.5k |
| [design-system.md](engineering/design-system.md) | **Binding** for UI. The tokens, type roles, spacing, shape, elevation, icons, layout, overlay anatomy, component defaults, interface copy and stylesheet rules, with a checklist | chat | ~10k |
| [theme-authoring.md](engineering/theme-authoring.md) | For designers: an annotated palette map and screen-by-screen tables from what you see to the palette property that paints it. For engineers: how a Theme reaches the screen, and the steps and tests for adding one | chat | ~11k |
| [mudblazor.md](engineering/mudblazor.md) | Which MudBlazor facts were checked against the package | chat | small |
| [acp-agent-guide.md](engineering/acp-agent-guide.md) | What was verified against the real adapter and what is only assumed. Written under the old `Team.*` names | acp | ~9k |
| [acp-session-config.md](engineering/acp-session-config.md) | How an ACP agent advertises models and effort, and how a client selects them | acp | ~4k |
| [Huddle.UserGuide.md](Huddle.UserGuide.md) | What the app does from the Human's side, and what changes in the files behind each action | chat | ~12k |
| [Huddle.About.md](Huddle.About.md) | A one-page, non-technical description of the product for marketing copy, distilled from the user guide | chat | ~1k |

## 4. Decisions (ADRs)

One row per decision. Open the ADR only when its row is your question.

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](adr/0001-app-hosts-named-pipe-server.md) | The app hosts a named-pipe server; Agent Hosts connect as clients. | Accepted |
| [0002](adr/0002-jsonl-file-per-room.md) | Message history lives in append-only JSONL per Room, not SQLite. | Accepted |
| [0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md) | Every Agent receives every Message with a mention flag; a direct Room is defined by membership. Uses the pre-2026-09-11 words. | Accepted |
| [0004](adr/0004-direct-rooms-reply-without-mention.md) | In a two-Member Room an Agent answers every Message without a Mention. Uses the pre-2026-09-11 words. | Accepted |
| [0005](adr/0005-agent-topologies-are-emergent.md) | Agent topology is emergent; Rooms are seeded by their creators and followed by opt-in. | Proposed |
| [0006](adr/0006-a-room-has-a-budget-for-agent-replies.md) | A Room has a Budget for agent replies; Human approval raises it. | Accepted |
| [0007](adr/0007-model-facing-text-is-configuration.md) | Model-facing text is configuration with defaults in code, not hard-coded literals. | Accepted |
| [0008](adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md) | A Turn is visible, stoppable, and reports failures. | Accepted |
| [0009](adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) | A Theme is a CSS file layered over 39 Tokens. Replaced by 0010. | Superseded |
| [0010](adr/0010-a-theme-is-a-mudblazor-theme.md) | A Theme is a MudTheme object in ThemeCatalog, not a stylesheet. | Accepted |
| [0011](adr/0011-a-rename-moves-the-teammate-not-its-history.md) | Renaming a Persona renames the existing user row in place. | Accepted |
| [0012](adr/0012-a-room-says-why-it-stayed-quiet.md) | A Room-level note explains why it stayed quiet. | Accepted |
| [0013](adr/0013-an-adapter-is-a-property-of-the-persona.md) | One Persona, one Adapter, named in its frontmatter. | Accepted |
| [0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md) | The tool-name prefix is derived from the Adapter Profile in code. | Accepted |
| [0015](adr/0015-model-and-effort-reset-when-the-adapter-changes.md) | Changing Adapter clears Model and Effort, and says so inline. | Accepted |
| [0016](adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md) | VS Code themes are converted once at authoring, not imported at run time. | Superseded (ports removed 2026-10-17) |
| [0017](adr/0017-a-theme-is-a-palette-not-a-pair.md) | A Theme carries one palette; its ThemeMode names light or dark. | Accepted |
| [0018](adr/0018-a-room-can-be-archived-or-deleted.md) | Rooms can be archived reversibly or deleted permanently. | Accepted |
| [0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) | An Avatar is three optional fields (Image, Label, Background) held apart from the Persona. | Accepted |
| [0020](adr/0020-a-hook-is-a-prompt.md) | Hook is renamed to Prompt everywhere in the codebase. | Accepted |
| [0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md) | A Skill is named Markdown the Agent reads with a tool. | Accepted |
| [0022](adr/0022-an-agent-asks-the-human-with-a-question.md) | An Agent asks with an `ask_human` tool; the answer is a Message quoting and Mentioning the asker. | Accepted |
| [0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md) | The change list rides on the Agent's next Turn, built inside PersonaRunner. | Accepted |
| [0024](adr/0024-an-agent-holds-one-session-per-room.md) | One Room Session per Persona and Room pair; continuity through Memory and File Changes. | Accepted |
| [0025](adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) | Tasks get their own root; a Team is a folder by convention. | Accepted |
| [0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) | Any Task change posts a Message Mentioning the assignee, waking it through the Reply Gate. | Accepted |
| [0027](adr/0027-the-library-sees-only-configured-roots.md) | The Library shows only configured roots; the resolver refuses paths outside them. | Accepted |
| [0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md) | The Library edits Markdown as source text; a save is byte-exact and keeps the encoding. | Accepted |
| [0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md) | Last write wins; the Library re-reads on focus when there are no unsaved edits. | Accepted |
| [0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md) | One tree at `Teams/`, read by both Tasks and the Library. | Proposed |
| [0031](adr/0031-teammates-and-teams-are-sibling-folders.md) | `Teammates/` and `Teams/` are sibling folders; `work/` lives under each Teammate. | Accepted |
| [0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) | Team and Project folders may hold a `memory/` folder of shared facts. | Accepted |
| [0033](adr/0033-a-persona-has-a-work-mode.md) | A Persona has a Work Mode from its Adapter, applied at open and resume. | Accepted |
| [0034](adr/0034-turn-detail-rides-on-tool-activity-as-optional-fields.md) | ToolActivity gains optional Path, Line and Edit fields; the wire version stays 3. | Accepted |
| [0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) | A command is a Message like `@Nova /compact`; an allowlist sits on the Adapter Profile. | Accepted |
| [0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) | A Prompt block (an image, or a document's text) is sent only when the Adapter advertised it, and only from the Message that started the Turn. | Accepted |
| [0037](adr/0037-a-sprint-is-a-named-group-of-tasks-in-one-markdown-file.md) | A Sprint is an id and a name in one `sprints.md`; a Task holds the id; the line is removed when no Task holds it; no dates, state or screen. | Accepted |
| [0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md) | A team type is a folder of JSON and Markdown files; built-ins are embedded, and a user folder replaces by id. Code knows no role name. | Accepted |
| [0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md) | A Team's gates are listed in `Process.md` and enforced in `TaskService` and the permission handler; only the Human overrides, on record. | Accepted |
| [0040](adr/0040-a-request-is-a-task-in-the-receiving-teams-backlog.md) | A Request is a Task with six extra keys in the receiving Team's backlog; a state machine and its tools decide who may move it. | Accepted |
| [0041](adr/0041-retro-proposals-are-applied-only-by-the-humans-click.md) | A retro agent only proposes; Huddle edits `Process.md`, `_team.md` or a Teammate file when the Human clicks Accept, and the register row is Huddle's. | Accepted |
| [0042](adr/0042-cadence-state-is-persisted-so-a-restart-does-not-double-fire.md) | The last digest and retro firing is stored in `team.db`, written before the post and restored if it is refused, so a restart fires nothing twice. | Accepted |
| [0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md) | An imported Skill is pinned to a commit, recorded with a SHA-256 in `.source.json`, and reviewed before install; a missing licence warns but never blocks. Partly superseded by 0059: the download goes. | Accepted, partly superseded |
| [0044](adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md) | An avatar library is a read-only folder read on open and dropped on close; a pick copies the image into the Avatar store through the upload path. | Accepted |
| [0045](adr/0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md) | A Tab is a remembered URL; only the active Tab is mounted; tabs persist in browser storage and the address decides which is active. | Accepted |
| [0046](adr/0046-a-group-mounts-its-own-page-and-the-url-names-the-focused-one.md) | Each Tab group mounts its own page; the URL names the focused group; a Tab moves between up to two groups and can be pinned. | Accepted |
| [0047](adr/0047-a-teammates-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md) | A Teammate's avatar is edited in its own dialog, opened by pressing the avatar in every card mode; Apply holds the result in Create and Edit, Save writes it at once in View; the colour controls are hidden under An image. | Accepted |
| [0048](adr/0048-the-avatar-library-ships-with-the-app-and-is-the-default.md) | The avatar library ships in the repository and is the default when `Team:AvatarLibrary:Dir` is unset; a configured `Dir` replaces it and is never backed by it. | Accepted |
| [0049](adr/0049-a-hiring-pool-holds-the-jobs-and-a-team-type-lists-them.md) | Jobs live once in a global hiring pool (`hiringpool.json` plus a persona file each); a team type lists the jobs it uses and keeps only its manager, limit, coverage and vendor rule; presets are gone; the pool is browsed by a type cloud and a tags-and-skills cloud. Amends 0038. | Accepted |
| [0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) | Needs You is one in-memory singleton keyed by Room id that observes existing `RoomEvents`; four reasons (Mention, Question, Form, Budget); only a Human post or a dismissal clears a Room, never opening it; the Mention reason is rebuilt from the end of each Transcript at startup; tracker, list and card are separate so the list can move to a right-hand sidebar. | Accepted |
| [0051](adr/0051-search-keeps-no-index-of-its-own.md) | Global Search keeps no index of its own: every search re-reads from memory or disk, bounded by a per-root visit budget, a per-file size cap, the debounce and cancellation; `WikiLinkIndex` is not used. | Accepted |
| [0052](adr/0052-a-room-belongs-to-the-root-or-to-one-team.md) | A Room's Scope is the root or one Team, stored in a `room_teams` sibling table at creation and never changed; root and Team one-to-ones are separate Rooms; each Team has a Team Room reconciled from the catalog; a Team that leaves the catalog archives its Rooms. Revises TeamPages D-1 and D-9. | Accepted |
| [0053](adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) | A file attached in the composer is saved at once into the Room's upload folder (a new built-in Uploads Library Root, or the Team's `Uploads` folder) and its path is appended to the Message at send; the existing prompt-block planner decides what the Agent gets; kind by magic bytes, SVG and executables refused; the chat Message is the notification and upload folders are silent in File Changes; Room delete recycles its uploads. Amends 0027. | Built |
| [0054](adr/0054-a-light-theme-and-a-dark-theme.md) | The Human picks one light and one dark Theme; a top-bar button switches between them. | Accepted |
| [0055](adr/0055-a-reference-is-text-with-a-stable-key.md) | A reference in the Composer is ordinary text with a stable key (a Task id, a Room id, a Team Name, a Library path); the Message's `References` snapshot is optional metadata of what the sender saw, additive on the wire with the version unchanged; Agents get a compact, fresh Referenced items header for the trigger Message only. | Accepted |
| [0056](adr/0056-the-ui-renders-at-100-percent-with-a-px-type-scale.md) | The page is not zoomed (`html { zoom: 0.9 }` removed); the type scale is set in rendered px in the Theme (h5 20, h6 16, subtitle 14, body1 14, body2 13, caption 12, overline 11; nothing under 11px; weights 400, 500, 600); prose is capped at 75ch and the Transcript column at about 760px. | Accepted |
| [0057](adr/0057-a-theme-may-carry-a-style-pack.md) | A Theme may carry a style pack (`wwwroot/packs/<name>.css`): a stylesheet whose every selector is scoped to the Theme's marker (`data-theme-pack`), holding no colour literal, guarded by `PackSourceTests`. Only Nordic has one; its Surface is the white raised paper and Background the ground. Supersedes ADR-0010's "no `theme.css`" for packs only. | Superseded by 0058 |
| [0058](adr/0058-a-theme-is-a-palette-a-skin-and-traits.md) | A Theme is one embedded JSON skin per Theme and mode (`Themes/Skins/`): palette, `--hu-*` tokens from the `SkinTokens` registry (defaults reproduce Huddle's old look), structural traits rendered as `data-hu-*` and written once in `app.css`, faces and shadows. Per-Theme customizations in `appearance.json` replace Custom Accent. Supersedes 0057. | Accepted |
| [0059](adr/0059-skills-and-packs-are-fetched-by-the-humans-ai-tool.md) | The Human's AI tool fetches Skills and Packs and records local-path pointers in SKILLS.md; Huddle makes no network call for Skills, reads the pointers and runs the same checks as `huddle-packs`. Partly supersedes ADR-0043 (the download goes) and amends ADR-0027 (a read-only Skill Sources root). | Accepted |
| [0061](adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md) | A Task with `human_gate: true` or an `action:` is Human-gated: the ninth status Needs You (between Review and Done) is entered only with complete Proof; only the Human's Accept, bound to a hash of the title, Action and description, opens Done; Request changes and Decline need a reason; no override on these Tasks; Needs You lists them from the Task files. Amends 0039 and 0050. | Accepted |
| [0062](adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md) | A `## Definition of Done` section in `Process.md`, one `- Label: rule` bullet per item, with four rules (`evidence from <step>`, `command … exits 0`, `task tagged <tag>`, `document reviewed by <step>`); the Done gates of 0039 join on their own; an unreadable rule is open; enforced only on Human-gated Tasks. | Accepted |
| [0063](adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md) | The Feed is one `feed_events` table in `team.db`, written only by a background writer; producers (`TaskService`, `NeedsYouTracker`, `ChatService`, `TeamRoomReconciler`, `DocHistoryStore`, later Helpers) call `IFeedPublisher.Post`/`Resolve`, which only enqueue and never fail the domain operation; the sender decides `notify` and resolves its own rows; no grouping, no Message text; keyset paging; dismissed Notifications deleted 30 days after dismissal, Activity 30 days after creation. | Accepted |
| [0064](adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md) | A Helper is not a Teammate: no tools, Room, Persona or Messages; it answers one judgement in JSON in a fresh adapter process, and Huddle applies the typed Verdict through the domain service. Each Helper has a daily call cap that pauses it and asks the Human to Continue; failures never block a save. The Needs Human Helper only turns the Human gate on, as `TaskActorKind.Helper`, and never judges a Task again once the Human turned its gate off. | Proposed |
| [0065](adr/0065-huddle-posts-as-itself.md) | Huddle has its own sender, `KnownIds.Huddle`, neither the Human nor an Agent, no `User` row and no Membership; `ChatService.PostAsHuddleAsync` alone may use it. It answers no Mention, clears nothing from Needs You, resets no Budget and drops no waiting card. First used for a Helper's wake-up. | Accepted |
| [0066](adr/0066-an-agents-search-reads-its-own-rooms-unless-it-asks-for-all.md) | The Agent `search` tool reads the Rooms its caller is a Member of, plus Rooms scoped to the caller's Teams, unless it passes `scope: "all"`; the list is applied before any Transcript is read, and every `all` search is logged. | Accepted |

## 5. Manual tests

One page per feature area, run by hand in a browser. Start with `common.md`.

| Page | Covers |
| --- | --- |
| [common.md](engineering/manual-tests/common.md) | Setup shared by all tests: terminals, states, procedures, oracles |
| [planning.md](engineering/manual-tests/planning.md) | Choosing which tests to run, cost guidance, test areas |
| [tracker.md](engineering/manual-tests/tracker.md) | Where each test stands, and recording a result |
| [run-log.md](engineering/manual-tests/run-log.md) | Every manual test, its file, and the history of its runs |
| [adapter-commands.md](engineering/manual-tests/adapter-commands.md) | Adapter commands, compaction, profile enforcement, card listing, Stop |
| [adapters.md](engineering/manual-tests/adapters.md) | Adapter chosen per Persona, several adapters in one Room, streaming |
| [app-tools.md](engineering/manual-tests/app-tools.md) | App tool registration, discovery, real-model compliance |
| [appearance-theme.md](engineering/manual-tests/appearance-theme.md) | Theme selection, palette switching, the Appearance tab |
| [at-tagging.md](engineering/manual-tests/at-tagging.md) | References in a Message: the `@` picker, what a pick writes, Room and Team links, Copy link, the Referenced items header |
| [avatar-library.md](engineering/manual-tests/avatar-library.md) | Avatar library picker: narrowing the 324 pictures, picking one, the thumbnail mount, a missing or unreadable library |
| [doc-history.md](engineering/manual-tests/doc-history.md) | Document history: the byline, the History, the History panel, how often Agents log |
| [human-gate.md](engineering/manual-tests/human-gate.md) | Human gate: the Proof list, the decision card, Accept and Decline, the board drag, the Needs You page |
| [helpers.md](engineering/manual-tests/helpers.md) | Helpers: the Helpers tab, Save and Reset, the Needs Human judgement, the Human's override, the daily cap with Continue, a failing Adapter |
| [feed.md](engineering/manual-tests/feed.md) | Feed: the Notifications bell and panel, the Feed Tab, cards, filters, the new-items pill, the restart |
| [file-changes.md](engineering/manual-tests/file-changes.md) | File changes, Watched Folders, agent isolation, Memory |
| [global-search.md](engineering/manual-tests/global-search.md) | Global Search: the box, the dropdown, filters, the jump to a Message, the Top Bar placement |
| [invite-rooms.md](engineering/manual-tests/invite-rooms.md) | Room creation, membership, sidebar updates, invite paths |
| [library.md](engineering/manual-tests/library.md) | Library explorer, reading, writing, navigation, copying |
| [model-effort.md](engineering/manual-tests/model-effort.md) | Model and Effort selection, catalog probe, persistence |
| [needs-you.md](engineering/manual-tests/needs-you.md) | Needs You card: Mention rebuilt at startup, Dismiss, Archive |
| [right-sidebar.md](engineering/manual-tests/right-sidebar.md) | Right sidebar: the panels by Subject, Outline scrolling, collapse and focus |
| [persona-lifecycle.md](engineering/manual-tests/persona-lifecycle.md) | Persona lifecycle, Work Dirs, health badges, session restart |
| [pipe-external.md](engineering/manual-tests/pipe-external.md) | Named pipe protocol, external agent clients, message routing |
| [prompt-blocks.md](engineering/manual-tests/prompt-blocks.md) | Library images and document text sent to an Adapter as prompt blocks |
| [prompts-settings.md](engineering/manual-tests/prompts-settings.md) | Prompts editor in Settings: validation, save, per-field reset |
| [questions.md](engineering/manual-tests/questions.md) | An Agent asks with options (`ask_human`): the card, the answer as a Message |
| [reply-gate-budget.md](engineering/manual-tests/reply-gate-budget.md) | Reply gate, Mention resolution, per-Room Budget |
| [room-messaging.md](engineering/manual-tests/room-messaging.md) | Room messaging, Draft streaming, Markdown rendering, Transcript |
| [room-sessions.md](engineering/manual-tests/room-sessions.md) | Room Sessions: per-Room context, resume, memory isolation |
| [shell-nav.md](engineering/manual-tests/shell-nav.md) | App shell, navigation routes, layout, asset loading |
| [skill-packs.md](engineering/manual-tests/skill-packs.md) | Skill Packs: a real AI tool, choosing a Pack, updates, the move, shared folders, a withdrawn Source |
| [skills.md](engineering/manual-tests/skills.md) | Skills, Chief of Staff Greeting, team-building, safety |
| [startup-config.md](engineering/manual-tests/startup-config.md) | Startup configuration, first-run state, environment variables |
| [streaming-turn.md](engineering/manual-tests/streaming-turn.md) | Turn streaming, Drafts, Stop, failure alerts, reload |
| [tasks.md](engineering/manual-tests/tasks.md) | Tasks, Board drag, keyboard navigation, frontmatter, agent edits |
| [team-chats.md](engineering/manual-tests/team-chats.md) | Team chats: the Team Room, scoped New chat, Members-tab Message, archiving, Task wakes and digests |
| [team-pages.md](engineering/manual-tests/team-pages.md) | Teams sidebar, Members tab, Files tab, shared Memory |
| [teammate-card.md](engineering/manual-tests/teammate-card.md) | Teammate card: create, edit, delete |
| [teammates-library.md](engineering/manual-tests/teammates-library.md) | Teammates page, file mirroring, Team grouping, filter, watcher |
| [turn-detail.md](engineering/manual-tests/turn-detail.md) | Turn detail, tool-call rows, Edit preview, Spend |
| [work-mode.md](engineering/manual-tests/work-mode.md) | Work Mode selection, adapter modes, permission handling, restart |

## 6. Records, not instructions

Do not orient from these. They explain how the product got here.

| Page | What it is |
| --- | --- |
| [why-agency-huddle.md](why-agency-huddle.md) | The essay: why a chat app, and where it differs from a pipeline. Updated 2026-10-03 |
| [engineering/product-observations.md](engineering/product-observations.md) | Observations about the product, kept as notes |
| [Huddle.TeamPages-RetroActions.md](Huddle.TeamPages-RetroActions.md) | Actions from the Team Pages retrospectives |
