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
| Write or change a test | [testing.md](engineering/testing.md), then [../agents/Testing.md](../agents/Testing.md); for a Razor component also [../agents/BlazorTesting.md](../agents/BlazorTesting.md) |
| Test the running app by hand | [manual-tests.md](engineering/manual-tests.md), [manual-tests/common.md](engineering/manual-tests/common.md), then the feature's page in §5 |
| Choose which manual tests to run, or record a result | [manual-tests/planning.md](engineering/manual-tests/planning.md), [manual-tests/tracker.md](engineering/manual-tests/tracker.md) |
| Build, run, or find a `Team:` configuration key | [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) (Configuration, Build test run) |
| Debug a red CI run | [../agents/CIPipeline.md](../agents/CIPipeline.md) |
| Open a PR or talk to the Gitea remote | [../agents/GiteaOperations.md](../agents/GiteaOperations.md) |
| Write a spec or plan with UI | [../agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) |
| Code or test a MudBlazor component | [../agents/MudBlazorImplementation.md](../agents/MudBlazorImplementation.md), [engineering/mudblazor.md](engineering/mudblazor.md) |
| Run a project plan with parallel subagents | [../agents/DeliveryPlaybook.md](../agents/DeliveryPlaybook.md) |
| Build a test data folder | [../agents/DataSeeding.md](../agents/DataSeeding.md) |
| Change anything a Human sees | [Huddle.UserGuide.md](Huddle.UserGuide.md): update it in the same change |
| "Fix" something that looks missing | [known-limits.md](engineering/known-limits.md) first |
| Revisit a decision | [decisions.md](engineering/decisions.md), then the ADR in §4 |
| Add to the product plan | [roadmap.md](engineering/roadmap.md) |
| Learn what the product is for | [why-agency-huddle.md](why-agency-huddle.md), then [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) |

## 2. By feature

Each row is one feature: its design, the decisions behind it, and the by-hand tests
that exercise it. All are **chat** unless stated.

