# Agent CLI: letting an AI agent drive Huddle

> [!NOTE]
> **Status: backlog, not scheduled.** Explored on 2026-11-16 and parked on purpose. Nothing here
> is built. Before you build it, turn this brief into a specification and an ADR, and recheck the
> code facts in [What already exists](#what-already-exists): they were true on 2026-11-16.

## The idea

An AI agent running outside Huddle, such as Claude Code in a terminal, gets a CLI it can use to
work in Huddle as a registered Agent. With it, the agent can:

- join a Room and chat;
- create, read, update and close Tasks, Rooms, memory and Jobs;
- change settings.

The Human can watch it happen in the browser.

There are two levels:

1. **Level 1:** CRUD, settings and chat through the CLI.
2. **Level 2:** Level 1, plus the Human seeing it: the open browser shows where the agent is
   working and what it changed.

## Decisions already made

| # | Decision |
| --- | --- |
| D1 | **The CLI hides the transport.** The caller only ever runs CLI verbs. It never uses MCP, the pipe or HTTP directly, so the CLI is free to change what it uses underneath. |
| D2 | **The caller is a registered Agent with its own key.** Huddle generates a key for the agent, and the agent sends it on every call. |
| D3 | **The CLI goes through a web API on the running app.** It never edits `team.db` or the config and data files directly. |
| D4 | **V1 is Strict local only:** this computer, the same as the app's default. |
| D5 | **Versions must match exactly.** The CLI compares its own app version with the running app's and fails with an error on any difference. No negotiation: the CLI and the app always ship together at the same version. |
| D6 | **The contracts are `internal`.** The request and response types are never published. |
| D7 | **No OpenAPI.** The API exists only for the CLI. |
| D8 | **Level 2 does not use browser automation.** No Playwright, no fake cursor, no typing into the page. |

## Why the CLI does not edit files

Huddle keeps its data in about 30 kinds of file (`_team.md`, `profile.json`, `appearance.json`,
`schedule.md`, `views.json` and others), in `team.db` (SQLite) and in one `.jsonl` file per Room.
Editing those directly would cause three problems:

- **Open screens go stale.** Some stores watch their files (`PersonaStore`, `TaskStore`,
  `TeamCatalog`, `AppearanceStore`, `SkillStore`), but the `team.db` rows (Model, Effort,
  connectors, spend) are not watched.
- **The checks are skipped.** They live in the services, not in the files: the Human gate's hash
  ([ADR 0061](../adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md)),
  the rename cascade
  ([ADR 0011](../adr/0011-a-rename-moves-the-agent-not-its-history.md)),
  `TeamMemoryWriteGuard` and `WorkDirPermissionHandler`.
- **Writes collide with the running app.** Between the Human and an agent, the last write wins
  ([ADR 0029](../adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)).

## What already exists

| Piece | Where | How the CLI reuses it |
| --- | --- | --- |
| `huddle` CLI verbs (`auth-token`, `dashboard`, `devices`), which run before the web host starts | `src/Huddle.App/Cli/CliVerbs.cs` | The new verbs go here as `huddle agent …`. |
| Finding the running copy | `CliVerbs.RunningCopy`, `InstanceLock`, `instance.json` | Gives each CLI call the address of the running app. |
| An HTTP route group behind a bearer token | `Gateway/GatewayAdminEndpoints.cs` (`/_gateway`, `RequireBearerAsync`) | The `/_agent` routes use the same kind of filter. |
| Hashed, scoped, revocable tokens | `Gateway/DeviceRegistry.cs`, `GatewayScopes` | Agent keys follow the same model, with one extra field: the agent the key belongs to. |
| About 40 agent tools, each bound to its caller | `Acp/Tools/*Tool.cs` (`IAppTool`, `callerAgentId`), built by `DotAcpAgentHostFactory.BuildToolSet` | Served to the agent the key names, through `/_agent/tools/{name}`. |
| External agents joining chat over the named pipe (no authentication) | `Pipes/`, `Huddle.Contracts`, [ADR 0001](../adr/0001-app-hosts-named-pipe-server.md), `tools/echo-bot.ps1` | Left as it is. The CLI uses HTTP for chat instead. |
| Navigation started by the server | `Circuits/ServerNavigator.cs`, `Components/Layout/TabSync.razor`, `TabService` | The base for Level 2. |

## Level 1: the design

```text
caller ──▶ huddle agent <verb> … (JSON out, exit 0 done / 1 failed or refused / 2 usage)
             │  key from HUDDLE_AGENT_KEY or --key-file; finds the running copy; checks the version
             ▼
running Huddle /_agent/*  ──  key → agentId + scopes  ──▶  the same services the UI calls
```

| CLI verb | Route (key required) | Goes through |
| --- | --- | --- |
| `version` (run before every call) | `GET /_agent/version` | The app's informational version. A mismatch fails the call. |
| `chat post` | `POST /rooms/{id}/messages` | `ChatService.PostAsync`, as `PostMessageTool` does |
| `chat read` | `GET /rooms/{id}/messages?after=…` | The transcript tail |
| `chat watch` | `GET /rooms/{id}/events` (Server-Sent Events) | `RoomEvents`: posts, deltas, tool activity |
| `task`, `room`, `memory`, `jobs` … | `POST /tools/{name}` | The existing `IAppTool`, bound to the key's agent |
| `tools list`, `call <tool> --json` | `GET /tools`, `POST /tools/{name}` | Lists and runs any tool, so a new tool needs no change to the CLI |
| `settings get\|set` | `GET`/`PUT /settings/{area}` | The store the Settings page uses, never a raw write |
| `ui show <target>` | `POST /ui/show` | Level 2 |

**Keys:**

- The Human creates the agent in Settings or with `huddle agents add <Name> --scope …`. The key
  is shown once and only its hash is stored, as `DeviceRegistry` does.
- `rotate`, `revoke` and `remove` work as they do for devices.
- The scopes keep chat, work, settings and `ui` apart. An agent that can chat cannot change a
  Model unless the Human gives it that scope.
- Like `auth-token --show`, the CLI never prints a key to redirected output.

**Where the code goes:**

- The verbs live in the `huddle` exe, next to the existing ones.
- The CLI and the endpoints share the same `internal` records, so they cannot drift apart (D5,
  D6).
- Routes and response shapes are pinned by contract tests in `tests/Huddle.Tests.*`.

## Level 2: the design

### How a backend call reaches the open screen

Huddle is Blazor Server: each browser tab is a circuit on the server. A service call raises an
event on a singleton (`RoomEvents`, `AppearanceStore.AppearanceChanged`, `ITeamCatalog.Changed`,
and so on). Every component subscribed to it repaints through
`RendererDispatch.RunAsync(this.InvokeAsync, …)`, and SignalR sends the change to the browser.
`Chat.razor`'s `OnInitialized` is the model to copy. Because of D3, every agent write already
updates the screens that subscribe to it.

### Showing where the agent works

```text
/_agent write or ui show ─▶ AgentActivity { agent, action, target, changed } (singleton event)
                               ▼  in every open browser
                     AgentPresence.razor in MainLayout (renders nothing, like TabSync)
                        ├─ Follow off (default) → snackbar "Claude changed Ada's Model to Opus [Show]"
                        └─ Follow on → open the target in a Tab → ServerNavigator → page
                               ▼
                     page repaints from its store → the section is briefly highlighted
```

- **Every write raises `AgentActivity` on its own.** `ui show` is only for drawing the Human's
  attention to something.
- **Targets have names, not URLs.** A target is `room:<id>`, `task:<id>`, `team:<name>`,
  `project:<team>/<project>` or `settings:<area>`. The server maps it to a route, and an unknown
  target fails the call.
- **The Human is never moved without asking.** Follow is off by default, and turning it on lasts
  only for that browser session. Under
  [ADR 0045](../adr/0045-a-tab-is-a-remembered-url-and-only-the-active-one-is-mounted.md) the
  target opens in its own Tab, so the Human's current Tab keeps its place and any half-written
  text.
- **Presence:** while the agent has been active recently, its avatar chip shows on the page and
  the Tab it touched. Start from `Presence/PresenceCue` and `PresenceGlyph`.
- **The highlight is on the section, not the field.** Anchors on every input would break with
  each MudBlazor layout change.

### Work this needs on the pages

- **Live refresh.** Every settings area exposed to agents needs its store to raise `Changed`
  and its page to subscribe. Today, pages backed by `team.db` re-read only after
  `PersonasChanged` or a reload. Expose an area only once its page refreshes live. This also fixes
  the same stale screen when a second browser makes the change.
- **Unsaved form edits.** If the Human has unsaved edits, the page must not silently re-read
  over them. It shows "Claude changed this. [Reload] [Keep mine]" instead.

### Out of scope

- Playwright or any other browser automation (D8).
- Pushing to closed browsers: a skipped navigation is not replayed later.
- Sending the Human's browser to another device.

Agents that want to click through the UI already have their own browser tools, such as Playwright
MCP or Claude in Chrome, which can open `localhost:5100`. Huddle does not need to ship or maintain
one.

## Rough size

| Slice | Size |
| --- | --- |
| Agent keys: store, Settings UI, `huddle agents add/rotate/revoke` | Medium |
| `/_agent` routes, key filter, version check | Medium |
| `huddle agent` verbs, `tools list/call` | Small to medium |
| Settings routes, one area at a time, each with its `Changed` event | Small per area |
| `AgentActivity`, `AgentPresence`, Follow, presence chip, highlight | Small to medium |

## Open questions

- **How a settings change is credited.** The Feed and history should probably say "Claude
  changed …". Decide where that is shown, in line with
  [ADR 0065](../adr/0065-huddle-posts-as-itself.md).
- **Which settings areas come first.** Model and Effort are the likely first ones.
- **Remote access** beyond Strict local, after V1. It would reuse the gateway's scopes and rate
  limits.
