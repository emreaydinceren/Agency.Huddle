# Huddle.Adapters — More vendors: Codex, Antigravity and Copilot (HLD)

**Status:** designed 2026-10-19, not built; reviewed twice; one spike run in part · **Date:**
2026-10-19 · **Extends:** [Huddle.Adapters-Specifications.md](Huddle.Adapters-Specifications.md)
(delivered 2026-09-16) · **Amends, when accepted:**
[ADR-0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)

Huddle runs every Agent through one ACP client (`src/Huddle.Acp`) and picks *which* agent
process to launch from an **Adapter Profile** named in the Persona's `adapter:` frontmatter
([ADR-0013](adr/0013-an-adapter-is-a-property-of-the-persona.md)). Today the profiles that exist
are `claude` (`@agentclientprotocol/claude-agent-acp`), `agency` and `mock`. This document
specifies one Adapter for each of three more vendors, plus a secondary Google one. Each speaks
ACP over stdio:

| Adapter id | Vendor agent | How it speaks ACP | Distributed as | ACP registry id |
| --- | --- | --- | --- | --- |
| `codex` | OpenAI Codex | a TypeScript adapter, `codex-acp`, that drives `codex app-server` over JSON-RPC | npm `@agentclientprotocol/codex-acp` (was `@zed-industries/codex-acp`); the Codex engine arrives through its dependency `@openai/codex` | `codex-acp` |
| `antigravity` | Google Antigravity | Google's own ACP server, `agy_acp_server`, separate from the `agy` CLI | a native binary per platform, downloaded from `dl.google.com`; proprietary | `antigravity-acp` |
| `copilot` | GitHub Copilot CLI | natively: `copilot --acp` (stdio by default), **public preview** | npm `@github/copilot` | `github-copilot-cli` |
| `gemini` *(secondary)* | Google Gemini CLI | natively: `gemini --acp` | npm `@google/gemini-cli` | `gemini` |

**Scope of detail.** Codex and Antigravity are specified in full, because their sources or their
servers have been examined: Codex's adapter source was read (§2.4), and Antigravity's server
answered a handshake (§2.3). Copilot and Gemini keep proposals marked †. §11 lists every spike:
what has run, and what each remaining one must verify before its values are built.