| Feature | Design | ADRs | Manual tests | Status |
| --- | --- | --- | --- | --- |
| Rooms, Messages, Mentions, the reply rule and the Budget | [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md), [architecture.md](engineering/architecture.md) | [0001](adr/0001-app-hosts-named-pipe-server.md), [0002](adr/0002-jsonl-file-per-room.md), [0003](adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md), [0004](adr/0004-direct-rooms-reply-without-mention.md), [0006](adr/0006-a-room-has-a-budget-for-agent-replies.md), [0012](adr/0012-a-room-says-why-it-stayed-quiet.md), [0018](adr/0018-a-room-can-be-archived-or-deleted.md) | [room-messaging](engineering/manual-tests/room-messaging.md), [invite-rooms](engineering/manual-tests/invite-rooms.md), [reply-gate-budget](engineering/manual-tests/reply-gate-budget.md), [pipe-external](engineering/manual-tests/pipe-external.md) | Built |
| A Turn: streaming, Stop, failures | [architecture.md](engineering/architecture.md) | [0008](adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md) | [streaming-turn](engineering/manual-tests/streaming-turn.md) | Built |
| Teammates: Persona files, rename, avatar, the card | [Huddle.UserGuide.md](Huddle.UserGuide.md) | [0011](adr/0011-a-rename-moves-the-teammate-not-its-history.md), [0019](adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) | [persona-lifecycle](engineering/manual-tests/persona-lifecycle.md), [teammate-card](engineering/manual-tests/teammate-card.md) | Built |
| Adapters, Model and Effort | [Huddle.Adapters-Specifications.md](Huddle.Adapters-Specifications.md), [acp-session-config.md](engineering/acp-session-config.md) (acp) | [0013](adr/0013-an-adapter-is-a-property-of-the-persona.md), [0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md), [0015](adr/0015-model-and-effort-reset-when-the-adapter-changes.md) | [adapters](engineering/manual-tests/adapters.md), [model-effort](engineering/manual-tests/model-effort.md) | Built |
| Adapter commands (`@Nova /compact`) | [Huddle.Commands-Specifications.md](Huddle.Commands-Specifications.md) | [0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md) | [adapter-commands](engineering/manual-tests/adapter-commands.md) | V1 built 2026-09-30 |
| Work Mode | [Huddle.WorkModes-Specifications.md](Huddle.WorkModes-Specifications.md) | [0033](adr/0033-a-persona-has-a-work-mode.md) | [work-mode](engineering/manual-tests/work-mode.md) | Phase 1 built; phases 2 and 3 designed |
| Turn detail and Spend | [Huddle.TurnDetail-Specifications.md](Huddle.TurnDetail-Specifications.md) | [0034](adr/0034-turn-detail-rides-on-tool-activity-as-optional-fields.md) | [turn-detail](engineering/manual-tests/turn-detail.md) | V1 built 2026-09-30 |
| One session per Room | [Huddle.RoomSessions-Specifications.md](Huddle.RoomSessions-Specifications.md) | [0024](adr/0024-an-agent-holds-one-session-per-room.md) | [room-sessions](engineering/manual-tests/room-sessions.md) | Built; the spec's header still says Proposed |
| File Changes and Memory | [Huddle.FileChanges-Specifications.md](Huddle.FileChanges-Specifications.md) | [0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md) | [file-changes](engineering/manual-tests/file-changes.md) | Built; paid live checks not run |
| Skills, Proposals and the Chief of Staff | [Huddle.Skills-Specifications.md](Huddle.Skills-Specifications.md) | [0020](adr/0020-a-hook-is-a-prompt.md), [0021](adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md) | [skills](engineering/manual-tests/skills.md) | Built |
| Agent tools (`mcp__team__*`) | [code-map.md](engineering/code-map.md) | none | [app-tools](engineering/manual-tests/app-tools.md) | Built |
| Questions (`ask_human`) | [Huddle.Questions-Specifications.md](Huddle.Questions-Specifications.md) | [0022](adr/0022-an-agent-asks-the-human-with-a-question.md) | [questions](engineering/manual-tests/questions.md) | Built 2026-10-01 |
| Prompt blocks: a Library image reaches the Agent as an image | [Huddle.PromptBlocks-Specifications.md](Huddle.PromptBlocks-Specifications.md) | [0036](adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md) | [prompt-blocks](engineering/manual-tests/prompt-blocks.md) | Built 2026-10-01 |
| Tasks, Views, Board and List | [Huddle.Tasks-Specifications.md](Huddle.Tasks-Specifications.md) | [0025](adr/0025-in-tasks-a-team-is-a-folder-by-convention.md), [0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) | [tasks](engineering/manual-tests/tasks.md) | Built |
| Library | [Huddle.Library-Specifications.md](Huddle.Library-Specifications.md) | [0027](adr/0027-the-library-sees-only-configured-roots.md), [0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md), [0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md) | [library](engineering/manual-tests/library.md), [teammates-library](engineering/manual-tests/teammates-library.md) | Built |
| Teams, Projects, Team Memory | [Huddle.TeamPages-Specifications.md](Huddle.TeamPages-Specifications.md), [Huddle.TeamPages-RetroActions.md](Huddle.TeamPages-RetroActions.md) | [0030](adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md), [0031](adr/0031-teammates-and-teams-are-sibling-folders.md), [0032](adr/0032-a-team-and-each-project-share-a-memory-folder.md) | [team-pages](engineering/manual-tests/team-pages.md) | Built 2026-09-29 |
| Themes and Appearance | [language.md](engineering/language.md) (Theme, Appearance) | [0009](adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) (superseded), [0010](adr/0010-a-theme-is-a-mudblazor-theme.md), [0016](adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md), [0017](adr/0017-a-theme-is-a-palette-not-a-pair.md) | [appearance-theme](engineering/manual-tests/appearance-theme.md) | Built |
| Prompts and Settings | [language.md](engineering/language.md) (Prompt) | [0007](adr/0007-model-facing-text-is-configuration.md) | [prompts-settings](engineering/manual-tests/prompts-settings.md) | Built |
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
| [roadmap.md](engineering/roadmap.md) | The numbered plan, and what exists before work in `PersonaRunner`, `ReplyGate`, `app.css` or `Themes/` | chat | ~10.7k |
| [decisions.md](engineering/decisions.md) | The decision record and the old-to-new vocabulary mapping | chat | ~6.3k |
| [mudblazor.md](engineering/mudblazor.md) | Which MudBlazor facts were checked against the package | chat | small |
| [acp-agent-guide.md](engineering/acp-agent-guide.md) | What was verified against the real adapter and what is only assumed. Written under the old `Team.*` names | acp | ~9k |
| [acp-session-config.md](engineering/acp-session-config.md) | How an ACP agent advertises models and effort, and how a client selects them | acp | ~4k |
| [Huddle.UserGuide.md](Huddle.UserGuide.md) | What the app does from the Human's side, and what changes in the files behind each action | chat | ~12k |

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
| [0016](adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md) | VS Code themes are converted once at authoring, not imported at run time. | Accepted |
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

## 5. Manual tests

One page per feature area, run by hand in a browser. Start with `common.md`.

| Page | Covers |
| --- | --- |
| [common.md](engineering/manual-tests/common.md) | Setup shared by all tests: terminals, states, procedures, oracles |
| [planning.md](engineering/manual-tests/planning.md) | Choosing which tests to run, cost guidance, test areas |
| [tracker.md](engineering/manual-tests/tracker.md) | Where each test stands, and recording a result |
| [adapter-commands.md](engineering/manual-tests/adapter-commands.md) | Adapter commands, compaction, profile enforcement, card listing, Stop |
| [adapters.md](engineering/manual-tests/adapters.md) | Adapter chosen per Persona, several adapters in one Room, streaming |
| [app-tools.md](engineering/manual-tests/app-tools.md) | App tool registration, discovery, real-model compliance |
| [appearance-theme.md](engineering/manual-tests/appearance-theme.md) | Theme selection, palette switching, the Appearance tab |
| [file-changes.md](engineering/manual-tests/file-changes.md) | File changes, Watched Folders, agent isolation, Memory |
| [invite-rooms.md](engineering/manual-tests/invite-rooms.md) | Room creation, membership, sidebar updates, invite paths |
| [library.md](engineering/manual-tests/library.md) | Library explorer, reading, writing, navigation, copying |
| [model-effort.md](engineering/manual-tests/model-effort.md) | Model and Effort selection, catalog probe, persistence |
| [persona-lifecycle.md](engineering/manual-tests/persona-lifecycle.md) | Persona lifecycle, Work Dirs, health badges, session restart |
| [pipe-external.md](engineering/manual-tests/pipe-external.md) | Named pipe protocol, external agent clients, message routing |
| [prompt-blocks.md](engineering/manual-tests/prompt-blocks.md) | Library images and document text sent to an Adapter as prompt blocks |
| [prompts-settings.md](engineering/manual-tests/prompts-settings.md) | Prompts editor in Settings: validation, save, per-field reset |
| [questions.md](engineering/manual-tests/questions.md) | An Agent asks with options (`ask_human`): the card, the answer as a Message |
| [reply-gate-budget.md](engineering/manual-tests/reply-gate-budget.md) | Reply gate, Mention resolution, per-Room Budget |
| [room-messaging.md](engineering/manual-tests/room-messaging.md) | Room messaging, Draft streaming, Markdown rendering, Transcript |
| [room-sessions.md](engineering/manual-tests/room-sessions.md) | Room Sessions: per-Room context, resume, memory isolation |
| [shell-nav.md](engineering/manual-tests/shell-nav.md) | App shell, navigation routes, layout, asset loading |
| [skills.md](engineering/manual-tests/skills.md) | Skills, Chief of Staff Greeting, team-building, safety |
| [startup-config.md](engineering/manual-tests/startup-config.md) | Startup configuration, first-run state, environment variables |
| [streaming-turn.md](engineering/manual-tests/streaming-turn.md) | Turn streaming, Drafts, Stop, failure alerts, reload |
| [tasks.md](engineering/manual-tests/tasks.md) | Tasks, Board drag, keyboard navigation, frontmatter, agent edits |
| [team-pages.md](engineering/manual-tests/team-pages.md) | Teams sidebar, Members tab, Files tab, shared Memory |
| [teammate-card.md](engineering/manual-tests/teammate-card.md) | Teammate card: create, edit, delete |
| [teammates-library.md](engineering/manual-tests/teammates-library.md) | Teammates page, file mirroring, Team grouping, filter, watcher |
| [turn-detail.md](engineering/manual-tests/turn-detail.md) | Turn detail, tool-call rows, Edit preview, Spend |
| [work-mode.md](engineering/manual-tests/work-mode.md) | Work Mode selection, adapter modes, permission handling, restart |

## 6. Records, not instructions

Do not orient from these. They explain how the product got here.

| Page | What it is |
| --- | --- |
| [why-agency-huddle.md](why-agency-huddle.md) | The essay: why a chat app, and where it differs from a pipeline. Updated 2026-10-01 |
| [engineering/product-observations.md](engineering/product-observations.md) | Observations about the product, kept as notes |
| [Huddle.TeamPages-RetroActions.md](Huddle.TeamPages-RetroActions.md) | Actions from the Team Pages retrospectives |