**Why Antigravity, and why Gemini only as a secondary preset.** Google moved Gemini CLI's users
to Antigravity CLI (`agy`), available from 2026-05-19. From **2026-06-18** Gemini CLI stopped
serving requests for consumer accounts (free Code Assist, Google AI Pro and Ultra). It keeps
working with a paid Gemini API key or an enterprise Code Assist licence
([Google Developers Blog](https://developers.googleblog.com/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)).
The `agy` CLI itself has no `--acp` flag
([feature request, open](https://github.com/google-antigravity/antigravity-cli/issues/31)).
Google instead ships a separate ACP server, `agy_acp_server`, listed in the ACP registry by
Google LLC. Community wrappers that drive `agy` over ACP exist; Huddle does **not** use them,
because one warns that a personal Antigravity account used through them risks suspension
([antigravity-acp](https://github.com/shubzkothekar/antigravity-acp)).

The vocabulary is binding: these are **Adapters**, not providers
([D-5](Huddle.Adapters-Specifications.md#17-decision-log),
[language.md](engineering/language.md)). "Provider" collides with Agency's `IModelProvider` and
with Global Search's search providers.

This page belongs to the chat surface, but it asks for three small additive changes in
`src/Huddle.Acp` (§6.4, §6.6, §6.8), plus possibly the linked fake agent for phase 6. The ACP
effort owns that code. These are requests to that effort, announced rather than merged silently
(see [§9](#9-who-changes-what)).

---

## Table of Contents

1. [Goal](#1-goal)
2. [What we learned](#2-what-we-learned)
3. [Non-goals](#3-non-goals)
4. [Design principles](#4-design-principles)
5. [The gap: what is still Claude-shaped](#5-the-gap-what-is-still-claude-shaped)
6. [Design](#6-design)
7. [Per-vendor profiles](#7-per-vendor-profiles)
8. [Edge cases and failure modes](#8-edge-cases-and-failure-modes)
9. [Who changes what](#9-who-changes-what)
10. [Test-first task plan](#10-test-first-task-plan)
11. [Spikes: what has run and what must be verified](#11-spikes-what-has-run-and-what-must-be-verified)
12. [Open questions](#12-open-questions)
13. [Decision log](#13-decision-log)
14. [Review record](#14-review-record)

---

## 1. Goal

### 1.1 Primary goal

A Human can hire or edit an Agent, choose **Codex**, **Antigravity** or **Copilot** (or
**Gemini**, where configured) in the Agent card's Adapter select, and that Agent takes
part in Rooms exactly as a Claude Agent does. It reads its Persona, calls `get_help`,
`post_message`, `ask_human` and the other App Tools, honours its Work Mode, reports when it
fails, and is stopped by Stop.

### 1.2 Why this is mostly configuration

The 2026-09-16 Adapters delivery already did the hard part. One profile-aware
`DotAcpAgentHostFactory` launches whichever process a profile names; seven of its eight steps
are identical for every Adapter ([D-1](Huddle.Adapters-Specifications.md#17-decision-log)).
`ModelCatalogProbe` reads each Adapter's models, effort and modes from `session/new`, cached per
Adapter id. `PersonaRunner`, `ReplyGate`, `ChatService`, the App Tools and the pipe never learn
which Adapter answered.

What is left is a short list of places where Claude's **extensions around ACP** leaked in as if
they were ACP itself (§5). Each becomes a profile field with a per-vendor default (§6).

### 1.3 Concrete objectives

1. A stock installation with no `Adapters` configured behaves **byte for byte** as today. The
   golden `systemPrompt.txt` does not change.
2. Every profile configured today, none of which names a preset, resolves exactly as it does
   today (§6.1).
3. An operator adds a vendor by writing a profile with `"Preset": "codex"` (or `antigravity`,
   `copilot`, `gemini`) and nothing else, given the vendor's agent is installed and signed in.
4. An Agent on any new Adapter can see and call every App Tool by the name its system prompt
   gives it.
5. An Agent on any new Adapter receives its Persona text and tool guide, on a new session and
   on a resumed one, even when the Adapter ignores `_meta.systemPrompt`.
6. A Persona looping across Rooms still goes `Degraded` and stops taking Turns on an Adapter
   that sends no `usage_update` (the per-Room Budget already holds on every Adapter; §6.6).
7. Every permission request reaches Huddle's guards in a shape they can read, so the protected
   folders, the instruction file and the process gates hold on every Adapter (§6.8).
8. An Adapter that is not signed in fails with a message naming the vendor's own sign-in step.
9. Hiring can place two roles on different vendors. `ModelSpec.DifferentVendorFrom` already
   asks for this, though `TierModelPicker` compares Adapter ids, not vendors, and counts only
   Adapters whose probe returned models.

---

## 2. What we learned

### 2.1 block/buzz

[block/buzz](https://github.com/block/buzz) (studied at commit `6e6173ac`, 2026-10-19) is a
Nostr-based workspace whose `buzz-acp` crate drives coding agents over ACP, much as
`Huddle.Acp` does. On `main` it supports Goose, Claude, Codex and its own `buzz-agent`, plus
preset harnesses (Cursor, Devin, OpenCode, Amp, Kimi, Grok and others). It supports none of
Antigravity, Gemini CLI or Copilot on `main`; five Antigravity pull requests are open and
unmerged, and one of them (#7384) is relevant here (§2.3).

| What Buzz does | File in Buzz | What Huddle takes from it |
| --- | --- | --- |
| One generic `AcpClient`; no per-vendor class. Quirks branch on a normalised command name. | `crates/buzz-acp/src/acp.rs`, `config.rs` | Confirms [D-1](Huddle.Adapters-Specifications.md#17-decision-log). Huddle keys quirks on an explicit `Preset` field instead (D-V2). |
| A **catalog of known runtimes**, data not code: commands, the underlying CLI, install commands, an auth probe, a login hint. | `desktop/src-tauri/src/managed_agents/discovery/catalog.rs` | `AdapterPresets` (§6.1). |
| Discovery classifies a runtime as available, CLI missing, adapter missing or not installed, and probes `codex login status`. | `discovery.rs`, `readiness.rs` | V2 for Huddle (§6.9). |
| A **minimum `codex-acp` version, 1.10.0**, checked by parsing `codex-acp --version`; older adapters bundle a Codex that rejects newer models, and the old `@zed-industries` package counts as outdated. | `discovery.rs:785-908` | Huddle pins 2.2.2 and records the floor (§7.2). |
| For Codex, **no `_meta.systemPrompt`**: at protocol v1 the standing instructions are framed into the **first user message** of each session and re-sent on every new one. | `pool.rs:316-347`, `pool/system_prompt_tests.rs:18-22` | Confirms that Codex ignores `_meta.systemPrompt` (§2.4) and that `FirstPrompt` is a working fallback. |
| `CODEX_CONFIG={"sandbox_workspace_write":{"network_access":true}}`, deep-merged with any persona and parent value, so tool subprocesses reach the network. | `config.rs:845-876`, `acp.rs:255-371` | **No longer effective** in current `codex-acp`: its workspace modes force network off on every turn (§2.4). Huddle does not copy it. |
| Codex usage from the `session/prompt` response's `usage`, including `total_tokens`. | `usage.rs:250-270, 332-333` | Codex also sends `usage_update` as context occupancy (§2.4), which is what Huddle reads. |
| Model and effort set after `session/new` with `set_config_option`, effort through the `thought_level` category. | `pool.rs:1953-1984, 2046-2080` | Same as Huddle today. |
| Permission requests answered by option **`kind`**, never a hard-coded option id; a mode applied only when advertised. | `acp.rs:2014-2069`, `pool.rs:2157-2181` | Huddle already does the first; §6.5 adopts the second. Codex has two `reject_once` options (§2.4, §6.8). |
| Cancel answers any pending permission request with `cancelled`, then sends `session/cancel`. | `acp.rs:923-1040` | Codex's adapter aborts pending requests itself on cancel (§2.4); nothing to add. |
| No `CODEX_HOME` isolation: every agent shares `~/.codex`. | `isolated_execution.rs`, `setup_mode.rs` | Same as Huddle V1 (§6.7). |
| Scripted fake agents in tests, ignored tests against real adapters, and a contract test that nothing but frames reaches stdout. | `crates/buzz-acp/TESTING.md` | Huddle's `mock-acp` plays that role and gains kind emulation (phase 6). |

### 2.2 The ACP registry

The ACP project publishes a curated registry of agents at
`https://cdn.agentclientprotocol.com/registry/v1/latest/registry.json`
([about the registry](https://agentclientprotocol.com/get-started/registry)). Each entry gives an
id, a version, a licence and a **distribution**: an `npx` package with its args, or a binary
archive and command per platform. On 2026-10-19 it listed:

| Registry id | Version | Distribution |
| --- | --- | --- |
| `codex-acp` | 2.2.2 | `npx @agentclientprotocol/codex-acp@2.2.2` |
| `antigravity-acp` | 1.4.1 | binary: `agy_acp_server.exe` (Windows x64 and arm64), `agy_acp_server.par` (macOS, Linux; Linux adds `--uid=`) |
| `github-copilot-cli` | 1.0.100 | `npx @github/copilot@1.0.100 --acp` |
| `gemini` | 0.64.0 | `npx @google/gemini-cli@0.64.0 --acp` |

Huddle uses it as the **source of launch details** (§6.1), not as runtime discovery: Huddle
never fetches it at startup.

### 2.3 Antigravity spike S-A1, 2026-10-19: the handshake

Google's `agy_acp_server` 1.4.1 for Windows x64 was downloaded from the registry's URL. The zip's
SHA-256 is `5f5b7d0328df17c128b2be338d4567b0d24e56ace8196e88794fec37d1372554`, and both
executables in it carry valid Authenticode signatures from Google LLC. A scratch probe drove it
the way Huddle would: ACP over stdio with no `fs` or `terminal` capabilities, one HTTP MCP server
named `team` with a bearer token, `_meta.systemPrompt` on `session/new`, and sentinel phrases in
`AGENTS.md` and `GEMINI.md` in the cwd. The machine had the `agy` CLI 1.2.12 installed and signed
in. Only `initialize` and `session/new` ran; the server was not signed in.

| Asked | Answer | What it means for this spec |
| --- | --- | --- |
| `initialize` at `protocolVersion: 1` | Accepted; answered `1` | Buzz [#7384](https://github.com/block/buzz/pull/7384)'s v2 problems (a required `info` field on `initialize`, `stopReason` arriving late) do not arise at v1. Whether Huddle sends 1 is Q-13. |
| `agentInfo` | `antigravity-acp`, "Google Antigravity", `1.4.1` | Matches the registry id. |
| `mcpCapabilities` | `http: true`, `sse: true` | Huddle's HTTP App Tool server is accepted. |
| `loadSession`, `sessionCapabilities` | `loadSession: true`; `resume` and `list` | `SessionPerRoom` can stay on. |
| `promptCapabilities` | `image`, `audio`, `embeddedContext` | Prompt blocks can be on. |
| `agentCapabilities._meta` | `antigravity.systemPrompt.append: true` | The server advertises a system-prompt append of its own. If `session/new` honours `_meta.systemPrompt` as `{ append }`, Antigravity takes `MetaAppend` and needs **no instruction file**. |
| `authMethods` | `oauth-personal`, `oauth-business`, `gemini-api-key`, `agent-platform` | The ACP server has its own sign-in choice. |
| `session/new`, signed out | `-32000` "Authentication required", with `data` naming the methods and the settings file | The signal Huddle already handles. The `agy` CLI's sign-in is **not** picked up automatically. |
| The server's own log | Home is `~/.gemini` unless `$GEMINI_HOME` is set; settings are `~/.gemini/antigravity-acp/settings.json`, where `auth.type` picks the method | Where credentials live, and how the Human picks a method without `authenticate`. |

Everything that needs a session is still open: spike S-A2 (§11.2).

### 2.4 Reading `codex-acp` 2.2.2

`agentclientprotocol/codex-acp` was read at commit `a62b568` (2026-10-19, package 2.2.2), and the
Codex tool-naming code in `openai/codex` `main`. Nothing was run. Codex is **not installed** on
the development machine, so none of this has been observed on the wire: spikes S-C1 and S-C2
(§11.3) verify it. "[source]" marks what was read; "[inferred]" what was not.

| Topic | What the source says | Consequence |
| --- | --- | --- |
| Packaging | `bin: codex-acp → dist/index.js`, one ESM bundle; the engine is the dependency `@openai/codex ^0.160.1`, launched as `node …/@openai/codex/bin/codex.js app-server` unless `CODEX_PATH` is set [source]. `@openai/codex` ships native `codex.exe` through platform packages such as `@openai/codex-win32-x64` [inferred from npm]. CI uses Node 24. A missing VC++ redistributable surfaces as exit code 3221225781 [source]. | Launch as `node <tools/acp>/node_modules/@agentclientprotocol/codex-acp/dist/index.js`: the existing `AdapterLocator` pattern, no `.cmd` shim, nothing else to install. Do **not** set `CODEX_PATH` (open issue #602: a path with a space fails). |
| `initialize` (v1) | `agentInfo` `@agentclientprotocol/codex-acp`; `loadSession`; `promptCapabilities` image and embedded context; `mcpCapabilities` `http: true`, `sse: false`; `sessionCapabilities` resume, list, close, delete, fork; top-level `_meta.steering.supported: true` [source]. | HTTP App Tools accepted; `SessionPerRoom` on; `session/close` supported. |
| System prompt | **`_meta.systemPrompt` is ignored.** The only instructions key is `_meta.jetbrains.air.customInstructions`, mapped to Codex `developerInstructions` on `session/new` only, **not on resume** [source]. Codex reads `AGENTS.md` from the cwd and the repository root [inferred]. Config passed in `CODEX_CONFIG` reaches both `thread/start` and `thread/resume` [inferred]. | §6.3: `InstructionsFile` (`AGENTS.md`) for Codex; a config-based delivery is a candidate to verify (S-C2). |
| MCP servers | stdio maps to `{command, args, env}`; http maps to `{url, http_headers}`; `sse` is rejected. **A requested server whose name already exists in the user's Codex config is skipped silently**, unless `DISABLE_MCP_CONFIG_FILTERING=true` [source]. | The preset sets `DISABLE_MCP_CONFIG_FILTERING=true`, or the `team` server can vanish on a machine whose `~/.codex/config.toml` has a `team` entry. |
| Tool names | Codex names an MCP tool `mcp__{server}__{tool}`; a no-prefix feature exists, under development and off by default [source, `openai/codex` main]. | `ToolNameFormat` is **Claude's**: `mcp__team__get_help`. |
| Modes (`mode` option) | `read-only`, `workspace-write`, `agent` ("Auto review": Codex's own reviewer asks only on actions it judges unsafe), `agent-full-access` (approval `never`, sandbox `danger-full-access`). Default `agent`, overridable with the `INITIAL_AGENT_MODE` env [source]. | Hide `agent-full-access`. Whether `agent` lets requests bypass Huddle's guards is Q-17. |
| Plan | A **separate** option, `collaboration_mode`, values `default` and `plan` [source]; its category was not established. | Plan is not a `mode` value for Codex (§6.5, Q-15). |
| Model and effort | `model` from Codex `model/list`; `reasoning_effort` with category `thought_level`; `fast-mode` with category `model_config`. Unknown ids or values give `invalidParams`; a change applies from the next Turn [source]. | Huddle's existing Model and Effort readers apply unchanged. |
| Network | Every Turn passes a sandbox policy from the mode; **workspace modes force `networkAccess: false`**, overriding `CODEX_CONFIG` and `config.toml`. Only `agent-full-access` has network. Open issues #406, #310, #477 and PRs #608, #368 track it [source]. | App Tool calls run inside the Codex process, not the sandboxed shell, so they should work [inferred]. An Agent's shell commands that need the network (`git push`, `dotnet restore`) fail in every mode Huddle offers (§8, E-V3). |
| Permission: execute | `toolCall {kind: "execute", rawInput: {command: <string>, cwd, …}, locations}`. The command is a **string** [source]. | Huddle's `command` argument name already matches. |
| Permission: edit | `toolCall {kind: "edit", locations: [changed paths]}` with **no `rawInput`** [source]. | The paths are only in `locations`, which Huddle's `ToolCallInfo` does not carry today (§6.8). |
| Permission options | `allow_once`, `allow_for_session` (allow_always), `accept_execpolicy_amendment` (allow_always), network-policy amendments, `decline` (reject_once, **continues** the Turn), `cancel` (reject_once, **ends** the Turn); listed allow first, then rejects [source]. | Two options share `reject_once`; Huddle takes the first (§6.8, Q-18). |
| Usage | `usage_update {used: last request's total tokens, size: context window}`, no cost; the prompt response also carries `usage` with total, input, cached, output and thought tokens [source]. | `UsageSource: UsageUpdate`, the same occupancy meaning as Claude. |
| Turns | `stopReason` is only `end_turn` or `cancelled`; failures are JSON-RPC errors. Cancel aborts pending permission requests, then interrupts. Resume does not replay history [source]. | Matches Huddle's handling. |
| Sign-in | `authMethods`: `api-key` always; `chat-gpt` (opens a browser) unless `NO_BROWSER` is set. Signed out, `session/new` throws `authRequired` [source], code -32000 [inferred]. The env `DEFAULT_AUTH_REQUEST={"methodId":"api-key"}` signs in automatically from `CODEX_API_KEY` or `OPENAI_API_KEY` [source]. Credentials live in Codex's home, `$CODEX_HOME` (default `~/.codex`) [inferred]. | §6.8. |
| Commands | `/plan`, `/mcp`, `/skills`, `/status`, `/review`, `/review-branch`, `/review-commit`, `/compact`, `/goal`, `/rename`, `/logout` [source]. | `Commands: ["compact"]`. |
| Unknown methods | Buzz says `codex-acp` answers unknown extension methods with `{}`. In 2.2.2 that branch is unreachable from the wire; a truly unknown method likely gets -32601 [source, inferred]. | P12 stands either way. |

---

## 3. Non-goals

- **No non-ACP path.** A vendor without an ACP mode is out of scope. All four agents here ship
  ACP.
- **No community wrappers.** Where a vendor ships its own ACP server or adapter, Huddle uses
  that one only.
- **No borrowing another client's extension.** Codex's `_meta.jetbrains.air.customInstructions`
  is a JetBrains AIR key; Huddle does not impersonate AIR to reach it (D-V12).
- **No API-key management in Huddle.** Each vendor keeps its own sign-in. Huddle never stores,
  prompts for or forwards a vendor credential (§6.8).
- **No `authenticate` flow in V1.** Huddle tells the Human which step to take (§6.8).
- **No in-app installer.** `tools/acp/install.ps1` installs the pinned packages and binaries.
- **No runtime use of the ACP registry.**
- **No per-vendor `IAgentHostFactory`.** [D-1](Huddle.Adapters-Specifications.md#17-decision-log)
  stands.
- **No new Persona store.** ([ADR-0013](adr/0013-an-adapter-is-a-property-of-the-persona.md)).
- **No `Team:` config root rename and no rename of the `team` MCP server.** `CLAUDE.md` pins both.
- **No fix for Codex's sandbox network.** Shell commands that need the network fail on Codex
  until upstream honours `network_access` (§2.4); Huddle documents it and does not work around
  it by offering full access.

---

## 4. Design principles

The eight principles of the [Adapters spec §4](Huddle.Adapters-Specifications.md#4-design-principles)
still hold. Five more apply to this work:

| # | Principle |
| --- | --- |
| P9 | **A vendor difference is a profile field, not a branch on the id.** Code reads `profile.SystemPromptDelivery`, never `profile.Id == "codex"`. |
| P10 | **A preset is only defaults.** Every field a preset sets can be overridden in `appsettings.json`. |
| P11 | **An ACP-only path always exists.** A profile gets it by saying `"Preset": "None"`. A profile that names no preset keeps today's behaviour (§6.1). |
| P12 | **Capabilities are read, not tried.** Anything an Adapter supports is learnt from `initialize` or `session/new`. Huddle never calls a method to find out whether it exists. |
| P13 | **A Claude default stays a Claude default.** Moving a value from a global constant to a profile field must leave today's profiles' values unchanged, and a golden or test must prove it. |

---

## 5. The gap: what is still Claude-shaped

| # | Assumption | Where | What another Adapter does | Fix |
| --- | --- | --- | --- | --- |
| G-1 | The model-facing tool prefix is either `mcp__team__` or nothing (`UsesToolNamePrefix` is a `bool`). | `DotAcpAgentHostFactory.cs:215`, ADR-0014 | Codex uses `mcp__{server}__{tool}`, Claude's form (§2.4). Gemini CLI documents `mcp_{serverName}_{toolName}` with single underscores ([geminicli.com](https://geminicli.com/docs/tools/mcp-server/)). Antigravity and Copilot are unverified. | §6.2 |
| G-2 | `ReferenceExpander` hard-codes `"mcp__team__"`. | `ReferenceExpander.cs:16` | Same as G-1. | §6.2 |
| G-3 | The Persona text and tool guide arrive in `_meta.systemPrompt`, a `claude-agent-acp` extension. | `DotAcpAgentHost.BuildMeta` (`Huddle.Acp`, :275-299) | Codex ignores it (§2.4). Antigravity advertises an append of its own (§2.3). An Adapter that ignores it silently runs **with no Persona and no tool names**. | §6.3 |
| G-4 | `_meta.claudeCode.options` isolates Claude's user settings and auto-memory. | `DotAcpAgentHostFactory.cs:480-498` | Ignored elsewhere. | §6.7 |
| G-5 | Document-history logging installs a Claude Code **Stop hook** in `<workDir>/.claude/settings.local.json`. | `DocHistoryStopHook.cs:55` | No other vendor reads that file. | §6.7 |
| G-6 | Only `~/.claude` is a protected directory. | `WorkDirPermissionHandler.cs:151-155` | Codex keeps credentials in `~/.codex`; Antigravity in `~/.gemini`. | §6.8 |
| G-7 | `HiddenModes` is install-wide, and Plan is matched as the `mode` value `"plan"`. | `WorkModePolicy.cs:23`, `PlanModePermissionHandler.cs:33` | Codex's mode ids differ, and its Plan is a separate `collaboration_mode` option (§2.4). | §6.5 |
| G-8 | The per-Persona token Budget sums `usage_update.used`. | `AcpOptions.cs:56-71`, `RoomSession.cs:1790-1795` | Codex sends `usage_update` (§2.4); others are unverified. One that sends none leaves the Budget inert. | §6.6 |
| G-9 | `/compact`'s outcome is parsed from Claude's before/after token figures. | `AdapterCommandOutcome.cs:40-60` | Outputs differ per vendor. | §6.10 |
| G-10 | `ask_human` parses AskUserQuestion shapes; elicitation reads `question_N` fields. | `AskHumanTool.cs:309`, `ElicitationSchemaReader.cs:381,479` | Others do not send AskUserQuestion. | none needed (E-V9) |
| G-11 | `AdapterLocator` finds only `claude-agent-acp`'s `dist/index.js`. | `AdapterLocator.cs:10-11` | Codex has its own `dist/index.js`; Antigravity is a native binary. | §6.1 |
| G-12 | No `authenticate`; `-32000` at `session/new` means "not signed in", and the message names Claude. | `DotAcpAgentHost` (:143-146, :194-197) | Codex and Antigravity raise the same error (§2.3, §2.4). | §6.8 |
| G-13 | The model probe's cwd is reasoned from `CLAUDE.md` auto-loading. | `ModelCatalogProbe.cs:275-281` | Codex reads `AGENTS.md` from the cwd and the repository root. The Work Dir root stays correct for the Work Dirs below it; ancestor files are another matter (§6.3). | none needed |
| G-14 | The permission handler reads Claude's tool-input shapes: paths from `file_path`, `path` or `notebook_path` in `rawInput`; commands from `command` or `cmd`. A request's `locations` never reach the App: `ToolCallInfo` carries only `RawInputJson`. | `WorkDirPermissionHandler.cs:68,148`; `PermissionRequestContext.cs` | Codex's **edit** requests carry their paths **only** in `locations`, with no `rawInput` (§2.4). Gemini's file tools take `absolute_path` / `dir_path`. Then the protected-folder check, the instruction-file guard and the `git merge` / `git push` process gate ([ADR-0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)) **silently never fire** for those edits. | §6.8 |
| G-15 | When refusing, the handlers take the **first** `reject_once` option. | `WorkDirPermissionHandler.cs:277`, `PlanModePermissionHandler.cs:55` | Codex offers two: `decline` continues the Turn, `cancel` ends it (§2.4). | §6.8 |

What is **already** neutral and stays untouched: the ACP client, `session/new` /
`session/resume` / `session/cancel` / `session/close`, `configOptions` parsing for Model and
ThoughtLevel, permission answers by option kind, `PromptBlocks` per profile, `SessionPerRoom`
per profile, `Commands` per profile, the HTTP App Tool server, the card's cascade,
`Team:Teams:Tiers:<adapterId>`, and `TouchedPaths.From`.

---

## 6. Design

### 6.1 Presets, and where launch details come from

```csharp
namespace Agency.Huddle.App.Acp;

/// <summary>The vendor whose defaults a profile starts from.</summary>
public enum AdapterPreset
{
    /// <summary>The ACP-only path (P11): no vendor extension is assumed.</summary>
    None,
    Claude,
    Codex,
    Antigravity,
    Copilot,
    Gemini,
}
```

`AdapterProfileOptions` gains a nullable `Preset`. `AdapterCatalog` builds each profile once, at
startup, so `AdapterProfile` stays a flat record with no preset logic.

| `Preset` | Resolves to |
| --- | --- |
| absent | **Exactly what `AdapterCatalog` builds today**, field for field, with no preset consulted. Every new field takes the *legacy value* that reproduces today's behaviour. This is how `claude`, `agency` and `mock` are configured today. |
| `"None"` | The ACP-only defaults: `FirstPrompt`, `{tool}`, not located, `UsageUpdate`, generic guards. |
| a vendor | That vendor's defaults (§7.2), then each configured field overrides its default. |

**Legacy values** for a profile with no preset:

| New field | Legacy value | Why it reproduces today |
| --- | --- | --- |
| `ToolNameFormat` | from `UsesToolNamePrefix`: `true` → `mcp__{server}__{tool}`, `false` → `{tool}` | the alias of §6.2 |
| `NodePackageEntry` | `claude-agent-acp`'s `dist/index.js` when `UsesToolNamePrefix` is `true`, else none | today's `AdapterLocator` gate |
| `SystemPromptDelivery` | `MetaReplace` | what every profile sends today |
| `HiddenModes` | `Team:Acp:HiddenModes` | today's install-wide list |
| `PlanMode` | `mode=plan` | today's literal |
| `UsageSource` | `UsageUpdate` | today's path |
| `InstallsClaudeHooks` | today's behaviour, unchanged | — |
| `ProtectedHomeFolders` | `~/.claude` | today's single folder |
| `CommandArgumentNames` | `["command", "cmd"]` | today's list |
| `PreferredRejectOptionIds` | none | today: the first `reject_once` |

`IsolateUserSettings`, `Commands` and the other existing fields keep their **existing**
`AdapterProfileOptions` defaults for a presetless profile (`false`, none). The profile
`AdapterCatalog` synthesises when no `Adapters` are configured (`AdapterCatalog.cs:62-74`) carries
`IsolateUserSettings: true` and `Commands: ["compact"]`, and so does the `Claude` preset. Writing
`"Preset": "claude"` on an existing configured entry therefore **turns on** isolation and
`/compact`: a small, intended change, noted in the Engineering Guide.

**Launch on Windows.** A `.cmd` shim cannot be started with `UseShellExecute = false`
(acp-agent-guide §3). A preset launches in one of two ways:

- **npm-distributed** (`claude`, `codex`, `copilot`, `gemini`): a **Node package entry** resolved
  under `tools/acp/node_modules`, the way `AdapterLocator` finds `claude-agent-acp` today. For
  Codex it is `@agentclientprotocol/codex-acp/dist/index.js` (§2.4).
- **binary-distributed** (`antigravity`): a native executable that `install.ps1` unpacks under
  `tools/acp/bin/<registry id>/<version>/`.

```csharp
// AdapterProfile gains:
string? NodePackageEntry   // "@agentclientprotocol/codex-acp/dist/index.js"
string? BinaryEntry        // "antigravity-acp/1.4.1/agy_acp_server.exe"
```

The precedence in `AgentProcessOptionsFactory.TryCreate` becomes: `Args` → `AdapterPath` →
`BinaryEntry` located under `tools/acp/bin` → `NodePackageEntry` located under
`tools/acp/node_modules` → `null`. The `UsesToolNamePrefix`-gates-`AdapterLocator` coupling
(ADR-0014, Consequences) is replaced by "a profile with an entry is located; a profile without
one never is"; the legacy values reproduce today's gate exactly.

**Where launch details come from.** Each preset records its **ACP registry id** (§2.2). The
pinned versions live in one file, `tools/acp/agents.json`: registry id → version, a minimum
version where one is known (`codex-acp` ≥ 1.10.0, §2.1), and for a binary the per-platform
archive URL, command, args and the SHA-256 recorded when the pin was taken. `install.ps1` reads
it: `npm install` for an `npx` distribution, download-verify-unpack for a binary. Bumping a
vendor is one reviewed edit to that file. Huddle never contacts the registry at run time.

### 6.2 The tool name is a format, not a flag

**Amends [ADR-0014](adr/0014-the-tool-name-prefix-belongs-to-the-adapter.md)**, which foresaw
this: *"the flag is a `bool` today and would become a small enum or a format string."*

```csharp
// AdapterProfile: replaces bool UsesToolNamePrefix
string ToolNameFormat   // "mcp__{server}__{tool}" | "{tool}" | "mcp_{server}_{tool}" | ...
```

- `{server}` is always `ToolServerName` (`"team"`); `{tool}` is the App Tool's own name.
- `UsesToolNamePrefix` stays **bindable** as a legacy alias: `true` means
  `"mcp__{server}__{tool}"` and `false` means `"{tool}"`. Setting both is a startup error.
- A single `ToolNamer` replaces the string the factory passes to `GetHelpTool` and
  `SystemPromptComposer.Compose`, and **`ReferenceExpander` takes it too** (G-2).
- Validation at startup: the format contains `{tool}` exactly once.

**Goldens.** `PromptGoldenTests` gains one golden per *distinct* format a preset uses. The
prefixed golden `systemPrompt.txt` must not change by a byte (P13). Codex uses the prefixed
format, so it needs no new golden if S-C2 confirms §2.4.

### 6.3 Delivering the system prompt

**The gap that matters most** (G-3). The prompt is fixed at `session/new`
([traps.md](engineering/traps.md)), and today it rides there in `_meta.systemPrompt`. An Adapter
that ignores `_meta` answers as a blank agent that has never heard of `post_message`.

```csharp
public enum SystemPromptDelivery
{
    /// <summary>_meta.systemPrompt as a string: replaces the agent's own prompt (claude-agent-acp).</summary>
    MetaReplace,
    /// <summary>_meta.systemPrompt as { append }: added to the agent's own prompt.</summary>
    MetaAppend,
    /// <summary>Written to the instruction file the agent reads from its cwd, before session/new.</summary>
    InstructionsFile,
    /// <summary>Sent as a framed text block ahead of the first prompt of each session.</summary>
    FirstPrompt,
}
```

| Strategy | Used by | How it works | Cost |
| --- | --- | --- | --- |
| `MetaReplace` | `claude`, and every presetless profile | unchanged | none |
| `MetaAppend` | `antigravity` † (advertised, §2.3) | the other branch of `BuildMeta` | none |
| `InstructionsFile` | `codex` → `AGENTS.md`; `copilot` → `AGENTS.md` †; `gemini` → `GEMINI.md` † | Huddle writes the composed prompt to `<workDir>/<InstructionsFileName>` before **every** `session/new` and `session/resume`, marked as generated. See *Who else can touch the file*. | The prompt becomes a file the agent may weigh as project guidance rather than identity. |
| `FirstPrompt` | the ACP-only path: `"Preset": "None"` | The first Turn of a session gets a leading text block framed as standing instructions; a resumed session does not resend it. Buzz uses this for Codex (§2.1). | Spends prompt tokens once per session; can drop out of context on compaction, and is lost on resume. |

**No change in `Huddle.Acp`.** `BuildMeta` already omits `systemPrompt` when
`AgentSessionOptions.SystemPrompt` is `null`, and `SystemPromptMode.Append` already exists.

**Why the file for Codex, and not the first prompt or `customInstructions`.** Codex ignores
`_meta.systemPrompt`. Its one instructions key, `_meta.jetbrains.air.customInstructions`, belongs
to another client and is dropped on resume (§2.4, D-V12). `FirstPrompt` is also lost on resume,
and Huddle resumes sessions routinely (`SessionPerRoom`). `AGENTS.md` is what Codex reads from
its cwd on every thread, new or resumed [inferred, S-C2 verifies].

**A candidate to verify for Codex: instructions through `CODEX_CONFIG`.** `codex-acp` passes
`CODEX_CONFIG` into both `thread/start` and `thread/resume` [inferred]. If Codex's
`developer_instructions` config key, set there, reaches the model on both, Huddle could deliver
the prompt per Persona process with **no file to guard**. Every Persona has its own process, so
its environment can carry its own prompt. If S-C2 confirms it, a fifth strategy is added:

```csharp
/// <summary>Merged as one key into a JSON configuration environment variable at launch.</summary>
EnvironmentJson,   // profile fields: SystemPromptEnvVar ("CODEX_CONFIG"), SystemPromptJsonKey ("developer_instructions")
```

It is named generically (P9), merges into any configured value of the same variable, and changes
the launch environment, so a Persona edit restarts the process, as it already does.

**Where `FirstPrompt` is composed.** In `RoomSession`, beside the first-Turn composition
(`firstTurnPending`, `RoomSession.cs:1005`), which already knows whether the open was a resume
(`resumedOnLastOpen`) and already skips a `WorkItemKind.Command` Turn.

**Profile fields:** `SystemPromptDelivery`, `InstructionsFileName` (a bare file name, validated
at startup) and, if added, `SystemPromptEnvVar` and `SystemPromptJsonKey`.

**Who else can touch the file.** The Work Dir is `Agents/<Name>/work/`
([ADR-0031](adr/0031-agents-and-teams-are-sibling-folders.md)), inside the `Agents/` Library
root ([ADR-0027](adr/0027-the-library-sees-only-configured-roots.md)), and it is the Agent's cwd.
The Human could edit `AGENTS.md` in the Library; the Agent could rewrite its own Persona, a
prompt-injection surface that does not exist with `_meta`; and a watched Work Dir would report
Huddle's own rewrite ([ADR-0023](adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)).
So the file is treated as **Huddle's output, derived from the Persona**:

1. **Regenerated, not trusted.** Before each `session/new` and `session/resume`, Huddle compares
   the file with the composed text, rewrites it on a difference and logs a warning. The file's
   first lines say it is generated from the Persona, with the Agent's name. This is the
   guarantee.
2. **One writer at a time.** The compare-and-write runs under one per-Persona gate, as
   `DocHistoryStopHook`'s `ScriptGate` does, and writes through `AtomicFile`.
3. **Denied to the Agent.** `WorkDirPermissionHandler` refuses a write to the file. This fires
   only when the Agent asks in a shape Huddle can read (§6.8) and never in a mode that skips
   asking; point 1 still holds.
4. **Not a file change.** `FileChangesOptions.EffectiveIgnore` names folders, not files, so a new
   narrow rule in the change scanner skips `<workDir>/<InstructionsFileName>` for a Persona whose
   profile uses `InstructionsFile`.

The Library shows the file like any other; no read-only mode is added, which would carve an
exception into [ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md).

**Ancestor files.** Codex reads `AGENTS.md` from the git root down to the cwd; Gemini scans
ancestors for `GEMINI.md` ([geminicli.com](https://geminicli.com/docs/cli/gemini-md/)). A
`DataDir` inside a git repository hands every Agent that repository's instruction files too,
and so does the probe's cwd. Startup warns when `DataDir` is inside a git working tree and any
profile uses `InstructionsFile`.

### 6.4 App Tools over HTTP

Huddle hands each session one `McpServerHttp` (`127.0.0.1`, a per-Persona port, a bearer token).
Codex and Antigravity both advertise `mcpCapabilities.http: true` (§2.3, §2.4).

- **V1.** `DotAcpAgentHost` records `mcpCapabilities.http` into `AgentHostInfo`. If an Adapter
  does **not** advertise HTTP, the Persona goes `Degraded` with *"<Adapter> does not accept HTTP
  tool servers; App Tools are unavailable."* This is request 1 to the ACP effort (§9).
- **V2, only if a vendor needs it.** A `huddle-mcp-bridge` relaying stdio to the `AppToolServer`
  URL, with the URL and token in the stdio server's `env`.

**Codex's name filter.** Codex skips a requested server whose name already exists in the user's
Codex config (§2.4). The `codex` preset sets `DISABLE_MCP_CONFIG_FILTERING=true` in
`EnvironmentOverrides`, and S-C1 verifies the `team` server survives a `config.toml` that
defines one.

### 6.5 Modes and Plan per profile

| Field | Was | Legacy value (P13) | Notes |
| --- | --- | --- | --- |
| `HiddenModes` | `Team:Acp:HiddenModes`, install-wide | `Team:Acp:HiddenModes` | A preset's list replaces it for that profile only. |
| `PlanMode` | the literal mode id `"plan"` | `mode=plan` | Written `<configId>=<value>`. `null` means no Plan; the Plan Work Mode is then not offered. Codex: `collaboration_mode=plan` (§2.4). |

`PlanModePermissionHandler` and the Work Mode mapping set and read Plan through the named config
option rather than assuming it is a `mode` value. Huddle's mode reader takes the **first** option
in the `mode` category (`ModeConfigOptions.Read`, via `SessionConfigSelects.Find`). If Codex's
`collaboration_mode` also has category `mode`, Huddle would read the wrong selector: Q-15, S-C1
records the categories.

Mode ids are **read from the Adapter, never invented** (P12). Any mode that skips approval for
everything is hidden by its preset, so Huddle's permission handler and process gates
([ADR-0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md))
stay in the loop.

**Codex.** Hidden: `agent-full-access` (approval `never`, full access). Codex's default, `agent`,
uses Codex's own reviewer, which asks only on actions *it* judges unsafe. If those decisions
never reach Huddle, the process gates do not see them (Q-17). Until S-C2 shows what `agent`
forwards, the preset starts sessions in `workspace-write`, where every request comes to Huddle,
by setting `INITIAL_AGENT_MODE=workspace-write`, and hides `agent` as well.

Never send a mode id that the current session did not advertise (Buzz `pool.rs:2157`).

### 6.6 Usage and the Budget

| Layer | Setting | Needs usage? | On an Adapter that reports none |
| --- | --- | --- | --- |
| Per Room | `Team:AgentMessageBudget` (40 Messages, resets on a Human Message) | no | works unchanged |
| Per Persona, across Rooms | `Team:Acp:TokenBudget` (sums the rises in `UsageUpdated.Used`) | **yes** | inert |

**A per-Persona Turn Budget, counted on every Adapter.** `AcpOptions` gains `TurnBudget`
(proposed default `60`; zero or less disables it):

- **Counted** in `RoomSession` when a Turn completes, through a new
  `IRoomSessionOwner.AddTurn()` that `PersonaRunner` implements beside its token counter.
- **Not counted** for a `WorkItemKind.Command` Turn ([traps.md](engineering/traps.md): *a
  compaction is not spend*).
- **Reset** by the same Human Message that resets `tokensConsumed`.
- **Gated** where `RoomSession` already consults `IRoomSessionOwner.TokenBudgetSpent`: a spent Turn
  Budget makes the Persona go `Degraded` and take no Turns. The text names which Budget tripped.

```csharp
public enum UsageSource
{
    /// <summary>usage_update notifications; Used is context occupancy (today).</summary>
    UsageUpdate,
    /// <summary>The usage object on the session/prompt response, when the wire carries one.</summary>
    PromptResponse,
}
```

- **`UsageUpdate`** is the legacy value and Codex's: `codex-acp` sends `usage_update` with `used`
  and `size` as context occupancy, the meaning Huddle already sums (§2.4). Codex reports no cost.
- **`PromptResponse`** needs `DotAcpAgentSession.PromptAsync` (`Huddle.Acp`, :212) to surface the
  response's `usage` as an optional field on `TurnCompleted`: request 2 to the ACP effort. No
  preset needs it yet; it stays for a vendor whose S-x2 shows no `usage_update`.

### 6.7 Claude-only side channels: isolation and the Stop hook

**Isolation (G-4).** `IsolateUserSettings` keeps meaning "build `_meta.claudeCode.options`" and is
honoured only for `MetaReplace` or `MetaAppend`. For other vendors, isolation would mean a
private home (`CODEX_HOME`, `GEMINI_HOME`). **V1 does not do this**: the vendor's sign-in lives in
that home. Buzz does not isolate either (§2.1). An operator who wants a private home sets it
through `EnvironmentOverrides` and signs that home in themselves.

**The document-history Stop hook (G-5).** `InstallsClaudeHooks` is `true` for the `claude`
preset, the legacy behaviour for a presetless profile, and `false` for every other preset, which
relies on the Agent's own `log_change` calls
([ADR-0060](adr/0060-a-documents-history-is-what-its-editors-report.md)).

### 6.8 Sign-in, protected folders and reading permission requests

**Sign-in (G-12).** Huddle never handles a vendor credential. Each preset carries a `LoginHint`
shown when `session/new` fails with `AuthenticationRequired`:

| Preset | `LoginHint` |
| --- | --- |
| `claude` | Run `claude` in a terminal and sign in, or set `ANTHROPIC_API_KEY`. |
| `codex` | Sign Codex in once: run `codex login` (or `node tools/acp/node_modules/@openai/codex/bin/codex.js login`), or set `OPENAI_API_KEY` and `DEFAULT_AUTH_REQUEST={"methodId":"api-key"}` for this Adapter. |
| `antigravity` | Set `auth.type` in `~/.gemini/antigravity-acp/settings.json`: `oauth-personal` for your Google account, `gemini-api-key` with `GEMINI_API_KEY`, or `agent-platform`. The `agy` CLI's own sign-in is not used automatically. |
| `copilot` | Run `copilot` in a terminal and use `/login`, or set a GitHub token the CLI accepts. † |
| `gemini` | Set a paid `GEMINI_API_KEY`, or sign in with an enterprise Code Assist licence. Consumer sign-in no longer works (2026-06-18). |

The App catches `AgentAuthenticationRequiredException` where the session is started and composes
the failure text from the profile; `Huddle.Acp` is unchanged. An empty `authMethods` does not
mean signed in; the error from `session/new` is the signal.

**No browser from an unattended Agent.** The `codex` preset sets `NO_BROWSER=1`, which removes
Codex's browser sign-in method, so an Agent starting at night never opens a sign-in page.
Antigravity's equivalent is Q-14.

**Protected folders (G-6).** `WorkDirPermissionHandler` protects a list: the union of every
configured profile's `ProtectedHomeFolders` (Codex `~/.codex` or `$CODEX_HOME`; Antigravity
`~/.gemini` or `$GEMINI_HOME`).

**Reading a permission request on any vendor (G-14).** Every guard depends on Huddle finding the
path or the command in the request:

- **Paths: `locations`, rooted strings anywhere, and today's named arguments.**
  - `ToolCallInfo` gains an optional `Locations` list, mapped from the ACP tool call's
    `locations[].path`. This is request 3 to the ACP effort: additive, defaulting to empty.
    Without it, every Codex edit bypasses the guards, because its paths appear nowhere else.
  - Every string in `rawInput`, at any depth, for which `Path.IsPathFullyQualified` is true, the
    rule `TouchedPaths.CollectPaths` already uses. A relative string is **not** resolved: today's
    `Path.GetFullPath` resolves against Huddle's process cwd, and applying it to every string
    would turn `"main"` or `"-m"` into a path.
  - The named arguments (`file_path`, `path`, `notebook_path`) keep today's handling, so P13
    holds.
- **Commands through a profile field**, `CommandArgumentNames`, legacy value
  `["command", "cmd"]`. An array of strings is joined with spaces. Codex sends a string under
  `command` (§2.4), which already matches.

**Choosing which refusal (G-15).** A profile may name `PreferredRejectOptionIds`, tried in order
before the first option of kind `reject_once`. The `codex` preset names `["decline"]`, which
refuses the action and lets the Turn continue, so the Agent hears why and can adapt, as with
Claude today. This names option ids, which P12's spirit discourages. It is the one place where
ACP's kinds cannot tell two options apart, and the fallback by kind keeps it safe if an id
disappears.

Until a vendor's request shapes are recorded by its spike, a profile with that preset logs a
startup warning that its file and process guards are unverified.

### 6.9 Readiness on the card (V2)

V1 keeps today's behaviour: a missing executable fails at the first Turn with a message naming
`install.ps1`. V2 adds an `AdapterReadiness` pass that resolves each profile's command and runs
an optional `ReadinessProbe` (`codex login status`), and checks a pinned minimum version, as Buzz
does (§2.1).

### 6.10 Adapter commands

`Commands` is a per-profile allowlist ([ADR-0035](adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md)).
Presets: `["compact"]` for `claude` and `codex` (§2.4), `["compress"]` † for `gemini`, none for
`antigravity` and `copilot` until their spikes show what they advertise. Configuration cannot
express an empty list ([rules.md](engineering/rules.md)), so "none" is the preset's default.
`AdapterCommandOutcome`'s token parsing stays Claude-only; other Adapters end with the generic
*"command finished"*.

### 6.11 The resulting profile

```csharp
/// <summary>One ACP agent this installation can launch, and how to launch and address it.</summary>
public sealed record AdapterProfile(
    string Id,
    string DisplayName,
    string? Description,
    AdapterPreset? Preset,                          // new; null = presetless, resolved as today (§6.1)
    string Command,
    IReadOnlyList<string>? Args,
    string? AdapterPath,
    string? NodePackageEntry,                       // new (§6.1)
    string? BinaryEntry,                            // new (§6.1)
    string ToolNameFormat,                          // replaces UsesToolNamePrefix (§6.2)
    SystemPromptDelivery SystemPromptDelivery,      // new (§6.3)
    string? InstructionsFileName,                   // new (§6.3)
    IReadOnlyList<string> HiddenModes,              // moved from AcpOptions (§6.5)
    string? PlanMode,                               // new (§6.5), "<configId>=<value>"
    UsageSource UsageSource,                        // new (§6.6); TurnBudget is on AcpOptions
    bool InstallsClaudeHooks,                       // new (§6.7)
    string? LoginHint,                              // new (§6.8)
    IReadOnlyList<string> ProtectedHomeFolders,     // new (§6.8)
    IReadOnlyList<string> CommandArgumentNames,     // new (§6.8)
    IReadOnlyList<string> PreferredRejectOptionIds, // new (§6.8)
    IReadOnlyDictionary<string, string>? EnvironmentOverrides,
    bool ReadsFiles,
    bool IsolateUserSettings,
    bool SessionPerRoom,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> PromptBlocks);
```

The binder shape `AdapterProfileOptions` keeps every collection **without an initialiser**
([rules.md](engineering/rules.md)).

---

## 7. Per-vendor profiles

### 7.1 Configuration

```jsonc
"Team": {
  "Acp": {
    "Enabled": true,
    "TurnBudget": 60,
    "Adapters": [
      { "Id": "claude",      "DisplayName": "Claude",      "Preset": "claude",
        "Description": "Anthropic, cloud, spends money per turn" },
      { "Id": "codex",       "DisplayName": "Codex",       "Preset": "codex",
        "Description": "OpenAI, cloud, uses your ChatGPT plan or API key" },
      { "Id": "antigravity", "DisplayName": "Antigravity", "Preset": "antigravity",
        "Description": "Google, cloud, uses your Google account or API key" },
      { "Id": "copilot",     "DisplayName": "Copilot",     "Preset": "copilot",
        "Description": "GitHub, cloud, uses your Copilot plan (preview)" }
    ]
  },
  "Teams": {
    "Tiers": {
      "codex":       { "Strongest": "<from S-C1>", "Balanced": "<from S-C1>", "Fast": "<from S-C1>" },
      "antigravity": { "Strongest": "<from S-A2>", "Balanced": "<from S-A2>", "Fast": "<from S-A2>" }
    }
  }
}
```

A Codex installation on an API key adds, to its `codex` profile,
`"EnvironmentOverrides": { "DEFAULT_AUTH_REQUEST": "{\"methodId\":\"api-key\"}" }` and sets
`OPENAI_API_KEY` in its own environment. It never writes the key into `appsettings.json`.

### 7.2 Preset defaults

**Mark key:**
- **(src)** was read in source but not yet seen on the wire.
- **(S-A1)** was observed on the wire.
- **†** is a proposal.

Nothing marked (src) or † is built before the spike named in §11 confirms it.

| Field | `claude` | `codex` | `antigravity` | `copilot` | `gemini` |
| --- | --- | --- | --- | --- | --- |
| Registry id and pin | Huddle's pin | `codex-acp` 2.2.2, minimum 1.10.0 | `antigravity-acp` 1.4.1 | `github-copilot-cli` 1.0.100 | `gemini` 0.64.0 |
| Launch | `node …/claude-agent-acp/dist/index.js` | `node …/@agentclientprotocol/codex-acp/dist/index.js` (src) | `BinaryEntry` `agy_acp_server.exe` (S-A1) | `node …/@github/copilot/<entry> --acp` † | `node …/@google/gemini-cli/<entry> --acp` † |
| `ToolNameFormat` | `mcp__{server}__{tool}` | `mcp__{server}__{tool}` (src) | † | † | `mcp_{server}_{tool}` † |
| `SystemPromptDelivery` | `MetaReplace` | `InstructionsFile` (src: `_meta` ignored); `EnvironmentJson` if S-C2 confirms | `MetaAppend` † (advertised, S-A1) | `InstructionsFile` † | `InstructionsFile` † |
| `InstructionsFileName` | — | `AGENTS.md` | none if `MetaAppend` holds | `AGENTS.md` † | `GEMINI.md` |
| `UsageSource` | `UsageUpdate` | `UsageUpdate` (src) | † | † | † |
| `HiddenModes` | `bypassPermissions`, `auto`, `plan` | `agent-full-access`, `agent` (src; `agent` until Q-17) | † | † | † |
| `PlanMode` | `mode=plan` | `collaboration_mode=plan` (src) | † | † | † |
| `Commands` | `compact` | `compact` (src) | none † | none † | `compress` † |
| `InstallsClaudeHooks` | true | false | false | false | false |
| `IsolateUserSettings` | true | false | false | false | false |
| `ProtectedHomeFolders` | `~/.claude` | `~/.codex`, or `$CODEX_HOME` | `~/.gemini`, or `$GEMINI_HOME` (S-A1) | `~/.copilot` † | `~/.gemini` |
| `PreferredRejectOptionIds` | none | `decline` (src) | † | † | † |
| `EnvironmentOverrides` | none | `DISABLE_MCP_CONFIG_FILTERING=true`, `NO_BROWSER=1`, `INITIAL_AGENT_MODE=workspace-write` (src) | none † | none | none |
| `SessionPerRoom` | true | true (src: `resume` advertised) | true (S-A1: `resume` advertised) | † | † |
| Prompt blocks | as today | image, embedded context (src) | image, audio, embedded context (S-A1) | † | † |

### 7.3 What a vendor would need beyond this

If a spike shows a vendor that cannot be reached through these fields, the fix is a new field
with an ACP-only default (P9, P11), recorded as an amendment here, never a branch on
`profile.Id`.

---

## 8. Edge cases and failure modes

| # | Case | Behaviour | Severity |
| --- | --- | --- | --- |
| E-V1 | An Adapter ignores `_meta.systemPrompt` and the profile still says `MetaReplace` | The Agent answers with no Persona and no tool names. This is exactly Codex's behaviour (§2.4), which is why its preset uses `InstructionsFile`. An operator adding an unknown Adapter writes `"Preset": "None"`. | **high** |
| E-V2 | The tool-name format is wrong for the Adapter | The model reports that no such tool exists and burns a Turn (ADR-0014). Spikes record the real names first. | **high** |
| E-V3 | Codex shell commands need the network | Codex's workspace modes force network off every Turn (§2.4), so `git push`, `dotnet restore` and similar fail inside the agent. Huddle does not offer full access to work around it. The Agent's Turn reports the failure; the Engineering Guide says Codex Agents cannot reach the network from the shell until upstream PRs #608 / #368 land. | high, accepted |
| E-V4 | The Adapter does not advertise `mcpCapabilities.http` | The Persona goes `Degraded` naming the reason (§6.4). | medium |
| E-V5 | Not signed in | `Failed` with the profile's `LoginHint`. | medium |
| E-V6 | No usage reported | `TokenBudget` is inert; the per-Room Budget and `TurnBudget` still stop a loop. | medium |
| E-V7 | An unmarked instruction file exists when a Persona first starts on an `InstructionsFile` Adapter | The Persona goes `Degraded` naming the file; Huddle does not overwrite it. Once Huddle has written it, drift is overwritten and logged. | medium |
| E-V7b | The Agent or the Human edits the instruction file | Refused where Huddle can read the request; regenerated before the next session in every case. | **high** if unguarded |
| E-V7c | `DataDir` sits inside a git repository | Codex and Gemini also read that repository's instruction files. Startup warns. | medium |
| E-V7d | Two Rooms open sessions for one Persona at once | The per-Persona gate serialises the write; `AtomicFile` prevents a partial read. | medium |
| E-V8 | A vendor's ACP behaviour changes between versions | Fixtures go stale; the vendor's spike and phase 7 re-run on each pin bump in `tools/acp/agents.json`. | medium, expected |
| E-V9 | A non-Claude Agent never uses AskUserQuestion | Nothing breaks: `ask_human` is Huddle's own App Tool. | low |
| E-V10 | An extension method is used to detect a capability | Forbidden by P12. | **high** if violated |
| E-V11 | A `.cmd` shim is configured as `Command` on Windows | Startup validation rejects it, naming `NodePackageEntry`. | medium |
| E-V12 | The Persona changes Adapter | Model and Effort reset ([ADR-0015](adr/0015-model-and-effort-reset-when-the-adapter-changes.md)); a marked instruction file is deleted. | low |
| E-V13 | A preset default changes across Huddle upgrades | Noted in the Engineering Guide's change list. | low |
| E-V14 | A downloaded binary does not match its recorded SHA-256 | `install.ps1` refuses to unpack it. | medium |
| E-V15 | A `gemini` profile on a consumer Google sign-in | The CLI no longer serves consumer accounts; the login hint says so. | low |
| E-V16 | The user's `~/.codex/config.toml` defines an MCP server named `team` | Without `DISABLE_MCP_CONFIG_FILTERING`, Codex drops Huddle's server silently and every App Tool call fails. The preset sets it; S-C1 verifies. | **high** if unset |
| E-V17 | A Codex edit request carries its paths only in `locations` | Without request 3 (§6.8), every Codex edit bypasses the protected folders and the instruction-file guard. | **high** until request 3 lands |
| E-V18 | Codex's `agent` mode lets its own reviewer approve without asking Huddle | Process gates would not see those actions. The preset starts in `workspace-write` and hides `agent` until S-C2 settles Q-17. | high |
| E-V19 | Codex is installed without the VC++ redistributable on Windows | The adapter reports exit code 3221225781 as a missing redistributable; the Persona goes `Failed` with that text. | low |
| E-V20 | A Codex adapter older than 1.10.0 is on the machine | Its bundled Codex rejects newer models. `install.ps1` installs the pin; V2 readiness checks the floor. | medium |

---

## 9. Who changes what

| Change | Subtree | Owner |
| --- | --- | --- |
| `AdapterPreset`, `AdapterPresets`, the new `AdapterProfile` fields, legacy values, startup validation | `src/Huddle.App/Acp/` | chat |
| `ToolNamer`, `GetHelpTool`, `SystemPromptComposer`, `ReferenceExpander`, new goldens | `src/Huddle.App/` | chat |
| Writing the instruction file under a per-Persona gate; `EnvironmentJson` if confirmed | `src/Huddle.App/Acp/` | chat |
| First-prompt framing beside the first-Turn composition | `src/Huddle.App/Acp/Sessions/RoomSession.cs` | chat |
| Skipping the instruction file in the change scanner | `src/Huddle.App/FileChanges/` | chat |
| Per-profile `HiddenModes` and `PlanMode` | `WorkModePolicy`, `PlanModePermissionHandler` | chat |
| `ProtectedHomeFolders`, `CommandArgumentNames`, `PreferredRejectOptionIds`, path scanning over `Locations` and rooted strings, `InstallsClaudeHooks` | `src/Huddle.App/Acp/` | chat |
| `AcpOptions.TurnBudget`, `IRoomSessionOwner.AddTurn` | `src/Huddle.App/Acp/` | chat |
| The login hint in the failure text | `src/Huddle.App/` | chat |
| **Request 1:** `AgentHostInfo` records `mcpCapabilities.http` | `src/Huddle.Acp/DotAcp/DotAcpAgentHost.cs` | **acp** |
| **Request 2:** `TurnCompleted` carries the prompt response's `usage` (optional; no preset needs it yet) | `src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs`, `Abstractions/AgentEvent.cs` | **acp** |
| **Request 3:** `ToolCallInfo` carries the tool call's `locations` (optional list of paths) | `src/Huddle.Acp/Abstractions/PermissionRequestContext.cs`, `DotAcp/SessionUpdateMapper.cs` | **acp** |
| Kind emulation for `mock-acp` | `src/Huddle.MockAdapter/`, driving `FakeAcpAgent`'s existing hooks | chat; **acp** only if the fake needs a new hook (then verify both assemblies) |
| `tools/acp/agents.json`, `package.json`, `install.ps1` | `tools/acp/` | shared root, announced |

None of this changes `Huddle.Contracts` or the pipe protocol.

---

## 10. Test-first task plan

Tiers follow the [Adapters spec §15.1](Huddle.Adapters-Specifications.md#151-four-tiers-and-which-are-blocked):
Tier 1 unit, Tier 2 bUnit, Tier 3 conformance against `mock-acp`, Tier 4 live and paid.

### Phase 0 — spikes · Tier 4

The spikes in §11. A vendor's preset values are built only after its spike's fixture is
committed. A Tier 1 test asserts that each preset's values equal what its fixture recorded, so a
pin bump with a re-recorded fixture fails loudly where a default no longer matches.

### Phase 1 — profile fields and presets · Tier 1

- T-V1: no `Adapters` resolves one profile equal, field for field, to today's synthesised profile.
- T-V1b: every presetless shape valid today (a bare `Command`; `Command` + `Args`; `AdapterPath`;
  `UsesToolNamePrefix: false`) resolves to today's `AdapterProfile`, including
  `IsolateUserSettings: false` and no `Commands`, with the same command line.
- T-V2: `Preset = codex` with nothing else yields the preset's defaults; each configured field
  overrides exactly that field; `"Preset": "claude"` yields isolation and `["compact"]`.
- T-V3: `UsesToolNamePrefix: true/false` bind to the two legacy formats; setting it with
  `ToolNameFormat` throws.
- T-V4: a `ToolNameFormat` without `{tool}`, an `InstructionsFileName` with a separator, a
  `PlanMode` without `=`, and a `.cmd` `Command` each throw at startup.
- T-V4b: a `BinaryEntry` profile is located under `tools/acp/bin` and preferred over a
  `NodePackageEntry`; the `codex` preset locates `dist/index.js` under `tools/acp/node_modules`.

### Phase 2 — tool names and the system prompt · Tier 1

- T-V5: `systemPrompt.txt` is unchanged (P13); one new golden per new format.
- T-V6: `ReferenceExpander` uses the profile's format.
- T-V7: `InstructionsFile` writes the marked file before `session/new` **and** `session/resume`,
  and refuses to overwrite an unmarked file.
- T-V7b: a marked file edited between sessions is rewritten and logged; the change scanner reports
  nothing; two concurrent opens produce one complete file.
- T-V7c: `WorkDirPermissionHandler` refuses a write to the instruction file, whether the path is
  in `rawInput` or only in `Locations`.
- T-V8: `FirstPrompt` sends the framed block once per new session, never on a resumed one or a
  command Turn.
- T-V8b (only if S-C2 confirms `EnvironmentJson`): the composed prompt is merged as
  `developer_instructions` into a configured `CODEX_CONFIG`, and the configured keys survive.

### Phase 3 — modes, usage, protection · Tier 1

- T-V9: `HiddenModes` and `PlanMode` are read from the profile; Plan is set through
  `collaboration_mode` for a `codex` profile; the Plan Work Mode is not offered when `PlanMode`
  is null.
- T-V10: `TurnBudget` stops a Persona with no usage after its Turns across Rooms reach the limit;
  a command Turn does not count; a Human Message resets it.
- T-V10b: both Budgets count on a Claude-shaped Adapter; with `TurnBudget` at zero, today's
  behaviour is unchanged.
- T-V11: every configured profile's home folder is refused.
- T-V11b: for each recorded permission fixture, the handler finds the path or command; a Codex
  edit whose only path is in `Locations` and points at a protected folder is refused.
- T-V11c: an input holding only relative strings is approved; existing
  `ToolCall_WithNoRecognisablePath_IsApproved` cases stay green.
- T-V11d: with `PreferredRejectOptionIds: ["decline"]`, a refusal picks `decline` over an earlier
  `cancel`; with the id absent, the first `reject_once` is picked.
- T-V12: with `InstallsClaudeHooks` false, nothing is written under `<workDir>/.claude`.

### Phase 4 — the card · Tier 2

- T-V13: with four profiles, the Adapter select lists all four in order; switching resets Model and
  Effort.

### Phase 5 — sign-in failure · Tier 1

- T-V14: `AuthenticationRequired` from `session/new` puts the Persona in `Failed` with the
  profile's `DisplayName` and `LoginHint`.

### Phase 6 — kind emulation in `mock-acp` · Tier 3, in CI

`mock-acp` gains `--kind codex|antigravity|copilot|gemini`. Each kind emulates only what its
fixture recorded: whether it honours `_meta.systemPrompt`, its tool-name format, `usage_update`,
its modes and plan option, its permission shapes (Codex: edit paths only in `locations`, two
`reject_once` options). Tests are marked `portable`, `mock-only` or `split`, as the
[amendment to D-12](Huddle.Adapters-Specifications.md#amendment-to-d-12-2026-09-18) requires. The
mock **records and never validates** (P8).

### Phase 7 — live, per vendor · Tier 4, paid

`engineering/manual-tests/adapter-vendors.md`, written when phase 7 starts: per vendor, an Agent
posts in a Room, calls `get_help`, answers a Mention, asks the Human a question, is refused a
protected-folder edit, is stopped mid-Turn, and resumes in a second Room with its Persona intact.
Re-run on each pin bump.

### Sequencing

1. **Antigravity S-A2**: one signed-in run; the server is already downloaded and the probe
   written.
2. **Codex S-C1**: needs only `npm install` of the pin; no sign-in.
3. **Codex S-C2**: needs a Codex sign-in or an API key.
4. Phases 1, 2, 3 and 5 can start as soon as S-C1 and S-A2 have fixed their values; the generic
   parts (legacy values, `ToolNamer`, `TurnBudget`, path scanning) can start now.
5. Copilot's spike when Copilot is wanted. Phase 6 needs the fixtures; phase 7 is last.

---

## 11. Spikes: what has run and what must be verified

### 11.1 How a spike runs

A spike is a scripted ACP client that plays Huddle's role exactly, run by hand, never in CI. It:

- speaks ACP over stdio at protocol v1, advertising no `fs` or `terminal`;
- hosts a minimal HTTP MCP server named `team` with a bearer token and one tool, `get_help`,
  whose output contains a sentinel;
- answers `session/request_permission` the way Huddle does, refusing by kind;
- logs every frame in both directions to JSON Lines.

It runs in a scratch cwd that has no ancestor instruction files and is not inside a git
repository. The probe used for S-A1 is the reference implementation. Its output becomes a
fixture under `tests/Huddle.Tests.Agents/Fixtures/vendors/<adapter id>/`, holding:

- the raw `initialize` and `session/new` responses;
- the recorded permission requests;
- the tool names the model reported;
- the vendor version and the date.

Spikes that send a prompt use the Human's account or API key and spend quota. Each runs only
with the Human's go-ahead.

### 11.2 Antigravity

**S-A1, handshake: done 2026-10-19** (§2.3). It confirmed protocol v1, HTTP MCP, resume, the sign-in
signal and error text, and the home and settings paths.

**S-A2, signed in: owed.** Prerequisite: the Human picks a sign-in method once, either
`auth.type` in `~/.gemini/antigravity-acp/settings.json` or an API key, at the desk, because a
browser may open.

| # | Verify | How | Pass | Decides |
| --- | --- | --- | --- | --- |
| A2-1 | Whether `oauth-personal` reuses the stored Google sign-in without a browser (Q-14) | Start the server with `auth.type: oauth-personal` and no browser available; call `session/new` | A session id, no browser | Whether an unattended Agent can use a personal account, or the preset recommends an API key |
| A2-2 | `session/new` with HTTP MCP | Record the response | `configOptions` for model, effort and mode; `availableCommands` | `HiddenModes`, `PlanMode`, `Commands`, Tiers |
| A2-3 | System-prompt route | One prompt asking for every sentinel (`_meta` append, `AGENTS.md`, `GEMINI.md`) and where each came from | The `_meta` sentinel is reported | `MetaAppend` vs `InstructionsFile`, and which file name |
| A2-4 | Resume keeps the prompt | `session/resume`, then ask again | The `_meta` sentinel is still reported | That `MetaAppend` survives Huddle's resumes |
| A2-5 | Tool names | Ask the model to list its MCP tool names and call `get_help` | The `get_help` sentinel comes back; names recorded | `ToolNameFormat` |
| A2-6 | Usage | Record `session/update`s and the prompt response | `usage_update` present or not | `UsageSource` |
| A2-7 | How a Turn ends | Record `stopReason` and timing | Arrives in the prompt response at v1 | Confirms Buzz #7384 does not apply |
| A2-8 | Permission shapes | Ask for one file write and one shell command in the scratch cwd; refuse both | Requests recorded with `kind`, `rawInput`, `locations` and options | Path and command extraction; `PreferredRejectOptionIds` |
| A2-9 | Modes | Set each advertised mode; note which skip approval | Behaviour per mode recorded | `HiddenModes` |
| A2-10 | Cancel | Cancel mid-Turn | `stopReason: cancelled`; pending permission request withdrawn | Stop behaviour |

### 11.3 Codex

Codex is not installed on the development machine. Every Codex value in §7.2 is (src) until
these run.

**S-C1, install and handshake: no sign-in needed.** Prerequisite: Node 24 and, on Windows, the
VC++ redistributable. `npm install @agentclientprotocol/codex-acp@2.2.2` into a scratch
`tools/acp`, with the Human's go-ahead for the download.

| # | Verify | How | Pass | Decides |
| --- | --- | --- | --- | --- |
| C1-1 | Launch without a shim | `node <prefix>/node_modules/@agentclientprotocol/codex-acp/dist/index.js`, no `CODEX_PATH` | Process starts; `@openai/codex`'s Windows binary is resolved | `NodePackageEntry`, install script |
| C1-2 | `initialize` at v1 | Record the response | `protocolVersion: 1`, `mcpCapabilities.http: true`, `resume` and `close`, prompt capabilities | §2.4's capability row, `SessionPerRoom` |
| C1-3 | `authMethods` with `NO_BROWSER=1` | Record them with and without the variable | `chat-gpt` absent with it | The `NO_BROWSER` default |
| C1-4 | Signed-out `session/new` | Call it with no credentials | `-32000`, `authRequired` | The sign-in path (G-12) |
| C1-5 | Option categories | If a session can be opened with an API key, record `configOptions`; otherwise read them from C2-1 | `mode` and `collaboration_mode` have distinct categories | Q-15: whether Huddle's mode reader picks the right selector |
| C1-6 | MCP name filter | A scratch `CODEX_HOME` whose `config.toml` defines a server `team`; start a session with and without `DISABLE_MCP_CONFIG_FILTERING` | Huddle's server present only with the variable | E-V16 and the preset's environment |

**S-C2, signed in: needs a Codex sign-in or an `OPENAI_API_KEY`.**

| # | Verify | How | Pass | Decides |
| --- | --- | --- | --- | --- |
| C2-1 | `session/new` | Record `configOptions` and `availableCommands` | Model list, `reasoning_effort`, `mode`, `collaboration_mode`, `/compact` | Tiers, `Commands`, `PlanMode` |
| C2-2 | `_meta.systemPrompt` is ignored | Send a `_meta` sentinel; ask for it | Not reported | Confirms E-V1 for Codex |
| C2-3 | `AGENTS.md` is read, new and resumed | Sentinel in the cwd's `AGENTS.md`; ask, `session/resume`, ask again | Reported both times | `InstructionsFile` for Codex |
| C2-4 | `developer_instructions` through `CODEX_CONFIG` | Launch with `CODEX_CONFIG={"developer_instructions":"<sentinel>"}`; ask, resume, ask again | Reported both times | Whether `EnvironmentJson` is added and preferred over the file |
| C2-5 | Tool names | List MCP tool names; call `get_help` | `mcp__team__get_help`; sentinel returned | `ToolNameFormat` |
| C2-6 | App Tools under the sandbox | Call `get_help` in `workspace-write` | It succeeds | That loopback MCP is unaffected by the network block |
| C2-7 | Shell network is blocked | In `workspace-write`, ask it to run a command that reaches the network | It fails | Confirms E-V3 |
| C2-8 | Usage | Record `usage_update` and the prompt response | `usage_update {used, size}` arrives | `UsageSource: UsageUpdate` |
| C2-9 | Permission shapes | One file edit and one shell command, refused | Edit: `kind: edit`, paths only in `locations`; execute: `rawInput.command` a string; options include `decline` and `cancel` | Request 3, `CommandArgumentNames`, `PreferredRejectOptionIds` |
| C2-10 | Refusing with `decline` | Refuse with `decline`, then with `cancel` | `decline` continues the Turn; `cancel` ends it | Q-18 |
| C2-11 | What `agent` mode forwards | In `agent` mode, ask for a file edit inside the cwd and a `git push`-shaped command | Record which reach `request_permission` | Q-17: whether `agent` may be offered |
| C2-12 | `INITIAL_AGENT_MODE` | Launch with `workspace-write` | The session's current mode is `workspace-write` | The preset's environment |
| C2-13 | Cancel and close | Cancel mid-Turn; then `session/close` | `cancelled`; the process stays healthy | Stop behaviour |

### 11.4 Copilot and Gemini

Not run. Each gets the same two-step shape when wanted: handshake (Copilot needs `npx` and a
GitHub sign-in check; Gemini needs a paid API key), then a signed-in run covering the same rows
as A2-2 to A2-10.

---

## 12. Open questions

Checked on 2026-10-19:

- **Sources and registry:**
  - `codex-acp` 2.2.2 source (§2.4) and Buzz `main` (§2.1).
  - The ACP registry (§2.2).
  - Antigravity's server, on the wire (§2.3).
- **Gemini CLI:** `--acp` is the flag, and consumer sign-in stopped on 2026-06-18.
- **Copilot CLI:** `copilot --acp` is in public preview.

| # | Question | Decides | Answered by |
| --- | --- | --- | --- |
| Q-1 | Tool names over ACP. Codex: `mcp__team__…` (src). Antigravity, Copilot, Gemini: open. | `ToolNameFormat` | C2-5, A2-5 |
| Q-2 | Can Codex's App Tool calls reach loopback under `workspace-write`, given shell network is off? | Whether Codex App Tools work at all | C2-6 |
| Q-3 | Copilot's and Gemini's Windows entry files | `NodePackageEntry` | reading each package |
| Q-4 | Does any vendor expose Model only through unstable `models` / `session/set_model`? Codex: no, `configOptions` (src). | Buzz's fallback | C2-1, A2-2 |
| Q-5 | Which system-prompt route each honours, on new and resumed sessions | `SystemPromptDelivery` | C2-2 to C2-4, A2-3, A2-4 |
| Q-6 | `mcpCapabilities.http`. Antigravity: yes (S-A1). Codex: yes (src). | the V2 bridge | C1-2 |
| Q-7 | Mode ids and which skip approval. Codex: §2.4 (src). Antigravity: open. | `HiddenModes` | C2-1, A2-9 |
| Q-8 | Home folder and credentials. Antigravity: `~/.gemini` (S-A1). Codex: `~/.codex` (inferred). | `ProtectedHomeFolders` | C1-4, docs |
| Q-9 | Resume support. Antigravity: yes (S-A1). Codex: yes (src). | `SessionPerRoom` | C1-2 |
| Q-10 | Does any vendor need protocol v2? | nothing; Huddle sends v1 | not needed |
| Q-11 | Signed-out signal. Antigravity: `-32000` at `session/new` (S-A1). Codex: `authRequired` there (src). | D-V7 | C1-4 |
| Q-12 | Is Antigravity's server meant for third-party clients on a personal account, and on what terms? | Whether the preset recommends a personal account or an API key | Antigravity's terms |
| Q-13 | Is `dotacp.protocol.ProtocolMeta.Version`, sent at `DotAcpAgentHost.cs:98`, equal to 1? | Whether Buzz #7384's v2 problems can reach Huddle | reading the `dotacp` package |
| Q-14 | Does Antigravity's `oauth-personal` reuse the stored sign-in without a browser? | Unattended start | A2-1 |
| Q-15 | Do Codex's `mode` and `collaboration_mode` options have distinct categories? | Whether Huddle's mode reader needs to select by config id | C1-5, C2-1 |
| Q-16 | Does `developer_instructions` in `CODEX_CONFIG` reach the model on new and resumed threads? | `EnvironmentJson` | C2-4 |
| Q-17 | In Codex's `agent` mode, which actions reach `request_permission` and which does Codex's reviewer approve itself? | Whether `agent` can be offered without bypassing Huddle's gates | C2-11 |
| Q-18 | Does refusing with Codex's `decline` continue the Turn as the source says? | `PreferredRejectOptionIds` | C2-10 |

---

## 13. Decision log

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-V1 | **Extend the Adapter Profile; one factory still** | a class per vendor; a strategy per vendor | Buzz reached the same answer independently; each vendor difference so far is a value. |
| D-V2 | **An explicit `Preset` field** | infer the vendor from the command's basename | Huddle launches most Adapters as `node <entry>`. |
| D-V3 | **Presets are defaults, overridable field by field; no preset means today** | fixed bundles; absent meaning "ACP-only" | One line of configuration per vendor change; every profile configured today keeps working. |
| D-V4 | **The tool name is a format string** | a third `bool`; an enum | ADR-0014 predicted it. |
| D-V5 | **Deliver the prompt the way each Adapter takes it; regenerate and guard a file where one is used** | first prompt everywhere; `_meta` and hope; a read-only Library mode | `_meta.systemPrompt` is a Claude extension that Codex ignores. A first prompt is lost on resume. |
| D-V6 | **A per-Persona Turn Budget beside the token Budget** | a tokens-per-Turn constant; nothing | Needs nothing from the wire and says what it counts. |
| D-V7 | **No credentials in Huddle; a login hint instead** | ACP `authenticate`; keys in configuration | Each vendor's own sign-in keeps secrets out of Huddle. |
| D-V8 | **Spikes before any (src) or † value is built** | build from vendor docs or source reading | Review found one vendor-doc claim wrong, and source reading found Buzz's network fix no longer effective. Only the wire settles it. |
| D-V9 | **Antigravity is the Google Adapter; Gemini is secondary** | Gemini only; a community `agy` wrapper | Gemini CLI no longer serves consumer accounts; a wrapper risks the Human's account. |
| D-V10 | **Launch details pinned from the ACP registry, never fetched at run time** | hand-maintained commands; startup resolution | Reproducible, offline-safe installs, with checksums for binaries. |
| D-V11 | **Permission scanning reads `locations` and rooted strings, never resolves relative ones** | named arguments only; resolving every string | Codex edits carry paths only in `locations`; resolving relative strings causes false refusals. |
| D-V12 | **No use of `_meta.jetbrains.air.customInstructions`** | sending it as Codex's instruction key | It is another client's namespace, it is dropped on resume, and impersonating AIR could change other behaviour in the adapter. |
| D-V13 | **Codex starts in `workspace-write`, with `agent` hidden until Q-17 is settled** | Codex's default `agent` | Huddle's gates must see every action an Agent takes; `agent` lets Codex's own reviewer decide some. |
| D-V14 | **Accept that Codex Agents cannot reach the network from the shell** | offering `agent-full-access`; copying Buzz's `CODEX_CONFIG` override | Full access removes every approval; the override no longer works. Upstream is tracking a fix. |

### Candidate ADRs

- **The tool name is a format, not a flag.** Amends ADR-0014.
- **The system prompt is delivered the way the Adapter takes it.** Includes the generated,
  guarded instruction file and, if confirmed, the per-process environment route.
- **A Persona's unattended work is capped in Turns as well as tokens.**
- **A permission request's paths include its `locations`.** It changes what `Huddle.Acp` hands
  the App, and every guard depends on it.

---

## 14. Review record

**First review.** Six findings were applied: the Gemini tool-name claim; presetless profiles that
would have stopped launching; the instruction file exposed to the Library and the Agent; the
permission handler reading only Claude's shapes; two unnecessary ACP requests; and the Turn
Budget. Its minor findings were folded in later.

**Second review.** Judged the spec ready for phase 0. All eight findings were applied:
presetless resolution, rooted-only scanning, the `TurnBudget` counting point, dropping the
read-only Library mode, serialising the file write, the single-file scanner rule, the
first-prompt owner, and a line reference.

**After the reviews:**
- **Antigravity** replaced Gemini as the Google Adapter (D-V9), and launch details moved to the
  ACP registry (D-V10).
- **S-A1** ran the Antigravity handshake (§2.3).
- **Codex** was specified from Buzz and the `codex-acp` source (§2.1, §2.4). That work:
  - corrected G-14: Codex commands are strings, and edit paths are only in `locations`;
  - added G-15, request 3, `PlanMode`, `PreferredRejectOptionIds`, and the Codex environment
    defaults;
  - recorded that Buzz's network override no longer works.

None of these changes has been reviewed.
